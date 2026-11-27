# 65차 계획 — 장비창 피그마 정합 + 무기 슬롯 1·2 + 핫바 등록/전환 + 인벤→핫바 드래그 (2026-10-14)

## 사용자 요구 4건
1. 장비 상호작용 UI(장비창)가 피그마와 아직 다름 → 피그마 정합
2. 피그마 변형: 슬롯 마지막 2개에 1·2번 숫자 배지 → **무기 슬롯**으로
3. 무기 장착 시 핫바 1·2슬롯 자동 등록 + **숫자(1·2)로 무기 전환**
4. 인벤토리에서 핫바 숫자패드로 드래그 → 해당 핫바 슬롯에 아이템 등록

## 피그마 실측 (감독자 API — 진실치)
- 15:33 EquippedSection(최신 — 유저 변형): EquipGrid 10슬롯 = helmet/chest-armor/boot/shield/gloves/gas-mask/syringe/gas-tank + **sword(무기1)/sword(무기2)** — 마지막 2개는 **EquipBadge 없음**(장비 슬롯과 구분).
- **221:8 WeaponSlotNumbers** — 그리드 아래(y424.4) 가로 라벨 바 427×18: spacer 3 + WeaponNumber1(81.6×18: HotkeyBadge #F0F3F5 + "1" #1A1D20) + WeaponNumber2(같음 "2") — **무기 슬롯 1·2 배지**.
- 카드형 ItemSlot: 배경 #21262D, EquipBadge(#58A6FF "EQ") + TierStrip(등급색 좌측 세로줄) + 아이콘(중앙) + 라벨(하단 #8B949E) — 현재 코드(RowView 행형)와 구조가 다름.
- 93:4(과거 — 라벨 참조): 투구/갑옷/신발/방패/무기/방독면/분사기/물약/반지/목걸이 — EquipmentManager.Slot 10종(Helmet~Necklace)과 일치.

## 현재 코드 좌표 (실측)
- EquipmentWindowUTK.cs: 8슬롯 RowView(행형: UTKSlot+이름+내구도+해제버튼) — WinW 443×780(84:4 EquipmentPanel 정합) — **카드형 10슬롯으로 재구성 필요**.
- EquipmentManager.EquipmentSlot: 10종(Helmet/Armor/Weapon/Shoes/Gloves/Back/Mask/Bag/Ring/Necklace) — 이미 존재.
- WeaponEquipManager.Equip 성공 분기(:269) — EquipmentManager.SetWeaponSlot(무기 슬롯 동기화) — **핫바 1·2 등록 후크 추가 위치**.
- HotbarUIUTK: 8슬롯, Alpha1~8 → ActivateSlot, AssignItem(index, item) 정적 — 드래그 수신 HotbarSlotDropTarget(Inventory payload) **이미 구현** — Play 검증 필요.

## 구현 설계
### A. 장비창 카드형 10슬롯 재구성 (EquipmentWindowUTK.cs)
- RowView(행형) → **SlotCard(카드형)**: 5열×2행 그리드, 카드 56×74(피그마 비율 — ItemSlot 68×72 스펙 코드 스케일 반영), 구성: TierStrip(좌측 세로 3px 등급색)+UTKSlot(아이콘)+라벨(하단 11px).
- 슬롯 배치(피그마 15:33 순서): 투구/갑옷/신발/방패/장갑/가면/분사기/가방 + **무기 슬롯 1/무기 슬롯 2**(마지막 2개 — EquipBadge 없음).
- 신규 슬롯 타입: SlotDef 확장 — kind 필드(기존 장비 vs 무기 1/2) — 무기 슬롯은 EquipmentManager.Slot.Weapon 대신 **WeaponSlotIndex(1/2)** 데이터 소스(신규 WeaponEquipManager.WeaponLoadout 참조).
- **WeaponSlotNumbers 라벨 바** — 그리드 하단에 핫키 배지 2개(1/2, 무기 슬롯 아래 정렬) — 피그마 221:8 정합(배지 #F0F3F5 원형 18×18 + 숫자 #1A1D20).
- 해제/상호작용: 카드 우클릭/버튼 → 기존 UnequipSlot 경로 유지(무기 슬롯은 WeaponEquipManager.Unequip).

### B. 무기 로드아웃 2슬롯 시스템 (WeaponEquipManager.cs)
- 신규 static: `WeaponLoadout[2]` (각: itemId + WeaponType + 장착 여부) — 무기 1·2 독립 등록.
- Equip 성공 시: 현재 무기가 로드아웃[0]이면 → 자동으로 [0] 갱신 / [0] 비어 있으면 [0]에 등록 / [0] 있으면 [1]에 등록(순차 채움 — 동일 무기면 기존 유지).
- **핫바 등록**: 등록 시 HotbarUIUTK.AssignWeaponToSlot(0, item) — 핫바 **1·2번 슬롯(index 6·7? 아니 — 유저가 "핫바에도 1번과 2번슬롯에 등록" = 핫바의 1·2번 = index 0·1)**에 자동 등록(PlayerPrefs 키 동일 경로로 영속).
- **숫자키 전환**: 핫바 ActivateSlot의 Alpha1/Alpha2 → ActivateAssignedItem → 무기면 Equip 토글 — 기존 경로가 이미 Alpha1~8 → ActivateAssignedItem이므로 **핫바 1·2에 무기가 등록되어 있으면 자동으로 무기 전환** — 추가 코드 최소(장착/해제 토글 + 두 슬롯 간 전환 시 반대 슬롯은 해제 상태 유지 — 토글 계약 유지).
- 로드아웃 상태는 PlayerPrefs 영속(poison_weapon_loadout_{0}).

### C. 인벤 → 핫바 숫자패드 드래그 (검증+최적화)
- 기존: HotbarSlotDropTarget(Inventory payload 수용) — InventoryWindowUTK의 드래그 시작(payload.Source=Inventory)와 협상 — 이미 배선.
- 검증 포인트: 인벤 셀 드래그 시 payload.Source==Inventory 확인 + 핫바 슬롯 위 드롭 판정(VisualElement 좌표) — Play에서 실패 시 수리(UTKDragDrop의 hitTest 판정부).
- 유저 요구 "각 숫자패드" = 전 8슬롯 — 기존 RegisterDropTarget이 전 슬롯 등록 — 점검 후 그대로/수리.

## 실행 Phase
- P1: WeaponLoadout(2슬롯) 시스템 + Equip 후크(핫바 등록) + PlayerPrefs 영속
- P2: 장비창 카드형 10슬롯 + 무기 슬롯 1·2 + WeaponSlotNumbers 배지
- P3: 인벤→핫바 드래그 경로 점검/수리(Play 진단 후)
- P4: 컴파일 + EditMode 회귀 + Play 게이트 목록

## 타임아웃 규칙
- delegate 600s 타임아웃 시 부모 직접 코딩 진행(사용자 지시) — Phase별 단일 위임.
