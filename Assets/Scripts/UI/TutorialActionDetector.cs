using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectName.Core.Data;

namespace ProjectName.UI
{
    /// <summary>
    /// T-Cycle-06: T4 설명창 11종 액션 감지 구현.
    ///
    /// - Update()에서 플레이어의 각종 액션(이동/카메라 회전/공격/대쉬/구르기/채집/인벤토리/크래프트 등)을 감지
    /// - 최초 감지 시 PlayerPrefs에 기록하고 TutorialGuideSystem.ShowGuide() 호출
    /// - 각 가이드는 최초 1회만 발동
    /// - 11종 모두 완료 시 TutorialQuestManager.OnAllGuidesComplete() 호출
    /// </summary>
    public class TutorialActionDetector : MonoBehaviour
    {
        // ================================================================
        // 상수 — 각 액션 ID (PlayerPrefs 키 + ShowGuide 파라미터)
        // ================================================================

        private const string PREFS_PREFIX = "TutorialAction_";

        /// <summary>액션 정보 구조체</summary>
        private struct ActionEntry
        {
            public string id;           // PlayerPrefs 키 / ShowGuide ID
            public string description;  // 디버그/로깅용
        }

        private static readonly ActionEntry[] ALL_ACTIONS = new ActionEntry[]
        {
            new ActionEntry { id = TutorialGuideData.ID_01_MOVEMENT,     description = "WASD 이동" },
            new ActionEntry { id = TutorialGuideData.ID_02_CAMERA,       description = "카메라 회전 (우클릭 드래그)" },
            new ActionEntry { id = TutorialGuideData.ID_03_ATTACK,       description = "좌클릭 공격" },
            new ActionEntry { id = "04_dash",                            description = "Shift 대쉬" },
            new ActionEntry { id = "05_roll",                            description = "Space 구르기" },
            new ActionEntry { id = "06_chop_tree",                     description = "E키 나무 채집" },
            new ActionEntry { id = "07_mine_stone",                    description = "E키 돌 채집" },
            new ActionEntry { id = TutorialGuideData.ID_08_HERB_PICK,    description = "E키 약초 채집" },
            new ActionEntry { id = "09_inventory",                        description = "I키 인벤토리" },
            new ActionEntry { id = "10_craft",                            description = "E키 제작대" },
            new ActionEntry { id = TutorialGuideData.ID_11_RECIPE_BOOK,  description = "R키 레시피 북" },
        };

        // ================================================================
        // 싱글톤
        // ================================================================

        private static TutorialActionDetector _instance;
        private static bool _applicationIsQuitting;

        public static TutorialActionDetector Instance
        {
            get
            {
                if (_applicationIsQuitting)
                    return null;

                if (_instance == null)
                {
                    var go = new GameObject("TutorialActionDetector");
                    _instance = go.AddComponent<TutorialActionDetector>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // ================================================================
        // 내부 상태
        // ================================================================

        private Transform _player;
        private Keyboard _keyboard;
        private Mouse _mouse;
        private bool _allDone;
        private bool[] _detectedState; // 캐시: 액션별 감지 완료 여부
        private readonly HashSet<string> _detectedActions = new HashSet<string>(); // T6 영지 액션 감지
        private float _interactionRange = 3f;

        // ================================================================
        // MonoBehaviour 생명주기
        // ================================================================

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[TutorialActionDetector] 중복 인스턴스 파괴");
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            _keyboard = Keyboard.current;
            _mouse = Mouse.current;
            _detectedState = new bool[ALL_ACTIONS.Length];

            // PlayerPrefs에서 이미 감지된 액션 복원
            for (int i = 0; i < ALL_ACTIONS.Length; i++)
            {
                _detectedState[i] = PlayerPrefs.HasKey(PREFS_PREFIX + ALL_ACTIONS[i].id);
            }

            CheckAllDone();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void OnApplicationQuit()
        {
            _applicationIsQuitting = true;
        }

        private void Update()
        {
            if (_allDone)
                return;

            // 플레이어 참조 갱신
            if (_player == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                if (go != null)
                    _player = go.transform;
                else
                    return;
            }

            if (_keyboard == null)
                _keyboard = Keyboard.current;
            if (_mouse == null)
                _mouse = Mouse.current;

            // 01: WASD 이동 감지
            if (!_detectedState[0] && _keyboard != null)
            {
                if (_keyboard.wKey.wasPressedThisFrame ||
                    _keyboard.aKey.wasPressedThisFrame ||
                    _keyboard.sKey.wasPressedThisFrame ||
                    _keyboard.dKey.wasPressedThisFrame)
                {
                    MarkActionDetected(0);
                }
            }

            // 02: 카메라 회전 (우클릭 드래그) 감지
            if (!_detectedState[1] && _mouse != null)
            {
                if (_mouse.rightButton.wasPressedThisFrame)
                {
                    // 우클릭 누른 프레임에 델타가 있으면 드래그 시작
                    Vector2 delta = _mouse.delta.ReadValue();
                    if (delta.magnitude > 1f)
                    {
                        MarkActionDetected(1);
                    }
                }
                // 혹은 이미 누르고 있는 상태에서 움직임이 감지된 경우
                else if (_mouse.rightButton.isPressed)
                {
                    Vector2 delta = _mouse.delta.ReadValue();
                    if (delta.magnitude > 10f)
                    {
                        MarkActionDetected(1);
                    }
                }
            }

            // 03: 좌클릭 공격 감지
            if (!_detectedState[2] && _mouse != null)
            {
                if (_mouse.leftButton.wasPressedThisFrame)
                {
                    MarkActionDetected(2);
                }
            }

            // 04: Shift 대쉬 감지
            if (!_detectedState[3] && _keyboard != null)
            {
                if (_keyboard.leftShiftKey.wasPressedThisFrame ||
                    _keyboard.rightShiftKey.wasPressedThisFrame)
                {
                    MarkActionDetected(3);
                }
            }

            // 05: Space 구르기 감지 (태스크 명세: Space 키)
            if (!_detectedState[4] && _keyboard != null)
            {
                if (_keyboard.spaceKey.wasPressedThisFrame)
                {
                    MarkActionDetected(4);
                }
            }

            // 06~08, 10: E 키 상호작용 감지 (나무/돌/약초/크래프트)
            if (_keyboard != null && _keyboard.eKey.wasPressedThisFrame)
            {
                DetectEKeyInteraction();
            }

            // 09: I키 인벤토리 감지
            if (!_detectedState[8] && _keyboard != null)
            {
                if (_keyboard.iKey.wasPressedThisFrame)
                {
                    MarkActionDetected(8);
                }
            }

            // 11: R키 레시피 북 감지
            if (!_detectedState[10] && _keyboard != null)
            {
                if (_keyboard.rKey.wasPressedThisFrame)
                {
                    MarkActionDetected(10);
                }
            }

            // T6 영지 액션 감지 (territoryGuidesStarted 시 활성화)
            DetectTerritoryActions();
        }

        // ================================================================
        // E 키 상호작용 — 주변 오브젝트 종류별 감지
        // ================================================================

        private void DetectEKeyInteraction()
        {
            if (_player == null) return;

            Collider[] hits = Physics.OverlapSphere(_player.position, _interactionRange);

            foreach (var hit in hits)
            {
                // 06: 나무 채집 (ResourceNode.Wood)
                if (!_detectedState[5])
                {
                    var node = hit.GetComponent<ResourceNode>();
                    if (node != null && node.NodeType == ResourceNode.ResourceType.Wood)
                    {
                        MarkActionDetected(5);
                        return;
                    }
                }

                // 08: 약초 채집 (HerbPickup)
                if (!_detectedState[7])
                {
                    var herb = hit.GetComponent<HerbPickup>();
                    if (herb != null && herb.IsAvailable)
                    {
                        MarkActionDetected(7);
                        return;
                    }
                }

                // 07: 돌 채집 (ResourceNode.Stone)
                if (!_detectedState[6])
                {
                    var node = hit.GetComponent<ResourceNode>();
                    if (node != null && node.NodeType == ResourceNode.ResourceType.Stone)
                    {
                        MarkActionDetected(6);
                        return;
                    }
                }

                // 10: 크래프트 테이블 (CraftingStation)
                if (!_detectedState[9])
                {
                    var craft = hit.GetComponent<CraftingStation>();
                    if (craft != null)
                    {
                        MarkActionDetected(9);
                        return;
                    }
                }
            }
        }

        // ================================================================
        // 액션 감지 처리
        // ================================================================

        private void MarkActionDetected(int index)
        {
            if (_detectedState[index])
                return;

            string actionId = ALL_ACTIONS[index].id;
            string desc = ALL_ACTIONS[index].description;

            _detectedState[index] = true;
            PlayerPrefs.SetInt(PREFS_PREFIX + actionId, 1);
            PlayerPrefs.Save();

            Debug.Log($"[TutorialActionDetector] ✅ 액션 감지: '{actionId}' ({desc})");

            // ShowGuide 호출 (가이드 데이터가 존재하는 경우에만 표시)
            var guideSystem = TutorialGuideSystem.Instance;
            if (guideSystem != null)
            {
                guideSystem.ShowGuide(actionId);
            }

            // 11종 모두 완료 체크
            CheckAllDone();
        }

        private void CheckAllDone()
        {
            if (_allDone)
                return;

            for (int i = 0; i < _detectedState.Length; i++)
            {
                if (!_detectedState[i])
                    return;
            }

            _allDone = true;
            Debug.Log("[TutorialActionDetector] 🎉 11종 액션 모두 감지 완료!");

            // TutorialQuestManager에 모든 가이드 완료 알림
            var questManager = TutorialQuestManager.Instance;
            if (questManager != null)
            {
                questManager.OnAllGuidesComplete();
            }
        }

        // ================================================================
        // 공개 메서드
        // ================================================================

        /// <summary>
        /// 학습 진행률을 문자열로 반환
        /// </summary>
        public string GetProgressString()
        {
            return $"{DetectedCount}/{TotalCount} actions completed";
        }

        /// <summary>
        /// 특정 액션이 이미 감지되었는지 확인합니다.
        /// </summary>
        public bool IsActionDetected(string actionId)
        {
            return PlayerPrefs.HasKey(PREFS_PREFIX + actionId);
        }

        // ===== T6 영지 액션 감지 =====

        private bool _territoryGuidesStarted = false;

        /// <summary>
        /// 영지 진입 후 T6 가이드 액션 감지 시작
        /// </summary>
        public void StartTerritoryGuides()
        {
            _territoryGuidesStarted = true;
        }

        private GuardPlaceholder _cachedGuard;
        private GuardInfoWindow _cachedGuardInfoWindow;
        private ShopWindow _cachedShopWindow;
        private float _territoryRefreshTimer;
        private const float TERRITORY_REFRESH_INTERVAL = 0.5f;

        /// <summary>
        /// T6 영지 액션 감지
        /// </summary>
        private void DetectTerritoryActions()
        {
            if (!_territoryGuidesStarted) return;

            // FindObjectOfType 캐싱 — 매 프레임 대신 간격 체크
            _territoryRefreshTimer -= Time.deltaTime;
            if (_territoryRefreshTimer <= 0f)
            {
                _territoryRefreshTimer = TERRITORY_REFRESH_INTERVAL;
                if (_cachedGuardInfoWindow == null)
                    _cachedGuardInfoWindow = FindObjectOfType<GuardInfoWindow>();
                if (_cachedShopWindow == null)
                    _cachedShopWindow = FindObjectOfType<ShopWindow>();
                if (_cachedGuard == null && !_detectedActions.Contains("12_guard_interact"))
                    _cachedGuard = FindGuardNearby();
            }

            // 12_guard_interact: E키 + GuardPlaceholder
            if (!_detectedActions.Contains("12_guard_interact") && _cachedGuard != null
                && _keyboard != null && _keyboard.eKey.wasPressedThisFrame)
            {
                MarkTerritoryAction("12_guard_interact");
            }

            // 13_guard_info: GuardInfoWindow 열림 감지
            if (!_detectedActions.Contains("13_guard_info")
                && _cachedGuardInfoWindow != null && _cachedGuardInfoWindow.IsOpen)
            {
                MarkTerritoryAction("13_guard_info");
            }

            // 18_shop: ShopWindow 열림
            if (!_detectedActions.Contains("18_shop")
                && _cachedShopWindow != null && _cachedShopWindow.IsOpen)
            {
                MarkTerritoryAction("18_shop");
            }

            // 19_world_map: M키
            if (!_detectedActions.Contains("19_world_map") && _keyboard != null
                && _keyboard.mKey.wasPressedThisFrame)
            {
                MarkTerritoryAction("19_world_map");
            }

            // 20_status: C키
            if (!_detectedActions.Contains("20_status") && _keyboard != null
                && _keyboard.cKey.wasPressedThisFrame)
            {
                MarkTerritoryAction("20_status");
            }

            // 22_building_enter: 건물 출입 — TODO: IndoorSceneTransition 이벤트 기반 구현 필요
            // TutorialLordSequence.Step9에서 IndoorSceneTransition.OnEnterBuilding += () => MarkTerritoryAction("22_building_enter")

            // T6 5종 완료 시 메시지
            if (_territoryGuidesStarted && !_t6Complete)
            {
                string[] t6Ids = { "12_guard_interact", "13_guard_info", "18_shop", "19_world_map", "20_status" };
                int t6Done = 0;
                foreach (var id in t6Ids)
                    if (_detectedActions.Contains(id)) t6Done++;

                if (t6Done >= t6Ids.Length)
                {
                    _t6Complete = true;
                    Debug.Log("[TutorialActionDetector] 🎉 튜토리얼 완료!");
                }
            }
        }

        /// <summary>T6 영지 액션 감지 마킹 헬퍼</summary>
        private void MarkTerritoryAction(string actionId)
        {
            if (_detectedActions.Contains(actionId)) return;
            _detectedActions.Add(actionId);
            PlayerPrefs.SetInt(PREFS_PREFIX + actionId, 1);
            PlayerPrefs.Save();
            Debug.Log($"[TutorialActionDetector] ✅ T6 액션 감지: '{actionId}'");

            if (TutorialGuideSystem.Instance != null)
                TutorialGuideSystem.Instance.ShowGuide(actionId);
        }

        private GuardPlaceholder FindGuardNearby()
        {
            var guards = FindObjectsOfType<GuardPlaceholder>();
            if (guards.Length == 0) return null;

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return guards[0];

            float minDist = 5f;
            GuardPlaceholder nearest = null;
            foreach (var g in guards)
            {
                float d = Vector3.Distance(player.transform.position, g.transform.position);
                if (d < minDist) { minDist = d; nearest = g; }
            }
            return nearest;
        }

        private bool _t6Complete = false;

        /// <summary>
        /// 모든 액션 감지 상태를 초기화합니다 (디버그용).
        /// </summary>
        public void ResetAllActions()
        {
            for (int i = 0; i < ALL_ACTIONS.Length; i++)
            {
                string key = PREFS_PREFIX + ALL_ACTIONS[i].id;
                if (PlayerPrefs.HasKey(key))
                    PlayerPrefs.DeleteKey(key);
                _detectedState[i] = false;
            }
            PlayerPrefs.Save();
            _allDone = false;
            Debug.Log("[TutorialActionDetector] 🔄 모든 액션 감지 상태 초기화 완료");
        }

        /// <summary>
        /// 현재까지 감지된 액션 수를 반환합니다.
        /// </summary>
        public int DetectedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _detectedState.Length; i++)
                {
                    if (_detectedState[i]) count++;
                }
                return count;
            }
        }

        /// <summary>
        /// 전체 액션 수를 반환합니다.
        /// </summary>
        public int TotalCount => ALL_ACTIONS.Length;

#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.Contains("Test"))
                return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 600));
            GUILayout.Label("[TutorialActionDetector] 디버그", GUI.skin.box);
            for (int i = 0; i < ALL_ACTIONS.Length; i++)
            {
                string status = _detectedState[i] ? "✅" : "⬜";
                GUILayout.Label($"  {status} {ALL_ACTIONS[i].description}");
            }
            GUILayout.Label($"감지: {DetectedCount}/{TotalCount}");
            if (GUILayout.Button("모든 액션 리셋"))
            {
                ResetAllActions();
            }
            GUILayout.EndArea();
        }
#endif
    }
}