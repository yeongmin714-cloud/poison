# 88차 스펙 — 음식점 배치(Figma 237:792 정합) + 마을 배치 지점 VFX/E키

> 발행: 감독자 (2026-10-16). 위임 근거: docs/AGENT_DELEGATION_POLICY.md.
> Figma 진실치는 2026-10-16T01:01:11Z 개정본 노드 API 실측(파일키 02vPXOGzFJUUYTaEPSVz3R, 프레임 237:792 "음식점 판매 관리" 1600×1040).
> 좌 "음식 인벤토리" 480×882 @(64,78) / 우 "음식점 배치" 968×882 @(568,78) / 헤더 1472×18 @(64,36) / 도움말 1472×14 @(64,984).

## Part A — Systems: 마을 음식점 배치 지점 + VFX + E키 (구현 위임 1차)

### A1. 배치 지점 산출 — `Systems/RestaurantPlacementSpotSystem.cs` (신규, static)
- `ComputeCandidateSpots(VillagePlacementSystem.VillageInfo village, int count = 3)` — 결정론:
  - 각도: `SeedBase(472) + GetHash($"{nation}_{index}_restspot_{i}")` System.Random → 0~360° (VillageBuilder.GetHash 동일 알고리즘 재사용/복제).
  - 반경: 일반 마을 `village.radius - 7`(=33m), 촌락 `village.radius - 5`(=17m) — 펜스(radius+3)·외곽 링(최대 28m) 회피.
- `TryGetValidSpots(VillageInfo village, List<Vector3> results)` — 런타임 유효 판정:
  1. 마을 루트 `Village_{nation}_{index:D2}` GameObject 하위 렌더러 transforms의 XZ bounds를 기준으로 각 후보를 VillageBuilder.EnforceSpacing 동일 방식(충돌 방향 반대로 밀기)으로 정리.
  2. 축출 조건: 광장 회피(center에서 `PlazaRadius(일반 6/촌락 4)+footprint` 이상), `bp_restaurant.footprintRadius=2.5` + 건물 마진 ≥ 2.0m 간격, `village.radius + ConstructionManager.VillagePlacementMargin(5m)` 이내, 지형 접지(y=GetHeightAt(x,z,Plains,42)+1).
  3. 점유 판정: `VillageDrugState.GetSnapshot(nation, index).restaurantBuilt == true` 또는 ConstructionManager.Structures에 bp_restaurant(완료/진행 무관)가 해당 마을에 매칭되면 스팟 없음(마커 표시 안 함).
- EditMode에서 씬 질의 불가 → 후보 산출(순수)은 테스트 가능하게 분리, 씬 의존부는 런타임 전용 경로로.

### A2. 마커/VFX + E키 — `Systems/RestaurantPlacementMarker.cs` (신규, MonoBehaviour)
- Update 폴링(플레이어 tag "Player" 캐싱 — ShopPlaceholder 47~52행 선례):
  - 플레이어에서 최근접 마을(반경+5m 내, RestaurantSystem 스킵 아님) → `TryGetValidSpots` 유효 지점들에 링 VFX 생성.
  - **표시 범위**: 스팟에서 25m 이내 접근 시 해당 마을의 유효 스팟 링 표시, 이탈 시 숨김(파괴 아님 — 재사용).
  - **VFX**: `SelectionRingController`(기존 84/85차 수제 링) + `CreateRingMaterial` — 색은 골드 앰버 `#E3B341`(RTS 파랑 선택링과 구분, "건설 가능" 관례). 반경 ~2.5m(footprint 일치). collider 없음.
  - **E 프롬프트**: `EInteractionPromptBridge.RaiseRegister(markerAnchor, "E: 음식점 배치", 3f)` — 최근접 유효 스팟 앵커에만. 이탈/숨김 시 RaiseUnregister.
- **E키 → 배치**: 최근접 유효 스팟 3m 이내에서 `Keyboard.current.eKey.wasPressedThisFrame` →
  - `territoryId = new TerritoryId(nation, villageIndex + 1).ToString()` (TestTerritoryCombatSetup 실측 조립과 동일)
  - `ConstructionManager.Instance.TryPlace(territoryId, "bp_restaurant", spotPos)` → 반환 메시지로 `PlacementAttempted(bool success, string message)` 정적 이벤트 발화(성공="건설 시작! ...", 실패=사유 — 골드/재료/허가세/겹침).
  - TryPlace 재호출 전 유효성 재확인(스팟 점유 변화 대응).
- 이벤트 구독은 UI(Phase B)가 담당(Systems→UI 순환참조 회피 정적 이벤트 규약). 무구독이면 로그만.
- 런타임 AddComponent 전용 — 프리팹/씬 직렬화 대상 아님(RestaurantDailyTicker 규약 준용). 부트는 `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` 구독 전 해제 패턴.

### A3. 판매금 적립 로직 전환 (Figma "판매금 받기" 계약)
- `VillageDrugEntry` 신규 필드: `pendingDrugGold`, `pendingNormalGold`(int).
- `RestaurantSystem.RunDailySale`: 기존 `stats.AddGold(...)` 즉시 지급 → `VillageDrugState.AddPendingRevenue(nation, villageIndex, drugGold, normalGold)` 적립으로 변경 (원장 태그 GoldSourceDrug/GoldSourceFood는 수령 시 유지).
- `VillageDrugState` 신규 API: `AddPendingRevenue(nation, idx, drugGold, normalGold)`, `GetPendingRevenue(nation, idx, out drugGold, out normalGold)`, `TakePendingRevenue(nation, idx)`(합산 반환 후 0 리셋 — 결정론).
- `RestaurantSystem.CollectRevenue(NationType, int villageIndex)` (신규): TakePendingRevenue → PlayerStats.AddGold(drugGold, GoldSourceDrug) + AddGold(normalGold, GoldSourceFood) → 총 수령액 반환(음수/0 안전). PlayerStats null 시 소비 없이 false.

### A4. 재고 누적 + 슬롯 상한 6 (Figma "4 / 6", "18 / 30" 계약)
- `RestaurantStockItem` 필드 추가: `placedTotal`, `soldTotal` (int) — 누적 집계용.
- `VillageDrugState.AddStock`: 병합/신규 관계없이 `placedTotal += item.count`. 동일 키 병합 시 기존 누적 보존.
- `VillageDrugState.ConsumeStock`: 실소모 수만큼 해당 슬롯 `soldTotal += consumed`.
- `RestaurantSystem.RestockFromPlayer`: **슬롯 상한 6 검사 신설** — 병합 키 신규 생성이 필요한데 슬롯 수가 이미 6이면 거부(false). 병합(기존 키)은 항상 허용. 상수 `RestaurantSystem.MaxStockSlots = 6`.
  - Test_15 시딩은 `AddStock` 직접 호출이라 상한 영향 없음(기존 검증 유지).
- **판매 담당 병사 자동 배치**: RestockFromPlayer 성공 직후 `snapshot.assignedGuardId` 빈 문자열이면 `AssignGuardFromPlayer` 1회 시도(로그). GuardManager 부재(EditMode)는 안전 무동작 — RunDailySale 스킵 계약 보호.

### A5. 세이브 DTO 확장 (하위호환)
- `VillageDrugSaveEntry` += `pendingDrugGold`, `pendingNormalGold`.
- `VillageDrugStockSaveEntry` += `placedTotal`, `soldTotal`.
- Export/Import 1:1 대응 갱신. 기존 세이브(필드 누락)는 JsonUtility 기본값 0 — 호환 무결.

### A6. 테스트 (Phase A 산출)
- `Tests/EditMode/RestaurantPlacementSpotTests.cs` (신규): ①후보 각도/반경 결정론 재현(같은 시드 → 같은 좌표) ②반경 클램프(radius-7/-5 + margin 이내) ③씬 질의 경로 소스 어서션(마커 프롬프트 문구 "E: 음식점 배치", territoryId 조립 `new TerritoryId(nation, villageIndex + 1)`, TryPlace 호출, PlacementAttempted 발화, SelectionRingController/CreateRingMaterial 사용, 골드 #E3B341 근사) ④점유 마을 스킵 소스 어서션(restaurantBuilt/구조물 매칭).
- `Tests/EditMode/RestaurantRevenueTests.cs` (신규): ①RunDailySale → PlayerStats 즉시 지급 코드 부재 + AddPendingRevenue 호출 소스 어서션 ②TakePendingRevenue 전이/리셋(상태 API 직접) ③AddStock→placedTotal 누적, ConsumeStock→soldTotal 누적 ④슬롯 상한 6 — 6개 이기 키 입고 거부/병합 허용 ⑤CollectRevenue 소스 어서션(GoldSourceDrug/Food 태그 유지).
- 기존 테스트 중 "RunDailySale 골드 즉시 지급" 계약(RestaurantSystemTests 등) 있으면 [88차] 갱신.
- 컴파일: `rm -f Temp/UnityLockfile && timeout 550 bash compile_test.sh` — error CS 0.

## Part B — UI: RestaurantWindowUTK 재구현 (구현 위임 2차 — Phase A 완료 후)

### B1. 창 골격
- `RestaurantWindowUTK` 전면 재구성 — 창 박스 = Figma 1600×1040×k(ApplyCanvasFit 84차 계약: `k=min(rootW/1920, rootH/1080)`, 내부 raw 치수 + `transform.scale=k`).
- 2패널: 좌 인벤토리 480×882 @(64,78), 우 판매 관리 968×882 @(568,78), 헤더 @(64,36), 도움말 @(64,984). 패널 bg #13181F r14, stroke #30363D/0.5.
- 기존 섹션(병사/중독 게이지/수요 문구)은 Figma에 없음 → 제거. 시스템 로직(A3/A4 자동병사 포함)은 유지.

### B2. 진실치 (Figma 실측 — [88차] 마커로 테스트 고정)
- 공통: 텍스트 #F0F6FC(본문 700/400), 보조 #8B949E, 액센트 #58A6FF, 액센트 배경 #58A6FF/0.078, 표면 #191F27, 입력/어두운 표면 #0F141B, 슬롯 #20262E, 선택 슬롯 #253346, 보더 #30363D/0.5, radius 14(패널)/10(카드)/6(버튼·박스).
- **우 패널**: 제목행 store 23×23 + '음식점 배치' 22/700 + '음식 판매 및 정산' 11/400 + 닫기 30×30 (창 닫기) | 운영 정보 920×62: utensils 36×36 + `{마을명} · 모닥불 식당` 14/700 + '영업 시간 08:00–22:00 · 배치한 음식이 자동으로 판매됩니다.' 11/400 + 영업 배지 62×23(원 5 + '영업 중' 11/500 — TimeManager.Hour 8~22 실측 판정, 이탈 시 '영업 종료' #8B949E) | '판매할 음식' 16/700 + 'N / 6' 12/400 #58A6FF + '판매 가격은 개당 기준 · 수량 단위: 개' 11/400 | 목록 920×330 r10: 열헤더 920×34 #0F141B — 열 x 38(음식)/418(권장 가격)/524(판매 가격)/634(배치)/704(판매 / 배치)/792(판매금)/912(관리) | 행 920×74(선택 #58A6FF/0.078+stroke 0.25, 일반 #191F27): 미리보기 44×44 #20262E r6 + 이름 14/700 + 설명 10/400('요리 · …') / 권장가 13/400 / 가격박스 98×32 #0F141B r6(값 13/400 #F0F6FC + 'G' 11/400) / 배치수량 13/400 / 진행 76×27('판매 / 배치' 13/400 + 바 66×3 #20262E→채움 #58A6FF r2) / 판매금 14/500 #58A6FF / ellipsis 18×18 | 추가 배치 안내 920×42 #0F141B r6: plus 15×15 + '왼쪽 인벤토리에서 음식을 선택해 추가 배치하세요.  N칸 사용 가능' 12/400 | 정산: '전체 판매 요약' 16/700 + '마지막 수령 이후 · 오늘 08:00부터' 11/400 + 4지표 221×80 gap12(총 배치 개수/총 판매 개수/남은 음식/총 판매금 — 마지막 카드만 액센트 테두리·수치 #58A6FF, 수치 23/500) | 판매금 수령 920×92 #58A6FF/0.078 r10: '받을 수 있는 판매금  N G' 13/400 + '수령 후 소지금  X → Y G  · 수수료 없음' 11/400 + 버튼 250×46 #58A6FF r6 '판매금 받기' 14/700 #0B0E14 | 안내 '판매금 수령 후에도 영업은 계속되며, 남은 음식은 판매대에 유지됩니다.' 11/400.
- **좌 패널**: 제목 backpack + '인벤토리' 22/700 + '보유 음식' 11/400 + 닫기 | 분류 탭 5개 81.6×36 r6 gap6(x 24/111.6/199.2/286.8/374.4): 전체(활성 #58A6FF, 글자 #0B0E14 12/700)/요리/디저트/음료/재료(비활성 #191F27, 글자 #8B949E) | 검색 432×34 #0F141B r6: search 15×15 + '음식 이름 검색' 12/400 #586473 + 정렬 16×16 | '판매 가능한 음식' 12/700 + 'N종 · M개' 11/400 | 슬롯 5열×3행 80×86 r6 gap8(x 24/112/200/288/376, y 214/310/406): 등급바 (x+6, y) 68×2(선택 #58A6FF/일반 #65768A/0.4), 아이콘 32×32, 이름 10/400, 수량 우상단 10/500, 빈슬롯 dashed | '판매대에 배치되지 않은 보유 수량입니다.' 11/400 @(24,502) | 선택 음식 배치 432×213 #58A6FF/0.078 r10: 미리보기 48×48 + 이름 15/700 + '보유 N개 · 요리' 11/400 + 권장가 15/400 #58A6FF | 수량행: '배치 수량' 12/400 + 조절 (rel 158.5) 116×32 #0F141B r6(− 16/400 #8B949E / N 14/400 #F0F6FC / + 16/400 #58A6FF) + '최대 N개' 11/400 #58A6FF | 실행 버튼 400×46 #58A6FF/0.078 r6(arrow-right 18×18 + 'N개 추가 배치' 14/700 #58A6FF) | '배치 후 보유 N개 · 예상 판매금 N G' 11/400 | 요약: '소지금  N G' 12/400 + 'N / 40 슬롯' 12/400 #58A6FF(실측 _maxSlots=40 — Figma 목업치 15는 형태만 채용) + 사용량 바 432×4 #20262E→채움 #58A6FF r2.
- **헤더**: house 18×18 + `{마을명}` 13/400 #8B949E + '/' #586473 + '음식점 관리' 13/500 #F0F6FC | 우측 clock + '{N}일차 · 낮/밤' 12/400 + 'HH:MM' 13/400 (TimeManager 실측).
- **도움말**: '음식을 선택하고 수량을 정해 판매 목록에 배치하세요.' 좌측 | '클릭  음식 선택     ·     Enter  배치 확인     ·     Esc  닫기' 우측 12/400 #8B949E.

### B3. 데이터 매핑 (위조 금지 — 전부 실측 API)
- 좌 슬롯 그리드: `PlayerInventory.GetSlotsByCategory(ItemCategory.Food)` + Drug 카테고리(마약 요리 — `drugged_` 접두 실측 규약) 합산. 탭 필터: 전체=Food+Drug / 요리=Food / 디저트·음료=서브카테고리 부재 → '0종' 표시 / 재료=Meat+Herb(Crop 있으면 포함). 검색=이름 contains.
- 권장 가격: 일반=basePrice 실측, 마약=`VillageDrugMath.RecommendedPrice(basePrice, addiction)`(기존 ResolveDemandBasePrice 재사용 가능).
- 판매 가격: 마약 행 = 마을 registeredPrice(TextField — 변경 시 SetRegisteredPrice, 기존 계약 유지), 일반 행 = basePrice 고정 표시.
- 진행 '판매 / 배치' = soldTotal/placedTotal, 판매금 = soldTotal × 가격, 4지표 = Σ placedTotal / Σ soldTotal / Σ count / GetPendingRevenue 합.
- '판매금 받기' → `RestaurantSystem.CollectRevenue` → 성공 토스트 + 즉시 갱신.
- 'N개 추가 배치' → `RestaurantSystem.RestockFromPlayer` → 성공/실패 토스트(재료 아님 — 인벤 수량 부족/슬롯 상한 사유 표시).
- 갱신 폴링 PollMs 1000ms 유지 + Collect/배치 직후 즉시 RefreshAll.
- **토스트 연계(Phase A 이벤트)**: `RestaurantPlacementMarker.PlacementAttempted` 구독 → `UTKToastService.Show(message)` — Bootstrap 구독 전 해제 패턴.

### B4. 테스트
- `RestaurantWindowFigmaTests.cs` (신규): 위 진실치 [88차] 마커 소스 어서션 — 1600×1040/480×882/968×882/@(64,78)@(568,78)/#13181F/#58A6FF/열 x 38·418·524·634·704·792/버튼 250×46/수량조절 116×32/슬롯 80×86·gap8/탭 81.6×36/'판매금 받기'/'N / 6' 상한 6/ApplyCanvasFit.
- `RestaurantWindowTests.cs` — 기존 계약 대거 갱신(병사/중독 섹션 삭제, 신규 섹션 반영).

## 검증/증거 (두 Phase 공통)
- 컴파일: `rm -f Temp/UnityLockfile && timeout 550 bash compile_test.sh` → `grep -c 'error CS' compile.log` = 0.
- EditMode(QA Phase): `timeout 590 bash run_editmode_check.sh` → TestOutput/editmode-results.xml 파싱 — baseline 11 고정 실패 집합(IsolatedSession…/CookingDatabase…/Performance×3/DamageIsIgnored+Initialize_Starts/EachAcceptedAttack×2/FindRecipe_UnknownCombo/ThemeUss) 외 0 실패.
- Play 확인 목록(사용자 게이트): ①마을 접근 → 골드 링 VFX 표시 ②E키 → "건설 시작!" 토스트 + 60s 후 실물 생성 ③부족 시 사유 토스트 ④E: 음식점 → 신규 창(Figma 정합) ⑤음식 선택→수량→'N개 추가 배치' ⑥판매금 받기 → 소지금 증가.

## 확정 사항(감독자 결정 — 보고에 명시)
- 영업 시간 08:00–22:00은 표시+영업 배지 판정만 적용, 판매 틱은 기존 일일 RunAllDaily 유지(최소검증 설계).
- 좌 요약 슬롯 표기는 실측 40 상한(Figma 목업치 15 미채용 — 위조 금지).
- 병사 배치/중독 UI 섹션은 Figma 부재로 제거, 병사는 첫 입고 시 자동 배치(A4).
