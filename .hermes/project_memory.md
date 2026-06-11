# Unity Game Project - Project Memory

## 프로젝트 정보
- **엔진**: Unity 6000.3.17f1 (Unity 6)
- **렌더러**: URP (Universal Render Pipeline)
- **언어**: C#
- **IDE**: VS Code
- **프로젝트 경로**: /mnt/c/Unity/code/
- **Windows 에디터 설치 경로**: C:\Program Files\Unity\Hub\Editor\6000.3.17f1\Editor\Unity.exe

## 디렉토리 구조
```
code/
├── Assets/
│   ├── Scenes/          # 씬 파일
│   ├── Scripts/         # C# 스크립트
│   ├── Editor/          # 에디터 스크립트
│   ├── Resources/       # 런타임 리소스
│   │   ├── Models/      # 3D 모델
│   │   ├── Textures/    # 텍스처
│   │   ├── Materials/   # 머티리얼
│   │   ├── Prefabs/     # 프리팹
│   │   ├── Audio/       # 오디오
│   │   └── UI/          # UI 에셋
│   ├── Shaders/         # 커스텀 셰이더
│   └── Animations/      # 애니메이션
├── Packages/
├── ProjectSettings/
├── Tests/
│   ├── EditMode/        # 에디터 테스트
│   └── PlayMode/        # 플레이모드 테스트
├── .hermes/             # Hermes 에이전트 설정
└── docs/                # 문서
```

## 개발 워크플로우
- **Subagent-driven dev**: delegate_task로 병렬 작업
- **TDD**: Test-First 개발 (Unity Test Framework)
- **CI**: run_tests.sh 스크립트 기반
- **Cronjob**: 반복 작업 자동화 (빌드, 테스트, QA)
- **Progresstracking**: PROGRESS.md 체크리스트

## 규칙
- 실행 전 설명 → 승인 필수
- 단일 진실: .hermes/project_memory.md
- 사장님 Telegram @dudals0714_bot (chat 6847418902)

## 초기 설정 상태
- [x] Unity Editor 6000.3.17f1 다운로드 중
- [x] 프로젝트 디렉토리 구조 생성
- [x] .gitignore, .editorconfig 설정
- [ ] Unity Editor 설치 완료
- [ ] Git 초기화
- [ ] VS Code 워크스페이스 설정
- [ ] 첫 번째 씬 생성