using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [156차 §1~§3] 이계 카드 시스템 EditMode 테스트.
    /// 검증: 카탈로그 무결성 / PickWeightedIndex 순수성 / 링 배율 / 마크 게이트 / 오퍼 결정론+중복금지 /
    ///   증강 보류 게이트 / 자격 필터(무기 슬롯·강화 레벨·스택·특수 1회성) / 선택 적용(패시브·특수) /
    ///   특수 소비(부활·사면·귀환·탈주수확) / EndRun 전면 리셋.
    /// Play 경로(훅 배선 — SurvivalSystem/PlayerCombat/PlayerHealth 등)는 Phase 4/5 게이트.
    /// [156차 P3] 카탈로그 36→40장(패시브 12→16 — 스택II 4종 추가) 반영 — 카운트 어서션/주석 일괄 갱신.
    /// </summary>
    public sealed class AfterlifeCardSystemTests
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
            AfterlifeCardSystem.ResetAll();
            AfterlifeSurvivalSystem.ResetAll();
        }

        // ── 카탈로그 무결성 (§2) ──

        [Test]
        public void Catalog_UniqueIds_AllKindsAndRaritiesRepresented()
        {
            var pool = CardCatalog.Cards;
            Assert.AreEqual(40, pool.Count, "풀 크기: 무기4 + 강화8 + 스킬8 + 패시브16 + 특수4 (§2 열거 기준 + [156차 P3] 스택II 4)");
            Assert.AreEqual(pool.Count, pool.Select(c => c.Id).Distinct().Count(), "Id 중복 없음");

            Assert.AreEqual(4, pool.Count(c => c.Kind == CardKind.Weapon), "무기 획득 4");
            Assert.AreEqual(8, pool.Count(c => c.Kind == CardKind.WeaponUpgrade), "무기 강화 8 (4무기 × Lv2/Lv3)");
            Assert.AreEqual(8, pool.Count(c => c.Kind == CardKind.Skill), "스킬 8");
            Assert.AreEqual(16, pool.Count(c => c.Kind == CardKind.Passive), "패시브 16 (기존 12 + [156차 P3] 스택II 4)");
            Assert.AreEqual(4, pool.Count(c => c.Kind == CardKind.Special), "특수 4");

            Assert.IsTrue(pool.Any(c => c.Rarity == CardRarity.Common), "Common 존재");
            Assert.IsTrue(pool.Any(c => c.Rarity == CardRarity.Rare), "Rare 존재");
            Assert.IsTrue(pool.Any(c => c.Rarity == CardRarity.Epic), "Epic 존재");
            Assert.IsTrue(pool.Any(c => c.Rarity == CardRarity.Legendary), "Legendary 존재");
        }

        [Test]
        public void Catalog_WeaponUpgrades_PairWithBaseWeapons_AtLevels2And3()
        {
            var upgrades = CardCatalog.Cards.Where(c => c.Kind == CardKind.WeaponUpgrade).ToList();
            var baseIds = CardCatalog.Cards.Where(c => c.Kind == CardKind.Weapon).Select(c => c.Id).ToHashSet();

            foreach (var up in upgrades)
            {
                Assert.IsFalse(string.IsNullOrEmpty(up.WeaponId), $"강화 카드 WeaponId 필수: {up.Id}");
                Assert.IsTrue(baseIds.Contains(up.WeaponId), $"강화 대상 무기 존재: {up.Id} → {up.WeaponId}");
                Assert.That(up.Level, Is.InRange(2, 3), $"강화 레벨 2/3: {up.Id}");
                Assert.AreNotEqual(up.Id, up.WeaponId, "자기 자신 강화 금지");
            }

            foreach (var baseId in baseIds)
                Assert.AreEqual(2, upgrades.Count(u => u.WeaponId == baseId), $"각 무기당 Lv2/Lv3 두 장: {baseId}");
        }

        [Test]
        public void Catalog_Specials_AllLegendary()
        {
            var specials = CardCatalog.Cards.Where(c => c.Kind == CardKind.Special).ToList();
            Assert.AreEqual(4, specials.Count);
            foreach (var s in specials)
                Assert.AreEqual(CardRarity.Legendary, s.Rarity, $"특수는 전부 Legendary: {s.Id}");
        }

        // ── 순수 수학 ──

        [Test]
        public void PickWeightedIndex_Deterministic_AndBoundsSafe()
        {
            int[] weights = { 70, 22, 6, 2 };
            var a = new System.Random(42);
            var b = new System.Random(42);
            for (int i = 0; i < 100; i++)
            {
                int ia = AfterlifeCardSystem.PickWeightedIndex(a, weights);
                int ib = AfterlifeCardSystem.PickWeightedIndex(b, weights);
                Assert.AreEqual(ia, ib, "동일 시드 = 동일 추출 (결정론)");
                Assert.That(ia, Is.InRange(0, weights.Length - 1), "범위 내");
            }
        }

        [Test]
        public void PickWeightedIndex_ExtremeWeights_ResolveCorrectly()
        {
            var rng = new System.Random(7);
            Assert.AreEqual(0, AfterlifeCardSystem.PickWeightedIndex(rng, new[] { 100, 0, 0, 0 }), "전부 0번 구간");
            Assert.AreEqual(1, AfterlifeCardSystem.PickWeightedIndex(rng, new[] { 0, 100, 0, 0 }), "전부 1번 구간");
            Assert.AreEqual(3, AfterlifeCardSystem.PickWeightedIndex(rng, new[] { 0, 0, 0, 100 }), "전부 3번 구간");
            Assert.AreEqual(0, AfterlifeCardSystem.PickWeightedIndex(rng, new[] { 1, -5, 0, 2 }), "음수 가중치 무시");
            Assert.AreEqual(0, AfterlifeCardSystem.PickWeightedIndex(rng, new[] { 0, 0, 0, 0 }), "전부 0 — 안전 0 반환");
        }

        [Test]
        public void RingFactor_Mapping()
        {
            Assert.AreEqual(0.5f, AfterlifeCardSystem.GetRingFactor(1), "Ring1 — 0.5");
            Assert.AreEqual(1f, AfterlifeCardSystem.GetRingFactor(2), "Ring2 — 1");
            Assert.AreEqual(2f, AfterlifeCardSystem.GetRingFactor(3), "Ring3 — 2");
            Assert.AreEqual(3f, AfterlifeCardSystem.GetRingFactor(4), "Ring4 — 3");
            Assert.AreEqual(0.5f, AfterlifeCardSystem.GetRingFactor(0), "미만 클램프");
            Assert.AreEqual(3f, AfterlifeCardSystem.GetRingFactor(9), "초과 클램프");
        }

        // ── 마크 게이트 / 오퍼 결정론 ──

        [Test]
        public void ShouldOffer_MarkGates_ByTarget()
        {
            Assert.That(AfterlifeCardSystem.ShouldOffer(300, 300f, 300f), Is.False, "Easy — 마크 미제시");
            Assert.That(AfterlifeCardSystem.ShouldOffer(300, 300f, 600f), Is.True, "Normal — 5분 오퍼");
            Assert.That(AfterlifeCardSystem.ShouldOffer(600, 600f, 600f), Is.False, "완주 시점 마크 = 오퍼 없음");
            Assert.That(AfterlifeCardSystem.ShouldOffer(300, 299.9f, 600f), Is.False, "미도달");
        }

        [Test]
        public void GenerateOffer_SameSeedAndRing_Deterministic_ThreeDistinct()
        {
            var offerA = AfterlifeCardSystem.GenerateOffer(42, ring: 2);
            var offerB = AfterlifeCardSystem.GenerateOffer(42, ring: 2);

            Assert.AreEqual(3, offerA.Count, "3택");
            CollectionAssert.AreEqual(offerA.Select(c => c.Id).ToList(), offerB.Select(c => c.Id).ToList(),
                "동일 시드+링 = 동일 오퍼 (결정론)");
            Assert.AreEqual(3, offerA.Select(c => c.Id).Distinct().Count(), "중복 제시 금지");
            var poolIds = CardCatalog.Cards.Select(c => c.Id).ToHashSet();
            foreach (var c in offerA)
                Assert.IsTrue(poolIds.Contains(c.Id), $"오퍼 항목은 풀 멤버: {c.Id}");
        }

        [Test]
        public void GenerateOffer_EligibilityRespected_WeaponUpgradeRequiresLevel()
        {
            // 무기 미보유 — Lv2 강화는 자격 없음 (Lv1 보유 필수)
            var offer = AfterlifeCardSystem.GenerateOffer(99, ring: 2);
            foreach (var c in offer)
                Assert.That(c.Kind, Is.Not.EqualTo(CardKind.WeaponUpgrade), "무기 미보유 — 강화 카드 배제");
        }

        // ── 제시 틱 (마크 + 증강 보류 + 이중 발화 가드) ──

        [Test]
        public void Tick_OfferAtMark_PendingThenGuardBlocksSecond()
        {
            AfterlifeCardSystem.BeginRun(ring: 2);
            AfterlifeCardSystem.Tick(300f, 600f, seedBase: 7, ring: 2, unscaledDt: 0.016f);
            Assert.That(AfterlifeCardSystem.IsPending, Is.True, "5분 도달 — 3택 제시");
            Assert.AreEqual(3, AfterlifeCardSystem.PendingChoices.Count);

            var firstId = AfterlifeCardSystem.PendingChoices[0].Id;
            AfterlifeCardSystem.Tick(600f, 900f, seedBase: 7, ring: 2, unscaledDt: 0.016f);
            Assert.That(AfterlifeCardSystem.IsPending, Is.True, "대기 중 유지");
            Assert.AreEqual(firstId, AfterlifeCardSystem.PendingChoices[0].Id, "대기 중 신규 오퍼 금지 — 첫 오퍼 유지");
        }

        [Test]
        public void Tick_DefersWhileAugmentPending_ResumesAfterResolve()
        {
            AfterlifeCardSystem.BeginRun(ring: 2);
            // 증강 오퍼 미해결 상태 만들기
            AfterlifeAugmentSystem.Tick(300f, 600f, seedBase: 7, unscaledDt: 0.016f);
            Assert.That(AfterlifeAugmentSystem.IsPending, Is.True, "증강 대기 중");

            AfterlifeCardSystem.Tick(300f, 600f, seedBase: 7, ring: 2, unscaledDt: 0.016f);
            Assert.That(AfterlifeCardSystem.IsPending, Is.False, "증강 미해결 — 카드 오퍼 보류");
            Assert.That(AfterlifeCardSystem.PendingChoices, Is.Empty, "카드 목록 없음");

            // 증강 선택 → 해결 → 카드 오퍼 재개
            AfterlifeAugmentSystem.Select(0);
            Assert.That(AfterlifeAugmentSystem.IsPending, Is.False, "증강 해결");

            AfterlifeCardSystem.Tick(300f, 600f, seedBase: 7, ring: 2, unscaledDt: 0.016f);
            Assert.That(AfterlifeCardSystem.IsPending, Is.True, "증강 해결 후 카드 오퍼 발화");
            Assert.AreEqual(3, AfterlifeCardSystem.PendingChoices.Count);
        }

        [Test]
        public void Tick_InactiveRun_NoOffer()
        {
            AfterlifeCardSystem.Tick(300f, 600f, seedBase: 7, ring: 2, unscaledDt: 0.016f);
            Assert.That(AfterlifeCardSystem.IsPending, Is.False, "런 미시작 — 오퍼 없음");
        }

        // ── 자격 필터 ──

        [Test]
        public void Eligibility_WeaponSlotLimit_FourWeapons()
        {
            AfterlifeCardSystem.BeginRun(ring: 2);
            string[] bases = { "w_saw_dagger", "w_grave_sword", "w_pierce_spear", "w_soul_bow" };
            foreach (var id in bases) Assert.IsTrue(AfterlifeCardSystem.IsEligible(CardCatalog.Get(id)), $"슬롯 여유 — 자격: {id}");

            foreach (var id in bases) AfterlifeCardSystem.SelectFromDef(CardCatalog.Get(id));
            Assert.AreEqual(4, AfterlifeCardSystem.WeaponCount, "슬롯 4 만석");

            foreach (var id in bases)
                Assert.That(AfterlifeCardSystem.IsEligible(CardCatalog.Get(id)), Is.False, "만석 — 무기 배제");
        }

        [Test]
        public void Eligibility_UpgradeGatedByLevel()
        {
            AfterlifeCardSystem.BeginRun(ring: 2);
            var lv2 = CardCatalog.Get("w_saw_dagger_2");
            var lv3 = CardCatalog.Get("w_saw_dagger_3");

            Assert.That(AfterlifeCardSystem.IsEligible(lv2), Is.False, "Lv1 미보유 — Lv2 배제");
            Assert.That(AfterlifeCardSystem.IsEligible(lv3), Is.False, "Lv1 미보유 — Lv3 배제");

            AfterlifeCardSystem.SelectFromDef(CardCatalog.Get("w_saw_dagger"));
            Assert.AreEqual(1, AfterlifeCardSystem.WeaponLevel("w_saw_dagger"), "획득 — Lv1");
            Assert.That(AfterlifeCardSystem.IsEligible(lv2), Is.True, "Lv1 보유 — Lv2 자격");
            Assert.That(AfterlifeCardSystem.IsEligible(lv3), Is.False, "Lv1 보유 — Lv3 배제");

            AfterlifeCardSystem.SelectFromDef(lv2);
            Assert.AreEqual(2, AfterlifeCardSystem.WeaponLevel("w_saw_dagger"), "강화 — Lv2");
            Assert.That(AfterlifeCardSystem.IsEligible(lv3), Is.True, "Lv2 보유 — Lv3 자격");
            Assert.That(AfterlifeCardSystem.IsEligible(lv2), Is.False, "Lv3 상한 도달 — Lv2 배제");

            AfterlifeCardSystem.SelectFromDef(lv3);
            Assert.AreEqual(3, AfterlifeCardSystem.WeaponLevel("w_saw_dagger"), "강화 — Lv3 상한");
        }

        [Test]
        public void Eligibility_StackAndOneTimeRules()
        {
            AfterlifeCardSystem.BeginRun(ring: 2);
            var emperor = CardCatalog.Get("p_emperor_seal"); // max 5
            var vampire = CardCatalog.Get("p_vampire_fang"); // max 1
            var special = CardCatalog.Get("sp_second_life");

            for (int i = 0; i < 5; i++) Assert.IsTrue(AfterlifeCardSystem.IsEligible(emperor), $"스택 {i + 1}/5 자격");
            AfterlifeCardSystem.SelectFromDef(emperor);
            Assert.That(AfterlifeCardSystem.IsEligible(emperor), Is.True, "1스택 — 여유");

            Assert.IsTrue(AfterlifeCardSystem.IsEligible(vampire), "비스택 미보유 — 자격");
            AfterlifeCardSystem.SelectFromDef(vampire);
            Assert.That(AfterlifeCardSystem.IsEligible(vampire), Is.False, "비스택 보유 중 — 배제");

            Assert.IsTrue(AfterlifeCardSystem.IsEligible(special), "특수 미보유 — 자격");
            AfterlifeCardSystem.SelectFromDef(special);
            Assert.That(AfterlifeCardSystem.IsEligible(special), Is.False, "특수 1회성 보유 중 — 배제");
        }

        // ── 선택 적용 (정적 상태) ──

        [Test]
        public void Select_Passives_ApplyMatchingState()
        {
            AfterlifeCardSystem.BeginRun(ring: 2);

            AfterlifeCardSystem.SelectFromDef(CardCatalog.Get("p_emperor_seal"));
            Assert.AreEqual(1.10f, AfterlifeCardSystem.DamageMult, "데미지 ×1.10");

            AfterlifeCardSystem.SelectFromDef(CardCatalog.Get("p_steel_shield"));
            Assert.AreEqual(0.92f, AfterlifeCardSystem.IncomingDamageFactor, 0.0001f, "받는피해 ×0.92");

            AfterlifeCardSystem.SelectFromDef(CardCatalog.Get("p_soul_jar"));
            Assert.AreEqual(1.2f, AfterlifeCardSystem.OrbValueMult, 0.0001f, "오브 가치 ×1.2");

            AfterlifeCardSystem.SelectFromDef(CardCatalog.Get("p_shadow_boots"));
            Assert.AreEqual(1.2f, AfterlifeCardSystem.RollDistanceFactor, 0.0001f, "구르기 ×1.2");

            AfterlifeCardSystem.SelectFromDef(CardCatalog.Get("p_perfect_mark"));
            Assert.AreEqual(0.02f, AfterlifeCardSystem.ExtraPerfectHitStop, 0.0001f, "PERFECT 히트스톱 +0.02");

            AfterlifeCardSystem.SelectFromDef(CardCatalog.Get("p_afterlife_talisman"));
            Assert.AreEqual(0.1f, AfterlifeCardSystem.TelegraphBonusSeconds, 0.0001f, "텔레그래프 +0.1");

            AfterlifeCardSystem.SelectFromDef(CardCatalog.Get("p_vampire_fang"));
            Assert.AreEqual(2f, AfterlifeCardSystem.KillHealPerStack, "처치힐 2");

            AfterlifeCardSystem.SelectFromDef(CardCatalog.Get("p_death_scythe"));
            Assert.IsTrue(AfterlifeCardSystem.DeathScytheActive, "사신 낫 활성");
            Assert.AreEqual(5, AfterlifeCardSystem.GetDeathScythePercent(isElite: false), "일반 5%");
            Assert.AreEqual(10, AfterlifeCardSystem.GetDeathScythePercent(isElite: true), "정예 5+5=10%");

            AfterlifeCardSystem.SelectFromDef(CardCatalog.Get("p_chain_seal"));
            Assert.AreEqual(0.9f, AfterlifeCardSystem.EnemyMoveFactor, 0.0001f, "적 이속 ×0.9");
        }

        [Test]
        public void Select_StackingCards_MultiplyPerStack()
        {
            AfterlifeCardSystem.BeginRun(ring: 2);

            AfterlifeCardSystem.SelectFromDef(CardCatalog.Get("p_emperor_seal"));
            AfterlifeCardSystem.SelectFromDef(CardCatalog.Get("p_emperor_seal"));
            Assert.AreEqual(1.21f, AfterlifeCardSystem.DamageMult, 0.0001f, "2스택 ×1.10²");

            AfterlifeCardSystem.SelectFromDef(CardCatalog.Get("p_steel_shield"));
            AfterlifeCardSystem.SelectFromDef(CardCatalog.Get("p_steel_shield"));
            AfterlifeCardSystem.SelectFromDef(CardCatalog.Get("p_steel_shield"));
            Assert.AreEqual(0.92f * 0.92f * 0.92f, AfterlifeCardSystem.IncomingDamageFactor, 0.0001f, "3스택 ×0.92³");
            Assert.That(AfterlifeCardSystem.IsEligible(CardCatalog.Get("p_steel_shield")), Is.False, "3스택 만석 — 배제");
        }

        [Test]
        public void Select_TimeSand_SwitchesWaveInterval()
        {
            AfterlifeCardSystem.BeginRun(ring: 2);
            Assert.AreEqual(120f, AfterlifeCardSystem.WaveIntervalSeconds, "기본 120초");

            AfterlifeCardSystem.SelectFromDef(CardCatalog.Get("p_time_sand"));
            Assert.AreEqual(135f, AfterlifeCardSystem.WaveIntervalSeconds, "시간의 모래 — 135초");
        }

        [Test]
        public void Select_Specials_GrantAndConsume()
        {
            AfterlifeCardSystem.BeginRun(ring: 2);

            AfterlifeCardSystem.SelectFromDef(CardCatalog.Get("sp_second_life"));
            Assert.IsTrue(AfterlifeCardSystem.SecondLifeAvailable, "부활 가능");
            Assert.AreEqual(0.5f, AfterlifeCardSystem.ConsumeSecondLife(), "부활 소비 — HP 50%");
            Assert.That(AfterlifeCardSystem.SecondLifeAvailable, Is.False, "1회 소비 후 불가");
            Assert.AreEqual(0f, AfterlifeCardSystem.ConsumeSecondLife(), "잔여 없음 — 0 반환");

            AfterlifeCardSystem.SelectFromDef(CardCatalog.Get("sp_wave_pardon"));
            Assert.AreEqual(1, AfterlifeCardSystem.WavePardonCount, "사면 1회");
            Assert.IsTrue(AfterlifeCardSystem.ConsumeWavePardon(), "사면 소비");
            Assert.That(AfterlifeCardSystem.ConsumeWavePardon(), Is.False, "잔여 없음");

            AfterlifeCardSystem.SelectFromDef(CardCatalog.Get("sp_pledge_receipt"));
            Assert.AreEqual(1.0f, AfterlifeCardSystem.SurrenderHarvestRatio, "탈주 수확 1.0");

            AfterlifeCardSystem.SelectFromDef(CardCatalog.Get("sp_realm_gate"));
            Assert.AreEqual(0.5f, AfterlifeCardSystem.ConsumeRealmGate(), "귀환 문 — 수확 ×0.5");

            // 기본값 (미보유)
            AfterlifeCardSystem.ResetAll();
            AfterlifeCardSystem.BeginRun(ring: 2);
            Assert.AreEqual(0.5f, AfterlifeCardSystem.SurrenderHarvestRatio, "기본 탈주 수확 0.5");
            Assert.AreEqual(0f, AfterlifeCardSystem.ConsumeRealmGate(), "귀환 문 미보유 — 0");
        }

        [Test]
        public void SkullRing_ChainCounts_AndTriggersAt10()
        {
            AfterlifeCardSystem.BeginRun(ring: 2);
            Assert.IsFalse(AfterlifeCardSystem.NotifyEnemyKilledForSkullRing(), "미보유 — 무시");

            AfterlifeCardSystem.SelectFromDef(CardCatalog.Get("p_skull_ring"));
            for (int i = 0; i < 9; i++) Assert.IsFalse(AfterlifeCardSystem.NotifyEnemyKilledForSkullRing(), $"연쇄 {i + 1}/10");
            Assert.IsTrue(AfterlifeCardSystem.NotifyEnemyKilledForSkullRing(), "10연속 — 오브 ×2 트리거");
            Assert.AreEqual(0, AfterlifeCardSystem.SkullRingChainCount, "트리거 후 리셋");
        }

        [Test]
        public void Select_InvalidIndex_NoOp()
        {
            AfterlifeCardSystem.BeginRun(ring: 2);
            AfterlifeCardSystem.Tick(300f, 600f, seedBase: 7, ring: 2, unscaledDt: 0.016f);
            Assert.That(AfterlifeCardSystem.IsPending, Is.True);

            AfterlifeCardSystem.Select(-1);
            AfterlifeCardSystem.Select(99);
            Assert.That(AfterlifeCardSystem.IsPending, Is.True, "유효하지 않은 인덱스 — 무시");
            Assert.AreEqual(0, AfterlifeCardSystem.OwnedCounts.Count, "소유 변화 없음");
        }

        [Test]
        public void Select_FiresEvents_ThenResolves()
        {
            AfterlifeCardSystem.BeginRun(ring: 2);
            AfterlifeCardSystem.Tick(300f, 600f, seedBase: 7, ring: 2, unscaledDt: 0.016f);

            CardDef applied = null;
            int resolved = 0;
            AfterlifeCardSystem.OnCardApplied += c => applied = c;
            AfterlifeCardSystem.OnOfferResolved += () => resolved++;

            AfterlifeCardSystem.Select(0);

            Assert.IsNotNull(applied, "적용 이벤트 발화");
            Assert.AreEqual(1, resolved, "해결 이벤트 발화");
            Assert.That(AfterlifeCardSystem.IsPending, Is.False);
        }

        // ── 런 라이프사이클 ──

        [Test]
        public void EndRun_ResetsAllState()
        {
            AfterlifeCardSystem.BeginRun(ring: 2);
            AfterlifeCardSystem.Tick(300f, 600f, seedBase: 7, ring: 2, unscaledDt: 0.016f);
            AfterlifeCardSystem.Select(0);
            AfterlifeCardSystem.SelectFromDef(CardCatalog.Get("sp_second_life"));

            AfterlifeCardSystem.EndRun();

            Assert.AreEqual(1f, AfterlifeCardSystem.DamageMult, "데미지 배율 리셋");
            Assert.AreEqual(1f, AfterlifeCardSystem.IncomingDamageFactor, "받는피해 리셋");
            Assert.AreEqual(1f, AfterlifeCardSystem.EnemyMoveFactor, "적 이속 리셋");
            Assert.AreEqual(1f, AfterlifeCardSystem.OrbValueMult, "오브 가치 리셋");
            Assert.AreEqual(1f, AfterlifeCardSystem.RollDistanceFactor, "구르기 리셋");
            Assert.AreEqual(0f, AfterlifeCardSystem.MaxHPBonus, "HP 보너스 리셋");
            Assert.AreEqual(0f, AfterlifeCardSystem.ExtraPerfectHitStop, "히트스톱 리셋");
            Assert.AreEqual(0f, AfterlifeCardSystem.TelegraphBonusSeconds, "텔레그래프 리셋");
            Assert.AreEqual(0, AfterlifeCardSystem.WeaponCount, "무기 소유 소멸");
            Assert.AreEqual(0, AfterlifeCardSystem.OwnedCounts.Count, "카드 소유 소멸");
            Assert.AreEqual(0, AfterlifeCardSystem.OwnedSkills.Count, "스킬 소유 소멸");
            Assert.AreEqual(0.5f, AfterlifeCardSystem.SurrenderHarvestRatio, "탈주 수확 기본 복원");
            Assert.AreEqual(120f, AfterlifeCardSystem.WaveIntervalSeconds, "웨이브 간격 기본 복원");
            Assert.IsFalse(AfterlifeCardSystem.SecondLifeAvailable, "부활 소멸");
            Assert.AreEqual(0, AfterlifeCardSystem.WavePardonCount, "사면 소멸");
            Assert.That(AfterlifeCardSystem.IsPending, Is.False, "대기 해제");
        }
    }
}
