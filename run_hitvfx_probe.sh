#!/bin/bash
# [11-16 사용자 요청] 피격 VFX 실카메라 캡처 프로브 러너 — run_cfx_probe.sh 선례 1:1
# 계약: -quit/-nographics 금지(Play 모드 배치), -logFile은 Windows식 경로(C:/...)
set -u
UNITY_EXE="C:/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe"
PROJECT="C:/Unity/code"
LOG_FILE_WIN="C:/Unity/code/TestOutput/hitvfx_probe.log"
ENTRY="ProjectName.EditorTools.HitVfxTelegramProbe.RunAll"
mkdir -p /mnt/c/Unity/code/TestOutput
# 락 가드 — 에디터 실행 중이면 충돌
if [ -f /mnt/c/Unity/code/Temp/UnityLockfile ]; then
  echo "LOCK: UnityLockfile 존재 — 에디터 실행 중, 중단"
  exit 2
fi
"/mnt/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe" \
  -projectPath "$PROJECT" \
  -executeMethod "$ENTRY" \
  -logFile "$LOG_FILE_WIN"
echo "=== EXIT $? ==="
