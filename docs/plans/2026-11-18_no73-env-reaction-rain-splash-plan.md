# 계획서: 환경 반응 VFX (무번호 73 — E2 오브젝트 스윙 + E3 우천 워터 스플래시)

**사용자 지시(2026-11-18)**: "진행" — 무번호 72 착지 보고의 잔여 원장 E2·E3 후속 승인. Play·커밋 별도 승인 유지.

---

## 원장 근거 (37 로드맵 계층 3)

- **E2 잔디/오브젝트 반응**: 근접 휘파람/폭발 시 주변 오브젝트 흔들림 — 원문 "셰이더 정점 변조 — 전용 셰이더 필요", 단 **규모 검토 후** 게이트 명시.
- **E3 우천 워터 스플래시**: Rain 상태 타격 시 물 튀김 입자 — WeatherTintDust 계약 연동, 25-IN1 선례.

## E2 — 임팩트/폭발 오브젝트 스윙 (규모 검토 결과 반영)

- **규모 검토(37 게이트 이행)**: 전용 정점 변조 셰이더는 GLB 소재(URP Lit `_BaseMap`)에 머티리얼 클론+셰이더 스왑을 요구 → 국가색/텍스처 계약 리스크 + 기존 FX 인프라와 이질. 검증된 코드 레버로 최소설계:
  - 쿼리 — `OverlapSphere` (선례: `GuardTaskSystem.FindNearestGatherNode`, 낚시 OverlapSphere 수평)
  - 절차 본 회전 — `FXMotion.Progress` 이징 Slerp (선례: `FxDeathRelax` W2, 28-U3 재구현 금지 규약)
  - **전용 셰이더 정점 변조는 Play 체감 후 후속 판정 항목으로 이월**(기각 아님 — 이월).
- **트리거 2종**: ①타격 임팩트 — `SlashVFXRunner.PlayImpactMulti`(45차 P3 임팩트 단일 경로, CombatFXGate:123·PlayerCombat:588·SlashVFXRunner:570 3호출부 전부 수렴) ②폭발 — `BombExplosionVisual.OnDisable` 발화 지점(기존 SpawnHitSparks와 동일 시점·위치).
- **대상 판정**: `HerbPickup` 컴포넌트 / tag `Gather`·`Grass` / 이름 herb·grass·풀·약초 — **`GuardTaskSystem.IsGatherNode` 판정식과 동일 조건**(중복 정의 금지 — static 노출 또는 동일 리터럴 계약).
- **동작**: OverlapSphere 반경 소형(기본 3m) → 대상 수 한정(게이팅) → 절차 딥 스윙(축 결정론 — 임팩트→오브젝트 방향 수직축, 진폭/주기 결정론 상수) → 자가 파괴·원복. EditMode에서는 쿼리 미실행(`Application.isPlaying` 가드).

## E3 — 우천 워터 스플래시

- **훅**: `SlashVFXRunner.PlayImpactMulti` 말미 1~2줄 — 게이트 `WeatherManager.Instance != null && CurrentWeather == Rain && Application.isPlaying`.
- **시각**: 절차 물 튀김 소형 입자 — 4옥타브 value noise 절차 텍스처(`EnsurePuffTexture` 선례 재사용, **사각 금지 계약**) 물색 틴트(25-IN1 Rain 청백 (0.65,0.9,1) 정합), 스폰 수 한정. **링 예산(MaxConcurrentRings 24) 미소비** — 입자 전용 레이어(X2 계약 무손상).
- **결정론**: Random 0건 — 25-IN1 결정론 계약 준용.

## 파일

- **신규** `Assets/Scripts/Systems/FxEnvReaction.cs` — E2 스윙 러너 + E3 스플래시 스폰 엔트리(결정론, EditMode 가드)
- **수정** `Assets/Scripts/Systems/SlashVFXRunner.cs` — `PlayImpactMulti` 말미 훅 1~2줄(E3 게이트 + E2 트리거)
- **수정** `Assets/Scripts/Systems/BombExplosionVisual.cs` — `OnDisable` 발화 후 E2 트리거 1줄
- **수정(선택)** `Assets/Scripts/Systems/GuardTaskSystem.cs` — `IsGatherNode` static 노출(동일 판정 리터럴 이중 정의 방지) — 기존 호출부·시그니처 보존
- **신규** `Assets/Scripts/Tests/EditMode/FxEnvReactionP73Tests.cs` — 소스 계약 테스트

## 계약 (잠금 무손상 — 수정 시 반드시 아래 문자열 카운트 유지)

- **X2_FxBudgetGuard**: `_activeRingCount++` 리터럴 **2곳**, `_activeRingCount = _activeRingCount + 1;`, 가드 배선 2곳, `NotifyRingDestroyed();` 2곳 — 신규 코드에서 해당 문자열 **0건 추가**
- `MAX_FX_PER_SECOND`(CombatFXGate) 무변경
- `WeatherTintDust` 기존색 유지 계약 무손상
- [71-G2] 훅 문자열 / [70-U3]·[71-L2] 문자열 무손상(SlashVFXRunner·CombatFXGate 수정 시)
- 결정론(Random 0건) 전 구현

## 테스트 (P73 소스 계약)

- 마커/게이트/IsGatherNode 판정식 동일성/Random 0건/기존 잠금 문자열 카운트/호출부 배선
- E2: 스윙 러너 수명·`FXMotion.Progress` 재사용·EditMode 가드
- E3: Rain 게이트 상수·스폰 한정·텍스처 재사용

## QA 게이트

- 컴파일 error CS 0 (compile_test.sh) + 전수 EditMode **baseline = qa_72(2527/2517/10) 실패집합 1:1** + 신규 테스트 GREEN
- 배치 background+폴링(600s foreground 금지 — run_tests.sh 프로토콜 준수)
- Play·커밋 별도 승인 대기
