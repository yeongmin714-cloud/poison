#!/bin/bash

# Set the Unity installation path
UNITY_PATH="/mnt/c/Unity/Editor/Unity"

# Check if Unity executable exists
if [ ! -f "$UNITY_PATH" ]; then
    echo "Unity executable not found at $UNITY_PATH"
    exit 1
fi

# Compile the project
echo "Starting Unity compile test..."
"$UNITY_PATH" -quit -batchmode -projectPath "/mnt/c/Unity/code" -executeMethod TestCompile.CompileTest -logFile "/mnt/c/Unity/code/compile.log"

if [ $? -eq 0 ]; then
    echo "Unity compile test completed successfully"
    exit 0
else
    echo "Unity compile test failed"
    exit 1
fi