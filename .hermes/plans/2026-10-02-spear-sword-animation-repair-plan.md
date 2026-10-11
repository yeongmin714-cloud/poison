# 창 첫 찌르기 미표시 · 검 콤보 끊김 진단/수정 계획

> **범위:** 사용자가 보고한 창 첫 공격 애니메이션 미표시와 검 공격 모션의 잔여 끊김만 우선 다룬다. 이번 단계는 근본 원인 분석과 계획 작성이며, 게임 소스·Animator 에셋·씬·테스트 코드는 수정하지 않는다. 활/가스 등 별도 트랙은 보류한다.

**목표:** 동일한 Play 실행에서 입력 승인 → Animator 상태/클립/포즈 → 피격·중단 → 타격 판정을 추적해 두 증상을 각각 재현하고, 확인된 원인만 최소 수정한다.

**검증 원칙:** Compile, EditMode XML, Play 시각 확인은 서로 다른 게이트다. EditMode 출력의 초록 문구만 믿지 않고 결과 XML을 확인한다. Animator 상태 진입 또는 피해 로그만으로 화면 모션 성공을 선언하지 않는다.

---

## 1. 조사 결과 — 현재 확인된 사실

### 창 첫 찌르기

- `PlayerCombat.TryAttack()`은 준비된 `HumanoidClipDriver`가 있을 때 창 공격의 즉시 레거시 피해 경로를 건너뛰고, accepted sequence를 드라이버에 넘긴다. 드라이버는 첫 창 입력에 `StartSpearOpener(false)` → `TriggerSpearAttack()` → `SetTrigger("AttackThrust")`를 호출한다.
- `Player_AC.controller`에는 Any State의 `AttackThrust` 트리거 전이와 `AttackThrust` 상태가 있다. 해당 상태의 `m_Motion`은 GUID `f076cbe...`인 `Thrust_Slash` FBX clip(90 frames)에 연결돼 있다. 즉, 현재 자산에서 확인되는 것은 누락된 트리거/상태/클립 연결이 아니다.
- 현재 Editor.log에는 창 입력이 드라이버에 수락된 뒤 `[Spear] AttackThrust hit resolved normT=0.50`이 기록됐고, 같은 경로에서 실제 `ResolveComboSwingHit()` 피해 처리와 레거시 공격 트레일 로그가 확인된다. 따라서 해당 Play 구간에서 AttackThrust 상태가 적어도 타격 기준 시점까지 진행한 코드 증거는 있다.
- 이는 사용자가 화면에서 모션을 봤다는 뜻은 아니다. 현재 driver 진단은 일부 상태명을 해시로만 기록하며, 이번 창 공격의 실제 Animator/clip 샘플, 동일 프레임의 뼈 변화와 화면 렌더 결과를 일치시킨 증거가 없다.
- 후보 중 하나는 피격 애니메이션의 상태 중단이다. Player HP가 줄면 `HumanoidClipDriver.UpdatePlayer()`가 `HitLight` trigger를 발화하고, `Player_AC.controller`에는 `HitLight` Any State 전이가 있다. Editor.log에 플레이어 HP 감소 후 HitLight로 보이는 반응 clip(`Slap_Reaction`)이 재생된 런타임 기록도 있다. 다만 해당 사건이 사용자가 보고한 ‘창 첫 찌르기 미표시’와 같은 입력 순간인지는 아직 상관 분석되지 않았다.

**현재 판정:** 입력→AttackThrust 경로는 소스상 연결되고 런타임에서 hit marker까지 진행한 사례가 확인됐다. 화면 모션 미표시의 단일 근본 원인은 아직 확정할 수 없다. 특히 피격 Any State 인터럽트, 실제 렌더 대상 Animator와 드라이버 Animator 불일치/뼈 덮어쓰기, 공격 자세가 화면에서 드러나지 않는 retarget 결과를 분리해 재현해야 한다.

### 검 공격 끊김

- `WeaponCombo` 상태는 `Weapon_Combo_2`에 연결되어 있고 상태 속도는 2.5다. 드라이버는 한 클립을 재생하며, 승인된 segment의 normalized-time marker마다 타격을 처리하고 미승인 구간 경계에서 홀드한다.
- 가장 최근 조사한 Editor.log에는 여러 `WeaponCombo 시작` 및 stage-1 hit 로그가 있지만 `[Combo] WeaponCombo follow-up accepted` 기록은 찾지 못했다. 이 로그 조각만으로 해당 기록들이 모두 사용자의 최신 검 영상과 같은 실행이라고 보장할 수는 없다. 다만 재생이 매번 1타부터 다시 시작하는 입력 승인/타이밍 실패 가능성을 우선 검사할 근거다.
- 또 하나의 구조적 중단 후보는 `HitLight` Any State 전이다. 플레이어가 공격 중 피해를 입으면 이 trigger가 full-body 공격 상태를 끊을 수 있다. 현재 Controller의 HitLight 전이는 Any State에 있고, HP 감소 시점과 combo state/normalized time을 같은 이벤트 ID로 묶어 기록하지 않아 기존 로그만으로 검 모션 중단의 직접 원인이라고 단정할 수 없다.
- 제공된 `Screenshots/공격 모션 테스트 3.mp4`(23.27초) 샘플 프레임에는 공격 자세 사이에 대기 자세로 보이는 간격이 있다. 영상만으로 물리 클릭 수, 승인된 follow-up, HitLight 발생 여부를 입증할 수 없다.

**현재 판정:** 끊김은 ‘후속 클릭이 수락되지 않아 stage 1이 종료/재시작되는 경우’와 ‘HP 감소가 발생해 HitLight가 공격을 인터럽트하는 경우’를 먼저 분리해야 한다. 둘 중 하나 또는 둘 다 작동할 수 있으며, 어느 것이 주 원인인지는 아직 같은 실행의 시간 정렬 증거가 부족하다.

---

## 2. 원인 확인 및 수정 순서

### Phase A — 재현 기준선과 단일 실행 진단

**목적:** 현상을 먼저 각각 재현하고 이벤트 인과관계를 확정한다. 이 단계에서 기능 동작은 바꾸지 않는다.

**대상 파일:**
- `Assets/Scripts/Systems/PlayerCombat.cs`
- `Assets/Scripts/Systems/HumanoidClipDriver.cs`
- `Assets/Resources/Animation/Controllers/Player_AC.controller` (읽기 전용 감사)
- 실제 재현 씬의 플레이어/무기 장착 경로 (실제 씬에서 확인; 현재 추측으로 특정 씬을 고정하지 않음)

**진단 로그 이벤트(세션 내 단조 증가 ID 포함):**
1. 실제 mouse-down → UI/부대/패링 소비 여부 → `TryAttack` accepted/rejected 사유, weapon type, accepted sequence.
2. sequence를 소비한 실제 `HumanoidClipDriver`의 instance ID, Animator instance ID, 부모/자식 관계, enabled/initialized/controller/avatar 상태.
3. 상태 전이 전/후의 이름(해시만 금지), 현재/다음 상태, 실제 `GetCurrentAnimatorClipInfo(0)` clip 이름, normalized time, Animator speed, interruption/transition 여부.
4. opener/follow-up 승인 시간, 현재 accepted-click 수, click-window 만료 시간, stage별 hit marker 한 번 여부.
5. HP 감소/HitLight trigger, 그 프레임의 현재 공격 상태/클립/normalized time을 동일 이벤트 ID/시각으로 기록.
6. 화면에 실제로 보이는 SkinnedMeshRenderer와 Animator skeleton 연관성, 대표 팔/다리의 localRotation 변화. 공격 정면에서 기록한 Play 영상과 로그 시각을 맞춘다.

**재현 케이스:**
- 창: 적이 플레이어를 때리지 않는 안전한 테스트 상태에서 정지·이동 중 각각 한 번 클릭하고, 별도 케이스로 적의 피격이 동시에 가능한 상태에서 반복한다.
- 검: 먼저 무피격 상태에서 1클릭, 이후 입력 창 안의 2클릭/3클릭을 명시적으로 수행한다. 별도 케이스에서 피격을 허용해 HitLight 중단을 비교한다.
- 네 케이스 모두 동일한 재현 실행의 영상과 fresh `Editor.log` 구간을 보존한다. 클릭 UI 오버레이 또는 입력 로그 없는 영상으로 클릭 횟수를 추정하지 않는다.

**Phase A 판정표:**
- `AttackThrust`에 진입하지 않음 → 입력 sequence/driver identity/Animator trigger 소비 문제.
- 상태에 진입하고 clip time이 진행하지만 해당 Animator의 뼈가 움직이지 않음 → clip/avatar binding 또는 다른 bone writer 문제.
- 해당 Animator의 뼈는 움직이지만 플레이어 mesh가 고정 → 표시 중복 body/SkinnedMeshRenderer rig 연결 문제.
- 창/검 모션이 HitLight 발생 시에만 사라짐 → Any State hit-reaction 중단으로 확정.
- 검의 두 번째 입력 자체가 거부되거나 follow-up 승인 로그가 없음 → click window/cooldown/sequence 소비 경쟁 문제. 승인 로그가 있는데 stage 2로 가지 않으면 Animator dispatch/transition 문제.

**게이트:** Phase A evidence report가 사용자 증상과 같은 한 번의 Play 실행을 기준으로 두 원인을 분리해야 한다. 단순한 과거 로그나 EditMode 결과만으로 A를 완료하지 않는다.

### Phase B — 창 첫 찌르기 최소 수리 (Phase A가 특정한 경우만)

**대상 후보:** `HumanoidClipDriver.cs`, 필요 시 `Player_AC.controller` 또는 실제 렌더 rig 배선 파일. 원인이 다른 레이어에 있으면 그 레이어만 다룬다.

- 입력/sequence 문제면 accepted 첫 입력 하나가 `AttackThrust`에 정확히 한 번 도달하도록 수리하고, 일반 창 공격/후속 sequence를 구분한다.
- Animator가 상태와 clip time을 진행시키지만 bone motion이 없으면 FBX Humanoid import/Avatar mapping/clip muscle binding과 runtime avatar를 대조한다. 상태 이름이나 trigger를 또 바꾸는 추측성 수리는 금지.
- bone pose가 바뀌지만 화면 모델이 안 바뀌면 visible SkinnedMeshRenderer와 실제 구동 Animator의 rig identity/중복 renderer를 수리한다.
- HitLight가 직접 원인으로 확인되면 피해 계산과 피격 FX를 삭제하지 않는다. 창 opener의 핵심 모션을 보존하는 최소 정책(공격 commit 동안 HitLight를 지연/허용할지)을 기존 피해 반응 계약과 맞춰 설계한 뒤 테스트한다.
- opener hit marker는 한 번만 유지하고, legacy immediate fallback과 driver-owned delayed hit가 동시에 발생하지 않게 한다.

**TDD/QA:** 실패 재현을 담은 최소 EditMode 정책/라우팅 테스트를 먼저 추가한다. Unity compile CS=0 및 결과 XML에 해당 테스트명/실제 집계가 있어야 한다. 별도 Play에서 창 1클릭, 상태·clip·pose 진행, 실제 화면 모션, 정확히 한 hit attempt를 확인한다.

### Phase C — 검 끊김의 원인별 분리 수리

**C1: 후속 입력 창/sequence 승인 문제로 확정된 경우**
- 실제 accepted mouse-down 시각과 드라이버 dispatch 시각을 분리해 비교한다. 공격 입력을 늦은 프레임에 소비하거나 큐에 보관한 경우, accepted edge 시각을 사용해 follow-up window를 판정한다.
- 1/2/3 클릭별 승인 segment, 재생 범위, hit marker 수를 표로 테스트한다. 클릭 타이밍을 임의로 넓히기보다 실제 입력 간격과 clip speed를 일관된 같은 시계로 계산한다.
- 후속 승인인데 stage 2로 이동하지 않을 때만 state transition/Animator handoff를 조사한다. stage 1 reset/hold cleanup이 승인된 pending delta를 지우지 않는지 테스트한다.

**C2: `HitLight` Any State 중단으로 확정된 경우**
- 플레이어의 HP/damage/feedback을 보존하면서 무기 공격 모션이 피해 시점에 끊기는 정책을 수정한다. 우선순위 후보는 공격 commit 동안 hit reaction을 지연시켜 combo 이후 처리하거나, 검증된 별도 upper-body/additive 반응을 쓰는 것이다. full-body Animator 구조를 확인하기 전에 임의로 어느 하나를 강제하지 않는다.
- 공격 중이 아닌 상태에서는 기존 HitLight가 계속 재생되는지, 사망/구르기/패링 등 더 높은 우선순위 상태를 방해하지 않는지 회귀 확인한다.

**C3: 두 원인이 동시에 확인된 경우**
- C1과 C2를 각각 분리 테스트·수리한 후 통합한다. 같은 패치에서 click window와 피격 우선순위를 동시에 조정해 효과를 알 수 없게 만들지 않는다.

**검증:** 검 1-click idle return, 입력창 내 2/3-click 연속 재생, 네 번째 입력 reject, 피격 전/중/후 behavior, segment별 한 번의 hit attempt를 확인한다. 로그상의 accepted segment와 영상의 모션 전환 프레임을 맞춘다.

### Phase D — 최종 코드/QA 및 문서

- 독립 code-agent review + QA-agent review. 변경 파일은 최소 범위로 지정하고 기존 미추적/dirty 파일은 보존한다.
- `git diff --check`, 최신 Unity batch compile(실제 로그/exit code/CS 오류 확인), 대상 EditMode 결과 XML, Play 영상/로그를 각각 기록한다.
- 컴파일 및 EditMode 성공만으로 시각 동작 완료를 선언하지 않는다. Play 불가 시 해당 항목은 미검증으로 남긴다.
- 구현을 실제 진행한 뒤에만 `QAPROGRESS.md`와 관련 ROADMAP 체크 상태를 갱신한다. 현 단계는 계획 수립이며 완료 표시/커밋/푸시를 하지 않는다.

---

## 3. 완료 조건

1. 창 한 번 클릭에서 `AttackThrust` 상태와 올바른 clip이 확인되고, 플레이어 화면 mesh에서 찌르기 포즈가 실제 재생되며 hit attempt는 정확히 한 번이다.
2. 검 follow-up 입력 승인/거부가 의도된 입력 창과 일치하고, 승인된 2·3타가 화면에서 연속 재생된다.
3. HitLight/피격, 공격 buffer, Idle 복귀 각각의 역할이 로그/Play 비교로 구분되고, 수리 후 공격이 원인 없이 끊기지 않는다.
4. 기존 비공격 피격 반응, 무기 route, 피해량/타격 소유권에는 회귀가 없다.
5. Compile, EditMode, Play 결과가 각각 증거와 함께 기록된다.

## 진행 경계

- 이 문서 작성만으로 구현 승인을 가정하지 않는다. 새 Play 근거가 기존 소스/로그 근거와 충돌하면 계획의 원인 가설을 먼저 갱신한 후 수정한다.
- 현 단계에서 원인 확정이라 보고할 수 있는 것은 Spear accepted path가 AttackThrust hit marker까지 진행한 런타임 사례와 HitLight Any State 경로의 존재다. 두 사용자 가시 증상과의 직접 인과는 Phase A에서 확인해야 한다.
- 기존 검/창 로직, 씬, controller, clip 설정에는 아직 변경을 가하지 않았다.

date: 2026-10-02
status: diagnosis-backed plan only; awaiting implementation authorization
source plan superseded for immediate priority: `.hermes/plans/2026-10-02-spear-bow-gas-vfx-plan.md` Phase 1/2 are deferred until this focused animation repair is resolved.
