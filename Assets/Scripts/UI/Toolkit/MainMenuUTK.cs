using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U5 Round 1-C — 메인 메뉴 (UTK).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    /// 원본: Assets/Scripts/Systems/MainMenuUI.cs (774줄 IMGUI) — 원본은 절대 수정하지 않는다.
    ///
    /// [구현]
    ///  ① 전체화면 오버레이 — VisualElement + ShowToRoot 패턴 (UTKModal류 참고).
    ///  ② 버튼: 새 게임 / 이어하기 / 설정 / 종료 → 원본 콜백 실측 직접 호출.
    ///  ③ 새 게임 → 난이도 선택 뷰(쉬움/보통/어려움 + 배율표 + 확인/돌아가기) → 확정 시
    ///     GameManager.CurrentDifficulty 설정 + LoadingManager.LoadSceneAsync("MainScene").
    ///  ④ 이어하기 → LoadGameUTK.Open(). 설정 → "준비 중..." 메시지. 종료 → Application.Quit().
    ///  각 경로에 [MainMenuUTK] UnityEngine.Debug 로그.
    /// [진입점] static Open() / Close(). 전체화면 오버레이.
    /// </summary>
    public class MainMenuUTK : VisualElement
    {
        // ===== 싱글턴 / 팩토리 =====
        private static MainMenuUTK _instance;
        public static MainMenuUTK Instance => _instance;

        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new MainMenuUTK();
        }

        /// <summary>메인 메뉴 표시.</summary>
        public static void Open()
        {
            Ensure();
            _instance.ShowToRoot();
            _instance.ShowMainView();
            _instance.StartTicking();
            Debug.Log("[MainMenuUTK] 메인 메뉴 표시");
        }

        /// <summary>메인 메뉴 숨김.</summary>
        public static void Close()
        {
            if (_instance == null) return;
            _instance.StopTicking();
            _instance.RemoveFromHierarchy();
            Debug.Log("[MainMenuUTK] 메인 메뉴 닫힘");
        }

        // ===== 레이아웃 =====
        private const float WinW = 400f;
        private const float WinH = 460f;
        private const float BtnW = 280f;
        private const float BtnH = 60f;
        private const float BtnSpacing = 20f;
        private const long TickMs = 400L;

        // ===== 상태 =====
        private DifficultyMode _selected = DifficultyMode.Normal;
        private string _settingsMsg = "";
        private float _settingsMsgTimer = 0f;

        // ===== 레퍼런스 =====
        private VisualElement _menuView;
        private VisualElement _difficultyView;
        private Label _menuMsgLabel;
        private Label[] _difficultyButtons;
        private IVisualElementScheduledItem _tickTask;

        private MainMenuUTK()
        {
            name = "MainMenuUTK";
            AddToClassList("utk-window");
            pickingMode = PickingMode.Position;

            BuildMainView();

            _difficultyView = new VisualElement();
            _difficultyView.name = "DifficultyView";
            Add(_difficultyView);

            _difficultyView.style.display = DisplayStyle.None;

            UTKWindowBase.ApplyUIToolkitFont(this);
            style.display = DisplayStyle.None;
        }

        // ===== 뷰 구성 =====

        private void BuildMainView()
        {
            _menuView = new VisualElement();
            _menuView.name = "MainView";
            _menuView.style.alignItems = Align.Center;
            _menuView.style.justifyContent = Justify.Center;

            var title = new Label("Korea 1420");
            title.style.fontSize = 34f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new StyleColor(new Color(0.9f, 0.7f, 0.3f, 1f));
            _menuView.Add(title);

            var subtitle = new Label("— 조선 —");
            subtitle.style.color = new StyleColor(UTKColor.TextSecondary);
            subtitle.style.marginTop = 4f;
            subtitle.style.marginBottom = 18f;
            _menuView.Add(subtitle);

            _menuView.Add(UTKButton.Create("🆕 새 게임", OnNewGameClicked, UTKButton.Variant.Primary));
            _menuView.Add(UTKButton.Create("📂 이어하기", OnContinueClicked, UTKButton.Variant.Secondary));
            _menuView.Add(UTKButton.Create("⚙ 설정", OnSettingsClicked, UTKButton.Variant.Secondary));
            _menuView.Add(UTKButton.Create("❌ 종료", OnQuitClicked, UTKButton.Variant.Danger));

            _menuMsgLabel = new Label("");
            _menuMsgLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            _menuMsgLabel.style.marginTop = 8f;
            _menuView.Add(_menuMsgLabel);

            Add(_menuView);
        }

        private void BuildDifficultyView()
        {
            _difficultyView.Clear();
            _difficultyView.style.alignItems = Align.Center;
            _difficultyView.style.justifyContent = Justify.Center;

            var title = new Label("🎯 난이도 선택");
            title.style.fontSize = 26f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new StyleColor(new Color(0.9f, 0.7f, 0.3f, 1f));
            _difficultyView.Add(title);

            string hint = GetSavedDifficultyHint();
            if (!string.IsNullOrEmpty(hint))
            {
                var hintLabel = new Label(hint);
                hintLabel.style.color = new StyleColor(UTKColor.TextSecondary);
                hintLabel.style.marginTop = 6f;
                hintLabel.style.marginBottom = 10f;
                _difficultyView.Add(hintLabel);
            }
            else
            {
                var spacer = new VisualElement();
                spacer.style.height = 24f;
                _difficultyView.Add(spacer);
            }

            _difficultyButtons = new Label[3];
            BuildDifficultyButton("🟢 쉬움", DifficultyMode.Easy, new Color(0.3f, 0.9f, 0.3f), 0);
            BuildDifficultyButton("🟡 보통", DifficultyMode.Normal, new Color(0.9f, 0.9f, 0.2f), 1);
            BuildDifficultyButton("🔴 어려움", DifficultyMode.Hard, new Color(0.9f, 0.2f, 0.2f), 2);

            // 배율표 (원본 DrawMultiplierRow 실측)
            string[] rows = { "적 체력", "적 데미지", "드랍률", "리스폰" };
            foreach (string rowName in rows)
            {
                float easy = GetMultiplier(rowName, DifficultyMode.Easy);
                float normal = GetMultiplier(rowName, DifficultyMode.Normal);
                float hard = GetMultiplier(rowName, DifficultyMode.Hard);
                var row = new Label($"{rowName}   ×{easy:F1}   ×{normal:F1}   ×{hard:F1}");
                row.style.color = new StyleColor(UTKColor.TextSecondary);
                row.style.fontSize = 13f;
                row.style.unityTextAlign = TextAnchor.MiddleCenter;
                _difficultyView.Add(row);
            }

            var warn = new Label("⚠ 게임 중에는 변경할 수 없습니다");
            warn.style.color = new StyleColor(new Color(1f, 0.7f, 0.2f, 1f));
            warn.style.fontSize = 12f;
            warn.style.marginTop = 6f;
            _difficultyView.Add(warn);

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.marginTop = 16f;
            _difficultyView.Add(btnRow);

            btnRow.Add(UTKButton.Create("✅ 확인", OnDifficultyConfirmed, UTKButton.Variant.Primary));
            btnRow.Add(UTKButton.Create("↩ 돌아가기", OnDifficultyBack, UTKButton.Variant.Secondary));
        }

        private void BuildDifficultyButton(string label, DifficultyMode mode, Color baseColor, int index)
        {
            var btn = new Label(label);
            btn.name = "DiffBtn_" + index;
            btn.style.width = 160f;
            btn.style.height = 44f;
            btn.style.fontSize = 17f;
            btn.style.unityTextAlign = TextAnchor.MiddleCenter;
            btn.style.backgroundColor = new StyleColor(new Color(baseColor.r * 0.5f, baseColor.g * 0.5f, baseColor.b * 0.5f, 0.9f));
            btn.style.marginBottom = 6f;
            btn.style.unityTextOutlineWidth = 1f;
            btn.RegisterCallback<PointerDownEvent>(_ => { _selected = mode; RefreshDifficultySelection(); });
            _difficultyView.Add(btn);
            _difficultyButtons[index] = btn;
        }

        // ===== 난이도 =====

        private float GetMultiplier(string row, DifficultyMode diff)
        {
            switch (row)
            {
                case "적 체력":   return DifficultyManager.GetHpMultiplier(diff);
                case "적 데미지": return DifficultyManager.GetDamageMultiplier(diff);
                case "드랍률":    return DifficultyManager.GetDropRateMultiplier(diff);
                default:          return DifficultyManager.GetRespawnRateMultiplier(diff);
            }
        }

        private void RefreshDifficultySelection()
        {
            for (int i = 0; i < _difficultyButtons.Length; i++)
            {
                var btn = _difficultyButtons[i];
                if (btn == null) continue;
                bool isSel = (int)_selected == i;
                btn.style.unityTextOutlineColor = isSel
                    ? new StyleColor(UTKColor.HoverGold)
                    : new StyleColor(Color.clear);
            }
        }

        private string GetSavedDifficultyHint()
        {
            if (SaveManager.Instance == null) return null;
            SaveData[] infos = SaveManager.Instance.GetAllSlotInfos();
            if (infos == null) return null;
            SaveData mostRecent = null;
            string newest = "";
            foreach (SaveData info in infos)
            {
                if (info != null && string.CompareOrdinal(info.timestamp, newest) > 0)
                {
                    mostRecent = info;
                    newest = info.timestamp;
                }
            }
            if (mostRecent == null) return null;
            return $"저장된 난이도: {GetDifficultyDisplayName(mostRecent.difficulty)}";
        }

        private string GetDifficultyDisplayName(DifficultyMode mode)
        {
            switch (mode)
            {
                case DifficultyMode.Easy: return "🟢 쉬움";
                case DifficultyMode.Hard: return "🔴 어려움";
                default: return "🟡 보통";
            }
        }

        // ===== 버튼 콜백 (원본 실측 직접 호출) =====

        private void OnNewGameClicked()
        {
            Debug.Log("[MainMenuUTK] 새 게임 → 난이도 선택 화면 전환");
            _selected = DifficultyMode.Normal;
            BuildDifficultyView();
            RefreshDifficultySelection();
            _menuView.style.display = DisplayStyle.None;
            _difficultyView.style.display = DisplayStyle.Flex;
        }

        private void OnContinueClicked()
        {
            Debug.Log("[MainMenuUTK] 이어하기 → 불러오기 화면 전환");
            LoadGameUTK.Open();
        }

        private void OnSettingsClicked()
        {
            _settingsMsg = "준비 중...";
            _settingsMsgTimer = 2.0f;
            Debug.Log("[MainMenuUTK] 설정: 준비 중 (placeholder)");
        }

        private void OnQuitClicked()
        {
            Debug.Log("[MainMenuUTK] 종료");
            Application.Quit();
        }

        private void OnDifficultyConfirmed()
        {
            Debug.Log($"[MainMenuUTK] 난이도 확정: {_selected}");
            if (GameManager.Instance != null)
                GameManager.CurrentDifficulty = (int)_selected;
            else
                Debug.LogError("[MainMenuUTK] GameManager.Instance가 null입니다. 난이도를 설정할 수 없습니다.");

            if (LoadingManager.Instance != null)
            {
                Close();
                LoadingManager.Instance.LoadSceneAsync("MainScene");
            }
            else
            {
                Debug.LogError("[MainMenuUTK] LoadingManager.Instance가 null입니다.");
            }
        }

        private void OnDifficultyBack()
        {
            Debug.Log("[MainMenuUTK] 난이도 선택 취소, 메인 메뉴 복귀");
            _difficultyView.style.display = DisplayStyle.None;
            ShowMainView();
        }

        private void ShowMainView()
        {
            if (_menuView != null)
                _menuView.style.display = DisplayStyle.Flex;
            if (_difficultyView != null)
                _difficultyView.style.display = DisplayStyle.None;
            _settingsMsg = "";
            _settingsMsgTimer = 0f;
            if (_menuMsgLabel != null)
                _menuMsgLabel.text = "";
        }

        // ===== 폴링 (설정 메시지 타이머 — 원본 Update 로직) =====

        private void StartTicking()
        {
            if (_tickTask != null) return;
            _tickTask = schedule.Execute(() =>
            {
                if (parent == null) return;
                if (_settingsMsgTimer > 0f)
                {
                    _settingsMsgTimer -= TickMs / 1000f;
                    if (_settingsMsgTimer <= 0f)
                        _settingsMsg = "";
                }
                if (_menuMsgLabel != null)
                    _menuMsgLabel.text = _settingsMsg;
            }).Every(TickMs);
        }

        private void StopTicking()
        {
            if (_tickTask != null)
            {
                _tickTask.Pause();
                _tickTask = null;
            }
        }

        // ===== 전체화면 오버레이 (ShowToRoot 패턴) =====

        private void ShowToRoot()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null)
            {
                Debug.LogWarning("[MainMenuUTK] UIRoot 없음 — 오버레이 표시 불가");
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