using System.Reflection;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    public class PlayerAttackSequenceTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;

        [Test]
        public void MultipleAcceptedAttacksAtTheSameTime_AdvanceSequenceForEveryAttack()
        {
            var go = new GameObject("AttackSequencePlayer");
            try
            {
                var combat = go.AddComponent<PlayerCombat>();
                var weapon = new WeaponData("ZeroCooldown", 1f, 0f, 1f, WeaponType.Bow);
                weapon.attackSpeed = 0f;
                combat.SetWeapon(weapon);
                var tryAttack = typeof(PlayerCombat).GetMethod("TryAttack", PrivateInstance);

                Assert.That((bool)tryAttack.Invoke(combat, null), Is.True);
                float acceptedTime = combat.LastAttackTime;
                Assert.That((bool)tryAttack.Invoke(combat, null), Is.True);
                Assert.That((bool)tryAttack.Invoke(combat, null), Is.True);

                Assert.That(combat.LastAttackTime, Is.EqualTo(acceptedTime),
                    "LastAttackTime remains a time value and can be identical for same-frame inputs.");
                Assert.That(combat.AcceptedAttackSequence, Is.EqualTo(3),
                    "Every accepted attack must have a distinct monotonic sequence even when timestamps match.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void RejectedAttack_DoesNotAdvanceSequenceOrChangeLastAttackTime()
        {
            var go = new GameObject("RejectedAttackPlayer");
            try
            {
                var combat = go.AddComponent<PlayerCombat>();
                combat.SetWeapon(new WeaponData("CooldownWeapon", 1f, 10f, 1f, WeaponType.Bow));
                var tryAttack = typeof(PlayerCombat).GetMethod("TryAttack", PrivateInstance);

                Assert.That((bool)tryAttack.Invoke(combat, null), Is.True);
                float acceptedTime = combat.LastAttackTime;
                Assert.That((bool)tryAttack.Invoke(combat, null), Is.False);

                Assert.That(combat.AcceptedAttackSequence, Is.EqualTo(1));
                Assert.That(combat.LastAttackTime, Is.EqualTo(acceptedTime));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void DriverConsumesEveryPendingSequenceDelta_AndDoesNotConsumeItTwice()
        {
            var consumeDelta = typeof(HumanoidClipDriver).GetMethod("ConsumeAttackSequenceDelta", PrivateStatic);
            Assert.That(consumeDelta, Is.Not.Null,
                "The driver should turn each unseen accepted sequence value into an exact pending delta.");
            object[] args = { 3L, 0L };

            Assert.That((long)consumeDelta.Invoke(null, args), Is.EqualTo(3L));
            Assert.That((long)args[1], Is.EqualTo(3L));
            args[0] = 3L;
            Assert.That((long)consumeDelta.Invoke(null, args), Is.Zero,
                "Polling the same sequence on the next frame must not replay prior attacks.");
        }

        [TestCase(1, 0, true)]
        [TestCase(2, 0, true)]
        [TestCase(1, 1, true)]
        [TestCase(1, 2, false)]
        [TestCase(2, 1, false)]
        [TestCase(3, 0, false)]
        public void ComboCapacity_IncludesUnconsumedAcceptedSequenceDeltas(
            int currentClicks, int pendingInputs, bool expectedCapacity)
        {
            var hasCapacity = typeof(HumanoidClipDriver).GetMethod("HasComboFollowupCapacity", PrivateStatic);
            Assert.That(hasCapacity, Is.Not.Null);
            Assert.That((bool)hasCapacity.Invoke(null, new object[] { currentClicks, pendingInputs }),
                Is.EqualTo(expectedCapacity));
        }

        [TestCase(true, 0, 0, true, false)]
        [TestCase(true, 0, 1, true, true)]
        [TestCase(true, 0, 2, true, true)]
        [TestCase(true, 0, 3, true, false)]
        [TestCase(true, 1, 0, true, true)]
        [TestCase(true, 1, 1, true, true)]
        [TestCase(true, 2, 0, true, true)]
        [TestCase(true, 2, 1, true, false)]
        [TestCase(true, 0, 1, false, false)]
        [TestCase(false, 0, 1, true, false)]
        public void BufferedFollowupGate_AllowsPendingOpenerButNeverExceedsThree(
            bool animatorReady, int activeClicks, long pendingClicks, bool windowOpen, bool expected)
        {
            var canAccept = typeof(HumanoidClipDriver).GetMethod("CanAcceptBufferedFollowup", PrivateStatic);
            Assert.That(canAccept, Is.Not.Null,
                "The driver gate must accept a rapid follow-up while the opener sequence is still unconsumed.");

            Assert.That((bool)canAccept.Invoke(null,
                new object[] { animatorReady, activeClicks, pendingClicks, windowOpen }), Is.EqualTo(expected));
        }

        [Test]
        public void DelayedMeleeDriverDiscovery_ResolvesNewDriverAndUsesReadinessForOwnership()
        {
            var resolveDriver = typeof(PlayerCombat).GetMethod("ResolveMeleeComboDriver", PrivateStatic);
            var shouldUseCombo = typeof(PlayerCombat).GetMethod("ShouldUseMeleeCombo", PrivateStatic);
            Assert.That(resolveDriver, Is.Not.Null,
                "A null/stale cached driver must be replaced with a driver discovered at the melee routing point.");
            Assert.That(shouldUseCombo, Is.Not.Null,
                "Melee combo ownership requires a ready driver, not just an attached component.");

            var go = new GameObject("DelayedMeleeDriver");
            try
            {
                var discovered = go.AddComponent<HumanoidClipDriver>();
                Assert.That(resolveDriver.Invoke(null, new object[] { null, discovered }), Is.SameAs(discovered));
                Assert.That(resolveDriver.Invoke(null, new object[] { discovered, null }), Is.SameAs(discovered),
                    "A live cached driver should remain selected when no replacement is found.");
                Assert.That((bool)shouldUseCombo.Invoke(null, new object[] { true, true }), Is.True);
                Assert.That((bool)shouldUseCombo.Invoke(null, new object[] { true, false }), Is.False,
                    "An attached but unready driver must leave melee with the legacy fallback.");
                Assert.That((bool)shouldUseCombo.Invoke(null, new object[] { false, true }), Is.False,
                    "Bow/Spear/non-melee routes must not be claimed by the melee combo.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void DelayedPlayerDriverInitialization_ConsumesSequenceAlreadyHandledByFallback()
        {
            var initializeBaseline = typeof(HumanoidClipDriver).GetMethod(
                "GetInitialConsumedAttackSequence", PrivateStatic);
            var consumeDelta = typeof(HumanoidClipDriver).GetMethod("ConsumeAttackSequenceDelta", PrivateStatic);
            Assert.That(initializeBaseline, Is.Not.Null,
                "A newly attached Player driver must baseline at the already accepted combat sequence.");
            Assert.That(consumeDelta, Is.Not.Null);

            object[] baselineArgs = { 4L };
            long baseline = (long)initializeBaseline.Invoke(null, baselineArgs);
            object[] deltaArgs = { 4L, baseline };
            Assert.That((long)consumeDelta.Invoke(null, deltaArgs), Is.Zero,
                "Fallback clicks accepted before Start must not be replayed as combo clicks.");
            deltaArgs[0] = 5L;
            Assert.That((long)consumeDelta.Invoke(null, deltaArgs), Is.EqualTo(1L),
                "A later accepted click must still be delivered to the newly initialized driver.");
        }

        [Test]
        public void SameTimeFollowups_UnlockExactlyTheThreeComboSegments()
        {
            var combo = new MeleeComboStateMachine();
            combo.Start(5f);

            Assert.That(combo.TryAcceptFollowup(5f, 0.6f), Is.True);
            Assert.That(combo.TryAcceptFollowup(5f, 0.6f), Is.True);
            Assert.That(combo.TryAcceptFollowup(5f, 0.6f), Is.False,
                "The combo accepts no more than its three swing segments.");
            Assert.That(combo.AcceptedClicks, Is.EqualTo(3));
        }
    }
}
