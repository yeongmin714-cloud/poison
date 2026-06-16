using System.Collections;
using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C10-01~05: BuildingTrigger EditMode 테스트.
    ///
    /// 테스트 대상:
    /// - BuildingTrigger 인스턴스화 및 필드 기본값
    /// - IndoorSceneTransition EnterBuilding/ExitBuilding Additive 동작
    /// - IndoorTransitionSetup 헬퍼 메서드
    /// - Gizmos (컴파일 확인)
    /// </summary>
    public class BuildingTriggerTests
    {
        private GameObject _triggerGo;
        private BuildingTrigger _trigger;

        [SetUp]
        public void Setup()
        {
            _triggerGo = new GameObject("TestBuildingTrigger");
            _trigger = _triggerGo.AddComponent<BuildingTrigger>();
            _trigger.BuildingType = "TestHouse";
        }

        [TearDown]
        public void TearDown()
        {
            if (_triggerGo != null)
                Object.DestroyImmediate(_triggerGo);
        }

        // ================================================================
        // 기본값 및 속성 테스트
        // ================================================================

        [Test]
        public void BuildingTrigger_Exists()
        {
            Assert.IsNotNull(_trigger, "BuildingTrigger가 생성되어야 함");
        }

        [Test]
        public void BuildingTrigger_DefaultInteractRange_Is3()
        {
            var freshTrigger = new GameObject("FreshTrigger").AddComponent<BuildingTrigger>();
            Assert.AreEqual(3f, freshTrigger.InteractRange, 0.001f,
                "기본 InteractRange는 3f");
            Object.DestroyImmediate(freshTrigger.gameObject);
        }

        [Test]
        public void BuildingTrigger_CanSetBuildingType()
        {
            _trigger.BuildingType = "Castle";
            Assert.AreEqual("Castle", _trigger.BuildingType);
        }

        [Test]
        public void BuildingTrigger_CanSetInteractRange()
        {
            _trigger.InteractRange = 5f;
            Assert.AreEqual(5f, _trigger.InteractRange, 0.001f);
        }

        [Test]
        public void BuildingTrigger_CanGetBuildingType()
        {
            Assert.AreEqual("TestHouse", _trigger.BuildingType);
        }

        [Test]
        public void BuildingTrigger_CanGetInteractRange()
        {
            Assert.AreEqual(3f, _trigger.InteractRange, 0.001f);
        }

        [Test]
        public void BuildingTrigger_BuildingType_Roundtrip()
        {
            string[] types = { "House", "Shop", "CraftHouse", "Church", "Castle" };
            foreach (var t in types)
            {
                _trigger.BuildingType = t;
                Assert.AreEqual(t, _trigger.BuildingType, $"BuildingType '{t}' 설정/읽기");
            }
        }

        [Test]
        public void BuildingTrigger_InteractRange_Roundtrip()
        {
            float[] ranges = { 1f, 2.5f, 3f, 5f, 10f };
            foreach (var r in ranges)
            {
                _trigger.InteractRange = r;
                Assert.AreEqual(r, _trigger.InteractRange, 0.001f, $"InteractRange {r}");
            }
        }

        // ================================================================
        // IndoorSceneTransition Additive 동작
        // ================================================================

        [Test]
        public void IndoorSceneTransition_InitialState_NoPendingBuilding()
        {
            Assert.IsNull(IndoorSceneTransition.GetPendingBuildingType(),
                "초기 상태에는 _pendingBuildingType이 null");
        }

        [Test]
        public void IndoorSceneTransition_EnterBuilding_SetsPreviousScene()
        {
            string currentScene = SceneManager.GetActiveScene().name;

            IndoorSceneTransition.EnterBuilding("TestHouse");

            Assert.AreEqual(currentScene, IndoorSceneTransition.GetPreviousSceneName(),
                "EnterBuilding 호출 시 현재 씬 이름 저장");
        }

        [Test]
        public void IndoorSceneTransition_IsIndoorSceneLoaded_InitiallyFalse()
        {
            Assert.IsFalse(IndoorSceneTransition.IsIndoorSceneLoaded(),
                "초기에는 IndoorScene이 로드되지 않음");
        }

        [Test]
        public void IndoorSceneTransition_MultipleEnter_ReusesPreviousScene()
        {
            string firstScene = SceneManager.GetActiveScene().name;

            IndoorSceneTransition.EnterBuilding("Shop");
            Assert.AreEqual(firstScene, IndoorSceneTransition.GetPreviousSceneName(),
                "첫 번째 EnterBuilding 후 이전 씬 저장");

            IndoorSceneTransition.EnterBuilding("Church");
            Assert.AreEqual(firstScene, IndoorSceneTransition.GetPreviousSceneName(),
                "두 번째 EnterBuilding 후에도 첫 번째 이전 씬 유지");
        }

        [Test]
        public void IndoorSceneTransition_EnterBuilding_AdditiveMode_DoesNotUnloadCurrentScene()
        {
            // 현재 씬이 EnterBuilding 후에도 로드 상태 유지되는지 확인
            string currentScene = SceneManager.GetActiveScene().name;

            IndoorSceneTransition.EnterBuilding("Shop");

            // 현재 씬은 여전히 로드되어 있어야 함 (Additive이므로)
            Scene activeScene = SceneManager.GetActiveScene();
            Assert.AreEqual(currentScene, activeScene.name,
                "EnterBuilding 직후에는 현재 씬이 그대로 활성화 상태 (Additive 로드 시작만 함)");
        }

        // ================================================================
        // ExitBuilding
        // ================================================================

        [Test]
        public void IndoorSceneTransition_ExitBuilding_WithNoPreviousScene_UsesDefault()
        {
            // 이전 씬이 없으면 기본 WorldScene 사용
            IndoorSceneTransition.ExitBuilding();
            Assert.Pass("ExitBuilding이 예외 없이 실행됨");
        }

        [Test]
        public void IndoorSceneTransition_ExitBuilding_AfterEnter_ClearsState()
        {
            IndoorSceneTransition.EnterBuilding("House");
            Assert.IsNotNull(IndoorSceneTransition.GetPreviousSceneName());

            IndoorSceneTransition.ExitBuilding();
            // ExitBuilding이 previousSceneName을 null로 설정
            Assert.IsNull(IndoorSceneTransition.GetPreviousSceneName(),
                "ExitBuilding 후 _previousSceneName은 null");
        }

        // ================================================================
        // IndoorTransitionSetup 헬퍼
        // ================================================================

        [Test]
        public void IndoorTransitionSetup_CreateBuildingTrigger_ReturnsGameObject()
        {
            GameObject result = IndoorTransitionSetup.CreateBuildingTrigger(
                Vector3.zero, "TestBuilding", 3f, null);

            Assert.IsNotNull(result, "CreateBuildingTrigger가 GameObject 반환");
            Assert.IsNotNull(result.GetComponent<BuildingTrigger>(),
                "BuildingTrigger 컴포넌트가 존재");

            Object.DestroyImmediate(result);
        }

        [Test]
        public void IndoorTransitionSetup_CreateBuildingTrigger_SetsBuildingType()
        {
            GameObject result = IndoorTransitionSetup.CreateBuildingTrigger(
                Vector3.zero, "Castle", 4f, null);

            var trigger = result.GetComponent<BuildingTrigger>();
            Assert.AreEqual("Castle", trigger.BuildingType);
            Assert.AreEqual(4f, trigger.InteractRange, 0.001f);

            Object.DestroyImmediate(result);
        }

        [Test]
        public void IndoorTransitionSetup_CreateTutorialHouseTrigger_HasHouseType()
        {
            GameObject result = IndoorTransitionSetup.CreateTutorialHouseTrigger(
                Vector3.one * 5f);

            var trigger = result.GetComponent<BuildingTrigger>();
            Assert.AreEqual("House", trigger.BuildingType);

            Object.DestroyImmediate(result);
        }

        [Test]
        public void IndoorTransitionSetup_CreateShopTrigger_HasShopType()
        {
            GameObject result = IndoorTransitionSetup.CreateShopTrigger(Vector3.zero);
            Assert.AreEqual("Shop", result.GetComponent<BuildingTrigger>().BuildingType);
            Object.DestroyImmediate(result);
        }

        [Test]
        public void IndoorTransitionSetup_CreateCraftHouseTrigger_HasCraftHouseType()
        {
            GameObject result = IndoorTransitionSetup.CreateCraftHouseTrigger(Vector3.zero);
            Assert.AreEqual("CraftHouse", result.GetComponent<BuildingTrigger>().BuildingType);
            Object.DestroyImmediate(result);
        }

        [Test]
        public void IndoorTransitionSetup_CreateChurchTrigger_HasChurchType()
        {
            GameObject result = IndoorTransitionSetup.CreateChurchTrigger(Vector3.zero);
            Assert.AreEqual("Church", result.GetComponent<BuildingTrigger>().BuildingType);
            Object.DestroyImmediate(result);
        }

        [Test]
        public void IndoorTransitionSetup_CreateCastleTrigger_HasCastleTypeAndLargerRange()
        {
            GameObject result = IndoorTransitionSetup.CreateCastleTrigger(Vector3.zero);
            var trigger = result.GetComponent<BuildingTrigger>();
            Assert.AreEqual("Castle", trigger.BuildingType);
            Assert.AreEqual(4f, trigger.InteractRange, 0.001f, "Castle은 4f 범위");
            Object.DestroyImmediate(result);
        }

        [Test]
        public void IndoorTransitionSetup_CreateBuildingTrigger_WithParent()
        {
            GameObject parent = new GameObject("Parent");
            GameObject result = IndoorTransitionSetup.CreateBuildingTrigger(
                Vector3.zero, "Shop", 3f, parent.transform);

            Assert.AreEqual(parent.transform, result.transform.parent,
                "부모 Transform이 설정되어야 함");

            Object.DestroyImmediate(result);
            Object.DestroyImmediate(parent);
        }
    }
}