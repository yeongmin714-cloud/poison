using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using ProjectName.UI.Toolkit;

namespace ProjectName.Tests.EditMode
{
    public class FigmaCanvasLayoutTests
    {
        [UnityTest]
        public IEnumerator Apply_CanvasRoot_UsesResolvedDimensionsAndKeepsElementInPlace()
        {
            var documentObject = new GameObject("FigmaCanvasLayoutTestDocument");
            var document = documentObject.AddComponent<UIDocument>();
            document.panelSettings = Resources.Load<PanelSettings>("UI/PanelSettings");
            Assert.That(document.panelSettings, Is.Not.Null, "Expected the project's existing UI PanelSettings resource.");
            yield return null;

            var canvasRoot = new VisualElement();
            document.rootVisualElement.Add(canvasRoot);
            var existingParent = new VisualElement();
            var element = new VisualElement();
            existingParent.Add(element);
            var bounds = new Rect(144f, 84f, 504f, 912f);
            float[,] cases =
            {
                { 1920f, 1080f, 144f, 84f, 504f, 912f },
                { 1440f, 810f, 108f, 63f, 378f, 684f },
                { 1440f, 900f, 108f, 70f, 378f, 760f }
            };

            for (int i = 0; i < cases.GetLength(0); i++)
            {
                canvasRoot.style.width = cases[i, 0];
                canvasRoot.style.height = cases[i, 1];
                yield return null;

                float resolvedWidth = canvasRoot.resolvedStyle.width;
                float resolvedHeight = canvasRoot.resolvedStyle.height;
                Assert.That(resolvedWidth, Is.GreaterThan(0f));
                Assert.That(resolvedHeight, Is.GreaterThan(0f));
                bool applied = FigmaCanvasLayout.Apply(element, bounds, canvasRoot);

                Assert.That(applied, Is.True);
                Assert.That(element.parent, Is.SameAs(existingParent));
                Assert.That(canvasRoot.childCount, Is.Zero);
                Assert.That(element.style.position.value, Is.EqualTo(Position.Absolute));
                Assert.That(element.style.left.value.value, Is.EqualTo(bounds.x * resolvedWidth / FigmaCanvasLayout.CanvasWidth).Within(0.001f));
                Assert.That(element.style.top.value.value, Is.EqualTo(bounds.y * resolvedHeight / FigmaCanvasLayout.CanvasHeight).Within(0.001f));
                Assert.That(element.style.width.value.value, Is.EqualTo(bounds.width * resolvedWidth / FigmaCanvasLayout.CanvasWidth).Within(0.001f));
                Assert.That(element.style.height.value.value, Is.EqualTo(bounds.height * resolvedHeight / FigmaCanvasLayout.CanvasHeight).Within(0.001f));
                Assert.That(resolvedWidth, Is.EqualTo(cases[i, 0]).Within(1f));
                Assert.That(resolvedHeight, Is.EqualTo(cases[i, 1]).Within(1f));
            }

            Object.DestroyImmediate(documentObject);
        }


        [Test]
        public void Apply_NullElementOrRoot_IsSafeAndDoesNotMutateElement()
        {
            var element = new VisualElement();
            element.style.left = 17f;
            element.style.top = 23f;
            var root = new VisualElement();
            root.style.width = 1920f;
            root.style.height = 1080f;

            Assert.That(FigmaCanvasLayout.Apply(null, new Rect(1f, 2f, 3f, 4f), root), Is.False);
            Assert.That(FigmaCanvasLayout.Apply(element, new Rect(1f, 2f, 3f, 4f), null), Is.False);
            Assert.That(element.style.left.value.value, Is.EqualTo(17f));
            Assert.That(element.style.top.value.value, Is.EqualTo(23f));
        }

        [Test]
        public void DesignScale_UsesIsotropicMinimumAgainstCanonicalCanvas()
        {
            Assert.That(FigmaCanvasLayout.DesignScale(new Vector2(1920f, 1080f)), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(FigmaCanvasLayout.DesignScale(new Vector2(960f, 540f)), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(FigmaCanvasLayout.DesignScale(new Vector2(1440f, 1080f)), Is.EqualTo(0.75f).Within(0.0001f),
                "min picks the width axis; a 16:9-tall root must not stretch vertically.");
            Assert.That(FigmaCanvasLayout.DesignScale(new Vector2(1920f, 1200f)), Is.EqualTo(1f).Within(0.0001f),
                "min picks the height axis; taller roots keep the 등배수 (isotropic) contract with no vertical stretch.");
            Assert.That(FigmaCanvasLayout.DesignScale(new Vector2(1440f, 900f)), Is.EqualTo(0.75f).Within(0.0001f),
                "Non-16:9 roots still take the minimum so Figma aspect ratios stay intact.");
        }

        [Test]
        public void DesignScale_UnusableRootSizes_ReturnZero()
        {
            Assert.That(FigmaCanvasLayout.DesignScale(new Vector2(0f, 0f)), Is.EqualTo(0f));
            Assert.That(FigmaCanvasLayout.DesignScale(new Vector2(float.NaN, 1080f)), Is.EqualTo(0f));
            Assert.That(FigmaCanvasLayout.DesignScale(new Vector2(1920f, float.NaN)), Is.EqualTo(0f));
            Assert.That(FigmaCanvasLayout.DesignScale(new Vector2(-10f, 1080f)), Is.EqualTo(0f));
            Assert.That(FigmaCanvasLayout.DesignScale(new Vector2(float.PositiveInfinity, 1080f)), Is.EqualTo(0f));
        }

        [Test]
        public void ApplyDesignSpace_NullArgumentsOrUnresolvedRoot_IsSafeAndDoesNotMutate()
        {
            var window = new VisualElement();
            window.style.left = 17f;
            window.style.top = 23f;
            var designSpace = new VisualElement();
            designSpace.style.width = 504f;
            var root = new VisualElement();   // detached — never laid out, resolvedStyle is unusable
            var bounds = new Rect(1f, 2f, 3f, 4f);

            Assert.That(FigmaCanvasLayout.ApplyDesignSpace(null, designSpace, bounds, root), Is.False);
            Assert.That(FigmaCanvasLayout.ApplyDesignSpace(window, null, bounds, root), Is.False);
            Assert.That(FigmaCanvasLayout.ApplyDesignSpace(window, designSpace, bounds, null), Is.False);
            Assert.That(FigmaCanvasLayout.ApplyDesignSpace(window, designSpace, bounds, root), Is.False);
            Assert.That(window.style.left.value.value, Is.EqualTo(17f));
            Assert.That(window.style.top.value.value, Is.EqualTo(23f));
            Assert.That(designSpace.style.width.value.value, Is.EqualTo(504f));
        }

        [UnityTest]
        public IEnumerator ApplyDesignSpace_UsesIsotropicWindowBoxAndKeepsDesignSpaceRaw()
        {
            var documentObject = new GameObject("FigmaDesignSpaceTestDocument");
            var document = documentObject.AddComponent<UIDocument>();
            document.panelSettings = Resources.Load<PanelSettings>("UI/PanelSettings");
            Assert.That(document.panelSettings, Is.Not.Null, "Expected the project's existing UI PanelSettings resource.");
            yield return null;

            var canvasRoot = new VisualElement();
            document.rootVisualElement.Add(canvasRoot);
            var window = new VisualElement();
            var designSpace = new VisualElement();
            window.Add(designSpace);
            var bounds = new Rect(144f, 84f, 504f, 912f);
            float[,] cases =
            {
                { 1920f, 1080f },
                { 960f, 540f },
                { 1440f, 900f }   // min(1440/1920, 900/1080)=0.75 — 너비 기준 등배수, 세로 신장 없음
            };

            for (int i = 0; i < cases.GetLength(0); i++)
            {
                canvasRoot.style.width = cases[i, 0];
                canvasRoot.style.height = cases[i, 1];
                yield return null;

                float resolvedWidth = canvasRoot.resolvedStyle.width;
                float resolvedHeight = canvasRoot.resolvedStyle.height;
                Assert.That(resolvedWidth, Is.GreaterThan(0f));
                Assert.That(resolvedHeight, Is.GreaterThan(0f));
                float k = FigmaCanvasLayout.DesignScale(new Vector2(resolvedWidth, resolvedHeight));
                bool applied = FigmaCanvasLayout.ApplyDesignSpace(window, designSpace, bounds, canvasRoot);

                Assert.That(applied, Is.True);
                Assert.That(window.style.position.value, Is.EqualTo(Position.Absolute));
                Assert.That(window.style.left.value.value, Is.EqualTo(bounds.x * k).Within(0.001f));
                Assert.That(window.style.top.value.value, Is.EqualTo(bounds.y * k).Within(0.001f));
                Assert.That(window.style.width.value.value, Is.EqualTo(bounds.width * k).Within(0.001f));
                Assert.That(window.style.height.value.value, Is.EqualTo(bounds.height * k).Within(0.001f));
                // 등배수 계약 — 창 박스 종횡비는 항상 Figma 종횡비와 동일(비대칭 신장 없음)
                Assert.That(window.style.width.value.value / window.style.height.value.value,
                    Is.EqualTo(bounds.width / bounds.height).Within(0.01f));
                Assert.That(designSpace.style.width.value.value, Is.EqualTo(bounds.width).Within(0.001f),
                    "The design space keeps the raw Figma pixel size.");
                Assert.That(designSpace.style.height.value.value, Is.EqualTo(bounds.height).Within(0.001f));
                Assert.That(designSpace.style.scale.value.value.x, Is.EqualTo(k).Within(0.001f));
                Assert.That(designSpace.style.scale.value.value.y, Is.EqualTo(k).Within(0.001f));
                Assert.That(designSpace.style.transformOrigin.value,
                    Is.EqualTo(new TransformOrigin(Length.Percent(0), Length.Percent(0))));
            }

            Object.DestroyImmediate(documentObject);
        }

        [Test]
        public void InventoryClusterFigmaLayout_UsesNormalizedFigma15Bounds()
        {
            AssertRect(new Rect(144f, 84f, 504f, 912f), InventoryClusterFigmaLayout.InventoryBounds);
            AssertRect(new Rect(672f, 175.2f, 576f, 729.6f), InventoryClusterFigmaLayout.DetailBounds);
            AssertRect(new Rect(1272f, 84f, 504f, 912f), InventoryClusterFigmaLayout.WarehouseBounds);
        }

        [Test]
        public void InventoryClusterPanelRegions_ToPanelLocal_UsesExactCanvasChildBounds()
        {
            AssertRect(new Rect(24f, 24f, 456f, 52.8f), InventoryClusterPanelRegions.ToPanelLocal(InventoryClusterPanelRegions.InventoryHeader, InventoryClusterFigmaLayout.InventoryBounds));
            AssertRect(new Rect(24f, 84f, 456f, 36.2f), InventoryClusterPanelRegions.ToPanelLocal(InventoryClusterPanelRegions.InventoryTabs, InventoryClusterFigmaLayout.InventoryBounds));
            AssertRect(new Rect(38.4f, 170.8f, 427.2f, 172.8f), InventoryClusterPanelRegions.ToPanelLocal(InventoryClusterPanelRegions.EquipGrid, InventoryClusterFigmaLayout.InventoryBounds));
            AssertRect(new Rect(24f, 373.4f, 456f, 446.4f), InventoryClusterPanelRegions.ToPanelLocal(InventoryClusterPanelRegions.BagGrid, InventoryClusterFigmaLayout.InventoryBounds));
            AssertRect(new Rect(24f, 373.4f, 456f, 81.6f), InventoryClusterPanelRegions.ToPanelLocal(InventoryClusterPanelRegions.BagRow1, InventoryClusterFigmaLayout.InventoryBounds));
            AssertRect(new Rect(24f, 464.6f, 456f, 81.6f), InventoryClusterPanelRegions.ToPanelLocal(InventoryClusterPanelRegions.BagRow2, InventoryClusterFigmaLayout.InventoryBounds));
            AssertRect(new Rect(24f, 555.8f, 456f, 81.6f), InventoryClusterPanelRegions.ToPanelLocal(InventoryClusterPanelRegions.BagRow3, InventoryClusterFigmaLayout.InventoryBounds));
            AssertRect(new Rect(24f, 647f, 456f, 81.6f), InventoryClusterPanelRegions.ToPanelLocal(InventoryClusterPanelRegions.BagRow4, InventoryClusterFigmaLayout.InventoryBounds));
            AssertRect(new Rect(24f, 738.2f, 456f, 81.6f), InventoryClusterPanelRegions.ToPanelLocal(InventoryClusterPanelRegions.BagRow5, InventoryClusterFigmaLayout.InventoryBounds));
            AssertRect(new Rect(24f, 827f, 456f, 56f), InventoryClusterPanelRegions.ToPanelLocal(InventoryClusterPanelRegions.InventoryFooter, InventoryClusterFigmaLayout.InventoryBounds));

            AssertRect(new Rect(24f, 24f, 528f, 52.8f), InventoryClusterPanelRegions.ToPanelLocal(InventoryClusterPanelRegions.DetailHeader, InventoryClusterFigmaLayout.DetailBounds));
            AssertRect(new Rect(24f, 96f, 528f, 67.2f), InventoryClusterPanelRegions.ToPanelLocal(InventoryClusterPanelRegions.DetailItemName, InventoryClusterFigmaLayout.DetailBounds));
            AssertRect(new Rect(24f, 182.4f, 528f, 384f), InventoryClusterPanelRegions.ToPanelLocal(InventoryClusterPanelRegions.DetailItemImage, InventoryClusterFigmaLayout.DetailBounds));
            AssertRect(new Rect(24f, 585.6f, 528f, 120f), InventoryClusterPanelRegions.ToPanelLocal(InventoryClusterPanelRegions.DetailDescription, InventoryClusterFigmaLayout.DetailBounds));

            AssertRect(new Rect(24f, 24f, 456f, 52.8f), InventoryClusterPanelRegions.ToPanelLocal(InventoryClusterPanelRegions.StorageHeader, InventoryClusterFigmaLayout.WarehouseBounds));
            AssertRect(new Rect(24f, 100.8f, 456f, 26.6f), InventoryClusterPanelRegions.ToPanelLocal(InventoryClusterPanelRegions.StorageStats, InventoryClusterFigmaLayout.WarehouseBounds));
            AssertRect(new Rect(24f, 151.4f, 456f, 537.6f), InventoryClusterPanelRegions.ToPanelLocal(InventoryClusterPanelRegions.StorageGrid, InventoryClusterFigmaLayout.WarehouseBounds));
            AssertRect(new Rect(24f, 151.4f, 456f, 81.6f), InventoryClusterPanelRegions.ToPanelLocal(InventoryClusterPanelRegions.StorageRow1, InventoryClusterFigmaLayout.WarehouseBounds));
            AssertRect(new Rect(24f, 242.6f, 456f, 81.6f), InventoryClusterPanelRegions.ToPanelLocal(InventoryClusterPanelRegions.StorageRow2, InventoryClusterFigmaLayout.WarehouseBounds));
            AssertRect(new Rect(24f, 333.8f, 456f, 81.6f), InventoryClusterPanelRegions.ToPanelLocal(InventoryClusterPanelRegions.StorageRow3, InventoryClusterFigmaLayout.WarehouseBounds));
            AssertRect(new Rect(24f, 425f, 456f, 81.6f), InventoryClusterPanelRegions.ToPanelLocal(InventoryClusterPanelRegions.StorageRow4, InventoryClusterFigmaLayout.WarehouseBounds));
            AssertRect(new Rect(24f, 516.2f, 456f, 81.6f), InventoryClusterPanelRegions.ToPanelLocal(InventoryClusterPanelRegions.StorageRow5, InventoryClusterFigmaLayout.WarehouseBounds));
            AssertRect(new Rect(24f, 607.4f, 456f, 81.6f), InventoryClusterPanelRegions.ToPanelLocal(InventoryClusterPanelRegions.StorageRow6, InventoryClusterFigmaLayout.WarehouseBounds));
            AssertRect(new Rect(24f, 713f, 456f, 61.2f), InventoryClusterPanelRegions.ToPanelLocal(InventoryClusterPanelRegions.StorageFooter, InventoryClusterFigmaLayout.WarehouseBounds));
            AssertRect(new Rect(24f, 727.4f, 223.2f, 46.8f), InventoryClusterPanelRegions.ToPanelLocal(InventoryClusterPanelRegions.StoreAllButton, InventoryClusterFigmaLayout.WarehouseBounds));
            AssertRect(new Rect(256.8f, 727.4f, 223.2f, 46.8f), InventoryClusterPanelRegions.ToPanelLocal(InventoryClusterPanelRegions.RetrieveAllButton, InventoryClusterFigmaLayout.WarehouseBounds));
        }

        [Test]
        public void InventoryClusterPanelRegions_StorageBodyRegionsUsePanelLocalFigma15BoundsAndScaleFromResolvedRoot()
        {
            Rect[] regions =
            {
                InventoryClusterPanelRegions.StorageHeaderBody,
                InventoryClusterPanelRegions.StorageStatsBody,
                InventoryClusterPanelRegions.StorageGridBody,
                InventoryClusterPanelRegions.StorageRow1Body,
                InventoryClusterPanelRegions.StorageRow2Body,
                InventoryClusterPanelRegions.StorageRow3Body,
                InventoryClusterPanelRegions.StorageRow4Body,
                InventoryClusterPanelRegions.StorageRow5Body,
                InventoryClusterPanelRegions.StorageRow6Body,
                InventoryClusterPanelRegions.StorageFooterBody,
                InventoryClusterPanelRegions.StoreAllButtonBody,
                InventoryClusterPanelRegions.RetrieveAllButtonBody
            };
            Rect[] expected =
            {
                new Rect(24f, 24f, 456f, 52.8f),
                new Rect(24f, 100.8f, 456f, 26.6f),
                new Rect(24f, 151.4f, 456f, 537.6f),
                new Rect(24f, 151.4f, 456f, 81.6f),
                new Rect(24f, 242.6f, 456f, 81.6f),
                new Rect(24f, 333.8f, 456f, 81.6f),
                new Rect(24f, 425f, 456f, 81.6f),
                new Rect(24f, 516.2f, 456f, 81.6f),
                new Rect(24f, 607.4f, 456f, 81.6f),
                new Rect(24f, 713f, 456f, 61.2f),
                new Rect(24f, 727.4f, 223.2f, 46.8f),
                new Rect(256.8f, 727.4f, 223.2f, 46.8f)
            };

            Vector2[] rootSizes =
            {
                new Vector2(1920f, 1080f),
                new Vector2(1440f, 900f)
            };
            foreach (Vector2 rootSize in rootSizes)
            {
                for (int i = 0; i < regions.Length; i++)
                {
                    AssertRect(expected[i], regions[i]);
                    AssertRect(FigmaCanvasLayout.ScaleRect(expected[i], rootSize),
                        InventoryClusterPanelRegions.ScaleStorageBody(regions[i], rootSize));
                }
            }

            AssertRect(expected[0], InventoryClusterPanelRegions.ScaleStorageBody(expected[0], rootSizes[0]));
            AssertRect(new Rect(18f, 20f, 342f, 44f),
                InventoryClusterPanelRegions.ScaleStorageBody(expected[0], rootSizes[1]));
            AssertRect(new Rect(192.6f, 606.1667f, 167.4f, 39f),
                InventoryClusterPanelRegions.ScaleStorageBody(expected[11], rootSizes[1]));
        }

        [Test]
        public void InventoryClusterPanelRegions_UsesFigmaCellMetricsAndFixedGridRows()
        {
            Assert.That(InventoryClusterPanelRegions.EquipGrid.width, Is.EqualTo(427.2f).Within(0.001f));
            Assert.That(InventoryClusterPanelRegions.EquipGrid.height, Is.EqualTo(172.8f).Within(0.001f));
            Assert.That(InventoryClusterPanelRegions.BagRowCount, Is.EqualTo(5));
            Assert.That(InventoryClusterPanelRegions.StorageRowCount, Is.EqualTo(6));
            Assert.That(InventoryClusterPanelRegions.CellSize, Is.EqualTo(81.6f).Within(0.001f));
            Assert.That(InventoryClusterPanelRegions.CellGap, Is.EqualTo(9.6f).Within(0.001f));
            Assert.That(InventoryClusterPanelRegions.StorageVisibleColumns, Is.EqualTo(5));
            Assert.That(InventoryClusterPanelRegions.StorageVisibleRows, Is.EqualTo(6));
            Assert.That(InventoryClusterPanelRegions.StorageVisibleColumns * InventoryClusterPanelRegions.CellSize
                + (InventoryClusterPanelRegions.StorageVisibleColumns - 1) * InventoryClusterPanelRegions.CellGap,
                Is.EqualTo(446.4f).Within(0.001f));
            Assert.That(InventoryClusterPanelRegions.StorageVisibleRows * InventoryClusterPanelRegions.CellSize
                + (InventoryClusterPanelRegions.StorageVisibleRows - 1) * InventoryClusterPanelRegions.CellGap,
                Is.EqualTo(537.6f).Within(0.001f));
        }

        [Test]
        public void InventoryClusterPanelRegions_StorageGridContentHeight_FitsSixVisibleRows()
        {
            MethodInfo getContentHeight = typeof(InventoryClusterPanelRegions).GetMethod(
                "GetStorageGridContentHeight", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(int) }, null);
            Assert.That(getContentHeight, Is.Not.Null,
                "Storage grid content height must come from the production geometry helper.");

            int itemCount = 30;
            int rows = (itemCount + InventoryClusterPanelRegions.StorageVisibleColumns - 1)
                / InventoryClusterPanelRegions.StorageVisibleColumns;
            Assert.That(rows, Is.EqualTo(6));
            Assert.That((float)getContentHeight.Invoke(null, new object[] { itemCount }),
                Is.EqualTo(537.6f).Within(0.001f));
        }

        [Test]
        public void InventoryClusterPanelRegions_StorageGridContentHeight_PreservesOverflowRows()
        {
            MethodInfo getContentHeight = typeof(InventoryClusterPanelRegions).GetMethod(
                "GetStorageGridContentHeight", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(int) }, null);
            Assert.That(getContentHeight, Is.Not.Null,
                "Storage grid content height must come from the production geometry helper.");

            int itemCount = 31;
            int rows = (itemCount + InventoryClusterPanelRegions.StorageVisibleColumns - 1)
                / InventoryClusterPanelRegions.StorageVisibleColumns;
            Assert.That(rows, Is.EqualTo(7));
            Assert.That((float)getContentHeight.Invoke(null, new object[] { itemCount }),
                Is.EqualTo(628.8f).Within(0.001f),
                "The seventh row must remain in scroll content rather than being clipped to the viewport.");
        }

        [Test]
        public void InventoryClusterPanelRegions_StorageCellBottomMargin_IsOmittedOnlyOnFinalRow()
        {
            MethodInfo getBottomMargin = typeof(InventoryClusterPanelRegions).GetMethod(
                "GetStorageCellBottomMargin", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(int), typeof(int) }, null);
            Assert.That(getBottomMargin, Is.Not.Null,
                "Storage cell bottom margin must come from the production geometry helper.");

            int itemCount = 31;
            Assert.That((float)getBottomMargin.Invoke(null, new object[] { 30, itemCount }), Is.EqualTo(0f));
            for (int index = 25; index < 30; index++)
                Assert.That((float)getBottomMargin.Invoke(null, new object[] { index, itemCount }),
                    Is.EqualTo(9.6f).Within(0.001f), $"Expected the preceding row margin at index {index}.");
        }

        [Test]
        public void InventoryClusterFigmaLayout_ScalesDetailRegionsFromPanelLocalGeometry()
        {
            Rect[] regions =
            {
                new Rect(24f, 24f, 528f, 52.8f),
                new Rect(24f, 96f, 528f, 67.2f),
                new Rect(24f, 182.4f, 528f, 384f),
                new Rect(24f, 585.6f, 528f, 120f)
            };
            Vector2[] rootSizes =
            {
                new Vector2(1920f, 1080f),
                new Vector2(1440f, 900f)
            };

            foreach (Vector2 rootSize in rootSizes)
            {
                foreach (Rect region in regions)
                {
                    Rect actual = InventoryClusterFigmaLayout.ScaleDetailRegion(region, rootSize);
                    AssertRect(FigmaCanvasLayout.ScaleRect(region, rootSize), actual);
                }
            }

            AssertRect(regions[0], InventoryClusterFigmaLayout.ScaleDetailRegion(regions[0], rootSizes[0]));
            AssertRect(new Rect(18f, 20f, 396f, 44f),
                InventoryClusterFigmaLayout.ScaleDetailRegion(regions[0], rootSizes[1]));
            AssertRect(new Rect(18f, 488f, 396f, 100f),
                InventoryClusterFigmaLayout.ScaleDetailRegion(regions[3], rootSizes[1]));
        }

        [Test]
        public void InventoryClusterPanelRegions_ScalesInventoryBodyPanelLocalRegionsFromResolvedRoot()
        {
            MethodInfo scalePanelLocal = typeof(InventoryClusterPanelRegions).GetMethod(
                "ScalePanelLocal", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(Rect), typeof(Vector2) }, null);
            Assert.That(scalePanelLocal, Is.Not.Null,
                "Inventory body regions need a production helper that scales panel-local geometry from UIRoot.");

            Vector2 rootSize = new Vector2(1440f, 900f);
            AssertRect(new Rect(18f, 106.1667f, 342f, 192f),
                (Rect)scalePanelLocal.Invoke(null, new object[] { new Rect(24f, 127.4f, 456f, 230.4f), rootSize }));
            AssertRect(new Rect(18f, 311.1667f, 342f, 372f),
                (Rect)scalePanelLocal.Invoke(null, new object[] { new Rect(24f, 373.4f, 456f, 446.4f), rootSize }));
            AssertRect(new Rect(18f, 689.1667f, 342f, 46.6667f),
                (Rect)scalePanelLocal.Invoke(null, new object[] { new Rect(24f, 827f, 456f, 56f), rootSize }));
        }

        [Test]
        public void InventoryClusterPanelRegions_GetVisibleBagRowCount_UsesFiveRowViewportContract()
        {
            MethodInfo getVisibleBagRowCount = typeof(InventoryClusterPanelRegions).GetMethod(
                "GetVisibleBagRowCount", BindingFlags.Public | BindingFlags.Static, null,
                System.Type.EmptyTypes, null);
            Assert.That(getVisibleBagRowCount, Is.Not.Null,
                "The viewport row count must come from a production layout helper.");
            Assert.That((int)getVisibleBagRowCount.Invoke(null, null), Is.EqualTo(5));
        }

        [UnityTest]
        public IEnumerator InventoryClusterFigmaLayout_ApplyRoutesUseCanvasRootAndKeepParent()
        {
            var documentObject = new GameObject("InventoryClusterFigmaLayoutTestDocument");
            var document = documentObject.AddComponent<UIDocument>();
            document.panelSettings = Resources.Load<PanelSettings>("UI/PanelSettings");
            Assert.That(document.panelSettings, Is.Not.Null, "Expected the project's existing UI PanelSettings resource.");
            yield return null;

            var canvasRoot = document.rootVisualElement;
            float rootWidth = canvasRoot.resolvedStyle.width;
            float rootHeight = canvasRoot.resolvedStyle.height;
            Assert.That(rootWidth, Is.GreaterThan(0f));
            Assert.That(rootHeight, Is.GreaterThan(0f));

            var inventory = new VisualElement();
            var detail = new VisualElement();
            var warehouse = new VisualElement();
            canvasRoot.Add(inventory);
            canvasRoot.Add(detail);
            canvasRoot.Add(warehouse);

            Assert.That(InventoryClusterFigmaLayout.ApplyInventory(inventory, canvasRoot), Is.True);
            Assert.That(InventoryClusterFigmaLayout.ApplyDetail(detail, canvasRoot), Is.True);
            Assert.That(InventoryClusterFigmaLayout.ApplyWarehouse(warehouse, canvasRoot), Is.True);

            AssertAppliedBounds(inventory, InventoryClusterFigmaLayout.InventoryBounds, rootWidth, rootHeight);
            AssertAppliedBounds(detail, InventoryClusterFigmaLayout.DetailBounds, rootWidth, rootHeight);
            AssertAppliedBounds(warehouse, InventoryClusterFigmaLayout.WarehouseBounds, rootWidth, rootHeight);
            Assert.That(inventory.parent, Is.SameAs(canvasRoot));
            Assert.That(detail.parent, Is.SameAs(canvasRoot));
            Assert.That(warehouse.parent, Is.SameAs(canvasRoot));

            Object.DestroyImmediate(documentObject);
        }

        [Test]
        public void ScaleRect_CanonicalRootSize_PreservesFigmaCoordinates()
        {
            var input = new Rect(120f, 90f, 640f, 360f);

            Rect result = FigmaCanvasLayout.ScaleRect(input, new Vector2(1920f, 1080f));

            AssertRect(input, result);
        }

        [Test]
        public void ScaleRect_1440By810Root_ScalesBothAxes()
        {
            var input = new Rect(120f, 90f, 640f, 360f);

            Rect result = FigmaCanvasLayout.ScaleRect(input, new Vector2(1440f, 810f));

            AssertRect(new Rect(90f, 67.5f, 480f, 270f), result);
        }

        [Test]
        public void ScaleRect_1440By900Root_ScalesAxesIndependently()
        {
            var input = new Rect(192f, 108f, 960f, 540f);

            Rect result = FigmaCanvasLayout.ScaleRect(input, new Vector2(1440f, 900f));

            AssertRect(new Rect(144f, 90f, 720f, 450f), result);
        }

        // ── [Figma 정합 v3] 등배수 디자인공간 계약 ──

        [Test]
        public void DesignScale_CanonicalRoot_ReturnsOne()
        {
            Assert.That(FigmaCanvasLayout.DesignScale(new Vector2(1920f, 1080f)), Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void DesignScale_ProportionalRoots_ReturnUniformFactors()
        {
            Assert.That(FigmaCanvasLayout.DesignScale(new Vector2(960f, 540f)), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(FigmaCanvasLayout.DesignScale(new Vector2(3840f, 2160f)), Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void DesignScale_Non169Roots_TakeMinimumAxisNoStretch()
        {
            // 너비 제약(세로가 더 여유) — min이 너비 배율을 선택해 세로 신장이 없다.
            Assert.That(FigmaCanvasLayout.DesignScale(new Vector2(1440f, 1080f)), Is.EqualTo(0.75f).Within(0.0001f));
            // 높이 제약(가로가 더 여유) — min이 높이 배율을 선택해 가로 신장이 없다.
            Assert.That(FigmaCanvasLayout.DesignScale(new Vector2(1920f, 1200f)), Is.EqualTo(1f).Within(0.0001f));
            float k = FigmaCanvasLayout.DesignScale(new Vector2(1067f, 776f));
            Assert.That(k, Is.EqualTo(Mathf.Min(1067f / 1920f, 776f / 1080f)).Within(0.0001f),
                "16:9가 아닌 게임창에서도 등배수(min) 계약이 유지되어야 한다.");
        }

        [Test]
        public void DesignScale_UnusableRoot_ReturnsZero()
        {
            Assert.That(FigmaCanvasLayout.DesignScale(new Vector2(0f, 0f)), Is.EqualTo(0f));
            Assert.That(FigmaCanvasLayout.DesignScale(new Vector2(float.NaN, 1080f)), Is.EqualTo(0f));
            Assert.That(FigmaCanvasLayout.DesignScale(new Vector2(-1920f, 1080f)), Is.EqualTo(0f));
        }

        [Test]
        public void ApplyDesignSpace_UnresolvedRoot_ReturnsFalseWithoutStyleWrites()
        {
            // resolvedStyle이 0인 베어 요소 — DesignScale 0 → false, 스타일 미기록.
            var window = new VisualElement();
            var designSpace = new VisualElement();

            bool applied = FigmaCanvasLayout.ApplyDesignSpace(window, designSpace, new Rect(144f, 48f, 480f, 984f), window);

            Assert.That(applied, Is.False, "Unusable root must be rejected.");
            Assert.That(window.style.width, Is.EqualTo(new StyleLength(StyleKeyword.Null)),
                "Rejected application must not leave partial style writes.");
        }

        [Test]
        public void ApplyDesignSpace_NullArguments_ReturnsFalse()
        {
            var element = new VisualElement();

            Assert.That(FigmaCanvasLayout.ApplyDesignSpace(null, element, new Rect(0f, 0f, 480f, 984f), element), Is.False);
            Assert.That(FigmaCanvasLayout.ApplyDesignSpace(element, null, new Rect(0f, 0f, 480f, 984f), element), Is.False);
            Assert.That(FigmaCanvasLayout.ApplyDesignSpace(element, element, new Rect(0f, 0f, 480f, 984f), null), Is.False);
        }

        private static void AssertAppliedBounds(VisualElement element, Rect figmaBounds, float rootWidth, float rootHeight)
        {
            Rect scaled = FigmaCanvasLayout.ScaleRect(figmaBounds, new Vector2(rootWidth, rootHeight));
            Assert.That(element.style.position.value, Is.EqualTo(Position.Absolute));
            Assert.That(element.style.left.value.value, Is.EqualTo(scaled.x).Within(0.001f));
            Assert.That(element.style.top.value.value, Is.EqualTo(scaled.y).Within(0.001f));
            Assert.That(element.style.width.value.value, Is.EqualTo(scaled.width).Within(0.001f));
            Assert.That(element.style.height.value.value, Is.EqualTo(scaled.height).Within(0.001f));
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
