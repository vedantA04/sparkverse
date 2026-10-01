# Simulation test server

A 'server' for testing a robot simulation that runs in Unity. Not a HTTP server
- uses MQTT to talk to the simulation.

# Getting started
- assumes you're on linux
- install [just](https://just.systems/)
- install [uv](https://docs.astral.sh/uv/)
```sh
just check-all  # format, lint, run tests
just mosq       # run the mosquitto broker (if not already running)
just server     # run the server
```

Now run the simulation. You should see messages in the Unity console, and on
the server console. Hopefully the robots are also moving!

The test server is configured via hard-coded settings at the top of main.py
