using ProjectName.Systems;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U6 Round D1 — 동적 이벤트 팝업 (알림/선택지) UTK 윈도우.
    /// 원본: Assets/Scripts/UI/DynamicEventUI.cs (425줄 IMGUI static) — 원본은 절대 수정하지 않는다.
    ///
    /// [구현]
    ///   ① 이벤트 알림 — WorldEventManager.OnEventStarted 구독 → 팝업 표시.
    ///   ② 이벤트 정보 — ActiveEvent 실측 필드(type/territory/description/RemainingTime) 직접 사용,
    ///      WorldEventManager.GetEventEmoji/GetEventDisplayName 호출.
    ///   ③ 선택지 — [이동하기] AcceptEvent / [무시] IgnoreEvent 실측 호출.
    ///   자동 종료 — 이벤트 비활성(phase != Active)이거나 15초 초과 시 Dismiss (400ms 폴링).
    ///   각 경로에 [DynEventUTK] UnityEngine.Debug 로그.
    /// [진입점] static Ensure() / ShowEvent(evt) / Open() / Toggle() / Close(). 순수 VisualElement 트리.
    /// </summary>
    public class DynamicEventUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 팩토리 =====
        private static DynamicEventUTK _instance;
        public static DynamicEventUTK Instance => _instance;

        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new DynamicEventUTK();
        }

        /// <summary>현재 표시 중인 이벤트 팝업 열기 (원본 DrawEvent 대응).</summary>
        public static void Open()
        {
            Ensure();
            _instance.Show();
        }

        /// <summary>이벤트 알림 팝업 표시 (원본 ShowEvent 대응, WorldEventManager가 호출).</summary>
        public static void ShowEvent(WorldEventManager.ActiveEvent evt)
        {
            if (evt == null)
            {
                Debug.LogWarning("[DynEventUTK] ShowEvent: evt가 null");
                return;
            }
            Ensure();
            _instance._current = evt;
            _instance._showTime = Time.time;
            Debug.Log("[DynEventUTK] 🌍 이벤트 팝업 표시: " + WorldEventManager.GetEventEmoji(evt.type) + " " + WorldEventManager.GetEventDisplayName(evt.type));
            _instance.Show();
        }

        public static void Toggle()
        {
            if (_instance != null && _instance.IsOpen) { _instance.Hide(); return; }
            Open();
        }

        public static void Close()
        {
            if (_instance != null)
                _instance.Hide();
        }

        /// <summary>이벤트 구독 (원본 Initialize 대응).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            WorldEventManager.OnEventStarted += ShowEvent;
        }

        // ===== 설정 =====
        private const float WinW = 480f;
        private const float WinH = 360f;
        private const long RefreshMs = 400L;
        private const float AutoDismissSeconds = 15f;

        // ===== 상태 =====
        private WorldEventManager.ActiveEvent _current;
        private float _showTime;

        // ===== 레퍼런스 =====
        private readonly VisualElement _body;
        private IVisualElementScheduledItem _refreshTask;

        private DynamicEventUTK() : base("🌍 월드 이벤트", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;
            _content.style.paddingTop = 6f;
            _content.style.paddingBottom = 6f;

            _body = new VisualElement();
            _body.name = "EventBody";
            _body.style.flexGrow = 1f;
            _body.style.flexDirection = FlexDirection.Column;
            _content.Add(_body);

            ApplyUIToolkitFont(this);

            style.display = DisplayStyle.None;
            style.left = 720f;
            style.top = 260f;
        }

        // ===== 생명주기 =====
        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            style.left = 720f;
            style.top = 260f;
            StartRefreshLoop();
            Refresh();
        }

        public override void Hide()
        {
            base.Hide();
            StopRefreshLoop();
        }

        private void StartRefreshLoop()
        {
            if (_refreshTask != null) return;
            _refreshTask = schedule.Execute(() =>
            {
                if (!IsOpen) return;
                if (_current == null)
                {
                    HideNow();
                    return;
                }
                // 이벤트가 더 이상 활성이면 종료
                if (_current.phase != WorldEventManager.EventPhase.Active)
                {
                    Debug.Log("[DynEventUTK] 이벤트 상태 변경으로 팝업 종료");
                    HideNow();
                    return;
                }
                // 자동 종료 (15초)
                if (Time.time - _showTime >= AutoDismissSeconds)
                {
                    if (WorldEventManager.Instance != null)
                        WorldEventManager.Instance.IgnoreEvent(_current);
                    Debug.Log("[DynEventUTK] 시간 초과로 이벤트 무시 후 종료");
                    HideNow();
                    return;
                }
                Refresh();
            }).Every(RefreshMs);
        }

        private void StopRefreshLoop()
        {
            if (_refreshTask != null)
            {
                _refreshTask.Pause();
                _refreshTask = null;
            }
        }

        private void HideNow()
        {
            _current = null;
            Close();
        }

        // ===== 렌더링 =====
        private void Refresh()
        {
            _body.Clear();
            if (_current == null) return;

            var evt = _current;
            var title = MakeLabel(WorldEventManager.GetEventEmoji(evt.type) + " " + WorldEventManager.GetEventDisplayName(evt.type), UTKColor.TextPrimary, false);
            title.style.fontSize = 18f;
            _body.Add(title);

            if (!string.IsNullOrEmpty(evt.description))
                _body.Add(MakeLabel(evt.description, UTKColor.TextSecondary, true));

            _body.Add(MakeLabel("📍 영지: " + evt.territoryName + " (" + evt.territoryId + ")", UTKColor.TextSecondary, false));

            float remaining = evt.RemainingTime;
            Color timerColor = remaining < 60f ? UTKColor.HealthRed : (remaining < 180f ? UTKColor.BorderGold : Color.white);
            _body.Add(MakeLabel("⏱️ " + FormatRemainingTime(remaining), timerColor, false));

            float autoRemaining = AutoDismissSeconds - (Time.time - _showTime);
            if (autoRemaining > 0f)
                _body.Add(MakeLabel("⏰ " + Mathf.CeilToInt(autoRemaining) + "초 후 자동 종료", UTKColor.TextSecondary, false));

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = 10f;
            _body.Add(row);

            row.Add(UTKButton.Create("⚔️ 이동하기", OnMoveToEvent, UTKButton.Variant.Primary));
            row.Add(UTKButton.Create("❌ 무시", OnIgnoreEvent, UTKButton.Variant.Danger));
        }

        // ===== 선택지 처리 =====
        private void OnMoveToEvent()
        {
            if (_current == null || WorldEventManager.Instance == null)
            {
                HideNow();
                return;
            }
            Debug.Log("[DynEventUTK] 🚀 플레이어가 " + _current.territoryName + " 이벤트로 이동 (수락)");
            WorldEventManager.Instance.AcceptEvent(_current);
            HideNow();
        }

        private void OnIgnoreEvent()
        {
            if (_current == null || WorldEventManager.Instance == null)
            {
                HideNow();
                return;
            }
            Debug.Log("[DynEventUTK] ❌ 플레이어가 " + _current.territoryName + " 이벤트를 무시");
            WorldEventManager.Instance.IgnoreEvent(_current);
            HideNow();
        }

        // ===== 헬퍼 =====
        private static string FormatRemainingTime(float seconds)
        {
            if (seconds <= 0f)
                return "시간 초과";
            int totalSec = Mathf.CeilToInt(seconds);
            int minutes = totalSec / 60;
            int secs = totalSec % 60;
            return minutes > 0 ? "남은 시간: " + minutes + "분 " + secs.ToString("D2") + "초"
                               : "남은 시간: " + secs + "초";
        }

        private static Label MakeLabel(string text, Color color, bool wrap)
        {
            var l = new Label(text);
            l.style.fontSize = 13f;
            l.style.color = new StyleColor(color);
            if (wrap)
                l.style.whiteSpace = WhiteSpace.Normal;
            return l;
        }
    }
}