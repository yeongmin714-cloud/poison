using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// G3-12: SoundRefinement — FootstepSoundController, UISoundIntegrator, BiomeAmbientController 테스트
    /// </summary>
    public class SoundRefinementTests
    {
        // ================================================================
        // FootstepSoundController Tests
        // ================================================================

        [Test]
        public void FootstepSoundController_CanBeAdded()
        {
            var go = new GameObject("TestFootstep");
            var controller = go.AddComponent<FootstepSoundController>();
            Assert.IsNotNull(controller, "FootstepSoundController 컴포넌트가 추가되어야 함");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void FootstepSoundController_DefaultIntervals_AreCorrect()
        {
            var go = new GameObject("TestFootstep");
            var controller = go.AddComponent<FootstepSoundController>();

            Assert.AreEqual(0.5f, controller.WalkInterval, "걷기 간격은 0.5초");
            Assert.AreEqual(0.35f, controller.RunInterval, "달리기 간격은 0.35초");
            Assert.AreEqual(0.25f, controller.DashInterval, "대쉬 간격은 0.25초");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void FootstepSoundController_DefaultSurface_IsGrass()
        {
            var go = new GameObject("TestFootstep");
            var controller = go.AddComponent<FootstepSoundController>();

            Assert.AreEqual("step_grass", controller.CurrentSurfaceTag, "기본 표면은 step_grass");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void FootstepSoundController_SetSurfaceVariant_UpdatesTag()
        {
            var go = new GameObject("TestFootstep");
            var controller = go.AddComponent<FootstepSoundController>();

            controller.SetSurfaceVariant("step_stone");
            Assert.AreEqual("step_stone", controller.CurrentSurfaceTag, "SetSurfaceVariant로 태그 변경 가능");

            controller.SetSurfaceVariant("step_wood");
            Assert.AreEqual("step_wood", controller.CurrentSurfaceTag, "SetSurfaceVariant로 나무 태그 변경 가능");

            controller.SetSurfaceVariant("step_water");
            Assert.AreEqual("step_water", controller.CurrentSurfaceTag, "SetSurfaceVariant로 물 태그 변경 가능");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void FootstepSoundController_RequiresPlayerMovement()
        {
            var go = new GameObject("TestFootstep");
            var controller = go.AddComponent<FootstepSoundController>();

            // RequireComponent 속성 확인
            var attributes = typeof(FootstepSoundController).GetCustomAttributes(typeof(RequireComponent), true);
            Assert.IsNotEmpty(attributes, "RequireComponent 속성이 있어야 함");

            Object.DestroyImmediate(go);
        }

        // ================================================================
        // UISoundIntegrator Tests
        // ================================================================

        [Test]
        public void UISoundIntegrator_CanBeAdded()
        {
            var go = new GameObject("TestUISound");
            var integrator = go.AddComponent<UISoundIntegrator>();
            Assert.IsNotNull(integrator, "UISoundIntegrator 컴포넌트가 추가되어야 함");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void UISoundIntegrator_PlayClick_DoesNotThrow()
        {
            var go = new GameObject("TestUISound");
            var integrator = go.AddComponent<UISoundIntegrator>();

            // UISoundManager 인스턴스가 없어도 예외가 발생하지 않아야 함
            Assert.DoesNotThrow(() => integrator.PlayClick(), "PlayClick이 예외를 던지면 안 됨");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void UISoundIntegrator_PlayOpen_DoesNotThrow()
        {
            var go = new GameObject("TestUISound");
            var integrator = go.AddComponent<UISoundIntegrator>();

            Assert.DoesNotThrow(() => integrator.PlayOpen(), "PlayOpen이 예외를 던지면 안 됨");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void UISoundIntegrator_PlayClose_DoesNotThrow()
        {
            var go = new GameObject("TestUISound");
            var integrator = go.AddComponent<UISoundIntegrator>();

            Assert.DoesNotThrow(() => integrator.PlayClose(), "PlayClose가 예외를 던지면 안 됨");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void UISoundIntegrator_AllMethods_SafeWithoutManager()
        {
            var go = new GameObject("TestUISound");
            var integrator = go.AddComponent<UISoundIntegrator>();

            // 모든 퍼블릭 메서드가 매니저 없이 안전하게 호출 가능해야 함
            Assert.DoesNotThrow(() => integrator.PlayClick());
            Assert.DoesNotThrow(() => integrator.PlayOpen());
            Assert.DoesNotThrow(() => integrator.PlayClose());

            Object.DestroyImmediate(go);
        }

        // ================================================================
        // BiomeAmbientController Tests
        // ================================================================

        [Test]
        public void BiomeAmbientController_CanBeAdded()
        {
            var go = new GameObject("TestBiome");
            var controller = go.AddComponent<BiomeAmbientController>();
            Assert.IsNotNull(controller, "BiomeAmbientController 컴포넌트가 추가되어야 함");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void BiomeAmbientController_SetBiome_UpdatesLastBiome()
        {
            var go = new GameObject("TestBiome");
            var controller = go.AddComponent<BiomeAmbientController>();

            controller.SetBiome("Forest");
            Assert.AreEqual("Forest", controller.LastBiome, "SetBiome('Forest') 호출 후 LastBiome이 Forest여야 함");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void BiomeAmbientController_SetBiome_WithEmptyString_DoesNotChange()
        {
            var go = new GameObject("TestBiome");
            var controller = go.AddComponent<BiomeAmbientController>();

            controller.SetBiome("Desert");
            Assert.AreEqual("Desert", controller.LastBiome, "Desert 설정 후 LastBiome이 Desert여야 함");

            // 빈 문자열로 호출해도 변경되지 않음
            controller.SetBiome("");
            Assert.AreEqual("Desert", controller.LastBiome, "빈 문자열 SetBiome은 LastBiome을 변경하지 않음");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void BiomeAmbientController_SetBiome_AfterForceUpdate_Works()
        {
            var go = new GameObject("TestBiome");
            var controller = go.AddComponent<BiomeAmbientController>();

            controller.ForceUpdateAmbient();
            Assert.IsNull(controller.LastBiome, "ForceUpdateAmbient 후 LastBiome은 null로 초기화");

            controller.SetBiome("Swamp");
            Assert.AreEqual("Swamp", controller.LastBiome, "ForceUpdate 후 SetBiome('Swamp')가 정상 동작");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void BiomeAmbientController_MultipleSetBiome_UpdatesCorrectly()
        {
            var go = new GameObject("TestBiome");
            var controller = go.AddComponent<BiomeAmbientController>();

            controller.SetBiome("Forest");
            Assert.AreEqual("Forest", controller.LastBiome);

            controller.SetBiome("Desert");
            Assert.AreEqual("Desert", controller.LastBiome, "Forest에서 Desert로 변경되어야 함");

            controller.SetBiome("Mountain");
            Assert.AreEqual("Mountain", controller.LastBiome, "Desert에서 Mountain으로 변경되어야 함");

            controller.SetBiome("Town");
            Assert.AreEqual("Town", controller.LastBiome, "Mountain에서 Town으로 변경되어야 함");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void BiomeAmbientController_ForceUpdateAmbient_DoesNotThrow()
        {
            var go = new GameObject("TestBiome");
            var controller = go.AddComponent<BiomeAmbientController>();

            // SoundManagerEnhanced 인스턴스가 없어도 예외가 발생하지 않음
            Assert.DoesNotThrow(() => controller.ForceUpdateAmbient(), "ForceUpdateAmbient가 예외를 던지면 안 됨");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void BiomeAmbientController_SetBiome_DoesNotThrow()
        {
            var go = new GameObject("TestBiome");
            var controller = go.AddComponent<BiomeAmbientController>();

            Assert.DoesNotThrow(() => controller.SetBiome("Forest"));
            Assert.DoesNotThrow(() => controller.SetBiome("Desert"));
            Assert.DoesNotThrow(() => controller.SetBiome("Water"));
            Assert.DoesNotThrow(() => controller.SetBiome("Mountain"));
            Assert.DoesNotThrow(() => controller.SetBiome("Swamp"));
            Assert.DoesNotThrow(() => controller.SetBiome("Town"));
            Assert.DoesNotThrow(() => controller.SetBiome("Default"));

            Object.DestroyImmediate(go);
        }
    }
}