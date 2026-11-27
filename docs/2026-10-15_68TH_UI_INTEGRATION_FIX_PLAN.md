# 68차 계획 — 인벤 피그마 정합 재수리 + F키 상호작용 패널 + 선택 링 1초 소멸 + 핫바 숫자키 들기

> 작성: 2026-10-15. Figma 진실치는 2026-10-15 실측(노드 API, 파일 02vPXOGzFJUUYTaEPSVz3R, 프레임 원점 (1191,3)).
> 15:4(인벤 클러스터)와 15:33(장비창)의 EquippedSection이 **동일 개정본**으로 갱신됨 — 65차가 따랐던 구 15:33(세로 등급줄/슬롯 라벨/카드내 배지)은 폐기 대상.

## 진실치 — Figma 15:4 EquippedSection (인벤창 임베드 = 본체)

EquippedSection: 윈도우-로컬 (24,127.4) 456×230.4. 전 컬럼 x=24 기준.

| 요소 | 윈도우-로컬 | 크기 | 스타일 |
|---|---|---|---|
| SectionLabel "장착 중인 장비" | (38.4,141.8) | 87×17 | 14.4/700 #58A6FF |
| EquipGrid(10카드 5×2) | (38.4,170.8) | 427.2×172.8 | 카드 81.6×81.6, 열 스트라이드 86.4(갭4.8), 행 스트라이드 91.2(갭9.6) |
| WeaponSlotNumbers 바 | (38.4,337.4) | 427.2×18 | 스페이서 3칸(81.6×12) + 뱃지 2칸 |
| Divider | (24,365) | 456×1.2 | #30363D 50% |

카드(81.6×81.6): bg **#21262D 60%**, radius **9.6**, 테두리 없음.
- EquipBadge: (4.8,4.8) 22.6×15.8, radius 3.6, bg #58A6FF, 텍스트 "EQ" 9.6/700 #0B0E14 — **무기 카드엔 없음**
- TierStrip: **카드 최상단 가로** (0,0) 81.6×3.6 — 등급색(epic #BC8CFF / rare #1F6FEB / common #8B949E)
- 아이콘: (21.6,21.6) 38.4×38.4 (카드 중앙)
- **카드별 슬롯명 라벨 없음** (툴팁으로 대체 — 기존 라벨 삭제)
- WeaponSlotNumbers 뱃지: 18×18 radius 3.6 bg #F0F3F5, 숫자 10.8/900 #1A1D20, **카드열 4·5(0-based 3·4) 중앙 정렬** — 뱃지 x = 38.4+col×86.4+31.8

슬롯 순서(10): 투구/갑옷/신발/방패/장갑/방독면(Mask)/가방(Bag)/반지(Ring)/무기1/무기2 — EquipmentManager 매핑 66차 불변.

## 진실치 — 가방 그리드 셀 (15:4 GridRow)

- 셀 81.6×81.6, bg **#30363D 80%**, radius 9.6, 테두리 없음
- TierStrip 최상단 가로 81.6×3.6 (등급색, 빈 셀=common 회색)
- 아이콘 (21.6,21.6) 38.4×38.4
- Count: 우하단, 13.2/500 #8B949E
- 호버: 액센트 테두리(휴먼 피드백, Figma 미정의 — 이탈 시 원복)

## 진실치 — 탭/푸터/헤더 (이미 구현, 검증만)

- 탭: 활성 bg #58A6FF + 텍스트 #0B0E14, 비활성 투명 + #8B949E, 14.4/700, radius 7.2 — 구현됨(radius 토큰 불일치 시 7.2로)
- 푸터: WeightLabel 텍스트 "인벤토리 적재량" 15.6/400 #8B949E(현재 "적재량" → 텍스트/폰트 수정). WeightValue는 위조 금지 규약 유지("사용 N/40")
- 헤더 타이틀 24/700 — a5eea29b 완료

---

## Phase 1 (구현 서브에이전트 A) — 인벤 피그마 정합

파일: `Assets/Scripts/UI/Toolkit/InventoryWindowUTK.cs`, `Assets/Scripts/UI/Toolkit/InventoryClusterPanelRegions.cs`, 신규 테스트.

1. `InventoryClusterPanelRegions` 신설 Rect: `EquippedSectionLabel=(38.4,141.8,87,17)`, `InventoryDivider=(24,365,456,1.2)`, `EquipNumbersBar=(38.4,337.4,427.2,18)` (윈도우-로컬).
2. `BuildEquipPanel` 재구성(위 표 그대로):
   - 카드 = UTKSlot 루트(81.6×81.6, bg #21262D 60%, radius 9.6, border 0) — 클릭/툴팁 영역이 카드 전체가 되도록
   - TierStrip → 상단 가로 81.6×3.6(오버레이), EquipBadge 22.6×15.8(4.8,4.8) "EQ" 9.6/700, 아이콘 38.4 중앙, 슬롯명 라벨 삭제
   - 무기 카드: 배지 없음 + WeaponSlotNumbers 바(스페이서 3칸 + 뱃지 2칸, 카드열 4·5 정렬) — 카드 내부 핫키 배지 삭제
   - 섹션 라벨 "장착 중인 장비" + Divider 추가
   - 무기 로드아웃 장착 테두리 #58A6FF 유지(런타임 상태 피드백 — Figma 미정의 상태), 일반 카드는 테두리 없음
3. `RefreshEquipPanel` — 등급 표기를 보더 링 → 상단 TierStrip 색으로 전환, 나머지 바인딩 유지.
4. `BuildSlotCell`(가방) — bg #30363D 80%, radius 9.6, border 0, 상단 TierStrip 추가, 아이콘 38.4, Count 13.2/500 #8B949E. 호버 액센트 유지.
5. `ApplyDarkSlotBase/ApplyRankBorder` 재작성(인벤 한정 헬퍼 — 다른 창 영향 없음 확인).
6. WeightLabel 텍스트 "인벤토리 적재량" + 폰트 15.6. 탭 radius 7.2 확인.
7. TDD: 기존 EquipEmbedAndBackdropTests 확장 + 신규 어서션(카드 81.6/스트립 상단 가로/뱃지 22.6×15.8/라벨 부재/넘버바 뱃지 열 4·5/섹션 라벨/Diviver). 15:4 계약.

## Phase 2 (구현 서브에이전트 B) — F키 패널 + 링 1초 + 핫바 들기

파일: `Assets/Scripts/Systems/PlayerMovement.cs`, `Assets/Scripts/Systems/SelectionRingController.cs`, `Assets/Scripts/Systems/GuardSelectionManager.cs`, `Assets/Scripts/UI/Toolkit/HotbarUIUTK.cs`, `Assets/Scripts/UI/GblItemIconRenderer.cs`, `Assets/Scripts/UI/Toolkit/WeaponItemResolver.cs`(신규 — InventoryWindowUTK의 TryResolveWeaponEquip 이전/공개), `Assets/Scripts/Systems/ItemHoldManager.cs`(신규), 테스트.

- P1 **F키 → 상호작용 패널**: PlayerMovement F 스캔 병사 분기 — `RaiseInteract`(구 SoldierInteractUTK 창) → `RaiseTargetInteraction(병사.IsRecruited ? Ally : EnemyGuard, 병사)` 로 교체. NPC 분기 유지. InteractionPanelUTK는 Ally/EnemyGuard+GuardPlaceholder 지원(63차 검증済). 구 RaiseInteract 메서드는 유지(호출처 0).
- P2 **선택 링 1초 소멸**: GuardSelectionManager `lifetimeSeconds 5f→1f`, `fadeSeconds 0.8f→0.3f`; SelectionRingController 기본값 동일 변경. 주석 갱신. 공격 타겟 링(autoFadeOut=false) 무영향. SelectionRingFadeTests 기본값 어서션 추가(1.0/0.3).
- P3 **핫바 숫자키 들기**:
  - `HotbarUIUTK.Updater`: **아이템 모드에서만**(IsSquadMode=false) Alpha1~8+Keypad1~8 폴링 — `kb.fKey.isPressed`면 스킵(F+숫자=부대 등록 경로와 충돌 방지). 부대 모드는 GuardSquadHotbar.HandleSelectKeys 단일소스 유지.
  - 슬롯 아이템 id → `WeaponItemResolver.TryResolve`(무기)=WeaponEquipManager.Equip(equipId, player, type) / 비무기=GblItemIconRenderer.ResolveModelKey(→public) 후 신규 `ItemHoldManager.Hold(modelKey, player)`.
  - 동일 슬롯 재입력 = 해제 토글(무기=Unequip, 아이템=Unhold). 다른 슬롯 입력 = 교체. 홀드 중 슬롯 테두리 #58A6FF 피드백.
  - `ItemHoldManager`(Systems, 정적): RuntimeModelLoader.TryGetModel로 프리팹 → RightHand 부착, Rigidbody/Collider 제거, bounds 최대변 0.22m 스케일, 손 오프셋 (0,0.02,0). 무기 홀드 전환 시 기존 아이템/무기 정리(WeaponEquipManager.Unequip 경유). 손 본 탐색은 WeaponEquipManager에 `public static Transform GetHandBone(Animator,bool bowLeft)` 추출 후 재사용.
  - 어셈블리 방향: UI(HotbarUIUTK)→Systems(ItemHoldManager/WeaponEquipManager) OK. Systems→UI 금지 유지.
  - TDD: HotbarHoldKeyTests — F홀드 스킵/부대모드 스킵/무기 Equip 전환/아이템 Hold/재입력 해제. WeaponItemResolver 이전 후 기존 테스트 참조 갱신.

## 공통

- 컴파일 error CS 0 + EditMode 전수(현재 837/826/11 — baseline 11 집합 유지, 신규 회귀 0).
- Play 게이트는 별도(사용자 실측): ①인벤 창이 15:4와 동일 ②F키=피그마 상호작용 패널(병사 81:4/NPC 81:91) ③선택 링 ~1s 소멸 ④숫자키=들기/재입력 해제.
- stage/commit/push는 사용자 승인 후.
