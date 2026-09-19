// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 114
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.UI;   // UIFont

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U5 Round 1-B — 옵션 창 (게임 내 ESC 설정).
    /// 원본: Assets/Scripts/UI/OptionsUI.cs (714줄, IMGUI) — 본 파일은 그 데이터 경로만 이식한
    /// UTKWindowBase 파생. 원본은 절대 수정하지 않는다.
    ///
    /// [역할 분담 — 실측]
    ///  - OptionsUI(→OptionsUTK): 게임 내 설정 (그래픽/오디오/키 설정). 게임 플레이 중 접근.
    ///  - SettingsMenuUI(→SettingsMenuUTK): 독립 설정 메뉴(접근성 탭 포함). ESC 메뉴의 "설정" 버튼.
    ///  두 창은 동일 PlayerPrefs 키(Settings_*)를 그대로 사용 — 데이터 경로 단일화, 복제 금지.
    ///
    /// [컨트롤] UTK 기본 컨트롤: DropdownField(품질/해상도), Slider(볼륨), Toggle(전체화면).
    /// [저장] 원본 SaveSettings()의 PlayerPrefs 키 그대로 즉시 저장 + PlayerPrefs.Save().
    /// [엔진 규약] foreach만 / UnityEngine.Debug / IStyle 4면 개별 / 폴링 schedule.Pause 정지.
    /// </summary>
    public class OptionsUTK : UTKWindowBase
    {
        // ─────────────────────────── 싱글턴 / 팩토리 ───────────────────────────
        private static OptionsUTK _instance;
        public static OptionsUTK Instance => _instance;

        /// <summary>팩토리 — 멱등 생성.</summary>
        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new OptionsUTK();
        }

        /// <summary>열기 (게임 내 설정).</summary>
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

        /// <summary>전체화면 상태가 바뀌면 토글 UI 동기화를 위한 접근 (원본 Screen.fullScreen 연동).</summary>
        public static void NotifyExternalFullscreenChanged()
        {
            if (_instance != null && _instance._fsToggle != null)
                _instance._fsToggle.value = Screen.fullScreen;
        }

        // ─────────────────────────── 설정 ───────────────────────────
        private const float WinW = 720f;
        private const float WinH = 560f;

        private enum Tab { Graphics, Audio, KeyBindings }
        private Tab _currentTab = Tab.Graphics;

        // 그래픽 상태 (원본 실측 PlayerPrefs 키)
        private int _qualityLevel = 2;
        private int _resolutionIndex;
        private Resolution[] _resolutions;

        // 오디오 상태 (원본 실측 기본값)
        private float _bgmVolume = 0.8f;
        private float _sfxVolume = 0.8f;
        private float _uiVolume = 0.7f;
        private float _ambientVolume = 0.6f;

        // 티어별 컨테이너 (탭 전환 시 display 토글)
        private readonly VisualElement _graphicsRoot;
        private readonly VisualElement _audioRoot;
        private readonly VisualElement _keysRoot;
        private DropdownField _qualityDrop, _resDrop;
        private Toggle _fsToggle;

        private OptionsUTK() : base("⚙ 옵션", new Vector2(WinW, WinH))
        {
            LoadFromPrefs();

            _content.style.flexDirection = FlexDirection.Column;
            _content.style.flexGrow = 1f;

            BuildTabBar();
            _graphicsRoot = new VisualElement { name = "GraphicsTab" };
            _audioRoot = new VisualElement { name = "AudioTab" };
            _keysRoot = new VisualElement { name = "KeysTab" };
            _content.Add(_graphicsRoot);
            _content.Add(_audioRoot);
            _content.Add(_keysRoot);

            BuildGraphicsTab(_graphicsRoot);
            BuildAudioTab(_audioRoot);
            BuildKeyBindingsTab(_keysRoot);

            RefreshResolutionLabels();

            ApplyUIToolkitFont(this);
            SelectTab(Tab.Graphics);
            style.display = DisplayStyle.None;
            style.left = 120f;
            style.top = 80f;
        }

        // ─────────────────────────── 저장 로드 ───────────────────────────
        private void LoadFromPrefs()
        {
            _qualityLevel = PlayerPrefs.GetInt("Settings_Quality", QualitySettings.GetQualityLevel());
            _resolutionIndex = PlayerPrefs.GetInt("Settings_Resolution", 0);
            _bgmVolume = PlayerPrefs.GetFloat("Settings_BGM", 0.8f);
            _sfxVolume = PlayerPrefs.GetFloat("Settings_SFX", 0.8f);
            _uiVolume = PlayerPrefs.GetFloat("Settings_UI", 0.7f);
            _ambientVolume = PlayerPrefs.GetFloat("Settings_Ambient", 0.6f);

            int maxQ = QualitySettings.names != null && QualitySettings.names.Length > 0
                ? QualitySettings.names.Length - 1 : 0;
            _qualityLevel = Mathf.Clamp(_qualityLevel, 0, maxQ);
        }

        /// <summary>원본 SaveSettings() 키 그대로 — 두 창 공용 저장 경로 (SettingsMenuUTK와 공유).</summary>
        private void SavePrefs()
        {
            PlayerPrefs.SetInt("Settings_Quality", _qualityLevel);
            PlayerPrefs.SetInt("Settings_Resolution", _resolutionIndex);
            PlayerPrefs.SetFloat("Settings_BGM", _bgmVolume);
            PlayerPrefs.SetFloat("Settings_SFX", _sfxVolume);
            PlayerPrefs.SetFloat("Settings_UI", _uiVolume);
            PlayerPrefs.SetFloat("Settings_Ambient", _ambientVolume);
            PlayerPrefs.Save();
            QualitySettings.SetQualityLevel(_qualityLevel, true);
            Debug.Log("[OptionsUTK] 설정 저장 완료");
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
            Debug.Log($"[OptionsUTK] 탭 전환 → {tab}");
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
            root.Add(MkDesc("  (* 변경 즉시 저장 + QualitySettings.SetQualityLevel)", 12, UTKColor.GuildGreen));

            Span(root, 10f);

            root.Add(MkLabel("해상도", 16, UTKColor.AccentRare));
            _resDrop = new DropdownField("해상도", new List<string>(), 0);
            _resDrop.name = "ResolutionDropdown";
            _resDrop.style.width = 300f;
            _resDrop.RegisterValueChangedCallback(_ =>
            {
                _resolutionIndex = _resDrop.index;
                ApplyCurrentResolution();
                SavePrefs();
            });
            root.Add(_resDrop);

            Span(root, 10f);

            _fsToggle = new Toggle("전체 화면") { value = Screen.fullScreen };
            _fsToggle.name = "FullscreenToggle";
            _fsToggle.RegisterValueChangedCallback(evt =>
            {
                Screen.fullScreen = evt.newValue;
                if (_resolutions != null && _resolutionIndex >= 0 && _resolutionIndex < _resolutions.Length)
                {
                    var r = _resolutions[_resolutionIndex];
                    Screen.SetResolution(r.width, r.height, evt.newValue);
                }
            });
            root.Add(_fsToggle);

            Span(root, 14f);

            var applyBtn = UTKButton.Create("적용 / 저장",
                () => { SavePrefs(); UTKToastService.Show("✅ 설정이 저장되었습니다."); },
                UTKButton.Variant.Primary);
            applyBtn.style.width = 180f;
            root.Add(applyBtn);
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

            root.Add(MkDesc("  ※ 변경사항은 즉시 저장됩니다.", 12, UTKColor.TextSecondary));
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
            root.Add(MkLabel("키 설정 (읽기 전용 — 변경은 게임 외 설정 메뉴)", 15, UTKColor.AccentRare));

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

        // ─────────────────────────── 해상도 헬퍼 ───────────────────────────
        private void RefreshResolutionLabels()
        {
            _resolutions = Screen.resolutions;
            if (_resolutions == null || _resolutions.Length == 0) return;

            _resolutionIndex = Mathf.Clamp(_resolutionIndex, 0, _resolutions.Length - 1);

            var labels = new List<string>();
            foreach (var r in _resolutions)
                labels.Add($"{r.width}x{r.height}@{r.refreshRateRatio.value:0}Hz");
            if (_resDrop != null)
            {
                _resDrop.choices = labels;
                if (_resolutionIndex >= 0 && _resolutionIndex < labels.Count)
                    _resDrop.index = _resolutionIndex;
            }
        }

        private void ApplyCurrentResolution()
        {
            if (_resolutions != null && _resolutionIndex >= 0 && _resolutionIndex < _resolutions.Length)
            {
                var r = _resolutions[_resolutionIndex];
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
            RefreshResolutionLabels();
            Debug.Log("[OptionsUTK] 옵션 창 열림");
        }

        public override void Hide()
        {
            SavePrefs();
            base.Hide();
            Debug.Log("[OptionsUTK] 옵션 창 닫힘");
        }

        private void CenterOnParent()
        {
            var par = parent;
            if (par == null) return;
            float pw = par.resolvedStyle.width;
            float ph = par.resolvedStyle.height;
            if (pw <= 0f || ph <= 0f) return;
            style.left = Mathf.Max(40f, (pw - resolvedStyle.width) * 0.5f);
            style.top = Mathf.Max(40f, (ph - resolvedStyle.height) * 0.5f);
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