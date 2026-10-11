# Figma 1920×1080 UI 전면 정합 — 구현 계획

> 상태: 조사·계획 완료 / 구현 승인·착수 전
> 기준 파일: Figma `02vPXOGzFJUUYTaEPSVz3R` (제목 없음)
> 이번 조회의 Figma `lastModified`: `2026-10-04T15:24:28Z`
> 프로젝트: Unity 6000.4.10f1, `/mnt/c/Unity/code`

## 목표

최신 Figma의 1920×1080 캔버스와 연결된 실제 UI 프레임을 단일 기준으로 삼아, 게임 화면의 패널 구성·위치·크기·슬롯 수와 배치·하위 창·사용자 동작까지 대조하고 차이가 있는 화면을 단계적으로 재구성한다. 코드가 Figma와 충돌하면 Figma의 설계와 상호작용에 맞춰 게임 UI 흐름을 수정하되, 데이터/경제/전투 같은 도메인 규칙은 해당 시스템의 공개 API를 통해 보존한다.

## 이번 조사에서 확인한 Figma 현황

이번 직접 API 조회에서 Figma 파일이 실제로 갱신됐고, 1920×1080 크기의 캔버스가 확인됐다. 이전 계획의 1440×900 치수와 1.333배 환산값은 낡았으므로 구현 기준으로 사용하지 않는다. 노드의 `absoluteBoundingBox`를 그대로 1920×1080 좌표로 취한다.

38개 최상위 프레임은 모두 서로 다른 38개 화면을 뜻하지 않는다. 캔버스 프레임, 중복/변형 화면, 작은 HUD/인터랙션 부품이 섞여 있다. 구현 전 프레임별 ID·역할·중복 관계를 정리한다. 주요 캔버스는 아래와 같다.

| Figma 캔버스 | 프레임 ID | 확인된 주요 자식 / 의미 |
|---|---|---|
| 인벤토리·상세·창고 | `15:4` | 인벤 504×912 @ (144,84), 상세 576×729.6 @ (672,175.2), 창고 504×912 @ (1272,84) |
| HUD | `23:6` | 게이지·퀵슬롯·Tab 힌트 포함 |
| 제작·요리 | `63:5`, `64:2`, `64:337` | 각기 3패널 캔버스. 대표 `64:2`: 제작 504×912 @ (144,84), 상세 576×912 @ (672,84), 재료창고 504×912 @ (1272,84) |
| 병사관리 | `69:4` | 목록 480×984 @ (168,48), 상세 576×984 @ (672,48), 배치 480×984 @ (1272,48) |
| 전리품 | `70:4` | 단일 504×432 패널 @ (708,324) |
| 수집 결과 | `72:4`, `72:80`, `72:143` | 낚시/채집/광질의 세 결과 변형 |
| 상점 | `74:4`, `76:7` | 상점 504×912, 상세 576×912, 보관/판매 504×912 패널 |
| 상태 | `83:4` | 상태 패널 576×936 @ (672,72), Telemetry overlay 포함 |
| 상태·장비 | `84:4` | 상태 576×936 @ (391.8,72), 장비 531.6×936 @ (996.6,72) |
| 퀘스트 | `93:230` | 목록 480×984, 상세 624×576, 보상 480×984 |
| 전투로그 | `94:6` | 744×984 패널 @ (588,48) |
| 월드맵 | `95:167` | 전체 캔버스, 지도·나침반·헤더 |
| 낚시 | `160:4` | 전체 캔버스, 미니게임 패널 336×345.6 @ (792,367.2) |

작은 Figma 프레임도 별도 트랙으로 포함한다: 인터랙션 패널(`81:*`,`82:*`,`93:185`), NPC 대화(`163:23`,`187:14`), 몬스터 HP(`101:5`), 선택 링(`156:18`,`172:2`), 포션 상태(`173:4`), fluent gauge(`175:65`). 일부는 월드/화면 오버레이이지 창이 아니다.

## 구조 원칙 및 구현 기준

1. **Figma 캔버스와 UI 최상위 구성을 먼저 대조한다.** Figma에서 한 캔버스 아래 형제 패널로 배치된 디자인은 같은 composition의 sibling panel들로 표현한다. 내부 3열을 요구하는 화면은 child columns로, Figma가 진짜 개별 창인 경우에만 독립 창으로 둔다. `UTKThreeColumnLayout.Place()`의 균등 3등분을 Figma 절대 좌표 대용으로 쓰지 않는다.
2. **1920×1080 좌표를 직접 사용한다.** 현재 PanelSettings의 ReferenceResolution·ScaleWithScreenSize·match 설정을 확인하고, 1920×1080에서 Figma bounds가 일치하는지 검증한다. 다른 화면 해상도는 별도 반응형 확인 대상이며, Figma 수치를 임의 배율로 다시 곱하지 않는다.
3. **빈 칸·최대 슬롯 수·행/열을 디자인 계약으로 취급한다.** 데이터 개수에 따라 유동 배치되는 현재 UI와 Figma의 고정 슬롯 그리드(예: 재료가방 5×5)는 다르다. 실제 허용량과 빈 슬롯 표시를 맞추되, 저장 용량이나 아이템 제한을 임의로 바꾸지 않는다.
4. **선택된 프레임이 여러 변형을 가진 경우 변형 목록을 만든 뒤 구현한다.** `crafting-panel`은 `63:5`, `64:2`, `64:337` 세 개가 있고, 상점·결과 화면도 프레임 변형이 있다. 한 변형을 전체 화면에 임의로 대입하지 않는다.
5. **Figma에 없는 화면을 상상으로 Figma 준수 처리하지 않는다.** 현행 93개 Toolkit 스크립트 모두가 Figma-backed 화면은 아니다. 제공된 화면/부품과 연관된 런타임 UI를 우선 전환하고, Figma 프레임이 없는 UI는 현재 작동을 보존하며 별도 자체설계 유지 목록으로 명시한다.
6. **프레임워크/공통 인프라는 별도 보호한다.** `UTKWindowBase`, `UTKWindowManager`, `UTKDragDrop`, `UTKControls`, `UTKTheme`, `UTKWireUp` 등은 다수 화면의 기반이다. 특정 창을 맞추려고 공통 Theme.uss, 드래그/창 관리자, 공유 슬롯을 무차별 변경하지 않는다.
7. **시각 정합과 기능 정합은 독립 수락항목이다.** 슬롯 위치만 맞고 클릭·드래그가 옛 인덱스를 참조하거나, 버튼은 보이지만 실제 API를 부르지 않는 상태를 통과시키지 않는다.

## 검증된 게임 UI 구조와 초기 갭

- **인벤토리 클러스터:** `InventoryWindowUTK`, `ItemDescriptionWindowUTK`, `WarehouseWindowUTK`는 현재 별도 `UTKWindowBase` 인스턴스다. 갱신 계획은 Figma 15:4 캔버스의 좌/중/우 위치·panel bounds를 적용하고, composition open/close를 정리한다. Inventory 선택→Detail 표시, 창고 양방향 DnD·실패 rollback, 슬롯 인덱스 매핑은 상호 의존 경로다.
- **제작/요리/연금:** `CraftBenchBaseUTK`의 재료+결과+레시피북 흐름, `CraftingWindowUTK`의 레시피 상세/프리셋, `CookingWindowUTK`의 RecipeCatalog 조합, `CookingBenchUTK`의 레거시 CookingDatabase 및 `AlchemyBenchUTK`/`AlchemyStationUTK`의 연금 DB는 서로 다른 도메인 계약이다. 단일 레이아웃으로 합치더라도 제작 알고리즘을 섞지 않는다. CookingWindowUTK는 현재 760×620 단일창 내 레시피·상세·가변 재료목록이며, Figma 64:2는 제작/상세/고정 5×5 재료가방 세 패널이다.
- **상점:** `ShopWindowUTK`에는 내부 구매/상세/판매 3영역이 이미 있으나, 코드 bounds·비율·판매 탭/패널 노출 방식이 Figma 캔버스와 다르다. 경제 가격, 구매/판매/환불, 영지별 밀매 라우팅은 변경하지 않고 패널 구조와 배치를 먼저 맞춘다.
- **상태·장비:** `StatusWindowUTK` 내부 영역 배치와 독립 `EquipmentWindowUTK`가 공존한다. Figma `83:4`와 `84:4`의 관계/컨텍스트를 다시 분류하고 슬롯 정의가 중복되지 않도록 한다. EquipmentManager 장착·해제 API를 단일 데이터 소스로 유지한다.
- **나머지 화면:** 병사관리/퀘스트/전투로그/월드맵/결과창/낚시/월드 상호작용/대화/몬스터 바/가스 포션·선택 링은 각각 실제 UI owner와 호출 경로를 추적한다. 과거 문서의 준수 체크는 검증 증거가 아니라 비교 단서로 취급한다.

## 단계별 실행 계획

각 구현 단위는 아래 공통 순서를 거친다: (1) 지정 프레임의 실제 node tree·render 보관, (2) 활성 런타임 창/씬/호출자·데이터 소스 확인, (3) Figma-vs-game gap/수락 기준 확정, (4) 테스트를 먼저 작성, (5) 해당 패널/로직 재구성, (6) 컴파일 및 관련 EditMode 결과, (7) 1920×1080 Play 캡처 대조, (8) 결과 기록. 한 번에 하나의 클러스터를 전환하며, 기존의 무관한 미커밋 파일은 건드리거나 되돌리지 않는다.

### Phase 0 — 기준선·프레임-클래스 인벤토리

**대상:** Figma full-canvas 16개와 UI 부품 22개, `Assets/Scripts/UI/Toolkit` 93 C# 파일, 레거시 `Assets/Scripts/UI` 진입점, `GameSetup`/`UIToolkitBootstrap`/`UTKWireUp` 및 상호작용 호출자.

**작업:**
- API에서 최신 전체 node tree와 각 캔버스/부품 bounds, grid rows/columns, gaps, padding, labels, fill 상태를 재저장하고 중복 프레임을 `canonical frame + variant + shared component` 표로 정규화한다.
- 각 프레임을 정확한 runtime owner/type/entry route와 연결한다. 프레임 없음·호출부 없음·legacy 중복·비활성/우회 경로도 표시한다.
- 저장된 노드 JSON과 프레임별 PNG render를 버전/파일 수정 시각과 함께 보관해 이후 화면 대조의 기준 artifact로 만든다.
- 현재 Unity source, `.asmdef`, panel settings, runtime input, 관련 EditMode tests, 현재 편집기의 실제 실행 상태를 기록한다. 작업 트리에 이미 여러 unrelated user changes가 있으므로 파일 소유/겹침 표를 먼저 만든다.

**통과 기준:** 모든 프레임에 1개 이상의 owner 또는 “아직 구현 안 됨” 기록, 모든 93 UI Toolkit 파일은 Figma-backed / supporting infra / custom-only 중 하나로 분류, frame variants/dedup 설명 포함. 이 단계에서 게임 소스·씬을 수정하지 않는다.

### Phase 1 — 1920 기준 좌표계·패널 composition 기반

**대상:** `PanelSettings.asset`, `UIToolkitBootstrap.cs`, `UTKWindowBase.cs`, `UTKThreeColumnLayout.cs`, `UTKWindowManager.cs`는 필요한 경우에만; 테스트는 전용 Layout/Composition test.

**작업:**
- PanelSettings reference resolution과 match 설정을 실측해 1920×1080 Figma 캔버스와 대응시키는 좌표 규칙을 확정한다.
- 절대좌표 배치/창 내부 패널 배치/화면 overlay/월드 overlay를 각각 분리한다. `Place()` 균등분할, `PlaceAt()` absolute, composite canvas root의 용도를 테스트로 고정한다.
- sibling panels가 여러 개 있는 캔버스의 그룹 lifecycle(열기/닫기/ESC)과 panel별 close 버튼 동작을 구성 helper로 정의한다. 기존 호출자는 public static Open/Toggle 계약을 통해 접근하게 하고, 화면 간 controller 중복과 ESC stack 충돌을 방지한다.
- 안전한 UI scale/resolution 변화 규칙을 추가하되 1920×1080 기준 좌표와 panel ratios가 Figma와 1:1임을 우선한다.

**통과 기준:** 1920×1080에서 캔버스·패널 위치/크기 일치, 대표 화면 2~3종에 대하여 1600×900 및 2560×1440에서 잘림/입력 불능 없음, 기존 ESC/close/drop-target 수명 회귀 없음. 공유 프레임워크 변경은 최소화한다.

### Phase 2 — 인벤토리·상세·창고 composition (15:4)

**파일군:** `InventoryWindowUTK.cs`, `ItemDescriptionWindowUTK.cs`, `WarehouseWindowUTK.cs`, 필요 시 전용 layout builders/tests.

**작업:**
- Figma 좌 504×912 / 중앙 576×729.6 / 우 504×912를 배치한다. 인벤 5열, 장비 5×2, 중앙 상세 구조, 창고 고정 행/열 및 footer/capacity 등 실제 노드트리의 내부 슬롯 레이아웃을 별도 추출한다.
- 현재 generic 3-column `Place()`/별도 창을 Figma 캔버스 구성과 일치시키되 each top-level visual panel이 independently close 가능한지 Figma close buttons, game open routes와 맞춘다.
- 선택 아이템과 설명창 sync, item context, equipment tags, capacity/weight display, tabs, empty slot placeholders를 구현한다.
- 창고 grid를 Figma 5열/고정 empty slots 및 하단 action controls에 맞춘다. `WarehouseSystem` tenant key, filtering index→actual index, Deposit/Withdraw rollback, drag payload 등 데이터 계약은 그대로 단일 source API 호출한다.

**통과 기준:** Figma grid counts/layout test; 아이템 선택/장착/해제/인벤→창고/창고→인벤/가득 참 rollback test; UI route/open-close tests; 1920 screenshot. Warehouse save/load 호환성 점검.

### Phase 3 — 전리품 panel (70:4)와 상점 3패널 variants (74:4, 76:7)

**파일군:** `LootWindowUTK.cs`, `ShopWindowUTK.cs`, `SmuggleWindowUTK.cs`, 관련 DnD tests.

**작업:**
- Loot Figma 504×432 centered layout, 실제 5×2=10 slots, empty placeholders, tier strip, item count, footer actions를 노드 대조로 맞춘다. 바구니 수량/인벤토리 용량이 10 초과일 때 정책을 정의한다(보이지 않게 버리면 안 됨: pagination/overflow/TakeAll 중 Figma 및 logic evidence에 맞는 처리).
- Shop 504/576/504 패널 좌표/너비/높이, item slot 68×68, tabs, detail image/stats, sell inventory stock grid를 캔버스 좌표로 재배치한다. Figma variants `74:4`/`76:7`의 차이를 node tree에서 비교해 user faction/nation state mapping을 명시한다.
- 기존 거래 로직(가격·할인·골드·재고·환불), 3열 refresh, territory location, smuggling separate window/only enemy territory gates를 preserve한다.

**2026-10-05 구현 상태:** 저장 기준선에서 LootPanel (708,324,504,432), 내부 456×172.8, 10칸 81.6px/9.6px 확인. Shop variants는 세 패널 bounds와 공통 구조가 동일하고 central action만 Buy(74:4)/Sell(76:7)로 다름; 목업 아이템/잔액은 진영 판정 증거가 아님. UTK에 고정 10슬롯 placeholder와 초과 loot scroll, 3패널 shop 배치를 반영했고 기존 거래·밀매 진입 계약을 유지함. compile exit 0, focused EditMode 5/5 통과. Play 캡처 및 구매/판매 실상호작용·초과 슬롯 시각 확인 전이므로 Phase 3는 partial이며 승인되지 않음.

**통과 기준:** 10-slot overflow policy test; purchase/sell/sufficient gold/insufficient gold/inventory full rollback tests; variant route tests (unsupported faction assumptions prohibited); drag/selection; screenshots.

### Phase 4 — 제작/요리/연금 panels + detail + ingredients bag (63:5, 64:2, 64:337)

**파일군:** `CookingWindowUTK.cs`, `CookingBenchUTK.cs`, `CraftBenchBaseUTK.cs`, `WeaponForgeUTK.cs`, `AlchemyBenchUTK.cs`, `AlchemyStationUTK.cs`, `CraftingWindowUTK.cs`, `RepairStationUTK.cs` (Figma 배킹 존재 여부를 먼저 확인; 비배킹이면 자체 UX 유지).

**작업:**
- 세 Figma variant를 node-level diff해 1920 위치/치수, 3-column hierarchy, header, tabs, combination slots (64×64 ingredient / 76×76 result where variant says so), recipe rows (60px), 5×5 storage bag (68px cells, empty placeholders), detail item image/stats, footer button, weight/capacity labels를 각각 mapping한다.
- 화면 composition은 Figma sibling panel 구조로 전환한다. 왼쪽은 crafting/cooking recipe list + combination flow, 중앙은 selected recipe/result detail, 우측은 material bag/storage. Current 760×620 CookingWindow single mixed panel은 target이 아니다.
- CookingWindow의 현재 `RecipeCatalog`/3-slot match와 cook result contract를 유지한다. Figma row selection → detail/required material/autofill 동작을 일치시킨다. 우측 가방이 player inventory source인지 territory pantry source인지 하나로 결정하고, withdraw-to-inventory/consume-in-place, full bag, cooking selection race를 명시한다. UI가 warehouse를 직접 보이게 한다면 `WarehouseSystem` API와 transaction rollback을 확장하되, UI에서 item consume을 임의 구현하지 않는다.
- `CookingBenchUTK` legacy `CookingDatabase`와 `CookingWindowUTK` RecipeCatalog path, `CraftBenchBaseUTK` and `CraftingWindowUTK` and Alchemy counterparts are separate systems. Calls must route to the intended canonical Figma composition, no duplicate open windows. Forge/cooking/alchemy recipes and probability/success/failure material semantics remain from respective systems.
- If Figma detail panel displays real cooking effects, map fields from recipe/dish data; do not invent effect/stats from mock Figma strings.

**통과 기준:** each variant canvas test; three panels presence/bounds test; 25-slot fixed grid test; recipe selection/detail sync; material withdrawal and cook transaction; no duplicate legacy and UTK cooking UI; category and failure path tests; screenshots of all three canvases.

### Phase 5 — 상태·장비·병사관리 (83:4, 84:4, 69:4)

**파일군:** `StatusWindowUTK.cs`, `EquipmentWindowUTK.cs`, `SoldierManagementUTK.cs`, `GuardInfoUTK.cs`, `GuardSquadHotbarUTK.cs`, composition tests.

**작업:**
- Figma status-only vs status+equipment variants를 비교해 shared status subpanel과 equipment panel 관계를 정한다. Existing status tree currently contains multiple zones; independent equipment window is also available. Duplicate slots/labels/close behavior를 제거한다.
- Figma 5×2 equipment cell order, 3-panel soldier management layout, list filters, detail stats, deployment/role grid, card counts/empty state를 node bounds에 맞춘다.
- `EquipmentManager`, `WeaponEquipManager`, `GuardManager`, `GuardTaskSystem` public API를 preserve하고 status vs equipment ownership single source를 유지한다.
- SoldierManagement has historical runtime entry uncertainty; ensure authoritative open route only after current caller/scene bootstrap inventory.

**통과 기준:** equipment slot enumeration/map tests, unequip/equip roundtrip, soldier list/detail/task assignment tests, state data refresh, three-panel bounds test, 1920 screenshots.

### Phase 6 — 퀘스트·대화·상호작용 panels (93:230, 81/82/93:185, 163:23/187:14)

**파일군:** `QuestWindowUTK.cs`, `QuestJournalUTK.cs`, `InteractionPanelUTK.cs`, `SoldierInteractUTK.cs`, `NPCDialoguePanelUTK.cs`, `NPCDialogueUTK.cs`, `UTKWireUp.cs`, `ContextCommandRouter`/`SoldierInteractBridge` only where routing gaps found.

**작업:**
- Quest three panels: list, center detail, right rewards at Figma absolute bounds. Current 1920-host coordinates must be checked for exact viewport clipping; implement the actual nodes rather than assume old 1440×900*1.333. Card selected/progress/empty filters and objective/reward rows are aligned to current node tree.
- Interaction 5 variants plus compact fishing/sleep/lordroom/lockpick variants; implement panel size/2×2 or single-row action layout, target types and owner-specific action mappings.
- NPC dialogue variants (no choices vs 3 choices) remain distinct. The compact conversation route and larger quest dialogue route must have explicit caller semantics; avoid leaving an alternate “implemented but never called” window.
- Reuse existing QuestManager/QuestChainManager/NPC data/bridge semantics. Any unimplemented Figma button must be visibly disabled or wired; logging a pretend success is not accepted.

**통과 기준:** quest select→detail→reward/accept/complete state tests; each interaction TargetKind dispatch test (one click = one action); dialogue version/choice callback tests; Unity root/ESC/close lifecycle; screenshot by variant.

### Phase 7 — HUD, world map, result, fishing, potion, world overlays (23:6, 95:167, 72 variants, 160:4, 173:4, 101:5, 156:18, 172:2)

**파일군:** `HUDUTK.cs`, `HotbarUIUTK.cs`, `WorldMapWindowUTK.cs`, `HarvestResultUTK.cs`, `FishingUTK.cs`, `GasSprayUTK.cs` plus active Systems bridges and corresponding overlay owners.

**작업:**
- Full canvas vs overlay distinction: HUD and map root canvas use screen coordinates; monster hp/selection rings are world-space anchored references and cannot be resized as normal window panels.
- HUD safe-zone, gauges, 8/8 hotbar slots and tab hint are located against 1920 canvas. Keep hotbar key/PlayerPrefs mapping and health/stamina data owners.
- World map Figma subnodes coordinate against world-extent data, not viewport-stretched terrain texture; retain territory source, normalization, map click actions, zoom/pan.
- Harvest result variants reconcile `72:*` with real trigger systems. Test Keep/Discard by actual inventory effects; if Figma's “discard” is a deletion action, implement through source owner with rollback/confirmation as required.
- Fishing: existing two-bar design is an earlier reference, but latest 160:4 child panel bound and inner `160:11` nodes must be remeasured before asserting prior size; keep System progress/tension cadence, Space/Q event mapping and result bridge.
- Potion-status `173:4` must be disambiguated from gas sprayer vs quick use. Integrate the existing Figma item count/slot/weight/controls into the correct owner; preserve `PotionUseSystem` successful-only consume and `GasSprayerController` loaded dose contract separately.
- World overlays: verify Figma node corresponds to actual nameplate vs MonsterInfo window; retain pooling, distance culling, input-ignore, rendering and world-space lifetime.

**통과 기준:** overlay position/size at 1920/other resolutions, hotbar binding persistence, HUD gauge correctness, map marker hit tests, result acquire/discard state tests, fishing state/Cancel/success tests, potion/gas inventory accounting, world-overlay visibility.

### Phase 8 — 나머지 UI 및 legacy 중복/배선 정리

**범위:** Figma-supported root frames not covered above, smaller supporting widgets, 93 UTK file inventory, legacy 84 UI files.

**작업:**
- P0 matrix에서 남은 화면을 처리한다. Window classes without backing frame are not automatically deleted or rethemed into fictional Figma designs; classify as supporting/custom UI and retain current owner semantics unless they conflict with a Figma-backed path.
- Track visual dupes between IMGUI/legacy and UTK. For each gameplay entry, verify exactly one intended implementation is active in MainScene and relevant test/interior scenes. Remove/suppress legacy display only when confirmed replacement has active route and regression tests.
- Verify every close button, ESC behavior, modal/overlay input gate, pointer picking, keyboard ownership, drag-drop source/target registration, scene bootstrap, open/close group behavior.

**통과 기준:** no dead Figma window, no duplicate same-role window, no Figma button that only logs when it represents active interaction, no broken scene input route. List remaining custom-only UIs explicitly.

### Phase 9 — 통합 QA / 1920×1080 시각 승인 / 마감 기록

- Unity fresh batch compile, parse errors/warnings; compile must be current-source confirmed via newly generated assembly/log, not old `compile.log`.
- EditMode tests for Figma frames/slots/layout and behavior; results XML counts and per-test failures. Keep unrelated existing baselines separate.
- Play through each screen in actual MainScene + relevant dedicated scene; compare screenshots against the exact Figma frame render at 1920×1080. Visually verify panel position/size, window count, all subwindows, slot counts/rows/columns/empty cells, selected states, text/overflow, pointer hit targets and buttons.
- Test 16:9 alternate screen, DPI/panel scale, min supported dimensions, keyboard/mouse interaction, ESC/close/hide/reopen, scene change, saved state reload.
- After each approved cluster: update `QAPROGRESS.md`, ROADMAP phase checkbox only when all its acceptance criteria are actually complete, keep user’s unrelated dirty changes intact, and use file-specific git staging only after reviewing per-file mixed hunks. Do not claim full Figma parity until every in-scope frame passes Play visual comparison.

## 순서와 게이트

`P0 baseline → P1 coordinate/composition helper → P2 inventory/detail/storage → P3 loot/shop → P4 crafting/cooking/alchemy → P5 status/equipment/soldiers → P6 quest/interaction/dialogue → P7 HUD/map/minigame/overlays → P8 remaining Figma/custom/legacy audit → P9 full QA + Play captures`.

Dependency-driven order: P1 before any screen; P2 before composition/panel reuse; P4 depends on panel and inventory/storage interactions; P5 uses inventory/equipment mapping; P6 uses shared window/event/lifecycle patterns; P7 has separate world/screen coordinates; P8 resolves duplication after each Figma path is active. Each phase gets its own TDD/edit batch and visual gate. If code file is 800+ lines or crosses multiple live routes, split phase further; no broad rewrite of all 93 Toolkit files in one pass.

## 주요 위험 / 승인 전 질문 없이 고정하지 않을 항목

- **Figma layout root ownership:** Figma has panels as siblings under full-canvas frames; existing project convention sometimes keeps sibling top-level draggable windows. Plan assumes one canvas composition controller with independently built child panels and per-panel controls, while preserving each existing `Open` caller through a thin compatibility bridge. This should be prototyped on one low-risk frame before broad rollout.
- **Viewport overflow:** Existing 1440 design scaled 1.333 led to overflow risks; latest Figma has 1920 canvases, so all old scale multiplication and position math must be audited. 1920 can still intentionally clip if an individual Figma node extends beyond the canvas; use exact current bounds rather than assume.
- **Semantic divergence:** Some current Figma frames use placeholder ingredients/effects/labels. Only map to real game data if the domain source contains it; otherwise display the correct current data gracefully rather than copying fictional Figma content.
- **Storage semantics:** Figma “StoragePanel” may represent player materials, territory pantry, crafting station storage or a filtered transfer view depending on the canvas. P0 must resolve source and consume/deposit direction per frame before linking buttons.
- **Figma outside frames:** 38 root frames include multiple variations and compact overlays; count implementation work by unique UI archetype + state variant, not number of frames. Keep an exact coverage matrix to prevent silently omitting variants.
- **Desktop editor state:** User’s worktree has many unrelated source and asset changes including `GameSetup.cs`, interior and combat/gas changes. Do not checkout/reset or bundle those files into UI patches. Editor can hold the project; use batch compile only per current Unity environment/lock checks.

## 미포함 / 제한

- This artifact is an implementation plan only. No source, scenes, assets, QAPROGRESS, or ROADMAP files were changed in this planning pass.
- The refreshed Figma node tree is directly confirmed for canvas frame bounds and its variants. Before implementation, keep the downloaded current node JSON and per-frame render with the plan’s baseline so later design edits are detectable.
- Full “all UI windows” is interpreted as all Figma-backed frame variants plus active gameplay UI required by those frames. Figma-free menus/dialogs/custom systems are inventoried and retained, not forcibly redesigned absent a Figma frame.
