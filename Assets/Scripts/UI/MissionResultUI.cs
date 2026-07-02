using System.Collections.Generic;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.UI
{
    /// <summary>
    /// [5.3.9] 자동 임무 결과 알림 UI (IMGUI)
    /// 
    /// AutoMissionManager.OnMissionResultsReady를 구독하여
    /// 우측 하단에 간략 결과 알림을 표시하고,
    /// M키로 전체 결과 히스토리 창을 토글합니다.
    /// </summary>
    public class MissionResultUI : MonoBehaviour
    {
        public static MissionResultUI Instance { get; private set; }

        [Header("설정")]
        [SerializeField] private KeyCode _historyKey = KeyCode.M;
        [SerializeField] private float _notificationDuration = 10f;
        [SerializeField] private int _maxHistory = 20;

        // ===== 알림 상태 =====
        private bool _showNotification = false;
        private float _notificationTimer;
        private string _notificationText = "";

        // ===== 히스토리 =====
        private bool _showHistory = false;
        private readonly List<HistoryEntry> _history = new List<HistoryEntry>(20);
        private Vector2 _historyScrollPos;

        // ===== 스타일 =====
        private GUIStyle _styleTitle;
        private GUIStyle _styleLabel;
        private GUIStyle _styleValue;
        private GUIStyle _styleTimestamp;
        private GUIStyle _styleBg;
        private bool _stylesInitialized;

        // ===== 상수 =====
        private const float NOTIF_WIDTH = 420f;
        private const float NOTIF_HEIGHT = 100f;
        private const float HISTORY_WIDTH = 600f;
        private const float HISTORY_HEIGHT = 500f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            AutoMissionManager.OnMissionResultsReady += OnMissionResults;
        }

        private void OnDisable()
        {
            AutoMissionManager.OnMissionResultsReady -= OnMissionResults;
        }

        private void Update()
        {
            // M키로 히스토리 토글
            if (Input.GetKeyDown(_historyKey))
            {
                _showHistory = !_showHistory;
            }

            // 알림 타이머
            if (_showNotification)
            {
                _notificationTimer -= Time.deltaTime;
                if (_notificationTimer <= 0f)
                {
                    _showNotification = false;
                }
            }
        }

        private void OnMissionResults(MissionResultsBatch batch)
        {
            // 알림 텍스트 생성
            _notificationText = BuildGroupedSummary(batch);
            _showNotification = true;
            _notificationTimer = _notificationDuration;

            // 히스토리에 추가
            _history.Add(new HistoryEntry
            {
                timestamp = Time.time,
                text = _notificationText
            });
            while (_history.Count > _maxHistory)
                _history.RemoveAt(0);
        }

        private void OnGUI()
        {
            EnsureStyles();

            // 알림 패널 (우측 하단)
            if (_showNotification && !string.IsNullOrEmpty(_notificationText))
            {
                DrawNotificationPanel();
            }

            // 히스토리 창
            if (_showHistory)
            {
                DrawHistoryWindow();
            }
        }

        // ===== 알림 패널 =====

        private void DrawNotificationPanel()
        {
            float alpha = Mathf.Clamp01(_notificationTimer / 2f); // 2초간 페이드
            var prevColor = GUI.color;
            GUI.color = new Color(prevColor.r, prevColor.g, prevColor.b, alpha);

            float x = Screen.width - NOTIF_WIDTH - 20f;
            float y = Screen.height - NOTIF_HEIGHT - 20f;

            // 배경
            GUI.color = new Color(0.08f, 0.08f, 0.1f, 0.85f * alpha);
            GUI.Box(new Rect(x, y, NOTIF_WIDTH, NOTIF_HEIGHT), "");
            GUI.color = new Color(0.3f, 0.8f, 0.3f, 0.85f * alpha);
            GUI.Box(new Rect(x, y, NOTIF_WIDTH, 2f), ""); // 상단 라인
            GUI.color = prevColor;

            // 제목
            GUI.Label(new Rect(x + 10, y + 6, NOTIF_WIDTH - 20, 22), "📋 임무 결과", _styleTitle);
            GUI.Label(new Rect(x + 10, y + 32, NOTIF_WIDTH - 20, NOTIF_HEIGHT - 40), _notificationText, _styleLabel);

            // 원래 색상 복원
            GUI.color = prevColor;
        }

        // ===== 히스토리 창 =====

        private void DrawHistoryWindow()
        {
            float x = (Screen.width - HISTORY_WIDTH) / 2f;
            float y = (Screen.height - HISTORY_HEIGHT) / 2f;

            // 배경
            GUI.Box(new Rect(x, y, HISTORY_WIDTH, HISTORY_HEIGHT), "");

            // 닫기 버튼
            if (GUI.Button(new Rect(x + HISTORY_WIDTH - 30, y + 5, 24, 24), "X"))
            {
                _showHistory = false;
                return;
            }

            float cy = y + 15f;
            GUI.Label(new Rect(x + 15, cy, HISTORY_WIDTH - 30, 28), "📋 임무 결과 기록", _styleTitle);
            cy += 34f;

            float listY = cy;
            float listH = HISTORY_HEIGHT - (cy - y) - 20f;

            GUI.BeginGroup(new Rect(x + 10, listY, HISTORY_WIDTH - 20, listH));

            if (_history.Count == 0)
            {
                GUI.Label(new Rect(10, 10, HISTORY_WIDTH - 40, 24), "기록된 임무 결과가 없습니다.", _styleLabel);
            }
            else
            {
                float entryH = 38f;
                float viewH = _history.Count * entryH;

                _historyScrollPos = GUI.BeginScrollView(
                    new Rect(0, 0, HISTORY_WIDTH - 20, listH),
                    _historyScrollPos,
                    new Rect(0, 0, HISTORY_WIDTH - 40, viewH)
                );

                for (int i = 0; i < _history.Count; i++)
                {
                    var entry = _history[i];
                    float iy = i * entryH;

                    GUI.Box(new Rect(0, iy, HISTORY_WIDTH - 40, entryH - 2), "");

                    // 타임스탬프
                    string timeStr = FormatTimestamp(entry.timestamp);
                    GUI.Label(new Rect(8, iy + 2, 100, 16), timeStr, _styleTimestamp);

                    // 내용
                    GUI.Label(new Rect(8, iy + 18, HISTORY_WIDTH - 60, 18), entry.text, _styleValue);
                }

                GUI.EndScrollView();
            }

            GUI.EndGroup();
        }

        // ===== 그룹핑 =====

        /// <summary>
        /// 결과를 역할별로 그룹핑하여 한 줄 요약 문자열 생성
        /// 예: "⛏️ 광부: 나무×3, 돌×2 | 🏹 사냥꾼: 고기×5 | 🌿 약초꾼: 일반민들레×2"
        /// </summary>
        private string BuildGroupedSummary(MissionResultsBatch batch)
        {
            var parts = new List<string>();

            // 광부 결과 그룹핑
            if (batch.mineResults != null && batch.mineResults.Count > 0)
            {
                string mineSummary = GroupMineResults(batch.mineResults);
                if (!string.IsNullOrEmpty(mineSummary))
                    parts.Add("⛏️ 광부: " + mineSummary);
            }

            // 사냥 결과 그룹핑
            if (batch.huntResults != null && batch.huntResults.Count > 0)
            {
                string huntSummary = GroupHuntResults(batch.huntResults);
                if (!string.IsNullOrEmpty(huntSummary))
                    parts.Add("🏹 사냥꾼: " + huntSummary);
            }

            // 약초 결과 그룹핑
            if (batch.gatherResults != null && batch.gatherResults.Count > 0)
            {
                string gatherSummary = GroupGatherResults(batch.gatherResults);
                if (!string.IsNullOrEmpty(gatherSummary))
                    parts.Add("🌿 약초꾼: " + gatherSummary);
            }

            if (parts.Count == 0)
                return "수행 중인 임무 없음";

            return string.Join(" | ", parts);
        }

        private string GroupMineResults(List<MiningMission.MineResult> results)
        {
            var itemCounts = new Dictionary<string, int>();
            int successCount = 0;

            foreach (var r in results)
            {
                if (!r.success) continue;
                successCount++;
                string key = string.IsNullOrEmpty(r.resourceName) ? "자원" : r.resourceName;
                if (itemCounts.ContainsKey(key))
                    itemCounts[key] += r.itemsGathered;
                else
                    itemCounts[key] = r.itemsGathered;
            }

            if (successCount == 0)
            {
                // 실패 메시지 하나만 대표로
                foreach (var r in results)
                {
                    if (!string.IsNullOrEmpty(r.message))
                        return r.message;
                }
                return "채광 실패";
            }

            var items = new List<string>();
            foreach (var kv in itemCounts)
            {
                string displayName = GetResourceDisplayName(kv.Key);
                items.Add($"{displayName}×{kv.Value}");
            }

            return string.Join(", ", items);
        }

        private string GroupHuntResults(List<HuntingMission.HuntResult> results)
        {
            int totalItems = 0;
            int successCount = 0;

            foreach (var r in results)
            {
                if (r.success)
                {
                    successCount++;
                    totalItems += r.itemsGathered;
                }
            }

            if (successCount == 0)
            {
                foreach (var r in results)
                {
                    if (!string.IsNullOrEmpty(r.message))
                        return r.message;
                }
                return "사냥 실패";
            }

            return $"고기/재료×{totalItems} ({successCount}마리)";
        }

        private string GroupGatherResults(List<HerbGatheringMission.GatherResult> results)
        {
            var herbCounts = new Dictionary<string, int>();
            int successCount = 0;

            foreach (var r in results)
            {
                if (!r.success) continue;
                successCount++;
                string key = string.IsNullOrEmpty(r.herbName) ? "약초" : r.herbName;
                if (herbCounts.ContainsKey(key))
                    herbCounts[key] += r.herbsGathered;
                else
                    herbCounts[key] = r.herbsGathered;
            }

            if (successCount == 0)
            {
                foreach (var r in results)
                {
                    if (!string.IsNullOrEmpty(r.message))
                        return r.message;
                }
                return "채집 실패";
            }

            var items = new List<string>();
            foreach (var kv in herbCounts)
            {
                items.Add($"{kv.Key}×{kv.Value}");
            }

            return string.Join(", ", items);
        }

        private static string GetResourceDisplayName(string resourceId)
        {
            switch (resourceId)
            {
                case "wood": return "나무";
                case "stone": return "돌";
                case "iron_ore": return "철광석";
                case "iron_ingot": return "철괴";
                default: return resourceId;
            }
        }

        private static string FormatTimestamp(float time)
        {
            int minutes = Mathf.FloorToInt(time / 60f);
            int seconds = Mathf.FloorToInt(time % 60f);
            return $"{minutes:00}:{seconds:00}";
        }

        // ===== 스타일 =====

        private void EnsureStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;

            _styleTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            _styleLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
                normal = { textColor = new Color(0.8f, 0.9f, 0.8f) }
            };

            _styleValue = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.7f, 0.9f, 0.7f) }
            };

            _styleTimestamp = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                normal = { textColor = Color.gray }
            };

            _styleBg = new GUIStyle(GUI.skin.box)
            {
                normal = { background = Texture2D.whiteTexture }
            };
        }

        // ===== 히스토리 엔트리 =====

        private struct HistoryEntry
        {
            public float timestamp;
            public string text;
        }
    }
}
