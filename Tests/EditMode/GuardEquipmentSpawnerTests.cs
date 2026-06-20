using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    [TestFixture]
    public class GuardEquipmentSpawnerTests
    {
        [Test]
        public void GenerateEquipmentItem_Head_ReturnsArmor()
        {
            var item = GuardEquipmentSpawner.GenerateEquipmentItem(
                ItemRarity.Common, EquipmentPartConfig.EquipmentPart.Head, 5);
            Assert.AreEqual(PlayerInventory.ItemCategory.Armor, item.category);
            Assert.That(item.displayName, Does.Contain("투구"));
        }

        [Test]
        public void GenerateEquipmentItem_Body_ReturnsArmor()
        {
            var item = GuardEquipmentSpawner.GenerateEquipmentItem(
                ItemRarity.Rare, EquipmentPartConfig.EquipmentPart.Body, 10);
            Assert.AreEqual(PlayerInventory.ItemCategory.Armor, item.category);
            Assert.That(item.displayName, Does.Contain("갑옷"));
        }

        [Test]
        public void GenerateEquipmentItem_Weapon_ReturnsWeapon()
        {
            var item = GuardEquipmentSpawner.GenerateEquipmentItem(
                ItemRarity.Epic, EquipmentPartConfig.EquipmentPart.Weapon, 25);
            Assert.AreEqual(PlayerInventory.ItemCategory.Weapon, item.category);
        }

        [Test]
        public void GenerateEquipmentItem_HasDisplayName()
        {
            var item = GuardEquipmentSpawner.GenerateEquipmentItem(
                ItemRarity.Legendary, EquipmentPartConfig.EquipmentPart.Hands, 30);
            Assert.IsFalse(string.IsNullOrEmpty(item.displayName));
            Assert.That(item.displayName, Does.Contain("전설"));
            Assert.That(item.displayName, Does.Contain("장갑"));
        }

        [Test]
        public void GenerateEquipmentItem_MaxStackIs1()
        {
            var item = GuardEquipmentSpawner.GenerateEquipmentItem(
                ItemRarity.Common, EquipmentPartConfig.EquipmentPart.Feet, 1);
            Assert.AreEqual(1, item.maxStack);
        }

        [Test]
        public void GenerateEquipmentItem_HasDurability()
        {
            var item = GuardEquipmentSpawner.GenerateEquipmentItem(
                ItemRarity.Rare, EquipmentPartConfig.EquipmentPart.Body, 15);
            Assert.Greater(item.maxDurability, 0);
        }

        [Test]
        public void MapPartToSlot_HeadToArmor()
        {
            Assert.AreEqual(GuardEquipmentSystem.EquipSlot.Armor,
                GuardEquipmentSpawner.MapPartToSlot(EquipmentPartConfig.EquipmentPart.Head));
        }

        [Test]
        public void MapPartToSlot_BodyToArmor()
        {
            Assert.AreEqual(GuardEquipmentSystem.EquipSlot.Armor,
                GuardEquipmentSpawner.MapPartToSlot(EquipmentPartConfig.EquipmentPart.Body));
        }

        [Test]
        public void MapPartToSlot_HandsToAccessory()
        {
            Assert.AreEqual(GuardEquipmentSystem.EquipSlot.Accessory,
                GuardEquipmentSpawner.MapPartToSlot(EquipmentPartConfig.EquipmentPart.Hands));
        }

        [Test]
        public void MapPartToSlot_FeetToAccessory()
        {
            Assert.AreEqual(GuardEquipmentSystem.EquipSlot.Accessory,
                GuardEquipmentSpawner.MapPartToSlot(EquipmentPartConfig.EquipmentPart.Feet));
        }

        [Test]
        public void MapPartToSlot_WeaponToWeapon()
        {
            Assert.AreEqual(GuardEquipmentSystem.EquipSlot.Weapon,
                GuardEquipmentSpawner.MapPartToSlot(EquipmentPartConfig.EquipmentPart.Weapon));
        }
    }
}
