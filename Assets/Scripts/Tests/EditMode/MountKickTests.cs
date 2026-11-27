using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 86-P3] 말 전투(발길질) 계약 — IsMounted 정적 게이트 + 쿨다운/사거리 결정론 +
    /// Rear 발화와 로코모션 복귀(Rear 상태 출구 전이 없음 보정) + AttackSystem/PlayerCombat 승마 중 억제.
    /// </summary>
    public class MountKickTests
    {
        static string Read(string p) => System.IO.File.ReadAllText(p);

        [Test]
        public void IsMounted_StaticGate_Exists()
        {
            var prop = typeof(HorseMountSystem).GetProperty("IsMounted",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(prop, Is.Not.Null, "[86-P3] IsMounted 정적 게이트 존재");
            Assert.That(prop.CanRead, Is.True, "[86-P3] IsMounted 읽기 가능");
            Assert.That(prop.GetSetMethod(nonPublic: false), Is.Null,
                "[86-P3] public 세터 부재(Mount/Dismount 전용 — private set)");
        }

        [Test]
        public void KickContract_DeterministicConstants_AndRearRecovery()
        {
            string src = Read("Assets/Scripts/Systems/HorseMountSystem.cs");
            Assert.That(src, Does.Contain("[무번호 86-P3]"), "[86-P3] 마커 주석");
            Assert.That(src, Does.Contain("TriggerKick"), "[86-P3] 발길질 진입점");
            Assert.That(src, Does.Contain("OverlapSphere"), "[86-P3] 전방 타격 판정");
            Assert.That(src, Does.Contain("Play(\"Rear\")"), "[86-P3] 앞발 들고 서기 발화");
            Assert.That(src, Does.Contain("CrossFade(\"Idle\""), "[86-P3] Rear 홀드 복귀(출구 전이 없음 보정)");
            Assert.That(src, Does.Contain("TakeDamage("), "[86-P3] IDamageable 피해 적용");
            Assert.That(src, Does.Contain("KickCooldown = 1.0f"), "[86-P3] 쿨다운 결정론 상수");
            Assert.That(src, Does.Contain("KickRadius = 3f"), "[86-P3] 사거리 결정론 상수");
        }

        [Test]
        public void AttackSystem_GatedWhileMounted()
        {
            string src = Read("Assets/Scripts/Systems/AttackSystem.cs");
            Assert.That(src, Does.Contain("[무번호 86-P3]"), "[86-P3] AttackSystem 게이트 마커");
            Assert.That(src, Does.Contain("HorseMountSystem.IsMounted"), "[86-P3] 승마 중 검격 억제");
        }

        [Test]
        public void PlayerCombat_GatedWhileMounted()
        {
            string src = Read("Assets/Scripts/Systems/PlayerCombat.cs");
            Assert.That(src, Does.Contain("[무번호 86-P3]"), "[86-P3] PlayerCombat 게이트 마커");
            Assert.That(src, Does.Contain("HorseMountSystem.IsMounted"), "[86-P3] 승마 중 무기 공격 억제(Test_10 주 경로)");
        }
    }
}
