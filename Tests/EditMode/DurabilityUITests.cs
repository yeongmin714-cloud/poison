using NUnit.Framework;
using ProjectName.UI;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C9-18 장비 내구도 UI 테스트
    /// </summary>
    public class DurabilityUITests
    {
        // ===================== InventoryWindow 탭 =====================

        [Test]
        public void InventoryWindow_HasWeaponTab()
        {
            // InventoryWindow에 Weapon/Armor/Tool 카테고리가 있는지 확인
            Assert.IsTrue(System.Enum.IsDefined(typeof(PlayerInventory.ItemCategory), PlayerInventory.ItemCategory.Weapon),
                "ItemCategory에 Weapon이 있어야 합니다");
            Assert.IsTrue(System.Enum.IsDefined(typeof(PlayerInventory.ItemCategory), PlayerInventory.ItemCategory.Armor),
                "ItemCategory에 Armor가 있어야 합니다");
            Assert.IsTrue(System.Enum.IsDefined(typeof(PlayerInventory.ItemCategory), PlayerInventory.ItemCategory.Tool),
                "ItemCategory에 Tool이 있어야 합니다");
        }

        // ===================== GetCategoryColor =====================

        [Test]
        public void InventoryWindow_GetCategoryColor_Weapon_ReturnsRed()
        {
            var go = new GameObject("TestInv");
            var inv = go.AddComponent<InventoryWindow>();

            var method = typeof(InventoryWindow).GetMethod("GetCategoryColor",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var color = (Color)method.Invoke(inv, new object[] { PlayerInventory.ItemCategory.Weapon });
            Assert.Greater(color.r, 0.5f, "무기 색상은 빨간색 계열");
            Assert.Less(color.g, 0.5f, "무기 색상은 빨간색 계열");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void InventoryWindow_GetCategoryColor_Armor_ReturnsBlue()
        {
            var go = new GameObject("TestInv");
            var inv = go.AddComponent<InventoryWindow>();

            var method = typeof(InventoryWindow).GetMethod("GetCategoryColor",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var color = (Color)method.Invoke(inv, new object[] { PlayerInventory.ItemCategory.Armor });
            Assert.Greater(color.b, 0.5f, "방어구 색상은 파란색 계열");

            Object.DestroyImmediate(go);
        }

        // ===================== 내구도 색상 =====================

        [Test]
        public void DurabilityBar_Green_WhenAbove60()
        {
            var slot = new PlayerInventory.ItemSlot
            {
                item = PlayerInventory.SwordWood,
                count = 1,
                currentDurability = 15
            };
            float ratio = EquipmentDurabilitySystem.GetDurabilityRatio(slot);
            Assert.GreaterOrEqual(ratio, 0.6f);
            Assert.AreEqual("🟢", EquipmentDurabilitySystem.GetDurabilityColorTag(slot));
        }

        [Test]
        public void DurabilityBar_Red_WhenBelow30()
        {
            var slot = new PlayerInventory.ItemSlot
            {
                item = PlayerInventory.SwordWood,
                count = 1,
                currentDurability = 3
            };
            Assert.AreEqual("🔴", EquipmentDurabilitySystem.GetDurabilityColorTag(slot));
        }

        // ===================== GetDurabilityString =====================

        [Test]
        public void GetDurabilityString_ShowsCorrectFormat()
        {
            var slot = new PlayerInventory.ItemSlot
            {
                item = PlayerInventory.SwordWood,
                count = 1,
                currentDurability = 10
            };

            string str = EquipmentDurabilitySystem.GetDurabilityString(slot);
            Assert.IsTrue(str.Contains("10/20"), "내구도 문자열에 current/max 포함");
        }

        [Test]
        public void GetDurabilityString_NoDurability_ReturnsInfinity()
        {
            var slot = new PlayerInventory.ItemSlot
            {
                item = PlayerInventory.Herb_Red,
                count = 5,
                currentDurability = 0
            };

            string str = EquipmentDurabilitySystem.GetDurabilityString(slot);
            Assert.AreEqual("∞", str, "내구도 없는 아이템은 ∞ 표시");
        }

        // ===================== InventoryWindow Slot 업데이트 =====================

        [Test]
        public void InventoryWindow_RefreshInventory_Works()
        {
            var go = new GameObject("TestInv");
            var inv = go.AddComponent<InventoryWindow>();
            inv.Toggle(); // 열기

            var method = typeof(InventoryWindow).GetMethod("RefreshInventory",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(method, "RefreshInventory 메서드가 있어야 합니다");

            // PlayerInventory.Instance가 없으면 예외 없이 실행만 확인
            method.Invoke(inv, null);

            inv.Toggle(); // 닫기
            Object.DestroyImmediate(go);
        }
    }
}