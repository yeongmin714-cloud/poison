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
