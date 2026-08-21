#!/bin/bash
# auto_test_and_capture.sh - Unity PlayMode 자동 실행 + 스크린샷 캡처 + 분석

set -e

PROJECT_PATH="C:/Unity/code"
UNITY="/mnt/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe"
SCREENSHOT_DIR="/mnt/c/Unity/code/Screenshots"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
SCREENSHOT_FILE="${SCREENSHOT_DIR}/gameplay_${TIMESTAMP}.png"
LOG_FILE="/tmp/unity_auto_test_${TIMESTAMP}.log"

mkdir -p "$SCREENSHOT_DIR"

echo "========================================"
echo "[AutoTest] Starting automated gameplay test"
echo "========================================"
echo "Project: $PROJECT_PATH"
echo "Screenshot: $SCREENSHOT_FILE"
echo "Log: $LOG_FILE"
echo ""

# Unity PlayMode 실행 (xvfb 가상 디스플레이 사용, -nographics 제거)
xvfb-run -a -s "-screen 0 1920x1080x24" \
  "$UNITY" \
  -batchmode \
  -projectPath "$PROJECT_PATH" \
  -executeMethod AutoGameplayTest.RunAndCapture \
  -logFile "$LOG_FILE" \
  2>&1 | tail -30

EXIT_CODE=$?

if [ $EXIT_CODE -eq 0 ]; then
    echo ""
    echo "[AutoTest] Unity execution completed successfully"
    
    # 스크린샷이 생성되었는지 확인
    if [ -f "$SCREENSHOT_FILE" ]; then
        echo "[AutoTest] Screenshot captured: $SCREENSHOT_FILE"
        ls -lh "$SCREENSHOT_FILE"
    else
        echo "[AutoTest] WARNING: Screenshot not found at $SCREENSHOT_FILE"
        # 최신 스크린샷 찾기
        LATEST=$(ls -t "$SCREENSHOT_DIR"/gameplay_*.png 2>/dev/null | head -1)
        if [ -n "$LATEST" ]; then
            echo "[AutoTest] Using latest: $LATEST"
            SCREENSHOT_FILE="$LATEST"
        fi
    fi
else
    echo "[AutoTest] ERROR: Unity execution failed (exit code: $EXIT_CODE)"
    echo "Last 50 lines of log:"
    tail -50 "$LOG_FILE"
    exit $EXIT_CODE
fi

echo ""
echo "========================================"
echo "[AutoTest] Test completed"
echo "========================================"

# 결과 파일 경로 출력 (다음 단계에서 사용)
echo "SCREENSHOT_FILE=$SCREENSHOT_FILE"
echo "LOG_FILE=$LOG_FILE"