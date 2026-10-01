import logging
import math

from control_algs.control_algorithm import ControlAlgorithm
from models import Pose, Command, TurnCommand, DriveCommand


logger = logging.getLogger(__name__)


class ProportionalWaypointFollowingControl(ControlAlgorithm):
    """A proportional-only waypoint-following control algorithm.

    Each robot follows a cyclic list of waypoints. When a robot reaches a
    waypoint within a distance threshold, it advances to the next one.
    """

    def __init__(
        self,
        waypoints: list[tuple[float, float]],
        location_error_multiplier: float = 1.0,
        location_error_threshold: float = 0.1,
        heading_error_multiplier: float = 1.0,
        heading_error_threshold_deg: float = 5.0,
        min_drive_speed: float = 0.5,
        min_turn_speed: float = 0.4,
        cmd_duration_ms: int = 200,
    ) -> None:
        self._waypoints = waypoints
        self._location_error_multiplier = location_error_multiplier
        self._location_error_threshold = location_error_threshold
        self._heading_error_multiplier = heading_error_multiplier
        self._heading_error_threshold_deg = heading_error_threshold_deg
        self._min_drive_speed = min_drive_speed
        self._min_turn_speed = min_turn_speed
        self._cmd_duration_ms = cmd_duration_ms

        # robot id: next waypoint index
        self._waypoint_index: dict[int, int] = {}

    def current_waypoint(self, robot_id: int):
        """The current waypoint the robot is heading towards"""
        idx = self._waypoint_index[robot_id]
        return self._waypoints[idx]

    def _next_waypoint(
        self, robot_id: int, robot_x: float, robot_y: float
    ) -> tuple[float, float]:

        # if no waypoint is set, set to closest
        if robot_id not in self._waypoint_index:
            closest_idx = min(
                range(len(self._waypoints)),
                key=lambda i: math.hypot(
                    self._waypoints[i][0] - robot_x, self._waypoints[i][1] - robot_y
                ),
            )
            self._waypoint_index[robot_id] = closest_idx

        idx = self._waypoint_index[robot_id]

        return self._waypoints[idx]

    def _desired_heading(self, x: float, y: float, wx: float, wy: float) -> float:
        """Angle in degrees from (x, y) toward (wx, wy), 0 = north."""
        dx = wx - x
        dy = wy - y
        return math.degrees(math.atan2(dx, dy))

    @staticmethod
    def _normalise_180(degrees: float) -> float:
        """Constrain an angle to the range [-180, 180] degrees."""
        while degrees > 180:
            degrees -= 360
        while degrees < -180:
            degrees += 360
        return degrees

    def compute(self, poses: dict[int, Pose]) -> list[tuple[int, Command]]:
        commands: list[tuple[int, Command]] = []

        for robot_id, pose in poses.items():
            wx, wy = self._next_waypoint(robot_id, pose.x, pose.y)

            desired_heading = self._desired_heading(pose.x, pose.y, wx, wy)
            heading_error_deg = self._normalise_180(desired_heading - pose.rotation)
            location_error = math.hypot(wx - pose.x, wy - pose.y)

            logger.debug(f"pose: {pose}")
            logger.debug(
                f"next wp at ({wx:.1f}, {wy:.1f}), heading {desired_heading:.0f}"
            )
            logger.debug(
                f"loc err: {location_error:0.2f}, head err: {heading_error_deg:.0f}"
            )

            if abs(heading_error_deg) > self._heading_error_threshold_deg:
                control = self._heading_error_multiplier * heading_error_deg / 180.0
                if abs(control) < self._min_turn_speed:
                    control = (
                        -self._min_turn_speed if control < 0 else self._min_turn_speed
                    )
                speed = clamp(control, -1.0, 1.0)
                commands.append(
                    (
                        robot_id,
                        TurnCommand(speed=speed, duration_ms=self._cmd_duration_ms),
                    )
                )

            elif location_error > self._location_error_threshold:
                control = self._location_error_multiplier * location_error
                logger.debug(
                    f"{self._location_error_multiplier} x {location_error} = {control}"
                )
                if abs(control) < self._min_drive_speed:
                    control = (
                        -self._min_drive_speed if control < 0 else self._min_drive_speed
                    )
                speed = clamp(control, -1.0, 1.0)
                commands.append(
                    (
                        robot_id,
                        DriveCommand(speed=speed, duration_ms=self._cmd_duration_ms),
                    )
                )

            else:
                # Reached the waypoint – advance to the next one (cyclic)
                idx = self._waypoint_index.get(robot_id, 0)
                self._waypoint_index[robot_id] = (idx + 1) % len(self._waypoints)

        return commands


def clamp(value, lo, hi):
    return max(lo, min(value, hi))
