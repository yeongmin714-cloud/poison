using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Tests.EditMode
{
    public class CraftingHelperTests
    {
        [SetUp]
        public void Setup()
        {
            RecipeDiscoverySystem.Reset();
        }

        [Test]
        public void CraftAlchemy_InvalidCombo_ReturnsFalse()
        {
            // Non-existent combo
            bool result = CraftingHelper.CraftAlchemy("A1", "Z99");
            Assert.IsFalse(result);
        }

        [Test]
        public void CraftAlchemy_NoPlayerInventory_ReturnsFalse()
        {
            // In EditMode, PlayerInventory.Instance is null
            bool result = CraftingHelper.CraftAlchemy("A1", "A2");
            Assert.IsFalse(result); // because no inventory
        }

        [Test]
        public void CraftAlchemy_KnownCombo_DoesNotThrow()
        {
            // Should not throw even if inventory is missing
            Assert.DoesNotThrow(() => CraftingHelper.CraftAlchemy("A1", "A2"));
        }
    }
}