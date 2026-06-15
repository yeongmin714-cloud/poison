using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C9-17 장비 내구도 시스템 테스트
    /// </summary>
    public class DurabilityTests
    {
        // ===================== ItemData maxDurability =====================

        [Test]
        public void SwordWood_HasDurability()
        {
            Assert.AreEqual(20, PlayerInventory.SwordWood.maxDurability, "목검 내구도 20");
        }

        [Test]
        public void LeatherArmor_HasDurability()
        {
            Assert.AreEqual(30, PlayerInventory.LeatherArmor.maxDurability, "가죽 갑옷 내구도 30");
        }

        [Test]
        public void Herb_HasNoDurability()
        {
            Assert.AreEqual(0, PlayerInventory.Herb_Red.maxDurability, "약초는 내구도 없음");
        }

        // ===================== ItemSlot currentDurability =====================

        [Test]
        public void ItemSlot_InitialDurability_IsMax()
        {
            var slot = new PlayerInventory.ItemSlot
            {
                item = PlayerInventory.SwordWood,
                count = 1,
                currentDurability = PlayerInventory.SwordWood.maxDurability
            };
            Assert.AreEqual(20, slot.currentDurability, "초기 내구도 = maxDurability");
        }

        // ===================== ReduceDurability =====================

        [Test]
        public void ReduceDurability_DecreasesByOne()
        {
            var slot = new PlayerInventory.ItemSlot
            {
                item = PlayerInventory.SwordWood,
                count = 1,
                currentDurability = 20
            };

            bool broken = EquipmentDurabilitySystem.ReduceDurability(slot);
            Assert.AreEqual(19, slot.currentDurability, "내구도 -1");
            Assert.IsFalse(broken, "아직 파괴되지 않음");
        }

        [Test]
        public void ReduceDurability_ToZero_ReturnsBroken()
        {
            var slot = new PlayerInventory.ItemSlot
            {
                item = PlayerInventory.SwordWood,
                count = 1,
                currentDurability = 1
            };

            bool broken = EquipmentDurabilitySystem.ReduceDurability(slot);
            Assert.AreEqual(0, slot.currentDurability, "내구도 0");
            Assert.IsTrue(broken, "파괴 신호 반환");
        }

        [Test]
        public void ReduceDurability_NoDurabilityItem_NoEffect()
        {
            var slot = new PlayerInventory.ItemSlot
            {
                item = PlayerInventory.Herb_Red,
                count = 5,
                currentDurability = 0
            };

            bool broken = EquipmentDurabilitySystem.ReduceDurability(slot);
            Assert.IsFalse(broken, "내구도 없는 아이템은 파괴되지 않음");
        }

        // ===================== Repair =====================

        [Test]
        public void Repair_FullRepair_RestoresToMax()
        {
            var slot = new PlayerInventory.ItemSlot
            {
                item = PlayerInventory.SwordWood,
                count = 1,
                currentDurability = 5
            };

            EquipmentDurabilitySystem.Repair(slot);
            Assert.AreEqual(20, slot.currentDurability, "완전 수리 후 maxDurability");
        }

        [Test]
        public void Repair_PartialRepair()
        {
            var slot = new PlayerInventory.ItemSlot
            {
                item = PlayerInventory.SwordWood,
                count = 1,
                currentDurability = 5
            };

            EquipmentDurabilitySystem.Repair(slot, 10);
            Assert.AreEqual(15, slot.currentDurability, "부분 수리 +10");
        }

        [Test]
        public void Repair_CannotExceedMax()
        {
            var slot = new PlayerInventory.ItemSlot
            {
                item = PlayerInventory.SwordWood,
                count = 1,
                currentDurability = 18
            };

            EquipmentDurabilitySystem.Repair(slot, 10);
            Assert.AreEqual(20, slot.currentDurability, "수리는 maxDurability 초과 불가");
        }

        // ===================== GetDurabilityRatio =====================

        [Test]
        public void GetDurabilityRatio_Full_Returns1()
        {
            var slot = new PlayerInventory.ItemSlot { item = PlayerInventory.SwordWood, count = 1, currentDurability = 20 };
            Assert.AreEqual(1f, EquipmentDurabilitySystem.GetDurabilityRatio(slot));
        }

        [Test]
        public void GetDurabilityRatio_Half_Returns0point5()
        {
            var slot = new PlayerInventory.ItemSlot { item = PlayerInventory.SwordWood, count = 1, currentDurability = 10 };
            Assert.AreEqual(0.5f, EquipmentDurabilitySystem.GetDurabilityRatio(slot));
        }

        // ===================== GetDurabilityColorTag =====================

        [Test]
        public void GetDurabilityColorTag_Green_Above60()
        {
            var slot = new PlayerInventory.ItemSlot { item = PlayerInventory.SwordWood, count = 1, currentDurability = 15 };
            Assert.AreEqual("🟢", EquipmentDurabilitySystem.GetDurabilityColorTag(slot));
        }

        [Test]
        public void GetDurabilityColorTag_Yellow_30to60()
        {
            var slot = new PlayerInventory.ItemSlot { item = PlayerInventory.SwordWood, count = 1, currentDurability = 8 };
            Assert.AreEqual("🟡", EquipmentDurabilitySystem.GetDurabilityColorTag(slot));
        }

        [Test]
        public void GetDurabilityColorTag_Red_Below30()
        {
            var slot = new PlayerInventory.ItemSlot { item = PlayerInventory.SwordWood, count = 1, currentDurability = 3 };
            Assert.AreEqual("🔴", EquipmentDurabilitySystem.GetDurabilityColorTag(slot));
        }

        // ===================== GetRepairCost =====================

        [Test]
        public void GetRepairCost_FullDurability_Zero()
        {
            var slot = new PlayerInventory.ItemSlot { item = PlayerInventory.SwordWood, count = 1, currentDurability = 20 };
            Assert.AreEqual(0, EquipmentDurabilitySystem.GetRepairCost(slot));
        }

        [Test]
        public void GetRepairCost_BrokenWeapon_NonZero()
        {
            var slot = new PlayerInventory.ItemSlot { item = PlayerInventory.SwordWood, count = 1, currentDurability = 0 };
            Assert.Greater(EquipmentDurabilitySystem.GetRepairCost(slot), 0, "파괴된 무기는 수리 비용 > 0");
        }

        // ===================== IsBroken =====================

        [Test]
        public void IsBroken_DurabilityZero_True()
        {
            var slot = new PlayerInventory.ItemSlot { item = PlayerInventory.SwordWood, count = 1, currentDurability = 0 };
            Assert.IsTrue(EquipmentDurabilitySystem.IsBroken(slot));
        }

        [Test]
        public void IsBroken_DurabilityAboveZero_False()
        {
            var slot = new PlayerInventory.ItemSlot { item = PlayerInventory.SwordWood, count = 1, currentDurability = 5 };
            Assert.IsFalse(EquipmentDurabilitySystem.IsBroken(slot));
        }

        [Test]
        public void IsBroken_NoDurabilityItem_False()
        {
            var slot = new PlayerInventory.ItemSlot { item = PlayerInventory.Herb_Red, count = 5, currentDurability = 0 };
            Assert.IsFalse(EquipmentDurabilitySystem.IsBroken(slot), "내구도 없는 아이템은 파괴되지 않음");
        }

        // ===================== GetDurabilityString =====================

        [Test]
        public void GetDurabilityString_IncludesRatio()
        {
            var slot = new PlayerInventory.ItemSlot { item = PlayerInventory.SwordWood, count = 1, currentDurability = 10 };
            string str = EquipmentDurabilitySystem.GetDurabilityString(slot);
            Assert.IsTrue(str.Contains("10/20"), "내구도 문자열에 '10/20' 포함");
        }
    }
}