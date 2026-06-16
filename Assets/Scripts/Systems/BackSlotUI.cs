using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C8-32: Back 슬롯 HUD — 장착된 가스 분사기 상세 정보를 표시합니다.
    /// C8-34: 재장전 프로그레스바 + 가스 게이지 개선 + 깜빡임 + 키 안내
    /// 화면 우측 상단에 등급, 남은 가스 시간, 분사 범위를 표시합니다.
    /// GasSprayerController가 동일 GameObject에 있어야 합니다.
    /// </summary>
    [RequireComponent(typeof(GasSprayerController))]
    public class BackSlotUI : MonoBehaviour
    {
        private GasSprayerController _controller;

        // ===== 스타일 상수 =====
        private const float HUD_WIDTH = 280f;
        private const float HUD_HEIGHT = 150f;
        private const float HUD_MARGIN = 10f;

        // ===== 색상 =====
        private static readonly Color ColorBg = new Color(0.18f, 0.13f, 0.16f, 0.85f);
        private static readonly Color ColorBorder = new Color(0.80f, 0.60f, 0.20f, 0.8f);
        private static readonly Color ColorText = new Color(0.92f, 0.88f, 0.80f, 1f);
        private static readonly Color ColorGasLow = new Color(1f, 0.3f, 0.3f, 1f);
        private static readonly Color ColorGasMid = new Color(1f, 0.8f, 0.2f, 1f);
        private static readonly Color ColorGasFull = new Color(0.3f, 1f, 0.3f, 1f);
        private static readonly Color ColorSpraying = new Color(0.2f, 0.8f, 1f, 1f);
        private static readonly Color ColorReload = new Color(1f, 0.7f, 0.1f, 1f);
        private static readonly Color ColorHint = new Color(0.5f, 0.8f, 1f, 1f);

        // ===== 캐시 =====
        private GUIStyle _styleBox;
        private GUIStyle _styleTitle;
        private GUIStyle _styleDetail;
        private GUIStyle _styleSpraying;
        private GUIStyle _styleReload;
        private GUIStyle _styleHint;
        private Texture2D _texWhite;
        private bool _stylesInitialized;

        // ===== C8-34: 재장전 완료 안내 타이머 =====
        private float _reloadCompleteTimer;
        private const float RELOAD_COMPLETE_DURATION = 3.0f;

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

            // C8-34: 재장전 완료 이벤트 구독
            if (_controller != null)
            {
                _controller.OnReloadCompleted += OnReloadCompleted;
            }
        }

        private void OnDestroy()
        {
            if (_controller != null)
            {
                _controller.OnReloadCompleted -= OnReloadCompleted;
            }
        }

        /// <summary>C8-34: 재장전 완료 시 안내 메시지 표시</summary>
        private void OnReloadCompleted()
        {
            _reloadCompleteTimer = RELOAD_COMPLETE_DURATION;
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

            // C8-34: 재장전 완료 안내 타이머 감소
            if (_reloadCompleteTimer > 0f)
            {
                _reloadCompleteTimer -= Time.deltaTime;
            }
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

            // C8-34: 재장전 중 스타일
            _styleReload = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ColorReload },
                padding = new RectOffset(0, 0, 0, 0)
            };

            // C8-34: 키 안내 스타일
            _styleHint = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = ColorHint },
                padding = new RectOffset(0, 0, 0, 0)
            };

            _stylesInitialized = true;
        }

        private void DrawHudElement(float x, float y)
        {
            var data = _controller.GetCurrentSprayerData();
            float remaining = _controller.CurrentSprayTimeRemaining;
            bool isSpraying = _controller.IsSpraying;
            bool isReloading = _controller.IsReloading;

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
            float barHeight = 8f; // C8-34: 4px → 8px
            float currentY = y + padding;

            // 배경
            float actualHeight = CalculateRequiredHeight(isReloading, data.isUnlimited);
            GUI.Box(new Rect(x, y, HUD_WIDTH, actualHeight), "", _styleBox);

            // 상단 테두리 라인
            var oldColor = GUI.color;
            GUI.color = ColorBorder;
            GUI.DrawTexture(new Rect(x, y, HUD_WIDTH, 2), _texWhite);
            GUI.color = oldColor;

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

            // 가스 게이지 바 (시간 기반) — C8-34: 높이 8px, 10% 미만 깜빡임
            if (!data.isUnlimited && data.maxSprayTime > 0f)
            {
                float ratio = Mathf.Clamp01(remaining / data.maxSprayTime);
                float barWidth = HUD_WIDTH - 24f;
                float barY = currentY + lineHeight - 2f;

                // 게이지 색상 결정
                Color barColor = remaining < 5f ? ColorGasLow : (remaining < 15f ? ColorGasMid : ColorGasFull);

                // C8-34: 가스 10% 미만 깜빡임 효과 (재장전 중이 아닐 때)
                if (ratio < 0.1f && !isReloading)
                {
                    bool visible = Time.time % 0.5f < 0.25f;
                    barColor = visible ? ColorGasLow : new Color(0f, 0f, 0f, 0f);
                }

                // 배경 바
                GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
                GUI.DrawTexture(new Rect(x + 6, barY, barWidth, barHeight), _texWhite);

                // 게이지 바
                GUI.color = barColor;
                GUI.DrawTexture(new Rect(x + 6, barY, barWidth * ratio, barHeight), _texWhite);
                GUI.color = oldColor;

                currentY = barY + barHeight + 2f;
            }

            // ===== C8-34: 재장전 섹션 =====
            if (isReloading)
            {
                float reloadTime = GasSprayerManager.GetReloadTime(_controller.CurrentGrade);
                float remainingReloadTime = _controller.ReloadTimeRemaining;
                float reloadRatio = reloadTime > 0f ? Mathf.Clamp01(1f - (remainingReloadTime / reloadTime)) : 1f;

                float reloadBarWidth = HUD_WIDTH - 24f;
                float reloadBarHeight = 14f;
                float reloadBarY = currentY + 2f;

                // "재장전 중..." 텍스트
                GUI.color = ColorReload;
                GUI.Label(new Rect(x + 6, currentY, HUD_WIDTH - 12, lineHeight),
                    $"🔄 재장전 중... ({remainingReloadTime:F1}s)", _styleReload);
                currentY = reloadBarY + 2f;

                // 재장전 프로그레스바 배경
                GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
                GUI.DrawTexture(new Rect(x + 6, currentY, reloadBarWidth, reloadBarHeight), _texWhite);

                // 재장전 프로그레스바 채움
                GUI.color = ColorReload;
                GUI.DrawTexture(new Rect(x + 6, currentY, reloadBarWidth * reloadRatio, reloadBarHeight), _texWhite);
                GUI.color = oldColor;

                currentY += reloadBarHeight + 4f;
            }

            // ===== C8-34: 키 입력 안내 =====
            if (isReloading)
            {
                GUI.Label(new Rect(x + 6, currentY, HUD_WIDTH - 12, lineHeight),
                    "재장전 완료 후 우클릭하여 분사", _styleHint);
                currentY += lineHeight;
            }
            else if (_reloadCompleteTimer > 0f)
            {
                // 재장전 완료 안내
                GUI.color = ColorHint;
                GUI.Label(new Rect(x + 6, currentY, HUD_WIDTH - 12, lineHeight),
                    "✅ 가스 재장전 완료! 우클릭하여 분사", _styleHint);
                GUI.color = oldColor;
                currentY += lineHeight;
            }
            else if (remaining > 0f && !isSpraying)
            {
                GUI.Label(new Rect(x + 6, currentY, HUD_WIDTH - 12, lineHeight),
                    "🖱 우클릭: 분사 시작", _styleHint);
                currentY += lineHeight;
            }
        }

        /// <summary>C8-34: 현재 상태에 필요한 HUD 높이 계산</summary>
        private float CalculateRequiredHeight(bool isReloading, bool isUnlimited)
        {
            float baseHeight = 8f + (4 * 18f) + 4f; // padding + 4 lines + bottom padding
            if (!isUnlimited)
            {
                baseHeight += 10f; // gas bar (8px + 2px spacing)
            }
            if (isReloading)
            {
                baseHeight += 18f + 18f + 4f; // reload label + progress bar + spacing
                baseHeight += 18f; // hint line
            }
            else
            {
                baseHeight += 18f; // hint line (always show hint)
            }
            return baseHeight;
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