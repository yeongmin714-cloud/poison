using System.Reflection;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    public class PlayerCombatParryInputTests
    {
        private GameObject _player;
        private PlayerCombat _combat;

        [SetUp]
        public void SetUp()
        {
            _player = new GameObject("ParryInputRegressionPlayer");
            _combat = _player.AddComponent<PlayerCombat>();
            UITransitionState.PointerOverUI = false;
        }

        [TearDown]
        public void TearDown()
        {
            UITransitionState.PointerOverUI = false;
            if (_player != null) Object.DestroyImmediate(_player);
        }

        [Test]
        public void DedicatedParryRequest_OpensOneWindowWithoutQueueingAnAttack()
        {
            // This reflection test exercises the parry-only request/state transition, not physical Keyboard input.
            float startedAt = Time.unscaledTime;

            Assert.That(RequestParry(), Is.True);

            Assert.That(GetField<bool>("_parryActive"), Is.True);
            Assert.That(GetField<float>("_parryActiveUntil"), Is.EqualTo(startedAt + 0.45f).Within(0.001f));
            Assert.That(GetField<int>("_queuedWeaponAttacks"), Is.Zero);
            Assert.That(GetField<bool>("_queuedBowAction"), Is.False);

            float firstExpiry = GetField<float>("_parryActiveUntil");
            Assert.That(RequestParry(), Is.False, "A repeated parry event must not restart the active window.");
            Assert.That(GetField<float>("_parryActiveUntil"), Is.EqualTo(firstExpiry));
            Assert.That(GetField<int>("_queuedWeaponAttacks"), Is.Zero);
            Assert.That(GetField<bool>("_queuedBowAction"), Is.False);
        }

        [Test]
        public void PointerOverUI_BlocksDedicatedParryRequest()
        {
            UITransitionState.PointerOverUI = true;

            Assert.That(RequestParry(), Is.False);
            Assert.That(GetField<bool>("_parryActive"), Is.False);
            Assert.That(GetField<int>("_queuedWeaponAttacks"), Is.Zero);
            Assert.That(GetField<bool>("_queuedBowAction"), Is.False);
        }

        [Test]
        public void ParryWindowExpiry_EndsDedicatedWindow()
        {
            Assert.That(RequestParry(), Is.True);

            var endWindow = typeof(PlayerCombat).GetMethod("EndParryWindow", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(endWindow, Is.Not.Null);
            endWindow.Invoke(_combat, null);

            Assert.That(GetField<bool>("_parryActive"), Is.False);
            Assert.That(GetField<float>("_parryActiveUntil"), Is.EqualTo(-999f));
        }

        [Test]
        public void TryParry_ConsumesDedicatedWindowOnce()
        {
            Assert.That(RequestParry(), Is.True);

            Assert.That(_combat.TryParry(), Is.True);
            Assert.That(GetField<bool>("_parryActive"), Is.False);
            Assert.That(GetField<float>("_parryActiveUntil"), Is.EqualTo(-999f));
            Assert.That(_combat.TryParry(), Is.False);
            Assert.That(GetField<float>("_parryActiveUntil"), Is.EqualTo(-999f));
        }

        private bool RequestParry()
        {
            var method = typeof(PlayerCombat).GetMethod("TryStartParry", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, "Dedicated parry input should have a separate parry-only handler.");
            return (bool)method.Invoke(_combat, null);
        }

        private T GetField<T>(string name)
        {
            var field = typeof(PlayerCombat).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, $"Expected PlayerCombat.{name}.");
            return (T)field.GetValue(_combat);
        }
    }
}
