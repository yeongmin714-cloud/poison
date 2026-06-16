using System;
using System.Reflection;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C12: LoadingManager EditMode 테스트
    ///
    /// 테스트 대상:
    /// - 싱글톤 Instance
    /// - StartLoading / SetProgress / CompleteLoading
    /// - Progress 값 범위
    /// - 이벤트 호출
    /// - LoadSceneAsync 기본 동작 (씬 없으면 에러 로그 + CompleteLoading)
    /// </summary>
    public class LoadingManagerTests
    {
        private GameObject _managerGo;

        // ================================================================
        // 헬퍼: 리플렉션 Instance 설정
        // ================================================================

        private void SetManagerInstance(LoadingManager instance)
        {
            var field = typeof(LoadingManager).GetField("Instance",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(null, instance);
        }

        private void ClearManagerInstance()
        {
            var field = typeof(LoadingManager).GetField("Instance",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(null, null);
        }

        // ================================================================
        // Setup / Teardown
        // ================================================================

        [SetUp]
        public void Setup()
        {
            _managerGo = new GameObject("TestLoadingManager");
            var manager = _managerGo.AddComponent<LoadingManager>();
            SetManagerInstance(manager);
        }

        [TearDown]
        public void Teardown()
        {
            if (_managerGo != null)
                UnityEngine.Object.DestroyImmediate(_managerGo);
            ClearManagerInstance();
        }

        // ================================================================
        // 테스트
        // ================================================================

        [Test]
        public void Singleton_Instance_NotNull()
        {
            Assert.IsNotNull(LoadingManager.Instance, "Instance는 null이 아니어야 함");
        }

        [Test]
        public void Singleton_SecondInstance_Destroyed()
        {
            var secondGo = new GameObject("SecondLoadingManager");
            var secondManager = secondGo.AddComponent<LoadingManager>();

            // Awake에서 두 번째 Instance 파괴
            Assert.AreSame(LoadingManager.Instance, _managerGo.GetComponent<LoadingManager>(),
                "첫 번째 Instance가 유지되어야 함");

            // 두 번째 GameObject는 Destroy되었거나 Instance가 아님
            Assert.IsNull(secondManager == null ? null : secondManager,
                "두 번째 인스턴스는 null이어야 함 (Destroy됨)");

            if (secondGo != null)
                UnityEngine.Object.DestroyImmediate(secondGo);
        }

        [Test]
        public void StartLoading_SetsIsLoadingTrue()
        {
            var manager = LoadingManager.Instance;
            Assert.IsFalse(manager.IsLoading, "초기 IsLoading은 false");

            manager.StartLoading();
            Assert.IsTrue(manager.IsLoading, "StartLoading 후 IsLoading은 true");
        }

        [Test]
        public void StartLoading_SetsProgressToZero()
        {
            var manager = LoadingManager.Instance;
            manager.StartLoading();
            Assert.AreEqual(0f, manager.Progress, 0.001f, "StartLoading 후 Progress는 0");
        }

        [Test]
        public void StartLoading_UpdatesCurrentTip()
        {
            var manager = LoadingManager.Instance;
            string tipBefore = manager.CurrentTip;
            manager.StartLoading();
            Assert.IsFalse(string.IsNullOrEmpty(manager.CurrentTip), "팁이 비어있지 않아야 함");
        }

        [Test]
        public void SetProgress_UpdatesProgress()
        {
            var manager = LoadingManager.Instance;
            manager.StartLoading();
            manager.SetProgress(0.5f);
            Assert.AreEqual(0.5f, manager.Progress, 0.001f);
        }

        [Test]
        public void SetProgress_ClampsToZero()
        {
            var manager = LoadingManager.Instance;
            manager.StartLoading();
            manager.SetProgress(-0.5f);
            Assert.AreEqual(0f, manager.Progress, 0.001f, "음수는 0으로 클램프");
        }

        [Test]
        public void SetProgress_ClampsToOne()
        {
            var manager = LoadingManager.Instance;
            manager.StartLoading();
            manager.SetProgress(1.5f);
            Assert.AreEqual(1f, manager.Progress, 0.001f, "1 초과는 1로 클램프");
        }

        [Test]
        public void CompleteLoading_SetsIsLoadingFalse()
        {
            var manager = LoadingManager.Instance;
            manager.StartLoading();
            Assert.IsTrue(manager.IsLoading);
            manager.CompleteLoading();
            Assert.IsFalse(manager.IsLoading, "CompleteLoading 후 IsLoading은 false");
        }

        [Test]
        public void CompleteLoading_SetsProgressToOne()
        {
            var manager = LoadingManager.Instance;
            manager.StartLoading();
            manager.SetProgress(0.3f);
            manager.CompleteLoading();
            Assert.AreEqual(1f, manager.Progress, 0.001f, "CompleteLoading 후 Progress는 1");
        }

        [Test]
        public void OnLoadStart_EventFired()
        {
            var manager = LoadingManager.Instance;
            bool eventFired = false;
            manager.OnLoadStart += () => eventFired = true;

            manager.StartLoading();
            Assert.IsTrue(eventFired, "OnLoadStart 이벤트가 발생해야 함");
        }

        [Test]
        public void OnProgressChanged_EventFired()
        {
            var manager = LoadingManager.Instance;
            bool eventFired = false;
            float receivedProgress = -1f;
            manager.OnProgressChanged += (p) =>
            {
                eventFired = true;
                receivedProgress = p;
            };

            manager.StartLoading();
            Assert.IsTrue(eventFired, "StartLoading 시 OnProgressChanged 이벤트 발생");
            Assert.AreEqual(0f, receivedProgress, 0.001f, "진행률 0 전달");
        }

        [Test]
        public void OnProgressChanged_MultipleUpdates()
        {
            var manager = LoadingManager.Instance;
            float lastProgress = -1f;
            int callCount = 0;
            manager.OnProgressChanged += (p) =>
            {
                lastProgress = p;
                callCount++;
            };

            manager.StartLoading();
            manager.SetProgress(0.25f);
            manager.SetProgress(0.75f);

            Assert.AreEqual(3, callCount, "3번 호출 (0, 0.25, 0.75)");
            Assert.AreEqual(0.75f, lastProgress, 0.001f);
        }

        [Test]
        public void OnLoadComplete_EventFired()
        {
            var manager = LoadingManager.Instance;
            bool eventFired = false;
            manager.OnLoadComplete += () => eventFired = true;

            manager.StartLoading();
            manager.CompleteLoading();
            Assert.IsTrue(eventFired, "OnLoadComplete 이벤트가 발생해야 함");
        }

        [Test]
        public void LoadSceneAsync_InvalidScene_DoesNotCrash()
        {
            var manager = LoadingManager.Instance;
            bool completeFired = false;
            manager.OnLoadComplete += () => completeFired = true;

            manager.LoadSceneAsync("_NonExistentScene_999_");

            // 씬이 없으므로 CompleteLoading이 호출되어야 함 (에러 로그 + 완료)
            Assert.IsTrue(completeFired, "존재하지 않는 씬 로드는 CompleteLoading 호출");
        }

        [Test]
        public void DoubleStartLoading_Ignored()
        {
            var manager = LoadingManager.Instance;
            manager.StartLoading();
            manager.StartLoading(); // 두 번째 호출은 무시
            Assert.IsTrue(manager.IsLoading, "두 번째 StartLoading도 로딩 상태 유지");
        }

        [Test]
        public void CompleteLoading_WithoutStart_Ignored()
        {
            var manager = LoadingManager.Instance;
            manager.CompleteLoading(); // StartLoading 없이 호출 — 무시
            Assert.IsFalse(manager.IsLoading, "StartLoading 없이 CompleteLoading은 무시");
        }

        [Test]
        public void SetProgress_WithoutStart_UpdatesProperty()
        {
            var manager = LoadingManager.Instance;
            manager.SetProgress(0.5f);
            Assert.AreEqual(0.5f, manager.Progress, 0.001f,
                "StartLoading 없이도 SetProgress는 Progress 변경");
        }
    }
}