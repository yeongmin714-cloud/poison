using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [A3 이계] 오브/증강 결정론 EditMode 테스트 (77차 A3).
    /// 검증: 드랍 구성 합계 산식 / 오퍼 결정론+3택 유효성 / 마크 게이트 / 티어 배율 /
    ///   XP 배율 반올림 / 오브 흡수 집계 / 오퍼 이중 발화 가드 / 선택 적용(정적 배율).
    /// Play 경로(웨이브 실드랍/자석 흡수/MaxHP·이속 훅)는 Play 게이트 — EditMode에서 씬 부작용 회피.
    /// </summary>
    public sealed class AfterlifeAugmentOrbTests
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

        // ── 드랍 구성 (결정론 산식) ──

        [Test]
        public void LootComposition_L1_SumsMatchTotals()
        {
            var gold = new List<int>();
            var xp = new List<int>();
            AfterlifeOrb.GetLootComposition(1, gold, xp);

            Assert.AreEqual(2, gold.Count, "골드 오브 2개");
            Assert.AreEqual(3, xp.Count, "XP 오브 3개");
            Assert.AreEqual(AfterlifeSurvivalSystem.RUN_GOLD_PER_KILL + 2, gold[0] + gold[1], "골드 총합 = 기준 5 + 레벨×2");
            Assert.AreEqual(12, xp[0] + xp[1] + xp[2], "XP 총합 = 8 + 레벨×4");
            foreach (var v in gold) Assert.GreaterOrEqual(v, 1, "골드 오브 최소 1");
            foreach (var v in xp) Assert.GreaterOrEqual(v, 1, "XP 오브 최소 1");
        }

        [Test]
        public void LootComposition_L5_SumsMatchTotals()
        {
            var gold = new List<int>();
            var xp = new List<int>();
            AfterlifeOrb.GetLootComposition(5, gold, xp);

            Assert.AreEqual(15, gold[0] + gold[1], "골드 총합 5+10");
            Assert.AreEqual(28, xp[0] + xp[1] + xp[2], "XP 총합 8+20");
        }

        // ── 3택1 오퍼 (결정론) ──

        [Test]
        public void GenerateOffer_SameSeedDeterministic_ThreeDistinctValid()
        {
            var offerA = AfterlifeAugmentSystem.GenerateOffer(42);
            var offerB = AfterlifeAugmentSystem.GenerateOffer(42);

            Assert.AreEqual(3, offerA.Count, "3택");
            CollectionAssert.AreEqual(offerA.Select(a => a.Id).ToList(), offerB.Select(b => b.Id).ToList(),
                "동일 시드 = 동일 오퍼 (결정론)");
            Assert.AreEqual(3, offerA.Select(a => a.Id).Distinct().Count(), "중복 없음");
            var poolIds = AfterlifeAugmentSystem.Pool.Select(a => a.Id).ToHashSet();
            foreach (var a in offerA)
                Assert.IsTrue(poolIds.Contains(a.Id), $"오퍼 항목은 풀 멤버: {a.Id}");
        }

        [Test]
        public void ShouldOffer_MarkGates_ByTarget()
        {
            // Easy(5분 목표) — 마크 300이 목표와 동일 → 오퍼 없음
            Assert.That(AfterlifeAugmentSystem.ShouldOffer(300, 300f, 300f), Is.False, "Easy — 마크 미제시");
            // Normal(10분) — 마크 300만
            Assert.That(AfterlifeAugmentSystem.ShouldOffer(300, 300f, 600f), Is.True, "Normal — 5분 오퍼");
            Assert.That(AfterlifeAugmentSystem.ShouldOffer(600, 600f, 600f), Is.False, "완주 시점 마크 = 오퍼 없음");
            // Hard(15분) — 5/10분 둘 다
            Assert.That(AfterlifeAugmentSystem.ShouldOffer(300, 300f, 900f), Is.True);
            Assert.That(AfterlifeAugmentSystem.ShouldOffer(600, 600f, 900f), Is.True, "Hard — 10분 오퍼");
            // 미도달
            Assert.That(AfterlifeAugmentSystem.ShouldOffer(300, 299.9f, 600f), Is.False);
        }

        [Test]
        public void Tick_OfferAtMark_PendingThenGuardBlocksSecond()
        {
            AfterlifeAugmentSystem.Tick(300f, 600f, seedBase: 7, unscaledDt: 0.016f);
            Assert.That(AfterlifeAugmentSystem.IsPending, Is.True, "5분 도달 — 3택 제시");
            Assert.AreEqual(3, AfterlifeAugmentSystem.PendingChoices.Count);

            var firstId = AfterlifeAugmentSystem.PendingChoices[0].Id;
            AfterlifeAugmentSystem.Tick(600f, 900f, seedBase: 7, unscaledDt: 0.016f);
            Assert.That(AfterlifeAugmentSystem.IsPending, Is.True, "대기 중 유지");
            Assert.AreEqual(firstId, AfterlifeAugmentSystem.PendingChoices[0].Id, "대기 중 신규 오퍼 금지 — 첫 오퍼 유지");
        }

        [Test]
        public void Select_AppliesMatchingMultiplier_ThenResolves()
        {
            AfterlifeAugmentSystem.Tick(300f, 600f, seedBase: 7, unscaledDt: 0.016f);
            var def = AfterlifeAugmentSystem.PendingChoices[0];
            float magnetBefore = AfterlifeOrb.MagnetRadius;

            AfterlifeAugmentSystem.Select(0);

            Assert.That(AfterlifeAugmentSystem.IsPending, Is.False, "선택 후 대기 해제");
            Assert.AreEqual(0, AfterlifeAugmentSystem.PendingChoices.Count);
            switch (def.Kind)
            {
                case AfterlifeAugmentSystem.Kind.AttackSpeed:
                    Assert.Greater(AfterlifeAugmentSystem.AttackSpeedMult, 1f);
                    break;
                case AfterlifeAugmentSystem.Kind.MoveSpeed:
                    Assert.Greater(AfterlifeAugmentSystem.MoveSpeedMult, 1f);
                    break;
                case AfterlifeAugmentSystem.Kind.MaxHP:
                    Assert.Greater(AfterlifeAugmentSystem.MaxHPMult, 1f);
                    break;
                case AfterlifeAugmentSystem.Kind.MagnetRadius:
                    Assert.Greater(AfterlifeOrb.MagnetRadius, magnetBefore, "흡수 반경 증가");
                    break;
                case AfterlifeAugmentSystem.Kind.XPGain:
                    Assert.Greater(AfterlifeAugmentSystem.XPGainMult, 1f);
                    break;
                case AfterlifeAugmentSystem.Kind.Regen:
                    Assert.Greater(AfterlifeAugmentSystem.RegenPerSec, 0f);
                    break;
            }
        }

        [Test]
        public void EndRun_ResetsMultipliers_AndMagnet()
        {
            AfterlifeAugmentSystem.BeginRun(ring: 3, territoryIndex: 12);
            Assert.AreEqual(3, AfterlifeAugmentSystem.DeathRing, "런 진입 — 티어 기록");
            AfterlifeAugmentSystem.Tick(300f, 600f, seedBase: 7, unscaledDt: 0.016f);
            AfterlifeAugmentSystem.Select(0);

            AfterlifeAugmentSystem.EndRun();

            Assert.AreEqual(1f, AfterlifeAugmentSystem.AttackSpeedMult, "공속 배율 리셋");
            Assert.AreEqual(1f, AfterlifeAugmentSystem.XPGainMult, "XP 배율 리셋");
            Assert.AreEqual(0f, AfterlifeAugmentSystem.RegenPerSec, "재생 리셋");
            Assert.AreEqual(AfterlifeOrb.BaseAbsorbRadius, AfterlifeOrb.MagnetRadius, "자석 반경 기본 복원");
            Assert.That(AfterlifeAugmentSystem.IsPending, Is.False);
        }

        // ── 티어 / XP 배율 (순수) ──

        [Test]
        public void TierFactor_RingMapping()
        {
            Assert.AreEqual(0.85f, AfterlifeAugmentSystem.GetTierFactor(1), "Ring1 외곽 — 쉬움");
            Assert.AreEqual(1.00f, AfterlifeAugmentSystem.GetTierFactor(2), "Ring2 기준");
            Assert.AreEqual(1.15f, AfterlifeAugmentSystem.GetTierFactor(3), "Ring3 — 어려움");
            Assert.AreEqual(1.30f, AfterlifeAugmentSystem.GetTierFactor(4), "Ring4 중앙 — 가장 어려움");
            Assert.AreEqual(1.30f, AfterlifeAugmentSystem.GetTierFactor(7), "범위 초과 — 클램프");
            Assert.AreEqual(0.85f, AfterlifeAugmentSystem.GetTierFactor(0), "범위 미만 — 클램프");
        }

        [Test]
        public void RoundXPGain_DefaultMultIdentity_ExplicitMultApplied()
        {
            Assert.AreEqual(10, AfterlifeAugmentSystem.RoundXPGain(10), "기본 배율 1 — 원값");
            Assert.AreEqual(10, AfterlifeAugmentSystem.RoundXPGain(8, 1.25f), "8 × 1.25 = 10");
        }

        // ── 오브 흡수 집계 (정적 tally) ──

        [Test]
        public void NotifyOrbAbsorbed_TalliesGoldAndXP()
        {
            AfterlifeSurvivalSystem.NotifyOrbAbsorbed(AfterlifeOrb.OrbType.Gold, 5);
            AfterlifeSurvivalSystem.NotifyOrbAbsorbed(AfterlifeOrb.OrbType.XP, 8);
            AfterlifeSurvivalSystem.NotifyOrbAbsorbed(AfterlifeOrb.OrbType.Gold, 4);

            Assert.AreEqual(9, AfterlifeSurvivalSystem.RunGold, "골드 합산");
            Assert.AreEqual(8, AfterlifeSurvivalSystem.RunXP, "XP 합산 (기본 배율)");

            AfterlifeSurvivalSystem.ResetAll();
            Assert.AreEqual(0, AfterlifeSurvivalSystem.RunGold, "리셋 — 골드");
            Assert.AreEqual(0, AfterlifeSurvivalSystem.RunXP, "리셋 — XP");
        }
    }
}
