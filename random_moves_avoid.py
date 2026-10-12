#!/usr/bin/env -S uv run --script
#
# /// script
# requires-python = ">=3.14"
# dependencies = ["paho-mqtt"]
#
# The easiest way to run this script is to install uv:
# https://docs.astral.sh/uv/, then you can run it like
# any other script: ./random_moves_avoid.py
#
# Otherwise, you need to install python & the above dependencies,
# and run this file with python.
# ///

import itertools
import math
import random
import time
from dataclasses import dataclass
from enum import Enum, auto

import paho.mqtt.client as mqtt

HOST = "localhost"
CMD_TOPIC = "arena/sparknode{}/cmd"
POSES_TOPIC = "arena/poses/sim"

ROBOT_MAX_TURN_RATE = 120  # degrees per second, determined empirically
AVOID_MARGIN = 0.4
AVOID_TURN_DURATION_S = 2.0
AVOID_DRIVE_DURATION_S = 2.0
AVOID_DURATION_S = AVOID_TURN_DURATION_S + AVOID_DRIVE_DURATION_S

X_MIN, X_MAX = -1.5, 1.5
Y_MIN, Y_MAX = -1.5, 1.5


class State(Enum):
    NORMAL = auto()
    AVOID_TURN = auto()
    AVOID_DRIVE = auto()


@dataclass
class Robot:
    id: int
    x: float
    y: float
    heading: float
    state: State = State.NORMAL
    avoid_started_at: float | None = None

    def publish(self, client: mqtt.Client, command: str) -> None:
        client.publish(CMD_TOPIC.format(self.id), command)

    def start_avoid(
        self, client: mqtt.Client, away_heading: float, now: float, reason: str = "wall"
    ) -> None:
        diff = norm_180(away_heading - self.heading)
        direction = "left" if diff < 0 else "right"
        turn_ms = round(abs(diff) / ROBOT_MAX_TURN_RATE * 1000)
        self.state = State.AVOID_TURN
        self.avoid_started_at = now
        print(f"robot {self.id}: avoiding {reason}, turn {direction} for {turn_ms}ms")
        self.publish(client, "stop")
        self.publish(client, f"turn {direction} 63 {turn_ms}")

    def progress_avoid(self, client: mqtt.Client, now: float) -> None:
        assert self.avoid_started_at is not None
        elapsed = now - self.avoid_started_at
        if elapsed >= AVOID_DURATION_S:
            self.state = State.NORMAL
            self.avoid_started_at = None
        elif self.state is State.AVOID_TURN and elapsed >= AVOID_TURN_DURATION_S:
            self.state = State.AVOID_DRIVE
            self.publish(
                client, f"drive forward 50 {int(AVOID_DRIVE_DURATION_S * 1000)}"
            )


def norm_180(x: float) -> float:
    # normalise a heading to [-180, 180)
    x = (x + 360) % 360
    return x if x < 180 else x - 360


def nearest_wall(x: float, y: float) -> tuple[float, float]:
    # returns (distance, heading to avoid)
    return min(
        (X_MAX - x, 270.0),
        (x - X_MIN, 90.0),
        (Y_MAX - y, 180.0),
        (y - Y_MIN, 0.0),
    )


def heading_away_from(a: Robot, b: Robot) -> float:
    # heading at a that points away from b
    return math.degrees(math.atan2(a.x - b.x, a.y - b.y))


def check_robot_proximity(
    client: mqtt.Client, robots: dict[int, Robot], now: float
) -> None:
    for a, b in itertools.combinations(robots.values(), 2):
        if math.dist((a.x, a.y), (b.x, b.y)) >= AVOID_MARGIN:
            continue
        if a.state is State.NORMAL:
            a.start_avoid(client, heading_away_from(a, b), now, f"robot {b.id}")
        if b.state is State.NORMAL:
            b.start_avoid(client, heading_away_from(b, a), now, f"robot {a.id}")


def random_move() -> str:
    speed, duration = random.randint(50, 63), random.randint(2000, 5000)
    if random.random() < 0.8:
        return f"drive forward {speed} {duration}"
    return f"turn {random.choice(('left', 'right'))} {speed} {duration}"


def on_message(
    client: mqtt.Client, robots: dict[int, Robot], msg: mqtt.MQTTMessage
) -> None:
    now = time.monotonic()
    for record in msg.payload.decode().split(","):
        id_, x, y, heading = record.split()
        id_ = int(id_)
        x, y, heading = float(x), float(y), float(heading)
        if id_ not in robots:
            robots[id_] = Robot(int(id_), x, y, heading)
        robot = robots[id_]
        robot.x, robot.y, robot.heading = x, y, heading
        if robot.state is State.NORMAL:
            distance, away_heading = nearest_wall(robot.x, robot.y)
            if distance < AVOID_MARGIN:
                robot.start_avoid(client, away_heading, now)
        else:
            robot.progress_avoid(client, now)
    check_robot_proximity(client, robots, now)


def main() -> None:
    robots: dict[int, Robot] = {}
    client = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2)
    client.user_data_set(robots)
    client.on_message = on_message
    client.connect(HOST)
    client.loop_start()
    client.subscribe(POSES_TOPIC)
    try:
        while True:
            movable = [r for r in robots.values() if r.state is State.NORMAL]
            for robot in movable:
                robot.publish(client, random_move())
                time.sleep(1)
            if not movable:
                time.sleep(1)
    except KeyboardInterrupt:
        pass
    finally:
        client.loop_stop()
        client.disconnect()


if __name__ == "__main__":
    main()
