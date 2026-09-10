#!/bin/bash

# Set the Unity installation path (Unity Hub default install location)
UNITY_PATH="/mnt/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe"
PROJECT_PATH="/mnt/c/Unity/code"
LOG_FILE="/mnt/c/Unity/code/compile.log"

# Check if Unity executable exists
if [ ! -f "$UNITY_PATH" ]; then
    echo "Unity executable not found at $UNITY_PATH"
    exit 1
fi

# WSL cannot pass Linux-style paths to a Windows exe — convert to Windows paths
PROJECT_PATH_WIN=$(wslpath -w "$PROJECT_PATH")
LOG_FILE_WIN=$(wslpath -w "$LOG_FILE")

# Compile the project
echo "Starting Unity compile test..."
"$UNITY_PATH" -quit -batchmode -projectPath "$PROJECT_PATH_WIN" -executeMethod TestCompile.CompileTest -logFile "$LOG_FILE_WIN"

if [ $? -eq 0 ]; then
    echo "Unity compile test completed successfully"
    exit 0
else
    echo "Unity compile test failed"
    exit 1
fi
