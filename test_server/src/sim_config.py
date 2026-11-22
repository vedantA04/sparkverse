"""Reads the Unity simulation's config files (see Robot_config_JSONS).

Mirrors Assets/Scripts/Config/SimulationConfig.cs, which is the source of truth
"""

import json
from dataclasses import dataclass, field, fields
from pathlib import Path
from typing import Any

from control_algs.geometry import Arena


@dataclass(frozen=True)
class RobotConfig:
    id: int = 0
    name: str = ""
    prefab: str = ""
    x: float = 0.0
    y: float = 0.0


@dataclass(frozen=True)
class RandomSpawnConfig:
    count: int = 0
    prefab: str = "simplediffdrivebot"
    namePrefix: str = "Robot"
    startId: int = 1
    minDistance: float = 0.5


@dataclass(frozen=True)
class ArenaConfig:
    sizeX: float = 3.0
    sizeY: float = 3.0
    originX: float = 0.0
    originY: float = 0.0

    def to_arena(self) -> Arena:
        return Arena.from_origin_and_size(
            origin=(self.originX, self.originY), size=(self.sizeX, self.sizeY)
        )


@dataclass(frozen=True)
class MqttConfig:
    enablePublishPoses: bool = True
    posePublishInterval: float = 0.5
    enableTrackPoses: bool = False


@dataclass(frozen=True)
class SimConfig:
    robots: tuple[RobotConfig, ...] = ()
    randomSpawn: RandomSpawnConfig = field(default_factory=RandomSpawnConfig)
    arena: ArenaConfig = field(default_factory=ArenaConfig)
    mqtt: MqttConfig = field(default_factory=MqttConfig)
    enableBoidBehaviour: bool = False

    @property
    def pose_publish_hz(self) -> float:
        return 1.0 / self.mqtt.posePublishInterval


def load_sim_config(path: Path) -> SimConfig:
    return parse_sim_config(json.loads(path.read_text()))


def parse_sim_config(data: dict[str, Any]) -> SimConfig:
    _reject_unknown_keys(SimConfig, data, path="")
    config = SimConfig(
        robots=tuple(
            _parse(RobotConfig, robot, path=f"robots[{i}]")
            for i, robot in enumerate(data.get("robots", []))
        ),
        randomSpawn=_parse(
            RandomSpawnConfig, data.get("randomSpawn", {}), path="randomSpawn"
        ),
        arena=_parse(ArenaConfig, data.get("arena", {}), path="arena"),
        mqtt=_parse(MqttConfig, data.get("mqtt", {}), path="mqtt"),
        enableBoidBehaviour=bool(data.get("enableBoidBehaviour", False)),
    )
    if config.mqtt.posePublishInterval <= 0:
        raise ValueError(
            f"posePublishInterval must be positive, got "
            f"{config.mqtt.posePublishInterval}"
        )
    return config


def _parse[T](cls: type[T], data: dict[str, Any], path: str) -> T:
    _reject_unknown_keys(cls, data, path)
    return cls(**data)


def _reject_unknown_keys(cls: type, data: dict[str, Any], path: str) -> None:
    known = {f.name for f in fields(cls)}
    unknown = sorted(set(data) - known)
    if unknown:
        location = f" in '{path}'" if path else ""
        raise ValueError(
            f"Unknown config key(s){location}: {', '.join(unknown)} "
            f"(known: {', '.join(sorted(known))})"
        )
