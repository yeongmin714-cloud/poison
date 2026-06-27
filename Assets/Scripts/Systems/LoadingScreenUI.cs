using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// G3-11: 향상된 로딩 화면 UI.
    /// 그라디언트 배경, 골드 로고, 부드러운 진행바, 카테고리별 팁, 회전 링 스피너.
    /// LoadingManager의 IsLoading 상태를 감시하여 풀스크린 로딩 화면을 표시합니다.
    /// DontDestroyOnLoad로 씬 전환 간 유지됩니다.
    /// </summary>
    [RequireComponent(typeof(LoadingManager))]
    public class LoadingScreenUI : MonoBehaviour
    {
        private LoadingManager _manager;

        // ===== 레이아웃 상수 =====
        private const float LOGO_FONT_SIZE = 36f;
        private const float SUBTITLE_FONT_SIZE = 14f;
        private const float SPINNER_SIZE = 48f;
        private const float BAR_WIDTH = 400f;
        private const float BAR_HEIGHT = 20f;
        private const float BAR_BORDER = 2f;
        private const float TIP_WIDTH = 600f;
        private const float TIP_LINE_HEIGHT = 50f;
        private const float LERP_SPEED = 3f; // 진행바 보간 속도

        // ===== 색상 =====
        // 그라디언트 배경 — 상단 진한 파랑 / 하단 진한 네이비
        private static readonly Color ColorBgTop = new Color(0.02f, 0.02f, 0.08f);
        private static readonly Color ColorBgBottom = new Color(0.05f, 0.02f, 0.12f);

        // 로고/서브타이틀
        private static readonly Color ColorLogo = new Color(0.85f, 0.70f, 0.20f);
        private static readonly Color ColorSubtitle = new Color(0.60f, 0.60f, 0.60f);

        // 진행바
        private static readonly Color ColorBarBg = new Color(0.12f, 0.12f, 0.18f, 0.90f);
        private static readonly Color ColorBarBorder = new Color(0.85f, 0.70f, 0.20f, 0.80f);
        private static readonly Color ColorBarFillBlue = new Color(0.30f, 0.70f, 1.00f);
        private static readonly Color ColorBarFillGold = new Color(0.85f, 0.70f, 0.20f);
        private static readonly Color ColorProgressText = new Color(0.85f, 0.85f, 0.85f);

        // 스피너
        private static readonly Color ColorSpinner = new Color(0.85f, 0.70f, 0.20f, 0.85f);

        // 팁
        private static readonly Color ColorTipCategory = new Color(0.85f, 0.70f, 0.20f, 0.90f);
        private static readonly Color ColorTipText = new Color(0.75f, 0.75f, 0.75f, 0.85f);
        private static readonly Color ColorTipDivider = new Color(0.60f, 0.50f, 0.20f, 0.40f);

        // ===== 카테고리별 아이콘 =====
        private static readonly string[] CategoryIcons = { "🎮", "⚔️", "🧠", "📖" };
        private static readonly string[] CategoryLabels = { "Gameplay", "Combat", "Strategy", "Lore" };

        // ===== 상태 =====
        private float _animatedProgress; // 부드럽게 보간된 진행률
        private string _tipText1;
        private TipCategory _tipCat1;
        private string _tipText2;
        private TipCategory _tipCat2;

        // ===== 스타일 =====
        private GUIStyle _styleLogo;
        private GUIStyle _styleSubtitle;
        private GUIStyle _stylePct;
        private GUIStyle _styleTipCategory;
        private GUIStyle _styleTipText;
        private Texture2D _texWhite;
        private Texture2D _texGradient;
        private Texture2D _texRingSegment;
        private bool _stylesInitialized;

        // ===== 생명주기 =====
        private void Awake()
        {
            _manager = GetComponent<LoadingManager>();
            if (_manager == null)
                _manager = FindFirstObjectByType<LoadingManager>();

            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (_manager == null || !_manager.IsLoading)
                return;

            // 진행률 부드러운 보간
            _animatedProgress = Mathf.Lerp(_animatedProgress, _manager.Progress, Time.deltaTime * LERP_SPEED);

            // 목표값에 매우 가까우면 바로 설정
            if (Mathf.Abs(_animatedProgress - _manager.Progress) < 0.001f)
                _animatedProgress = _manager.Progress;

            // 팁이 없거나 변경되었을 때 새 팁 로드
            if (string.IsNullOrEmpty(_tipText1))
            {
                var (t1, c1, t2, c2) = TipDatabase.GetTwoRandomTips();
                _tipText1 = t1;
                _tipCat1 = c1;
                _tipText2 = t2;
                _tipCat2 = c2;
            }
        }

        // ===== OnGUI =====
        private void OnGUI()
        {
            if (_manager == null || !_manager.IsLoading)
                return;

            InitStyles();

            float sw = Screen.width;
            float sh = Screen.height;
            float cx = sw / 2f;

            var oldColor = GUI.color;
            var oldMatrix = GUI.matrix;

            // ===== 1. 그라디언트 배경 (풀스크린) =====
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _texGradient);

            // ===== 2. 로고 =====
            GUI.Label(new Rect(0, sh * 0.10f, sw, 48f), "Crusader Kingdom", _styleLogo);

            // ===== 3. 서브타이틀 =====
            GUI.Label(new Rect(0, sh * 0.10f + 46f, sw, 24f), "⚔️ 크루세이더 킹덤", _styleSubtitle);

            // ===== 4. 회전 링 스피너 =====
            DrawRingSpinner(cx, sh * 0.38f);

            // ===== 5. 진행률 텍스트 (막대 위 중앙) =====
            float barY = sh * 0.48f;
            float barX = cx - BAR_WIDTH / 2f;
            string pctText = $"{(Mathf.Clamp01(_animatedProgress) * 100f):F0}%";
            GUI.Label(new Rect(cx - 60f, barY - 28f, 120f, 22f), pctText, _stylePct);

            // ===== 6. 프로그레스 바 배경 (테두리 포함) =====
            // 테두리 (금색)
            GUI.color = ColorBarBorder;
            GUI.DrawTexture(new Rect(barX - BAR_BORDER, barY - BAR_BORDER,
                                     BAR_WIDTH + BAR_BORDER * 2f, BAR_HEIGHT + BAR_BORDER * 2f), _texWhite);
            // 배경
            GUI.color = ColorBarBg;
            GUI.DrawTexture(new Rect(barX, barY, BAR_WIDTH, BAR_HEIGHT), _texWhite);

            // ===== 7. 프로그레스 바 채움 (색상 보간: 파랑 → 금색) =====
            float fill = Mathf.Clamp01(_animatedProgress);
            Color barFillColor = Color.Lerp(ColorBarFillBlue, ColorBarFillGold, fill);
            GUI.color = barFillColor;
            GUI.DrawTexture(new Rect(barX, barY, BAR_WIDTH * fill, BAR_HEIGHT), _texWhite);
            GUI.color = oldColor;

            // ===== 8. 카테고리별 팁 2개 =====
            float tipsY = barY + BAR_HEIGHT + 40f;
            DrawTip(tipsY, cx, _tipText1, _tipCat1, 0);
            DrawTip(tipsY + TIP_LINE_HEIGHT + 8f, cx, _tipText2, _tipCat2, 1);

            // ===== 9. 팁 구분선 =====
            float dividerY = tipsY + TIP_LINE_HEIGHT + 2f;
            GUI.color = ColorTipDivider;
            GUI.DrawTexture(new Rect(cx - 100f, dividerY, 200f, 1f), _texWhite);
            GUI.color = oldColor;

            GUI.matrix = oldMatrix;
        }

        // ===== 스타일 초기화 =====
        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _texWhite = MakeTexture(1, 1, Color.white);
            _texGradient = MakeGradientTexture(4, 64, ColorBgTop, ColorBgBottom);
            _texRingSegment = MakeRingSegmentTexture(32, 10f, 14f);

            _styleLogo = new GUIStyle(GUI.skin.label)
            {
                fontSize = 36,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = ColorLogo }
            };

            _styleSubtitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = ColorSubtitle }
            };

            _stylePct = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ColorProgressText }
            };

            _styleTipCategory = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.LowerLeft,
                wordWrap = false,
                normal = { textColor = ColorTipCategory }
            };

            _styleTipText = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                normal = { textColor = ColorTipText }
            };

            _stylesInitialized = true;
        }

        // ===== 개별 팁 그리기 =====
        private void DrawTip(float y, float cx, string tipText, TipCategory category, int index)
        {
            int catIdx = (int)category;
            string icon = CategoryIcons[catIdx];
            string label = CategoryLabels[catIdx];
            string categoryStr = $"{icon} {label}";

            float left = cx - TIP_WIDTH / 2f;

            // 카테고리 라벨 (좌측 상단)
            GUI.Label(new Rect(left, y, 120f, 18f), categoryStr, _styleTipCategory);

            // 팁 텍스트 (카테고리 아래)
            float textY = y + 18f;
            GUI.Label(new Rect(left, textY, TIP_WIDTH, 32f), tipText, _styleTipText);
        }

        // ===== 회전 링 스피너 =====
        private void DrawRingSpinner(float cx, float cy)
        {
            float angleOffset = Time.time * 100f; // 초당 100도 회전
            float segmentSize = 32f;
            float half = segmentSize / 2f;

            for (int i = 0; i < 4; i++)
            {
                float angle = angleOffset + i * 90f;
                GUIUtility.RotateAroundPivot(angle, new Vector2(cx, cy));

                // 4개 세그먼트 중 회전 방향에 따라 알파값이 다른 3개만 보임
                float alpha;
                if (i == 0)
                    alpha = 1.0f; // 선두 세그먼트 — 가장 밝음
                else if (i == 1)
                    alpha = 0.6f;
                else if (i == 2)
                    alpha = 0.3f;
                else
                    alpha = 0.05f; // 꼬리 — 거의 투명

                Color c = ColorSpinner;
                c.a *= alpha;
                GUI.color = c;
                GUI.DrawTexture(new Rect(cx - half, cy - half, segmentSize, segmentSize), _texRingSegment);
                GUI.color = Color.white;

                GUI.matrix = Matrix4x4.identity;
            }
        }

        // ===== 텍스처 생성 유틸 =====
        /// <summary>단색 텍스처 생성</summary>
        private Texture2D MakeTexture(int w, int h, Color color)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, color);
            tex.Apply();
            return tex;
        }

        /// <summary>상하 그라디언트 텍스처 생성</summary>
        private Texture2D MakeGradientTexture(int w, int h, Color topColor, Color bottomColor)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                float t = y / (float)(h - 1);
                Color c = Color.Lerp(topColor, bottomColor, t);
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, c);
            }
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            return tex;
        }

        /// <summary>90도 링 세그먼트 텍스처 생성 (회전 링 스피너용)</summary>
        private Texture2D MakeRingSegmentTexture(int size, float innerRadius, float outerRadius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    // 첫 번째 사분면(0°~90°)의 링 부분만 흰색
                    if (dist >= innerRadius && dist <= outerRadius && angle >= 0f && angle <= 90f)
                        tex.SetPixel(x, y, Color.white);
                    else
                        tex.SetPixel(x, y, Color.clear);
                }
            }
            tex.Apply();
            return tex;
        }
    }
}