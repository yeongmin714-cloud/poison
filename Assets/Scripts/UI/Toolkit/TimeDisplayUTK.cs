using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Systems;   // TimeManager

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U7 Round C — 시간 표시 (top-right, 상시 노출 HUD).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    /// 원본: Assets/Scripts/Systems/TimeDisplayUI.cs — 절대 수정 금지.
    ///
    /// [기능]
    ///   ① 우상단 고정 패널에 요일/시각 표시 (TimeManager 실측).
    ///   ② 주/야 아이콘 (☀️/🌙) + DayProgress 프로그레스 바 (원본 OnGUI 대응).
    ///   ③ 원본 TimeDisplayUI.OnGUI가 그리던 'Hour:Minute' + 진행률 게이지 시각화.
    ///   ④ MonoBehaviour Updater 300ms 폴링 (TimeManager 실측 갱신).
    ///
    /// 상시 노출 바 — HotbarUIUTK/StatusWindowUTK 부트스트랩 선례 (RuntimeInitialize + Updater).
    /// </summary>
    public class TimeDisplayUTK : VisualElement
    {
        private static TimeDisplayUTK _instance;
        public static TimeDisplayUTK Instance => _instance;

        private const float PollInterval = 0.3f;   // 300ms 폴링

        private readonly Label _timeLabel;
        private readonly Label _dayLabel;
        private readonly VisualElement _barFill;
        private readonly VisualElement _barHost;
        private string _lastDayText;
        private float _lastProgress = -1f;

        /// <summary>[P27] 패널형 시간표시 퇴역 — TimeClockGlassUTK(좌상단 글래스 숫자)가 대체.
        ///   Bootstrap 자동부착을 중지해 우상단 중복 패널을 제거한다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // [P27] 비활성 — 글래스 시계(TimeClockGlassUTK)로 대체. 남은 코드는 보존(롤백용).
            // Ensure();
            // EnsureUpdater();
        }

        /// <summary>싱글턴 보장 — UIRoot에 부재 시 생성·부착. 멱등.</summary>
        public static void Ensure()
        {
            if (_instance != null) return;
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return;
            _instance = new TimeDisplayUTK();
            root.Add(_instance);
        }

        private static void EnsureUpdater()
        {
            if (_instance != null) return;   // 부트스트랩이 이미 Updater 보유 시도
            var go = new GameObject("TimeDisplayUTK");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Updater>();
            Debug.Log("[TimeUTK] 부트스트랩 완료 — Updater 등록, 우상단 시간 표시 준비");
        }

        private TimeDisplayUTK()
        {
            name = "TimeDisplay";
            AddToClassList("utk-time");
            style.position = Position.Absolute;
            style.top = 10f;
            style.right = 10f;
            style.width = 180f;
            style.height = 58f;
            style.backgroundColor = new StyleColor(UTKColor.BgPanel);
            style.borderTopWidth = 1f;
            style.borderBottomWidth = 1f;
            style.borderLeftWidth = 1f;
            style.borderRightWidth = 1f;
            style.borderTopColor = new StyleColor(UTKColor.BorderBronze);
            style.borderBottomColor = new StyleColor(UTKColor.BorderBronze);
            style.borderLeftColor = new StyleColor(UTKColor.BorderBronze);
            style.borderRightColor = new StyleColor(UTKColor.BorderBronze);
            pickingMode = PickingMode.Ignore;

            _dayLabel = new Label("Day 1");
            _dayLabel.style.fontSize = 12f;
            _dayLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            Add(_dayLabel);

            _timeLabel = new Label("--:--");
            _timeLabel.style.fontSize = 26f;
            _timeLabel.style.color = new StyleColor(UTKColor.TextPrimary);
            _timeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            Add(_timeLabel);

            _barHost = new VisualElement();
            _barHost.style.height = 8f;
            _barHost.style.backgroundColor = new StyleColor(UTKColor.BgPanelDark);
            Add(_barHost);

            _barFill = new VisualElement();
            _barFill.style.height = 8f;
            _barFill.style.width = 0f;
            _barHost.Add(_barFill);

            UTKWindowBase.ApplyUIToolkitFont(this);
            Refresh();
        }

        // ─────────────────────────── 폴링 갱신 ───────────────────────────

        private void Refresh()
        {
            var tm = TimeManager.Instance;
            if (tm == null) return;

            string timeText = tm.IsDay ? "\u2600\uFE0F " : "\U0001F319 ";  // ☀️ / 🌙
            timeText += tm.GetFormattedTime();                              // HH:MM

            string dayText = "Day " + tm.CurrentDay;

            float progress = tm.DayProgress;
            bool tChanged = _timeLabel.text != timeText;
            bool dChanged = _lastDayText != dayText;

            if (tChanged)
            {
                _timeLabel.text = timeText;
            }
            if (dChanged)
            {
                _dayLabel.text = dayText;
                _lastDayText = dayText;
            }

            if (Mathf.Abs(progress - _lastProgress) > 0.005f)
            {
                _lastProgress = progress;
                _barFill.style.width = new Length(progress * 164f, LengthUnit.Pixel);
                _barFill.style.backgroundColor = new StyleColor(tm.IsDay ? UTKColor.AccentRare : UTKColor.AccentMagic);
            }
        }

        // ─────────────────────────── Updater ───────────────────────────

        /// <summary>MonoBehaviour 폴링 — UIRoot 부착 + 300ms 갱신.</summary>
        public class Updater : MonoBehaviour
        {
            private float _tick;

            private void Update()
            {
                var root = UIToolkitBootstrap.UIRoot;
                if (root != null && _instance != null && _instance.parent == null)
                    root.Add(_instance);

                if (_instance == null) return;
                _tick -= Time.unscaledDeltaTime;
                if (_tick <= 0f)
                {
                    _tick = PollInterval;
                    if (_instance.parent != null)
                        _instance.Refresh();
                }
            }
        }
    }
}