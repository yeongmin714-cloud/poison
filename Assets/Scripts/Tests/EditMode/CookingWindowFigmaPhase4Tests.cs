using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using ProjectName.Core;
using ProjectName.UI.Toolkit;

namespace ProjectName.Tests.EditMode
{
    public class CookingWindowFigmaPhase4Tests
    {
        [Test]
        public void CookingPanelGroup_UsesVerifiedSavedFigmaBounds()
        {
            Assert.That(FigmaCanvasLayout.CanvasWidth, Is.EqualTo(1920f));
            Assert.That(FigmaCanvasLayout.CanvasHeight, Is.EqualTo(1080f));
            AssertRect(new Rect(144f, 84f, 1632f, 912f), CookingWindowUTK.FigmaBounds);
            AssertRect(new Rect(144f, 84f, 504f, 912f), CookingWindowUTK.CraftingPanelBounds);
            AssertRect(new Rect(672f, 84f, 576f, 912f), CookingWindowUTK.DetailPanelBounds);
            AssertRect(new Rect(1272f, 84f, 504f, 912f), CookingWindowUTK.StoragePanelBounds);
            string source = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/CookingWindowUTK.cs");
            Assert.That(source, Does.Contain("FigmaCanvasLayout.ApplyDesignSpace(this, _content, FigmaBounds, _canvasLayoutRoot)"),
                "The existing host must map the design-space contract (window box = FigmaBounds x k, raw-px content scaled by k) when the root resolves.");
            Assert.That(source, Does.Contain("UTKWindowChrome.Frameless"),
                "The host must not retain the old standard window title/border chrome.");
        }

        [Test]
        public void CookingPanelBounds_ScaleIndependentlyAgainstCanvasSize()
        {
            var rootSize = new Vector2(960f, 540f);
            AssertRect(new Rect(72f, 42f, 252f, 456f),
                FigmaCanvasLayout.ScaleRect(CookingWindowUTK.CraftingPanelBounds, rootSize));
            AssertRect(new Rect(336f, 42f, 288f, 456f),
                FigmaCanvasLayout.ScaleRect(CookingWindowUTK.DetailPanelBounds, rootSize));
            AssertRect(new Rect(636f, 42f, 252f, 456f),
                FigmaCanvasLayout.ScaleRect(CookingWindowUTK.StoragePanelBounds, rootSize));
        }

        [Test]
        public void RuntimeTree_BuildsThreeSiblingPanelsAtFigmaBoundsWithCookingControls()
        {
            CookingWindowUTK.Ensure();
            var host = CookingWindowUTK.Instance;
            Assert.That(host, Is.Not.Null);

            var craft = host.Q<VisualElement>("cooking-craft-panel");
            var detail = host.Q<VisualElement>("cooking-detail-panel");
            var storage = host.Q<VisualElement>("cooking-storage-panel");
            Assert.That(craft, Is.Not.Null, "Craft must be a real direct child panel in the runtime tree.");
            Assert.That(detail, Is.Not.Null, "Detail must be a real direct child panel in the runtime tree.");
            Assert.That(storage, Is.Not.Null, "Storage must be a real direct child panel in the runtime tree.");
            Assert.That(craft.parent, Is.SameAs(detail.parent));
            Assert.That(detail.parent, Is.SameAs(storage.parent));
            Assert.That(craft.parent, Is.SameAs(host.Content));
            AssertRect(new Rect(0f, 0f, 504f, 912f), BoundsOf(craft));
            AssertRect(new Rect(528f, 0f, 576f, 912f), BoundsOf(detail));
            AssertRect(new Rect(1128f, 0f, 504f, 912f), BoundsOf(storage));
            Assert.That(detail.style.left.value.value, Is.EqualTo(528f),
                "At canonical scale the original Figma-local panel bounds must remain unchanged.");

            Assert.That(craft.Q("cooking-recipe-tabs"), Is.Not.Null);
            Assert.That(craft.Q("cooking-recipe-list"), Is.Not.Null);
            Assert.That(craft.Q("cooking-input-slot-0"), Is.Not.Null);
            Assert.That(craft.Q("cooking-input-slot-1"), Is.Not.Null);
            Assert.That(craft.Q("cooking-input-slot-2"), Is.Not.Null);
            Assert.That(craft.Q("cooking-rate"), Is.Not.Null);
            Assert.That(craft.Q("cooking-cook-action"), Is.Not.Null);
            Assert.That(detail.Q("cooking-selected-detail"), Is.Not.Null);
            Assert.That(detail.Q("cooking-detail-ingredients"), Is.Not.Null);
            Assert.That(storage.Q("cooking-storage-scroll"), Is.Not.Null);
            Assert.That(host.Q("cooking-close-button"), Is.Not.Null, "Frameless host still needs a visible close control.");
        }

        [UnityTest]
        public IEnumerator HostGeometryUpdate_AppliesDesignSpaceAtHalfCanvasKeepingPanelsRaw()
        {
            var documentObject = new GameObject("CookingDesignSpaceTestDocument");
            var document = documentObject.AddComponent<UIDocument>();
            document.panelSettings = Resources.Load<PanelSettings>("UI/PanelSettings");
            Assert.That(document.panelSettings, Is.Not.Null, "Expected the project's existing UI PanelSettings resource.");
            yield return null;

            var canvasRoot = new VisualElement { name = "cooking-design-space-test-root" };
            document.rootVisualElement.Add(canvasRoot);
            canvasRoot.style.width = 960f;
            canvasRoot.style.height = 540f;
            yield return null;
            Assert.That(canvasRoot.resolvedStyle.width, Is.EqualTo(960f).Within(1f));
            Assert.That(canvasRoot.resolvedStyle.height, Is.EqualTo(540f).Within(1f));

            CookingWindowUTK.Ensure();
            var host = CookingWindowUTK.Instance;
            Assert.That(host, Is.Not.Null);
            typeof(CookingWindowUTK)
                .GetField("_canvasLayoutRoot", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(host, canvasRoot);
            host.style.display = DisplayStyle.Flex;
            canvasRoot.Add(host);
            yield return null;

            MethodInfo applyFigmaBounds = typeof(CookingWindowUTK).GetMethod("ApplyFigmaBounds", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(applyFigmaBounds, Is.Not.Null, "Canvas geometry updates must apply the design-space contract.");
            applyFigmaBounds.Invoke(host, null);
            yield return null;

            // 창 박스 = FigmaBounds × k (k = min(960/1920, 540/1080) = 0.5, 등배수)
            AssertRect(new Rect(72f, 42f, 816f, 456f), BoundsOf(host));
            // designSpace(_content)는 raw Figma px 유지 + 균일 스케일 k
            Assert.That(host.Content.style.width.value.value, Is.EqualTo(1632f).Within(0.001f),
                "The design space keeps the raw Figma pixel size.");
            Assert.That(host.Content.style.height.value.value, Is.EqualTo(912f).Within(0.001f));
            Assert.That(host.Content.style.scale.value.value.x, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(host.Content.style.scale.value.value.y, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(host.Content.style.transformOrigin.value,
                Is.EqualTo(new TransformOrigin(Length.Percent(0), Length.Percent(0))));
            // 패널 박스는 CreatePanel의 raw 오프셋 진실치 그대로 — 재스케일 경로 없음
            AssertRect(new Rect(0f, 0f, 504f, 912f), BoundsOf(host.Q<VisualElement>("cooking-craft-panel")));
            AssertRect(new Rect(528f, 0f, 576f, 912f), BoundsOf(host.Q<VisualElement>("cooking-detail-panel")));
            AssertRect(new Rect(1128f, 0f, 504f, 912f), BoundsOf(host.Q<VisualElement>("cooking-storage-panel")));
            // [Figma 64:2 IngredientsGroup] 재료 슬롯 76.8×76.8 — designSpace 내부 authored raw 크기 유지
            Assert.That(host.Q("cooking-input-slot-0").style.width.value.value, Is.EqualTo(76.8f),
                "Subpanel input controls retain authored local dimensions; the design space transform scales them uniformly.");

            host.RemoveFromHierarchy();
            UnityEngine.Object.DestroyImmediate(documentObject);
        }

        [Test]
        public void StorageSourceSlots_AreClickableAndUseExistingIngredientPlacementFlow()
        {
            CookingWindowUTK.Ensure();
            var host = CookingWindowUTK.Instance;
            var inventoryObject = new GameObject("CookingWindowFigmaPhase4InventoryTest");
            try
            {
                var inventory = inventoryObject.AddComponent<PlayerInventory>();
                typeof(PlayerInventory).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(inventory, null);
                var itemData = new PlayerInventory.ItemData
                {
                    id = "cooking-test-apple",
                    displayName = "사과",
                    category = PlayerInventory.ItemCategory.Food
                };
                inventory.AddItem(itemData, 2);
                var privateRefresh = typeof(CookingWindowUTK).GetMethod("RefreshStoragePanel", BindingFlags.Instance | BindingFlags.NonPublic);
                privateRefresh.Invoke(host, null);
                var sourceSlot = host.Q<UTKSlot>("cooking-storage-item-0");
                Assert.That(sourceSlot, Is.Not.Null, "Inventory ingredients materialize as right-hand source slots.");
                Assert.That(sourceSlot.pickingMode, Is.EqualTo(PickingMode.Position),
                    "Right-hand inventory source slots must receive click input.");
                Assert.That(sourceSlot.style.marginLeft.value.value, Is.EqualTo(0f));
                Assert.That(sourceSlot.style.marginRight.value.value, Is.EqualTo(0f));
                Assert.That(sourceSlot.style.marginTop.value.value, Is.EqualTo(0f));
                Assert.That(sourceSlot.style.marginBottom.value.value, Is.EqualTo(0f),
                    "The UTKSlot overlay must stay inside the fixed 81.6 x 81.6 cell.");
                AssertSourcePlacementCallback();

                typeof(CookingWindowUTK).GetMethod("PlaceIngredient", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(host, new object[] { itemData });
                var firstInputSlot = host.Q<UTKSlot>("cooking-input-slot-0");
                Assert.That(firstInputSlot, Is.Not.Null);
                Assert.That(((Label)firstInputSlot.parent[1]).text, Is.EqualTo("사과"),
                    "The existing placement operation fills the first cooking input slot without consuming inventory.");
                Assert.That(inventory.GetItemCount(itemData.id), Is.EqualTo(2), "Placing an ingredient is not consumption.");
            }
            finally
            {
                typeof(PlayerInventory).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                    .GetSetMethod(true).Invoke(null, new object[] { null });
                UnityEngine.Object.DestroyImmediate(inventoryObject);
            }
        }

        private static void AssertSourcePlacementCallback()
        {
            string source = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/CookingWindowUTK.cs");
            int callback = source.IndexOf("entry.RegisterCallback<PointerDownEvent>(evt =>", StringComparison.Ordinal);
            Assert.That(callback, Is.GreaterThanOrEqualTo(0));
            int place = source.IndexOf("PlaceIngredient(ingredient);", callback, StringComparison.Ordinal);
            Assert.That(place, Is.GreaterThan(callback), "A left-click on the inventory source uses existing placement logic.");
            int rightClickGuard = source.IndexOf("if (evt.button == 1) return;", callback, StringComparison.Ordinal);
            Assert.That(rightClickGuard, Is.GreaterThan(callback), "Right click does not clear or replace the source behavior.");
        }

        [Test]
        public void FixedStorageOverlays_ClearDefaultMarginsInsideTheirCells()
        {
            string source = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/CookingWindowUTK.cs");
            int overlay = source.IndexOf("var entry = new UTKSlot();", StringComparison.Ordinal);
            Assert.That(overlay, Is.GreaterThanOrEqualTo(0));
            int cellOverlay = source.IndexOf("entry.style.width = Length.Percent(100f);", overlay, StringComparison.Ordinal);
            Assert.That(cellOverlay, Is.GreaterThan(overlay));
            int nextEntry = source.IndexOf("var entry = new UTKSlot();", cellOverlay + 1, StringComparison.Ordinal);
            string fixedCell = source.Substring(cellOverlay, nextEntry - cellOverlay);
            Assert.That(fixedCell, Does.Contain("entry.style.marginLeft = 0f;"));
            Assert.That(fixedCell, Does.Contain("entry.style.marginRight = 0f;"));
            Assert.That(fixedCell, Does.Contain("entry.style.marginTop = 0f;"));
            Assert.That(fixedCell, Does.Contain("entry.style.marginBottom = 0f;"));
        }

        [Test]
        public void StorageRuntimeTree_HasFixed25PlaceholdersAndScrollableOverflow()
        {
            CookingWindowUTK.Ensure();
            var storage = CookingWindowUTK.Instance.Q<VisualElement>("cooking-storage-panel");
            Assert.That(storage, Is.Not.Null);
            Assert.That(storage.Query<VisualElement>(className: "cooking-storage-placeholder").ToList().Count, Is.EqualTo(25));
            Assert.That(storage.Q("cooking-storage-overflow"), Is.Not.Null,
                "Items beyond the 25 fixed visual cells must remain reachable in the storage scroll view.");
        }

        [Test]
        public void StorageFooter_ExposesSlotCapacityAndProgressWithoutInventingWeightData()
        {
            CookingWindowUTK.Ensure();
            var storage = CookingWindowUTK.Instance.Q<VisualElement>("cooking-storage-panel");
            var footer = storage.Q<VisualElement>("cooking-storage-footer");
            Assert.That(footer, Is.Not.Null, "Saved Figma StorageFooter must have a runtime counterpart.");
            Assert.That(footer.Q<Label>("cooking-storage-weight-title"), Is.Not.Null);
            Assert.That(footer.Q<Label>("cooking-storage-weight-value").text, Does.Contain("사용 슬롯 기반"),
                "PlayerInventory has slot capacity, not weight data; do not claim a numeric weight.");
            Assert.That(footer.Q<VisualElement>("cooking-storage-progress-track"), Is.Not.Null);
            Assert.That(footer.Q<VisualElement>("cooking-storage-progress-fill"), Is.Not.Null);
            Assert.That(footer.style.width.value.value, Is.EqualTo(456f).Within(0.001f));
            // [Figma 64:2 StorageFooter] 456×56 — 구식 61.2 기대를 저장 트리 진실치로 교정
            Assert.That(footer.style.height.value.value, Is.EqualTo(56f).Within(0.001f));
        }

        [Test]
        public void StorageGrid_UsesExactSavedFigmaCellSizeAndHorizontalGap()
        {
            CookingWindowUTK.Ensure();
            var storage = CookingWindowUTK.Instance.Q<VisualElement>("cooking-storage-panel");
            var grid = storage.Q<VisualElement>("cooking-storage-grid");
            Assert.That(grid, Is.Not.Null);
            Assert.That(grid.style.width.value.value, Is.EqualTo(456f).Within(0.001f));
            var cells = storage.Query<VisualElement>(className: "cooking-storage-placeholder").ToList();
            Assert.That(cells.Count, Is.EqualTo(25));
            for (int i = 0; i < cells.Count; i++)
            {
                Assert.That(cells[i].style.width.value.value, Is.EqualTo(81.6f).Within(0.001f), $"cell {i} width");
                Assert.That(cells[i].style.height.value.value, Is.EqualTo(81.6f).Within(0.001f), $"cell {i} height");
                Assert.That(cells[i].style.marginRight.value.value,
                    Is.EqualTo(i % 5 == 4 ? 0f : 9.6f).Within(0.001f), $"cell {i} horizontal gap");
                Assert.That(cells[i].style.marginBottom.value.value, Is.EqualTo(9.6f).Within(0.001f), $"cell {i} vertical gap");
            }
        }

        [Test]
        public void StorageFooterCapacity_ReflectsPlayerInventorySlotArray()
        {
            CookingWindowUTK.Ensure();
            var host = CookingWindowUTK.Instance;
            var inventoryObject = new GameObject("CookingStorageFooterCapacityTest");
            try
            {
                var inventory = inventoryObject.AddComponent<PlayerInventory>();
                typeof(PlayerInventory).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(inventory, null);
                inventory.AddItem(new PlayerInventory.ItemData { id = "footer-test", displayName = "테스트", category = PlayerInventory.ItemCategory.Food }, 1);
                typeof(CookingWindowUTK).GetMethod("RefreshStoragePanel", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(host, null);
                Assert.That(host.Q<Label>("cooking-storage-capacity").text, Does.Contain("1 / 40"));
                var fill = host.Q<VisualElement>("cooking-storage-progress-fill");
                Assert.That(fill.style.width.value.value, Is.EqualTo(11.4f).Within(0.001f));
            Assert.That(host.Q<Label>("cooking-storage-capacity").text, Does.Contain("1 / 40"));
            }
            finally
            {
                typeof(PlayerInventory).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                    .GetSetMethod(true).Invoke(null, new object[] { null });
                UnityEngine.Object.DestroyImmediate(inventoryObject);
            }
        }

        [Test]
        public void StorageGridSource_UsesSavedFigmaGapAndFooterGeometry()
        {
            string source = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/CookingWindowUTK.cs");
            Assert.That(source, Does.Contain("placeholder.style.marginRight = (i % StorageGridColumns == StorageGridColumns - 1) ? 0f : 9.6f;"));
            Assert.That(source, Does.Contain("cooking-storage-footer"));
            Assert.That(source, Does.Contain("cooking-storage-capacity"));
            Assert.That(source, Does.Contain("cooking-storage-progress-fill"));
        }


        [Test]
        public void CookingStationEntry_RoutesToToolkitCookingWindow()
        {
            Type stationType = Type.GetType("ProjectName.UI.CookingStation, ProjectName.UI");
            Assert.That(stationType, Is.Not.Null, "CookingStation must remain available.");
            MethodInfo stationOpen = stationType.GetMethod("OpenCooking", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo cookingOpen = typeof(CookingWindowUTK).GetMethod("Open", BindingFlags.Public | BindingFlags.Static,
                null, Type.EmptyTypes, null);
            Assert.That(stationOpen, Is.Not.Null);
            Assert.That(cookingOpen, Is.Not.Null);
            Assert.That(HasCallTo(stationOpen, cookingOpen), Is.True,
                "The existing station entry route must still open CookingWindowUTK.");
        }

        private static bool HasCallTo(MethodInfo caller, MethodInfo target)
        {
            byte[] il = caller?.GetMethodBody()?.GetILAsByteArray();
            if (il == null || target == null) return false;
            for (int i = 0; i + 4 < il.Length; i++)
            {
                if (il[i] != 0x28 && il[i] != 0x6f) continue;
                int token = il[i + 1] | (il[i + 2] << 8) | (il[i + 3] << 16) | (il[i + 4] << 24);
                try
                {
                    MethodBase called = caller.Module.ResolveMethod(token);
                    if (called == target || (called != null && called.Name == target.Name && called.DeclaringType == target.DeclaringType))
                        return true;
                }
                catch (ArgumentException)
                {
                    // Ignore coincidental metadata tokens that do not resolve to methods.
                }
                i += 4;
            }
            return false;
        }

        private static Rect BoundsOf(VisualElement element)
        {
            return new Rect(element.style.left.value.value, element.style.top.value.value,
                element.style.width.value.value, element.style.height.value.value);
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

// Verified Figma source: .hermes/plans/figma-1920-baseline-2026-10-04/frame-index.json, node 64:2.
// The three saved panels are siblings; CookingWindowUTK remains monolithic, so this change maps
// their group bounds without claiming independent runtime panel elements already exist.
// Tests do not instantiate the cooking singleton or modify inventory state.
// Focus this class for delegated EditMode verification; parent agent runs the compile gate.
