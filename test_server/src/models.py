from dataclasses import dataclass


@dataclass(frozen=True)
class Pose:
    x: float
    y: float
    rotation: float  # degrees, 0 = north / direction (0, 1)


class Command:
    pass


@dataclass(frozen=True)
class DriveCommand(Command):
    action: str = "drive"
    duration_ms: int = 0

    # 1.0 = full speed forward
    # -1.0 = full speed reverse
    speed: float = 0.0


@dataclass(frozen=True)
class TurnCommand(Command):
    action: str = "turn"
    duration_ms: int = 0

    # 1.0 = full speed right
    # -1.0 = full speed left
    speed: float = 0.0


@dataclass(frozen=True)
class ControlConfig:
    loop_hz: float = 5.0
    broker_host: str = "localhost"
    broker_port: int = 1883
    qos: int = 1
    client_id: str = "test-server"
