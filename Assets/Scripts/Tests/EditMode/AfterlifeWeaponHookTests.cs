using System.Collections.Generic;
using System.IO;
using System.Linq; // [156차 P4] Find(.First) — CS1061 수리
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [156차 P4] 이계 카드 무기 훅 EditMode 테스트.
    /// 검증: 활성 무기 배율(획득/강화/전환) / CooldownFactor / 패시브×무기 배율 통합 /
    ///   사신 즉사 순수 함수 결정론 / PlayerCombat·AfterlifeEnemy 소스-계약(훅 배선 존재).
    /// Play 경로(실제 스왑/쿨다운 체감)는 별도 승인 게이트.
    /// </summary>
    public sealed class AfterlifeWeaponHookTests
    {
        [SetUp]
        public void SetUp()
        {
            AfterlifeSurvivalSystem.ResetAll();
            AfterlifeCardSystem.ResetAll();
        }

        [TearDown]
        public void TearDown()
        {
            AfterlifeCardSystem.EndRun(); // [156차 P4] 무기 원복 포함 전면 리셋 — 전역 오염 방지
        }

        private static CardDef Find(string id) => AfterlifeCardSystem.Pool.First(c => c.Id == id);

        // ===== 활성 무기 배율 =====

        [Test]
        public void SawDagger_Acquire_Cooldown07_Damage08()
        {
            AfterlifeCardSystem.SelectFromDef(Find("w_saw_dagger"));
            Assert.AreEqual("w_saw_dagger", AfterlifeCardSystem.ActiveWeaponId);
            Assert.AreEqual(0.7f, AfterlifeCardSystem.CooldownFactor, 0.0001f);
            Assert.AreEqual(0.8f, AfterlifeCardSystem.DamageMult, 0.0001f);
        }

        [Test]
        public void GraveSword_Acquire_Damage11_CooldownNeutral()
        {
            AfterlifeCardSystem.SelectFromDef(Find("w_grave_sword"));
            Assert.AreEqual(1.1f, AfterlifeCardSystem.DamageMult, 0.0001f);
            Assert.AreEqual(1.0f, AfterlifeCardSystem.CooldownFactor, 0.0001f);
        }

        [Test]
        public void PierceSpear_Acquire_Damage125_Cooldown115()
        {
            AfterlifeCardSystem.SelectFromDef(Find("w_pierce_spear"));
            Assert.AreEqual(1.25f, AfterlifeCardSystem.DamageMult, 0.0001f);
            Assert.AreEqual(1.15f, AfterlifeCardSystem.CooldownFactor, 0.0001f);
        }

        [Test]
        public void SawDagger_Lv2_Cooldown_595()
        {
            AfterlifeCardSystem.SelectFromDef(Find("w_saw_dagger"));
            AfterlifeCardSystem.SelectFromDef(Find("w_saw_dagger_2"));
            Assert.AreEqual(0.7f * 0.85f, AfterlifeCardSystem.CooldownFactor, 0.0001f);
        }

        [Test]
        public void SawDagger_Lv3_Damage_16()
        {
            AfterlifeCardSystem.SelectFromDef(Find("w_saw_dagger"));
            AfterlifeCardSystem.SelectFromDef(Find("w_saw_dagger_3"));
            Assert.AreEqual(0.8f * 2.0f, AfterlifeCardSystem.DamageMult, 0.0001f);
        }

        [Test]
        public void GraveSword_Lv2_Damage_143()
        {
            AfterlifeCardSystem.SelectFromDef(Find("w_grave_sword"));
            AfterlifeCardSystem.SelectFromDef(Find("w_grave_sword_2"));
            Assert.AreEqual(1.1f * 1.3f, AfterlifeCardSystem.DamageMult, 0.0001f);
        }

        [Test]
        public void WeaponSwitch_RecalcsToLatestAcquired()
        {
            AfterlifeCardSystem.SelectFromDef(Find("w_saw_dagger"));
            AfterlifeCardSystem.SelectFromDef(Find("w_grave_sword"));
            Assert.AreEqual("w_grave_sword", AfterlifeCardSystem.ActiveWeaponId);
            Assert.AreEqual(1.1f, AfterlifeCardSystem.DamageMult, 0.0001f);
            Assert.AreEqual(1.0f, AfterlifeCardSystem.CooldownFactor, 0.0001f);
        }

        [Test]
        public void PassiveStack_MultipliesWithWeaponMult()
        {
            AfterlifeCardSystem.SelectFromDef(Find("w_saw_dagger"));
            AfterlifeCardSystem.SelectFromDef(Find("p_emperor_seal"));
            // 무기 0.8 × 패시브 1.10 = 0.88
            Assert.AreEqual(0.8f * 1.1f, AfterlifeCardSystem.DamageMult, 0.0001f);
        }

        // ===== 사신 즉사 순수 함수 =====

        [Test]
        public void DeathScythe_Roll_Inactive_AlwaysFalse()
        {
            var rng = new System.Random(7);
            for (int i = 0; i < 20; i++)
                Assert.IsFalse(AfterlifeCardSystem.RollDeathScytheInstakill(rng));
        }

        [Test]
        public void DeathScythe_Roll_DeterministicPerSeed()
        {
            AfterlifeCardSystem.SelectFromDef(Find("p_death_scythe"));
            var a = new System.Random(42);
            var b = new System.Random(42);
            for (int i = 0; i < 50; i++)
                Assert.AreEqual(AfterlifeCardSystem.RollDeathScytheInstakill(a),
                                AfterlifeCardSystem.RollDeathScytheInstakill(b));
        }

        [Test]
        public void DeathScythe_Acquire_SetsActiveAndPercent()
        {
            AfterlifeCardSystem.SelectFromDef(Find("p_death_scythe"));
            Assert.IsTrue(AfterlifeCardSystem.DeathScytheActive);
            Assert.AreEqual(5, AfterlifeCardSystem.DeathScytheBasePercent);
            Assert.AreEqual(5, AfterlifeCardSystem.DeathScytheEliteBonusPercent);
        }

        // ===== EndRun 리셋 =====

        [Test]
        public void EndRun_ResetsWeaponMultipliersAndActive()
        {
            AfterlifeCardSystem.SelectFromDef(Find("w_saw_dagger"));
            AfterlifeCardSystem.SelectFromDef(Find("p_emperor_seal"));
            AfterlifeCardSystem.EndRun();
            Assert.AreEqual(1f, AfterlifeCardSystem.DamageMult, 0.0001f);
            Assert.AreEqual(1f, AfterlifeCardSystem.CooldownFactor, 0.0001f);
            Assert.IsNull(AfterlifeCardSystem.ActiveWeaponId);
        }

        // ===== 소스-계약 (훅 배선 존재 — 문자열 토큰만 검색) =====

        [Test]
        public void SourceContract_PlayerCombat_CooldownFactorWired()
        {
            string src = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/Systems/PlayerCombat.cs"));
            Assert.That(src, Does.Contain("AfterlifeCardSystem.CooldownFactor"));
            Assert.That(src, Does.Contain("public WeaponData CurrentWeapon"));
        }

        [Test]
        public void SourceContract_AfterlifeEnemy_DamageMultWired()
        {
            string src = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/Systems/AfterlifeEnemy.cs"));
            Assert.That(src, Does.Contain("AfterlifeCardSystem.DamageMult"));
            Assert.That(src, Does.Contain("AfterlifeCardSystem.DeathScytheActive"));
        }
    }
}
