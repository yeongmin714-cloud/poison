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
        [Test]
        public void ExplicitTypedHit_IsInterceptedByActiveParry(string weaponType)
        {
            SetField(_combat, "_parryActive", true);
            LogAssert.Expect(UnityEngine.LogType.Log, new System.Text.RegularExpressions.Regex(".*패링 성공.*"));
            LogAssert.Expect(UnityEngine.LogType.Log, new System.Text.RegularExpressions.Regex(".*패링으로.*공격 흡수.*"));

            _health.TakeDamage(25f, Vector3.forward, weaponType);

            Assert.That(_health.CurrentHP, Is.EqualTo(100f), $"{weaponType ?? "<null>"} hit should be fully parried");
            Assert.That(_combat.TryParry(), Is.False, "A successful parry consumes the active window once");
        }

        // ===== [95차] 패링 액션감 강화 — F1 판정 결과 전달 / F4 방향성 스파크 계약 =====

        [Test] // [95차 F1] PERFECT 등급 패링도 흡수 계약 유지 — 판정 결과 전달이 데미지 흡수를 깨지 않는다
        public void PerfectGradeParry_StillAbsorbsTypedDamage()
        {
            SetField(_combat, "_parryActive", true);
            SetField(_combat, "_parryActiveUntil", Time.unscaledTime + 0.45f); // 신선한 창 — 경과 ≈0 → PERFECT

            // [95차 F1 수리] 로그 grade는 enum ToString("Perfect") 타이틀 케이스.
            // 주의: Unity LogAssert는 LogMatch.MessageRegex에서 패턴 문자열만 보관 후 new Regex(pattern)으로
            // 재컴파일하므로 RegexOptions.IgnoreCase가 무시된다(UTF 996f223345ba LogMatch.cs:37).
            // → 인라인 플래그 (?i)를 패턴에 직접 포함(재컴파일 후에도 유지), 등급 경로 검증 계약(\(PERFECT\) 앵커) 유지.
            LogAssert.Expect(UnityEngine.LogType.Log, new System.Text.RegularExpressions.Regex("(?i).*패링 성공\\(PERFECT\\).*"));
            LogAssert.Expect(UnityEngine.LogType.Log, new System.Text.RegularExpressions.Regex(".*패링으로.*공격 흡수.*"));

            _health.TakeDamage(25f, Vector3.forward, "Sword");

            Assert.That(_health.CurrentHP, Is.EqualTo(100f), "PERFECT 판정 경로에서도 데미지는 완전 흡수된다");
            Assert.That(_combat.TryParry(), Is.False, "성공 패링은 창을 1회 소비한다(기존 계약)");
        }

        [Test] // [95차 F4] FX 방향성 스폰 함수 존재 — ShockwaveRingFX.SpawnDirectionalSpark(origin, direction, color[, duration])
        public void DirectionalSparkSpawnFunction_Exists()
        {
            var method = typeof(ShockwaveRingFX).GetMethod(
                "SpawnDirectionalSpark", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "[95차 F4] 방향성 스파크 스폰 함수(ShockwaveRingFX.SpawnDirectionalSpark) 존재");

            var parameters = method.GetParameters();
            Assert.That(parameters.Length, Is.GreaterThanOrEqualTo(3), "최소 시그니처 (origin, direction, color)");
            Assert.That(parameters[0].ParameterType, Is.EqualTo(typeof(Vector3)), "파라미터 1 — 흡수점 origin");
            Assert.That(parameters[1].ParameterType, Is.EqualTo(typeof(Vector3)), "파라미터 2 — 공격자 방향(kbDir 역방향) 축");
            Assert.That(parameters[2].ParameterType, Is.EqualTo(typeof(Color)), "파라미터 3 — 파편 색상");
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, $"Expected field {fieldName}");
            field.SetValue(target, value);
        }
    }
}
