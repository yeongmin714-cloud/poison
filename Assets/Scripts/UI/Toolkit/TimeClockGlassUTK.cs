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
            style.width = 166f;
            style.height = GlyphHeight + 14f;
            style.flexDirection = FlexDirection.Row;
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