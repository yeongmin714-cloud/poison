# ⚔️ 테스트23 후속 수리 계획 (TEST23_FIX_PLAN)

> **목적**: 테스트23(2026-09-16 01:00) 피드백 + Editor.log(01:03) 실측 근본 원인 수리 + UI 고품질 가독성 개선.
> **증거**: 사용자 제공 Editor.log(그립 pivotT/offset, 방어구 부착, 화살 부족, 병사 FBX 폴백, 드래그 무반응) + 영상 프레임 + 코드 실측.
> **방침**: 기존 기능(위치/레이아웃)은 유지, 디자인/부착/발사/비례만 보강.

---

## 0. 테스트23 근본 원인 실측 (Editor.log 01:03 — 62차 반영 후)

| # | 문제 | 근본 원인 (로그/코드 실측) | 난이도 |
|:-:|:--|:--|:--:|
| A-2 | 장착은 되나 파츠 미부착 | `ArmorVisualAttachSystem`이 `EquipmentManager.OnEquipmentChanged` 구독 후 Player 태그 본에 GLB 부착. 로그에 부착 확인 로그 없음 → **플레이어에 부착할 armature 본(Head/Armor 뼈) 탐지 실패 or GLB 미로드**. 62차로 장착(슬롯 기록)은 성공하나 시각 부착 경로 미도달 | 🔴 |
| B/C | 그립 그대로 | **Log1(검)**: `pivotT=0.01`(피벗 신뢰), bounds(0.17,1.00,0.29). 최장축 1.00(수직). 피벗 신뢰로 **오프셋 0** → 테이블 포즈 그대로 → 사용자 "그대로". **Log2(창)**: `offset=(0.253,-0.711,0.487)` 대형 — 창 휴리스틱 오프셋 과보정(bounds 1.52,0.55,1.80, 최장축 1.80). 62차 Y+180이 **방향만** 바꿨고 손 부착 위치(Center)는 미보정 | 🔴 |
| D | 병사 GLB 미부착 | `Resources.Load<GameObject>("Models/UserProvided/Soldier_Lv1-20_Rigged.glb")`(확장자 포함)가 **null** → FBX 폴백(6기 전부 `Humanoid FBX 부착`). 슬라임은 확장자 없는 `"Models/UserProvided/Slime_Rigged"` 로드로 성공. **확장자(.glb) 포함이 문제**일 가능성 | 🔴 |
| E | 화살 부족 | 화살 3종을 **창고(Warehouse wh_test)** 에 시딩했는데 `ArrowManager`는 **`PlayerInventory`(인벤)** 에서만 화살을 소모(`GetAllSlots`/`RemoveItem`) → 발사 시 인벤에 화살 0 → "화살 부족". 시딩 소스 불일치 | 🔴 |
| F | 드래그 전혀 안 됨 | 62차에서 GuardSelectionManager 드래그를 `squadModeActive`(Tab 부대 모드)로 게이트. `GuardSquadHotbar.Update`가 갱신하는데 **GuardSquadHotbar가 씬에 생성됐는지**·`Tab` 눌렀을 때 부대 모드 진입하는지 미확인. 안 떠 있으면 squadModeActive=false로 영구 비활성 | 🔴 |
| G | UI 비율 미변화 + 품질 | HUD는 `_canvasScale`(303행) 보유하나 미니맵/핫바는 `_uiScale` 검색 0건 → **창 크기 변경 시 하트/미니맵/핫바 고정 크기**로 비율 불변(사용자 지적). 62차 팔레트(딥차콜/브론즈/골드/양피지)는 UIStyleManager 참조창에 적용됐으나 일부 하드코딩+버 in | 🟡 |

---

## Phase T23-A — 방어구 파츠 시각 부착 🔴
**목표**: 장착 시 캐릭터 몸에 방어구 GLB가 보이게.
- [ ] `ArmorVisualAttachSystem` — Player 태그 armature에서 `Head/Spine` 등의 뼈(본) 탐지 로그 추가. 본 미발견 시 경고 1회(어떤 뼈가 없는지).
- [ ] 방어구 GLB(wood_armor/helmet 등) 로드 경로 확인 — `Resources.Load`가 null이면 로드 폴백(WeaponEquipManager와 동일 방식).
- [ ] `OnEquipmentChanged` 수신 확인 — 62차 장착 로그 다음에 ArmorVisual 부착 로그가 있는지 Play 판정.
- [ ] **검증**: wood_armor 장착 → 캐릭터 몸통에 갑옷 GLB 부착·해제 시 제거.

## Phase T23-B/C — 그립 위치·방향 동시 보정 🔴
**목표**: Log2(창/활)의 대형 오프셋 근본 해결 + Log1(검) 피벗 신뢰를 실제 손 위치에.
- [ ] `ApplyBoundsGripAlignment` — **피벗 신뢰(Log1)** 분기에서, 피벗이 최장축 끝부라도 **그립부=피벗**이라면 `handBone` 기준으로 피벗 자체를 손에 정렬(단순 축 방향만이 아니라 손 중앙에). 현재는 오프셋 0/테이블 포즈만 → 손 위치와 상관없이 부착 오차.
- [ ] **창(Log2)** — 휴리스틱 오프셋(0.253,-0.711,0.487)이 과대. pwivot/axis 산출 재검토 — 최장축(Axis) 판정이 잘못되면 대형 오프셋. 창은 **자루 중심 그립 + 창두 전방**이 되도록 `pose.GripEnd`/`LocalPos`를 Play 로그로 튜닝(Y+180은 방향만, 위치는 LocalPos로).
- [ ] 그립 로그를 `[Weapon] 그립 정렬(bone, offset, bounds, pivotT, tip)` 형식으로 강화 — 다음 Play에서 정밀 값 확보.
- [ ] **검증**: 검 날 앞·손잡이정확 / 창 자루 중심·창두 전방 / 활 왼손 활대·오른손 시위.

## Phase T23-D — 병사 GLB 로드 🔴
**목표**: 내/적 병사를 GLB로 렌더.
- [ ] `CreateGuard` GLB 로드 경로 — `"Models/UserProvided/Soldier_Lv1-20_Rigged.glb"`(확장자 포함) → **확장자 제거** `"Models/UserProvided/Soldier_Lv1-20_Rigged"` 시도(슬라임 선례). 둘 다 시도 폴백.
- [ ] GLB 로드 성공 시 `[TestTerritoryCombat] ✅ 병사 GLB 부착` 로그 확인.
- [ ] **검증**: 6기 병사가 병사 GLB(창/방패+애니)로 렌더, FBX 폴백 0.

## Phase T23-E — 화살 인벤 시딩 + 발사 🔴
**목표**: 활 착용 시 화살이 실제 발사되어 적중.
- [ ] `SeedPlayerInventory`(1008행)에 화살 3종 동일 시딩 추가(창고 아님 인벤) — ArrowManager가 인벤에서 소모하므로.
- [ ] (보강) `ArrowManager.TryShootArrow` — 인벤 소모 실패 시 창고에서 화살 이관 시도 or 인벤+창고 병합 소모.
- [ ] **검증**: 활 착용→화살 소지→좌클릭 시 화살 발사·적중(Magic)·인벤 소모.

## Phase T23-F — 드래그를 Ctrl 홀드로만 활성화 (사용자 개선안) 🔴
**목표**: 좌클릭=공격 그대로, **Ctrl 키를 누른 채 드래그할 때만** 병사 단체 선택 — Tab 모드 불필요·직관적.

- [ ] `GuardSelectionManager.Update` — `squadModeActive`(Tab 부대 모드) 게이트를 **`Ctrl 홀드`** 로 교체: `Keyboard.current`(90행 선례)의 `ctrlKey/leftCtrlKey/rightCtrlKey`(119-121행 구조 재사용)가 눌려 있는 동안에만 좌클릭 드래그 시작 + `consumeLeftClickAsDrag=true` 설정.
- [ ] Ctrl 미홀드 시: 드래그 무시 + `consumeLeftClickAsDrag` 미세팅 → PlayerCombat이 좌클릭=공격 유지(소비 플래그가 안 세워지므로 공격 흐름 무교란).
- [ ] `GuardSquadHotbar.squadModeActive` 갱신 **제거** (더 이상 이 게이트 불필요 — Ctrl 방식으로 대체). GuardSquadHotbar는 부대 등록/선택(숫자키)만 담당 유지.
- [ ] Ctrl 홀드 드래그 → `SelectGuardsInRect` 선택(Shift는 기존 additive 유지) → `Ctrl+숫자` 등록 → 슬롯 병사 얼굴 표시.
- [ ] **검증**: ① 평상 좌클릭=공격(슬래시/그립 정상) ② Ctrl 누른 채 드래그 박스로 병사 단체 선택 ③ Ctrl+1 등록 → 병사 핫바 슬롯 얼굴 표시 ④ 1키로 재선택.

## Phase T23-G — UI 해상도 비례 + 고품질 가독성 🟡
**목표**: 창 크기 변경 시 전 UI(하트/미니맵/핫바/스탯) 비율 변화 + 중세 판타지 고품질.
- [ ] `HUD` — 하트/HP라벨/EXP/버프를 `_canvasScale` 외 **해상도 변경 감지 시 스타일 재생성**(UIFont 관례) + 크기·여백을 토큰 비례.
- [ ] `MinimapUI` — `_uiScale` 도입 + 마커/게이지/프레임 비례(미니맵 위치·크기 토큰).
- [ ] `HotbarUI` — `_uiScale` + 슬롯 크기/단축키 폰트 비례.
- [ ] `UI_DESIGN_GUIDELINES` 고품질 강화 — ① **방식 텍스처 UI**(곡선 테두리/원형 트림을 적은 미세 반복 텍스처로) ② **타이포 계층**(제목=골드, 본문=양피지, 수치=마법블루 Bold) ③ **슬롯 여백/그리드 정렬** 강화 ④ **호버·검은·비활성 상태** 명시.
- [ ] 전 창이 `UIStyleManager` 토큰을 쓰도록 하드코딩 색 잔여 스캔·치환.
- [ ] **검증**: 창 크기 변경 → 인벤/미니맵/하트/핫바 전부 비례, 고품질 가독성.

---

## 실행 순서 (에디터 닫고 배치컴파일)
```
T23-E(화살 인벤)·T23-D(병사 GLB) → T23-A(방어구 부착)·T23-B/C(그립)  [1차]
  ↓
T23-F(드래그) → T23-G(UI 비례+고품질)  [2차]
  ↓
통합 컴파일 + Play 판정 → Z(문서/QAPROGRESS/메모리/git)
```

## 리스크
- 그립(G)는 GLB별 피벗/축 상이 → 로그 기반 1~2회 튜닝 필요 (H-5).
- ArmorVisual 부착은 뼈 이름/아바타 구조 의존 → 로그로 뼈 확인 후 매핑.
- 화살 인벤 시딩 중복 방지(가드).
- 드래그 Tab 게이트는 GuardSquadHotbar 생성 실패 시 무한 비활 → 자가 생성 폴백 필수.