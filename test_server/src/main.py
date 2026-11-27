import argparse
import logging
import time
from pathlib import Path

from models import ControlConfig
from state_store import StateStore
from control_thread import ControlThread
from mqtt.bridge import MQTTBridge
from control_algs.boids import BoidsControl
from control_algs.control_algorithm import ControlAlgorithm
from control_algs.prop_waypoints import ProportionalWaypointFollowingControl
from sim_config import SimConfig, load_sim_config

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(name)s] %(levelname)s: %(message)s",
)

# "waypoints" or "boids". For boids, disable enableBoidBehaviour in the
# simulation config, otherwise the simulation also drives the robots itself.
CONTROL_ALGORITHM = "waypoints"

logger = logging.getLogger(__name__)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Robot simulation test server")
    parser.add_argument(
        "sim_config",
        nargs="?",
        type=Path,
        help="Simulation config file (e.g. ../Robot_config_JSONS/4_square.json). "
        "Without one, the simulation's defaults are used.",
    )
    return parser.parse_args()


def make_algorithm(sim_config: SimConfig) -> ControlAlgorithm:
    cmd_duration_ms = round(1000 / sim_config.pose_publish_hz)
    if CONTROL_ALGORITHM == "waypoints":
        return ProportionalWaypointFollowingControl(
            waypoints=[
                (0.0, 0.0),
                (1.0, 0.0),
                (1.0, 1.0),
                (0.0, 1.0),
            ],
            heading_error_multiplier=2,
            location_error_multiplier=4,
            cmd_duration_ms=cmd_duration_ms,
        )
    elif CONTROL_ALGORITHM == "boids":
        return BoidsControl(arena=sim_config.arena.to_arena())
    else:
        raise RuntimeError(f"Unknown CONTROL_ALGORITHM: '{CONTROL_ALGORITHM}'")


def main():
    args = parse_args()
    sim_config = load_sim_config(args.sim_config) if args.sim_config else SimConfig()
    logger.info(
        f"Simulation config: {args.sim_config or 'defaults'}, "
        f"control/pose rate {sim_config.pose_publish_hz:g} Hz, {sim_config.arena}"
    )

    config = ControlConfig(
        loop_hz=sim_config.pose_publish_hz,
        broker_host="localhost",
        broker_port=1883,
        qos=1,
        client_id="test-server",
    )
    state_store = StateStore()
    algorithm = make_algorithm(sim_config)
    mqtt = MQTTBridge(config, state_store)
    control_thread = ControlThread(state_store, algorithm, mqtt, config)

    if not mqtt.connect(control_thread._shutdown_event):
        logger.warning(
            "MQTT broker not reachable - control thread will still run but commands won't be sent"
        )
    control_thread.start()

    try:
        while True:
            time.sleep(0.1)
    except KeyboardInterrupt:
        pass
    finally:
        logger.info("Shutting down...")
        control_thread.stop()
        mqtt.disconnect()


if __name__ == "__main__":
    main()
