#!/bin/bash
# run_tests.sh — Unity EditMode/PlayMode test runner
# Usage: ./run_tests.sh [editmode|playmode|all]

set -e

UNITY="/mnt/c/Program Files/Unity/Hub/Editor/6000.3.17f1/Editor/Unity.exe"
PROJECT="/mnt/c/Unity/code"
LOG_FILE="/tmp/unity-test-log.txt"

MODE="${1:-all}"

echo "=== Unity Test Runner ==="
echo "Project: $PROJECT"
echo "Mode: $MODE"
echo ""

run_editmode() {
    echo "--- EditMode Tests ---"
    "$UNITY" -quit -batchmode \
        -projectPath "$PROJECT" \
        -runEditorTests \
        -editorTestsResultFile "/tmp/unity-editmode-results.xml" \
        -logFile "$LOG_FILE" 2>&1 | tail -20

    if grep -q "FAILED" "$LOG_FILE" 2>/dev/null; then
        echo "❌ EditMode tests FAILED"
        grep -A5 "FAILED" "$LOG_FILE" 2>/dev/null
        return 1
    fi
    echo "✅ EditMode tests passed"
    return 0
}

run_playmode() {
    echo "--- PlayMode Tests ---"
    "$UNITY" -quit -batchmode \
        -projectPath "$PROJECT" \
        -runTests \
        -testPlatform PlayMode \
        -testResults "/tmp/unity-playmode-results.xml" \
        -logFile "$LOG_FILE" 2>&1 | tail -20

    if grep -q "FAILED" "$LOG_FILE" 2>/dev/null; then
        echo "❌ PlayMode tests FAILED"
        grep -A5 "FAILED" "$LOG_FILE" 2>/dev/null
        return 1
    fi
    echo "✅ PlayMode tests passed"
    return 0
}

EXIT_CODE=0

if [ "$MODE" = "editmode" ] || [ "$MODE" = "all" ]; then
    run_editmode || EXIT_CODE=$?
fi

if [ "$MODE" = "playmode" ] || [ "$MODE" = "all" ]; then
    run_playmode || EXIT_CODE=$?
fi

echo ""
if [ "$EXIT_CODE" -eq 0 ]; then
    echo "🎉 All tests passed!"
else
    echo "💥 Some tests failed (exit code: $EXIT_CODE)"
fi

exit $EXIT_CODE