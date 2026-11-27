using System.Collections.Generic;
using NUnit.Framework;
using ProjectName.Core.Data; // NationType/RestaurantStockItem/VillageDrugSnapshot (TerritoryData.cs/VillageDrugState.cs)

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [2026-10-15 D2 — 마을 음식점/마약 상태 저장소] 계약 테스트 (VillageDrugState.cs).
    /// - 미접촉 마을 스냅샷: 기본값 규약(미건설/가드 빈문자열/중독 0/등록가 0/이탈 0/재고 빈 목록).
    /// - 음식점/병사: 건설 플래그 반영, 가드 배치·제거 후 빈 문자열 복귀.
    /// - 재고 병합: 키 (foodItemId, isDrugged, drugStage) 동일 슬롯 수량 합산(표시명 보완·기본가 최신화),
    ///   이질 슬롯은 분리 보관(입고 순서 유지).
    /// - 재고 소모: 요청 ≤ 보유면 정확히 소모, 부족하면 보유량만 소모 후 반환, 0 슬롯 정리, 부재/0 요청 0.
    /// - 중독 클램프: 음수 gain → 0 하한, 누적 100 초과 → 100 상한.
    /// - 등록가/이탈: Set→Get 왕복, RecordDeparture 누적.
    /// - 상태 독립: 다른 국가/다른 villageIndex 키는 서로 간섭하지 않는다.
    /// 실행 판정은 QA 서브에이전트 증거로 확정(구현 서브에이전트는 실행하지 않음).
    /// </summary>
    public sealed class VillageDrugStateTests
    {
        private const float Tol = 0.0001f; // 중독도(float) 비교 허용 오차

        [SetUp]
        public void SetUp()
        {
            // 테스트 독립성 — static 저장소이므로 매 테스트 전 전체 초기화(VillageDrugState 테스트 전용 계약).
            VillageDrugState.ResetAll();
        }

        /// <summary>재고 입고용 슬롯 생성 헬퍼 — 필드 나열 실수 방지.</summary>
        private static RestaurantStockItem Item(
            string foodItemId, string displayName, int count, bool isDrugged, int drugStage, int basePrice)
        {
            return new RestaurantStockItem
            {
                foodItemId = foodItemId,
                displayName = displayName,
                count = count,
                isDrugged = isDrugged,
                drugStage = drugStage,
                basePrice = basePrice,
            };
        }

        // ════════════════════════════════════════════════════════════════════
        // 1. 기본값 스냅샷 — 상태를 만지지 않은 마을
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void Snapshot_UntouchedVillage_ReturnsContractDefaults()
        {
            VillageDrugSnapshot snapshot = VillageDrugState.GetSnapshot(NationType.East, 5);

            // 조회 규약: 미접촉 마을은 기본값 스냅샷 — 예외 대신 기본값 반환
            Assert.That(snapshot.restaurantBuilt, Is.False, "미건설 마을의 restaurantBuilt는 false여야 한다.");
            Assert.That(snapshot.assignedGuardId, Is.EqualTo(string.Empty),
                "미배치 가드는 null이 아닌 빈 문자열(규약)이어야 한다.");
            Assert.That(snapshot.addiction, Is.EqualTo(0f).Within(Tol), "미접촉 마을 중독도는 0이어야 한다.");
            Assert.That(snapshot.registeredPrice, Is.EqualTo(0), "미등록 판매가는 0이어야 한다.");
            Assert.That(snapshot.departedVillagers, Is.EqualTo(0), "미접촉 마을 이탈 주민은 0이어야 한다.");

            // 재고 규약: 상태 없는 마을은 빈 목록(복사본)
            List<RestaurantStockItem> stock = VillageDrugState.GetStock(NationType.East, 5);
            Assert.That(stock, Is.Empty, "미접촉 마을 재고는 빈 목록이어야 한다.");
        }

        // ════════════════════════════════════════════════════════════════════
        // 2. 음식점 건설/병사 배치 — 스냅샷 반영 및 제거 복귀
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void BuildAndGuard_SetThenRemove_ReflectsInSnapshot()
        {
            VillageDrugState.SetRestaurantBuilt(NationType.West, 3, true);
            VillageDrugState.AssignGuard(NationType.West, 3, "G7");

            VillageDrugSnapshot built = VillageDrugState.GetSnapshot(NationType.West, 3);
            Assert.That(built.restaurantBuilt, Is.True, "건설 플래그 설정은 스냅샷에 반영되어야 한다.");
            Assert.That(built.assignedGuardId, Is.EqualTo("G7"), "배치한 병사 ID가 스냅샷에 반영되어야 한다.");

            VillageDrugState.RemoveGuard(NationType.West, 3);

            VillageDrugSnapshot removed = VillageDrugState.GetSnapshot(NationType.West, 3);
            Assert.That(removed.assignedGuardId, Is.EqualTo(string.Empty),
                "가드 제거 후 assignedGuardId는 빈 문자열로 복귀해야 한다.");
            Assert.That(removed.restaurantBuilt, Is.True,
                "가드 제거는 음식점 건설 플래그에 영향을 주지 않아야 한다.");
        }

        // ════════════════════════════════════════════════════════════════════
        // 3. 재고 병합 — 동일 키 슬롯 합산 / 이질 슬롯 분리
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void AddStock_SameKeySlotsMerge_AndDifferentKeysStaySeparate()
        {
            // 동일 병합 키 (meat_grilled, false, 0) 2회 입고 — 수량 합산
            VillageDrugState.AddStock(NationType.East, 0, Item("meat_grilled", "고기구이", 3, false, 0, 10));
            VillageDrugState.AddStock(NationType.East, 0, Item("meat_grilled", string.Empty, 2, false, 0, 12));

            // 이질 병합 키 (meat_grilled, true, 4) — 분리 슬롯 신규 추가
            VillageDrugState.AddStock(NationType.East, 0, Item("meat_grilled", "마약 뿌려진 고기구이", 1, true, 4, 20));

            List<RestaurantStockItem> stock = VillageDrugState.GetStock(NationType.East, 0);

            // 병합 규약: 동일 키 1슬롯 + 이질 키 1슬롯 = 총 2슬롯(입고 순서 유지)
            Assert.That(stock, Has.Count.EqualTo(2), "동일 키는 합산되고 이질 키는 분리되어야 한다.");

            // 첫 슬롯 — 수량 3+2=5, 표시명은 빈 슬롯에만 채워지므로 최초값 유지, 기본가는 최신 입고값
            Assert.That(stock[0].foodItemId, Is.EqualTo("meat_grilled"));
            Assert.That(stock[0].isDrugged, Is.False);
            Assert.That(stock[0].drugStage, Is.EqualTo(0));
            Assert.That(stock[0].count, Is.EqualTo(5), "동일 키 슬롯은 수량이 합산되어야 한다.");
            Assert.That(stock[0].displayName, Is.EqualTo("고기구이"),
                "표시명은 기존 값이 비어 있지 않으면 유지되어야 한다.");
            Assert.That(stock[0].basePrice, Is.EqualTo(12), "기본가는 최신 입고값으로 갱신되어야 한다.");

            // 두 번째 슬롯 — 이질 키는 독립 보관
            Assert.That(stock[1].isDrugged, Is.True, "마약 여부가 다른 슬롯은 분리되어야 한다.");
            Assert.That(stock[1].drugStage, Is.EqualTo(4), "마약 단계가 다른 슬롯은 분리되어야 한다.");
            Assert.That(stock[1].count, Is.EqualTo(1), "이질 슬롯은 자기 수량만 가져야 한다.");
        }

        // ════════════════════════════════════════════════════════════════════
        // 4. 재고 소모 — 정확 소모/부족 소모/빈 슬롯 정리
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void ConsumeStock_TakesExact_AvailableOnly_AndCleansEmptySlot()
        {
            VillageDrugState.AddStock(NationType.North, 2, Item("herb_stew", "허브스튜", 5, true, 6, 15));

            // 충분 수량 — 요청치 정확히 소모
            Assert.That(VillageDrugState.ConsumeStock(NationType.North, 2, "herb_stew", true, 6, 2),
                Is.EqualTo(2), "보유량이 충분하면 요청 수량을 정확히 소모해야 한다.");
            List<RestaurantStockItem> afterPartial = VillageDrugState.GetStock(NationType.North, 2);
            Assert.That(afterPartial, Has.Count.EqualTo(1));
            Assert.That(afterPartial[0].count, Is.EqualTo(3), "부분 소모 후 잔여 수량이 정확해야 한다.");

            // 부족 수량 — 실제 보유량(3)만 소모하고 반환
            Assert.That(VillageDrugState.ConsumeStock(NationType.North, 2, "herb_stew", true, 6, 10),
                Is.EqualTo(3), "요청이 보유량을 초과하면 실제 보유량만 소모하여 반환해야 한다.");

            // 0이 된 슬롯 정리 — 재조회 시 목록에서 사라진다
            Assert.That(VillageDrugState.GetStock(NationType.North, 2), Is.Empty,
                "수량 0이 된 슬롯은 정리되어 재조회 목록에 없어야 한다.");

            // 방어 규약 — 부재 키/0 이하 요청은 0 반환
            Assert.That(VillageDrugState.ConsumeStock(NationType.North, 2, "herb_stew", true, 6, 1),
                Is.EqualTo(0), "부재 슬롯 소모 시도는 0을 반환해야 한다.");
            Assert.That(VillageDrugState.ConsumeStock(NationType.North, 2, "herb_stew", true, 6, 0),
                Is.EqualTo(0), "0 이하 소모 요청은 0을 반환해야 한다.");
        }

        // ════════════════════════════════════════════════════════════════════
        // 5. 중독 클램프 — 0 하한 / 100 상한
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void AddictionClamp_NegativeGainFloorsAtZero_AndAccumulationCapsAt100()
        {
            // 음수 gain — 0 하한 클램프(미접촉 상태에서 음수 가산도 음수가 되지 않는다)
            Assert.That(VillageDrugState.AddAddiction(NationType.South, 7, -30f),
                Is.EqualTo(0f).Within(Tol), "음수 gain 후 중독도는 0 하한으로 클램프되어야 한다.");

            // 누적 — 60 + 70 = 130 → 100 상한 클램프
            Assert.That(VillageDrugState.AddAddiction(NationType.South, 7, 60f),
                Is.EqualTo(60f).Within(Tol), "중독 가산 결과가 반환되어야 한다.");
            Assert.That(VillageDrugState.AddAddiction(NationType.South, 7, 70f),
                Is.EqualTo(100f).Within(Tol), "누적 100 초과는 100으로 클램프되어야 한다.");

            // 저장소 값 동기 — 조회 메서드/스냅샷 모두 동일 값
            Assert.That(VillageDrugState.GetAddiction(NationType.South, 7),
                Is.EqualTo(100f).Within(Tol), "GetAddiction은 클램프된 누적값을 반환해야 한다.");
            Assert.That(VillageDrugState.GetSnapshot(NationType.South, 7).addiction,
                Is.EqualTo(100f).Within(Tol), "스냅샷 중독도는 클램프된 누적값과 일치해야 한다.");
        }

        // ════════════════════════════════════════════════════════════════════
        // 6. 등록 판매가 — Set → Get 왕복
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void RegisteredPrice_SetThenGet_RoundTrips()
        {
            Assert.That(VillageDrugState.GetRegisteredPrice(NationType.East, 11),
                Is.EqualTo(0), "미등록 마을의 등록가는 0이어야 한다.");

            VillageDrugState.SetRegisteredPrice(NationType.East, 11, 45);

            Assert.That(VillageDrugState.GetRegisteredPrice(NationType.East, 11),
                Is.EqualTo(45), "설정한 등록가가 조회된다.");
            Assert.That(VillageDrugState.GetSnapshot(NationType.East, 11).registeredPrice,
                Is.EqualTo(45), "스냅샷의 등록가도 설정값과 일치해야 한다.");
        }

        // ════════════════════════════════════════════════════════════════════
        // 7. 주민 이탈 — RecordDeparture 누적
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void Departures_RecordDeparture_AccumulatesPerCall()
        {
            VillageDrugState.RecordDeparture(NationType.West, 0);
            VillageDrugState.RecordDeparture(NationType.West, 0);
            VillageDrugState.RecordDeparture(NationType.West, 0);

            Assert.That(VillageDrugState.GetSnapshot(NationType.West, 0).departedVillagers, Is.EqualTo(3),
                "이탈 기록 3회는 departedVillagers 3으로 누적되어야 한다.");
        }

        // ════════════════════════════════════════════════════════════════════
        // 8. 상태 독립 — 다른 국가/다른 마을 인덱스 키 비간섭
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void Isolation_DifferentNationOrVillageIndex_KeepsIndependentState()
        {
            // (East, 0) / (West, 0) / (East, 1) 세 키에 서로 다른 상태 기입
            VillageDrugState.SetRestaurantBuilt(NationType.East, 0, true);
            VillageDrugState.AddAddiction(NationType.East, 0, 40f);
            VillageDrugState.SetRegisteredPrice(NationType.West, 0, 77);
            VillageDrugState.RecordDeparture(NationType.East, 1);

            VillageDrugSnapshot east0 = VillageDrugState.GetSnapshot(NationType.East, 0);
            Assert.That(east0.restaurantBuilt, Is.True, "East 0은 자기 상태(건설)를 유지해야 한다.");
            Assert.That(east0.addiction, Is.EqualTo(40f).Within(Tol), "East 0은 자기 중독도를 유지해야 한다.");
            Assert.That(east0.registeredPrice, Is.EqualTo(0), "East 0은 West 0의 등록가 영향을 받지 않아야 한다.");
            Assert.That(east0.departedVillagers, Is.EqualTo(0), "East 0은 East 1의 이탈 영향을 받지 않아야 한다.");

            VillageDrugSnapshot west0 = VillageDrugState.GetSnapshot(NationType.West, 0);
            Assert.That(west0.registeredPrice, Is.EqualTo(77), "West 0은 자기 등록가를 유지해야 한다.");
            Assert.That(west0.restaurantBuilt, Is.False, "West 0은 East 0의 건설 플래그 영향을 받지 않아야 한다.");
            Assert.That(west0.addiction, Is.EqualTo(0f).Within(Tol), "West 0은 East 0의 중독도 영향을 받지 않아야 한다.");

            VillageDrugSnapshot east1 = VillageDrugState.GetSnapshot(NationType.East, 1);
            Assert.That(east1.departedVillagers, Is.EqualTo(1), "East 1은 자기 이탈 누적을 유지해야 한다.");
            Assert.That(east1.restaurantBuilt, Is.False, "East 1은 East 0의 건설 플래그 영향을 받지 않아야 한다.");
            Assert.That(east1.addiction, Is.EqualTo(0f).Within(Tol), "East 1은 East 0의 중독도 영향을 받지 않아야 한다.");
            Assert.That(east1.assignedGuardId, Is.EqualTo(string.Empty),
                "East 1은 가드 미배치 상태를 유지해야 한다.");
        }
    }
}
