using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Core.Utils;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [2026-10-15 D3-B — 음식점 일일 판매 시스템] 계약 테스트 (RestaurantSystem.cs) — EditMode 가능 부.
    /// - RestockFromPlayer: 인벤 → 마을 재고 이동(일반/마약 구분, "[마약: stage n]" 마커 파싱,
    ///   정의 부재 시 10G 폴백), 수량 부족 실패 시 부분 소모 없음(인벤·재고 무변경).
    /// - RunDailySale: 음식점 미건설/병사 미배치 마을 스킵 / 정상 판매(재고 실소모 + 중독 상승 +
    ///   [88차] 판매금 pending 적립 — 즉시 지급 폐지, 수령은 CollectRevenue) / 동일 시드 재현(결정론).
    /// - RunAllDaily: 음식점 없는 월드에서 예외 없이 전체 순회 + 상태 생성 없음.
    ///
    /// ⚠ Play 게이트(EditMode 미실행 — 씬/오브젝트 의존):
    ///   · 발각 → 배치 병사 실체 처형(GuardManager 등록 목록 + Object.Destroy — GameObject 의존)
    ///   · 발각 → 적대 전환은 EditMode에서도 롤에 따라 발생할 수 있으나(TerritoryState만 터치),
    ///     확률 롤이라 단정 대신 상태 복원만 보장한다(TearDown).
    /// EditMode 규약: AddComponent는 Awake 미실행 → PlayerInventory/PlayerStats는 리플렉션 세팅
    /// (CraftBenchTests/AfterlifeSurrenderTests 75차 P18 패턴 준용).
    /// 실행 판정은 QA 서브에이전트 증거로 확정(구현 서브에이전트는 실행하지 않음).
    /// </summary>
    public sealed class RestaurantSystemTests
    {
        private const float Tol = 0.0001f;          // 중독도(float) 비교 허용 오차
        private const int TestSeed = 20261015;      // 결정론 시드 — 어서션은 시드값에 의존하지 않는 관계식

        private static readonly NationType TestNation = NationType.East;
        private const int TestVillage = 0;          // 기준 영지 = East_01 (villageIndex 0 ↔ 영지 1)

        private GameObject _invGo;
        private PlayerInventory _inv;
        private GameObject _statsGo;
        private TerritoryState _territory;
        private float _prevLoyalty;
        private bool _prevHostile;

        [SetUp]
        public void SetUp()
        {
            VillageDrugState.ResetAll();

            // 영지 상태 저장/복원 — 발각 시 적대 플래그/호감도가 변할 수 있어 테스트 후 원복
            _territory = TerritoryDatabase.Instance.GetState(TestNation, TestVillage + 1);
            _prevLoyalty = _territory.loyaltyToPlayer;
            _prevHostile = _territory.playerHostile;
            _territory.loyaltyToPlayer = 0f; // 발각 확률 최대 조건 — 판매 수학 어서션에는 영향 없음
            _territory.playerHostile = false;
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
            // 도메인 위생 — 리플렉션으로 세팅한 PlayerStats.Instance 원복 (AfterlifeSurrenderTests 패턴)
            typeof(PlayerStats).GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                ?.SetValue(null, null);

            // 영지 상태 원복 — 다른 테스트/런타임 오염 방지
            if (_territory != null)
            {
                _territory.loyaltyToPlayer = _prevLoyalty;
                _territory.playerHostile = _prevHostile;
            }
        }

        /// <summary>PlayerInventory EditMode 세팅 — AddComponent Awake 미실행 → Instance/슬롯 리플렉션 보장.</summary>
        private void EnsureInventory()
        {
            if (_inv != null) return;
            PlayerInventory.ResetInstance();
            _invGo = new GameObject("RestoTest_Inventory");
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

        /// <summary>PlayerStats EditMode 세팅 — 리플렉션 Instance 세팅(Awake 미실행, 75차 P18 패턴).</summary>
        private PlayerStats EnsureStats()
        {
            if (_statsGo == null)
            {
                _statsGo = new GameObject("RestoTest_Stats");
                var stats = _statsGo.AddComponent<PlayerStats>();
                typeof(PlayerStats).GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    ?.SetValue(null, stats); // Awake 미실행 — 리플렉션 세팅
            }
            return PlayerStats.Instance;
        }

        /// <summary>마을 재고 직접 입고 헬퍼 — 필드 나열 실수 방지.</summary>
        private static void Stock(string foodItemId, int count, bool isDrugged, int drugStage, int basePrice)
        {
            VillageDrugState.AddStock(TestNation, TestVillage, new RestaurantStockItem
            {
                foodItemId = foodItemId,
                displayName = foodItemId,
                count = count,
                isDrugged = isDrugged,
                drugStage = drugStage,
                basePrice = basePrice,
            });
        }

        /// <summary>음식점 개업 + 판매 담당 배치 헬퍼.</summary>
        private static void OpenRestaurant(string guardId)
        {
            VillageDrugState.SetRestaurantBuilt(TestNation, TestVillage, true);
            VillageDrugState.AssignGuard(TestNation, TestVillage, guardId);
        }

        /// <summary>재고 총수 집계 헬퍼.</summary>
        private static int TotalStock(bool isDrugged)
        {
            int total = 0;
            foreach (RestaurantStockItem slot in VillageDrugState.GetStock(TestNation, TestVillage))
                if (slot.isDrugged == isDrugged)
                    total += slot.count;
            return total;
        }

        private static void AssertDefaultReport(RestaurantSystem.SaleReport report)
        {
            Assert.That(report.soldNormal, Is.EqualTo(0), "스킵 마을 — 일반 판매 0.");
            Assert.That(report.soldDrugged, Is.EqualTo(0), "스킵 마을 — 마약 판매 0.");
            Assert.That(report.gold, Is.EqualTo(0), "스킵 마을 — 수입 0.");
            Assert.That(report.addictionGain, Is.EqualTo(0f).Within(Tol), "스킵 마을 — 중독 변화 없음.");
            Assert.That(report.guardExecuted, Is.False, "스킵 마을 — 처형 없음.");
            Assert.That(report.hostileConverted, Is.False, "스킵 마을 — 적대 전환 없음.");
        }

        // ════════════════════════════════════════════════════════════════════
        // 1. RestockFromPlayer — 인벤 → 마을 재고 이동
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void Restock_NormalFood_MovesInventoryToVillageStock()
        {
            EnsureInventory();
            var dish = new PlayerInventory.ItemData
            {
                id = "resto_test_dish",
                displayName = "고기구이",
                description = "맛있는 고기구이.",
                basePrice = 7, // O2 ItemData.basePrice 실측 — 정의 가격 사용
            };
            Assert.IsTrue(_inv.AddItem(dish, 3), "사전 조건: 인벤 입고");

            bool ok = RestaurantSystem.RestockFromPlayer(TestNation, TestVillage, "resto_test_dish", 3);

            Assert.That(ok, Is.True, "보유량 충분 시 입고 성공.");
            Assert.That(_inv.GetItemCount("resto_test_dish"), Is.EqualTo(0), "인벤에서 실제 제거.");

            List<RestaurantStockItem> stock = VillageDrugState.GetStock(TestNation, TestVillage);
            Assert.That(stock, Has.Count.EqualTo(1), "단일 슬롯 입고.");
            Assert.That(stock[0].foodItemId, Is.EqualTo("resto_test_dish"));
            Assert.That(stock[0].count, Is.EqualTo(3));
            Assert.That(stock[0].isDrugged, Is.False, "일반 음식 — drugged_ 접두 없음.");
            Assert.That(stock[0].drugStage, Is.EqualTo(0), "일반 음식 — 마약 단계 0.");
            Assert.That(stock[0].basePrice, Is.EqualTo(7), "정의 basePrice(O2)가 재고 기본가로 반영.");
        }

        [Test]
        public void Restock_DruggedFood_ParsesStageMarkerAndFallsBackPrice()
        {
            EnsureInventory();
            // CookingDrugHelper 규약(D3-A): itemId = "drugged_{dishId}", 설명 뒤 " [마약: stage n]" 부착
            var drugged = new PlayerInventory.ItemData
            {
                id = "drugged_resto_test_dish",
                displayName = "마약 뿌려진 고기구이",
                description = "맛있는 고기구이. [마약: stage 4]",
                basePrice = 0, // 조리 생성물 — 정적 정의 없음(무가격)
            };
            Assert.IsTrue(_inv.AddItem(drugged, 2), "사전 조건: 인벤 입고");

            bool ok = RestaurantSystem.RestockFromPlayer(TestNation, TestVillage, "drugged_resto_test_dish", 2);

            Assert.That(ok, Is.True);
            Assert.That(_inv.GetItemCount("drugged_resto_test_dish"), Is.EqualTo(0), "인벤에서 실제 제거.");

            List<RestaurantStockItem> stock = VillageDrugState.GetStock(TestNation, TestVillage);
            Assert.That(stock, Has.Count.EqualTo(1));
            Assert.That(stock[0].isDrugged, Is.True, "drugged_ 접두 → 마약 음식 판정.");
            Assert.That(stock[0].drugStage, Is.EqualTo(4), "설명 마커 [마약: stage 4] → 단계 4 파싱.");
            Assert.That(stock[0].basePrice, Is.EqualTo(RestaurantSystem.DefaultFoodBasePrice),
                "마약 음식/기반 요리 정의 부재 → 10G 폴백(보고 로그 동반).");
        }

        [Test]
        public void Restock_InsufficientCount_FailsWithoutPartialConsumption()
        {
            EnsureInventory();
            var dish = new PlayerInventory.ItemData { id = "resto_test_dish", displayName = "고기구이", basePrice = 7 };
            Assert.IsTrue(_inv.AddItem(dish, 2), "사전 조건: 2개만 보유");

            bool ok = RestaurantSystem.RestockFromPlayer(TestNation, TestVillage, "resto_test_dish", 5);

            Assert.That(ok, Is.False, "보유량 부족 → 실패.");
            Assert.That(_inv.GetItemCount("resto_test_dish"), Is.EqualTo(2),
                "RemoveItem의 부분 소모를 사전 검증으로 차단 — 인벤 무변경.");
            Assert.That(VillageDrugState.GetStock(TestNation, TestVillage), Is.Empty,
                "실패 시 마을 재고 무변경.");
        }

        [Test]
        public void Restock_NoInventoryInstance_Fails()
        {
            // EnsureInventory 미호출 — PlayerInventory.Instance null 상태
            bool ok = RestaurantSystem.RestockFromPlayer(TestNation, TestVillage, "resto_test_dish", 1);
            Assert.That(ok, Is.False, "인벤 미존재 → 실패(방어).");
            Assert.That(VillageDrugState.GetStock(TestNation, TestVillage), Is.Empty);
        }

        // ════════════════════════════════════════════════════════════════════
        // 2. RunDailySale — 스킵 규약
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void RunDailySale_NotBuiltVillage_Skips()
        {
            Stock("resto_test_dish", 20, false, 0, 10); // 재고만 있고 미건설

            RestaurantSystem.SaleReport report =
                RestaurantSystem.RunDailySale(TestNation, TestVillage, new System.Random(TestSeed));

            AssertDefaultReport(report);
            Assert.That(TotalStock(false), Is.EqualTo(20), "스킵 — 재고 무소모.");
        }

        [Test]
        public void RunDailySale_BuiltWithoutGuard_Skips()
        {
            VillageDrugState.SetRestaurantBuilt(TestNation, TestVillage, true); // 가드 미배치
            Stock("resto_test_dish", 20, false, 0, 10);

            RestaurantSystem.SaleReport report =
                RestaurantSystem.RunDailySale(TestNation, TestVillage, new System.Random(TestSeed));

            AssertDefaultReport(report);
            Assert.That(TotalStock(false), Is.EqualTo(20), "스킵 — 재고 무소모.");
        }

        [Test]
        public void RunDailySale_EmptyStock_Skips()
        {
            OpenRestaurant("G1"); // 개업 + 가드, 재고 없음

            RestaurantSystem.SaleReport report =
                RestaurantSystem.RunDailySale(TestNation, TestVillage, new System.Random(TestSeed));

            AssertDefaultReport(report);
        }

        // ════════════════════════════════════════════════════════════════════
        // 3. RunDailySale — 정상 판매 (소모/중독/골드/보고)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void RunDailySale_NormalOnly_ConsumesStockAndPaysBasePrice()
        {
            OpenRestaurant("G1");
            Stock("resto_test_dish", 20, false, 0, 10); // 기본가 10G
            VillageDrugState.SetRegisteredPrice(TestNation, TestVillage, 50); // 마약 없음 → 미적용
            PlayerStats stats = EnsureStats();
            int goldBefore = stats.Gold;

            RestaurantSystem.SaleReport report =
                RestaurantSystem.RunDailySale(TestNation, TestVillage, new System.Random(TestSeed));

            // 수요 규약: 일반 마을 주민 근사 8 × 기본 수요율 0.2 = 1.6 → round 2, 지터 ±1 → 1~3
            Assert.That(report.soldDrugged, Is.EqualTo(0), "마약 재고 없음 — 마약 판매 0.");
            Assert.That(report.soldNormal, Is.InRange(1, 3), "일반 판매 = 수요(주민 8 × 0.2 ± 지터 1).");
            Assert.That(report.gold, Is.EqualTo(report.soldNormal * 10), "골드 = 일반 판매 × 기본가.");
            // [88차] 판매금 적립 전환 — RunDailySale은 즉시 지급하지 않고 마을 pending에 적립한다.
            Assert.That(stats.Gold, Is.EqualTo(goldBefore),
                "[88차] 즉시 지급 폐지 — RunDailySale 시점 PlayerStats 잔액 불변.");
            VillageDrugState.GetPendingRevenue(TestNation, TestVillage, out int pendingDrug, out int pendingNormal);
            Assert.That(pendingDrug, Is.EqualTo(0), "[88차] 일반 판매만 — 마약 적립 0.");
            Assert.That(pendingNormal, Is.EqualTo(report.gold),
                "[88차] 일반 판매 수입이 마을 pending에 적립된다(판매금 받기 전까지).");
            Assert.That(TotalStock(false), Is.EqualTo(20 - report.soldNormal), "재고 실소모.");
            Assert.That(report.addictionGain, Is.EqualTo(0f).Within(Tol), "일반 음식 — 중독 불변.");
            Assert.That(VillageDrugState.GetAddiction(TestNation, TestVillage), Is.EqualTo(0f).Within(Tol));
            Assert.That(report.guardExecuted, Is.False, "마약 판매 없음 → 발각 경로 없음.");
            Assert.That(report.hostileConverted, Is.False);

            // 결정론 — 동일 시드 재주입 시 동일 수요 → 동일 판매(재고는 차감된 상태에서 같은 양 소모)
            int soldFirst = report.soldNormal;
            RestaurantSystem.SaleReport second =
                RestaurantSystem.RunDailySale(TestNation, TestVillage, new System.Random(TestSeed));
            Assert.That(second.soldNormal, Is.EqualTo(soldFirst), "동일 시드 → 동일 판매량(결정론 계약).");
        }

        [Test]
        public void RunDailySale_DruggedFirst_RaisesAddictionAndPaysRegisteredPrice()
        {
            OpenRestaurant("G1");
            Stock("resto_test_dish", 20, false, 0, 10);
            Stock("drugged_resto_test_dish", 5, true, 3, 10);
            // 등록가 = 권장가(중독 0 → 기본가 10) → 수요 계수 1.0 (과대 가격 패널트 회피 — 수학은 D2 계약)
            VillageDrugState.SetRegisteredPrice(TestNation, TestVillage, 10);
            PlayerStats stats = EnsureStats();
            int goldBefore = stats.Gold;

            // 기대 중독 등급 — 구현과 동일 폴백 규약(DrugDatabase 미로드 시 Low)
            AddictionLevel expectedLevel = DrugDatabase.GetByStage(3)?.Addiction ?? AddictionLevel.Low;

            RestaurantSystem.SaleReport report =
                RestaurantSystem.RunDailySale(TestNation, TestVillage, new System.Random(TestSeed));

            // 수요 1~3 < 마약 재고 5 — 마약 우선 규약으로 전 수요가 마약에서 소진
            Assert.That(report.soldDrugged, Is.InRange(1, 3), "마약 우선 소진(수요 = 주민 8 × 0.2 ± 지터 1).");
            Assert.That(report.soldNormal, Is.EqualTo(0), "수요가 마약 재고로 충족 — 일반 판매 없음.");
            Assert.That(report.gold, Is.EqualTo(report.soldDrugged * 10), "골드 = 마약 판매 × 등록가.");
            // [88차] 판매금 적립 전환 — 즉시 지급 폐지, 마약 판매 수입은 마약 pending에 적립.
            Assert.That(stats.Gold, Is.EqualTo(goldBefore),
                "[88차] 즉시 지급 폐지 — RunDailySale 시점 PlayerStats 잔액 불변.");
            VillageDrugState.GetPendingRevenue(TestNation, TestVillage, out int pendingDrug, out int pendingNormal);
            Assert.That(pendingDrug, Is.EqualTo(report.gold),
                "[88차] 마약 판매 수입이 마약 pending에 적립된다(판매금 받기 전까지).");
            Assert.That(pendingNormal, Is.EqualTo(0), "[88차] 마약 우선 소진 — 일반 적립 0.");

            float expectedGain = VillageDrugMath.AddictionGainPerSale(expectedLevel) * report.soldDrugged;
            Assert.That(report.addictionGain, Is.EqualTo(expectedGain).Within(Tol),
                "중독 상승 = 등급별 판매 1건당 상승량 × 실제 판매 수량.");
            Assert.That(VillageDrugState.GetAddiction(TestNation, TestVillage),
                Is.EqualTo(expectedGain).Within(Tol), "마을 중독도에 실가산.");

            Assert.That(TotalStock(true), Is.EqualTo(5 - report.soldDrugged), "마약 재고 실소모.");
            Assert.That(TotalStock(false), Is.EqualTo(20), "일반 재고 무소모(마약 우선).");
        }

        // ════════════════════════════════════════════════════════════════════
        // 4. [100차 P1] 일반 음식 등록가 — 판매가 반영 / 미설정 폴백 / 수요 계수 전달
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void RunDailySale_NormalRegisteredPriceSet_SellsAtRegisteredPrice100()
        {
            OpenRestaurant("G1");
            Stock("resto_test_dish", 20, false, 0, 10);
            // [100차] 일반 등록가 5G(<재고 basePrice 10G) — 가격 수요 계수 VisitorDemandFactor(5,10)=2.0
            VillageDrugState.SetNormalRegisteredPrice(TestNation, TestVillage, 5);
            PlayerStats stats = EnsureStats();
            int goldBefore = stats.Gold;

            RestaurantSystem.SaleReport report =
                RestaurantSystem.RunDailySale(TestNation, TestVillage, new System.Random(TestSeed));

            Assert.That(report.soldDrugged, Is.EqualTo(0), "마약 재고 없음 — 마약 판매 0.");
            // 기저 수요 = round(주민 8×0.2)=2 ±지터 1 → 1~3, ×계수 2.0(floor) → 2/4/6 (재고 20 내)
            Assert.That(report.soldNormal, Is.InRange(2, 6),
                "[100차] 가격 수요 계수 2.0이 DemandCount 기저에 곱해진다.");
            Assert.That(report.gold, Is.EqualTo(report.soldNormal * 5),
                "[100차] 골드 = 일반 판매 × 일반 등록가 5G(basePrice 10G 아님).");
            VillageDrugState.GetPendingRevenue(TestNation, TestVillage, out int pendingDrug, out int pendingNormal);
            Assert.That(pendingNormal, Is.EqualTo(report.gold), "[100차] 일반 등록가 기준 pending 적립.");
            Assert.That(pendingDrug, Is.EqualTo(0), "일반 판매만 — 마약 적립 0.");
            Assert.That(stats.Gold, Is.EqualTo(goldBefore), "[88차] 즉시 지급 폐지 계약 유지 — 잔액 불변.");
            Assert.That(TotalStock(false), Is.EqualTo(20 - report.soldNormal), "재고 실소모.");
        }

        [Test]
        public void RunDailySale_NormalRegisteredPriceUnset_UsesStockBasePrice100()
        {
            OpenRestaurant("G1");
            Stock("resto_test_dish", 20, false, 0, 7); // 일반 등록가 미설정(0) → basePrice 7G 폴백
            PlayerStats stats = EnsureStats();
            int goldBefore = stats.Gold;

            RestaurantSystem.SaleReport report =
                RestaurantSystem.RunDailySale(TestNation, TestVillage, new System.Random(TestSeed));

            // 등가(7G=7G) → 가격 계수 1.0 — 기저 수요 그대로(주민 8×0.2 ±지터)
            Assert.That(report.soldNormal, Is.InRange(1, 3), "미설정 시 가격 계수 1.0 — 기저 수요 유지.");
            Assert.That(report.gold, Is.EqualTo(report.soldNormal * 7),
                "[100차] 일반 등록가 미설정(0) → 재고 basePrice로 정산.");
            VillageDrugState.GetPendingRevenue(TestNation, TestVillage, out _, out int pendingNormal);
            Assert.That(pendingNormal, Is.EqualTo(report.gold), "[100차] 미설정 시 basePrice 기준 pending 적립.");
            Assert.That(stats.Gold, Is.EqualTo(goldBefore), "[88차] 잔액 불변 계약 유지.");
        }

        [Test]
        public void RunDailySale_NormalPriceFactor_DoublesDemandAtSameSeed100()
        {
            // 결정론 계수 전달 검증 — 동일 시드/동일 마을 조건 2곳(v0=등가 1.0 / v1=등록가 5G 계수 2.0).
            // v0·v1 모두 일반 마을(영지 1·2 — 촌락 링 밖) → 주민 근사 8 동일, DemandCount 기저 동일.
            const int secondVillage = 1;
            VillageDrugState.SetRestaurantBuilt(TestNation, TestVillage, true);
            VillageDrugState.AssignGuard(TestNation, TestVillage, "G1");
            VillageDrugState.AddStock(TestNation, TestVillage, new RestaurantStockItem
            {
                foodItemId = "resto_test_dish", displayName = "고기구이",
                count = 20, isDrugged = false, drugStage = 0, basePrice = 10,
            });
            VillageDrugState.SetRestaurantBuilt(TestNation, secondVillage, true);
            VillageDrugState.AssignGuard(TestNation, secondVillage, "G2");
            VillageDrugState.AddStock(TestNation, secondVillage, new RestaurantStockItem
            {
                foodItemId = "resto_test_dish", displayName = "고기구이",
                count = 20, isDrugged = false, drugStage = 0, basePrice = 10,
            });
            VillageDrugState.SetNormalRegisteredPrice(TestNation, secondVillage, 5); // 계수 2.0

            RestaurantSystem.SaleReport neutral =
                RestaurantSystem.RunDailySale(TestNation, TestVillage, new System.Random(TestSeed));
            RestaurantSystem.SaleReport boosted =
                RestaurantSystem.RunDailySale(TestNation, secondVillage, new System.Random(TestSeed));

            Assert.That(boosted.soldNormal, Is.EqualTo(2 * neutral.soldNormal),
                "[100차] 동일 기저 수요에 계수 2.0 → 판매 2배(floor(d×2.0), 재고 20 내) — "
                + "RunDailySale이 VisitorDemandFactor를 demandFactor 인자로 전달한다.");
            Assert.That(boosted.gold, Is.EqualTo(boosted.soldNormal * 5),
                "[100차] 부스트 마을 정산가 = 일반 등록가 5G.");
            Assert.That(neutral.gold, Is.EqualTo(neutral.soldNormal * 10),
                "[100차] 미설정 마을 정산가 = basePrice 10G.");
        }

        // ════════════════════════════════════════════════════════════════════
        // 5. RunAllDaily — 전체 순회 (음식점 없는 월드)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void RunAllDaily_NoRestaurants_NoStateAndNoThrow()
        {
            Assert.DoesNotThrow(() => RestaurantSystem.RunAllDaily(TestSeed),
                "음식점 없는 월드 — 전 마을 스킵, 예외 없음.");

            // 미접촉 마을은 상태 엔트리가 생성되지 않는다(기본값 스냅샷 규약 유지)
            VillageDrugSnapshot untouched = VillageDrugState.GetSnapshot(NationType.West, 7);
            Assert.That(untouched.restaurantBuilt, Is.False);
            Assert.That(untouched.assignedGuardId, Is.EqualTo(string.Empty));
            Assert.That(untouched.addiction, Is.EqualTo(0f).Within(Tol));
        }
    }
}
