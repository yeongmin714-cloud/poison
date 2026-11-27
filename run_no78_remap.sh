#!/bin/bash
# run_no78_remap.sh — [무번호 78] Meteorite FBX 결손 머티리얼 리맵 배치 런
# 진입점: No78MaterialRemapBatch.Run (Assets/Editor/No78MaterialRemapBatch.cs)
# 로그:   /mnt/c/Unity/code/TestOutput/no78_remap.log
# 산출:   /mnt/c/Unity/code/TestOutput/no78_material_remap.txt (기계판독용)
# ⚠ -quit 사용 가능(에디터 전용 임포트 작업 — Play 모드 배치 계약 아님)

UNITY_PATH="/mnt/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe"
PROJECT_PATH="/mnt/c/Unity/code"
PROJECT_PATH_WIN="C:/Unity/code"
LOG_FILE_WIN="C:/Unity/code/TestOutput/no78_remap.log"
LOG_FILE="$PROJECT_PATH/TestOutput/no78_remap.log"
ENTRY="No78MaterialRemapBatch.Run"

if [ ! -f "$UNITY_PATH" ]; then
  echo "[no78remap] ERROR: Unity 실행 파일 없음 — $UNITY_PATH"
  exit 1
fi

# Unity 프로세스 실측 가드 — 실제 실행 중이면 실패, 잔여 lockfile(프로세스 부재)이면 제거 후 진행
if pgrep -f "Unity.exe" > /dev/null 2>&1; then
  echo "[no78remap] ERROR: Unity 프로세스 실행 중 — 종료 후 재실행"
  exit 2
fi
if [ -f "$PROJECT_PATH/Temp/UnityLockfile" ]; then
  echo "[no78remap] 잔여 UnityLockfile 감지(프로세스 부재) — 제거 후 진행"
  rm -f "$PROJECT_PATH/Temp/UnityLockfile"
fi

mkdir -p "$PROJECT_PATH/TestOutput"
rm -f "$LOG_FILE" "$PROJECT_PATH/TestOutput/no78_material_remap.txt"

echo "[no78remap] 리맵 배치 런: $ENTRY"
"$UNITY_PATH" -quit -batchmode \
  -projectPath "$PROJECT_PATH_WIN" \
  -executeMethod "$ENTRY" \
  -logFile "$LOG_FILE_WIN" 2>&1 | tail -5
EC=$?
echo "[no78remap] unity_exit=$EC"

RESULT_FILE="$PROJECT_PATH/TestOutput/no78_material_remap.txt"
if [ -f "$RESULT_FILE" ]; then
  grep -E "VERDICT|SUMMARY|VERIFY_BROKEN|FAIL|EXCEPTION" "$RESULT_FILE"
  if grep -q "NO78_REMAP_VERDICT=PASS" "$RESULT_FILE"; then
    echo "[no78remap] PASS"
    exit 0
  else
    echo "[no78remap] FAIL"
    exit 1
  fi
else
  echo "[no78remap] 결과 파일 부재 — 실패"
  tail -40 "$LOG_FILE" 2>/dev/null
  exit 1
fi
