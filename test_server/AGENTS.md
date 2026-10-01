This project is a server for testing a robot simulation that runs in Unity.

The Unity simulation subscribes to MQTT topics that can receive commands for
the robots.

The simulation publishes poses of all simulated robots to the MQTT topic
`arena/poses/sim`. The test server subscribes to that topic and stores the
poses. The message format is comma-separated per robot, space-separated per
element: `robot_id x y rot` (rotation in degrees), e.g.
`1001 3.2 1.1 57.5,1002 3.0 2.1 180.1`.

In the lab, real robot poses are tracked by an overhead camera. This test server
can be used to generate any robot poses.

A pose is (x, y, rotation), where rotation is in degrees. Assume zero degrees is
north, or in the direction (x: 0, y: 1)

# tools used
- `uv` is used for dependencies and running python scripts
- `just` is used for running tasks, eg. `just check-all` to lint, type check and
  run tests

# Coding style
- use functional code and immutable data types, eg. frozen dataclasses
- prefer self-describing code instead of lots of comments
- run `just check-all` to run all linting, type checking and tests

For more details, read README.md
