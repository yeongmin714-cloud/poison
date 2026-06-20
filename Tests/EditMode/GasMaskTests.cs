using NUnit.Framework;
using ProjectName.Core;

namespace ProjectName.Tests.EditMode
{
    public class GasMaskTests
    {
        [SetUp]
        public void Setup()
        {
            if (!GasMaskSystem.IsInitialized)
                GasMaskSystem.Initialize();
        }

        [Test]
        public void Equip_WoodMask_SetsCorrectValues()
        {
            GasMaskSystem.Equip(GasMaskGrade.Wood);
            Assert.IsTrue(GasMaskSystem.IsActive);
            Assert.IsNotNull(GasMaskSystem.EquippedMask);
            Assert.AreEqual("나무 방독면", GasMaskSystem.EquippedMask.Value.displayName);
            Assert.AreEqual(3, GasMaskSystem.CurrentDurability);
            Assert.AreEqual(10f, GasMaskSystem.RemainingTime);
        }

        [Test]
        public void Equip_StoneMask_HasLongerDuration()
        {
            GasMaskSystem.Equip(GasMaskGrade.Stone);
            Assert.AreEqual("돌 방독면", GasMaskSystem.EquippedMask.Value.displayName);
            Assert.AreEqual(8, GasMaskSystem.CurrentDurability);
            Assert.AreEqual(30f, GasMaskSystem.RemainingTime);
        }

        [Test]
        public void Equip_SpecialMask_HasInfiniteDurability()
        {
            GasMaskSystem.Equip(GasMaskGrade.Special);
            Assert.AreEqual(int.MaxValue, GasMaskSystem.CurrentDurability);
            Assert.AreEqual(300f, GasMaskSystem.RemainingTime);
        }

        [Test]
        public void Unequip_ClearsState()
        {
            GasMaskSystem.Equip(GasMaskGrade.Wood);
            GasMaskSystem.Unequip();
            Assert.IsFalse(GasMaskSystem.IsActive);
            Assert.IsNull(GasMaskSystem.EquippedMask);
        }

        [Test]
        public void Update_ReducesRemainingTime()
        {
            GasMaskSystem.Equip(GasMaskGrade.Wood);
            float before = GasMaskSystem.RemainingTime;
            GasMaskSystem.Update(1f);
            float after = GasMaskSystem.RemainingTime;
            Assert.Less(after, before);
        }

        [Test]
        public void Update_WhenTimeExpires_ReducesDurability()
        {
            GasMaskSystem.Equip(GasMaskGrade.Wood);
            int beforeDur = GasMaskSystem.CurrentDurability;

            // Simulate full duration use
            GasMaskSystem.Update(11f); // 10s duration + 1s overflow

            Assert.AreEqual(beforeDur - 1, GasMaskSystem.CurrentDurability,
                "Durability should decrease by 1 after full duration");
            Assert.IsTrue(GasMaskSystem.IsActive,
                "Mask should still be active if durability > 0");
        }

        [Test]
        public void Update_WhenDurabilityZero_DestroysMask()
        {
            GasMaskSystem.Equip(GasMaskGrade.Wood);
            // Use all 3 charges
            GasMaskSystem.Update(11f); // charge 1
            GasMaskSystem.Update(11f); // charge 2
            GasMaskSystem.Update(11f); // charge 3

            Assert.AreEqual(0, GasMaskSystem.CurrentDurability);
            Assert.IsFalse(GasMaskSystem.IsActive,
                "Mask should be unequipped when durability reaches 0");
            Assert.IsNull(GasMaskSystem.EquippedMask);
        }

        [Test]
        public void GetDef_Wood_ReturnsCorrectDef()
        {
            var def = GasMaskSystem.GetDef(GasMaskGrade.Wood);
            Assert.IsTrue(def.HasValue);
            Assert.AreEqual(3, def.Value.maxDurability);
            Assert.AreEqual(10f, def.Value.duration);
        }

        [Test]
        public void GetDef_Special_ReturnsCorrectDef()
        {
            var def = GasMaskSystem.GetDef(GasMaskGrade.Special);
            Assert.IsTrue(def.HasValue);
            Assert.AreEqual(int.MaxValue, def.Value.maxDurability);
            Assert.AreEqual(300f, def.Value.duration);
        }

        [Test]
        public void CreateGasMaskItem_Wood_ReturnsItem()
        {
            var item = GasMaskSystem.CreateGasMaskItem(GasMaskGrade.Wood);
            Assert.IsNotNull(item);
            Assert.AreEqual("gasmask_0", item.id);
            Assert.AreEqual("나무 방독면", item.displayName);
        }

        [Test]
        public void Update_IdleWhenNotEquipped_DoesNothing()
        {
            GasMaskSystem.Unequip();
            Assert.DoesNotThrow(() => GasMaskSystem.Update(100f));
        }
    }
}