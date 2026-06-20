using NUnit.Framework;
using ProjectName.Core.Data;

namespace ProjectName.Tests.EditMode
{
    public class DrugDatabaseTests
    {
        [Test]
        public void DrugDatabase_Loaded_All10Stages()
        {
            var all = DrugDatabase.All;
            Assert.AreEqual(10, all.Count, "Should have 10 drug stages loaded");
        }

        [Test]
        public void GetByStage_Stage1_ReturnsLowAddiction()
        {
            var drug = DrugDatabase.GetByStage(1);
            Assert.IsTrue(drug.HasValue);
            Assert.AreEqual("가벼운 취기제", drug.Value.drugName);
            Assert.AreEqual(AddictionLevel.Low, drug.Value.addiction);
        }

        [Test]
        public void GetByStage_Stage3_ContainsHerbs()
        {
            var drug = DrugDatabase.GetByStage(3);
            Assert.IsTrue(drug.HasValue);
            Assert.AreEqual("몽환의 향수", drug.Value.drugName);
            Assert.IsTrue(drug.Value.ingredients.Contains("환각포자"));
            Assert.IsTrue(drug.Value.ingredients.Contains("안개꽃"));
        }

        [Test]
        public void GetByStage_Stage5_HighAddiction()
        {
            var drug = DrugDatabase.GetByStage(5);
            Assert.IsTrue(drug.HasValue);
            Assert.AreEqual("광기의 환각제", drug.Value.drugName);
            Assert.AreEqual(AddictionLevel.High, drug.Value.addiction);
        }

        [Test]
        public void GetByStage_Stage10_FatalAddiction()
        {
            var drug = DrugDatabase.GetByStage(10);
            Assert.IsTrue(drug.HasValue);
            Assert.AreEqual("금지된 낙원", drug.Value.drugName);
            Assert.AreEqual(AddictionLevel.Fatal, drug.Value.addiction);
        }

        [Test]
        public void GetByStage_InvalidStage_ReturnsNull()
        {
            var drug = DrugDatabase.GetByStage(0);
            Assert.IsFalse(drug.HasValue);
            drug = DrugDatabase.GetByStage(99);
            Assert.IsFalse(drug.HasValue);
        }

        [Test]
        public void GetByName_FindsKnownDrug()
        {
            var drug = DrugDatabase.GetByName("현실 도피액");
            Assert.IsTrue(drug.HasValue);
            Assert.AreEqual(6, drug.Value.stage);
            Assert.AreEqual(AddictionLevel.High, drug.Value.addiction);
        }

        [Test]
        public void GetByName_Unknown_ReturnsNull()
        {
            var drug = DrugDatabase.GetByName("없는 약물");
            Assert.IsFalse(drug.HasValue);
        }

        [Test]
        public void All_OrderedByStage()
        {
            var all = DrugDatabase.All;
            for (int i = 0; i < all.Count; i++)
            {
                Assert.AreEqual(i + 1, all[i].stage, $"Stage {i+1} should be at index {i}");
            }
        }

        [Test]
        public void DrugDescriptions_AreNotEmpty()
        {
            var all = DrugDatabase.All;
            foreach (var drug in all)
            {
                Assert.IsFalse(string.IsNullOrEmpty(drug.description),
                    $"Drug '{drug.drugName}' should have a description");
            }
        }
    }
}