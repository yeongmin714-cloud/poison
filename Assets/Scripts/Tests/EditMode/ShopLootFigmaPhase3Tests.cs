using NUnit.Framework;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using ProjectName.Core;
using ProjectName.UI.Toolkit;

namespace ProjectName.Tests.EditMode
{
    public class ShopLootFigmaPhase3Tests
    {
        private static readonly BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
        private GameObject _documentObject;
        private UIDocument _previousDocument;
        private LootWindowUTK _previousInstance;

        [SetUp]
        public void SetUp()
        {
            _previousInstance = (LootWindowUTK)typeof(LootWindowUTK)
                .GetField("_instance", StaticPrivate).GetValue(null);
            typeof(LootWindowUTK).GetField("_instance", StaticPrivate).SetValue(null, null);
            _previousDocument = (UIDocument)typeof(UIToolkitBootstrap)
                .GetField("_document", StaticPrivate).GetValue(null);
            _documentObject = new GameObject("ShopLootFigmaPhase3TestDocument");
            var document = _documentObject.AddComponent<UIDocument>();
            document.panelSettings = Resources.Load<PanelSettings>("UI/PanelSettings");
            typeof(UIToolkitBootstrap).GetField("_document", StaticPrivate).SetValue(null, document);
        }

        [TearDown]
        public void TearDown()
        {
            if (LootWindowUTK.Instance != null)
                LootWindowUTK.Instance.RemoveFromHierarchy();
            typeof(LootWindowUTK).GetField("_instance", StaticPrivate).SetValue(null, _previousInstance);
            typeof(UIToolkitBootstrap).GetField("_document", StaticPrivate).SetValue(null, _previousDocument);
            if (_documentObject != null) Object.DestroyImmediate(_documentObject);
        }

        [Test]
        public void LootPanel_UsesSavedFigma70_4BoundsAndTenVisibleSlots()
        {
            AssertRect(new Rect(708f, 324f, 504f, 432f), LootWindowUTK.FigmaBounds);
            Assert.That(LootWindowUTK.VisibleSlotCount, Is.EqualTo(10));
            Assert.That(LootWindowUTK.GridColumns, Is.EqualTo(5));
            Assert.That(LootWindowUTK.GetRenderedSlotCount(0), Is.EqualTo(10));
            Assert.That(LootWindowUTK.GetRenderedSlotCount(3), Is.EqualTo(10));
            Assert.That(LootWindowUTK.GridViewportHeight, Is.EqualTo(172.8f).Within(0.001f));
            Assert.That(LootWindowUTK.GetGridContentHeight(10), Is.EqualTo(LootWindowUTK.GridViewportHeight).Within(0.001f),
                "Two 81.6px rows plus one 9.6px inter-row gap exactly fill the viewport.");
            Assert.That(LootWindowUTK.GetGridSlotBottomMargin(4, 10), Is.EqualTo(LootWindowUTK.SlotGap));
            Assert.That(LootWindowUTK.GetGridSlotBottomMargin(9, 10), Is.Zero,
                "The final visible row must not add a bottom gap beyond the fixed viewport.");
        }

        [Test]
        public void LootPanel_OverflowEntriesRemainRepresentedAfterTenFixedSlots()
        {
            Assert.That(LootWindowUTK.GetRenderedSlotCount(11), Is.EqualTo(11));
            Assert.That(LootWindowUTK.GetRenderedSlotCount(25), Is.EqualTo(25));
            Assert.That(LootWindowUTK.GetOverflowSlotCount(25), Is.EqualTo(15));
            Assert.That(LootWindowUTK.GetGridContentHeight(25), Is.GreaterThan(LootWindowUTK.GridViewportHeight));
            Assert.That(LootWindowUTK.GetGridSlotBottomMargin(14, 15), Is.Zero,
                "The last currently rendered row has no trailing margin; prior rows keep their gap.");
        }

        [UnityTest]
        public IEnumerator LootWindow_AppliesEqualMultipleDesignSpaceContract()
        {
            // [Figma 정합 v3] 70:4 계약 — 창 박스=(708,324,504,432)×k, Content raw 504×432 + scale=k(등배수).
            // 주의: UIRoot는 스트레치(left/right/top/bottom=0)라 style.width 강제 설정 시 resolved가
            // 프레임마다 요동한다(실측 1919→1440) — 자연 resolved에서 k를 산출해 검증한다.
            yield return null;
            var canvasRoot = UIToolkitBootstrap.UIRoot;
            Assert.That(canvasRoot, Is.Not.Null, "UIDocument swap must provide UIRoot.");
            yield return null;
            yield return null;

            LootWindowUTK.Ensure();
            var loot = LootWindowUTK.Instance;
            typeof(LootWindowUTK)
                .GetMethod("ApplyFigmaPlacement", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(loot, new object[] { canvasRoot });
            yield return null;

            float k = Mathf.Min(canvasRoot.resolvedStyle.width / FigmaCanvasLayout.CanvasWidth,
                canvasRoot.resolvedStyle.height / FigmaCanvasLayout.CanvasHeight);
            Assert.That(k, Is.GreaterThan(0f), "루트 resolved가 해석돼야 한다.");
            AssertDesignSpaceContract(loot, LootWindowUTK.FigmaBounds, k);
            AssertRawSize(loot.Q<VisualElement>("LootPanelHeader"), 48f, "header height raw");
            AssertRawSize(loot.Q<VisualElement>("LootGridScroll"), 172.8f, "grid viewport height raw");
            AssertRawWidth(loot.Q<VisualElement>("LootGrid"), 456f, "grid width raw");
            AssertRawSize(loot.Q<VisualElement>("LootStats"), 26.6f, "stats row height raw");
        }

        [Test]
        public void LootWindow_UsesDesignSpaceReapplyPathWithoutIndependentXYScaling()
        {
            // 재적용 본체는 ApplyDesignSpace 단일 경로여야 한다 — X/Y 독립 스케일(FigmaCanvasLayout.Apply) 잔여 금지.
            string source = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/LootWindowUTK.cs");
            Assert.That(source, Does.Contain("FigmaCanvasLayout.ApplyDesignSpace(this, _content, _activeBounds, root)"),
                "창 박스는 활성 rect(기본 FigmaBounds, 조합 시 오버라이드)×k ApplyDesignSpace 한 경로로 적용된다.");
            Assert.That(source, Does.Not.Contain("FigmaCanvasLayout.Apply("),
                "X/Y 독립 스케일 경로(ScaleRect 곱셈)는 계약에서 제거됐다.");
            Assert.That(source, Does.Contain("AddCornerDecals(_content)"),
                "모서리 데칼은 디자인공간(_content)에 raw 좌표로 부착된다.");

            // [Figma 정합 v3 / 조합 배치(사용자 확정)] 전리품은 바구니 라우트에서 오른쪽 위(15:4 우측 컬럼 위치)
            AssertRect(new Rect(1272f, 84f, 504f, 432f), LootWindowUTK.CompositionBounds);
            string wire = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/UTKWireUp.cs");
            Assert.That(wire, Does.Contain("LootWindowUTK.Open(basket, LootWindowUTK.CompositionBounds)"),
                "바구니 라우트는 조합 배치 rect(오른쪽 위)를 전달해야 한다.");
        }

        /// <summary>[144차 P3-LOOT] Phase 3-① 전리품 Figma 정합 수리 소스계약 — 근거: TestOutput/figma_audit_144/loot_results.md
        /// (#14/#15 빈셀, #12/#13 Count, #17/#20/#22/#44 헤더, #30 푸터, #40 데칼). 괴리 수리에 따른 신규 계약 — 기능 계약 삭제 없음.</summary>
        [Test]
        public void LootWindow_144Phase3_FigmaParityRepairs()
        {
            string source = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/LootWindowUTK.cs");

            // [#14 P0] 빈 셀 square-dashed 38.4 — UTKSlot V3 어휘(프레임 모드 + 플레이스홀더).
            Assert.That(source, Does.Contain("slot.SetEmpty(true);"),
                "빈 셀은 UTKSlot V3 프레임 모드로 렌더된다(loot_results.md #14 P0).");
            Assert.That(source, Does.Contain("slot.SetEmptyIcon(true, UTKTheme.TextSub);"),
                "빈 셀 square-dashed 38.4 #8B949E 플레이스홀더(70:48-49 — BuildEmptySlot 아이콘 누락 수리).");

            // [#15 P1] 빈 셀 TierStrip #8B949E h3.6 상단 — 채움 셀과 동일 보더 어휘(전면 균일 보더 폐기).
            Assert.That(source, Does.Contain("slot.style.borderTopWidth = 3.6f;"),
                "빈 셀 상단 스트립 h3.6 보더 어휘(70:56 — #15).");
            Assert.That(source, Does.Contain("slot.SetTierStrip(true, UTKTheme.TextSub);"),
                "TierStrip 어휘 계약 호출(#15 — 내부 요소는 프레임 모드에서 숨김, 렌더는 borderTop 담당).");

            // [#12/#13] Count — 순수 숫자 + 우하단 앵커 (7.6, 4.6).
            Assert.That(source, Does.Contain("new Label(entry.Count.ToString())"),
                "Count는 순수 숫자 — \"x\" 접두사 금지(70:35 — #12).");
            Assert.That(source, Does.Contain("countLabel.style.right = 7.6f;"),
                "Count 우단 엣지=셀−7.6(70:35 — #13).");
            Assert.That(source, Does.Contain("countLabel.style.bottom = 4.6f;"),
                "Count 하단 4.6(70:35 — #13).");

            // [#17/#44] 헤더 pb 9.6 + 프레임 스트로크 #30363D@0.502 4면.
            Assert.That(source, Does.Contain("panelHeader.style.paddingBottom = 9.6f;"),
                "헤더 pb 9.6(70:18 — #17, Title 중앙정렬 y≈5.2 귀결 — #18).");
            int headerIdx = source.IndexOf("name = \"LootPanelHeader\"");
            int headerEnd = source.IndexOf("LootStats", headerIdx);
            Assert.That(headerIdx, Is.GreaterThanOrEqualTo(0), "LootPanelHeader 존재.");
            Assert.That(headerEnd, Is.GreaterThan(headerIdx), "헤더 블록 경계.");
            string headerBody = source.Substring(headerIdx, headerEnd - headerIdx);
            Assert.That(headerBody, Does.Contain("UTKTheme.ApplyElementBorder(panelHeader);"),
                "헤더 프레임 스트로크 #30363D@0.502 4면(70:18 — #44).");

            // [#20] Subtitle 재실측 y 15.2(구 10.2 정정).
            Assert.That(source, Does.Contain("lootSub.style.top = 15.2f;"),
                "Subtitle @(79,15.2) 재실측 진실치(loot_results.md #20).");
            Assert.That(source, Does.Not.Contain("lootSub.style.top = 10.2f;"),
                "구 기록 10.2 잔존 금지(재실측 정정분 — #20).");

            // [#22] 닫기 글리프 색 #8B949E(벡터→글리프 근사 유지).
            Assert.That(source, Does.Contain("headerClose.style.color = new StyleColor(UTKTheme.TextSub);"),
                "닫기 글리프 색 #8B949E(70:23 — #22).");

            // [#30] 푸터 스트로크 4면(상단 1면 폐기).
            int footerIdx = source.IndexOf("name = \"LootFooter\"");
            int footerEnd = source.IndexOf("_acquireAllBtn = new Button", footerIdx);
            Assert.That(footerIdx, Is.GreaterThanOrEqualTo(0), "LootFooter 존재.");
            Assert.That(footerEnd, Is.GreaterThan(footerIdx), "푸터 블록 경계.");
            string footerBody = source.Substring(footerIdx, footerEnd - footerIdx);
            Assert.That(footerBody, Does.Contain("UTKTheme.ApplyElementBorder(footer);"),
                "푸터 스트로크 #30363D@0.502 4면(70:75 — #30).");
            Assert.That(footerBody, Does.Not.Contain("borderTopWidth = UTKTheme.GlassStrokeWidth"),
                "푸터 1면 보더 잔존 금지(#30 수리 — 4면 전환).");

            // [#40] 코너 데칼 #58A6FF 두께 1.8.
            int decalIdx = source.IndexOf("private static void AddCornerDecals");
            int decalEnd = source.IndexOf("private static Label MkTextLabel", decalIdx);
            Assert.That(decalIdx, Is.GreaterThanOrEqualTo(0), "AddCornerDecals 존재.");
            Assert.That(decalEnd, Is.GreaterThan(decalIdx), "데칼 블록 경계.");
            string decalBody = source.Substring(decalIdx, decalEnd - decalIdx);
            Assert.That(decalBody, Does.Contain("new StyleColor(UTKTheme.Accent)"),
                "데칼 색 #58A6FF(70:7 — #40, 구 Stroke #2E343D).");
            Assert.That(decalBody, Does.Contain("1.8f"), "데칼 두께 1.8(70:7 — #40).");
        }

        /// <summary>[144차 P3-FISH] Phase 3-③-a 낚시 패널 본체 Figma 정합 수리 소스계약 — 근거:
        /// TestOutput/figma_audit_144/fishing.md (#18 데칼 P0, #6/#7/#8 글래스 크롬, #10~#17 패딩·간격 24px 체계,
        /// #19~#22/#27/#28/#35 폰트·색, #25/#26 구분선, #30/#37 바 스타일, #36 위험 배지 위치).
        /// game-header-bar/HUD-tip-bar/액션카드/163:4 컴팩트 패널은 ③-b 후속 태스크 소관(이번 계약 제외).</summary>
        [Test]
        public void FishingUTK_144Phase3_FigmaParityRepairs()
        {
            string source = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/FishingUTK.cs");

            // [#18 P0] 코너 데칼 4개 — Phase 0 V1 공용 어휘(9.6×9.6 L자 #58A6FF w1.8, 안쪽 모드).
            Assert.That(source, Does.Contain("UTKTheme.AddCornerDecals(_content, 9.6f, 1.8f, \"#58A6FF\", outsideCorner: false);"),
                "코너 데칼 4개(160:12~23) — 9.6×9.6 L자 #58A6FF w1.8(fishing.md #18 P0).");

            // [#19] 훅 아이콘 크기 16.8(이모지 근사 유지 — 구 14.4 잔존 금지).
            Assert.That(source, Does.Contain("titleIcon.style.fontSize = 16.8f;"),
                "훅 아이콘 16.8(벡터→이모지 근사 기록 — #19).");
            Assert.That(source, Does.Not.Contain("titleIcon.style.fontSize = 14.4f;"),
                "구 아이콘 크기 14.4 잔존 금지(#19).");

            // [#6/#7/#8] 패널 글래스 크롬 — 파생 오버라이드(UTKWindowBase 미수정 계약).
            int setChromeIdx = source.IndexOf("SetChrome(UTKWindowChrome.Frameless);");
            int glassBorderIdx = source.IndexOf("UTKTheme.ApplyElementBorder(this);");
            Assert.That(setChromeIdx, Is.GreaterThanOrEqualTo(0), "Frameless 크롬(#4 PASS 유지).");
            Assert.That(glassBorderIdx, Is.GreaterThan(setChromeIdx),
                "글래스 크롬 오버라이드는 SetChrome 이후 최종 적용된다(#6/#7/#8).");
            Assert.That(source, Does.Contain("style.backgroundColor = new StyleColor(UTKTheme.GlassPanelFill);"),
                "패널 fill #161B22@0.702(160:11 — #6, 구 불투명 Panel 폐기).");
            Assert.That(source, Does.Contain("UTKTheme.ApplyRadius(this, UTKTheme.GlassRadius);"),
                "패널 코너 r14.4(160:11 — #8, 구 0).");

            // [#10] 패딩 24 복원(구 재정의 20 폐기).
            Assert.That(source, Does.Not.Contain("_content.style.paddingLeft = 20f;"), "패딩 20 잔존 금지(#10).");
            Assert.That(source, Does.Contain("_content.style.paddingLeft = 24f;"), "패널 패딩 24(160:11 — #10).");

            // [#11~#15] 간격 24px 체계(구분선 렌더 두께 1.2 보정 = 22.8 포함 — 앵커 정밀 일치).
            Assert.That(source, Does.Contain("sep1.style.marginTop = 24f;"), "TitleRow↔Line 24(#11).");
            Assert.That(source, Does.Contain("sep1.style.marginBottom = 22.8f;"), "Line↔Progress 24−1.2 구분선 보정(#12).");
            Assert.That(source, Does.Contain("tensionLabel.style.marginTop = 23.8f;"), "Progress↔Tension 23.8(#13).");
            Assert.That(source, Does.Contain("sep2.style.marginTop = 24.8f;"), "Tension↔Line 24.8(#14).");
            Assert.That(source, Does.Contain("sep2.style.marginBottom = 22.8f;"), "Line↔ActionGrid 24−1.2 구분선 보정(#15).");

            // [#16/#17/#30/#37] 바 공용 빌더 — 9.6/r4.8/#161B22/스트로크 1.2.
            int barIdx = source.IndexOf("private VisualElement BuildBar(");
            int barEnd = source.IndexOf("// ===== GitHub-dark", barIdx);
            Assert.That(barIdx, Is.GreaterThanOrEqualTo(0), "BuildBar 존재.");
            Assert.That(barEnd, Is.GreaterThan(barIdx), "빌더 블록 경계.");
            string barBody = source.Substring(barIdx, barEnd - barIdx);
            Assert.That(barBody, Does.Contain("track.style.height = 9.6f;"), "바 높이 9.6(#30/#37).");
            Assert.That(barBody, Does.Contain("track.style.marginTop = 9.6f;"), "라벨행↔바 9.6(#16/#17).");
            Assert.That(barBody, Does.Contain("UTKTheme.ApplyRadius(track, 4.8f);"), "바 r4.8(#30/#37).");
            Assert.That(barBody, Does.Contain("UTKTheme.ApplyElementBorder(track,"), "바 스트로크 #30363D 1.2(#30/#37).");
            Assert.That(barBody, Does.Not.Contain("GitHubDark.Sub"), "구 바 배경 #21262D 잔존 금지(#30/#37 — #161B22).");

            // [#25/#26] 구분선 크롬/두께.
            Assert.That(source, Does.Contain("sep1.style.backgroundColor = new StyleColor(UTKTheme.BorderChrome);"), "구분선 #30363D@0.502(#25).");
            Assert.That(source, Does.Contain("sep2.style.height = 1.2f;"), "구분선 두께 1.2(#26).");

            // [#20/#21/#22] 타이틀행 간격/폰트.
            Assert.That(source, Does.Contain("titleLbl.style.marginLeft = 7.2f;"), "아이콘↔라벨 7.2(#20).");
            Assert.That(source, Does.Contain("titleLbl.style.fontSize = 15.6f;"), "낚시하기 15.6(#21).");
            Assert.That(source, Does.Contain("titleLbl.style.unityFontStyleAndWeight = FontStyle.Bold;"), "낚시하기 700(#21).");
            Assert.That(source, Does.Contain("new StyleColor(GitHubDark.White);"), "낚시하기 #FFFFFF(#21).");
            Assert.That(source, Does.Contain("_tensionState.style.fontSize = 13.2f;"), "ACTIVE 13.2(#22, 색 Accent 유지).");

            // [#27/#28/#35] 섹션 폰트 색/웨이트.
            Assert.That(source, Does.Contain("new StyleColor(GitHubDark.TextBody);"), "섹션 라벨 #C9D1D9(#27/#35).");
            Assert.That(source, Does.Contain("pctLabel.style.color = new StyleColor(GitHubDark.Accent);"), "65% #58A6FF(#28).");

            // [#36] 위험 배지 — Tension 라벨행 우측 표시로 전환(TitleRow 대체 표시 폐기).
            Assert.That(source, Does.Contain("if (_tDanger != null) _tDanger.visible = on;"),
                "위험 배지 = Tension 행 _tDanger 토글(#36).");
            Assert.That(source, Does.Not.Contain("_tensionState.text = \"위험!\";"),
                "TitleRow 위험 대체 표시 잔존 금지(#36).");

            // [#33 기록] progress-fill 외부 그림자 — UI Toolkit 미지원 보류 근거가 소스에 기록돼야 한다.
            Assert.That(source, Does.Contain("DROP_SHADOW:4.8"), "progress-fill 그림자 보류 근거 기록(#33 — 감사 보류 축).");
        }

        [Test]
        public void ShopVariants_UseIdenticalThreePanelGeometryAndSupportedContent()
        {
            AssertRect(new Rect(144f, 84f, 504f, 912f), ShopWindowUTK.StoreBounds);
            AssertRect(new Rect(672f, 84f, 576f, 912f), ShopWindowUTK.DetailBounds);
            AssertRect(new Rect(1272f, 84f, 504f, 912f), ShopWindowUTK.InventoryBounds);
            Assert.That(ShopWindowUTK.StoreGridCellSize, Is.EqualTo(81.6f).Within(0.001f));
            Assert.That(ShopWindowUTK.StoreTabCount, Is.EqualTo(6));
            Assert.That(ShopWindowUTK.ShopVariantDifference, Is.EqualTo("Action label only: 74:4 shows Buy; 76:7 shows Sell. The saved trees share the same store tabs, grid, details, inventory, and geometry; their sample content is not a faction mapping."));
            string source = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/ShopWindowUTK.cs");
            int chromeCall = source.IndexOf("UTKTheme.ApplyWindowChrome(this, titleBar, _content);");
            int transparentReset = source.IndexOf("style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));", chromeCall);
            Assert.That(chromeCall, Is.GreaterThanOrEqualTo(0));
            Assert.That(transparentReset, Is.GreaterThan(chromeCall), "Restore transparency after shared chrome sets an opaque panel color.");
            Assert.That(source.IndexOf("style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 0f;", transparentReset), Is.GreaterThan(transparentReset));
            Assert.That(source.IndexOf("pickingMode = PickingMode.Ignore;", transparentReset), Is.GreaterThan(transparentReset),
                "The canvas-sized host must not itself act as a hit target.");
            // [118차 Figma 정합 갱신] Detail 패널 단일 카드 → Figma 74:4 4섹션 재구성 + 3패널 #161B22/70% 배경.
            Assert.That(source, Does.Contain("name = \"ItemNameSection\""),
                "Detail은 Figma 4섹션(ItemName/ItemImage/Description/ActionButton)으로 재구성됐다.");
            Assert.That(source, Does.Contain("name = \"ItemImageSection\""));
            Assert.That(source, Does.Contain("name = \"DescriptionSection\""));
            Assert.That(source, Does.Contain("0x16 / 255f, 0x1B / 255f, 0x22 / 255f, 0.7f"),
                "3패널 배경 = Figma #161B22/70%(반투명 — 게임 씬 비침).");
            Assert.That(source, Does.Contain("구매하기 ({bp:N0}G)"),
                "액션 버튼 텍스트에 가격 포함 — Figma '구매하기 (12,500G)' 계약.");
        }

        /// <summary>[144차 P1-SHOP] Phase 1-① 상점 Figma 정합 수리 소스계약 — 근거: TestOutput/figma_audit_144/shop.md
        /// (요소별 괴리 행 + "알파 재검증 정정(144차)" 섹션). 괴리 수리에 따른 기대값 갱신만 — 기능 계약 삭제 없음.</summary>
        [Test]
        public void ShopVariants_144Phase1_FigmaParityRepairs()
        {
            string source = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/ShopWindowUTK.cs");

            // [P0 #56] 빈 가방 셀 = 프레임만(Figma ItemSlotEmpty 자식 0 — 74:4·76:7 38칸 전수 확인).
            // Phase 0 V3 UTKSlot.SetEmpty(true) 프레임 모드 사용, 수동 TierStrip 부가 렌더 금지.
            int emptyCell = source.IndexOf("private VisualElement BuildEmptyBagCell()");
            int emptyCellEnd = source.IndexOf("private VisualElement BuildSellRow(", emptyCell);
            Assert.That(emptyCell, Is.GreaterThanOrEqualTo(0), "BuildEmptyBagCell 존재.");
            Assert.That(emptyCellEnd, Is.GreaterThan(emptyCell), "BuildEmptyBagCell 본문 경계.");
            string emptyCellBody = source.Substring(emptyCell, emptyCellEnd - emptyCell);
            Assert.That(emptyCellBody, Does.Contain("slotBox.SetEmpty(true);"),
                "빈셀은 Phase 0 V3 프레임 모드(아이콘/카운트/TierStrip 숨김)로 렌더된다.");
            // [124차 규약] 주석 폐기 표기 오탐 방지 — 실코드 라인만 스캔(주석 시작 행 제외).
            string emptyCellCode = string.Join("\n", emptyCellBody.Split('\n')
                .Select(l => l.TrimStart())
                .Where(l => !l.StartsWith("//")));
            Assert.That(emptyCellCode, Does.Not.Contain("SetTierStrip"),
                "Figma ItemSlotEmpty에는 자식이 없다 — 빈셀 TierStrip 부가 렌더 금지(shop.md #56 P0).");

            // [#45] 판매 모드 액션 버튼 = 구매 판본과 동일 Primary(#58A6FF/1.0, 텍스트 #0B0E14) — 판본 차이는 라벨만.
            Assert.That(source, Does.Not.Contain("StyleButton(_detailActionBtn, UTKButton.Variant.Danger)"),
                "Figma 76:7 SellButton은 #58A6FF — 판매 버튼 Danger(#F85149) 변이 금지(shop.md #45).");

            // [#42/#46] 액션 버튼 텍스트 웨이트 — Figma 진실치 "Roboto 700 Bold"(shop_buy__74_4.json BuyText font 필드).
            Assert.That(source, Does.Contain("_detailActionBtn.style.unityFontStyleAndWeight = FontStyle.Bold;"),
                "액션 버튼 텍스트 웨이트 700 계약(shop.md #42/#46).");

            // [알파 재검증 정정 #7/#9/#12/#16/#20/#27/#31/#36/#38/#40/#55/#57] #30363D 스트로크 실효알파 0.502 —
            // Phase 0 V2 공용 크롬 상수(UTKTheme.BorderChrome) 재사용(이중 상수 금지).
            Assert.That(source, Does.Contain("private static readonly Color ShopStroke = UTKTheme.BorderChrome;"),
                "ShopStroke = #30363D@0.502 공용 크롬 상수.");

            // [#17] 활성 탭 테두리 없음 — Figma 활성 Tab('무기')은 strokes 없음(미선택 fill 투명은 정정 PASS — 유지).
            int tabStyle = source.IndexOf("private static void StyleStoreTabButton(Button tab, bool active)");
            int tabStyleEnd = source.IndexOf("/// <summary>GitHub-dark 버튼 인라인 오버라이드", tabStyle);
            Assert.That(tabStyle, Is.GreaterThanOrEqualTo(0));
            Assert.That(tabStyleEnd, Is.GreaterThan(tabStyle));
            string tabBody = source.Substring(tabStyle, tabStyleEnd - tabStyle);
            Assert.That(tabBody, Does.Contain("float tabBorderW = active ? 0f : 1.2f;"),
                "활성 탭 border 0 / 미선택 1.2 — Figma 활성 Tab 스트로크 없음(shop.md #17).");

            // [#60 D7] WeightValue — 무게(kg) 실데이터 부재 → 슬롯 표기('칸') 유지(데이터 위조 금지, D7 확정 2026-10-31).
            Assert.That(source, Does.Contain("{usedSlots} / 80 칸\""),
                "무게 kg 목업 금지 — 칸수 슬롯 표기 유지(D7 확정).");

            // [#6 기록] 패널 fx(블러36+그림자) — UI Toolkit 미지원 수리 보류 계약(주석 기록 유지).
            Assert.That(source, Does.Contain("BACKGROUND_BLUR 36.0"),
                "패널 fx 미지원 보류 근거가 소스에 기록돼야 한다(감사 보류 축).");
        }

        [Test]
        public void ShopTradeEntryPoints_PreservePricingAndTransactionSafeguards()
        {
            var buyPrice = typeof(ShopWindowUTK).GetMethod("GetBuyPrice");
            var sellPrice = typeof(ShopWindowUTK).GetMethod("CalculateSellPrice");
            var buy = typeof(ShopWindowUTK).GetMethod("BuyItem", new[] { typeof(ShopWindowUTK.ShopItem) });
            var sell = typeof(ShopWindowUTK).GetMethod("SellSlot", new[] { typeof(PlayerInventory.ItemSlot) });
            var locationOpen = typeof(ShopWindowUTK).GetMethod("Open", new[] { typeof(Vector3?) });
            Assert.That(buyPrice, Is.Not.Null);
            Assert.That(sellPrice, Is.Not.Null);
            Assert.That(buy, Is.Not.Null);
            Assert.That(sell, Is.Not.Null);
            Assert.That(locationOpen, Is.Not.Null);
            Assert.That(buyPrice.GetMethodBody().GetILAsByteArray().Length, Is.GreaterThan(0));
            Assert.That(sellPrice.GetMethodBody().GetILAsByteArray().Length, Is.GreaterThan(0));
            string source = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/ShopWindowUTK.cs");
            Assert.That(source, Does.Contain("SpendGold(price, \"shop_purchase\")"));
            Assert.That(source, Does.Contain("AddGold(price, \"shop_refund\")"));
            Assert.That(source, Does.Contain("AddGold(sellPrice, \"shop_sale\")"));
            Assert.That(source, Does.Contain("PlayerInventory.Instance.AddItem(item.item, 1)"));
            Assert.That(source, Does.Contain("PlayerInventory.Instance.RemoveItem(slot.item.id, 1)"));
        }

        [Test]
        public void ShopBounds_ScaleIndependentlyAgainstCanvasWithoutPanelSettingsChanges()
        {
            var rootSize = new Vector2(960f, 540f);
            AssertRect(new Rect(72f, 42f, 252f, 456f), FigmaCanvasLayout.ScaleRect(ShopWindowUTK.StoreBounds, rootSize));
            AssertRect(new Rect(336f, 42f, 288f, 456f), FigmaCanvasLayout.ScaleRect(ShopWindowUTK.DetailBounds, rootSize));
            AssertRect(new Rect(636f, 42f, 252f, 456f), FigmaCanvasLayout.ScaleRect(ShopWindowUTK.InventoryBounds, rootSize));
        }

        private static void AssertDesignSpaceContract(VisualElement window, Rect bounds, float k)
        {
            // 창 박스 = Figma rect × k (등배수).
            Assert.That(window.style.left.value.value, Is.EqualTo(bounds.x * k).Within(0.001f));
            Assert.That(window.style.top.value.value, Is.EqualTo(bounds.y * k).Within(0.001f));
            Assert.That(window.style.width.value.value, Is.EqualTo(bounds.width * k).Within(0.001f));
            Assert.That(window.style.height.value.value, Is.EqualTo(bounds.height * k).Within(0.001f));
            // 디자인공간(Content) = raw Figma px 크기 + scale=k.
            var content = window.Q<VisualElement>("Content");
            Assert.That(content, Is.Not.Null);
            Assert.That(content.style.width.value.value, Is.EqualTo(bounds.width).Within(0.001f),
                "디자인공간은 raw Figma px 크기를 유지한다.");
            Assert.That(content.style.height.value.value, Is.EqualTo(bounds.height).Within(0.001f));
            Assert.That(content.style.scale.value.value.x, Is.EqualTo(k).Within(0.001f));
            Assert.That(content.style.scale.value.value.y, Is.EqualTo(k).Within(0.001f),
                "등배수 계약 — X/Y 스케일이 동일해야 한다(종횡비 왜곡 금지).");
        }

        private static void AssertRawSize(VisualElement element, float expected, string label)
        {
            Assert.That(element, Is.Not.Null, label);
            Assert.That(element.style.height.value.value, Is.EqualTo(expected).Within(0.001f), label);
        }

        private static void AssertRawWidth(VisualElement element, float expected, string label)
        {
            Assert.That(element, Is.Not.Null, label);
            Assert.That(element.style.width.value.value, Is.EqualTo(expected).Within(0.001f), label);
        }

        private static void AssertRect(Rect expected, Rect actual)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.001f));
            Assert.That(actual.width, Is.EqualTo(expected.width).Within(0.001f));
            Assert.That(actual.height, Is.EqualTo(expected.height).Within(0.001f));
        }
    }
}

// Figma 74:4 (buy action) and 76:7 (sell action) have matching panel node geometry
// and content-tree structure. The separate sample inventory, balance and selected item
// are mock content in the source and intentionally do not imply any faction binding.
// The sole supported structural difference is the central action node label: BuyButton
// versus SellButton; runtime action is still derived from the active buy/sell mode.
// Saved source: .hermes/plans/figma-1920-baseline-2026-10-04/figma-file.json
// Nodes compared: 74:169/172/173 and 76:169/170/171.

// The edit-mode test assembly includes this source file's namespace via the project assembly.