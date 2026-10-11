# RTS 링 비주얼 + 이동 정확도 수정 계획 (2026-10-11)

> **게이트: 계획 전용** — 사용자 "원인 분석하고 전체적으로 수정 계획을 세워줘" → 본 문서 저장까지만 수행. 소스/테스트 수정 없음. 승인 후 Phase별 실행.

## 0. 사용자 요구 (rts 1.PNG 실측 반영)

1. 부대 지정 시 병사 발밑에는 **파란 링만** 있어야 하는데 **검은색 사각형**도 함께 떠서 이상함 → 제거
2. **몬스터(공격 타겟) 링과 이동 위치 표시 링도 동일 증상** → 함께 수정
3. 링 지름 = 현재의 **절반**
4. 링 두께 = 현재보다 **조금 더 두껍게**
5. 이동 명령 시 병사가 이동은 하지만 **지정한 포인트에 정확히 도착하지 않음** → 원인 분석 + 수정

### 스크린샷 픽셀 실측 (Screenshots/rts 1.PNG, 1240×784)
- 선택 병사 발밑: 얇은 파란 타원 링 + 그보다 **큰 검은 직사각형**(날카로운 모서리, 좌우로 돌출) 동시 렌더
- 검은 사각형 폭 ~194px, 하단 에지 y≈617에서 급절단(직사각형 확정 — 타원/디스크 아님)
- 종횡비 ≈ 3.34:1 ≈ 링 쿼드 3.47:1.04(CommandRingPresentation.BaseScale 1.9 × FigmaCoreRingAspect 400:120)와 일치 → **검은 사각형 = 링 쿼드 자체**

## 1. 근본원인 분석

### A. 검은 사각형 — 세 링 공통 체인의 이중 결함

세 링(선택 파랑 `GuardSelectionManager`/공격타겟 빨강 `AttackTargetRingManager`/지속 이동마커 골드 `RTSCommandSystem.TrackMoveMark`)이 **전부 같은 시각 체인**으로 수렴:
`SelectionRingController`/`CommandMarker.SetupPersistent` → `CommandRingPresentation.CreateMaterial` → **URP Unlit + `Resources/UI/MoveTargetRing` 텍스처**

**결함 1 — 베이크 링 텍스처가 깨져 있음(확정, 파일 실측)**:
- `Assets/Resources/UI/MoveTargetRing.png`(256²)의 불투명 픽셀이 **1,350/65,536 = 2%**에 불과하고 환형 밴드가 없음
- 방사형 알파 프로파일: r=0~120 전부 alpha 0, 중심선(cx/cy) 런 길이 0 — 코멘트가 주장하는 "outer 0.9 / inner 0.68 밴드"가 실제 파일에 존재하지 않음(산발 화이트 스펙클만 존재)
- → 링 쿼드가 그릴 링이 텍스처에 없음. 링의 "얇은 파란 타원"은 이 체인이 그린 것이 아님

**결함 2 — 알파 블렌딩 미적용(강한 가설, Play 재현으로 확정 예정)**:
- 근사-빈 텍스처(투명 텍셀 RGB=검정)에서 **배경이 검게 보인다 = 프레임버퍼에 알파 0 픽셀이 기록되고 있다**는 뜻(올바른 SrcAlpha 블렌드면 alpha 0 → 비침, 검정 기록 없음)
- 프로젝트의 **동작 중 투명 패턴**(H2 블롭 섀도우/짚 데칼 — 캡처로 검증됨)과 `CreateMaterial`의 차이는 딱 2가지:
  - `m.SetFloat("_Surface", 1f)` **누락**
  - `m.SetOverrideTag("RenderType", "Transparent")` **누락**
  - (_SrcBlend/_DstBlend/_ZWrite/큐 3000/키워드는 동일하게 세팅돼 있음)
- Unity 6 URP Unlit은 `Blend [_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]` — 인스펙터 경로(_Surface=1)가 아닌 수동 SetInt만으로 상태가 완전히 잡히지 않을 가능성

**결함 3 — 이중 생산자(확정, 09-30 계약 위반 잔존)**:
- `GuardPlaceholder.SetSelected(true)` → `SpecialEffectsController.AddSelectionOutline(guard)`가 **레거시 평평한 Cylinder**(반경 0.6, URP Lit 파랑 alpha 0.5)를 발밑에 추가로 스폰
- `GuardSelectionManager.SyncSelectionAuras`에 방어적 `RemoveSelectionOutline`이 있으나 호출 순서에 따라 레거시 디스크가 살아남아 **파란 링 + 레거시 디스크가 겹쳐 보임** — 스크린샷의 얇은 파란 타원이 이 레거시 Cylinder(또는 셰이더 폴백)일 가능성
- 09-30 사용자 시각 계약이 이미 "SetSelected 레거시 duplicate 즉시 제거/검증"을 요구 — 미이행 상태

### B. 링 크기/두께
- 현재 발밑 링 쿼드 = 1.9 × (1.826, 0.548) = **3.47 × 1.04m** 타원 — 병사 대비 과대(스크린샷에서도 병신처럼 넓게 읽힘)
- 사용자 요구: 지름 절반(≈1.74 × 0.52m), 두께는 증가

### C. 이동 정확도 — 소스 확정 4요인

1. **정지 반경(확정)**: `GuardPlaceholder` — `MOVE_CLEAR_RADIUS=1.0m`(distXZ≤1.0 → ClearCommand+Idle), `MOVE_STOP_RADIUS=0.6m`(StepToward 클램프 `min(speed·dt, distXZ−0.6)`) → **물리적으로 지정점 0.6~1.0m 손전 정지**
2. **분산 배치(확정, 설계 의도)**: `RTSCommandSystem.IssueMoveCommand` → `FormationSpread.Distribute(pos, aliveCount)` — unitRadius=1.6 → 링1 반경 **3.2m**(6개), 링2 **5.6m**(12개) — 병사 수가 많을수록 실제 도착점이 클릭점에서 수 m 떨어짐(뭉침 방지 Phase O5 설계). 1명일 때만 중심=클릭점
3. **물리 간섭(2차)**: StepToward는 Rigidbody.MovePosition 직진 — 다른 병사/몬스터 collider에 가로막히면 목표에 못 미치고 도착판정(XZ 거리)만으로 해제되지 않아 멈춘 위치가 어긋남
4. **도착 판정 XZ 전용(사소)**: y 무시 — 경사면에서 미세 오차

## 2. Acceptance Criteria (사용자 문장 그대로)

- AC1: 부대 지정 시 병사 발밑에 **파란 링 하나만** 렌더 — 검은 사각형/레거시 디스크 소멸
- AC2: 공격 지정 몬스터 발밑 **빨간 링 하나만**, 이동 지속 마커 **골드 링 하나만** — 동일 증상 소멸
- AC3: 링 지름 = 현행 절반(3.47×1.04 → ≈1.74×0.52m 타원)
- AC4: 링 밴드 두께 = 현행보다 두껍게(사용자 캡처 승인 게이트)
- AC5: 단일 병사 이동 명령 → 지정점 중심 ±0.25m 이내 도착
- AC6: 멀티 병사 이동 — 결정 사항 D1 참조(분산 유지/축소 or 정확 도착)
- AC7: 기존 계약 보존 — 링 무콜라이더·그림자 미방출·HoverTargetClassifier 비오염·드래그 선택 박스 무변경

## 3. Phase 구성

### Phase 0 — 진단 확정 (소규모, 먼저)
- Play 재현 캡처(사용자 또는 기존 로그) + **격리 A/B**: `GuardPlaceholder.SetSelected`의 `AddSelectionOutline` 호출 임시 게이트 → 얇은 파란 타원의 생산자 확정(레거시 Cylinder vs 기타)
- `CreateMaterial`에 `_Surface=1`+OverrideTag 추가 1회 실험 → 검은 사각형 소멸 여부로 결함 2 확정
- 산출: 원인표 최종 확정(본 계획의 "가설"→"확정" 갱신)
- 게이트: 컴파일 0 (실험 후 원복 또는 유지 판정)

### Phase 1 — 링 비주얼 단일화 + 크기/두께 (본체)
- **D2(방식) 기본안: `Custom/SelectionRing.shader` 단일 소스 전환**
  - `SelectionRingController.Awake`/`CommandRingPresentation`을 셰이더 머티리얼 경로로 전환 — 깨진 `MoveTargetRing.png` 의존 제거(텍스처 재베이크 불필요, SDF 환형이라 두께/반경 파라미터 직접 제어)
  - `CommandMarker.SetupPersistent`도 동일 경로(SetupFading은 이미 셰이더 사용 — 일원화)
  - 셰이더 파라미터: `_RingRadius`/`_RingThickness` 상수화 — 지름 절반(쿼드 부모 스케일 1.9→0.95) + `_RingThickness` 0.035→**0.06~0.08**(조금 더 두껍게)
  - 대안(사용자 거부 시): MoveTargetRing.png 절차 재베이크(PIL/Editor 스크립트) + CreateMaterial 투명 패턴 수리 — 텍스처 의존은 유지
- **이중 생산자 제거**: `GuardPlaceholder.SetSelected`의 `SpecialEffectsController.AddSelectionOutline/RemoveSelectionOutline` 호출 제거(09-30 계약 이행) — SpecialEffectsController 본체는 다른 소비자 확인 후 유지
- **펄스/국가색 계약 유지**: SelectedGuardColor(파랑)/ExplicitTargetColor(빨강)/MoveTargetColor(골드) 3색 독립, 공유 머티리얼 변이 금지(인스턴스 머티리얼), 무콜라이더/그림자 미방출 유지
- 테스트: 신규 `RTSRingVisualTests` — ①단일 선택 시 SelectionOutline_* 부재+SelectionRing 1개 ②셰이더 머티리얼 계약(Custom/SelectionRing, _RingThickness 상수) ③3색 독립 ④무콜라이더. 기존 `AttackTargetRingTests`/`RTSTests` 회귀 확인
- 게이트: 컴파일 0 → focused 신규+기존 링 테스트 → **Play 캡처(사용자)** — AC1~4 시각 승인

### Phase 2 — 이동 정확도
- **정지 반경 축소(확정 요인)**: `MOVE_CLEAR_RADIUS 1.0→0.25m`, `MOVE_STOP_RADIUS 0.6→0.15m` — 단일 병사가 지정점 ±0.25m 내 정지(AC5). 프레임 스텝 클램프식은 유지(넘어감 방지)
- **D1(분산) 기본안**: 분산 배치 유지 + `unitRadius 1.6→0.9`(링1 3.2→1.8m, 링2 5.6→3.15m) — 뭉침 방지 유지하면서 클릭점 근접. index 0(선행 병사)은 여전히 정확한 클릭점. (대안: 전원 클릭점 도착 + 병사 간 분리력 — 공수 큼, D1로 확정 시 별도 Phase)
- **물리 간섭 완화**: 도착 실패 누적(예: distXZ 감소 정지 0.5s) 시 명령 해제 + 마커 소멸 — 영구 멈춤 방지. collider 충돌 원인(병사 간 collider 유무)은 Phase 0에서 로그로 확인
- 테스트: `FormationSpreadTests` 기대값 갱신(unitRadius) + 신규 도착 판정 계약(정지 반경 상수 단정 + 더미 이동 시뮬레이션)
- 게이트: 컴파일 0 → focused → Play(사용자) — AC5/AC6 실측

### Phase 4(=3) — 기록/마무리
- QAPROGRESS 13차 기록, 계약 갱신 요약, (명시 시에만) stage/commit/push

## 4. 사용자 확정 필요 사항

- **D1 — 멀티 병사 이동**: ①분산 유지+반경 축소(기본안 — 뭉침 방지 유지) ②전원 클릭점 정확 도착(분리력 신설 필요) 중 선택
- **D2 — 링 구현 방식**: ①기존 셰이더 단일화(기본안 — 텍스처 의존 제거) ②MoveTargetRing.png 재베이크
- Phase 0 진단 실험의 진행 허용(소스 1~2줄 임시 게이트/원복 포함)

## 5. 영향 파일 목록(예상)

- `Assets/Scripts/Systems/CommandRingPresentation.cs` (셰이더 전환/상수)
- `Assets/Scripts/Systems/SelectionRingController.cs` (스케일/머티리얼)
- `Assets/Scripts/Systems/CommandMarker.cs` (persistent 경로 일원화)
- `Assets/Scripts/Systems/GuardPlaceholder.cs` (레거시 호출 제거+정지 반경 상수)
- `Assets/Scripts/Systems/RTSCommandSystem.cs` (없음 or 마커 소유자 처리)
- `Assets/Scripts/Systems/FormationSpread.cs` (unitRadius 기본값 — D1)
- 테스트: `Tests/EditMode/RTSTests.cs` 인접 신규 `RTSRingVisualTests.cs`(+meta), `FormationSpreadTests` 갱신
- (D2 대안 채택 시에만) `Assets/Resources/UI/MoveTargetRing.png` 재베이크
