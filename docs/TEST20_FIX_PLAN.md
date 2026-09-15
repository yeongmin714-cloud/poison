# 🎯 테스트 20 통합 수리 계획 (TEST20_FIX_PLAN)

> **목적:** 사용자가 테스트 20 영상(2026-09-15)에서 발견한 9건 + 코드 실측(Editor.log·프레임 스캔)으로 추가 확인한 계획/구현 간 차이를 모두 확정하고, 각각의 근본 원인을 한 번에 수리한다.
> **방식:** 이미 동작 중인 전투 체인을 보존하되, 이중 경로/생성 누락/잔존 상태를 단일화·보강하는 방향.
> **기준:** 배치컴파일 error CS=0. 실행 전 에디터 닫기. 각 페이즈 완료 후 Play 판정.

---

## 0. 테스트 20 영상 근본 원인 실측 요약

| # | 문제 | 판정 | 근본 원인 (실측) | 난이도 |
|:-:|:--|:--:|:--|:--:|
| 1 | 피격 시 몬스터 흰색에서 안 돌아옴 | ❌ 재현 | **이중 경로:** `AnimalAI.TakeDamage`(890) + `HitReaction`(71)이 CombatFXGate→CombatVFXController(refcount)와 **별개인 레거시 `HitVFX.PlayHitFlash`(MaterialPropertyBlock)** 를 병행 호출. refcount는 sharedMaterial.color 직접, MPB는 SetPropertyBlock으로 `_BaseColor`를 흰색으로 덮음 = MPB가 sharedMaterial 색보다 우선 → 흰 고정 | 🔴 |
| 2 | 어그로/적대 느낌표가 사라지지 않음 | ❌ 로그 확인 | `MonsterAggroSystem.ShowAggroVisual`(수명 무제한, 상태 기반)이 적대(Combat) 상태 내내 재표시. `ShowTransientExclamation`(1.2s 제거 코루틴)과 별개로 충성도 주기(2s) 검사가 계속 느낌표를 다시 켬. Hide 조건(Idle/Cooldown 복귀)이 적대 유지 중엔 미달 | 🟡 |
| 3 | 전리품→인벤 드래그앤드롭 안 됨 | 🔶 구조상 취약 | 고스트/드롭 렌더가 **인벤토리 IsOpen에 의존**. 인벤 닫힌 채 전리품 드래그 시 LootWindow.OnGUI가 대행하나 좌표 hit-test가 스케일 상수(_uiScale)와 불일치 위험(G-3 미검증), 인벤 열림 전환 순간 고스트 소실 | 🟡 |
| 4 | 방어구(장비) 장착 안 됨 | ❌ 로그 확정 | **Test_10의 `EnsureGameManager`에 `EquipmentManager`(및 `ArmorVisualAttachSystem`) 생성 누락** — CoreSystemsBootstrap 미실행 씬. 로그: `결과 실패(사유: EquipmentManager 없음)` | 🔴 |
| 5 | 무기 파츠가 정확히 손에 미부착 | 🔶 자체 실패 | 무기 장착은 성공(로그 `✅ wood_sword → RightHand`)하나 영상에선 손에 안 보임 → **첨부된 인스턴스 렌더러가 비활성/scale 0/그립 오프셋** 문제. 54차 강제활성화가 실걳적용 안 될 가능(아뱌타 Generic/Renderer 시점) | 🔴 |
| 6 | 슬래시가 흰색이 아님 | ✅ 확정 | `ComboStageTint`가 **콤보 스테이지별(1타 흰/2타 골드/3타 주황)** 로 다르게 반환. 사용자는 흰색 원함 + **Free Slash 폴백 전환 고려**. VFX Graph(white-blue)가 원본색 유지 | 🟡 |
| 7 | 내 병사 GLB+애니 미부착 | 🔶 미확정 | 병사는 존재&공격 동작(myGuard_0~2 로그)하나 **GLB 부착 로그 0건** → GLB/FBX 로드 실패 후 캡슐 잔존 OR 반환 시 inactive(`Coroutine couldn't be started ... inactive`) | 🔴 |
| 8 | 드래그 모션 없음(드래그 불가) | 🔶 조건적 | `DrawGhost`는 인벤 IsOpen일 때만 InventoryWindow.OnGUI가 렌더. 드래그 시작 전 인벤 닫힘이면 전리품 경로만 대행 → 인벤/장비/창고 출발 드래그가 고스트 없음 | 🟡 |
| 9 | 병사 접지 안 됨(공중 부양) | 🔶 지속 | `GroundModelToY(SurfaceY)`를 CreateGuard GLB/FBX 경로에 적용했으나, `GuardPlaceholder`가 StepToward로 transform.position을 **직접 이동하며 y를 배치 오프셋(+1.0)으로 되돌리는 경로** 존재. RB isKinematic이라 중력으로 재접지 불가 | 🟡 |

### 계획(ATTACK_FEEL_UPGRADE_PLAN.md) 대비 추가로 확인된 차이 (사용자 9건 외)
- **A.** `Core/HitVFX.cs`(레거시 MPB)가 `AnimalAI`·`HitReaction`에서 계속 사용됨 — 46차 "단일 경로화" 주장과 달리 **이중 히트플래시 경로 실재**. CombatFXGate의 refcount만 두지 말고 레거시 MPB 호출을 전부 제거/단일화해야 함.
- **B.** Test_10 씬은 **CoreSystemsBootstrap 미실행** → 무기/방어구/장비 3개 시스템 + ArmorVisualAttachSystem이 전부 미생성. 54차가 "수정"했지만 Test_10만의 생성 누락이 남아 있음.
- **C.** 병사 `SetActive(false)` 경로(GuardPlaceholder 665행)로 인해 재소환/배치 후 inactive → Coroutine/GLB 미표시.
- **D.** `_uiScale` 스케일 전환이 **드래그 물리 hit-test에 미검증**(계획 G-3 잔여) — 전리품·장비·창고 출발 드래그 좌표 일치 문제 잠재.

---

## Phase T1 — 단일 히트플래시 경로 (문제1) 🔴
**목표:** sharedMaterial refcount(CombatVFXController) 단일 경로로 통일, 레거시 MPB 제거.
- [ ] `Systems/AnimalAI.cs` TakeDamage(890행) — `HitVFX.PlayHitFlash(flashRenderer)` → `CombatVFXController.PlayHitFlash(gameObject)`로 교체(자식 렌더러 폴백 로직은 PlayHitFlash 내부 GetComponentsInChildren이 대체). 레거시 import 정리.
- [ ] `Systems/HitReaction.cs`(71행) — `PlayHitFlash(_targetRenderer)` → CombatFXGate/CombatVFXController 경로로 교체(PlayHitReaction 내 호출이 아니라 별도 배선이면 CombatFXGate.PlayHitFX에 위임). PlayerCombat이 이미 CombatFXGate로 발화하므로 이것은 보조.
- [ ] `Core/HitVFX.cs`의 `PlayHitFlash`+`HitFlashRunner`(MPB) → **사용처 0건 확인 후 제거**('죽은 코드' 청산). 필요한 static 캐시(HitFxHost)는 CombatVFXController로 이관.
- [ ] 유지: `CombatVFXController.FlashMaterialBegin/End`의 재질 refcount(+ `_flashOrigColor/_flashRef/_flashOrigEmission`) — 원본 복원은 마지막 플래시 종료 시 1회, 대상 파괴에도 재질 키로 복원.
- [ ] **검증:** Play에서 연속 타격·다중 몬스터 동시 피격에도 흰색 잔존 0, 0.15s 후 원본색 복귀.
- [ ] 배치컴파일 error CS=0.

## Phase T2 — 느낌표 수명 통일 (문제2) 🟡
**목표:** 적대/어그로 느낌표가 상태 이탈·일정 시간 후 반드시 사라짐을 보장.
- [ ] `MonsterAggroSystem.ShowAggroVisual` — 붉은 오라는 유지, **느낌표(exclamation)만 `ShowTransientExclamation` 식 단일 수명(예 1.5s)으로 통일**. 상태가 Idle/Cooldown으로 복귀하면 즉시 `HideAggroVisual` 발동.
- [ ] `MonsterAggroSystem.Update` 상태 머신(80-180행) — Cooldown/Idle로 전이 시 exclamation 오브젝트 Destroy 강제(기존 HideAggroVisual 경로 보장, 주기 2s 재표시 방지 위해 '본 적대 표시 후 Cooldown 딜레이' 추가).
- [ ] `GuardHostilitySystem` 적대 전환 — `ShowTransientExclamation` 단일 경로만 쓰도록, `ShowAggroVisual`(무제한) 재사용 제거.
- [ ] **검증:** 공격→느낌표(잠깐)→공격 종료/이탈/Cooldown 시 사라짐. 적대 유지 중엔 다시 뜸(의도).
- [ ] 배치컴파일 error CS=0.

## Phase T3 — 드래그앤드롭 물리 보강 (문제3·8) 🟡
**목표:** 인벤/장비/전리품/창고 어느 창에서 출발하든 고스트가 보이고 드롭 hit-test가 정확.
- [ ] `ItemDragContext.DrawGhost` — 인벤 IsOpen과 무관하게 **현재 드래그 중이면 항상 그려지도록** 별도 HUD-레벨 렌더 주체 확보(예: InputSystem-Agnostic OnGUI가 드래그 중이면 고스트 렌더 담당). 층/창 OnGUI 의존 제거.
- [ ] `InventoryWindow.ProcessDrag` Loot/Equipment/Warehouse 분기 — `_uiScale` 반영 좌표로 슬롯 hit-test 일치 검증(계획 G-3 마감). `TryGetSlotAtScreenPoint`·슬롯 Rect 캐시가 스케일 상수와 같은 배수 사용 확인.
- [ ] 인벤 닫힘 중 전리품 출발 드래그 — LootWindow.OnGUI(387-397행) 대행 유지 + 장비칸(Equipment)/창고(Warehouse) 출발 고스트 커버.
- [ ] `Source.Loot → 인벤 그리드 MouseUp = TryTakeDraggedToInventory()` 경로가 빈 닫김 인벤에서도 동작 — 인벤 열림 강제 or TakeItem 직접.
- [ ] **검증:** 전리품/장비/창고 각 AI이 드래그 시작→고스 twitter→인벤 드롭 성공. 인벤 자힌 채 전리품 드래그 가능.- [ ] 배치컴파일 error CS=0.

## Phase T4 — 장비(방어구) 장착 활성화 (문제4) 🔴
**목표:** Test_10 씬에서 EquipmentManager·ArmorVisualAttachSystem 개념화.
- [ ] `TestTerritoryCombatSetup.EnsureGameManager`(80-84행) — `EquipmentManager`, `ArmorVisualAttachSystem`(리플렉션/AddComponent) 추가 생성. (CoreSystemsBootstrap 미실행이므로 이 씬 자체 셋업 필수.)
- [ ] `WeaponEquipManager`는 static이라 생성 불필요하나, 장착 시 `EquipmentManager.Instance` 없이도 무기 슬롯은 동작하도록 217-220행 가드가 이미 있음 → 방어구만 살아나면 됨.
- [ ] **검증:** 우클릭 wood_armor → 성공 로그 + 캐릭터 본에 GLB 부착/해제 + 장비칸 반영.
- [ ] 배치컴파일 error CS=0.

## Phase T5 — 무기 손 부착 정밀화 (문제5) 🔴
**목표:** 무기 인스턴스 렌더러 활성/스케일 + 그립을 실제 손에 정확히.
- [ ] `WeaponEquipManager.Equip`(198-215행) — 54차 강제활성(Rigidbody 제거/콜라이더 off/렌더러 강제활성/localScale 0 방어)이 **Applied로 Renderer 시점 후까지 유지**되는지. GLB 아바타 Generic 시 GetBoneTransform null → 이름 폴백(54차)이 **실제로 되는데 RightHand 본이 안 잡혀 skip** 되는지 진단 로그존→ 그립 폴백(루트 부착) 보강.
- [ ] `ApplyBoundsGripAlignment`(279행) — pivotT 신뢰 후에도 실제 손 위치와 어긋나 디버그. Play 로그(`[Weapon] 인스턴스: ... 렌더러=N`) 값으로 검/창/활 `GripPose` 오프셋 튜닝(H-5 마감).
- [ ] **검증:** 검/창/활/단도가 손바닥 그립부에, 스윙 시 이탈 없음.
- [ ] 배치컴파일 error CS=0.

## Phase T6 — 슬래시 흰색 + Free Slash 폴백 결정 (문제6) 🟡
**목표:** 사용자 요구(흰색)와 렌더 실패 대응을 확정.
- [ ] **결정:** 현재 white-blue VFX Graph(스타일라이즈드)가 렌더됨. 사용자는 "흰색 아님" 지적인데 이건 스테이지 틴트(골드/주황) 때문 → `ComboStageTint`가 **항상 CoreTint(흰)` 반환**하도록 통일(53차 수정 주석이 실제 코드 103-107에는 남아있지 않음 — 실제 검증).
- [ ] 렌더 실력(fallback) 전환 재확인: `SlashAliveProbe`가 두 체크 모두 alive≤0일 때만 Free Slash(`LoadSlashPrefab`)로 폴백. **사용자가 free slash 강조**하니 폴백 유지 + 스테이지 틴트 흰색.
- [ ] `PlaySlashStage`에서 스타일라이즈드=white-blue 원본색, 폴백=stageTint(흰) 적용 일관.
- [ ] **검증:** 1/2/3타 전부 흰 아크, 렌더 실패 시 Free Slasha 자동.
- [ ] 배치컴파일 error CS=0.

## Phase T7 — 병사 GLB/애니 + 접지 (문제7·9) 🔴
**목표:** 병사 GLB/FBX 부착 + 발 지면 고정 + inactive 해소.
- [ ] `TestTerritoryCombatSetup.CreateGuard`(673-780) — GLB 부착 로그가 0건 → **GLB 로드가 Resources.load null인지 확인**(`Models/UserProvided/Soldier_Lv1-20_Rigged.glb` 존재 확인 + 로드 폴백). 로드 실패 시 FBX 경로(재질 이식 포함)가 탆승하도록.
- [ ] 병사 `SetActive(false)`(GuardPlaceholder 665행) → 재소환/배치 시 활성화 보장. inactive 로그(`Coroutine couldn't be started`) 해소.
- [ ] 접지: `GuardPlaceholder`의 transform.position 직접 이동이 y를 배치 오프셋으로 되돌리는 것 방지 — **이동 시 y를 SurfaceY 기반 재접지** or 모델 발끔 ground-offset 유지. RB isKinematic 유지하되 movement 코드가 y보존.
- [ ] 병사 상단 구역(EnemyGateGuard 등)도 동일 접지 적용 확인.
- [ ] **검증:** 내병사 3명 + 적문지기 3명이 병사 GLB(창/방패)+애니로 렌더, 발 지면, 공격/추종 시 이탈 없음.
- [ ] 배치컴파일 error CS=0.

---

## 통합 실행 순서 (round별 배치컴파일 + Play 판정)
```
T1(단일 히트플래시·흰) → T2(느끗표 수명) → T3(드래그 물리)  [1차 배치 병렬 위임]
  ↓
T4(방어구 생성·⚠) → T5(무기 그립·⚠) → T7 병사(⚠)      [2차 배치 병렬 위임]
  ↓
T6(슬래시 흰색·⚠) → 전체 통합 컴파일 + Play 판정      [3차]
  ↓
Z (QAPROGRESS / ROADMAP / docs/TEST20_FIX_PLAN.md / 영구메모리 / git commit·push)
```
> 위임 규칙: 각 Phase는 서브에이전트(delegate_task, context "Respond in Korean")에 위임. 600s 타임아웃 시 **부모 직접** 구현(프로젝트 규칙). 병렬 배치 최대 3.

## 위험/회귀 리스크
- **T1 제거 시** `Core/HitVFX.cs`의 SpawnDamageNumber 등 다른 정적 API 사용처 회귀 확인(제거는 PlayHitFlash+HitFlashRunner만, SpawnDamageNumber는 유지).
- **T4 Test_10 생성** — ArmorVisualAttachSystem이 Player 태그 대상 본에 부착 → 플레이어 생성 전 타이밍(SetupPlayer보다 먼저 생성되면 구독 시점 폴링으로 대응, 45차 패턴).
- **T6** — 사용자가 "free slash로 바꾸는 것 고려"이므로 최종 라운드에 맞춰 스타일라이즈드 유지 vs 폴백 강제를 **Play 판정으로 결정**, 문서 갱신.
- 배치컴파일은 에디터 닫은 상태에서만 실행(에디터 점유 시 잠김).