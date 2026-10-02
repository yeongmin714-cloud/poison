using System.Reflection;
using NUnit.Framework;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    public class HumanoidClipDriverComboTests
    {
        private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

        [TestCase(false, false, false, false, false)]
        [TestCase(true, true, false, false, true)]
        [TestCase(true, false, true, true, true)]
        [TestCase(true, false, false, false, false)]
        public void ExternalHitLight_QueuesOnlyWhilePlayerMeleeIsCurrentOrNext(
            bool isPlayer, bool currentMelee, bool transitioning, bool nextMelee, bool expectedQueued)
        {
            var shouldQueue = typeof(HumanoidClipDriver).GetMethod("ShouldQueueExternalHitLight", PrivateStatic);
            Assert.That(shouldQueue, Is.Not.Null);
            Assert.That((bool)shouldQueue.Invoke(null,
                new object[] { isPlayer, currentMelee, transitioning, nextMelee }), Is.EqualTo(expectedQueued));
        }

        [Test]
        public void OneAcceptedClick_AuthorizesOnlyFirstThirdAndOneHitMarker()
        {
            var combo = StartCombo();

            Assert.That(GetPlaybackLimit(combo.AcceptedClicks), Is.EqualTo(1f / 3f).Within(0.0001f));
            Assert.That(EligibleHitSegments(combo.AcceptedClicks, 0.32f), Is.EqualTo(new[] { 1 }));
            Assert.That(EligibleHitSegments(combo.AcceptedClicks, 0.70f), Is.EqualTo(new[] { 1 }));
            Assert.That(EligibleHitSegments(combo.AcceptedClicks, 0.34f), Is.EqualTo(new[] { 1 }),
                "Crossing later animation time still grants no segment-two hit without its click.");
        }

        [Test]
        public void TimelyFollowup_AuthorizesSecondThirdAndSecondHitMarker()
        {
            var combo = StartCombo();

            Assert.That(combo.TryAcceptFollowup(5.2f, 0.6f), Is.True);
            Assert.That(GetPlaybackLimit(combo.AcceptedClicks), Is.EqualTo(2f / 3f).Within(0.0001f));
            Assert.That(EligibleHitSegments(combo.AcceptedClicks, 0.32f), Is.EqualTo(new[] { 1 }));
            Assert.That(EligibleHitSegments(combo.AcceptedClicks, 0.45f), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(EligibleHitSegments(combo.AcceptedClicks, 0.70f), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(EligibleHitSegments(combo.AcceptedClicks, 0.68f), Is.EqualTo(new[] { 1, 2 }),
                "Crossing later animation time still grants no segment-three hit without its click.");
        }

        [Test]
        public void ThirdTimelyClick_AuthorizesAllThreeMarkersAndNoFourthClick()
        {
            var combo = StartCombo();
            Assert.That(combo.TryAcceptFollowup(5.1f, 0.6f), Is.True);
            Assert.That(combo.TryAcceptFollowup(5.2f, 0.6f), Is.True);

            Assert.That(GetPlaybackLimit(combo.AcceptedClicks), Is.EqualTo(1f));
            Assert.That(EligibleHitSegments(combo.AcceptedClicks, 0.99f), Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(combo.TryAcceptFollowup(5.25f, 0.6f), Is.False,
                "The three-click capacity must not authorize a fourth swing.");
        }

        [Test]
        public void WeaponComboPlaybackSpeedAndFollowupWindow_MatchFastSegmentTiming()
        {
            var controllerText = System.IO.File.ReadAllText(
                System.IO.Path.Combine(UnityEngine.Application.dataPath,
                    "Resources/Animation/Controllers/Player_AC.controller"));
            var state = System.Text.RegularExpressions.Regex.Match(controllerText,
                @"(?m)^  m_Name: WeaponCombo\r?\n  m_Speed: ([0-9.]+)$");
            Assert.That(state.Success, Is.True, "The controller must define the WeaponCombo playback speed.");
            Assert.That(float.Parse(state.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                Is.EqualTo(2.5f), "WeaponCombo should play at 2.5x.");

            float clickWindow = GetClickWindow();
            Assert.That(clickWindow, Is.EqualTo(0.85f).Within(0.0001f),
                "At 2.5x, a 0.85s window adds a small post-segment input grace to the ~0.60s segment.");

            var timelyCombo = StartCombo();
            Assert.That(timelyCombo.TryAcceptFollowup(5.84f, clickWindow), Is.True,
                "A follow-up at 5.84f after the 5f start should be accepted within the tuned window.");
            var lateCombo = StartCombo();
            Assert.That(lateCombo.TryAcceptFollowup(5.86f, clickWindow), Is.False,
                "A follow-up at 5.86f after the 5f start should be rejected beyond the tuned window.");
        }

        [Test]
        public void FollowupResumesAtHeldBoundary_AndExpiredWindowExitsAfterAuthorizedSegment()
        {
            float clickWindow = GetClickWindow();
            var combo = StartCombo();
            Assert.That(combo.Evaluate(5.1f, 1f / 3f, clickWindow), Is.EqualTo(MeleeComboStateMachine.Decision.Hold));
            float timelyFollowupTime = 5f + clickWindow - 0.01f;
            Assert.That(combo.TryAcceptFollowup(timelyFollowupTime, clickWindow), Is.True);
            Assert.That(combo.Evaluate(timelyFollowupTime, 1f / 3f, clickWindow), Is.EqualTo(MeleeComboStateMachine.Decision.Resume));

            var expired = StartCombo();
            float expiryTime = 5f + clickWindow + 0.01f;
            Assert.That(expired.Evaluate(expiryTime, 1f / 3f, clickWindow), Is.EqualTo(MeleeComboStateMachine.Decision.Exit));
            Assert.That(expired.DidExitBecauseFollowupWindowExpired, Is.True);
            Assert.That(expired.TryAcceptFollowup(expiryTime + 0.01f, clickWindow), Is.False);
        }

        [TestCase(1, 1f / 3f, new[] { 1 })]
        [TestCase(2, 2f / 3f, new[] { 1, 2 })]
        [TestCase(3, 1f, new[] { 1, 2, 3 })]
        public void ClickCount_AuthorizesExistingOneTwoOrThreeSegmentMapping(int clicks, float playbackLimit, int[] expectedSegments)
        {
            Assert.That(GetPlaybackLimit(clicks), Is.EqualTo(playbackLimit).Within(0.0001f));
            Assert.That(EligibleHitSegments(clicks, 0.99f), Is.EqualTo(expectedSegments));
        }

        private static float GetClickWindow()
        {
            var field = typeof(HumanoidClipDriver).GetField("MeleeComboClickWindow", PrivateStatic);
            Assert.That(field, Is.Not.Null);
            return (float)field.GetRawConstantValue();
        }

        [Test]
        public void ThreeStrikeMarkers_AreDistinctAndOrderedWithinWeaponCombo()
        {
            float first = GetStrikeMarker(1);
            float second = GetStrikeMarker(2);
            float third = GetStrikeMarker(3);

            Assert.That(first, Is.GreaterThan(0f));
            Assert.That(second, Is.GreaterThan(first));
            Assert.That(third, Is.GreaterThan(second));
            Assert.That(third, Is.LessThan(1f));
        }

        [Test]
        public void IncorrectSingleAttackHelpers_AreRemoved()
        {
            Assert.That(typeof(HumanoidClipDriver).GetMethod("GetPlayerMeleeOneShotStateName", PrivateStatic), Is.Null);
            Assert.That(typeof(HumanoidClipDriver).GetMethod("PlayPlayerMeleeOneShot", BindingFlags.NonPublic | BindingFlags.Instance), Is.Null);
        }

        [Test]
        public void SpearFollowupDispatch_UsesAcceptedClickTimeRatherThanDelayedDispatchTime()
        {
            var dispatch = typeof(HumanoidClipDriver).GetMethod("ShouldAuthorizeSpearFollowupAtDispatch", PrivateStatic);
            Assert.That(dispatch, Is.Not.Null,
                "Dispatch must consume a click accepted inside the window even if processing occurs afterward.");

            Assert.That((bool)dispatch.Invoke(null, new object[] { false, 1 }), Is.True,
                "An accepted click inside PlayerCombat is dispatched after its input-time window check.");
            Assert.That((bool)dispatch.Invoke(null, new object[] { true, 1 }), Is.False);
            Assert.That((bool)dispatch.Invoke(null, new object[] { false, 0 }), Is.False);
            Assert.That((bool)dispatch.Invoke(null, new object[] { false, 2 }), Is.False,
                "The spear sequence remains capped at opener plus one follow-up.");
            Assert.That((bool)dispatch.Invoke(null, new object[] { false, 1 }), Is.True);
        }

        [Test]
        public void SpearRestart_RecognizesExpiredAcceptedClickAndReplaysFromStart()
        {
            var expired = typeof(HumanoidClipDriver).GetMethod("IsSpearSequenceExpired", PrivateStatic);
            Assert.That(expired, Is.Not.Null);
            Assert.That((bool)expired.Invoke(null, new object[] { true, 5f, 5.849f, 0.85f }), Is.False);
            Assert.That((bool)expired.Invoke(null, new object[] { true, 5f, 5.85f, 0.85f }), Is.True);
            Assert.That((bool)expired.Invoke(null, new object[] { false, 5f, 6f, 0.85f }), Is.False);
        }

        [Test]
        public void SpearSequence_IsRetiredWhenOpenerOrFollowupIsInterrupted()
        {
            var interrupted = typeof(HumanoidClipDriver).GetMethod("ShouldResetInterruptedSpearSequence", PrivateStatic);
            Assert.That(interrupted, Is.Not.Null);

            Assert.That((bool)interrupted.Invoke(null, new object[] { true, false, false, false, false, false }), Is.False,
                "The active AttackThrust opener remains valid until its state actually exits.");
            Assert.That((bool)interrupted.Invoke(null, new object[] { true, true, false, false, false, false }), Is.True,
                "An opener that exited before the follow-up starts is no longer active.");
            Assert.That((bool)interrupted.Invoke(null, new object[] { true, true, true, true, false, false }), Is.True,
                "An interrupted WeaponCombo follow-up must clear the spear sequence.");
            Assert.That((bool)interrupted.Invoke(null, new object[] { true, true, true, false, true, false }), Is.False,
                "The opener remains valid until it reaches the authored exit transition.");
            Assert.That((bool)interrupted.Invoke(null, new object[] { true, false, false, false, true, false }), Is.False,
                "The opener remains active while AttackThrust plays.");
            Assert.That((bool)interrupted.Invoke(null, new object[] { true, false, false, false, false, false }), Is.False,
                "A Spear trigger may be pending while the Animator still reports Idle.");
            Assert.That((bool)interrupted.Invoke(null, new object[] { true, true, true, true, false, true }), Is.False,
                "The authorized follow-up remains active while WeaponCombo plays.");
            Assert.That((bool)interrupted.Invoke(null, new object[] { false, false, false, false, false, false }), Is.False);
        }

        [Test]
        public void SpearOpener_UsesAttackThrustAndResolvesOneHitOnlyAfterItsStrikeMarker()
        {
            var stateName = typeof(HumanoidClipDriver).GetField("SpearOpenerStateName", PrivateStatic);
            var resolve = typeof(HumanoidClipDriver).GetMethod("ShouldResolveSpearOpenerHit", PrivateStatic);
            Assert.That(stateName, Is.Not.Null, "The opener must explicitly target the existing AttackThrust state.");
            Assert.That(stateName.GetRawConstantValue(), Is.EqualTo("AttackThrust"));
            Assert.That(resolve, Is.Not.Null, "Spear opener impacts need a one-shot normalized-time policy.");

            Assert.That((bool)resolve.Invoke(null, new object[] { false, 0.8f, false }), Is.False,
                "An unrelated legacy AttackThrust must not be claimed as a spear opener hit.");
            Assert.That((bool)resolve.Invoke(null, new object[] { true, 0.49f, false }), Is.False,
                "The opener must not hit before its thrust strike marker.");
            Assert.That((bool)resolve.Invoke(null, new object[] { true, 0.5f, false }), Is.True);
            Assert.That((bool)resolve.Invoke(null, new object[] { true, 0.8f, true }), Is.False,
                "Crossing later frames must not repeat the opener hit.");
        }

        [Test]
        public void SpearFollowup_AuthorizesOnlyOneWeaponComboSwingAtItsSegmentTwoMarker()
        {
            var accept = typeof(HumanoidClipDriver).GetMethod("EvaluateSpearFollowupAcceptance", PrivateStatic);
            var marker = typeof(HumanoidClipDriver).GetMethod("GetSpearFollowupStrikeNormalizedTime", PrivateStatic);
            var resolve = typeof(HumanoidClipDriver).GetMethod("ShouldResolveSpearFollowupHit", PrivateStatic);
            Assert.That(accept, Is.Not.Null, "Only a timely second spear click may authorize the follow-up.");
            Assert.That(marker, Is.Not.Null);
            Assert.That(resolve, Is.Not.Null, "The WeaponCombo follow-up must own one distinct hit marker.");

            Assert.That((bool)accept.Invoke(null, new object[] { false, 0, 1L, 5f, 5.8f, 0.85f }), Is.True,
                "A rapid follow-up is valid even while the driver's opener sequence is still pending consumption.");
            Assert.That((bool)accept.Invoke(null, new object[] { true, 1, 1L, 5f, 5.8f, 0.85f }), Is.False,
                "A pending delta is already the accepted second click and must not authorize a new physical input.");
            var dispatch = typeof(HumanoidClipDriver).GetMethod("ShouldAuthorizeSpearFollowupAtDispatch", PrivateStatic);
            Assert.That(dispatch, Is.Not.Null);
            Assert.That((bool)dispatch.Invoke(null, new object[] { false, 1 }), Is.True,
                "The already-accepted second click must still dispatch after a delay, independently of new-input capacity.");
            Assert.That((bool)accept.Invoke(null, new object[] { true, 1, 2L, 5f, 5.2f, 0.85f }), Is.False,
                "The one available follow-up cannot be reserved twice.");
            Assert.That((bool)accept.Invoke(null, new object[] { true, 1, 0L, 5f, 5.8f, 0.85f }), Is.True);
            Assert.That((bool)accept.Invoke(null, new object[] { true, 1, 0L, 5f, 5.86f, 0.85f }), Is.False);
            Assert.That((bool)accept.Invoke(null, new object[] { true, 2, 0L, 5f, 5.2f, 0.85f }), Is.False,
                "The two-hit spear sequence does not authorize a third attack.");
            float strike = (float)marker.Invoke(null, null);
            Assert.That(strike, Is.EqualTo((1f + 0.32f) / 3f).Within(0.0001f),
                "The follow-up should use the authored WeaponCombo segment-two slash marker.");
            Assert.That((bool)resolve.Invoke(null, new object[] { true, strike - 0.001f, false }), Is.False);
            Assert.That((bool)resolve.Invoke(null, new object[] { true, strike, false }), Is.True);
            Assert.That((bool)resolve.Invoke(null, new object[] { true, strike + 0.1f, true }), Is.False);
            Assert.That((bool)accept.Invoke(null, new object[] { true, 1, 2L, 5f, 5.2f, 0.85f }), Is.False,
                "A third accepted click is rejected once opener and follow-up deltas are reserved.");
        }

        [Test]
        public void SpearDriverOwnership_PreventsLegacyImmediateImpactWithoutChangingOtherWeapons()
        {
            var shouldUse = typeof(PlayerCombat).GetMethod("ShouldUseSpearAttackDriver", PrivateStatic);
            Assert.That(shouldUse, Is.Not.Null,
                "A ready clip driver must own both spear attack animation and its delayed hit check.");
            Assert.That((bool)shouldUse.Invoke(null, new object[] { true, true }), Is.True);
            Assert.That((bool)shouldUse.Invoke(null, new object[] { true, false }), Is.False,
                "Without a ready driver Spear retains its legacy hit fallback.");
            Assert.That((bool)shouldUse.Invoke(null, new object[] { false, true }), Is.False,
                "Fist, Sword, and Bow routing must not be changed by spear ownership.");
        }

        [Test]
        public void SpearAcceptedSecondClick_DoesNotExpireWhileWaitingForDriverDispatch()
        {
            var shouldAuthorize = typeof(HumanoidClipDriver).GetMethod("ShouldAuthorizeSpearFollowupAtDispatch", PrivateStatic);
            Assert.That(shouldAuthorize, Is.Not.Null,
                "Once PlayerCombat accepted a follow-up sequence delta, driver dispatch must not apply the time-window gate a second time.");
            Assert.That((bool)shouldAuthorize.Invoke(null, new object[] { false, 1 }), Is.True);
            Assert.That((bool)shouldAuthorize.Invoke(null, new object[] { true, 1 }), Is.False);
            Assert.That((bool)shouldAuthorize.Invoke(null, new object[] { false, 2 }), Is.False);
        }

        [Test]
        public void SpearExpiredSequence_IsDetectedOnlyAfterClickWindow()
        {
            var expired = typeof(HumanoidClipDriver).GetMethod("IsSpearSequenceExpired", PrivateStatic);
            Assert.That(expired, Is.Not.Null);
            Assert.That((bool)expired.Invoke(null, new object[] { true, 5f, 5.84f, 0.85f }), Is.False);
            Assert.That((bool)expired.Invoke(null, new object[] { true, 5f, 5.85f, 0.85f }), Is.True);
            Assert.That((bool)expired.Invoke(null, new object[] { false, 5f, 6f, 0.85f }), Is.False);
        }

        [Test]
        public void SpearOpenerRestart_RewindsAndUnexpectedStateExitCleansSequence()
        {
            var restartTime = typeof(HumanoidClipDriver).GetMethod("GetSpearOpenerRestartNormalizedTime", PrivateStatic);
            var interrupted = typeof(HumanoidClipDriver).GetMethod("ShouldResetInterruptedSpearSequence", PrivateStatic);
            Assert.That(restartTime, Is.Not.Null,
                "A restarted AttackThrust must explicitly rewind instead of relying on a disabled self-transition.");
            Assert.That((float)restartTime.Invoke(null, null), Is.Zero);
            Assert.That(interrupted, Is.Not.Null);
            Assert.That((bool)interrupted.Invoke(null, new object[] { true, false, false, false, false, false }), Is.False,
                "An accepted opener may be waiting for its Animator state to enter.");
            Assert.That((bool)interrupted.Invoke(null, new object[] { true, false, true, false, true, false }), Is.False,
                "An authorized follow-up remains active while the opener is still in its authored state.");
            Assert.That((bool)interrupted.Invoke(null, new object[] { true, true, true, true, false, true }), Is.False,
                "An active WeaponCombo follow-up must not be cleared as an opener interruption.");
            Assert.That((bool)interrupted.Invoke(null, new object[] { true, true, true, false, false, true }), Is.False,
                "An authorized AttackThrust-to-WeaponCombo handoff remains active.");
            Assert.That((bool)interrupted.Invoke(null, new object[] { true, true, true, false, false, false }), Is.True,
                "An authorized sequence that leaves both AttackThrust and WeaponCombo must be retired.");
        }

        private static MeleeComboStateMachine StartCombo()
        {
            var combo = new MeleeComboStateMachine();
            combo.Start(5f);
            return combo;
        }

        private static float GetPlaybackLimit(int acceptedClicks)
        {
            var method = typeof(HumanoidClipDriver).GetMethod("GetMeleeComboPlaybackLimit", PrivateStatic);
            Assert.That(method, Is.Not.Null);
            return (float)method.Invoke(null, new object[] { acceptedClicks });
        }

        private static float GetStrikeMarker(int segment)
        {
            var method = typeof(HumanoidClipDriver).GetMethod("GetMeleeComboStrikeNormalizedTime", PrivateStatic);
            Assert.That(method, Is.Not.Null);
            return (float)method.Invoke(null, new object[] { segment });
        }

        private static int[] EligibleHitSegments(int acceptedClicks, float normalizedTime)
        {
            var eligible = new System.Collections.Generic.List<int>();
            float playbackLimit = GetPlaybackLimit(acceptedClicks);
            for (int segment = 1; segment <= acceptedClicks; segment++)
                if (GetStrikeMarker(segment) <= playbackLimit && normalizedTime >= GetStrikeMarker(segment))
                    eligible.Add(segment);
            return eligible.ToArray();
        }
    }
}
