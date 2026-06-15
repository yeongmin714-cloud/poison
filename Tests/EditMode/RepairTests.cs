using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C9-19 장비 수리 시스템 테스트
    /// </summary>
    public class RepairTests
    {
        // ===================== TryRepair =====================

        [Test]
        public void TryRepair_NullSlot_Fails()
        {
            var result = EquipmentRepairSystem.TryRepair(null);
            Assert.IsFalse(result.success, "null 슬롯은 수리 불가");
        }

        [Test]
        public void TryRepair_NoDurabilityItem_Fails()
        {
            var slot = new PlayerInventory.ItemSlot
            {
                item = PlayerInventory.Herb_Red,
                count = 5,
                currentDurability = 0
            };
            var result = EquipmentRepairSystem.TryRepair(slot);
            Assert.IsFalse(result.success, "내구도 없는 아이템은 수리 불가");
        }

        [Test]
        public void TryRepair_FullDurability_Fails()
        {
            var slot = new PlayerInventory.ItemSlot
            {
                item = PlayerInventory.SwordWood,
                count = 1,
                currentDurability = 20
            };
            var result = EquipmentRepairSystem.TryRepair(slot);
            Assert.IsFalse(result.success, "내구도 가득 참");
            StringAssert.Contains("가득", result.message);
        }

        [Test]
        public void TryRepair_DamagedWeapon_WithoutMaterials_Fails()
        {
            var go = new GameObject("TestPlayer");
            var inv = go.AddComponent<PlayerInventory>();

            var slot = new PlayerInventory.ItemSlot
            {
                item = PlayerInventory.SwordWood,
                count = 1,
                currentDurability = 5
            };

            var result = EquipmentRepairSystem.TryRepair(slot);
            Assert.IsFalse(result.success, "재료 없으면 수리 불가");
            StringAssert.Contains("재료 부족", result.message);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void TryRepair_DamagedWeapon_WithMaterials_Succeeds()
        {
            var go = new GameObject("TestPlayer");
            var inv = go.AddComponent<PlayerInventory>();

            // 수리 재료 추가
            var mat = EquipmentRepairSystem.CreateRepairMaterial(PlayerInventory.ItemCategory.Weapon);
            Assert.IsNotNull(mat, "수리 재료 생성");
            inv.AddItem(mat, 99);

            var slot = new PlayerInventory.ItemSlot
            {
                item = PlayerInventory.SwordWood,
                count = 1,
                currentDurability = 5
            };

            var result = EquipmentRepairSystem.TryRepair(slot);
            Assert.IsTrue(result.success, "재료 있으면 수리 성공");
            Assert.AreEqual(20, slot.currentDurability, "수리 후 내구도 max 복원");
            StringAssert.Contains("수리 완료", result.message);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void TryRepair_ConsumesMaterials()
        {
            var go = new GameObject("TestPlayer");
            var inv = go.AddComponent<PlayerInventory>();

            var mat = EquipmentRepairSystem.CreateRepairMaterial(PlayerInventory.ItemCategory.Weapon);
            inv.AddItem(mat, 10);

            var slot = new PlayerInventory.ItemSlot
            {
                item = PlayerInventory.SwordWood,
                count = 1,
                currentDurability = 0
            };

            int beforeCount = inv.GetItemCount(mat.id);
            EquipmentRepairSystem.TryRepair(slot);
            int afterCount = inv.GetItemCount(mat.id);

            Assert.Less(afterCount, beforeCount, "수리 후 재료 소모됨");

            Object.DestroyImmediate(go);
        }

        // ===================== CreateRepairMaterial =====================

        [Test]
        public void CreateRepairMaterial_Weapon_ReturnsMetal()
        {
            var mat = EquipmentRepairSystem.CreateRepairMaterial(PlayerInventory.ItemCategory.Weapon);
            Assert.IsNotNull(mat);
            StringAssert.Contains("금속", mat.displayName);
        }

        [Test]
        public void CreateRepairMaterial_Armor_ReturnsLeather()
        {
            var mat = EquipmentRepairSystem.CreateRepairMaterial(PlayerInventory.ItemCategory.Armor);
            Assert.IsNotNull(mat);
            StringAssert.Contains("가죽", mat.displayName);
        }

        [Test]
        public void CreateRepairMaterial_Tool_ReturnsWood()
        {
            var mat = EquipmentRepairSystem.CreateRepairMaterial(PlayerInventory.ItemCategory.Tool);
            Assert.IsNotNull(mat);
            StringAssert.Contains("나무", mat.displayName);
        }

        // ===================== InventoryWindow 수리 버튼 =====================

        [Test]
        public void InventoryWindow_HasFindAndSelectSlot()
        {
            var method = typeof(UI.InventoryWindow).GetMethod("FindAndSelectSlot",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(method, "InventoryWindow에 FindAndSelectSlot 메서드가 있어야 합니다");
        }

        // ===================== GetRepairCost 연동 =====================

        [Test]
        public void GetRepairCost_MatchesRepairNeeds()
        {
            var slot = new PlayerInventory.ItemSlot
            {
                item = PlayerInventory.SwordWood,
                count = 1,
                currentDurability = 0
            };

            int cost = EquipmentDurabilitySystem.GetRepairCost(slot);
            Assert.Greater(cost, 0, "파괴된 무기 수리 비용 > 0");
            Assert.LessOrEqual(cost, 10, "기본 무기 수리 비용 = 10 이하");
        }
    }
}