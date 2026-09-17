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
        private const float WinH = 190f;
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
        private Label _statusLabel;
        private Label _destLabel;
        private Label _distLabel;
        private Label _notifLabel;
        private Label _arrivalLabel;
        private IVisualElementScheduledItem _tickTask;

        private AutoMoveUTK() : base("🎯 자동 이동", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;

            _statusLabel = MakeLabel("🚶 대기 중...", UTKColor.TextPrimary);
            _statusLabel.style.fontSize = 16f;
            _content.Add(_statusLabel);

            _destLabel = MakeLabel("🎯 목적지: -", UTKColor.TextSecondary);
            _destLabel.style.fontSize = 13f;
            _content.Add(_destLabel);

            _distLabel = MakeLabel("📏 남은 거리: 0.0m", UTKColor.TextSecondary);
            _distLabel.style.fontSize = 13f;
            _content.Add(_distLabel);

            _notifLabel = MakeLabel("", Color.white);
            _notifLabel.style.fontSize = 15f;
            _content.Add(_notifLabel);

            _arrivalLabel = MakeLabel("✅ 도착했습니다!", new Color(0f, 1f, 0f));
            _arrivalLabel.style.fontSize = 22f;
            _arrivalLabel.style.display = DisplayStyle.None;
            _arrivalLabel.style.marginTop = 6f;
            _content.Add(_arrivalLabel);

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
                return;
            }

            if (mgr.IsPaused)
            {
                _statusLabel.text = "⏸️ 전투 중 - 자동 이동 일시 정지";
                _statusLabel.style.color = new StyleColor(Color.yellow);
            }
            else if (mgr.IsMoving)
            {
                _statusLabel.text = "🚶 자동 이동 중... [WASD로 취소]";
                _statusLabel.style.color = new StyleColor(Color.cyan);
            }
            else
            {
                _statusLabel.text = "🚶 대기 중...";
                _statusLabel.style.color = new StyleColor(UTKColor.TextPrimary);
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
    }
}