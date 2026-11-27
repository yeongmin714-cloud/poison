#!/bin/bash
# run_icondiag194.sh — [194차 P3 진단] 결함 키 원본 자세/extent 로그 배치 런
# 사용법: bash /mnt/c/Unity/code/run_icondiag194.sh
UNITY="/mnt/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe"
[ -f "$UNITY" ] || { echo "UNITY NOT FOUND: $UNITY"; exit 1; }
cd /mnt/c/Unity/code || exit 1

if [ -f Temp/UnityLockfile ]; then
  echo "[icondiag194] ERROR: Unity 에디터가 열려 있음(Temp/UnityLockfile) — 에디터를 닫고 재실행"
  exit 2
fi

rm -rf TestOutput/icon_diag_194
WINLOG="$(wslpath -w /mnt/c/Unity/code/TestOutput/icondiag194.log)"
"$UNITY" -quit -batchmode -projectPath "C:/Unity/code" -executeMethod IconDiag194.Run -logFile "$WINLOG"
echo "EXIT=$?"
echo "--- DIAG ---"
cat TestOutput/icon_diag_194/diag.log 2>/dev/null
