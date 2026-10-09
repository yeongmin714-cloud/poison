using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U4 Round B-2b — 🎯 자동 이동 HUD (UTK).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    /// 원본: Assets/Scripts/UI/AutoMoveUI.cs (326줄 IMGUI) — 원본은 절대 수정하지 않는다.
    ///
    /// [구현]
    ///  ① 상태 폴링 — AutoMoveManager.IsMoving / IsPaused / Destination / RemainingDistance 실측 (200ms).
    ///  ② 이동 중 — 목적지(Destination.x/z) + 남은 거리 표시.
    ///  ③ 일시 정지 — "전투 중 - 자동 이동 일시 정지" 표시.
    ///  ④ 알림 — AutoMoveManager.OnAutoMoveNotification 정적 이벤트 구독,
    ///     메시지에 "도착"/"취소"/"일시 정지|전투" 에 따라 색상/도착 팝업 분기 (원본과 동일).
    ///  ⑤ 알림 타이머 — 폴링 틱에서 감소, 도착 시 추가 표시.
    ///  각 경로에 [AutoUTK] UnityEngine.Debug 로그.
    /// [진입점] static Open() / Ensure(). 순수 VisualElement 트리.
    /// </summary>
    public class AutoMoveUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 팩토리 =====
        private static AutoMoveUTK _instance;
        public static AutoMoveUTK Instance => _instance;

        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new AutoMoveUTK();
            AutoMoveManager.OnAutoMoveNotification += _instance.HandleNotification;
        }

        /// <summary>자동 이동 HUD 열기.</summary>
        public static void Open()
        {
            Ensure();
            _instance.Show();
        }

        private const float WinW = 380f;
        private const float WinH = 230f;
        private const float SectionGap = 8f;
        private const long TickMs = 200L;
        private const float NotificationDuration = 3f;

        // ===== 상태 =====
        private string _currentMessage = "";
        private Color _currentMessageColor = Color.white;
        private float _messageTimer = 0f;
        private bool _hasNotification = false;
        private bool _showArrival = false;
        private float _arrivalTimer = 0f;

        // ===== 레퍼런스 =====
        private VisualElement _statusCard;
        private VisualElement _infoCard;
        private VisualElement _actionCard;
        private VisualElement _destCard;
        private VisualElement _distCard;
        private Label _statusLabel;
        private Label _destLabel;
        private Label _distLabel;
        private Label _notifLabel;
        private Label _arrivalLabel;
        private IVisualElementScheduledItem _tickTask;

        private AutoMoveUTK() : base("🎯 자동 이동", new Vector2(WinW, WinH))
        {
            UTKTheme.ApplyWindowChrome(this, null, _content);
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;
            _content.style.paddingLeft = UTKTheme.PanelPadding;
            _content.style.paddingRight = UTKTheme.PanelPadding;
            _content.style.paddingTop = UTKTheme.PanelPadding;
            _content.style.paddingBottom = UTKTheme.PanelPadding;
            _content.style.backgroundColor = new StyleColor(UTKTheme.Panel);

            // Status/header region uses shared panel styling; its accent follows live status.
            _statusCard = MakeCard(secondary: true);
            _statusCard.style.minHeight = 44f;
            _statusCard.style.justifyContent = Justify.Center;
            _statusLabel = MakeLabel("🚶 대기 중...", UTKTheme.TextMain);
            _statusLabel.style.fontSize = 16.8f;
            _statusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _statusCard.Add(_statusLabel);
            _content.Add(_statusCard);

            // Route/distance information shares one panel with evenly spaced sub-panels.
            _infoCard = MakeCard(secondary: true);
            _infoCard.style.flexDirection = FlexDirection.Row;
            _infoCard.style.marginTop = SectionGap;
            _infoCard.style.paddingLeft = SectionGap;
            _infoCard.style.paddingRight = SectionGap;
            _infoCard.style.paddingTop = SectionGap;
            _infoCard.style.paddingBottom = SectionGap;

            _destCard = MakeCard();
            _destCard.style.flexGrow = 1f;
            _destCard.style.marginRight = SectionGap;
            _destLabel = MakeLabel("🎯 목적지: -", UTKTheme.TextSub);
            _destLabel.style.fontSize = 13.2f;
            _destCard.Add(_destLabel);
            _infoCard.Add(_destCard);

            _distCard = MakeCard();
            _distCard.style.flexGrow = 1f;
            _distLabel = MakeLabel("📏 남은 거리: 0.0m", UTKTheme.TextSub);
            _distLabel.style.fontSize = 13.2f;
            _distCard.Add(_distLabel);
            _infoCard.Add(_distCard);
            _content.Add(_infoCard);

            // Notifications/arrival form a distinct action-feedback region.
            _actionCard = MakeCard();
            _actionCard.style.marginTop = SectionGap;
            _actionCard.style.display = DisplayStyle.None;
            _notifLabel = MakeLabel("", UTKTheme.TextMain);
            _notifLabel.style.fontSize = 15.6f;
            _actionCard.Add(_notifLabel);

            _arrivalLabel = MakeLabel("✅ 도착했습니다!", Color.green);
            _arrivalLabel.style.fontSize = 21.6f;
            _arrivalLabel.style.display = DisplayStyle.None;
            _arrivalLabel.style.marginTop = 6f;
            _actionCard.Add(_arrivalLabel);
            _content.Add(_actionCard);

            ApplyUIToolkitFont(this);

            style.display = DisplayStyle.None;
            style.left = 470f;
            style.top = 20f;
        }

        // ===== 생명주기 =====

        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            style.left = 470f;
            style.top = 20f;
            ResetNotifications();
            StartTicking();
            Debug.Log("[AutoUTK] 자동 이동 HUD 열림");
        }

        public override void Hide()
        {
            base.Hide();
            StopTicking();
            ResetNotifications();
            Debug.Log("[AutoUTK] 자동 이동 HUD 닫힘");
        }

        // ===== 상태 폴링 (200ms) =====

        private void StartTicking()
        {
            if (_tickTask != null) return;
            _tickTask = schedule.Execute(() =>
            {
                if (!IsOpen) return;
                TickTimers();
                RefreshState();
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

        /// <summary>알림 타이머 감소 (원본 Update 로직).</summary>
        private void TickTimers()
        {
            if (_hasNotification && _messageTimer > 0f)
            {
                _messageTimer -= TickMs / 1000f;
                if (_messageTimer <= 0f)
                {
                    _hasNotification = false;
                    _currentMessage = "";
                }
            }
            if (_showArrival && _arrivalTimer > 0f)
            {
                _arrivalTimer -= TickMs / 1000f;
                if (_arrivalTimer <= 0f)
                    _showArrival = false;
            }
        }

        /// <summary>AutoMoveManager 실측 상태를 HUD 로벨에 반영.</summary>
        private void RefreshState()
        {
            var mgr = AutoMoveManager.Instance;
            if (mgr == null)
            {
                _statusLabel.text = "🚶 AutoMoveManager 없음";
                SetStatusAccent(UTKTheme.Danger);
                return;
            }

            if (mgr.IsPaused)
            {
                _statusLabel.text = "⏸️ 전투 중 - 자동 이동 일시 정지";
                SetStatusAccent(UTKTheme.Warn);
            }
            else if (mgr.IsMoving)
            {
                _statusLabel.text = "🚶 자동 이동 중... [WASD로 취소]";
                SetStatusAccent(UTKTheme.Accent);
            }
            else
            {
                _statusLabel.text = "🚶 대기 중...";
                SetStatusAccent(UTKTheme.TextMain);
            }

            if (mgr.HasDestination)
            {
                Vector3 dest = mgr.Destination;
                _destLabel.text = $"🎯 목적지: ({dest.x:F1}, {dest.z:F1})";
                _destLabel.style.display = DisplayStyle.Flex;
                _distLabel.text = $"📏 남은 거리: {mgr.RemainingDistance:F1}m";
                _distLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                _destLabel.style.display = DisplayStyle.None;
                _distLabel.style.display = DisplayStyle.None;
            }

            _notifLabel.text = _currentMessage;
            _notifLabel.style.color = new StyleColor(_currentMessageColor);
            _notifLabel.style.display = (_hasNotification && !string.IsNullOrEmpty(_currentMessage))
                ? DisplayStyle.Flex : DisplayStyle.None;

            _arrivalLabel.style.display = _showArrival ? DisplayStyle.Flex : DisplayStyle.None;
            _actionCard.style.display = (_notifLabel.style.display == DisplayStyle.Flex || _showArrival)
                ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ===== 알림 처리 (정적 이벤트 구독) =====

        private void HandleNotification(string message)
        {
            _currentMessage = message ?? "";
            _messageTimer = NotificationDuration;
            _hasNotification = true;

            if (message.Contains("도착"))
            {
                _currentMessageColor = Color.green;
                _showArrival = true;
                _arrivalTimer = NotificationDuration * 1.5f;
            }
            else if (message.Contains("취소"))
            {
                _currentMessageColor = new Color(1f, 0.7f, 0.3f);
            }
            else if (message.Contains("일시 정지") || message.Contains("전투"))
            {
                _currentMessageColor = Color.yellow;
            }
            else
            {
                _currentMessageColor = Color.cyan;
            }

            Debug.Log($"[AutoUTK] 알림: {message}");
        }

        private void ResetNotifications()
        {
            _hasNotification = false;
            _showArrival = false;
            _currentMessage = "";
            _messageTimer = 0f;
            _arrivalTimer = 0f;
        }

        // ===== 헬퍼 =====

        private static Label MakeLabel(string text, Color color)
        {
            var l = new Label(text);
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.color = new StyleColor(color);
            return l;
        }

        /// <summary>Creates a shared-theme panel, optionally using the secondary surface.</summary>
        private static VisualElement MakeCard(bool secondary = false)
        {
            return UTKTheme.CreatePanel(secondary: secondary);
        }

        /// <summary>상태 카드 포인트 색 + 상태 텍스트 색 동기화.</summary>
        private void SetStatusAccent(Color accent)
        {
            if (_statusCard != null)
                _statusCard.style.borderTopColor = new StyleColor(accent);
            if (_statusLabel != null)
                _statusLabel.style.color = new StyleColor(accent);
        }
    }
}