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
            style.flexDirection = FlexDirection.Column;
            style.justifyContent = Justify.SpaceBetween;
            style.paddingLeft = 8f;
            style.paddingRight = 8f;
            style.paddingTop = 6f;
            style.paddingBottom = 6f;
            style.backgroundColor = new StyleColor(new Color32(0x16, 0x1B, 0x22, 0xFF));
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 1f;
            var stroke = new StyleColor(new Color32(0x2E, 0x34, 0x3D, 0xFF));
            style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = stroke;
            style.borderTopLeftRadius = style.borderTopRightRadius = 6f;
            style.borderBottomLeftRadius = style.borderBottomRightRadius = 6f;
            pickingMode = PickingMode.Ignore;

            // GitHub-dark 배지 카드의 한 줄 헤더: 시간과 날짜를 양 끝에 배치.
            var infoRow = new VisualElement();
            infoRow.style.flexDirection = FlexDirection.Row;
            infoRow.style.alignItems = Align.Center;
            infoRow.style.justifyContent = Justify.SpaceBetween;
            Add(infoRow);

            _timeLabel = new Label("--:--");
            _timeLabel.style.fontSize = 22f;
            _timeLabel.style.color = new StyleColor(UTKColor.TextPrimary);
            _timeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            infoRow.Add(_timeLabel);

            _dayLabel = new Label("Day 1");
            _dayLabel.style.fontSize = 11f;
            _dayLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            infoRow.Add(_dayLabel);

            _barHost = new VisualElement();
            _barHost.style.height = 4f;
            _barHost.style.backgroundColor = new StyleColor(new Color32(0x0D, 0x11, 0x17, 0xFF));
            _barHost.style.borderTopLeftRadius = _barHost.style.borderTopRightRadius = 2f;
            _barHost.style.borderBottomLeftRadius = _barHost.style.borderBottomRightRadius = 2f;
            Add(_barHost);

            _barFill = new VisualElement();
            _barFill.style.height = 4f;
            _barFill.style.width = 0f;
            _barFill.style.borderTopLeftRadius = _barFill.style.borderBottomLeftRadius = 2f;
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
            _timeLabel.style.color = new StyleColor(tm.IsDay ? UTKColor.AccentRare : UTKColor.AccentMagic);
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