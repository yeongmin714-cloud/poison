# Fluent 링 + 게이지 시계방향 소모 + 이동 끊김 수정 계획 (2026-10-11, 14차 예정)

> **게이트: 계획 전용** — 사용자 "전체적으로 수정 계획 세워줘" → 본 문서 저장까지만 수행. 소스/테스트 수정 없음. 승인 후 Phase별 실행.

## 0. 사용자 요구 (4건)

1. **링 Fluent화** — 링 크기·두께는 현재가 좋았으니 유지하고, 게임 UI(UTKTheme GitHub-dark Fluent: 패널 #161B22/스트로크 #2E343D/액센트 #58A6FF/골드 #E3B341) 분위기에 맞게 비주얼 개선
2. **이동 링 색상** — 골드(노랑) → **파란색**
3. **이동 포인트 링 크기** — 현행의 **절반** (현행 = 원래의 1/2로 이미 줄어든 상태 → 이동 마커만 추가 절반)
4. **이동 끊김 수리** — 여전히 이동이 끊기는 문제
5. **HUD 게이지 소모 방식** — 체력바/스태미너바가 색상으로 꽉 채워졌다가 **소모 시 시계방향으로 색상이 빠지는** 방식이어야 함

## 1. 근본원인 분석

### A. HUD 게이지 소모 방향 (소스 확정)
- 배선은 정상: `HUDUTK` 250ms 폴링 → `hpRatio/stRatio → UTKCircularGauge.Fraction` (값 미연결 아님)
- **방향 불일치(확정)**: `UTKCircularGauge.WriteRing` — 12시(-90°) 고정 앵커에서 시계방향으로 `360×fraction` 채움 → 값이 감소하면 **공백이 12시 기준 반시계 방향으로 확대**(색 끝이 반시계로 후퇴). 사용자 요구는 반대: **소비된 색이 시계방향으로 빠져나감**(공백이 12시부터 시계방향으로 성장, 채움의 시작 에지가 시계방향으로 이동하고 끝은 12시에 고정)
- 영향 소비처 2곳: `HUDUTK`(HP/스태미나) + `BowAimReticleUTK`(파워 링 — P22-5 기본값 보호 계약 존재)

### B. 이동 끊김 (소스 확정 — 애니메이션 이중 구동 충돌)
병사 위치 이동은 teleport식(`transform.position`, RB 없음)이라 collider 막힘은 원인 아님. "끊김"의 실체 후보 2건 모두 애니메이션 계열:

1. **`RigAnimationController.SetState` 전환 재시작 루프(확정)** — `StepToward`가 매 프레임 `_rigAnim.SetState(AnimationState.Walk)` 호출. SetState는 비트리거 상태 재호출 시 `newState == _currentState && !IsTransitioning`일 때만 early-return → **전환 코루틴 진행 중(IsTransitioning)에는 매 프레임 StopCoroutine+StartCoroutine으로 크로스페이드를 재시작** → 전환완료가 사실상 불가능 → 걷기 애니가 뚝뚝 끊김/멈춤. (병사는 TerritoryBuilder가 `Soldier_AC`+`RigAnimationController`를 함께 부착 — 컨트롤러 있으므로 SetState가 실동작)
2. **`HumanoidClipDriver.UpdateSoldier` raw 속도(확정)** — Soldier 모드는 `delta.magnitude / dt` **스무딩 없이** Speed에 직접 대입(Player 모드의 "지수 평활로 끊김 제거" 패턴 미적용). delta는 **y 포함** — ①지형 y스냅(`TryGetGroundY` 매 스텝 직대입)의 급변 ②SeparationSystem 밀기(0.1s±0.02m)가 속도에 노이즈로 합산 → Idle/Walk/Run 블렌드 스래싱.

보조: 이전 13차의 stuck 게이트(0.6s)는 teleport 이동에서 발화 조건이 거의 없어 후순위 — Phase S0 관측 로그로 부정/긍정 확인.

### C. 링 Fluent화 (현황)
- 선택 링은 이미 `#58A6FF`(Fluent 액센트와 동일). 현재 셰이더 링 = 액센트 밴드 + 소프트 글로우만 — HUD의 GitHub-dark 언어(다크 패널 바탕 + 크리스프 스트로크 + 절제된 광)와 톤 차이
- 이동 링 = `MoveTargetColor` 골드(1, 0.82, 0.35) → 파랑으로 변경 필요
- 이동 지속 마커 = `BaseScale 0.95` — 추가 절반(≈0.475, 타원 ~0.87×0.26m) 필요. 공격 페이드 마커는 현행 유지(별도 항목 아님)

## 2. Acceptance Criteria

- AC1: HUD HP/스태미나 게이지 — 만원 상태에서 소모 시 **공백이 12시 방향부터 시계방향으로 성장**(채움 시작 에지가 시계방향 이동, 끝 12시 고정). BowAimReticle 파워 링은 픽셀 무변경(opt-in 보호)
- AC2: 월드 링 3종이 Fluent 톤 — 액센트 스트로크 + 다크 패널 백킹 밴드 + 절제된 글로우(현재 크기·두께 유지)
- AC3: 이동 지속 마커 = 파란색 + 현행 지름의 절반. 선택 링/타겟 링 크기·두께 현행 유지
- AC4: RTS 이동 중 걷기 애니가 끊김 없이 연속 재생(크로스페이드 재시작 루프 소멸 + Speed 스무딩)
- AC5: 기존 계약 보존 — 무콜라이더/그림자 OFF/3색 인스턴스 머티리얼/드래그 박스 무변경/게이지 시맨틱 색(HP 레드·스타미나 옐로) 유지

## 3. Phase 구성

### Phase G1 — HUD 게이지 시계방향 소모 (UTKCircularGauge)
- `UTKCircularGauge`에 opt-in 속성 `DrainClockwise`(기본 false — BowAimReticle 픽셀 무변경 보호) 신설
- `WriteRing`/`WriteTip`/글로우 패스: DrainClockwise=true면 호를 `[−90° + 360°×(1−f), −90°+360°]` 구간으로 기록(시작 에지 = 소모 경계, 끝 = 12시 wrap). tip은 소모 경계(시작 에지)에 배치
- `HUDUTK.BuildOneGauge`에서 `DrainClockwise = true` 배선(HP/스태미나 양쪽)
- 테스트: 신규 계약 — 속성 배선 소스 단정 + WriteRing 각도 수식 단정(순수 메서드 분리 시 EditMode 검증 가능하도록 각도 계산 헬퍼 추출 권장) + BowAimReticle 기본값 false 회귀
- 게이트: 컴파일 0 → focused(UI/게이지) → **Play 캡처(사용자)** — HP 깎아보며 시계방향 소모 확인

### Phase G2 — 링 Fluent화 + 이동 링 파랑 + 마커 절반 (셰이더/프레젠테이션)
- **`Custom/SelectionRing.shader` 확장**: 액센트 밴드 아래에 **다크 백킹 밴드**(패널 톤 #161B22 계열, 알파 ~0.5, 액센트 밴드보다 ~1.6× 폭) 추가 — Fluent "다크 크롬 위 액센트 스트로크" 언어. 글로우 축소(`GlowWidth` 공유 상수 0.08→0.04) + 펄스 진폭 소폭 축소(PulseAmplitude 0.06→0.04)
- **이동 링 파랑**: `CommandRingPresentation.MoveTargetColor` = `#58A6FF`(액센트)로 변경. 선택 링과 동일색이 되므로 구분은 크기(절반)+백킹 대비로 담당(사용자 명시 요구 우선)
- **이동 마커 절반**: `CommandRingPresentation.MoveMarkerScale = BaseScale × 0.5` 신설 + `CommandMarker.Spawn(position, color, owner, scaleOverride)` 오버로드 — `RTSCommandSystem.TrackMoveMark`가 지속 마커에만 적용. 공격 페이드 마커는 BaseScale 유지
- 크기·두께(현재 승인값 ShaderRingRadius 0.78/Thickness 0.14/BaseScale 0.95) 불변
- 테스트: 상수 계약 갱신(MoveTargetColor/MoveMarkerScale) + 셰이더 백킹 속성 단정 + 기존 RTSRingVisualTests 회귀
- 게이트: 컴파일 0 → focused(링) → **Play 캡처(사용자)** — Fluent 톤/파랑/절반 체감

### Phase G3 — 이동 끊김 수리 (애니 이중 구동 봉합)
- **`RigAnimationController.SetState` 멱등화**: 전환 코루틴의 목표 상태 추적 필드(`_transitionTargetState`) 추가 — `newState == _currentState && 목표 상태도 동일`이면 IsTransitioning 여부와 무관하게 early-return(매 프레임 재시작 루프 소멸). 트리거 상태(Jump/Attack 등) 재발화 경로 불변
- **`HumanoidClipDriver.UpdateSoldier` 스무딩**: delta를 **XZ 전용**으로 계산(지형 y스냅 노이즈 제외) + Player 모드의 지수 평활 패턴 이식(동일 상수/클램프 — "끊김 제거" 주석의 원 패턴). 필요 시 멈춤 스냅 임계 재활용
- **관측(부수)**: stuck 게이트 발화 시 Debug.Log 유지(Phase S0 역할) — Play 로그로 stuck 게이트 오탈 여부 판정, 오탈이면 기준 완화(1.0s) 또는 제거
- 테스트: 순수 로직 분리 — ①SetState 동일상태 재호출 시 코루틴 재시작 없음(리플렉션/카운터 프레임워크 수준의 소스 계약) ②UpdateSoldier 속도 계산 XZ 전용+평활 단정(더미 프레임 시퀀스)
- 게이트: 컴파일 0 → focused(애니/RTS) → **Play(사용자)** — 부대 이동 중 애니 연속성 실측

### Phase G4 — 기록/마무리
- QAPROGRESS 14차 기록, 계약 갱신 요약, (명시 시에만) stage/commit/push

## 4. 사용자 확정 필요 사항(기본안 자동 적용, 변경 원하면 지정)

- **D3 게이지 방식**: opt-in 속성(HUD만 시계방향, 리티클 보호) — 기본안. 전역 플립 원하면 지정
- **D4 이동 링 파랑톤**: 선택 링과 동일 액센트 #58A6FF — 기본안. 선택보다 연한/다른 파랑 원하면 지정(예: #79B8FF)
- **D5 백킹 밴드 강도**: 알파 0.5 기본 — Play 캡처 후 과하면 1줄 튜닝

## 5. 영향 파일 목록(예상)

- `Assets/Scripts/UI/Toolkit/UTKCircularGauge.cs` (DrainClockwise + 각도 수식)
- `Assets/Scripts/UI/Toolkit/HUDUTK.cs` (배선 1줄)
- `Assets/Resources/FX/Selection/SelectionRing.shader` (백킹 밴드/글로우)
- `Assets/Scripts/Systems/CommandRingPresentation.cs` (MoveTargetColor/MoveMarkerScale/상수)
- `Assets/Scripts/Systems/CommandMarker.cs` (scale 오버로드)
- `Assets/Scripts/Systems/RTSCommandSystem.cs` (TrackMoveMark 스케일 전달)
- `Assets/Scripts/Systems/RigAnimationController.cs` (SetState 멱등화)
- `Assets/Scripts/Systems/HumanoidClipDriver.cs` (UpdateSoldier XZ+평활)
- 테스트: 게이지/링/애니 계약 신설·갱신 (Tests/EditMode)
