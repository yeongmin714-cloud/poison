#!/bin/bash
# run_no79_rect.sh — [무번호 79-R17] 베이지 사각 원인 특정 배치 런 (UV붕괴 vs 알파강제 판별)
# 진입점: ProjectName.EditorTools.Fx79RectProbe.Run
# 로그:   TestOutput/no79rect.log / 요약: TestOutput/no79rect.txt
# ⚠ -quit/-nographics 금지(Play 모드 배치 계약), UnityLockfile 가드

UNITY_PATH="/mnt/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe"
PROJECT_PATH="/mnt/c/Unity/code"
LOG_FILE="$PROJECT_PATH/TestOutput/no79rect.log"
ENTRY="ProjectName.EditorTools.Fx79RectProbe.Run"

if [ ! -f "$UNITY_PATH" ]; then
  echo "[79R] ERROR: Unity 없음"; exit 1
fi
if [ -f "$PROJECT_PATH/Temp/UnityLockfile" ]; then
  echo "[79R] ERROR: 에디터 열림(UnityLockfile)"; exit 2
fi

mkdir -p "$PROJECT_PATH/TestOutput"
rm -f "$LOG_FILE" "$PROJECT_PATH"/TestOutput/no79r_*.png "$PROJECT_PATH/TestOutput/no79rect.txt"

echo "[79R] 배치 런: $ENTRY (타임아웃 600s)"
timeout 600 "$UNITY_PATH" \
  -projectPath "C:/Unity/code" \
  -executeMethod "$ENTRY" \
  -logFile "C:/Unity/code/TestOutput/no79rect.log" 2>&1

RC=$?
echo "[79R] 종료 RC=$RC"
echo "--- 요약 ---"
grep -E '\[79R-SUMMARY\]|GRAD_VERDICT|FAIL' "$LOG_FILE" | tail -20
exit $RC
