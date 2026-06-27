using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ProjectName.Core;
using ProjectName.UI.Themes;
#pragma warning disable 0414

namespace ProjectName.UI
{
    /// <summary>
    /// 옵션 UI — 그래픽, 오디오, 키 설정 탭을 제공합니다.
    /// UIWindow 기반으로 Show/Hide/애니메이션 지원.
    /// UIManager를 통해 열기/닫기.
    /// </summary>
    public class OptionsUI : UIWindow
    {
        #region Nested Types

        private enum OptionsTab { Graphics, Audio, KeyBindings }

        #endregion

        #region Serialized Fields

        [Header("Options UI Layout")]
        [SerializeField] private int _tabButtonHeight = 90;
        [SerializeField] private int _contentPadding = 15;
        [SerializeField] private int _sliderHeight = 45;
        [SerializeField] private int _buttonHeight = 45;
        [SerializeField] private int _spacing = 12;
        [SerializeField] private int _labelFontSize = 28;
        [SerializeField] private int _valueFontSize = 28;

        [Header("Options UI Colors")]
        [SerializeField] private Color _tabActiveColor = new Color(0.3f, 0.5f, 0.7f, 1f);
        [SerializeField] private Color _tabInactiveColor = new Color(0.15f, 0.25f, 0.35f, 0.9f);
        [SerializeField] private Color _sliderBgColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        [SerializeField] private Color _sliderThumbColor = new Color(0.5f, 0.7f, 0.9f, 1f);
        [SerializeField] private Color _backColor = new Color(0.6f, 0.2f, 0.2f, 0.9f);
        [SerializeField] private Color _labelColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        [SerializeField] private Color _buttonColor = new Color(0.2f, 0.35f, 0.5f, 0.9f);
        [SerializeField] private Color _successColor = new Color(0.3f, 0.6f, 0.3f, 1f);
        [SerializeField] private Color _dangerColor = new Color(0.5f, 0.3f, 0.3f, 1f);

        #endregion

        #region Private Fields

        private OptionsTab _currentTab = OptionsTab.Graphics;
        private UIDesignTheme _optionsTheme;

        // Graphics state
        private int _selectedQualityLevel;
        private int _selectedResolutionIndex;
        private Resolution[] _availableResolutions;
        private string[] _resolutionLabels;

        // Audio state
        private float _bgmVolume = 0.8f;
        private float _sfxVolume = 0.8f;
        private float _uiVolume = 0.7f;
        private float _ambientVolume = 0.6f;

        // uGUI references (built in CreateUI)
        private GameObject _tabContainer;
        private GameObject _contentContainer;
        private Text _statusText;
        private float _statusMessageTimer;

        // Cached strings (OnGUI GC 방지)
        private string[] _keyBindingLabels;
        private string[] _qualityButtonLabels;
        private string[] _tabNames;
        private OptionsTab[] _tabValues;
        private bool _stringsCached;

        // Styles (IMGUI fallback)
        private GUIStyle _titleStyle;
        private GUIStyle _tabStyle;
        private GUIStyle _tabActiveStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _valueStyle;
        private GUIStyle _backButtonStyle;
        private GUIStyle _fullscreenButtonStyle;
        private GUIStyle _dimStyle;
        private GUIStyle _bgBoxStyle;
        private GUIStyle _sliderBgStyle;
        private GUIStyle _sliderThumbStyle;
        private GUIStyle _guideLabelStyle;
        private Texture2D _dimTexture;
        private Texture2D _bgTexture;
        private Texture2D _sliderBgTex;
        private Texture2D _sliderThumbTex;
        private bool _stylesInitialized;

        #endregion

        #region Unity Lifecycle

        protected override void Awake()
        {
            base.Awake();

            if (_optionsTheme == null)
                _optionsTheme = Phase33_Themes.SettingsTheme();

            CacheStrings();

            // Load saved settings
            _selectedQualityLevel = PlayerPrefs.GetInt("Settings_Quality", QualitySettings.GetQualityLevel());
            _selectedResolutionIndex = PlayerPrefs.GetInt("Settings_Resolution", 0);
            _bgmVolume = PlayerPrefs.GetFloat("Settings_BGM", 0.8f);
            _sfxVolume = PlayerPrefs.GetFloat("Settings_SFX", 0.8f);
            _uiVolume = PlayerPrefs.GetFloat("Settings_UI", 0.7f);
            _ambientVolume = PlayerPrefs.GetFloat("Settings_Ambient", 0.6f);
        }

        private void Start()
        {
            _availableResolutions = Screen.resolutions;
            if (_availableResolutions != null && _availableResolutions.Length > 0)
            {
                _resolutionLabels = new string[_availableResolutions.Length];
                for (int i = 0; i < _availableResolutions.Length; i++)
                {
                    var r = _availableResolutions[i];
                    _resolutionLabels[i] = $"{r.width}x{r.height} @{r.refreshRate}Hz";
                }

                // Clamp saved resolution index
                _selectedResolutionIndex = Mathf.Clamp(_selectedResolutionIndex, 0, _availableResolutions.Length - 1);

                // Try to match current resolution
                if (_selectedResolutionIndex == 0)
                {
                    for (int i = 0; i < _availableResolutions.Length; i++)
                    {
                        var r = _availableResolutions[i];
                        if (r.width == Screen.currentResolution.width &&
                            r.height == Screen.currentResolution.height)
                        {
                            _selectedResolutionIndex = i;
                            break;
                        }
                    }
                }
            }

            _selectedQualityLevel = Mathf.Clamp(
                _selectedQualityLevel,
                0,
                QualitySettings.names.Length - 1);

            ApplySavedResolution();
            CacheKeyBindingLabels();
            CacheQualityButtonLabels();
        }

        #endregion

        #region Show / Hide Overrides

        public override void Show()
        {
            if (_optionsTheme == null)
                _optionsTheme = Phase33_Themes.SettingsTheme();
            ApplyTheme(_optionsTheme);

            _currentTab = OptionsTab.Graphics;
            _statusMessageTimer = 0f;

            base.Show();
        }

        public override void Hide()
        {
            SaveSettings();
            base.Hide();
        }

        protected override void OnShow()
        {
            base.OnShow();
            CreateUIElements();
        }

        #endregion

        #region UI Construction

        /// <summary>uGUI 요소가 없으면 생성합니다.</summary>
        private void CreateUIElements()
        {
            if (_tabContainer != null) return;

            var root = _windowRoot != null ? _windowRoot : gameObject;

            // Tab container
            _tabContainer = new GameObject("TabContainer");
            _tabContainer.transform.SetParent(root.transform, false);
            var tabRect = _tabContainer.AddComponent<RectTransform>();
            tabRect.anchorMin = new Vector2(0f, 0.85f);
            tabRect.anchorMax = new Vector2(1f, 1f);
            tabRect.offsetMin = Vector2.zero;
            tabRect.offsetMax = Vector2.zero;

            // Content container
            _contentContainer = new GameObject("ContentContainer");
            _contentContainer.transform.SetParent(root.transform, false);
            var contentRect = _contentContainer.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(1f, 0.85f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
        }

        /// <summary>지정된 오브젝트 아래에 Text 레이블을 생성합니다.</summary>
        private Text CreateLabel(GameObject parent, string text, int fontSize, Color color, TextAnchor alignment)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent.transform, false);
            var txt = go.AddComponent<Text>();
            txt.text = text;
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize = fontSize;
            txt.color = color;
            txt.alignment = alignment;
            return txt;
        }

        /// <summary>지정된 오브젝트 아래에 Button을 생성합니다.</summary>
        private Button CreateSimpleButton(GameObject parent, string text, int fontSize, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button");
            go.transform.SetParent(parent.transform, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(onClick);

            var txt = go.AddComponent<Text>();
            txt.text = text;
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize = fontSize;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(160, _buttonHeight);
            return btn;
        }

        #endregion

        #region OnGUI (IMGUI Content)

        /// <summary>IMGUI 기반 옵션 내용을 그립니다. (UIWindow.OnGUI에서 호출)</summary>
        protected override void DrawWindowContent()
        {
            InitializeStyles();

            if (_windowRoot == null) return;

            var rect = _rectTransform != null ? _rectTransform.rect : new Rect(0, 0, 800, 600);
            Vector2 pos = _rectTransform != null ? (Vector2)_rectTransform.position : Vector2.zero;

            var windowRect = new Rect(
                pos.x + rect.x + _contentPadding,
                pos.y + rect.y + 10,
                rect.width - _contentPadding * 2,
                rect.height - 20);

            // Title
            GUI.Label(new Rect(windowRect.x, windowRect.y, windowRect.width, 40),
                "⚙ 설정", _titleStyle);

            // Tabs
            float tabY = windowRect.y + 45;
            DrawTabs((int)windowRect.x, (int)tabY, (int)windowRect.width);

            // Content
            float contentY = tabY + _tabButtonHeight + 15;
            float contentX = windowRect.x;
            float contentWidth = windowRect.width;

            switch (_currentTab)
            {
                case OptionsTab.Graphics:
                    DrawGraphicsTab((int)contentX, (int)contentY, (int)contentWidth);
                    break;
                case OptionsTab.Audio:
                    DrawAudioTab((int)contentX, (int)contentY, (int)contentWidth);
                    break;
                case OptionsTab.KeyBindings:
                    DrawKeyBindingsTab((int)contentX, (int)contentY, (int)contentWidth);
                    break;
            }

            // Status message (3초 표시)
            if (Time.time < _statusMessageTimer && _statusText != null)
            {
                GUI.Label(new Rect(contentX, contentY + 400, contentWidth, 30),
                    _statusText.text, _labelStyle);
            }
        }

        #endregion

        #region Tab Drawing

        private void DrawTabs(int x, int y, int width)
        {
            int tabWidth = (width - 10) / _tabValues.Length;
            int tabX = x;

            for (int i = 0; i < _tabValues.Length; i++)
            {
                bool isActive = _currentTab == _tabValues[i];
                GUI.backgroundColor = isActive ? _tabActiveColor : _tabInactiveColor;
                if (GUI.Button(new Rect(tabX + i * (tabWidth + 5), y, tabWidth, _tabButtonHeight),
                    _tabNames[i], isActive ? _tabActiveStyle : _tabStyle))
                {
                    _currentTab = _tabValues[i];
                }
            }
        }

        #endregion

        #region Graphics Tab

        private void DrawGraphicsTab(int x, int y, int width)
        {
            int currentY = y;

            // Quality setting
            GUI.Label(new Rect(x + 10, currentY, width - 20, 30), "품질 설정", _labelStyle);
            currentY += 28;

            if (_qualityButtonLabels != null && QualitySettings.names != null)
            {
                string[] qualityNames = QualitySettings.names;
                int optionsWidth = width - 40;
                int optionWidth = optionsWidth / qualityNames.Length;

                for (int i = 0; i < qualityNames.Length; i++)
                {
                    bool isSelected = _selectedQualityLevel == i;
                    GUI.backgroundColor = isSelected ? _sliderThumbColor : _sliderBgColor;
                    string btnLabel = isSelected
                        ? $"▶ {_qualityButtonLabels[i]}"
                        : _qualityButtonLabels[i];

                    int btnX = x + 20 + i * (optionWidth + 4);
                    if (GUI.Button(new Rect(btnX, currentY, optionWidth, _buttonHeight),
                        btnLabel, _fullscreenButtonStyle))
                    {
                        _selectedQualityLevel = i;
                        QualitySettings.SetQualityLevel(i, true);
                    }
                }
            }
            currentY += _buttonHeight + 12;

            // Resolution
            GUI.Label(new Rect(x + 10, currentY, width - 20, 30), "해상도", _labelStyle);
            currentY += 28;

            if (_availableResolutions != null && _availableResolutions.Length > 0)
            {
                int resBtnWidth = (width - 50) / 2;
                if (GUI.Button(new Rect(x + 20, currentY, resBtnWidth, _buttonHeight),
                    "<<", _fullscreenButtonStyle))
                {
                    _selectedResolutionIndex = Mathf.Max(0, _selectedResolutionIndex - 1);
                    ApplyCurrentResolution();
                }

                string resLabel = _selectedResolutionIndex < _resolutionLabels.Length
                    ? _resolutionLabels[_selectedResolutionIndex]
                    : "N/A";
                GUI.Label(new Rect(x + 20 + resBtnWidth + 10, currentY,
                    width - resBtnWidth * 2 - 60, _buttonHeight),
                    resLabel, _valueStyle);

                if (GUI.Button(new Rect(x + width - resBtnWidth - 20, currentY,
                    resBtnWidth, _buttonHeight), ">>", _fullscreenButtonStyle))
                {
                    _selectedResolutionIndex = Mathf.Min(
                        _availableResolutions.Length - 1,
                        _selectedResolutionIndex + 1);
                    ApplyCurrentResolution();
                }
            }
            currentY += _buttonHeight + 12;

            // Fullscreen toggle
            bool isFullscreen = Screen.fullScreen;
            GUI.backgroundColor = isFullscreen ? _successColor : _dangerColor;
            if (GUI.Button(new Rect(x + 20, currentY, width - 40, _buttonHeight),
                isFullscreen ? "✅ 전체화면" : "⬜ 창모드", _fullscreenButtonStyle))
            {
                Screen.fullScreen = !isFullscreen;
                if (_selectedResolutionIndex < _availableResolutions.Length)
                {
                    var res = _availableResolutions[_selectedResolutionIndex];
                    Screen.SetResolution(res.width, res.height, Screen.fullScreen);
                }
            }
            currentY += _buttonHeight + 12;

            // Apply / Back buttons
            GUI.backgroundColor = _buttonColor;
            if (GUI.Button(new Rect(x + 20, currentY, (width - 50) / 2, _buttonHeight),
                "적용", _backButtonStyle))
            {
                SaveSettings();
                ShowStatusMessage("✅ 설정이 저장되었습니다.");
            }
            GUI.backgroundColor = _backColor;
            if (GUI.Button(new Rect(x + 30 + (width - 50) / 2, currentY,
                (width - 50) / 2, _buttonHeight), "← 닫기", _backButtonStyle))
            {
                Hide();
            }
        }

        #endregion

        #region Audio Tab

        private void DrawAudioTab(int x, int y, int width)
        {
            int currentY = y;

            DrawVolumeSlider(ref currentY, x, width, "BGM (배경음악)", ref _bgmVolume);
            DrawVolumeSlider(ref currentY, x, width, "SFX (효과음)", ref _sfxVolume);
            DrawVolumeSlider(ref currentY, x, width, "UI (UI 사운드)", ref _uiVolume);
            DrawVolumeSlider(ref currentY, x, width, "Ambient (환경음)", ref _ambientVolume);

            // Guide
            GUI.Label(new Rect(x + 10, currentY + 10, width - 20, 30),
                "※ 변경사항은 자동 저장됩니다.", _guideLabelStyle);
        }

        private void DrawVolumeSlider(ref int y, int x, int width, string label, ref float volume)
        {
            GUI.Label(new Rect(x + 10, y, width - 20, 28), label, _labelStyle);
            y += 24;

            int sliderX = x + 15;
            int sliderWidth = width - 70;
            int sliderY = y;

            volume = GUI.HorizontalSlider(
                new Rect(sliderX, sliderY, sliderWidth, _sliderHeight),
                volume, 0f, 1f, _sliderBgStyle, _sliderThumbStyle);

            // Volume percentage
            GUI.Label(new Rect(sliderX + sliderWidth + 10, sliderY, 75, _sliderHeight),
                $"{(int)(volume * 100)}%", _valueStyle);

            y += _sliderHeight + 8;

            // Guide marks
            GUI.Label(new Rect(sliderX, y, 30, 18), "0%", _guideLabelStyle);
            GUI.Label(new Rect(sliderX + sliderWidth / 2 - 10, y, 35, 18), "50%", _guideLabelStyle);
            GUI.Label(new Rect(sliderX + sliderWidth - 35, y, 35, 18), "100%", _guideLabelStyle);
            y += 16;
        }

        #endregion

        #region KeyBindings Tab

        private void DrawKeyBindingsTab(int x, int y, int width)
        {
            if (_keyBindingLabels == null)
            {
                GUI.Label(new Rect(x + 10, y, width - 20, 30),
                    "키 설정 데이터가 없습니다.", _labelStyle);
                return;
            }

            int currentY = y;
            int labelHeight = 28;

            for (int i = 0; i < _keyBindingLabels.Length; i++)
            {
                if (currentY + labelHeight > y + 400) break;

                GUI.Label(new Rect(x + 20, currentY, width - 40, labelHeight),
                    _keyBindingLabels[i], _labelStyle);
                currentY += labelHeight + 4;
            }

            GUI.Label(new Rect(x + 10, currentY + 10, width - 20, 30),
                "※ 키 변경은 KeyBindings 창에서 가능합니다.", _guideLabelStyle);
        }

        #endregion

        #region Settings Persistence

        private void SaveSettings()
        {
            PlayerPrefs.SetInt("Settings_Quality", _selectedQualityLevel);
            PlayerPrefs.SetInt("Settings_Resolution", _selectedResolutionIndex);
            PlayerPrefs.SetFloat("Settings_BGM", _bgmVolume);
            PlayerPrefs.SetFloat("Settings_SFX", _sfxVolume);
            PlayerPrefs.SetFloat("Settings_UI", _uiVolume);
            PlayerPrefs.SetFloat("Settings_Ambient", _ambientVolume);
            PlayerPrefs.Save();

            QualitySettings.SetQualityLevel(_selectedQualityLevel, true);
            Debug.Log("[OptionsUI] 설정 저장 완료");
        }

        private void ApplySavedResolution()
        {
            if (_selectedResolutionIndex > 0 &&
                _availableResolutions != null &&
                _selectedResolutionIndex < _availableResolutions.Length)
            {
                var res = _availableResolutions[_selectedResolutionIndex];
                Screen.SetResolution(res.width, res.height, Screen.fullScreen);
            }
        }

        private void ApplyCurrentResolution()
        {
            if (_availableResolutions != null &&
                _selectedResolutionIndex < _availableResolutions.Length)
            {
                var res = _availableResolutions[_selectedResolutionIndex];
                Screen.SetResolution(res.width, res.height, Screen.fullScreen);
            }
        }

        private void ShowStatusMessage(string message)
        {
            if (_statusText == null)
            {
                var go = new GameObject("StatusText");
                go.transform.SetParent(_windowRoot != null ? _windowRoot.transform : transform, false);
                _statusText = go.AddComponent<Text>();
                _statusText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                _statusText.fontSize = 24;
                _statusText.alignment = TextAnchor.MiddleCenter;
                _statusText.color = Color.green;
            }
            _statusText.text = message;
            _statusMessageTimer = Time.time + 3f;
        }

        #endregion

        #region Caching

        private void CacheStrings()
        {
            if (_stringsCached) return;
            _tabNames = new[] { "그래픽", "오디오", "키 설정" };
            _tabValues = new[] { OptionsTab.Graphics, OptionsTab.Audio, OptionsTab.KeyBindings };
            _stringsCached = true;
        }

        private void CacheKeyBindingLabels()
        {
            var keyBindings = new (string name, string key)[]
            {
                ("이동 (WASD)", "W/A/S/D"),
                ("달리기", "Shift"),
                ("점프", "Space"),
                ("공격", "좌클릭"),
                ("상호작용", "E"),
                ("인벤토리", "I"),
                ("레시피", "R"),
                ("퀘스트", "Q"),
                ("월드맵", "M"),
                ("크래프트", "C"),
                ("복수명부", "K"),
                ("ESC 메뉴", "ESC"),
            };
            _keyBindingLabels = new string[keyBindings.Length];
            for (int i = 0; i < keyBindings.Length; i++)
            {
                _keyBindingLabels[i] = $"{keyBindings[i].name}:  [{keyBindings[i].key}]";
            }
        }

        private void CacheQualityButtonLabels()
        {
            string[] qualityNames = QualitySettings.names;
            _qualityButtonLabels = new string[qualityNames.Length];
            for (int i = 0; i < qualityNames.Length; i++)
            {
                _qualityButtonLabels[i] = qualityNames[i];
            }
        }

        #endregion

        #region IMGUI Styles

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _titleStyle = new GUIStyle
            {
                fontSize = 48,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            _tabStyle = new GUIStyle
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            _tabStyle.hover.background = MakeTexture(1, 1, new Color(0.3f, 0.5f, 0.7f, 1f));
            _tabStyle.active.background = MakeTexture(1, 1, new Color(0.2f, 0.3f, 0.5f, 1f));

            _tabActiveStyle = new GUIStyle(_tabStyle);
            _tabActiveStyle.normal.background = MakeTexture(1, 1, _tabActiveColor);

            _labelStyle = new GUIStyle
            {
                fontSize = 28,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = _labelColor },
                padding = new RectOffset(4, 4, 2, 2)
            };

            _valueStyle = new GUIStyle
            {
                fontSize = _valueFontSize,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = Color.white },
                padding = new RectOffset(4, 12, 2, 2)
            };

            _backButtonStyle = new GUIStyle
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            _backButtonStyle.hover.background = MakeTexture(1, 1, new Color(0.8f, 0.3f, 0.3f, 1f));
            _backButtonStyle.active.background = MakeTexture(1, 1, new Color(0.5f, 0.1f, 0.1f, 1f));

            _fullscreenButtonStyle = new GUIStyle
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            _fullscreenButtonStyle.hover.background = MakeTexture(1, 1, new Color(0.3f, 0.6f, 0.3f, 1f));
            _fullscreenButtonStyle.active.background = MakeTexture(1, 1, new Color(0.1f, 0.4f, 0.1f, 1f));

            // Background and dim textures
            _dimTexture = MakeTexture(1, 1, new Color(0f, 0f, 0f, 0.5f));
            _dimStyle = new GUIStyle { normal = { background = _dimTexture } };

            _bgTexture = MakeTexture(1, 1, new Color(0f, 0f, 0f, 0.88f));
            _bgBoxStyle = new GUIStyle { normal = { background = _bgTexture } };

            _sliderBgTex = MakeTexture(1, 1, _sliderBgColor);
            _sliderBgStyle = new GUIStyle { normal = { background = _sliderBgTex } };

            _sliderThumbTex = MakeTexture(1, 1, _sliderThumbColor);
            _sliderThumbStyle = new GUIStyle { normal = { background = _sliderThumbTex } };

            _guideLabelStyle = new GUIStyle
            {
                fontSize = 20,
                normal = { textColor = Color.gray }
            };

            _stylesInitialized = true;
        }

        private static Texture2D MakeTexture(int width, int height, Color color)
        {
            var tex = new Texture2D(width, height);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    tex.SetPixel(x, y, color);
            tex.Apply();
            return tex;
        }

        #endregion
    }
}