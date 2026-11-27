#!/bin/bash
# run_attack_premium_probe.sh — [무번호5/프리미엄] 공격/패링 배치 진단 프로브 런
#
# 진입점: ProjectName.EditorTools.AttackPremiumProbe.RunAll (Assets/Editor/AttackPremiumProbe.cs)
# 로그:   /mnt/c/Unity/code/TestOutput/attack_premium_probe.log
# 산출:   /mnt/c/Unity/code/Screenshots/premium_player_view.png
#         /mnt/c/Unity/code/Screenshots/premium_ring_fill_early.png
#         /mnt/c/Unity/code/Screenshots/premium_ring_fill_late.png
#         /mnt/c/Unity/code/Screenshots/premium_ring_conclude.png
#         /mnt/c/Unity/code/Screenshots/premium_parry_flash.png
#
# ⚠ -quit/-nographics 금지(Play 모드 배치 계약 — run_cave146_probe.sh 사유 승계).
# 순서: 컴파일 게이트는 부모가 별도 실행(UnityLockfile 충돌 방지).

UNITY_PATH="/mnt/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe"
PROJECT_PATH="/mnt/c/Unity/code"
PROJECT_PATH_WIN="C:/Unity/code"
LOG_FILE="$PROJECT_PATH/TestOutput/attack_premium_probe.log"
ENTRY="ProjectName.EditorTools.AttackPremiumProbe.RunAll"

if [ ! -f "$UNITY_PATH" ]; then
  echo "[premium] ERROR: Unity 실행 파일 없음 — $UNITY_PATH"
  exit 1
fi

if [ -f "$PROJECT_PATH/Temp/UnityLockfile" ]; then
  echo "[premium] ERROR: Unity 에디터가 열려 있음(Temp/UnityLockfile 존재) — 에디터를 닫고 재실행"
  exit 2
fi

mkdir -p "$PROJECT_PATH/TestOutput"

echo "[premium] 프로브 배치 런: $ENTRY"
"$UNITY_PATH" \
  -batchmode \
  -projectPath "$PROJECT_PATH_WIN" \
  -executeMethod "$ENTRY" \
  -logFile "$(wslpath -w "$LOG_FILE")"
RC=$?
echo "[premium] ExitCode=$RC"
echo "[premium] 로그 산출:"
grep -E "PremiumProbe\] (MAT|CHECK|STATE|DONE|FAIL|STEP)" "$LOG_FILE" 2>/dev/null | tail -40
exit $RC
