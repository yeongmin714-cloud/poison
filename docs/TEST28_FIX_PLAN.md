# 📋 TEST28 수정 계획 — 69차 (2026-09-16, 4차 라운드)

> **입력**: 테스트 26 영상(프레임 실측 — 병사 HP바 "경비병 Lv.1" 표시 확인✓, 창 head-up 수직, 투구/방패 공중 부양, 병사-슬라임 대치 무공격) + 68차 Editor.log(스케일 클램프 실측, 어그로 등록 로그, 그립 정점 로그) + 사용자 리포트.
> **확인된 것**: 드래그 동작 ✓(늦은Ctrl 경로), 병사 HP바 ✓, 몬스터 HP바 ✓, 화살 비행 ✓(단 궤도가 화살답지 않음), 검 그립 ✓.

---

## #1 장비 착용감 2차 — 스케일 클램프 + 앵커 모드

**실측 (68차 ✅ 로그)**: 부츠 스케일 x0.40 → 결과 bounds 0.55m(목표 0.36 미달) — **클램프 하한 0.4가 목표 스케일 0.26을 잘라냄**(장갑/헬멧 동일). 또한 bounds 중심 스냅만으로는 "딱 붙는" 느낌 부족(투구/방패 공중 부양 — 영상 f10 실측: 헬멧 머리 위 부유, 방패 팔 옆 허공).
**수리 (ArmorVisualAttachSystem)**:
1. `NormalizeVisualScale` 클램프 0.4→**0.12** 하한 완화(상한 5.0 유지) — 모든 부위가 목표 치수에 도달.
2. **앵커 모드 도입**(center 스냅의 한계): Helmet/Boots = **Bottom 앵커**(bounds 최하단 → 본 원점+오프셋 — 투구는 머리에 "쓰고", 부츠는 발에 "신는"), Armor = Center(스파인), Gloves = Center, Back(방패) = Center+손 전방 0.1m. 슬롯별 오프셋 재튜닝(Helmet +0.02 / Boots +0.01).
3. 로그의 본↔중심 임계 0.35m 유지 — 다음 Play에서 부위별 수치로 미세 튜닝.

## #2 몬스터가 병사를 공격하게 — 추격/공격 대상 확장

**실측**: 어그로 등록은 성공(`슬라임 어그로 → MyGuard_1` 로그) — 그러나 공격 없음. 뿌리: AnimalAI 이동/공격 판정(L568/592)이 **전부 _player 기준 dist** — 병사가 옆에 있어도 플레이어가 멀면 몬스터는 플레이어만 추격하고 공격 판정에 들어가지 않음.
**수리 (AnimalAI)**:
1. 근접 몬스터(default/boar 케이스)의 이동·공격 판정 대상을 **"생존한 _aggroTarget 우선, 없으면 플레이어"** 로 교체 — `Vector3 targetPos = (_aggroTarget != null && 생존) ? _aggroTarget.transform.position : _player.position;` dist/방향/공격 게이트 전부 targetPos 기준.
2. 대상 전환 로그 1회(어그로 대상 추격 시작). 원거리/스킬 몬스터는 기존 유지(회귀 최소화).

## #3 병사 사망 → 쓰러짐 연출 후 전리품

**현재**: Die()가 즉시 LootBasket 생성(연출과 무관하게 바닥에 등장).
**수리 (GuardPlaceholder)**: Die()에서 전리품 생성을 **1.2s 지연**(코루틴 — 쓰러짐 연출 먼저 → 그 후 전리품 바구니 생성). 경험치/카메라 킬 이펙트는 기존 유지.

## #4 병사 경험치 + 레벨업 (몬스터/병사 사냥)

**수리 (GuardPlaceholder)**:
1. `AddEXP(int)` + 레벨업 게이트(필요 EXP = level×50): 레벨업 시 level++, `_maxHP += 10`, 데미지는 기존 level×1.5 수식이 자동 반영, 로그 + (선택) 레벨업 이펙트.
2. **킬 크레딧**: PerformAttack에서 `TakeDamage` 후 타겟 `IsAlive == false`면 이 병사에게 EXP 지급(대상 EXP = 10 + 대상 level×5 — 몬스터/다른 병사 공통, IDamageable 구현 대상). 플레이어에게 가던 병사 사망 EXP는 기존 유지.

## #5 화살 궤적 — 화살답게

**실측**: 비행은 정상(발사 성공 8회) — 속도 30이 느려 부유감. 사용자 제안(사거리/속도 증가) 수용.
**수리 (ArrowManager/ArrowProjectile)**: `_arrowSpeed` 30→**45** + 트레일 time 0.35→**0.5**(잔상 길게) + 중력 유지(포물선은 살리되 속도로 직선감 확보). 필요 시 다음 라운드 미세 조정.

## #6 그립 잔여 — 단검 + 창 방향

**실측**: 검 = 정점 그립 성공(손오차 0.000, 사용자 확인 ✓). 단검 = id 테이블 미등록으로 구 경로(손↔중심 0.206m). 창 = 그립은 정상(손오차 0.000)이나 **방향이 수직 head-up**(영상 f10 실측) — 사용자 판정 "머리/바닥 뒤집힘".
**수리 (WeaponEquipManager)**:
1. 단검 id(`wood_dagger` + `weapon_dagger_*`) 테이블 추가 — GripStrategy 2(가드 기준), factor 0.6.
2. 창 방향: LocalEuler (-90,180,0) → **(-180,180,0)** 1차 적용(수직→수평 전방) — Play 스크린샷 판정 후 필요 시 ±90 조정(그립점은 정점 기반이라 방향 변경과 무관하게 손에 고정).

## #7 활 idle/run 클립 배선 (사용자 제공분)

**파일 확인**: `Assets/플레이어 애니메이션/` — `..._Idle_Holding_Bow_withSkin.fbx`, `..._Run_Forward_with_Bow_withSkin.fbx`.
**수리 (Player_AC.controller)**:
1. **BowAimedF 모션 교체**: Walk_Forward_with_Bow_Aimed → **Idle_Holding_Bow** 클립(활 장착+정지 = 활 든 대기 — "장착 시 애니 변화" 확보). 클립 참조 = 해당 FBX guid + fileID 7400000(단일 테이크 관례 — 실패 시 FBX 임포트 클립 fileID 확인).
2. **신규 상태 BowRunF**(Run_Forward_with_Bow 클립) + 전이: `BowAimedF→BowRunF`(Speed > 런 경계) / `BowRunF→BowAimedF`(Speed ≤ 경계) / `BowRunF→Idle`(Speed<0.2). Walk/Idle의 IsBow 전이(68차) 유지.
3. 클립명은 FBX 임포트 후 로그로 확인 — 배선 실패 시 대체 fileID 탐색.

## #8 부대 핫바 더블-눌패드 등록

**현재**: Ctrl+1~8 등록(GuardSquadHotbar.HandleCtrlAssignKeys).
**수리 (GuardSquadHotbar)**: `HandleDoubleNumpadKeys()` 추가 — numpad1~8 키 wasPressedThisFrame 추적, **같은 키 0.6초 내 2회 입력 → 선택된 병사 그룹을 해당 슬롯에 등록**(기존 등록 메서드 재사용 — 선택 없으면 안내 로그). 1회 입력은 기존 1~8 부대 선택 유지.

## #9 창고/전리품 드래그 드롭 복구

**뿌리 (코드 실측)**: 드롭 판정이 **InventoryWindow.ProcessDrag가 대행**(L107/L430/L575 주석 — "인벤이 열려 있으면") → **인벤 창을 닫으면 판정 주체가 없어** 드래그가 시작돼도 드롭이 안 됨(폴백은 클릭=획득만).
**수리**:
1. **WarehouseUI/LootWindow 자체 MouseUp 드롭 판정** 추가 — 인벤이 닫혀 있을 때: 드래그 중 자체 슬롯 Rect 캐시 + MouseUp 시 화면 좌표가 인벤 창 Rect 위면 `InventoryWindow`의 기존 수용 API(외부 아이템 수용 경로) 호출로 이동. 인벤 열림 시 기존 ProcessDrag 대행 유지(이중 처리 방지 가드).
2. 진단 로그: 드래그 시작/드롭 시도/수신 결과(침묵 금지 — 67차 원칙).

## #10 인벤토리 정렬 제거 — 전체 표시

**수리 (InventoryWindow)**: 정렬 버튼 제거 + `_sortMode` 고정 None(열거형은 유지) — 모든 아이템이 원본 슬롯 순서로 전부 표시. (정렬 로직 함수는 잔존 — UI 진입점만 제거, 회귀 최소화.)

---

## 실행 순서
#1(장비) → #5(화살) → #6(그립 잔여) → #7(활 배선) → #2(몬스터 타겟) → #4(병사 EXP) → #3(전리품 순서) → #8(더블 눌패드) → #9(드래그 드롭) → #10(정렬 제거) → 배치컴파일 error CS=0 → ROADMAP/QAPROGRESS/커밋.

## 검증 (Play 판정)
① 장비가 몸에 딱 붙음(투구 머리에 쓰임/부츠 발에/방패 손 앞) ② 병사가 슬라임 근처에 서면 몬스터가 병사를 추격·공격 → 병사 HP바 하락·쓰러짐 ③ 쓰러진 후 전리품 바구니 생성 ④ 병사 킬 → EXP→레벨업(HP바 Lv 상승) ⑤ 화살이 빠르게 화살답게 날아감 ⑥ 단검 손에 정확히/창 수평 전방 ⑦ 활 장착 시 활 든 대기 애니+뛰면 활 뛰기 ⑧ 눌패드 1 두 번 → 핫바 1번 등록 ⑨ 창고/전리품 → 인벤 드래그 드롭(인벤 닫힌 상태 포함) ⑩ 인벤 전 아이템 무정렬 표시
