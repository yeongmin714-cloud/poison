using ProjectName.Core;
using ProjectName.UI;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// T-Cycle-04: 살인명부 연동 — TutorialLordSequence Step5~7 연결.
    /// ShowRevengeListForTutorial(): UIManager로 RevengeListWindow 열기 → 3초 후 자동 닫기 (Invoke).
    /// PlayerPrefs "TutorialRevengeList_Shown"으로 최초 1회만 실행.
    /// </summary>
    public static class TutorialRevengeListIntegration
    {
        private const string PREFS_KEY = "TutorialRevengeList_Shown";

        public static bool HasShown => PlayerPrefs.HasKey(PREFS_KEY);

        public static void MarkShown()
        {
            PlayerPrefs.SetInt(PREFS_KEY, 1);
            PlayerPrefs.Save();
        }

        public static void ResetShown()
        {
            if (PlayerPrefs.HasKey(PREFS_KEY))
            {
                PlayerPrefs.DeleteKey(PREFS_KEY);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// RevengeListWindow 열기 → 3초 후 자동 닫힘 → TutorialQuestManager 가이드 표시.
        /// PlayerPrefs로 최초 1회만 실행됩니다.
        /// </summary>
        public static void ShowRevengeListForTutorial()
        {
            if (HasShown)
            {
                Debug.Log("[TutorialRevengeListIntegration] 이미 표시됨 (PlayerPrefs) — 무시");
                return;
            }

            MarkShown();

            var controllerGo = new GameObject("[TutorialRevengeListController]");
            Object.DontDestroyOnLoad(controllerGo);
            var controller = controllerGo.AddComponent<RevengeListController>();
            controller.StartSequence();
        }

        private class RevengeListController : MonoBehaviour
        {
            public void StartSequence()
            {
                // 1) RevengeListManager 초기화
                if (!RevengeListManager.Instance.IsInitialized)
                    RevengeListManager.Instance.Initialize();

                // 2) 튜토리얼 영주(첫 번째 엔트리) 공개
                var entries = RevengeListManager.Instance.Entries;
                if (entries.Count > 0 && !entries[0].isRevealed && !entries[0].isCompleted)
                    RevengeListManager.Instance.RevealReason(entries[0].territoryId);

                // 3) RevengeListWindow 열기
                var uiManager = UIManager.Instance;
                if (uiManager != null && uiManager.revengeListWindow != null)
                {
                    if (!uiManager.revengeListWindow.IsOpen)
                        uiManager.ToggleWindow(uiManager.revengeListWindow);
                }

                // 4) 첫 번째 항목 선택 (하이라이트)
                var field = typeof(RevengeListWindow).GetField("_selectedIndex",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (field != null)
                    field.SetValue(uiManager.revengeListWindow, 0);

                // 5) 3초 후 Invoke로 자동 닫기
                Invoke(nameof(CloseAndContinue), 3f);
            }

            private void CloseAndContinue()
            {
                // RevengeListWindow 닫기
                var uiManager = UIManager.Instance;
                if (uiManager != null && uiManager.revengeListWindow != null)
                {
                    if (uiManager.revengeListWindow.IsOpen)
                        uiManager.revengeListWindow.Hide();
                }

                // 가이드 표시
                var questManager = TutorialQuestManager.Instance;
                if (questManager != null)
                {
                    questManager.StartTutorialQuests();
                }
                else
                {
                    var guideSystem = TutorialGuideSystem.Instance;
                    guideSystem?.ShowGuide("01_movement");
                }

                Debug.Log("[TutorialRevengeListIntegration] 살인명부 튜토리얼 시퀀스 완료");
                Destroy(gameObject);
            }
        }
    }
}