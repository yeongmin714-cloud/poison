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
        // 공통 색상 — [TEST21-FOLLOWUP Phase G] 중세 판타지 × Duckov 클린 파레트
        // docs/UI_DESIGN_GUIDELINES.md §1 참조 (25차 전체 UI, 위치 규칙 무변경)
        // ================================================================

        /// <summary>창 배경 — 딥 차콜 레더 반투명(28,28,28,0.88) — 미드나이트 블루에서 교체</summary>
        public static readonly Color BgColor = new Color(0.11f, 0.11f, 0.11f, 0.88f);
        /// <summary>패널 심층 — 다크 슬레이트(20,20,20,0.9) — 슬롯/내부 영역</summary>
        public static readonly Color BgDarkColor = new Color(0.078f, 0.078f, 0.078f, 0.9f);
        /// <summary>테두리 — 브론즈(140,107,63) — 이전 밝은 회백 라인 교체</summary>
        public static readonly Color BorderColor = new Color(0.55f, 0.42f, 0.25f, 0.85f);
        /// <summary>강조 테두리 — 앤틱 골드(201,162,39) — 선택/마법/희귀</summary>
        public static readonly Color BorderGoldColor = new Color(0.79f, 0.64f, 0.15f, 1f);
        /// <summary>미세 그리드 라인 — 다크 아이언(58,58,58) — 슬롯 구분</summary>
        public static readonly Color IronLineColor = new Color(0.23f, 0.23f, 0.23f, 0.6f);
        /// <summary>제목 — 양피지 화이트(245,239,224) — 이전 청백 교체</summary>
        public static readonly Color TitleColor = new Color(0.96f, 0.94f, 0.88f, 1f);
        /// <summary>강조 — 마법 블루(74,123,208) — 스탯/마나</summary>
        public static readonly Color AccentColor = new Color(0.29f, 0.48f, 0.81f, 1f);
        /// <summary>희귀 강조 — 희귀 골드(231,183,58)</summary>
        public static readonly Color RareGoldColor = new Color(0.91f, 0.72f, 0.23f, 1f);
        /// <summary>체력/위험 — 헬스 레드(200,56,56)</summary>
        public static readonly Color HealthRedColor = new Color(0.78f, 0.22f, 0.22f, 1f);
        /// <summary>딤드 오버레이</summary>
        // [P16-5 수리] 딤드 0.5 → 0.18 — 전화면 짙은 검은 커튼이 "게임이 멈췄다"로 보이는 뿌리.
        //   월드가 보이며 창 밖 상황 인지 가능해짐(모달성은 창 자체가 담당).
        public static readonly Color DimColor = new Color(0f, 0f, 0f, 0.18f);
        /// <summary>버튼 호버 — 골드(217,180,91)</summary>
        public static readonly Color HoverColor = new Color(0.85f, 0.71f, 0.36f, 1f);
        /// <summary>닫기 버튼 (빨강)</summary>
        public static readonly Color CloseBtnColor = new Color(0.7f, 0.15f, 0.15f, 0.9f);
        /// <summary>닫기 호버</summary>
        public static readonly Color CloseHoverColor = new Color(0.9f, 0.2f, 0.2f, 1f);
        /// <summary>텍스트 — 양피지 화이트</summary>
        public static readonly Color TextColor = new Color(0.96f, 0.94f, 0.88f, 1f);
        /// <summary>서브텍스트 — 뮤트 웜 그레이(185,179,166)</summary>
        public static readonly Color SubTextColor = new Color(0.72f, 0.70f, 0.65f, 1f);
        /// <summary>테두리 두께 (픽셀) — Flat 모드: 얇은 2px 라인</summary>
        public const int BorderWidth = 2;

        // ================================================================
        // 스타일 캐싱
        // ================================================================

        private static GUIStyle _titleStyle;
        private static GUIStyle _closeButtonStyle;
        private static GUIStyle _borderBoxStyle;
        private static GUIStyle _dimBoxStyle;
        private static GUIStyle _labelStyle;
        private static bool _initialized;

        // 테두리 캐싱 (OnGUI GC 방지)
        private static Texture2D _cachedBorderTex;
        private static GUIStyle _cachedBorderStyle;
        private static Texture2D _cachedDimTex;
        private static Texture2D _cachedBgTex;

        // 닫기 버튼 텍스처 캐싱 (Cleanup에서 정리 가능하도록)
        private static Texture2D _cachedCloseBtnTex;
        private static Texture2D _cachedCloseHoverTex;
        private static Texture2D _cachedCloseActiveTex;

        private static void EnsureStyles()
        {
            if (_initialized) return;

            // 텍스처 사전 생성 (EnsureStyles는 한 번만 실행됨)
            _cachedDimTex = MakeTexture(1, 1, DimColor);
            _cachedBgTex = MakeTexture(1, 1, BgColor);
            _cachedBorderTex = MakeTexture(1, 1, BorderColor);
            _cachedBorderStyle = new GUIStyle
            {
                normal = { background = _cachedBorderTex }
            };

            _titleStyle = new GUIStyle
            {
                font = UIFont.Load(), // P7-1: 한글 서포트 커스텀 폰트 (실패 시 빌트인 폴백)
                fontSize = 40,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = TitleColor }
            };

            _cachedCloseBtnTex = MakeTexture(1, 1, CloseBtnColor);
            _cachedCloseHoverTex = MakeTexture(1, 1, CloseHoverColor);
            _cachedCloseActiveTex = MakeTexture(1, 1, new Color(0.5f, 0.1f, 0.1f, 1f));

            _closeButtonStyle = new GUIStyle
            {
                font = UIFont.Load(), // P7-1
                fontSize = 52,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white, background = _cachedCloseBtnTex }
            };
            _closeButtonStyle.hover.background = _cachedCloseHoverTex;
            _closeButtonStyle.active.background = _cachedCloseActiveTex;

            _borderBoxStyle = new GUIStyle
            {
                normal = { background = _cachedBgTex }
            };

            _dimBoxStyle = new GUIStyle
            {
                normal = { background = _cachedDimTex }
            };

            _labelStyle = new GUIStyle
            {
                font = UIFont.Load(), // P7-1
                fontSize = 40,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = TextColor },
                padding = new RectOffset(16, 16, 4, 4)
            };

            _initialized = true;
        }

        // ================================================================
        // 텍스처 헬퍼 (중복 생성 방지)
        // ================================================================

        /// <summary>
        /// 단색 텍스처를 생성합니다. OnGUI 진입 전 EnsureStyles()에서 캐싱 완료.
        /// 외부 호출 시에도 생성된 텍스처는 Destroy되지 않으므로 주의.
        /// </summary>
        public static Texture2D MakeTexture(int width, int height, Color color)
        {
            var tex = new Texture2D(width, height);
            tex.hideFlags = HideFlags.HideAndDontSave;
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
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "", _dimBoxStyle);
        }

        /// <summary>
        /// 창 배경 (어두운 + 골드 테두리)을 그립니다.
        /// </summary>
        public static void DrawWindowBackground(Rect rect)
        {
            EnsureStyles();

            int bw = BorderWidth;
            var borderRect = new Rect(rect.x - bw, rect.y - bw,
                rect.width + bw * 2, rect.height + bw * 2);

            // 왼쪽
            GUI.Box(new Rect(borderRect.x, borderRect.y, bw, borderRect.height), "", _cachedBorderStyle);
            // 오른쪽
            GUI.Box(new Rect(borderRect.x + borderRect.width - bw, borderRect.y, bw, borderRect.height), "", _cachedBorderStyle);
            // 위 (좌우 테두리와 겹치지 않도록)
            GUI.Box(new Rect(borderRect.x + bw, borderRect.y, borderRect.width - bw * 2, bw), "", _cachedBorderStyle);
            // 아래 (좌우 테두리와 겹치지 않도록)
            GUI.Box(new Rect(borderRect.x + bw, borderRect.y + borderRect.height - bw, borderRect.width - bw * 2, bw), "", _cachedBorderStyle);

            // 내부 배경
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
            var prevColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.white;
            bool result = GUI.Button(new Rect(btnX, btnY, btnSize, btnSize), "X", _closeButtonStyle);
            GUI.backgroundColor = prevColor;
            return result;
        }

        /// <summary>
        /// 기본 라벨 스타일을 반환합니다. EnsureStyles()를 통해 지연 초기화됩니다.
        /// </summary>
        public static GUIStyle LabelStyle
        {
            get { EnsureStyles(); return _labelStyle; }
        }

        /// <summary>
        /// 제목 스타일을 반환합니다. EnsureStyles()를 통해 지연 초기화됩니다.
        /// </summary>
        public static GUIStyle TitleStyle
        {
            get { EnsureStyles(); return _titleStyle; }
        }

        // ================================================================
        // 리소스 정리
        // ================================================================

        /// <summary>
        /// 생성된 모든 텍스처와 GUIStyle 리소스를 정리합니다.
        /// MonoBehaviour.OnDestroy() 또는 씬 전환 시 호출하세요.
        /// </summary>
        public static void Cleanup()
        {
            if (!_initialized) return;

            Object.Destroy(_cachedBorderTex);
            Object.Destroy(_cachedDimTex);
            Object.Destroy(_cachedBgTex);
            Object.Destroy(_cachedCloseBtnTex);
            Object.Destroy(_cachedCloseHoverTex);
            Object.Destroy(_cachedCloseActiveTex);

            _cachedBorderTex = null;
            _cachedDimTex = null;
            _cachedBgTex = null;
            _cachedCloseBtnTex = null;
            _cachedCloseHoverTex = null;
            _cachedCloseActiveTex = null;
            _cachedBorderStyle = null;

            _titleStyle = null;
            _closeButtonStyle = null;
            _borderBoxStyle = null;
            _dimBoxStyle = null;
            _labelStyle = null;

            _initialized = false;
        }
    }
}