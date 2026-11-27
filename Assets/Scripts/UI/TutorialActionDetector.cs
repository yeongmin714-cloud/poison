using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectName.Core.Themes;
using ProjectName.Systems;
using ProjectName.Core.Data;

namespace ProjectName.UI
{
    /// <summary>
    /// T-Cycle-06: T4 설명창 11종 액션 감지 구현.
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
            // [132차] id("04_dash")는 PlayerPrefs 키/ShowGuide ID 호환 유지 — 문구만 신체계(더블탭 달리기)로 갱신
            new ActionEntry { id = "04_dash",                            description = "WASD 더블탭 달리기" },
            new ActionEntry { id = "05_roll",                            description = "Shift+방향 구르기" }, // [132차] 기존 "Space 구르기"는 구체계 잔재(Space=점프) — 신체계 문구로 갱신
            new ActionEntry { id = "06_chop_tree",                     description = "E키 나무 채집" },
            new ActionEntry { id = "07_mine_stone",                    description = "E키 돌 채집" },
            new ActionEntry { id = TutorialGuideData.ID_08_HERB_PICK,    description = "E키 약초 채집" },
            new ActionEntry { id = "09_inventory",                        description = "I키 인벤토리" },
            new ActionEntry { id = "10_craft",                            description = "E키 제작대" },
            new ActionEntry { id = TutorialGuideData.ID_11_RECIPE_BOOK,  description = "R키 레시피 북" }
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

        // [132차] 더블탭 WASD 감지 상태 — 같은 방향키를 0.3초 내 2회 press(튜토리얼 04 달리기 감지 전용)
        private readonly float[] _doubleTapWASDLastPress = { -10f, -10f, -10f, -10f }; // W/A/S/D 순, 음수 초기화(시작 직후 오인 방지)
        private const float DoubleTapWASDWindow = 0.3f;

        // 가드 캐시 (매 프레임 FindObjectsByType 방지)
        private GuardPlaceholder _cachedGuard;
        private float _guardCacheTimer;
        private const float GUARD_CACHE_INTERVAL = 1.5f;

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

            // 04: [132차] 더블탭 WASD 달리기 감지 — 같은 방향키 0.3초 내 2회 press(기존 Shift 대쉬 감지 교체)
            if (!_detectedState[3] && _keyboard != null)
            {
                if (DetectDoubleTapWASD())
                {
                    MarkActionDetected(3);
                }
            }

            // 05: [132차] Shift+방향 구르기 감지 — 신체계(Shift+방향) 양엣지(기존 Space 감지는 점프와 혼동되는 구체계 잔재)
            if (!_detectedState[4] && _keyboard != null)
            {
                if (DetectShiftDirectionRoll())
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

        /// <summary>[132차] 같은 방향키(W/A/S/D)를 0.3초 내 2회 press하면 true — 튜토리얼 04 감지 전용.</summary>
        private bool DetectDoubleTapWASD()
        {
            var wasd = new[] { _keyboard.wKey, _keyboard.aKey, _keyboard.sKey, _keyboard.dKey }; // KeyControl 배열(암시 타입)
            for (int i = 0; i < wasd.Length; i++)
            {
                if (!wasd[i].wasPressedThisFrame) continue;
                if (Time.time - _doubleTapWASDLastPress[i] < DoubleTapWASDWindow)
                    return true; // 더블탭 감지 — 감지는 1회면 충분(플래그는 MarkActionDetected가 관리)
                _doubleTapWASDLastPress[i] = Time.time;
            }
            return false;
        }

        /// <summary>[132차] Shift+방향 구르기 감지 — PlayerMovement 신체계와 동일 양엣지(튜토리얼 05 감지 전용).</summary>
        private bool DetectShiftDirectionRoll()
        {
            bool dirHeld = _keyboard.wKey.isPressed || _keyboard.aKey.isPressed
                || _keyboard.sKey.isPressed || _keyboard.dKey.isPressed;
            bool dirPressed = _keyboard.wKey.wasPressedThisFrame || _keyboard.aKey.wasPressedThisFrame
                || _keyboard.sKey.wasPressedThisFrame || _keyboard.dKey.wasPressedThisFrame;
            bool shiftHeld = _keyboard.leftShiftKey.isPressed || _keyboard.rightShiftKey.isPressed;
            bool shiftPressed = _keyboard.leftShiftKey.wasPressedThisFrame || _keyboard.rightShiftKey.wasPressedThisFrame;
            return (dirPressed && shiftHeld) || (shiftPressed && dirHeld);
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
                    }
                }

                // 07: 돌 채집 (ResourceNode.Stone)
                if (!_detectedState[6])
                {
                    var node = hit.GetComponent<ResourceNode>();
                    if (node != null && node.NodeType == ResourceNode.ResourceType.Stone)
                    {
                        MarkActionDetected(6);
                    }
                }

                // 08: 약초 채집 (ResourceNode.Herb)
                if (!_detectedState[7])
                {
                    var node = hit.GetComponent<ResourceNode>();
                    if (node != null && node.NodeType == ResourceNode.ResourceType.Herb)
                    {
                        MarkActionDetected(7);
                    }
                }

                // 10: 제작대 상호작용 (태그 기반)
                if (!_detectedState[9])
                {
                    if (hit.CompareTag("CraftingStation") || hit.CompareTag("Workbench"))
                    {
                        MarkActionDetected(9);
                    }
                }
            }
        }

        // ================================================================
        // T6 영지 액션 감지
        // ================================================================

        private void DetectTerritoryActions()
        {
            if (!_territoryGuidesStarted) return;

            // 12_guard_interact: 경비병과 상호작용
            if (!_detectedActions.Contains("12_guard_interact"))
            {
                var guard = FindGuardNearby();
                if (guard != null && Vector3.Distance(_player.position, guard.transform.position) <= 2f)
                {
                    if (Input.GetKeyDown(KeyCode.E))
                    {
                        MarkTerritoryAction("12_guard_interact");
                    }
                }
            }

            // 13_guard_info: GuardInfoWindow 열림 감지
            if (!_detectedActions.Contains("13_guard_info"))
            {
                if (_guardInfoWindow != null && _guardInfoWindow.IsVisible)
                {
                    MarkTerritoryAction("13_guard_info");
                }
            }

            // 18_shop: ShopWindow 열림
            if (!_detectedActions.Contains("18_shop"))
            {
                if (_shopWindow != null && _shopWindow.IsOpen)
                {
                    MarkTerritoryAction("18_shop");
                }
            }

            // 19_world_map: M키
            if (!_detectedActions.Contains("19_world_map") && _keyboard != null)
            {
                if (_keyboard.mKey.wasPressedThisFrame)
                {
                    MarkTerritoryAction("19_world_map");
                }
            }

            // 20_status: C키
            if (!_detectedActions.Contains("20_status") && _keyboard != null)
            {
                if (_keyboard.cKey.wasPressedThisFrame)
                {
                    MarkTerritoryAction("20_status");
                }
            }

            // 22_building_enter: 건물 출입 — TODO: IndoorSceneTransition 이벤트 기반 구현 필요
            // (주석 처리된 코드는 그대로 유지)
        }

        // ================================================================
        // T6 영지 액션 감지 헬퍼
        // ================================================================

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
            // 캐시 사용: 주기적 갱신 (매 프레임 FindObjectsByType 방지)
            _guardCacheTimer += Time.deltaTime;
            if (_guardCacheTimer >= GUARD_CACHE_INTERVAL || _cachedGuard == null)
            {
                _guardCacheTimer = 0f;
                var guards = FindObjectsByType<GuardPlaceholder>();
                if (guards.Length == 0)
                {
                    _cachedGuard = null;
                    return null;
                }

                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player == null)
                {
                    _cachedGuard = guards[0];
                    return _cachedGuard;
                }

                float minDist = 5f;
                GuardPlaceholder nearest = null;
                foreach (var g in guards)
                {
                    float d = Vector3.Distance(player.transform.position, g.transform.position);
                    if (d < minDist) { minDist = d; nearest = g; }
                }
                _cachedGuard = nearest;
            }

            return _cachedGuard;
        }

        // ================================================================
        // 상태 관리
        // ================================================================

        private void CheckAllDone()
        {
            _allDone = true;
            for (int i = 0; i < ALL_ACTIONS.Length; i++)
            {
                if (!_detectedState[i])
                {
                    _allDone = false;
                    break;
                }
            }
            if (_allDone && TutorialQuestManager.Instance != null)
            {
                TutorialQuestManager.Instance.OnAllGuidesComplete();
            }
        }

        private void MarkActionDetected(int index)
        {
            if (_detectedState[index]) return;
            _detectedState[index] = true;
            string key = PREFS_PREFIX + ALL_ACTIONS[index].id;
            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.Save();
            Debug.Log($"[TutorialActionDetector] ✅ 액션 감지: {ALL_ACTIONS[index].description}");

            if (TutorialGuideSystem.Instance != null)
                TutorialGuideSystem.Instance.ShowGuide(ALL_ACTIONS[index].id);

            CheckAllDone();
        }

        // ================================================================
        // 필드 (직렬화되지 않은 것들)
        // ================================================================

        [System.NonSerialized]
        private bool _territoryGuidesStarted = false;

        [System.NonSerialized]
        private GuardInfoWindow _guardInfoWindow;

        [System.NonSerialized]
        private ShopWindow _shopWindow;

        // ================================================================
        // 공개 메서드 (타 시스템에서 호출)
        // ================================================================

        public void StartTerritoryGuides()
        {
            _territoryGuidesStarted = true;
            Debug.Log("[TutorialActionDetector] T6 영지 안내 시작");
        }

        public void SetGuardInfoWindow(GuardInfoWindow window)
        {
            _guardInfoWindow = window;
        }

        public void SetShopWindow(ShopWindow window)
        {
            _shopWindow = window;
        }

        // ================================================================
        // T5.6.3: 미구현 기능 플래그 (추후 구현 시 제거)
        // ================================================================

        // TODO: T5.6.3: 미구현 기능 플래그 (추후 구현 시 제거)
        // private bool _featureFlag = false;
    }
}