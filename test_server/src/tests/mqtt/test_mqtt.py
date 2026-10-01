from models import DriveCommand, Pose, TurnCommand
from mqtt.commands import to_mqtt_message
from mqtt.poses import parse_poses, serialize_poses


class TestDriveCommandToMqttString:
    def test_drive_zero_speed(self):
        cmd = DriveCommand(speed=0.0, duration_ms=500)
        assert to_mqtt_message(cmd) == "drive forward 0 500"

    def test_drive_half_speed(self):
        cmd = DriveCommand(speed=0.5, duration_ms=1000)
        assert to_mqtt_message(cmd) == "drive forward 32 1000"

    def test_drive_full_speed(self):
        cmd = DriveCommand(speed=1.0, duration_ms=2000)
        assert to_mqtt_message(cmd) == "drive forward 63 2000"

    def test_drive_reverse(self):
        cmd = DriveCommand(speed=-1.0, duration_ms=2000)
        assert to_mqtt_message(cmd) == "drive reverse 63 2000"

    def test_drive_arbitrary(self):
        cmd = DriveCommand(speed=0.333, duration_ms=750)
        assert to_mqtt_message(cmd) == "drive forward 21 750"

    def test_drive_zero_duration(self):
        cmd = DriveCommand(speed=0.8, duration_ms=0)
        assert to_mqtt_message(cmd) == "drive forward 50 0"


class TestTurnCommandToMqttString:
    def test_turn_zero_speed(self):
        cmd = TurnCommand(speed=0.0, duration_ms=300)
        assert to_mqtt_message(cmd) == "turn right 0 300"

    def test_turn_half_speed(self):
        cmd = TurnCommand(speed=0.5, duration_ms=1500)
        assert to_mqtt_message(cmd) == "turn right 32 1500"

    def test_turn_full_speed(self):
        cmd = TurnCommand(speed=1.0, duration_ms=100)
        assert to_mqtt_message(cmd) == "turn right 63 100"

    def test_turn_left(self):
        cmd = TurnCommand(speed=-1.0, duration_ms=100)
        assert to_mqtt_message(cmd) == "turn left 63 100"


class TestParsePoses:
    def test_single_pose(self):
        poses = parse_poses("1001 3.2 1.1 57.5")
        assert poses == [(1001, Pose(x=3.2, y=1.1, rotation=57.5))]

    def test_multiple_poses(self):
        poses = parse_poses("1001 3.2 1.1 57.5,1002 3.0 2.1 180.1")
        assert poses == [
            (1001, Pose(x=3.2, y=1.1, rotation=57.5)),
            (1002, Pose(x=3.0, y=2.1, rotation=180.1)),
        ]

    def test_negative_and_decimal_values(self):
        poses = parse_poses("1001 -3.5 -2.0 -45.0")
        assert poses == [(1001, Pose(x=-3.5, y=-2.0, rotation=-45.0))]

    def test_trailing_comma(self):
        poses = parse_poses("1001 1.0 2.0 3.0,")
        assert poses == [(1001, Pose(x=1.0, y=2.0, rotation=3.0))]


class TestSerializePoses:
    def test_single_pose(self):
        poses = [(1001, Pose(x=3.2, y=1.1, rotation=57.5))]
        assert serialize_poses(poses) == "1001 3.2 1.1 57.5"

    def test_multiple_poses(self):
        poses = [
            (1001, Pose(x=3.2, y=1.1, rotation=57.5)),
            (1002, Pose(x=3.0, y=2.1, rotation=180.1)),
        ]
        assert serialize_poses(poses) == "1001 3.2 1.1 57.5,1002 3.0 2.1 180.1"

    def test_negative_values(self):
        poses = [(1001, Pose(x=-3.5, y=-2.0, rotation=-45.0))]
        assert serialize_poses(poses) == "1001 -3.5 -2.0 -45.0"

    def test_round_trip(self):
        poses = [
            (1001, Pose(x=3.2, y=1.1, rotation=57.5)),
            (1002, Pose(x=3.0, y=2.1, rotation=180.1)),
        ]
        assert parse_poses(serialize_poses(poses)) == poses
