using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.UI
{
    /// <summary>
    /// 📊 게임 통계 화면 — IMGUI 싱글톤.
    /// U키로 열기/닫기, ESC키로 닫기.
    /// GameStatsCollector에서 통계를 읽어 표시합니다.
    /// </summary>
    public class GameStatsWindow : MonoBehaviour
    {
        // ======================================================================
        // 싱글톤
        // ======================================================================
        public static GameStatsWindow Instance { get; private set; }

        // ======================================================================
        // 설정
        // ======================================================================
        [Header("Window Settings")]
        [SerializeField] private KeyCode _toggleKey = KeyCode.U;
        [SerializeField] private KeyCode _closeKey = KeyCode.Escape;
        [SerializeField] private float _windowWidth = 420f;
        [SerializeField] private float _windowHeight = 580f;
        [SerializeField] private Color _bgColor = new Color(0f, 0f, 0f, 0.88f);
        [SerializeField] private Color _titleColor = new Color(1f, 0.85f, 0.3f, 1f);
        [SerializeField] private Color _statLabelColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        [SerializeField] private Color _statValueColor = new Color(0.5f, 1f, 0.5f, 1f);
        [SerializeField] private Color _sectionColor = new Color(0.6f, 0.8f, 1f, 1f);
        [SerializeField] private Color _borderColor = new Color(0.4f, 0.4f, 0.4f, 1f);

        // ======================================================================
        // 상태
        // ======================================================================
        private bool _isOpen;
        private Vector2 _scrollPos = Vector2.zero;

        // ======================================================================
        // 스타일 (GC 방지 — 캐싱)
        // ======================================================================
        private GUIStyle _styleBg;
        private GUIStyle _styleTitle;
        private GUIStyle _styleSection;
        private GUIStyle _styleStatLabel;
        private GUIStyle _styleStatValue;
        private GUIStyle _styleCloseButton;
        private GUIStyle _styleStatRow;
        private bool _stylesInit;
        private Texture2D _bgTexture;
        private Texture2D _borderTexture;

        private const float TITLE_FONT_SIZE = 28f;
        private const float SECTION_FONT_SIZE = 18f;
        private const float STAT_FONT_SIZE = 16f;
        private const float BUTTON_FONT_SIZE = 14f;
        private const float ROW_HEIGHT = 24f;
        private const float SECTION_PADDING = 6f;
        private const float CONTENT_PADDING = 10f;

        // ======================================================================
        // 생명주기
        // ======================================================================

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

        private void Start()
        {
            // GameStatsCollector가 아직 로드되지 않았으면 로드
            // (GameStatsCollector는 정적 클래스이므로 LoadStats는 어디서든 한 번 호출되어야 함)
            GameStatsCollector.LoadStats();
            Debug.Log("[GameStatsWindow] 초기화 완료. U키로 통계창 열기/닫기");
        }

        private void OnApplicationQuit()
        {
            GameStatsCollector.SaveStats();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            // 모바일에서 백그라운드 전환 시 저장
            if (pauseStatus)
                GameStatsCollector.SaveStats();
        }

        private void Update()
        {
            // 키 입력 처리
            if (Input.GetKeyDown(_toggleKey))
            {
                Toggle();
            }

            // ESC: 열려있을 때만 닫기
            if (_isOpen && Input.GetKeyDown(_closeKey))
            {
                Close();
            }
        }

        // ======================================================================
        // 공개 메서드
        // ======================================================================

        public void Open()
        {
            if (_isOpen) return;
            _isOpen = true;
            _scrollPos = Vector2.zero;
            Debug.Log("[GameStatsWindow] 📊 통계창 열림");
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            Debug.Log("[GameStatsWindow] 통계창 닫힘");
        }

        public void Toggle()
        {
            if (_isOpen)
                Close();
            else
                Open();
        }

        public bool IsOpen => _isOpen;

        // ======================================================================
        // IMGUI
        // ======================================================================

        private void InitStyles()
        {
            if (_stylesInit) return;

            _bgTexture = MakeTexture(1, 1, _bgColor);
            _borderTexture = MakeTexture(1, 1, _borderColor);

            _styleBg = new GUIStyle
            {
                normal = { background = _bgTexture },
                border = new RectOffset(2, 2, 2, 2)
            };

            _styleTitle = new GUIStyle
            {
                fontSize = Mathf.RoundToInt(TITLE_FONT_SIZE),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _titleColor }
            };

            _styleSection = new GUIStyle
            {
                fontSize = Mathf.RoundToInt(SECTION_FONT_SIZE),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = _sectionColor },
                padding = new RectOffset(8, 0, 2, 2)
            };

            _styleStatLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(STAT_FONT_SIZE),
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = _statLabelColor },
                padding = new RectOffset(12, 0, 2, 2)
            };

            _styleStatValue = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(STAT_FONT_SIZE),
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = _statValueColor },
                padding = new RectOffset(0, 12, 2, 2)
            };

            _styleCloseButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(BUTTON_FONT_SIZE),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                hover = { textColor = new Color(1f, 0.85f, 0.3f) }
            };

            _styleStatRow = new GUIStyle
            {
                normal = { background = null }
            };

            _stylesInit = true;
        }

        private void OnGUI()
        {
            if (!_isOpen) return;

            InitStyles();

            // 창 위치 (중앙)
            float x = (Screen.width - _windowWidth) * 0.5f;
            float y = (Screen.height - _windowHeight) * 0.5f;

            // 외부 테두리 박스
            DrawBorderBox(x, y, _windowWidth, _windowHeight);

            // 내부 배경
            GUI.Box(new Rect(x + 2, y + 2, _windowWidth - 4, _windowHeight - 4), "", _styleBg);

            // 타이틀
            float titleY = y + 8;
            float titleH = 36;
            GUI.Label(new Rect(x, titleY, _windowWidth, titleH), "📊 게임 통계", _styleTitle);

            // 구분선 (타이틀 아래)
            float dividerY = titleY + titleH + 4;
            DrawDivider(x + 10, dividerY, _windowWidth - 20);

            // 스크롤 가능한 통계 영역
            float contentX = x + CONTENT_PADDING;
            float contentY = dividerY + 6;
            float contentW = _windowWidth - CONTENT_PADDING * 2;
            float contentH = _windowHeight - (dividerY - y) - 52; // 닫기 버튼 공간 확보

            // 스크롤뷰 시작
            float totalContentH = CalculateTotalContentHeight();
            _scrollPos = GUI.BeginScrollView(
                new Rect(contentX, contentY, contentW, contentH),
                _scrollPos,
                new Rect(0, 0, contentW - 20, totalContentH)
            );

            float currentY = 0f;

            // ===== ⏱️ 플레이타임 =====
            DrawSectionHeader(0, ref currentY, contentW, "⏱️ 플레이타임");
            DrawStatRow(0, ref currentY, contentW, "총 플레이 시간", GameStatsCollector.FormatTime(GameStatsCollector.PlayTime));

            // ===== 🧟 전투 통계 =====
            DrawSectionHeader(0, ref currentY, contentW, "⚔️ 전투");
            DrawStatRow(0, ref currentY, contentW, "처치한 몬스터", GameStatsCollector.Kills.ToString("N0"));
            DrawStatRow(0, ref currentY, contentW, "사망 횟수", GameStatsCollector.Deaths.ToString("N0"));

            // ===== 🏆 퀘스트 & 탐험 =====
            DrawSectionHeader(0, ref currentY, contentW, "🏆 퀘스트 & 탐험");
            DrawStatRow(0, ref currentY, contentW, "완료한 퀘스트", GameStatsCollector.CompletedQuests.ToString("N0"));
            DrawStatRow(0, ref currentY, contentW, "이동 거리", GameStatsCollector.FormatDistance(GameStatsCollector.DistanceTraveled));

            // ===== 👑 영지 & 전쟁 =====
            DrawSectionHeader(0, ref currentY, contentW, "👑 영지 & 전쟁");
            DrawStatRow(0, ref currentY, contentW, "점령 영지", GameStatsCollector.OwnedTerritories.ToString("N0"));
            DrawStatRow(0, ref currentY, contentW, "전쟁 참여", GameStatsCollector.WarParticipations.ToString("N0"));
            DrawStatRow(0, ref currentY, contentW, "암살한 영주", GameStatsCollector.CompletedRevenge.ToString("N0"));

            // ===== 🏟️ 아레나 =====
            DrawSectionHeader(0, ref currentY, contentW, "🏟️ 아레나");
            DrawStatRow(0, ref currentY, contentW, "승리", GameStatsCollector.ArenaWins.ToString("N0"));
            DrawStatRow(0, ref currentY, contentW, "패배", GameStatsCollector.ArenaLosses.ToString("N0"));
            DrawStatRow(0, ref currentY, contentW, "최고 연승", GameStatsCollector.ArenaBestStreak.ToString("N0"));
            string arenaTotal = GameStatsCollector.ArenaTotal > 0
                ? $"{GameStatsCollector.ArenaWins} / {GameStatsCollector.ArenaTotal}"
                : "0 / 0";
            DrawStatRow(0, ref currentY, contentW, "전적", arenaTotal);

            // ===== 🐟 수집 =====
            DrawSectionHeader(0, ref currentY, contentW, "🐟 수집");
            DrawStatRow(0, ref currentY, contentW, "획득 물고기", GameStatsCollector.FishCaught.ToString("N0"));

            // ===== 💰 경제 =====
            DrawSectionHeader(0, ref currentY, contentW, "💰 경제");
            DrawStatRow(0, ref currentY, contentW, "획득 골드", GameStatsCollector.FormatGold(GameStatsCollector.GoldEarned));
            DrawStatRow(0, ref currentY, contentW, "사용 골드", GameStatsCollector.FormatGold(GameStatsCollector.GoldSpent));
            DrawStatRow(0, ref currentY, contentW, "순수익", GameStatsCollector.FormatGold(GameStatsCollector.GoldNet));

            GUI.EndScrollView();

            // 닫기 버튼
            float btnW = 100f;
            float btnH = 34f;
            float btnX = x + (_windowWidth - btnW) * 0.5f;
            float btnY = y + _windowHeight - 42f;
            if (GUI.Button(new Rect(btnX, btnY, btnW, btnH), "닫기 (ESC)", _styleCloseButton))
            {
                Close();
            }
        }

        // ======================================================================
        // IMGUI 드로잉 헬퍼
        // ======================================================================

        private void DrawBorderBox(float x, float y, float w, float h)
        {
            // 상단
            GUI.Box(new Rect(x, y, w, 2), "", new GUIStyle { normal = { background = _borderTexture } });
            // 하단
            GUI.Box(new Rect(x, y + h - 2, w, 2), "", new GUIStyle { normal = { background = _borderTexture } });
            // 좌측
            GUI.Box(new Rect(x, y, 2, h), "", new GUIStyle { normal = { background = _borderTexture } });
            // 우측
            GUI.Box(new Rect(x + w - 2, y, 2, h), "", new GUIStyle { normal = { background = _borderTexture } });
        }

        private void DrawDivider(float x, float y, float width)
        {
            Color oldColor = GUI.color;
            GUI.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            GUI.Box(new Rect(x, y, width, 1), "", new GUIStyle { normal = { background = _borderTexture } });
            GUI.color = oldColor;
        }

        private void DrawSectionHeader(float x, ref float y, float width, string title)
        {
            // 섹션 배경 (약간 어둡게)
            Color oldColor = GUI.color;
            GUI.color = new Color(0.2f, 0.2f, 0.25f, 0.5f);
            GUI.Box(new Rect(x, y, width, 26), "", new GUIStyle { normal = { background = _bgTexture } });
            GUI.color = oldColor;

            GUI.Label(new Rect(x, y, width, 26), title, _styleSection);
            y += 26 + SECTION_PADDING;
        }

        private void DrawStatRow(float x, ref float y, float width, string label, string value)
        {
            float rowH = ROW_HEIGHT;

            // 줄무늬 배경 (짝수 줄마다 약간 다르게)
            int rowIndex = Mathf.RoundToInt(y / ROW_HEIGHT);
            if (rowIndex % 2 == 0)
            {
                Color oldColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.04f);
                GUI.Box(new Rect(x, y, width, rowH), "", new GUIStyle { normal = { background = _bgTexture } });
                GUI.color = oldColor;
            }

            GUI.Label(new Rect(x, y, width * 0.55f, rowH), label, _styleStatLabel);
            GUI.Label(new Rect(x + width * 0.55f, y, width * 0.45f, rowH), value, _styleStatValue);
            y += rowH;
        }

        private float CalculateTotalContentHeight()
        {
            // 각 섹션의 높이를 계산
            float h = 0f;

            // 플레이타임 섹션 (1 stat)
            h += 26 + SECTION_PADDING; // 섹션 헤더
            h += ROW_HEIGHT; // 1 stat

            // 전투 섹션 (2 stats)
            h += 26 + SECTION_PADDING;
            h += ROW_HEIGHT * 2;

            // 퀘스트 & 탐험 섹션 (2 stats)
            h += 26 + SECTION_PADDING;
            h += ROW_HEIGHT * 2;

            // 영지 & 전쟁 섹션 (3 stats)
            h += 26 + SECTION_PADDING;
            h += ROW_HEIGHT * 3;

            // 아레나 섹션 (4 stats)
            h += 26 + SECTION_PADDING;
            h += ROW_HEIGHT * 4;

            // 수집 섹션 (1 stat)
            h += 26 + SECTION_PADDING;
            h += ROW_HEIGHT;

            // 경제 섹션 (3 stats)
            h += 26 + SECTION_PADDING;
            h += ROW_HEIGHT * 3;

            return h + 20f; // 여유 공간
        }

        // ======================================================================
        // 유틸리티
        // ======================================================================

        private static Texture2D MakeTexture(int width, int height, Color color)
        {
            var tex = new Texture2D(width, height);
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}