using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ProjectName.UI.Toolkit;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ProjectName.Tests.EditMode
{
    public class SoldierManagementFigma69Tests
    {
        private static readonly BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
        private GameObject _documentObject;
        private UIDocument _previousDocument;
        private SoldierManagementUTK _previousInstance;
        private GameObject _windowManagerObject;
        private Component _previousUpdater;
        private bool _previousWarnedNoUpdater;

        [SetUp]
        public void SetUp()
        {
            _previousInstance = (SoldierManagementUTK)typeof(SoldierManagementUTK)
                .GetField("_instance", StaticPrivate).GetValue(null);
            typeof(SoldierManagementUTK).GetField("_instance", StaticPrivate).SetValue(null, null);
            var managerFlags = StaticPrivate;
            var updaterType = typeof(UTKWindowManager).GetNestedType("Updater", managerFlags);
            var updaterField = typeof(UTKWindowManager).GetField("_updater", managerFlags);
            var warnedField = typeof(UTKWindowManager).GetField("_warnedNoUpdater", managerFlags);
            _previousUpdater = (Component)updaterField.GetValue(null);
            _previousWarnedNoUpdater = (bool)warnedField.GetValue(null);
            if (_previousUpdater == null) warnedField.SetValue(null, false);
            _windowManagerObject = new GameObject("SoldierManagementFigma69WindowManager");
            updaterField.SetValue(null, _windowManagerObject.AddComponent(updaterType));
            _previousDocument = (UIDocument)typeof(UIToolkitBootstrap)
                .GetField("_document", StaticPrivate).GetValue(null);
            _documentObject = new GameObject("SoldierManagementFigma69TestDocument");
            var document = _documentObject.AddComponent<UIDocument>();
            document.panelSettings = Resources.Load<PanelSettings>("UI/PanelSettings");
            typeof(UIToolkitBootstrap).GetField("_document", StaticPrivate).SetValue(null, document);
        }

        [TearDown]
        public void TearDown()
        {
            if (SoldierManagementUTK.Instance != null)
            {
                if (SoldierManagementUTK.Instance.IsOpen)
                    SoldierManagementUTK.Instance.Hide();
                SoldierManagementUTK.Instance.RemoveFromHierarchy();
            }
            typeof(SoldierManagementUTK).GetField("_instance", StaticPrivate).SetValue(null, _previousInstance);
            typeof(UIToolkitBootstrap).GetField("_document", StaticPrivate).SetValue(null, _previousDocument);
            typeof(UTKWindowManager).GetField("_updater", StaticPrivate).SetValue(null, _previousUpdater);
            typeof(UTKWindowManager).GetField("_warnedNoUpdater", StaticPrivate).SetValue(null, _previousWarnedNoUpdater);
            if (_windowManagerObject != null) Object.DestroyImmediate(_windowManagerObject);
            if (_documentObject != null) Object.DestroyImmediate(_documentObject);
        }

        [UnityTest]
        public IEnumerator Open_CreatesThreeDirectPanelRootsAtFigma69CanvasGeometry()
        {
            yield return null;
            var canvasRoot = UIToolkitBootstrap.UIRoot;
            Assert.That(canvasRoot, Is.SameAs(_documentObject.GetComponent<UIDocument>().rootVisualElement));
            canvasRoot.style.width = 1920f;
            canvasRoot.style.height = 1080f;
            yield return null;
            SoldierManagementUTK.Open();
            yield return null;
            var composition = SoldierManagementUTK.Instance;
            var list = composition.Q<VisualElement>("SoldierListPanel");
            var detail = composition.Q<VisualElement>("SoldierDetailPanel");
            var deployment = composition.Q<VisualElement>("SoldierDeploymentPanel");

            Assert.That(composition.IsFrameless, Is.True);
            Assert.That(composition.resolvedStyle.backgroundColor.a, Is.EqualTo(0f).Within(0.001f),
                "A frameless composition host must remain transparent so it does not paint over the canvas.");
            Assert.That(composition.resolvedStyle.borderTopWidth, Is.EqualTo(0f).Within(0.001f));
            Assert.That(composition.resolvedStyle.borderBottomWidth, Is.EqualTo(0f).Within(0.001f));
            Assert.That(composition.resolvedStyle.borderLeftWidth, Is.EqualTo(0f).Within(0.001f));
            Assert.That(composition.resolvedStyle.borderRightWidth, Is.EqualTo(0f).Within(0.001f));
            AssertRect(new Rect(168f, 48f, 1584f, 984f), SoldierManagementUTK.CompositionBounds);
            AssertRect(new Rect(0f, 0f, 480f, 984f), SoldierManagementUTK.SoldierListBounds);
            AssertRect(new Rect(504f, 0f, 576f, 984f), SoldierManagementUTK.SoldierDetailBounds);
            AssertRect(new Rect(1104f, 0f, 480f, 984f), SoldierManagementUTK.DeploymentBounds);
            Assert.That(list, Is.Not.Null);
            Assert.That(detail, Is.Not.Null);
            Assert.That(deployment, Is.Not.Null);
            Assert.That(list.resolvedStyle.backgroundColor.a, Is.GreaterThan(0f), "The list panel root paints its own dark background.");
            Assert.That(detail.resolvedStyle.backgroundColor.a, Is.GreaterThan(0f), "The detail panel root paints its own dark background.");
            Assert.That(deployment.resolvedStyle.backgroundColor.a, Is.GreaterThan(0f), "The deployment panel root paints its own dark background.");
            Assert.That(list.parent, Is.SameAs(composition.Content));
            Assert.That(detail.parent, Is.SameAs(composition.Content));
            Assert.That(deployment.parent, Is.SameAs(composition.Content));
            Assert.That(composition.Content.childCount, Is.EqualTo(3),
                "The Figma 69:4 composition must have exactly three sibling panel roots.");

            AssertRawRect(list, SoldierManagementUTK.SoldierListBounds);
            AssertRawRect(detail, SoldierManagementUTK.SoldierDetailBounds);
            AssertRawRect(deployment, SoldierManagementUTK.DeploymentBounds);
            Assert.That(composition.Q<ScrollView>(), Is.Not.Null, "Soldier listing remains in the list panel.");
            Assert.That(composition.Q<Label>("DeploySummary"), Is.Not.Null, "The live summary remains in the list panel.");
            Assert.That(composition.Q<Button>("DeploymentCloseButton"), Is.Not.Null, "The frameless composition keeps a visible close affordance.");
            Assert.That(composition.Q<Button>("DeploymentCloseButton").parent, Is.SameAs(deployment));
            Assert.That(deployment.Q<ScrollView>("DeploymentOptionsList"), Is.Not.Null);
            // 계약 의도: 임무 카드 8개 존재. 정확-이름 Query는 유니크 이름(…_0~_7)이라 1만 반환하는 취약 단정이므로 접두사 매칭으로 교정.
            Assert.That(deployment.Query<VisualElement>()
                .Where(e => e.name != null && e.name.StartsWith("DeploymentOption_")).ToList().Count, Is.EqualTo(8));
            Assert.That(deployment.Q<Button>("ApplyDeploymentButton"), Is.Not.Null,
                "Deployment options are selectable cards with one bottom apply action.");
            Assert.That(deployment.Query<Button>().ToList().Count, Is.EqualTo(2),
                "Only close and the single apply action are buttons; task rows must not have per-row apply buttons.");
            Assert.That(deployment.Q<Label>("DeploymentCount_5"), Is.Not.Null,
                "Figma mining assignment row must expose its live active count.");

            canvasRoot.style.width = 1440f;
            canvasRoot.style.height = 900f;
            yield return null;
            AssertRawRect(list, SoldierManagementUTK.SoldierListBounds);
            AssertRawRect(detail, SoldierManagementUTK.SoldierDetailBounds);
            AssertRawRect(deployment, SoldierManagementUTK.DeploymentBounds);
            // [Figma 정합 v3] 등배수 디자인공간 — k=min(1440/1920,900/1080)=0.75, 창 박스만 등비 축소,
            // 패널은 raw Figma px 유지(조상 _content scale=k). X/Y 독립 신장은 계약에서 제거됐다.
            float k = Mathf.Min(1440f / FigmaCanvasLayout.CanvasWidth, 900f / FigmaCanvasLayout.CanvasHeight);
            Assert.That(composition.resolvedStyle.left, Is.EqualTo(168f * k).Within(1f));
            Assert.That(composition.resolvedStyle.top, Is.EqualTo(48f * k).Within(1f));
            Assert.That(composition.resolvedStyle.width, Is.EqualTo(1584f * k).Within(1f));
            Assert.That(composition.resolvedStyle.height, Is.EqualTo(984f * k).Within(1f));
            Assert.That(composition.Content.style.width.value.value, Is.EqualTo(1584f).Within(0.001f),
                "디자인공간은 raw Figma px 크기를 유지한다.");
            Assert.That(composition.Content.style.height.value.value, Is.EqualTo(984f).Within(0.001f));
            Assert.That(composition.Content.style.scale.value.value.x, Is.EqualTo(k).Within(0.001f));
            Assert.That(composition.Content.style.scale.value.value.y, Is.EqualTo(k).Within(0.001f),
                "등배수 계약 — X/Y 스케일이 동일해야 한다(종횡비 왜곡 금지).");
        }

        private static void AssertRawRect(VisualElement element, Rect bounds)
        {
            // [Figma 정합 v3] 패널 스타일은 raw Figma px 고정 — 루트 크기와 무관(등배수는 조상 스케일).
            Assert.That(element.style.left.value.unit, Is.EqualTo(LengthUnit.Pixel), "left unit");
            Assert.That(element.style.left.value.value, Is.EqualTo(bounds.x).Within(0.001f));
            Assert.That(element.style.top.value.value, Is.EqualTo(bounds.y).Within(0.001f));
            Assert.That(element.style.width.value.value, Is.EqualTo(bounds.width).Within(0.001f));
            Assert.That(element.style.height.value.value, Is.EqualTo(bounds.height).Within(0.001f));
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
