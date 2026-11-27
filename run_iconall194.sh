#!/bin/bash
# run_iconall194.sh — [194차 P3] 전 아이템 정면 아이콘 전수 베이크 스냅샷 배치 런
# 진입점: IconBakeAll194.Run (Assets/Editor/IconBakeAll194.cs)
# 산출:   TestOutput/icon_bake_all_194/*.png + facts.log (OK/MISS/FALLBACK/GREENISH/SUMMARY)
# 실행 방식: run_editmode_check.sh 선례 — WSL 직접 Unity.exe 호출(cmd.exe 경유 따옴표 이스케이프 회피)
# 사용법: bash /mnt/c/Unity/code/run_iconall194.sh
UNITY="/mnt/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe"
[ -f "$UNITY" ] || { echo "UNITY NOT FOUND: $UNITY"; exit 1; }
cd /mnt/c/Unity/code || exit 1

if [ -f Temp/UnityLockfile ]; then
  echo "[iconall194] ERROR: Unity 에디터가 열려 있음(Temp/UnityLockfile) — 에디터를 닫고 재실행"
  exit 2
fi

rm -rf TestOutput/icon_bake_all_194
WINLOG="$(wslpath -w /mnt/c/Unity/code/TestOutput/iconall194.log)"
"$UNITY" -quit -batchmode -projectPath "C:/Unity/code" -executeMethod IconBakeAll194.Run -logFile "$WINLOG"
echo "EXIT=$?"
echo "--- FACTS ---"
tail -40 TestOutput/icon_bake_all_194/facts.log 2>/dev/null
echo "--- FILES ---"
ls TestOutput/icon_bake_all_194/*.png 2>/dev/null | wc -l
