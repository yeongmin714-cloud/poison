using System;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C8-33: GasPotionLoader EditMode 테스트
    ///
    /// 테스트 대상:
    /// - LoadPotion (인벤토리 → 분사기 물약 장전)
    /// - UnloadPotion (분사기 → 인벤토리 물약 반환)
    /// - GetLoadedPotionCount (장전 개수 확인)
    /// - CanLoadPotion / CanUnloadPotion
    /// - 카테고리 제한 (Potion/Herb/Drug만 허용)
    /// </summary>
    public class GasPotionLoaderTests
    {
        private GameObject _inventoryGo;
        private GameObject _controllerGo;

        // ===== 헬퍼 아이템 =====

        private static PlayerInventory.ItemData HerbRedItem => (PlayerInventory.ItemData)Activator.CreateInstance(typeof(PlayerInventory.ItemData),
            nonPublic: true) ?? new PlayerInventory.ItemData
        {
            id = "herb_red",
            displayName = "치유초",
            description = "빨간 약초",
            category = PlayerInventory.ItemCategory.Herb,
            maxStack = 20
        };

        private static PlayerInventory.ItemData HerbGreenItem => new PlayerInventory.ItemData
        {
            id = "herb_green",
            displayName = "피어리",
            description = "초록 약초",
            category = PlayerInventory.ItemCategory.Herb,
            maxStack = 20
        };

        private static PlayerInventory.ItemData MeatItem => new PlayerInventory.ItemData
        {
            id = "meat_rabbit",
            displayName = "토끼고기",
            description = "고기",
            category = PlayerInventory.ItemCategory.Meat,
            maxStack = 20
        };

        private static PlayerInventory.ItemData DrugItem => new PlayerInventory.ItemData
        {
            id = "drug_test",
            displayName = "테스트 마약",
            description = "마약 테스트",
            category = PlayerInventory.ItemCategory.Drug,
            maxStack = 10
        };

        // ===== Setup / Teardown =====

        [SetUp]
        public void Setup()
        {
            // Create PlayerInventory
            _inventoryGo = new GameObject("TestPlayerInventory");
            var inv = _inventoryGo.AddComponent<PlayerInventory>();

            // Manually initialize slots and set Instance
            var slotsField = typeof(PlayerInventory).GetField("_slots",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (slotsField != null)
                slotsField.SetValue(inv, new PlayerInventory.ItemSlot[40]);

            SetInventoryInstance(inv);

            // Create GasSprayerController
            _controllerGo = new GameObject("TestGasSprayerController");
            var controller = _controllerGo.AddComponent<GasSprayerController>();

            SetControllerInstance(controller);
        }

        [TearDown]
        public void Teardown()
        {
            if (_controllerGo != null)
                UnityEngine.Object.DestroyImmediate(_controllerGo);
            if (_inventoryGo != null)
                UnityEngine.Object.DestroyImmediate(_inventoryGo);
            ClearInventoryInstance();
            ClearControllerInstance();
        }

        // ================================================================
        // 테스트 1: LoadPotion_ValidHerb_LoadsSuccessfully
        // ================================================================

        [Test]
        public void LoadPotion_ValidHerb_LoadsSuccessfully()
        {
            // Arrange
            PlayerInventory.Instance.AddItem(HerbRedItem, 5);
            var controller = GasSprayerController.Instance;
            controller.Equip(GasSprayerGrade.Wood);  // 장착

            // Act
            int loaded = GasPotionLoader.LoadPotion(controller, "herb_red");

            // Assert
            Assert.AreEqual(5, loaded, "5개의 치유초가 장전되어야 함");
            Assert.AreEqual("herb_red", controller.LoadedPotionId, "LoadedPotionId가 herb_red여야 함");
            Assert.AreEqual(5, controller.LoadedPotionCount, "LoadedPotionCount가 5여야 함");
            Assert.AreEqual(0, PlayerInventory.Instance.GetItemCount("herb_red"), "인벤토리에서 아이템이 제거되어야 함");
        }

        // ================================================================
        // 테스트 2: LoadPotion_NotEquipped_ReturnsZero
        // ================================================================

        [Test]
        public void LoadPotion_NotEquipped_ReturnsZero()
        {
            // Arrange
            PlayerInventory.Instance.AddItem(HerbRedItem, 5);
            var controller = GasSprayerController.Instance;
            // 장착하지 않음

            // Act
            int loaded = GasPotionLoader.LoadPotion(controller, "herb_red");

            // Assert
            Assert.AreEqual(0, loaded, "장착되지 않았으면 0 반환");
            Assert.AreEqual("", controller.LoadedPotionId, "LoadedPotionId가 비어있어야 함");
            Assert.AreEqual(5, PlayerInventory.Instance.GetItemCount("herb_red"), "인벤토리에 아이템이 그대로 있어야 함");
        }

        // ================================================================
        // 테스트 3: LoadPotion_ItemNotInInventory_ReturnsZero
        // ================================================================

        [Test]
        public void LoadPotion_ItemNotInInventory_ReturnsZero()
        {
            // Arrange
            var controller = GasSprayerController.Instance;
            controller.Equip(GasSprayerGrade.Wood);
            // 인벤토리에 아이템 없음

            // Act
            int loaded = GasPotionLoader.LoadPotion(controller, "herb_red");

            // Assert
            Assert.AreEqual(0, loaded, "인벤토리에 없으면 0 반환");
        }

        // ================================================================
        // 테스트 4: LoadPotion_NonPotionCategory_ReturnsZero
        // ================================================================

        [Test]
        public void LoadPotion_NonPotionCategory_ReturnsZero()
        {
            // Arrange
            PlayerInventory.Instance.AddItem(MeatItem, 3);
            var controller = GasSprayerController.Instance;
            controller.Equip(GasSprayerGrade.Wood);

            // Act
            int loaded = GasPotionLoader.LoadPotion(controller, "meat_rabbit");

            // Assert
            Assert.AreEqual(0, loaded, "Meat 카테고리는 장전 불가");
            Assert.AreEqual("", controller.LoadedPotionId, "LoadedPotionId가 비어있어야 함");
            Assert.AreEqual(3, PlayerInventory.Instance.GetItemCount("meat_rabbit"), "인벤토리에 아이템이 그대로 있어야 함");
        }

        // ================================================================
        // 테스트 5: LoadPotion_DrugCategory_LoadsSuccessfully
        // ================================================================

        [Test]
        public void LoadPotion_DrugCategory_LoadsSuccessfully()
        {
            // Arrange
            PlayerInventory.Instance.AddItem(DrugItem, 2);
            var controller = GasSprayerController.Instance;
            controller.Equip(GasSprayerGrade.Wood);

            // Act
            int loaded = GasPotionLoader.LoadPotion(controller, "drug_test");

            // Assert
            Assert.AreEqual(2, loaded, "Drug 카테고리도 장전 가능");
            Assert.AreEqual("drug_test", controller.LoadedPotionId);
            Assert.AreEqual(2, controller.LoadedPotionCount);
        }

        // ================================================================
        // 테스트 6: LoadPotion_AlreadyHasDifferentPotion_ReturnsZero
        // ================================================================

        [Test]
        public void LoadPotion_AlreadyHasDifferentPotion_ReturnsZero()
        {
            // Arrange
            PlayerInventory.Instance.AddItem(HerbRedItem, 5);
            PlayerInventory.Instance.AddItem(HerbGreenItem, 3);
            var controller = GasSprayerController.Instance;
            controller.Equip(GasSprayerGrade.Wood);

            // Act - 첫 번째 물약 로드
            GasPotionLoader.LoadPotion(controller, "herb_red");
            Assert.AreEqual(5, controller.LoadedPotionCount, "첫 번째 로드 성공");

            // Act - 다른 물약 로드 시도
            int loaded = GasPotionLoader.LoadPotion(controller, "herb_green");

            // Assert
            Assert.AreEqual(0, loaded, "다른 물약 장전 시도는 0 반환");
            Assert.AreEqual("herb_red", controller.LoadedPotionId, "기존 물약 유지");
            Assert.AreEqual(3, PlayerInventory.Instance.GetItemCount("herb_green"), "인벤토리에 아이템 유지");
        }

        // ================================================================
        // 테스트 7: LoadPotion_AddMoreToExisting_Accumulates
        // ================================================================

        [Test]
        public void LoadPotion_AddMoreToExisting_Accumulates()
        {
            // Arrange
            PlayerInventory.Instance.AddItem(HerbRedItem, 3);
            var controller = GasSprayerController.Instance;
            controller.Equip(GasSprayerGrade.Wood);

            // Act - 첫 번째 로드
            GasPotionLoader.LoadPotion(controller, "herb_red");
            Assert.AreEqual(3, controller.LoadedPotionCount);

            // 추가 아이템 인벤토리에 넣고 다시 로드
            PlayerInventory.Instance.AddItem(HerbRedItem, 2);
            int loaded = GasPotionLoader.LoadPotion(controller, "herb_red");

            // Assert
            Assert.AreEqual(2, loaded, "추가로 2개 더 로드됨");
            Assert.AreEqual(5, controller.LoadedPotionCount, "총 5개로 누적");
        }

        // ================================================================
        // 테스트 8: UnloadPotion_ReturnsLoadedPotionToInventory
        // ================================================================

        [Test]
        public void UnloadPotion_ReturnsLoadedPotionToInventory()
        {
            // Arrange
            PlayerInventory.Instance.AddItem(HerbRedItem, 5);
            var controller = GasSprayerController.Instance;
            controller.Equip(GasSprayerGrade.Wood);
            GasPotionLoader.LoadPotion(controller, "herb_red");
            Assert.AreEqual(0, PlayerInventory.Instance.GetItemCount("herb_red"), "장전 후 인벤토리에서 제거됨");

            // Act
            int unloaded = GasPotionLoader.UnloadPotion(controller);

            // Assert
            Assert.AreEqual(5, unloaded, "5개 반환");
            Assert.AreEqual("", controller.LoadedPotionId, "물약 ID 초기화");
            Assert.AreEqual(0, controller.LoadedPotionCount, "개수 초기화");
            Assert.AreEqual(5, PlayerInventory.Instance.GetItemCount("herb_red"), "인벤토리에 5개 반환됨");
        }

        // ================================================================
        // 테스트 9: UnloadPotion_NoPotionLoaded_ReturnsZero
        // ================================================================

        [Test]
        public void UnloadPotion_NoPotionLoaded_ReturnsZero()
        {
            // Arrange
            var controller = GasSprayerController.Instance;
            controller.Equip(GasSprayerGrade.Wood);

            // Act
            int unloaded = GasPotionLoader.UnloadPotion(controller);

            // Assert
            Assert.AreEqual(0, unloaded, "장전된 물약이 없으면 0 반환");
        }

        // ================================================================
        // 테스트 10: UnloadPotion_Unequipped_ReturnsZero
        // ================================================================

        [Test]
        public void UnloadPotion_NotEquipped_ReturnsZero()
        {
            // Arrange
            PlayerInventory.Instance.AddItem(HerbRedItem, 5);
            var controller = GasSprayerController.Instance;
            controller.Equip(GasSprayerGrade.Wood);
            GasPotionLoader.LoadPotion(controller, "herb_red");
            controller.Unequip();  // 해제 시 물약 정보는 초기화됨

            // Act - 이미 해제되어 물약 정보 없음
            int unloaded = GasPotionLoader.UnloadPotion(controller);

            // Assert
            Assert.AreEqual(0, unloaded, "장착이 해제되었으므로 0 반환");
        }

        // ================================================================
        // 테스트 11: CanLoadPotion_ValidConditions_ReturnsTrue
        // ================================================================

        [Test]
        public void CanLoadPotion_ValidConditions_ReturnsTrue()
        {
            // Arrange
            PlayerInventory.Instance.AddItem(HerbRedItem, 3);
            var controller = GasSprayerController.Instance;
            controller.Equip(GasSprayerGrade.Wood);

            // Act
            bool canLoad = GasPotionLoader.CanLoadPotion(controller, "herb_red");

            // Assert
            Assert.IsTrue(canLoad, "분사기 장착 + 인벤토리에 아이템 있음 + Herb 카테고리 = true");
        }

        // ================================================================
        // 테스트 12: CanLoadPotion_NotEquipped_ReturnsFalse
        // ================================================================

        [Test]
        public void CanLoadPotion_NotEquipped_ReturnsFalse()
        {
            // Arrange
            PlayerInventory.Instance.AddItem(HerbRedItem, 3);
            var controller = GasSprayerController.Instance;

            // Act
            bool canLoad = GasPotionLoader.CanLoadPotion(controller, "herb_red");

            // Assert
            Assert.IsFalse(canLoad, "장착되지 않으면 false");
        }

        // ================================================================
        // 테스트 13: CanLoadPotion_NonPotionCategory_ReturnsFalse
        // ================================================================

        [Test]
        public void CanLoadPotion_NonPotionCategory_ReturnsFalse()
        {
            // Arrange
            PlayerInventory.Instance.AddItem(MeatItem, 3);
            var controller = GasSprayerController.Instance;
            controller.Equip(GasSprayerGrade.Wood);

            // Act
            bool canLoad = GasPotionLoader.CanLoadPotion(controller, "meat_rabbit");

            // Assert
            Assert.IsFalse(canLoad, "Meat 카테고리는 false");
        }

        // ================================================================
        // 테스트 14: CanUnloadPotion_PotionLoaded_ReturnsTrue
        // ================================================================

        [Test]
        public void CanUnloadPotion_PotionLoaded_ReturnsTrue()
        {
            // Arrange
            PlayerInventory.Instance.AddItem(HerbRedItem, 5);
            var controller = GasSprayerController.Instance;
            controller.Equip(GasSprayerGrade.Wood);
            GasPotionLoader.LoadPotion(controller, "herb_red");

            // Act
            bool canUnload = GasPotionLoader.CanUnloadPotion(controller);

            // Assert
            Assert.IsTrue(canUnload, "물약이 장전되어 있으면 true");
        }

        // ================================================================
        // 테스트 15: CanUnloadPotion_NoPotion_ReturnsFalse
        // ================================================================

        [Test]
        public void CanUnloadPotion_NoPotion_ReturnsFalse()
        {
            // Arrange
            var controller = GasSprayerController.Instance;
            controller.Equip(GasSprayerGrade.Wood);

            // Act
            bool canUnload = GasPotionLoader.CanUnloadPotion(controller);

            // Assert
            Assert.IsFalse(canUnload, "장전된 물약이 없으면 false");
        }

        // ================================================================
        // 헬퍼 메서드
        // ================================================================

        private void ClearInventoryInstance()
        {
            var field = typeof(PlayerInventory).GetField("Instance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(null, null);
        }

        private void SetInventoryInstance(PlayerInventory instance)
        {
            var field = typeof(PlayerInventory).GetField("Instance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(null, instance);
        }

        private void ClearControllerInstance()
        {
            var field = typeof(GasSprayerController).GetField("Instance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(null, null);
        }

        private void SetControllerInstance(GasSprayerController instance)
        {
            var field = typeof(GasSprayerController).GetField("Instance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(null, instance);
        }
    }
}
