using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;
using ProjectName.Core;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C9-12 병사 약물 중독 시스템 테스트
    /// </summary>
    public class GuardAddictionTests
    {
        // ===================== 기본 상수 확인 =====================

        [Test]
        public void AddictionSystem_Stages_AreDefined()
        {
            Assert.AreEqual(20f, GuardAddictionSystem.STAGE_0_MAX);
            Assert.AreEqual(40f, GuardAddictionSystem.STAGE_1_MAX);
            Assert.AreEqual(60f, GuardAddictionSystem.STAGE_2_MAX);
            Assert.AreEqual(80f, GuardAddictionSystem.STAGE_3_MAX);
            Assert.AreEqual(100f, GuardAddictionSystem.STAGE_4_MAX);
        }

        // ===================== GetAddictionStage 테스트 =====================

        [Test]
        public void GetAddictionStage_ReturnsCorrectStage()
        {
            Assert.AreEqual(0, GuardAddictionSystem.GetAddictionStage(0));
            Assert.AreEqual(0, GuardAddictionSystem.GetAddictionStage(10));
            Assert.AreEqual(1, GuardAddictionSystem.GetAddictionStage(25));
            Assert.AreEqual(2, GuardAddictionSystem.GetAddictionStage(50));
            Assert.AreEqual(3, GuardAddictionSystem.GetAddictionStage(70));
            Assert.AreEqual(4, GuardAddictionSystem.GetAddictionStage(90));
            Assert.AreEqual(5, GuardAddictionSystem.GetAddictionStage(150));
        }

        // ===================== GetStageName 테스트 =====================

        [Test]
        public void GetStageName_ReturnsCorrectNames()
        {
            Assert.AreEqual("정상", GuardAddictionSystem.GetStageName(0));
            Assert.AreEqual("가벼운 의존", GuardAddictionSystem.GetStageName(1));
            Assert.AreEqual("중독", GuardAddictionSystem.GetStageName(2));
            Assert.AreEqual("심각한 중독", GuardAddictionSystem.GetStageName(3));
            Assert.AreEqual("완전 중독", GuardAddictionSystem.GetStageName(4));
            Assert.AreEqual("과다복용 ⚠️", GuardAddictionSystem.GetStageName(5));
        }

        // ===================== GetCombatMultiplier 테스트 =====================

        [Test]
        public void GetCombatMultiplier_DecreasesWithAddiction()
        {
            Assert.AreEqual(1.0f, GuardAddictionSystem.GetCombatMultiplier(0));
            Assert.AreEqual(1.0f, GuardAddictionSystem.GetCombatMultiplier(20));
            Assert.AreEqual(0.9f, GuardAddictionSystem.GetCombatMultiplier(50));
            Assert.AreEqual(0.7f, GuardAddictionSystem.GetCombatMultiplier(70));
            Assert.AreEqual(0.4f, GuardAddictionSystem.GetCombatMultiplier(90));
            Assert.AreEqual(0.0f, GuardAddictionSystem.GetCombatMultiplier(150));
        }

        // ===================== GetEffectDescription 테스트 =====================

        [Test]
        public void GetEffectDescription_NotEmpty()
        {
            for (int i = 0; i <= 5; i++)
            {
                string desc = GuardAddictionSystem.GetEffectDescription(i * 20f + 5f);
                Assert.IsNotEmpty(desc, $"Stage {i} 설명이 비어있지 않아야 합니다");
            }
        }

        // ===================== ApplyPoison 테스트 =====================

        [Test]
        public void ApplyPoison_DecreasesLoyaltyIncreasesAddiction()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            guard.Loyalty = 50f;
            guard.Addiction = 0f;

            GuardAddictionSystem.ApplyPoison(guard);

            Assert.Less(guard.Loyalty, 50f, "독약 투여 후 호감도 하락");
            Assert.Greater(guard.Addiction, 0f, "독약 투여 후 중독도 상승");

            Object.DestroyImmediate(go);
        }

        // ===================== ApplyAntidote 테스트 =====================

        [Test]
        public void ApplyAntidote_ReducesAddiction()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            guard.Addiction = 80f;

            GuardAddictionSystem.ApplyAntidote(guard);

            Assert.AreEqual(40f, guard.Addiction, 1f, "해독제 사용 후 중독도 50% 감소");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ApplyAntidote_DoesNotGoBelowZero()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            guard.Addiction = 5f;

            GuardAddictionSystem.ApplyAntidote(guard);

            Assert.GreaterOrEqual(guard.Addiction, 0f, "해독제 사용 후 중독도는 0 이상");

            Object.DestroyImmediate(go);
        }

        // ===================== ProcessDecay 테스트 =====================

        [Test]
        public void ProcessDecay_GraduallyReducesAddiction()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            guard.Addiction = 50f;

            GuardAddictionSystem.ProcessDecay(guard, 1f);
            Assert.Less(guard.Addiction, 50f, "1초 후 중독도가 감소해야 합니다");

            float before = guard.Addiction;
            GuardAddictionSystem.ProcessDecay(guard, 1000f);
            Assert.Less(guard.Addiction, before, "시간 경과에 따라 중독도 감소");
            Assert.GreaterOrEqual(guard.Addiction, 0f, "중독도는 0 미만으로 감소하지 않음");

            Object.DestroyImmediate(go);
        }

        // ===================== CheckOverdose 테스트 =====================

        [Test]
        public void CheckOverdose_KillsGuard()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            guard.Addiction = 150f;

            bool overdosed = GuardAddictionSystem.CheckOverdose(guard);
            Assert.IsTrue(overdosed, "100% 초과 중독도는 사망 처리되어야 함");
            Assert.IsFalse(guard.IsAlive, "과다복용으로 사망");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void CheckOverdose_SafeUnder100()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            guard.Addiction = 80f;

            bool overdosed = GuardAddictionSystem.CheckOverdose(guard);
            Assert.IsFalse(overdosed, "80%에서는 과다복용 아님");
            Assert.IsTrue(guard.IsAlive, "아직 살아있음");

            Object.DestroyImmediate(go);
        }

        // ===================== GetLoyaltyBonusFromAddiction 테스트 =====================

        [Test]
        public void GetLoyaltyBonus_Stage1_GivesBonus()
        {
            Assert.AreEqual(5f, GuardAddictionSystem.GetLoyaltyBonusFromAddiction(30));
            Assert.AreEqual(0f, GuardAddictionSystem.GetLoyaltyBonusFromAddiction(10));
            Assert.AreEqual(0f, GuardAddictionSystem.GetLoyaltyBonusFromAddiction(50));
        }

        [Test]
        public void GetLoyaltyBonus_Stage4_GivesHighBonus()
        {
            Assert.AreEqual(20f, GuardAddictionSystem.GetLoyaltyBonusFromAddiction(90));
        }

        // ===================== GetBehaviorErrorChance 테스트 =====================

        [Test]
        public void GetBehaviorErrorChance_IncreasesWithAddiction()
        {
            Assert.AreEqual(0f, GuardAddictionSystem.GetBehaviorErrorChance(0));
            Assert.AreEqual(0.05f, GuardAddictionSystem.GetBehaviorErrorChance(25));
            Assert.AreEqual(0.15f, GuardAddictionSystem.GetBehaviorErrorChance(50));
            Assert.AreEqual(0.30f, GuardAddictionSystem.GetBehaviorErrorChance(70));
            Assert.AreEqual(0.50f, GuardAddictionSystem.GetBehaviorErrorChance(90));
            Assert.AreEqual(1.0f, GuardAddictionSystem.GetBehaviorErrorChance(150));
        }

        // ===================== ProcessPoisonDamage 테스트 =====================

        [Test]
        public void ProcessPoisonDamage_HarmsGuard()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            float beforeHP = guard.HP;

            GuardAddictionSystem.ProcessPoisonDamage(guard, 10f);
            Assert.Less(guard.HP, beforeHP, "중독 데미지로 HP 감소");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ProcessPoisonDamage_ZeroWhenNoAddiction()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            guard.Addiction = 0f;
            float beforeHP = guard.HP;

            GuardAddictionSystem.ProcessPoisonDamage(guard, 10f);
            Assert.AreEqual(beforeHP, guard.HP, "중독도 0에서는 데미지 없음");

            Object.DestroyImmediate(go);
        }
    }
}