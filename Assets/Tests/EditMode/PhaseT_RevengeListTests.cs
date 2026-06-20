using System.Reflection;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;
using ProjectName.UI;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// T-Cycle-04: TutorialRevengeListIntegration EditMode 테스트
    ///
    /// 테스트 대상 (10개):
    ///  1. HasShown_ReturnsFalse_Initially — PlayerPrefs 초기값 false 확인
    ///  2. MarkShown_SavesToPlayerPrefs — MarkShown 후 PlayerPrefs 저장 확인
    ///  3. ResetShown_ClearsPlayerPrefs — ResetShown 후 PlayerPrefs 삭제 확인
    ///  4. ShowRevengeListForTutorial_DoesNotRun_WhenAlreadyShown — 이미 표시 시 무시 확인
    ///  5. ShowRevengeListForTutorial_MarksPlayerPrefs — 호출 시 PlayerPrefs 기록 확인
    ///  6. ShowRevengeListForTutorial_CreatesControllerObject — 호출 시 컨트롤러 GameObject 생성 확인
    ///  7. ShowRevengeListForTutorial_InitializesManager — 호출 시 RevengeListManager 초기화 확인
    ///  8. ShowRevengeListForTutorial_CallsShowOnWindow — UIManager.revengeListWindow.Show() 호출 확인
    ///  9. ResetShown_DoesNothing_WhenNotShown — 표시 전 ResetShown 무시 확인
    /// 10. HasShown_ReturnsTrue_AfterMarkShown — MarkShown 후 HasShown true 확인
    /// </summary>
    public class PhaseT_RevengeListTests
    {
        private GameObject _uiManagerGo;
        private GameObject _revengeListWindowGo;
        private RevengeListWindow _revengeListWindow;
        private UIManager _uiManager;

        // ================================================================
        // Setup / Teardown
        // ================================================================

        [SetUp]
        public void Setup()
        {
            // UIManager 생성
            _uiManagerGo = new GameObject("TestUIManager");
            _uiManager = _uiManagerGo.AddComponent<UIManager>();
            var instanceField = typeof(UIManager).GetField("_instance",
                BindingFlags.Static | BindingFlags.NonPublic);
            instanceField?.SetValue(null, _uiManager);

            // RevengeListWindow 생성
            _revengeListWindowGo = new GameObject("TestRevengeListWindow");
            _revengeListWindow = _revengeListWindowGo.AddComponent<RevengeListWindow>();
            _uiManager.revengeListWindow = _revengeListWindow;

            // PlayerPrefs 정리
            if (PlayerPrefs.HasKey("TutorialRevengeList_Shown"))
                PlayerPrefs.DeleteKey("TutorialRevengeList_Shown");
            PlayerPrefs.Save();

            // RevengeListManager 초기화
            RevengeListManager.Instance.Reset();
        }

        [TearDown]
        public void Teardown()
        {
            // PlayerPrefs 정리
            if (PlayerPrefs.HasKey("TutorialRevengeList_Shown"))
                PlayerPrefs.DeleteKey("TutorialRevengeList_Shown");
            PlayerPrefs.Save();

            // Controller GameObject 정리
            var controllerGo = GameObject.Find("[TutorialRevengeListController]");
            if (controllerGo != null)
                Object.DestroyImmediate(controllerGo);

            // UI 정리
            if (_revengeListWindowGo != null)
                Object.DestroyImmediate(_revengeListWindowGo);
            if (_uiManagerGo != null)
                Object.DestroyImmediate(_uiManagerGo);

            // UIManager instance 정리
            var instanceField = typeof(UIManager).GetField("_instance",
                BindingFlags.Static | BindingFlags.NonPublic);
            instanceField?.SetValue(null, null);

            // RevengeListManager 리셋
            RevengeListManager.Instance.Reset();
        }

        // ================================================================
        // 헬퍼
        // ================================================================

        private bool FindControllerObject()
        {
            return GameObject.Find("[TutorialRevengeListController]") != null;
        }

        // ================================================================
        // 1. HasShown 초기값 false
        // ================================================================

        [Test]
        public void HasShown_ReturnsFalse_Initially()
        {
            Assert.IsFalse(TutorialRevengeListIntegration.HasShown,
                "HasShown는 초기에 false여야 함");
        }

        // ================================================================
        // 2. MarkShown PlayerPrefs 저장
        // ================================================================

        [Test]
        public void MarkShown_SavesToPlayerPrefs()
        {
            // Pre-condition
            Assert.IsFalse(PlayerPrefs.HasKey("TutorialRevengeList_Shown"),
                "PlayerPrefs에 아직 키가 없어야 함");

            // When
            TutorialRevengeListIntegration.MarkShown();

            // Then
            Assert.IsTrue(PlayerPrefs.HasKey("TutorialRevengeList_Shown"),
                "MarkShown 후 PlayerPrefs에 키가 저장되어야 함");
            Assert.AreEqual(1, PlayerPrefs.GetInt("TutorialRevengeList_Shown", 0),
                "PlayerPrefs 값이 1이어야 함");
        }

        // ================================================================
        // 3. ResetShown PlayerPrefs 삭제
        // ================================================================

        [Test]
        public void ResetShown_ClearsPlayerPrefs()
        {
            // Given: MarkShown 상태
            TutorialRevengeListIntegration.MarkShown();
            Assert.IsTrue(TutorialRevengeListIntegration.HasShown,
                "전제 조건: HasShown이 true여야 함");

            // When
            TutorialRevengeListIntegration.ResetShown();

            // Then
            Assert.IsFalse(TutorialRevengeListIntegration.HasShown,
                "ResetShown 후 HasShown이 false여야 함");
            Assert.IsFalse(PlayerPrefs.HasKey("TutorialRevengeList_Shown"),
                "ResetShown 후 PlayerPrefs 키가 삭제되어야 함");
        }

        // ================================================================
        // 4. 이미 표시된 경우 ShowRevengeListForTutorial 무시
        // ================================================================

        [Test]
        public void ShowRevengeListForTutorial_DoesNotRun_WhenAlreadyShown()
        {
            // Given: 이미 표시 완료 상태
            TutorialRevengeListIntegration.MarkShown();
            Assert.IsTrue(TutorialRevengeListIntegration.HasShown,
                "MarkShown 후 HasShown이 true여야 함");

            // When: ShowRevengeListForTutorial 호출
            TutorialRevengeListIntegration.ShowRevengeListForTutorial();

            // Then: Controller GameObject가 생성되지 않아야 함
            Assert.IsFalse(FindControllerObject(),
                "이미 표시된 상태에서는 컨트롤러 GameObject가 생성되지 않아야 함");
        }

        // ================================================================
        // 5. ShowRevengeListForTutorial 호출 시 PlayerPrefs 기록
        // ================================================================

        [Test]
        public void ShowRevengeListForTutorial_MarksPlayerPrefs()
        {
            // Pre-condition
            Assert.IsFalse(TutorialRevengeListIntegration.HasShown,
                "호출 전에는 HasShown이 false여야 함");

            // When: ShowRevengeListForTutorial 호출 (예외 없이 실행)
            Assert.DoesNotThrow(() =>
            {
                TutorialRevengeListIntegration.ShowRevengeListForTutorial();
            }, "ShowRevengeListForTutorial은 예외가 발생하지 않아야 함");

            // Then: PlayerPrefs가 기록됨
            Assert.IsTrue(TutorialRevengeListIntegration.HasShown,
                "ShowRevengeListForTutorial 호출 후 HasShown이 true여야 함");
        }

        // ================================================================
        // 6. ShowRevengeListForTutorial 호출 시 Controller GameObject 생성
        // ================================================================

        [Test]
        public void ShowRevengeListForTutorial_CreatesControllerObject()
        {
            // Given: 아직 표시되지 않음
            Assert.IsFalse(TutorialRevengeListIntegration.HasShown);

            // When
            TutorialRevengeListIntegration.ShowRevengeListForTutorial();

            // Then: Controller GameObject가 생성됨
            Assert.IsTrue(FindControllerObject(),
                "ShowRevengeListForTutorial 호출 후 Controller GameObject가 생성되어야 함");
        }

        // ================================================================
        // 7. ShowRevengeListForTutorial 호출 시 RevengeListManager 초기화
        // ================================================================

        [Test]
        public void ShowRevengeListForTutorial_InitializesManager()
        {
            // Given: Manager가 초기화되지 않음
            RevengeListManager.Instance.Reset();
            Assert.IsFalse(RevengeListManager.Instance.IsInitialized,
                "전제 조건: RevengeListManager가 초기화되지 않아야 함");

            // When
            TutorialRevengeListIntegration.ShowRevengeListForTutorial();

            // Then: Manager가 초기화됨 (Initialize() 호출 후 엔트리 있음)
            Assert.IsTrue(RevengeListManager.Instance.IsInitialized,
                "ShowRevengeListForTutorial 호출 후 RevengeListManager가 초기화되어야 함");
            Assert.Greater(RevengeListManager.Instance.Entries.Count, 0,
                "초기화 후 엔트리 개수가 0보다 커야 함");
        }

        // ================================================================
        // 8. ShowRevengeListForTutorial 호출 시 RevengeListWindow.Show() 호출
        // ================================================================

        [Test]
        public void ShowRevengeListForTutorial_CallsShowOnWindow()
        {
            // Given: 아직 열리지 않음
            Assert.IsFalse(_revengeListWindow.IsOpen,
                "전제 조건: 윈도우가 닫혀 있어야 함");

            // When
            TutorialRevengeListIntegration.ShowRevengeListForTutorial();

            // Then: 윈도우가 열림
            Assert.IsTrue(_revengeListWindow.IsOpen,
                "ShowRevengeListForTutorial 호출 후 RevengeListWindow가 열려야 함");
        }

        // ================================================================
        // 9. ResetShown 표시 전 호출 시 아무 일도 일어나지 않음
        // ================================================================

        [Test]
        public void ResetShown_DoesNothing_WhenNotShown()
        {
            // Given: 아직 표시되지 않음
            Assert.IsFalse(TutorialRevengeListIntegration.HasShown,
                "전제 조건: HasShown이 false여야 함");

            // When: ResetShown 호출 (예외 없이)
            Assert.DoesNotThrow(() =>
            {
                TutorialRevengeListIntegration.ResetShown();
            }, "ResetShown은 표시 전 호출 시 예외가 발생하지 않아야 함");

            // Then: 여전히 false
            Assert.IsFalse(TutorialRevengeListIntegration.HasShown,
                "ResetShown 호출 후에도 HasShown이 false여야 함");
        }

        // ================================================================
        // 10. MarkShown 후 HasShown true
        // ================================================================

        [Test]
        public void HasShown_ReturnsTrue_AfterMarkShown()
        {
            // Pre-condition
            Assert.IsFalse(TutorialRevengeListIntegration.HasShown,
                "MarkShown 전에는 HasShown이 false여야 함");

            // When
            TutorialRevengeListIntegration.MarkShown();

            // Then
            Assert.IsTrue(TutorialRevengeListIntegration.HasShown,
                "MarkShown 후 HasShown이 true여야 함");
        }
    }
}