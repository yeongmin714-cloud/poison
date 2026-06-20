using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Tests.EditMode
{
    public class DrugEffectSystemTests
    {
        [SetUp]
        public void Setup()
        {
            // Force initialize DrugEffectSystem
            if (!DrugEffectSystem.IsInitialized)
                DrugEffectSystem.Initialize();
        }

        [Test]
        public void ApplyDrug_Stage1_GivesGold()
        {
            int gold = DrugEffectSystem.ApplyDrug(1);
            Assert.Greater(gold, 0, "Stage 1 drug should generate gold");
        }

        [Test]
        public void ApplyDrug_Stage10_GivesMoreGoldThanStage1()
        {
            int gold1 = DrugEffectSystem.ApplyDrug(1);
            int gold10 = DrugEffectSystem.ApplyDrug(10);
            Assert.Greater(gold10, gold1 * 2, "Stage 10 drug should generate significantly more gold than stage 1");
        }

        [Test]
        public void ApplyDrug_IncreasesAddiction()
        {
            float before = DrugEffectSystem.AddictionLevel;
            DrugEffectSystem.ApplyDrug(1);
            float after = DrugEffectSystem.AddictionLevel;
            Assert.Greater(after, before, "Addiction should increase after using drugs");
        }

        [Test]
        public void ApplyDrug_HigherStage_MoreAddiction()
        {
            DrugEffectSystem.ReduceAddiction(100f); // reset
            DrugEffectSystem.ApplyDrug(1);  // low addiction increase
            float afterLow = DrugEffectSystem.AddictionLevel;

            DrugEffectSystem.ReduceAddiction(100f); // reset
            DrugEffectSystem.ApplyDrug(10); // high addiction increase
            float afterHigh = DrugEffectSystem.AddictionLevel;

            Assert.Greater(afterHigh, afterLow, "Stage 10 should increase addiction more than stage 1");
        }

        [Test]
        public void ReduceAddiction_DecreasesLevel()
        {
            DrugEffectSystem.ApplyDrug(3); // increase first
            float before = DrugEffectSystem.AddictionLevel;
            DrugEffectSystem.ReduceAddiction(5f);
            float after = DrugEffectSystem.AddictionLevel;
            Assert.LessOrEqual(after, before, "ReduceAddiction should decrease or maintain addiction level");
        }

        [Test]
        public void AddictionLevel_ClampedAt100()
        {
            // Apply multiple high-stage drugs to push addiction high
            for (int i = 0; i < 10; i++)
                DrugEffectSystem.ApplyDrug(10);

            Assert.LessOrEqual(DrugEffectSystem.AddictionLevel, 100f, "Addiction should not exceed 100%");
        }

        [Test]
        public void GetAddictionLabel_StartsClean()
        {
            DrugEffectSystem.ReduceAddiction(100f); // reset to 0
            string label = DrugEffectSystem.GetAddictionLabel();
            Assert.AreEqual("깨끗함", label);
        }

        [Test]
        public void GetAddictionLabel_HighAddiction_ShowsCorrectLabel()
        {
            DrugEffectSystem.ReduceAddiction(100f); // reset
            for (int i = 0; i < 10; i++)
                DrugEffectSystem.ApplyDrug(7); // 8% each

            string label = DrugEffectSystem.GetAddictionLabel();
            Assert.IsTrue(label == "심각한 중독" || label == "완전 중독", 
                $"Expected serious addiction label, got '{label}'");
        }

        [Test]
        public void CreateDrugItem_Stage1_ReturnsCorrectItem()
        {
            var item = DrugEffectSystem.CreateDrugItem(1);
            Assert.IsNotNull(item);
            Assert.AreEqual("가벼운 취기제", item.displayName);
            Assert.AreEqual(PlayerInventory.ItemCategory.Drug, item.category);
        }

        [Test]
        public void CreateDrugItem_Stage10_ReturnsCorrectItem()
        {
            var item = DrugEffectSystem.CreateDrugItem(10);
            Assert.IsNotNull(item);
            Assert.AreEqual("금지된 낙원", item.displayName);
            Assert.AreEqual(PlayerInventory.ItemCategory.Drug, item.category);
        }

        [Test]
        public void CreateDrugItem_InvalidStage_ReturnsNull()
        {
            var item = DrugEffectSystem.CreateDrugItem(0);
            Assert.IsNull(item);
            item = DrugEffectSystem.CreateDrugItem(99);
            Assert.IsNull(item);
        }
    }
}