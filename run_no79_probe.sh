#!/bin/bash
# run_no79_probe.sh — [무번호 79-R0-B] 베이지/검정 사각 근원 바이섹션 프로브 배치 런
# 진입점: ProjectName.EditorTools.Fx79BisectProbe.Run (Assets/Editor/Fx79BisectProbe.cs)
# 로그:   /mnt/c/Unity/code/TestOutput/no79_probe.log
# 산출:   /mnt/c/Unity/code/TestOutput/no79_*.png + no79_bisect.txt(요약/억제표)
# ⚠ -quit 사용 금지(Play 모드 배치 계약 — 프로브가 스스로 EditorApplication.Exit 호출)
# ⚠ -nographics 금지(게임뷰/RT 캡처에 그래픽스 필요)
# ⚠ UnityLockfile 가드 — 에디터가 열려 있으면 즉시 실패(exit 2)
# ⚠ 창 모드 런(cfx 선례) — 540s 하드 타임아웃(무한 대기 방지)

UNITY_PATH="/mnt/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe"
PROJECT_PATH="/mnt/c/Unity/code"
PROJECT_PATH_WIN="C:/Unity/code"
LOG_FILE_WIN="C:/Unity/code/TestOutput/no79_probe.log"
LOG_FILE="$PROJECT_PATH/TestOutput/no79_probe.log"
ENTRY="ProjectName.EditorTools.Fx79BisectProbe.Run"

if [ ! -f "$UNITY_PATH" ]; then
  echo "[no79] ERROR: Unity 실행 파일 없음 — $UNITY_PATH"
  exit 1
fi

if [ -f "$PROJECT_PATH/Temp/UnityLockfile" ]; then
  echo "[no79] ERROR: Unity 에디터가 열려 있음(Temp/UnityLockfile) — 닫고 재실행"
  exit 2
fi

mkdir -p "$PROJECT_PATH/TestOutput"
rm -f "$LOG_FILE"
rm -f "$PROJECT_PATH"/TestOutput/no79_*.png 2>/dev/null
rm -f "$PROJECT_PATH/TestOutput/no79_bisect.txt"

echo "[no79] 프로브 배치 런: $ENTRY (타임아웃 540s)"
timeout 540 "$UNITY_PATH" \
  -projectPath "$PROJECT_PATH_WIN" \
  -executeMethod "$ENTRY" \
  -logFile "$LOG_FILE_WIN" 2>&1

RC=$?
echo "[no79] 배치 종료 RC=$RC"
echo "--- 요약 마커 ---"
grep -E '\[79-SUMMARY\]|\[79-BURST\]|FINISH|FAIL|\[79-ERR\]' "$LOG_FILE" | tail -30
exit $RC
