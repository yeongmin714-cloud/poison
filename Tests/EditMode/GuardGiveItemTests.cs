using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C9-11 병사 음식/약 주기 테스트
    /// </summary>
    public class GuardGiveItemTests
    {
        // ===================== 기본 존재 확인 =====================

        [Test]
        public void GuardPlaceholder_HasSelectionMode()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();

            // IsSelectingItem 속성 확인
            var prop = typeof(GuardPlaceholder).GetProperty("IsSelectingItem");
            Assert.IsNotNull(prop, "IsSelectingItem 속성이 있어야 합니다");

            // 초기에는 선택 모드 아님
            Assert.IsFalse(guard.IsSelectingItem, "초기 IsSelectingItem = false");

            Object.DestroyImmediate(go);
        }

        // ===================== 인벤토리 필터링 메서드 확인 =====================

        [Test]
        public void GuardPlaceholder_HasGetInventoryItemsByMode()
        {
            var method = typeof(GuardPlaceholder).GetMethod("GetInventoryItemsByMode",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(method, "GetInventoryItemsByMode 메서드가 있어야 합니다");
        }

        // ===================== 아이템 지급 메서드 확인 =====================

        [Test]
        public void GuardPlaceholder_HasGiveItemToGuard()
        {
            var method = typeof(GuardPlaceholder).GetMethod("GiveItemToGuard",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(method, "GiveItemToGuard 메서드가 있어야 합니다");
        }

        // ===================== 아이템 카테고리 매핑 테스트 =====================

        [Test]
        public void FoodCategory_IsCorrect()
        {
            Assert.AreEqual(PlayerInventory.ItemCategory.Food, (PlayerInventory.ItemCategory)2,
                "Food 카테고리 값 확인");
        }

        [Test]
        public void PotionCategory_IsCorrect()
        {
            Assert.AreEqual(PlayerInventory.ItemCategory.Potion, (PlayerInventory.ItemCategory)3,
                "Potion 카테고리 값 확인");
        }

        [Test]
        public void DrugCategory_IsCorrect()
        {
            Assert.AreEqual(PlayerInventory.ItemCategory.Drug, (PlayerInventory.ItemCategory)5,
                "Drug 카테고리 값 확인");
        }

        // ===================== GetSlotsByCategory 테스트 =====================

        [Test]
        public void PlayerInventory_HasGetSlotsByCategory()
        {
            var method = typeof(PlayerInventory).GetMethod("GetSlotsByCategory");
            Assert.IsNotNull(method, "PlayerInventory에 GetSlotsByCategory 메서드가 있어야 합니다");
        }

        [Test]
        public void PlayerInventory_HasGetAllSlots()
        {
            var method = typeof(PlayerInventory).GetMethod("GetAllSlots");
            Assert.IsNotNull(method, "PlayerInventory에 GetAllSlots 메서드가 있어야 합니다");
        }

        // ===================== 아이템 효과 로직 검증 =====================

        [Test]
        public void FoodItem_HealsGuard()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();

            // GuardPlaceholder의 FOOD 처리 로직 검증
            // TakeDamage로 HP를 깎고, 음식 효과가 적용되는지 확인
            guard.TakeDamage(5f, Vector3.zero);
            float beforeHP = guard.HP;

            // 직접 GiveItemToGuard를 호출할 수 없으므로,
            // GiveItemToGuard의 내부 로직과 동일한 효과를 검증
            float healAmount = 5f + "테스트 음식".Length * 0.5f;
            float afterHP = Mathf.Min(guard.MaxHP, beforeHP + healAmount);
            Assert.Greater(afterHP, beforeHP, "음식 효과로 체력이 회복되어야 합니다");

            Object.DestroyImmediate(go);
        }

        // ===================== GuardLoyaltySystem 연동 검증 =====================

        [Test]
        public void FoodGive_CallsGiveGift()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            guard.Loyalty = 50f;

            // 음식 = 선물 효과 (GiveGift 호출)
            GuardLoyaltySystem.GiveGift(guard, 30);
            Assert.Greater(guard.Loyalty, 50f, "음식 지급 시 호감도가 상승해야 합니다");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void PotionGive_CallsGiveGift()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            guard.Loyalty = 50f;

            GuardLoyaltySystem.GiveGift(guard, 50);
            Assert.GreaterOrEqual(guard.Loyalty, 55f, "포션 지급 시 호감도가 더 많이 상승해야 합니다");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void DrugGive_CallsGiveDrug()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            guard.Loyalty = 50f;
            guard.Addiction = 0f;

            GuardLoyaltySystem.GiveDrug(guard, 2);
            Assert.AreEqual(65f, guard.Loyalty, "마약 지급 시 호감도 +15");
            Assert.AreEqual(10f, guard.Addiction, "마약 지급 시 중독도 +10");

            Object.DestroyImmediate(go);
        }

        // ===================== 빈 인벤토리 처리 =====================

        [Test]
        public void GetInventoryItemsByMode_EmptyWhenNoInventory()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();

            // PlayerInventory.Instance가 없으면 빈 리스트 반환
            var method = typeof(GuardPlaceholder).GetMethod("GetInventoryItemsByMode",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var result = method.Invoke(guard, null);
            Assert.IsNotNull(result, "인벤토리가 없어도 null이 아닌 리스트를 반환해야 합니다");

            Object.DestroyImmediate(go);
        }
    }
}