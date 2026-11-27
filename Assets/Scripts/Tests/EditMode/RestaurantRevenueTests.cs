using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using ProjectName.Core;         // PlayerStats/PlayerInventory — 싱글턴 리플렉션 세팅 대상
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [88차 A6 — 판매금 적립/수령 + 재고 누적/슬롯 상한] 계약 테스트 — EditMode 가능 부.
    /// - RunDailySale 적립 전환 소스 어서션: RunDailySale 본문에서 PlayerStats 즉시 지급 코드 부재 +
    ///   AddPendingRevenue 호출(스코프 추출 판정 — CollectRevenue의 수령 경로 AddGold와 구분).
    /// - TakePendingRevenue 전이/리셋, AddPendingRevenue 음수 방어, 미접촉 마을 0 반환(상태 생성 없음).
    /// - AddStock → placedTotal 누적(신규/병합), ConsumeStock → soldTotal 누적(실소모 기준).
    /// - 슬롯 상한 6(MaxStockSlots) — 신규 키 도달 시 거부(인벤 소비 없음), 병합은 항상 허용.
    /// - CollectRevenue — TakePendingRevenue → PlayerStats.AddGold(원장 태그 유지) 소스/동작 어서션,
    ///   PlayerStats 미존재 시 소비 없이 0(적립 유지).
    ///
    /// EditMode 규약: AddComponent는 Awake 미실행 → PlayerInventory/PlayerStats는 리플렉션 세팅
    /// (RestaurantSystemTests/75차 P18 패턴 준용). 실행 판정은 QA 서브에이전트 증거로 확정
    /// (구현 서브에이전트는 실행하지 않음).
    /// </summary>
    public sealed class RestaurantRevenueTests
    {
        private static readonly NationType TestNation = NationType.East;
        private const int TestVillage = 0; // 기준 영지 = East_01 (villageIndex 0 ↔ 영지 1)

        private GameObject _invGo;
        private PlayerInventory _inv;
        private GameObject _statsGo;

        [SetUp]
        public void SetUp()
        {
            VillageDrugState.ResetAll();

            // 싱글턴 클리어 — 결정론 보장(RestaurantWindowTests SetUp 리플렉션 클리어 선례 패턴)
            typeof(PlayerStats).GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                ?.SetValue(null, null);
            typeof(GuardManager).GetProperty("Instance")?.SetValue(null, null);
        }

        [TearDown]
        public void TearDown()
        {
            VillageDrugState.ResetAll();

            if (_invGo != null)
            {
                PlayerInventory.ResetInstance(); // GO 파괴 + Instance 해제(ResetInstance 계약)
                _invGo = null;
                _inv = null;
            }

            if (_statsGo != null)
            {
                Object.DestroyImmediate(_statsGo);
                _statsGo = null;
            }
            // 도메인 위생 — 리플렉션으로 세팅한 싱글턴 원복(RestaurantSystemTests TearDown 패턴)
            typeof(PlayerStats).GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                ?.SetValue(null, null);
            typeof(GuardManager).GetProperty("Instance")?.SetValue(null, null);
        }

        /// <summary>PlayerStats EditMode 세팅 — 리플렉션 Instance 세팅(Awake 미실행, 75차 P18 패턴).</summary>
        private PlayerStats EnsureStats()
        {
            if (_statsGo == null)
            {
                _statsGo = new GameObject("RevenueTest_Stats");
                var stats = _statsGo.AddComponent<PlayerStats>();
                typeof(PlayerStats).GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    ?.SetValue(null, stats); // Awake 미실행 — 리플렉션 세팅
            }
            return PlayerStats.Instance;
        }

        /// <summary>PlayerInventory EditMode 세팅 — RestaurantSystemTests.EnsureInventory 패턴 준용.</summary>
        private void EnsureInventory()
        {
            if (_inv != null) return;
            PlayerInventory.ResetInstance();
            _invGo = new GameObject("RevenueTest_Inventory");
            _inv = _invGo.AddComponent<PlayerInventory>();
            var instProp = typeof(PlayerInventory).GetProperty("Instance");
            if (instProp != null && instProp.GetValue(null) == null)
                instProp.SetValue(null, _inv);
            if (_inv.GetAllSlots() == null)
            {
                var slotsField = typeof(PlayerInventory).GetField("_slots",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var maxField = typeof(PlayerInventory).GetField("_maxSlots",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                int max = maxField != null ? (int)maxField.GetValue(_inv) : 30;
                slotsField.SetValue(_inv, new PlayerInventory.ItemSlot[max]);
            }
        }

        /// <summary>인벤 입고 헬퍼 — basePrice 10 실측값(폴백 경고 회피).</summary>
        private void AddInvItem(string itemId, int count)
        {
            var item = new PlayerInventory.ItemData
            {
                id = itemId,
                displayName = itemId,
                description = "테스트용 음식.",
                basePrice = 10,
            };
            Assert.IsTrue(_inv.AddItem(item, count), $"사전 조건: 인벤 입고 {itemId} ×{count}");
        }

        // ════════════════════════════════════════════════════════════════════
        // 1. RunDailySale 적립 전환 — 소스 어서션 (스코프 추출)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void RunDailySale_Source_AccruesPending_NoImmediateGold()
        {
            string src = File.ReadAllText("Assets/Scripts/Systems/RestaurantSystem.cs");

            Assert.That(src, Does.Contain("[88차]"), "[88차] 진실치 마커 주석 존재.");

            // RunDailySale 본문만 스코프 추출 — CollectRevenue(수령 경로)의 AddGold와 구분
            int start = src.IndexOf("public static SaleReport RunDailySale");
            int end = src.IndexOf("public static void RunAllDaily");
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "RunDailySale 시그니처 앵커 존재.");
            Assert.That(end, Is.GreaterThan(start), "RunAllDaily 앵커 존재 — 본문 범위 추출 가능.");
            string body = src.Substring(start, end - start);

            Assert.That(body, Does.Not.Contain("AddGold("),
                "[88차] RunDailySale — PlayerStats 즉시 지급 코드 부재(적립 전환 계약).");
            Assert.That(body, Does.Contain("AddPendingRevenue(nation, villageIndex, drugGold, normalGold)"),
                "[88차] RunDailySale — 판매 수입을 VillageDrugState.AddPendingRevenue로 적립.");
        }

        [Test]
        public void CollectRevenue_Source_LedgerTagsPreserved()
        {
            string src = File.ReadAllText("Assets/Scripts/Systems/RestaurantSystem.cs");

            Assert.That(src, Does.Contain("public static int CollectRevenue(NationType nation, int villageIndex)"),
                "[88차] CollectRevenue 신설 — 판매금 받기 진입점.");
            Assert.That(src, Does.Contain("TakePendingRevenue(nation, villageIndex)"),
                "수령 경로 — TakePendingRevenue(합산 반환 후 0 리셋) 사용.");
            Assert.That(src, Does.Contain("stats.AddGold(pending.drugGold, GoldSourceDrug)"),
                "마약 수령 — 원장 태그 GoldSourceDrug 유지.");
            Assert.That(src, Does.Contain("stats.AddGold(pending.normalGold, GoldSourceFood)"),
                "일반 수령 — 원장 태그 GoldSourceFood 유지.");
        }

        // ════════════════════════════════════════════════════════════════════
        // 2. VillageDrugState — pending 적립 전이/리셋 (상태 API 직접)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void PendingRevenue_AddGetTake_TransitionsAndResets()
        {
            VillageDrugState.AddPendingRevenue(TestNation, TestVillage, 30, 12);
            VillageDrugState.GetPendingRevenue(TestNation, TestVillage, out int drugAfterAdd, out int normalAfterAdd);
            Assert.That(drugAfterAdd, Is.EqualTo(30), "마약 적립 조회.");
            Assert.That(normalAfterAdd, Is.EqualTo(12), "일반 적립 조회.");

            VillageDrugState.AddPendingRevenue(TestNation, TestVillage, 5, 0);
            VillageDrugState.GetPendingRevenue(TestNation, TestVillage, out int drugAfterMore, out int normalAfterMore);
            Assert.That(drugAfterMore, Is.EqualTo(35), "적립 누적(마약).");
            Assert.That(normalAfterMore, Is.EqualTo(12), "적립 누적(일반 — 기존 보존).");

            (int drugGold, int normalGold) taken = VillageDrugState.TakePendingRevenue(TestNation, TestVillage);
            Assert.That(taken.drugGold, Is.EqualTo(35), "인출 반환 — 마약 적립 합산.");
            Assert.That(taken.normalGold, Is.EqualTo(12), "인출 반환 — 일반 적립 합산.");

            VillageDrugState.GetPendingRevenue(TestNation, TestVillage, out int drugAfterTake, out int normalAfterTake);
            Assert.That(drugAfterTake, Is.EqualTo(0), "인출 후 마약 적립 0 리셋.");
            Assert.That(normalAfterTake, Is.EqualTo(0), "인출 후 일반 적립 0 리셋.");

            (int drugGold, int normalGold) second = VillageDrugState.TakePendingRevenue(TestNation, TestVillage);
            Assert.That(second.drugGold, Is.EqualTo(0), "재인출 — 0(결정론).");
            Assert.That(second.normalGold, Is.EqualTo(0), "재인출 — 0(결정론).");
        }

        [Test]
        public void PendingRevenue_NegativeInputs_AreIgnored()
        {
            VillageDrugState.AddPendingRevenue(TestNation, TestVillage, -5, -3);
            VillageDrugState.GetPendingRevenue(TestNation, TestVillage, out int drug, out int normal);
            Assert.That(drug, Is.EqualTo(0), "음수 적립 무시(방어 — 결정론 유지).");
            Assert.That(normal, Is.EqualTo(0), "음수 적립 무시(방어 — 결정론 유지).");
        }

        [Test]
        public void PendingRevenue_UnknownVillage_ZeroWithoutStateCreation()
        {
            VillageDrugState.GetPendingRevenue(NationType.South, 11, out int drug, out int normal);
            Assert.That(drug, Is.EqualTo(0), "미접촉 마을 — 마약 적립 0.");
            Assert.That(normal, Is.EqualTo(0), "미접촉 마을 — 일반 적립 0.");

            (int drugGold, int normalGold) taken = VillageDrugState.TakePendingRevenue(NationType.South, 11);
            Assert.That(taken.drugGold, Is.EqualTo(0), "미접촉 마을 인출 — 0.");
            Assert.That(taken.normalGold, Is.EqualTo(0), "미접촉 마을 인출 — 0.");

            // 조회/인출은 상태 엔트리를 생성하지 않는다(기본값 스냅샷 규약 유지)
            VillageDrugSnapshot snapshot = VillageDrugState.GetSnapshot(NationType.South, 11);
            Assert.That(snapshot.restaurantBuilt, Is.False, "읽기 경로 — 상태 생성 없음.");
        }

        // ════════════════════════════════════════════════════════════════════
        // 3. 재고 누적 — placedTotal(입고) / soldTotal(판매)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void AddStock_AccumulatesPlacedTotal_OnNewAndMerge()
        {
            VillageDrugState.AddStock(TestNation, TestVillage, new RestaurantStockItem
            {
                foodItemId = "meat_grilled", displayName = "고기구이", count = 5, isDrugged = false, drugStage = 0, basePrice = 10,
            });
            List<RestaurantStockItem> afterNew = VillageDrugState.GetStock(TestNation, TestVillage);
            Assert.That(afterNew, Has.Count.EqualTo(1));
            Assert.That(afterNew[0].placedTotal, Is.EqualTo(5), "[88차] 신규 슬롯 — 배치 누적 = 최초 입고 수량.");
            Assert.That(afterNew[0].soldTotal, Is.EqualTo(0), "판매 전 — 판매 누적 0.");

            // 동일 키 병합 — 기존 누적 보존 + 가산
            VillageDrugState.AddStock(TestNation, TestVillage, new RestaurantStockItem
            {
                foodItemId = "meat_grilled", displayName = "고기구이", count = 3, isDrugged = false, drugStage = 0, basePrice = 10,
            });
            List<RestaurantStockItem> afterMerge = VillageDrugState.GetStock(TestNation, TestVillage);
            Assert.That(afterMerge, Has.Count.EqualTo(1), "동일 키 — 병합(슬롯 분리 없음).");
            Assert.That(afterMerge[0].count, Is.EqualTo(8), "병합 수량 합산.");
            Assert.That(afterMerge[0].placedTotal, Is.EqualTo(8), "[88차] 병합 입고도 배치 누적 가산(5+3).");

            // 이질 키 — 별도 슬롯 누적 분리
            VillageDrugState.AddStock(TestNation, TestVillage, new RestaurantStockItem
            {
                foodItemId = "meat_grilled", displayName = "마약 뿌려진 고기구이", count = 2, isDrugged = true, drugStage = 3, basePrice = 10,
            });
            List<RestaurantStockItem> afterSecond = VillageDrugState.GetStock(TestNation, TestVillage);
            Assert.That(afterSecond, Has.Count.EqualTo(2), "이질 키 — 분리 보관.");
            Assert.That(afterSecond[1].placedTotal, Is.EqualTo(2), "이질 슬롯 — 자체 배치 누적.");
        }

        [Test]
        public void ConsumeStock_AccumulatesSoldTotal()
        {
            VillageDrugState.AddStock(TestNation, TestVillage, new RestaurantStockItem
            {
                foodItemId = "meat_grilled", displayName = "고기구이", count = 5, isDrugged = false, drugStage = 0, basePrice = 10,
            });

            int first = VillageDrugState.ConsumeStock(TestNation, TestVillage, "meat_grilled", false, 0, 2);
            Assert.That(first, Is.EqualTo(2), "전제: 부분 소모.");
            List<RestaurantStockItem> afterPartial = VillageDrugState.GetStock(TestNation, TestVillage);
            Assert.That(afterPartial[0].soldTotal, Is.EqualTo(2), "[88차] 실소모 수만큼 판매 누적.");
            Assert.That(afterPartial[0].count, Is.EqualTo(3), "재고 차감.");

            int second = VillageDrugState.ConsumeStock(TestNation, TestVillage, "meat_grilled", false, 0, 10);
            Assert.That(second, Is.EqualTo(3), "부족 요청 — 보유량만 소모.");
            Assert.That(VillageDrugState.GetStock(TestNation, TestVillage), Is.Empty,
                "0이 된 슬롯 정리 — 기존 규약 유지.");
        }

        // ════════════════════════════════════════════════════════════════════
        // 4. 슬롯 상한 6 — 신규 키 거부 / 병합 허용 (RestockFromPlayer)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void MaxStockSlots_Constant_Is6()
        {
            Assert.That(RestaurantSystem.MaxStockSlots, Is.EqualTo(6),
                "[88차] 슬롯 상한 상수 = 6 — Figma \"N / 6\" 계약.");
        }

        [Test]
        public void Restock_SlotLimit_BlocksNewKeyAtLimit_AllowsMerge()
        {
            EnsureInventory();

            // 6개 이기 키 입고 — 각 신규 슬롯 생성
            for (int i = 0; i < RestaurantSystem.MaxStockSlots; i++)
            {
                string itemId = $"resto_slot_{i}";
                AddInvItem(itemId, 2);
                Assert.IsTrue(RestaurantSystem.RestockFromPlayer(TestNation, TestVillage, itemId, 1),
                    $"[{i}] 상한 이내 신규 키 입고 성공.");
            }
            Assert.That(VillageDrugState.GetStock(TestNation, TestVillage),
                Has.Count.EqualTo(RestaurantSystem.MaxStockSlots), "전제: 6슬롯 도달.");

            // 7번째 이기 키 — 거부 + 인벤 무변경(벤치 원칙: 실패 시 소비 없음)
            AddInvItem("resto_slot_6", 2);
            bool blocked = RestaurantSystem.RestockFromPlayer(TestNation, TestVillage, "resto_slot_6", 1);
            Assert.That(blocked, Is.False, "[88차] 슬롯 상한 도달 — 신규 키 입고 거부.");
            Assert.That(_inv.GetItemCount("resto_slot_6"), Is.EqualTo(2),
                "거부 시 인벤 소비 없음.");
            Assert.That(VillageDrugState.GetStock(TestNation, TestVillage),
                Has.Count.EqualTo(RestaurantSystem.MaxStockSlots), "거부 시 슬롯 수 불변.");

            // 병합 입고 — 상한 도달 상태에서 기존 키는 항상 허용
            // [88차 수리] 병합 요청량(2) 충족 보충 — 루프 잔여 1개 + 보충 1개 = 2
            // (부족 시 RestaurantSystem "인벤 수량 부족" 경로로 거부되어 슬롯 상한 검증 불가)
            AddInvItem("resto_slot_0", 1);
            Assert.That(_inv.GetItemCount("resto_slot_0"), Is.EqualTo(2), "전제: 병합 요청량 인벤 충족.");
            bool merged = RestaurantSystem.RestockFromPlayer(TestNation, TestVillage, "resto_slot_0", 2);
            Assert.That(merged, Is.True, "[88차] 병합 키 입고 — 상한 무관 항상 허용.");
            List<RestaurantStockItem> stock = VillageDrugState.GetStock(TestNation, TestVillage);
            Assert.That(stock, Has.Count.EqualTo(RestaurantSystem.MaxStockSlots), "병합 — 슬롯 수 불변.");
            foreach (RestaurantStockItem slot in stock)
            {
                if (slot.foodItemId == "resto_slot_0")
                {
                    Assert.That(slot.count, Is.EqualTo(3), "병합 수량 합산(1+2).");
                    Assert.That(slot.placedTotal, Is.EqualTo(3), "[88차] 병합 입고 배치 누적 보존/가산.");
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // 5. CollectRevenue — 수령 동작 (pending → PlayerStats 원장 태그)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void CollectRevenue_PaysPending_AndResets()
        {
            PlayerStats stats = EnsureStats();
            int goldBefore = stats.Gold;

            VillageDrugState.AddPendingRevenue(TestNation, TestVillage, 30, 12);
            int total = RestaurantSystem.CollectRevenue(TestNation, TestVillage);

            Assert.That(total, Is.EqualTo(42), "수령 총액 = 마약+일반 합산.");
            Assert.That(stats.Gold, Is.EqualTo(goldBefore + 42), "PlayerStats 잔액 대사 — 수령액 가산.");
            VillageDrugState.GetPendingRevenue(TestNation, TestVillage, out int drugAfter, out int normalAfter);
            Assert.That(drugAfter, Is.EqualTo(0), "수령 후 마약 적립 리셋.");
            Assert.That(normalAfter, Is.EqualTo(0), "수령 후 일반 적립 리셋.");

            Assert.That(RestaurantSystem.CollectRevenue(TestNation, TestVillage), Is.EqualTo(0),
                "재수령 — 적립 0(음수/0 안전).");
            Assert.That(stats.Gold, Is.EqualTo(goldBefore + 42), "재수령 무지급.");
        }

        [Test]
        public void CollectRevenue_EmptyPending_ReturnsZero_WithoutGoldChange()
        {
            PlayerStats stats = EnsureStats();
            int goldBefore = stats.Gold;

            int total = RestaurantSystem.CollectRevenue(TestNation, TestVillage);

            Assert.That(total, Is.EqualTo(0), "적립 없음 — 수령 0.");
            Assert.That(stats.Gold, Is.EqualTo(goldBefore), "수령액 0 — 잔액 불변.");
        }

        [Test]
        public void CollectRevenue_NoPlayerStats_ReturnsZero_AndKeepsPending()
        {
            // SetUp에서 PlayerStats.Instance = null 보장 — EnsureStats 미호출
            VillageDrugState.AddPendingRevenue(TestNation, TestVillage, 30, 12);

            int total = RestaurantSystem.CollectRevenue(TestNation, TestVillage);

            Assert.That(total, Is.EqualTo(0), "PlayerStats 미존재 — 수령 실패(0).");
            VillageDrugState.GetPendingRevenue(TestNation, TestVillage, out int drug, out int normal);
            Assert.That(drug, Is.EqualTo(30), "소비 없이 실패 — 마약 적립 유지(원복 계약).");
            Assert.That(normal, Is.EqualTo(12), "소비 없이 실패 — 일반 적립 유지(원복 계약).");
        }
    }
}
