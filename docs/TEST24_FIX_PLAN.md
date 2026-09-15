# 🎯 테스트24 후속 통합 수리 계획 (TEST24_FIX_PLAN)

> **목적**: 테스트24(2026-09-16 01:33) 피드백 + Editor.log(01:38) 실측 근본 원인 수리.
> **방침**: 기존 기능·레이아웃 유지, 발사·부착·탐지·드래그 박스 보강.

---

## 0. 테스트24 근본 원인 실측 요약

| # | 문제 | 근본 원인 (로그/코드 실측) | 난이도 |
|:-:|:--|:--|:--:|
| H1 | 활이 **우클릭**으로 발사됨(좌클릭은 차지와 충돌) | `PlayerCombat` 업데이트에서 **활 장착인지에 상관없이** ① 우클릭=차지(196-176행) ② 좌클릭=TryAttack(198)이 동시에 살아있음. 활일 때 좌클릭은 TryBowShot(363-365)이 맞지만, **우클릭 차지(ReleaseCharge)가 활에서도 근접 강공**으로 동작 → '우클릭 발사'처럼 보임 | 🔴 |
| H2 | 활 장착 시 활 애니 전환 | `HumanoidClipDriver`에 `BowEnter/IsBow` 트리거(427-434) 존재하나, 활 장착 **즉시** 전환되는지·아이들에서 활 대기 자세로 가는지 미확인 | 🟡 |
| H3 | 화살 조준점 표시 | **신규 미구현** — 활 장착 시 마우스 커서 방향으로 조준선/레티클(월드) 표시 없음 | 🔴 |
| D' | **내 소속 병사만 GLB 미부착** (적은 부착) | 로그 실측: **`Tag: RecruitedSoldier is not defined`** — ProjectSettings/TagManager.tags에 `RecruitedSoldier`가 **없음**(Guard/Monster/Enemy/Player만). 833행 `guardGO.tag = recruited ? "RecruitedSoldier" : "Guard"`에서 태그가 무효화 → 내 병사에 태그 미부착 → GLB/충돌/선택 로직 회피. **적(Guard 태그)은 정상인데 내 병사(무효 태그)만 실패**의 직접 원인 | 🔴 |
| A' | 장비 파츠 시각 미구현 | ArmorVisual GLB 로드(62차 확장자 폴백)는 적용했으나, 로그에 `OnEquipmentChanged 수신/부착`이 없음 — 장착 자체가 활/Sword만 테스트되고 방어구 우클릭이 안 됐거나, 뼈(Head/Spine) 탐지 실패 | 🔴 |
| F' | Ctrl 눌러도 드래그 안 됨 + 드래그 박스 안 보임 | ① `GuardSelectionManager.OnGUI`(140)/박스(189)는 존재하나 **Ctrl 게이트(72-83)가 Update만 막고 OnGUI까지는 아닐 텐데**, 실제로 태그 미정의(RecruitedSoldier)로 `SelectGuardsInRect`가 내 병사를 못 고름 → "드래그 안 됨" 체감 ② 사용자 "드래그 시 눈에 보이는 네모부터" — OnGUI 드래그 박스가 **부대 모드/Ctrl 진입 시에만** 그려지는지·인벤 우클릭과 충돌해서 안 보이는지 | 🔴 |

---

## Phase H1 — 활 좌클릭 발사 (우클릭 차지와 분리) 🔴
**목표**: 활 장착 시 **좌클릭 = 화살 발사**, 우클릭은 근접용(검/창) 차지만.
- [ ] `PlayerCombat.Update` — 활 장착 중이면 **우클릭 차지 블록(174-195) 스킵**(ReleaseCharge/차지 상태 진입 차단). 활은 좌클릭 TryBowShot만.
- [ ] `ReleaseCharge`/`TryChargeAttack` 진입부 — `_currentWeapon.weaponType == Bow`면 조기 return(활에 차지 강공 금지).
- [ ] 좌클릭은 Bow면 TryBowShot(이미 363-365) — 유지 확인.
- [ ] **검증**: 활 장착 → 우클릭 무반응(차지 없음) + 좌클릭 화살 발사·적중.

## Phase H2 — 활 장착 시 애니 전환 🟡
**목표**: 활을 장착하는 순간 캐릭터가 활 대기/보행 애니로.
- [ ] `HumanoidClipDriver` — `WeaponEquipManager.CurrentType == Bow`가 되면 `BowEnter` + `IsBow=true` 즉시 전환 확인(L427-434). 현재는 감시(Update)라 활 장착 직후 자연 전환되는지 Play 판정.
- [ ] 필요 시 `WeaponEquipManager.Equip(Bow)` 시점에 드라이버 `SetWeaponType`/`SetBool(IsBow)` 호출 추가(장착 즉시 강제).
- [ ] 발사 `ArcheryShot` 유지(좌클릭 성공 시).
- [ ] **검증**: 활 장착 → Idle→활 대기 자세, 걷기 시 활 보행, 좌클릭 → ArcheryShot.

## Phase H3 — 화살 조준점 표시 (신규) 🔴
**목표**: 활 장착 시 마우스 커서 방향으로 조준 레티클/라인 표시.
- [ ] 신규 `BowAimIndicator`(Systems) — 활 장착 중 매 프레임: 마우스 Ray(카메라 ScreenPointToRay) 방향으로 **지면/월드 조준점(원형 레티클)** + 짧은 진행선 렌더(LineRenderer/Gizmo). 부대 모드 아님(월드 공격 조준).
- [ ] `PlayerCombat`/`WeaponEquipManager.CurrentType == Bow` 연동 — 활 장착 시 표시, 해제/장비 전환 시 숨김.
- [ ] 좌클릭 발사 시 조준점 위치로 화살 origin orient 보정(레티클=실제 명중 지점).
- [ ] **검증**: 활 장착 → 마우스 움직임에 조준점/선 추적, 좌클릭 시 그 방향으로 화살.

## Phase D' — 내 소속 병사 태그 정의 + 영지 병사 GLB 🔴
**목표**: 내 병사(RecruitedSoldier)·내 영지 병사 모두 GLB 부착.
- [ ] `ProjectSettings/TagManager.asset` tags에 **`RecruitedSoldier` 추가** (미정의 태그 → tag 무효 → GLB/선택 회피 근본 해결). (에디터에서 설정 가능, asset 직접 수정은 safe — 1개 토큰 추가.)
- [ ] 추가로 `GuardPlaceholder`/`GuardCombatAI`가 `RecruitedSoldier` 태그 기준 로직이 태그 Lookup 미정의로 스킵되는지 확인(CompareTag는 미정의 태그와 false).
- [ ] **내 영지 소속 병사** — `SetupTerritoriesAndGuards`가 PlayerOwned 영지 병사도 `CreateGuard(recruited=true)`로 생성되게 확인 (지금 초록 캡슐이 MyGuard인지, 내 영지(Territory_PlayerOwned) 병사인지 판별).
- [ ] `CreateGuard` GLB 로드(62차 확장자 폴백) 그대로 — 태그만 정의되면 내 병사도 GLB 렌더.
- [ ] **검증**: 내 병사(MyGuard + 내 영지 병사)가 병사 GLB+애니, 태그 정상, 공격/추종 동작.

## Phase A' — 장비 파츠 시각 부착 확정 🔴
**목표**: 방어구(목갑옷/투구 등) 장착 시 캐릭터에 GLB 부착.
- [ ] `ArmorVisualAttachSystem` — 뼈 탐지(ResolveBones) 실패 시 어떤 뼈(Head/Spine)가 없는지 로그 + 폴백(캐릭터 루트 부착 or 클립 근접 뼈).
- [ ] 방어구 실제 우클릭 장착 → `OnEquipmentChanged 수신 → 부착` 로그 순서 확인 (Test_10 인벤에 wood_armor 시딩 확인 + EquipmentManager lazy 이미 62차).
- [ ] **검증**: wood_armor/helmet/boot 장착 시 캐릭터 몸에 GLB 부착·해제 제거.

## Phase F' — 드래그 선택박스 보이기 + Ctrl 드래그 (태그 연동) 🔴
**목표**: 드래그 시 **눈에 보이는 선택 네모** + Ctrl로 병사 담기.
- [ ] `GuardSelectionManager.OnGUI`(140) — 드래그 시 사각형(파란 반투명+테두리) 렌더를 **Ctrl 홀드 중이면 항상** 그리도록. (현재 박스 로직은 존재하나 Ctrl 게이트/태그로 진입 안 되어 안 보였을 수 있음.)
- [ ] `SelectGuardsInRect` — `GuardPlaceholder`에 RecruitedSoldier/Guard 태그 기준이 아닌 **객체 기준**(IsRecruited)으로 선택(태그 미정의 회피). 내 병사를 박스 안에 담으면 파란 네모에 하이라이트.
- [ ] Ctrl 게이트는 유지(단순 클릭=공격), 드래그 감지+박스 렌더가 Ctrl 홀드 중 확실히 동작.
- [ ] **검증**: Ctrl 누른 채 드래그 → 화면에 반투명 네모 표시, 네모에 닿은 내 병사 선택(파란 테두리), Ctrl+1 등록 → 슬롯 얼굴.

---

## Phase G — 무기 손 부착(그립) 정밀 튜닝 🔴
**목표**: 검/창/활이 실제 손에 정확히(거리+방향) 부착.
> 사용자(테스트24): **검=손에 붙은 느낌 없이 멀리 떨어짐 / 활=여전히 반대 손잡이 / 창=반대**. 그립 로그 2건 실측:

| 무기 | 로그 | 해석 |
|:--|:--|:--|
| Log1 (활/창) | `offset=(-0.184,-0.926,-0.240)`, bounds=(0.45,1.00,1.95), 최장축 1.95 | 휴리스틱(340행) 경로. **최장축 1.95 = 길쭉한 무기(활 or 창)**. offset y=-0.926 대형 → 손에서 크게 아래로 벗어남. 활 Y+90(62차)이 방향만 바꾸고 **배치 위치(중심)는 안 고침** |
| Log2 (검) | `pivotT=0.03`, bounds=(0.66,0.89,0.15) | 피벗 신뢰(317행) 경로. **오프셋 0** → 테이블 포즈(y0.05) 그대로. 손 위치와 무관 → "손에서 멀리 떨어짐" 체감 |

### 수리
- [ ] **Log1(활/창) 휴리스틱 과보정 해결** — `ApplyBoundsGripAlignment`에서 oc 생성 offset이 과대(±0.5 이상)하면 **테이블 GripPose.LocalPos 기준으로 클램프**(과보정 방지). 창/활은 자루 중심을 손에 두도록 `GripEnd` or LocalPos 중심 고정.
- [ ] **활 방향** — `LocalEuler Y+90(62차)`가 반대로 보임 → **Play 시각 확인 후 Y 90↔-90 재시도** + 활 손잡이(가운데)를 RightHand에 명시(양손은 좌/우 본 대신 센터 그립).
- [ ] **창 방향** — Log1과 같은 과보정 우려, 자루 중심 그립 + 창두 방향 명시.
- [ ] **검 거리** — `pivotT=0.03` 피벗 신뢰 분기에서 **손 본 중심으로 pivot을 정렬**(단순 테이블 포즈가 아니라 handBone에 밀착). LocalPos y0.05는 유지하되 손 위치 오차를 InverseTransformPoint로 보정.
- [ ] 그립 로그 강화: `[Weapon] 그립 정렬(bone, offset, bounds, pivotT, type, tip)` + 방향 표기 — 다음 Play에서 활/창/검 각각 정밀 값 확보.
- [ ] **검증**: 검 날 앞·손바닥 밀착(멀리 아님) / 창 자루 중심+창두 전방 / 활 왼손 활대·오른손 시위(반대 아님).

---

## 실행 순서 (에디터 닫고 배치컴파일)
```
D'(태그+영지병사) → H1(활 좌클릭) → H2(활 애니)  [1차]
  ↓
G(무기 그립·손부착) → H3(조준점) → A'(장비부착) → F'(드래그 박스)  [2차]
  ↓
통합 컴파일 + Play 판정 → Z(문서/QAPROGRESS/메모리/git)
```

## 리스크
- **D' TagManager.asset** — asset 직접 수정은 YAML 토큰 1개 추가. 에디터 재시작 필요(태그 목록 캐시). 안전하되 Play 전 에디터 재시작 안내.
- **H1** — 활 우클릭 차지 스킵이 기존 근접 차지(검/창) 회귀 없게 Bow 타입 분기로만.
- **H3 조준점** — LineRenderer/월드 레티클은 URP 치트키 아님, 성능 무시(단일). 부대/인벤 모드에선 숨김.
- **F'** — Ctrl 드래그 박스가 좌클릭 공격과 겹치지 않게 Ctrl 홀드 게이트 유지.