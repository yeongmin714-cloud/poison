using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [68차] 인벤토리 UI Figma 15:4 개정본 정합 계약 (소스 문자열 어서션 — EquipEmbedAndBackdropTests 선례).
    /// 진실치: 카드=UTKSlot 루트 81.6(bg #21262D 60% · radius 9.6 · border 0) · TierStrip 상단 가로 81.6×3.6 ·
    /// EQ 배지 22.6×15.8 · 아이콘 38.4 중앙 · 카드 라벨 없음 · WeaponSlotNumbers 바(열 4·5) ·
    /// 섹션 라벨 "장착 중인 장비" · Divider · 가방 셀 bg #21262D 60% · Count [185차 후속] 15.6/500 #8B949E ·
    /// 푸터 "인벤토리 적재량" 15.6 · 탭 radius 7.2.
    /// </summary>
    public class EquipFigmaSpecTests
    {
        private static string Inv() => System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/InventoryWindowUTK.cs");
        private static string Regions() => System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/InventoryClusterPanelRegions.cs");

        [Test]
        public void EquipCard_IsUtkSlotRoot_WithFigmaGeometry()
        {
            var src = Inv();
            Assert.That(src, Does.Contain("var card = new UTKSlot();"),
                "장비 카드 루트는 UTKSlot(클릭/툴팁 영역=카드 전체)이어야 한다.");
            Assert.That(src, Does.Contain("ApplyEquipCardBase(card);"),
                "카드 베이스(bg #21262D 60% · radius 9.6 · border 0) 적용이 있어야 한다.");
            Assert.That(src, Does.Contain("iconHost.style.width = 38.4f;"),
                "카드 아이콘 38.4×38.4(피그마 실측)여야 한다.");
            Assert.That(src, Does.Contain("iconHost.style.left = 21.6f;"),
                "아이콘은 카드 중앙(21.6,21.6)에 위치해야 한다.");
        }

        [Test]
        public void TierStrip_IsTopHorizontal_FullWidth()
        {
            var src = Inv();
            Assert.That(src, Does.Not.Contain("strip.style.width = 3f;"),
                "65차 세로 등급줄(3px)은 폐기되어야 한다.");
            Assert.That(src, Does.Contain("strip.style.top = 0f;"),
                "TierStrip은 카드 최상단에 붙는다.");
            Assert.That(src, Does.Contain("InventoryClusterPanelRegions.EquippedStripHeight"),
                "스트립 높이는 진실치 상수(3.6)를 쓴다.");
        }

        [Test]
        public void EquipBadge_FigmaSize22_6x15_8()
        {
            var src = Inv();
            Assert.That(src, Does.Contain("badge.style.width = 22.6f;"),
                "EQ 배지 폭 22.6(피그마 실측).");
            Assert.That(src, Does.Contain("badge.style.height = 15.8f;"),
                "EQ 배지 높이 15.8.");
            Assert.That(src, Does.Contain("eqText.style.fontSize = 12f;"),
                "EQ 텍스트 [185차 후속] 9.6→12 사용자 가독성 조정.");
        }

        [Test]
        public void CardLabels_AreRemoved_FigmaHasNone()
        {
            var src = Inv();
            Assert.That(src, Does.Not.Contain("var slotLabel = new Label(def.label);"),
                "피그마 15:4 카드에는 슬롯명 라벨이 없다(툴팁 대체).");
        }

        [Test]
        public void WeaponNumbersBar_Exists_AtColumns4And5()
        {
            var src = Inv();
            Assert.That(src, Does.Contain("BuildEquipNumbersBar();"),
                "WeaponSlotNumbers 바가 빌드되어야 한다.");
            Assert.That(src, Does.Contain("bar.style.top = 210f;"),
                "넘버 바는 EquippedSection 하단(equipPanel-local 210)에 위치한다.");
            Assert.That(src, Does.Contain("WeaponNumberSpacer"),
                "스페이서 3칸이 있어야 한다.");
            Assert.That(src, Does.Contain("numText.style.fontSize = 13.2f;"),
                "웨폰 넘버 [185차 후속] 10.8→13.2 사용자 가독성 조정.");
        }

        [Test]
        public void SectionLabel_AndDivider_Exist()
        {
            var src = Inv();
            Assert.That(src, Does.Contain("장착 중인 장비"),
                "섹션 라벨 텍스트는 피그마 진실치 그대로.");
            Assert.That(src, Does.Contain("name = \"InventoryDivider\""),
                "디바이더 요소가 있어야 한다.");
            Assert.That(Regions(), Does.Contain("new Rect(24f, 381.6f, 456f, 1.2f)"),   // [136차 B2-L2] 365→381.6 진실화
                "Divider 지오메트리 (24,381.6) 456×1.2.");
            Assert.That(Regions(), Does.Contain("new Rect(38.4f, 158.4f, 87f, 17f)"),   // [136차 B2-L2] 141.8→158.4 진실화
                "SectionLabel 지오메트리 (38.4,158.4) 87×17.");
        }

        [Test]
        public void BodyLayout_SkipsLabelAndBarChildren()
        {
            var src = Inv();
            Assert.That(src, Does.Contain("if (row.name == \"EquipSectionLabel\" || row.name == \"EquipNumbersBar\") continue;"),
                "행 순회가 라벨/넘버 바를 카드 행으로 오인하지 않아야 한다.");
        }

        [Test]
        public void BagCells_FigmaFidelity()
        {
            var src = Inv();
            Assert.That(src, Does.Contain("new StyleColor(BagCellBg)"),
                "가방 셀 bg #21262D 60%.");
            Assert.That(src, Does.Contain("countLabel.style.fontSize = 15.6f;"),
                "Count [185차 후속] 13.2→15.6 사용자 가독성 조정.");
            Assert.That(src, Does.Contain("countLabel.style.color = new StyleColor(UTKTheme.TextSub);"),
                "Count 색 #8B949E.");
            Assert.That(src, Does.Contain("name = \"InvStrip\""),
                "가방 셀에도 상단 TierStrip이 있어야 한다.");
            Assert.That(src, Does.Contain("StripColorForRank("),
                "등급색 판정은 공용 헬퍼(단일소스)여야 한다.");
        }

        [Test]
        public void FooterLabel_FigmaText()
        {
            var src = Inv();
            Assert.That(src, Does.Contain("인벤토리 적재량"),
                "푸터 라벨은 피그마 진실치 \"인벤토리 적재량\".");
            Assert.That(src, Does.Contain("UTKTheme.StyleFigmaText(_weightLabel, 18f, 400,"),
                "푸터 라벨 폰트 [185차 후속] 사용자 가독성 조정 18/400(구 Figma 15.6→상향).");
        }

        [Test]
        public void TabRadius_IsFigma72()
        {
            var src = Inv();
            Assert.That(src, Does.Contain("tab.style.borderTopLeftRadius = 7.2f;"),
                "탭 radius 7.2(피그마 실측).");
        }

        [Test]
        public void WeaponResolver_IsSingleSource()
        {
            var src = Inv();
            Assert.That(src, Does.Not.Contain("_weaponIdMap"),
                "private 맵 중복은 WeaponItemResolver 단일소스로 해체되어야 한다.");
            Assert.That(src, Does.Contain("WeaponItemResolver.TryResolve(itemId, out equipId, out type)"),
                "TryResolveWeaponEquip는 공용 해석기에 위임한다.");
        }
    }
}
