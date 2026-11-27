import pytest

from control_algs.boids import BoidsConfig, BoidsControl
from control_algs.geometry import Arena
from models import Pose, WheelSpeedsCommand

ARENA = Arena.from_origin_and_size(origin=(0.0, 0.0), size=(3.0, 3.0))
NORTH, EAST, SOUTH, WEST = 0.0, 90.0, 180.0, 270.0


def default_config():
    return BoidsConfig()


def build_controller(config: BoidsConfig = default_config()) -> BoidsControl:
    return BoidsControl(arena=ARENA, config=config)


def command_for(commands, robot_id: int) -> WheelSpeedsCommand:
    command = dict(commands)[robot_id]
    assert isinstance(command, WheelSpeedsCommand)
    return command


def forward_of(command: WheelSpeedsCommand) -> float:
    return (command.left + command.right) / 2


def turn_of(command: WheelSpeedsCommand) -> float:
    """Positive = turning right (clockwise)."""
    return (command.left - command.right) / 2


class TestCompute:
    def test_empty_poses_returns_empty_list(self):
        assert build_controller().compute({}) == []

    def test_lone_robot_cruises_straight_ahead(self):
        ctrl = build_controller()
        commands = ctrl.compute({1: Pose(x=0.0, y=0.0, rotation=NORTH)})
        command = command_for(commands, 1)
        assert forward_of(command) > 0
        assert turn_of(command) == pytest.approx(0, abs=1e-9)
        assert command.duration_ms == default_config().cmd_duration_ms

    def test_lone_robot_without_cruise_is_commanded_to_stop(self):
        config = BoidsConfig(cruise_weight=0.0)
        ctrl = build_controller(config)
        commands = ctrl.compute({1: Pose(x=0.0, y=0.0, rotation=NORTH)})
        assert command_for(commands, 1) == WheelSpeedsCommand(
            left=0.0, right=0.0, duration_ms=config.cmd_duration_ms
        )

    def test_scale_wheel_input_scales_commands(self):
        pose = {1: Pose(x=0.0, y=0.0, rotation=NORTH)}
        full = command_for(build_controller().compute(pose), 1)
        half = command_for(
            build_controller(BoidsConfig(scale_wheel_input=0.5)).compute(pose), 1
        )
        assert forward_of(half) == pytest.approx(forward_of(full) / 2)

    def test_backs_off_from_neighbor_too_close_ahead(self):
        poses = {
            1: Pose(x=0.0, y=0.0, rotation=NORTH),
            2: Pose(x=0.0, y=0.2, rotation=SOUTH),
        }
        commands = build_controller().compute(poses)
        for robot_id in poses:
            assert forward_of(command_for(commands, robot_id)) < 0

    def test_drives_away_from_neighbor_close_behind(self):
        poses = {
            1: Pose(x=0.0, y=0.0, rotation=NORTH),
            2: Pose(x=0.0, y=-0.4, rotation=NORTH),
        }
        assert forward_of(command_for(build_controller().compute(poses), 1)) > 0

    def test_turns_away_from_arena_edge(self):
        commands = build_controller().compute(
            {1: Pose(x=ARENA.max_x - 0.2, y=0.5, rotation=EAST)}
        )
        command = command_for(commands, 1)
        assert abs(turn_of(command)) > abs(forward_of(command))

    def test_rejects_separation_radius_inside_min_distance(self):
        with pytest.raises(ValueError):
            build_controller(BoidsConfig(body_radius=0.3, separation_radius=0.6))
