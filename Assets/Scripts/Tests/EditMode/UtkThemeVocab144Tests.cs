// [144차 Phase 0 V1~V8] 공용 UI 어휘 정비 계약 테스트.
// 근거: docs/144_FIGMA_UI_PARITY_FIX_PLAN.md §2 (사용자 D1~D7 확정) + docs/144_figma_parity_audit.md
//       + TestOutput/figma_audit_144/*.md + docs/figma_truth_144/*.json (python 실측 진실치).
// 구성: 소스계약(ReadSource 텍스트 단언) + EditMode 행위계약(VisualElement 인스턴스 — FluentScrollerTests 선례).
//       런타임 렌더/Play 판정은 QA 서브 몫 — 본 파일은 소스/인스턴스 계약만.
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.UI.Toolkit;

namespace ProjectName.Tests.EditMode
{
    [TestFixture]
    public class UtkThemeVocab144Tests
    {
        private const string ThemeSourcePath = "Assets/Scripts/UI/Toolkit/UTKTheme.cs";
        private const string ControlsSourcePath = "Assets/Scripts/UI/Toolkit/UTKControls.cs";
        private const string RegionsSourcePath = "Assets/Scripts/UI/Toolkit/InventoryClusterPanelRegions.cs";

        private string ReadSource(string path)
        {
            var full = Path.Combine(Application.dataPath, "..", path);
            Assert.IsTrue(File.Exists(full), $"소스 파일 없음: {path}");
            return File.ReadAllText(full);
        }

        private static void AssertColor(Color expected, Color actual, string message)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.002f), message + " (r)");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.002f), message + " (g)");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.002f), message + " (b)");
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.002f), message + " (a)");
        }

        // ═══════════════ V1 — 코너 데칼 4각 헬퍼 (124차 L자 선례 이식) ═══════════════

        [Test]
        public void V1_Source_AddCornerDecals_SignatureAndTruth()
        {
            string src = ReadSource(ThemeSourcePath);
            Assert.That(src, Does.Contain("[144차 P0 V1]"), "V1 마커 주석 계약.");
            Assert.That(src, Does.Contain("public static void AddCornerDecals(VisualElement root, float size = 10f, float weight = 1.8f"), "V1 시그니처 계약.");
            Assert.That(src, Does.Contain("string hexColor = \"#58A6FF\""), "V1 기본색 #58A6FF 진실치.");
            Assert.That(src, Does.Contain("bool outsideCorner = true"), "V1 코너 밖 걸침 기본값(Figma 진실 — 대화 163:24~33).");
            Assert.That(src, Does.Contain("Decal(\"DecalTL\", true, true);"), "V1 4코너 L자 구조(DecalTL/TR/BL/BR).");
        }

        [Test]
        public void V1_Behavior_CreatesFourLShapedDecals_OutsideStraddle()
        {
            var root = new VisualElement();
            UTKTheme.AddCornerDecals(root);
            Assert.AreEqual(4, root.childCount, "코너 데칼은 4개(14×14/10×10 요소 — 기본 10).");

            var tl = root.Q("DecalTL");
            var tr = root.Q("DecalTR");
            var bl = root.Q("DecalBL");
            var br = root.Q("DecalBR");
            Assert.That(tl, Is.Not.Null, "TL 데칼 존재.");
            Assert.That(tr, Is.Not.Null, "TR 데칼 존재.");
            Assert.That(bl, Is.Not.Null, "BL 데칼 존재.");
            Assert.That(br, Is.Not.Null, "BR 데칼 존재.");

            // 10×10 / L자 2면 보더 1.8 #58A6FF — TR=border-top+border-right, 나머지 0(124차 선례)
            Assert.That(tr.style.width.value.value, Is.EqualTo(10f).Within(0.001f), "데칼 기본 크기 10.");
            Assert.That(tr.style.borderTopWidth.value, Is.EqualTo(1.8f).Within(0.001f), "TR 상단 보더 1.8.");
            Assert.That(tr.style.borderRightWidth.value, Is.EqualTo(1.8f).Within(0.001f), "TR 우측 보더 1.8.");
            Assert.That(tr.style.borderBottomWidth.value, Is.EqualTo(0f).Within(0.001f), "TR 하단 보더 0(L자 2면).");
            Assert.That(tr.style.borderLeftWidth.value, Is.EqualTo(0f).Within(0.001f), "TR 좌측 보더 0(L자 2면).");
            AssertColor(UTKTheme.Accent, tr.style.borderTopColor.value, "TR 데칼색 #58A6FF.");

            // 코너 밖 걸침(기본) — inset −size/2 → TL은 left/top = −5
            Assert.That(tl.style.left.value.value, Is.EqualTo(-5f).Within(0.001f), "밖 걸침 TL left −5(−size/2).");
            Assert.That(tl.style.top.value.value, Is.EqualTo(-5f).Within(0.001f), "밖 걸침 TL top −5.");
            Assert.That(br.style.right.value.value, Is.EqualTo(-5f).Within(0.001f), "밖 걸침 BR right −5.");
            Assert.That(br.style.bottom.value.value, Is.EqualTo(-5f).Within(0.001f), "밖 걸침 BR bottom −5.");
            Assert.AreEqual(PickingMode.Ignore, tl.pickingMode, "데칼은 입력 비흡수.");
        }

        [Test]
        public void V1_Behavior_InsideMode_Inset4_Legacy()
        {
            var root = new VisualElement();
            UTKTheme.AddCornerDecals(root, 14f, 1.8f, "#58A6FF", false);
            var tl = root.Q("DecalTL");
            Assert.That(tl, Is.Not.Null, "안쪽 모드 TL 데칼 존재.");
            Assert.That(tl.style.width.value.value, Is.EqualTo(14f).Within(0.001f), "안쪽 모드 크기 14 인자 반영.");
            Assert.That(tl.style.left.value.value, Is.EqualTo(4f).Within(0.001f), "안쪽 모드 inset +4(NPCDialoguePanelUTK 선례).");
            Assert.That(tl.style.top.value.value, Is.EqualTo(4f).Within(0.001f), "안쪽 모드 inset +4.");
        }

        // ═══════════════ V2 — 패널/요소 테두리 크롬 상수 + 헬퍼 ═══════════════

        [Test]
        public void V2_Source_BorderChrome_Constants()
        {
            string src = ReadSource(ThemeSourcePath);
            Assert.That(src, Does.Contain("[144차 P0 V2]"), "V2 마커 주석 계약.");
            Assert.That(src, Does.Contain("public static readonly Color BorderChrome"), "V2 크롬색 상수 계약.");
            Assert.That(src, Does.Contain("0.502f"), "V2 실효알파 0.502 진실치.");
            Assert.That(src, Does.Contain("public const float BorderChromeWidth = 1.2f"), "V2 두께 1.2 진실치.");
            Assert.That(src, Does.Contain("public static void ApplyElementBorder(VisualElement el, Color? color = null, float? width = null)"), "V2 헬퍼 시그니처 계약.");
        }

        [Test]
        public void V2_Behavior_ApplyElementBorder_DefaultsToChromeTruth()
        {
            var el = new VisualElement();
            UTKTheme.ApplyElementBorder(el);
            Assert.That(el.style.borderTopWidth.value, Is.EqualTo(1.2f).Within(0.001f), "기본 두께 1.2.");
            Assert.That(el.style.borderLeftWidth.value, Is.EqualTo(1.2f).Within(0.001f), "좌측 두께 1.2.");
            AssertColor(UTKTheme.BorderChrome, el.style.borderTopColor.value, "기본 크롬색 #30363D@0.502.");
            // 요소별 오버라이드 — 인자 전달 시 진실치 대체
            var custom = new VisualElement();
            UTKTheme.ApplyElementBorder(custom, UTKTheme.Accent, 1.8f);
            AssertColor(UTKTheme.Accent, custom.style.borderBottomColor.value, "오버라이드 색 Accent.");
            Assert.That(custom.style.borderBottomWidth.value, Is.EqualTo(1.8f).Within(0.001f), "오버라이드 두께 1.8.");
        }

        // ═══════════════ V3 — UTKSlot 슬롯 어휘 보강 ═══════════════

        [Test]
        public void V3_Source_SlotVocabulary_Signatures()
        {
            string src = ReadSource(ControlsSourcePath);
            Assert.That(src, Does.Contain("[144차 P0 V3]"), "V3 마커 주석 계약.");
            Assert.That(src, Does.Contain("public void SetEmpty(bool empty)"), "V3 빈셀 프레임 모드 계약.");
            Assert.That(src, Does.Contain("public void SetEmptyIcon(bool visible"), "V3 square-dashed 옵션 계약.");
            Assert.That(src, Does.Contain("public void SetTierStrip(bool visible, Color tierColor)"), "V3 TierStrip 옵션 계약.");
            Assert.That(src, Does.Contain("public void SetCount(string text)"), "V3 Count 문자열 오버로드 계약.");
            Assert.That(src, Does.Contain("public void SetCount(int count)"), "기존 SetCount(int) 유지 계약(호출부 무파괴).");
            Assert.That(src, Does.Contain("_countLabel.style.fontSize = 13.2f;"), "V3 Count 13.2px 진실치.");
            Assert.That(src, Does.Contain("UTKTheme.TextSub"), "V3 Count #8B949E 진실치.");
        }

        [Test]
        public void V3_Behavior_SetEmpty_FrameOnly_RestoreOnExit()
        {
            var slot = new UTKSlot();
            slot.SetCount(3);
            Assert.That(slot.IconHost.style.display.value, Is.Not.EqualTo(DisplayStyle.None), "전제: 빈셀 모드 전 아이콘 표시.");

            slot.SetEmpty(true);
            Assert.AreEqual(DisplayStyle.None, slot.IconHost.style.display.value, "빈셀 모드: 아이콘 숨김(프레임만 렌더).");
            var count = slot.Q<Label>("Count");
            Assert.AreEqual(DisplayStyle.None, count.style.display.value, "빈셀 모드: 카운트 숨김.");

            slot.SetEmpty(false);
            Assert.That(slot.IconHost.style.display.value, Is.Not.EqualTo(DisplayStyle.None), "해제: 아이콘 복원.");
            Assert.That(count.style.display.value, Is.Not.EqualTo(DisplayStyle.None), "해제: 카운트 복원.");
        }

        [Test]
        public void V3_Behavior_SetEmptyIcon_PlaceholderOptional()
        {
            var slot = new UTKSlot();
            slot.SetEmptyIcon(true);   // 옵션 선언 — 빈셀 모드 진입 시 표시
            Assert.That(slot.Q("EmptyIconHost"), Is.Null, "옵션만으로는 미표현(빈셀 진입 전).");

            slot.SetEmpty(true);
            var host = slot.Q("EmptyIconHost");
            Assert.That(host, Is.Not.Null, "빈셀 + 옵션 → 플레이스홀더 생성.");
            Assert.AreEqual(DisplayStyle.Flex, host.style.display.value, "플레이스홀더 표시.");
            var icon = host.Q("EmptyIcon");
            Assert.That(icon.style.width.value.value, Is.EqualTo(38.4f).Within(0.001f), "플레이스홀더 기본 38.4(loot 70:48 진실치).");
            Assert.That(icon.style.borderTopWidth.value, Is.EqualTo(2.4f).Within(0.001f), "플레이스홀더 스트로크 2.4(70:49 진실치).");

            slot.SetEmptyIcon(false);
            Assert.AreEqual(DisplayStyle.None, host.style.display.value, "옵션 해제 → 숨김.");

            // 상점형(프레임만) — 옵션 미호출 시 빈셀에 플레이스홀더 없음
            var frameOnly = new UTKSlot();
            frameOnly.SetEmpty(true);
            Assert.That(frameOnly.Q("EmptyIconHost"), Is.Null, "옵션 없는 빈셀은 프레임만(ItemSlotEmpty 자식 0 진실).");
        }

        [Test]
        public void V3_Behavior_SetTierStrip_Height36_TopStrip()
        {
            var slot = new UTKSlot();
            var tierColor = new Color(0xBC / 255f, 0x8C / 255f, 0xFF / 255f, 1f);
            slot.SetTierStrip(true, tierColor);
            var strip = slot.Q("TierStrip");
            Assert.That(strip, Is.Not.Null, "TierStrip 요소 생성.");
            Assert.AreEqual(DisplayStyle.Flex, strip.style.display.value, "TierStrip 표시.");
            Assert.That(strip.style.height.value.value, Is.EqualTo(3.6f).Within(0.001f), "TierStrip h3.6 진실치(15:64/70:56).");
            Assert.That(strip.style.top.value.value, Is.EqualTo(0f).Within(0.001f), "TierStrip 상단 (0,0) 부착.");
            AssertColor(tierColor, strip.style.backgroundColor.value, "TierStrip 티어색 배경.");

            slot.SetTierStrip(false, tierColor);
            Assert.AreEqual(DisplayStyle.None, strip.style.display.value, "TierStrip 숨김(요소 유지).");

            // 빈셀 모드에서는 프레임만 — 스트립 강제 숨김(Figma ItemSlotEmpty 자식 0)
            slot.SetEmpty(true);
            slot.SetTierStrip(true, tierColor);
            Assert.AreEqual(DisplayStyle.None, strip.style.display.value, "빈셀 프레임 모드에서 TierStrip 숨김.");
        }

        [Test]
        public void V3_Behavior_SetCountString_FigmaTypo()
        {
            var slot = new UTKSlot();
            slot.SetCount("12");
            var count = slot.Q<Label>("Count");
            Assert.AreEqual("12", count.text, "카운트 텍스트 반영.");
            Assert.That(count.style.fontSize.value.value, Is.EqualTo(13.2f).Within(0.001f), "Count 13.2px 진실치.");
            Assert.AreEqual(FontStyle.Bold, count.style.unityFontStyleAndWeight.value, "Figma 600 → UITK Bold 매핑(StyleFigmaText 규약).");
            AssertColor(UTKTheme.TextSub, count.style.color.value, "Count #8B949E 진실치.");

            slot.SetCount("");
            Assert.AreEqual(DisplayStyle.None, count.style.display.value, "빈 문자열 → 숨김.");
        }

        // ═══════════════ V4 — 헤더 2행 어휘 ═══════════════

        [Test]
        public void V4_Source_Header2Row_Signature()
        {
            string src = ReadSource(ThemeSourcePath);
            Assert.That(src, Does.Contain("[144차 P0 V4]"), "V4 마커 주석 계약.");
            Assert.That(src, Does.Contain("public static VisualElement BuildPanelHeader2Row(VisualElement parent, string title, string subtitle)"), "V4 시그니처 계약.");
            Assert.That(src, Does.Contain("StyleFigmaText(subtitleLabel, FontBody, 400, TextSub"), "V4 Subtitle 14.4/400 #8B949E 진실치.");
        }

        [Test]
        public void V4_Behavior_Header2Row_TitleAndSubtitle()
        {
            var parent = new VisualElement();
            var header = UTKTheme.BuildPanelHeader2Row(parent, "인벤토리", "INVENTORY");
            Assert.That(header, Is.Not.Null, "헤더 반환 계약.");
            Assert.AreEqual(2, header.childCount, "세로 2행(제목+부제).");
            Assert.AreEqual(parent, header.parent, "parent 부착 계약.");

            var title = header[0] as Label;
            var subtitle = header[1] as Label;
            Assert.That(title, Is.Not.Null, "1행 = 제목 라벨.");
            Assert.That(subtitle, Is.Not.Null, "2행 = 영문 부제 라벨.");
            Assert.AreEqual("인벤토리", title.text, "제목 텍스트.");
            Assert.AreEqual("INVENTORY", subtitle.text, "부제 텍스트(영문).");
            Assert.That(title.style.fontSize.value.value, Is.EqualTo(UTKTheme.FontTitle).Within(0.001f), "제목 = 기존 타이틀 상수 재사용.");
            Assert.AreEqual(FontStyle.Bold, title.style.unityFontStyleAndWeight.value, "제목 700.");
            AssertColor(UTKTheme.TextMain, title.style.color.value, "제목색 TextMain.");
            Assert.That(subtitle.style.fontSize.value.value, Is.EqualTo(14.4f).Within(0.001f), "부제 14.4 진실치.");
            Assert.AreEqual(FontStyle.Normal, subtitle.style.unityFontStyleAndWeight.value, "부제 400.");
            AssertColor(UTKTheme.TextSub, subtitle.style.color.value, "부제색 #8B949E.");
        }

        [Test]
        public void V4_Behavior_Header2Row_EmptySubtitle_SingleRow()
        {
            var header = UTKTheme.BuildPanelHeader2Row(null, "제목만", null);
            Assert.AreEqual(1, header.childCount, "부제 공백 → 1행.");
        }

        // ═══════════════ V5 — CloseButton 어휘 (Figma 288:7 실측) ═══════════════

        [Test]
        public void V5_Source_CloseButton_Truth()
        {
            string src = ReadSource(ControlsSourcePath);
            Assert.That(src, Does.Contain("[144차 P0 V5]"), "V5 마커 주석 계약.");
            Assert.That(src, Does.Contain("public static Button CreateCloseButton(Action onClick = null, float size = 32f)"), "V5 팩토리 시그니처 계약.");
            Assert.That(src, Does.Contain("0x16 / 255f, 0x1B / 255f, 0x22 / 255f, 0.8f"), "V5 fill #161B22@0.8 진실치.");
            Assert.That(src, Does.Contain("UTKTheme.TextSub"), "V5 글리프색 #8B949E 진실치.");
        }

        [Test]
        public void V5_Behavior_CloseButton_Geometry32_R16()
        {
            var btn = UTKCloseButton.CreateCloseButton(null);
            Assert.That(btn.style.width.value.value, Is.EqualTo(32f).Within(0.001f), "CloseButton 32×32 진실치(288:7).");
            Assert.That(btn.style.height.value.value, Is.EqualTo(32f).Within(0.001f), "CloseButton 32×32 진실치.");
            Assert.That(btn.style.borderTopLeftRadius.value.value, Is.EqualTo(16f).Within(0.001f), "r16(원) 진실치.");
            Assert.That(btn.style.borderTopWidth.value, Is.EqualTo(1.2f).Within(0.001f), "스트로크 1.2 진실치.");
            AssertColor(UTKTheme.BorderChrome, btn.style.borderTopColor.value, "스트로크 #30363D@0.502 진실치.");
            AssertColor(new Color(0x16 / 255f, 0x1B / 255f, 0x22 / 255f, 0.8f), btn.style.backgroundColor.value, "fill #161B22@0.8 진실치.");
            Assert.AreEqual("✕", btn.text, "닫기 글리프.");

            var small = UTKCloseButton.CreateCloseButton(() => { }, 24f);
            Assert.That(small.style.width.value.value, Is.EqualTo(24f).Within(0.001f), "size 인자 반영.");
            Assert.That(small.style.borderTopLeftRadius.value.value, Is.EqualTo(12f).Within(0.001f), "size 비례 원 반경.");
        }

        // ═══════════════ V6 — 배지/칩 3종 공용 스타일 ═══════════════

        [Test]
        public void V6_Source_BadgeHelpers_Truth()
        {
            string src = ReadSource(ThemeSourcePath);
            Assert.That(src, Does.Contain("[144차 P0 V6]"), "V6 마커 주석 계약.");
            Assert.That(src, Does.Contain("public static VisualElement CreateTierBadge(string text, Color tierColor"), "V6 TierBadge 헬퍼 계약.");
            Assert.That(src, Does.Contain("public static VisualElement CreateCountBadge(string text, Color textColor"), "V6 CountBadge 헬퍼 계약.");
            Assert.That(src, Does.Contain("public static VisualElement CreateStatusPill(string text, Color statusColor"), "V6 StatusPill 헬퍼 계약.");
            Assert.That(src, Does.Contain("new Color(tierColor.r, tierColor.g, tierColor.b, 0.122f)"), "V6 TierBadge fill 티어색@0.122 진실치.");
            Assert.That(src, Does.Contain("new Color(0f, 0f, 0f, 0.251f)"), "V6 CountBadge/StatusPill #000000@0.251 진실치.");
        }

        [Test]
        public void V6_Behavior_TierBadge_R48Fill122StrokeTier()
        {
            var tier = new Color(0xBC / 255f, 0x8C / 255f, 0xFF / 255f, 1f);   // #BC8CFF(63:125 EPIC 표본)
            var badge = UTKTheme.CreateTierBadge("EPIC", tier);
            Assert.AreEqual("EPIC", badge.Q<Label>().text, "배지 텍스트.");
            Assert.That(badge.style.borderTopLeftRadius.value.value, Is.EqualTo(4.8f).Within(0.001f), "TierBadge r4.8 진실치.");
            AssertColor(new Color(tier.r, tier.g, tier.b, 0.122f), badge.style.backgroundColor.value, "TierBadge fill 티어색@0.122 진실치.");
            Assert.That(badge.style.borderTopWidth.value, Is.EqualTo(1.2f).Within(0.001f), "TierBadge 스트로크 1.2 진실치.");
            AssertColor(tier, badge.style.borderTopColor.value, "TierBadge 스트로크 티어색 진실치.");
            var label = badge.Q<Label>();
            Assert.That(label.style.fontSize.value.value, Is.EqualTo(13.2f).Within(0.001f), "배지 텍스트 13.2 진실치.");
            Assert.AreEqual(FontStyle.Bold, label.style.unityFontStyleAndWeight.value, "배지 텍스트 700 진실치.");
            AssertColor(tier, label.style.color.value, "배지 텍스트 티어색 진실치.");
        }

        [Test]
        public void V6_Behavior_CountBadgeAndStatusPill_Alpha251System()
        {
            var countBadge = UTKTheme.CreateCountBadge("3명 활성", UTKTheme.Accent);
            AssertColor(new Color(0f, 0f, 0f, 0.251f), countBadge.style.backgroundColor.value, "CountBadge fill #000000@0.251 진실치(69:197).");
            Assert.That(countBadge.style.borderTopLeftRadius.value.value, Is.EqualTo(7.2f).Within(0.001f), "CountBadge r7.2 진실치.");
            AssertColor(UTKTheme.BorderChrome, countBadge.style.borderTopColor.value, "CountBadge 크롬 스트로크 진실치.");
            var countLabel = countBadge.Q<Label>();
            Assert.That(countLabel.style.fontSize.value.value, Is.EqualTo(14.4f).Within(0.001f), "CountBadge 텍스트 14.4 진실치(69:198).");
            AssertColor(UTKTheme.Accent, countLabel.style.color.value, "CountBadge 텍스트색 인자 반영.");

            var status = UTKTheme.CreateStatusPill("활성", new Color(0x39 / 255f, 0xD3 / 255f, 0x53 / 255f, 1f));
            AssertColor(new Color(0f, 0f, 0f, 0.251f), status.style.backgroundColor.value, "StatusPill #000000@0.251 체계 진실치(69:40).");
            Assert.That(status.style.borderTopLeftRadius.value.value, Is.EqualTo(4.8f).Within(0.001f), "StatusPill r4.8 진실치.");
            Assert.That(status.style.paddingTop.value.value, Is.EqualTo(2.4f).Within(0.001f), "StatusPill pad 2.4 진실치.");
            var statusLabel = status.Q<Label>();
            Assert.That(statusLabel.style.fontSize.value.value, Is.EqualTo(12f).Within(0.001f), "StatusPill 텍스트 12 진실치(69:41).");
            Assert.AreEqual(FontStyle.Bold, statusLabel.style.unityFontStyleAndWeight.value, "StatusPill 텍스트 700 진실치.");
        }

        // ═══════════════ V7 — stale 리전 정리 (InventoryFooter) ═══════════════

        [Test]
        public void V7_Source_InventoryFooter_TruthRect()
        {
            string src = ReadSource(RegionsSourcePath);
            Assert.That(src, Does.Contain("[144차 P0 V7]"), "V7 마커 주석 계약.");
            Assert.That(src, Does.Contain("new Rect(168f, 928f, 456f, 48.2f)"), "V7 캔버스 진실치 (168,928,456,48.2) — Figma 15:133 재실측.");
            Assert.That(src, Does.Not.Contain("new Rect(168f, 911f, 456f, 56f)"), "구 stale치 (168,911,456,56) 잔존 금지(inventory.md A51).");
        }

        [Test]
        public void V7_Behavior_InventoryFooter_PanelLocal844()
        {
            // 패널-로컬 환산 = (24,844) — JSON ay 931 − 15:5 패널 ay 87 = 844(inventory.md A51 정정치)
            var panelLocal = InventoryClusterPanelRegions.ToPanelLocal(
                InventoryClusterPanelRegions.InventoryFooter, InventoryClusterFigmaLayout.InventoryBounds);
            Assert.That(panelLocal.x, Is.EqualTo(24f).Within(0.001f), "패널-로컬 x 24.");
            Assert.That(panelLocal.y, Is.EqualTo(844f).Within(0.001f), "패널-로컬 y 844(구 827 stale).");
            Assert.That(panelLocal.width, Is.EqualTo(456f).Within(0.001f), "폭 456.");
            Assert.That(panelLocal.height, Is.EqualTo(48.2f).Within(0.001f), "높이 48.2(구 56 stale).");
        }

        // ═══════════════ V8 — 글래스 알파 통일 (D1 확정 — Figma 0.702) ═══════════════

        [Test]
        public void V8_Source_GlassAlpha_0702Unified()
        {
            string src = ReadSource(ThemeSourcePath);
            Assert.That(src, Does.Contain("[144차 P0 V8]"), "V8 마커 주석 계약.");
            Assert.That(src, Does.Contain("new Color(0f, 0f, 0f, 0.702f)"), "V8 GlassPanel 흑 글래스 0.702 진실치.");
            Assert.That(src, Does.Contain("new Color32(0x16, 0x1B, 0x22, 0xB3)"), "V8 GlassPanelFill #161B22@0.702(0xB3) 진실치.");
            // D1 계약 진화(94차 규칙) — 구 사용자 조정치 0.85/0.88 잔존 금지
            Assert.That(src, Does.Not.Contain("0.85f"), "구 글래스 알파 0.85 잔존 금지(D1 확정 — 0.702 통일).");
            Assert.That(src, Does.Not.Contain("0.88f"), "구 글래스 알파 0.88 잔존 금지(D1 확정).");
        }

        [Test]
        public void V8_Behavior_GlassConstantsAndApplyFigmaGlass_0702()
        {
            Assert.That(UTKTheme.GlassPanel.a, Is.EqualTo(0.702f).Within(0.001f), "GlassPanel 흑 글래스 알파 0.702.");
            Assert.That(UTKTheme.GlassPanelFill.a, Is.EqualTo(0.702f).Within(0.001f), "GlassPanelFill #161B22 알파 0.702.");
            AssertColor(new Color(0x16 / 255f, 0x1B / 255f, 0x22 / 255f, 0.702f), UTKTheme.GlassPanelFill, "GlassPanelFill hue #161B22 유지(알파만 진화).");

            var panel = new VisualElement();
            UTKTheme.ApplyFigmaGlass(panel);
            AssertColor(UTKTheme.GlassPanelFill, panel.style.backgroundColor.value, "ApplyFigmaGlass 배경 = GlassPanelFill(0.702).");
            Assert.That(panel.style.borderTopWidth.value, Is.EqualTo(UTKTheme.GlassStrokeWidth).Within(0.001f), "글래스 스트로크 1.2 불변.");
            Assert.That(panel.style.borderTopLeftRadius.value.value, Is.EqualTo(UTKTheme.GlassRadius).Within(0.001f), "글래스 r14.4 불변.");
        }
    }
}
