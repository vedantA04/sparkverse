# MQTT reference

Just a reference. The source of truth is here: https://github.com/asekerci/syncspark/blob/main/docs/software/mqtt.md

# topics and message formats

## simulated robot poses
- topic: `arena/poses/sim`
- format: comma-separated, `robot_id x y rot` (rotation in degrees)
    - all poses are batched in a single message
    - example: `1001 3.2 1.1 57.5,1002 3.0 2.1 180.1`

## real robot poses
- topic: `arena/poses/real`
- format: same as simulated robots

## commands
See https://github.com/asekerci/syncspark/blob/main/docs/software/mqtt.md
