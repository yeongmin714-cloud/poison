using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C12-02: 로딩 화면 UI.
    /// LoadingManager의 IsLoading 상태를 감시하여 풀스크린 로딩 화면을 표시합니다.
    /// DontDestroyOnLoad로 씬 전환 간 유지됩니다.
    /// </summary>
    [RequireComponent(typeof(LoadingManager))]
    public class LoadingScreenUI : MonoBehaviour
    {
        private LoadingManager _manager;

        // ===== 레이아웃 =====
        private const float SPINNER_SIZE = 48f;
        private const float BAR_WIDTH = 300f;
        private const float BAR_HEIGHT = 20f;
        private const float TIP_WIDTH = 500f;

        // ===== 색상 =====
        private static readonly Color ColorBg = new Color(0f, 0f, 0f, 0.92f);
        private static readonly Color ColorBarBg = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        private static readonly Color ColorBarFill = new Color(0.3f, 0.7f, 1f, 1f);
        private static readonly Color ColorSpinner = new Color(1f, 1f, 1f, 0.9f);
        private static readonly Color ColorProgressText = new Color(0.8f, 0.8f, 0.8f, 1f);
        private static readonly Color ColorTipText = new Color(0.7f, 0.7f, 0.7f, 0.8f);

        // ===== 스타일 =====
        private GUIStyle _styleLabel;
        private GUIStyle _styleTip;
        private GUIStyle _stylePct;
        private Texture2D _texWhite;
        private bool _stylesInitialized;

        private void Awake()
        {
            _manager = GetComponent<LoadingManager>();
            if (_manager == null)
                _manager = FindObjectOfType<LoadingManager>();
        }

        private void OnGUI()
        {
            if (_manager == null || !_manager.IsLoading)
                return;

            InitStyles();

            float sw = Screen.width;
            float sh = Screen.height;
            float cx = sw / 2f;
            float cy = sh / 2f;

            // ===== 배경 (풀스크린) =====
            var oldColor = GUI.color;
            GUI.color = ColorBg;
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _texWhite);
            GUI.color = oldColor;

            // ===== 스피너 (회전) =====
            float angle = Time.time * 180f; // 180도/초
            GUIUtility.RotateAroundPivot(angle, new Vector2(cx, cy - 40f));
            GUI.color = ColorSpinner;
            DrawSpinner(cx, cy - 40f);
            GUI.color = oldColor;
            GUI.matrix = Matrix4x4.identity;

            // ===== 프로그레스 바 배경 =====
            float barX = cx - BAR_WIDTH / 2f;
            float barY = cy + 60f;
            GUI.color = ColorBarBg;
            GUI.DrawTexture(new Rect(barX, barY, BAR_WIDTH, BAR_HEIGHT), _texWhite);

            // ===== 프로그레스 바 채움 =====
            float fill = _manager.Progress;
            GUI.color = ColorBarFill;
            GUI.DrawTexture(new Rect(barX, barY, BAR_WIDTH * fill, BAR_HEIGHT), _texWhite);
            GUI.color = oldColor;

            // ===== 진행률 텍스트 =====
            string pctText = $"{(fill * 100f):F0}%";
            GUI.Label(new Rect(cx - 60f, barY + BAR_HEIGHT + 4f, 120f, 24f), pctText, _stylePct);

            // ===== 팁 텍스트 =====
            if (!string.IsNullOrEmpty(_manager.CurrentTip))
            {
                float tipX = cx - TIP_WIDTH / 2f;
                float tipY = barY + 60f;
                GUI.Label(new Rect(tipX, tipY, TIP_WIDTH, 40f), _manager.CurrentTip, _styleTip);
            }
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _texWhite = MakeTexture(1, 1, Color.white);

            _styleLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ColorProgressText }
            };

            _stylePct = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ColorProgressText }
            };

            _styleTip = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = ColorTipText }
            };

            _stylesInitialized = true;
        }

        private void DrawSpinner(float cx, float cy)
        {
            // 4개의 점으로 구성된 스피너
            float radius = SPINNER_SIZE * 0.4f;
            float dotSize = 6f;

            for (int i = 0; i < 4; i++)
            {
                float a = (Time.time * 360f + i * 90f) * Mathf.Deg2Rad;
                float dx = Mathf.Cos(a) * radius;
                float dy = Mathf.Sin(a) * radius;
                GUI.DrawTexture(new Rect(cx + dx - dotSize / 2f, cy + dy - dotSize / 2f, dotSize, dotSize), _texWhite);
            }
        }

        private Texture2D MakeTexture(int w, int h, Color color)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, color);
            tex.Apply();
            return tex;
        }
    }
}