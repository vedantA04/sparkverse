#!/bin/bash
set -eu

usage() {
    echo "Usage: $0 [--headless] <config_file>"
    echo ""
    echo "Runs the SparkVerse simulation with the specified configuration file."
    echo ""
    echo "Arguments:"
    echo "  config_file    Path to the configuration file (e.g. scenarios/example.json)"
    echo "  --headless     Run in headless (batch) mode without graphics"
    exit 1
}

case "$(uname -s)" in
    Darwin)
        SPARKVERSE_EXE=./build/SparkVerse.app/Contents/MacOS/testProject
        ;;
    Linux)
        SPARKVERSE_EXE=./build/SparkVerse
        ;;
    *)
        echo "Error: Unsupported OS ($(uname -s))"
        exit 1
        ;;
esac

HEADLESS=false
MQTT_BROKER="localhost:1883"
SCREEN_WIDTH=1920
SCREEN_HEIGHT=1080

# Parse arguments
while [ $# -gt 0 ]; do
    case "$1" in
        --headless)
            HEADLESS=true
            shift
            ;;
        -*)
            echo "Unknown option: $1"
            usage
            ;;
        *)
            CONFIG_FILE="$1"
            shift
            break
            ;;
    esac
done

if [ -z "${CONFIG_FILE:-}" ]; then
    usage
fi

CONFIG_FILE=$(realpath $CONFIG_FILE)

run() {
    $SPARKVERSE_EXE \
        -config "$CONFIG_FILE" \
        -broker "$MQTT_BROKER" \
        -window-mode windowed \
        -screen-width $SCREEN_WIDTH \
        -screen-height $SCREEN_HEIGHT \
        -logFile -
}

runHeadless() {
    $SPARKVERSE_EXE \
        -config "$CONFIG_FILE" \
        -broker "$MQTT_BROKER" \
        -batchmode \
        -nographics \
        -logFile -
}

if [ "$HEADLESS" = true ]; then
    runHeadless
else
    run
fi
