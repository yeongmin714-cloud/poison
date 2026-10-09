// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
// U-G (2026-10-01): FishingUTK Figma fishing-hud-panel(160:11) 2바 리일 재구현 — 진행(물고기 당기는 중)+장력(라인/위험).
//   Figma 280×289·pad20/gap20: TitleRow→Line→ProgressSection(라벨+% + progress-bar)→TensionSection(라벨+위험 + tension-bar)→Line→ActionGrid(keycap-SPACE 잡기 / keycap-Q 취소).
//   시스템(FishingSystem.TryReel/ReelProgress/ReelTension)은 Figma 2바 시뮬로 재구성됨. 레거시 1바 핀(핀·스위트스팟·프레임 베이크)는 제거.
//   입력: SPACE=TryReel, Q=취소, ESC=취소.
#pragma warning disable 114
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Systems;   // FishingSystem

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit — 낚시 미니게임 (UTKWindowBase 파생).
    /// Figma fishing-hud-panel(160:11)을 GitHub-dark로 재현 — 2바(진행/장력) 리일.
    /// 상태/판정은 모두 FishingSystem가 소유. 원본 FishingUI.cs(IMGUI)는 절대 수정하지 않는다.
    /// </summary>
    public class FishingUTK : UTKWindowBase
    {
        private static FishingUTK _instance;
        private static Updater _updater;

        private const float WinW = 336f;   // Archived 160:11 fishing-hud-panel
        private const float WinH = 345.6f;
        private const long TickMs = 50L;

        // 바 (Figma 240×8 두께)
        private readonly VisualElement _progressFill;
        private readonly Label _progressPct;
        private readonly VisualElement _tensionFill;
        private readonly Label _tensionState;
        private readonly Label _hintLabel;
        private readonly Label _popupLabel;
        private readonly VisualElement _popupHost;
        private readonly VisualElement _bite;   // 입질 대기 물결 찌 (P29 유지)
        private IVisualElementScheduledItem _tickTask;

        public static FishingUTK Instance => _instance;

        private FishingUTK() : base("🎣 낚시", new Vector2(WinW, WinH))
        {
            SetChrome(UTKWindowChrome.Frameless);
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;
            _content.style.paddingLeft = 24f; _content.style.paddingRight = 24f;
            _content.style.paddingTop = 24f; _content.style.paddingBottom = 24f;

            _popupHost = new VisualElement();
            _popupHost.style.position = Position.Absolute;
            _popupHost.style.left = 0; _popupHost.style.right = 0; _popupHost.style.top = 0; _popupHost.style.bottom = 0;
            _popupHost.style.alignItems = Align.Center;
            _popupHost.style.justifyContent = Justify.Center;
            _popupHost.style.display = DisplayStyle.None;
            _popupHost.pickingMode = PickingMode.Ignore;
            _content.Add(_popupHost);

            _popupLabel = new Label("");
            _popupLabel.style.fontSize = 16.8f;
            _popupLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _popupLabel.style.color = new StyleColor(GitHubDark.Accent);
            _popupLabel.style.whiteSpace = WhiteSpace.Normal;
            _popupLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _popupHost.Add(_popupLabel);

            // ── TitleRow: 🎣 낚시하기 + ACTIVE ── [Figma 160:24]
            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            var titleIcon = new Label("🎣");
            titleIcon.style.fontSize = 14.4f;
            titleRow.Add(titleIcon);
            var titleLbl = new Label("낚시하기");
            titleLbl.style.marginLeft = 4f;
            titleLbl.style.fontSize = 14.4f;
            titleLbl.style.color = new StyleColor(GitHubDark.TextMain);
            titleLbl.style.flexGrow = 1f;
            titleRow.Add(titleLbl);
            _tensionState = new Label("ACTIVE");   // 원래 장력 상태 배지 자리(상단) — 대기 시에도 상태 표시
            _tensionState.style.fontSize = 12f;
            _tensionState.style.color = new StyleColor(GitHubDark.Accent);
            titleRow.Add(_tensionState);
            _content.Add(titleRow);

            // ── 구분선 ── [Figma 160:29]
            var sep1 = new VisualElement();
            sep1.style.height = 1f;
            sep1.style.marginTop = 2f;
            sep1.style.backgroundColor = new StyleColor(GitHubDark.Stroke);
            _content.Add(sep1);

            // ── ProgressSection: 물고기를 당기는 중 + 65% + progress-bar ── [Figma 160:30]
            MakeSectionRow("물고기를 당기는 중...", out _progressPct);
            _progressFill = BuildBar(new Color32(0x58, 0xA6, 0xFF, 0xFF));   // 진행 = 액센트 파랑

            // ── TensionSection: 라인 장력 (Tension) + 위험! + tension-bar ── [Figma 160:36]
            var tensionLabel = new VisualElement();
            tensionLabel.style.flexDirection = FlexDirection.Row;
            tensionLabel.style.alignItems = Align.Center;
            _content.Add(tensionLabel);
            var tLabel = new Label("라인 장력 (Tension)");
            tLabel.style.fontSize = 13.2f;
            tLabel.style.color = new StyleColor(GitHubDark.TextSub);
            tLabel.style.flexGrow = 1f;
            tensionLabel.Add(tLabel);
            var tDanger = new Label("위험!");
            tDanger.style.fontSize = 13.2f;
            tDanger.style.color = new StyleColor(GitHubDark.Danger);
            tDanger.visible = false;
            tensionLabel.Add(tDanger);
            _tensionFill = BuildBar(new Color32(0x3F, 0xB9, 0x50, 0xFF));   // 장력 = 안전 녹색 → 위험 시 담당자가 빨강

            // ── 구분선 ── [Figma 160:42]
            var sep2 = new VisualElement();
            sep2.style.height = 1f;
            sep2.style.marginTop = 2f;
            sep2.style.backgroundColor = new StyleColor(GitHubDark.Stroke);
            _content.Add(sep2);

            // ── ActionGrid: SPACE 잡기 / Q 취소 ── [Figma 160:43]
            var actionGrid = new VisualElement();
            actionGrid.style.flexDirection = FlexDirection.Column;
            actionGrid.style.marginTop = 4f;
            _hintLabel = new Label("");
            _hintLabel.style.fontSize = 12f;
            _hintLabel.style.color = new StyleColor(GitHubDark.TextSub);
            _hintLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            _hintLabel.style.whiteSpace = WhiteSpace.Normal;
            actionGrid.Add(_hintLabel);
            _content.Add(actionGrid);

            // 대기 중 물결 찌 (P29 고품질화 — 입질 대기 시 표시)
            _bite = new VisualElement();
            _bite.style.position = Position.Absolute;
            _bite.style.alignSelf = Align.Center;
            _bite.style.left = 24f; _bite.style.top = 4f;
            _bite.style.width = 48f; _bite.style.height = 36f;
            _bite.AddToClassList("utk-slot");
            _bite.style.backgroundImage = UTKTextureSafe.ToBackground(Resources.Load<Texture2D>("UI/FishingBite"));
            _bite.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            _content.Add(_bite);

            ApplyUIToolkitFont(this);
            style.display = DisplayStyle.None;
            ApplyCanvasBounds();
        }

        private void ApplyCanvasBounds()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return;
            var frameBounds = new Rect(792f, 367.2f, 336f, 345.6f);
            FigmaCanvasLayout.Apply(this, frameBounds, root);

            Vector2 rootSize = new Vector2(root.resolvedStyle.width, root.resolvedStyle.height);
            float sx = rootSize.x / FigmaCanvasLayout.CanvasWidth;
            float sy = rootSize.y / FigmaCanvasLayout.CanvasHeight;
            _content.style.position = Position.Absolute;
            _content.style.left = 0f;
            _content.style.top = 0f;
            // Keep the content tree in canonical Figma units and scale it as one visual subtree.
            // This scales typography, bars, spacing and keycaps consistently on both axes while
            // preserving the outer frame bounds and leaving the 1920×1080 layout unchanged.
            _content.style.width = frameBounds.width;
            _content.style.height = frameBounds.height;
            _content.style.paddingLeft = 20f;
            _content.style.paddingRight = 20f;
            _content.style.paddingTop = 20f;
            _content.style.paddingBottom = 20f;
            _content.style.transformOrigin = new TransformOrigin(0f, 0f, 0f);
            _content.style.scale = new StyleScale(new Scale(new Vector2(sx, sy)));
        }

        /// <summary>[Figma] 섹션 라벨 행(라벨 + 우측 % ). </summary>
        private VisualElement MakeSectionRow(string labelText, out Label pctLabel)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            var label = new Label(labelText);
            label.style.fontSize = 13.2f;
            label.style.color = new StyleColor(GitHubDark.TextSub);
            label.style.flexGrow = 1f;
            row.Add(label);
            pctLabel = new Label("0%");
            pctLabel.style.fontSize = 13.2f;
            pctLabel.style.color = new StyleColor(GitHubDark.TextMain);
            row.Add(pctLabel);
            _content.Add(row);
            return row;
        }

        /// <summary>[Figma 160:34/40] 240×8 바 트랙 + 채움.</summary>
        private VisualElement BuildBar(Color fillColor)
        {
            var track = new VisualElement();
            track.style.height = 8f;
            track.style.marginTop = 2f;
            track.style.marginBottom = 2f;
            track.style.backgroundColor = new StyleColor(GitHubDark.Sub);
            track.style.borderTopLeftRadius = 2f; track.style.borderTopRightRadius = 2f;
            track.style.borderBottomLeftRadius = 2f; track.style.borderBottomRightRadius = 2f;
            _content.Add(track);

            var fill = new VisualElement();
            fill.name = "BarFill";
            fill.style.position = Position.Absolute;
            fill.style.left = 0; fill.style.top = 0; fill.style.bottom = 0;
            fill.style.width = 0f;
            fill.style.backgroundColor = new StyleColor(fillColor);
            track.Add(fill);
            return fill;
        }

        // ===== GitHub-dark 팔레트 (이 창 한정) =====
        private static class GitHubDark
        {
            public static readonly Color Panel    = Hex(0x161B22);
            public static readonly Color Sub      = Hex(0x21262D);
            public static readonly Color Accent   = Hex(0x58A6FF);
            public static readonly Color TextMain = Hex(0xF0F6FC);
            public static readonly Color TextSub  = Hex(0x8B949E);
            public static readonly Color Stroke   = Hex(0x2E343D);
            public static readonly Color Danger   = Hex(0xF85149);
            private static Color Hex(uint rgb) =>
                new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 0xFF);
        }

        // ===== 공개 API =====

        /// <summary>싱글톤 + 입력 Updater 보장 (멱등).</summary>
        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new FishingUTK();
            var go = new GameObject("FishingUTK_UPD");
            Object.DontDestroyOnLoad(go);
            _updater = go.AddComponent<Updater>();
            _updater.window = _instance;
            Debug.Log("[FishUTK] 인스턴스 생성");
        }

        /// <summary>창 오픈.</summary>
        public static void Open()
        {
            Ensure();
            _instance?.Show();
        }

        /// <summary>창 닫기.</summary>
        public static void CloseUI() => _instance?.Hide();

        /// <summary>토글.</summary>
        public static void Toggle()
        {
            if (_instance != null && _instance.IsOpen) { _instance.Hide(); return; }
            Open();
        }

        // ===== 생명주기 =====

        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            ApplyCanvasBounds();
            StartTick();
            Refresh();
        }

        public override void Hide()
        {
            StopTick();
            base.Hide();
        }

        private void StartTick()
        {
            if (_tickTask != null) return;
            _tickTask = schedule.Execute(() =>
            {
                if (IsOpen) Refresh();
            }).Every(TickMs);
        }

        private void StopTick()
        {
            if (_tickTask != null)
            {
                _tickTask.Pause();
                _tickTask = null;
            }
        }

        // ===== 화면 갱신 =====

        private void Refresh()
        {
            var fs = FishingSystem.Instance;
            if (fs == null) return;

            // 팝업 메시지
            bool showPopup = fs.PopupTimer > 0f && !string.IsNullOrEmpty(fs.PopupMessage);
            _popupHost.style.display = showPopup ? DisplayStyle.Flex : DisplayStyle.None;
            if (showPopup) _popupLabel.text = fs.PopupMessage;

            // 미니게임 활성 시에만 바 갱신, 대기 중에는 입질 찌 표시
            bool waiting = fs.IsWaitingForBite;
            bool active = fs.IsMinigameActive;
            _progressFill.visible = active;
            _tensionFill.visible = active;
            _bite.visible = waiting;
            _progressPct.visible = active;
            _hintLabel.style.display = (active || waiting) ? DisplayStyle.Flex : DisplayStyle.None;

            if (waiting)
            {
                _tensionState.text = "대기 중";
                _tensionState.style.color = new StyleColor(GitHubDark.TextSub);
                _hintLabel.text = "🎣 입질을 기다리는 중... (ESC: 취소)";
            }
            else if (active)
            {
                _tensionState.text = "ACTIVE";
                _tensionState.style.color = new StyleColor(GitHubDark.Accent);

                int pct = Mathf.RoundToInt(Mathf.Clamp01(fs.ReelProgress) * 100f);
                _progressPct.text = $"{pct}%";

                // 진행 바 — Figma progress-fill
                float prog = Mathf.Clamp01(fs.ReelProgress);
                _progressFill.style.width = new Length(prog * 100f, LengthUnit.Percent);

                // 장력 바 — Figma tension-fill (색상: 안전 녹 → 위험 빨강)
                float tens = Mathf.Clamp01(fs.ReelTension);
                _tensionFill.style.width = new Length(tens * 100f, LengthUnit.Percent);
                if (tens >= fs.TensionDangerThreshold)
                {
                    _tensionFill.style.backgroundColor = new StyleColor(GitHubDark.Danger);
                    SetTensionDanger(true);
                }
                else
                {
                    _tensionFill.style.backgroundColor = new StyleColor(new Color32(0x3F, 0xB9, 0x50, 0xFF));
                    SetTensionDanger(false);
                }

                _hintLabel.text = "[SPACE] 잡기  [Q] 취소";
            }
            else
            {
                _tensionState.text = "ACTIVE";
                _tensionState.style.color = new StyleColor(GitHubDark.Accent);
                _hintLabel.text = "[SPACE] 잡기  [Q] 취소";
            }
        }

        /// <summary>[Figma 160:39] 장력 위험 배지 표시/숨김.</summary>
        private void SetTensionDanger(bool on)
        {
            // 위험 라벨은 TensionSection 행에 있지만 간단히 타이틀 배지로 대체 표시
            if (on)
            {
                _tensionState.text = "위험!";
                _tensionState.style.color = new StyleColor(GitHubDark.Danger);
            }
            else if (FishingSystem.Instance != null)
            {
                _tensionState.text = "ACTIVE";
                _tensionState.style.color = new StyleColor(GitHubDark.Accent);
            }
        }

        // ===== 입력 Updater =====

        private class Updater : MonoBehaviour
        {
            public FishingUTK window;

            private void Update()
            {
                var root = UIToolkitBootstrap.UIRoot;
                if (root != null && window != null && window.parent == null)
                    root.Add(window);

                var fs = FishingSystem.Instance;
                if (fs == null) return;

                // [P29] 낚시 상태 폴링 자동 오픈/닫기
                bool active = fs.IsWaitingForBite || fs.IsMinigameActive;
                bool shouldOpen = window != null && fs.IsFishing && active;
                if (shouldOpen && !FishingUTK.Instance.IsOpen)
                    FishingUTK.Open();
                else if (!shouldOpen && FishingUTK.Instance.IsOpen)
                    FishingUTK.CloseUI();

                if (window == null || !window.IsOpen) return;

                if (fs.IsMinigameActive)
                {
                    if (UnityEngine.Input.GetKeyDown(KeyCode.Space))
                    {
                        fs.TryReel();
                        Debug.Log("[FishUTK] Space — 리일");
                    }
                }

                if (UnityEngine.Input.GetKeyDown(KeyCode.Q) || UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                {
                    fs.CancelFishing();
                    Debug.Log("[FishUTK] Q/ESC — 취소");
                }
            }

            private void OnDestroy()
            {
                if (window != null)
                {
                    window.StopTick();
                    window.RemoveFromHierarchy();
                }
            }
        }
    }
}