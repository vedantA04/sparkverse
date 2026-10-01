# Overview

This is a unity project that simulates two-wheeled robots.

Robots are controlled using MQTT. Refer to ./docs/mqtt.md for MQTT topics and
message formats.

The simulation publishes robot poses periodically over MQTT.

There is a separate project (outside of this one) that implements path planning
and control algorithms. It uses the poses published by the simulation, and
controls the robots via MQTT.


# Project structure
Typical Unity project folders. Some important items:

./Assets/Scenes/StartScene.unity: the entry point for the simulation. The user
selects a config file and then starts the simulation
./Assets/Scripts/SimulationManager.cs: the central controller of the simulation

Some extra non-unity stuff:

./Robot_config_JSONS: example configurations that specify robot types and initial positions
./test_server: A simplified path planning and control server intended for testing


# Conventions
## Config
Currently, simulation configuration is done partially via the Unity editor, and
partially via config file. We are moving towards all configuration being done
via file - Editor-based configuration is deprecated.
