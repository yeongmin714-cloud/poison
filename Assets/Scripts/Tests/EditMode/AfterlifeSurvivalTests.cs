using NUnit.Framework;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [A2 이계] AfterlifeSurvivalSystem 결정론 EditMode 테스트 (75차 A2 코어).
    /// 검증: 난이도별 지속시간 매핑 / UseAfterlifeRespawn=false 게이트(진입 없음) /
    ///   ReviveInPlace HP 복원 / 이계 적 TakeDamage→사망→처치 통보 / ResetAll.
    /// Play 경로(EnterBuilding/웨이브/퍼머데스)는 Play 게이트 — EditMode에서 씬 로드 부작용 회피.
    /// </summary>
    public sealed class AfterlifeSurvivalTests
    {
        [SetUp]
        public void SetUp()
        {
            AfterlifeSurvivalSystem.ResetAll();
        }

        [TearDown]
        public void TearDown()
        {
            AfterlifeSurvivalSystem.ResetAll();
        }

        [Test]
        public void TargetSeconds_MapDifficulty()
        {
            Assert.That(AfterlifeSurvivalSystem.GetTargetSecondsForDifficulty(0), Is.EqualTo(300f), "Easy 5분");
            Assert.That(AfterlifeSurvivalSystem.GetTargetSecondsForDifficulty(1), Is.EqualTo(600f), "Normal 10분");
            Assert.That(AfterlifeSurvivalSystem.GetTargetSecondsForDifficulty(2), Is.EqualTo(900f), "Hard 15분");
        }

        [Test]
        public void InterceptDeath_DisabledSetting_ReturnsFalse_NoEntry()
        {
            AfterlifeSurvivalSystem.UseAfterlifeRespawn = false;
            Assert.That(AfterlifeSurvivalSystem.TryInterceptDeath(), Is.False, "설정 OFF — 기존 즉시 부활 경로");
            Assert.That(AfterlifeSurvivalSystem.IsActive, Is.False, "이계 진입 없음");
            Assert.That(TimeManager.IsTimeFrozen, Is.False, "시간 동결 없음");
        }

        [Test]
        public void ResetAll_ClearsState_AndUnfreezesTime()
        {
            TimeManager.IsTimeFrozen = true;
            AfterlifeSurvivalSystem.ResetAll();
            Assert.That(TimeManager.IsTimeFrozen, Is.False, "ResetAll — 시간 동결 해제 포함");
            Assert.That(AfterlifeSurvivalSystem.IsActive, Is.False);
            Assert.That(AfterlifeSurvivalSystem.RunGold, Is.EqualTo(0));
        }

        [Test]
        public void ReviveInPlace_RestoresHPPercent_WithoutInstance()
        {
            var go = new GameObject("AfterlifeTest_Player");
            var health = go.AddComponent<PlayerHealth>(); // 컴포넌트 참조 — Awake/Instance 무의존 패턴
            health.ReviveInPlace(0.5f);
            Assert.That(health.HPRatio, Is.EqualTo(0.5f).Within(0.01f), "이계 진입 부활 = 50%");
            Assert.That(health.IsDead, Is.False, "사망 상태 해제");
            health.ReviveInPlace(0.25f);
            Assert.That(health.HPRatio, Is.EqualTo(0.25f).Within(0.01f), "재호출 — 비율 반영");
        }

        [Test]
        public void AfterlifeEnemy_TakeDamageToDeath_NotifiesSystem()
        {
            var go = new GameObject("AfterlifeTest_Enemy");
            var enemy = go.AddComponent<AfterlifeEnemy>();
            enemy.Configure(40f, 5f, 2f, 3);
            Assert.That(enemy.IsAlive, Is.True, "스폰 직후 생존");
            enemy.TakeDamage(10f, Vector3.forward, "melee");
            Assert.That(enemy.IsAlive, Is.True, "40 중 10 데미지 — 생존");
            enemy.TakeDamage(30f, Vector3.forward, "melee");
            Assert.That(enemy.IsDead, Is.True, "누적 40 = 사망");
            Assert.That(AfterlifeSurvivalSystem.Kills, Is.GreaterThanOrEqualTo(1), "처치 통보 집계");
        }
    }
}
