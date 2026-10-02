using System.Reflection;
using NUnit.Framework;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    public class RegisteredMeleeComboTests
    {
        private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

        [TestCase(1, 1f / 3f)]
        [TestCase(2, 2f / 3f)]
        [TestCase(3, 1f)]
        public void AcceptedSwordClickCount_AdvancesBoundaryInTheSameWeaponComboClip(int clicks, float expectedLimit)
        {
            var method = typeof(HumanoidClipDriver).GetMethod("GetMeleeComboPlaybackLimit", PrivateStatic);
            Assert.That(method, Is.Not.Null);
            Assert.That((float)method.Invoke(null, new object[] { clicks }), Is.EqualTo(expectedLimit).Within(0.0001f));
        }

        [Test]
        public void PlayerController_MapsSingleWeaponComboStateToWeaponComboClip()
        {
            var controllerPath = System.IO.Path.Combine(UnityEngine.Application.dataPath,
                "Resources/Animation/Controllers/Player_AC.controller");
            string controller = System.IO.File.ReadAllText(controllerPath);
            var state = System.Text.RegularExpressions.Regex.Match(controller,
                @"(?s)m_Name: WeaponCombo\r?\n  m_Speed: 2\.5.*?m_Motion: \{fileID: [^,]+, guid: ([0-9a-f]{32})");
            Assert.That(state.Success, Is.True, "The single WeaponCombo Animator state must bind an authored clip.");
            Assert.That(state.Groups[1].Value, Is.EqualTo("cf0d62405fd723f4094948a660129696"),
                "All Sword click-count segments must play the same Weapon_Combo_2 clip.");
        }

        [Test]
        public void WeaponComboStrikeMarkers_AreOneShotAndInsideEachAuthorizedClipSegment()
        {
            var marker = typeof(HumanoidClipDriver).GetMethod("GetMeleeComboStrikeNormalizedTime", PrivateStatic);
            var resolve = typeof(HumanoidClipDriver).GetMethod("TryResolveMeleeComboHit", PrivateStatic);
            Assert.That(marker, Is.Not.Null);
            Assert.That(resolve, Is.Not.Null);
            var resolved = new bool[3];
            float first = (float)marker.Invoke(null, new object[] { 1 });
            float second = (float)marker.Invoke(null, new object[] { 2 });
            float third = (float)marker.Invoke(null, new object[] { 3 });
            Assert.That(first, Is.GreaterThan(0f).And.LessThan(1f / 3f));
            Assert.That(second, Is.GreaterThan(1f / 3f).And.LessThan(2f / 3f));
            Assert.That(third, Is.GreaterThan(2f / 3f).And.LessThan(1f));
            Assert.That(resolve.Invoke(null, new object[] { 1, first - 0.001f, resolved }), Is.EqualTo(false));
            Assert.That(resolve.Invoke(null, new object[] { 1, first, resolved }), Is.EqualTo(true));
            Assert.That(resolve.Invoke(null, new object[] { 1, 0.99f, resolved }), Is.EqualTo(false));
        }

        [Test]
        public void BufferedRegisteredStages_AreBoundedToThreeClicks()
        {
            var capacity = typeof(HumanoidClipDriver).GetMethod("HasComboFollowupCapacity", PrivateStatic);
            Assert.That(capacity, Is.Not.Null);
            Assert.That(capacity.Invoke(null, new object[] { 1, 0 }), Is.EqualTo(true));
            Assert.That(capacity.Invoke(null, new object[] { 1, 1 }), Is.EqualTo(true));
            Assert.That(capacity.Invoke(null, new object[] { 2, 0 }), Is.EqualTo(true),
                "AcceptedClicks already includes the buffered second click; the third accepted click must still fit.");
            Assert.That(capacity.Invoke(null, new object[] { 1, 2 }), Is.EqualTo(false));
            Assert.That(capacity.Invoke(null, new object[] { 3, 0 }), Is.EqualTo(false));
        }
    }
}
