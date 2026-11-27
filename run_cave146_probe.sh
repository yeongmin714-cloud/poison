#!/bin/bash
# run_cave146_probe.sh — [146차 P0] 동굴 던전 1층→2층 하강 "플레이어 외부 판정" 런타임 근거 수집 프로브 배치 런
#
# 진입점: ProjectName.EditorTools.Cave146LevelProbe.RunAll (Assets/Editor/Cave146LevelProbe.cs)
# 로그:   /mnt/c/Unity/code/TestOutput/cave146_probe_run.log
# 산출:   /mnt/c/Unity/code/Screenshots/cave146_1_f1_portal.png
#         /mnt/c/Unity/code/Screenshots/cave146_2_f2_player_view.png
#         /mnt/c/Unity/code/Screenshots/cave146_3_f2_topdown.png
#         /mnt/c/Unity/code/Screenshots/cave146_4_f2_topdown_bright.png
#         (+ 로그 내 STATE=/CHECK=/CAPTURE=/DONE/FAIL 지표)
#
# ⚠ -quit 사용 금지(Play 모드 배치 계약 — run_farm154_probe.sh와의 의도적 차이):
#   -executeMethod 복귀 즉시 -quit가 프로세스를 종료해 Play 모드 펌프/런너가 소멸한다.
#   본 프로브는 스스로 EditorApplication.Exit(0/1)로 종료한다(LordDaughterPlayVerify 126차 Play 선례 동일).
#   따라서 무응답 시 무한 대기 방지용 전체 감시 타임아웃(180s)이 프로브 내부에 있다.
#   (-nographics도 금지 — RT+cam.Render 캡처에 그래픽스 필요)
#
# 순서: compile_test.sh 컴파일 게이트는 이 스크립트에서 제외(부모가 별도 실행 — UnityLockfile 충돌 방지).
#   본 스크립트는 프로브 배치 런만 수행한다.
# 사용법: bash /mnt/c/Unity/code/run_cave146_probe.sh

UNITY_PATH="/mnt/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe"
PROJECT_PATH="/mnt/c/Unity/code"
PROJECT_PATH_WIN="C:/Unity/code"
LOG_FILE="$PROJECT_PATH/TestOutput/cave146_probe_run.log"
ENTRY="ProjectName.EditorTools.Cave146LevelProbe.RunAll"

if [ ! -f "$UNITY_PATH" ]; then
  echo "[cave146] ERROR: Unity 실행 파일 없음 — $UNITY_PATH"
  exit 1
fi

# 에디터 락 가드 — Unity 에디터가 열려 있으면 두 번째 인스턴스가 프로젝트를 열 수 없음
if [ -f "$PROJECT_PATH/Temp/UnityLockfile" ]; then
  echo "[cave146] ERROR: Unity 에디터가 열려 있음(Temp/UnityLockfile 존재) — 에디터를 닫고 재실행"
  exit 2
fi

mkdir -p "$PROJECT_PATH/TestOutput"

echo "[cave146] 프로브 배치 런: $ENTRY"
"$UNITY_PATH" \
  -batchmode \
  -projectPath "$PROJECT_PATH_WIN" \
  -executeMethod "$ENTRY" \
  -logFile "$(wslpath -w "$LOG_FILE")"
EXIT_CODE=$?

if [ $EXIT_CODE -ne 0 ]; then
  echo "[cave146] Unity 실행 실패(exit=$EXIT_CODE) — 로그 마지막 60행:"
  tail -60 "$LOG_FILE" 2>/dev/null
  exit $EXIT_CODE
fi

echo ""
echo "[cave146] 프로브 완료 — 상태/판정/캡처 지표(ASCII 패턴 매칭 — 인코딩 무관):"
grep -a -e "STATE" -e "CHECK" -e "CAPTURE" -e "DONE" -e "FAIL" "$LOG_FILE" | sed 's/\r$//'
echo ""
echo "[cave146] 판정 요약(CHECK만):"
grep -a "CHECK" "$LOG_FILE" | sed 's/\r$//'
echo ""
echo "[cave146] 산출물:"
ls -lh "$PROJECT_PATH"/Screenshots/cave146_*.png 2>/dev/null || echo "[cave146] 경고: PNG 산출물 없음 — 로그 확인: $LOG_FILE"
