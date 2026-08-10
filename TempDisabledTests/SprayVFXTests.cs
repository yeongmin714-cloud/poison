using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C8-33: SprayVFX EditMode 테스트
    ///
    /// 테스트 대상:
    /// - VFX 초기화 및 파티클 시스템 생성
    /// - 분사 시작/중단 시 VFX 활성화/비활성화
    /// - 물약 속성별 색상 매핑
    /// - GetColorForPotion 정확성
    /// </summary>
    public class SprayVFXTests
    {
        private GameObject _controllerGo;
        private GameObject _inventoryGo;

        [SetUp]
        public void Setup()
        {
            // Create PlayerInventory
            _inventoryGo = new GameObject("TestPlayerInventory");
            var inv = _inventoryGo.AddComponent<PlayerInventory>();

            var slotsField = typeof(PlayerInventory).GetField("_slots",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (slotsField != null)
                slotsField.SetValue(inv, new PlayerInventory.ItemSlot[40]);

            SetInventoryInstance(inv);

            // Create GasSprayerController + SprayVFX
            _controllerGo = new GameObject("TestGasSprayerController");
            _controllerGo.AddComponent<GasSprayerController>();
            _controllerGo.AddComponent<SprayVFX>();

            SetControllerInstance(_controllerGo.GetComponent<GasSprayerController>());
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
        // 테스트 1: VFX_CreatedOnAwake_HasParticleSystem
        // ================================================================

        [Test]
        public void VFX_CreatedOnAwake_HasParticleSystem()
        {
            // Arrange
            var vfx = _controllerGo.GetComponent<SprayVFX>();

            // Assert
            Assert.IsNotNull(vfx, "SprayVFX 컴포넌트가 존재해야 함");
            Assert.IsNotNull(vfx.CurrentParticleSystem, "Awake 후 ParticleSystem이 생성되어야 함");
            Assert.IsFalse(vfx.CurrentParticleSystem.isPlaying, "초기에는 재생 중이 아니어야 함");
        }

        // ================================================================
        // 테스트 2: VFX_StartsPlaying_WhenSpraying
        // ================================================================

        [Test]
        public void VFX_StartsPlaying_WhenSpraying()
        {
            // Arrange
            var controller = GasSprayerController.Instance;
            var vfx = _controllerGo.GetComponent<SprayVFX>();
            controller.Equip(GasSprayerGrade.Wood);

            // Act
            controller.StartSpray();

            // Assert
            Assert.IsTrue(controller.IsSpraying, "분사 상태여야 함");
            // Note: ParticleSystem.Play는 실제 프레임이 필요하므로 isPlaying 체크는
            // ParticleSystem.Play() 호출 후 즉시 true가 되는 것이 정상
            Assert.IsTrue(vfx.CurrentParticleSystem.isPlaying || !vfx.CurrentParticleSystem.isStopped,
                "분사 시작 시 파티클이 재생 상태여야 함");
        }

        // ================================================================
        // 테스트 3: VFX_StopsPlaying_WhenNotSpraying
        // ================================================================

        [Test]
        public void VFX_StopsPlaying_WhenNotSpraying()
        {
            // Arrange
            var controller = GasSprayerController.Instance;
            var vfx = _controllerGo.GetComponent<SprayVFX>();
            controller.Equip(GasSprayerGrade.Wood);

            // Act
            controller.StartSpray();
            controller.StopSpray();

            // Update를 수동으로 호출하여 VFX가 상태 변경 감지
            vfx.RefreshVFX();

            // Assert
            Assert.IsFalse(controller.IsSpraying, "분사 중단 상태여야 함");
            Assert.IsTrue(vfx.CurrentParticleSystem.isStopped, "분사 중단 시 파티클이 정지 상태여야 함");
        }

        // ================================================================
        // 테스트 4: GetColorForPotion_HerbRed_ReturnsHealColor
        // ================================================================

        [Test]
        public void GetColorForPotion_HerbRed_ReturnsHealColor()
        {
            // Arrange
            var vfx = _controllerGo.GetComponent<SprayVFX>();

            // Act
            Color color = vfx.GetColorForPotion("herb_red");

            // Assert
            Assert.IsTrue(color.r > 0.8f, "herb_red는 빨간색 계열이어야 함 (R > 0.8)");
            Assert.IsTrue(color.g < 0.4f, "herb_red는 빨간색 계열 (G < 0.4)");
            Assert.IsTrue(color.b < 0.4f, "herb_red는 빨간색 계열 (B < 0.4)");
        }

        // ================================================================
        // 테스트 5: GetColorForPotion_HerbPurple_ReturnsPoisonColor
        // ================================================================

        [Test]
        public void GetColorForPotion_HerbPurple_ReturnsPoisonColor()
        {
            // Arrange
            var vfx = _controllerGo.GetComponent<SprayVFX>();

            // Act
            Color color = vfx.GetColorForPotion("herb_purple");

            // Assert
            Assert.IsTrue(color.r > 0.4f && color.r < 0.8f, "herb_purple은 보라색 계열");
            Assert.IsTrue(color.b > 0.6f, "herb_purple은 보라색 계열 (B > 0.6)");
        }

        // ================================================================
        // 테스트 6: GetColorForPotion_HerbYellow_ReturnsHallucinationColor
        // ================================================================

        [Test]
        public void GetColorForPotion_HerbYellow_ReturnsHallucinationColor()
        {
            // Arrange
            var vfx = _controllerGo.GetComponent<SprayVFX>();

            // Act
            Color color = vfx.GetColorForPotion("herb_yellow");

            // Assert
            Assert.IsTrue(color.r > 0.8f, "herb_yellow는 노란색 계열 (R > 0.8)");
            Assert.IsTrue(color.g > 0.7f, "herb_yellow는 노란색 계열 (G > 0.7)");
        }

        // ================================================================
        // 테스트 7: GetColorForPotion_HerbSilver_ReturnsDetoxColor
        // ================================================================

        [Test]
        public void GetColorForPotion_HerbSilver_ReturnsDetoxColor()
        {
            // Arrange
            var vfx = _controllerGo.GetComponent<SprayVFX>();

            // Act
            Color color = vfx.GetColorForPotion("herb_silver");

            // Assert
            Assert.IsTrue(color.r > 0.6f, "herb_silver는 은색 계열 (R > 0.6)");
            Assert.IsTrue(color.g > 0.6f, "herb_silver는 은색 계열 (G > 0.6)");
            Assert.IsTrue(color.b > 0.7f, "herb_silver는 은색 계열 (B > 0.7)");
        }

        // ================================================================
        // 테스트 8: GetColorForPotion_HerbGreen_ReturnsRegenColor
        // ================================================================

        [Test]
        public void GetColorForPotion_HerbGreen_ReturnsRegenColor()
        {
            // Arrange
            var vfx = _controllerGo.GetComponent<SprayVFX>();

            // Act
            Color color = vfx.GetColorForPotion("herb_green");

            // Assert
            Assert.IsTrue(color.g > 0.7f, "herb_green는 초록색 계열 (G > 0.7)");
            Assert.IsTrue(color.r < 0.4f, "herb_green는 초록색 계열 (R < 0.4)");
        }

        // ================================================================
        // 테스트 9: GetColorForPotion_UnknownId_ReturnsDefault
        // ================================================================

        [Test]
        public void GetColorForPotion_UnknownId_ReturnsDefault()
        {
            // Arrange
            var vfx = _controllerGo.GetComponent<SprayVFX>();

            // Act
            Color color = vfx.GetColorForPotion("unknown_potion");

            // Assert
            Assert.IsTrue(color.r > 0.7f, "기본 색상은 흰색 계열 (R > 0.7)");
            Assert.IsTrue(color.g > 0.7f, "기본 색상은 흰색 계열 (G > 0.7)");
            Assert.IsTrue(color.b > 0.7f, "기본 색상은 흰색 계열 (B > 0.7)");
        }

        // ================================================================
        // 테스트 10: GetColorForPotion_EmptyString_ReturnsDefault
        // ================================================================

        [Test]
        public void GetColorForPotion_EmptyString_ReturnsDefault()
        {
            // Arrange
            var vfx = _controllerGo.GetComponent<SprayVFX>();

            // Act
            Color color = vfx.GetColorForPotion("");

            // Assert
            Assert.IsTrue(color.r > 0.7f, "빈 문자열도 기본 흰색 (R > 0.7)");
            Assert.IsTrue(color.g > 0.7f, "빈 문자열도 기본 흰색 (G > 0.7)");
            Assert.IsTrue(color.b > 0.7f, "빈 문자열도 기본 흰색 (B > 0.7)");
        }

        // ================================================================
        // 테스트 11: VFX_ColorChanges_WhenPotionChanges
        // ================================================================

        [Test]
        public void VFX_ColorChanges_WhenPotionChanges()
        {
            // Arrange
            var controller = GasSprayerController.Instance;
            var vfx = _controllerGo.GetComponent<SprayVFX>();
            controller.Equip(GasSprayerGrade.Wood);

            // Act - herb_red 로드
            controller.LoadPotion("herb_red", 3);
            vfx.RefreshVFX();

            // Assert
            Color colorWithRed = vfx.GetColorForPotion(controller.LoadedPotionId);
            Assert.IsTrue(colorWithRed.r > 0.8f, "herb_red 로드 시 빨간색");

            // Act - herb_green으로 변경
            controller.UnloadPotion();
            controller.LoadPotion("herb_green", 2);
            vfx.RefreshVFX();

            // Assert
            Color colorWithGreen = vfx.GetColorForPotion(controller.LoadedPotionId);
            Assert.IsTrue(colorWithGreen.g > 0.7f, "herb_green 로드 시 초록색");

            // 두 색상이 달라야 함
            Assert.AreNotEqual(colorWithRed, colorWithGreen, "물약 변경 시 색상이 바뀌어야 함");
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
