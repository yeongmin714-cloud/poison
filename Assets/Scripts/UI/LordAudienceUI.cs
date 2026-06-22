using UnityEngine;
using ProjectName.Core;
using ProjectName.Systems;
using ProjectName.UI.Themes;

namespace ProjectName.UI
{
    /// <summary>
    /// Phase 5.7.5: 영주 대면 UI.
    /// 영주와의 대화에서 화술(Speech) 능력치를 사용하여 설득/협상합니다.
    /// NPCQuestGiver/WanderingMerchant와의 연동도 지원.
    /// UIWindow를 상속받아 ESC 창 관리 체계에 포함됩니다.
    /// </summary>
    public class LordAudienceUI : UIWindow
    {
        private UIDesignTheme _lordTheme;

        [System.Serializable]
        public class AudienceOption
        {
            public string text;                          // 선택지 텍스트
            public int speechDifficulty;                 // 필요 화술 난이도 (0 = 자동 성공)
            public string successResult;                 // 성공 시 표시 텍스트
            public string failResult;                    // 실패 시 표시 텍스트
            public System.Action onSuccess;              // 성공 시 액션
            public System.Action onFail;                 // 실패 시 액션
        }

        [Header("영주 정보")]
        [SerializeField] private string _lordName = "영주";
        [SerializeField] private string _lordTitle = "동부 영지 영주";

        [Header("대면 선택지")]
        [SerializeField] private AudienceOption[] _options;

        [Header("현재 상태")]
        [SerializeField] private string _dialogueText = "";
        [SerializeField] private bool _showOptions = true;
        [SerializeField] private int _selectedOption = -1;

        // === IMGUI ===
        private const float WINDOW_WIDTH = 600f;
        private const float WINDOW_HEIGHT = 500f;
        private const float OPTION_HEIGHT = 40f;

        private static readonly Color ColorBg = new Color(0.15f, 0.10f, 0.12f, 0.92f);
        private static readonly Color ColorTitle = new Color(0.12f, 0.08f, 0.10f, 1f);
        private static readonly Color ColorText = new Color(0.92f, 0.88f, 0.80f, 1f);
        private static readonly Color ColorSuccess = new Color(0.3f, 0.9f, 0.4f, 1f);
        private static readonly Color ColorFail = new Color(0.9f, 0.3f, 0.3f, 1f);
        private static readonly Color ColorDim = new Color(0.5f, 0.45f, 0.40f, 1f);
        private GUIStyle _styleTitle, _styleText, _styleOption, _styleResult;
        private Texture2D _texWhite;
        private bool _stylesInit;

        protected override void OnShow()
        {
            base.OnShow();
            if (_lordTheme == null)
                _lordTheme = Phase33_Themes.LordAudienceTheme();
            ApplyTheme(_lordTheme);
            _showOptions = true;
            _selectedOption = -1;
            _dialogueText = $"{_lordName}: \"무슨 일로 왔느냐?\"";
            Debug.Log($"[LordAudienceUI] {_lordName} 대면 시작");
        }

        protected override void OnHide()
        {
            base.OnHide();
            Debug.Log("[LordAudienceUI] 대면 종료");
        }

        private void OnGUI()
        {
            if (!IsOpen) return;
            InitStyles();

            float x = (Screen.width - WINDOW_WIDTH) / 2;
            float y = (Screen.height - WINDOW_HEIGHT) / 2;

            // 배경
            GUI.Box(new Rect(x, y, WINDOW_WIDTH, WINDOW_HEIGHT), "", new GUIStyle(GUI.skin.box)
            { normal = { background = MakeTex(1, 1, ColorBg) } });

            // 제목 바
            DrawColorRect(new Rect(x, y, WINDOW_WIDTH, 36), ColorTitle);
            GUI.Label(new Rect(x + 10, y + 4, WINDOW_WIDTH - 60, 28), $"👑 {_lordTitle} — {_lordName}", _styleTitle);

            // 닫기 버튼
            if (GUI.Button(new Rect(x + WINDOW_WIDTH - 44, y + 4, 36, 28), "✕",
                new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold }))
            {
                Hide();
                return;
            }

            // 대화 텍스트
            float textY = y + 44;
            GUI.Label(new Rect(x + 14, textY, WINDOW_WIDTH - 28, 60), _dialogueText, _styleText);

            // 선택지 또는 결과
            float optY = textY + 70;

            if (_showOptions && _options != null)
            {
                for (int i = 0; i < _options.Length && i < 4; i++)
                {
                    var opt = _options[i];
                    Rect optRect = new Rect(x + 20, optY, WINDOW_WIDTH - 40, OPTION_HEIGHT);

                    bool isHover = optRect.Contains(Event.current.mousePosition);
                    Color optColor = isHover ? new Color(0.35f, 0.25f, 0.18f, 1f) : new Color(0.22f, 0.18f, 0.15f, 1f);
                    DrawColorRect(optRect, optColor);

                    string label = $"{i + 1}. {opt.text}";
                    if (opt.speechDifficulty > 0)
                        label += $" [화술 {opt.speechDifficulty}]";

                    GUI.Label(new Rect(optRect.x + 8, optRect.y + 8, optRect.width - 16, 24), label, _styleOption);

                    if (Event.current.type == EventType.MouseDown && isHover)
                    {
                        ExecuteOption(i);
                        Event.current.Use();
                    }

                    optY += OPTION_HEIGHT + 4;
                }

                // 하단 안내
                GUI.Label(new Rect(x + 14, y + WINDOW_HEIGHT - 28, WINDOW_WIDTH - 28, 20),
                    "💡 선택지를 클릭하여 대화를 진행하세요.", _styleOption);
            }
        }

        private void ExecuteOption(int index)
        {
            if (_options == null || index < 0 || index >= _options.Length) return;

            var opt = _options[index];
            _showOptions = false;
            _selectedOption = index;

            int speechLevel = PlayerStats.Instance != null ? PlayerStats.Instance.Level : 1;
            // SpeechAffinityBonus = _level, 화술 성공 판정: speechLevel >= difficulty
            bool success = opt.speechDifficulty <= 0 || speechLevel >= opt.speechDifficulty;

            if (success)
            {
                _dialogueText = $"{_lordName}: \"{opt.successResult}\"";
                GUI.color = ColorSuccess;
                opt.onSuccess?.Invoke();
                Debug.Log($"[LordAudienceUI] ✅ 화술 성공! (레벨 {speechLevel} ≥ 필요 {opt.speechDifficulty})");
            }
            else
            {
                _dialogueText = $"{_lordName}: \"{opt.failResult}\"";
                GUI.color = ColorFail;
                opt.onFail?.Invoke();
                Debug.Log($"[LordAudienceUI] ❌ 화술 실패! (레벨 {speechLevel} < 필요 {opt.speechDifficulty})");
            }
            GUI.color = Color.white;

            // 3초 후 선택지 재표시
            Invoke(nameof(ResetDialogue), 3f);
        }

        private void ResetDialogue()
        {
            _showOptions = true;
            _selectedOption = -1;
            _dialogueText = $"{_lordName}: \"또 무슨 일이냐?\"";
        }

        private void InitStyles()
        {
            if (_stylesInit) return;
            _texWhite = MakeTex(1, 1, Color.white);

            _styleTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = ColorText }
            };

            _styleText = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, fontStyle = FontStyle.Normal,
                wordWrap = true,
                normal = { textColor = ColorText }
            };

            _styleOption = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12, fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = ColorText }
            };

            _styleResult = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ColorText }
            };

            _stylesInit = true;
        }

        private static Texture2D MakeTex(int w, int h, Color c)
        {
            var t = new Texture2D(w, h);
            for (int i = 0; i < w * h; i++) t.SetPixel(i % w, i / w, c);
            t.Apply();
            return t;
        }

        private static void DrawColorRect(Rect rect, Color color)
        {
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}