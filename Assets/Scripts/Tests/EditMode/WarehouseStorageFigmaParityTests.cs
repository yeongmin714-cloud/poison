using System.Reflection;
using NUnit.Framework;
using ProjectName.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectName.Tests.EditMode
{
    public sealed class WarehouseStorageFigmaParityTests
    {
        private WarehouseWindowUTK _window;

        [SetUp]
        public void SetUp()
        {
            _window = (WarehouseWindowUTK)typeof(WarehouseWindowUTK)
                .GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, System.Type.EmptyTypes, null)
                .Invoke(null);
        }

        [TearDown]
        public void TearDown()
        {
            if (_window != null)
                _window.Hide();
        }

        [Test]
        public void StoragePanel_UsesFramelessChromeAndSingleFigmaHeader()
        {
            Assert.That(_window.IsFrameless, Is.True);
            Assert.That(_window.Q<Label>("storage-title")?.text, Is.EqualTo("창고"));
            Assert.That(_window.Q<Label>("storage-subtitle")?.text, Is.EqualTo("BASE DEPOSIT"));
            Assert.That(_window.Q<Button>("storage-close"), Is.Not.Null);
            Assert.That(_window.Q("TitleBar").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(_window.Q<Label>("storage-title").style.fontSize.value.value, Is.EqualTo(24f).Within(0.001f));
            Assert.That(_window.Q("StorageCornerTL").style.left.value.value, Is.EqualTo(0f).Within(0.001f));
            Assert.That(_window.Q("StorageCornerTR").style.left.value.value, Is.EqualTo(489.6f).Within(0.01f));
            Assert.That(_window.Q<VisualElement>("StorageFooter").style.top.value.value, Is.EqualTo(713f).Within(0.001f));
            Assert.That(_window.Q<Button>("StoreAll").style.backgroundColor.value.a, Is.EqualTo(0.05882353f).Within(0.001f));
            Assert.That(_window.Q<Button>("RetrieveAll").style.borderTopLeftRadius.value.value, Is.EqualTo(7.2f).Within(0.001f));
        }

        [Test]
        public void StoragePanel_HasFigmaStatsAndBulkActionLabelsWithoutCategoryTabs()
        {
            Invoke("RefreshCapacity");
            Assert.That(_window.Q<Label>("storage-availability")?.style.fontSize.value.value, Is.EqualTo(12f).Within(0.001f));
            Assert.That(_window.Q<Label>("storage-availability")?.text, Is.EqualTo("보관 가능"));
            Assert.That(_window.Q<Label>("storage-capacity")?.text, Does.Contain("사용 슬롯: 0 / 20"));
            MethodInfo capacityLayout = typeof(WarehouseWindowUTK).GetMethod(
                "ApplyCapacityLabelLayout", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(capacityLayout, Is.Not.Null);
            capacityLayout.Invoke(null, new object[] { _window.Q<Label>("storage-capacity"), 1f, 1f });
            Assert.That(_window.Q<Label>("storage-capacity").style.position.value, Is.EqualTo(Position.Absolute));
            Assert.That(_window.Q<Label>("storage-capacity").style.right.value.value, Is.EqualTo(0f).Within(0.001f));
            Assert.That(_window.Q<Button>("StoreAll")?.text, Is.EqualTo("전부 보관하기"));
            Assert.That(_window.Q<Button>("RetrieveAll")?.text, Is.EqualTo("전부 꺼내기"));
            Assert.That(_window.Q("StorageTabs"), Is.Null, "Figma 15:4 has no warehouse category tab strip.");
            Assert.That(_window.Q<Label>("TerritoryButton"), Is.Not.Null);
        }

        [Test]
        public void StoragePanel_RendersSixRowsOfFiveSlotsEvenWhenWarehouseCapacityIsTwenty()
        {
            Invoke("RefreshWarehouseGrid");
            var slots = _window.Query<UTKSlot>(className: "utk-slot").ToList();
            Assert.That(slots, Has.Count.EqualTo(30));
            Assert.That(slots[0].style.width.value.value, Is.EqualTo(81.6f).Within(0.001f));
            Assert.That(slots[0].style.height.value.value, Is.EqualTo(81.6f).Within(0.001f));
            Assert.That(InventoryClusterPanelRegions.CellGap, Is.EqualTo(9.6f).Within(0.001f));
            Assert.That(InventoryClusterPanelRegions.GetStorageGridContentHeight(30), Is.EqualTo(537.6f).Within(0.01f));
            Assert.That(slots[0].Q<VisualElement>("Icon").style.position.value, Is.EqualTo(Position.Absolute));
            Assert.That(slots[0].Q("WarehouseEmptyMark"), Is.Not.Null, "Figma empty slots include the dashed placeholder glyph.");
            Assert.That(slots[0].Q("WarehouseEmptyMark").style.display.value, Is.EqualTo(DisplayStyle.None), "The empty placeholder is enabled only after slot state resolves; unopened test fixtures must not force-show it.");
            Assert.That(slots[0].Q("WarehouseTierStrip"), Is.Not.Null, "Figma cells show a narrow rarity strip.");
        }

        [Test]
        public void StoragePanel_UsesFigmaRootBoundsAndHasCornerDecalsAndFooter()
        {
            Assert.That(_window.style.width.value.value, Is.EqualTo(504f).Within(0.001f));
            Assert.That(_window.style.height.value.value, Is.EqualTo(912f).Within(0.001f));
            Assert.That(_window.style.position.value, Is.EqualTo(Position.Absolute));
            Assert.That(_window.style.left.value.value, Is.EqualTo(2f * FigmaCanvasLayout.CanvasWidth / 3f + 8f).Within(0.001f));
            Assert.That(_window.style.top.value.value, Is.EqualTo(UTKThreeColumnLayout.TopMargin).Within(0.001f));
            Assert.That(_window.Q("WarehouseGridViewport"), Is.Not.Null);
            Assert.That(_window.Q("StorageHeader"), Is.Not.Null);
            Assert.That(_window.Q("StorageFooter"), Is.Not.Null);
            Assert.That(_window.Q("StorageCornerTL"), Is.Not.Null);
            Assert.That(_window.Q("StorageCornerTR"), Is.Not.Null);
            Assert.That(_window.Q("StorageCornerBL"), Is.Not.Null);
            Assert.That(_window.Q("StorageCornerBR"), Is.Not.Null);
        }

        [Test]
        public void StoragePanel_BodyLayoutUsesPanelLocalFigmaRegionsAndKeepsSixRowsInViewport()
        {
            Invoke("ApplyStorageBodyLayout");
            var root = UIToolkitBootstrap.UIRoot;
            Vector2 rootSize = root != null
                ? new Vector2(root.resolvedStyle.width, root.resolvedStyle.height)
                : Vector2.zero;
            if (rootSize.x <= 0f || rootSize.y <= 0f)
                rootSize = FigmaCanvasLayout.CanonicalCanvasSize;

            AssertScaledRect(InventoryClusterPanelRegions.StorageHeaderBody, rootSize, _window.Q("StorageHeader").style);
            AssertScaledRect(InventoryClusterPanelRegions.StorageStatsBody, rootSize, _window.Q("StorageStats").style);
            AssertScaledRect(InventoryClusterPanelRegions.StorageGridBody, rootSize, _window.Q("StorageColumns").style);
            AssertScaledRect(InventoryClusterPanelRegions.StorageFooterBody, rootSize, _window.Q("StorageFooter").style);
            AssertScaledRect(InventoryClusterPanelRegions.StoreAllButtonBody, rootSize, _window.Q<Button>("StoreAll").style);
            AssertScaledRect(InventoryClusterPanelRegions.RetrieveAllButtonBody, rootSize, _window.Q<Button>("RetrieveAll").style);

            float sx = rootSize.x / FigmaCanvasLayout.CanvasWidth;
            float sy = rootSize.y / FigmaCanvasLayout.CanvasHeight;
            var viewport = _window.Q<ScrollView>("WarehouseGridViewport");
            Assert.That(viewport.style.position.value, Is.EqualTo(Position.Absolute));
            Assert.That(viewport.style.left.value.value, Is.EqualTo(0f).Within(0.001f));
            Assert.That(viewport.style.top.value.value, Is.EqualTo(0f).Within(0.001f));
            Assert.That(viewport.style.width.value.value, Is.EqualTo(InventoryClusterPanelRegions.StorageGridBody.width * sx).Within(0.001f));
            Assert.That(viewport.style.height.value.value, Is.EqualTo(InventoryClusterPanelRegions.StorageGridBody.height * sy).Within(0.001f));
        }

        [Test]
        public void StoragePanel_CornerDecalsScalePositionAndStrokeAxesIndependently()
        {
            MethodInfo layout = typeof(WarehouseWindowUTK).GetMethod(
                "ApplyCornerDecalLayout",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(float), typeof(float) },
                null);
            Assert.That(layout, Is.Not.Null);
            Assert.That(layout.IsStatic, Is.False, "Production scales each panel instance's own corner decals.");
            layout.Invoke(_window, new object[] { 0.5f, 0.75f });

            VisualElement topRight = _window.Q("StorageCornerTR");
            Assert.That(topRight.style.left.value.value, Is.EqualTo(244.8f).Within(0.001f));
            Assert.That(topRight.style.width.value.value, Is.EqualTo(7.2f).Within(0.001f));
            Assert.That(topRight.style.height.value.value, Is.EqualTo(10.8f).Within(0.001f));
            Assert.That(topRight[0].style.width.value.value, Is.EqualTo(6f).Within(0.001f));
            Assert.That(topRight[0].style.height.value.value, Is.EqualTo(1.35f).Within(0.001f));
            Assert.That(topRight[0].style.left.value.value, Is.EqualTo(1.2f).Within(0.001f));
            Assert.That(topRight[1].style.width.value.value, Is.EqualTo(0.9f).Within(0.001f));
            Assert.That(topRight[1].style.height.value.value, Is.EqualTo(9f).Within(0.001f));
            Assert.That(topRight[1].style.left.value.value, Is.EqualTo(6.3f).Within(0.001f));

            VisualElement bottomLeft = _window.Q("StorageCornerBL");
            Assert.That(bottomLeft.style.top.value.value, Is.EqualTo(673.2f).Within(0.001f));
            Assert.That(bottomLeft[0].style.top.value.value, Is.EqualTo(9.45f).Within(0.001f));
            Assert.That(bottomLeft[1].style.top.value.value, Is.EqualTo(1.8f).Within(0.001f));
        }

        [Test]
        public void StoragePanel_SlotDecorationScalesHorizontalAndVerticalMetricsSeparately()
        {
            var slot = new UTKSlot();
            MethodInfo styleSlot = typeof(WarehouseWindowUTK).GetMethod(
                "StyleWarehouseSlot",
                BindingFlags.Static | BindingFlags.NonPublic,
                null,
                new[] { typeof(VisualElement), typeof(float), typeof(float), typeof(float), typeof(float), typeof(float), typeof(float) },
                null);
            Assert.That(styleSlot, Is.Not.Null);
            styleSlot.Invoke(null, new object[] { slot, 0.5f, 0.75f, 10.8f, 16.2f, 19.2f, 28.8f });

            Assert.That(slot.Q<VisualElement>("Icon").style.left.value.value, Is.EqualTo(10.8f).Within(0.001f));
            Assert.That(slot.Q<VisualElement>("Icon").style.top.value.value, Is.EqualTo(16.2f).Within(0.001f));
            Assert.That(slot.Q<VisualElement>("Icon").style.width.value.value, Is.EqualTo(19.2f).Within(0.001f));
            Assert.That(slot.Q<VisualElement>("Icon").style.height.value.value, Is.EqualTo(28.8f).Within(0.001f));
            Assert.That(slot.style.borderLeftWidth.value, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(slot.style.borderTopWidth.value, Is.EqualTo(0.75f).Within(0.001f));
            Assert.That(slot.style.borderTopLeftRadius.value.value, Is.EqualTo(4.8f).Within(0.001f));
            Assert.That(slot.Q<Label>("Count").style.left.value.value, Is.EqualTo(28.8f).Within(0.001f));
            Assert.That(slot.Q<Label>("Count").style.top.value.value, Is.EqualTo(45f).Within(0.001f));
            Assert.That(slot.Q("WarehouseTierStrip").style.width.value.value, Is.EqualTo(40.8f).Within(0.001f));
            Assert.That(slot.Q("WarehouseTierStrip").style.height.value.value, Is.EqualTo(2.7f).Within(0.001f));
            Assert.That(slot.Q("WarehouseEmptyMark").style.width.value.value, Is.EqualTo(19.2f).Within(0.001f));
            Assert.That(slot.Q("WarehouseEmptyMark")[1].style.left.value.value, Is.EqualTo(15.2f).Within(0.001f));
            Assert.That(slot.Q("WarehouseEmptyMark")[1].style.top.value.value, Is.EqualTo(28.05f).Within(0.001f));
        }

        [Test]
        public void PlayerCastleLighting_UsesLowWarmAmbientAndRestrainedLightsForIndoorMood()
        {
            Color previousAmbient = RenderSettings.ambientLight;
            UnityEngine.Rendering.AmbientMode previousAmbientMode = RenderSettings.ambientMode;
            GameObject room = ProjectName.Systems.PlayerCastleInteriorBuilder.BuildPlayerCastleInterior("Empire", 0);
            try
            {
                Assert.That(RenderSettings.ambientLight.r, Is.LessThanOrEqualTo(0.10f));
                Assert.That(RenderSettings.ambientLight.g, Is.LessThanOrEqualTo(0.08f));
                Assert.That(RenderSettings.ambientLight.b, Is.LessThanOrEqualTo(0.06f));
                int lightCount = 0;
                bool foundWarmSource = false;
                foreach (Light light in room.GetComponentsInChildren<Light>(true))
                {
                    lightCount++;
                    Assert.That(light.intensity, Is.LessThanOrEqualTo(0.4f), $"{light.name} should remain an understated accent");
                    Assert.That(light.range, Is.LessThanOrEqualTo(6f), $"{light.name} should not wash the room uniformly");
                    Assert.That(RenderSettings.ambientMode, Is.EqualTo(UnityEngine.Rendering.AmbientMode.Flat));
                    if (light.color.r > light.color.b && light.intensity > 0f)
                        foundWarmSource = true;
                }
                Assert.That(lightCount, Is.GreaterThan(0));
                Assert.That(foundWarmSource, Is.True);
            }
            finally
            {
                RenderSettings.ambientLight = previousAmbient;
                RenderSettings.ambientMode = previousAmbientMode;
                Object.DestroyImmediate(room);
            }
        }

        private void Invoke(string methodName)
        {
            MethodInfo method = typeof(WarehouseWindowUTK).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(_window, null);
        }

        private static void AssertScaledRect(Rect figmaRect, Vector2 rootSize, IStyle actual)
        {
            Rect expected = FigmaCanvasLayout.ScaleRect(figmaRect, rootSize);
            Assert.That(actual.position.value, Is.EqualTo(Position.Absolute));
            Assert.That(actual.left.value.value, Is.EqualTo(expected.x).Within(0.001f));
            Assert.That(actual.top.value.value, Is.EqualTo(expected.y).Within(0.001f));
            Assert.That(actual.width.value.value, Is.EqualTo(expected.width).Within(0.001f));
            Assert.That(actual.height.value.value, Is.EqualTo(expected.height).Within(0.001f));
        }
    }
}
