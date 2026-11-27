using System.Reflection;
using NUnit.Framework;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    public class HumanoidClipDriverComboTests
    {
        private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

        [TestCase(1, "AttackCombo")]
        [TestCase(2, "AttackCombo2")]
        [TestCase(3, "AttackCombo3")]
        public void SwordCombo_MapsEachAcceptedStageToItsRegisteredAnimatorState(int stage, string expectedState)
        {
            var stateName = typeof(HumanoidClipDriver).GetMethod("GetStageStateName", PrivateStatic);
            Assert.That(stateName, Is.Not.Null,
                "Sword/Fist playback must route through the three registered controller states, not WeaponCombo slices.");
            Assert.That(stateName.Invoke(null, new object[] { stage }), Is.EqualTo(expectedState));
        }

        [Test]
        public void RegisteredStageNames_AreDistinctAndDoNotCollapseFollowupsToStageOne()
        {
            var stateName = typeof(HumanoidClipDriver).GetMethod("GetStageStateName", PrivateStatic);
            Assert.That(stateName, Is.Not.Null);
            var stageOne = stateName.Invoke(null, new object[] { 1 });
            var stageTwo = stateName.Invoke(null, new object[] { 2 });
            var stageThree = stateName.Invoke(null, new object[] { 3 });
            Assert.That(stageOne, Is.Not.EqualTo(stageTwo));
            Assert.That(stageTwo, Is.Not.EqualTo(stageThree));
            Assert.That(stageOne, Is.Not.EqualTo(stageThree));

        }

        [Test]
        public void BufferedClicks_AuthorizeAtMostThreeRegisteredStages()
        {
            var capacity = typeof(HumanoidClipDriver).GetMethod("CanAcceptBufferedFollowup", PrivateStatic);
            Assert.That(capacity, Is.Not.Null);
            Assert.That(capacity.Invoke(null, new object[] { true, 1, 0, true }), Is.EqualTo(true));
            Assert.That(capacity.Invoke(null, new object[] { true, 1, 1, true }), Is.EqualTo(true));
            Assert.That(capacity.Invoke(null, new object[] { true, 1, 2, true }), Is.EqualTo(false));
            Assert.That(capacity.Invoke(null, new object[] { true, 2, 0, true }), Is.EqualTo(true));
            Assert.That(capacity.Invoke(null, new object[] { true, 3, 0, true }), Is.EqualTo(false));
            Assert.That(capacity.Invoke(null, new object[] { true, 1, 0, false }), Is.EqualTo(false));
        }

        [TestCase(1, 0.31f, false)]
        [TestCase(1, 0.32f, true)]
        [TestCase(2, 0.32f, true)]
        [TestCase(3, 0.32f, true)]
        public void EachRegisteredStageOwnsExactlyOneHitMarker(int stage, float normalizedTime, bool expectedResolve)
        {
            var resolve = typeof(HumanoidClipDriver).GetMethod("TryResolveStageHit", PrivateStatic);
            Assert.That(resolve, Is.Not.Null);
            var alreadyResolved = new bool[3];
            Assert.That(resolve.Invoke(null, new object[] { stage, normalizedTime, alreadyResolved }), Is.EqualTo(expectedResolve));
            if (expectedResolve)
                Assert.That(resolve.Invoke(null, new object[] { stage, normalizedTime, alreadyResolved }), Is.EqualTo(false),
                    "An authorized registered state may resolve its hit marker only once.");
        }
    }
}
