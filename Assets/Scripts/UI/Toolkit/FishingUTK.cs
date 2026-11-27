// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
// U-G (2026-10-01): FishingUTK Figma fishing-hud-panel(160:11) 2바 리일 재구현 — 진행(물고기 당기는 중)+장력(라인/위험).
//   Figma 280×289·pad20/gap20: TitleRow→Line→ProgressSection(라벨+% + progress-bar)→TensionSection(라벨+위험 + tension-bar)→Line→ActionGrid(keycap-SPACE 잡기 / keycap-Q 취소).
//   시스템(FishingSystem.TryReel/ReelProgress/ReelTension)은 Figma 2바 시뮬로 재구성됨. 레거시 1바 핀(핀·스위트스팟·프레임 베이크)는 제거.
//   입력: SPACE=TryReel, Q=취소, ESC=취소.
//   [144차 P3-FISH] fishing.md 행 근거 수리 — 코너 데칼 P0(#18), 글래스 크롬(#6/#7/#8), 패딩·간격 24px 체계(#10~#17),
//   타이틀/섹션 폰트·색(#19~#22/#27/#28/#35), 구분선 크롬·두께(#25/#26), 바 스타일(#30/#37), 위험 배지 위치(#36).
//   진행바 외부 그림자(#33)는 UI Toolkit 미지원 — 보류 기록(BuildBar 주석). game-header-bar/HUD-tip-bar/액션카드/
//   interaction-panel-fishing(163:4)는 ③-b 후속 태스크 소관 — 본 파일은 패널 본체만 수정.
#pragma warning disable 114
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Systems;   // FishingSystem

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit — 낚시 미니게임 (UTKWindowBase 파생).
    /// Figma fishing-hud-panel(160:11)을 GitHub-dark로 재현 — 2바(진행/장력) 리일.
    /// 상태/판정은 모두 FishingSystem가 소유. 원본 FishingUI.cs(IMGUI)는 절대 수정하지 않는다.
    /// </summary>
    public class FishingUTK : UTKWindowBase
    {
        private static FishingUTK _instance;
        private static Updater _updater;

        private const float WinW = 336f;   // Archived 160:11 fishing-hud-panel
        private const float WinH = 345.6f;
        private const long TickMs = 50L;

        // 바 (Figma 240×8 두께)
        private readonly VisualElement _progressFill;
        private readonly Label _progressPct;
        private readonly VisualElement _tensionFill;
        private readonly Label _tensionState;
        private Label _tDanger;   // [144차 P3-FISH] #36 — Tension 라벨행 우측 "위험!" 배지(160:39)
        private readonly Label _hintLabel;
        private VisualElement _cardReel;    // [144차 P3-FISH2] #41 — ActionReel 카드(160:44~47)
        private VisualElement _cardCancel;  // [144차 P3-FISH2] #42 — ActionCancel 카드(160:48~51)
        private VisualElement _headerBar;   // [144차 P3-FISH2] #43 — game-header-bar(160:5~10)
        private VisualElement _tipBar;      // [144차 P3-FISH2] #44 — HUD-tip-bar(160:52~61)
        private readonly Label _popupLabel;
        private readonly VisualElement _popupHost;
        private readonly VisualElement _bite;   // 입질 대기 물결 찌 (P29 유지)
        private IVisualElementScheduledItem _tickTask;

        public static FishingUTK Instance => _instance;

        private FishingUTK() : base("🎣 낚시", new Vector2(WinW, WinH))
        {
            SetChrome(UTKWindowChrome.Frameless);
            // [144차 P3-FISH] #6/#7/#8 — Frameless 기본 크롬(UTKTheme.Panel 불투명/보더 0/r 0 — UTKWindowBase:100~103)을
            // Figma 160:11 글래스 크롬으로 파생 오버라이드(UTKWindowBase 수정 금지 계약 — 본 파일에서 최종 적용).
            style.backgroundColor = new StyleColor(UTKTheme.GlassPanelFill);   // #161B22@0.702(0.85 폐기 — V8 D1 확정)
            UTKTheme.ApplyElementBorder(this);                                  // #30363D@0.502 sw1.2
            UTKTheme.ApplyRadius(this, UTKTheme.GlassRadius);                   // r14.4
            // [144차 P3-FISH] #18 P0 — 코너 데칼 4개(160:12~23 — 9.6×9.6 L자 2선 #58A6FF w1.8) — Phase 0 V1 공용 어휘.
            // outsideCorner=false(안쪽 inset 4): .utk-window overflow:hidden(Theme.uss)이라 코너 밖 걸침(−4.8)은 클립된다.
            // 진실치는 데칼이 패널 모서리 접합(inset 0) — V1 inside 모드 고정 인셋 4와의 차이는 어휘 근사(P2 기록).
            UTKTheme.AddCornerDecals(_content, 9.6f, 1.8f, "#58A6FF", outsideCorner: false);
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;
            _content.style.paddingLeft = 24f; _content.style.paddingRight = 24f;
            _content.style.paddingTop = 24f; _content.style.paddingBottom = 24f;

            _popupHost = new VisualElement();
            _popupHost.style.position = Position.Absolute;
            _popupHost.style.left = 0; _popupHost.style.right = 0; _popupHost.style.top = 0; _popupHost.style.bottom = 0;
            _popupHost.style.alignItems = Align.Center;
            _popupHost.style.justifyContent = Justify.Center;
            _popupHost.style.display = DisplayStyle.None;
            _popupHost.pickingMode = PickingMode.Ignore;
            _content.Add(_popupHost);

            _popupLabel = new Label("");
            _popupLabel.style.fontSize = 16.8f;
            _popupLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _popupLabel.style.color = new StyleColor(GitHubDark.Accent);
            _popupLabel.style.whiteSpace = WhiteSpace.Normal;
            _popupLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _popupHost.Add(_popupLabel);

            // ── TitleRow: 🎣 낚시하기 + ACTIVE ── [Figma 160:24]
            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            var titleIcon = new Label("🎣");
            // [144차 P3-FISH] #19 — 진실치는 벡터 16.8×16.8 stroke #58A6FF(160:26) — 이모지 유지+크기만 16.8 근사(기록).
            titleIcon.style.fontSize = 16.8f;
            titleRow.Add(titleIcon);
            var titleLbl = new Label("낚시하기");
            titleLbl.style.marginLeft = 7.2f;   // [144차 P3-FISH] #20 — 아이콘↔라벨 gap 7.2(160:25, 구 4)
            titleLbl.style.fontSize = 15.6f;    // [144차 P3-FISH] #21 — 15.6(구 14.4, 160:27)
            titleLbl.style.unityFontStyleAndWeight = FontStyle.Bold;   // [144차 P3-FISH] #21 — 700(구 Normal)
            titleLbl.style.color = new StyleColor(GitHubDark.White);   // [144차 P3-FISH] #21 — #FFFFFF(구 TextMain #F0F6FC)
            titleLbl.style.flexGrow = 1f;
            titleRow.Add(titleLbl);
            _tensionState = new Label("ACTIVE");   // 원래 장력 상태 배지 자리(상단) — 대기 시에도 상태 표시
            _tensionState.style.fontSize = 13.2f;   // [144차 P3-FISH] #22 — 13.2(구 12, 160:28)
            _tensionState.style.unityFontStyleAndWeight = FontStyle.Bold;   // [144차 P3-FISH] #22 — 700(구 Normal), 색 Accent 유지
            _tensionState.style.color = new StyleColor(GitHubDark.Accent);
            titleRow.Add(_tensionState);
            _content.Add(titleRow);

            // ── 구분선 ── [Figma 160:29]
            var sep1 = new VisualElement();
            // [144차 P3-FISH] #26 — 두께 1.2(160:29 진실치 strokeW — Figma 노드는 h0 라인, 코드는 보이는 두께로 렌더).
            sep1.style.height = 1.2f;
            sep1.style.marginTop = 24f;   // [144차 P3-FISH] #11 — TitleRow↔Line 24(412→436, 구 2)
            sep1.style.marginBottom = 22.8f;   // [144차 P3-FISH] #12 — Line↔Progress 24 − 구분선 두께 1.2(앵커 460 정밀 일치)
            sep1.style.backgroundColor = new StyleColor(UTKTheme.BorderChrome);   // [144차 P3-FISH] #25 — #30363D@0.502(구 Stroke #2E343D 불투명)
            _content.Add(sep1);

            // ── ProgressSection: 물고기를 당기는 중 + 65% + progress-bar ── [Figma 160:30]
            MakeSectionRow("물고기를 당기는 중...", out _progressPct);
            _progressFill = BuildBar(new Color32(0x58, 0xA6, 0xFF, 0xFF));   // 진행 = 액센트 파랑

            // ── TensionSection: 라인 장력 (Tension) + 위험! + tension-bar ── [Figma 160:36]
            var tensionLabel = new VisualElement();
            tensionLabel.style.marginTop = 23.8f;   // [144차 P3-FISH] #13 — Progress↔Tension 23.8(494.2→518; 바 mb 0과 합산)
            tensionLabel.style.flexDirection = FlexDirection.Row;
            tensionLabel.style.alignItems = Align.Center;
            _content.Add(tensionLabel);
            var tLabel = new Label("라인 장력 (Tension)");
            tLabel.style.fontSize = 13.2f;
            // [144차 P3-FISH] #35 — #C9D1D9 + Bold(구 TextSub #8B949E Normal — 160:38 진실치 700).
            tLabel.style.color = new StyleColor(GitHubDark.TextBody);
            tLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            tLabel.style.flexGrow = 1f;
            tensionLabel.Add(tLabel);
            // [144차 P3-FISH] #36 — "위험!" 배지 Tension 라벨행 우측 표시로 전환(160:39 — 구 TitleRow 대체 표시 폐기).
            // 색 #F85149는 의미색(위험) 유지 원칙 적용(진실치 #FFB703 — 판정 제외 기록), 웨이트 700은 진실치 반영.
            _tDanger = new Label("위험!");
            _tDanger.style.fontSize = 13.2f;
            _tDanger.style.color = new StyleColor(GitHubDark.Danger);
            _tDanger.style.unityFontStyleAndWeight = FontStyle.Bold;
            _tDanger.visible = false;
            tensionLabel.Add(_tDanger);
            _tensionFill = BuildBar(new Color32(0x3F, 0xB9, 0x50, 0xFF));   // 장력 = 안전 녹색 → 위험 시 담당자가 빨강

            // ── 구분선 ── [Figma 160:42]
            var sep2 = new VisualElement();
            // [144차 P3-FISH] #26 — 두께 1.2(160:42 진실치 strokeW).
            sep2.style.height = 1.2f;
            sep2.style.marginTop = 24.8f;   // [144차 P3-FISH] #14 — Tension↔Line 24.8(552.2→577, 구 2)
            sep2.style.marginBottom = 22.8f;   // [144차 P3-FISH] #15 — Line↔ActionGrid 24 − 구분선 두께 1.2(앵커 601 정밀 일치)
            sep2.style.backgroundColor = new StyleColor(UTKTheme.BorderChrome);   // [144차 P3-FISH] #25 — #30363D@0.502
            _content.Add(sep2);

            // ── ActionGrid: SPACE 잡기 / Q 취소 ── [Figma 160:43]
            var actionGrid = new VisualElement();
            actionGrid.style.flexDirection = FlexDirection.Column;
            actionGrid.style.marginTop = 0f;   // [144차 P3-FISH] #15 — 간격은 sep2 marginBottom 22.8로 이관(구 4 폐기)
            _hintLabel = new Label("");
            _hintLabel.style.fontSize = 12f;
            _hintLabel.style.color = new StyleColor(GitHubDark.TextSub);
            _hintLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            _hintLabel.style.whiteSpace = WhiteSpace.Normal;
            _hintLabel.style.display = DisplayStyle.None;
            actionGrid.Add(_hintLabel);

            // [144차 P3-FISH2] #41/#42 — Figma 160:44~51 액션 카드 2세트(구 텍스트 힌트 대체 — 대기 상태 힌트는 유지).
            //   카드 288×40.8 · 패드 9.6 · gap 7.2(카드간) · r4.8.
            //   Reel: fill #30363D@0.8 · stroke #58A6FF 1.2 + keycap-SPACE 57.6×21.6(fill/stroke #58A6FF, "SPACE" 10.8/700 #161B22)
            //         + "릴 감기 (물고기 저항)" 13.2/700 #58A6FF.
            //   Cancel: fill #21262D@0.8 · stroke #30363D 1.2 + keycap-Q 21.6×21.6(fill/stroke #8B949E, "Q" 12/700 #161B22)
            //         + "낚시 취소하기" 13.2/700 #8B949E. keycap 그림자 7.2@0.4는 UI Toolkit 미지원 — 보류 기록.
            _cardReel = BuildActionCard(reel: true);
            _cardCancel = BuildActionCard(reel: false);
            actionGrid.Add(_cardReel);
            actionGrid.Add(_cardCancel);
            _content.Add(actionGrid);

            // 대기 중 물결 찌 (P29 고품질화 — 입질 대기 시 표시)
            _bite = new VisualElement();
            _bite.style.position = Position.Absolute;
            _bite.style.alignSelf = Align.Center;
            _bite.style.left = 24f; _bite.style.top = 4f;
            _bite.style.width = 48f; _bite.style.height = 36f;
            _bite.AddToClassList("utk-slot");
            _bite.style.backgroundImage = UTKTextureSafe.ToBackground(Resources.Load<Texture2D>("UI/FishingBite"));
            _bite.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            _content.Add(_bite);

            ApplyUIToolkitFont(this);
            style.display = DisplayStyle.None;
            ApplyCanvasBounds();
        }

        private void ApplyCanvasBounds()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return;
            var frameBounds = new Rect(792f, 367.2f, 336f, 345.6f);
            FigmaCanvasLayout.Apply(this, frameBounds, root);

            Vector2 rootSize = new Vector2(root.resolvedStyle.width, root.resolvedStyle.height);
            float sx = rootSize.x / FigmaCanvasLayout.CanvasWidth;
            float sy = rootSize.y / FigmaCanvasLayout.CanvasHeight;
            _content.style.position = Position.Absolute;
            _content.style.left = 0f;
            _content.style.top = 0f;
            // Keep the content tree in canonical Figma units and scale it as one visual subtree.
            // This scales typography, bars, spacing and keycaps consistently on both axes while
            // preserving the outer frame bounds and leaving the 1920×1080 layout unchanged.
            _content.style.width = frameBounds.width;
            _content.style.height = frameBounds.height;
            // [144차 P3-FISH] #10 — 패널 패딩 24 복원(구 재정의 20 폐기 — 내용폭 296→288 부수효과 해소, 160:11 pl/pr/pt/pb 24).
            _content.style.paddingLeft = 24f;
            _content.style.paddingRight = 24f;
            _content.style.paddingTop = 24f;
            _content.style.paddingBottom = 24f;
            _content.style.transformOrigin = new TransformOrigin(0f, 0f, 0f);
            _content.style.scale = new StyleScale(new Scale(new Vector2(sx, sy)));
        }

        /// <summary>[144차 P3-FISH2] #41/#42 — Figma 160:44~51 액션 카드 빌더.
        /// reel=true: SPACE 키캡 57.6×21.6 + "릴 감기 (물고기 저항)" / false: Q 키캡 21.6×21.6 + "낚시 취소하기".</summary>
        private VisualElement BuildActionCard(bool reel)
        {
            var card = new VisualElement();
            card.style.height = 40.8f;
            card.style.flexDirection = FlexDirection.Row;
            card.style.alignItems = Align.Center;
            card.style.paddingLeft = 9.6f; card.style.paddingRight = 9.6f;
            card.style.paddingTop = 9.6f; card.style.paddingBottom = 9.6f;
            card.style.marginBottom = 7.2f;   // 카드 간 gap 7.2(패널 gap 패밀리)
            UTKTheme.ApplyRadius(card, 4.8f);
            if (reel)
            {
                card.style.backgroundColor = new Color(0x30 / 255f, 0x36 / 255f, 0x3D / 255f, 0.8f);   // #30363D@0.8
                UTKTheme.ApplyElementBorder(card, GitHubDark.Accent, 1.2f);   // #58A6FF 1.2
            }
            else
            {
                card.style.backgroundColor = new Color(0x21 / 255f, 0x26 / 255f, 0x2D / 255f, 0.8f);   // #21262D@0.8
                UTKTheme.ApplyElementBorder(card, new Color(0x30 / 255f, 0x36 / 255f, 0x3D / 255f, 1f), 1.2f);   // #30363D 1.2
            }

            var key = new Label(reel ? "SPACE" : "Q");
            key.style.width = reel ? 57.6f : 21.6f;
            key.style.height = 21.6f;
            key.style.backgroundColor = new StyleColor(reel ? GitHubDark.Accent : GitHubDark.TextSub);
            UTKTheme.ApplyElementBorder(key, reel ? GitHubDark.Accent : GitHubDark.TextSub, 1.2f);
            UTKTheme.ApplyRadius(key, 4.8f);
            key.style.fontSize = reel ? 10.8f : 12f;
            key.style.unityFontStyleAndWeight = FontStyle.Bold;
            key.style.color = new StyleColor(new Color(0x16 / 255f, 0x1B / 255f, 0x22 / 255f, 1f));   // #161B22
            key.style.unityTextAlign = TextAnchor.MiddleCenter;
            card.Add(key);

            var lbl = new Label(reel ? "릴 감기 (물고기 저항)" : "낚시 취소하기");
            lbl.style.fontSize = 13.2f;
            lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            lbl.style.color = new StyleColor(reel ? GitHubDark.Accent : GitHubDark.TextSub);
            lbl.style.marginLeft = 9.6f;   // 키캡↔라벨 gap(패널 gap 패밀리 — 160:44 gap 9.6)
            card.Add(lbl);
            return card;
        }

        /// <summary>[144차 P3-FISH2] #43/#44 — game-header-bar(160:5~10) + HUD-tip-bar(160:52~61) 스크린 오버레이.
        /// FishingUTK 창은 336×345.6 패널이라 바 2종은 UIRoot 직속(FigmaCanvasLayout.Place — 디자인공간 좌표)에 두고
        /// Show/Hide와 표시를 동기화한다. 내부 요소는 raw Figma px + 스케일 래퍼(캔버스 크기 변화 대응).</summary>
        private void EnsureScreenBars()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return;
            float sx = root.resolvedStyle.width / FigmaCanvasLayout.CanvasWidth;
            float sy = root.resolvedStyle.height / FigmaCanvasLayout.CanvasHeight;

            if (_headerBar == null)
            {
                _headerBar = FigmaCanvasLayout.Place(new Rect(0f, 0f, 1920f, 76.8f));
                if (_headerBar == null) return;
                _headerBar.style.backgroundColor = new Color(0x0D / 255f, 0x11 / 255f, 0x17 / 255f, 0.502f);   // #0D1117@0.502
                UTKTheme.ApplyElementBorder(_headerBar, UTKTheme.BorderChrome, UTKTheme.BorderChromeWidth);
                _headerBar.pickingMode = PickingMode.Ignore;

                var inner = new VisualElement();   // raw Figma px + 스케일 래퍼
                inner.style.width = 1920f; inner.style.height = 76.8f;
                inner.style.transformOrigin = new TransformOrigin(0f, 0f, 0f);
                inner.style.scale = new StyleScale(new Scale(new Vector2(sx, sy)));
                inner.style.flexDirection = FlexDirection.Row;
                inner.style.alignItems = Align.Center;
                inner.style.paddingLeft = 48f; inner.style.paddingRight = 48f;   // pl/pr 48
                _headerBar.Add(inner);

                var anchorIcon = new Label("⚓");   // 160:57/58 벡터 21.6 — 글리프 근사(기록)
                anchorIcon.style.fontSize = 21.6f;
                anchorIcon.style.color = GitHubDark.Accent;
                inner.Add(anchorIcon);

                var questTitle = new Label("[돌발 임무] 신비로운 물고기 낚시");   // 160:8 — Figma 문구(동적 연결 후보 기록)
                questTitle.style.fontSize = 16.8f;
                questTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                questTitle.style.color = GitHubDark.White;
                questTitle.style.marginLeft = 14.4f;   // 아이콘(48~69.6) 후 타이틀 x84
                inner.Add(questTitle);

                var spacer = new VisualElement();
                spacer.style.flexGrow = 1f;
                inner.Add(spacer);

                var spot = new Label("지역: 고요한 푸른 호수");   // 160:10 — 우측 pr48 정렬
                spot.style.fontSize = 15.6f;
                spot.style.unityFontStyleAndWeight = FontStyle.Bold;
                spot.style.color = GitHubDark.TextSub;
                inner.Add(spot);

                // 캔버스 크기 변화 대응 — 스케일 재적용
                _headerBar.RegisterCallback<GeometryChangedEvent>(_ =>
                {
                    float rx = root.resolvedStyle.width / FigmaCanvasLayout.CanvasWidth;
                    float ry = root.resolvedStyle.height / FigmaCanvasLayout.CanvasHeight;
                    inner.style.scale = new StyleScale(new Scale(new Vector2(rx, ry)));
                });
            }
            if (_tipBar == null)
            {
                _tipBar = FigmaCanvasLayout.Place(new Rect(0f, 1006f, 1920f, 36f));
                if (_tipBar == null) return;
                _tipBar.style.backgroundColor = StyleKeyword.None;
                _tipBar.pickingMode = PickingMode.Ignore;

                var box = new VisualElement();   // 372.8×36 화면중앙(774) — 컨테이너
                box.style.position = Position.Absolute;
                box.style.left = 774f; box.style.top = 0f;
                box.style.width = 372.8f; box.style.height = 36f;
                box.style.backgroundColor = new Color(0x16 / 255f, 0x1B / 255f, 0x22 / 255f, 0.702f);   // #161B22@0.702
                UTKTheme.ApplyElementBorder(box, UTKTheme.BorderChrome, UTKTheme.BorderChromeWidth);   // #30363D@0.502 1.2
                UTKTheme.ApplyRadius(box, 7.2f);
                box.style.opacity = 0.8f;
                box.style.flexDirection = FlexDirection.Row;
                box.style.alignItems = Align.Center;
                box.style.paddingLeft = 19.2f; box.style.paddingRight = 19.2f;
                _tipBar.Add(box);

                var inner = new VisualElement();   // 스케일 래퍼
                inner.style.width = 372.8f; inner.style.height = 36f;
                inner.style.transformOrigin = new TransformOrigin(0f, 0f, 0f);
                inner.style.scale = new StyleScale(new Scale(new Vector2(sx, sy)));
                inner.style.flexDirection = FlexDirection.Row;
                inner.style.alignItems = Align.Center;
                inner.style.flexGrow = 1f;
                box.Add(inner);

                var infoIcon = new Label("ⓘ");   // 160:60/61 벡터 16.8 #8B949E — 글리프 근사(기록)
                infoIcon.style.fontSize = 16.8f;
                infoIcon.style.color = GitHubDark.TextSub;
                inner.Add(infoIcon);

                var tipText = new Label("장력이 100%에 도달하면 라인이 끊어지니 주의하십시오.");   // 160:55
                tipText.style.fontSize = 13.2f;
                tipText.style.unityFontStyleAndWeight = FontStyle.Bold;
                tipText.style.color = GitHubDark.TextSub;
                tipText.style.marginLeft = 9.6f;   // gap 9.6
                tipText.style.whiteSpace = WhiteSpace.NoWrap;
                inner.Add(tipText);

                _tipBar.RegisterCallback<GeometryChangedEvent>(_ =>
                {
                    float rx = root.resolvedStyle.width / FigmaCanvasLayout.CanvasWidth;
                    float ry = root.resolvedStyle.height / FigmaCanvasLayout.CanvasHeight;
                    inner.style.scale = new StyleScale(new Scale(new Vector2(rx, ry)));
                });
            }
        }

        /// <summary>[144차 P3-FISH2] 바 2종 표시 동기화 — Show에서 Flex, Hide에서 None.</summary>
        private void SetScreenBarsVisible(bool visible)
        {
            if (visible) EnsureScreenBars();
            if (_headerBar != null) _headerBar.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (_tipBar != null) _tipBar.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }


        private VisualElement MakeSectionRow(string labelText, out Label pctLabel)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            var label = new Label(labelText);
            label.style.fontSize = 13.2f;
            // [144차 P3-FISH] #27 — #C9D1D9 + Bold(구 TextSub #8B949E Normal — 160:32 진실치 700).
            label.style.color = new StyleColor(GitHubDark.TextBody);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.flexGrow = 1f;
            row.Add(label);
            pctLabel = new Label("0%");
            pctLabel.style.fontSize = 13.2f;
            // [144차 P3-FISH] #28 — #58A6FF + Bold(구 TextMain #F0F6FC Normal — 160:33 진실치).
            pctLabel.style.color = new StyleColor(GitHubDark.Accent);
            pctLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(pctLabel);
            _content.Add(row);
            return row;
        }

        /// <summary>[Figma 160:34/40] 288×9.6 바 트랙 + 채움. [144차 P3-FISH] #30/#37 —
        /// #161B22·h9.6·r4.8·스트로크 #30363D(불투명 진실치) 1.2 — 패널/구분선(0.502)과 알파 상이 확인 적용.</summary>
        private VisualElement BuildBar(Color fillColor)
        {
            var track = new VisualElement();
            track.style.height = 9.6f;        // [144차 P3-FISH] #30/#37 — h9.6(구 8)
            track.style.marginTop = 9.6f;     // [144차 P3-FISH] #16/#17 — 라벨행↔바 9.6(구 2)
            track.style.marginBottom = 0f;    // [144차 P3-FISH] #13/#14 — 섹션 간격은 다음 요소 marginTop(23.8/24.8)으로 단일화(구 2)
            track.style.backgroundColor = new StyleColor(GitHubDark.Panel);   // [144차 P3-FISH] #30/#37 — #161B22(구 Sub #21262D)
            UTKTheme.ApplyRadius(track, 4.8f);   // [144차 P3-FISH] #30/#37 — r4.8(구 2)
            UTKTheme.ApplyElementBorder(track, GitHubDark.BarStroke, UTKTheme.BorderChromeWidth);   // [144차 P3-FISH] #30/#37 — #30363D 1.2(구 없음)
            track.style.overflow = Overflow.Hidden;   // fill이 스트로크를 덮지 않게 클립(라운드 코너 포함)
            _content.Add(track);

            var fill = new VisualElement();
            fill.name = "BarFill";
            fill.style.position = Position.Absolute;
            fill.style.left = 0; fill.style.top = 0; fill.style.bottom = 0;
            fill.style.width = 0f;
            fill.style.backgroundColor = new StyleColor(fillColor);
            track.Add(fill);
            // [144차 P3-FISH] #33 — progress-fill fx DROP_SHADOW:4.8 #58A6FF/0.4: UI Toolkit 런타임은 외부
            // drop-shadow 미지원. 내부 밝은 스트립 근사는 발광 위치가 '바 아래(외부)'가 아닌 '바 안'이 되어
            // 진실치와 정반대 렌더 → 근사 대신 보류 기록(판단). 향후 9슬라이스 글로우 텍스처 도입 시 재검토.
            return fill;
        }

        // ===== GitHub-dark 팔레트 (이 창 한정) =====
        private static class GitHubDark
        {
            public static readonly Color Panel    = Hex(0x161B22);
            public static readonly Color Sub      = Hex(0x21262D);
            public static readonly Color Accent   = Hex(0x58A6FF);
            public static readonly Color TextMain = Hex(0xF0F6FC);
            public static readonly Color TextSub  = Hex(0x8B949E);
            public static readonly Color Stroke   = Hex(0x2E343D);
            public static readonly Color Danger   = Hex(0xF85149);
            // [144차 P3-FISH] 신규 토큰 — #21/#27/#28/#35/#30·37 수리분(진실치 인라인 근거).
            public static readonly Color White     = Hex(0xFFFFFF);   // #21 "낚시하기" #FFFFFF(160:27)
            public static readonly Color TextBody  = Hex(0xC9D1D9);   // #27/#35 섹션 라벨 #C9D1D9(160:32/38)
            public static readonly Color BarStroke = Hex(0x30363D);   // #30/#37 바 스트로크 #30363D 불투명(160:34/40 — 패널 0.502와 알파 상이)
            private static Color Hex(uint rgb) =>
                new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 0xFF);
        }

        // ===== 공개 API =====

        /// <summary>[2026-10-11 수리] 전 씬 자가부트 — Test_15처럼 GameSetup(메인씬 전용)을 거치지
        /// 않는 씬에서는 Ensure가 한 번도 불리지 않아 Updater(자동 오픈/닫기 폴링)가 부재했다:
        /// E키로 낚시 "시스템 상태"는 시작되지만 창이 영구 미오픈 → 사용자에게는 아무 일도
        /// 일어나지 않는다("호수에서 낚시 안 됨"의 실체 — 로그: FishUTK 인스턴스 생성 0건).
        /// TimeClockGlassUTK/HUDUTK의 [RuntimeInitializeOnLoadMethod] 선례와 동일.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => Ensure();

        /// <summary>싱글톤 + 입력 Updater 보장 (멱등).</summary>
        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new FishingUTK();
            var go = new GameObject("FishingUTK_UPD");
            Object.DontDestroyOnLoad(go);
            _updater = go.AddComponent<Updater>();
            _updater.window = _instance;
            Debug.Log("[FishUTK] 인스턴스 생성");
        }

        /// <summary>창 오픈.</summary>
        public static void Open()
        {
            Ensure();
            _instance?.Show();
        }

        /// <summary>창 닫기.</summary>
        public static void CloseUI() => _instance?.Hide();

        /// <summary>토글.</summary>
        public static void Toggle()
        {
            if (_instance != null && _instance.IsOpen) { _instance.Hide(); return; }
            Open();
        }

        // ===== 생명주기 =====

        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            ApplyCanvasBounds();
            SetScreenBarsVisible(true);   // [144차 P3-FISH2] #43/#44 — 헤더바/팁바 표시 동기화
            StartTick();
            Refresh();
        }

        public override void Hide()
        {
            StopTick();
            SetScreenBarsVisible(false);   // [144차 P3-FISH2] — 바 2종 숨김(UIRoot 잔존, 재Show 재표시)
            base.Hide();
        }

        private void StartTick()
        {
            if (_tickTask != null) return;
            _tickTask = schedule.Execute(() =>
            {
                if (IsOpen) Refresh();
            }).Every(TickMs);
        }

        private void StopTick()
        {
            if (_tickTask != null)
            {
                _tickTask.Pause();
                _tickTask = null;
            }
        }

        // ===== 화면 갱신 =====

        private void Refresh()
        {
            var fs = FishingSystem.Instance;
            if (fs == null) return;

            // 팝업 메시지
            bool showPopup = fs.PopupTimer > 0f && !string.IsNullOrEmpty(fs.PopupMessage);
            _popupHost.style.display = showPopup ? DisplayStyle.Flex : DisplayStyle.None;
            if (showPopup) _popupLabel.text = fs.PopupMessage;

            // 미니게임 활성 시에만 바 갱신, 대기 중에는 입질 찌 표시
            bool waiting = fs.IsWaitingForBite;
            bool active = fs.IsMinigameActive;
            _progressFill.visible = active;
            _tensionFill.visible = active;
            _bite.visible = waiting;
            _progressPct.visible = active;
            // [144차 P3-FISH2] #41/#42 — 미니게임/비활성 = Figma 액션 카드 2세트, 대기 = 기존 힌트 라벨.
            bool cardsVisible = active || (!waiting && !active);
            _hintLabel.style.display = waiting ? DisplayStyle.Flex : DisplayStyle.None;
            if (_cardReel != null) _cardReel.style.display = cardsVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (_cardCancel != null) _cardCancel.style.display = cardsVisible ? DisplayStyle.Flex : DisplayStyle.None;

            if (waiting)
            {
                _tensionState.text = "대기 중";
                _tensionState.style.color = new StyleColor(GitHubDark.TextSub);
                SetTensionDanger(false);   // [144차 P3-FISH] #36 — 비활성 구간 배지 숨김 유지
                _hintLabel.text = "🎣 입질을 기다리는 중... (ESC: 취소)";
            }
            else if (active)
            {
                _tensionState.text = "ACTIVE";
                _tensionState.style.color = new StyleColor(GitHubDark.Accent);

                int pct = Mathf.RoundToInt(Mathf.Clamp01(fs.ReelProgress) * 100f);
                _progressPct.text = $"{pct}%";

                // 진행 바 — Figma progress-fill
                float prog = Mathf.Clamp01(fs.ReelProgress);
                _progressFill.style.width = new Length(prog * 100f, LengthUnit.Percent);

                // 장력 바 — Figma tension-fill (색상: 안전 녹 → 위험 빨강)
                float tens = Mathf.Clamp01(fs.ReelTension);
                _tensionFill.style.width = new Length(tens * 100f, LengthUnit.Percent);
                if (tens >= fs.TensionDangerThreshold)
                {
                    _tensionFill.style.backgroundColor = new StyleColor(GitHubDark.Danger);
                    SetTensionDanger(true);
                }
                else
                {
                    _tensionFill.style.backgroundColor = new StyleColor(new Color32(0x3F, 0xB9, 0x50, 0xFF));
                    SetTensionDanger(false);
                }

                _hintLabel.text = "[SPACE] 잡기  [Q] 취소";
            }
            else
            {
                _tensionState.text = "ACTIVE";
                _tensionState.style.color = new StyleColor(GitHubDark.Accent);
                SetTensionDanger(false);   // [144차 P3-FISH] #36 — 비활성 구간 배지 숨김 유지
                _hintLabel.text = "[SPACE] 잡기  [Q] 취소";
            }
        }

        /// <summary>[Figma 160:39] 장력 위험 배지 표시/숨김 — [144차 P3-FISH] #36: Tension 라벨행 우측
        /// 비표시 요소(_tDanger) 활성화 방식으로 수리. TitleRow 배지(_tensionState)는 상태 라벨(ACTIVE/대기 중)
        /// 전용 — 위험 대체 표시(구 구현) 폐기.</summary>
        private void SetTensionDanger(bool on)
        {
            if (_tDanger != null) _tDanger.visible = on;
        }

        // ===== 입력 Updater =====

        private class Updater : MonoBehaviour
        {
            public FishingUTK window;

            private void Update()
            {
                var root = UIToolkitBootstrap.UIRoot;
                if (root != null && window != null && window.parent == null)
                    root.Add(window);

                var fs = FishingSystem.Instance;
                if (fs == null) return;

                // [P29] 낚시 상태 폴링 자동 오픈/닫기
                bool active = fs.IsWaitingForBite || fs.IsMinigameActive;
                bool shouldOpen = window != null && fs.IsFishing && active;
                if (shouldOpen && !FishingUTK.Instance.IsOpen)
                    FishingUTK.Open();
                else if (!shouldOpen && FishingUTK.Instance.IsOpen)
                    FishingUTK.CloseUI();

                if (window == null || !window.IsOpen) return;

                if (fs.IsMinigameActive)
                {
                    if (UnityEngine.Input.GetKeyDown(KeyCode.Space))
                    {
                        fs.TryReel();
                        Debug.Log("[FishUTK] Space — 리일");
                    }
                }

                if (UnityEngine.Input.GetKeyDown(KeyCode.Q) || UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                {
                    fs.CancelFishing();
                    Debug.Log("[FishUTK] Q/ESC — 취소");
                }
            }

            private void OnDestroy()
            {
                if (window != null)
                {
                    window.StopTick();
                    window.RemoveFromHierarchy();
                }
            }
        }
    }
}