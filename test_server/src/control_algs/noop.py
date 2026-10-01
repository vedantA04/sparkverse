from control_algs.control_algorithm import ControlAlgorithm
from models import Pose, Command


class NoOpControl(ControlAlgorithm):
    """A control algorithm that never produces any commands."""

    def compute(self, poses: dict[int, Pose]) -> list[tuple[int, Command]]:
        return []
