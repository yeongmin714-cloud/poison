using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [Milestone A] 운·희귀도 기반 제작 성공률(ComputeFinalCraftChance) + RecipeDB_SO 검증.
    /// ComputeFinalCraftChance는 PlayerStats 미생성 시 운 보너스 0 처리 → EditMode에서 결정론 검증 가능.
    /// </summary>
    [TestFixture]
    public class CraftChanceTests
    {
        [Test]
        public void ComputeFinalCraftChance_ZeroLuckCommon_ReturnsBase()
        {
            Assert.AreEqual(90, CraftingHelper.ComputeFinalCraftChance(90, ItemRarity.Common));
        }

        [Test]
        public void RarityPenalty_OrderingIsMonotonic()
        {
            int common = CraftingHelper.ComputeFinalCraftChance(90, ItemRarity.Common);
            int rare = CraftingHelper.ComputeFinalCraftChance(90, ItemRarity.Rare);
            int legendary = CraftingHelper.ComputeFinalCraftChance(90, ItemRarity.Legendary);
            Assert.Greater(common, rare);
            Assert.Greater(rare, legendary);
        }

        [Test]
        public void GetRarityPenalty_ExactValues()
        {
            Assert.AreEqual(0, CraftingHelper.GetRarityPenalty(ItemRarity.Common));
            Assert.AreEqual(-8, CraftingHelper.GetRarityPenalty(ItemRarity.Uncommon));
            Assert.AreEqual(-18, CraftingHelper.GetRarityPenalty(ItemRarity.Rare));
            Assert.AreEqual(-30, CraftingHelper.GetRarityPenalty(ItemRarity.Epic));
            Assert.AreEqual(-45, CraftingHelper.GetRarityPenalty(ItemRarity.Legendary));
        }

        [Test]
        public void ComputeFinalCraftChance_ClampsTo95Max()
        {
            Assert.LessOrEqual(CraftingHelper.ComputeFinalCraftChance(300, ItemRarity.Common), 95);
            Assert.AreEqual(95, CraftingHelper.ComputeFinalCraftChance(300, ItemRarity.Common));
        }

        [Test]
        public void ComputeFinalCraftChance_ClampsTo5Min()
        {
            Assert.GreaterOrEqual(CraftingHelper.ComputeFinalCraftChance(10, ItemRarity.Legendary), 5);
            Assert.AreEqual(5, CraftingHelper.ComputeFinalCraftChance(10, ItemRarity.Legendary));
        }

        [Test]
        public void RecipeDB_TryMatchResult_FindsEntry()
        {
            var db = UnityEngine.ScriptableObject.CreateInstance<RecipeDB_SO>();
            db.recipes.Add(new RecipeDB_SO.RecipeEntry
            {
                ResultId = "weapon_sword_wood",
                Mat1Id = "mat_wood", Mat1Count = 2,
                rarity = ItemRarity.Common,
            });
            Assert.IsTrue(db.TryMatchResult("weapon_sword_wood", out var e));
            Assert.AreEqual("mat_wood", e.Mat1Id);
            Assert.AreEqual(2, e.Mat1Count);
            UnityEngine.Object.DestroyImmediate(db);
        }

        [Test]
        public void RecipeDB_TryMatchResult_MissingReturnsFalse()
        {
            var db = UnityEngine.ScriptableObject.CreateInstance<RecipeDB_SO>();
            Assert.IsFalse(db.TryMatchResult("nonexistent_result", out _));
            UnityEngine.Object.DestroyImmediate(db);
        }
    }
}
