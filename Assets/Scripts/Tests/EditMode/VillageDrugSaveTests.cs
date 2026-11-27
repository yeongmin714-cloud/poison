using System.Collections.Generic;
using NUnit.Framework;
using ProjectName.Core.Data; // NationType/RestaurantStockItem/VillageDrugSaveEntry (TerritoryData.cs/VillageDrugState.cs)

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [2026-10-16 D5-A — 마을 음식점/마약 세이브] 계약 테스트 (VillageDrugState.cs 수출/수입).
    /// - 라운드트립: Export→(상태 파괴)→Import 후 전 필드 동일(건설/가드/중독/등록가/이탈 누적/재고 2종 —
    ///   일반 음식 슬롯 + 마약 음식 슬롯, 슬롯 순서와 필드 1:1 보존).
    /// - [88차] 신규 필드 라운드트립: pendingDrugGold/pendingNormalGold(판매금 적립) +
    ///   placedTotal/soldTotal(재고 누적 집계) 1:1 보존.
    /// - 스킵 규약: 재고 없는 마을은 수출에서 제외(용량 절감) — 건설 플래그만 있어도 제외 대상.
    ///   [88차] 확장: pending 적립금이 있으면 재고가 비어도 수출 대상(적립금 유실 방지).
    /// - Import null: 구버전 세이브(필드 부재) → 기존 상태 전면 클리어만 수행(예외 없음).
    /// - 결정론: Export는 사전 내부 순서와 무관하게 (국가, 마을 인덱스) 오름차순으로 수출하며,
    ///   같은 상태에 대한 2회 수출 결과가 필드 단위로 완전히 동일하다(수출이 상태를 비파괴).
    /// 실행 판정은 QA 서브에이전트 증거로 확정(구현 서브에이전트는 실행하지 않음).
    /// </summary>
    public sealed class VillageDrugSaveTests
    {
        private const float Tol = 0.0001f; // 중독도(float) 비교 허용 오차

        [SetUp]
        public void SetUp()
        {
            // 테스트 독립성 — static 저장소이므로 매 테스트 전 전체 초기화(VillageDrugState 테스트 전용 계약)
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
        // 1. Export→Import 라운드트립 — 전 필드 보존 (건설/가드/중독/등록가/이탈/재고 2종)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void ExportImport_RoundTrip_PreservesAllFields()
        {
            // 상태 구축 — 마약 경제의 전 상태 축을 한 마을에 모은다
            VillageDrugState.SetRestaurantBuilt(NationType.East, 7, true);
            VillageDrugState.AssignGuard(NationType.East, 7, "G1234");
            VillageDrugState.AddAddiction(NationType.East, 7, 37.5f);
            VillageDrugState.SetRegisteredPrice(NationType.East, 7, 120);
            VillageDrugState.RecordDeparture(NationType.East, 7);
            VillageDrugState.RecordDeparture(NationType.East, 7); // 이탈 누적 2명
            VillageDrugState.AddStock(NationType.East, 7, Item("meat_grilled", "고기구이", 5, false, 0, 30));
            VillageDrugState.AddStock(NationType.East, 7, Item("meat_grilled", "마약 뿌려진 고기구이", 3, true, 4, 30));

            List<VillageDrugSaveEntry> exported = VillageDrugState.ExportSaveEntries();
            Assert.That(exported, Has.Count.EqualTo(1), "재고 있는 마을 1곳만 수출되어야 한다.");
            VillageDrugSaveEntry save = exported[0];
            Assert.That(save.nation, Is.EqualTo((int)NationType.East), "수출 nation은 enum 정수여야 한다.");
            Assert.That(save.villageIndex, Is.EqualTo(7), "수출 villageIndex 보존.");

            // 상태 파괴 후 복원 — 라운드트립은 기존 상태와 무관해야 한다
            VillageDrugState.ResetAll();
            VillageDrugState.ImportSaveEntries(exported);

            VillageDrugSnapshot snap = VillageDrugState.GetSnapshot(NationType.East, 7);
            Assert.That(snap.restaurantBuilt, Is.True, "건설 플래그 복원.");
            Assert.That(snap.assignedGuardId, Is.EqualTo("G1234"), "가드 ID 복원.");
            Assert.That(snap.addiction, Is.EqualTo(37.5f).Within(Tol), "중독도 복원.");
            Assert.That(snap.registeredPrice, Is.EqualTo(120), "등록 판매가 복원.");
            Assert.That(snap.departedVillagers, Is.EqualTo(2), "이탈 주민 누적 복원.");

            List<RestaurantStockItem> stock = VillageDrugState.GetStock(NationType.East, 7);
            Assert.That(stock, Has.Count.EqualTo(2), "재고 2종(일반+마약) 복원.");

            // 슬롯 1 — 일반 음식 (입고 순서 유지)
            Assert.That(stock[0].foodItemId, Is.EqualTo("meat_grilled"));
            Assert.That(stock[0].displayName, Is.EqualTo("고기구이"));
            Assert.That(stock[0].count, Is.EqualTo(5));
            Assert.That(stock[0].isDrugged, Is.False);
            Assert.That(stock[0].drugStage, Is.EqualTo(0));
            Assert.That(stock[0].basePrice, Is.EqualTo(30));

            // 슬롯 2 — 마약 음식
            Assert.That(stock[1].foodItemId, Is.EqualTo("meat_grilled"));
            Assert.That(stock[1].displayName, Is.EqualTo("마약 뿌려진 고기구이"));
            Assert.That(stock[1].count, Is.EqualTo(3));
            Assert.That(stock[1].isDrugged, Is.True);
            Assert.That(stock[1].drugStage, Is.EqualTo(4));
            Assert.That(stock[1].basePrice, Is.EqualTo(30));
        }

        // ════════════════════════════════════════════════════════════════════
        // 2. 스킵 규약 — 재고 없는 마을은 수출에서 제외 (용량 절감)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void ExportImport_RoundTrip_Preserves88PendingAndTotals()
        {
            // [88차] 신규 필드 구축 — 판매금 적립(pending) + 재고 누적(placedTotal/soldTotal)
            VillageDrugState.SetRestaurantBuilt(NationType.East, 7, true);
            VillageDrugState.AddStock(NationType.East, 7, Item("meat_grilled", "고기구이", 5, false, 0, 30));
            VillageDrugState.ConsumeStock(NationType.East, 7, "meat_grilled", false, 0, 2); // soldTotal += 2, count 5→3
            VillageDrugState.AddPendingRevenue(NationType.East, 7, 50, 25);

            List<VillageDrugSaveEntry> exported = VillageDrugState.ExportSaveEntries();
            Assert.That(exported, Has.Count.EqualTo(1), "재고 보유 마을 수출.");
            VillageDrugSaveEntry save = exported[0];
            Assert.That(save.pendingDrugGold, Is.EqualTo(50), "[88차] 수출 — 마약 판매금 적립 1:1.");
            Assert.That(save.pendingNormalGold, Is.EqualTo(25), "[88차] 수출 — 일반 판매금 적립 1:1.");
            Assert.That(save.stock[0].count, Is.EqualTo(3), "수출 — 소모 반영 재고 수량.");
            Assert.That(save.stock[0].placedTotal, Is.EqualTo(5), "[88차] 수출 — 배치 누적 1:1.");
            Assert.That(save.stock[0].soldTotal, Is.EqualTo(2), "[88차] 수출 — 판매 누적 1:1.");

            // 상태 파괴 후 복원 — 라운드트립
            VillageDrugState.ResetAll();
            VillageDrugState.ImportSaveEntries(exported);

            VillageDrugState.GetPendingRevenue(NationType.East, 7, out int drugRestored, out int normalRestored);
            Assert.That(drugRestored, Is.EqualTo(50), "[88차] 복원 — 마약 판매금 적립 보존.");
            Assert.That(normalRestored, Is.EqualTo(25), "[88차] 복원 — 일반 판매금 적립 보존.");

            List<RestaurantStockItem> stock = VillageDrugState.GetStock(NationType.East, 7);
            Assert.That(stock, Has.Count.EqualTo(1));
            Assert.That(stock[0].count, Is.EqualTo(3), "복원 — 재고 수량.");
            Assert.That(stock[0].placedTotal, Is.EqualTo(5), "[88차] 복원 — 배치 누적 보존.");
            Assert.That(stock[0].soldTotal, Is.EqualTo(2), "[88차] 복원 — 판매 누적 보존.");
        }

        [Test]
        public void Export_PendingOnlyVillage_IsExported88()
        {
            // [88차] 스킵 규약 확장 — pending 적립금이 있으면 재고가 비어도 수출 대상(유실 방지)
            VillageDrugState.AddPendingRevenue(NationType.West, 2, 77, 0);

            List<VillageDrugSaveEntry> exported = VillageDrugState.ExportSaveEntries();
            Assert.That(exported, Has.Count.EqualTo(1),
                "[88차] pending 적립금 보유 마을 — 재고 없어도 수출 대상.");
            Assert.That(exported[0].pendingDrugGold, Is.EqualTo(77), "수출 — 적립금 보존.");
            Assert.That(exported[0].villageIndex, Is.EqualTo(2), "수출 — 마을 키 보존.");

            VillageDrugState.ResetAll();
            VillageDrugState.ImportSaveEntries(exported);
            VillageDrugState.GetPendingRevenue(NationType.West, 2, out int drug, out int normal);
            Assert.That(drug, Is.EqualTo(77), "복원 — 적립금 무결.");
            Assert.That(normal, Is.EqualTo(0), "복원 — 미적립 축 0.");
        }

        [Test]
        public void ExportImport_NormalRegisteredPrice_RoundTrip100()
        {
            // [100차] 일반 음식 등록가 — 수출/수입 1:1 + 등록가 단독 마을(재고·적립 0) 유실 방지 수출
            VillageDrugState.SetNormalRegisteredPrice(NationType.East, 7, 45);

            List<VillageDrugSaveEntry> exported = VillageDrugState.ExportSaveEntries();
            Assert.That(exported, Has.Count.EqualTo(1),
                "[100차] 등록가만 있어도 수출 대상 — [88차] pending 유실 방지 규약 확장 준용.");
            Assert.That(exported[0].normalRegisteredPrice, Is.EqualTo(45), "[100차] 수출 — 일반 등록가 1:1.");
            Assert.That(exported[0].registeredPrice, Is.EqualTo(0), "마약 등록가 축 무변경.");

            VillageDrugState.ResetAll();
            VillageDrugState.ImportSaveEntries(exported);
            VillageDrugSnapshot snap = VillageDrugState.GetSnapshot(NationType.East, 7);
            Assert.That(snap.normalRegisteredPrice, Is.EqualTo(45), "[100차] 복원 — 일반 등록가 보존.");

            // 구세이브(필드 누락) 호환 — DTO에 0이면 스냅샷도 0(미설정 → 재고 basePrice 사용)
            VillageDrugState.ResetAll();
            VillageDrugState.ImportSaveEntries(new List<VillageDrugSaveEntry>
            {
                new VillageDrugSaveEntry { nation = (int)NationType.East, villageIndex = 7 },
            });
            VillageDrugSnapshot legacy = VillageDrugState.GetSnapshot(NationType.East, 7);
            Assert.That(legacy.normalRegisteredPrice, Is.EqualTo(0),
                "[100차] 구세이브 누락 → 0 호환(미설정 규약).");
        }

        [Test]
        public void Export_VillageWithoutStock_IsSkipped()
        {
            // 재고 없는 마을 — 건설/가드/중독만 있어도 스킵 대상
            VillageDrugState.SetRestaurantBuilt(NationType.West, 1, true);
            VillageDrugState.AssignGuard(NationType.West, 1, "G9");
            VillageDrugState.AddAddiction(NationType.West, 1, 10f);
            // 재고 있는 마을 — 수출 대상
            VillageDrugState.AddStock(NationType.North, 9, Item("herb_stew", "허브스튜", 2, true, 6, 15));

            List<VillageDrugSaveEntry> exported = VillageDrugState.ExportSaveEntries();

            Assert.That(exported, Has.Count.EqualTo(1), "재고 없는 마을은 스킵되고 재고 있는 마을만 수출된다.");
            Assert.That(exported[0].nation, Is.EqualTo((int)NationType.North), "수출 항목은 재고 보유 마을(북국 #9)이어야 한다.");
            Assert.That(exported[0].villageIndex, Is.EqualTo(9), "수출 항목의 마을 인덱스 보존.");
        }

        // ════════════════════════════════════════════════════════════════════
        // 3. Import null — 구버전 세이브(필드 부재) → 클리어만 수행
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void Import_Null_ClearsExistingState_WithoutThrowing()
        {
            VillageDrugState.SetRestaurantBuilt(NationType.East, 0, true);
            VillageDrugState.AssignGuard(NationType.East, 0, "G1");
            VillageDrugState.AddStock(NationType.East, 0, Item("meat_grilled", "고기구이", 4, false, 0, 10));

            Assert.DoesNotThrow(() => VillageDrugState.ImportSaveEntries(null), "null 수입은 예외 없이 클리어만 수행해야 한다.");

            VillageDrugSnapshot snap = VillageDrugState.GetSnapshot(NationType.East, 0);
            Assert.That(snap.restaurantBuilt, Is.False, "Import null 후 건설 플래그는 클리어되어야 한다.");
            Assert.That(snap.assignedGuardId, Is.EqualTo(string.Empty), "Import null 후 가드는 미배치(빈 문자열) 규약.");
            List<RestaurantStockItem> stock = VillageDrugState.GetStock(NationType.East, 0);
            Assert.That(stock, Is.Empty, "Import null 후 재고는 빈 목록이어야 한다.");
        }

        // ════════════════════════════════════════════════════════════════════
        // 4. 결정론 — 2회 수출 결과 완전 동일 (정렬 순서 + 필드 단위 일치)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void Export_Twice_IsDeterministic_AndSortedByNationVillage()
        {
            // 삽입 순서를 마을 키 순서와 어긋나게 구성 — 사전 내부 순서 의존성이 없음을 검증
            VillageDrugState.AddStock(NationType.West, 3, Item("bread", "빵", 1, false, 0, 8));
            VillageDrugState.AddStock(NationType.East, 19, Item("meat_grilled", "고기구이", 2, true, 3, 30));
            VillageDrugState.AddStock(NationType.North, 0, Item("herb_stew", "허브스튜", 3, true, 6, 15));
            VillageDrugState.AddStock(NationType.East, 2, Item("bread", "빵", 4, false, 0, 8));

            List<VillageDrugSaveEntry> first = VillageDrugState.ExportSaveEntries();
            List<VillageDrugSaveEntry> second = VillageDrugState.ExportSaveEntries(); // 수출은 상태를 비파괴해야 한다

            Assert.That(first, Has.Count.EqualTo(4), "재고 보유 마을 4곳 모두 수출.");
            Assert.That(second, Has.Count.EqualTo(first.Count), "2회 수출 항목 수 동일.");

            // (국가, 마을 인덱스) 오름차순 정렬 규약 — 동(1) → 서(2) → 북(4)의 enum 정수 순 (None=0 미사용)
            for (int i = 1; i < first.Count; i++)
            {
                bool ordered = first[i - 1].nation < first[i].nation
                    || (first[i - 1].nation == first[i].nation
                        && first[i - 1].villageIndex < first[i].villageIndex);
                Assert.That(ordered, Is.True, $"수출 순서는 (nation, villageIndex) 오름차순이어야 한다(항목 {i - 1}→{i}).");
            }

            // 필드 단위 완전 일치 — 2회 수출이 결정론임을 보장
            for (int i = 0; i < first.Count; i++)
            {
                VillageDrugSaveEntry a = first[i];
                VillageDrugSaveEntry b = second[i];
                Assert.That(b.nation, Is.EqualTo(a.nation), $"[{i}] nation 일치.");
                Assert.That(b.villageIndex, Is.EqualTo(a.villageIndex), $"[{i}] villageIndex 일치.");
                Assert.That(b.restaurantBuilt, Is.EqualTo(a.restaurantBuilt), $"[{i}] restaurantBuilt 일치.");
                Assert.That(b.assignedGuardId, Is.EqualTo(a.assignedGuardId), $"[{i}] assignedGuardId 일치.");
                Assert.That(b.addiction, Is.EqualTo(a.addiction).Within(Tol), $"[{i}] addiction 일치.");
                Assert.That(b.registeredPrice, Is.EqualTo(a.registeredPrice), $"[{i}] registeredPrice 일치.");
                Assert.That(b.departedVillagers, Is.EqualTo(a.departedVillagers), $"[{i}] departedVillagers 일치.");
                Assert.That(b.stock, Has.Count.EqualTo(a.stock.Count), $"[{i}] 재고 슬롯 수 일치.");
                for (int s = 0; s < a.stock.Count; s++)
                {
                    Assert.That(second[i].stock[s].foodItemId, Is.EqualTo(a.stock[s].foodItemId), $"[{i}].stock[{s}] foodItemId 일치.");
                    Assert.That(second[i].stock[s].displayName, Is.EqualTo(a.stock[s].displayName), $"[{i}].stock[{s}] displayName 일치.");
                    Assert.That(second[i].stock[s].count, Is.EqualTo(a.stock[s].count), $"[{i}].stock[{s}] count 일치.");
                    Assert.That(second[i].stock[s].isDrugged, Is.EqualTo(a.stock[s].isDrugged), $"[{i}].stock[{s}] isDrugged 일치.");
                    Assert.That(second[i].stock[s].drugStage, Is.EqualTo(a.stock[s].drugStage), $"[{i}].stock[{s}] drugStage 일치.");
                    Assert.That(second[i].stock[s].basePrice, Is.EqualTo(a.stock[s].basePrice), $"[{i}].stock[{s}] basePrice 일치.");
                }
            }
        }
    }
}
