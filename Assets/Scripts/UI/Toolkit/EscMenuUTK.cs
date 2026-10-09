using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U5 Round 1-C — ESC 메뉴 (일시정지) (UTK).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    /// 원본: Assets/Scripts/UI/EscMenuUI.cs (143줄 IMGUI) — 원본은 절대 수정하지 않는다.
    ///
    /// [구현]
    ///  ① ESC 입력 → Time.timeScale=0 토글 (원본 Toggle/Open/Resume 실측).
    ///  ② 버튼: 계속 / 설정 / 메인으로 → 원본 콜백 직접 호출.
    ///  ③ static Toggle() — IsOpen 분기 필수 (CS0176 방지).
    ///  각 경로에 [EscUTK] UnityEngine.Debug 로그.
    /// [진입점] static Ensure() / Toggle() / Open(). 전체화면 오버레이.
    /// </summary>
    public class EscMenuUTK : VisualElement
    {
        // ===== 싱글턴 / 팩토리 =====
        private static EscMenuUTK _instance;
        public static EscMenuUTK Instance => _instance;

        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new EscMenuUTK();
        }

        /// <summary>ESC 토글 — IsOpen 분기 필수.</summary>
        public static void Toggle()
        {
            Ensure();
            if (_instance.IsOpen) { _instance.Resume(); return; }
            Open();
        }

        /// <summary>열기 — timeScale=0, 오버레이 표시.</summary>
        public static void Open()
        {
            Ensure();
            _instance.SetOpen(true);
        }

        /// <summary>닫기 — timeScale 복원.</summary>
        public static void Close()
        {
            if (_instance != null) _instance.Resume();
        }

        public bool IsOpen { get; private set; }

        // ===== 레이아웃 =====
        private const float WinW = 600f;

        // ===== 레퍼런스 =====
        private VisualElement _menuView;

        private EscMenuUTK()
        {
            name = "EscMenuUTK";
            AddToClassList("utk-window");
            pickingMode = PickingMode.Position;
            style.display = DisplayStyle.None;

            _menuView = new VisualElement();
            _menuView.name = "EscMenuView";
            _menuView.style.alignItems = Align.Center;
            _menuView.style.width = WinW;
            _menuView.style.paddingLeft = 28f;
            _menuView.style.paddingRight = 28f;
            _menuView.style.paddingTop = 28f;
            _menuView.style.paddingBottom = 28f;
            _menuView.style.backgroundColor = new StyleColor(UTKColor.BgPanel);
            _menuView.style.borderTopWidth = 1f;
            _menuView.style.borderBottomWidth = 1f;
            _menuView.style.borderLeftWidth = 1f;
            _menuView.style.borderRightWidth = 1f;
            _menuView.style.borderTopColor = new StyleColor(UTKColor.IronLine);
            _menuView.style.borderBottomColor = new StyleColor(UTKColor.IronLine);
            _menuView.style.borderLeftColor = new StyleColor(UTKColor.IronLine);
            _menuView.style.borderRightColor = new StyleColor(UTKColor.IronLine);
            _menuView.style.borderTopLeftRadius = 8f;
            _menuView.style.borderTopRightRadius = 8f;
            _menuView.style.borderBottomLeftRadius = 8f;
            _menuView.style.borderBottomRightRadius = 8f;

            var title = new Label("일시정지");
            title.style.fontSize = 31.2f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new StyleColor(UTKColor.AccentRare);
            title.style.marginBottom = 8f;
            _menuView.Add(title);

            var subtitle = new Label("게임을 잠시 멈췄습니다");
            subtitle.style.fontSize = 14.4f;
            subtitle.style.color = new StyleColor(UTKColor.TextSecondary);
            subtitle.style.marginBottom = 22f;
            _menuView.Add(subtitle);

            var resumeButton = UTKButton.Create("▶ 계속", OnResumeClicked, UTKButton.Variant.Primary);
            var settingsButton = UTKButton.Create("⚙ 설정", OnSettingsClicked, UTKButton.Variant.Secondary);
            var mainMenuButton = UTKButton.Create("🏠 메인으로", OnMainMenuClicked, UTKButton.Variant.Danger);
            ConfigureMenuCard(resumeButton);
            ConfigureMenuCard(settingsButton);
            ConfigureMenuCard(mainMenuButton);
            _menuView.Add(resumeButton);
            _menuView.Add(settingsButton);
            _menuView.Add(mainMenuButton);

            Add(_menuView);
            UTKWindowBase.ApplyUIToolkitFont(this);
        }

        private static void ConfigureMenuCard(Button button)
        {
            button.style.width = Length.Percent(100f);
            button.style.minHeight = 58f;
            button.style.marginBottom = 10f;
            button.style.paddingLeft = 18f;
            button.style.paddingRight = 18f;
            button.style.backgroundColor = new StyleColor(new Color(0x21 / 255f, 0x26 / 255f, 0x2D / 255f, 1f));
            button.style.color = new StyleColor(UTKColor.TextPrimary);
            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            button.style.borderTopWidth = 1f;
            button.style.borderBottomWidth = 1f;
            button.style.borderLeftWidth = 1f;
            button.style.borderRightWidth = 1f;
            button.style.borderTopColor = new StyleColor(new Color(0x2E / 255f, 0x34 / 255f, 0x3D / 255f, 1f));
            button.style.borderBottomColor = new StyleColor(new Color(0x2E / 255f, 0x34 / 255f, 0x3D / 255f, 1f));
            button.style.borderLeftColor = new StyleColor(new Color(0x2E / 255f, 0x34 / 255f, 0x3D / 255f, 1f));
            button.style.borderRightColor = new StyleColor(new Color(0x2E / 255f, 0x34 / 255f, 0x3D / 255f, 1f));
            button.style.borderTopLeftRadius = 8f;
            button.style.borderTopRightRadius = 8f;
            button.style.borderBottomLeftRadius = 8f;
            button.style.borderBottomRightRadius = 8f;

            button.RegisterCallback<MouseEnterEvent>(_ =>
            {
                button.style.borderTopColor = new StyleColor(UTKColor.HoverGold);
                button.style.borderBottomColor = new StyleColor(UTKColor.HoverGold);
                button.style.borderLeftColor = new StyleColor(UTKColor.HoverGold);
                button.style.borderRightColor = new StyleColor(UTKColor.HoverGold);
            });
            button.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                var stroke = new StyleColor(new Color(0x2E / 255f, 0x34 / 255f, 0x3D / 255f, 1f));
                button.style.borderTopColor = stroke;
                button.style.borderBottomColor = stroke;
                button.style.borderLeftColor = stroke;
                button.style.borderRightColor = stroke;
            });
        }

        // ===== 버튼 콜백 (원본 실측 직접 호출) =====

        private void OnResumeClicked()
        {
            Debug.Log("[EscUTK] 계속 (재개)");
            Resume();
        }

        private void OnSettingsClicked()
        {
            Debug.Log("[EscUTK] 설정 열기");
            // 원본: Resume(); SettingsMenuUI.Instance?.Show();
            Resume();
            SettingsMenuUI.Instance?.Show();
            if (SettingsMenuUI.Instance == null)
                Debug.LogWarning("[EscUTK] SettingsMenuUI 인스턴스 없음 — 설정 미표시");
        }

        private void OnMainMenuClicked()
        {
            Debug.Log("[EscUTK] 메인 메뉴로 이동");
            // 원본: Time.timeScale=1f; SceneManager.LoadScene("MainMenu");
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        // ===== 열림/닫힘 (timeScale 제어) =====

        private void SetOpen(bool open)
        {
            IsOpen = open;
            Time.timeScale = open ? 0f : 1f;
            if (open)
                ShowToRoot();
            else
                RemoveFromHierarchy();
            Debug.Log($"[EscUTK] {(open ? "열림" : "닫힘")} — timeScale={Time.timeScale}");
        }

        private void Resume()
        {
            if (!IsOpen) return;
            SetOpen(false);
        }

        // ===== 전체화면 오버레이 (ShowToRoot 패턴) =====

        private void ShowToRoot()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null)
            {
                Debug.LogWarning("[EscUTK] UIRoot 없음 — 오버레이 표시 불가");
                return;
            }
            style.display = DisplayStyle.Flex;
            style.position = Position.Absolute;
            style.left = 0f;
            style.right = 0f;
            style.top = 0f;
            style.bottom = 0f;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;
            if (parent == null)
                root.Add(this);
            BringToFront();
        }
    }
}