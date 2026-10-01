from typing import Protocol

from models import Pose, Command


class ControlAlgorithm(Protocol):
    def compute(self, poses: dict[int, Pose]) -> list[tuple[int, Command]]: ...
