using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C8-32: Back 슬롯 HUD — 장착된 가스 분사기 상세 정보를 표시합니다.
    /// 화면 우측 상단에 등급, 남은 가스 시간, 분사 범위를 표시합니다.
    /// GasSprayerController가 동일 GameObject에 있어야 합니다.
    /// </summary>
    [RequireComponent(typeof(GasSprayerController))]
    public class BackSlotUI : MonoBehaviour
    {
        private GasSprayerController _controller;

        // ===== 스타일 상수 =====
        private const float HUD_WIDTH = 260f;
        private const float HUD_HEIGHT = 80f;
        private const float HUD_MARGIN = 10f;

        // ===== 색상 =====
        private static readonly Color ColorBg = new Color(0.18f, 0.13f, 0.16f, 0.85f);
        private static readonly Color ColorBorder = new Color(0.80f, 0.60f, 0.20f, 0.8f);
        private static readonly Color ColorText = new Color(0.92f, 0.88f, 0.80f, 1f);
        private static readonly Color ColorGasLow = new Color(1f, 0.3f, 0.3f, 1f);
        private static readonly Color ColorGasMid = new Color(1f, 0.8f, 0.2f, 1f);
        private static readonly Color ColorGasFull = new Color(0.3f, 1f, 0.3f, 1f);
        private static readonly Color ColorSpraying = new Color(0.2f, 0.8f, 1f, 1f);

        // ===== 캐시 =====
        private GUIStyle _styleBox;
        private GUIStyle _styleTitle;
        private GUIStyle _styleDetail;
        private GUIStyle _styleSpraying;
        private Texture2D _texWhite;
        private bool _stylesInitialized;

        // ===== 등급별 표시명 =====
        private static readonly string[] GradeDisplayNames =
        {
            "나무 (Wood)", "돌 (Stone)", "철 (Iron)",
            "강화 (Reinforced)", "특수합금 (Special Alloy)"
        };

        private void Awake()
        {
            _controller = GetComponent<GasSprayerController>();
            if (_controller == null)
            {
                _controller = FindObjectOfType<GasSprayerController>();
            }
        }

        private void Start()
        {
            if (_controller == null)
            {
                _controller = GasSprayerController.Instance;
            }
        }

        private void OnGUI()
        {
            if (_controller == null || !_controller.IsEquipped)
                return;

            InitStyles();

            // 우측 상단에 표시
            float x = Screen.width - HUD_WIDTH - HUD_MARGIN;
            float y = HUD_MARGIN;

            DrawHudElement(x, y);
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _texWhite = MakeTexture(1, 1, Color.white);

            // 배경 박스
            _styleBox = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTexture(1, 1, ColorBg), textColor = ColorText },
                border = new RectOffset(2, 2, 2, 2),
                padding = new RectOffset(6, 6, 4, 4),
                margin = new RectOffset(0, 0, 0, 0),
                fontSize = 11,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true
            };

            // 제목 라벨 (분사기 이름)
            _styleTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = ColorText },
                padding = new RectOffset(0, 0, 0, 0)
            };

            // 세부 정보 라벨
            _styleDetail = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.78f, 0.78f, 0.78f, 1f) },
                padding = new RectOffset(0, 0, 0, 0)
            };

            // 분사 중 표시 스타일
            _styleSpraying = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = ColorSpraying },
                padding = new RectOffset(0, 0, 0, 0)
            };

            _stylesInitialized = true;
        }

        private void DrawHudElement(float x, float y)
        {
            var data = _controller.GetCurrentSprayerData();
            float remaining = _controller.CurrentSprayTimeRemaining;
            bool isSpraying = _controller.IsSpraying;

            // 등급 인덱스
            int gradeIdx = (int)data.grade;
            string gradeDisplay = gradeIdx >= 0 && gradeIdx < GradeDisplayNames.Length
                ? GradeDisplayNames[gradeIdx]
                : data.grade.ToString();

            // 남은 시간 표시
            string timeDisplay;
            Color timeColor = ColorText;
            if (data.isUnlimited)
            {
                timeDisplay = "∞ (무제한)";
                timeColor = ColorGasFull;
            }
            else if (remaining <= 0f)
            {
                timeDisplay = "0.0s (소진)";
                timeColor = ColorGasLow;
            }
            else
            {
                timeDisplay = $"{remaining:F1}s";
                timeColor = remaining < 5f ? ColorGasLow : (remaining < 15f ? ColorGasMid : ColorGasFull);
            }

            // 범위 표시
            string rangeDisplay = $"{data.sprayRange:F1}m";

            // 분사 상태
            string sprayStatus = isSpraying ? "▶ 분사 중" : "";

            // ===== 레이아웃 =====
            float lineHeight = 18f;
            float padding = 4f;

            // 배경
            GUI.Box(new Rect(x, y, HUD_WIDTH, HUD_HEIGHT), "", _styleBox);

            // 상단 테두리 라인
            var oldColor = GUI.color;
            GUI.color = ColorBorder;
            GUI.DrawTexture(new Rect(x, y, HUD_WIDTH, 2), _texWhite);
            GUI.color = oldColor;

            float currentY = y + padding;

            // 라인 1: 분사기 이름 (좌) + 분사 상태 (우)
            GUI.Label(new Rect(x + 6, currentY, HUD_WIDTH * 0.6f - 6, lineHeight),
                $"🎒 {_controller.EquippedSprayerName}", _styleTitle);
            if (isSpraying)
            {
                GUI.color = ColorSpraying;
                GUI.Label(new Rect(x + HUD_WIDTH * 0.6f, currentY, HUD_WIDTH * 0.4f - 6, lineHeight),
                    sprayStatus, _styleSpraying);
                GUI.color = oldColor;
            }
            currentY += lineHeight;

            // 라인 2: 등급 + 범위
            GUI.Label(new Rect(x + 6, currentY, HUD_WIDTH - 12, lineHeight),
                $"등급: {gradeDisplay}  |  사거리: {rangeDisplay}", _styleDetail);
            currentY += lineHeight;

            // 라인 3: 남은 가스 시간
            GUI.color = timeColor;
            GUI.Label(new Rect(x + 6, currentY, HUD_WIDTH - 12, lineHeight),
                $"가스: {timeDisplay}", _styleDetail);
            GUI.color = oldColor;

            // 가스 게이지 바 (시간 기반)
            if (!data.isUnlimited && data.maxSprayTime > 0f)
            {
                float ratio = Mathf.Clamp01(remaining / data.maxSprayTime);
                float barWidth = HUD_WIDTH - 24f;
                float barHeight = 4f;
                float barY = currentY + lineHeight - 2f;

                // 배경 바
                GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
                GUI.DrawTexture(new Rect(x + 6, barY, barWidth, barHeight), _texWhite);

                // 게이지 바
                GUI.color = remaining < 5f ? ColorGasLow : (remaining < 15f ? ColorGasMid : ColorGasFull);
                GUI.DrawTexture(new Rect(x + 6, barY, barWidth * ratio, barHeight), _texWhite);
                GUI.color = oldColor;
            }
        }

        /// <summary>1x1 텍스처 생성</summary>
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