# 🔫 테스트21 후속 통합 수리 계획 (TEST21_FOLLOWUP_PLAN)

> **목적**: 사용자가 테스트21 피드백 + Editor.log로 제공한 오류들을 근본 원인 확정 후 수리.
> **증거**: 사용자 제공 Editor.log(그립 pivotT/offset, Rigidbody 의존, inactive, ArrowManager 등) + 코드 실측.
> **기준**: 배치컴파일 error CS=0. 각 Phase 후 Play 판정.

---

## 0. 근본 원인 실측 요약

| # | 문제 | 근본 원인 (코드/로그 실측) | 난이도 |
|:-:|:--|:--|:--:|
| A | 방어구 우클릭 장착 실패 | `EquipmentManager.Instance`가 null로 보임(`[WeaponEquipManager] EquipmentManager.Instance 없음` 반복). 60차에서 EnsureGameManager에 생성 추가했으나 **최신 로그에 생성/이미존재 로그가 안 찍힘** → 씬 진입 순서 또는 FindAnyObjectByType로 inactive 잔여 GO에 막힘 | 🔴 |
| B | 검 그립: 날 방향 반대 | `pivotT=0.96`(피벗=그립부 신뢰). 검이 피벗이 **날 끝**에 있는 GLB인데 피벗 신뢰 규칙(pivotT≥0.85)이 적용돼 블레이드가 손잡이로 오판 → 날이 뒤쪽으로 | 🔴 |
| C | 활 그립: 왼손 활대/오른손 시위 반전 | `Bow LocalEuler=(0,-90,0)` + GripPose — 활 GLB가 좌우 미러돼 손잡이·시위 방향이 뒤집힘 | 🟡 |
| D | 병사 플레이스홀더(GL B K), MyGuard inactive | ① `PlayerPlaceholder.TryLoadGLBModel:108` — `Can't remove Rigidbody because ProceduralAnimationController depends on it`: DestroyImmediate(rb)가 의존 컴포넌트로 차단됨 → RB 잔존 → 중력 낙하 ② MyGuard_1/2 `inactive` → HitReaction Flinch 코루틴/애니 불가(부활/재활 문제) | 🔴 |
| E | 화살 발사 시스템 미구현 | `ArrowManager` 클래스는 존재하나 **Test_10 어디에서도 생성(AddComponent) 안 됨** — `ArrowManager.Instance` null → `TryShootArrow` 스킵. 활 장착 시 `BowEnter` 애니 트리거 코드는 존재 | 🔴 |
| F | 드래그(병사 단체 선택) | `consumeLeftClickAsDrag`가 **모든 좌클릭**(단순 클릭 포함)을 드래그로 소비해 공격이 막힐 수 있음. 사용자 제안: **Tab(병사 부대 핫바) 모드에서만 드래그 활성화** | 🟡 |

### 추가 (사용자 로그에서 노출된 비차단 오류 — 수리 대상)
- `[GroundWatch] Ground_Inner가 씬에서 사라짐` (PlayerMovement 1256)
- `[AnimalAI] slime: RigAnimationController/QuadrupedProceduralAnimation 없음 → 공격 애니 메딜 출력` (AnimalAI 762)
- `[AnimalAI] BuffManager 인스턴스를 찾을 수 없습니다` (AnimalAI 813)

---

## Phase A — 방어구 장착 확정 (EquipmentManager Instance null 해결) 🔴
**목표**: Test_10 씬에서 `EquipmentManager.Instance`가 반드시 세팅되도록.
- [ ] `TestTerritoryCombatSetup.EnsureGameManager` — `FindAnyObjectByType<EquipmentManager>(FindObjectsInactive.Include)`가 **inactive 잔여 GO**를 집어 생성을 건너뛰는 경우를 막음. `GameManager.Instance.gameObject`에 **not instance 로그**를 늘리고, Instance==null이면서 GO만 존재하는 경우 GO를 활성화(SetActive(true)) or 새로 생성.
- [ ] `EquipmentManager.Awake`(52행) — Instance 세팅 로그(`[EquipmentManager] Instance={this}`) 추가 → 다음 Play에서 생성/세팅 여부 즉시 판별.
- [ ] `InventoryWindow.TryEquipItem` Armor 분기(3446) — em==null이어도 생성 시도(Ensure EquipmentManager 1회, GuardHostilitySystem 선례) 후 재시도.
- [ ] **검증**: 우클릭 wood_armor → `[Equip] 우클릭 장착 wood_armor → 결과 성공(Armor…)` + 장비칸 반영 + ArmorVisual GLB 부착.

## Phase B — 창 그립: 창두 방향 수정 🔴
**목표**: 창 뾰족한 부분(창두)이 앞(전방)을 향하게.
- [ ] `WeaponEquipManager._gripTable[Spear].LocalEuler` — 기존 `(-90,0,0)`에서 **`Y +180`** 보정(`(-90,180,0)`)으로 창두(뾰족)가 뒤로 가던 것을 전방(+)으로. **적용 완료 [61차]** — Play로 offset/pivotT 로그 확인 후 원([창두 전방])로 미세 조정.
- [ ] 적용 후 `[Weapon] 그립 정렬(...)` 로그의 pivotT·offset로 창두 방향 확정.
- [ ] **검증**: 창을 들면 뾰족한 부분이 정면을 향해 손에 자루 중심으로 정확히 잡힘.

## Phase B2 — 검 그립 (사용자 정정: "손에서 조금 어긋난 정도") 🟡
**목표**: 검은 뒤집힘이 아니라 단순 오프셋 미세조정만.
- [ ] 검은 날 방향 정상(사용자 확인) — `GripPose.Sword.LocalPos`를 Play 로그 offset값으로 미세 이동(ex. y/z 몇 cm). 뒤집힘 없음.
- [ ] **검증**: 검 날이 앞, 손잡이에 정확히(조금 어긋남 0).

## Phase C — 활 그립: 좌/우 손잡이 방향 🔴
**목표**: 왼손이 활대(상단), 오른손이 시위(하단).
- [ ] `WeaponEquipManager._gripTable[Bow].LocalEuler` — `(0,-90,0)` → 날 방향/좌우 미러 보정(활 GLB 축 실측 후 `Y` 반전 + 필요한 `X/Z` 회전). bow는 양손(GripEnd 자동)으로.
- [ ] 적용 후 `[Weapon] 그립 정렬(bow)` offset 로그로 확인.
- [ ] **검증**: 활을 잡는 즉시 왼손 활대/오른손 시위.

## Phase D — 병사 GLB 부착 + inactive 해소 🔴
**목표**: 내 소속 병사가 GLB 모델+애니로 렌더, inactive 사망 잔존 해소.
- [ ] `PlayerPlaceholder.TryLoadGLBModel:108` — Rigidbody 제거를 의존 Destroy 실패로부터 보호: `ProceduralAnimationController/QuadrupedProceduralAnimation` 의존이 있으면 **제거 대신 isKinematic+useGravity=false+detectCollisions=false(현재 이미)** 유지 + DestroyImmediate 실패를 try-catch로 감싸 중단 없이 진행(중력 낙하만 방지면 충분).
- [ ] `GuardPlaceholder` inactive(665) — 사망 후 `Resurrect`(1093)가 다시 SetActive(true)하나, `HitReactionDriver.PlayFlinch`가 inactive GO에서 코루틴 실패 → HitReactionRunner/CombatFXGate에서 `gameObject.activeInHierarchy` 체크 후 inactive면 **스킵(반환)** 아니라 위치 기반 폴백 or 활성화시도.
- [ ] `TestTerritoryCombatSetup.SpawnGuard/CreateGuard` — MyGuard GO가 active 상태로 시작 + 사망 시 `_isDead` 처리 후 재활성 경로 확인.
- [ ] **검증**: 병사 GLB 렌더 + 이동/공격 애니 + 피격 flinch(비활성 불가 메시지 0).

## Phase E — 화살 발사 시스템 구현 🔴
**목표**: 활 장착 → 좌클릭 → 화살 발사체가 날아가 적중.
- [ ] `TestTerritoryCombatSetup.EnsureGameManager` — `ArrowManager` AddComponent(중복 가드) + 무기 장착 시 활 스폰 포인트 연결. (`ArrowManager.Instance` null이 원인).
- [ ] `PlayerCombat.TryBowShot`(430) — `ArrowManager.Instance` null이면 **생성 시도**(Ensure 1회) 후 재시도, 그래도 null이면 경고+스킵(기존 회귀 방지).
- [ ] `HumanoidClipDriver.TriggerBowShot/ArcheryShot` — 활 장착 즉시 Idle 에서 활 보행/대기 애니로 전환(`IsBow/BowEnter`). 기존 트리거 경로 확인.
- [ ] `ArrowProjectile.Spawn` — 화살 발사체가 이동·적중(레이캐스트·물리)·파괴 확인. ArrowManager.TryShootArrow가 화살 소모(PlayerInventory arrow_*) 포함.
- [ ] **검증**: 활 장착→화살 소지→좌클릭 시 화살이 날아가 몬스터/병사 적중, 화살 인벤 소모, ArcheryShot 애니.

## Phase F — 드래그를 Tab(병사 핫바) 모드에서만 활성화 🟡
**목표**: 좌클릭=공격 유지, **Tab(부대 모드)에서만** 드래그 선택 → 핫바 슬롯 등록 → **슬롯에 병사 얼굴 표시**.
- [ ] `GuardSelectionManager` — 좌클릭 드래그 감지 게이트에 `GuardSquadHotbar.IsSquadMode()`(Tab 부대 모드) 조건 추가. 부대 모드가 아니면 `_isDragging` 무시+`consumeLeftClickAsDrag` 미세팅(PlayerCombat 공격 유지).
- [ ] `GuardSquadHotbar` — `public static bool IsSquadMode()` 노출(Tab 상태).
- [ ] `PlayerCombat` — `consumeLeftClickAsDrag` 소비가 부대 모드일 때만 발동 (이미 Tab 게이트라 면 된다).
- [ ] **[핫바 얼굴 표시 — 이미 구현 확인]** `GuardSquadHotbar.RefreshSlotVisual` → `GuardIconRenderer.GetOrCreateIcon(lead)`(실제 3D 병사 얼굴 아이콘, 오프스크린 베이크)를 슬롯 아바타로 표시. `Ctrl+숫자` 등록 시 `_slots[index]` 저장 + 즉시 아바타 반영. → **드래그 활성화되면 얼굴 표시는 자동 연결**. 남은 것은 드래그→선택→Ctrl+등록 플로우 Play 검증.
- [ ] **검증**: ① 평소 좌클릭=공격 ② Tab 누른 뒤 드래그 박스로 병사 단체 선택 ③ Ctrl+1 등록 → 하단 병사 핫바 슬롯 1에 병사 얼굴(3D 아이콘) 표시 ④ 1키로 해당 병사 재선택.

---

## Phase G — UI 디자인 전면 교체 (Medieval Fantasy × Duckov Cleanish) 🟡
>  상세: `docs/UI_DESIGN_GUIDELINES.md` 참조.
- [ ] **G-1** `UIStyleManager` 파레트 전환 — 미드나이트 블루 → **딥 차콜 레더 패널 + 브론즈/벤틱골드 테두리 + 양피지 화이트 타이포 + 마법블루/희귀골드 스탯**.
- [ ] **G-2** `UIFont` 타이포 계층 확정(Display/Title/Heading/Body/Stats/Badge) + `_uiScale`/폰트 스케일 무결성(비16:9·해상도변경 폰트 왜곡 방지).
- [ ] **G-3** 기존 창 전수 적용 — **인벤토리=왼쪽·창고=오른쪽·전리품=우측·장비=우측·체력·미니맵·핫바 위치 규칙은 유지**한 채 색/폰트/간격/테두리만 교체. (`UIStyleManager` 참조 창 전수: Inventory/Equipment/Warehouse/Loot/Status/Minimap/Hotbar/HUD + 퀘스트/레시피/연금/병사/크래프트.)
- [ ] **G-4** 신규 UI 작성 원칙 — 모든 새 창은 `UIStyleManager` 토큰 + `UIFont` 계층 + `_uiScale`만 사용(로컬 하드코딩 금지, static 캐시 재생성 회피).
- [ ] **검증**: 배치컴파일 error CS=0 + Play 시각(테두리/가독성/스케일) 재검증.

---

## 실행 순서
```
B(창 그립 Y 전방 — 적용 완료) · A(방어구·Instance) → B2(검 미세)·C(활 좌우)  [1차]
  ↓
D(병사 GLB/inactive) → E(화살 발사)  [2차]
  ↓
F(드래그 Tab 모드 → 핫바 병사 얼굴, 이미 구현 연동) → 통합 컴파일 + Play 판정  [3차]
  ↓
G(UI 디자인 전면 교체 — docs/UI_DESIGN_GUIDELINES.md, 좌:인벤/우:창고 유지)  [4차]
  ↓
Z (QAPROGRESS / ROADMAP / 이 계획서 / UI 가이드라인 / 영구메모리 / git commit·push)
```
> 위임 규칙: 서브에이전트(delegate_task, "Respond in Korean"). 600s 타임아웃 시 부모 직접(프로젝트 규칙). 배치 최대 3 병렬.

## 리스크
- **B/C 그립**은 GLB별 피벗/축이 제각각 → 로그 offset/pivotT 기반으로 1회 튜닝 후 Play 확인 필요(H-5).
- **D** Rigidbody Destroy 실패는 이미 isKinematic 처리 권고 — 제거 시도 대신 중립 상태 유지로 회귀 리스크 낮음.
- **E** ArrowManager Instance 생성이 PlayerCombat·HumanoidClipDriver와 순서 충돌 가능 → Ensure(중복 가드+폴링)로.
- **F** 부대 모드 게이트로 기존 좌클릭 공격/콤보 회귀 우려 — 평상 시 공격 경로 그대로 유지 검증.