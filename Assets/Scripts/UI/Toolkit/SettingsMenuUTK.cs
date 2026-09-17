using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Systems;   // AccessibilityManager
using ProjectName.UI;        // UIFont

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U5 Round 1-B — 설정 메뉴 (독립 메뉴 + 접근성 탭).
    /// 원본: Assets/Scripts/UI/SettingsMenuUI.cs (642줄, IMGUI) — 본 파일은 그 데이터 경로만 이식.
    /// 원본은 절대 수정하지 않는다.
    ///
    /// [역할 분담 — 실측]
    ///  - SettingsMenuUI(→SettingsMenuUTK): ESC 메뉴 "⚙ 설정" 버튼으로 여는 독립 설정 메뉴.
    ///    탭: 그래픽/오디오/키 설정/⚙접근성(4탭).
    ///  - OptionsUI(→OptionsUTK): 게임 내 설정(그래픽/오디오/키 설정 3탭).
    ///  두 창은 동일 PlayerPrefs 키(Settings_*)를 그대로 사용 — 공용 저장 경로, 복제 금지.
    ///
    /// [접근성 — 실측 API] AccessibilityManager (static):
    ///   ColorBlindMode(bool get), TooltipDelay(float get), SubtitleScale(float get),
    ///   SetTooltipDelay/SetColorBlindMode/SetSubtitleScale(즉시 저장), SaveSettings(), Initialize().
    ///   원본 DrawAccessibilityTab과 동일 바인딩.
    ///
    /// [컨트롤] DropdownField/Slider/Toggle (UTK 기본 컨트롤) + UTKButton 탭 바.
    /// </summary>
    public class SettingsMenuUTK : UTKWindowBase
    {
        // ─────────────────────────── 싱글턴 / 팩토리 ───────────────────────────
        private static SettingsMenuUTK _instance;
        public static SettingsMenuUTK Instance => _instance;

        /// <summary>팩토리 — 멱등 생성. 접근성 초기화는 원본 Awake 패리티로 여기서 1회 보장.</summary>
        public static void Ensure()
        {
            if (_instance != null) return;
            AccessibilityManager.Initialize();   // 원본 SettingsMenuUI.Awake() 패리티 (멱등)
            _instance = new SettingsMenuUTK();
        }

        /// <summary>열기 (독립 설정 메뉴 — ESC 메뉴 "설정" 버튼 연동 지점).</summary>
        public static void Open()
        {
            Ensure();
            _instance.Show();
        }

        /// <summary>토글 — static은 반드시 IsOpen 분기.</summary>
        public static void Toggle()
        {
            if (_instance != null && _instance.IsOpen) { _instance.Close(); return; }
            Open();
        }

        // ─────────────────────────── 설정 ───────────────────────────
        private const float WinW = 760f;
        private const float WinH = 600f;

        private enum Tab { Graphics, Audio, KeyBindings, Accessibility }
        private Tab _currentTab = Tab.Graphics;

        // 그래픽 상태 (원본 실측 PlayerPrefs 키 — OptionsUTK와 공유)
        private int _qualityLevel = 2;
        private int _selectedResolutionIndex;
        private Resolution[] _resolutionList;

        // 오디오 상태
        private float _bgmVolume = 0.8f;
        private float _sfxVolume = 0.8f;
        private float _uiVolume = 0.7f;
        private float _ambientVolume = 0.6f;

        // 티어별 컨테이너
        private readonly VisualElement _graphicsRoot;
        private readonly VisualElement _audioRoot;
        private readonly VisualElement _keysRoot;
        private readonly VisualElement _accessRoot;
        private DropdownField _qualityDrop, _resDrop;
        private Toggle _fsToggle, _colorBlindToggle;
        private Slider _tooltipSlider, _subtitleSlider;
        private Label _colorBlindDescLabel;

        private SettingsMenuUTK() : base("⚙ 설정", new Vector2(WinW, WinH))
        {
            LoadFromPrefs();

            _content.style.flexDirection = FlexDirection.Column;
            _content.style.flexGrow = 1f;

            BuildTabBar();
            _graphicsRoot = new VisualElement { name = "GraphicsTab" };
            _audioRoot = new VisualElement { name = "AudioTab" };
            _keysRoot = new VisualElement { name = "KeysTab" };
            _accessRoot = new VisualElement { name = "AccessibilityTab" };
            _content.Add(_graphicsRoot);
            _content.Add(_audioRoot);
            _content.Add(_keysRoot);
            _content.Add(_accessRoot);

            BuildGraphicsTab(_graphicsRoot);
            BuildAudioTab(_audioRoot);
            BuildKeyBindingsTab(_keysRoot);
            BuildAccessibilityTab(_accessRoot);

            RefreshResolutionList();

            ApplyUIToolkitFont(this);
            SelectTab(Tab.Graphics);
            style.display = DisplayStyle.None;
            style.left = 160f;
            style.top = 80f;
        }

        // ─────────────────────────── 저장 로드 ───────────────────────────
        private void LoadFromPrefs()
        {
            _qualityLevel = PlayerPrefs.GetInt("Settings_Quality", QualitySettings.GetQualityLevel());
            _selectedResolutionIndex = PlayerPrefs.GetInt("Settings_Resolution", 0);
            _bgmVolume = PlayerPrefs.GetFloat("Settings_BGM", 0.8f);
            _sfxVolume = PlayerPrefs.GetFloat("Settings_SFX", 0.8f);
            _uiVolume = PlayerPrefs.GetFloat("Settings_UI", 0.7f);
            _ambientVolume = PlayerPrefs.GetFloat("Settings_Ambient", 0.6f);

            int maxQ = QualitySettings.names != null && QualitySettings.names.Length > 0
                ? QualitySettings.names.Length - 1 : 0;
            _qualityLevel = Mathf.Clamp(_qualityLevel, 0, maxQ);
        }

        /// <summary>원본 SaveSettings() 키 그대로 — 공용 저장 경로.</summary>
        private void SavePrefs()
        {
            PlayerPrefs.SetInt("Settings_Quality", _qualityLevel);
            PlayerPrefs.SetInt("Settings_Resolution", _selectedResolutionIndex);
            PlayerPrefs.SetFloat("Settings_BGM", _bgmVolume);
            PlayerPrefs.SetFloat("Settings_SFX", _sfxVolume);
            PlayerPrefs.SetFloat("Settings_UI", _uiVolume);
            PlayerPrefs.SetFloat("Settings_Ambient", _ambientVolume);
            PlayerPrefs.Save();
            QualitySettings.SetQualityLevel(_qualityLevel, true);
            AccessibilityManager.SaveSettings();   // 원본: 접근성 PlayerPrefs(Access_*) 동시 저장
            Debug.Log("[SettingsUTK] 설정 저장 완료");
        }

        // ─────────────────────────── 탭 바 ───────────────────────────
        private void BuildTabBar()
        {
            var bar = new VisualElement { name = "TabBar" };
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.marginBottom = 8f;
            _content.Add(bar);

            BarBtn(bar, "그래픽", Tab.Graphics);
            BarBtn(bar, "오디오", Tab.Audio);
            BarBtn(bar, "키 설정", Tab.KeyBindings);
            BarBtn(bar, "⚙ 접근성", Tab.Accessibility);
        }

        private void BarBtn(VisualElement bar, string label, Tab tab)
        {
            var b = UTKButton.Create(label, () => SelectTab(tab), UTKButton.Variant.Secondary);
            b.style.flexGrow = 1f;
            b.style.height = 32f;
            b.name = "Tab_" + label;
            bar.Add(b);
        }

        private void SelectTab(Tab tab)
        {
            _currentTab = tab;
            _graphicsRoot.style.display = (_currentTab == Tab.Graphics) ? DisplayStyle.Flex : DisplayStyle.None;
            _audioRoot.style.display = (_currentTab == Tab.Audio) ? DisplayStyle.Flex : DisplayStyle.None;
            _keysRoot.style.display = (_currentTab == Tab.KeyBindings) ? DisplayStyle.Flex : DisplayStyle.None;
            _accessRoot.style.display = (_currentTab == Tab.Accessibility) ? DisplayStyle.Flex : DisplayStyle.None;
            Debug.Log($"[SettingsUTK] 탭 전환 → {tab}");
        }

        // ─────────────────────────── 그래픽 탭 ───────────────────────────
        private void BuildGraphicsTab(VisualElement root)
        {
            root.style.paddingTop = 6f;
root.style.paddingBottom = 6f;
root.style.paddingLeft = 6f;
root.style.paddingRight = 6f;

            root.Add(MkLabel("품질 설정", 16, UTKColor.AccentRare));
            _qualityDrop = new DropdownField("품질", QualityNameList(), 0);
            _qualityDrop.name = "QualityDropdown";
            _qualityDrop.style.width = 260f;
            _qualityDrop.index = Mathf.Clamp(_qualityLevel, 0, Mathf.Max(0, _qualityDrop.choices.Count - 1));
            _qualityDrop.RegisterValueChangedCallback(_ =>
            {
                _qualityLevel = _qualityDrop.index;
                SavePrefs();
            });
            root.Add(_qualityDrop);

            Span(root, 10f);

            root.Add(MkLabel("해상도", 16, UTKColor.AccentRare));
            _resDrop = new DropdownField("해상도", new List<string>(), 0);
            _resDrop.name = "ResolutionDropdown";
            _resDrop.style.width = 300f;
            _resDrop.RegisterValueChangedCallback(_ =>
            {
                _selectedResolutionIndex = _resDrop.index;
                ApplyCurrentResolution();
                PlayerPrefs.SetInt("Settings_Resolution", _selectedResolutionIndex);
                PlayerPrefs.Save();
            });
            root.Add(_resDrop);

            Span(root, 10f);

            _fsToggle = new Toggle("전체 화면") { value = Screen.fullScreen };
            _fsToggle.name = "FullscreenToggle";
            _fsToggle.RegisterValueChangedCallback(evt =>
            {
                Screen.fullScreen = evt.newValue;
                if (_resolutionList != null && _selectedResolutionIndex >= 0 && _selectedResolutionIndex < _resolutionList.Length)
                {
                    var r = _resolutionList[_selectedResolutionIndex];
                    Screen.SetResolution(r.width, r.height, evt.newValue);
                }
            });
            root.Add(_fsToggle);
        }

        // ─────────────────────────── 오디오 탭 ───────────────────────────
        private void BuildAudioTab(VisualElement root)
        {
            root.style.paddingTop = 6f;
root.style.paddingBottom = 6f;
root.style.paddingLeft = 6f;
root.style.paddingRight = 6f;

            AddVolumeSlider(root, "BGM (배경음악)", _bgmVolume, v => _bgmVolume = v);
            AddVolumeSlider(root, "SFX (효과음)", _sfxVolume, v => _sfxVolume = v);
            AddVolumeSlider(root, "UI (UI 사운드)", _uiVolume, v => _uiVolume = v);
            AddVolumeSlider(root, "Ambient (환경음)", _ambientVolume, v => _ambientVolume = v);

            root.Add(MkDesc("  ※ 변경사항은 자동 저장됩니다.", 12, UTKColor.TextSecondary));
        }

        private void AddVolumeSlider(VisualElement root, string label, float init,
                                     System.Action<float> setter)
        {
            var wrap = new VisualElement();
            wrap.style.flexDirection = FlexDirection.Column;
            wrap.style.marginBottom = 6f;
            wrap.style.paddingBottom = 2f;

            var head = new VisualElement();
            head.style.flexDirection = FlexDirection.Row;
            head.style.alignItems = Align.Center;
            var nameLbl = MkLabel(label, 15, UTKColor.TextPrimary);
            nameLbl.style.flexGrow = 1f;
            head.Add(nameLbl);

            var valueLabel = MkLabel($"{(int)(init * 100f)}%", 15, UTKColor.GuildGreen);
            valueLabel.style.width = 70f;
            valueLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            head.Add(valueLabel);
            wrap.Add(head);

            var slider = new Slider(0f, 1f) { value = init };
            slider.name = "Vol_" + label;
            slider.RegisterValueChangedCallback(evt =>
            {
                setter(evt.newValue);
                valueLabel.text = $"{(int)(evt.newValue * 100f)}%";
                SavePrefs();
            });
            wrap.Add(slider);
            root.Add(wrap);
        }

        // ─────────────────────────── 키 설정 탭 ───────────────────────────
        private static readonly (string name, string key)[] KeyBindings =
        {
            ("이동 (WASD)", "W/A/S/D"), ("달리기", "Shift"), ("점프", "Space"),
            ("공격", "좌클릭"), ("상호작용", "E"), ("인벤토리", "I"),
            ("레시피", "R"), ("퀘스트", "Q"), ("월드맵", "M"),
            ("크래프트", "C"), ("복수명부", "K"), ("ESC 메뉴", "ESC"),
        };

        private void BuildKeyBindingsTab(VisualElement root)
        {
            root.style.paddingTop = 6f;
root.style.paddingBottom = 6f;
root.style.paddingLeft = 6f;
root.style.paddingRight = 6f;
            root.Add(MkLabel("키 설정 (읽기 전용 — 게임 내 옵션에서도 동일)", 15, UTKColor.AccentRare));

            var list = new ScrollView();
            list.style.flexGrow = 1f;
            list.style.marginTop = 6f;
            root.Add(list);

            foreach (var kb in KeyBindings)
            {
                var line = MkLabel($"{kb.name}:  [{kb.key}]", 15, UTKColor.TextPrimary);
                line.style.marginBottom = 4f;
                list.Add(line);
            }

            root.Add(MkDesc("  ※ 키 변경은 전용 키 설정 메뉴에서 가능합니다.", 12, UTKColor.TextSecondary));
        }

        // ─────────────────────────── 접근성 탭 (AccessibilityManager 실측 연동) ───────────────────────────
        private void BuildAccessibilityTab(VisualElement root)
        {
            root.style.paddingTop = 6f;
root.style.paddingBottom = 6f;
root.style.paddingLeft = 6f;
root.style.paddingRight = 6f;

            // ── 툴팁 지연 시간 (원본: 0~1.5s, 기본 0.3) ──
            root.Add(MkLabel("툴팁 지연 시간", 16, UTKColor.AccentRare));
            var tooltipHead = new VisualElement();
            tooltipHead.style.flexDirection = FlexDirection.Row;
            tooltipHead.style.alignItems = Align.Center;
            var tooltipVal = MkLabel($"{AccessibilityManager.TooltipDelay:F1}초", 15, UTKColor.GuildGreen);
            tooltipVal.style.width = 80f;
            tooltipVal.style.unityTextAlign = TextAnchor.MiddleRight;
            tooltipHead.Add(tooltipVal);
            _tooltipSlider = new Slider(0f, 1.5f) { value = AccessibilityManager.TooltipDelay };
            _tooltipSlider.name = "TooltipDelaySlider";
            _tooltipSlider.RegisterValueChangedCallback(evt =>
            {
                AccessibilityManager.SetTooltipDelay(evt.newValue);   // 즉시 저장
                tooltipVal.text = $"{evt.newValue:F1}초";
            });
            root.Add(tooltipHead);
            root.Add(_tooltipSlider);
            root.Add(MkDesc("  ※ 변경 즉시 저장", 12, UTKColor.TextSecondary));
            Span(root, 10f);

            // ── 색맹 모드 (원본: 켜짐/꺼짐 토글) ──
            root.Add(MkLabel("색맹 모드", 16, UTKColor.AccentRare));
            _colorBlindToggle = new Toggle("색맹 모드") { value = AccessibilityManager.ColorBlindMode };
            _colorBlindToggle.name = "ColorBlindToggle";
            _colorBlindDescLabel = MkDesc(_colorBlindToggle.value
                ? "✓ 빨간색/초록색 대신 패턴/아이콘으로 표시" : "  (꺼짐)", 12,
                _colorBlindToggle.value ? UTKColor.GuildGreen : UTKColor.TextSecondary);
            _colorBlindDescLabel.style.whiteSpace = WhiteSpace.Normal;
            _colorBlindToggle.RegisterValueChangedCallback(evt =>
            {
                AccessibilityManager.SetColorBlindMode(evt.newValue);   // 즉시 저장
                _colorBlindDescLabel.text = evt.newValue
                    ? "✓ 빨간색/초록색 대신 패턴/아이콘으로 표시\n✓ 등급 색상에 텍스트 라벨 추가"
                    : "  (꺼짐)";
                _colorBlindDescLabel.style.color = new StyleColor(
                    evt.newValue ? UTKColor.GuildGreen : UTKColor.TextSecondary);
                string prefixTest = AccessibilityManager.GetRarityPrefix("전설");
                Debug.Log("[SettingsUTK] 색맹 모드 → " + AccessibilityManager.ColorBlindMode + " (GetRarityPrefix 시험): " + prefixTest);
            });
            root.Add(_colorBlindToggle);
            root.Add(_colorBlindDescLabel);
            Span(root, 10f);

            // ── 자막 크기 (원본: 0.8~2.0x, 기본 1.0) ──
            root.Add(MkLabel("자막 크기", 16, UTKColor.AccentRare));
            var subtitleHead = new VisualElement();
            subtitleHead.style.flexDirection = FlexDirection.Row;
            subtitleHead.style.alignItems = Align.Center;
            var subtitleVal = MkLabel($"{AccessibilityManager.SubtitleScale:F1}x", 15, UTKColor.GuildGreen);
            subtitleVal.style.width = 60f;
            subtitleVal.style.unityTextAlign = TextAnchor.MiddleRight;
            subtitleHead.Add(subtitleVal);
            _subtitleSlider = new Slider(0.8f, 2.0f) { value = AccessibilityManager.SubtitleScale };
            _subtitleSlider.name = "SubtitleScaleSlider";
            _subtitleSlider.RegisterValueChangedCallback(evt =>
            {
                AccessibilityManager.SetSubtitleScale(evt.newValue);   // 즉시 저장
                subtitleVal.text = $"{evt.newValue:F1}x";
            });
            root.Add(subtitleHead);
            root.Add(_subtitleSlider);
            root.Add(MkDesc("  ※ 변경 즉시 저장", 12, UTKColor.TextSecondary));
        }

        // ─────────────────────────── 해상도 헬퍼 ───────────────────────────
        private void RefreshResolutionList()
        {
            _resolutionList = Screen.resolutions;
            if (_resolutionList == null || _resolutionList.Length == 0) return;
            _selectedResolutionIndex = Mathf.Clamp(_selectedResolutionIndex, 0, _resolutionList.Length - 1);

            var labels = new List<string>();
            foreach (var r in _resolutionList)
                labels.Add($"{r.width}x{r.height}@{r.refreshRateRatio.value:0}Hz");
            if (_resDrop != null)
            {
                _resDrop.choices = labels;
                if (_selectedResolutionIndex >= 0 && _selectedResolutionIndex < labels.Count)
                    _resDrop.index = _selectedResolutionIndex;
            }
        }

        private void ApplyCurrentResolution()
        {
            if (_resolutionList != null && _selectedResolutionIndex >= 0 && _selectedResolutionIndex < _resolutionList.Length)
            {
                var r = _resolutionList[_selectedResolutionIndex];
                Screen.SetResolution(r.width, r.height, Screen.fullScreen);
            }
        }

        private static List<string> QualityNameList()
        {
            var items = new List<string>();
            var names = QualitySettings.names;
            if (names == null || names.Length == 0) { items.Add("Default"); return items; }
            foreach (var n in names)
                items.Add(n);
            return items;
        }

        // ─────────────────────────── 생명주기 ───────────────────────────
        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            CenterOnParent();
            RefreshResolutionList();
            // 접근성 상태가 외부에서 바뀌었을 수 있으니 매 열림 시 동기화 (실측 API getter)
            if (_tooltipSlider != null) _tooltipSlider.value = AccessibilityManager.TooltipDelay;
            if (_colorBlindToggle != null) _colorBlindToggle.value = AccessibilityManager.ColorBlindMode;
            if (_subtitleSlider != null) _subtitleSlider.value = AccessibilityManager.SubtitleScale;
            Debug.Log("[SettingsUTK] 설정 메뉴 열림");
        }

        public override void Hide()
        {
            SavePrefs();
            base.Hide();
            Debug.Log("[SettingsUTK] 설정 메뉴 닫힘");
        }

        private void CenterOnParent()
        {
            var par = parent;
            if (par == null) return;
            float pw = par.resolvedStyle.width;
            float ph = par.resolvedStyle.height;
            if (pw <= 0f || ph <= 0f) return;
            style.left = Mathf.Max(50f, (pw - resolvedStyle.width) * 0.5f);
            style.top = Mathf.Max(50f, (ph - resolvedStyle.height) * 0.5f);
        }

        // ─────────────────────────── 내부 헬퍼 ───────────────────────────
        private static void Span(VisualElement parent, float h)
        {
            var s = new VisualElement();
            s.style.height = h;
            parent.Add(s);
        }

        private static Label MkLabel(string text, float size, Color color)
        {
            var l = new Label(text ?? "");
            l.style.fontSize = size;
            l.style.color = new StyleColor(color);
            return l;
        }

        private static Label MkDesc(string text, float size, Color color)
        {
            var l = MkLabel(text ?? "", size, color);
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.opacity = 0.85f;
            return l;
        }
    }
}