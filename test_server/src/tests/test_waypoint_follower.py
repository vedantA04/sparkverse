import pytest

from control_algs.prop_waypoints import ProportionalWaypointFollowingControl
from models import DriveCommand, Pose, TurnCommand


class TestCompute:
    @pytest.mark.parametrize(
        "multiplier, heading_error, expected_speed",
        [
            (1.0, 0.0, 0.0),
            (1.0, 5.0, 0.0),  # at threshold, no drive
            (1.0, 18, 0.1),
            (1.0, 180, 1.0),
            (2.0, 18, 0.2),
            (2.0, 90, 1.0),
            (2.0, 180, 1.0),  # clamped to 1.0
        ],
    )
    def test_turn_speed_multiplier(self, multiplier, heading_error, expected_speed):
        # waypoint is due north (0 degrees)
        ctrl = ProportionalWaypointFollowingControl(
            waypoints=[(0.0, 0.1)],
            heading_error_multiplier=multiplier,
            heading_error_threshold_deg=5.0,
            min_drive_speed=0.0,
            min_turn_speed=0.0,
        )
        robot_id = 1
        poses = {robot_id: Pose(x=0.0, y=0.0, rotation=-heading_error)}
        actual_commands = ctrl.compute(poses)
        if heading_error <= 5.0:
            assert len(actual_commands) == 0
        else:
            assert len(actual_commands) == 1
            cmd_id, actual_cmd = actual_commands[0]
            assert cmd_id == robot_id
            assert isinstance(actual_cmd, TurnCommand)
            assert actual_cmd.speed == expected_speed

    @pytest.mark.parametrize(
        "multiplier, distance, expected_speed",
        [
            (1.0, 0.0, 0.0),
            (1.0, 0.1, 0.0),  # at threshold, no drive
            (1.0, 0.2, 0.2),
            (1.0, 1.0, 1.0),
            (2.0, 0.2, 0.4),
            (2.0, 0.5, 1.0),
            (2.0, 1.0, 1.0),  # clamped to 1.0
        ],
    )
    def test_drive_speed_multiplier(self, multiplier, distance, expected_speed):
        """Expected speed is multiplier * distance from waypoint"""
        ctrl = ProportionalWaypointFollowingControl(
            waypoints=[(0.0, distance)],
            location_error_multiplier=multiplier,
            location_error_threshold=0.1,
            min_drive_speed=0.0,
            min_turn_speed=0.0,
        )
        robot_id = 1
        poses = {robot_id: Pose(x=0.0, y=0.0, rotation=0.0)}
        commands = ctrl.compute(poses)
        if expected_speed == 0:
            assert len(commands) == 0
        else:
            assert len(commands) == 1
            _, command = commands[0]
            assert isinstance(command, DriveCommand)
            assert command.speed == pytest.approx(expected_speed)

    def test_robot_waypoints_initialized_to_closest(self):
        """Each robot independently gets its own closest waypoint."""
        ctrl = ProportionalWaypointFollowingControl(
            waypoints=[(0.0, 0.0), (10.0, 10.0), (-5.0, -5.0)]
        )
        poses = {
            1: Pose(x=9.0, y=9.0, rotation=0.0),  # closest to (10, 10)
            2: Pose(x=-4.0, y=-4.0, rotation=0.0),  # closest to (-5, -5)
        }
        ctrl.compute(poses)
        assert ctrl.current_waypoint(1) == (10, 10)
        assert ctrl.current_waypoint(2) == (-5, -5)

    def test_advance_waypoint_when_reached(self):
        ctrl = ProportionalWaypointFollowingControl(
            waypoints=[(0.0, 0.0), (10.0, 10.0)]
        )
        robot_id = 1
        poses = {robot_id: Pose(x=0.0, y=0.0, rotation=0.0)}
        commands = ctrl.compute(poses)
        assert len(commands) == 0
        assert ctrl.current_waypoint(robot_id) == (10.0, 10.0)

    def test_next_waypoint_is_first_when_at_last_waypoint(self):
        ctrl = ProportionalWaypointFollowingControl(
            waypoints=[(0.0, 0.0), (10.0, 10.0)]
        )
        robot_id = 1
        # hack: set internal waypoint index to the last index
        ctrl._waypoint_index[robot_id] = 1
        poses = {robot_id: Pose(x=10.0, y=10.0, rotation=0.0)}
        commands = ctrl.compute(poses)
        assert len(commands) == 0
        assert ctrl.current_waypoint(robot_id) == (0.0, 0.0)

    def test_multiple_robots(self):
        ctrl = ProportionalWaypointFollowingControl(waypoints=[(0.0, 5.0)])
        poses = {
            1: Pose(x=0.0, y=0.0, rotation=180.0),
            2: Pose(x=0.0, y=0.0, rotation=0.0),
        }
        commands = ctrl.compute(poses)
        assert len(commands) == 2

        cmd_map = dict(commands)
        assert 1 in cmd_map
        assert isinstance(cmd_map[1], TurnCommand)
        assert 2 in cmd_map
        assert isinstance(cmd_map[2], DriveCommand)

    def test_robot_reaches_waypoint_while_another_drives(self):
        """One robot reaches its waypoint (advances, no command), another
        still needs to drive."""
        ctrl = ProportionalWaypointFollowingControl(waypoints=[(0.0, 0.0), (5.0, 5.0)])
        poses = {
            1: Pose(x=0.0, y=0.0, rotation=0.0),  # at waypoint
            2: Pose(x=0.0, y=0.0, rotation=0.0),  # also at waypoint, both advance
        }
        commands = ctrl.compute(poses)
        assert len(commands) == 0
        assert ctrl.current_waypoint(1) == (5.0, 5.0)
        assert ctrl.current_waypoint(2) == (5.0, 5.0)

        # Second compute: both now have waypoint (5,5) and need to drive.
        # Point robots toward (5,5) (heading = 45°) so they drive instead of turn.
        poses = {
            1: Pose(x=0.0, y=0.0, rotation=45.0),
            2: Pose(x=0.0, y=0.0, rotation=45.0),
        }
        commands = ctrl.compute(poses)
        assert len(commands) == 2
        for _, cmd in commands:
            assert isinstance(cmd, DriveCommand)

    def test_empty_poses_returns_empty_list(self):
        ctrl = ProportionalWaypointFollowingControl(waypoints=[(0.0, 0.0)])
        assert ctrl.compute({}) == []
