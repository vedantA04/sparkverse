from pathlib import Path

import pytest

from control_algs.geometry import Arena
from sim_config import (
    ArenaConfig,
    MqttConfig,
    RandomSpawnConfig,
    RobotConfig,
    SimConfig,
    load_sim_config,
    parse_sim_config,
)

CONFIG_DIR = Path(__file__).parents[3] / "Robot_config_JSONS"


def test_empty_config_uses_simulation_defaults():
    assert parse_sim_config({}) == SimConfig()
    assert SimConfig().pose_publish_hz == 2.0


def test_reads_arena_and_pose_publish_interval():
    config = parse_sim_config(
        {
            "arena": {"sizeX": 7.0, "sizeY": 5.0, "originX": 1.0, "originY": 0.0},
            "mqtt": {"enablePublishPoses": True, "posePublishInterval": 0.2},
        }
    )
    assert config.arena == ArenaConfig(sizeX=7.0, sizeY=5.0, originX=1.0)
    assert config.arena.to_arena() == Arena(
        min_x=-2.5, max_x=4.5, min_y=-2.5, max_y=2.5
    )
    assert config.mqtt == MqttConfig(posePublishInterval=0.2)
    assert config.pose_publish_hz == pytest.approx(5.0)


def test_reads_robots_random_spawn_and_boid_flag():
    config = parse_sim_config(
        {
            "enableBoidBehaviour": True,
            "robots": [
                {"id": 1001, "name": "R1", "prefab": "digitaltwin", "x": 1, "y": 2}
            ],
            "randomSpawn": {"count": 3, "namePrefix": "Bot"},
            "mqtt": {"enableTrackPoses": True},
        }
    )
    assert config.enableBoidBehaviour
    assert config.robots == (
        RobotConfig(id=1001, name="R1", prefab="digitaltwin", x=1, y=2),
    )
    assert config.randomSpawn == RandomSpawnConfig(count=3, namePrefix="Bot")
    assert config.mqtt == MqttConfig(enableTrackPoses=True)


@pytest.mark.parametrize(
    "data",
    [
        {"boid": {}},
        {"arena": {"other": 1}},
        {"mqtt": {"poseUpdateInterval": 0.2}},
        {"randomSpawn": {"cnt": 1}},
        {"robots": [{"id": 1, "nme": "R1"}]},
    ],
    ids=lambda d: next(iter(d)),
)
def test_rejects_unknown_keys(data):
    with pytest.raises(ValueError, match="Unknown config key"):
        parse_sim_config(data)


def test_rejects_non_positive_pose_publish_interval():
    with pytest.raises(ValueError):
        parse_sim_config({"mqtt": {"posePublishInterval": 0}})


@pytest.mark.parametrize(
    "path", sorted(CONFIG_DIR.glob("*.json")), ids=lambda p: p.name
)
def test_example_config_has_only_known_keys(path):
    assert isinstance(load_sim_config(path), SimConfig)


def test_example_configs_exist():
    assert list(CONFIG_DIR.glob("*.json"))
