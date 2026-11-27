#!/bin/bash
# run_no79_pigeon.sh — [79-R19] 전보 비둘기 검증 배치 런 (Test_15_Livelihood)
# 진입점: ProjectName.EditorTools.Fx79PigeonProbe.Run
# 로그:   TestOutput/no79pigeon.log / 요약: TestOutput/no79pigeon.txt
# ⚠ -quit/-nographics 금지(Play 모드 배치 계약), UnityLockfile 가드

UNITY_PATH="/mnt/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe"
PROJECT_PATH="/mnt/c/Unity/code"
LOG_FILE="$PROJECT_PATH/TestOutput/no79pigeon.log"
ENTRY="ProjectName.EditorTools.Fx79PigeonProbe.Run"

if [ ! -f "$UNITY_PATH" ]; then
  echo "[79P] ERROR: Unity 없음"; exit 1
fi

# 에디터 실행 중이면 강제 종료 + UnityLockfile 제거(배치 런 규약)
if [ -f "$PROJECT_PATH/Temp/UnityLockfile" ]; then
  echo "[79P] 에디터 실행 중 — taskkill + UnityLockfile 제거"
  taskkill.exe /IM Unity.exe /F 2>/dev/null
  sleep 5
  rm -f "$PROJECT_PATH/Temp/UnityLockfile"
fi

mkdir -p "$PROJECT_PATH/TestOutput"
rm -f "$LOG_FILE" "$PROJECT_PATH"/TestOutput/no79pigeon_*.png "$PROJECT_PATH/TestOutput/no79pigeon.txt"

echo "[79P] 배치 런: $ENTRY (타임아웃 600s)"
timeout 600 "$UNITY_PATH" \
  -projectPath "C:/Unity/code" \
  -executeMethod "$ENTRY" \
  -logFile "C:/Unity/code/TestOutput/no79pigeon.log" 2>&1

RC=$?
echo "[79P] 종료 RC=$RC"
echo "--- 요약 ---"
grep -E '\[79P-PROBE\]|PASS|미스폰|미발견|미표시' "$LOG_FILE" | tail -20
exit $RC
