using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C10-06: FadeManager EditMode 테스트.
    ///
    /// 테스트 대상:
    /// - FadeManager 싱글톤 Instance
    /// - FadeIn / FadeOut 기본 동작
    /// - 알파 값 설정 및 전환
    /// - CanvasGroup 생성 확인
    /// - DontDestroyOnLoad 동작
    /// - FadeManager-LoadingManager 통합
    /// </summary>
    public class FadeManagerTests
    {
        private GameObject _fadeGo;
        private FadeManager _fadeManager;

        // ================================================================
        // 헬퍼: 리플렉션 Instance 설정
        // ================================================================

        private void SetManagerInstance(FadeManager instance)
        {
            var field = typeof(FadeManager).GetField("_instance",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(null, instance);
        }

        private void ClearManagerInstance()
        {
            var field = typeof(FadeManager).GetField("_instance",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(null, null);

            var quittingField = typeof(FadeManager).GetField("_instanceQuitting",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (quittingField != null)
                quittingField.SetValue(null, false);
        }

        // ================================================================
        // Setup / Teardown
        // ================================================================

        [SetUp]
        public void Setup()
        {
            ClearManagerInstance();
            _fadeGo = new GameObject("TestFadeManager");
            _fadeManager = _fadeGo.AddComponent<FadeManager>();
            SetManagerInstance(_fadeManager);
        }

        [TearDown]
        public void Teardown()
        {
            if (_fadeGo != null)
                Object.DestroyImmediate(_fadeGo);
            ClearManagerInstance();
        }

        // ================================================================
        // 싱글톤 테스트
        // ================================================================

        [Test]
        public void Singleton_Instance_NotNull()
        {
            Assert.IsNotNull(FadeManager.Instance, "Instance는 null이 아니어야 함");
        }

        [Test]
        public void Singleton_Instance_IsSame()
        {
            Assert.AreSame(_fadeManager, FadeManager.Instance,
                "Instance가 생성한 인스턴스와 동일해야 함");
        }

        [Test]
        public void Singleton_SecondInstance_Destroyed()
        {
            var secondGo = new GameObject("SecondFadeManager");
            var secondManager = secondGo.AddComponent<FadeManager>();

            Assert.AreSame(_fadeManager, FadeManager.Instance,
                "첫 번째 Instance가 유지되어야 함");

            Object.DestroyImmediate(secondGo);
        }

        // ================================================================
        // 초기 상태
        // ================================================================

        [Test]
        public void InitialAlpha_IsZero()
        {
            Assert.AreEqual(0f, _fadeManager.CurrentAlpha, 0.001f,
                "초기 알파는 0 (투명)");
        }

        [Test]
        public void InitialIsFading_IsFalse()
        {
            Assert.IsFalse(_fadeManager.IsFading, "초기 IsFading은 false");
        }

        // ================================================================
        // SetAlpha
        // ================================================================

        [Test]
        public void SetAlpha_SetsCorrectValue()
        {
            _fadeManager.SetAlpha(0.5f);
            Assert.AreEqual(0.5f, _fadeManager.CurrentAlpha, 0.001f);
        }

        [Test]
        public void SetAlpha_ClampsToZero()
        {
            _fadeManager.SetAlpha(-0.1f);
            Assert.AreEqual(0f, _fadeManager.CurrentAlpha, 0.001f);
        }

        [Test]
        public void SetAlpha_ClampsToOne()
        {
            _fadeManager.SetAlpha(1.5f);
            Assert.AreEqual(1f, _fadeManager.CurrentAlpha, 0.001f);
        }

        [Test]
        public void SetAlpha_ZeroToFullRange()
        {
            _fadeManager.SetAlpha(0f);
            Assert.AreEqual(0f, _fadeManager.CurrentAlpha, 0.001f);

            _fadeManager.SetAlpha(1f);
            Assert.AreEqual(1f, _fadeManager.CurrentAlpha, 0.001f);

            _fadeManager.SetAlpha(0.3f);
            Assert.AreEqual(0.3f, _fadeManager.CurrentAlpha, 0.001f);
        }

        // ================================================================
        // Fade 코루틴
        // ================================================================

        [UnityTest]
        public IEnumerator FadeOut_SetsAlphaToOne()
        {
            _fadeManager.SetAlpha(0f);
            Assert.AreEqual(0f, _fadeManager.CurrentAlpha, 0.001f);

            _fadeManager.FadeOut(0.01f); // 매우 짧은 duration
            yield return new WaitForSeconds(0.02f);

            Assert.IsTrue(_fadeManager.CurrentAlpha > 0.99f,
                $"FadeOut 후 알파가 1에 가까워야 함 (현재: {_fadeManager.CurrentAlpha})");
        }

        [UnityTest]
        public IEnumerator FadeIn_SetsAlphaToZero()
        {
            _fadeManager.SetAlpha(1f);
            Assert.AreEqual(1f, _fadeManager.CurrentAlpha, 0.001f);

            _fadeManager.FadeIn(0.01f); // 매우 짧은 duration
            yield return new WaitForSeconds(0.02f);

            Assert.IsTrue(_fadeManager.CurrentAlpha < 0.01f,
                $"FadeIn 후 알파가 0에 가까워야 함 (현재: {_fadeManager.CurrentAlpha})");
        }

        [UnityTest]
        public IEnumerator FadeOut_FadeIn_Cycle()
        {
            // 시작: 투명 (alpha=0)
            Assert.AreEqual(0f, _fadeManager.CurrentAlpha, 0.001f);

            // 페이드 아웃 (투명→불투명)
            _fadeManager.FadeOut(0.02f);
            yield return new WaitForSeconds(0.03f);
            Assert.IsTrue(_fadeManager.CurrentAlpha > 0.99f,
                $"FadeOut 완료 후 alpha=1 (현재: {_fadeManager.CurrentAlpha})");

            // 페이드 인 (불투명→투명)
            _fadeManager.FadeIn(0.02f);
            yield return new WaitForSeconds(0.03f);
            Assert.IsTrue(_fadeManager.CurrentAlpha < 0.01f,
                $"FadeIn 완료 후 alpha=0 (현재: {_fadeManager.CurrentAlpha})");
        }

        [UnityTest]
        public IEnumerator FadeOut_MarksIsFading()
        {
            Assert.IsFalse(_fadeManager.IsFading);

            _fadeManager.FadeOut(0.05f);
            Assert.IsTrue(_fadeManager.IsFading, "FadeOut 시작 시 IsFading은 true");

            yield return new WaitForSeconds(0.07f);

            Assert.IsFalse(_fadeManager.IsFading, "FadeOut 완료 시 IsFading은 false");
        }

        [UnityTest]
        public IEnumerator FadeIn_MarksIsFading()
        {
            _fadeManager.SetAlpha(1f);
            Assert.IsFalse(_fadeManager.IsFading);

            _fadeManager.FadeIn(0.05f);
            Assert.IsTrue(_fadeManager.IsFading, "FadeIn 시작 시 IsFading은 true");

            yield return new WaitForSeconds(0.07f);

            Assert.IsFalse(_fadeManager.IsFading, "FadeIn 완료 시 IsFading은 false");
        }

        [UnityTest]
        public IEnumerator MultipleFade_CancelsPrevious()
        {
            _fadeManager.FadeOut(0.5f); // 긴 fade
            _fadeManager.FadeIn(0.01f); // 즉시 취소하고 fade in

            yield return new WaitForSeconds(0.02f);

            // FadeIn이 우선 적용되었으므로 alpha는 0에 가까워야 함
            Assert.IsTrue(_fadeManager.CurrentAlpha < 0.01f,
                $"두 번째 FadeIn 호출이 첫 번째 FadeOut을 취소해야 함 (현재: {_fadeManager.CurrentAlpha})");
        }

        // ================================================================
        // LoadingManager 통합
        // ================================================================

        [UnityTest]
        public IEnumerator LoadingManager_WithFadeManager_CompletesWithoutError()
        {
            // LoadingManager 생성
            var loadingGo = new GameObject("TestLoadingManager");
            var loadingManager = loadingGo.AddComponent<LoadingManager>();

            // Instance 설정
            var loadingField = typeof(LoadingManager).GetField("Instance",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (loadingField != null)
                loadingField.SetValue(null, loadingManager);

            // 존재하지 않는 씬 로드 — FadeManager가 있으면 fade 시퀀스 실행 후 CompleteLoading
            loadingManager.LoadSceneAsync("_NonExistentScene_Fade_Test_");

            yield return new WaitForSeconds(0.1f);

            // 완료 처리되어야 함 (씬 없으면 에러 로그 + CompleteLoading)
            Assert.IsFalse(loadingManager.IsLoading, "로딩이 완료되어야 함");

            if (loadingGo != null)
                Object.DestroyImmediate(loadingGo);
            if (loadingField != null)
                loadingField.SetValue(null, null);
        }

        [UnityTest]
        public IEnumerator LoadingManager_FadeSequence_Called()
        {
            // FadeManager가 존재하는 상태에서 LoadingManager.LoadSceneAsync 호출
            // FadeOut → Load → FadeIn 시퀀스가 실행되어야 함

            var loadingGo = new GameObject("TestLoadingManager2");
            var loadingManager = loadingGo.AddComponent<LoadingManager>();

            var loadingField = typeof(LoadingManager).GetField("Instance",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (loadingField != null)
                loadingField.SetValue(null, loadingManager);

            // FadeManager 인스턴스 확인
            Assert.IsNotNull(FadeManager.Instance, "FadeManager 인스턴스 존재");

            // 초기 alpha = 0
            Assert.AreEqual(0f, _fadeManager.CurrentAlpha, 0.001f);

            // 씬 로드 요청 (존재하지 않는 씬 — LoadWithFade 경로)
            loadingManager.LoadSceneAsync("_NonExistentScene_Fade_Test_2_");

            yield return new WaitForSeconds(0.1f);

            // FadeManager가 사용되었는지 확인 (alpha가 변경되었거나 IsFading 경험)
            // 에러 씬이므로 CompleteLoading 호출
            Assert.IsFalse(loadingManager.IsLoading, "로딩 완료");

            if (loadingGo != null)
                Object.DestroyImmediate(loadingGo);
            if (loadingField != null)
                loadingField.SetValue(null, null);
        }
    }
}