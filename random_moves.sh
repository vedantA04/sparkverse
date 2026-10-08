#!/bin/bash
HOST=localhost
MOSQ_PUB="mosquitto_pub"

function cmd {
    id="$1"
    shift
    args="$*"

    echo $MOSQ_PUB -h $HOST -t "arena/sparknode${id}/cmd" -m "$args"
    $MOSQ_PUB -h $HOST -t "arena/sparknode${id}/cmd" -m "$args"
}

function rand {
    # rand min max
    echo $((RANDOM % ($2 - $1 + 1) + $1))
}

while true; do
    for id in 1001 1002 1003 1004; do
        speed=$(rand 40 63)
        duration=$(rand 500 3000)
        if [ $((RANDOM % 2)) -eq 0 ]; then
            dir=(forward reverse)
            verb=drive
        else
            dir=(left right)
            verb=turn
        fi
        echo $id $verb ${dir[RANDOM % 2]} $speed $duration
        cmd $id $verb ${dir[RANDOM % 2]} $speed $duration
        sleep 1
    done
done
