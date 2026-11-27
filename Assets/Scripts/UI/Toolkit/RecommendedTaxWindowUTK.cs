using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// [117차 R2] 세금 관리 창 — 피그마 244:281 "단일 영지 세금 관리" 624×974 전체 정합.
    ///
    /// 진실치(Figma 재실측 2026-10-23):
    ///   · 창 624×974, 모서리 장식 4코너 12×12(L자 1.8)
    ///   · 패널 제목 (24,24): "세금 관리" 21.6px + "영지 세율 및 마을 호감도" 13.2px
    ///   · 선택한 영지 (24,96) 576×86.4: 등급 바 4.8 · 상징 52.8 · 이름 16.8px · 세금 항목 "주민세 · 농업세"
    ///     · 현재 호감도 (492.6,117.3) 수치 19.2px "{favor} / 100"
    ///   · 세율 설정: "세율 설정" (43.2,226.6) 16.8px · "슬라이더로 징수할 세율을 조절하세요." 12px
    ///     · 현재 설정 (532.8,220.8) 12px + 수치 26.4px
    ///   · 세율 슬라이더 (43.2,285.6) 537.6×100 — 구간 바 246.8×7.2(동적 분할) · 손잡이 24×24 ·
    ///     권장 표시선 2.4×24 · 최대 표시선 2.4×15 · 라벨 최소/권장/최대 12px + 수치 14.4px
    ///     · 손잡이 x = 10 + (rate/30)×493.6 (피그마 15% 앵커 x=256.8과 정확히 일치하는 선형 매핑)
    ///   · 권장 초과 안내 (43.2,400): "권장 세율보다 {d}%p 높음" 13.2px + "권장값으로 설정" 버튼 113×32.4
    ///   · 호감도 영향 설명창 (24,482.8) 576×279: "마을 호감도 영향" + "매주 −{loss}" ·
    ///     현재 호감도 → 다음 징수 후 예상(수치 24px) · 현재 세율 영향 2줄 · 계산 기준 2줄
    ///   · 세금 요약 및 적용 (24,781) 576×169: "매주 월요일 00:00 자동 징수" + "다음 징수부터 적용" ·
    ///     예상 주간 징수액(26.4px) + "권장 세금 대비 {±g}" · 취소 132×43.2 + 세율 적용 434.4×43.2
    ///
    /// 데이터 소스(위조 금지): TaxManager(영지별 세율/호감도/EstimateWeeklyTax) + TerritoryDatabase(이름/난이도).
    /// [수금] 버튼 폐기 — 피그마 "자동 징수" 계약(TaxManager.ApplyWeeklyTax가 주 경계일에 자동 지급).
    /// 레이아웃: ApplyCanvasFit 84차 계약(창박스=figma×k 등배수, _content raw·scale=k — RestaurantWindowUTK 88차 선례).
    /// </summary>
    public class RecommendedTaxWindowUTK : UTKWindowBase
    {
        // ===== 스타일 상수 [Figma 244:281 팔레트 준용] =====
        private static readonly Color PanelBg = new Color(0.075f, 0.094f, 0.122f, 0.98f);   // #13181F
        private static readonly Color PanelSub = new Color(0.11f, 0.137f, 0.173f, 1f);      // #1C232C
        private static readonly Color StrokeColor = new Color(0.188f, 0.212f, 0.239f, 1f);  // #30363D
        private static readonly Color AccentColor = new Color(0.345f, 0.651f, 1f, 1f);      // #58A6FF
        private static readonly Color TextMainColor = new Color(0.941f, 0.965f, 0.988f, 1f); // #F0F6FC
        private static readonly Color TextSubColor = new Color(0.545f, 0.580f, 0.620f, 1f);  // #8B949E
        private static readonly Color WarnColor = new Color(0.988f, 0.769f, 0.208f, 1f);     // #FCC435
        private static readonly Color DangerColor = new Color(0.957f, 0.408f, 0.408f, 1f);   // #F46868
        private static readonly Color FavorWarnColor = new Color(0.824f, 0.6f, 0.133f, 1f);  // #D29922 [118차 R0] 피그마 경고 톤
        private static readonly Color FavorWeeklyWarnColor = new Color(0.941f, 0.529f, 0.49f, 1f);   // #F0887D [144차 P7-TAX #39] 호감도 주간 변화 경고 톤
        private static readonly Color TrackRestColor = new Color(0.188f, 0.212f, 0.239f, 1f);   // #30363D [118차 R0] 실측 정정

        private static readonly Vector2 WinSize = new Vector2(624, 974);

        // 슬라이더 기하 [피그마 실측 — 세율 슬라이더 프레임(43.2,285.6) 537.6×100 로컬 좌표]
        private const float TrackX = 10f;          // 설정 구간 시작 x
        private const float TrackSpan = 493.6f;    // 손잡이 이동 전체 폭(피그마 15% 앵커 정합)
        private const float TrackY = 26f;          // 구간 바 y
        private const float BarHeight = 7.2f;
        private const float HandleSize = 24f;

        // ===== UI 노드 =====
        private Label _territoryName;
        private Label _territoryType;
        private Label _favorHeader;        // 영지 카드 우측 "현재 호감도" 수치
        private Label _rateValue;          // 현재 설정 수치(26.4px)
        private VisualElement _handle;
        private VisualElement _slider;
        private VisualElement _barFilled;
        private VisualElement _barRest;
        private VisualElement _recMarker;
        private Label _recRateLabel;       // "권장 세금" 하단 수치(10% 등)
        private Label _excessLabel;        // 권장 초과 안내 문구
        private Button _recButton;         // 권장값으로 설정
        private Label _favorNow;
        private Label _favorAfter;
        private Label _favorWeekly;        // "매주 −{loss}"
        private Label _impactText;         // 현재 세율 영향 2줄
        private Label _calcText;           // 계산 기준 2줄(권장 안내 행)
        private Label _estimateValue;      // 예상 주간 징수액
        private Label _estimateDelta;      // 권장 세금 대비 ±g
        private Button _applyButton;

        // ===== 상태 =====
        private NationType _nation;
        private int _territoryIndex;
        private bool _hasTerritory;
        private int _recommendedRate = 10;
        private int _rate;                 // 드래그 중 임시 세율
        private bool _dragging;
        private float _lastFitRootW = -1f;
        private float _lastFitRootH = -1f;

        // =====================================================================
        // 생성자 — UI 구축 (절대 배치 = 피그마 좌표 그대로)
        // =====================================================================
        public RecommendedTaxWindowUTK() : base("💰 세금 관리", WinSize)
        {
            SetChrome(UTKWindowChrome.Frameless);
            // [144차 P7-TAX #2] [118차 R0 피그마 정합] fill=#13181F/1.0(불투명) stroke=#30363D@0.502 border=1.2 r=14.4 — 실측 정정(118차 border 0.5 폐기)
            style.backgroundColor = new Color(0.075f, 0.094f, 0.122f, 1f);
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 1.2f;   // [144차 P7-TAX #2] 0.5→1.2
            style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = StrokeColor;
            style.borderTopLeftRadius = style.borderTopRightRadius = style.borderBottomLeftRadius = style.borderBottomRightRadius = 14.4f;

            var content = Content;
            if (content == null) return;
            content.style.position = Position.Relative;

            BuildCornerDeco(content);
            BuildTitle(content);
            BuildTerritoryCard(content);
            BuildRateSection(content);
            BuildSlider(content);
            BuildExcessRow(content);
            BuildFavorPanel(content);
            BuildSummary(content);
        }

        // ── 모서리 장식 4코너 12×12 (L자 1.8) [피그마 모서리 장식] ──
        private static void BuildCornerDeco(VisualElement content)
        {
            void Corner(float x, float y)
            {
                var corner = new VisualElement();
                corner.style.position = Position.Absolute;
                corner.style.left = x; corner.style.top = y;
                corner.style.width = 12f; corner.style.height = 12f;
                corner.pickingMode = PickingMode.Ignore;
                var v = new VisualElement();   // 세로 1.8×12
                v.style.position = Position.Absolute;
                v.style.left = x < 12f ? 0f : 10.2f; v.style.top = 0f;
                v.style.width = 1.8f; v.style.height = 12f;
                v.style.backgroundColor = AccentColor;
                var h = new VisualElement();   // 가로 12×1.8
                h.style.position = Position.Absolute;
                h.style.left = 0f; h.style.top = y < 12f ? 0f : 10.2f;
                h.style.width = 12f; h.style.height = 1.8f;
                h.style.backgroundColor = AccentColor;
                corner.Add(v); corner.Add(h);
                content.Add(corner);
            }
            Corner(0f, 0f);
            Corner(612f, 0f);
            Corner(0f, 962f);
            Corner(612f, 962f);
        }

        private static Label MakeLabel(VisualElement parent, string text, float x, float y, float fontSize,
            Color color, bool bold = false, float width = -1f)
        {
            var label = new Label(text);
            label.style.position = Position.Absolute;
            label.style.left = x; label.style.top = y;
            if (width >= 0f) label.style.width = width;
            label.style.fontSize = fontSize;
            label.style.color = color;
            if (bold) label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.pickingMode = PickingMode.Ignore;
            parent.Add(label);
            return label;
        }

        private void BuildTitle(VisualElement content)
        {
            MakeLabel(content, "세금 관리", 24f, 24f, 21.6f, TextMainColor, bold: true);
            MakeLabel(content, "영지 세율 및 마을 호감도", 121f, 32f, 13.2f, TextSubColor);
        }

        private void BuildTerritoryCard(VisualElement content)
        {
            var card = new VisualElement();
            card.style.position = Position.Absolute;
            card.style.left = 24f; card.style.top = 96f;
            card.style.width = 576f; card.style.height = 86.4f;
            // [144차 P7-TAX #7] [118차 R0] fill=#FFFFFF/0.08 stroke=#58A6FF border=1.8 r=9.6 — 실측 정정(118차 1.0 폐기)
            card.style.backgroundColor = new Color(1f, 1f, 1f, 0.08f);
            card.style.borderTopWidth = card.style.borderBottomWidth = card.style.borderLeftWidth = card.style.borderRightWidth = 1.8f;   // [144차 P7-TAX #7] 1.0→1.8
            card.style.borderTopColor = card.style.borderBottomColor = card.style.borderLeftColor = card.style.borderRightColor = AccentColor;
            card.style.borderTopLeftRadius = card.style.borderTopRightRadius = 9.6f;
            card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 9.6f;
            card.pickingMode = PickingMode.Ignore;
            content.Add(card);

            var gradeBar = new VisualElement();   // 등급 바 4.8×86.4
            gradeBar.style.position = Position.Absolute;
            gradeBar.style.left = 0f; gradeBar.style.top = 0f;
            gradeBar.style.width = 4.8f; gradeBar.style.height = 86.4f;
            gradeBar.style.backgroundColor = AccentColor;
            card.Add(gradeBar);

            // [144차 P7-TAX #9] [118차 R0] 영지 상징 — 원형 컨테이너 fill=#21262D/0.6 stroke=#30363D@0.502 border=1.2 r=100(원)
            var emblemRing = new VisualElement();
            emblemRing.style.position = Position.Absolute;
            emblemRing.style.left = 14.4f; emblemRing.style.top = 16.8f;
            emblemRing.style.width = 52.8f; emblemRing.style.height = 52.8f;
            emblemRing.style.backgroundColor = new Color(0.129f, 0.149f, 0.176f, 0.6f);
            emblemRing.style.borderTopWidth = emblemRing.style.borderBottomWidth = emblemRing.style.borderLeftWidth = emblemRing.style.borderRightWidth = 1.2f;   // [144차 P7-TAX #9] 0.5→1.2
            emblemRing.style.borderTopColor = emblemRing.style.borderBottomColor = emblemRing.style.borderLeftColor = emblemRing.style.borderRightColor = StrokeColor;
            emblemRing.style.borderTopLeftRadius = emblemRing.style.borderTopRightRadius = 26.4f;
            emblemRing.style.borderBottomLeftRadius = emblemRing.style.borderBottomRightRadius = 26.4f;
            emblemRing.pickingMode = PickingMode.Ignore;
            card.Add(emblemRing);
            var emblem = new Label("🏰");
            emblem.style.position = Position.Absolute;
            emblem.style.left = 0f; emblem.style.top = 0f;
            emblem.style.width = 52.8f; emblem.style.height = 52.8f;
            emblem.style.fontSize = 26f;
            emblem.style.unityTextAlign = TextAnchor.MiddleCenter;
            emblem.pickingMode = PickingMode.Ignore;
            emblemRing.Add(emblem);

            _territoryName = MakeLabel(card, "—", 81.6f, 22.3f, 16.8f, TextMainColor, bold: true);
            _territoryType = MakeLabel(card, "난이도 —", 153.2f, 27.3f, 12f, TextSubColor);
            MakeLabel(card, "주민세 · 농업세", 81.6f, 47.1f, 14.4f, TextSubColor);
            MakeLabel(card, "현재 호감도", 469f, 21.3f, 12f, TextSubColor, width: 93f);   // [144차 P7-TAX #14] x 502.6→469(12 #8B949E, 카드로컬 y21 유지)
            _favorHeader = MakeLabel(card, "100 / 100", 468.6f, 40.1f, 19.2f, AccentColor, bold: true, width: 93f);   // [144차 P7-TAX #15] TextMainColor→AccentColor 19.2 Geist Mono 700
            _favorHeader.style.unityFont = GetGeistMonoFont();   // [144차 P7-TAX #15] Geist Mono — 에셋 부재 시 UIFont 폴백(WorldMap/WeaponForge 선례)
        }

        private void BuildRateSection(VisualElement content)
        {
            // [144차 P7-TAX #16] [118차 R0] 세금 조절 섹션 — fill=#FFFFFF/0.02 stroke=#30363D border=1.2 r=9.6 (피그마 컨테이너 복원)
            var rateSection = new VisualElement();
            rateSection.style.position = Position.Absolute;
            rateSection.style.left = 24f; rateSection.style.top = 201.6f;
            rateSection.style.width = 576f; rateSection.style.height = 262f;
            rateSection.style.backgroundColor = new Color(1f, 1f, 1f, 0.02f);
            rateSection.style.borderTopWidth = rateSection.style.borderBottomWidth = rateSection.style.borderLeftWidth = rateSection.style.borderRightWidth = 1.2f;   // [144차 P7-TAX #16] 0.5→1.2
            rateSection.style.borderTopColor = rateSection.style.borderBottomColor = rateSection.style.borderLeftColor = rateSection.style.borderRightColor = StrokeColor;
            rateSection.style.borderTopLeftRadius = rateSection.style.borderTopRightRadius = 9.6f;
            rateSection.style.borderBottomLeftRadius = rateSection.style.borderBottomRightRadius = 9.6f;
            rateSection.pickingMode = PickingMode.Ignore;
            content.Add(rateSection);

            MakeLabel(content, "세율 설정", 43.2f, 226.6f, 16.8f, TextMainColor, bold: true);
            MakeLabel(content, "슬라이더로 징수할 세율을 조절하세요.", 43.2f, 251.4f, 12f, TextSubColor);
            MakeLabel(content, "현재 설정", 532.8f, 220.8f, 12f, TextSubColor, width: 48f);
            _rateValue = MakeLabel(content, "10%", 532.8f, 237.2f, 26.4f, AccentColor, bold: true, width: 48f);
        }

        private void BuildSlider(VisualElement content)
        {
            var slider = new VisualElement();
            _slider = slider;
            slider.style.position = Position.Absolute;
            slider.style.left = 43.2f; slider.style.top = 285.6f;
            slider.style.width = 537.6f; slider.style.height = 100f;
            content.Add(slider);

            // 구간 바 — 손잡이 기준 동적 분할(피그마: 설정 구간=채움, 남은 구간=나머지)
            _barFilled = new VisualElement();
            _barFilled.style.position = Position.Absolute;
            _barFilled.style.left = TrackX; _barFilled.style.top = TrackY;
            _barFilled.style.height = BarHeight;
            _barFilled.style.backgroundColor = AccentColor;
            _barFilled.style.borderTopLeftRadius = _barFilled.style.borderBottomLeftRadius = 4f;
            slider.Add(_barFilled);

            _barRest = new VisualElement();
            _barRest.style.position = Position.Absolute;
            _barRest.style.top = TrackY;
            _barRest.style.height = BarHeight;
            _barRest.style.backgroundColor = TrackRestColor;
            _barRest.style.borderTopRightRadius = _barRest.style.borderBottomRightRadius = 4f;
            slider.Add(_barRest);

            // 권장 표시선 2.4×24 — 권장 세율 위치(동적)
            _recMarker = new VisualElement();
            _recMarker.style.position = Position.Absolute;
            _recMarker.style.width = 2.4f; _recMarker.style.height = 24f;
            _recMarker.style.top = 18.4f;   // [144차 P7-TAX #24] top 18→18.4(2.4×24 #F0F6FC 유지)
            _recMarker.style.backgroundColor = TextMainColor;   // [118차 R0] 권장 표시선 = #F0F6FC(흰색) 실측 정정
            _recMarker.pickingMode = PickingMode.Ignore;
            slider.Add(_recMarker);

            // [144차 P7-TAX #25] 최대 표시선 2.4×15 — 피그마 x=569.2(슬라이더 로컬 526) y=22.4
            var maxMarker = new VisualElement();
            maxMarker.style.position = Position.Absolute;
            maxMarker.style.left = 526f; maxMarker.style.top = 22.4f;
            maxMarker.style.width = 2.4f; maxMarker.style.height = 15f;
            maxMarker.style.backgroundColor = TextSubColor;   // [144차 P7-TAX #25] StrokeColor→#8B949E
            maxMarker.pickingMode = PickingMode.Ignore;
            slider.Add(maxMarker);

            // 손잡이 24×24 — 드래그 대상
            _handle = new VisualElement();
            _handle.style.position = Position.Absolute;
            _handle.style.width = HandleSize; _handle.style.height = HandleSize;
            _handle.style.top = 18f;
            // [144차 P7-TAX #23] [118차 R0] 손잡이 fill=#58A6FF stroke=#F0F6FC border=2.4 (흰 테두리 원) — 실측 정정
            _handle.style.backgroundColor = AccentColor;
            _handle.style.borderTopWidth = _handle.style.borderBottomWidth = _handle.style.borderLeftWidth = _handle.style.borderRightWidth = 2.4f;   // [144차 P7-TAX #23] 1.0→2.4
            _handle.style.borderTopColor = _handle.style.borderBottomColor = _handle.style.borderLeftColor = _handle.style.borderRightColor = TextMainColor;
            _handle.style.borderTopLeftRadius = _handle.style.borderTopRightRadius = 12f;
            _handle.style.borderBottomLeftRadius = _handle.style.borderBottomRightRadius = 12f;
            slider.Add(_handle);

            // 라벨 행 — 최소/권장/최대 (y=337.6 → 슬라이더 로컬 y=52) + 수치(y=70.8)
            MakeLabel(slider, "최소 세금", 0f, 52f, 12f, TextSubColor);
            MakeLabel(slider, "권장 세금", 139f, 52.4f, 12f, AccentColor, bold: true, width: 48f);   // [144차 P7-TAX #28] TextSubColor→AccentColor + Bold 700
            MakeLabel(slider, "최대 세금", 489.6f, 52f, 12f, TextSubColor, width: 48f);
            MakeLabel(slider, "0%", 0f, 70.8f, 14.4f, TextMainColor);
            _recRateLabel = MakeLabel(slider, "10%", 150f, 70.4f, 14.4f, AccentColor, bold: true, width: 48f);   // [144차 P7-TAX #29] WarnColor→AccentColor 14.4 Geist Mono 700
            _recRateLabel.style.unityFont = GetGeistMonoFont();   // [144차 P7-TAX #29] Geist Mono — 에셋 부재 시 UIFont 폴백
            MakeLabel(slider, "30%", 511.6f, 70.8f, 14.4f, TextMainColor);

            // 드래그 — 트랙 전체에서 포인터 캡처(백드롭 양보 계약: 창 드래그와 분리)
            slider.RegisterCallback<PointerDownEvent>(OnSliderPointerDown);
            slider.RegisterCallback<PointerMoveEvent>(OnSliderPointerMove);
            slider.RegisterCallback<PointerUpEvent>(OnSliderPointerUp);
        }

        private void OnSliderPointerDown(PointerDownEvent evt)
        {
            _dragging = true;
            _handle.CapturePointer(evt.pointerId);
            UpdateRateFromPointer(_slider.WorldToLocal(evt.position).x);
            evt.StopPropagation();
        }

        private void OnSliderPointerMove(PointerMoveEvent evt)
        {
            if (!_dragging) return;
            UpdateRateFromPointer(_slider.WorldToLocal(evt.position).x);
        }

        private void OnSliderPointerUp(PointerUpEvent evt)
        {
            if (!_dragging) return;
            _dragging = false;
            _handle.ReleasePointer(evt.pointerId);
        }

        /// <summary>로컬 x → 세율(0~30) — 손잡이 x 매핑의 역변환(피그마 선형 계약).</summary>
        private void UpdateRateFromPointer(float localX)
        {
            float t = Mathf.Clamp((localX - TrackX) / TrackSpan, 0f, 1f);
            SetRate(Mathf.RoundToInt(t * TaxManager.MaxTaxPercent));
        }

        private void BuildExcessRow(VisualElement content)
        {
            // [144차 P7-TAX #32] [118차 R0] 권장 초과 안내 컨테이너 — stroke=#30363D/1(무채움) (피그마 프레임 복원, 0.5→1 실측 정정)
            var excessBox = new VisualElement();
            excessBox.style.position = Position.Absolute;
            excessBox.style.left = 43.2f; excessBox.style.top = 400f;
            excessBox.style.width = 537.6f; excessBox.style.height = 44.4f;
            excessBox.style.borderTopWidth = excessBox.style.borderBottomWidth = excessBox.style.borderLeftWidth = excessBox.style.borderRightWidth = 1f;   // [144차 P7-TAX #32]
            excessBox.style.borderTopColor = excessBox.style.borderBottomColor = excessBox.style.borderLeftColor = excessBox.style.borderRightColor = StrokeColor;
            excessBox.pickingMode = PickingMode.Ignore;
            content.Add(excessBox);

            _excessLabel = MakeLabel(content, "권장 세율 이하", 43.2f, 420.7f, 13.2f, TextSubColor, width: 200f);
            // [144차 P7-TAX #34] [118차 R0] 권장값 버튼 fill=#FFFFFF/0.02 stroke=#30363D/1.2 r=7.2 — 액센트 버튼 폐기(실측 정정)
            _recButton = new Button(OnRecommendedClicked) { text = "권장값으로 설정" };
            _recButton.style.position = Position.Absolute;
            _recButton.style.left = 467.8f; _recButton.style.top = 412f;
            _recButton.style.width = 113f; _recButton.style.height = 32.4f;
            _recButton.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            _recButton.style.backgroundColor = new Color(1f, 1f, 1f, 0.02f);
            _recButton.style.color = TextMainColor;
            _recButton.style.unityFontStyleAndWeight = FontStyle.Bold;   // [144차 P7-TAX #35] '권장값으로 설정' Bold 700
            _recButton.style.borderTopWidth = _recButton.style.borderBottomWidth = _recButton.style.borderLeftWidth = _recButton.style.borderRightWidth = 1.2f;   // [144차 P7-TAX #34]
            _recButton.style.borderTopColor = _recButton.style.borderBottomColor = _recButton.style.borderLeftColor = _recButton.style.borderRightColor = StrokeColor;
            _recButton.style.borderTopLeftRadius = _recButton.style.borderTopRightRadius = 7.2f;
            _recButton.style.borderBottomLeftRadius = _recButton.style.borderBottomRightRadius = 7.2f;
            content.Add(_recButton);
        }

        private void BuildFavorPanel(VisualElement content)
        {
            var panel = new VisualElement();
            panel.style.position = Position.Absolute;
            panel.style.left = 24f; panel.style.top = 482.8f;
            panel.style.width = 576f; panel.style.height = 279f;
            // [144차 P7-TAX #36] [118차 R0] fill=#D29922/0.06 stroke=#D29922/0.3(1.2) r=9.6 — 경고 톤 컨테이너(PanelSub 폐기, 두께 0.5→1.2 실측 정정)
            panel.style.backgroundColor = new Color(0.824f, 0.6f, 0.133f, 0.06f);
            panel.style.borderTopWidth = panel.style.borderBottomWidth = panel.style.borderLeftWidth = panel.style.borderRightWidth = 1.2f;   // [144차 P7-TAX #36]
            panel.style.borderTopColor = panel.style.borderBottomColor = panel.style.borderLeftColor = panel.style.borderRightColor = new Color(0.824f, 0.6f, 0.133f, 0.3f);
            panel.style.borderTopLeftRadius = panel.style.borderTopRightRadius = 9.6f;
            panel.style.borderBottomLeftRadius = panel.style.borderBottomRightRadius = 9.6f;
            panel.pickingMode = PickingMode.Ignore;
            content.Add(panel);

            // [144차 P7-TAX #40] [118차 R0] 호감도 예측 서브박스 — fill=#0B0E14 stroke=#30363D/1.2 r=7.2 (라벨 뒤, 두께 실측 정정)
            var predictBox = new VisualElement();
            predictBox.style.position = Position.Absolute;
            predictBox.style.left = 19.2f; predictBox.style.top = 53.2f;
            predictBox.style.width = 537.6f; predictBox.style.height = 78.6f;
            predictBox.style.backgroundColor = new Color(0.043f, 0.055f, 0.078f, 1f);
            predictBox.style.borderTopWidth = predictBox.style.borderBottomWidth = predictBox.style.borderLeftWidth = predictBox.style.borderRightWidth = 1.2f;   // [144차 P7-TAX #40]
            predictBox.style.borderTopColor = predictBox.style.borderBottomColor = predictBox.style.borderLeftColor = predictBox.style.borderRightColor = StrokeColor;
            predictBox.style.borderTopLeftRadius = predictBox.style.borderTopRightRadius = 7.2f;
            predictBox.style.borderBottomLeftRadius = predictBox.style.borderBottomRightRadius = 7.2f;
            predictBox.pickingMode = PickingMode.Ignore;
            panel.Add(predictBox);

            MakeLabel(panel, "⚠", 19.2f, 19.2f, 16f, WarnColor, width: 28.8f);   // triangle-alert 대체 글리프
            MakeLabel(panel, "마을 호감도 영향", 48f, 19.2f, 16.8f, TextMainColor, bold: true);
            _favorWeekly = MakeLabel(panel, "매주 −0", 509.8f, 20.7f, 14.4f, FavorWeeklyWarnColor, bold: true, width: 47f);   // [144차 P7-TAX #39] #F0887D Bold 700(118차 R0 "D29922" 코멘트 실측 불일치 정정)

            MakeLabel(panel, "현재 호감도", 33.6f, 68f, 12f, TextSubColor);
            _favorNow = MakeLabel(panel, "100 / 100", 33.6f, 86.8f, 24f, TextMainColor, bold: true, width: 225.6f);
            MakeLabel(panel, "→", 278.4f, 83.3f, 19.2f, TextSubColor);
            MakeLabel(panel, "다음 징수 후 예상", 316.8f, 68f, 12f, TextSubColor);
            _favorAfter = MakeLabel(panel, "100 / 100", 316.8f, 86.8f, 24f, TextMainColor, bold: true, width: 225.6f);

            _impactText = MakeLabel(panel,
                "권장 세율 이하로 설정하면 세금으로 인한 호감도 하락이 없습니다.",
                19.2f, 146.6f, 14.4f, TextMainColor, width: 537.6f);

            MakeLabel(panel, "권장 세율 초과 1%p당 호감도 −1 / 주 · 최대 세금 30%일 때 −20 / 주", 19.2f, 219f, 12f, TextSubColor, width: 537.6f);   // [144차 P7-TAX #47] 후단 누락 보강(JSON 원문)
            _calcText = MakeLabel(panel, "권장 세율 이하로 설정하면 세금으로 인한 호감도 하락이 없습니다.",
                19.2f, 241.8f, 12f, AccentColor, width: 537.6f);   // [144차 P7-TAX #48] #58A6FF
        }

        private void BuildSummary(VisualElement content)
        {
            var summary = new VisualElement();
            summary.style.position = Position.Absolute;
            summary.style.left = 24f; summary.style.top = 781f;
            summary.style.width = 576f; summary.style.height = 169f;
            // [144차 P7-TAX #49] [118차 R0] 세금 요약 프레임 — 무채움 stroke=#30363D/1(채움 없음, PanelSub 폐기, 0.5→1 실측 정정)
            summary.style.backgroundColor = new StyleColor(StyleKeyword.None);
            summary.style.borderTopWidth = summary.style.borderBottomWidth = summary.style.borderLeftWidth = summary.style.borderRightWidth = 1f;   // [144차 P7-TAX #49]
            summary.style.borderTopColor = summary.style.borderBottomColor = summary.style.borderLeftColor = summary.style.borderRightColor = StrokeColor;
            summary.pickingMode = PickingMode.Ignore;
            content.Add(summary);

            MakeLabel(summary, "매주 월요일 00:00 자동 징수", 0f, 14.4f, 13.2f, TextSubColor);
            MakeLabel(summary, "다음 징수부터 적용", 481f, 14.9f, 12f, AccentColor, width: 95f);   // [144차 P7-TAX #51] #58A6FF

            // [144차 P7-TAX #52] [118차 R0] 예상 주간 세금 서브박스 — fill=#58A6FF/0.08 stroke=#58A6FF/0.25(1.2) r=9.6 (두께 실측 정정)
            var estimateBox = new VisualElement();
            estimateBox.style.position = Position.Absolute;
            estimateBox.style.left = 0f; estimateBox.style.top = 44f;
            estimateBox.style.width = 576f; estimateBox.style.height = 68f;
            estimateBox.style.backgroundColor = new Color(0.345f, 0.651f, 1f, 0.08f);
            estimateBox.style.borderTopWidth = estimateBox.style.borderBottomWidth = estimateBox.style.borderLeftWidth = estimateBox.style.borderRightWidth = 1.2f;   // [144차 P7-TAX #52]
            estimateBox.style.borderTopColor = estimateBox.style.borderBottomColor = estimateBox.style.borderLeftColor = estimateBox.style.borderRightColor = new Color(0.345f, 0.651f, 1f, 0.25f);
            estimateBox.style.borderTopLeftRadius = estimateBox.style.borderTopRightRadius = 9.6f;
            estimateBox.style.borderBottomLeftRadius = estimateBox.style.borderBottomRightRadius = 9.6f;
            estimateBox.pickingMode = PickingMode.Ignore;
            summary.Add(estimateBox);

            MakeLabel(summary, "예상 주간 징수액", 14.4f, 58.2f, 16.8f, TextMainColor, bold: true, width: 119f);
            _estimateDelta = MakeLabel(summary, "권장 세금 대비 ±0g", 14.4f, 83f, 12f, TextSubColor, width: 119f);
            _estimateValue = MakeLabel(summary, "0g", 466.6f, 60.6f, 26.4f, AccentColor, bold: true, width: 95f);   // [144차 P7-TAX #55] #58A6FF 26.4
            _estimateValue.style.unityFont = GetGeistMonoFont();   // [144차 P7-TAX #55] Geist Mono — 에셋 부재 시 UIFont 폴백(WorldMap/WeaponForge 선례)

            var cancelButton = new Button(Close) { text = "취소" };
            cancelButton.style.position = Position.Absolute;
            cancelButton.style.left = 0f; cancelButton.style.top = 125.8f;
            cancelButton.style.width = 132f; cancelButton.style.height = 43.2f;
            StyleSecondaryButton(cancelButton);
            summary.Add(cancelButton);

            _applyButton = new Button(OnApplyClicked) { text = "세율 10% 적용" };
            _applyButton.style.position = Position.Absolute;
            _applyButton.style.left = 141.6f; _applyButton.style.top = 125.8f;
            _applyButton.style.width = 434.4f; _applyButton.style.height = 43.2f;
            StyleAccentButton(_applyButton);
            summary.Add(_applyButton);
        }

        // =====================================================================
        // 공개 진입점 — 루트 부착 + 표시 (고아 가드 — 115차 선례 유지)
        // =====================================================================
        public static RecommendedTaxWindowUTK Instance { get; private set; }

        /// <summary>개방 — 영지 컨텍스트 없으면 PlayerOwned 첫 영지 폴백(실측 데이터 기반).</summary>
        public static RecommendedTaxWindowUTK Open(string territoryId = null)
        {
            NationType nation;
            int index;
            if (!TryParseTerritoryId(territoryId, out nation, out index))
                (nation, index) = ResolveFirstPlayerOwned();

            var root = UIToolkitBootstrap.UIRoot;
            if (root == null)
            {
                Debug.LogWarning("[RecommendedTaxWindowUTK] UI Toolkit 루트 없음 — 부트스트랩 미완료.");
                return null;
            }

            if (Instance != null && Instance.parent == root)
            {
                Instance.Show();
            }
            else
            {
                if (Instance != null)
                    Instance.RemoveFromHierarchy();   // 고아 인스턴스 정리 — 114차 WorldMap Ensure 가드 함정 선례.

                Instance = new RecommendedTaxWindowUTK();
                root.Add(Instance);
                Instance.Show();
            }

            Instance.SetTerritory(nation, index);
            return Instance;
        }

        /// <summary>TerritoryId("{nation}_{index:D2}" 계약) 파싱. 실패 시 false.</summary>
        public static bool TryParseTerritoryId(string territoryId, out NationType nation, out int index)
        {
            nation = default;
            index = -1;
            if (string.IsNullOrEmpty(territoryId)) return false;
            int sep = territoryId.LastIndexOf('_');
            if (sep <= 0 || sep >= territoryId.Length - 1) return false;
            if (!System.Enum.TryParse<NationType>(territoryId.Substring(0, sep), out nation)) return false;
            return int.TryParse(territoryId.Substring(sep + 1), out index);
        }

        /// <summary>PlayerOwned 첫 영지 폴백 — 세금제안이 영지 컨텍스트 없이 호출될 때의 실측 대상.</summary>
        private static (NationType nation, int index) ResolveFirstPlayerOwned()
        {
            var db = TerritoryDatabase.Instance;
            if (db != null)
            {
                foreach (var definition in db.GetAllDefinitions())
                {
                    if (definition.id.nation == default && definition.id.index <= 0) continue;
                    var state = db.GetState(definition.id.nation, definition.id.index);
                    if (state != null && state.ownership == TerritoryOwnership.PlayerOwned)
                        return (definition.id.nation, definition.id.index);
                }
            }
            return (default, -1);
        }

        // =====================================================================
        // UTKWindowBase 훅 + 캔버스핏(84차 등배수 계약)
        // =====================================================================
        protected override void OnWindowOpen()
        {
            RefreshData();
            // [88차 선례] ApplyCanvasFit — 개방 즉시 + 레이아웃 지연 대응 재시도(16/120/400ms)
            schedule.Execute(ApplyCanvasFit).StartingIn(16);
            schedule.Execute(ApplyCanvasFit).StartingIn(120);
            schedule.Execute(ApplyCanvasFit).StartingIn(400);
        }

        protected override void OnWindowClosed()
        {
            if (Instance == this) Instance = null;
        }

        private void ApplyCanvasFit()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return;
            float rootW = root.resolvedStyle.width;
            float rootH = root.resolvedStyle.height;
            if (rootW <= 1f || rootH <= 1f) return;
            if (Mathf.Approximately(rootW, _lastFitRootW) && Mathf.Approximately(rootH, _lastFitRootH))
                return;
            float k = Mathf.Min(rootW / FigmaCanvasLayout.CanvasWidth, rootH / FigmaCanvasLayout.CanvasHeight);
            if (k <= 0f) return;

            _lastFitRootW = rootW;
            _lastFitRootH = rootH;
            style.width = WinSize.x * k;
            style.height = WinSize.y * k;
            style.left = Mathf.Max(0f, (rootW - WinSize.x * k) * 0.5f);
            style.top = Mathf.Max(0f, (rootH - WinSize.y * k) * 0.5f);
            Content.style.transformOrigin = new TransformOrigin(0f, 0f, 0f);
            Content.style.scale = new StyleScale(new Scale(new Vector2(k, k)));
        }

        // =====================================================================
        // 데이터 갱신 / 액션
        // =====================================================================
        private void SetTerritory(NationType nation, int index)
        {
            _nation = nation;
            _territoryIndex = index;
            _hasTerritory = index > 0;
            _recommendedRate = _hasTerritory ? TaxManager.Instance.GetRecommendedRate(nation, index) : 10;
            _rate = _hasTerritory ? TaxManager.Instance.GetTaxRate(nation, index) : _recommendedRate;
            RefreshData();
        }

        /// <summary>TaxManager 실측값으로 전 라벨 갱신(위조 금지 — 소스 없는 값은 "—" 폴백).</summary>
        public void RefreshData()
        {
            var tax = TaxManager.Instance;
            string name = "—";
            string typeText = "난이도 —";
            if (_hasTerritory)
            {
                var db = TerritoryDatabase.Instance;
                if (db != null)
                {
                    var definition = db.GetDefinition(_nation, _territoryIndex);
                    if (definition.id.nation == _nation && definition.id.index == _territoryIndex)
                    {
                        name = string.IsNullOrEmpty(definition.territoryName) ? $"{_nation} #{_territoryIndex}" : definition.territoryName;
                        typeText = $"난이도 {definition.difficulty}";
                    }
                    else
                    {
                        name = $"{_nation} #{_territoryIndex}";
                        typeText = "난이도 —";
                    }
                }
            }
            _territoryName.text = name;
            _territoryType.text = typeText;

            int favor = _hasTerritory ? tax.GetFavor(_nation, _territoryIndex) : TaxManager.DefaultFavor;
            _favorHeader.text = $"{favor} / 100";
            _favorNow.text = $"{favor} / 100";

            SetRate(_rate);
        }

        /// <summary>슬라이더 값 설정 — 핸들/바/라벨/예상액/호감도 예측/적용 버튼 전파(단일소스).</summary>
        private void SetRate(int newRate)
        {
            _rate = Mathf.Clamp(newRate, TaxManager.MinTaxPercent, TaxManager.MaxTaxPercent);
            int rate = _rate;
            float handleLeft = TrackX + rate / (float)TaxManager.MaxTaxPercent * TrackSpan;

            _handle.style.left = handleLeft;
            _barFilled.style.width = Mathf.Max(0f, handleLeft - TrackX);
            _barRest.style.left = handleLeft + HandleSize;
            _barRest.style.width = Mathf.Max(0f, (TrackX + TrackSpan) - (handleLeft + HandleSize));

            // [144차 P7-TAX #24] 권장 표시선 — 피그마 실측 고정 로컬 x=182(마커 중심 183.2, 2.4×24 #F0F6FC).
            //   손잡이 선형 계산 폐기 — "±4px 내 정합" 자인 제거(118차 R0 주석), 권장율 라벨(_recRateLabel)은 동적 유지.
            _recMarker.style.left = 182f;
            _recRateLabel.text = $"{_recommendedRate}%";

            _rateValue.text = $"{rate}%";
            _applyButton.text = $"세율 {rate}% 적용";

            int loss = Mathf.Max(0, rate - _recommendedRate);
            bool excess = loss > 0;
            _excessLabel.text = excess ? $"권장 세율보다 {loss}%p 높음" : "권장 세율 이하";
            _excessLabel.style.color = excess ? FavorWarnColor : TextSubColor;   // [144차 P7-TAX #33] 초과 시 #D29922(WarnColor 폐기)
            _favorWeekly.text = $"매주 −{loss}";
            _favorWeekly.style.color = excess ? FavorWeeklyWarnColor : TextSubColor;   // [144차 P7-TAX #39] excess 시 #F0887D(정적 라벨과 동일 톤)

            int favor = _hasTerritory ? TaxManager.Instance.GetFavor(_nation, _territoryIndex) : TaxManager.DefaultFavor;
            int after = Mathf.Max(0, favor - loss);
            _favorAfter.text = $"{after} / 100";
            _favorAfter.style.color = excess ? DangerColor : TextMainColor;

            _impactText.text = excess
                ? $"현재 설정한 {rate}%는 권장 세금 {_recommendedRate}%를 초과합니다. 이 세율을 유지하면 세금 징수 시 마을 호감도가 매주 {loss}씩 하락합니다."
                : $"현재 설정한 {rate}%는 권장 세금 {_recommendedRate}% 이하입니다. 세금 징수 시 마을 호감도가 하락하지 않습니다.";
            _calcText.text = $"{_recommendedRate}% 이하로 설정하면 세금으로 인한 호감도 하락이 없습니다.";

            if (_hasTerritory)
            {
                int estimate = TaxManager.Instance.EstimateWeeklyTax(_nation, _territoryIndex, rate);
                int recommended = TaxManager.Instance.EstimateWeeklyTax(_nation, _territoryIndex, _recommendedRate);
                int delta = estimate - recommended;
                _estimateValue.text = $"{estimate:N0}g";
                _estimateDelta.text = delta == 0 ? "권장 세금과 동일" : $"권장 세금 대비 {(delta > 0 ? "+" : "")}{delta:N0}g";
                _estimateDelta.style.color = TextSubColor;   // [144차 P7-TAX #54] 권장 세금 비교 — 양수/음수 공통 #8B949E(피그마 양수 샘플 기준, WarnColor 폐기, 문구 로직 무손상)
            }
            else
            {
                _estimateValue.text = "—";
                _estimateDelta.text = "영지 컨텍스트 없음";
            }
        }

        private void OnRecommendedClicked()
        {
            SetRate(_recommendedRate);
        }

        private void OnApplyClicked()
        {
            if (!_hasTerritory) { Close(); return; }
            TaxManager.Instance.SetTaxRate(_nation, _territoryIndex, _rate);
            Debug.Log($"[RecommendedTaxWindowUTK] 세율 적용 — {_nation}_{_territoryIndex:D2} = {_rate}% (다음 징수부터 적용)");
            Close();
        }

        // =====================================================================
        // 스타일 헬퍼 — GitHub-dark 인라인 오버라이드 (ShopWindowUTK.StyleButton 준용)
        // =====================================================================
        private static void StyleAccentButton(Button btn)
        {
            if (btn == null) return;
            btn.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            btn.style.backgroundColor = AccentColor;
            btn.style.color = new Color(0.043f, 0.055f, 0.078f, 1f);   // #0B0E14 [144차 P7-TAX #58]
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;   // [144차 P7-TAX #58] '적용' Bold 700
            btn.style.borderTopWidth = btn.style.borderBottomWidth = btn.style.borderLeftWidth = btn.style.borderRightWidth = 0f;
            btn.style.borderTopLeftRadius = btn.style.borderTopRightRadius = 7.2f;   // [118차 R0] 피그마 r=7.2
            btn.style.borderBottomLeftRadius = btn.style.borderBottomRightRadius = 7.2f;   // [118차 R0]
        }

        private static void StyleSecondaryButton(Button btn)
        {
            if (btn == null) return;
            // [144차 P7-TAX #56] [118차 R0] 취소 버튼 fill=#FFFFFF/0.02 stroke=#30363D/1.2 r=7.2 — 실측 정정
            btn.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            btn.style.backgroundColor = new Color(1f, 1f, 1f, 0.02f);
            btn.style.color = TextMainColor;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;   // [144차 P7-TAX #58] '취소' Bold 700
            btn.style.borderTopWidth = btn.style.borderBottomWidth = btn.style.borderLeftWidth = btn.style.borderRightWidth = 1.2f;   // [144차 P7-TAX #56]
            btn.style.borderTopColor = btn.style.borderBottomColor = btn.style.borderLeftColor = btn.style.borderRightColor = StrokeColor;
            btn.style.borderTopLeftRadius = btn.style.borderTopRightRadius = 7.2f;
            btn.style.borderBottomLeftRadius = btn.style.borderBottomRightRadius = 7.2f;
        }

        // [144차 P7-TAX] Geist Mono 로드(Figma 패밀리 계약) — 에셋 미보유 실측(2026-10-24) → UIFont 폴백
        // (WorldMapWindowUTK/WeaponForgeUTK/RestaurantWindowUTK GetGeistMonoFont 선례). Resources/Fonts/GeistMono 추가 시 자동 적용.
        private static Font _geistMono;

        private static Font GetGeistMonoFont()
        {
            if (_geistMono == null)
                _geistMono = Resources.Load<Font>("Fonts/GeistMono");
            return _geistMono != null ? _geistMono : UIFont.Load();
        }
    }
}
