using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// 🌅 게임 엔딩 매니저 — 모든 영지 점령 시 엔딩 시퀀스를 트리거합니다.
    /// </summary>
    public class GameEndingManager : MonoBehaviour
    {
        public static GameEndingManager Instance { get; private set; }

        [Header("설정")]
        [SerializeField] private float _slowMotionTimeScale = 0.5f;

        // ===== 상태 =====
        private bool _isEndingTriggered;
        private bool _isSequenceActive;
        private bool _isInitialized;

        /// <summary>
        /// 엔딩이 이미 트리거되었는지 여부 (재트리거 방지)
        /// </summary>
        public bool IsEndingTriggered => _isEndingTriggered;

        /// <summary>
        /// 엔딩 시퀀스가 현재 진행 중인지 여부
        /// </summary>
        public bool IsSequenceActive => _isSequenceActive;

        // ===== 생명주기 =====

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // EndingCreditsUI 자동 생성
            EnsureEndingCreditsUI();
        }

        private void Start()
        {
            _isInitialized = true;
            Debug.Log("[GameEndingManager] 초기화 완료");
        }

        private void EnsureEndingCreditsUI()
        {
            if (EndingCreditsUI.Instance != null) return;

            var existing = FindAnyObjectByType<EndingCreditsUI>();
            if (existing != null) return;

            var go = new GameObject("EndingCreditsUI");
            go.AddComponent<EndingCreditsUI>();
            go.SetActive(false);
            Debug.Log("[GameEndingManager] EndingCreditsUI 자동 생성됨");
        }

        private void Update()
        {
            // 초기화 전 또는 엔딩 트리거 후에는 체크하지 않음
            if (!_isInitialized || _isEndingTriggered) return;

            CheckEndingCondition();
        }

        // ===== 엔딩 조건 체크 =====

        /// <summary>
        /// 모든 영지가 플레이어 소유인지 확인하고, 조건 충족 시 엔딩을 트리거합니다.
        /// 외부에서도 호출 가능 (영지 변경 이벤트 등에서).
        /// </summary>
        public void CheckEndingCondition()
        {
            if (_isEndingTriggered) return;

            if (IsAllTerritoriesConquered())
            {
                TriggerEnding();
            }
        }

        /// <summary>
        /// TerritoryDatabase의 모든 영지가 PlayerOwned 상태인지 확인합니다.
        /// </summary>
        /// <returns>모든 영지 점령 시 true</returns>
        public static bool IsAllTerritoriesConquered()
        {
            var db = TerritoryDatabase.Instance;
            if (db == null)
            {
                Debug.LogWarning("[GameEndingManager] TerritoryDatabase.Instance가 null입니다.");
                return false;
            }

            var definitions = db.GetAllDefinitions();
            if (definitions == null)
            {
                Debug.LogWarning("[GameEndingManager] GetAllDefinitions()가 null을 반환했습니다.");
                return false;
            }

            int totalChecked = 0;
            int playerOwned = 0;

            foreach (var def in definitions)
            {
                // None 타입 스킵
                if (def.id.nation == NationType.None) continue;

                var state = db.GetState(def.id);
                if (state == null) continue;

                totalChecked++;

                if (state.ownership == TerritoryOwnership.PlayerOwned)
                {
                    playerOwned++;
                }
            }

            Debug.Log($"[GameEndingManager] 영지 상태 확인: {playerOwned}/{totalChecked} 점령");

            // 82개 영지 중 None 제외 = 82 (또는 None 국가가 없으면 82)
            // 정확히는 East(20) + West(20) + South(20) + North(20) + Empire(1) + Dracula(1) = 82
            return totalChecked > 0 && playerOwned >= totalChecked;
        }

        // ===== 엔딩 트리거 =====

        private void TriggerEnding()
        {
            if (_isEndingTriggered) return;
            _isEndingTriggered = true;
            _isSequenceActive = true;

            Debug.Log("[GameEndingManager] 👑 모든 영지 점령! 엔딩 시퀀스 시작!");

            // 1. 슬로우 모션
            Time.timeScale = _slowMotionTimeScale;

            // 2. 업적 해제 (진정한 왕)
            UnlockTrueEndingAchievement();

            // 3. EndingCreditsUI 시작
            var creditsUI = EndingCreditsUI.Instance;
            if (creditsUI != null)
            {
                // 콜백 연결
                creditsUI.OnNewGamePlusClicked = OnNewGamePlusSelected;
                creditsUI.OnMainMenuClicked = OnMainMenuSelected;
                creditsUI.StartEndingSequence();
            }
            else
            {
                Debug.LogError("[GameEndingManager] EndingCreditsUI.Instance가 null입니다!");
                _isSequenceActive = false;
            }
        }

        private void UnlockTrueEndingAchievement()
        {
            try
            {
                var achType = System.Type.GetType("ProjectName.UI.AchievementSystem");
                if (achType != null)
                {
                    var instanceField = achType.GetProperty("Instance",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    var instance = instanceField?.GetValue(null);

                    if (instance != null)
                    {
                        var unlockMethod = achType.GetMethod("Unlock",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        unlockMethod?.Invoke(instance, new object[] { "true_ending" });
                        Debug.Log("[GameEndingManager] 🏆 '진정한 왕' 업적 해제");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[GameEndingManager] 업적 해제 중 오류: {ex.Message}");
            }
        }

        // ===== 선택지 콜백 =====

        private void OnNewGamePlusSelected()
        {
            Debug.Log("[GameEndingManager] 🔄 뉴게임+ 선택됨");

            // TimeScale 복원
            Time.timeScale = 1f;

            // EndingCreditsUI 종료
            var creditsUI = EndingCreditsUI.Instance;
            if (creditsUI != null)
            {
                creditsUI.Close();
            }

            _isSequenceActive = false;

            // NG+ 시작
            NewGamePlusSystem.StartNewGamePlus();
        }

        private void OnMainMenuSelected()
        {
            Debug.Log("[GameEndingManager] 🏠 메인 메뉴 선택됨");

            // TimeScale 복원
            Time.timeScale = 1f;

            // EndingCreditsUI 종료
            var creditsUI = EndingCreditsUI.Instance;
            if (creditsUI != null)
            {
                creditsUI.Close();
            }

            _isSequenceActive = false;

            // 메인 메뉴로 로드
            LoadMainMenu();
        }

        private void LoadMainMenu()
        {
            var loadingManager = LoadingManager.Instance;
            if (loadingManager != null)
            {
                loadingManager.LoadSceneAsync("MainMenu", 0.5f, 0.5f);
            }
            else
            {
                Debug.LogWarning("[GameEndingManager] LoadingManager.Instance가 null입니다. SceneManager로 직접 로드합니다.");
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
            }
        }

        // ===== 종료 =====

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// 게임 오브젝트가 활성화될 때 TimeScale을 복원합니다 (씬 재시작 대비).
        /// </summary>
        private void OnDisable()
        {
            if (_isEndingTriggered && !_isSequenceActive)
            {
                Time.timeScale = 1f;
            }
        }
    }
}
