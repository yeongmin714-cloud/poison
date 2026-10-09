using System.Reflection;
using NUnit.Framework;
using ProjectName.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectName.Tests.EditMode
{
    public class StatusEquipmentFigma84Tests
    {
        private StatusWindowUTK CreateWindow()
        {
            ConstructorInfo constructor = typeof(StatusWindowUTK).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic, null, System.Type.EmptyTypes, null);
            Assert.That(constructor, Is.Not.Null, "StatusWindowUTK must retain its private owner constructor.");
            return (StatusWindowUTK)constructor.Invoke(null);
        }

        [Test]
        public void Figma84Bounds_AreCanvasLocalAfterSubtractingFrameOrigin()
        {
            // Figma frame 84:4 origin=(29618,3); panel absolute bounds are normalized to that origin.
            AssertRect(new Rect(391.8f, 72f, 576f, 936f), StatusWindowUTK.CharacterStatusCanvasBounds);
            AssertRect(new Rect(996.6f, 72f, 531.6f, 936f), StatusWindowUTK.EquipmentCanvasBounds);
            AssertRect(new Rect(391.8f, 72f, 1136.4f, 936f), StatusWindowUTK.CompositionCanvasBounds);
        }

        [Test]
        public void RuntimeComposition_HasTwoCanvasPanelSiblings_AndKeepsStatusBelowFoldContent()
        {
            StatusWindowUTK host = CreateWindow();
            try
            {
                VisualElement content = host.Q<VisualElement>("Content");
                VisualElement status = host.Q<VisualElement>("CharacterStatusPanel");
                VisualElement equipment = host.Q<VisualElement>("EquipmentPanel");
                Assert.That(content, Is.Not.Null);
                Assert.That(status, Is.Not.Null);
                Assert.That(equipment, Is.Not.Null);
                Assert.That(status.parent, Is.SameAs(content));
                Assert.That(equipment.parent, Is.SameAs(content));
                Assert.That(content.childCount, Is.EqualTo(2), "Only the two Figma panel roots are visible siblings.");
                Assert.That(host.IsFrameless, Is.True);
                Assert.That(host.style.backgroundColor.value.a, Is.EqualTo(0f).Within(0.001f));

                Assert.That(status.Q<ScrollView>("StatusScrollView"), Is.Not.Null);
                Assert.That(status.Q<VisualElement>("LegacySupplementaryDetails"), Is.Not.Null);
                Assert.That(status.Q<Label>("CoreStat_Attack"), Is.Not.Null);
                Assert.That(status.Q<Label>("CoreStat_Defense"), Is.Not.Null);
                Assert.That(status.Q<Label>("CoreStat_MaxHealth"), Is.Not.Null);
                Assert.That(status.Q<Label>("CoreStat_Agility"), Is.Not.Null);
                Assert.That(status.Q<Button>("StatusCloseButton"), Is.Not.Null);
                Assert.That(status.Q<Button>("Allocate_Str"), Is.Not.Null);
                Assert.That(status.Q<Button>("TitleCycleButton"), Is.Not.Null);
                Assert.That(status.Q<Button>("AuditReportButton"), Is.Not.Null);

                for (int i = 0; i < 8; i++)
                    Assert.That(equipment.Q<VisualElement>("EquipSlot_" + new[] { "Helmet", "Armor", "Weapon", "Shoes", "Gloves", "Back", "Mask", "Bag" }[i]), Is.Not.Null);
                Assert.That(equipment.Q<VisualElement>("InventoryPackGrid").childCount, Is.EqualTo(25),
                    "Pack presents exactly 25 fixed visual cells, independently of inventory contents.");
                Assert.That(equipment.Q<ScrollView>("EquipmentScrollView"), Is.Not.Null);
            }
            finally
            {
                host.RemoveFromHierarchy();
            }
        }

        [Test]
        public void ToggleRoute_UsesPKey_AndLifecycleStillExposesEscapeClose()
        {
            MethodInfo bootstrap = typeof(StatusWindowUTK).GetMethod("Bootstrap", BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo update = typeof(StatusWindowUTK).GetNestedType("Updater", BindingFlags.NonPublic)
                .GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(bootstrap, Is.Not.Null, "The post-scene status bootstrap must remain in place.");
            Assert.That(update, Is.Not.Null, "P-key and ESC routing must remain in the updater lifecycle.");
            string updateBody = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/StatusWindowUTK.cs");
            StringAssert.Contains("kb.pKey.wasPressedThisFrame", updateBody);
            StringAssert.Contains("kb.escapeKey.wasPressedThisFrame", updateBody);
            Assert.That(typeof(UTKWindowBase).GetMethod("Close", BindingFlags.Instance | BindingFlags.Public), Is.Not.Null,
                "ESC and the panel close button must retain the shared close lifecycle.");
        }

        private static void AssertRect(Rect expected, Rect actual)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.01f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.01f));
            Assert.That(actual.width, Is.EqualTo(expected.width).Within(0.01f));
            Assert.That(actual.height, Is.EqualTo(expected.height).Within(0.01f));
        }
    }
}

