using System.Collections.Generic;
using NUnit.Framework;
using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C-O1-03: 세트 파밍 튜닝(SetChestTuning) 테스트.
    /// 기대 횟수 닫힌식(H_K/p) / 권장 확률 표 / 레벨 밴드 매핑 / 결정론 파츠 롤.
    /// </summary>
    public class SetChestTuningTests
    {
        // ===================== 권장 확률 표 (ITEM_TIERS.md 기준) =====================

        [Test]
        public void GetChanceForRemainingParts_MatchesBenchmarkTable()
        {
            Assert.AreEqual(0.20f, SetChestTuning.GetChanceForRemainingParts(1), 0.0001f);
            Assert.AreEqual(0.30f, SetChestTuning.GetChanceForRemainingParts(2), 0.0001f);
            Assert.AreEqual(0.33f, SetChestTuning.GetChanceForRemainingParts(3), 0.0001f);
            Assert.AreEqual(0.37f, SetChestTuning.GetChanceForRemainingParts(4), 0.0001f);
            Assert.AreEqual(0.37f, SetChestTuning.GetChanceForRemainingParts(10), 0.0001f, "K>4은 상한 37%");
        }

        // ===================== 기대 횟수 닫힌식 =====================

        [Test]
        public void ExpectedRunsToComplete_BenchmarkTargets_About5Runs()
        {
            Assert.AreEqual(5.0f, SetChestTuning.ExpectedRunsToComplete(1, 0.20f), 0.01f, "K=1, p=20% → 5회");
            Assert.AreEqual(5.0f, SetChestTuning.ExpectedRunsToComplete(2, 0.30f), 0.01f, "K=2, p=30% → 5회");
            Assert.AreEqual(5.56f, SetChestTuning.ExpectedRunsToComplete(3, 0.33f), 0.05f, "K=3, p=33%");
            Assert.AreEqual(5.63f, SetChestTuning.ExpectedRunsToComplete(4, 0.37f), 0.05f, "K=4, p=37%");
        }

        [Test]
        public void ExpectedRunsToComplete_ZeroOrInvalid_ReturnsZero()
        {
            Assert.AreEqual(0f, SetChestTuning.ExpectedRunsToComplete(0, 0.37f));
            Assert.AreEqual(0f, SetChestTuning.ExpectedRunsToComplete(4, 0f));
            Assert.AreEqual(0f, SetChestTuning.ExpectedRunsToComplete(4, -1f));
        }

        // ===================== 레벨 밴드 매핑 =====================

        [Test]
        public void GetSetKindForLevel_MapsBands()
        {
            Assert.AreEqual(EquipmentSetKind.Leather, SetChestTuning.GetSetKindForLevel(1));
            Assert.AreEqual(EquipmentSetKind.Leather, SetChestTuning.GetSetKindForLevel(10));
            Assert.AreEqual(EquipmentSetKind.Chain, SetChestTuning.GetSetKindForLevel(11));
            Assert.AreEqual(EquipmentSetKind.Chain, SetChestTuning.GetSetKindForLevel(20));
            Assert.AreEqual(EquipmentSetKind.Plate, SetChestTuning.GetSetKindForLevel(21));
            Assert.AreEqual(EquipmentSetKind.Plate, SetChestTuning.GetSetKindForLevel(50));
        }

        [Test]
        public void GetRollChanceForLevel_IncreasesWithBand()
        {
            float low = SetChestTuning.GetRollChanceForLevel(5);
            float mid = SetChestTuning.GetRollChanceForLevel(15);
            float high = SetChestTuning.GetRollChanceForLevel(25);
            Assert.Less(low, mid, "하위 밴드 확률 < 중위");
            Assert.Less(mid, high, "중위 밴드 확률 < 상위");
            Assert.AreEqual(0.30f, low, 0.0001f);
            Assert.AreEqual(0.33f, mid, 0.0001f);
            Assert.AreEqual(0.37f, high, 0.0001f);
        }

        // ===================== 월드 드랍 희소 규약 =====================

        [Test]
        public void WorldRareDropChance_IsInTightSparseBand()
        {
            Assert.LessOrEqual(SetChestTuning.WorldRareDropChanceMin, SetChestTuning.WorldRareDropChanceMax);
            Assert.AreEqual(0.001f, SetChestTuning.WorldRareDropChanceMin, 0.0001f, "0.1% 하한");
            Assert.AreEqual(0.005f, SetChestTuning.WorldRareDropChanceMax, 0.0001f, "0.5% 상한");
            Assert.Less(SetChestTuning.WorldRareDropChanceMax, SetChestTuning.GetRollChanceForLevel(25),
                "월드 희소 드랍은 확정 파밍 경로(세트 롤)보다 훨씬 희소해야 한다");
        }

        // ===================== 결정론 파츠 롤 =====================

        [Test]
        public void RollSetPiecesForLevel_AlwaysHit_RollsAll4Parts()
        {
            var rolled = SetChestTuning.RollSetPiecesForLevel(25, null, () => 0.0);
            Assert.AreEqual(4, rolled.Count, "rng=0(전부 성공) → 밴드 세트 4파츠 전부");
            var ids = new HashSet<string>();
            foreach (var item in rolled) ids.Add(item.id);
            Assert.AreEqual(4, ids.Count, "파츠 id 중복 없음");
        }

        [Test]
        public void RollSetPiecesForLevel_AlwaysMiss_RollsNothing()
        {
            var rolled = SetChestTuning.RollSetPiecesForLevel(5, null, () => 0.999);
            Assert.AreEqual(0, rolled.Count, "rng≈1(전부 실패) → 0파츠");
        }

        [Test]
        public void RollSetPiecesForLevel_OwnedPartsExcluded()
        {
            var owned = new List<string>
            {
                PlayerInventory.HelmetStone.id,
                PlayerInventory.ArmorStone.id,
                PlayerInventory.BootStone.id,
                PlayerInventory.GloveCrystal.id,
            };
            var rolled = SetChestTuning.RollSetPiecesForLevel(25, owned, () => 0.0);
            Assert.AreEqual(0, rolled.Count, "판금세트 전체 보유 → 롤 대상 없음");
        }

        [Test]
        public void RollSetPiecesForLevel_PartialOwnership_RollsOnlyMissing()
        {
            var owned = new List<string> { PlayerInventory.HelmetStone.id, PlayerInventory.GloveCrystal.id };
            var rolled = SetChestTuning.RollSetPiecesForLevel(25, owned, () => 0.0);
            Assert.AreEqual(2, rolled.Count, "보유 2개 제외 → 나머지 2개만 롤");
            foreach (var item in rolled)
            {
                Assert.IsFalse(owned.Contains(item.id), "보유 파츠는 재롤되지 않음");
            }
        }

        [Test]
        public void RollSetPiecesForLevel_LevelBand_SelectsCorrectSet()
        {
            var rolled = SetChestTuning.RollSetPiecesForLevel(5, null, () => 0.0);
            Assert.AreEqual(PlayerInventory.HelmetWood.id, rolled[0].id, "Lv5 → 가죽세트 시작");

            var rolledChain = SetChestTuning.RollSetPiecesForLevel(15, null, () => 0.0);
            Assert.AreEqual(PlayerInventory.HelmetSteel.id, rolledChain[0].id, "Lv15 → 사슬세트 시작");
        }

        [Test]
        public void RollSetPiecesForLevel_NullRng_ReturnsEmptySafely()
        {
            var rolled = SetChestTuning.RollSetPiecesForLevel(5, null, null);
            Assert.AreEqual(0, rolled.Count, "rng null → 안전하게 빈 결과");
        }
    }
}
