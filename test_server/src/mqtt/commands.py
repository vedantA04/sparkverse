from models import Command, DriveCommand, TurnCommand


def to_mqtt_message(cmd: Command) -> str:
    if isinstance(cmd, DriveCommand):
        direction = "forward" if cmd.speed >= 0 else "reverse"
        speed_int = round(abs(cmd.speed) * 63)
        return f"drive {direction} {speed_int} {cmd.duration_ms}"
    if isinstance(cmd, TurnCommand):
        direction = "right" if cmd.speed >= 0 else "left"
        speed_int = round(abs(cmd.speed) * 63)
        return f"turn {direction} {speed_int} {cmd.duration_ms}"
    raise TypeError(f"Unknown command type: {type(cmd)}")
