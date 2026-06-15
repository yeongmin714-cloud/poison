using NUnit.Framework;
using ProjectName.UI;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C9-06 상점 기본 아이템 (무기/방어구/도구) 테스트
    /// </summary>
    public class ShopItemTests
    {
        // ===================== 새로운 ItemCategory 값 확인 =====================

        [Test]
        public void ItemCategory_HasNewTypes()
        {
            Assert.AreEqual(7, (int)PlayerInventory.ItemCategory.Weapon, "Weapon = 7");
            Assert.AreEqual(8, (int)PlayerInventory.ItemCategory.Armor, "Armor = 8");
            Assert.AreEqual(9, (int)PlayerInventory.ItemCategory.Tool, "Tool = 9");
        }

        // ===================== 무기 ItemData 확인 =====================

        [Test]
        public void SwordWood_ItemData_Exists()
        {
            Assert.IsNotNull(PlayerInventory.SwordWood, "SwordWood ItemData가 존재해야 합니다");
            Assert.AreEqual("목검", PlayerInventory.SwordWood.displayName);
            Assert.AreEqual(PlayerInventory.ItemCategory.Weapon, PlayerInventory.SwordWood.category);
        }

        [Test]
        public void SpearWood_ItemData_Exists()
        {
            Assert.IsNotNull(PlayerInventory.SpearWood, "SpearWood ItemData가 존재해야 합니다");
            Assert.AreEqual(PlayerInventory.ItemCategory.Weapon, PlayerInventory.SpearWood.category);
        }

        [Test]
        public void BowWood_ItemData_Exists()
        {
            Assert.IsNotNull(PlayerInventory.BowWood, "BowWood ItemData가 존재해야 합니다");
        }

        // ===================== 방어구 ItemData 확인 =====================

        [Test]
        public void ArmorItems_Exist()
        {
            Assert.IsNotNull(PlayerInventory.ClothArmor, "ClothArmor ItemData가 존재해야 합니다");
            Assert.AreEqual(PlayerInventory.ItemCategory.Armor, PlayerInventory.ClothArmor.category);
            Assert.AreEqual("천 옷", PlayerInventory.ClothArmor.displayName);

            Assert.IsNotNull(PlayerInventory.LeatherArmor, "LeatherArmor ItemData가 존재해야 합니다");
            Assert.AreEqual(PlayerInventory.ItemCategory.Armor, PlayerInventory.LeatherArmor.category);
            Assert.AreEqual("가죽 갑옷", PlayerInventory.LeatherArmor.displayName);
        }

        // ===================== 도구 ItemData 확인 =====================

        [Test]
        public void ToolItems_Exist()
        {
            Assert.IsNotNull(PlayerInventory.Pickaxe, "Pickaxe ItemData가 존재해야 합니다");
            Assert.AreEqual(PlayerInventory.ItemCategory.Tool, PlayerInventory.Pickaxe.category);
            Assert.AreEqual("곡괭이", PlayerInventory.Pickaxe.displayName);

            Assert.IsNotNull(PlayerInventory.Axe, "Axe ItemData가 존재해야 합니다");
            Assert.AreEqual(PlayerInventory.ItemCategory.Tool, PlayerInventory.Axe.category);

            Assert.IsNotNull(PlayerInventory.FishingRod, "FishingRod ItemData가 존재해야 합니다");
            Assert.AreEqual(PlayerInventory.ItemCategory.Tool, PlayerInventory.FishingRod.category);
        }

        // ===================== 무기 maxStack = 1 확인 =====================

        [Test]
        public void WeaponItems_HaveMaxStack1()
        {
            Assert.AreEqual(1, PlayerInventory.SwordWood.maxStack, "무기는 최대 1개까지 소지");
            Assert.AreEqual(1, PlayerInventory.SpearWood.maxStack, "무기는 최대 1개까지 소지");
            Assert.AreEqual(1, PlayerInventory.BowWood.maxStack, "무기는 최대 1개까지 소지");
        }

        // ===================== 상점 재고에 무기/방어구/도구 포함 확인 =====================

        [Test]
        public void ShopWindow_HasWeaponItems()
        {
            var go = new GameObject("TestShopWindow");
            var shop = go.AddComponent<ShopWindow>();

            var invField = typeof(ShopWindow).GetField("_shopInventory",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var inventory = invField.GetValue(shop) as System.Collections.Generic.List<ShopWindow.ShopItem>;

            Assert.IsNotNull(inventory);

            // 무기 아이템 확인
            bool hasSword = false, hasSpear = false, hasBow = false;
            bool hasCloth = false, hasLeather = false;
            bool hasPickaxe = false, hasAxe = false, hasFishingRod = false;

            foreach (var si in inventory)
            {
                if (si.item == PlayerInventory.SwordWood) hasSword = true;
                if (si.item == PlayerInventory.SpearWood) hasSpear = true;
                if (si.item == PlayerInventory.BowWood) hasBow = true;
                if (si.item == PlayerInventory.ClothArmor) hasCloth = true;
                if (si.item == PlayerInventory.LeatherArmor) hasLeather = true;
                if (si.item == PlayerInventory.Pickaxe) hasPickaxe = true;
                if (si.item == PlayerInventory.Axe) hasAxe = true;
                if (si.item == PlayerInventory.FishingRod) hasFishingRod = true;
            }

            Assert.IsTrue(hasSword, "상점에 목검이 있어야 합니다");
            Assert.IsTrue(hasSpear, "상점에 나무 창이 있어야 합니다");
            Assert.IsTrue(hasBow, "상점에 나무 활이 있어야 합니다");
            Assert.IsTrue(hasCloth, "상점에 천 옷이 있어야 합니다");
            Assert.IsTrue(hasLeather, "상점에 가죽 갑옷이 있어야 합니다");
            Assert.IsTrue(hasPickaxe, "상점에 곡괭이가 있어야 합니다");
            Assert.IsTrue(hasAxe, "상점에 도끼가 있어야 합니다");
            Assert.IsTrue(hasFishingRod, "상점에 낚싯대가 있어야 합니다");

            Object.DestroyImmediate(go);
        }

        // ===================== 상점 무기 가격 확인 =====================

        [Test]
        public void ShopWeapon_PricesAreReasonable()
        {
            var go = new GameObject("TestShopWindow");
            var shop = go.AddComponent<ShopWindow>();

            var invField = typeof(ShopWindow).GetField("_shopInventory",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var inventory = invField.GetValue(shop) as System.Collections.Generic.List<ShopWindow.ShopItem>;

            foreach (var si in inventory)
            {
                if (si.item.category == PlayerInventory.ItemCategory.Weapon)
                {
                    Assert.Greater(si.price, 0, $"무기 '{si.item.displayName}' 가격이 0보다 커야 합니다");
                    Assert.GreaterOrEqual(si.price, 50, $"무기 '{si.item.displayName}' 가격은 최소 50G 이상");
                }
                if (si.item.category == PlayerInventory.ItemCategory.Armor)
                {
                    Assert.Greater(si.price, 0, $"방어구 '{si.item.displayName}' 가격이 0보다 커야 합니다");
                }
                if (si.item.category == PlayerInventory.ItemCategory.Tool)
                {
                    Assert.Greater(si.price, 0, $"도구 '{si.item.displayName}' 가격이 0보다 커야 합니다");
                }
            }

            Object.DestroyImmediate(go);
        }

        // ===================== CalculateSellPrice 업데이트 확인 =====================

        [Test]
        public void CalculateSellPrice_IncludesNewCategories()
        {
            var go = new GameObject("TestShopWindow");
            var shop = go.AddComponent<ShopWindow>();

            var method = typeof(ShopWindow).GetMethod("CalculateSellPrice",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // 무기 판매가
            var weapon = new PlayerInventory.ItemData
            {
                id = "test_weapon",
                displayName = "테스트 무기",
                category = PlayerInventory.ItemCategory.Weapon
            };
            int weaponPrice = (int)method.Invoke(shop, new object[] { weapon });
            Assert.AreEqual(30, weaponPrice, "무기 판매가는 30G");

            // 방어구 판매가
            var armor = new PlayerInventory.ItemData
            {
                id = "test_armor",
                displayName = "테스트 방어구",
                category = PlayerInventory.ItemCategory.Armor
            };
            int armorPrice = (int)method.Invoke(shop, new object[] { armor });
            Assert.AreEqual(25, armorPrice, "방어구 판매가는 25G");

            // 도구 판매가
            var tool = new PlayerInventory.ItemData
            {
                id = "test_tool",
                displayName = "테스트 도구",
                category = PlayerInventory.ItemCategory.Tool
            };
            int toolPrice = (int)method.Invoke(shop, new object[] { tool });
            Assert.AreEqual(20, toolPrice, "도구 판매가는 20G");

            Object.DestroyImmediate(go);
        }

        // ===================== 전체 상점 아이템 수 확인 =====================

        [Test]
        public void ShopWindow_TotalItems_AfterC906()
        {
            var go = new GameObject("TestShopWindow");
            var shop = go.AddComponent<ShopWindow>();

            var invField = typeof(ShopWindow).GetField("_shopInventory",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var inventory = invField.GetValue(shop) as System.Collections.Generic.List<ShopWindow.ShopItem>;

            // 기존 8개(약초4+고기1+레시피3+포션2) + 신규 8개(무기3+방어구2+도구3) = 16개
            Assert.GreaterOrEqual(inventory.Count, 16, "C9-06 후 최소 16개 아이템이 있어야 합니다");

            Object.DestroyImmediate(go);
        }
    }
}