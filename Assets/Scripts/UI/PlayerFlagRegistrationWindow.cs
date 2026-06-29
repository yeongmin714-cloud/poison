using UnityEngine;
using ProjectName.Core;
using ProjectName.Systems;
using ProjectName.UI.Themes;
#pragma warning disable 0414

namespace ProjectName.UI
{
    /// <summary>
    /// Phase 34: 국기 등록 화면 — IMGUI 기반.
    /// 게임 시작 시 플레이어가 자신의 영지 국기를 설정합니다.
    /// </summary>
    public class PlayerFlagRegistrationWindow : UIWindow
    {
        private UIDesignTheme _flagTheme;

        [Header("Flag Registration Settings")]
        [SerializeField] private int _windowWidth = 1395;
        [SerializeField] private int _windowHeight = 1238;
        [SerializeField] private int _previewSize = 360;

        // ── 편집 중인 문장 데이터 (임시) ──
        private string _editName;
        private EmblemColor _editPrimaryColor;
        private EmblemShape _editShape;

        // ── 상태 ──
        private string _message = "";
        private bool _showMessage;
        private Vector2 _scrollPos;

        // ── 스타일 ──
        private GUIStyle _titleStyle;
        private GUIStyle _sectionLabelStyle;
        private GUIStyle _colorBtnStyle;
        private GUIStyle _selectedColorBtnStyle;
        private GUIStyle _shapeBtnStyle;
        private GUIStyle _selectedShapeBtnStyle;
        private GUIStyle _messageStyle;
        private GUIStyle _symbolPreviewStyle;    // 프리뷰 문양 스타일 (캐싱)
        private GUIStyle _previewNameStyle;      // 프리뷰 이름 스타일 (캐싱)
        private bool _stylesInitialized;

        // ── 모든 색상 및 문양 목록 ──
        private static readonly EmblemColor[] AllColors = (EmblemColor[])System.Enum.GetValues(typeof(EmblemColor));
        private static readonly EmblemShape[] AllShapes = (EmblemShape[])System.Enum.GetValues(typeof(EmblemShape));

        // ── 문양 유니코드 심볼 (표시용) ──
        private static readonly System.Collections.Generic.Dictionary<EmblemShape, string> ShapeSymbols = new System.Collections.Generic.Dictionary<EmblemShape, string>
        {
            { EmblemShape.Shield, "\uD83D\uDEE1\uFE0F" },
            { EmblemShape.Sword,  "\u2694\uFE0F" },
            { EmblemShape.Dragon, "\uD83D\uDC09" },
            { EmblemShape.Eagle,  "\uD83E\uDD85" },
            { EmblemShape.Skull,  "\uD83D\uDC80" },
            { EmblemShape.Rose,   "\uD83C\uDF39" },
            { EmblemShape.Flame,  "\uD83D\uDD25" },
            { EmblemShape.Star,   "\u2B50" },
            { EmblemShape.Crown,  "\uD83D\uDC51" },
            { EmblemShape.Moon,   "\uD83C\uDF19" }
        };

        // ── 색상 표시 문자열 ──
        private static readonly System.Collections.Generic.Dictionary<EmblemColor, string> ColorSymbols = new System.Collections.Generic.Dictionary<EmblemColor, string>
        {
            { EmblemColor.Red,    "\uD83D\uDFE5" },
            { EmblemColor.Blue,   "\uD83D\uDFE6" },
            { EmblemColor.Green,  "\uD83D\uDFE9" },
            { EmblemColor.Purple, "\uD83D\uDFEA" },
            { EmblemColor.Gold,   "\uD83D\uDFE8" },
            { EmblemColor.Silver, "\u2B1C" },
            { EmblemColor.White,  "\u26AA" },
            { EmblemColor.Black,  "\u26AB" }
        };

        protected override void OnShow()
        {
            base.OnShow();
            if (_flagTheme == null)
                _flagTheme = Phase33_Themes.FlagRegTheme();
            ApplyTheme(_flagTheme);
            _stylesInitialized = false;

            // 현재 문장 데이터를 편집 기본값으로 로드
            if (EmblemManager.Instance != null)
            {
                _editName = EmblemManager.Instance.CurrentEmblem.emblemName;
                _editPrimaryColor = EmblemManager.Instance.CurrentEmblem.primaryColor;
                _editShape = EmblemManager.Instance.CurrentEmblem.shape;
            }
            else
            {
                _editName = "내 문장";
                _editPrimaryColor = EmblemColor.Gold;
                _editShape = EmblemShape.Shield;
            }

            _message = "";
            _showMessage = false;
            _scrollPos = Vector2.zero;
        }

        protected override void OnHide()
        {
            base.OnHide();
            _message = "";
            _showMessage = false;
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 320,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.9f, 0.4f) }
            };

            _sectionLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 208,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }
            };

            _colorBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 28,
                alignment = TextAnchor.MiddleCenter,
                fixedWidth = 48,
                fixedHeight = 40,
                normal = { textColor = Color.white }
            };

            _selectedColorBtnStyle = new GUIStyle(_colorBtnStyle)
            {
                normal = { textColor = Color.yellow }
            };

            _shapeBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 28,
                alignment = TextAnchor.MiddleCenter,
                fixedWidth = 50,
                fixedHeight = 44,
                normal = { textColor = Color.white }
            };

            _selectedShapeBtnStyle = new GUIStyle(_shapeBtnStyle)
            {
                normal = { textColor = Color.yellow }
            };

            _messageStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 28,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.7f, 1f, 0.7f) }
            };

            _symbolPreviewStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 44,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            _previewNameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            _stylesInitialized = true;
        }

        protected override void OnGUI()
        {
            if (!IsOpen) return;
            InitializeStyles();

            // 배경 딤드
            Rect dimRect = new Rect(0, 0, Screen.width, Screen.height);
            GUI.color = new Color(0, 0, 0, 0.6f);
            GUI.DrawTexture(dimRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 메인 윈도우 (중앙)
            float x = (Screen.width - _windowWidth) / 2f;
            float y = (Screen.height - _windowHeight) / 2f;
            Rect windowRect = new Rect(x, y, _windowWidth, _windowHeight);

            GUILayout.BeginArea(windowRect, GUI.skin.box);

            // ── 제목 ──
            GUILayout.Label("\uD83C\uDFF3\uFE0F 내 영지의 국기", _titleStyle);

            // ── 국기 미리보기 ──
            GUILayout.Space(6);
            DrawFlagPreview();

            // ── 이름 입력 ──
            GUILayout.Space(8);
            GUILayout.Label("이름", _sectionLabelStyle);
            GUI.SetNextControlName("FlagNameField");
            _editName = GUILayout.TextField(_editName, 8, GUILayout.Height(42), GUILayout.Width(_windowWidth - 40));
            if (_editName.Length > 8)
                _editName = _editName.Substring(0, 8);

            // ── 배경색 선택 ──
            GUILayout.Space(8);
            GUILayout.Label("배경색", _sectionLabelStyle);
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(84));
            GUILayout.BeginHorizontal();
            foreach (var color in AllColors)
            {
                string symbol = ColorSymbols.TryGetValue(color, out var s) ? s : "?";
                bool isSelected = color == _editPrimaryColor;

                if (GUILayout.Button(symbol, isSelected ? _selectedColorBtnStyle : _colorBtnStyle))
                {
                    _editPrimaryColor = color;
                    _message = "";
                    _showMessage = false;
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();

            // ── 문양 선택 ──
            GUILayout.Space(6);
            GUILayout.Label("문양", _sectionLabelStyle);
            GUILayout.BeginHorizontal();
            foreach (var shape in AllShapes)
            {
                string symbol = ShapeSymbols.TryGetValue(shape, out var ss) ? ss : "?";
                bool isSelected = shape == _editShape;

                if (GUILayout.Button(symbol, isSelected ? _selectedShapeBtnStyle : _shapeBtnStyle))
                {
                    _editShape = shape;
                    _message = "";
                    _showMessage = false;
                }
            }
            GUILayout.EndHorizontal();

            // ── 비용 정보 ──
            GUILayout.Space(8);
            int cost = EmblemManager.Instance != null ? EmblemManager.Instance.ChangeCost : 100;
            int playerGold = GetPlayerGold();
            GUILayout.BeginHorizontal();
            GUILayout.Label($"\uD83D\uDCB0 변경 비용: {cost}G  (보유: {playerGold}G)", _sectionLabelStyle);
            GUILayout.EndHorizontal();

            // ── 메시지 ──
            if (_showMessage && !string.IsNullOrEmpty(_message))
            {
                GUILayout.Space(4);
                GUILayout.Box(_message, _messageStyle, GUILayout.Height(48));
            }

            // ── 완료 버튼 ──
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            bool canConfirm = EmblemManager.Instance != null && !string.IsNullOrEmpty(_editName);
            GUI.enabled = canConfirm;
            if (GUILayout.Button("완료", GUILayout.Width(210), GUILayout.Height(60)))
            {
                OnConfirm();
            }
            GUI.enabled = true;

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.EndArea();
        }

        /// <summary>
        /// 국기 미리보기 영역 그리기
        /// </summary>
        private void DrawFlagPreview()
        {
            Color bgColor = EmblemManager.GetEmblemColor(_editPrimaryColor);
            string symbol = ShapeSymbols.TryGetValue(_editShape, out var s) ? s : "?";
            string displayName = string.IsNullOrEmpty(_editName) ? "이름 없음" : _editName;

            // 미리보기 영역
            Rect previewContainer = GUILayoutUtility.GetRect(_windowWidth - 30, _previewSize);
            previewContainer.x += 5;
            previewContainer.width = _windowWidth - 40;

            // 배경 사각형
            GUI.color = bgColor;
            GUI.DrawTexture(previewContainer, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 테두리
            Color borderColor = (_editPrimaryColor == EmblemColor.White || _editPrimaryColor == EmblemColor.Silver)
                ? new Color(0.3f, 0.3f, 0.3f)
                : new Color(1f, 1f, 1f, 0.5f);
            GUI.color = borderColor;
            GUI.Box(previewContainer, "");
            GUI.color = Color.white;

            // 문양 심볼 (중앙)
            float symbolSize = Mathf.Min(_previewSize * 0.5f, 64f);
            float symbolX = previewContainer.x + (previewContainer.width - symbolSize) / 2f;
            float symbolY = previewContainer.y + (_previewSize * 0.35f - symbolSize * 0.5f);

            // 심볼 배경 (반투명)
            Rect symbolRect = new Rect(symbolX, symbolY, symbolSize, symbolSize);
            GUI.color = new Color(0, 0, 0, 0.3f);
            GUI.DrawTexture(symbolRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 심볼 텍스트 (캐싱된 스타일 사용)
            _symbolPreviewStyle.fontSize = (int)(symbolSize * 0.7f);
            GUI.Label(symbolRect, symbol, _symbolPreviewStyle);

            // 국기 이름 (하단)
            Rect nameRect = new Rect(previewContainer.x, previewContainer.y + _previewSize * 0.72f, previewContainer.width, _previewSize * 0.25f);
            GUI.Label(nameRect, displayName, _previewNameStyle);
        }

        /// <summary>
        /// 확인 버튼 클릭 시 처리
        /// </summary>
        private void OnConfirm()
        {
            if (EmblemManager.Instance == null)
            {
                _message = "문장 시스템을 사용할 수 없습니다.";
                _showMessage = true;
                return;
            }

            if (string.IsNullOrEmpty(_editName))
            {
                _message = "국기 이름을 입력해주세요.";
                _showMessage = true;
                return;
            }

            int playerGold = GetPlayerGold();

            var newEmblem = new PlayerEmblemData
            {
                emblemName = _editName.Trim(),
                shape = _editShape,
                primaryColor = _editPrimaryColor,
                secondaryColor = EmblemManager.Instance.CurrentEmblem.secondaryColor
            };

            bool success = EmblemManager.Instance.ChangeEmblem(newEmblem, playerGold);

            if (success)
            {
                _message = $"\u2705 국기 '{_editName}' 등록 완료!";
                _showMessage = true;
                Hide();
            }
            else
            {
                _message = $"\u26A0\uFE0F 골드가 부족합니다. (필요: {EmblemManager.Instance.ChangeCost}G)";
                _showMessage = true;
            }
        }

        /// <summary>
        /// 플레이어 골드 조회
        /// </summary>
        private int GetPlayerGold()
        {
            if (PlayerStats.Instance != null)
                return PlayerStats.Instance.Gold;

            if (PlayerInventory.Instance != null)
                return PlayerInventory.Instance.GetItemCount("gold");

            return 0;
        }

        // ── 테스트 지원 메서드 ──
        public string EditName => _editName;
        public EmblemColor EditPrimaryColor => _editPrimaryColor;
        public EmblemShape EditShape => _editShape;
        public string Message => _message;
        public bool HasMessage => _showMessage;

        /// <summary>테스트에서 색상을 직접 설정</summary>
        public void SetEditColor(EmblemColor color) => _editPrimaryColor = color;

        /// <summary>테스트에서 문양을 직접 설정</summary>
        public void SetEditShape(EmblemShape shape) => _editShape = shape;

        /// <summary>테스트에서 이름을 직접 설정</summary>
        public void SetEditName(string name) => _editName = name;

        /// <summary>테스트에서 완료 버튼 로직 호출</summary>
        public void TestConfirm() => OnConfirm();
    }
}