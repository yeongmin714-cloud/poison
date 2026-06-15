using NUnit.Framework;
using UnityEngine;
using ProjectName.UI;
using ProjectName.Systems;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C9-05 상점 시스템 테스트
    /// </summary>
    public class ShopWindowTests
    {
        // ===================== ShopWindow 기본 테스트 =====================

        [Test]
        public void ShopWindow_Type_Exists()
        {
            Assert.IsNotNull(typeof(ShopWindow), "ShopWindow 타입이 존재해야 합니다");
        }

        [Test]
        public void ShopWindow_InheritsUIWindow()
        {
            Assert.IsTrue(typeof(ShopWindow).IsSubclassOf(typeof(UIWindow)),
                "ShopWindow는 UIWindow를 상속해야 합니다");
        }

        // ===================== ShopItem 구조 테스트 =====================

        [Test]
        public void ShopItem_HasRequiredFields()
        {
            var fields = typeof(ShopWindow.ShopItem).GetFields();
            var names = new System.Collections.Generic.HashSet<string>();
            foreach (var f in fields) names.Add(f.Name);

            Assert.IsTrue(names.Contains("item"), "ShopItem에 item 필드 필요");
            Assert.IsTrue(names.Contains("price"), "ShopItem에 price 필드 필요");
            Assert.IsTrue(names.Contains("stock"), "ShopItem에 stock 필드 필요");
            Assert.IsTrue(names.Contains("isRare"), "ShopItem에 isRare 필드 필요");
        }

        // ===================== 상점 초기화 테스트 =====================

        [Test]
        public void ShopWindow_InitializeShopInventory_HasItems()
        {
            var go = new GameObject("TestShopWindow");
            var shop = go.AddComponent<ShopWindow>();

            // InitializeShopInventory 호출 (Awake에서 자동 호출)
            shop.InitializeShopInventory();

            // _shopInventory 접근 (리플렉션)
            var invField = typeof(ShopWindow).GetField("_shopInventory",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(invField, "_shopInventory 필드가 있어야 합니다");

            var inventory = invField.GetValue(shop) as System.Collections.Generic.List<ShopWindow.ShopItem>;
            Assert.IsNotNull(inventory, "_shopInventory가 List<ShopItem>여야 합니다");
            Assert.GreaterOrEqual(inventory.Count, 8, "최소 8개 이상의 아이템이 초기화되어야 합니다");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ShopWindow_ShopItems_HavePrices()
        {
            var go = new GameObject("TestShopWindow");
            var shop = go.AddComponent<ShopWindow>();

            var invField = typeof(ShopWindow).GetField("_shopInventory",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var inventory = invField.GetValue(shop) as System.Collections.Generic.List<ShopWindow.ShopItem>;

            foreach (var item in inventory)
            {
                Assert.Greater(item.price, 0, $"아이템 '{item.item.displayName}'의 가격이 0보다 커야 합니다");
            }

            Object.DestroyImmediate(go);
        }

        // ===================== 구매/판매 가격 테스트 =====================

        [Test]
        public void ShopWindow_CalculateSellPrice()
        {
            var go = new GameObject("TestShopWindow");
            var shop = go.AddComponent<ShopWindow>();

            var method = typeof(ShopWindow).GetMethod("CalculateSellPrice",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(method, "CalculateSellPrice 메서드가 있어야 합니다");

            // 재료 아이템
            var matItem = new PlayerInventory.ItemData
            {
                id = "test_mat",
                displayName = "테스트 재료",
                category = PlayerInventory.ItemCategory.Material
            };
            int matPrice = (int)method.Invoke(shop, new object[] { matItem });
            Assert.AreEqual(5, matPrice, "재료 판매가는 5G");

            // 포션 아이템
            var potItem = new PlayerInventory.ItemData
            {
                id = "test_pot",
                displayName = "테스트 포션",
                category = PlayerInventory.ItemCategory.Potion
            };
            int potPrice = (int)method.Invoke(shop, new object[] { potItem });
            Assert.AreEqual(15, potPrice, "포션 판매가는 15G");

            Object.DestroyImmediate(go);
        }

        // ===================== ShopPlaceholder 테스트 =====================

        [Test]
        public void ShopPlaceholder_Type_Exists()
        {
            Assert.IsNotNull(typeof(ShopPlaceholder), "ShopPlaceholder 타입이 존재해야 합니다");
        }

        [Test]
        public void ShopPlaceholder_HasInteractRange()
        {
            var field = typeof(ShopPlaceholder).GetField("_interactRange",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, "ShopPlaceholder에 _interactRange 필드가 있어야 합니다");
            Assert.AreEqual(typeof(float), field.FieldType, "_interactRange는 float 타입");
        }

        // ===================== BuildingPlaceholder 상점 연결 =====================

        [Test]
        public void BuildingPlaceholder_SupportsShopType()
        {
            BuildingPlaceholder.BuildingType shopType = BuildingPlaceholder.BuildingType.Shop;
            Assert.AreEqual(0, (int)shopType, "BuildingType.Shop = 0이어야 합니다");
        }

        // ===================== UIWindow 기반 기능 =====================

        [Test]
        public void ShopWindow_IsInitiallyClosed()
        {
            var go = new GameObject("TestShopWindow");
            var shop = go.AddComponent<ShopWindow>();

            // 기본적으로 닫혀 있어야 함
            Assert.IsFalse(shop.IsOpen, "ShopWindow는 초기에 닫혀 있어야 합니다");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ShopWindow_CanToggle()
        {
            var go = new GameObject("TestShopWindow");
            var shop = go.AddComponent<ShopWindow>();

            shop.Toggle();
            Assert.IsTrue(shop.IsOpen, "Toggle 후 ShopWindow가 열려야 합니다");

            shop.Toggle();
            Assert.IsFalse(shop.IsOpen, "Toggle 후 ShopWindow가 닫혀야 합니다");

            Object.DestroyImmediate(go);
        }
    }
}