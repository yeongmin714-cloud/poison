using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;
using ProjectName.UI;
using ProjectName.Core;
using System.Collections.Generic;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C9-07 떠돌이 상인 테스트
    /// </summary>
    public class WanderingMerchantTests
    {
        // ===================== 기본 타입 확인 =====================

        [Test]
        public void WanderingMerchant_Type_Exists()
        {
            Assert.IsNotNull(typeof(WanderingMerchant), "WanderingMerchant 타입이 존재해야 합니다");
        }

        [Test]
        public void WanderingMerchant_IsMonoBehaviour()
        {
            Assert.IsTrue(typeof(WanderingMerchant).IsSubclassOf(typeof(MonoBehaviour)),
                "WanderingMerchant는 MonoBehaviour를 상속해야 합니다");
        }

        // ===================== 필드 확인 =====================

        [Test]
        public void WanderingMerchant_HasVisitFields()
        {
            var type = typeof(WanderingMerchant);
            // 상호작용 범위
            var interactField = type.GetField("_interactRange",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(interactField, "_interactRange 필드가 있어야 합니다");

            // 체류 시간
            var durField = type.GetField("_visitDuration",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(durField, "_visitDuration 필드가 있어야 합니다");

            // 방문 간격
            var minField = type.GetField("_minInterval",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(minField, "_minInterval 필드가 있어야 합니다");

            var maxField = type.GetField("_maxInterval",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(maxField, "_maxInterval 필드가 있어야 합니다");
        }

        // ===================== 인스턴스화 테스트 =====================

        [Test]
        public void WanderingMerchant_CanInstantiate()
        {
            var go = new GameObject("TestMerchant");
            var merchant = go.AddComponent<WanderingMerchant>();
            Assert.IsNotNull(merchant, "WanderingMerchant 인스턴스가 생성되어야 합니다");

            // 초기에는 비활성화 상태
            Assert.IsFalse(go.activeInHierarchy, "초기에는 비활성화 상태여야 합니다");

            Object.DestroyImmediate(go);
        }

        // ===================== 희귀 재고 생성 테스트 =====================

        [Test]
        public void WanderingMerchant_GenerateRareInventory_HasItems()
        {
            var go = new GameObject("TestMerchant");
            var merchant = go.AddComponent<WanderingMerchant>();

            // GenerateRareInventory는 private 메서드
            var method = typeof(WanderingMerchant).GetMethod("GenerateRareInventory",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(method, "GenerateRareInventory 메서드가 있어야 합니다");

            var inventory = method.Invoke(merchant, null) as List<ShopWindow.ShopItem>;
            Assert.IsNotNull(inventory, "GenerateRareInventory는 List<ShopItem>을 반환해야 합니다");
            Assert.GreaterOrEqual(inventory.Count, 8, "최소 8개 이상의 희귀 아이템이 있어야 합니다");

            // 모든 아이템이 희귀 등급인지 확인
            foreach (var item in inventory)
            {
                Assert.IsTrue(item.isRare, $"모든 떠돌이 상인 아이템은 희귀 등급이어야 합니다: {item.item.displayName}");
                Assert.Greater(item.price, 0, $"아이템 '{item.item.displayName}'의 가격이 0보다 커야 합니다");
                Assert.Greater(item.stock, 0, $"아이템 '{item.item.displayName}'의 재고가 0보다 커야 합니다");
            }

            Object.DestroyImmediate(go);
        }

        [Test]
        public void WanderingMerchant_Inventory_HasRareItems()
        {
            var go = new GameObject("TestMerchant");
            var merchant = go.AddComponent<WanderingMerchant>();

            var method = typeof(WanderingMerchant).GetMethod("GenerateRareInventory",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var inventory = method.Invoke(merchant, null) as List<ShopWindow.ShopItem>;

            // 각 항목별 존재 확인
            var names = new HashSet<string>();
            foreach (var item in inventory)
            {
                names.Add(item.item.displayName);
            }

            Assert.IsTrue(names.Contains("불굴의 물약"), "불굴의 물약이 재고에 있어야 합니다");
            Assert.IsTrue(names.Contains("희귀 보석"), "희귀 보석이 재고에 있어야 합니다");

            Object.DestroyImmediate(go);
        }

        // ===================== 상태 프로퍼티 테스트 =====================

        [Test]
        public void WanderingMerchant_DefaultState()
        {
            var go = new GameObject("TestMerchant");
            var merchant = go.AddComponent<WanderingMerchant>();

            Assert.IsFalse(merchant.IsVisiting, "초기 방문 상태는 false여야 합니다");
            Assert.AreEqual(0f, merchant.RemainingTime, "초기 남은 시간은 0이어야 합니다");

            Object.DestroyImmediate(go);
        }

        // ===================== ShopWindow 연동 테스트 =====================

        [Test]
        public void WanderingMerchant_CreateShopWindow_Works()
        {
            var go = new GameObject("TestMerchant");
            var merchant = go.AddComponent<WanderingMerchant>();

            var method = typeof(WanderingMerchant).GetMethod("CreateShopWindow",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(method, "CreateShopWindow 메서드가 있어야 합니다");

            // ShopWindow 인스턴스 생성 확인 (UIManager 없이도 생성 가능)
            method.Invoke(merchant, null);

            // shopWindowInstance 필드 확인
            var instanceField = typeof(WanderingMerchant).GetField("_shopWindowInstance",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var shopWindowField = typeof(WanderingMerchant).GetField("_shopWindow",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var instance = instanceField.GetValue(merchant) as GameObject;
            var shopWindow = shopWindowField.GetValue(merchant) as ShopWindow;

            Assert.IsNotNull(instance, "shopWindowInstance가 생성되어야 합니다");
            Assert.IsNotNull(shopWindow, "shopWindow가 생성되어야 합니다");
            Assert.IsInstanceOf<ShopWindow>(shopWindow, "shopWindow는 ShopWindow 타입이어야 합니다");

            Object.DestroyImmediate(go);
        }

        // ===================== 방문 타이머 테스트 =====================

        [Test]
        public void WanderingMerchant_HasTimers()
        {
            var go = new GameObject("TestMerchant");
            var merchant = go.AddComponent<WanderingMerchant>();

            // private 필드 접근
            var nextVisitField = typeof(WanderingMerchant).GetField("_nextVisitTime",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var isVisitingField = typeof(WanderingMerchant).GetField("_isVisiting",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            float nextTime = (float)nextVisitField.GetValue(merchant);
            Assert.Greater(nextTime, 0, "_nextVisitTime이 0보다 커야 합니다");

            bool isVisiting = (bool)isVisitingField.GetValue(merchant);
            Assert.IsFalse(isVisiting, "초기에는 방문 중이 아니어야 함");

            Object.DestroyImmediate(go);
        }
    }
}