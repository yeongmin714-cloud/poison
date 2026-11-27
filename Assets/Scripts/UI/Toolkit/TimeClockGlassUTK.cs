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
        private VisualElement _dayRow;      // [142차 E2] DAY 행(워드 라벨+글래스 글리프)
        private VisualElement _dayGlyphs;   // [142차 E2] DAY 숫자 글리프 스트립
        private Label _dayLabel;   // [30차] DAY 워드 라벨
        private int _lastDay = -1;
        private const float DayGlyphHeight = 20f;   // [142차 E2] 시간 글리프(46f) 축소판 — 동일 소스/스타일
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
            // [142차 E] 시계 카드 박스 제거(사용자 지시: "사각 박스 없이 숫자와 밑에 day") —
            // GitHub-dark 카드/액센트 테두리/라운드 전면 폐기 → 글리프 숫자+DAY 라벨만 부유(플루언트).
            // .utk-clock USS 배경이 있어도 인라인 투명이 우선한다.
            style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 0f;
            pickingMode = PickingMode.Ignore;   // [P23 규약] HUD는 클릭 흡수 금지

            _row = new VisualElement();
            _row.name = "ClockRow";
            _row.style.flexDirection = FlexDirection.Row;
            _row.style.alignItems = Align.Center;
            Add(_row);

            for (char c = '0'; c <= '9'; c++)
                _tex[c] = Resources.Load<Texture2D>("UI/GlassDig_" + c);
            _tex[':'] = Resources.Load<Texture2D>("UI/GlassColon");

            // [142차 E2] DAY 표시 — 시간 표시와 동일 글래스 글리프 스타일(사용자 지시: "밑에 day도
            //   시간 표시와 동일한 스타일"). "DAY" 워드 라벨 + 숫자는 시간과 같은 GlassDig 텍스처 글리프.
            //   날짜 = TimeManager.CurrentDay + 1(0기반 → 1일차 표시).
            _dayRow = new VisualElement();
            _dayRow.name = "DayRow";
            _dayRow.style.flexDirection = FlexDirection.Row;
            _dayRow.style.alignItems = Align.Center;
            _dayRow.style.justifyContent = Justify.Center;
            _dayRow.pickingMode = PickingMode.Ignore;
            Add(_dayRow);

            _dayLabel = new Label("DAY");
            _dayLabel.style.fontSize = 13f;
            _dayLabel.style.color = new StyleColor(new Color32(0x58, 0xA6, 0xFF, 0xFF));   // GitHub 액센트(기존 계약색)
            _dayLabel.style.marginRight = 6f;
            _dayLabel.pickingMode = PickingMode.Ignore;
            _dayRow.Add(_dayLabel);

            _dayGlyphs = new VisualElement();
            _dayGlyphs.name = "DayGlyphs";
            _dayGlyphs.style.flexDirection = FlexDirection.Row;
            _dayGlyphs.style.alignItems = Align.Center;
            _dayGlyphs.pickingMode = PickingMode.Ignore;
            _dayRow.Add(_dayGlyphs);

            Refresh();
        }

        private void Refresh()
        {
            // [2026-10-10 수리] 구 코드는 TimeManager.Instance를 직접 읽어 씬에 TimeManager가 없으면
            //   영구 빈 패널이었다(hud ui 7 좌상단 빈 시계창의 근본 원인). StarField/RegionBGM 선례대로
            //   GetOrCreate()로 지연 생성해 어떤 씬에서도 시계가 살아있게 한다.
            var tm = TimeManager.GetOrCreate();
            if (tm == null) return;

            // [142차 E2] DAY 갱신 — 숫자를 시간과 동일 GlassDig 글리프로 렌더(자정 넘어갈 때 즉시).
            int dayNum = tm.CurrentDay + 1;
            if (dayNum != _lastDay && _dayGlyphs != null)
            {
                _lastDay = dayNum;
                for (int i = _dayGlyphs.childCount - 1; i >= 0; i--)
                    _dayGlyphs[i].RemoveFromHierarchy();
                string dayStr = dayNum.ToString("0");
                for (int i = 0; i < dayStr.Length; i++)
                {
                    char c = dayStr[i];
                    if (!_tex.TryGetValue(c, out var tex) || tex == null) continue;
                    float w = GlyphW.TryGetValue(c, out var ww) ? ww : 28f;
                    var g = new VisualElement();
                    g.name = "DayGlyph_" + c;
                    g.pickingMode = PickingMode.Ignore;
                    g.style.width = w * (DayGlyphHeight / GlyphHeight);
                    g.style.height = DayGlyphHeight;
                    g.style.backgroundImage = UTKTextureSafe.ToBackground(tex);
                    g.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                    if (i < dayStr.Length - 1) g.style.marginRight = 2f;
                    _dayGlyphs.Add(g);
                }
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