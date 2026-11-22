"""Boids flocking for two-wheeled robots.

Cohesion, separation and alignment, plus a cruise term that keeps the flock
travelling, and arena edge avoidance. Collision avoidance has three layers:

1. Separation grows sharply as a neighbor approaches the minimum clearance
   distance (two body radii).
2. Avoidance has priority: flocking terms are scaled down while a robot is
   being pushed away, and cohesion fades out once a robot is within
   comfortable spacing of the flock center.
3. Forward speed is limited by the clearance to the nearest robot (or arena
   edge) in the robot's path, and a robot that is already too close backs off.
"""

import time
from collections.abc import Callable
from dataclasses import dataclass

from control_algs.control_algorithm import ControlAlgorithm
from control_algs.geometry import (
    Arena,
    EAST,
    NORTH,
    ZERO,
    Vec2,
    clamp,
    clamp01,
    degrees2vec,
    random_unit_vector,
    norm180,
)
from models import Command, Pose, WheelSpeedsCommand


@dataclass(frozen=True)
class BoidsConfig:
    # final wheel inputs are scaled by this factor
    scale_wheel_input: float = 1.0
    body_radius: float = 0.15
    separation_radius: float = 0.6
    separation_weight: float = 1.5
    cohesion_radius: float = 1.1
    cohesion_weight: float = 1.0
    alignment_radius: float = 1.0
    alignment_weight: float = 0.6
    cruise_weight: float = 0.8
    edge_margin: float = 1.2
    edge_weight: float = 1.0
    # Heading error at or above which a robot turns in place at full input, and
    # below which forward motion blends in.
    turn_in_place_angle_deg: float = 45.0
    # Steering magnitude below which the robot holds still, so a settled flock
    # doesn't wiggle in place on tiny force imbalances.
    deadband_steering: float = 0.1
    # Neighbor speeds below this (m/s) are treated as stationary for alignment.
    min_alignment_speed: float = 0.02
    # Drive input used to back away from a neighbor that is too close ahead.
    back_off_input: float = 0.3
    # How much the direction to the arena center (vs. the robot's own heading)
    # decides which way a robot turns along an edge.
    edge_turn_center_bias: float = 0.5
    cmd_duration_ms: int = 200

    @property
    def min_distance(self) -> float:
        """Center-to-center distance at which two robots are too close."""
        return 2.0 * self.body_radius


@dataclass(frozen=True)
class _TrackedBoid:
    """Used for velocity -> alignment calculation"""

    position: Vec2
    changed_at: float
    velocity: Vec2


def _track(previous: _TrackedBoid | None, position: Vec2, now: float) -> _TrackedBoid:
    if previous is None:
        return _TrackedBoid(position=position, changed_at=now, velocity=ZERO)
    if position == previous.position:
        return previous
    dt = now - previous.changed_at
    velocity = (position - previous.position) / dt if dt > 1e-4 else previous.velocity
    return _TrackedBoid(position=position, changed_at=now, velocity=velocity)


class BoidsControl(ControlAlgorithm):
    def __init__(
        self,
        arena: Arena,
        config: BoidsConfig = BoidsConfig(),
        clock: Callable[[], float] = time.monotonic,
    ) -> None:
        if config.separation_radius <= config.min_distance:
            raise ValueError(
                f"separation_radius ({config.separation_radius}) must be larger "
                f"than 2 * body_radius ({config.min_distance})"
            )
        self._arena = arena
        self._cfg = config
        self._clock = clock
        self._tracked: dict[int, _TrackedBoid] = {}

    def compute(self, poses: dict[int, Pose]) -> list[tuple[int, Command]]:
        now = self._clock()
        positions = {id_: Vec2(p.x, p.y) for id_, p in poses.items()}
        self._tracked = {
            id_: _track(self._tracked.get(id_), position, now)
            for id_, position in positions.items()
        }
        velocities = {id_: track.velocity for id_, track in self._tracked.items()}

        commands: list[tuple[int, Command]] = []
        for robot_id, pose in poses.items():
            command = self._command(
                robot_id, degrees2vec(pose.rotation), positions, velocities
            )
            commands.append((robot_id, command))
        return commands

    def _command(
        self,
        robot_id: int,
        forward: Vec2,
        positions: dict[int, Vec2],
        velocities: dict[int, Vec2],
    ) -> WheelSpeedsCommand:
        cfg = self._cfg
        min_distance = cfg.min_distance
        position = positions[robot_id]
        others = {id_: p for id_, p in positions.items() if id_ != robot_id}

        steering = self._steering(position, forward, others, velocities)
        ahead_clear, behind_clear = self._clearance(position, forward, others)
        too_close_ahead = ahead_clear < min_distance
        can_back_off = behind_clear > min_distance

        if steering.length() < cfg.deadband_steering and not too_close_ahead:
            return self._to_cmd(0.0, 0.0)

        angle = (
            norm180(steering.heading() - forward.heading()) if steering != ZERO else 0.0
        )
        turn_input = clamp(angle / cfg.turn_in_place_angle_deg, -1.0, 1.0)

        if too_close_ahead and can_back_off:
            forward_input = -cfg.back_off_input
        elif too_close_ahead or abs(angle) >= cfg.turn_in_place_angle_deg:
            forward_input = 0.0
        else:
            alignment_factor = 1.0 - abs(angle) / cfg.turn_in_place_angle_deg
            clearance_factor = clamp01(
                (ahead_clear - min_distance) / (cfg.separation_radius - min_distance)
            )
            forward_input = (
                clamp01(steering.length()) * alignment_factor * clearance_factor
            )

        left = (forward_input + turn_input) * cfg.scale_wheel_input
        right = (forward_input - turn_input) * cfg.scale_wheel_input
        return self._to_cmd(left, right)

    def _to_cmd(self, left: float, right: float) -> WheelSpeedsCommand:
        """Command wheel inputs, scaled down so the stronger wheel stays at or
        below full input"""
        strongest = max(abs(left), abs(right), 1.0)
        return WheelSpeedsCommand(
            left=left / strongest,
            right=right / strongest,
            duration_ms=self._cfg.cmd_duration_ms,
        )

    def _steering(
        self,
        position: Vec2,
        forward: Vec2,
        others: dict[int, Vec2],
        velocities: dict[int, Vec2],
    ) -> Vec2:
        """Desired direction of travel, with magnitude roughly in [0, 1+]
        meaning how urgently the robot wants to move."""
        cfg = self._cfg
        separation = ZERO
        center_sum, cohesion_count = ZERO, 0
        velocity_sum, alignment_count = ZERO, 0

        for other_id, other_pos in others.items():
            away = position - other_pos
            distance = away.length()

            if distance < cfg.separation_radius:
                # 0 at the separation radius, 1 at the minimum distance,
                # growing quadratically beyond that.
                t = (cfg.separation_radius - distance) / (
                    cfg.separation_radius - cfg.min_distance
                )
                direction = away / distance if distance > 1e-4 else random_unit_vector()
                separation += direction * (t * t)

            if distance < cfg.cohesion_radius:
                center_sum += other_pos
                cohesion_count += 1

            if distance < cfg.alignment_radius:
                velocity_sum += velocities[other_id]
                alignment_count += 1

        avoidance = separation * cfg.separation_weight + self._edge_avoidance(
            position, forward
        )

        cohesion = ZERO
        if cohesion_count > 0:
            to_center = center_sum / cohesion_count - position
            # Fade out within comfortable spacing of the center, so robots
            # gather loosely instead of all converging on one point.
            strength = clamp01(
                (to_center.length() - cfg.separation_radius) / cfg.separation_radius
            )
            cohesion = to_center.normalized() * (strength * cfg.cohesion_weight)

        alignment = ZERO
        if alignment_count > 0:
            avg_velocity = velocity_sum / alignment_count
            if avg_velocity.length() > cfg.min_alignment_speed:
                alignment = avg_velocity.normalized() * cfg.alignment_weight

        cruise = forward * cfg.cruise_weight

        flock_scale = 1.0 - clamp01(avoidance.length())
        flocking = (cohesion + alignment + cruise).clamp_length(1.0) * flock_scale
        return avoidance + flocking

    def _edge_avoidance(self, position: Vec2, forward: Vec2) -> Vec2:
        """Push away from arena edges closer than edge_margin, plus an equal
        push along them in the direction the robot already leans, so it turns
        instead of stalling against the cruise term."""
        arena = self._arena
        inward = (
            EAST * self._edge_strength(position.x - arena.min_x)
            - EAST * self._edge_strength(arena.max_x - position.x)
            + NORTH * self._edge_strength(position.y - arena.min_y)
            - NORTH * self._edge_strength(arena.max_y - position.y)
        )
        strength = inward.length()
        if strength < 1e-4:
            return ZERO

        along = (inward / strength).rot90()
        to_center = (arena.center - position).normalized()
        lean = forward.dot(along) + self._cfg.edge_turn_center_bias * to_center.dot(
            along
        )
        if lean < 0.0:
            along = -along

        return (inward + along * strength) * self._cfg.edge_weight

    def _edge_strength(self, distance_to_edge: float) -> float:
        cfg = self._cfg
        span = cfg.edge_margin - cfg.body_radius
        if distance_to_edge >= cfg.edge_margin or span <= 0.0:
            return 0.0
        t = (cfg.edge_margin - distance_to_edge) / span
        return t * t

    def _clearance(
        self, position: Vec2, forward: Vec2, others: dict[int, Vec2]
    ) -> tuple[float, float]:
        """Center-to-center distance to the nearest robot in a corridor
        directly ahead of and behind the robot. The arena edge counts as an
        obstacle on the same scale (a robot touching the edge reports a gap
        of min_distance)."""
        body_radius = self._cfg.body_radius
        ahead = self._arena.distance_to_edge(position, forward) + body_radius
        behind = self._arena.distance_to_edge(position, -forward) + body_radius
        right = forward.rot90()
        corridor_half_width = self._cfg.min_distance

        for other_pos in others.values():
            rel = other_pos - position
            if abs(rel.dot(right)) > corridor_half_width:
                continue
            if rel.dot(forward) > 0.0:
                ahead = min(ahead, rel.length())
            else:
                behind = min(behind, rel.length())

        return ahead, behind
