# 93차 스펙 — 대화 카메라 덮어쓰기 근본 수리 + U키 건설창 렌더 정지 격리 + 애니 폴리시

> 발행: 감독자 (2026-10-16). 근거: 마을씬 테스트 4.mp4(32.0s) 프레임 실측 + Editor.log 포렌식. **계획만 — 구현 별도 승인.**
> 사용자 보고: ①애니메이션 여전히 이상 ②U키 건설창 안뜸 ③대화 카메라 여전히 이상.

## 포렌식 확정 (마을씬 테스트 4 — 게임 t 1.2~71.4+ 세션, 영상=게임 t 41.0~73.4, 오프셋 41.0s 실측)

### 결함 1 — 대화 카메라 "안 줌"의 근본 원인: PlayerMovement.ApplyFollowCamera가 매 프레임 카메라를 덮어씀 (신규 발견 — 최상위)
- **증거 체인**:
  1. Begin 로그(프레이밍 적용 후 실측): `pos=(-429.8,3.2,-87.1) boundsY=1.80` — **1.8m 고정 계약 프레이밍이 정확히 적용됨**(NPC y≈2.3+0.9). bounds 비대설(92차 후속 추정)은 **폐기** — boundsY=1.80 정상.
  2. 같은 순간 영상(대화창 표시 19~22s)은 **탑다운 원거리 그대로 — 줌인 전무**(1fps 10프레임 전수 판정, NPC 크기 변화 0).
  3. 화면은 살아있음(주변 diff 0.3~0.9 — 동결 아님, 대화창 패널도 정상 렌더).
- **근본 원인**: `PlayerMovement.ApplyFollowCamera()` — 주석 자체가 "LateUpdate: **모든 스크립트 이후에 카메라를 최종 적용** — 플레이어 추적 보장". PlayerMovement.LateUpdate(Update→HandleCameraInput 339행 경유)가 **대화 중에도 매 프레임** Camera.main을 플레이어 추적 오빗 위치로 강제 → DialogueCameraDirector가 Begin/ReframeCamera에서 세팅한 프레이밍을 즉시 덮어씀. Begin 로그는 Begin 시점(덮어쓰기 전)이라 정상으로 기록 — 로그와 화면의 모순 완전 해명.
- **수리**:
  1. PlayerMovement에 `public static bool CameraFollowSuppressed` 게이트 추가 — `ApplyFollowCamera()` 선두에서 `if (CameraFollowSuppressed) return;`. [93차 F1] 마커.
  2. DialogueCameraDirector.Begin → `PlayerMovement.CameraFollowSuppressed = true`, End → false(멱등, 예외 경로 포함 finally 성격 — End는 멱등 호출 계약 유지). [93차 F1] 마커.
  3. 병사/영주 대화 경로(73차, 사선)도 동일 게이트 적용 — 대화 중 카메라 추적 일원 억제.
  4. 방어: Director의 EnsureUpdater를 LateUpdate로 전환하지 않음(게이트가 근본 수리 — 이중화 금지).

### 결함 2 — U키 건설창 "안뜸": 개방 자체는 정상, **개방 순간 게임뷰 리페인트 정지**가 체감 원인
- **증거**: 열림 로그(t=71.4) `attached=True` → 영상 30.0~32.0s(게임 71.0~73.4) **픽셀 diff 0.000 전 구간(리페인트 정지)** → 닫힘(유저가 못 본 지 2s 후 재U — Toggle→Close 스택 실측). 예외 0건, HUD 진단/게임 루프 정상 가동(열림~닫힘 사이 로그 지속) → **Update 루프는 살아있고 화면만 갱신 안 됨**.
- 참고: 영상2의 동결(11.8s)과 동일 현상군 — 영상2는 스왑+토스트 홍수 동반(당시 수리 유효), 영상4는 **대형 UTK 창 개방만으로** 재현.
- **수리(격리 우선 — 원인 확정 전 코드 추측 수정 금지)**:
  1. **재판정 장치**: ConstructionWindowUTK.Show 시 1회 `[ConstructionUTK] 개방 직후 화면 상태 — panel=(WxH) k=... windowRect=(x,y,WxH) onScreen=...` 진단 로그 추가(ApplyCanvasFit 결과 실측 — 스케일 0/오프스크린 배치 여부 즉격). [93차 F2] 마커.
  2. **방어(무해 우선)**: ApplyCanvasFit 후 k≤0.2 또는 windowRect가 패널 밖이면 기본값 폴백(중앙 배치+k=panel.min/1600 상한) — 스케일 0/오프스크린 방어. [93차 F2] 마커.
  3. **유저 협조 진단**: 재Play 시 ①U키 직후 화면 동결 여부(영상) ②에디터 설정 확인 요청 — Edit>Preferences>General: Interaction Mode / "Play Focused" 토글 상태(포커스 상실 시 루프 일시정지 후보 — 텔레그램/녹화 병행 시 유력 환경 요인). 동결이 에디터 환경이면 코드 결함 아님을 확정하는 게 이번 핵심.
  4. (동결 재발 + 환경 요인 배제 시) GameView 강제 Repaint 훅은 에디터 전용 스크립트 영역 — 별도 승인 후.

### 결함 3 — 애니 남은 결함(폴리시 — AlwaysAnimate/믹사모 전환은 성공)
- **실측**: 영상4 1~9s 64프레임 판정 **A-** — 포즈 동결 소멸(AlwaysAnimate 유효), 믹사모 클립 정상 재생. 잔여: ①미세 발 미끄러짐(이동 속도 vs 클립 스트라이드 불일치) ②방향 전환 즉시 회전(선회 보간 계수 과민).
- **수리**:
  1. 보폭-속도 정합 자동화: VillagerWanderer 첫 Walk 진입 시 1사이클(clip.length) hips 전진량 실측 → `anim.speed = 목표속도/실측속도` 보정(모델별 1회, 로그 `[VillagerWanderer] 스트라이드 정합 — model=... animSpeed=...`). 캘리브레이션 실패 시 기존 고정값(성인 1.4/girl 1.25) 유지. [93차 F3] 마커.
  2. 선회 보간: 목표 방향 회전을 Slerp 회전속도 상한(예: 360°/s)으로 완화 — 기존 선회 보간 구조 유지, 계수만 조정. [93차 F3] 마커.

## 범위 화이트리스트 (구현 시)
- Systems: PlayerMovement.cs(게이트 필드+ApplyFollowCamera 선두 가드), DialogueCameraDirector.cs(Begin/End 게이트 세팅), VillagerWanderer.cs(스트라이드 정합+선회 계수)
- UI: ConstructionWindowUTK.cs(개방 진단 로그+ApplyCanvasFit 폴백 방어)
- Tests: PlayerMovementFollowGateTests(신규 또는 인접 확장)/DialogueFeedbackTests/ConstructionWindowTests/VillageNpcFKeyTests — [93차] 마커 소스 어서션

## 검증 계획
- 컴파일 error CS 0 / EditMode baseline 11 외 0.
- Play(승인 게이트): ①대화 → **NPC 전신 정면 줌인 실제 표시**(핵심) ②병사 대화 기존 프레이밍 유지 ③U키 → 창 표시(동결 재발 여부+진단 로그) ④보행 미끄러짐/선회 체감 완화.

## 확정 사항(감독자)
- 대화 카메라는 이번이 진짜 뿌리 — ApplyFollowCamera 게이트 없이는 어떤 프레이밍 수리도 화면에 도달 불가(92차 결함3의 스왑 폐기도 필요조건이었고 이번이 충분조건).
- U키 동결은 코드 결함 단정 보류 — 진단 로그+유저 환경 확인(Play Focused/Interaction Mode)로 분리 후 판정.
- 애니는 폴리시 단계(구조 결함 소멸) — 스트라이드 자동 정합이 마지막 레버.
