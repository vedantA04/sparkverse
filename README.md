# SparkVerse

Unity-based hybrid simulation world for SparkNodes.

## Getting started

This guide will walk you through setting up and running a simulation with four SparkNodes.
We assume you are using a Linux machine.

### Install and run the MQTT broker on your local machine

1. Install the broker packages. On Ubuntu Linux, install `mosquitto` and
`mosquitto-clients`. In this guide, we’ll run the broker locally [^1].
2. Open `/etc/mosquitto/mosquitto.conf` and make sure the following lines are present:

```text
   listener 1883
   allow_anonymous true
```

Note: After changing the broker configuration, restart the broker. You can use the following commands:

```bash
  sudo systemctl status mosquitto   # check whether the broker is running
  sudo systemctl stop mosquitto
  sudo systemctl start mosquitto
```

### Test the operation of the broker

1. Open a terminal window and run: `mosquitto_sub -h localhost -t "arena/#"`
2. Open a second terminal window and run: `mosquitto_pub -h localhost -t "arena" -m "Hello world!"`
   You should see the message show up in the first terminal.

### Install Unity and the SparkVerse simulation suite

1. Clone this repository to your local machine.
2. Download [Unity](https://unity.com/). If you do not already have a Unity account, you’ll be prompted to create one.
3. Open Unity Hub and sign in.

### Run the simulation via the Unity UI

1. Open the local copy of the repository from the Unity Hub UI.
   Unity will prompt you to install the editor version that matches this project (6000.3.2f1).
   You can dismiss the package errors for now.
2. Open the `StartScene` scene.
3. Press `Play` in Unity to start the simulation.
4. Under the "Config JSON File" section, choose the `Robot_config_JSONS/4_square.json` file.
   Leave the MQTT broker address unchanged.
5. Click `Start simulation`.
   - Ignore the "Server error: Cannot connect to destination host" messages.
   - You can pan and zoom using the arrow keys.
6. Send an MQTT command to a SparkNode (ID 1001) using `mosquitto`:

   ```bash
   mosquitto_pub -h localhost -t "arena/sparknode1001/cmd" -m "drive forward 40 2000"
   ```

   You should see one of the robots move forward and then stop.

## Licensing

Unless otherwise stated, all source, configuration, documentation etc. in this repository is covered by [GPL v3](./LICENSES/GPL-3.0.txt).

Included and referenced libraries may have different license terms. See [LICENSES/readme](./LICENSES/readme.md)

## Footnotes

[^1]: You can also run the broker via Docker. This script starts it in Docker: `run-mosquitto.sh`
