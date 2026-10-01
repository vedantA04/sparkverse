#!/bin/bash
HOST=localhost
MOSQ_PUB="docker run --rm --net host eclipse-mosquitto mosquitto_pub"

function cmd {
    id="$1"
    shift
    args="$*"

    $MOSQ_PUB -h $HOST -t "arena/sparknode${id}/cmd" -m "$args"
}

cmd $*