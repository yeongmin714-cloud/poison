using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Core.Utils;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// [D3-B] 음식점 일일 판매 시스템 (RESTAURANT_DRUG_ECONOMY_PLAN.md Phase D3).
    ///
    /// 역할:
    ///  · 판매 틱: TimeManager.OnDayStart(게임일 경계, GuardSalaryManager와 동일 구독 패턴) →
    ///    RunAllDaily — 마을 80곳(4국가 × 20, VillagePlacementSystem 결정론 순서) 순회.
    ///  · 판매 수학: RestaurantSaleMath(순수) + VillageDrugMath(D2 수학) 재사용 —
    ///    확률 롤(발각/적대)만 본 클래스가 System.Random으로 수행(호출부 주입 → 결정론 재현 가능).
    ///  · 발각: 마약 판매 시 DetectionProbability(영지 호감도, 등급) 롤 → 발각 시
    ///    ①배치 병사 즉시 처형 ②적대 전환 롤(HostileConversionProbability) → TerritoryState.playerHostile.
    ///    ⚠ 처형은 GuardPlaceholder.Die()를 호출하지 않는다 — Die()는 OnAnyGuardDied →
    ///    GuardResurrectionSystem이 무조건 부활시키므로, 무시락 경로
    ///    (GuardManager.RemoveGuardFromAllTerritories + GameObject 파괴)를 사용한다.
    ///  · 재고 입고: RestockFromPlayer — 플레이어 인벤 → VillageDrugState 마을 재고 이동.
    ///
    /// 골드 원장: [88차] RunDailySale은 VillageDrugState pending 적립(즉시 지급 폐지)으로 전환 —
    /// 수령(CollectRevenue) 시 PlayerStats.AddGold source 태그 "restaurant_drug_sale" /
    /// "restaurant_food_sale" 유지(EconomyAuditSystem 원장 스타일 준용).
    ///
    /// 결정론: 마을 순회 순서/시드 파생/수요 산식은 전부 결정론. 세이브 연동은 Phase D5.
    /// </summary>
    public static class RestaurantSystem
    {
        /// <summary>마약 음식 판매 수입 원장 태그 (PlayerStats.AddGold source — EconomyAuditSystem).</summary>
        public const string GoldSourceDrug = "restaurant_drug_sale";

        /// <summary>일반 음식 판매 수입 원장 태그 (PlayerStats.AddGold source — EconomyAuditSystem).</summary>
        public const string GoldSourceFood = "restaurant_food_sale";

        /// <summary>
        /// [88차] 마을 음식점 재고 슬롯 상한 — Figma "N / 6" 계약. 병합 키 신규 생성이 필요한 입고는
        /// 슬롯 수가 상한에 도달하면 거부하고, 기존 키 병합은 항상 허용한다(RestockFromPlayer 검사).
        /// </summary>
        public const int MaxStockSlots = 6;

        /// <summary>
        /// 음식 가격 폴백 기본가 — [실측] PlayerInventory.ItemData.basePrice(O2 C-O2-02)가
        /// 정의 부재/무가격(0)일 때 10G로 폴백하며 경고 로그로 보고한다.
        /// </summary>
        public const int DefaultFoodBasePrice = 10;

        /// <summary>마약 음식 id 접두사 — 규약 고정(CookingDrugHelper/D3-A): "drugged_{dishId}".</summary>
        private const string DruggedPrefix = "drugged_";

        /// <summary>마약 단계 마커 접두 — 규약 고정(CookingDrugHelper/D3-A): 설명 뒤 " [마약: stage n]" 부착.</summary>
        private const string DrugStageMarker = "[마약: stage ";

        // 마을 주민 수 근사 — [실측] VillageNpcSpawner(P31-D/T3-2): 일반 6~10 / 촌락(isHamlet) 3~5.
        // NPC 오브젝트 실집계는 씬 의존이라 EditMode/헤드리스에서 비결정적 → 등급별 중앙값 고정 근사(결정론).
        // D4(주민 이탈 — NPC 실제 제거)가 실집계 API를 제공하면 그때 대체 검토.
        private const int VillagerCountNormal = 8;  // 6~10 중앙값
        private const int VillagerCountHamlet = 4;  // 3~5 중앙값

        /// <summary>촌락 판정 영지 인덱스 하한 — [T3-1 실측] Ring4 = 인덱스 16~20 (VillagePlacementSystem과 동일 근거).</summary>
        private const int HamletTerritoryIndexMin = 16;

        /// <summary>촌락 판정 영지 인덱스 상한 — [T3-1 실측] Ring4 = 인덱스 16~20.</summary>
        private const int HamletTerritoryIndexMax = 20;

        /// <summary>처형 시 GameObject 파괴 지연(초) — 연출은 최소(즉시 제거 대신 짧은 지연, 상세 연출은 로그로 대체).</summary>
        private const float ExecuteDestroyDelay = 0.5f;

        /// <summary>
        /// 일일 판매 결과 보고.
        /// ⚠ CS0246 교훈(중첩 타입의 외부 파일 미한정 참조 금지) — 최상위 public struct로 선언한다.
        /// </summary>
        public struct SaleReport
        {
            public int soldNormal;        // 일반 음식 판매 수 (기본가 × 이 수만큼 수입)
            public int soldDrugged;       // 마약 음식 판매 수 (등록가 × 이 수만큼 수입)
            public int gold;              // 총 판매 수입 (마약 등록가 + 일반 기본가 합계)
            public float addictionGain;   // 이번 판매로 오른 마을 중독도 (%p 합계 — 마약 등급별 판매수 가중)
            public bool guardExecuted;    // 발각 → 배치 병사 처형 경로 실행 여부(실체 파괴 또는 실체 부재 시 플래그 클리어 포함)
            public bool hostileConverted; // 발각 → 영지 적대 전환 롤 성공 여부(TerritoryState.playerHostile = true)
        }

        // ===== 일일 판매 틱 =====

        /// <summary>
        /// 단일 마을 일일 판매 실행. 음식점 미건설/판매 담당 병사 미배치/재고 없음 마을은 스킵한다.
        /// rng는 수요 지터 → 발각 롤 → 적대 롤 순으로 소비하며(마약 판매가 없으면 지터만 소비),
        /// 호출부 소유이므로 동일 시드 재주입 시 동일 결과가 재현된다(결정론).
        /// [88차] 판매 수입은 PlayerStats 즉시 지급 대신 VillageDrugState pending 적립으로 전환
        /// (수령 = CollectRevenue — Figma "판매금 받기" 계약).
        /// </summary>
        public static SaleReport RunDailySale(NationType nation, int villageIndex, System.Random rng)
        {
            var report = default(SaleReport);
            string key = $"{nation}_v{villageIndex}";

            VillageDrugSnapshot snapshot = VillageDrugState.GetSnapshot(nation, villageIndex);
            if (!snapshot.restaurantBuilt)
            {
                Debug.Log($"[RestaurantSystem] {key} 음식점 미건설 — 일일 판매 스킵");
                return report;
            }
            if (string.IsNullOrEmpty(snapshot.assignedGuardId))
            {
                Debug.Log($"[RestaurantSystem] {key} 판매 담당 병사 미배치 — 일일 판매 스킵");
                return report;
            }

            List<RestaurantStockItem> stock = VillageDrugState.GetStock(nation, villageIndex);
            if (stock.Count == 0)
            {
                Debug.Log($"[RestaurantSystem] {key} 재고 없음 — 일일 판매 스킵");
                return report;
            }

            // ── 재고 집계 ──
            // 다중 슬롯 → 단일 가격 수학(RestaurantSaleMath 시그니처) 매핑 규약(결정론):
            //   · 일반 기본가 = basePrice > 0인 첫 일반 슬롯(입고 순서) — 없으면 0(일반 판매 없음).
            //   · 마약 권장가 기준 = 마약 슬롯 중 최고 basePrice(권장가는 basePrice 단조 증가 → 보수적 최고가).
            //   · 발각/적대 등급 기준 = 마약 슬롯 중 최고 drugStage(보수적).
            int normalStock = 0;
            int druggedStock = 0;
            int normalBasePrice = 0;
            int drugMaxBasePrice = 0;
            int topDrugStage = 0;
            foreach (RestaurantStockItem slot in stock)
            {
                if (slot.isDrugged)
                {
                    druggedStock += Mathf.Max(slot.count, 0);
                    if (slot.basePrice > drugMaxBasePrice) drugMaxBasePrice = slot.basePrice;
                    if (slot.drugStage > topDrugStage) topDrugStage = slot.drugStage;
                }
                else
                {
                    normalStock += Mathf.Max(slot.count, 0);
                    if (normalBasePrice == 0 && slot.basePrice > 0) normalBasePrice = slot.basePrice;
                }
            }

            // ── 수요/판매 계획 (순수 수학 위임) ──
            // [100차] 일반 음식 판매가 — 마을 일반 등록가 > 0이면 우선, 미설정(0)이면 재고 basePrice.
            //         PlanDailySale의 defaultBasePrice 인자로도 전달한다(골드 산식·무료 판매 방지 게이트 공유).
            int normalSalePrice = snapshot.normalRegisteredPrice > 0
                ? snapshot.normalRegisteredPrice
                : normalBasePrice;

            int recommendedPrice = druggedStock > 0
                ? VillageDrugMath.RecommendedPrice(drugMaxBasePrice, snapshot.addiction)
                : 0;
            float demandFactor = druggedStock > 0
                ? VillageDrugMath.DemandFactor(snapshot.registeredPrice, recommendedPrice)
                : 1f; // 마약 재고 없음 → 등록가 수요 곡선 미적용
            // [100차] 가격 수요 계수 — 일반 판매가를 DemandCount 기저에 곱한다. PlanDailySale 시그니처
            //         불변 계약(인자 재활용) → 마약 등록가 계수 × 일반 가격 계수를 demandFactor로 합성 전달.
            //         일반 판매가 축이 없는 마을(normalSalePrice<=0 — 가격 미확보)은 계수 1.0 유지로
            //         마약 수요를 오염시키지 않는다.
            if (normalSalePrice > 0)
                demandFactor *= RestaurantDemandMath.VisitorDemandFactor(normalSalePrice, normalBasePrice);

            int demand = RestaurantSaleMath.DemandCount(
                EstimateVillagerCount(villageIndex), snapshot.addiction, snapshot.departedVillagers, rng.NextDouble());

            (int planNormal, int planDrugged, int _) = RestaurantSaleMath.PlanDailySale(
                normalStock, druggedStock, demand, snapshot.registeredPrice, normalSalePrice, demandFactor);

            // ── 실소모 (VillageDrugState — 입고 순서 슬롯 순회, 마약 먼저) ──
            int remainingDrugged = planDrugged;
            int remainingNormal = planNormal;
            float addictionGain = 0f;
            AddictionLevel topConsumedLevel = AddictionLevel.Low;
            foreach (RestaurantStockItem slot in stock)
            {
                if (remainingDrugged > 0 && slot.isDrugged && slot.count > 0)
                {
                    int consumed = VillageDrugState.ConsumeStock(
                        nation, villageIndex, slot.foodItemId, true, slot.drugStage, remainingDrugged);
                    if (consumed > 0)
                    {
                        remainingDrugged -= consumed;
                        AddictionLevel level = ResolveAddictionLevel(slot.drugStage);
                        addictionGain += VillageDrugMath.AddictionGainPerSale(level) * consumed;
                        if ((int)level > (int)topConsumedLevel) topConsumedLevel = level;
                    }
                }
                else if (remainingNormal > 0 && !slot.isDrugged && slot.count > 0)
                {
                    int consumed = VillageDrugState.ConsumeStock(
                        nation, villageIndex, slot.foodItemId, false, slot.drugStage, remainingNormal);
                    if (consumed > 0)
                        remainingNormal -= consumed;
                }
            }

            int soldDrugged = planDrugged - Mathf.Max(remainingDrugged, 0);
            int soldNormal = planNormal - Mathf.Max(remainingNormal, 0);

            // ── 중독 상승 (마약 등급별 판매수 가중 합계) ──
            if (addictionGain > 0f)
                VillageDrugState.AddAddiction(nation, villageIndex, addictionGain);

            // ── [88차] 판매금 적립 (즉시 지급 폐지 — Figma "판매금 받기" 계약) ──
            // 판매 수입을 마을 단위 pending으로 적립하고, 수령은 RestaurantSystem.CollectRevenue가
            // 담당한다. 원장 태그(GoldSourceDrug/GoldSourceFood)는 수령 시점에 동일 적용된다.
            int drugGold = soldDrugged * Mathf.Max(snapshot.registeredPrice, 0);
            int normalGold = soldNormal * Mathf.Max(normalSalePrice, 0); // [100차] 일반 판매가 = 등록가 우선 → basePrice 폴백
            int gold = drugGold + normalGold;

            if (gold > 0)
                VillageDrugState.AddPendingRevenue(nation, villageIndex, drugGold, normalGold);

            // ── 발각 → ①처형 ②적대 전환 (마약 판매가 있을 때만) ──
            bool guardExecuted = false;
            bool hostileConverted = false;
            if (soldDrugged > 0)
            {
                // 마을→영지 1:1 대응 — [T3 실측] villageIndex 0~19 ↔ 영지 인덱스 1~20
                TerritoryState territory = TerritoryDatabase.Instance.GetState(nation, villageIndex + 1);
                float loyalty = territory.loyaltyToPlayer;

                // 발각 롤 — 등급은 소모 슬롯 중 최고 등급(보수적 단일 롤 규약)
                bool detected = rng.NextDouble() < VillageDrugMath.DetectionProbability(loyalty, topConsumedLevel);
                if (detected)
                {
                    // ①처형 — 무시락 경로(Die() 미사용 — 부활 시스템 회피). 실체 부재 시 플래그 클리어+보고.
                    guardExecuted = ExecutePlacedGuard(nation, villageIndex, snapshot.assignedGuardId);
                    VillageDrugState.RemoveGuard(nation, villageIndex); // 처형/실체 부재 모두 판매 담당 해제

                    // ②적대 전환 롤 — 성공 시 영지 적대 플래그([D3-B] TerritoryState.playerHostile)
                    if (rng.NextDouble() < VillageDrugMath.HostileConversionProbability(topConsumedLevel))
                    {
                        territory.playerHostile = true;
                        hostileConverted = true;
                        // [D4-C] 적대 전환 즉시 — 해당 영지 주둔 병사에게 플레이어 공격 명령 발행.
                        //        정적 진입점(컴포넌트 불필요) — 씬/GuardManager 부재 시 내부에서 안전 반환.
                        HostileTerritoryCombat.NotifyHostile(nation, villageIndex + 1);
                        Debug.LogWarning(
                            $"[RestaurantSystem] {key} 마약 발각 → 영지 {new TerritoryId(nation, villageIndex + 1)} 적대 전환!");
                    }
                }
            }

            report.soldNormal = soldNormal;
            report.soldDrugged = soldDrugged;
            report.gold = gold;
            report.addictionGain = addictionGain;
            report.guardExecuted = guardExecuted;
            report.hostileConverted = hostileConverted;

            Debug.Log($"[RestaurantSystem] {key} 일일 판매: 일반 {soldNormal}(+{normalGold}G) / " +
                      $"마약 {soldDrugged}(+{drugGold}G) / 중독 +{addictionGain:F1} / 처형={guardExecuted} / 적대={hostileConverted}");
            return report;
        }

        /// <summary>
        /// 전 마을(4국가 × 20 = 80곳, VillagePlacementSystem 결정론 순서) 일일 판매 순회.
        /// 각 마을에 고정 시드 System.Random을 부여한다 — 해시 17·31 문자열 해시
        /// (VillagePlacementSystem/TerritoryDatabase와 동일 알고리즘)로 daySeed+마을 키에서 파생 →
        /// 프로세스 재시작 간 동일 롤 시퀀스(결정론). UnityEngine.Random 사용 없음.
        /// 개별 마을 예외는 로그 후 계속(하나의 실패가 전체 틱을 중단시키지 않음 — GuardSalaryManager 패턴).
        /// </summary>
        public static void RunAllDaily(int daySeed)
        {
            List<VillagePlacementSystem.VillageInfo> villages = VillagePlacementSystem.GetAllVillages();
            int soldVillages = 0;
            foreach (VillagePlacementSystem.VillageInfo village in villages)
            {
                try
                {
                    var rng = new System.Random(DeriveVillageSeed(daySeed, village.nation, village.index));
                    SaleReport report = RunDailySale(village.nation, village.index, rng);
                    if (report.soldNormal + report.soldDrugged > 0)
                        soldVillages++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[RestaurantSystem] {village.nation}_v{village.index} 일일 판매 실패: {ex.Message}");
                }
            }
            Debug.Log($"[RestaurantSystem] RunAllDaily(day={daySeed}) — 판매 발생 마을 {soldVillages}/{villages.Count}");
        }

        // ===== 판매금 수령 [88차] =====

        /// <summary>
        /// [88차] 판매금 수령 — 마을 pending 적립금(Figma "판매금 받기")을 일괄 수령한다.
        /// TakePendingRevenue(합산 반환 후 0 리셋) → PlayerStats.AddGold에 원장 태그 분리 적용
        /// (마약 = GoldSourceDrug / 일반 = GoldSourceFood — 적립 전환 후에도 태그 계약 유지).
        /// 반환: 총 수령액(음수/0 안전 — 0이면 수령 없음). PlayerStats 미존재 시 적립금을
        /// 소비하지 않고(원복) 0 반환 — "소비 없이 실패" 계약.
        /// </summary>
        public static int CollectRevenue(NationType nation, int villageIndex)
        {
            (int drugGold, int normalGold) pending = VillageDrugState.TakePendingRevenue(nation, villageIndex);
            int total = pending.drugGold + pending.normalGold;
            if (total <= 0)
                return 0; // 음수/0 안전 — 수령액 없음(지출/태그 발생 없음)

            PlayerStats stats = PlayerStats.Instance;
            if (stats == null)
            {
                // 소비 없이 실패 — Take로 소진한 적립금을 원복한다(결정론 복원)
                VillageDrugState.AddPendingRevenue(nation, villageIndex, pending.drugGold, pending.normalGold);
                Debug.LogWarning($"[RestaurantSystem] {nation}_v{villageIndex} PlayerStats 미존재 — 판매금 {total}G 수령 실패(적립 유지)");
                return 0;
            }

            if (pending.drugGold > 0) stats.AddGold(pending.drugGold, GoldSourceDrug);
            if (pending.normalGold > 0) stats.AddGold(pending.normalGold, GoldSourceFood);
            Debug.Log($"[RestaurantSystem] {nation}_v{villageIndex} 판매금 수령: 총 {total}G (마약 {pending.drugGold}G / 일반 {pending.normalGold}G)");
            return total;
        }

        // ===== 재고 입고 (플레이어 → 마을) =====

        /// <summary>
        /// 플레이어 인벤토리 → 마을 음식점 재고 이동. 음식 id 규약(CookingDrugHelper/D3-A 고정):
        ///   · 일반 음식 — 그대로 입고(drugStage 0). basePrice 해석 우선순위: ①인벤 실물 ItemData의
        ///     basePrice → ②정적 정의(GetItemById, O2)의 basePrice → ③둘 다 미실측(<=0)이면
        ///     DefaultFoodBasePrice(10G) 폴백 + 경고 보고.
        ///   · 마약 음식 — id가 "drugged_" 접두 → drugStage는 실물 인스턴스(조리 생성) 설명의
        ///     "[마약: stage n]" 마커에서 파싱. 기본가는 같은 우선순위(실물 → 기반 요리(dishId) 정의
        ///     재조회)로 해석하고, 그래도 없으면 DefaultFoodBasePrice(10G) 폴백 + 경고 보고.
        /// 부분 소모 방지: RemoveItem은 부족 시 일부만 제거하고 false를 반환하므로
        /// 사전에 GetItemCount로 보유량을 검증한다(검증 실패 시 인벤/재고 무변경).
        /// [88차] 신규: 재고 슬롯 상한(MaxStockSlots=6) — 병합 키 신규 생성 필요 시 슬롯 도달이면
        ///   인벤 소비 전에 거부한다(병합 입고는 항상 허용). 성공 시 판매 담당 병사 미배치면
        ///   AssignGuardFromPlayer 1회 자동 시도(GuardManager 부재 = 안전 무동작).
        /// </summary>
        public static bool RestockFromPlayer(NationType nation, int villageIndex, string itemId, int count)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0)
            {
                Debug.LogWarning($"[RestaurantSystem] 재고 입고 거부 — itemId/수량 비정상 (id={itemId}, count={count})");
                return false;
            }

            PlayerInventory inventory = PlayerInventory.Instance;
            if (inventory == null)
            {
                Debug.LogWarning("[RestaurantSystem] PlayerInventory 미존재 — 재고 입고 실패");
                return false;
            }

            if (inventory.GetItemCount(itemId) < count)
            {
                Debug.LogWarning($"[RestaurantSystem] 인벤 수량 부족 — {itemId} 요청 {count}");
                return false;
            }

            bool isDrugged = itemId.StartsWith(DruggedPrefix, System.StringComparison.Ordinal);

            // 표시명/마약 마커 소스 — 실물 인스턴스(조리 시 생성, 마커 보유) 우선, 정적 정의 폴백
            PlayerInventory.ItemData held = FindFirstSlotItem(inventory, itemId);
            PlayerInventory.ItemData definition = PlayerInventory.GetItemById(itemId);
            PlayerInventory.ItemData source = held ?? definition;

            string displayName = !string.IsNullOrEmpty(source?.displayName) ? source.displayName : itemId;
            int drugStage = isDrugged ? ParseDrugStage(source?.description) : 0;
            // 기본가 해석 우선순위 — ①인벤 실물 ItemData(같은 id 보유 시)의 basePrice
            // → ②정적 정의(일반: GetItemById(itemId) / 마약: 기반 요리 GetItemById(dishId))의 basePrice
            // → ③둘 다 미실측(basePrice<=0, 0=무가격 규약 O2 C-O2-02)일 때만 10G 폴백 + 경고.
            // 인벤 실물이 1순위인 이유: 조리/구매 시점에 인스턴스에 실측된 가격이 가장 신뢰 가능하며,
            // 정의 DB 미등록 아이템(테스트 세팅 포함)도 실물 가격을 존중하기 위함.
            int basePrice = DefaultFoodBasePrice;

            if (held != null && held.basePrice > 0)
            {
                basePrice = held.basePrice; // ① 인벤 실물 실측가
            }
            else if (!isDrugged && definition != null && definition.basePrice > 0)
            {
                basePrice = definition.basePrice; // ② 일반 음식 정적 정의(O2)
            }
            else if (isDrugged)
            {
                // 마약 음식 — 기반 요리(dishId = "drugged_" 제거) 정의 재조회, 없으면 10G 폴백(보고)
                string dishId = itemId.Substring(DruggedPrefix.Length);
                PlayerInventory.ItemData dishDef = PlayerInventory.GetItemById(dishId);
                if (dishDef != null && dishDef.basePrice > 0)
                    basePrice = dishDef.basePrice; // ② 기반 요리 정의 가격
                else
                    Debug.LogWarning($"[RestaurantSystem] 마약 음식 기본가 미실측({itemId}) — 기본 {DefaultFoodBasePrice}G 폴백");
            }
            else
            {
                Debug.LogWarning($"[RestaurantSystem] 음식 아이템 기본가 미실측({itemId}) — 기본 {DefaultFoodBasePrice}G 폴백");
            }

            // ── [88차] 재고 슬롯 상한 검사(6) — 인벤 소비 전에 판정(벤치 원칙: 실패 시 소비 없음) ──
            // 병합 키 (foodItemId, isDrugged, drugStage)가 이미 존재하면 병합 입고 — 상한 무관 항상 허용.
            // 신규 키가 필요한데 슬롯 수가 MaxStockSlots에 도달했으면 거부한다.
            bool mergeKeyExists = HasStockKey(nation, villageIndex, itemId, isDrugged, drugStage);
            if (!mergeKeyExists
                && VillageDrugState.GetStock(nation, villageIndex).Count >= MaxStockSlots)
            {
                Debug.LogWarning($"[RestaurantSystem] {nation}_v{villageIndex} 재고 슬롯 상한({MaxStockSlots}) 도달 — 신규 키 입고 거부 ({itemId})");
                return false;
            }

            if (!inventory.RemoveItem(itemId, count))
            {
                Debug.LogWarning($"[RestaurantSystem] 인벤 제거 실패(경합?) — {itemId} ×{count}");
                return false;
            }

            VillageDrugState.AddStock(nation, villageIndex, new RestaurantStockItem
            {
                foodItemId = itemId,
                displayName = displayName,
                count = count,
                isDrugged = isDrugged,
                drugStage = drugStage,
                basePrice = basePrice,
            });

            // ── [88차] 판매 담당 병사 자동 배치 — 입고 성공 직후 미배치면 1회 시도 ──
            // GuardManager 부재(EditMode)는 AssignGuardFromPlayer 내부에서 안전 무동작(null 반환) —
            // RunDailySale의 "미배치 스킵" 계약을 해치지 않는다.
            if (string.IsNullOrEmpty(VillageDrugState.GetSnapshot(nation, villageIndex).assignedGuardId))
            {
                string assignedId = AssignGuardFromPlayer(nation, villageIndex);
                if (string.IsNullOrEmpty(assignedId))
                    Debug.Log($"[RestaurantSystem] {nation}_v{villageIndex} 가용 병사 없음 — 판매 담당 자동 배치 미완료(수동 배치 필요)");
            }

            // ── [151차 P1] Test_15 음식점 즉시 구매 트리거 — 입고 완료 직후 방문 스캔 1회 즉시 실행 ──
            //   병목 해소: 재고 입고 후 다음 티커 스캔(≤2.5s/1s) 대기 없이 ScanAndAssign 즉시 호출.
            //   Livelihood 씬(Test_15/20) 한정 — 프로덕션 씬 무영향. ScanAndAssign 내부에서
            //   영업시간/재고>0/건물+담당 게이트를 재판정(멱등 스캔 — 중복 실행 무해)하며
            //   정산 진실원천(재고/골드/적립) 불접촉 계약도 그대로 유지된다.
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.Contains("Livelihood"))
                RestaurantVisitorSystem.ScanAndAssign();   // internal — 같은 어셈블리(ProjectName.Systems)
            return true;
        }

        /// <summary>
        /// [88차 B3] RestockFromPlayer 사유 진단 오버로드 — 기존 bool 시그니처 유지·위임(기존 단일 소스 경로 재사용).
        /// 실패 시 실측 상태(PlayerInventory 보유량/병합 키/슬롯 상한)로 사유 문구를 산출해 UI 토스트가
        /// 위조 없이 표시한다. 성공 시 reason = 빈 문자열.
        /// </summary>
        public static bool RestockFromPlayer(NationType nation, int villageIndex, string itemId, int count, out string reason)
        {
            bool ok = RestockFromPlayer(nation, villageIndex, itemId, count);
            if (ok)
            {
                reason = string.Empty;
                return true;
            }
            reason = DescribeRestockFailure(nation, villageIndex, itemId, count);
            return false;
        }

        /// <summary>[88차 B3] 입고 실패 사유 산출 — bool 경로 실패 직후의 실측 상태로 원인을 좁힌다(상태 비파괴).</summary>
        private static string DescribeRestockFailure(NationType nation, int villageIndex, string itemId, int count)
        {
            PlayerInventory inventory = PlayerInventory.Instance;
            if (inventory == null)
                return "인벤토리 미존재 — 입고 불가";
            if (string.IsNullOrEmpty(itemId) || count <= 0)
                return $"입고 요청 비정상 — 아이템/수량 확인 (id={itemId}, count={count})";

            int held = inventory.GetItemCount(itemId);
            if (held < count)
                return $"보유 수량 부족 — 보유 {held}개 / 요청 {count}개";

            // 이 지점 도달 실패 = 슬롯 상한(6) 거부 또는 인벤 제거 경합 — 병합 키 재판정으로 구분
            bool isDrugged = itemId.StartsWith(DruggedPrefix, System.StringComparison.Ordinal);
            PlayerInventory.ItemData heldItem = FindFirstSlotItem(inventory, itemId);
            PlayerInventory.ItemData definition = PlayerInventory.GetItemById(itemId);
            PlayerInventory.ItemData source = heldItem ?? definition;
            int drugStage = isDrugged ? ParseDrugStage(source?.description) : 0;
            if (!HasStockKey(nation, villageIndex, itemId, isDrugged, drugStage)
                && VillageDrugState.GetStock(nation, villageIndex).Count >= MaxStockSlots)
            {
                return $"판매대 슬롯 상한({MaxStockSlots}) 도달 — 새 음식 종류는 더 배치할 수 없음(기존 음식 추가 입고는 가능)";
            }

            return $"입고 실패 — 인벤 상태 확인(경합) ({itemId})";
        }

        /// <summary>[88차] 병합 키 (foodItemId, isDrugged, drugStage) 슬롯 존재 판정 — GetStock 복사본 조회(상태 비파괴).</summary>
        private static bool HasStockKey(NationType nation, int villageIndex, string itemId, bool isDrugged, int drugStage)
        {
            foreach (RestaurantStockItem slot in VillageDrugState.GetStock(nation, villageIndex))
            {
                if (slot.foodItemId == itemId && slot.isDrugged == isDrugged && slot.drugStage == drugStage)
                    return true;
            }
            return false;
        }

        // ===== 판매 담당 병사 배치 [D5-B] =====

        /// <summary>
        /// [D5-B] 음식점 판매 담당 병사 자동 배치 — 가용(플레이어 소유) 병사 중 첫 실체를 선택해
        /// VillageDrugState.AssignGuard로 배치하고 그 StableId를 반환한다.
        ///
        /// 가용 판정(실측): GuardManager.GetAllPlayerGuards() — 플레이어 소유 영지 등록 병사 +
        /// standalone(실내 막사 등) 포섭 병사(GuardPlaceholder.IsRecruited = _isRecruited 래퍼, 38차 실측).
        /// 여기에 IsAlive와 "타 마을 음식점 미배치"(4국가×20마을 스냅샷 스캔 — 결정론)를 추가 필터한다.
        ///
        /// 반환: 배치된 병사 StableId(성공) / null(가용 병사 없음·GuardManager 부재 — 안전 무동작,
        /// 마을 상태 생성 없음). GameObject 의존부(Play 전용) — EditMode에서는 null 반환.
        /// </summary>
        public static string AssignGuardFromPlayer(NationType nation, int villageIndex)
        {
            GuardManager guardManager = GuardManager.Instance;
            if (guardManager == null)
            {
                Debug.LogWarning("[RestaurantSystem] GuardManager 미존재 — 판매 담당 병사 배치 실패");
                return null;
            }

            HashSet<string> assignedIds = CollectAssignedGuardIds();
            foreach (GuardPlaceholder guard in guardManager.GetAllPlayerGuards())
            {
                if (guard == null || !guard.IsAlive || !guard.IsRecruited)
                    continue; // 사망/미포섭 병사 제외 — 가용 판정 규약
                if (!string.IsNullOrEmpty(guard.StableId) && assignedIds.Contains(guard.StableId))
                    continue; // 이미 다른 마을 음식점에 배치된 병사 제외 — 중복 배지 방지

                VillageDrugState.AssignGuard(nation, villageIndex, guard.StableId);
                Debug.Log($"[RestaurantSystem] {nation}_v{villageIndex} 판매 담당 병사 배치: {guard.GuardName}({guard.StableId})");
                return guard.StableId;
            }

            Debug.Log($"[RestaurantSystem] {nation}_v{villageIndex} 가용 병사 없음 — 배치 실패");
            return null;
        }

        /// <summary>
        /// [D5-B] 전 마을(4국가 × 20 = 80곳, VillagePlacementSystem 결정론 축) 스냅샷 순회 —
        /// 현재 음식점에 배치된 병사 id 집합. 읽기 전용 조회(GetSnapshot)만 수행한다(상태 생성 없음).
        /// </summary>
        private static HashSet<string> CollectAssignedGuardIds()
        {
            var ids = new HashSet<string>();
            NationType[] villageNations = { NationType.East, NationType.West, NationType.South, NationType.North };
            foreach (NationType nation in villageNations)
            {
                for (int villageIndex = 0; villageIndex < 20; villageIndex++)
                {
                    string assignedId = VillageDrugState.GetSnapshot(nation, villageIndex).assignedGuardId;
                    if (!string.IsNullOrEmpty(assignedId))
                        ids.Add(assignedId);
                }
            }
            return ids;
        }

        // ===== 내부 헬퍼 =====

        /// <summary>
        /// 발각 시 판매 담당 병사 즉시 처형(무시락 경로). GuardManager 등록 목록에서
        /// assignedGuardId(GuardPlaceholder.StableId 매칭)로 실체를 찾아 등록 해제 + 파괴한다.
        /// ⚠ Die() 미사용 — Die()는 OnAnyGuardDied → GuardResurrectionSystem이 무조건 부활시킨다.
        /// 실체가 없으면(오프라인 마을/씬 미로드) 배치 플래그 클리어만 수행하고 false를 보고한다.
        /// GameObject 파괴는 Play 전용 경로 — EditMode 테스트에서는 실행되지 않는다(Play 게이트).
        /// </summary>
        private static bool ExecutePlacedGuard(NationType nation, int villageIndex, string assignedGuardId)
        {
            string key = $"{nation}_v{villageIndex}";
            GuardManager guardManager = GuardManager.Instance;

            GuardPlaceholder target = null;
            if (guardManager != null)
            {
                foreach (KeyValuePair<TerritoryId, GuardPlaceholder> pair in guardManager.GetAllRegisteredGuards())
                {
                    if (pair.Value != null && pair.Value.StableId == assignedGuardId)
                    {
                        target = pair.Value;
                        break;
                    }
                }
            }

            if (target == null)
            {
                // 실체 없음 — 배치 플래그 클리어만 + 로그 보고(처형 상태 정리는 RemoveGuard가 담당)
                Debug.LogWarning(
                    $"[RestaurantSystem] {key} 마약 발각 — 배치 병사 실체 미발견(id={assignedGuardId}), 배치 플래그만 해제");
                return false;
            }

            // 무시락: 사망 이벤트 미발생 경로로 전 영지 등록 해제 → 전투 로그(처형) → GameObject 파괴(짧은 지연)
            guardManager.RemoveGuardFromAllTerritories(target);
            CombatLog.AddEntry($"[음식점] 마약 발각 — 판매 담당 병사 {target.GuardName} 즉시 처형 ({key})", LogType.Kill);
            Object.Destroy(target.gameObject, ExecuteDestroyDelay);
            Debug.LogWarning($"[RestaurantSystem] {key} 마약 발각 — 배치 병사 {target.GuardName} 처형");
            return true;
        }

        /// <summary>마약 단계 → 중독 등급. [실측] DrugDatabase.GetByStage(1~10) → DrugInfo.Addiction. 미실측 단계는 Low 방어.</summary>
        private static AddictionLevel ResolveAddictionLevel(int drugStage)
        {
            DrugInfo? info = DrugDatabase.GetByStage(drugStage);
            return info.HasValue ? info.Value.Addiction : AddictionLevel.Low;
        }

        /// <summary>
        /// 마을 주민 수 근사 — [실측] VillageNpcSpawner: 일반 6~10 / 촌락 3~5의 중앙값 고정(결정론).
        /// 촌락 판정 = 기준 영지 인덱스(villageIndex+1)가 Ring4(16~20) — [T3-1 실측].
        /// </summary>
        private static int EstimateVillagerCount(int villageIndex)
        {
            int territoryIndex = villageIndex + 1; // 마을→영지 1:1 (T3 실측)
            bool isHamlet = territoryIndex >= HamletTerritoryIndexMin && territoryIndex <= HamletTerritoryIndexMax;
            return isHamlet ? VillagerCountHamlet : VillagerCountNormal;
        }

        /// <summary>마을별 결정론 시드 파생 — 해시 17·31 문자열 해시(프로세스 재시작 간 동일, string.GetHashCode 미사용).</summary>
        private static int DeriveVillageSeed(int daySeed, NationType nation, int villageIndex)
        {
            unchecked
            {
                int hash = 17;
                string input = $"restaurant_{daySeed}_{(int)nation}_{villageIndex}";
                foreach (char c in input)
                    hash = hash * 31 + c;
                return hash;
            }
        }

        /// <summary>인벤 슬롯에서 해당 id의 첫 실물 ItemData 반환(조리 생성 인스턴스 — 마약 마커 보유). 없으면 null.</summary>
        private static PlayerInventory.ItemData FindFirstSlotItem(PlayerInventory inventory, string itemId)
        {
            PlayerInventory.ItemSlot[] slots = inventory.GetAllSlots();
            if (slots == null)
                return null;
            foreach (PlayerInventory.ItemSlot slot in slots)
            {
                if (slot != null && slot.item != null && slot.item.id == itemId)
                    return slot.item;
            }
            return null;
        }

        /// <summary>CookingDrugHelper 마커("[마약: stage n]")에서 단계 파싱 — 실패 시 0 + 경고(호출부에서 Low 방어).</summary>
        private static int ParseDrugStage(string description)
        {
            if (!string.IsNullOrEmpty(description))
            {
                int markerIndex = description.IndexOf(DrugStageMarker, System.StringComparison.Ordinal);
                if (markerIndex >= 0)
                {
                    int start = markerIndex + DrugStageMarker.Length;
                    int end = description.IndexOf(']', start);
                    if (end > start &&
                        int.TryParse(description.Substring(start, end - start).Trim(), out int stage))
                        return Mathf.Max(stage, 0);
                }
            }
            Debug.LogWarning("[RestaurantSystem] 마약 단계 마커 파싱 실패 — drugStage 0 처리");
            return 0;
        }
    }

    /// <summary>
    /// [D3-B] 음식점 일일 판매 부트스트래퍼 — TimeManager.OnDayStart(게임일 경계) 구독.
    /// 구독 패턴은 GuardSalaryManager와 동일(OnEnable 구독/OnDisable 해제, Instance 널 가드).
    /// 씬 배선은 GameSetup 계열 Ensure* 멱등 패턴으로 추가(D5 UI 단계 연계 — 본 파일은 로직만 제공).
    /// </summary>
    public sealed class RestaurantDailyTicker : MonoBehaviour
    {
        private void OnEnable()
        {
            TimeManager tm = TimeManager.Instance;
            if (tm != null) tm.OnDayStart += OnDayStart;
        }

        private void OnDisable()
        {
            TimeManager tm = TimeManager.Instance;
            if (tm != null) tm.OnDayStart -= OnDayStart;
        }

        /// <summary>게임일 경계 → 전 마을 일일 판매(시드 = 현재 게임 일차) → 주민 이탈 판정.</summary>
        private void OnDayStart()
        {
            TimeManager tm = TimeManager.Instance;
            int day = tm != null ? tm.CurrentDay : 0;
            RestaurantSystem.RunAllDaily(day);
            // [D4-B] 주민 이탈 판정 — RunAllDaily 직후 같은 게임일 틱에서 실행(당일 중독/폭력화 상태 반영).
            VillagerDepartureSystem.RunDailyDeparture(day);
        }
    }
}
