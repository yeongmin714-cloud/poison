# 70차 계획 — 골드 비아이템화(지갑 단일소스) + 핫바 코인 배지

> 작성: 2026-10-15. 사용자 요구: "골드를 아이템처럼 표현하지 말고 별개로 UI에 따로 표현하고,
> 골드를 습득하면 골드가 증가할 수만 있도록". clarify 무응답 → 기존검증 최소설계(60차 규약):
> 핫바 우측 코인 배지(상시) 단일 표시로 확정 — 인벤창 내부 표기는 Figma 15:4 정합 위반 우회로 배제.

## 진단 (실측)

현재 골드는 **이중 경제**:
- 지갑: `PlayerStats._gold` + `AddGold/SpendGold` + `GoldChanged` 이벤트(원장) — 상점/퀘스트/정비/용병 등 이미 지갑 사용.
- 아이템: `PlayerInventory.Gold`(id="gold", Material) — 드랍 폴백/이벤트 보상/테스트 시드가 인벤 슬롯으로 흘러들고,
  FastTravel/교회/건설/수리/깃발 UI가 `GetItemCount("gold")`로 지갑 대신 인벤을 읽음 → 표시·결제 불일치.

## Phase 1 — 획득 인터셉트 (PlayerInventory.AddItem 단일 지점)

파일: `Assets/Scripts/Core/PlayerInventory.cs`(88행 AddItem 선두).

- AddItem 진입부(item null/count 가드 바로 뒤)에 `[70차]` 라우팅 추가:
  `if (item.id == "gold") return RouteGoldToWallet(count);`
- `RouteGoldToWallet(int count)`: PlayerStats.Instance != null → `AddGold(count, "loot_gold")` + return true.
  Instance null(EditMode) → 로그 1회 + return true(호출자 경고 소음 방지).
- 이 효과(전수 커버 — 개별 소스 수정 불필요): GuardPlaceholder 폴백 드랍(632/648행), AnimalAI 보증 드랍(1103),
  WorldEventManager 보상(568/653/690), Test 시드(AllInOne 1099/TerritoryCombat 2278/2350), LootBasket 수령(156/203),
  **구세이드 골드 아이템 마이그레이션**(ApplyInventoryData → AddItem → 지갑. 로드 순서
  ApplyPlayerData→ApplyInventoryData 실측 — data.gold 복원 후 마이그레이션이라 이중/소실 없음).
- `mat_gold_ore`(금광석) 등 실제 재료는 무관 — 미변경.

## Phase 2 — 소비/표시 경로 지갑 전환 (9파일 12지점 — 기계 치환)

1. FastTravelSystem.cs:113 → `PlayerStats.Instance != null && PlayerStats.Instance.Gold >= cost`(인벤 null 가드 블록 교체)
   :165 → `PlayerStats.Instance != null ? PlayerStats.Instance.Gold : 0`  :177 RemoveItem →
   `PlayerStats.Instance != null && PlayerStats.Instance.SpendGold(cost, "fast_travel")`(실패 시 false 경로 유지)
2. FastTravelUI.cs:115 / RepairStationUI.cs:181 / ChurchUTK.cs:331 / ConstructionWindowUTK.cs:176 /
   FastTravelUTK.cs:275 / PlayerFlagUTK.cs:428 / RepairStationUTK.cs:183 → 지갑 조회 치환
   (`PlayerStats.Instance != null ? PlayerStats.Instance.Gold : 0` — 기존 삼항 구조 존중).
3. MercenaryHireUTK.cs:20/주석, FastTravelUTK.cs:20/RepairStationUTK.cs:17 헤더 주석 갱신(지갑 규약).
4. SaveManager 무변경(Phase 1이 커버).

## Phase 3 — 인벤 그리드 골드 미표시 (벨트+서스펜더)

- InventoryWindowUTK RefreshGrid(1053행 `MatchesActiveTab` 필터)에 `[70차]` 가드 추가:
  `if (slots[i].item != null && slots[i].item.id == "gold") continue;` — 구세이드 잔존 골드 슬롯도 그리드에 노출 금지.

## Phase 4 — 핫바 우측 코인 배지 (GoldBadgeUTK 신규)

파일: 신규 `Assets/Scripts/UI/Toolkit/GoldBadgeUTK.cs` + HUDUTK 부트에 Ensure 연동.

- VisualElement 기반(HUDUTK 21-42행 Ensure/Bootstrap 패턴 복제 — RuntimeInitializeOnLoadMethod(AfterSceneLoad) +
  UIRoot null 가드 재시도). HUDUTK.Bootstrap에서 Ensure() 호출로 배선 단일화.
- 디자인(GitHub-dark 일관 — 피그마 미정의 신규 요소 최소 배지):
  - pill 132×36, bg #21262D 85%, radius 6, 테두리 1 #30363D
  - 코인 원형 20×20 #E3B341 + 내부 링 테두리 #A8791C / 골드 숫자 13.2/700 #E3B341(천단위 콤마)
  - 위치: 핫바 우측 — `left = 1548 * anchorX`(핫바 우측 1532 + 16 raw), `top = rootSize.y - 124.8*k - 12`에서
    배지 높이(36) 센터 정렬 보정: `top += (124.8*k - 36*k)*0.5f`(핫바 세로 중앙). scale k는 HUDUTK/HUD와 동일 산식.
  - GeometryChangedEvent 기반 ApplyBounds(HUDUTK 387-425행 패턴).
- 값 갱신: `PlayerStats.GoldChanged` 구독 → 라벨 텍스트 갱신(변화 시에만). 구독은 배지 요소 수명과 동일(부착 시/탈부착 시).
- 실시간 피드백: 획득/지출 시 라벨 색 펄스 1회(#E3B341 유지 — 과애니 금지, 0.3s 스케일 펄스 옵션 없음 — 텍스트만 유지, 단순 갱신).

## Phase 5 — TDD

- 신규 `Assets/Scripts/Tests/EditMode/GoldEconomyTests.cs`(패턴: HotbarHoldKeyTests 선례):
  1. AddItem_Gold_RoutesToWallet_NotInventory — 리플렉션으로 PlayerStats.Instance+`_gold` 임시 세팅
     (EditMode AddComponent Awake 미호출 규약 — 67차 실측), `AddItem(PlayerInventory.Gold, 10)` →
     wallet +10 / `GetItemCount("gold")==0` / GetItemCount 일반 아이템은 인벤 저장 유지(대조 케이스).
  2. RouteGoldToWallet_NoInstance_NoThrow — Instance null 경로 예외 금지.
  3. InventoryGrid_SkipsGoldSlots — InventoryWindowUTK 소스에 `[70차]` 골드 스킵 가드 마커.
  4. WalletReaders_Unified — 9파일 소스에 `GetItemCount("gold")` 잔존 0건(주석 제외 — 실제 코드 라인 기준) + FastTravelSystem SpendGold 마커.
  5. GoldBadgeUTK_Wired — 소스에 GoldChanged 구독 + Ensure 부트 + 1548/anchorX 배치 마커.
- 회귀: 기존 ShopLootFigmaPhase3Tests(`AddGold(price, "shop_refund")`) 등 지갑 계약 무영향.

## 공통

- 컴파일 error CS 0 ×2회 / EditMode 전수(866/855/11 유지 — 신규 회귀 0).
- Play 게이트(사용자): ①적 처치 → 전리품 수령 시 인벤에 "금" 슬롯 없음 + 핫바 배지 숫자 증가 ②인벤 열어도 골드 미표시
  ③빠른이동/수리/교회 등 골드 결제 = 배지 차감 반영 ④구세이브 로드 시 골드 아이템 → 배지 합산 ⑤WASD/UTK 창 무사고.
- stage/commit/push는 사용자 승인 후.
