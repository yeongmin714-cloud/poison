using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// Shared GitHub-dark / Fluent design tokens and small UI Toolkit styling helpers.
    /// Existing windows can continue to use Theme.uss; helpers here are opt-in for new
    /// panels and incremental window migrations.
    /// </summary>
    public static class UTKTheme
    {
        // ===== GitHub-dark / Fluent dark tokens =====
        /// <summary>Screen/backdrop base. #0B0E14</summary>
        public static readonly Color BgBase = Hex(0x0B0E14);
        /// <summary>Main panel. #161B22</summary>
        public static readonly Color Panel = Hex(0x161B22);
        /// <summary>Secondary panel/title/card. #21262D</summary>
        public static readonly Color PanelSub = Hex(0x21262D);
        /// <summary>Accent. #58A6FF</summary>
        public static readonly Color Accent = Hex(0x58A6FF);
        /// <summary>Rare/active/gold. #E3B341</summary>
        public static readonly Color Gold = Hex(0xE3B341);
        /// <summary>Primary text. #F0F6FC</summary>
        public static readonly Color TextMain = Hex(0xF0F6FC);
        /// <summary>Secondary text. #8B949E</summary>
        public static readonly Color TextSub = Hex(0x8B949E);
        /// <summary>Border/divider. #2E343D</summary>
        public static readonly Color Stroke = Hex(0x2E343D);
        /// <summary>Danger. #F85149</summary>
        public static readonly Color Danger = Hex(0xF85149);
        /// <summary>Success. #3FB950</summary>
        public static readonly Color Success = Hex(0x3FB950);
        /// <summary>Healthy HP fill; shares the canonical success-green token.</summary>
        public static readonly Color Health = Success;
        /// <summary>Warning. #D29922</summary>
        public static readonly Color Warn = Hex(0xD29922);
        public static readonly Color DangerHover = Hex(0xDA3633);
        public static readonly Color AccentHover = Hex(0x79C0FF);
        public static readonly Color SuccessHover = Hex(0x57AB5A);

        public const int RadiusMain = 8;
        public const int RadiusSub = 6;
        public const int RadiusBadge = 4;
        public const float PanelBorderWidth = 1f;
        public const float PanelPadding = 10f;

        /// <summary>
        /// Creates a consistently styled panel suitable for cards and composite layouts.
        /// Use ApplyPanelStyle when styling an existing VisualElement instead.
        /// </summary>
        public static VisualElement CreatePanel(string name = null, bool secondary = false)
        {
            var panel = new VisualElement();
            if (!string.IsNullOrEmpty(name))
                panel.name = name;
            panel.AddToClassList("utk-panel");
            if (secondary)
                panel.AddToClassList("utk-panel-sub");
            ApplyPanelStyle(panel, secondary);
            return panel;
        }

        /// <summary>Applies shared panel tokens without requiring a stylesheet class.</summary>
        public static void ApplyPanelStyle(VisualElement panel, bool secondary = false)
        {
            if (panel == null)
                return;

            Color fill = secondary ? PanelSub : Panel;
            panel.style.backgroundColor = new StyleColor(fill);
            panel.style.color = new StyleColor(TextMain);
            panel.style.borderTopWidth = PanelBorderWidth;
            panel.style.borderBottomWidth = PanelBorderWidth;
            panel.style.borderLeftWidth = PanelBorderWidth;
            panel.style.borderRightWidth = PanelBorderWidth;
            panel.style.borderTopColor = new StyleColor(Stroke);
            panel.style.borderBottomColor = new StyleColor(Stroke);
            panel.style.borderLeftColor = new StyleColor(Stroke);
            panel.style.borderRightColor = new StyleColor(Stroke);
            panel.style.borderTopLeftRadius = secondary ? RadiusSub : RadiusMain;
            panel.style.borderTopRightRadius = secondary ? RadiusSub : RadiusMain;
            panel.style.borderBottomLeftRadius = secondary ? RadiusSub : RadiusMain;
            panel.style.borderBottomRightRadius = secondary ? RadiusSub : RadiusMain;
            panel.style.paddingLeft = PanelPadding;
            panel.style.paddingRight = PanelPadding;
            panel.style.paddingTop = PanelPadding;
            panel.style.paddingBottom = PanelPadding;
        }

        /// <summary>Shared theme chrome for windows explicitly opting into code styling.</summary>
        public static void ApplyWindowChrome(VisualElement window, VisualElement titleBar, VisualElement content)
        {
            if (window == null)
                return;

            window.style.backgroundColor = new StyleColor(Panel);
            window.style.color = new StyleColor(TextMain);
            window.style.borderTopWidth = PanelBorderWidth;
            window.style.borderBottomWidth = PanelBorderWidth;
            window.style.borderLeftWidth = PanelBorderWidth;
            window.style.borderRightWidth = PanelBorderWidth;
            window.style.borderTopColor = new StyleColor(Stroke);
            window.style.borderBottomColor = new StyleColor(Stroke);
            window.style.borderLeftColor = new StyleColor(Stroke);
            window.style.borderRightColor = new StyleColor(Stroke);
            window.style.borderTopLeftRadius = RadiusMain;
            window.style.borderTopRightRadius = RadiusMain;
            window.style.borderBottomLeftRadius = RadiusMain;
            window.style.borderBottomRightRadius = RadiusMain;

            if (titleBar != null)
            {
                titleBar.style.backgroundColor = new StyleColor(PanelSub);
                titleBar.style.borderBottomWidth = PanelBorderWidth;
                titleBar.style.borderBottomColor = new StyleColor(Stroke);
            }
            if (content != null)
                content.style.color = new StyleColor(TextMain);
        }

        private static Color Hex(uint rgb)
        {
            return new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 0xFF);
        }

        // =====================================================================
        //  [Figma 타이포 토큰 — 2026-10-08 저장 트리 실측] 1920 내보내기의 진실치.
        //  모든 폰트는 1.2× 패밀리로 일관(21.6/16.8/15.6/14.4/13.2/12/26.4/38.4) — 기존 근사값(20/15/13/11) 전면 교체 대상.
        //  폰트 패밀리는 달라질 수 있으나 "크기/웨이트/색"은 그대로 쓴다.
        //  =====================================================================
        public const float FontTitleLarge = 24f;      // 패널 제목(스테이터스 '상태 정보' 등) — 700
        public const float FontTitle = 21.6f;         // 패널 제목(병사/창고/요리 등) — 700
        public const float FontDisplayName = 26.4f;   // 상세 대표 이름(코드네임 등) — 700
        public const float FontBigNumber = 38.4f;     // LEVEL 큰 숫자 — 800
        public const float FontCardName = 16.8f;      // 카드/스탯 값 이름 — 700
        public const float FontRowLabel = 15.6f;      // 행 라벨(스탯 라벨/푸터 좌) — 500/400
        public const float FontRowValue = 16.8f;      // 행 값(스탯 값) — 700
        public const float FontTab = 14.4f;           // 탭/카드 Lv — 700
        public const float FontBody = 14.4f;          // 본문(직책 등) — 400
        public const float FontSubtitle = 13.2f;      // 영문 서브타이틀 — 500
        public const float FontBadge = 12f;           // 배지/카운트/급여 — 700

        /// <summary>라벨을 Figma 타이포 토큰으로 스타일링 (색/정렬은 호출부). 폰트 패밀리는 Theme 공용 유지.
        /// ⚠ UI Toolkit FontStyle은 Normal/Bold만 존재 — Figma 웨이트 600 이상 = Bold, 미만 = Normal 매핑.</summary>
        public static void StyleFigmaText(Label label, float fontSize, int weight, Color color,
            TextAnchor align = TextAnchor.MiddleLeft)
        {
            if (label == null) return;
            label.style.fontSize = fontSize;
            label.style.unityFontStyleAndWeight = weight >= 600 ? FontStyle.Bold : FontStyle.Normal;
            label.style.color = new StyleColor(color);
            label.style.unityTextAlign = align;
        }

        // =====================================================================
        //  [Figma 글래스 패널 — 69:4/84:4 등 전 패널 공통 실측] 글래스모피즘.
        //  fill #161B22@0.7 + stroke #30363D@0.5 1.2px + r14.4 + DROP_SHADOW(y19.2/blur38.4/25%).
        //  ⚠ UI Toolkit 런타임은 BACKGROUND_BLUR 미지원 — 0.7 알파(배경 30% 비침)로 유사 효과만 제공.
        //  =====================================================================
        // 사용자 조정(2026-10-08): 피그마 0.7보다 "조금 더 불투명" — 0.85. 배경 비침은 15%로 축소.
        public static readonly Color GlassPanelFill = new Color32(0x16, 0x1B, 0x22, 0xD9);   // 0.85 ≈ D9/255
        public static readonly Color GlassPanelStroke = new Color32(0x30, 0x36, 0x3D, 0x80); // 0.5
        public const float GlassStrokeWidth = 1.2f;
        public const float GlassRadius = 14.4f;
        public const float GlassShadowY = 19.2f;
        public const float GlassShadowBlur = 38.4f;
        public const float GlassShadowAlpha = 0.25f;

        /// <summary>Figma 글래스 패널 스타일 — 창 본체/복합 패널 루트에 적용. 기존 ApplyPanelStyle과 병용 금지(덮어씀).</summary>
        public static void ApplyFigmaGlass(VisualElement panel)
        {
            if (panel == null) return;
            panel.style.backgroundColor = new StyleColor(GlassPanelFill);
            panel.style.color = new StyleColor(TextMain);
            panel.style.borderTopWidth = GlassStrokeWidth;
            panel.style.borderBottomWidth = GlassStrokeWidth;
            panel.style.borderLeftWidth = GlassStrokeWidth;
            panel.style.borderRightWidth = GlassStrokeWidth;
            panel.style.borderTopColor = new StyleColor(GlassPanelStroke);
            panel.style.borderBottomColor = new StyleColor(GlassPanelStroke);
            panel.style.borderLeftColor = new StyleColor(GlassPanelStroke);
            panel.style.borderRightColor = new StyleColor(GlassPanelStroke);
            panel.style.borderTopLeftRadius = GlassRadius;
            panel.style.borderTopRightRadius = GlassRadius;
            panel.style.borderBottomLeftRadius = GlassRadius;
            panel.style.borderBottomRightRadius = GlassRadius;
        }

        // ── [Fluent 스크롤러] Unity 기본 스크롤러(두꺼운 트랙+화살표 버튼)는 GitHub-dark/Fluent 미학과 어긋난다.
        //    Figma 프레임은 스크롤러가 없는 정적 목업(보이는 부분만 렌더)이므로, 런타임에서만 필요한
        //    오버플로 스크롤은 "얇은 오버레이 썸"으로 최소 표현한다. (2026-10-08 사용자 확정: 스크롤 자체는 OK, 스타일만 Fluent화)
        /// <summary>Fluent 스크롤러 썸 기본색 — 보조텍스트 #8B949E @ 0.35.</summary>
        public static readonly Color FluentScrollThumb = new Color(0.545f, 0.580f, 0.616f, 0.35f);
        /// <summary>Fluent 스크롤러 썸 호버색 — 보조텍스트 #8B949E @ 0.65.</summary>
        public static readonly Color FluentScrollThumbHover = new Color(0.545f, 0.580f, 0.616f, 0.65f);
        /// <summary>Fluent 스크롤러 트랙 — 화이트 @ 0.04 (거의 투명).</summary>
        public static readonly Color FluentScrollTrack = new Color(1f, 1f, 1f, 0.04f);
        /// <summary>Fluent 스크롤러 두께(px) — Figma 1.2× 패밀리의 6.</summary>
        public const float FluentScrollThickness = 6f;

        /// <summary>
        /// 트리 전체의 모든 ScrollView에 Fluent 스크롤러를 일괄 적용한다(멱등).
        /// UTKWindowBase.Show가 호출하는 중앙 적용 지점 — 개별 창의 지연 생성 ScrollView에도 직접 호출 가능.
        /// </summary>
        public static void ApplyFluentScrollers(VisualElement tree)
        {
            if (tree == null) return;
            tree.Query<ScrollView>().ForEach(ApplyFluentScroller);
        }

        /// <summary>
        /// ScrollView를 Fluent 스타일(얇은 트랙 + 둥근 반투명 썸 + 화살표 버튼 제거)로 통일한다.
        /// 가로 스크롤은 숨기고 세로는 내용이 넘칠 때만 표시(ScrollerVisibility.Auto).
        /// 멱등 — 중복 적용해도 콜백이 쌓이지 않는다(utk-fluent-scroller 마커 클래스).
        /// </summary>
        public static void ApplyFluentScroller(ScrollView sv)
        {
            if (sv == null || sv.ClassListContains("utk-fluent-scroller")) return;
            sv.AddToClassList("utk-fluent-scroller");

            sv.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            sv.verticalScrollerVisibility = ScrollerVisibility.Auto;

            StyleFluentScroller(sv.verticalScroller, true);
            StyleFluentScroller(sv.horizontalScroller, false);
        }

        private static void StyleFluentScroller(Scroller scroller, bool vertical)
        {
            if (scroller == null) return;
            float t = FluentScrollThickness;

            // 두께는 스크롤 방향의 교차축만 — 방향축은 레이아웃(100%)을 유지해야 붕괴되지 않는다.
            if (vertical)
                scroller.style.width = t;
            else
                scroller.style.height = t;
            scroller.style.backgroundColor = new StyleColor(FluentScrollTrack);
            scroller.style.borderTopWidth = scroller.style.borderBottomWidth =
                scroller.style.borderLeftWidth = scroller.style.borderRightWidth = 0f;

            // Fluent는 트랙+썸만 — 화살표(리피트) 버튼 제거.
            if (scroller.lowButton != null) scroller.lowButton.style.display = DisplayStyle.None;
            if (scroller.highButton != null) scroller.highButton.style.display = DisplayStyle.None;

            var slider = scroller.slider;
            if (slider == null) return;
            if (vertical)
                slider.style.width = t;
            else
                slider.style.height = t;
            slider.style.backgroundColor = StyleKeyword.Null;

            var dragger = slider.Q(className: "unity-base-slider__dragger");
            if (dragger == null) return;
            dragger.style.backgroundColor = new StyleColor(FluentScrollThumb);
            dragger.style.borderTopWidth = dragger.style.borderBottomWidth =
                dragger.style.borderLeftWidth = dragger.style.borderRightWidth = 0f;
            dragger.style.borderTopLeftRadius = dragger.style.borderTopRightRadius =
                dragger.style.borderBottomLeftRadius = dragger.style.borderBottomRightRadius = t * 0.5f;
            if (vertical)
                dragger.style.width = t;

            dragger.RegisterCallback<PointerEnterEvent>(evt =>
                dragger.style.backgroundColor = new StyleColor(FluentScrollThumbHover));
            dragger.RegisterCallback<PointerLeaveEvent>(evt =>
                dragger.style.backgroundColor = new StyleColor(FluentScrollThumb));
        }
    }
}

namespace ProjectName.UI.Toolkit
{
    /// <summary>Window presentation mode. Standard preserves the established Theme.uss chrome.</summary>
    public enum UTKWindowChrome
    {
        Standard,
        Frameless
    }
}
