#!/bin/bash
# run_no79_magswap.sh — [무번호 79-R16] F10 아틀라스 마젠타 스왑 검증 배치 런
# 진입점: ProjectName.EditorTools.Fx79MagentaSwapProbe.Run (Assets/Editor/Fx79MagentaSwapProbe.cs)
# 로그:   /mnt/c/Unity/code/TestOutput/no79_magswap.log
# 산출:   /mnt/c/Unity/code/TestOutput/no79m_*.png + no79_magswap.txt(요약/판정)
# ⚠ -quit 사용 금지(Play 모드 배치 계약 — 프로브가 스스로 EditorApplication.Exit 호출)
# ⚠ -nographics 금지(게임뷰 캡처에 그래픽스 필요)
# ⚠ UnityLockfile 가드 — 에디터가 열려 있으면 즉시 실패(exit 2)

UNITY_PATH="/mnt/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe"
PROJECT_PATH="/mnt/c/Unity/code"
PROJECT_PATH_WIN="C:/Unity/code"
LOG_FILE_WIN="C:/Unity/code/TestOutput/no79_magswap.log"
LOG_FILE="$PROJECT_PATH/TestOutput/no79_magswap.log"
ENTRY="ProjectName.EditorTools.Fx79MagentaSwapProbe.Run"

if [ ! -f "$UNITY_PATH" ]; then
  echo "[no79m] ERROR: Unity 실행 파일 없음 — $UNITY_PATH"
  exit 1
fi

if [ -f "$PROJECT_PATH/Temp/UnityLockfile" ]; then
  echo "[no79m] ERROR: Unity 에디터가 열려 있음(Temp/UnityLockfile) — 닫고 재실행"
  exit 2
fi

mkdir -p "$PROJECT_PATH/TestOutput"
rm -f "$LOG_FILE"
rm -f "$PROJECT_PATH"/TestOutput/no79m_*.png 2>/dev/null
rm -f "$PROJECT_PATH/TestOutput/no79_magswap.txt"

echo "[no79m] F10 스왑 프로브 배치 런: $ENTRY (타임아웃 540s)"
timeout 540 "$UNITY_PATH" \
  -projectPath "$PROJECT_PATH_WIN" \
  -executeMethod "$ENTRY" \
  -logFile "$LOG_FILE_WIN" 2>&1

RC=$?
echo "[no79m] 배치 종료 RC=$RC"
echo "--- 요약 마커 ---"
grep -E '\[79M-SUMMARY\]|\[79M-VERDICT\]|FINISH|FAIL|\[79M-ERR\]' "$LOG_FILE" | tail -40
exit $RC
