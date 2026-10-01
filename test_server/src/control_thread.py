import logging
import threading

from state_store import StateStore
from control_algs.control_algorithm import ControlAlgorithm
from models import ControlConfig
from mqtt.bridge import MQTTBridge

logger = logging.getLogger(__name__)


class ControlThread:
    def __init__(
        self,
        state_store: StateStore,
        algorithm: ControlAlgorithm,
        mqtt: MQTTBridge,
        config: ControlConfig,
    ) -> None:
        self._state_store = state_store
        self._algorithm = algorithm
        self._mqtt = mqtt
        self._config = config
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
        period = 1.0 / self._config.loop_hz
        while not self._shutdown_event.is_set():
            poses = self._state_store.snapshot()

            commands = self._algorithm.compute(poses)

            if commands:
                for robot_id, command in commands:
                    self._mqtt.publish_command(robot_id, command)
                for c in commands:
                    logger.info(c)

            self._shutdown_event.wait(timeout=period)
