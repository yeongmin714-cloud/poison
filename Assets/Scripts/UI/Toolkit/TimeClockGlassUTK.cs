using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Systems;   // TimeManager

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// P27 — 글래스모피즘 시간 시계 (좌상단, 숫자만 부유).
    /// "다른 창 없이 숫자만 입체적으로 떠서 시간 표현" 요구 구현.
    /// 이 엔진은 CSS backdrop-filter 미지원(E<P20 drop-shadow와 동일) → 실제 블러 대신
    /// **베이크 글래스 숫자 스프라이트**(DigitalGlyph: 0-9/콜론, 흰 테두리+상단 하이라이트+
    /// 남색 그림자로 입체 유리 표면)를 사용. 패널/테두리 없이 숫자 이미지만 좌상단에 배치.
    /// 300ms 폴링(TimeManager.GetFormattedTime → "HH:MM"). 기존 TimeDisplayUTK(패널형) 대체.
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
            if (_instance != null) return;
            var go = new GameObject("TimeClockGlassUTK");
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
            style.width = 150f;
            style.height = GlyphHeight + 14f;   // 그림자 여유
            pickingMode = PickingMode.Ignore;   // [P23 규약] HUD는 클릭 흡수 금지

            _row = new VisualElement();
            _row.name = "ClockRow";
            _row.style.position = Position.Absolute;
            _row.style.left = 0f;
            _row.style.top = 0f;
            _row.style.flexDirection = FlexDirection.Row;
            _row.style.alignItems = Align.Center;
            Add(_row);

            for (char c = '0'; c <= '9'; c++)
                _tex[c] = Resources.Load<Texture2D>("UI/GlassDig_" + c);
            _tex[':'] = Resources.Load<Texture2D>("UI/GlassColon");

            Refresh();
        }

        private void Refresh()
        {
            var tm = TimeManager.Instance;
            if (tm == null) return;

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