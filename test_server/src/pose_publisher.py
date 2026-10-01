import logging
import random
import threading

from models import Pose
from mqtt.bridge import MQTTBridge

logger = logging.getLogger(__name__)


def gen_random_poses(n: int) -> list[tuple[int, Pose]]:
    return [
        (
            1000 + i,
            Pose(x=random.random(), y=random.random(), rotation=random.random() * 360),
        )
        for i in range(n)
    ]


class PosePublisher:
    def __init__(
        self,
        mqtt: MQTTBridge,
        frequency_hz: float,
        num_poses: int,
    ) -> None:
        self._mqtt = mqtt
        self._period = 1.0 / frequency_hz
        self._num_poses = num_poses
        self._shutdown_event = threading.Event()
        self._thread: threading.Thread | None = None

    def start(self) -> None:
        self._thread = threading.Thread(target=self._run, daemon=True)
        self._thread.start()

    def stop(self) -> None:
        self._shutdown_event.set()
        if self._thread is not None:
            self._thread.join(timeout=5.0)

    def _run(self) -> None:
        while not self._shutdown_event.is_set():
            poses = gen_random_poses(self._num_poses)
            self._mqtt.publish_poses(poses)
            logging.getLogger(__name__).debug("Published %d real poses", len(poses))
            self._shutdown_event.wait(timeout=self._period)
