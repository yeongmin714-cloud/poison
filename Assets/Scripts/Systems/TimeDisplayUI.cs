using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// C13-04: IMGUI 기반 시간 표시 UI.
    /// HUD 우측 상단에 현재 시간과 주야 프로그레스 바를 표시합니다.
    /// </summary>
    [RequireComponent(typeof(TimeManager))]
    public class TimeDisplayUI : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] private int _windowWidth = 150;
        [SerializeField] private int _windowHeight = 60;
        [SerializeField] private int _marginRight = 10;
        [SerializeField] private int _marginTop = 10;

        [Header("Colors")]
        [SerializeField] private Color _dayBarColor = new Color(1f, 0.9f, 0.2f);    // 노랑
        [SerializeField] private Color _nightBarColor = new Color(0.2f, 0.3f, 0.8f); // 파랑
        [SerializeField] private Color _textColor = Color.white;
        [SerializeField] private Color _bgColor = new Color(0f, 0f, 0f, 0.4f);

        [Header("Font")]
        [SerializeField] private int _fontSize = 16;

        private TimeManager _timeManager;
        private GUIStyle _labelStyle;
        private GUIStyle _barBgStyle;
        private GUIStyle _barFillStyle;
        private Texture2D _fillTexture; // GC 방지: 재사용 1x1 텍스처
        private bool _stylesInitialized;

        private void Start()
        {
            _timeManager = TimeManager.Instance;
            if (_timeManager == null)
            {
                Debug.LogWarning("[TimeDisplayUI] TimeManager.Instance가 없습니다.");
                enabled = false;
            }
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _labelStyle = new GUIStyle
            {
                fontSize = _fontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _textColor }
            };

            _barBgStyle = new GUIStyle();
            _barBgStyle.normal.background = CreateTexture1x1(new Color(0.2f, 0.2f, 0.2f, 0.6f));

            _barFillStyle = new GUIStyle();
            _fillTexture = CreateTexture1x1(Color.white);
            _barFillStyle.normal.background = _fillTexture;

            _stylesInitialized = true;
        }

        /// <summary>1x1 단색 텍스처 생성. Mipmap 비활성화로 메모리 최적화.</summary>
        private static Texture2D CreateTexture1x1(Color color)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        private void OnGUI()
        {
            if (_timeManager == null) return;
            InitializeStyles();

            int x = Screen.width - _windowWidth - _marginRight;
            int y = _marginTop;

            // 배경 박스
            GUI.Box(new Rect(x, y, _windowWidth, _windowHeight), "", _barBgStyle);

            // 시간 텍스트
            string emoji = _timeManager.IsDay ? "\u2600\uFE0F" : "\U0001F319"; // ☀️ / 🌙
            string timeText = $"{emoji} {_timeManager.Hour:D2}:{_timeManager.Minute:D2}";
            GUI.Label(new Rect(x, y, _windowWidth, 28), timeText, _labelStyle);

            // 프로그레스 바 (하루 진행률)
            float progress = _timeManager.DayProgress;
            int barY = y + 32;
            int barHeight = 12;
            int barPadding = 8;
            int barWidth = _windowWidth - barPadding * 2;

            // 배경
            GUI.Box(new Rect(x + barPadding, barY, barWidth, barHeight), "", _barBgStyle);

            // 채움 — 재사용 텍스처에 SetPixel만 수행 (GC 할당 0)
            Color fillColor = _timeManager.IsDay ? _dayBarColor : _nightBarColor;
            _fillTexture.SetPixel(0, 0, fillColor);
            _fillTexture.Apply();

            int fillWidth = Mathf.RoundToInt(barWidth * progress);
            if (fillWidth > 0)
            {
                GUI.Box(new Rect(x + barPadding, barY, fillWidth, barHeight), "", _barFillStyle);
            }
        }
    }
}
