#!/bin/bash
# run_cave156_probe.sh — [156차] 동굴 셀맵 배치 Play 프로브 런 (run_cave146_probe.sh 복제 — 로그/엔트리만 교체)
#
# 진입점: ProjectName.EditorTools.Cave156LevelProbe.RunAll (Assets/Editor/Cave156LevelProbe.cs)
# 로그:   /mnt/c/Unity/code/TestOutput/cave156_probe_run.log
# 산출:   /mnt/c/Unity/code/Screenshots/cave156_f1_player_view.png
#         /mnt/c/Unity/code/Screenshots/cave156_f{1~5}_topdown.png (+_bright)
#
# ⚠ -quit/-nographics 금지(Play 모드 배치 계약 — run_cave146_probe.sh 사유 승계).
# 순서: 컴파일 게이트는 부모가 별도 실행(UnityLockfile 충돌 방지).

UNITY_PATH="/mnt/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe"
PROJECT_PATH="/mnt/c/Unity/code"
PROJECT_PATH_WIN="C:/Unity/code"
LOG_FILE="$PROJECT_PATH/TestOutput/cave156_probe_run.log"
ENTRY="ProjectName.EditorTools.Cave156LevelProbe.RunAll"

if [ ! -f "$UNITY_PATH" ]; then
  echo "[cave156] ERROR: Unity 실행 파일 없음 — $UNITY_PATH"
  exit 1
fi

if [ -f "$PROJECT_PATH/Temp/UnityLockfile" ]; then
  echo "[cave156] ERROR: Unity 에디터가 열려 있음(Temp/UnityLockfile 존재) — 에디터를 닫고 재실행"
  exit 2
fi

mkdir -p "$PROJECT_PATH/TestOutput"

echo "[cave156] 프로브 배치 런: $ENTRY"
"$UNITY_PATH" \
  -batchmode \
  -projectPath "$PROJECT_PATH_WIN" \
  -executeMethod "$ENTRY" \
  -logFile "$(wslpath -w "$LOG_FILE")"
EXIT_CODE=$?

if [ $EXIT_CODE -ne 0 ]; then
  echo "[cave156] Unity 실행 실패(exit=$EXIT_CODE) — 로그 마지막 60행:"
  tail -60 "$LOG_FILE"
  exit $EXIT_CODE
fi

echo "[cave156] 완료 — 지표:"
grep -E 'STATE=|CHECK|CAPTURE=|DONE|FAIL' "$LOG_FILE" | tail -80
exit 0
