import threading

from models import Pose


class StateStore:
    """A thread-safe store for simulation states"""

    def __init__(self) -> None:
        self._lock = threading.Lock()
        self._poses: dict[int, Pose] = {}

    def update(self, poses: list[tuple[int, Pose]]) -> None:
        with self._lock:
            self._poses = {i: pose for i, pose in poses}

    def snapshot(self) -> dict[int, Pose]:
        """Get a copy of all current robot poses"""
        with self._lock:
            return dict(self._poses)
