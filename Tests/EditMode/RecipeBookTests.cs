using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Tests.EditMode
{
    public class RecipeBookTests
    {
        [SetUp]
        public void Setup()
        {
            RecipeDiscoverySystem.Reset();
        }

        [Test]
        public void MarkDiscovered_AddsToDiscovered()
        {
            RecipeDiscoverySystem.MarkDiscovered("만능 치유액");
            Assert.IsTrue(RecipeDiscoverySystem.IsDiscovered("만능 치유액"));
            Assert.AreEqual(1, RecipeDiscoverySystem.DiscoveredCount);
        }

        [Test]
        public void MarkDiscovered_Duplicate_DoesNotIncreaseCount()
        {
            RecipeDiscoverySystem.MarkDiscovered("만능 치유액");
            RecipeDiscoverySystem.MarkDiscovered("만능 치유액");
            Assert.AreEqual(1, RecipeDiscoverySystem.DiscoveredCount);
        }

        [Test]
        public void IsDiscovered_Unknown_ReturnsFalse()
        {
            Assert.IsFalse(RecipeDiscoverySystem.IsDiscovered("없는 레시피"));
        }

        [Test]
        public void MarkDiscovered_MultipleRecipes()
        {
            RecipeDiscoverySystem.MarkDiscovered("토끼 허브 구이");
            RecipeDiscoverySystem.MarkDiscovered("만능 치유액");
            RecipeDiscoverySystem.MarkDiscovered("독성 가시액");
            Assert.AreEqual(3, RecipeDiscoverySystem.DiscoveredCount);
        }

        [Test]
        public void Reset_ClearsAll()
        {
            RecipeDiscoverySystem.MarkDiscovered("만능 치유액");
            RecipeDiscoverySystem.Reset();
            Assert.AreEqual(0, RecipeDiscoverySystem.DiscoveredCount);
        }

        [Test]
        public void GetAllDiscovered_ReturnsAll()
        {
            RecipeDiscoverySystem.MarkDiscovered("A");
            RecipeDiscoverySystem.MarkDiscovered("B");
            var all = RecipeDiscoverySystem.GetAllDiscovered();
            Assert.AreEqual(2, all.Count);
            Assert.Contains("A", all);
            Assert.Contains("B", all);
        }

        [Test]
        public void MarkDiscovered_All80Alchemy_AddsCorrectly()
        {
            // Simulate discovering all alchemy combos
            var all = HerbComboDatabase.AllCombos;
            int count = 0;
            foreach (var kv in all)
            {
                RecipeDiscoverySystem.MarkDiscovered(kv.Value.resultName);
                count++;
            }
            Assert.AreEqual(count, RecipeDiscoverySystem.DiscoveredCount);
        }

        [Test]
        public void MarkDiscovered_All38Dishes_AddsCorrectly()
        {
            var all = DishDatabase.All;
            int count = 0;
            foreach (var dish in all)
            {
                RecipeDiscoverySystem.MarkDiscovered(dish.DisplayName);
                count++;
            }
            Assert.AreEqual(count, RecipeDiscoverySystem.DiscoveredCount);
        }
    }
}