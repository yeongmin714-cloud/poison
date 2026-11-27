using System.Reflection;
using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    public class HumanoidClipDriverHitReactionTests
    {
        private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

        [TestCase("AttackThrust")]
        [TestCase("WeaponCombo")]
        [TestCase("Attack")]
        [TestCase("AttackBase")]
        public void CommittedMeleeStates_DeferHitLight(string stateName)
        {
            bool isMelee = IsMeleeAttackState(stateName);
            Assert.That(isMelee, Is.True, $"{stateName} must be protected from HitLight interruption.");
            Assert.That(ShouldDeferHitLight(isMelee, false, false), Is.True,
                "HitLight is deferred while a committed melee state is current.");
        }

        [TestCase("Idle")]
        [TestCase("HitLight")]
        [TestCase("ArcheryShot")]
        public void NonMeleeStates_DoNotDeferHitLight(string stateName)
        {
            bool isMelee = IsMeleeAttackState(stateName);
            Assert.That(isMelee, Is.False, $"{stateName} is intentionally outside melee protection.");
            Assert.That(ShouldDeferHitLight(isMelee, false, false), Is.False);
        }

        [Test]
        public void IdleWithPendingAcceptedMeleeOpener_DefersHitLightBeforeAnimatorTransitions()
        {
            bool pendingHitLight = true;
            bool openerCommitted = ShouldDeferHitLightForMeleeIntent(
                currentStateIsMelee: false, isTransitioning: false, nextStateIsMelee: false,
                comboStage: 0, spearAcceptedClicks: 0, pendingAcceptedMeleeRoute: true);

            Assert.That(ConsumePendingHitReaction(ref pendingHitLight, openerCommitted, discardForPriorityState: false), Is.False,
                "The accepted melee opener must prevent the pending HitLight from flushing while Animator is still Idle.");
            Assert.That(pendingHitLight, Is.True, "Keep the HP reaction queued through the opener transition.");
        }

        [TestCase(1, 0, false)]
        [TestCase(0, 1, false)]
        [TestCase(0, 0, true)]
        public void LogicalMeleeCommitment_DefersHitLight(int comboStage, int spearAcceptedClicks, bool pendingAcceptedMeleeRoute)
        {
            Assert.That(ShouldDeferHitLightForMeleeIntent(
                currentStateIsMelee: false, isTransitioning: false, nextStateIsMelee: false,
                comboStage: comboStage, spearAcceptedClicks: spearAcceptedClicks,
                pendingAcceptedMeleeRoute: pendingAcceptedMeleeRoute), Is.True);
        }

        [Test]
        public void PendingBowRoute_DoesNotDeferHitLight()
        {
            bool pendingHitLight = true;
            Assert.That(IsMeleeWeaponType(ProjectName.Core.WeaponType.Bow), Is.False,
                "Bow is not an accepted melee route.");
            Assert.That(ConsumePendingHitReaction(ref pendingHitLight,
                ShouldDeferHitLightForMeleeIntent(false, false, false, 0, 0, false), discardForPriorityState: false), Is.True,
                "A pending Bow/ArcheryShot route remains unprotected and flushes HitLight.");
            Assert.That(pendingHitLight, Is.False);
        }

        [TestCase(ProjectName.Core.WeaponType.Fist, true)]
        [TestCase(ProjectName.Core.WeaponType.Sword, true)]
        [TestCase(ProjectName.Core.WeaponType.Spear, true)]
        [TestCase(ProjectName.Core.WeaponType.Bow, false)]
        public void AcceptedRouteClassification_IsMeleeOnly(ProjectName.Core.WeaponType weaponType, bool expected)
        {
            Assert.That(IsMeleeWeaponType(weaponType), Is.EqualTo(expected));
        }

        [Test]
        public void AttackTransition_ConsidersCurrentAndNextStateWithoutProtectingUnrelatedTransitions()
        {
            Assert.That(ShouldDeferHitLight(true, true, false), Is.True,
                "The current melee state remains protected while transitioning out.");
            Assert.That(ShouldDeferHitLight(false, true, true), Is.True,
                "A transition into a melee state is committed and protected too.");
            Assert.That(ShouldDeferHitLight(false, false, true), Is.False,
                "A next-state name alone is ignored when Animator is not transitioning.");
        }

        [Test]
        public void PendingHitReaction_NormalMeleeCompletionEmitsOnce()
        {
            bool pending = true;

            Assert.That(ConsumePendingHitReaction(ref pending, committedMeleeAttack: true,
                discardForPriorityState: false), Is.False);
            Assert.That(pending, Is.True, "A hit during a melee attack must remain queued.");

            Assert.That(ConsumePendingHitReaction(ref pending, committedMeleeAttack: false,
                discardForPriorityState: false), Is.True,
                "Normal melee completion emits the deferred reaction.");
            Assert.That(pending, Is.False);
            Assert.That(ConsumePendingHitReaction(ref pending, committedMeleeAttack: false,
                discardForPriorityState: false), Is.False,
                "The same queued hit must not emit repeatedly.");
        }

        [TestCase("Roll")]
        [TestCase("Parry")]
        [TestCase("Stun")]
        [TestCase("Death")]
        [TestCase("Knockdown")]
        public void PendingHitReaction_ActivePriorityStateDiscardsWithoutOverridingIt(string stateName)
        {
            bool pending = true;
            int stateHash = Animator.StringToHash(stateName);
            Assert.That(IsHigherPriorityHitLightState(stateHash), Is.True);
            bool discard = ShouldDiscardPendingHitLight(stateHash, isTransitioning: false, nextStateShortNameHash: 0);

            Assert.That(ConsumePendingHitReaction(ref pending, committedMeleeAttack: false,
                discardForPriorityState: discard), Is.False,
                $"A deferred HitLight must not fire over {stateName}.");
            Assert.That(pending, Is.False, "Discard interrupted reactions instead of replaying them later.");
        }

        [TestCase("Roll")]
        [TestCase("Parry")]
        [TestCase("Stun")]
        [TestCase("Death")]
        [TestCase("Knockdown")]
        public void PendingHitReaction_EnteringPriorityStateDiscardsWithoutOverridingIt(string stateName)
        {
            bool pending = true;
            int nextStateHash = Animator.StringToHash(stateName);
            Assert.That(IsHigherPriorityHitLightState(nextStateHash), Is.True);
            bool discard = ShouldDiscardPendingHitLight(currentStateShortNameHash: 0,
                isTransitioning: true, nextStateShortNameHash: nextStateHash);

            Assert.That(ConsumePendingHitReaction(ref pending, committedMeleeAttack: false,
                discardForPriorityState: discard), Is.False,
                $"A deferred HitLight must be discarded while entering {stateName}.");
            Assert.That(pending, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PendingHitLight_IsDiscardedWhileActiveOrEntering(bool entering)
        {
            bool pending = true;
            int hitLightHash = Animator.StringToHash("HitLight");
            Assert.That(IsHitLightState(hitLightHash), Is.True);
            bool discard = entering
                ? ShouldDiscardPendingHitLight(currentStateShortNameHash: 0,
                    isTransitioning: true, nextStateShortNameHash: hitLightHash)
                : ShouldDiscardPendingHitLight(currentStateShortNameHash: hitLightHash,
                    isTransitioning: false, nextStateShortNameHash: 0);

            Assert.That(discard, Is.True);
            Assert.That(ConsumePendingHitReaction(ref pending, committedMeleeAttack: false,
                discardForPriorityState: discard), Is.False);
            Assert.That(pending, Is.False);
        }

        [TestCase("Roll")]
        [TestCase("Parry")]
        [TestCase("Stun")]
        [TestCase("Death")]
        public void TransitioningIntoPriorityState_DiscardsPendingHitLight(string stateName)
        {
            bool pending = true;
            bool discard = ShouldDiscardPendingHitLight(currentStateShortNameHash: 0,
                isTransitioning: true, nextStateShortNameHash: Animator.StringToHash(stateName));

            Assert.That(ConsumePendingHitReaction(ref pending, committedMeleeAttack: false,
                discardForPriorityState: discard), Is.False);
            Assert.That(pending, Is.False);
        }

        [Test]
        public void MeleeCompletionPendingHitLightAndPriorityState_DiscardsRatherThanEmits()
        {
            bool pending = true;
            Assert.That(ConsumePendingHitReaction(ref pending, committedMeleeAttack: false,
                discardForPriorityState: true), Is.False);
            Assert.That(pending, Is.False,
                "If melee cleanup exposes a priority interrupt, discard before the deferred AnyState trigger can be set.");
        }

        [Test]
        public void PlayerTriggerHitLight_QueuesInsteadOfBypassingMeleeDeferral()
        {
            var gameObject = new GameObject("HitReactionTriggerTest");
            try
            {
                var animator = gameObject.AddComponent<Animator>();
                var driver = gameObject.AddComponent<HumanoidClipDriver>();
                var animField = typeof(HumanoidClipDriver).GetField("_anim",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var pendingField = typeof(HumanoidClipDriver).GetField("_pendingPlayerHitLight",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var modeField = typeof(HumanoidClipDriver).GetField("mode",
                    BindingFlags.Instance | BindingFlags.Public);
                Assert.That(animField, Is.Not.Null);
                Assert.That(pendingField, Is.Not.Null);
                Assert.That(modeField, Is.Not.Null);
                animField.SetValue(driver, animator);
                modeField.SetValue(driver, HumanoidClipDriver.DriveMode.Player);
                pendingField.SetValue(driver, false);

                driver.TriggerHitLight();

                Assert.That(pendingField.GetValue(driver), Is.True,
                    "Player hit-reaction entry points must feed the same deferred queue as HP polling.");
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void TriggerDeath_ClearsQueuedHitLight()
        {
            var gameObject = new GameObject("HitReactionDeathTest");
            try
            {
                var animator = gameObject.AddComponent<Animator>();
                var driver = gameObject.AddComponent<HumanoidClipDriver>();
                var animField = typeof(HumanoidClipDriver).GetField("_anim",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var pendingField = typeof(HumanoidClipDriver).GetField("_pendingPlayerHitLight",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(animField, Is.Not.Null);
                Assert.That(pendingField, Is.Not.Null);
                animField.SetValue(driver, animator);
                pendingField.SetValue(driver, true);

                typeof(HumanoidClipDriver).GetMethod("TriggerDeath",
                    BindingFlags.Instance | BindingFlags.Public).Invoke(driver, null);

                Assert.That(pendingField.GetValue(driver), Is.False,
                    "Death clears a reaction queued during melee instead of allowing it to flush later.");
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        private static bool IsMeleeAttackState(string stateName)
        {
            var method = typeof(HumanoidClipDriver).GetMethod("IsMeleeAttackStateName", PrivateStatic);
            Assert.That(method, Is.Not.Null, "HumanoidClipDriver must expose the tested pure melee-state policy.");
            return (bool)method.Invoke(null, new object[] { stateName });
        }

        private static bool ShouldDeferHitLightForMeleeIntent(bool currentStateIsMelee,
            bool isTransitioning, bool nextStateIsMelee, int comboStage,
            int spearAcceptedClicks, bool pendingAcceptedMeleeRoute)
        {
            var method = typeof(HumanoidClipDriver).GetMethod("ShouldDeferHitLightForMeleeIntent", PrivateStatic);
            Assert.That(method, Is.Not.Null, "HitLight policy must include committed and accepted melee intent.");
            return (bool)method.Invoke(null, new object[] { currentStateIsMelee, isTransitioning,
                nextStateIsMelee, comboStage, spearAcceptedClicks, pendingAcceptedMeleeRoute });
        }

        private static bool IsMeleeWeaponType(ProjectName.Core.WeaponType weaponType)
        {
            var method = typeof(HumanoidClipDriver).GetMethod("IsMeleeWeaponType", PrivateStatic);
            Assert.That(method, Is.Not.Null, "Pending accepted routes must be explicitly classified as melee-only.");
            return (bool)method.Invoke(null, new object[] { weaponType });
        }

        private static bool ShouldDeferHitLight(bool currentMelee, bool isTransitioning, bool nextMelee)
        {
            var method = typeof(HumanoidClipDriver).GetMethod("ShouldDeferHitLight", PrivateStatic);
            Assert.That(method, Is.Not.Null, "Current/next state transition policy must be independently testable.");
            return (bool)method.Invoke(null, new object[] { currentMelee, isTransitioning, nextMelee });
        }

        private static bool ConsumePendingHitReaction(ref bool pending, bool committedMeleeAttack,
            bool discardForPriorityState)
        {
            var method = typeof(HumanoidClipDriver).GetMethod("ConsumePendingHitReaction", PrivateStatic);
            Assert.That(method, Is.Not.Null, "Pending hit reaction must have a pure one-shot/interruption policy.");
            var args = new object[] { pending, committedMeleeAttack, discardForPriorityState };
            bool shouldEmit = (bool)method.Invoke(null, args);
            pending = (bool)args[0];
            return shouldEmit;
        }

        private static bool IsHigherPriorityHitLightState(int stateShortNameHash)
        {
            var method = typeof(HumanoidClipDriver).GetMethod("IsHigherPriorityHitLightState", PrivateStatic);
            Assert.That(method, Is.Not.Null, "Priority interrupt state classification must be pure and testable.");
            return (bool)method.Invoke(null, new object[] { stateShortNameHash });
        }

        private static bool IsHitLightState(int stateShortNameHash)
        {
            var method = typeof(HumanoidClipDriver).GetMethod("IsHitLightState", PrivateStatic);
            Assert.That(method, Is.Not.Null, "HitLight current/next state policy must be pure and testable.");
            return (bool)method.Invoke(null, new object[] { stateShortNameHash });
        }

        private static bool ShouldDiscardPendingHitLight(int currentStateShortNameHash,
            bool isTransitioning, int nextStateShortNameHash)
        {
            var method = typeof(HumanoidClipDriver).GetMethod("ShouldDiscardPendingHitLight", PrivateStatic);
            Assert.That(method, Is.Not.Null, "Pending HitLight discard policy must cover Animator current/next states.");
            return (bool)method.Invoke(null, new object[] { currentStateShortNameHash, isTransitioning,
                nextStateShortNameHash });
        }
    }
}
