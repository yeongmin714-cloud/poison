using System.Reflection;
using NUnit.Framework;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    public class RegisteredMeleeComboTests
    {
        private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

        [TestCase(1, 0.5f)]
        [TestCase(2, 1f)]
        [TestCase(3, 1f)]   // [168-P1] 상한 클램프 — 3클릭은 2클릭 경계로 수렴
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
                @"(?s)m_Name: WeaponCombo\r?\n  m_Speed: 4(?:\.0)?.*?m_Motion: \{fileID: [^,]+, guid: ([0-9a-f]{32})");
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
            var resolved = new bool[2];
            float first = (float)marker.Invoke(null, new object[] { 1 });
            float second = (float)marker.Invoke(null, new object[] { 2 });
            Assert.That(first, Is.GreaterThan(0f).And.LessThan(0.5f));
            Assert.That(second, Is.GreaterThan(0.5f).And.LessThan(1f));   // [168-P1] 2세그먼트 스트라이크(1.32/2=0.66)
            Assert.That(resolve.Invoke(null, new object[] { 1, first - 0.001f, resolved }), Is.EqualTo(false));
            Assert.That(resolve.Invoke(null, new object[] { 1, first, resolved }), Is.EqualTo(true));
            Assert.That(resolve.Invoke(null, new object[] { 1, 0.99f, resolved }), Is.EqualTo(false));
        }

        [Test]
        public void BufferedRegisteredStages_AreBoundedToTwoClicks()
        {
            var capacity = typeof(HumanoidClipDriver).GetMethod("HasComboFollowupCapacity", PrivateStatic);
            Assert.That(capacity, Is.Not.Null);
            Assert.That(capacity.Invoke(null, new object[] { 1, 0 }), Is.EqualTo(true));
            Assert.That(capacity.Invoke(null, new object[] { 1, 1 }), Is.EqualTo(false),
                "AcceptedClicks already includes the buffered second click; the combo is full at two. [168-P1]");
            Assert.That(capacity.Invoke(null, new object[] { 2, 0 }), Is.EqualTo(false));
            Assert.That(capacity.Invoke(null, new object[] { 1, 2 }), Is.EqualTo(false));
            Assert.That(capacity.Invoke(null, new object[] { 3, 0 }), Is.EqualTo(false));
        }
    }
}
