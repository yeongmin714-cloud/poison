using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.UI
{
    /// <summary>
    /// ⏱️ 전투 기록 로그 UI (IMGUI 싱글톤).
    /// L키 열기/닫기, ESC 닫기.
    /// 스크롤 가능한 로그 목록 (최근 100개), 전체 지우기 버튼.
    /// </summary>
    public class CombatLogUI : MonoBehaviour
    {
        public static CombatLogUI Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private KeyCode _toggleKey = KeyCode.L;
        [SerializeField] private KeyCode _closeKey = KeyCode.Escape;

        private bool _isVisible = false;
        private Vector2 _scrollPos;

        // Styles (lazy init)
        private GUIStyle _styleNormal;
        private GUIStyle _styleDamage;
        private GUIStyle _styleHeal;
        private GUIStyle _styleKill;
        private GUIStyle _styleWarning;
        private GUIStyle _styleTimestamp;
        private GUIStyle _styleHeader;

        private const float WINDOW_WIDTH = 500f;
        private const float WINDOW_HEIGHT = 450f;
        private const float ENTRY_HEIGHT = 22f;
        private const int MAX_VISIBLE_ENTRIES = 100;

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

        private void Update()
        {
            // L키 토글
            if (Input.GetKeyDown(_toggleKey))
            {
                _isVisible = !_isVisible;
            }

            // ESC 닫기
            if (_isVisible && Input.GetKeyDown(_closeKey))
            {
                _isVisible = false;
            }
        }

        private void OnGUI()
        {
            if (!_isVisible) return;

            EnsureStyles();

            float x = Screen.width - WINDOW_WIDTH - 20f;
            float y = 60f;

            // 배경
            GUI.Box(new Rect(x, y, WINDOW_WIDTH, WINDOW_HEIGHT), "");

            // 제목
            GUI.Label(new Rect(x + 10, y + 5, WINDOW_WIDTH - 20, 24), "⚔️ 전투 기록", _styleHeader);

            // 스크롤 가능한 로그 목록
            float listY = y + 32f;
            float listH = WINDOW_HEIGHT - 90f;

            var entries = CombatLog.GetRecentEntries(MAX_VISIBLE_ENTRIES);
            float viewH = entries.Count * ENTRY_HEIGHT;

            GUI.BeginGroup(new Rect(x + 10, listY, WINDOW_WIDTH - 20, listH));
            _scrollPos = GUI.BeginScrollView(
                new Rect(0, 0, WINDOW_WIDTH - 20, listH),
                _scrollPos,
                new Rect(0, 0, WINDOW_WIDTH - 36, viewH)
            );

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                float iy = i * ENTRY_HEIGHT;

                // [MM:SS] 타임스탬프
                int minutes = Mathf.FloorToInt(entry.timestamp / 60f);
                int seconds = Mathf.FloorToInt(entry.timestamp % 60f);
                string timeStr = $"[{minutes:D2}:{seconds:D2}]";

                GUI.Label(new Rect(0, iy, 60, ENTRY_HEIGHT), timeStr, _styleTimestamp);

                // 메시지 (타입별 색상)
                GUIStyle style = GetStyleForType(entry.type);
                GUI.Label(new Rect(62, iy, WINDOW_WIDTH - 100, ENTRY_HEIGHT), entry.message, style);
            }

            GUI.EndScrollView();
            GUI.EndGroup();

            // 하단 "전체 지우기" 버튼
            float btnY = y + WINDOW_HEIGHT - 48f;
            if (GUI.Button(new Rect(x + WINDOW_WIDTH / 2 - 60, btnY, 120, 30), "전체 지우기"))
            {
                CombatLog.Clear();
            }
        }

        private GUIStyle GetStyleForType(LogType type)
        {
            switch (type)
            {
                case LogType.Damage:    return _styleDamage;
                case LogType.Heal:      return _styleHeal;
                case LogType.Kill:      return _styleKill;
                case LogType.Warning:   return _styleWarning;
                default:                return _styleNormal;
            }
        }

        private void EnsureStyles()
        {
            if (_styleNormal != null) return;

            _styleNormal = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = Color.gray }
            };

            _styleDamage = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = Color.red }
            };

            _styleHeal = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = Color.green }
            };

            _styleKill = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.yellow }
            };

            _styleWarning = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = new Color(1f, 0.5f, 0f) } // 주황
            };

            _styleTimestamp = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
            };

            _styleHeader = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
        }
    }
}