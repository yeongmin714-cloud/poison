using UnityEngine;

namespace ProjectName.UI
{
    /// <summary>
    /// G3-05: UI 통일 스타일 매니저.
    /// 모든 창(12개)에 공통 스타일을 제공합니다.
    /// 정적 클래스로 인스턴스 불필요.
    /// </summary>
    public static class UIStyleManager
    {
        // ================================================================
        // 공통 색상
        // ================================================================

        /// <summary>창 배경 (어두움)</summary>
        public static readonly Color BgColor = new Color(0f, 0f, 0f, 0.88f);
        /// <summary>골드 테두리</summary>
        public static readonly Color BorderColor = new Color(0.85f, 0.65f, 0.15f, 0.8f);
        /// <summary>제목 색상 (골드)</summary>
        public static readonly Color TitleColor = new Color(0.9f, 0.7f, 0.3f, 1f);
        /// <summary>딤드 오버레이</summary>
        public static readonly Color DimColor = new Color(0f, 0f, 0f, 0.5f);
        /// <summary>버튼 호버 강조</summary>
        public static readonly Color HoverColor = new Color(0.3f, 0.5f, 0.7f, 1f);
        /// <summary>닫기 버튼 (빨강)</summary>
        public static readonly Color CloseBtnColor = new Color(0.7f, 0.15f, 0.15f, 0.9f);
        /// <summary>닫기 호버</summary>
        public static readonly Color CloseHoverColor = new Color(0.9f, 0.2f, 0.2f, 1f);
        /// <summary>텍스트</summary>
        public static readonly Color TextColor = Color.white;
        /// <summary>서브텍스트 (회색)</summary>
        public static readonly Color SubTextColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        /// <summary>테두리 두께 (픽셀)</summary>
        public const int BorderWidth = 4;

        // ================================================================
        // 스타일 캐싱
        // ================================================================

        private static GUIStyle _titleStyle;
        private static GUIStyle _closeButtonStyle;
        private static GUIStyle _borderBoxStyle;
        private static GUIStyle _dimBoxStyle;
        private static GUIStyle _labelStyle;
        private static bool _initialized;

        private static void EnsureStyles()
        {
            if (_initialized) return;

            _titleStyle = new GUIStyle
                        {
                            fontSize = 60,
                            fontStyle = FontStyle.Bold,
                            alignment = TextAnchor.MiddleCenter,
                            normal = { textColor = TitleColor }
                        };

                        _closeButtonStyle = new GUIStyle
                        {
                            fontSize = 52,
                            fontStyle = FontStyle.Bold,
                            alignment = TextAnchor.MiddleCenter,
                            normal = { textColor = Color.white, background = MakeTexture(1, 1, CloseBtnColor) }
                        };
                        _closeButtonStyle.hover.background = MakeTexture(1, 1, CloseHoverColor);
                        _closeButtonStyle.active.background = MakeTexture(1, 1, new Color(0.5f, 0.1f, 0.1f, 1f));

                        _borderBoxStyle = new GUIStyle
                        {
                            normal = { background = MakeTexture(1, 1, BgColor) },
                            border = new RectOffset(BorderWidth, BorderWidth, BorderWidth, BorderWidth)
                        };

                        _dimBoxStyle = new GUIStyle
                        {
                            normal = { background = MakeTexture(1, 1, DimColor) }
                        };

                        _labelStyle = new GUIStyle
                        {
                            fontSize = 40,
                            fontStyle = FontStyle.Normal,
                            alignment = TextAnchor.MiddleLeft,
                            normal = { textColor = TextColor },
                            padding = new RectOffset(16, 16, 4, 4)
                        };

            _initialized = true;
        }

        // ================================================================
        // 텍스처 헬퍼 (중복 방지)
        // ================================================================

        private static Texture2D _cachedDimTex;
        private static Texture2D _cachedBgTex;

        /// <summary>
        /// 단색 텍스처를 생성합니다. 캐싱 지원.
        /// </summary>
        public static Texture2D MakeTexture(int width, int height, Color color)
        {
            var tex = new Texture2D(width, height);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    tex.SetPixel(x, y, color);
            tex.Apply();
            return tex;
        }

        // ================================================================
        // 공통 드로잉 메서드
        // ================================================================

        /// <summary>
        /// 화면 전체 딤드 오버레이를 그립니다.
        /// </summary>
        public static void DrawDimOverlay()
        {
            EnsureStyles();
            if (_cachedDimTex == null)
                _cachedDimTex = MakeTexture(1, 1, DimColor);
            _dimBoxStyle.normal.background = _cachedDimTex;
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "", _dimBoxStyle);
        }

        /// <summary>
        /// 창 배경 (어두운 + 골드 테두리)을 그립니다.
        /// </summary>
        public static void DrawWindowBackground(Rect rect)
        {
            EnsureStyles();

            // 테두리용 박스 (골드)
            var borderRect = new Rect(rect.x - BorderWidth, rect.y - BorderWidth,
                rect.width + BorderWidth * 2, rect.height + BorderWidth * 2);

            // 골드 테두리 그리기
            var borderTex = MakeTexture(1, 1, BorderColor);
            var borderStyle = new GUIStyle { normal = { background = borderTex } };
            // 왼쪽
            GUI.Box(new Rect(borderRect.x, borderRect.y, BorderWidth, borderRect.height), "", borderStyle);
            // 오른쪽
            GUI.Box(new Rect(borderRect.x + borderRect.width - BorderWidth, borderRect.y, BorderWidth, borderRect.height), "", borderStyle);
            // 위
            GUI.Box(new Rect(borderRect.x, borderRect.y, borderRect.width, BorderWidth), "", borderStyle);
            // 아래
            GUI.Box(new Rect(borderRect.x, borderRect.y + borderRect.height - BorderWidth, borderRect.width, BorderWidth), "", borderStyle);

            // 내부 배경
            if (_cachedBgTex == null)
                _cachedBgTex = MakeTexture(1, 1, BgColor);
            _borderBoxStyle.normal.background = _cachedBgTex;
            GUI.Box(rect, "", _borderBoxStyle);
        }

        /// <summary>
        /// 창 제목을 그립니다.
        /// </summary>
        public static void DrawTitle(Rect windowRect, string title)
        {
            EnsureStyles();
            GUI.Label(new Rect(windowRect.x, windowRect.y + 20, windowRect.width, 70), title, _titleStyle);
        }

        /// <summary>
        /// 닫기 버튼(X)을 그리고 클릭 여부를 반환합니다.
        /// </summary>
        public static bool DrawCloseButton(Rect windowRect)
        {
            EnsureStyles();
            int btnSize = 56;
            int btnX = (int)(windowRect.x + windowRect.width - btnSize - 10);
            int btnY = (int)(windowRect.y + 10);
            GUI.backgroundColor = CloseBtnColor;
            return GUI.Button(new Rect(btnX, btnY, btnSize, btnSize), "X", _closeButtonStyle);
        }

        /// <summary>
        /// 기본 라벨 스타일을 반환합니다.
        /// </summary>
        public static GUIStyle LabelStyle
        {
            get { EnsureStyles(); return _labelStyle; }
        }

        /// <summary>
        /// 제목 스타일을 반환합니다.
        /// </summary>
        public static GUIStyle TitleStyle
        {
            get { EnsureStyles(); return _titleStyle; }
        }
    }
}