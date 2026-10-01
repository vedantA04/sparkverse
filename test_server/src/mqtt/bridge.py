import logging
import threading

import paho.mqtt.client as mqtt

from models import ControlConfig, Command, Pose
from mqtt.commands import to_mqtt_message
from mqtt.poses import parse_poses, serialize_poses
from state_store import StateStore

logger = logging.getLogger(__name__)

SIM_POSES_TOPIC = "arena/poses/sim"
REAL_POSES_TOPIC = "arena/poses/real"


class MQTTBridge:
    def __init__(
        self,
        config: ControlConfig,
        state_store: StateStore | None = None,
    ) -> None:
        self._config = config
        self._state_store = state_store
        self._client = mqtt.Client(
            mqtt.CallbackAPIVersion.VERSION2,
            client_id=config.client_id,
        )
        if state_store is not None:
            self._client.on_message = self._handle_message

    def _handle_message(self, _client, _userdata, message) -> None:
        if message.topic == SIM_POSES_TOPIC and self._state_store is not None:
            poses = parse_poses(message.payload.decode("utf-8"))
            self._state_store.update(poses)
            logger.debug("Received %d simulated poses", len(poses))

    def connect(self, shutdown_event: threading.Event) -> bool:
        try:
            self._client.connect(self._config.broker_host, self._config.broker_port)
        except Exception:
            logger.warning(
                "Failed to connect to MQTT broker at %s:%d",
                self._config.broker_host,
                self._config.broker_port,
            )
            return False
        self._client.loop_start()
        if self._state_store is not None:
            self._client.subscribe(SIM_POSES_TOPIC, qos=0)
        return True

    def publish_command(self, robot_id: int, command: Command) -> None:
        topic = f"arena/sparknode{robot_id}/cmd"
        payload = to_mqtt_message(command)
        logger.debug(payload)
        self._client.publish(topic, payload, qos=self._config.qos)

    def publish_poses(self, poses: list[tuple[int, Pose]]) -> None:
        payload = serialize_poses(poses)
        logger.debug("Publishing %s", payload)
        self._client.publish(REAL_POSES_TOPIC, payload, qos=0)

    def disconnect(self) -> None:
        self._client.loop_stop()
        self._client.disconnect()
