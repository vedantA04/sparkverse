from models import Pose


def _parse_pose(text: str) -> tuple[int, Pose]:
    robot_id_str, x_str, y_str, rot_str = text.strip().split()
    return int(robot_id_str), Pose(
        x=float(x_str), y=float(y_str), rotation=float(rot_str)
    )


def parse_poses(message: str) -> list[tuple[int, Pose]]:
    """Parse an MQTT pose message into a list of (robot_id, Pose).

    Message is comma-separated per robot, space-separated per element:
    `robot_id x y rot`, e.g. `1001 3.2 1.1 57.5,1002 3.0 2.1 180.1`.
    """
    poses: list[tuple[int, Pose]] = []
    for part in message.split(","):
        if part.strip():
            poses.append(_parse_pose(part))
    return poses


def serialize_poses(poses: list[tuple[int, Pose]]) -> str:
    """Serialize a list of (robot_id, Pose) into an MQTT message.

    Message is comma-separated per robot, space-separated per element:
    `robot_id x y rot`, e.g. `1001 3.2 1.1 57.5,1002 3.0 2.1 180.1`.
    """
    return ",".join(f"{rid} {p.x} {p.y} {p.rotation}" for rid, p in poses)
