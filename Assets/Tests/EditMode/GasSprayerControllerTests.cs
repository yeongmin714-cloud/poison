using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ProjectName.Core;
using ProjectName.Systems;
using ProjectName.UI;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C8-32: GasSprayerController EditMode 테스트
    ///
    /// 테스트 대상:
    /// - EquipSprayer (장착)
    /// - UnequipSprayer (해제)
    /// - GetCurrentSprayerData
    /// - CanEquipSprayer
    /// - GetSprayerItemId
    /// - OnEquipChanged 이벤트
    /// </summary>
    public class GasSprayerControllerTests
    {
        // ===== 테스트용 ItemData 헬퍼 =====
        private static PlayerInventory.ItemData WoodSprayerItem => new PlayerInventory.ItemData
        {
            id = "GasSprayer_Wood",
            displayName = "나무 가스 분사기",
            description = "나무로 만든 기본 가스 분사기",
            category = PlayerInventory.ItemCategory.Tool,
            maxStack = 1
        };

        private static PlayerInventory.ItemData StoneSprayerItem => new PlayerInventory.ItemData
        {
            id = "GasSprayer_Stone",
            displayName = "돌 가스 분사기",
            description = "돌로 만든 가스 분사기",
            category = PlayerInventory.ItemCategory.Tool,
            maxStack = 1
        };

        private static PlayerInventory.ItemData IronSprayerItem => new PlayerInventory.ItemData
        {
            id = "GasSprayer_Iron",
            displayName = "철 가스 분사기",
            description = "철로 만든 가스 분사기",
            category = PlayerInventory.ItemCategory.Tool,
            maxStack = 1
        };

        private static PlayerInventory.ItemData SomeOtherItem => new PlayerInventory.ItemData
        {
            id = "tool_pickaxe",
            displayName = "곡괭이",
            description = "광석 채굴용 도구",
            category = PlayerInventory.ItemCategory.Tool,
            maxStack = 1
        };

        // ===== Test Setup / Teardown =====

        private GameObject _inventoryGo;
        private GameObject _controllerGo;

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

            // Manually set Instance (bypass Awake's singleton check)
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
        // 테스트 1: EquipSprayer_ValidItem_EquipsSuccessfully
        // ================================================================

        [Test]
        public void EquipSprayer_ValidItem_EquipsSuccessfully()
        {
            // Arrange
            PlayerInventory.Instance.AddItem(WoodSprayerItem, 1);
            var controller = GasSprayerController.Instance;

            // Act
            bool result = controller.EquipSprayer("GasSprayer_Wood");

            // Assert
            Assert.IsTrue(result, "유효한 분사기 아이템 장착은 true 반환");
            Assert.IsTrue(controller.IsEquipped, "장착 후 IsEquipped는 true");
            Assert.AreEqual(GasSprayerGrade.Wood, controller.CurrentGrade, "등급이 Wood여야 함");
            Assert.AreEqual("나무 가스 분사기", controller.EquippedSprayerName, "이름이 일치해야 함");
            Assert.AreEqual(0, PlayerInventory.Instance.GetItemCount("GasSprayer_Wood"), "인벤토리에서 아이템이 제거되어야 함");
        }

        // ================================================================
        // 테스트 2: EquipSprayer_AlreadyEquipped_ReturnsFalse
        // ================================================================

        [Test]
        public void EquipSprayer_AlreadyEquipped_ReturnsFalse()
        {
            // Arrange
            PlayerInventory.Instance.AddItem(WoodSprayerItem, 1);
            PlayerInventory.Instance.AddItem(StoneSprayerItem, 1);
            var controller = GasSprayerController.Instance;
            controller.EquipSprayer("GasSprayer_Wood");

            // Act
            bool result = controller.EquipSprayer("GasSprayer_Stone");

            // Assert
            Assert.IsFalse(result, "이미 장착된 상태에서 추가 장착 시도는 false 반환");
            Assert.IsTrue(controller.IsEquipped, "기존 장착 상태 유지");
            Assert.AreEqual(GasSprayerGrade.Wood, controller.CurrentGrade, "기존 등급 유지");
        }

        // ================================================================
        // 테스트 3: EquipSprayer_ItemNotFound_ReturnsFalse
        // ================================================================

        [Test]
        public void EquipSprayer_ItemNotFound_ReturnsFalse()
        {
            // Arrange
            var controller = GasSprayerController.Instance;
            // 인벤토리에 아이템을 추가하지 않음

            // Act
            bool result = controller.EquipSprayer("GasSprayer_Wood");

            // Assert
            Assert.IsFalse(result, "인벤토리에 아이템이 없으면 false 반환");
            Assert.IsFalse(controller.IsEquipped, "장착되지 않음");
        }

        // ================================================================
        // 테스트 4: UnequipSprayer_WhenEquipped_UnequipsSuccessfully
        // ================================================================

        [Test]
        public void UnequipSprayer_WhenEquipped_UnequipsSuccessfully()
        {
            // Arrange
            PlayerInventory.Instance.AddItem(WoodSprayerItem, 1);
            var controller = GasSprayerController.Instance;
            controller.EquipSprayer("GasSprayer_Wood");

            // Act
            bool result = controller.UnequipSprayer();

            // Assert
            Assert.IsTrue(result, "장착 해제는 true 반환");
            Assert.IsFalse(controller.IsEquipped, "해제 후 IsEquipped는 false");
            Assert.AreEqual("", controller.LoadedPotionId, "물약 정보 초기화");
            Assert.AreEqual(0, controller.LoadedPotionCount, "물약 개수 초기화");
            Assert.AreEqual(0f, controller.CurrentSprayTimeRemaining, "분사 시간 초기화");
        }

        // ================================================================
        // 테스트 5: UnequipSprayer_WhenNotEquipped_ReturnsFalse
        // ================================================================

        [Test]
        public void UnequipSprayer_WhenNotEquipped_ReturnsFalse()
        {
            // Arrange
            var controller = GasSprayerController.Instance;

            // Act
            bool result = controller.UnequipSprayer();

            // Assert
            Assert.IsFalse(result, "장착되지 않은 상태에서 해제 시도는 false 반환");
        }

        // ================================================================
        // 테스트 6: UnequipSprayer_ItemReturnedToInventory
        // ================================================================

        [Test]
        public void UnequipSprayer_ItemReturnedToInventory()
        {
            // Arrange
            PlayerInventory.Instance.AddItem(WoodSprayerItem, 1);
            var controller = GasSprayerController.Instance;
            controller.EquipSprayer("GasSprayer_Wood");
            Assert.AreEqual(0, PlayerInventory.Instance.GetItemCount("GasSprayer_Wood"), "장착 후 인벤토리에서 제거됨");

            // Act
            controller.UnequipSprayer();

            // Assert
            Assert.AreEqual(1, PlayerInventory.Instance.GetItemCount("GasSprayer_Wood"), "해제 후 인벤토리에 반환됨");
        }

        // ================================================================
        // 테스트 7: GetCurrentSprayerData_WhenEquipped_ReturnsCorrectData
        // ================================================================

        [Test]
        public void GetCurrentSprayerData_WhenEquipped_ReturnsCorrectData()
        {
            // Arrange
            PlayerInventory.Instance.AddItem(IronSprayerItem, 1);
            var controller = GasSprayerController.Instance;
            controller.EquipSprayer("GasSprayer_Iron");

            // Act
            var data = controller.GetCurrentSprayerData();

            // Assert
            Assert.AreEqual(GasSprayerGrade.Iron, data.grade, "등급 일치");
            Assert.AreEqual("철 가스 분사기", data.sprayerName, "이름 일치");
            Assert.AreEqual(45f, data.maxSprayTime, "분사 시간 일치");
            Assert.AreEqual(5f, data.sprayRange, "분사 범위 일치");
            Assert.AreEqual("Back", data.equippedSlotName, "슬롯 이름 일치");
        }

        // ================================================================
        // 테스트 8: GetCurrentSprayerData_WhenNotEquipped_ReturnsDefault
        // ================================================================

        [Test]
        public void GetCurrentSprayerData_WhenNotEquipped_ReturnsDefault()
        {
            // Arrange
            var controller = GasSprayerController.Instance;

            // Act
            var data = controller.GetCurrentSprayerData();

            // Assert
            Assert.AreEqual(default(GasSprayerData), data, "장착되지 않으면 default 반환");
            Assert.AreEqual(default(GasSprayerGrade), data.grade, "등급은 기본값");
        }

        // ================================================================
        // 테스트 9: CanEquipSprayer_ItemExists_ReturnsTrue
        // ================================================================

        [Test]
        public void CanEquipSprayer_ItemExists_ReturnsTrue()
        {
            // Arrange
            PlayerInventory.Instance.AddItem(WoodSprayerItem, 1);
            var controller = GasSprayerController.Instance;

            // Act
            bool result = controller.CanEquipSprayer("GasSprayer_Wood");

            // Assert
            Assert.IsTrue(result, "인벤토리에 아이템이 있으면 true 반환");
        }

        // ================================================================
        // 테스트 10: CanEquipSprayer_ItemMissing_ReturnsFalse
        // ================================================================

        [Test]
        public void CanEquipSprayer_ItemMissing_ReturnsFalse()
        {
            // Arrange
            var controller = GasSprayerController.Instance;

            // Act
            bool result = controller.CanEquipSprayer("GasSprayer_Wood");

            // Assert
            Assert.IsFalse(result, "인벤토리에 아이템이 없으면 false 반환");
        }

        // ================================================================
        // 테스트 11: GetSprayerItemId_AllGrades_ReturnsCorrect
        // ================================================================

        [Test]
        public void GetSprayerItemId_AllGrades_ReturnsCorrect()
        {
            // Assert
            Assert.AreEqual("GasSprayer_Wood", GasSprayerController.GetSprayerItemId(GasSprayerGrade.Wood));
            Assert.AreEqual("GasSprayer_Stone", GasSprayerController.GetSprayerItemId(GasSprayerGrade.Stone));
            Assert.AreEqual("GasSprayer_Iron", GasSprayerController.GetSprayerItemId(GasSprayerGrade.Iron));
            Assert.AreEqual("GasSprayer_Reinforced", GasSprayerController.GetSprayerItemId(GasSprayerGrade.Reinforced));
            Assert.AreEqual("GasSprayer_SpecialAlloy", GasSprayerController.GetSprayerItemId(GasSprayerGrade.SpecialAlloy));
        }

        // ================================================================
        // 테스트 12: EquipThenUnequip_RestoresState
        // ================================================================

        [Test]
        public void EquipThenUnequip_RestoresState()
        {
            // Arrange
            PlayerInventory.Instance.AddItem(WoodSprayerItem, 1);
            var controller = GasSprayerController.Instance;

            // Act - 장착
            controller.EquipSprayer("GasSprayer_Wood");
            Assert.IsTrue(controller.IsEquipped, "장착 성공");

            // Act - 해제
            controller.UnequipSprayer();

            // Assert - 완전히 초기 상태로 복원
            Assert.IsFalse(controller.IsEquipped, "IsEquipped는 false");
            Assert.AreEqual(default(GasSprayerGrade), controller.CurrentGrade, "CurrentGrade 초기화");
            Assert.IsNull(controller.EquippedSprayerName, "EquippedSprayerName 초기화");
            Assert.AreEqual("", controller.LoadedPotionId, "LoadedPotionId 초기화");
            Assert.AreEqual(0, controller.LoadedPotionCount, "LoadedPotionCount 초기화");
            Assert.AreEqual(0f, controller.CurrentSprayTimeRemaining, "CurrentSprayTimeRemaining 초기화");
            Assert.AreEqual(1, PlayerInventory.Instance.GetItemCount("GasSprayer_Wood"), "아이템이 인벤토리에 반환됨");
        }

        // ================================================================
        // 테스트 13: OnEquipChanged_EventFired_OnEquip
        // ================================================================

        [Test]
        public void OnEquipChanged_EventFired_OnEquip()
        {
            // Arrange
            PlayerInventory.Instance.AddItem(WoodSprayerItem, 1);
            var controller = GasSprayerController.Instance;
            bool eventFired = false;
            controller.OnEquipChanged += () => eventFired = true;

            // Act
            controller.EquipSprayer("GasSprayer_Wood");

            // Assert
            Assert.IsTrue(eventFired, "장착 시 OnEquipChanged 이벤트가 발생해야 함");
        }

        // ================================================================
        // 테스트 14: OnEquipChanged_EventFired_OnUnequip
        // ================================================================

        [Test]
        public void OnEquipChanged_EventFired_OnUnequip()
        {
            // Arrange
            PlayerInventory.Instance.AddItem(WoodSprayerItem, 1);
            var controller = GasSprayerController.Instance;
            controller.EquipSprayer("GasSprayer_Wood");

            bool eventFired = false;
            controller.OnEquipChanged += () => eventFired = true;

            // Act
            controller.UnequipSprayer();

            // Assert
            Assert.IsTrue(eventFired, "해제 시 OnEquipChanged 이벤트가 발생해야 함");
        }

        // ================================================================
        // 헬퍼 메서드
        // ================================================================

        /// <summary>리플렉션으로 PlayerInventory.Instance 초기화</summary>
        private void ClearInventoryInstance()
        {
            var field = typeof(PlayerInventory).GetField("Instance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(null, null);
        }

        /// <summary>리플렉션으로 PlayerInventory.Instance 설정</summary>
        private void SetInventoryInstance(PlayerInventory instance)
        {
            var field = typeof(PlayerInventory).GetField("Instance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(null, instance);
        }

        /// <summary>리플렉션으로 GasSprayerController.Instance 초기화</summary>
        private void ClearControllerInstance()
        {
            var field = typeof(GasSprayerController).GetField("Instance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(null, null);
        }

        /// <summary>리플렉션으로 GasSprayerController.Instance 설정</summary>
        private void SetControllerInstance(GasSprayerController instance)
        {
            var field = typeof(GasSprayerController).GetField("Instance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(null, instance);
        }

        // ================================================================
        // C8-34: 재장전 관련 테스트
        // ================================================================

        // ================================================================
        // 테스트 15: Reload_GasExhausted_StartsReload
        // ================================================================

        [Test]
        public void Reload_GasExhausted_StartsReload()
        {
            // Arrange
            var controller = GasSprayerController.Instance;
            controller.Equip(GasSprayerGrade.Wood);

            // 가스를 거의 소진시킴
            controller.CurrentSprayTimeRemaining = 0f;

            // Act - StartReload 호출
            controller.StartReload();

            // Assert
            Assert.IsTrue(controller.IsReloading, "재장전 상태여야 함");
            Assert.Greater(controller.ReloadTimeRemaining, 0f, "재장전 남은 시간이 0보다 커야 함");
        }

        // ================================================================
        // 테스트 16: Reload_StartSprayDuringReload_Fails
        // ================================================================

        [Test]
        public void Reload_StartSprayDuringReload_Fails()
        {
            // Arrange
            var controller = GasSprayerController.Instance;
            controller.Equip(GasSprayerGrade.Wood);
            controller.CurrentSprayTimeRemaining = 0f;
            controller.StartReload();
            Assert.IsTrue(controller.IsReloading, "재장전 중이어야 함");

            // Act
            controller.StartSpray();

            // Assert
            Assert.IsFalse(controller.IsSpraying, "재장전 중에는 분사가 불가능해야 함");
        }

        // ================================================================
        // 테스트 17: Reload_CompleteReload_RefillsGas
        // ================================================================

        [Test]
        public void Reload_CompleteReload_RefillsGas()
        {
            // Arrange
            var controller = GasSprayerController.Instance;
            controller.Equip(GasSprayerGrade.Wood);
            controller.CurrentSprayTimeRemaining = 0f;
            controller.StartReload();
            Assert.IsTrue(controller.IsReloading, "재장전 중이어야 함");

            var data = GasSprayerManager.GetGradeData(GasSprayerGrade.Wood);

            // Act - reflection으로 private CompleteReload() 호출
            var completeMethod = typeof(GasSprayerController).GetMethod("CompleteReload",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(completeMethod, "CompleteReload 메서드를 찾을 수 없음");
            completeMethod.Invoke(controller, null);

            // Assert
            Assert.IsFalse(controller.IsReloading, "재장전 완료 후 IsReloading은 false");
            Assert.AreEqual(data.maxSprayTime, controller.CurrentSprayTimeRemaining, 0.001f,
                "재장전 완료 후 가스가 maxSprayTime으로 리필되어야 함");
        }

        // ================================================================
        // 테스트 18: Reload_CancelReload_ResetsState
        // ================================================================

        [Test]
        public void Reload_CancelReload_ResetsState()
        {
            // Arrange
            var controller = GasSprayerController.Instance;
            controller.Equip(GasSprayerGrade.Wood);
            controller.CurrentSprayTimeRemaining = 0f;
            controller.StartReload();
            Assert.IsTrue(controller.IsReloading, "재장전 중이어야 함");
            Assert.Greater(controller.ReloadTimeRemaining, 0f, "재장전 시간이 설정되어야 함");

            // Act
            controller.CancelReload();

            // Assert
            Assert.IsFalse(controller.IsReloading, "취소 후 IsReloading은 false");
            Assert.AreEqual(0f, controller.ReloadTimeRemaining, "취소 후 ReloadTimeRemaining은 0");
            Assert.AreEqual(0f, controller.CurrentSprayTimeRemaining, "취소 후 CurrentSprayTimeRemaining은 0");
        }

        // ================================================================
        // 테스트 19: Reload_SpecialAlloy_NotRequired
        // ================================================================

        [Test]
        public void Reload_SpecialAlloy_NotRequired()
        {
            // Arrange
            var controller = GasSprayerController.Instance;
            controller.Equip(GasSprayerGrade.SpecialAlloy);
            Assert.IsTrue(float.IsPositiveInfinity(controller.CurrentSprayTimeRemaining),
                "SpecialAlloy는 무제한 가스");

            // Act - StartReload는 호출되어도 isUnlimited면 return
            controller.StartReload();

            // Assert
            Assert.IsFalse(controller.IsReloading, "SpecialAlloy는 재장전 상태가 되면 안 됨");
            Assert.IsTrue(float.IsPositiveInfinity(controller.CurrentSprayTimeRemaining),
                "SpecialAlloy 가스는 계속 무제한이어야 함");
            Assert.AreEqual(0f, GasSprayerManager.GetReloadTime(GasSprayerGrade.SpecialAlloy),
                "SpecialAlloy 재장전 시간은 0초");
        }
    }
}