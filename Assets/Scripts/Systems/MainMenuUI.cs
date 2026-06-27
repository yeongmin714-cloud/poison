using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C17-01: 메인 메뉴 UI.
    /// IMGUI 기반으로 "새 게임", "불러오기", "설정" 버튼을 제공합니다.
    /// C20-03: 난이도 선택 화면 추가.
    /// 게임 시작 시 자동으로 표시되며, "새 게임"은 LoadingManager를 통해 MainScene을 로드합니다.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] private int _windowWidth = 400;
        [SerializeField] private int _windowHeight = 450;
        [SerializeField] private int _buttonWidth = 280;
        [SerializeField] private int _buttonHeight = 60;
        [SerializeField] private int _buttonSpacing = 20;

        [Header("G3-02: Background Settings")]
        [SerializeField] private Color _bgGradientTop = new Color(0.02f, 0.02f, 0.08f);
        [SerializeField] private Color _bgGradientBottom = new Color(0.05f, 0.01f, 0.15f);
        [SerializeField] private Color _titlePulseColor = new Color(1f, 0.85f, 0.4f);

        [Header("Colors")]
        [SerializeField] private Color _bgColor = new Color(0f, 0f, 0f, 0.9f);
        [SerializeField] private Color _titleColor = new Color(0.9f, 0.7f, 0.3f, 1f);

        [Header("G3-02: Menu Enhancement")]
        [SerializeField] private Color _creditsBgColor = new Color(0f, 0f, 0f, 0.92f);
        [SerializeField] private Color _creditsTextColor = new Color(0.85f, 0.85f, 0.8f, 1f);
        [SerializeField] private float _titlePulseSpeed = 1.5f;
        [SerializeField] private float _titlePulseAmount = 0.1f;

        [Header("Button Colors")]
        [SerializeField] private Color _buttonColor = new Color(0.2f, 0.35f, 0.5f, 0.9f);
        [SerializeField] private Color _buttonHoverColor = new Color(0.3f, 0.5f, 0.7f, 1f);
        [SerializeField] private Color _textColor = Color.white;
        [SerializeField] private Color _easyColor = new Color(0.3f, 0.9f, 0.3f);
        [SerializeField] private Color _normalColor = new Color(0.9f, 0.9f, 0.2f);
        [SerializeField] private Color _hardColor = new Color(0.9f, 0.2f, 0.2f);

        // ===== 상태 =====
        private bool _isVisible = true;
        private bool _showLoadUI;
        private bool _showDifficultySelect; // C20-03
        private DifficultyMode _selectedDifficulty = DifficultyMode.Normal; // C20-03
        private string _settingsMessage = "";
        private bool _showCredits;
        private float _titlePulseTimer;
        private float _settingsMessageTimer;
        private GameObject _loadGameUIObject;

        private GUIStyle _titleStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _settingsMessageStyle;
        private GUIStyle _difficultyTitleStyle;
        private GUIStyle _difficultyDescStyle;
        private GUIStyle _multiplierStyle;
        private GUIStyle _creditsTitleStyle;
        private GUIStyle _creditsTextStyle;
        private bool _stylesInitialized;

        // ===== 캐싱 필드 (OnGUI 매프레임 할당 방지) =====
        private GUIStyle _subtitleStyle;
        private GUIStyle _smallButtonStyle;
        private GUIStyle _difficultyHeaderStyle;
        private GUIStyle _difficultyWarnStyle;
        private GUIStyle _dimBgStyle;
        private GUIStyle _bgPanelStyle;
        private GUIStyle _creditsBgPanelStyle;
        private GUIStyle _cachedGradientStyle;
        private GUIStyle _cachedStarStyle;
        private Texture2D[] _cachedStarTextures;
        private Texture2D _cachedDimTexture;
        private Texture2D _cachedBgPanelTexture;
        private Texture2D _cachedCreditsBgTexture;
        private Texture2D _cachedGradientTexture;
        private int _lastScreenWidth;
        private int _lastScreenHeight;
        private Vector2[] _cachedStarPositions;
        private bool _cachedStarPositionsInitialized;

        /// <summary>
        /// LoadGameUI 컴포넌트에 접근하기 위한 프로퍼티.
        /// </summary>
        public LoadGameUI LoadGameUIComponent { get; private set; }

        private void Awake()
        {
            // Create a child GameObject for LoadGameUI
            _loadGameUIObject = new GameObject("LoadGameUI");
            _loadGameUIObject.transform.SetParent(transform);
            LoadGameUIComponent = _loadGameUIObject.AddComponent<LoadGameUI>();
            LoadGameUIComponent.SetMainMenuUI(this);
            _loadGameUIObject.SetActive(false);
        }

        private void Start()
        {
            Debug.Log("[MainMenuUI] 메인 메뉴 표시");
            _showLoadUI = false;
            _showDifficultySelect = false;
        }

        /// <summary>
        /// 메인 메뉴를 표시합니다.
        /// </summary>
        public void Show()
        {
            _isVisible = true;
            _showLoadUI = false;
            _showDifficultySelect = false;
            if (_loadGameUIObject != null)
                _loadGameUIObject.SetActive(false);
        }

        /// <summary>
        /// 메인 메뉴를 숨깁니다.
        /// </summary>
        public void Hide()
        {
            _isVisible = false;
            if (_loadGameUIObject != null)
                _loadGameUIObject.SetActive(false);
        }

        /// <summary>
        /// 불러오기 UI의 표시 상태를 설정합니다.
        /// </summary>
        public void SetLoadUIActive(bool active)
        {
            _showLoadUI = active;
            if (_loadGameUIObject != null)
                _loadGameUIObject.SetActive(active);
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _titleStyle = new GUIStyle
            {
                fontSize = 36,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _titleColor }
            };

            _buttonStyle = new GUIStyle
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _textColor }
            };
            _buttonStyle.hover.background = MakeTexture(1, 1, _buttonHoverColor);
            _buttonStyle.active.background = MakeTexture(1, 1, new Color(0.1f, 0.2f, 0.5f, 1f));

            _settingsMessageStyle = new GUIStyle
            {
                fontSize = 18,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f, 1f) }
            };

            // C20-03: 난이도 UI 스타일
            _difficultyTitleStyle = new GUIStyle
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _titleColor }
            };

            _difficultyDescStyle = new GUIStyle
            {
                fontSize = 14,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f, 1f) }
            };

            _multiplierStyle = new GUIStyle
            {
                fontSize = 16,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };

            // ===== 캐싱 텍스처 =====
            _cachedDimTexture = MakeTexture(1, 1, new Color(0f, 0f, 0f, 0.6f));
            _cachedBgPanelTexture = MakeTexture(1, 1, _bgColor);
            _cachedCreditsBgTexture = MakeTexture(1, 1, _creditsBgColor);

            // 별 텍스처 미리 생성 (30개, 매프레임 생성 방지)
            int starCount = 30;
            _cachedStarTextures = new Texture2D[starCount];
            for (int i = 0; i < starCount; i++)
                _cachedStarTextures[i] = MakeTexture(2, 2, Color.white);

            // ===== 캐싱 스타일 =====
            _dimBgStyle = new GUIStyle { normal = { background = _cachedDimTexture } };
            _bgPanelStyle = new GUIStyle { normal = { background = _cachedBgPanelTexture } };
            _creditsBgPanelStyle = new GUIStyle { normal = { background = _cachedCreditsBgTexture } };
            _cachedStarStyle = new GUIStyle { normal = { background = _cachedStarTextures[0] } };

            _subtitleStyle = new GUIStyle
            {
                fontSize = 14,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f, 1f) }
            };

            _smallButtonStyle = new GUIStyle
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            _difficultyHeaderStyle = new GUIStyle
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            _difficultyWarnStyle = new GUIStyle
            {
                fontSize = 13,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.7f, 0.2f, 1f) }
            };

            // Credits 스타일
            _creditsTitleStyle = new GUIStyle
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _titleColor }
            };

            _creditsTextStyle = new GUIStyle
            {
                fontSize = 15,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _creditsTextColor },
                richText = true
            };

            _stylesInitialized = true;
        }

        private Texture2D MakeTexture(int width, int height, Color color)
        {
            var tex = new Texture2D(width, height);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    tex.SetPixel(x, y, color);
            tex.Apply();
            return tex;
        }

        private void Update()
        {
            // G3-02: 타이틀 펄스 타이머
            _titlePulseTimer += Time.deltaTime * _titlePulseSpeed;

            // 설정 메시지 타이머
            if (_settingsMessageTimer > 0f)
            {
                _settingsMessageTimer -= Time.deltaTime;
                if (_settingsMessageTimer <= 0f)
                {
                    _settingsMessage = "";
                }
            }
        }

        // C20-03: 저장된 게임 난이도 힌트 (UI 표시용)
        private string GetSavedDifficultyHint()
        {
            if (SaveManager.Instance != null)
            {
                var infos = SaveManager.Instance.GetAllSlotInfos();
                foreach (var info in infos)
                {
                    if (info != null)
                        return $"저장된 난이도: {GetDifficultyDisplayName(info.difficulty)}";
                }
            }
            return null;
        }

        private void OnGUI()
        {
            InitializeStyles();

            if (!_isVisible) return;

            // 배경 딤 — 캐싱된 텍스처/스타일 사용
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "", _dimBgStyle);

            if (_showDifficultySelect)
            {
                DrawDifficultySelectUI();
            }
            else
            {
                DrawMainMenu();
            }
        }

        // ===== 메인 메뉴 =====

        private void DrawMainMenu()
        {
            // G3-02: 배경 그라디언트
            DrawGradientBackground();

            // G3-02: Credits 화면
            if (_showCredits)
            {
                DrawCreditsScreen();
                return;
            }

            int centerX = (Screen.width - _windowWidth) / 2;
            int centerY = (Screen.height - _windowHeight) / 2;

            // 메인 박스 배경 — 캐싱 사용
            GUI.Box(new Rect(centerX, centerY, _windowWidth, _windowHeight), "", _bgPanelStyle);

            // G3-02: 펄스 효과 적용된 타이틀
            float pulse = 1f + Mathf.Sin(_titlePulseTimer) * _titlePulseAmount;
            var pulseTitleStyle = new GUIStyle(_titleStyle)
            {
                fontSize = (int)(36 * pulse)
            };
            pulseTitleStyle.normal.textColor = Color.Lerp(_titleColor, _titlePulseColor,
                (Mathf.Sin(_titlePulseTimer) + 1f) / 2f);
            GUI.Label(new Rect(centerX - 20, centerY + 30, _windowWidth + 40, 55), "Korea 1420", pulseTitleStyle);

            // 부제목 — 캐싱된 스타일 사용
            GUI.Label(new Rect(centerX, centerY + 80, _windowWidth, 30), "— 조선 —", _subtitleStyle);

            // 버튼 영역
            int buttonStartY = centerY + 140;
            int buttonX = centerX + (_windowWidth - _buttonWidth) / 2;

            GUI.backgroundColor = _buttonColor;

            // 새 게임 버튼
            if (GUI.Button(new Rect(buttonX, buttonStartY, _buttonWidth, _buttonHeight), "새 게임", _buttonStyle))
            {
                OnNewGameClicked();
            }

            // 불러오기 버튼
            if (GUI.Button(new Rect(buttonX, buttonStartY + (_buttonHeight + _buttonSpacing), _buttonWidth, _buttonHeight), "불러오기", _buttonStyle))
            {
                OnLoadGameClicked();
            }

            // 설정 버튼
            if (GUI.Button(new Rect(buttonX, buttonStartY + (_buttonHeight + _buttonSpacing) * 2, _buttonWidth, _buttonHeight), "설정", _buttonStyle))
            {
                OnSettingsClicked();
            }

            // 설정 준비 중 메시지
            if (!string.IsNullOrEmpty(_settingsMessage))
            {
                GUI.Label(new Rect(centerX, buttonStartY + (_buttonHeight + _buttonSpacing) * 3 + 10, _windowWidth, 40), _settingsMessage, _settingsMessageStyle);
            }

            // G3-02: Credits 버튼
            int creditsY = buttonStartY + (_buttonHeight + _buttonSpacing) * 3 + 20;
            if (_settingsMessage != "")
                creditsY += 40;
            GUI.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.7f);
            if (GUI.Button(new Rect(buttonX + _buttonWidth - 100, creditsY, 100, 30), "Credits", _smallButtonStyle))
            {
                _showCredits = true;
                Debug.Log("[MainMenuUI] Credits 화면 표시");
            }
            GUI.backgroundColor = _buttonColor;
        }

        private void OnNewGameClicked()
        {
            Debug.Log("[MainMenuUI] 새 게임 → 난이도 선택 화면으로 전환");
            _showDifficultySelect = true;
            _selectedDifficulty = DifficultyMode.Normal; // 기본값
        }

        private void OnLoadGameClicked()
        {
            Debug.Log("[MainMenuUI] 불러오기 화면으로 전환");
            _showLoadUI = true;
            if (_loadGameUIObject != null)
            {
                _loadGameUIObject.SetActive(true);
                if (LoadGameUIComponent != null)
                    LoadGameUIComponent.RefreshSlots();
            }
        }

        private void OnSettingsClicked()
        {
            _settingsMessage = "준비 중...";
            _settingsMessageTimer = 2.0f;
            Debug.Log("[MainMenuUI] 설정: 준비 중 (placeholder)");
        }

        // ===== C20-03: 난이도 선택 UI =====

        private void DrawDifficultySelectUI()
        {
            int winW = 500;
            int winH = 460;
            int centerX = (Screen.width - winW) / 2;
            int centerY = (Screen.height - winH) / 2;

            // 메인 박스 배경 — 캐싱 사용
            GUI.Box(new Rect(centerX, centerY, winW, winH), "", _bgPanelStyle);

            // 제목
            GUI.Label(new Rect(centerX, centerY + 15, winW, 40), "🎯 난이도 선택", _difficultyTitleStyle);

            // 설명
            string hint = GetSavedDifficultyHint();
            if (!string.IsNullOrEmpty(hint))
            {
                GUI.Label(new Rect(centerX, centerY + 55, winW, 25), hint, _difficultyDescStyle);
            }

            // 난이도 버튼 3개
            int btnY = centerY + 85;
            int btnW = 120;
            int btnH = 50;
            int gap = 20;
            int totalW = btnW * 3 + gap * 2;
            int btnStartX = centerX + (winW - totalW) / 2;

            // 🟢 쉬움
            DrawDifficultyButton(btnStartX, btnY, btnW, btnH, "🟢 쉬움", DifficultyMode.Easy, _easyColor);

            // 🟡 보통
            DrawDifficultyButton(btnStartX + btnW + gap, btnY, btnW, btnH, "🟡 보통", DifficultyMode.Normal, _normalColor);

            // 🔴 어려움
            DrawDifficultyButton(btnStartX + (btnW + gap) * 2, btnY, btnW, btnH, "🔴 어려움", DifficultyMode.Hard, _hardColor);

            // 난이도별 배율 표
            int tableY = btnY + btnH + 20;
            int tableX = centerX + 40;
            int colW = 100;

            // 헤더 — 캐싱된 스타일 사용
            GUI.Label(new Rect(tableX + 10, tableY, colW, 25), "구분", _difficultyHeaderStyle);
            GUI.Label(new Rect(tableX + 10 + colW, tableY, colW, 25), "쉬움", _difficultyHeaderStyle);
            GUI.Label(new Rect(tableX + 10 + colW * 2, tableY, colW, 25), "보통", _difficultyHeaderStyle);
            GUI.Label(new Rect(tableX + 10 + colW * 3, tableY, colW, 25), "어려움", _difficultyHeaderStyle);

            // 적 체력
            DrawMultiplierRow(tableX, tableY + 30, "적 체력",
                DifficultyManager.GetHpMultiplier(DifficultyMode.Easy),
                DifficultyManager.GetHpMultiplier(DifficultyMode.Normal),
                DifficultyManager.GetHpMultiplier(DifficultyMode.Hard));

            // 적 데미지
            DrawMultiplierRow(tableX, tableY + 60, "적 데미지",
                DifficultyManager.GetDamageMultiplier(DifficultyMode.Easy),
                DifficultyManager.GetDamageMultiplier(DifficultyMode.Normal),
                DifficultyManager.GetDamageMultiplier(DifficultyMode.Hard));

            // 드랍률
            DrawMultiplierRow(tableX, tableY + 90, "드랍률",
                DifficultyManager.GetDropRateMultiplier(DifficultyMode.Easy),
                DifficultyManager.GetDropRateMultiplier(DifficultyMode.Normal),
                DifficultyManager.GetDropRateMultiplier(DifficultyMode.Hard));

            // 리스폰 속도
            DrawMultiplierRow(tableX, tableY + 120, "리스폰",
                DifficultyManager.GetRespawnRateMultiplier(DifficultyMode.Easy),
                DifficultyManager.GetRespawnRateMultiplier(DifficultyMode.Normal),
                DifficultyManager.GetRespawnRateMultiplier(DifficultyMode.Hard));

            // 경고 메시지 — 캐싱된 스타일 사용
            GUI.Label(new Rect(centerX, tableY + 155, winW, 25), "⚠ 게임 중에는 변경할 수 없습니다", _difficultyWarnStyle);

            // 하단 버튼
            int bottomBtnY = centerY + winH - 60;
            int confirmX = centerX + (winW / 2) - btnW - 10;
            int backX = centerX + (winW / 2) + 10;

            GUI.backgroundColor = new Color(0.2f, 0.6f, 0.3f, 0.9f);
            if (GUI.Button(new Rect(confirmX, bottomBtnY, btnW, 40), "✅ 확인", _buttonStyle))
            {
                OnDifficultyConfirmed();
            }

            GUI.backgroundColor = new Color(0.5f, 0.2f, 0.2f, 0.9f);
            if (GUI.Button(new Rect(backX, bottomBtnY, btnW, 40), "↩ 돌아가기", _buttonStyle))
            {
                OnDifficultyBack();
            }

            GUI.backgroundColor = Color.white;
        }

        private void DrawDifficultyButton(int x, int y, int w, int h, string label, DifficultyMode mode, Color baseColor)
        {
            bool isSelected = _selectedDifficulty == mode;
            Color bgColor = isSelected ? baseColor : baseColor * 0.5f;
            GUI.backgroundColor = bgColor;

            // isSelected 상태에 따라 textColor 설정 (new GUIStyle 방지)
            Color originalTextColor = _buttonStyle.normal.textColor;
            Color originalHoverColor = _buttonStyle.hover.textColor;
            if (isSelected)
            {
                _buttonStyle.normal.textColor = Color.white;
                _buttonStyle.hover.textColor = Color.white;
            }

            GUIStyle btnStyle = _buttonStyle;
            int originalFontSize = _buttonStyle.fontSize;
            _buttonStyle.fontSize = 18;

            if (GUI.Button(new Rect(x, y, w, h), label, btnStyle))
            {
                _selectedDifficulty = mode;
                Debug.Log($"[MainMenuUI] 난이도 선택: {mode}");
            }

            // 원래 값 복원
            _buttonStyle.fontSize = originalFontSize;
            _buttonStyle.normal.textColor = originalTextColor;
            _buttonStyle.hover.textColor = originalHoverColor;
            GUI.backgroundColor = Color.white;
        }

        private void DrawMultiplierRow(int x, int y, string label, float easy, float normal, float hard)
        {
            int colW = 100;
            var style = _multiplierStyle;

            GUI.Label(new Rect(x, y, colW, 25), label, style);
            GUI.Label(new Rect(x + colW, y, colW, 25), $"×{easy:F1}", style);
            GUI.Label(new Rect(x + colW * 2, y, colW, 25), $"×{normal:F1}", style);
            GUI.Label(new Rect(x + colW * 3, y, colW, 25), $"×{hard:F1}", style);
        }

        private void OnDifficultyConfirmed()
        {
            Debug.Log($"[MainMenuUI] 난이도 확정: {_selectedDifficulty}");

            if (GameManager.Instance != null)
            {
                GameManager.CurrentDifficulty = (int)_selectedDifficulty;
            }
            else
            {
                Debug.LogError("[MainMenuUI] GameManager.Instance가 null입니다. 난이도를 설정할 수 없습니다.");
            }

            if (LoadingManager.Instance != null)
            {
                Hide();
                LoadingManager.Instance.LoadSceneAsync("MainScene");
            }
            else
            {
                Debug.LogError("[MainMenuUI] LoadingManager.Instance가 null입니다.");
            }
        }

        private void OnDifficultyBack()
        {
            Debug.Log("[MainMenuUI] 난이도 선택 취소, 메인 메뉴로 복귀");
            _showDifficultySelect = false;
        }

        // C20-03: 난이도 표시명
        private string GetDifficultyDisplayName(DifficultyMode mode)
        {
            return mode switch
            {
                DifficultyMode.Easy => "🟢 쉬움",
                DifficultyMode.Hard => "🔴 어려움",
                _ => "🟡 보통"
            };
        }

        // ================================================================
        // G3-02: 배경 그라디언트 & Credits
        // ================================================================

        /// <summary>
        /// 메인 메뉴 배경에 그라디언트 효과를 그립니다.
        /// 위에서 아래로 어두운 청색→진한 보라색.
        /// 작은 별들이 반짝이는 효과도 추가합니다.
        /// </summary>
        private void DrawGradientBackground()
        {
            int width = Screen.width;
            int height = Screen.height;

            // 화면 크기가 바뀌면 그라디언트 텍스처/스타일 재생성
            if (_cachedGradientTexture == null || _lastScreenWidth != width || _lastScreenHeight != height)
            {
                if (_cachedGradientTexture != null)
                    Object.Destroy(_cachedGradientTexture);

                _cachedGradientTexture = MakeGradientTexture(width, height, _bgGradientTop, _bgGradientBottom);
                _cachedGradientStyle = new GUIStyle { normal = { background = _cachedGradientTexture } };
                _lastScreenWidth = width;
                _lastScreenHeight = height;
            }

            // 그라디언트 배경 — 캐싱된 스타일 사용
            GUI.Box(new Rect(0, 0, width, height), "", _cachedGradientStyle);

            // 별 위치 캐싱 (화면 크기 기준 최초 1회)
            if (!_cachedStarPositionsInitialized || _lastScreenWidth != width || _lastScreenHeight != height)
            {
                int starCount = _cachedStarTextures.Length;
                _cachedStarPositions = new Vector2[starCount];
                System.Random rng = new System.Random(42);
                for (int i = 0; i < starCount; i++)
                {
                    float starX = (float)(rng.NextDouble() * width);
                    float starY = (float)(rng.NextDouble() * height * 0.7f);
                    _cachedStarPositions[i] = new Vector2(starX, starY);
                }
                _cachedStarPositionsInitialized = true;
            }

            // 별 반짝임 — 캐싱된 텍스처/스타일 사용
            for (int i = 0; i < _cachedStarPositions.Length; i++)
            {
                float starX = _cachedStarPositions[i].x;
                float starY = _cachedStarPositions[i].y;
                float twinkle = 0.3f + 0.7f * Mathf.Sin(_titlePulseTimer * 2f + i * 1.7f);
                twinkle = Mathf.Max(0.1f, twinkle);
                Color starColor = new Color(1f, 1f, 1f, twinkle * 0.5f);

                // 미리 생성된 별 텍스처의 색상만 업데이트
                var tex = _cachedStarTextures[i];
                tex.SetPixel(0, 0, starColor);
                tex.SetPixel(1, 0, starColor);
                tex.SetPixel(0, 1, starColor);
                tex.SetPixel(1, 1, starColor);
                tex.Apply();

                _cachedStarStyle.normal.background = tex;
                GUI.Box(new Rect(starX, starY, 2, 2), "", _cachedStarStyle);
            }
        }

        /// <summary>
        /// 단일 Texture2D로 그라디언트를 생성합니다.
        /// </summary>
        private Texture2D MakeGradientTexture(int width, int height, Color top, Color bottom)
        {
            var tex = new Texture2D(width, height);
            for (int y = 0; y < height; y++)
            {
                float t = (float)y / height;
                Color color = Color.Lerp(top, bottom, t);
                for (int x = 0; x < width; x++)
                {
                    tex.SetPixel(x, y, color);
                }
            }
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Credits 화면을 그립니다.
        /// 게임 제작진 정보를 표시하고, 클릭 시 메인 메뉴로 돌아갑니다.
        /// </summary>
        private void DrawCreditsScreen()
        {
            int winW = 500;
            int winH = 400;
            int centerX = (Screen.width - winW) / 2;
            int centerY = (Screen.height - winH) / 2;

            // 배경 — 캐싱 사용
            GUI.Box(new Rect(centerX, centerY, winW, winH), "", _creditsBgPanelStyle);

            // Credits 제목 — 캐싱된 스타일 사용
            GUI.Label(new Rect(centerX, centerY + 20, winW, 40), "Korea 1420", _creditsTitleStyle);

            // Credits 내용 — 캐싱된 스타일 사용

            string[] lines = new string[]
            {
                "",
                "<b>제작</b>",
                "Hermes Agent — Nous Research",
                "",
                "<b>게임 디자인 & 기획</b>",
                "영민 조 (사장님)",
                "",
                "<b>프로그래밍</b>",
                "Hermes Agent (AI)",
                "",
                "<b>엔진</b>",
                "Unity 6000.4.10f1 (URP)",
                "",
                "<b>Special Thanks</b>",
                "Claude, GPT, DeepSeek",
            };

            int textY = centerY + 70;
            foreach (string line in lines)
            {
                GUI.Label(new Rect(centerX + 20, textY, winW - 40, 22), line, _creditsTextStyle);
                textY += 24;
            }

            // 돌아가기 버튼
            int btnW = 140;
            int btnH = 40;
            int btnX = centerX + (winW - btnW) / 2;
            int btnY = centerY + winH - 60;
            GUI.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.9f);
            if (GUI.Button(new Rect(btnX, btnY, btnW, btnH), "← 돌아가기", _buttonStyle))
            {
                _showCredits = false;
                Debug.Log("[MainMenuUI] Credits → 메인 메뉴 복귀");
            }
            GUI.backgroundColor = Color.white;
        }
    }
}