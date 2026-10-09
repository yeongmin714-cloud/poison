using System.Reflection;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    public class MeleeClickOwnershipTests
    {
        private GameObject _player;

        [SetUp]
        public void SetUp()
        {
            _player = new GameObject("MeleeClickOwnershipTestPlayer");
            _player.tag = "Player";
            _player.AddComponent<PlayerCombat>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.DestroyImmediate(_player);
        }

        [Test]
        public void TerritoryTestSetup_DoesNotAttachSecondPlayerClickConsumer()
        {
            var setupObject = CreateInactiveSetupObject("TestTerritoryCombatSetupTest");
            try
            {
                var setup = setupObject.GetComponent<TestTerritoryCombatSetup>();
                InvokeAttachAttackSystem(setup);

                Assert.That(_player.GetComponent<PlayerCombat>(), Is.Not.Null);
                Assert.That(_player.GetComponent<AttackSystem>(), Is.Null,
                    "PlayerCombat is the sole player melee click consumer; Test_10 must not attach AttackSystem to that player.");
            }
            finally
            {
                Object.DestroyImmediate(setupObject);
            }
        }

        [Test]
        public void TerritoryTestSetup_KeepsAttackSystemForPlayerWithoutPlayerCombat()
        {
            var playerCombat = _player.GetComponent<PlayerCombat>();
            Object.DestroyImmediate(playerCombat);
            var setupObject = CreateInactiveSetupObject("TestTerritoryCombatSetupLegacyPlayerTest");
            try
            {
                var setup = setupObject.GetComponent<TestTerritoryCombatSetup>();
                InvokeAttachAttackSystem(setup);

                Assert.That(_player.GetComponent<AttackSystem>(), Is.Not.Null,
                    "AttackSystem remains available for the legacy direct-damage path when PlayerCombat is absent.");
            }
            finally
            {
                Object.DestroyImmediate(setupObject);
            }
        }

        [Test]
        public void AttackSystem_RemainsAvailableForNonPlayerConsumers()
        {
            var npc = new GameObject("NonPlayerAttackSystemTest");
            try
            {
                Assert.That(npc.AddComponent<AttackSystem>(), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(npc);
            }
        }

        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(3, 3)]
        public void EachAcceptedAttack_IncrementsStreakUpToExpectedCount(int clickCount, int expectedStreak)
        {
            var combat = _player.GetComponent<PlayerCombat>();
            // Bow returns before the coroutine path, which cannot run in EditMode. A test-only fast
            // weapon lets each accepted call satisfy cooldown while keeping its previous timestamp in the streak window.
            combat.SetWeapon(new WeaponData("Test Bow", 8f, 0.01f, 10f, WeaponType.Bow));
            var tryAttack = typeof(PlayerCombat).GetMethod("TryAttack", BindingFlags.NonPublic | BindingFlags.Instance);
            var streakField = typeof(PlayerCombat).GetField("_attackStreak", BindingFlags.NonPublic | BindingFlags.Instance);
            var lastAttackField = typeof(PlayerCombat).GetField("_lastAttackTime", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(tryAttack, Is.Not.Null);
            Assert.That(streakField, Is.Not.Null);
            Assert.That(lastAttackField, Is.Not.Null);

            // Exercise accepted-call streak accounting directly; this does not simulate Mouse0, arrows,
            // Animator clips, or live input. Seed once outside the streak window; before later calls,
            // set the prior timestamp to 0.1s ago to satisfy cooldown and stay inside the 0.6s streak window.
            lastAttackField.SetValue(combat, Time.time - 10f);
            for (int click = 1; click <= clickCount; click++)
            {
                if (click > 1)
                    lastAttackField.SetValue(combat, Time.time - 0.1f);

                Assert.That(tryAttack.Invoke(combat, null), Is.EqualTo(true),
                    $"Attack call {click} should be accepted when its cooldown is ready.");
                Assert.That(streakField.GetValue(combat), Is.EqualTo(click),
                    $"Accepted attack call {click} should increment the streak once.");
            }

            Assert.That(streakField.GetValue(combat), Is.EqualTo(expectedStreak));
        }

        private static GameObject CreateInactiveSetupObject(string name)
        {
            var setupObject = new GameObject(name);
            setupObject.SetActive(false);
            setupObject.AddComponent<TestTerritoryCombatSetup>();
            return setupObject;
        }

        private static void InvokeAttachAttackSystem(TestTerritoryCombatSetup setup)
        {
            var attachMethod = typeof(TestTerritoryCombatSetup)
                .GetMethod("AttachAttackSystem", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(attachMethod, Is.Not.Null, "Expected TestTerritoryCombatSetup.AttachAttackSystem.");
            attachMethod.Invoke(setup, null);
        }
    }
}
