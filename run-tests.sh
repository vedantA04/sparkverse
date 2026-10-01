#!/bin/bash
case "$(uname -s)" in
    Darwin)
        UNITY_EDITOR=/Applications/Unity/Hub/Editor/6000.3.2f1/Unity.app/Contents/MacOS/Unity
        ;;
    Linux)
        UNITY_EDITOR=~/Unity/Hub/Editor/6000.3.2f1/Editor/Unity
        ;;
    *)
        echo "Error: Unsupported OS ($(uname -s))"
        exit 1
        ;;
esac

if [ ! -f "$UNITY_EDITOR" ]; then
    echo "Error: UNITY_EDITOR not found at $UNITY_EDITOR"
    echo
    echo "Make sure Unity is installed, including the correct"
    echo "editor version, then update run-tests.sh to point to the"
    echo "editor executable."
    exit 1
fi

echo "Running tests..."
$UNITY_EDITOR -runTests -batchmode -projectPath "$PWD" \
    -testPlatform PlayMode \
    -testResults testResults.xml
cat testResults.xml
echo ""