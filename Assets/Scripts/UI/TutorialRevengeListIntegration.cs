using ProjectName.Core;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.UI
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
#if false
        public static void ShowRevengeListForTutorial()
        {
            if (HasShown)
            {
                Debug.Log("[TutorialRevengeListIntegration] 이미 표시됨 (PlayerPrefs) — 무시");
                return;
            }

            var controllerGo = new GameObject("[TutorialRevengeListController]");
            Object.DontDestroyOnLoad(controllerGo);
            var controller = controllerGo.AddComponent<RevengeListController>();
            controller.StartSequence();
        }

        #if false
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
                    {
                        uiManager.ToggleWindow(uiManager.revengeListWindow);
                        // ToggleWindow → Show() → OnShow()에서 _selectedIndex = -1로 리셋되므로,
                        // ToggleWindow 완료 후 SelectIndex()로 첫 번째 항목 선택 (reflection 대체)
                        uiManager.revengeListWindow.SelectIndex(0);
                    }
                }

                // 4) PlayerPrefs 저장 (시퀀스 성공적 시작 후) — Invoke 직전에 저장하여
                //    시퀀스 도중 크래시 시 재시도 가능하도록 함
                MarkShown();

                // 5) 3초 후 Invoke로 자동 닫기
                Invoke(nameof(CloseAndContinue), 3f);
            }

            private void CloseAndContinue()
            {
                // RevengeListWindow 닫기 (스택 정리 보장을 위해 ToggleWindow 사용)
                var uiManager = UIManager.Instance;
                if (uiManager != null && uiManager.revengeListWindow != null)
                {
                    if (uiManager.revengeListWindow.IsOpen)
                        uiManager.ToggleWindow(uiManager.revengeListWindow);
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
                    if (guideSystem != null)
                    {
                        guideSystem.ShowGuide("01_movement");
                    }
                    else
                    {
                        Debug.LogWarning("[TutorialRevengeListIntegration] TutorialQuestManager와 TutorialGuideSystem 모두 null — 가이드 시작 불가");
                    }
                }

                Debug.Log("[TutorialRevengeListIntegration] 살인명부 튜토리얼 시퀀스 완료");
                Destroy(gameObject);
            }
        }
#endif
#endif
    }
}