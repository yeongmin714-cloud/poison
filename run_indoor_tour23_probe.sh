#!/bin/bash
# [IndoorTour23] 실내 관람 모드 배치 프로브 — Play 모드 배치(-quit/-nographics 금지: 프로브가 스스로 Exit)
set -u
cd /mnt/c/Unity/code
rm -f Temp/UnityLockfile
"/mnt/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe" \
  -batchmode -projectPath C:/Unity/code \
  -executeMethod ProjectName.EditorTools.IndoorTourMode23Probe.RunAll \
  -logFile TestOutput/indoor_tour23_probe.log
echo "UNITY_EXIT=$?"
grep -a "\[IndoorTour23\]" TestOutput/indoor_tour23_probe.log | tail -25
