using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.UI.Toolkit;

namespace ProjectName.Tests.EditMode
{
    public class CraftBenchFigmaPhase4Tests
    {
        [Test]
        public void ArchivedCraftBenchFrames_UseTheThreeFigmaPanelBounds()
        {
            Assert.That(FigmaCanvasLayout.CanvasWidth, Is.EqualTo(1920f));
            Assert.That(FigmaCanvasLayout.CanvasHeight, Is.EqualTo(1080f));
            AssertRect(new Rect(144f, 84f, 1632f, 912f), CraftBenchBaseUTK.CanvasBounds);
            AssertRect(new Rect(144f, 84f, 504f, 912f), CraftBenchBaseUTK.CraftingPanelBounds);
            AssertRect(new Rect(672f, 84f, 576f, 912f), CraftBenchBaseUTK.DetailPanelBounds);
            AssertRect(new Rect(1272f, 84f, 504f, 912f), CraftBenchBaseUTK.StoragePanelBounds);
            AssertRect(new Rect(168f, 108f, 456f, 52.8f), CraftBenchBaseUTK.CraftHeaderBounds);
            AssertRect(new Rect(168f, 236.4f, 456f, 194.4f), CraftBenchBaseUTK.CraftCombinationBounds);
            AssertRect(new Rect(168f, 470.4f, 456f, 419.2f), CraftBenchBaseUTK.CraftRecipeListBounds);
            AssertRect(new Rect(168f, 908.8f, 456f, 63.2f), CraftBenchBaseUTK.CraftFooterBounds);
            AssertRect(new Rect(1296f, 180f, 456f, 24.6f), CraftBenchBaseUTK.StorageStatsBounds);
            AssertRect(new Rect(1296f, 223.8f, 456f, 537.6f), CraftBenchBaseUTK.StorageGridBounds);
        }

        [TestCase(typeof(WeaponForgeUTK), "전체", "무기", "장비", "도구")]
        [TestCase(typeof(CookingBenchUTK), "전체")]
        [TestCase(typeof(AlchemyBenchUTK), "전체", "공격성", "회복성", "마약성", "정신성", "물리성")]
        public void BenchVariant_DeclaresArchiveSpecificLabelsAndFilters(Type type, params string[] labels)
        {
            Assert.That(type.GetProperty("RecipeFilters", BindingFlags.Instance | BindingFlags.NonPublic), Is.Not.Null);
            string source = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/" + type.Name + ".cs");
            foreach (var label in labels) Assert.That(source, Does.Contain("\"" + label + "\""));
        }

        [Test]
        public void BaseComposition_DeclaresSiblingPanelRootsTruthfulStorageAndIndependentRecipeTransactions()
        {
            string source = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/CraftBenchBaseUTK.cs");
            Assert.That(source, Does.Contain("_content.Add(CraftPanelRoot)"));
            Assert.That(source, Does.Contain("_content.Add(DetailPanelRoot)"));
            Assert.That(source, Does.Contain("_content.Add(StoragePanelRoot)"));
            Assert.That(source, Does.Contain("inventory.GetAllSlots()"));
            Assert.That(source, Does.Contain("사용 슬롯:"));
            Assert.That(source, Does.Contain("StorageSource"));
            Assert.That(source, Does.Contain("TryCraft(_matched"));
            // [계약 교정] 제작 위임은 구상 프로바이더가 소유(베이스는 추상 TryCraft만) —
            // WeaponForgeUTK=CraftingHelper.CraftWeapon / AlchemyBenchUTK=CraftingHelper.CraftAlchemy 소스에서 확인.
            string weaponSource = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/WeaponForgeUTK.cs");
            string alchemySource = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/AlchemyBenchUTK.cs");
            Assert.That(weaponSource, Does.Contain("CraftingHelper.CraftWeapon"),
                "Weapon crafting delegates to CraftingHelper in its concrete provider.");
            Assert.That(alchemySource, Does.Contain("CraftingHelper.CraftAlchemy"),
                "Alchemy crafting delegates to CraftingHelper in its concrete provider.");
            Assert.That(source, Does.Contain("_detailImage.style.backgroundImage"));
            Assert.That(source, Does.Not.Contain("245 - 310"), "Do not copy Figma mock stats into live item details.");
        }

        [Test]
        public void RuntimeBenchComposition_ExposesThreeDirectSiblingsAndPhaseButtons()
        {
            Type type = typeof(WeaponForgeUTK);
            var instanceField = type.GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
            var host = (WeaponForgeUTK)instanceField.GetValue(null);
            if (host == null)
            {
                var ctor = type.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, System.Type.EmptyTypes, null);
                host = (WeaponForgeUTK)ctor.Invoke(null);
                instanceField.SetValue(null, host);
            }

            Assert.That(host.IsFrameless, Is.True);
            Assert.That(host.Content.childCount, Is.EqualTo(3));
            Assert.That(host.CraftPanelRoot.parent, Is.SameAs(host.Content));
            Assert.That(host.DetailPanelRoot.parent, Is.SameAs(host.Content));
            Assert.That(host.StoragePanelRoot.parent, Is.SameAs(host.Content));
            AssertStyleRect(host.CraftPanelRoot, new Rect(0f, 0f, 504f, 912f));
            AssertStyleRect(host.DetailPanelRoot, new Rect(528f, 0f, 576f, 912f));
            AssertStyleRect(host.StoragePanelRoot, new Rect(1128f, 0f, 504f, 912f));
            Assert.That(host.Q<Button>("craft-bench-craft-button"), Is.Not.Null);
            Assert.That(host.Q<Button>("craft-bench-close-button"), Is.Not.Null);
            Assert.That(host.Q<ScrollView>("craft-bench-recipe-list"), Is.Not.Null);
            Assert.That(host.Q<VisualElement>("craft-bench-detail-description"), Is.Not.Null);
            Assert.That(host.Q<VisualElement>("craft-bench-storage-cell-24"), Is.Not.Null);
        }

        [Test]
        public void AlchemyBench_ListsDrugDatabaseRecipesWithExactHerbIngredients()
        {
            var instanceField = typeof(AlchemyBenchUTK).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
            var bench = (AlchemyBenchUTK)instanceField.GetValue(null);
            if (bench == null)
            {
                var ctor = typeof(AlchemyBenchUTK).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                    null, Type.EmptyTypes, null);
                bench = (AlchemyBenchUTK)ctor.Invoke(null);
                instanceField.SetValue(null, bench);
            }

            var recipesProperty = typeof(CraftBenchBaseUTK).GetProperty("Recipes", BindingFlags.Instance | BindingFlags.NonPublic);
            var recipes = (System.Collections.Generic.IReadOnlyList<CraftBenchBaseUTK.BenchRecipe>)recipesProperty.GetValue(bench);
            Assert.That(DrugDatabase.All.Count, Is.GreaterThanOrEqualTo(10));
            for (int stage = 1; stage <= 9; stage++)
            {
                string id = "drug_" + stage.ToString("D2");
                var recipe = recipes.FirstOrDefault(r => r.ResultId == id);
                Assert.That(recipe.ResultId, Is.Not.Null, "Drug recipe must be listed: " + id);
                Assert.That(recipe.ResultId, Is.EqualTo(id));
                Assert.That(recipe.MatIds, Has.Length.EqualTo(2));
                foreach (string herbId in recipe.MatIds)
                    Assert.That(HerbDatabase.GetHerbInfo(herbId).id, Is.EqualTo(herbId));
                Assert.That(DrugEffectSystem.CreateDrugItem(stage).id, Is.EqualTo(id));
            }

            Assert.That(recipes.Any(r => r.ResultId == "drug_10"), Is.False,
                "Stage 10 requires an undefined rare ingredient and must not be exposed as craftable.");
            MethodInfo matchesFilter = typeof(AlchemyBenchUTK).GetMethod("MatchesFilter", BindingFlags.Instance | BindingFlags.NonPublic);
            var stageOne = recipes.First(r => r.ResultId == "drug_01");
            Assert.That(matchesFilter.Invoke(bench, new object[] { stageOne, "마약성" }), Is.EqualTo(true));
            Assert.That(matchesFilter.Invoke(bench, new object[] { stageOne, "정신성" }), Is.EqualTo(false));
        }

        [Test]
        public void AlchemyBench_144P2Alch_AppliesV3SurgeryContract()
        {
            // [144차 P2-ALCH] A행 수술 핵심 수치 계약 — 64:337(v3 crafting-panel) 진실치 기반.
            var instanceField = typeof(AlchemyBenchUTK).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
            var bench = (AlchemyBenchUTK)instanceField.GetValue(null);
            if (bench == null)
            {
                var ctor = typeof(AlchemyBenchUTK).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                    null, Type.EmptyTypes, null);
                bench = (AlchemyBenchUTK)ctor.Invoke(null);
                instanceField.SetValue(null, bench);
            }
            var craft = bench.CraftPanelRoot;
            Assert.That(craft, Is.Not.Null);

            // [A5/A18/체인] v3 섹션 −1px — 조합섹션 y 151 · divider 365 · 레시피섹션 (24,385,456,420.2) · 스크롤 h 393.6.
            var combination = craft.Q<VisualElement>("craft-bench-combination-section");
            Assert.That(combination.style.top.value.value, Is.EqualTo(151f).Within(0.001f), "A5 combination section top 152.4→151");
            var recipeRegion = craft.Q<VisualElement>("craft-bench-recipe-list-section");
            Assert.That(recipeRegion.style.top.value.value, Is.EqualTo(385f).Within(0.001f), "A18 recipe section top 386.4→385");
            Assert.That(recipeRegion.style.height.value.value, Is.EqualTo(420.2f).Within(0.001f), "A18 recipe section height 420.2");
            var divider = craft.Q<VisualElement>("alch-combination-divider");
            Assert.That(divider, Is.Not.Null, "v3 divider must exist above the recipe section.");
            Assert.That(divider.style.top.value.value, Is.EqualTo(365f).Within(0.001f), "v3 divider top 366→365");
            Assert.That(divider.style.height.value.value, Is.EqualTo(1.2f).Within(0.001f));
            var book = craft.Q<ScrollView>("craft-bench-recipe-list");
            Assert.That(book.style.height.value.value, Is.EqualTo(393.6f).Within(0.001f), "v3 recipe scroll height 393.6");

            // [A3/A4] 탭바 h 36.2 · 6탭 · 폭 70/72.4 · TabLabel fs 14.4.
            var tabBar = craft.Q<VisualElement>("craft-bench-recipe-filters");
            Assert.That(tabBar, Is.Not.Null);
            Assert.That(tabBar.style.height.value.value, Is.EqualTo(36.2f).Within(0.001f), "A3 tab bar height 37.2→36.2");
            Assert.That(tabBar.childCount, Is.EqualTo(6), "v3 declares six tabs.");
            Assert.That(tabBar[0].style.width.value.value, Is.EqualTo(70f).Within(0.001f), "A3 active tab width 70");
            Assert.That(tabBar[1].style.width.value.value, Is.EqualTo(72.4f).Within(0.001f), "A3 inactive tab width 72.4");
            Assert.That(((Button)tabBar[0]).style.fontSize.value.value, Is.EqualTo(14.4f).Within(0.001f), "A4 tab label font 17→14.4");

            // [A6] 재료 슬롯 76.8×3 실사용 + 결과 91.2 — 60/72 판본 분기 오배칙 폐기.
            var slot0 = craft.Q<UTKSlot>("BenchSlot_0");
            Assert.That(slot0, Is.Not.Null);
            Assert.That(slot0.style.width.value.value, Is.EqualTo(76.8f).Within(0.001f), "A6 ingredient slot 60→76.8");
            Assert.That(craft.Q<UTKSlot>("BenchSlot_2"), Is.Not.Null, "A6 v3 uses three real ingredient slots");
            Assert.That(craft.Q<VisualElement>("BenchSlotPlaceholder_2"), Is.Null, "A6 placeholder branch must be gone");
            var resultSlot = craft.Q<UTKSlot>("BenchResult");
            Assert.That(resultSlot.style.width.value.value, Is.EqualTo(91.2f).Within(0.001f), "A6 result slot 72→91.2");

            // [A7] 성공확률 좌/우 분리 — 좌 "제조 성공 확률" 14.4 + 우측 값 y157.
            var rateLabel = craft.Q<Label>("alch-success-rate-label");
            Assert.That(rateLabel, Is.Not.Null, "A7 success-rate title label must exist.");
            Assert.That(rateLabel.text, Is.EqualTo("제조 성공 확률"));
            Assert.That(rateLabel.style.fontSize.value.value, Is.EqualTo(14.4f).Within(0.001f));
            var rateValue = craft.Q<Label>("craft-bench-success-rate");
            Assert.That(rateValue.style.top.value.value, Is.EqualTo(157f).Within(0.001f), "A7 rate value top 158→157");
            Assert.That(rateValue.style.fontSize.value.value, Is.EqualTo(14.4f).Within(0.001f), "A7 rate value font 13.2→14.4");

            // [A11 승계/W50] BioText fs 15.6 · h 69.
            var bio = bench.DetailPanelRoot.Q<Label>("craft-bench-detail-description");
            Assert.That(bio.style.fontSize.value.value, Is.EqualTo(15.6f).Within(0.001f), "BioText font 13.2→15.6");
            Assert.That(bio.style.height.value.value, Is.EqualTo(69f).Within(0.001f), "BioText height 46→69 (digest_v3 64:507)");

            // [A14/A15/A16] 스토리지 배지/푸터.
            var storage = bench.StoragePanelRoot;
            var badge = storage.Q<VisualElement>("alch-storage-stats-badge");
            Assert.That(badge, Is.Not.Null, "A14 storage badge must exist.");
            Assert.That(badge.style.top.value.value, Is.EqualTo(96f).Within(0.001f), "A14 badge at Figma 64:529 position");
            Assert.That(((Label)badge[0]).text, Is.EqualTo("보유 연금술 재료"));
            var footer = storage.Q<VisualElement>("alch-storage-footer");
            Assert.That(footer, Is.Not.Null, "A16 storage footer must exist.");
            Assert.That(footer.style.top.value.value, Is.EqualTo(697f).Within(0.001f), "A16 storage footer top 697");
            var track = storage.Q<VisualElement>("alch-storage-progress-track");
            Assert.That(track, Is.Not.Null, "A16 progress track must exist.");
            Assert.That(track.style.top.value.value, Is.EqualTo(48f).Within(0.001f), "A15/A16 progress track top 48");
            Assert.That(track.style.height.value.value, Is.EqualTo(7.2f).Within(0.001f));
            Assert.That(storage.Q<Label>("alch-storage-weight-value").text, Is.EqualTo("—"),
                "A15 weight value stays an em-dash slot (D7 — no fabricated weight data).");

            // [A1/A2/A10/A13/A6/A19/A20] 소스 문자열 계약.
            string source = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/AlchemyBenchUTK.cs");
            Assert.That(source, Does.Contain("\"물약 제작\""), "A1 title 24/700 (이모지 병기 문구 폐기).");
            Assert.That(source, Does.Contain("\"BREWING\""), "A2 subtitle BREWING.");
            Assert.That(source, Does.Contain("\"POTIONS\""), "A10 detail subtitle.");
            Assert.That(source, Does.Contain("\"REAGENTS\""), "A13 storage subtitle.");
            Assert.That(source, Does.Contain("ShowThirdIngredientPlaceholder => false"), "A6 60/72 branch retired.");
            Assert.That(source, Does.Contain("SetCount(held + \"/\" + required)"), "A8 count format 보유/요구.");
            Assert.That(source, Does.Contain("필요 연금술 레벨 —"), "A19 subtitle level slot (no fabricated level data).");
            Assert.That(source, Does.Contain("출처: 플레이어 인벤토리"), "A20 game-only source label must be kept.");
        }

        [Test]
        public void ShowLifecycle_AppliesUniformDesignSpaceScalingToBenchContent()
        {
            string source = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/CraftBenchBaseUTK.cs");
            Assert.That(source, Does.Contain("FigmaCanvasLayout.ApplyDesignSpace(this, _content, CanvasBounds, _canvasLayoutRoot)"),
                "The bench must map its window box from CanvasBounds x k while the raw-px content container scales by k (등배수 디자인공간).");
            Assert.That(source, Does.Contain("RegisterCallback<GeometryChangedEvent>(OnCanvasRootGeometryChanged)"),
                "UIRoot geometry changes must reapply the design-space scale.");
        }

        private static void AssertStyleRect(VisualElement element, Rect expected)
        {
            Assert.That(element.style.left.value.value, Is.EqualTo(expected.x).Within(0.001f));
            Assert.That(element.style.top.value.value, Is.EqualTo(expected.y).Within(0.001f));
            Assert.That(element.style.width.value.value, Is.EqualTo(expected.width).Within(0.001f));
            Assert.That(element.style.height.value.value, Is.EqualTo(expected.height).Within(0.001f));
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
