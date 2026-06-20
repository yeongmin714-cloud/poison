using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    public class GasMaskControllerTests
    {
        [SetUp]
        public void Setup()
        {
            if (!GasMaskSystem.IsInitialized)
                GasMaskSystem.Initialize();
        }

        [TearDown]
        public void Teardown()
        {
            GasMaskSystem.Unequip();
        }

        [Test]
        public void GetStatusText_WhenNotEquipped_ShowsNotWearing()
        {
            GasMaskSystem.Unequip();
            string text = GasMaskController.GetStatusText();
            Assert.AreEqual("방독면: 미착용", text);
        }

        [Test]
        public void GetStatusText_WhenWoodEquipped_ShowsCorrectInfo()
        {
            GasMaskSystem.Equip(GasMaskGrade.Wood);
            string text = GasMaskController.GetStatusText();
            Assert.IsTrue(text.Contains("나무 방독면"));
            Assert.IsTrue(text.Contains("10.0초"));
            Assert.IsTrue(text.Contains("3/3"));
        }

        [Test]
        public void GetStatusText_WhenSpecialEquipped_ShowsInfiniteDurability()
        {
            GasMaskSystem.Equip(GasMaskGrade.Special);
            string text = GasMaskController.GetStatusText();
            Assert.IsTrue(text.Contains("특수 합금 방독면"));
            Assert.IsTrue(text.Contains("∞"));
        }
    }
}