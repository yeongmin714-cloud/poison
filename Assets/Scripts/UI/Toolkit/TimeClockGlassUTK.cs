using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Systems;   // TimeManager

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// P27 — 좌상단 글래스 숫자 시계.
    /// 베이크 글래스 숫자 스프라이트(DigitalGlyph)는 보존하고 GitHub-dark 카드에 담아 표시.
    /// 300ms 폴링(TimeManager.GetFormattedTime → "HH:MM"). 기존 TimeDisplayUTK 대체.
    /// </summary>
    public class TimeClockGlassUTK : VisualElement
    {
        private static TimeClockGlassUTK _instance;
        public static TimeClockGlassUTK Instance => _instance;

        private const float PollInterval = 0.3f;
        private const float GlyphHeight = 46f;
        private static readonly Dictionary<char, float> GlyphW = new Dictionary<char, float>
        {
            { '0', 30f }, { '1', 26f }, { '2', 30f }, { '3', 30f }, { '4', 31f },
            { '5', 30f }, { '6', 30f }, { '7', 28f }, { '8', 31f }, { '9', 30f },
            { ':', 17f }
        };

        private VisualElement _row;
        private Label _dayLabel;   // [30차] DAY n 표시
        private readonly Dictionary<char, Texture2D> _tex = new Dictionary<char, Texture2D>();
        private string _lastText;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            Ensure();
            EnsureUpdater();
        }

        public static void Ensure()
        {
            if (_instance != null) return;
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return;
            _instance = new TimeClockGlassUTK();
            root.Add(_instance);
        }

        private static void EnsureUpdater()
        {
            // [2026-10-11 수리] 구 가드 `if (_instance != null) return;`는 Ensure()가 _instance를
            //   세팅한 뒤 호출되므로 항상 참 → Updater가 영구 미생성, 시계가 생성 시점 값에서
            //   고정되는 정지 버그(사용자 보고 "시간 ui도 시간이 안흐른다")의 근본 원인이었다.
            //   존재 여부는 씬 오브젝트로 판정한다 — 도메인 리로드 OFF여도 Play 종료 시
            //   DDOL GO는 파괴되므로 정적 bool 가드는 재발 위험이 있다(FindAnyObjectByType 안전).
            if (Object.FindAnyObjectByType<Updater>() != null) return;
            var go = new GameObject("TimeClockGlassUTK_Updater");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Updater>();
        }

        private TimeClockGlassUTK()
        {
            name = "TimeClockGlass";
            AddToClassList("utk-clock");
            style.position = Position.Absolute;
            style.left = 14f;
            style.top = 12f;
            style.width = 166f;
            style.height = GlyphHeight + 30f;   // [30차] DAY 라벨 행 추가
            style.flexDirection = FlexDirection.Column;
            style.alignItems = Align.Center;
            style.paddingLeft = 8f;
            style.paddingRight = 8f;
            style.backgroundColor = new StyleColor(new Color32(0x0D, 0x11, 0x17, 0xF2)); // GitHub #0D1117
            style.borderTopColor = new StyleColor(new Color32(0x58, 0xA6, 0xFF, 0xFF)); // GitHub blue accent
            style.borderBottomColor = new StyleColor(new Color32(0x30, 0x36, 0x3D, 0xFF));
            style.borderLeftColor = new StyleColor(new Color32(0x30, 0x36, 0x3D, 0xFF));
            style.borderRightColor = new StyleColor(new Color32(0x30, 0x36, 0x3D, 0xFF));
            style.borderTopWidth = 2f;
            style.borderBottomWidth = 1f;
            style.borderLeftWidth = 1f;
            style.borderRightWidth = 1f;
            style.borderTopLeftRadius = 8f;
            style.borderTopRightRadius = 8f;
            style.borderBottomLeftRadius = 8f;
            style.borderBottomRightRadius = 8f;
            pickingMode = PickingMode.Ignore;   // [P23 규약] HUD는 클릭 흡수 금지

            _row = new VisualElement();
            _row.name = "ClockRow";
            _row.style.flexDirection = FlexDirection.Row;
            _row.style.alignItems = Align.Center;
            Add(_row);

            for (char c = '0'; c <= '9'; c++)
                _tex[c] = Resources.Load<Texture2D>("UI/GlassDig_" + c);
            _tex[':'] = Resources.Load<Texture2D>("UI/GlassColon");

            // [2026-10-11 30차 사용자 요구] DAY 표시 — 좌상단 시계 카드 하단 "DAY n".
            //   날짜 = TimeManager.CurrentDay + 1(0기반 → 1일차 표시).
            _dayLabel = new Label("DAY 1");
            _dayLabel.style.fontSize = 10.5f;
            _dayLabel.style.color = new StyleColor(new Color32(0x58, 0xA6, 0xFF, 0xFF));   // GitHub 액센트
            _dayLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _dayLabel.pickingMode = PickingMode.Ignore;
            Add(_dayLabel);

            Refresh();
        }

        private void Refresh()
        {
            // [2026-10-10 수리] 구 코드는 TimeManager.Instance를 직접 읽어 씬에 TimeManager가 없으면
            //   영구 빈 패널이었다(hud ui 7 좌상단 빈 시계창의 근본 원인). StarField/RegionBGM 선례대로
            //   GetOrCreate()로 지연 생성해 어떤 씬에서도 시계가 살아있게 한다.
            var tm = TimeManager.GetOrCreate();
            if (tm == null) return;

            // [30차] DAY 갱신 — 시간 텍스트 무변경 조기 반환보다 앞에서(자정 넘어갈 때 즉시).
            if (_dayLabel != null)
            {
                string dayText = $"DAY {tm.CurrentDay + 1:0}";
                if (_dayLabel.text != dayText) _dayLabel.text = dayText;
            }

            string text = tm.GetFormattedTime();   // "HH:MM"
            if (text == _lastText) return;
            _lastText = text;

            // 갱신 시 기존 글리프 제거 후 재구성
            for (int i = _row.childCount - 1; i >= 0; i--)
                _row[i].RemoveFromHierarchy();

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (!_tex.TryGetValue(c, out var tex) || tex == null) continue;
                float w = GlyphW.TryGetValue(c, out var ww) ? ww : 28f;

                var g = new VisualElement();
                g.name = "Glyph_" + c;
                g.pickingMode = PickingMode.Ignore;
                g.style.width = w;
                g.style.height = GlyphHeight;
                g.style.backgroundImage = UTKTextureSafe.ToBackground(tex);
                g.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                if (i < text.Length - 1) g.style.marginRight = 3f;
                _row.Add(g);
            }
        }

        private class Updater : MonoBehaviour
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