#!/bin/bash
# [199차] before/after 고립 재베이크 프로브 래퍼 — $1 = before|after
PHASE="${1:-before}"
UNITY="/mnt/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe"
[ -f "$UNITY" ] || { echo "UNITY NOT FOUND"; exit 1; }
"$UNITY" -batchmode -projectPath "$(wslpath -w /mnt/c/Unity/code)" \
  -executeMethod "IconBakeSurgery199.Run${PHASE^}" -quit \
  -logFile "$(wslpath -w /mnt/c/Unity/code/TestOutput/icon_surgery_199_${PHASE}.log)"
echo "PROBE_EXIT=$?"
tail -20 TestOutput/icon_surgery_199/${PHASE}/facts.log 2>/dev/null
