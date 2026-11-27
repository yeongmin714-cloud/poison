#!/bin/bash
# run_paradox_probe.sh — [무번호5/P3] Time Paradox 타임 스톱 런타임 검증 프로브 런
#
# 진입점: ProjectName.EditorTools.ParadoxTimeStopProbe.RunAll (Assets/Editor/ParadoxTimeStopProbe.cs)
# 로그:   /mnt/c/Unity/code/TestOutput/paradox_probe.log
# 산출:   /mnt/c/Unity/code/Screenshots/paradox_timestop_hold.png
#
# ⚠ -quit/-nographics 금지(Play 모드 배치 계약 — run_attack_premium_probe.sh 사유 승계).
# 순서: 컴파일 게이트는 부모가 별도 실행(UnityLockfile 충돌 방지).

UNITY_PATH="/mnt/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe"
PROJECT_PATH="/mnt/c/Unity/code"
PROJECT_PATH_WIN="C:/Unity/code"
LOG_FILE="$PROJECT_PATH/TestOutput/paradox_probe.log"
ENTRY="ProjectName.EditorTools.ParadoxTimeStopProbe.RunAll"

if [ ! -f "$UNITY_PATH" ]; then
  echo "[paradox] ERROR: Unity 실행 파일 없음 — $UNITY_PATH"
  exit 1
fi

if [ -f "$PROJECT_PATH/Temp/UnityLockfile" ]; then
  echo "[paradox] ERROR: Unity 에디터가 열려 있음(Temp/UnityLockfile 존재) — 에디터를 닫고 재실행"
  exit 2
fi

mkdir -p "$PROJECT_PATH/TestOutput"

echo "[paradox] 프로브 배치 런: $ENTRY"
"$UNITY_PATH" \
  -batchmode \
  -projectPath "$PROJECT_PATH_WIN" \
  -executeMethod "$ENTRY" \
  -logFile "$(wslpath -w "$LOG_FILE")"
RC=$?
echo "[paradox] ExitCode=$RC"
echo "[paradox] 로그 산출:"
grep -E "ParadoxProbe\] (CHECK|STATE|DONE|FAIL|STEP)" "$LOG_FILE" 2>/dev/null | tail -40
exit $RC
