// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 114
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Systems;   // FishingSystem

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U6 Round B — 낚시 미니게임 (UTKWindowBase 파생).
    /// 원본: Assets/Scripts/UI/FishingUI.cs (158줄, IMGUI) — 진행 경로는 원본 동일:
    /// 순수 표시 + Space/ESC 입력만 처리. 상태/판정은 모두 FishingSystem가 소유.
    /// 원본은 절대 수정하지 않는다.
    ///
    /// [실측 API — 원본 FishingUI 경로 동일]
    ///   확성화: FishingSystem.Instance.IsMinigameActive
    ///   표시: fs.ProgressBarWidth(300px) / fs.SweetSpotStart / fs.SweetSpotWidth(30)
    ///         fs.PinPosition / fs.PopupMessage / fs.PopupTimer
    ///   입력: FishingSystem.Instance.TryCatch()  — Space
    ///         FishingSystem.Instance.CancelFishing() — ESC
    ///
    /// 폴링: schedule.Execute().Every(50ms) → Pause 정지. [FishUTK] 로그.
    /// </summary>
    public class FishingUTK : UTKWindowBase
    {
        private static FishingUTK _instance;
        private static Updater _updater;

        private const float WinW = 460f;
        private const float WinH = 260f;
        private const long TickMs = 50L;

        // 프로그레스바 요소
        private readonly VisualElement _bar;
        private readonly VisualElement _sweet;
        private readonly VisualElement _pin;
        private readonly VisualElement _bite;   // 입질 대기 물결 찌
        private readonly Label _hintLabel;
        private readonly Label _popupLabel;
        private readonly VisualElement _popupHost;
        private IVisualElementScheduledItem _tickTask;

        public static FishingUTK Instance => _instance;

        private FishingUTK() : base("🎣 낚시", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;

            _popupHost = new VisualElement();
            _popupHost.style.position = Position.Absolute;
            _popupHost.style.left = 0; _popupHost.style.right = 0; _popupHost.style.top = 0; _popupHost.style.bottom = 0;
            _popupHost.style.alignItems = Align.Center;
            _popupHost.style.justifyContent = Justify.Center;
            _popupHost.style.display = DisplayStyle.None;
            _popupHost.pickingMode = PickingMode.Ignore;
            _content.Add(_popupHost);

            _popupLabel = new Label("");
            _popupLabel.style.fontSize = 20f;
            _popupLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _popupLabel.style.color = new StyleColor(Color.white);
            _popupLabel.style.whiteSpace = WhiteSpace.Normal;
            _popupHost.Add(_popupLabel);

            // 중앙 하단 프로그레스바 컨테이너
            var barRow = new VisualElement();
            barRow.style.marginTop = 90f;
            barRow.style.alignSelf = Align.Center;
            barRow.style.width = 300f;
            barRow.style.height = 34f;
            barRow.style.position = Position.Relative;
            barRow.AddToClassList("utk-slot");
            _content.Add(barRow);

            // 바 트랙 — 베이크 물결 프레임 (P29 고품질화, 프리미티브 회색 교체)
            _bar = new VisualElement();
            _bar.style.position = Position.Absolute;
            _bar.style.left = 0; _bar.style.top = 0; _bar.style.right = 0; _bar.style.bottom = 0;
            _bar.AddToClassList("utk-slot");
            _bar.style.backgroundImage = UTKTextureSafe.ToBackground(Resources.Load<Texture2D>("UI/FishingBarFrame"));
            _bar.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            barRow.Add(_bar);

            // 스위트스팟 — 베이크 황금 존 (초록 사각 교체)
            _sweet = new VisualElement();
            _sweet.style.position = Position.Absolute;
            _sweet.style.top = -2f; _sweet.style.bottom = -2f;
            _sweet.style.width = 34f;
            _sweet.AddToClassList("utk-slot");
            _sweet.style.backgroundImage = UTKTextureSafe.ToBackground(Resources.Load<Texture2D>("UI/FishingSweetspot"));
            _sweet.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            barRow.Add(_sweet);

            // 핀 — 베이크 화살표 (빨강 4px 교체)
            _pin = new VisualElement();
            _pin.style.position = Position.Absolute;
            _pin.style.top = -2f; _pin.style.bottom = -2f;
            _pin.style.width = 24f;
            _pin.AddToClassList("utk-slot");
            _pin.style.backgroundImage = UTKTextureSafe.ToBackground(Resources.Load<Texture2D>("UI/FishingPin"));
            _pin.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            barRow.Add(_pin);

            // 대기 중 물결 찌 — 입질 대기 시 표시 (P29 고품질화 신규)
            _bite = new VisualElement();
            _bite.style.position = Position.Absolute;
            _bite.style.alignSelf = Align.Center;
            _bite.style.left = 126f; _bite.style.top = 4f;
            _bite.style.width = 48f; _bite.style.height = 36f;
            _bite.AddToClassList("utk-slot");
            _bite.style.backgroundImage = UTKTextureSafe.ToBackground(Resources.Load<Texture2D>("UI/FishingBite"));
            _bite.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            _content.Add(_bite);

            _hintLabel = new Label("␣ 스페이스바: 잡기 | ESC: 취소");
            _hintLabel.style.fontSize = 13f;
            _hintLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            _hintLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _hintLabel.style.marginTop = 8f;
            _content.Add(_hintLabel);

            ApplyUIToolkitFont(this);
            // 원본은 전체화면 위 미니게임만 해당 → 본 창은 항상 오픈 상태에서 미니게임 활성/비활동 시 표시
            style.display = DisplayStyle.None;
            style.left = 60f;
            style.top = 90f;
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

        /// <summary>창 오픈 (원본 FishingUI는 씬 배치 상시 표시 — 본 창도 오픈 시 폴링 시작).</summary>
        public static void Open()
        {
            Ensure();
            _instance?.Show();
        }

        /// <summary>창 닫기.</summary>
        public static void CloseUI() => _instance?.Hide();

        /// <summary>토글 — IsOpen 분기 필수.</summary>
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

            // 미니게임 활성 시에만 바/스위트/핀, 대기 중에는 물결 찌 표시
            bool waiting = fs.IsWaitingForBite;
            bool active = fs.IsMinigameActive;
            _bar.visible = active;
            _sweet.visible = active;
            _pin.visible = active;
            _bite.visible = waiting;   // 입질 대기 중 찌 (P29 고품질화)
            _hintLabel.style.display = (active || waiting) ? DisplayStyle.Flex : DisplayStyle.None;
            if (waiting) _hintLabel.text = "🎣 입질을 기다리는 중... (ESC: 취소)";
            else _hintLabel.text = "␣ 스페이스바: 잡기 | ESC: 취소";

            if (!active) return;

            _sweet.style.left = fs.SweetSpotStart;
            _sweet.style.width = fs.SweetSpotWidth;
            _pin.style.left = Mathf.Clamp(fs.PinPosition - 2f, 0f, fs.ProgressBarWidth - 4f);
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

                // [P29] 낚시 상태 폴링 자동 오픈/닫기 — Systems→UI 순환참조 없이
                // UI가 상태(대기/미니게임)를 보고 스스로 표시/숨김.
                bool active = fs.IsWaitingForBite || fs.IsMinigameActive;
                bool shouldOpen = window != null && fs.IsFishing && active;
                if (shouldOpen && !FishingUTK.Instance.IsOpen)
                    FishingUTK.Open();
                else if (!shouldOpen && FishingUTK.Instance.IsOpen)
                    FishingUTK.CloseUI();

                if (window == null || !window.IsOpen) return;
                if (fs == null || !fs.IsMinigameActive) return;

                if (Input.GetKeyDown(KeyCode.Space))
                {
                    fs.TryCatch();
                    Debug.Log("[FishUTK] Space — 잡기 시도");
                }

                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    fs.CancelFishing();
                    Debug.Log("[FishUTK] ESC — 낚시 취소");
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