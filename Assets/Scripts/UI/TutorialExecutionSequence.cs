using System.Collections;
using ProjectName.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectName.Systems
{
    /// <summary>
    /// T-Cycle-07: 퀘스트 완료 → 영주 처형 → 씬 전환 시퀀스.
    /// TutorialQuestManager.OnAllQuestsComplete → StartExecutionSequence()
    /// </summary>
    public class TutorialExecutionSequence : MonoBehaviour
    {
        public static TutorialExecutionSequence Instance { get; private set; }

        [SerializeField] private float _fadeOutDuration = 1.5f;
        [SerializeField] private float _resultDisplayDuration = 2f;

        private bool _isRunning = false;
        private bool _hasExecuted = false;

        private const string PREFS_KEY = "TutorialExecution_Done";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _hasExecuted = PlayerPrefs.GetInt(PREFS_KEY, 0) == 1;
        }

        public void StartExecutionSequence()
        {
            if (_isRunning || _hasExecuted) return;
            _isRunning = true;

            if (TutorialGuideSystem.Instance != null)
                TutorialGuideSystem.Instance.ShowGuide("execution_ready");
            else
                Debug.LogWarning("[TutorialExecutionSequence] TutorialGuideSystem.Instance is null!");

            StartCoroutine(ExecutionCoroutine());
        }

        private IEnumerator ExecutionCoroutine()
        {
            // 1. 플레이어가 E키로 영주에게 음식 전달할 때까지 대기
            yield return new WaitUntil(() => Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame);

            // 2. 영주 음식 섭취 + 행동불능
            Debug.Log("[TutorialExecutionSequence] 영주 음식 섭취 → 행동불능");

            // 3. MercyUI 표시 (처형/살려주기)
            MercyUI mercyUI = MercyUI.Instance;
            if (mercyUI != null)
            {
                MercyUI.Show("처형할까?");
                yield return new WaitUntil(() => mercyUI.IsComplete);
            }

            // 4. 처형 선택 시 → 페이드 아웃
            if (FadeManager.Instance != null)
                yield return FadeManager.Instance.FadeOut(_fadeOutDuration);
            else
                yield return new WaitForSeconds(_fadeOutDuration);

            // 5. 결과 메시지
            Debug.Log("[TutorialExecutionSequence] 🎉 첫 번째 영지를 획득했습니다!");

            // 6. 영지 씬으로 전환
            IndoorSceneTransition.ExitBuilding();

            yield return new WaitForSeconds(0.5f);

            // 7. 영지 가이드 시작
            if (TutorialGuideSystem.Instance != null)
                TutorialGuideSystem.Instance.ShowGuide("territory_intro");
            else
                Debug.LogWarning("[TutorialExecutionSequence] TutorialGuideSystem.Instance is null on territory guide!");

            _isRunning = false;
            _hasExecuted = true;
            PlayerPrefs.SetInt(PREFS_KEY, 1);
            PlayerPrefs.Save();
        }

        public void ResetExecution()
        {
            _hasExecuted = false;
            _isRunning = false;
            PlayerPrefs.DeleteKey(PREFS_KEY);
        }
    }
}