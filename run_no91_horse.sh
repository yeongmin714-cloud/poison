#!/bin/bash
# [무번호 91] 갈색말_AC 에디터 배치 러너
UNITY_PATH="/mnt/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe"
PROJECT_PATH="/mnt/c/Unity/code"
LOG_FILE="/mnt/c/Unity/code/no91_horse.log"

PROJECT_PATH_WIN=$(wslpath -w "$PROJECT_PATH")
LOG_FILE_WIN=$(wslpath -w "$LOG_FILE")

"$UNITY_PATH" -quit -batchmode -projectPath "$PROJECT_PATH_WIN" -executeMethod ProjectName.EditorTools.No91BrownHorseController.Build -logFile "$LOG_FILE_WIN"
echo "EXIT=$?"
