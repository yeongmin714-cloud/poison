# 🏗️ Windows 빌드 방법 — BUILD_INSTRUCTIONS.md

## 요구 사항

- **Unity 6000.4.10f1** 설치 (Unity Hub)
- **Windows Standalone** 모듈 설치
- 프로젝트 경로: `/mnt/c/Unity/code/` (Windows: `C:\Unity\code\`)

## 빌드 방법

### 방법 1: Unity Editor 메뉴

1. Unity Editor에서 프로젝트 열기
2. 상단 메뉴 → **Tools → Build Windows Standalone**
3. 빌드 완료 시 `Builds/Windows/Game.exe` 생성

### 방법 2: 명령줄 (batchmode)

```powershell
# PowerShell (관리자 실행)
& "C:\Program Files\Unity\Hub\Editor\6000.4.10f1\Editor\Unity.exe" -quit -batchmode -projectPath "C:\Unity\code" -executeMethod BuildTools.BuildWindows -logFile "build.log"
```

또는 WSL/bash에서:
```bash
"/mnt/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe" -quit -batchmode -projectPath "/mnt/c/Unity/code" -executeMethod BuildTools.BuildWindows -logFile "build.log"
```

## 빌드에 포함되는 씬

| 씬 | 설명 |
|-----|------|
| `MainScene.unity` | 메인 게임 월드 (85KB) |
| `IndoorScene.unity` | 실내 맵 (4KB) |
| `TopDownScene.unity` | 탑다운 오버월드 (6.8MB) |
| `WorldMap.unity` | 월드맵 (2.1MB) |

## 출력

- **경로:** `Builds/Windows/Game.exe`
- **용량:** 약 40MB (과거 빌드 기준)
- **설치:** 전체 `Builds/Windows/` 폴더를 대상 PC에 복사 후 `Game.exe` 실행

## 테스트 실행

```bash
# 모든 EditMode/PlayMode 테스트 실행
./run_tests.sh all

# EditMode 테스트만
./run_tests.sh editmode

# PlayMode 테스트만
./run_tests.sh playmode
```