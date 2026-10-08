using NUnit.Framework;
using System.Collections;
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
            yield return null;
            var canvasRoot = UIToolkitBootstrap.UIRoot;
            Assert.That(canvasRoot, Is.Not.Null, "UIDocument swap must provide UIRoot.");
            canvasRoot.style.width = 1920f;
            canvasRoot.style.height = 1080f;
            yield return null;

            LootWindowUTK.Ensure();
            var loot = LootWindowUTK.Instance;
            typeof(LootWindowUTK)
                .GetMethod("ApplyFigmaPlacement", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(loot, new object[] { canvasRoot });
            yield return null;

            // k — 루트 resolved 크기에서 산출(PanelSettings 스케일 반올림 허용). k=1에 수렴.
            float kFull = Mathf.Min(canvasRoot.resolvedStyle.width / FigmaCanvasLayout.CanvasWidth,
                canvasRoot.resolvedStyle.height / FigmaCanvasLayout.CanvasHeight);
            AssertDesignSpaceContract(loot, LootWindowUTK.FigmaBounds, kFull);
            AssertRawSize(loot.Q<VisualElement>("LootPanelHeader"), 48f, "header height raw");
            AssertRawSize(loot.Q<VisualElement>("LootGridScroll"), 172.8f, "grid viewport height raw");
            AssertRawWidth(loot.Q<VisualElement>("LootGrid"), 456f, "grid width raw");
            AssertRawSize(loot.Q<VisualElement>("LootStats"), 26.6f, "stats row height raw");

            // 1440×900 축소 — k=min(1440/1920,900/1080)=0.75, 창 박스만 등비 축소, 내부 raw 유지.
            canvasRoot.style.width = 1440f;
            canvasRoot.style.height = 900f;
            yield return null;
            typeof(LootWindowUTK)
                .GetMethod("ApplyFigmaPlacement", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(loot, new object[] { canvasRoot });
            yield return null;

            float kScaled = Mathf.Min(canvasRoot.resolvedStyle.width / FigmaCanvasLayout.CanvasWidth,
                canvasRoot.resolvedStyle.height / FigmaCanvasLayout.CanvasHeight);
            AssertDesignSpaceContract(loot, LootWindowUTK.FigmaBounds, kScaled);
            AssertRawSize(loot.Q<VisualElement>("LootGridScroll"), 172.8f, "grid viewport height stays raw at k=0.75");
            AssertRawWidth(loot.Q<VisualElement>("LootGrid"), 456f, "grid width stays raw at k=0.75");
        }

        [Test]
        public void LootWindow_UsesDesignSpaceReapplyPathWithoutIndependentXYScaling()
        {
            // 재적용 본체는 ApplyDesignSpace 단일 경로여야 한다 — X/Y 독립 스케일(FigmaCanvasLayout.Apply) 잔여 금지.
            string source = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/LootWindowUTK.cs");
            Assert.That(source, Does.Contain("FigmaCanvasLayout.ApplyDesignSpace(this, _content, FigmaBounds, root)"),
                "창 박스는 FigmaBounds×k ApplyDesignSpace 한 경로로 적용된다.");
            Assert.That(source, Does.Not.Contain("FigmaCanvasLayout.Apply("),
                "X/Y 독립 스케일 경로(ScaleRect 곱셈)는 계약에서 제거됐다.");
            Assert.That(source, Does.Contain("AddCornerDecals(_content)"),
                "모서리 데칼은 디자인공간(_content)에 raw 좌표로 부착된다.");
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
            Assert.That(source, Does.Contain("card.style.backgroundColor = UTKTheme.PanelSub;"),
                "Child detail panels retain their own opaque visual styling.");
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
            Assert.That(source, Does.Contain("SmuggleWindowUTK.Open(shopPosition)"));
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