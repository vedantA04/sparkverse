
## run via the terminal
- make sure you do the 'installation' steps, then:
```sh
# build the simulation (do this whenever you change any code)
./build.sh
# run unit tests
./run-tests.sh
# run the simulation (example config file shown)
./run-sim.sh Robot_config_JSONS/1_middle_robot.json
# run the simulation in headless mode, ie. no graphics
./run-sim.sh --headless Robot_config_JSONS/1_middle_robot.json
```


# Using the test server
The test server can be used to control the simulated robots from outside of
Unity.

See the docs in the [test server readme](./test_server/README.md).

# !!! Waiting to be reviewed.

# Software architecture
A quick overview of the software components in this simulator:

- `StartScene`: `StartScreenManager`
    - UI to select simulated robot configuration & MQTT broker
    - starts `SimulationScene`
- `SimulationScene`: `SimulationManager`
    - top level simulation manager
    - spawns virtual robots (digital twins) from the config file
    - optionally tracks real robots via HTTP GET <host>/robot/data
    - optionally sends digital twin and tracked robot poses via HTTP POST
      <host>/unityrobots/data
    - optionally controls digital twins - see `BoidController`

Optional behaviours can be turned on/off via the Unity UI. Open the
`SimulationScene`, select `SimulationManager`, then check the checkboxes in the
object inspector.


# Recording the simulation to video

1. Open the project in Unity
2. Go to Window -> General -> Recorder -> Recording Window
3. In the left-hand panel, add a recorder. Adjust the settings if you like, or
   leave them at their defaults
4. Click "START RECORDING". The simulation will also start
5. Use the simulation as per usual, and stop it when you are done. The video
   recording of the simulation will be saved under `Recordings/`, which is
   ignored by git
