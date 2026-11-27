#!/bin/bash
# run_iconfix194.sh — [194차 P3 검증] 오버라이드+Z보정 프레이밍 재베이크 배치 런
# 사용법: bash /mnt/c/Unity/code/run_iconfix194.sh
UNITY="/mnt/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe"
[ -f "$UNITY" ] || { echo "UNITY NOT FOUND: $UNITY"; exit 1; }
cd /mnt/c/Unity/code || exit 1

if [ -f Temp/UnityLockfile ]; then
  echo "[iconfix194] ERROR: Unity 에디터가 열려 있음(Temp/UnityLockfile) — 에디터를 닫고 재실행"
  exit 2
fi

rm -rf TestOutput/icon_fix_194
WINLOG="$(wslpath -w /mnt/c/Unity/code/TestOutput/iconfix194.log)"
"$UNITY" -quit -batchmode -projectPath "C:/Unity/code" -executeMethod IconBakeFix194.Run -logFile "$WINLOG"
echo "EXIT=$?"
echo "--- FACTS ---"
cat TestOutput/icon_fix_194/facts.log 2>/dev/null
