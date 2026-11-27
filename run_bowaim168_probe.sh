#!/bin/bash
# run_bowaim168_probe.sh — [168-P2] 활 조준 정합 "커서 수식 vs 리티클 수식" 패널좌표 차이 진단 프로브 배치 런
#
# 순서: 1) compile_test.sh 컴파일 게이트 → 성공 시 2) Unity batchmode -executeMethod 프로브 실행
# 진입점: ProjectName.EditorTools.BowAimDiagProbe.RunAll (Assets/Editor/BowAimDiagProbe.cs)
# 로그:   /mnt/c/Unity/code/TestOutput/bowaim168_probe.log
# 산출:   로그 내 CHECK=/Δ= 지표 + DONE/FAIL (Play 모드/캡처 불요 — 에디트모드 계산 프로브)
#
# 실행 방식 근거: run_farm154_probe.sh 선례 1:1 —
#   Unity.exe -batchmode -projectPath C:/Unity/code -executeMethod ... -quit -logFile ...
#   (-quit 사용: Farm154 선례 그대로 — 일회성 에디트모드 프로브, 테스트런 아님. 프로브가 스스로
#    EditorApplication.Exit(0/1)로도 종료하므로 이중 안전.)
# 사용법: bash /mnt/c/Unity/code/run_bowaim168_probe.sh

UNITY_PATH="/mnt/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe"
PROJECT_PATH="/mnt/c/Unity/code"
PROJECT_PATH_WIN="C:/Unity/code"
COMPILE_SCRIPT="$PROJECT_PATH/compile_test.sh"
LOG_FILE="$PROJECT_PATH/TestOutput/bowaim168_probe_run.log"
PROBE_LOG="$PROJECT_PATH/TestOutput/bowaim168_probe.log"
ENTRY="ProjectName.EditorTools.BowAimDiagProbe.RunAll"

if [ ! -f "$UNITY_PATH" ]; then
  echo "[bowaim168] ERROR: Unity 실행 파일 없음 — $UNITY_PATH"
  exit 1
fi

# 에디터 락 가드 — Unity 에디터가 열려 있으면 두 번째 인스턴스가 프로젝트를 열 수 없음
if [ -f "$PROJECT_PATH/Temp/UnityLockfile" ]; then
  echo "[bowaim168] ERROR: Unity 에디터가 열려 있음(Temp/UnityLockfile 존재) — 에디터를 닫고 재실행"
  exit 2
fi

mkdir -p "$PROJECT_PATH/TestOutput"
rm -f "$LOG_FILE" "$PROBE_LOG"

echo "[bowaim168] 1/2 컴파일 게이트(compile_test.sh) 실행..."
bash "$COMPILE_SCRIPT"
COMPILE_EXIT=$?
if [ $COMPILE_EXIT -ne 0 ]; then
  echo "[bowaim168] 컴파일 게이트 실패(exit=$COMPILE_EXIT) — 프로브 스킵. compile.log 마지막 50행:"
  tail -50 "$PROJECT_PATH/compile.log" 2>/dev/null
  exit 3
fi
echo "[bowaim168] 컴파일 게이트 통과."

echo "[bowaim168] 2/2 프로브 배치 런: $ENTRY"
"$UNITY_PATH" \
  -batchmode \
  -projectPath "$PROJECT_PATH_WIN" \
  -executeMethod "$ENTRY" \
  -quit \
  -logFile "$(wslpath -w "$LOG_FILE")"
EXIT_CODE=$?

if [ $EXIT_CODE -ne 0 ]; then
  echo "[bowaim168] Unity 실행 실패(exit=$EXIT_CODE) — 로그 마지막 60행:"
  tail -60 "$LOG_FILE" 2>/dev/null
  exit $EXIT_CODE
fi

echo ""
echo "[bowaim168] 프로브 완료 — CHECK/Δ 지표(ASCII 패턴 매칭 — 인코딩 무관):"
grep -a -e "CHECK=" -e "Δ=" -e "DONE" -e "FAIL" "$PROBE_LOG" 2>/dev/null | sed 's/\r$//'
if [ ! -f "$PROBE_LOG" ]; then
  echo "[bowaim168] 경고: 프로브 로그($PROBE_LOG) 없음 — 메인 로그 확인: $LOG_FILE"
  grep -a -e "BowAimDiagProbe" -e "FAIL" "$LOG_FILE" | tail -30
fi
