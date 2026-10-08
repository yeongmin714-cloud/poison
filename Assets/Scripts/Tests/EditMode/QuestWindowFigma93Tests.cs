using System.Reflection;
using NUnit.Framework;
using ProjectName.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectName.Tests.EditMode
{
    public class QuestWindowFigma93Tests
    {
        [Test]
        public void RuntimePanelRoots_UseSavedCanvasLocalBoundsAndScaleWithCanvas()
        {
            ConstructorInfo constructor = typeof(QuestWindowUTK).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic, null, System.Type.EmptyTypes, null);
            Assert.That(constructor, Is.Not.Null, "QuestWindowUTK keeps its owner-only constructor.");
            var host = (QuestWindowUTK)constructor.Invoke(null);
            try
            {
                VisualElement content = host.Q<VisualElement>("Content");
                VisualElement list = host.Q<VisualElement>("QuestListPanel");
                VisualElement detail = host.Q<VisualElement>("QuestDetailPanel");
                VisualElement reward = host.Q<VisualElement>("RewardPanel");
                Assert.That(content, Is.Not.Null);
                Assert.That(list, Is.Not.Null);
                Assert.That(detail, Is.Not.Null);
                Assert.That(reward, Is.Not.Null);
                Assert.That(list.parent, Is.SameAs(content));
                Assert.That(detail.parent, Is.SameAs(content));
                Assert.That(reward.parent, Is.SameAs(content));
                Assert.That(content.childCount, Is.EqualTo(3), "The transparent full-screen host contains only the three overlay roots.");
                Assert.That(host.IsFrameless, Is.True);
                Assert.That(host.style.backgroundColor.value.a, Is.EqualTo(0f).Within(0.001f));
                Assert.That(host.style.width.value.value, Is.EqualTo(1920f).Within(0.001f));
                Assert.That(host.style.height.value.value, Is.EqualTo(1080f).Within(0.001f));

                ApplyCanvasSize(host, new Vector2(1920f, 1080f));
                AssertPanel(list, new Rect(144f, 48f, 480f, 984f), Vector2.one);
                AssertPanel(detail, new Rect(648f, 252f, 624f, 576f), Vector2.one);
                AssertPanel(reward, new Rect(1296f, 48f, 480f, 984f), Vector2.one);
                AssertInCanvas(list);
                AssertInCanvas(detail);
                AssertInCanvas(reward);

                ApplyCanvasSize(host, new Vector2(960f, 540f));
                AssertPanel(list, new Rect(72f, 24f, 480f, 984f), new Vector2(0.5f, 0.5f));
                AssertPanel(detail, new Rect(324f, 126f, 624f, 576f), new Vector2(0.5f, 0.5f));
                AssertPanel(reward, new Rect(648f, 24f, 480f, 984f), new Vector2(0.5f, 0.5f));
            }
            finally
            {
                host.RemoveFromHierarchy();
            }
        }

        private static void ApplyCanvasSize(QuestWindowUTK host, Vector2 size)
        {
            MethodInfo apply = typeof(QuestWindowUTK).GetMethod("ApplyPanelScale", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(apply, Is.Not.Null, "The canvas geometry update scales the three live panel roots.");
            apply.Invoke(host, new object[] { size });
        }

        [Test]
        public void QuestJournalWindow_DeclaresFigma93ListBoundsAndDesignSpaceContract()
        {
            // [Figma 93:230 QuestListPanel] 480×984 @(144,48) — J키 저널(QuestJournalUTK) 디자인공간 계약.
            // Q키 QuestWindowUTK(상세/보상 패널)는 기존 X/Y 독립 경로 유지(다음 Phase 이관 대상) — 본 테스트와 무관.
            AssertRect(new Rect(144f, 48f, 480f, 984f), QuestJournalUTK.JournalBounds);
            string source = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/QuestJournalUTK.cs");
            Assert.That(source, Does.Contain("FigmaCanvasLayout.ApplyDesignSpace(this, _content, JournalBounds, _canvasLayoutRoot)"),
                "The journal must map its window box from JournalBounds x k while the raw-px content container scales by k (등배수 디자인공간).");
            Assert.That(source, Does.Contain("RegisterCallback<GeometryChangedEvent>(OnCanvasRootGeometryChanged)"),
                "UIRoot geometry changes must reapply the journal design-space scale.");
        }

        private static void AssertRect(Rect expected, Rect actual)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.001f));
            Assert.That(actual.width, Is.EqualTo(expected.width).Within(0.001f));
            Assert.That(actual.height, Is.EqualTo(expected.height).Within(0.001f));
        }

        private static void AssertPanel(VisualElement panel, Rect expected, Vector2 expectedScale)
        {
            Assert.That(panel.style.position.value, Is.EqualTo(Position.Absolute));
            Assert.That(panel.style.left.value.value, Is.EqualTo(expected.x).Within(0.001f));
            Assert.That(panel.style.top.value.value, Is.EqualTo(expected.y).Within(0.001f));
            Assert.That(panel.style.width.value.value, Is.EqualTo(expected.width).Within(0.001f));
            Assert.That(panel.style.height.value.value, Is.EqualTo(expected.height).Within(0.001f));
            Assert.That(panel.style.scale.value.value.x, Is.EqualTo(expectedScale.x).Within(0.001f));
            Assert.That(panel.style.scale.value.value.y, Is.EqualTo(expectedScale.y).Within(0.001f));
            Assert.That(panel.style.transformOrigin.value, Is.EqualTo(new TransformOrigin(0f, 0f, 0f)));
        }

        private static void AssertInCanvas(VisualElement panel)
        {
            float left = panel.style.left.value.value;
            float top = panel.style.top.value.value;
            Assert.That(left, Is.GreaterThanOrEqualTo(0f));
            Assert.That(top, Is.GreaterThanOrEqualTo(0f));
            Assert.That(left + panel.style.width.value.value, Is.LessThanOrEqualTo(1920f));
            Assert.That(top + panel.style.height.value.value, Is.LessThanOrEqualTo(1080f));
        }
    }
}

// Test: canvas-local geometry derived from saved Figma frame 93:230 (origin 32082,3).
// 93:231 QuestListPanel abs=(32226,51,480,984) -> local=(144,48,480,984).
// 93:325 QuestDetailPanel abs=(32730,255,624,576) -> local=(648,252,624,576).
// 93:371 DeploymentPanel abs=(33378,51,480,984) -> local=(1296,48,480,984).
