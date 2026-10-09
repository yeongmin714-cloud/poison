using NUnit.Framework;
using System.Reflection;
using ProjectName.Core;
using ProjectName.UI.Toolkit;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [Figma 15:4 잔여 구조 격차 2건] 인벤 카테고리 탭 5종 + 적재량 푸터(WeightRow) 계약 단정.
    ///
    /// - 탭: TabContainer 윈도우-로컬 (24,84,456,36.2), 탭 5개 각 87.4×36.2, x stride 92.2(갭 4.8).
    ///   라벨 전체/무기/방어구/소모품/재료, 14.4/700. 활성: accent #58A6FF fill + 라벨 #0B0E14,
    ///   비활성: 투명 배경 + 라벨 #8B949E.
    /// - 푸터: WeightLabel x=48 105×18 "적재량" (#8B949E 15.6/400) / WeightValue x=339 141×20 (#58A6FF 15.6/700 우측정렬).
    ///   Figma는 kg(34.8/120.0) 목업이나 런타임 무게 데이터 부재 — 위조 금지 규약(요리 푸터 선례)으로
    ///   "사용 N/40" 슬롯 기반 표기임을 소스계약으로 잠근다.
    ///
    /// 소스 계약 테스트 스타일 — HudBottomAnchorTests/FigmaCanvasLayoutTests 선례처럼
    /// 프로덕션 소스 파일 텍스트와 regions 상수로 배선 계약을 단정한다(최소 단정 원칙).
    /// </summary>
    public class InventoryFigmaTabsFooterTests
    {
        [Test]
        public void InventoryTabsBody_UsesFigma154WindowLocalGeometry()
        {
            AssertRect(new UnityEngine.Rect(24f, 84f, 456f, 36.2f),
                InventoryClusterPanelRegions.InventoryTabsBody);
            Assert.That(InventoryClusterPanelRegions.InventoryTabWidth, Is.EqualTo(87.4f).Within(0.001f),
                "탭 개별 폭은 Figma 15:4 값 87.4여야 한다.");
            Assert.That(InventoryClusterPanelRegions.InventoryTabHeight, Is.EqualTo(36.2f).Within(0.001f),
                "탭 개별 높이는 Figma 15:4 값 36.2여야 한다.");
            Assert.That(InventoryClusterPanelRegions.InventoryTabStride, Is.EqualTo(92.2f).Within(0.001f),
                "탭 x stride는 Figma 15:4 값 92.2(갭 4.8)여야 한다.");
            Assert.That(InventoryClusterPanelRegions.InventoryTabCount, Is.EqualTo(5),
                "탭 5종(전체/무기/방어구/소모품/재료) 계약.");
        }

        [Test]
        public void InventoryWeightRow_UsesFigma154WindowLocalGeometry()
        {
            AssertRect(new UnityEngine.Rect(48f, 841.4f, 105f, 18f),
                InventoryClusterPanelRegions.InventoryWeightLabel,
                "WeightLabel은 윈도우-로컬 x=48, 105×18이어야 한다.");
            AssertRect(new UnityEngine.Rect(339f, 841.4f, 141f, 20f),
                InventoryClusterPanelRegions.InventoryWeightValue,
                "WeightValue는 윈도우-로컬 x=339, 141×20(우측정렬)이어야 한다.");
            Assert.That(InventoryClusterPanelRegions.InventoryMaxSlots, Is.EqualTo(40),
                "슬롯 용량 상수는 PlayerInventory._maxSlots 기본값 40과 동기되어야 한다.");
        }

        [Test]
        public void MatchesActiveTab_CategoryMapping_FollowsFigmaTabContract()
        {
            // [매핑] 전체=전부, 무기=Weapon, 방어구=Armor+Accessory, 소모품=Food+Potion+Drug,
            // 재료=Herb+Meat+Material. Quest/Tool은 Figma 탭 미분류 — "전체" 탭에서만 노출.
            MethodInfo matches = typeof(InventoryWindowUTK).GetMethod(
                "MatchesActiveTab", BindingFlags.NonPublic | BindingFlags.Static, null,
                new[] { typeof(int), typeof(PlayerInventory.ItemCategory) }, null);
            Assert.That(matches, Is.Not.Null,
                "탭 필터 판정은 프로덕션 소스의 정적 헬퍼(단일 진실원)여야 한다.");

            bool Call(int tab, PlayerInventory.ItemCategory cat)
                => (bool)matches.Invoke(null, new object[] { tab, cat });

            // 탭 0 = 전체: 모든 카테고리 통과(탭 미분류 Quest/Tool 포함).
            foreach (PlayerInventory.ItemCategory cat in System.Enum.GetValues(typeof(PlayerInventory.ItemCategory)))
                Assert.That(Call(0, cat), Is.True, $"전체 탭은 {cat}을 통과해야 한다.");

            // 무기 탭 = Weapon만.
            Assert.That(Call(1, PlayerInventory.ItemCategory.Weapon), Is.True);
            Assert.That(Call(1, PlayerInventory.ItemCategory.Armor), Is.False);
            Assert.That(Call(1, PlayerInventory.ItemCategory.Tool), Is.False);

            // 방어구 탭 = Armor + Accessory.
            Assert.That(Call(2, PlayerInventory.ItemCategory.Armor), Is.True);
            Assert.That(Call(2, PlayerInventory.ItemCategory.Accessory), Is.True);
            Assert.That(Call(2, PlayerInventory.ItemCategory.Weapon), Is.False);
            Assert.That(Call(2, PlayerInventory.ItemCategory.Quest), Is.False);

            // 소모품 탭 = Food + Potion + Drug.
            Assert.That(Call(3, PlayerInventory.ItemCategory.Food), Is.True);
            Assert.That(Call(3, PlayerInventory.ItemCategory.Potion), Is.True);
            Assert.That(Call(3, PlayerInventory.ItemCategory.Drug), Is.True);
            Assert.That(Call(3, PlayerInventory.ItemCategory.Herb), Is.False);
            Assert.That(Call(3, PlayerInventory.ItemCategory.Material), Is.False);

            // 재료 탭 = Herb + Meat + Material.
            Assert.That(Call(4, PlayerInventory.ItemCategory.Herb), Is.True);
            Assert.That(Call(4, PlayerInventory.ItemCategory.Meat), Is.True);
            Assert.That(Call(4, PlayerInventory.ItemCategory.Material), Is.True);
            Assert.That(Call(4, PlayerInventory.ItemCategory.Food), Is.False);
            Assert.That(Call(4, PlayerInventory.ItemCategory.Tool), Is.False);
        }

        [Test]
        public void InventoryWindowUTK_TabAndFooterWiring_FollowsFigma154Contract()
        {
            string source = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/InventoryWindowUTK.cs");

            // 탭 5종 라벨 잔존.
            Assert.That(source, Does.Contain("\"전체\""), "탭 1 라벨 '전체' 잔존.");
            Assert.That(source, Does.Contain("\"무기\""), "탭 2 라벨 '무기' 잔존.");
            Assert.That(source, Does.Contain("\"방어구\""), "탭 3 라벨 '방어구' 잔존.");
            Assert.That(source, Does.Contain("\"소모품\""), "탭 4 라벨 '소모품' 잔존.");
            Assert.That(source, Does.Contain("\"재료\""), "탭 5 라벨 '재료' 잔존.");

            // 탭 배치/활성색 계약 — stride 배치 + accent fill/#0B0E14 라벨 + 비활성 #8B949E.
            Assert.That(source, Does.Contain("InventoryClusterPanelRegions.InventoryTabStride * i"),
                "탭 x 배치는 Figma stride 92.2 규약이어야 한다.");
            Assert.That(source, Does.Contain("UTKTheme.Accent : new Color(0f, 0f, 0f, 0f)"),
                "활성 탭=accent fill, 비활성=투명 배경 계약.");
            Assert.That(source, Does.Contain("active ? UTKTheme.BgBase : UTKTheme.TextSub"),
                "활성 라벨=#0B0E14(BgBase), 비활성 라벨=#8B949E(TextSub) 계약.");
            Assert.That(source, Does.Contain("SetActiveTab"),
                "탭 클릭 → SetActiveTab 경로 잔존.");
            Assert.That(source, Does.Contain("RefreshGrid(force: true)"),
                "탭 클릭/열림은 PointerOverUI 가드를 우회하는 강제 재렌더여야 한다.");

            // 푸터 계약 — WeightRow "적재량" + 슬롯 기반 값(위조 금지).
            Assert.That(source, Does.Contain("\"적재량\""), "WeightRow 라벨 텍스트 '적재량' 잔존.");
            Assert.That(source, Does.Contain("$\"사용 {usedSlots}/{capacity}\""),
                "푸터 값은 슬롯 기반 '사용 N/40' 표기여야 한다(요리 푸터 선례).");
            Assert.That(source, Does.Contain("위조 금지"),
                "kg 목업 대신 슬롯 기반 표기임을 위조 금지 규약 주석으로 기록해야 한다.");
            Assert.That(source, Does.Contain("UTKTheme.FontRowLabel"),
                "WeightRow 폰트는 Figma 15.6(FontRowLabel)이어야 한다.");
            Assert.That(source, Does.Contain("TextAnchor.MiddleRight"),
                "WeightValue는 우측정렬이어야 한다.");
        }

        [Test]
        public void InventoryWindowUTK_FooterIsWeightRowOnly_SelectionFeedbackMovedToDetailWindow()
        {
            string source = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/InventoryWindowUTK.cs");

            // 근거: Figma 15:4 InventoryFooter는 WeightRow만 포함 — 레거시 _selectedLabel(선택 피드백)은
            // 상세창(ItemDescriptionWindowUTK, 클러스터 v3)이 담당하므로 푸터에서 제거한다.
            Assert.That(source, Does.Not.Contain("_selectedLabel"),
                "Figma 15:4 푸터=WeightRow 전용 — _selectedLabel은 완전 제거되어야 한다(선택 피드백은 상세창 담당).");
            Assert.That(source, Does.Contain("ItemDescriptionWindowUTK.ShowItem"),
                "선택 시 상세창 호출 경로는 그대로 유지되어야 한다(단독 모드 회귀 방지).");
        }

        private static void AssertRect(UnityEngine.Rect expected, UnityEngine.Rect actual, string because = null)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.001f), because ?? "rect.x 불일치");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.001f), because ?? "rect.y 불일치");
            Assert.That(actual.width, Is.EqualTo(expected.width).Within(0.001f), because ?? "rect.width 불일치");
            Assert.That(actual.height, Is.EqualTo(expected.height).Within(0.001f), because ?? "rect.height 불일치");
        }
    }
}
