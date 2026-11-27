#!/bin/bash
# run_farm154_probe.sh — [154차 P0] 굉이질(Tilled) 오목 "검정 렌더" 격리 프로브 배치 런
#
# 순서: 1) compile_test.sh 컴파일 게이트 → 성공 시 2) Unity batchmode -executeMethod 프로브 실행
# 진입점: ProjectName.EditorTools.Farm154HoleIsolationProbe.RunAll (Assets/Editor/Farm154HoleIsolationProbe.cs)
# 로그:   /mnt/c/Unity/code/TestOutput/farm154_probe_run.log
# 산출:   /mnt/c/Unity/code/TestOutput/farm154_isolation_1.png ~ _6.png (+ 로그 내 combo=/avgRGB= 지표)
#
# 실행 방식 근거: 153차 선례(TestOutput/qa_153_play.log COMMAND LINE)와 동일 —
#   Unity.exe -batchmode -projectPath C:/Unity/code -executeMethod ... -quit -logFile ...
#   (xvfb 불요 — Windows Unity.exe 직접 호출이며 RT+cam.Render 캡처는 GPU 렌더 직접 수행.
#    -quit 사용: Farm153PlayProbe 선례 그대로 — 일회성 에디트모드 프로브, 테스트런 아님)
# 사용법: bash /mnt/c/Unity/code/run_farm154_probe.sh

UNITY_PATH="/mnt/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe"
PROJECT_PATH="/mnt/c/Unity/code"
PROJECT_PATH_WIN="C:/Unity/code"
COMPILE_SCRIPT="$PROJECT_PATH/compile_test.sh"
LOG_FILE="$PROJECT_PATH/TestOutput/farm154_probe_run.log"
ENTRY="ProjectName.EditorTools.Farm154HoleIsolationProbe.RunAll"

if [ ! -f "$UNITY_PATH" ]; then
  echo "[farm154] ERROR: Unity 실행 파일 없음 — $UNITY_PATH"
  exit 1
fi

# 에디터 락 가드 — Unity 에디터가 열려 있으면 두 번째 인스턴스가 프로젝트를 열 수 없음
if [ -f "$PROJECT_PATH/Temp/UnityLockfile" ]; then
  echo "[farm154] ERROR: Unity 에디터가 열려 있음(Temp/UnityLockfile 존재) — 에디터를 닫고 재실행"
  exit 2
fi

mkdir -p "$PROJECT_PATH/TestOutput"

echo "[farm154] 1/2 컴파일 게이트(compile_test.sh) 실행..."
bash "$COMPILE_SCRIPT"
COMPILE_EXIT=$?
if [ $COMPILE_EXIT -ne 0 ]; then
  echo "[farm154] 컴파일 게이트 실패(exit=$COMPILE_EXIT) — 프로브 스킵. compile.log 마지막 50행:"
  tail -50 "$PROJECT_PATH/compile.log" 2>/dev/null
  exit 3
fi
echo "[farm154] 컴파일 게이트 통과."

echo "[farm154] 2/2 프로브 배치 런: $ENTRY"
"$UNITY_PATH" \
  -batchmode \
  -projectPath "$PROJECT_PATH_WIN" \
  -executeMethod "$ENTRY" \
  -quit \
  -logFile "$(wslpath -w "$LOG_FILE")"
EXIT_CODE=$?

if [ $EXIT_CODE -ne 0 ]; then
  echo "[farm154] Unity 실행 실패(exit=$EXIT_CODE) — 로그 마지막 60행:"
  tail -60 "$LOG_FILE" 2>/dev/null
  exit $EXIT_CODE
fi

echo ""
echo "[farm154] 프로브 완료 — 판정 지표(combo / avgRGB):"
grep -a "combo=.*avgRGB=" "$LOG_FILE" | sed 's/\r$//'
COUNT=$(grep -ac "combo=.*avgRGB=" "$LOG_FILE" || true)
if [ "$COUNT" != "6" ]; then
  echo "[farm154] 경고: avgRGB 지표 $COUNT/6 — 일부 콤보 실패. 로그 확인: $LOG_FILE"
fi
echo ""
echo "[farm154] 상태/진단/변인 로그(ASCII 패턴 매칭 — 인코딩 무관):"
grep -a -e "states combo=" -e "tangents=" -e "bumpMap=" -e "_NORMALMAP=" "$LOG_FILE" | sed 's/\r$//'
echo ""
echo "[farm154] 캡처 저장 확인:"
grep -a "farm154_isolation" "$LOG_FILE" | sed 's/\r$//'
echo ""
echo "[farm154] 산출물:"
ls -lh "$PROJECT_PATH"/TestOutput/farm154_isolation_*.png 2>/dev/null || echo "[farm154] 경고: PNG 산출물 없음 — 로그 확인: $LOG_FILE"
