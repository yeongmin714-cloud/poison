#!/bin/bash
# run_tests.sh — Unity EditMode/PlayMode test runner
# Usage: ./run_tests.sh [editmode|playmode|all]
#
# [2026-10-15 수리] QA 발견 결함 2건 수정:
#  a) 로그 경로: Unity.exe(Windows 프로세스)는 -logFile/-testResults 경로를 Windows 규약으로 해석한다.
#     기존 "/tmp/..."는 C:\tmp\...에 기록되어 WSL /tmp grep이 항상 실패 → 실패 런도 "All passed" 오탐.
#     프로젝트 내부 경로(C:\Unity\code\Temp\... = WSL /mnt/c/Unity/code/Temp\...)로 통일 — 양쪽에서 동일 파일 참조.
#  b) 플래그: 레거시 -runEditorTests/-editorTestsResultFile 폐기 — 실측 성공 조합인
#     -runTests -testPlatform EditMode(+ -testResults)로 교체. PlayMode는 -testPlatform PlayMode.
#  판정: Unity 종료코드는 콘솔 파이프(tail)로 마스킹되므로 로그/결과 XML 파싱으로 실제 결과를 판정한다.
#     컴파일 에러(error CS)·컴파일 실패·batchmode 어보트·"테스트 0실행"·판정 근거 부재는 모두 실패 처리 — 오탐 통과 원천 차단.

set -e

UNITY="/mnt/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe"
PROJECT="C:/Unity/code"
# Unity(Windows 프로세스) 인자용 경로와 WSL grep/검증용 경로 — 서로 동일한 물리 파일을 가리킨다.
LOG_FILE="C:/Unity/code/Temp/testlog.txt"
LOG_WSL="/mnt/c/Unity/code/Temp/testlog.txt"
RESULTS_WIN="C:/Unity/code/Temp"
RESULTS_WSL="/mnt/c/Unity/code/Temp"

MODE="${1:-all}"

# Temp 디렉터리 보장 (Unity 프로젝트면 보통 존재, 방어적 생성)
mkdir -p "$RESULTS_WSL"

echo "=== Unity Test Runner ==="
echo "Project: $PROJECT"
echo "Mode: $MODE"
echo ""

# judge <플랫폼명> <결과XML WSL 경로>
# 판정 우선순위: ①컴파일 에러 ②컴파일 실패/어보트 ③결과 XML ④로그 완료 마커 — 근거 없으면 실패.
judge() {
    local platform="$1"
    local xml="$2"
    local cs_count

    if [ ! -f "$LOG_WSL" ]; then
        echo "❌ $platform: Unity 로그 부재($LOG_WSL) — Unity 실행 자체 실패로 판정"
        return 1
    fi

    cs_count=$(grep -c "error CS" "$LOG_WSL" || true)
    if [ "${cs_count:-0}" -gt 0 ]; then
        echo "❌ $platform: 컴파일 에러 ${cs_count}건 — 테스트 실행 전 중단"
        grep "error CS" "$LOG_WSL" | sort -u | head -10
        return 1
    fi
    if grep -q "Scripts have compiler errors" "$LOG_WSL"; then
        echo "❌ $platform: 스크립트 컴파일 실패 — 테스트 실행 전 중단"
        return 1
    fi
    if grep -q "Aborting batchmode" "$LOG_WSL"; then
        echo "❌ $platform: batchmode 어보트 감지"
        return 1
    fi

    # 결과 XML(최정밀 판정) — 루트 <test-run>의 result/passed/failed 속성 파싱
    if [ -f "$xml" ]; then
        local result_attr passed failed
        result_attr=$(head -c 4000 "$xml" | tr -d '\r' | grep -o 'result="[^"]*"' | head -1 | cut -d'"' -f2)
        passed=$(head -c 4000 "$xml" | tr -d '\r' | grep -o 'passed="[0-9]*"' | head -1 | tr -dc '0-9')
        failed=$(head -c 4000 "$xml" | tr -d '\r' | grep -o 'failed="[0-9]*"' | head -1 | tr -dc '0-9')
        passed=${passed:-0}
        failed=${failed:-0}
        if [ "$failed" -gt 0 ]; then
            echo "❌ $platform: 테스트 실패 — result=$result_attr, passed=$passed, failed=$failed"
            return 1
        fi
        case "$result_attr" in
            Passed*)
                echo "✅ $platform: 테스트 통과 — result=$result_attr, passed=$passed, failed=$failed"
                return 0
                ;;
            *)
                echo "❌ $platform: 미통과 판정 — result=$result_attr, passed=$passed, failed=$failed"
                return 1
                ;;
        esac
    fi

    # XML 부재 시 로그 완료 마커로 폴백 판정 (실측 마커: "Test run completed. Exiting with code N (...)")
    if grep -q "No tests were executed" "$LOG_WSL"; then
        echo "❌ $platform: 실행된 테스트가 없음(No tests were executed) — 통과로 인정 불가"
        return 1
    fi
    if grep -q "Test run completed. Exiting with code 2 (Failed)" "$LOG_WSL"; then
        echo "❌ $platform: 테스트 실패(로그 마커: code 2 Failed)"
        grep "Test run completed" "$LOG_WSL" | head -2
        return 1
    fi
    if grep -q "Test run completed. Exiting with code 0 (Ok). Run completed." "$LOG_WSL"; then
        echo "✅ $platform: 테스트 통과(로그 마커: code 0 Ok)"
        return 0
    fi

    echo "❌ $platform: 판정 근거 없음(결과 XML·완료 마커 모두 부재) — 실패 처리"
    return 1
}

run_editmode() {
    echo "--- EditMode Tests ---"
    "$UNITY" -batchmode \
        -projectPath "$PROJECT" \
        -runTests \
        -testPlatform EditMode \
        -testResults "$RESULTS_WIN/unity-editmode-results.xml" \
        -logFile "$LOG_FILE" 2>&1 | tail -20

    judge "EditMode" "$RESULTS_WSL/unity-editmode-results.xml"
}

run_playmode() {
    echo "--- PlayMode Tests ---"
    "$UNITY" -batchmode \
        -projectPath "$PROJECT" \
        -runTests \
        -testPlatform PlayMode \
        -testResults "$RESULTS_WIN/unity-playmode-results.xml" \
        -logFile "$LOG_FILE" 2>&1 | tail -20

    judge "PlayMode" "$RESULTS_WSL/unity-playmode-results.xml"
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
