import logging
import time

from models import ControlConfig
from state_store import StateStore
from control_thread import ControlThread
from mqtt.bridge import MQTTBridge
from control_algs.prop_waypoints import ProportionalWaypointFollowingControl
from pose_publisher import PosePublisher

logging.basicConfig(
    level=logging.DEBUG,
    format="%(asctime)s [%(name)s] %(levelname)s: %(message)s",
)

CONTROL_FREQ_HZ = 5
CONTROL_PERIOD_MS = 1000 // CONTROL_FREQ_HZ
ENABLE_MQTT_PUBLISH_RANDOM_POSES = False
PUBLISH_POSE_HZ = 2


def main():
    config = ControlConfig(
        loop_hz=CONTROL_FREQ_HZ,
        broker_host="localhost",
        broker_port=1883,
        qos=1,
        client_id="test-server",
    )
    state_store = StateStore()
    algorithm = ProportionalWaypointFollowingControl(
        waypoints=[
            (0.0, 0.0),
            (1.0, 0.0),
            (1.0, 1.0),
            (0.0, 1.0),
        ],
        heading_error_multiplier=2,
        location_error_multiplier=4,
        cmd_duration_ms=CONTROL_PERIOD_MS,
    )
    mqtt = MQTTBridge(config, state_store)
    control_thread = ControlThread(state_store, algorithm, mqtt, config)

    if not mqtt.connect(control_thread._shutdown_event):
        logging.getLogger(__name__).warning(
            "MQTT broker not reachable - control thread will still run but commands won't be sent"
        )
    control_thread.start()

    pose_publisher = None
    if ENABLE_MQTT_PUBLISH_RANDOM_POSES:
        pose_publisher = PosePublisher(mqtt, frequency_hz=PUBLISH_POSE_HZ, num_poses=1)
        pose_publisher.start()

    try:
        while True:
            time.sleep(0.1)
    except KeyboardInterrupt:
        pass
    finally:
        logging.getLogger(__name__).info("Shutting down...")
        if pose_publisher is not None:
            pose_publisher.stop()
        control_thread.stop()
        mqtt.disconnect()


if __name__ == "__main__":
    main()
