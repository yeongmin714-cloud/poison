# [115차] 계획 — A. 플레이어 영지 세금 수금(권장세율·호감도·주민이탈 이벤트) / B. 메인씬 월드맵 자가부트

> 상태: **계획 수립 — 구현 전 사용자 승인 대기.** 구현·QA는 승인 후 서브에이전트 분리 위임(docs/AGENT_DELEGATION_POLICY.md).
> 조사 근거: 코드 직접 실측(라인 동봉). 미실측 항목은 명시.

---

## 0. 조사 요약 (실측 근거)

### 기존 체계 — 세금
- `AITerritoryEconomySystem.cs`(269행) — **AI(LordOwned) 전용** 세수/유지비. 헤더 계약 L8~9: "플레이어 소유 영지와 플레이어 급료 시스템은 수정하지 않는다" → **플레이어 세금은 신규 시스템으로 분리해야 함(기존 파일 로직 확장 금지).**
  - 주기: `TAX_INTERVAL_DAYS=7`, `TAX_ANCHOR_DAY=1`(L16~17). 경계일 판정 `IsTaxBoundaryDay`(L58), 멱등 `_lastTaxWeek`(L20).
  - 주간 세액: `GetWeeklyTax(difficulty, taxPercent)` = 일세수(difficulty: Ring1 10/Ring2 20/Ring3 30/Ring4 40/Empire 50 — L83~93) × 7 × taxPercent/100 half-up(L65~69).
  - 경제 계수: `VillageEconomySystem.GetWeeklyCollectedTax`(L49~50) = 주간세액 × `WeeklyTaxFactor`(0.5+0.5×economy, VillageEconomyMath L60~63).
  - 적립처: `state.territoryGold`(금고 상한 cap L52).
  - 호출부: `GuardSalaryManager.cs:64`(TimeManager.OnDayStart 구독 체인).
- 마을↔영지 매핑: `VillagePlacementSystem` — villageIndex 0..19 ↔ 영지 인덱스 1..20 **1:1**(국가당 마을 20), `ComputeVillage` L111~. 마을 총원 근사 8/4(VillageEconomySystem L23~25).
- 프로필 세율: `TerritoryDifficultyProfileSystem.Resolve` → `profile.TaxPercent`(AI 세율 기본 — 권장세율의 기준으로 재사용).

### 기존 체계 — 주민 이탈 (재사용 선례)
- `VillagerDepartureSystem.cs`(109행): 일일 이탈 롤 — `RestaurantSystem.cs:681`(RestaurantDailyTicker.OnDayStart)에서 `RunDailyDeparture(day)` 호출. `ProcessVillageDeparture` L55~84:
  - **게이트: `snapshot.restaurantBuilt` 미건설 마을은 이탈 대상 아님**(L58~60) — 세금 이탈을 위해 게이트 확장 필요.
  - 롤: `VillageDrugMath.DepartureChance(addiction, lordViolent, personalityIdx)` = 중독/100×0.30 + 폭력화 0.20 + 성격 가중(Cruel +0.10/Wise −0.10, L156~162), 상한 클램프 0.90(L52).
  - 이탈 처리: `VillageDrugState.RecordDeparture`(누적) + `VillageNpcSpawner.RemoveVillageNPCs(nation, villageIndex, 1)`(실제 NPC 제거) + 로그.
  - 결정론: `DeriveVillageSeed(daySeed, nation, index)` 시드.
- `VillageDrugState.cs`(500행): 마을 단위 상태 저장소 — `addiction`/`departedVillagers`/`restaurantBuilt`(L29~34), 스냅샷 `GetSnapshot`, 세이브 DTO `VillageDrugSaveEntry`(D5, 구세이브 누락 안전 패턴 확립), SaveManager 수집/분배 훅 존재.
- 이벤트 표시 수단: `UTKToastService`(토스트, 92차), `DynamicEventUTK.ShowEvent(WorldEventManager.ActiveEvent)`(월드 이벤트 팝업 — MainScene에 WorldEventManager 존재). `WorldEventManager.cs` MainScene 배치 실측.

### 기존 체계 — 영지 점령
- 플레이어 점령 경로: `AssassinationCutscene.cs:252` — `db.SetOwnership(territoryId, PlayerOwned)` + Phase D 몰수(TerritoryLootSystem). 드라큘라 특수: `DraculaTerritoryController.cs:95`. **AIWarSystem은 PlayerOwned를 방어자만 허용(공격자 불가) — 플레이어의 신규 점령 경로는 이 2건 + 이후 확장.**
- `TerritoryState`(Core/Data/TerritoryData.cs L148~): `loyaltyToPlayer`(0~100, L153 — F5 영주 충성도, **세말 호감도와 의미 충돌 → 재사용 금지, 별도 필드 신설**), `territoryGold`.
- 세이브: SaveData v5. `TerritorySaveData`에 필드 추가(구세이브 누락→기본값 폴백 패턴 확립 — lordSpared/tributeWeeksPaid 선례).

### B. 월드맵 — 메인씬
- `WorldMapWindowUTK.cs`(885행, 114차 완성): `Ensure()` L53~62 — `UIToolkitBootstrap.UIRoot`에 부착 + `StartLoops()`(M키 자체 폴링) + `SuppressLegacyHotkey()`. **단, Ensure 가드가 `if (_instance != null) return;`(L55) → 씬 재로드 시 UIRoot 재생성되면 인스턴스는 존재하나 고아(부착끊김)·루프 정지 — 메인씬(씬 전환 존재)에서는 수리 필요(HUDUTK Ensure는 `parent != null` 가드 — L30 선례).**
- `UIToolkitBootstrap.cs`: static UIRoot + `Ensure()` 멱등. `HUDUTK.Ensure`는 `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`(HUDUTK.cs L38~42)로 **모든 씬 자가부트** — 메인씬 포함.
- **MainScene.unity에는 CoreSystemsBootstrap/HUDUTK/EscMenuUTK/KeyBindings 컴포넌트 0건(GUID grep 실측) — UTK는 HUDUTK RuntimeInitialize 자가부트로만 존재.**
- M키 충돌 감사: `KeyCode.M` 소비처 = `KeyBindings.cs:25`(`_mapKey`, 레거시 지도 — TestUISetup/TestAllInOneSetup 테스트씬에서만 MapWindow 생성, 메인씬 미생성). OptionsUTK/SettingsMenuUTK는 "월드맵 M" 라벨 표시만. TutorialActionDetector는 튜토리얼 감지 전용(114차 판정 동일). **메인씬 M키 충돌 0.**

---

## A. 플레이어 영지 세금 — 설계

### A-1. 데이터 (Core)

**① 신규 `Core/Utils/VillageTaxMath.cs` (순수 수학 단일소스 — VillageEconomyMath 선례)**
- `RecommendedTaxPercent(int profileTaxPercent, int favor)` — 권장 세율:
  - 기본 = profileTaxPercent(프로필 세율 그대로).
  - favor < 30 → `ceil(profileTaxPercent × 0.5)` (폭정 억제 권고), favor ≥ 60 → 프로필 그대로, 30~59 → 선형 보간.
- `FavorDelta(int actualTaxPercent, int recommendedTaxPercent)` — 주간 호감도 변화:
  - 실제 > 권장: `−ceil((실제−권장) × 0.2)` (초과 5%당 −1, 한주 캡 −20).
  - 실제 < 권장: `+1` (회복). 실제 == 권장: `0`.
- `FavorDepartureBonus(int favor)` — 이탈 확률 가산: favor < 30 → `(30 − favor) / 30 × 0.15` (favor 0 → +0.15), 그 외 0.
- `CollectAmount(int pending, int playerGold)` 미사용 — 수금은 pending 전액.
- 기본 수치는 **[승인 대기 — 조정 가능]**.

**② `TerritoryState` 필드 추가 (TerritoryData.cs, L148~ 구간)** — 플레이어 세금 2종:
- `_playerTaxRatePercent` (int, −1 = 미설정 → 프로필 세율 폴백. 세터 0..100 클램프, F5 loyaltyToPlayer 세터 선례).
- `_pendingTaxGold` (int, 미수금 누적).
- 접근자 + `[113차] lastPreferredFeedDay` 세터/센티널 패턴 동일.

**③ `TerritorySaveData` 필드 추가 (SaveData.cs, lordSpared 블록 뒤)** — `playerTaxRatePercent`(int, −1 기본) + `pendingTaxGold`(int, 0 기본). Collect/Apply 동행(SaveManager L127/L205 훅 — 구세이브 누락 안전, 113차 선례).

**④ `VillageDrugState` 호감도 추가:**
- `VillageDrugSnapshot` += `taxFavor`(int, 기본 50).
- 런타임 필드 `taxFavor` + `ApplyTaxFavor(nation, villageIndex, delta)` (0..100 클램프 — 마을 단위, 1:1 매핑이므로 영지 단위와 동치).
- `VillageDrugSaveEntry` += `taxFavor`(int, −1 기본 → 로드 시 50 폴백). Export/Import 동행.

### A-2. 시스템 (Systems)

**⑤ 신규 `Systems/PlayerTerritoryTaxSystem.cs` (static — AITerritoryEconomySystem 병렬, 헤더 계약 존중):**
- `ApplyDailyTax(int day)` — AI 경계일 규약 동일(TAX_INTERVAL_DAYS/ANCHOR 재사용, 자체 멱등 `_lastTaxWeek`):
  - PlayerOwned 영지만: 주간 세액 = `AITerritoryEconomySystem.GetWeeklyTax(difficulty, playerTaxRate)` × `WeeklyTaxFactor(마을 경제)` → `state._pendingTaxGold += 세액`.
  - 같은 경계일에 `VillageDrugState.ApplyTaxFavor(nation, villageIndex, VillageTaxMath.FavorDelta(실제, 권장))`.
- `Collect(TerritoryId)` — pending 전액 → `PlayerInventory` 골드 지급(PlayerInventory 지급 API — **미실측, 구현 시 ShopWindow/RestaurantSystem 판매금 지급 선례에서 단일 소스 확인**) → pending 0 → 토스트 "세금 N G 수금".
- `SetTaxRate(TerritoryId, percent)` — UI 입력 경로(클램프+저장).
- 훅: `GuardSalaryManager.cs:64`의 AI `ApplyDailyTax(day)` 호출 옆에 1행 추가(try 블록 동일 규약).
- 테스트씬: `TestTerritoryCombatSetup.cs:333` `RunDailyDeparture` 옆에 동일 훅(113차 TerritoryLordDoorSystem 선례).

**⑥ `VillagerDepartureSystem.ProcessVillageDeparture` 확장 (L55~84, 최소 변경):**
- 게이트: `restaurantBuilt` **또는** `snapshot.taxFavor < 30` → 이탈 대상(세금 폭정 마을도 이탈 가능). 기존 음식점 경로 무손상.
- 롤: 기존 3인자 `DepartureChance` **계약 보존** + `+ FavorDepartureBonus(favor)` 가산(오버로드 신설 — 3인자 시그니처 유지, 기존 테스트 보호).
- 이탈 발생 시: 기존 처리(RecordDeparture+RemoveVillageNPCs) + **토스트 1건**(`🚪 {마을명} 주민 1명 이탈 — 호감도 {favor}`).
- **호감도 임계 경고**: `taxFavor`가 처음 30 미만으로 진입한 주에 1회성 토스트(`⚠ {마을명} 주민 불만 폭발 — 세금을 낮추세요`) — 1회성은 VillageDrugState 플래그로 멱등.

**⑦ 신규 `Systems/VillageTaxInteractable.cs` — 마을 중심 E키 상호작용:**
- VillageBuilder가 마을 루트에 부착(부모 오브젝트에 AddComponent — scene 편집 불필요, FacilityPropBuilder 선례). 보유 영지(ownership==PlayerOwned) 마을 중심 반경 ~6m 진입 시 `EInteractionPromptBridge` "E: 세금 관리" → `TaxWindowUTK.Open(nation, villageIndex)` (OpenRequestedUTK 정적 이벤트 — RestaurantPropInteractable 선례).
- ⚠ E키 충돌 감사: 마을 중심 반경 6m와 음식점 배치 스팟(마을 가장자리)/문/기타 상호작용 겹침 — 프롬프트 동시 다발 시 최근접 우선(EInteractionPromptBridge 기존 동작) 실측 필요 → Play 게이트에 포함.

### A-3. UI — 신규 `UI/Toolkit/TaxWindowUTK.cs`

- 자가부트 패턴(NPCDialoguePanelUTK/RestaurantWindowUTK 선례 — Ensure→UIRoot 부착, ApplyCanvasFit, 창박스=figma×k·글래스0.85 계약, GitHub-dark 팔레트).
- 1개 마을 컨텍스트(근접한 마을): 헤더("💰 세금 관리 — {마을명}"), 권장 세율 배지("권장 {N}%"), 현재 세율 조정(−/＋ 5% 스텝, 0..100), 호감도 게이지(UTKGaugeStripArc — **벡터메시 금지 계약**, 84차), 미수금("미수금 N G"), [수금] 버튼(pending>0만 활성), 안내문("세율이 권장보다 높으면 주민 호감도가 매주 하락합니다. 호감도가 30 미만이면 주민이 마을을 떠납니다."), Esc=닫기. Poll 1000ms.
- **Figma**: 신규 프레임 필요 여부 — Figma가 단일소스(사용자 계약)이므로, Figma 프레임 먼저 제작하거나 기존 RestaurantWindowUTK 레이아웃을 유사 구조로 준용. **[사용자 결정 대기 — Figma 신규 프레임 제작 vs 기존 레이아웃 준용]**

### A-4. 세이브/로드

- TerritorySaveData/VillageDrugSaveEntry 필드 추가(위 ②④) — SaveData v5 유지(구세이브 additive, 폴백: taxRate −1→프로필, favor −1→50, pending 0).
- ExportSaveEntries/ImportSaveEntries + SaveManager 수집/분배 훅 갱신. 라운드트립 테스트 동반.

### A-5. TDD (신규 테스트 — EditMode)

| 테스트 파일 | 건수 | 커버 |
|---|---|---|
| VillageTaxMathTests (신규) | ~12 | 권장세율 3구간/FavorDelta 초과·미달·일치/이탈보너스/클램프 |
| PlayerTaxSystemTests (신규) | ~8 | 경계일 멱등/PlayerOwned만 적립/이탈 마을 factor 반영/Collect 전액·pending 0/구세이브 폴백/미설정 세율 폴백 |
| VillageTaxFavorTests (신규) | ~6 | ApplyTaxFavor 클램프/임계 경고 1회성/snapshot taxFavor/DTO 라운드트립 |
| VillagerDepartureSystemTests (갱신) | +4 | 게이트 확장(restaurantBuilt 없어도 favor<30 이탈)/favor 보너스 가산/기존 3인자 계약 무손상/이탈 토스트 |
| 기존 회귀 | 0 | VillageDrugMath 3인자·VillageEconomyMath·AITerritoryEconomySystem 계약 무손상 |

- 위임 후 QA: 컴파일 error CS 0 + EditMode 전수(baseline 11건 1:1 + 신규 전부 GREEN).

---

## B. 메인씬 월드맵 자가부트

### B-1. `WorldMapWindowUTK` 씬전환 수리 (핵심 — 고아 인스턴스)
- `Ensure()` 가드: `if (_instance != null && _instance.parent != null) return;` — parent 유실(씬 재로드로 UIRoot 재생성) 시:
  - `UIToolkitBootstrap.UIRoot`가 있으면 재부착 + `StartLoops()` 재기동(M키 폴링 스케줄러는 패널 소속 — detach로 정지하므로).
  - UIRoot null이면 `UIToolkitBootstrap.Ensure()` 먼저 호출(자가부트 완결 — 현재는 root null이면 부착 스킵만 하고 폴링 기동 L59가 돌지만 UIRoot 무부착 상태 → 메인씬 첫 Ensure에서 지도가 안 보이는 함정).
- 계약 유지: 마커 좌표 산식/WORLD_SPAN/GitHubDark 팔레트/SuppressLegacyHotkey 무수정.

### B-2. 자가부트 진입점
- `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] private static void Bootstrap() => Ensure();` — HUDUTK.Ensure(L38) 선례. 모든 씬(MainScene 포함)에서 UIRoot+지도 준비, 테스트씬 114차 배선(SetupUITestArena InvokeUtkStatic)과 멱등 공존.
- M키 폴링: WorldMapWindowUTK 자체 폴링 그대로 — 메인씬에서 M키 소비처 0건(실측) → 충돌 없음. ESC/재입력 닫기 기존 계약.
- 레거시 MapWindow는 테스트씬(TestUISetup/TestAllInOneSetup) 전용 — 메인씬 미생성 → SuppressLegacyHotkey 무해.

### B-3. TDD
- `WorldMapSceneWiringTests` 확장 +4: ①RuntimeInitializeOnLoadMethod(AfterSceneLoad) 소스 어서션 ②Ensure 고아 재부착+StartLoops 재기동 계약 ③UIRoot 부재 시 UIToolkitBootstrap.Ensure 우호출 계약 ④KeyCode.M 메인씬 소비처 0건(전수 grep 가드 — 레거시 TestUISetup만 허용).

---

## 실행 분할 (위임 계획 — 사전소화 지시문, 승인 후)

| Phase | 범위 | 위임 |
|---|---|---|
| P1 | VillageTaxMath(신규) + TerritoryState/SaveData/VillageDrugState 필드·DTO + Math/Save 테스트 | 서브 1 (Core+Tests) |
| P2 | PlayerTerritoryTaxSystem(신규) + VillagerDepartureSystem 확장 + 훅 배선(GuardSalaryManager/TestTerritoryCombatSetup) + 시스템/이탈 테스트 | 서브 2 (Systems) |
| P3 | TaxWindowUTK(신규) + VillageTaxInteractable(신규) + UI 테스트 | 서브 3 (UI) |
| P4 | WorldMapWindowUTK 수리+자가부트 + WorldMapSceneWiringTests 확장 | 서브 4 (UI — P3와 파일 분리) |
| QA | 컴파일(batchmode, 부모) + EditMode 전수 + baseline 판정 | 별도 QA 서브 |

- 각 서브 지시문에 위 라인번호·시그니처·선례(113차 DTO/폴백, 79차 D5, 88차 UI) 동봉. 컴파일은 부모(batchmode)만 수행.
- 순서: P1→(P2‖P4)→P3→QA. P3는 Figma 결정에 의존(기본값: 기존 레이아웃 준용으로 진행 가능).

## 리스크 / 충돌 감사
1. **M키**: 소비처 전수 실측 = 레거시 TestUISetup만 → 메인씬 충돌 0. TaxWindow는 E키 근접(핫키 없음) → 충돌 없음.
2. **`AITerritoryEconomySystem` 헤더 계약**(L8~9) — 신규 시스템 분리로 준수.
3. **DepartureChance 3인자 계약** — 오버로드 유지(기존 테스트 보호).
4. **WorldMap 고아 인스턴스** — 메인씬 테스트 시 지도 미표시 함정(Ensure 가드 L55) → B-1 수리 필수.
5. **E키 근접 충돌**(마을 중심 vs 음식점 스팟/문) — Play 게이트에서 실측.
6. **테스트씬 동기화** — Test_10/13/14/15/17 공유 셋업: 세금 UI/이탈 로그는 셋업 자가부트 경유 자동 커버; Play 확인은 Test_15(마을 존재)+메인씬.
7. **baseline 11건** — Aero/CraftBench/Instrument×3/InvadedLord×2/MeleeClick×2/RecipeCatalog/ThemeUss 1:1 유지.

## 미실측 (구현 시 확인)
- PlayerInventory 골드 지급 API 명칭(판매금 선례에서 단일소스 확인).
- `TerritoryDifficultyProfile.TaxPercent` 값 범위(0..100 가정).
- `WorldEventManager.ActiveEvent` 생성 비용 — DynamicEventUTK 통합은 MVP 토스트 후 여유 시.
- VillageBuilder 마을 루트 오브젝트 구조(Interactable 부착 지점).

## Play 게이트 (승인 후)
A: ①Test_15에서 보유 영지 마을 근접 → "E: 세금 관리" ②세율 조정 → 권장/현재 표시 ③수금 → 골드+pending 0 ④세율 초과 주간 경과 → 호감도 하락 표시 ⑤호감도 <30 → 경고 토스트 1회 ⑥이탈 발생 → NPC 실제 제거+토스트 ⑦세이브/로드 복원 ⑧AI 영지 세수(기존) 무변경.
B: ①메인씬 진입 → M키 → 월드맵 표시(영지/플레이어 마커+범례) ②씬 전환(Main↔Test_15) 후 M키 재작동 ③Test_10/13/14/15/17 M키 회귀 0 ④지도 개방 중 기타 UI 간섭 없음.

## 커밋
- 105/113/114차 미커밋 누적에 본 라운드 추가 — 커밋+push는 별도 승인 게이트.
