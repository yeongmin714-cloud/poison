using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectName.UI.Themes;

namespace ProjectName.UI
{
    /// <summary>
    /// G3-03: 설정 메뉴 UI.
    /// Graphics(품질/해상도), Audio(BGM/SFX/UI/Ambient), KeyBindings 표시.
    /// PlayerPrefs에 저장/불러오기.
    /// IMGUI 기반.
    /// </summary>
    public class SettingsMenuUI : MonoBehaviour
    {
        public static SettingsMenuUI Instance { get; private set; }

        [Header("Layout")]
        [SerializeField] private int _windowWidth = 800;
        [SerializeField] private int _windowHeight = 620;
        [SerializeField] private int _tabButtonHeight = 50;
        [SerializeField] private int _buttonHeight = 32;

        [Header("Colors")]
        [SerializeField] private Color _bgColor = new Color(0f, 0f, 0f, 0.88f);
        [SerializeField] private Color _textColor = Color.white;
        [SerializeField] private Color _tabActiveColor = new Color(0.3f, 0.5f, 0.7f, 1f);
        [SerializeField] private Color _tabInactiveColor = new Color(0.15f, 0.25f, 0.35f, 0.9f);
        [SerializeField] private Color _sliderBgColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        [SerializeField] private Color _sliderThumbColor = new Color(0.5f, 0.7f, 0.9f, 1f);
        [SerializeField] private Color _backColor = new Color(0.6f, 0.2f, 0.2f, 0.9f);
        [SerializeField] private Color _labelColor = new Color(0.8f, 0.8f, 0.8f, 1f);

        // ===== 탭 종류 =====
        private enum SettingsTab { Graphics, Audio, KeyBindings }
        private SettingsTab _currentTab = SettingsTab.Graphics;

        // ===== 상태 =====
        private bool _isVisible;
        private int _selectedQualityLevel;
        private int _selectedResolutionIndex;
        private Resolution[] _availableResolutions;
        private string[] _resolutionLabels;

        private float _bgmVolume = 0.8f;
        private float _sfxVolume = 0.8f;
        private float _uiVolume = 0.7f;
        private float _ambientVolume = 0.6f;

        // ===== 캐싱된 스타일 (OnGUI GC 방지) =====
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

        // ===== 캐싱된 문자열 (OnGUI string GC 방지) =====
        private string[] _keyBindingLabels;
        private string[] _qualityButtonLabels;

        // ===== 콜백 =====
        public Action OnSettingsClosed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadSettings();
        }

        private void Start()
        {
            _availableResolutions = Screen.resolutions;
            _resolutionLabels = new string[_availableResolutions.Length];
            for (int i = 0; i < _availableResolutions.Length; i++)
            {
                var r = _availableResolutions[i];
                _resolutionLabels[i] = $"{r.width}x{r.height} @{r.refreshRate}Hz";
            }

            // 현재 해상도와 가장 가까운 인덱스 찾기
            // (저장된 값이 없거나 유효하지 않은 인덱스인 경우에만)
            if (_selectedResolutionIndex <= 0 || _selectedResolutionIndex >= _availableResolutions.Length)
            {
                _selectedResolutionIndex = 0;
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

            _selectedQualityLevel = QualitySettings.GetQualityLevel();

            // 저장된 해상도 즉시 적용
            // (LoadSettings 시점엔 _availableResolutions가 null이었으므로 여기서 적용)
            ApplySavedResolution();

            // 키 바인딩 라벨 미리 포맷팅 (OnGUI string GC 방지)
            CacheKeyBindingLabels();
            // 품질 버튼 라벨 미리 포맷팅 (OnGUI string GC 방지)
            CacheQualityButtonLabels();
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

        // ===== 설정 저장/로드 =====

        private void LoadSettings()
        {
            _selectedQualityLevel = PlayerPrefs.GetInt("Settings_Quality", QualitySettings.GetQualityLevel());
            _selectedResolutionIndex = PlayerPrefs.GetInt("Settings_Resolution", 0);
            _bgmVolume = PlayerPrefs.GetFloat("Settings_BGM", 0.8f);
            _sfxVolume = PlayerPrefs.GetFloat("Settings_SFX", 0.8f);
            _uiVolume = PlayerPrefs.GetFloat("Settings_UI", 0.7f);
            _ambientVolume = PlayerPrefs.GetFloat("Settings_Ambient", 0.6f);
            // NOTE: 해상도 적용은 Start()에서 _availableResolutions 초기화 후 수행
        }

        private void ApplySavedResolution()
        {
            if (_selectedResolutionIndex >= 0 && _selectedResolutionIndex < _availableResolutions.Length)
            {
                var res = _availableResolutions[_selectedResolutionIndex];
                Screen.SetResolution(res.width, res.height, Screen.fullScreen);
            }
        }

        private void SaveSettings()
        {
            PlayerPrefs.SetInt("Settings_Quality", _selectedQualityLevel);
            PlayerPrefs.SetFloat("Settings_BGM", _bgmVolume);
            PlayerPrefs.SetFloat("Settings_SFX", _sfxVolume);
            PlayerPrefs.SetFloat("Settings_UI", _uiVolume);
            PlayerPrefs.SetFloat("Settings_Ambient", _ambientVolume);
            PlayerPrefs.Save();

            QualitySettings.SetQualityLevel(_selectedQualityLevel, true);
            Debug.Log("[SettingsMenuUI] 설정 저장 완료");
        }

        // ===== 표시/숨김 =====

        public void Show()
        {
            _isVisible = true;
            _currentTab = SettingsTab.Graphics;
        }

        public void Hide()
        {
            _isVisible = false;
            SaveSettings();
            OnSettingsClosed?.Invoke();
        }

        // ===== 스타일 초기화 =====

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _titleStyle = new GUIStyle
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _textColor }
            };

            _tabStyle = new GUIStyle
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _textColor }
            };
            _tabStyle.hover.background = MakeTexture(1, 1, new Color(0.3f, 0.5f, 0.7f, 1f));
            _tabStyle.active.background = MakeTexture(1, 1, new Color(0.2f, 0.3f, 0.5f, 1f));

            _tabActiveStyle = new GUIStyle(_tabStyle);
            _tabActiveStyle.normal.background = MakeTexture(1, 1, _tabActiveColor);

            _labelStyle = new GUIStyle
            {
                fontSize = 16,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = _labelColor },
                padding = new RectOffset(4, 4, 2, 2)
            };

            _valueStyle = new GUIStyle
            {
                fontSize = 16,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = _textColor },
                padding = new RectOffset(4, 12, 2, 2)
            };

            _backButtonStyle = new GUIStyle
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _textColor }
            };
            _backButtonStyle.hover.background = MakeTexture(1, 1, new Color(0.8f, 0.3f, 0.3f, 1f));
            _backButtonStyle.active.background = MakeTexture(1, 1, new Color(0.5f, 0.1f, 0.1f, 1f));

            _fullscreenButtonStyle = new GUIStyle
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _textColor }
            };
            _fullscreenButtonStyle.hover.background = MakeTexture(1, 1, new Color(0.3f, 0.6f, 0.3f, 1f));
            _fullscreenButtonStyle.active.background = MakeTexture(1, 1, new Color(0.1f, 0.4f, 0.1f, 1f));

            // ===== OnGUI GC 방지: 미리 텍스처 + 스타일 캐싱 =====
            _dimTexture = MakeTexture(1, 1, new Color(0f, 0f, 0f, 0.5f));
            _dimStyle = new GUIStyle { normal = { background = _dimTexture } };

            _bgTexture = MakeTexture(1, 1, _bgColor);
            _bgBoxStyle = new GUIStyle { normal = { background = _bgTexture } };

            _sliderBgTex = MakeTexture(1, 1, _sliderBgColor);
            _sliderBgStyle = new GUIStyle { normal = { background = _sliderBgTex } };

            _sliderThumbTex = MakeTexture(1, 1, _sliderThumbColor);
            _sliderThumbStyle = new GUIStyle { normal = { background = _sliderThumbTex } };

            _guideLabelStyle = new GUIStyle
            {
                fontSize = 12,
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

        // ===== OnGUI =====

        private void OnGUI()
        {
            InitializeStyles();
            if (!_isVisible) return;

            // 배경 딤 (캐싱된 텍스처/스타일 — GC 할당 0)
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "", _dimStyle);

            int centerX = (Screen.width - _windowWidth) / 2;
            int centerY = (Screen.height - _windowHeight) / 2;

            // 메인 박스 배경 (캐싱된 텍스처/스타일 — GC 할당 0)
            GUI.Box(new Rect(centerX, centerY, _windowWidth, _windowHeight), "", _bgBoxStyle);

            // 제목
            GUI.Label(new Rect(centerX, centerY + 12, _windowWidth, 52), "설정", _titleStyle);

            // ===== 탭 =====
            DrawTabs(centerX, centerY);

            // ===== 탭 내용 =====
            int contentY = centerY + 60 + _tabButtonHeight + 10;
            int contentX = centerX + 15;
            int contentWidth = _windowWidth - 30;

            switch (_currentTab)
            {
                case SettingsTab.Graphics:
                    DrawGraphicsTab(contentX, contentY, contentWidth);
                    break;
                case SettingsTab.Audio:
                    DrawAudioTab(contentX, contentY, contentWidth);
                    break;
                case SettingsTab.KeyBindings:
                    DrawKeyBindingsTab(contentX, contentY, contentWidth);
                    break;
            }

            // ===== 닫기 버튼 =====
            int backButtonY = centerY + _windowHeight - 50;
            int backButtonWidth = 270;
            int backButtonX = centerX + (_windowWidth - backButtonWidth) / 2;
            GUI.backgroundColor = _backColor;
            if (GUI.Button(new Rect(backButtonX, backButtonY, backButtonWidth, _backButtonStyle.fontSize + 12), "← 닫기", _backButtonStyle))
            {
                Hide();
            }
        }

        private void DrawTabs(int centerX, int centerY)
        {
            int tabY = centerY + 55;
            string[] tabNames = { "그래픽", "오디오", "키 설정" };
            SettingsTab[] tabs = { SettingsTab.Graphics, SettingsTab.Audio, SettingsTab.KeyBindings };
            int tabWidth = (_windowWidth - 30) / 3;
            int tabX = centerX + 15;

            for (int i = 0; i < tabs.Length; i++)
            {
                bool isActive = _currentTab == tabs[i];
                GUI.backgroundColor = isActive ? _tabActiveColor : _tabInactiveColor;
                if (GUI.Button(new Rect(tabX + i * (tabWidth + 5), tabY, tabWidth, _tabButtonHeight),
                    tabNames[i], isActive ? _tabActiveStyle : _tabStyle))
                {
                    _currentTab = tabs[i];
                }
            }
        }

        // ===== Graphics 탭 =====

        private void DrawGraphicsTab(int x, int y, int width)
        {
            int currentY = y;

            // 품질 설정
            GUI.Label(new Rect(x, currentY, width, 38), "품질 설정", _labelStyle);
            currentY += 30;

            string[] qualityNames = QualitySettings.names;
            int optionsWidth = width - 20;
            int optionWidth = optionsWidth / qualityNames.Length;

            for (int i = 0; i < qualityNames.Length; i++)
            {
                bool isSelected = _selectedQualityLevel == i;
                GUI.backgroundColor = isSelected ? _sliderThumbColor : _sliderBgColor;
                // 선택된 버튼에만 "▶ " 접두사 (1개만 할당, 미미함)
                string btnLabel = isSelected ? $"▶ {_qualityButtonLabels[i]}" : _qualityButtonLabels[i];

                int btnX = x + 10 + i * (optionWidth + 4);
                if (GUI.Button(new Rect(btnX, currentY, optionWidth, _buttonHeight), btnLabel, _fullscreenButtonStyle))
                {
                    _selectedQualityLevel = i;
                    QualitySettings.SetQualityLevel(i, true);
                }
            }
            currentY += _buttonHeight + 15;

            // 해상도
            GUI.Label(new Rect(x, currentY, width, 38), "해상도", _labelStyle);
            currentY += 30;

            int resBtnWidth = (width - 30) / 2;
            if (GUI.Button(new Rect(x + 10, currentY, resBtnWidth, _buttonHeight), "<<", _fullscreenButtonStyle))
            {
                _selectedResolutionIndex = Mathf.Max(0, _selectedResolutionIndex - 1);
                ApplyCurrentResolution();
            }
            GUI.Label(new Rect(x + 10 + resBtnWidth + 10, currentY, width - resBtnWidth * 2 - 40, _buttonHeight),
                _selectedResolutionIndex < _resolutionLabels.Length ? _resolutionLabels[_selectedResolutionIndex] : "N/A", _valueStyle);
            if (GUI.Button(new Rect(x + width - resBtnWidth - 10, currentY, resBtnWidth, _buttonHeight), ">>", _fullscreenButtonStyle))
            {
                _selectedResolutionIndex = Mathf.Min(_availableResolutions.Length - 1, _selectedResolutionIndex + 1);
                ApplyCurrentResolution();
            }
            currentY += _buttonHeight + 15;

            // 전체화면 토글
            bool isFullscreen = Screen.fullScreen;
            GUI.backgroundColor = isFullscreen ? new Color(0.3f, 0.6f, 0.3f, 1f) : new Color(0.5f, 0.3f, 0.3f, 1f);
            if (GUI.Button(new Rect(x + 10, currentY, width - 20, _buttonHeight),
                isFullscreen ? "✅ 전체화면" : "⬜ 창모드", _fullscreenButtonStyle))
            {
                Screen.fullScreen = !isFullscreen;
                if (_selectedResolutionIndex < _availableResolutions.Length)
                {
                    var res = _availableResolutions[_selectedResolutionIndex];
                    Screen.SetResolution(res.width, res.height, Screen.fullScreen);
                }
            }
            currentY += _buttonHeight + 15;
        }

        // ===== Audio 탭 =====

        private void DrawAudioTab(int x, int y, int width)
        {
            int currentY = y;

            DrawVolumeSlider(ref currentY, x, width, "BGM (배경음악)", ref _bgmVolume);
            DrawVolumeSlider(ref currentY, x, width, "SFX (효과음)", ref _sfxVolume);
            DrawVolumeSlider(ref currentY, x, width, "UI (UI 사운드)", ref _uiVolume);
            DrawVolumeSlider(ref currentY, x, width, "Ambient (환경음)", ref _ambientVolume);

            // 설명
            GUI.Label(new Rect(x + 10, currentY + 10, width, 30),
                "※ 변경사항은 자동 저장됩니다.", _labelStyle);
        }

        private void DrawVolumeSlider(ref int y, int x, int width, string label, ref float volume)
        {
            GUI.Label(new Rect(x + 10, y, width - 20, 38), label, _labelStyle);
            y += 28;

            int sliderX = x + 15;
            int sliderWidth = width - 70;
            int sliderY = y;
            int sliderHeight = 30;

            // 캐싱된 슬라이더 스타일 사용 (GC 할당 0)
            volume = GUI.HorizontalSlider(new Rect(sliderX, sliderY, sliderWidth, sliderHeight), volume, 0f, 1f, _sliderBgStyle, _sliderThumbStyle);

            // 볼륨 % 표시 (매 프레임 string 할당, 불가피 — 5~15바이트, 초소형)
            GUI.Label(new Rect(sliderX + sliderWidth + 10, sliderY, 75, sliderHeight),
                $"{(int)(volume * 100)}%", _valueStyle);

            y += sliderHeight + 10;

            // 0% / 50% / 100% 가이드 (캐싱된 스타일 사용 — GC 할당 0)
            GUI.Label(new Rect(sliderX, y, 45, 22), "0%", _guideLabelStyle);
            GUI.Label(new Rect(sliderX + sliderWidth / 2 - 10, y, 45, 22), "50%", _guideLabelStyle);
            GUI.Label(new Rect(sliderX + sliderWidth - 30, y, 45, 22), "100%", _guideLabelStyle);
            y += 18;
        }

        // ===== KeyBindings 탭 =====

        private void DrawKeyBindingsTab(int x, int y, int width)
        {
            int currentY = y;

            int keyWidth = (width - 30) / 2;
            int lineHeight = 36;

            // 미리 포맷팅된 _keyBindingLabels 사용 (GC 할당 0)
            for (int i = 0; i < _keyBindingLabels.Length; i++)
            {
                int row = i / 2;
                int col = i % 2;
                int bx = x + 10 + col * (keyWidth + 10);
                int by = currentY + row * lineHeight;

                GUI.Label(new Rect(bx, by, keyWidth - 10, lineHeight),
                    _keyBindingLabels[i], _labelStyle);
            }
        }

        /// <summary>현재 선택된 해상도를 즉시 적용하고 PlayerPrefs에 저장합니다.</summary>
        private void ApplyCurrentResolution()
        {
            if (_selectedResolutionIndex >= 0 && _selectedResolutionIndex < _availableResolutions.Length)
            {
                var res = _availableResolutions[_selectedResolutionIndex];
                Screen.SetResolution(res.width, res.height, Screen.fullScreen);
                PlayerPrefs.SetInt("Settings_Resolution", _selectedResolutionIndex);
                PlayerPrefs.Save();
            }
        }
    }
}