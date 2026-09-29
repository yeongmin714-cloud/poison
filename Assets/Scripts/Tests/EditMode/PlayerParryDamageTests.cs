using System.Reflection;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectName.Tests.EditMode
{
    public class PlayerParryDamageTests
    {
        private GameObject _player;
        private PlayerCombat _combat;
        private PlayerHealth _health;

        [SetUp]
        public void SetUp()
        {
            _player = new GameObject("ParryDamageTestPlayer");
            _combat = _player.AddComponent<PlayerCombat>();
            _health = _player.AddComponent<PlayerHealth>();
            SetField(_health, "_currentHP", 100f);
            SetField(_health, "_lastDamageTime", float.NegativeInfinity);
        }

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.DestroyImmediate(_player);
        }

        [TestCase("melee")]
        [TestCase("Fist")]
        [TestCase("Sword")]
        [TestCase("Spear")]
        [TestCase("Bow")]
        [TestCase("guard")]
        [TestCase("projectile")]
        [TestCase("")]
        [TestCase(null)]
        public void ExplicitTypedHit_IsInterceptedByActiveParry(string weaponType)
        {
            SetField(_combat, "_parryActive", true);
            LogAssert.Expect(UnityEngine.LogType.Log, new System.Text.RegularExpressions.Regex(".*패링 성공.*"));
            LogAssert.Expect(UnityEngine.LogType.Log, new System.Text.RegularExpressions.Regex(".*패링으로.*공격 흡수.*"));

            _health.TakeDamage(25f, Vector3.forward, weaponType);

            Assert.That(_health.CurrentHP, Is.EqualTo(100f), $"{weaponType ?? "<null>"} hit should be fully parried");
            Assert.That(_combat.TryParry(), Is.False, "A successful parry consumes the active window once");
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, $"Expected field {fieldName}");
            field.SetValue(target, value);
        }
    }
}
