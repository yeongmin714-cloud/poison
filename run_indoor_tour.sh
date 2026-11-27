#!/bin/bash
# run_indoor_tour.sh — [IndoorTour] 실내 전 씬 투어 스크린샷+미니맵 평면도 배치 런
#
# 진입점: ProjectName.EditorTools.IndoorTourProbe.RunAll (Assets/Editor/IndoorTourProbe.cs)
# 로그:   /mnt/c/Unity/code/TestOutput/indoor_tour_run.log
# 산출:   /mnt/c/Unity/code/Screenshots/tour_<label>_view.png     (플레이어/카메라 뷰 RT 1024x768)
#         /mnt/c/Unity/code/Screenshots/tour_<label>_minimap.png  (IndoorMinimapBaker.LatestTexture 그대로)
# 라벨:   cave_f1 cave_f2 house castle_player castle_lord shop restaurant barn
#         crafthouse church arena arenapit afterlife tavern
#
# ⚠ -quit 사용 금지(Play 모드 배치 계약 — 프로브가 스스로 EditorApplication.Exit 호출, Cave146 선례 동일)
# ⚠ -nographics 금지(RT+cam.Render 캡처에 그래픽스 필요)
# ⚠ UnityLockfile 가드 — 에디터가 열려 있으면 즉시 실패(exit 2)
# ⚠ 컴파일 게이트는 본 스크립트에 미포함 — 부모가 compile_test.sh 별도 실행(Cave146 선례 동일)
#
# 사용법: bash /mnt/c/Unity/code/run_indoor_tour.sh

UNITY_PATH="/mnt/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe"
PROJECT_PATH="/mnt/c/Unity/code"
PROJECT_PATH_WIN="C:/Unity/code"
LOG_FILE="$PROJECT_PATH/TestOutput/indoor_tour_run.log"
ENTRY="ProjectName.EditorTools.IndoorTourProbe.RunAll"

if [ ! -f "$UNITY_PATH" ]; then
  echo "[indoor_tour] ERROR: Unity 실행 파일 없음 — $UNITY_PATH"
  exit 1
fi

if [ -f "$PROJECT_PATH/Temp/UnityLockfile" ]; then
  echo "[indoor_tour] ERROR: Unity 에디터가 열려 있음(Temp/UnityLockfile) — 닫고 재실행"
  exit 2
fi

mkdir -p "$PROJECT_PATH/TestOutput"
rm -f "$LOG_FILE"

echo "[indoor_tour] 프로브 배치 런: $ENTRY"
"$UNITY_PATH" \
  -batchmode \
  -projectPath "$PROJECT_PATH_WIN" \
  -executeMethod "$ENTRY" \
  -logFile "$(wslpath -w "$LOG_FILE")"
EXIT_CODE=$?

if [ $EXIT_CODE -ne 0 ]; then
  echo "[indoor_tour] Unity 실행 실패(exit=$EXIT_CODE) — 로그 마지막 80행:"
  tail -80 "$LOG_FILE" 2>/dev/null
  exit $EXIT_CODE
fi

echo ""
echo "[indoor_tour] 프로브 완료 — STATE/CHECK/CAPTURE/TOUR 지표:"
grep -a -e "STATE" -e "CHECK" -e "CAPTURE" -e "DONE" -e "FAIL" -e "bounds" "$LOG_FILE" | sed 's/\r$//'
echo ""
echo "[indoor_tour] bounds_changed 판정 요약(CHECK만):"
grep -a "CHECK" "$LOG_FILE" | sed 's/\r$//'
echo ""
echo "[indoor_tour] 산출물:"
ls -lh "$PROJECT_PATH"/Screenshots/tour_*.png 2>/dev/null || echo "[indoor_tour] 경고: PNG 산출물 없음 — 로그 확인: $LOG_FILE"
