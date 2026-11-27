#!/bin/bash
# run_cfx_probe.sh — [무번호 16-A] 전투 FX 실카메라 검증 프로브 배치 런(Phase A 근거 수집)
# 진입점: ProjectName.EditorTools.CombatFxRealCamProbe.RunAll (Assets/Editor/CombatFxRealCamProbe.cs)
# 로그:   /mnt/c/Unity/code/TestOutput/cfx_probe.log
# 산출:   /mnt/c/Unity/code/TestOutput/cfx_*.png (실카메라 RT 캡처 6컷) + 상태 덤프(로그)
# ⚠ -quit 사용 금지(Play 모드 배치 계약 — 프로브가 스스로 EditorApplication.Exit 호출)
# ⚠ -nographics 금지(RT 캡처에 그래픽스 필요)
# ⚠ UnityLockfile 가드 — 에디터가 열려 있으면 즉시 실패(exit 2)

UNITY_PATH="/mnt/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe"
PROJECT_PATH="/mnt/c/Unity/code"
PROJECT_PATH_WIN="C:/Unity/code"
LOG_FILE_WIN="C:/Unity/code/TestOutput/cfx_probe.log"
LOG_FILE="$PROJECT_PATH/TestOutput/cfx_probe.log"
ENTRY="ProjectName.EditorTools.CombatFxRealCamProbe.RunAll"

if [ ! -f "$UNITY_PATH" ]; then
  echo "[cfx] ERROR: Unity 실행 파일 없음 — $UNITY_PATH"
  exit 1
fi

if [ -f "$PROJECT_PATH/Temp/UnityLockfile" ]; then
  echo "[cfx] ERROR: Unity 에디터가 열려 있음(Temp/UnityLockfile) — 닫고 재실행"
  exit 2
fi

mkdir -p "$PROJECT_PATH/TestOutput"
rm -f "$LOG_FILE"

echo "[cfx] 프로브 배치 런: $ENTRY"
# [무번호 16-A 수리] -batchmode 제거 — 배치 모드 실카메라 cam.Render()가 최초 스냅샷 동결 프레임을 반환하는
#   실측(4컷 md5 동일)으로 무번호 5 "녹색 단색 프레임"의 근원 확정. 윈도 모드로 라이브 렌더 캡처.
#   자동 종료(프로브 자가 Exit) — 사용자 조작 불요, Play 판정 게이트와 무관한 근거 수집.
"$UNITY_PATH" \
  -projectPath "$PROJECT_PATH_WIN" \
  -executeMethod "$ENTRY" \
  -logFile "$LOG_FILE_WIN" 2>&1

RC=$?
echo "[cfx] 배치 종료 RC=$RC"
# Play 모드 프로브는 Exit 코드로 판정 — 로그 말미 마커 병행 확인
grep -E "CFX프로브\] FINISH|===CAPTURE===|FAIL" "$LOG_FILE" | tail -12
exit $RC
