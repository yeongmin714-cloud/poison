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

                // [Figma 정합 v3] 패널은 raw Figma 좌표 고정(스케일 스타일 없음) — 루트 크기 대응은
                // 조상 _content의 등배수 스케일이 담당하고, 그 수학은 FigmaCanvasLayoutTests/요리 UIDocument 테스트가 검증.
                AssertPanel(list, new Rect(144f, 48f, 480f, 984f));
                AssertPanel(detail, new Rect(648f, 252f, 624f, 576f));
                AssertPanel(reward, new Rect(1296f, 48f, 480f, 984f));
                AssertInCanvas(list);
                AssertInCanvas(detail);
                AssertInCanvas(reward);
            }
            finally
            {
                host.RemoveFromHierarchy();
            }
        }

        [Test]
        public void QuestWindow_DeclaresUniformDesignSpaceContract()
        {
            // [Figma 정합 v3] Q키 풀캔버스 호스트도 등배수 디자인공간 계약으로 이관 —
            // 기존 패널별 X/Y 독립 scale(비-16:9 세로 신장 원인) 제거 확인.
            string source = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/QuestWindowUTK.cs");
            Assert.That(source, Does.Contain("FigmaCanvasLayout.ApplyDesignSpace(this, _content, CanvasBounds, _canvasLayoutRoot)"),
                "The quest host must map its window box from CanvasBounds x k while raw-px content scales by k.");
            Assert.That(source, Does.Contain("RegisterCallback<GeometryChangedEvent>(OnCanvasGeometryChanged)"),
                "UIRoot geometry changes must reapply the quest design-space scale.");
            Assert.That(source, Does.Not.Contain("ApplyPanelScale"),
                "The legacy per-panel anisotropic scale path must be removed.");
        }
        [Test]
        public void QuestJournalWindow_DeclaresFigma93ListBoundsAndDesignSpaceContract()
        {
            // [Figma 93:230 QuestListPanel] 480×984 @(144,48) — J키 저널(QuestJournalUTK) 디자인공간 계약.
            // Q키 QuestWindowUTK(상세/보상 패널)도 등배수 디자인공간 계약으로 이관 완료(QuestWindow_DeclaresUniformDesignSpaceContract).
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

        private static void AssertPanel(VisualElement panel, Rect expected)
        {
            Assert.That(panel.style.position.value, Is.EqualTo(Position.Absolute));
            Assert.That(panel.style.left.value.value, Is.EqualTo(expected.x).Within(0.001f));
            Assert.That(panel.style.top.value.value, Is.EqualTo(expected.y).Within(0.001f));
            Assert.That(panel.style.width.value.value, Is.EqualTo(expected.width).Within(0.001f));
            Assert.That(panel.style.height.value.value, Is.EqualTo(expected.height).Within(0.001f));
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
