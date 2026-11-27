using NUnit.Framework;
using ProjectName.Core.Data;
using ProjectName.Core.Utils;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [D4-B] 경제 연쇄 테스트 — 마을 경제 지수/세금 타격/영주 폭력화/주민 이탈.
    ///
    /// 검증 범위 (EditMode):
    ///  · VillageEconomyMath 순수 수학 — EconomyIndex(하위호환/감소/하한 0.1)/WeeklyTaxFactor(하위호환/단조)/ApplyTaxFactor(half-up).
    ///  · VillageEconomySystem — 마을 상태 → 경제 지수/영지 세금 계수 매핑 (VillageDrugState 스냅샷 연동).
    ///  · VillagerDepartureSystem — 결정론 롤(고정 시드) + RecordDeparture 연계. UnityEngine.Random 미사용 확인.
    ///  · LordViolenceSystem — 세금 미달 판정 순수부 + 상태 반영(폭력화/충성도 −10/제외 조건).
    ///
    /// [Play 게이트] — EditMode에서 검증하지 않는 항목(QA Play 검증 대상):
    ///  · VillageNpcSpawner.RemoveVillageNPCs의 실제 GameObject 제거(씬의 VillageNpcs_Root 필요).
    ///  · RestaurantDailyTicker/LordViolenceTicker의 OnDayStart 실발화(TimeManager 구동 필요).
    ///  · ApplyDailyTax 경계일 실트릭을 통한 세금 타격 종단 시나리오.
    ///
    /// 위생 규약: VillageDrugState/AITerritoryEconomySystem는 SetUp/TearDown에서 항상 초기화 —
    /// 타 테스트(AITerritoryWeeklyTaxTests 등)에 정적 상태 오염을 남기지 않는다.
    /// </summary>
    public class VillageEconomyTests
    {
        private const float Epsilon = 0.0001f;

        [SetUp]
        public void SetUp()
        {
            VillageDrugState.ResetAll();
            AITerritoryEconomySystem.ResetAll();
        }

        [TearDown]
        public void TearDown()
        {
            VillageDrugState.ResetAll();
            AITerritoryEconomySystem.ResetAll();
        }

        // ===== VillageEconomyMath — EconomyIndex =====

        [Test]
        public void EconomyIndex_PristineVillage_IsOne()
        {
            // 중독 0 + 이탈 0 = 경제 1.0 (깨끗한 마을 — 하위호환 기준점)
            Assert.AreEqual(1f, VillageEconomyMath.EconomyIndex(0f, 0, 8), Epsilon);
            Assert.AreEqual(1f, VillageEconomyMath.EconomyIndex(0f, 0, 4), Epsilon);
        }

        [Test]
        public void EconomyIndex_Addiction100_IsPointFour()
        {
            // 스펙 D4-B: 중독 100 = 1 − 0.6 = 0.4
            Assert.AreEqual(0.4f, VillageEconomyMath.EconomyIndex(100f, 0, 8), Epsilon);
        }

        [Test]
        public void EconomyIndex_DepartureReduction_Proportional()
        {
            // 이탈 비율(departed/주민수) × 0.3 감소 — 2/8 이탈 = −0.075, 4/8 이탈 = −0.15
            Assert.AreEqual(0.925f, VillageEconomyMath.EconomyIndex(0f, 2, 8), Epsilon);
            Assert.AreEqual(0.85f, VillageEconomyMath.EconomyIndex(0f, 4, 8), Epsilon);
        }

        [Test]
        public void EconomyIndex_Combined_AddictionAndDeparture()
        {
            // 중독 50(−0.3) + 이탈 4/8(−0.15) = 0.55
            Assert.AreEqual(0.55f, VillageEconomyMath.EconomyIndex(50f, 4, 8), Epsilon);
        }

        [Test]
        public void EconomyIndex_FloorClampsAtPointOne()
        {
            // 중독 100(−0.6) + 전원 이탈(−0.3) = 0.1 (하한에 정확히 도달)
            Assert.AreEqual(0.1f, VillageEconomyMath.EconomyIndex(100f, 8, 8), Epsilon);
            // 하한 초과 입력도 0.1로 클램프 — 완전 소진 방지선
            Assert.AreEqual(0.1f, VillageEconomyMath.EconomyIndex(100f, 100, 8), Epsilon);
        }

        [Test]
        public void EconomyIndex_ZeroVillagersDefended()
        {
            // 주민수 0 방어 — max(1)로 나눈다 → 이탈 1 = −0.3
            Assert.AreEqual(0.7f, VillageEconomyMath.EconomyIndex(0f, 1, 0), Epsilon);
        }

        [Test]
        public void EconomyIndex_NegativeInputsDefended()
        {
            // 음수 중독/이탈 방어 — 1.0 유지
            Assert.AreEqual(1f, VillageEconomyMath.EconomyIndex(-50f, -3, 8), Epsilon);
        }

        // ===== VillageEconomyMath — WeeklyTaxFactor =====

        [Test]
        public void WeeklyTaxFactor_PristineEconomy_IsOne()
        {
            // 경제 1.0 = 계수 1.0 — 하위호환 (D4 이전 세수와 동일)
            Assert.AreEqual(1f, VillageEconomyMath.WeeklyTaxFactor(1f), Epsilon);
        }

        [Test]
        public void WeeklyTaxFactor_Addiction100Economy_IsPointSeven()
        {
            // 스펙 D4-B: 중독 100 마을(economy 0.4) → 0.5 + 0.5 × 0.4 = 0.7
            Assert.AreEqual(0.7f, VillageEconomyMath.WeeklyTaxFactor(0.4f), Epsilon);
        }

        [Test]
        public void WeeklyTaxFactor_Monotonic_InEconomy()
        {
            // economy 증가에 단조 증가 — 세금 감면은 경제가 나빠질수록 커진다
            float prev = 0f;
            for (int i = 0; i <= 10; i++)
            {
                float factor = VillageEconomyMath.WeeklyTaxFactor(i / 10f);
                Assert.GreaterOrEqual(factor, prev, $"economy={i / 10f}");
                prev = factor;
            }
            // 범위 — economy 하한 0.1 대응 최소 0.55, 상한 1.0
            Assert.AreEqual(0.55f, VillageEconomyMath.WeeklyTaxFactor(0.1f), Epsilon);
            Assert.AreEqual(1f, VillageEconomyMath.WeeklyTaxFactor(1f), Epsilon);
        }

        [Test]
        public void WeeklyTaxFactor_ClampsOutOfRangeInput()
        {
            // 범위 백 입력 방어 — 2.0 → 1.0, -1.0 → 0.5
            Assert.AreEqual(1f, VillageEconomyMath.WeeklyTaxFactor(2f), Epsilon);
            Assert.AreEqual(0.5f, VillageEconomyMath.WeeklyTaxFactor(-1f), Epsilon);
        }

        // ===== VillageEconomyMath — ApplyTaxFactor =====

        [Test]
        public void ApplyTaxFactor_FactorOne_IsExactWeeklyTax()
        {
            // 계수 1.0 = 정확히 기존 주간 세액 (하위호환 — 반올림 왜곡 없음)
            Assert.AreEqual(70, VillageEconomyMath.ApplyTaxFactor(70, 1f));
            Assert.AreEqual(74, VillageEconomyMath.ApplyTaxFactor(74, 1f));
            Assert.AreEqual(700, VillageEconomyMath.ApplyTaxFactor(700, 1f));
        }

        [Test]
        public void ApplyTaxFactor_RoundsHalfUp()
        {
            // 70 × 0.55 = 38.5 → 39 (half-up — GetWeeklyTax 규약 준용)
            Assert.AreEqual(39, VillageEconomyMath.ApplyTaxFactor(70, 0.55f));
            // 66 × 0.55 = 36.3 → 36
            Assert.AreEqual(36, VillageEconomyMath.ApplyTaxFactor(66, 0.55f));
            // 700 × 0.7 = 490 (정확)
            Assert.AreEqual(490, VillageEconomyMath.ApplyTaxFactor(700, 0.7f));
        }

        [Test]
        public void ApplyTaxFactor_NegativeWeeklyTaxDefended()
        {
            Assert.AreEqual(0, VillageEconomyMath.ApplyTaxFactor(-5, 1f));
        }

        // ===== VillageEconomySystem — 상태 연동 =====

        [Test]
        public void GetVillageEconomy_UntrackedVillage_IsOne()
        {
            // 상태 없는 마을 = 중독 0/이탈 0 = 1.0 (하위호환)
            Assert.AreEqual(1f, VillageEconomySystem.GetVillageEconomy(NationType.East, 3), Epsilon);
        }

        [Test]
        public void GetVillageEconomy_AddictedVillage_Drops()
        {
            VillageDrugState.AddAddiction(NationType.East, 5, 50f);
            // 1 − 0.5 × 0.6 = 0.7 (일반 마을 근사 주민 8)
            Assert.AreEqual(0.7f, VillageEconomySystem.GetVillageEconomy(NationType.East, 5), Epsilon);
        }

        [Test]
        public void GetTerritoryEconomyFactor_MapsVillageOneToOne()
        {
            // 마을→영지 1:1 (T3 실측) — (West, 2) 마을 중독 100 → 영지 인덱스 3 계수 = 0.7
            VillageDrugState.AddAddiction(NationType.West, 2, 100f);
            Assert.AreEqual(0.7f, VillageEconomySystem.GetTerritoryEconomyFactor(NationType.West, 3), Epsilon);
            // 인접 영지(마을 (West, 3))는 무영향 — 1.0
            Assert.AreEqual(1f, VillageEconomySystem.GetTerritoryEconomyFactor(NationType.West, 4), Epsilon);
        }

        [Test]
        public void GetTerritoryEconomyFactor_OutOfRangeIndex_IsOne()
        {
            // 범위 밖 영지 인덱스 — 감면 없음(1.0)
            Assert.AreEqual(1f, VillageEconomySystem.GetTerritoryEconomyFactor(NationType.East, 0), Epsilon);
            Assert.AreEqual(1f, VillageEconomySystem.GetTerritoryEconomyFactor(NationType.East, 21), Epsilon);
        }

        [Test]
        public void GetWeeklyCollectedTax_PristineVillage_MatchesBaseWeeklyTax()
        {
            // 하위호환 — 마을 상태 없으면 GetWeeklyTax와 정확히 동일 (기존 테스트 기대값 재사용)
            Assert.AreEqual(70, VillageEconomySystem.GetWeeklyCollectedTax(TerritoryDifficulty.Ring1, 100, NationType.East, 1));
            Assert.AreEqual(74, VillageEconomySystem.GetWeeklyCollectedTax(TerritoryDifficulty.Ring1, 105, NationType.East, 1));
        }

        [Test]
        public void GetWeeklyCollectedTax_EmpireNation_NoVillage_IsFullTax()
        {
            // 마을 없는 국가(황제국) — 기본 스냅샷 → 계수 1.0 → 감면 없음
            Assert.AreEqual(700, VillageEconomySystem.GetWeeklyCollectedTax(TerritoryDifficulty.Empire, 100, NationType.Empire, 1));
        }

        [Test]
        public void GetWeeklyCollectedTax_AddictedVillage_Reduced()
        {
            // (South, 0) 마을 중독 100 → economy 0.4 → 계수 0.7 → Ring1 주간 70 × 0.7 = 49
            VillageDrugState.AddAddiction(NationType.South, 0, 100f);
            Assert.AreEqual(49, VillageEconomySystem.GetWeeklyCollectedTax(TerritoryDifficulty.Ring1, 100, NationType.South, 1));
        }

        // ===== VillagerDepartureSystem =====

        [Test]
        public void WillDepart_PureContract_HalfOpenInterval()
        {
            Assert.IsFalse(VillagerDepartureSystem.WillDepart(0f, 0.0));   // chance 0 → 항상 미이탈
            Assert.IsFalse(VillagerDepartureSystem.WillDepart(0f, 0.5));
            Assert.IsFalse(VillagerDepartureSystem.WillDepart(0.9f, 0.9)); // 반개구간 — roll == chance는 미이탈
            Assert.IsTrue(VillagerDepartureSystem.WillDepart(0.9f, 0.89));
            Assert.IsTrue(VillagerDepartureSystem.WillDepart(1f, 0.999));
        }

        [Test]
        public void RunDailyDeparture_NoRestaurants_NoDeparturesAnywhere()
        {
            // 음식점 없는 월드 — 어떤 마을도 이탈하지 않는다(예외 없음 — RestaurantSystemTests.RunAllDaily 패턴 준용)
            Assert.DoesNotThrow(() => VillagerDepartureSystem.RunDailyDeparture(1));
            Assert.AreEqual(0, VillageDrugState.GetSnapshot(NationType.East, 0).departedVillagers);
            Assert.AreEqual(0, VillageDrugState.GetSnapshot(NationType.North, 19).departedVillagers);
        }

        [Test]
        public void RunDailyDeparture_DeterministicRoll_LinkedToRecordDeparture()
        {
            // 결정론 계약 검증 — 테스트가 동일 시드 파생/롤 규약을 미러링해 RecordDeparture 연계를 확인한다.
            TerritoryDatabase db = TerritoryDatabase.Instance;
            VillageDrugState.SetRestaurantBuilt(NationType.East, 0, true);
            VillageDrugState.AddAddiction(NationType.East, 0, 100f);

            // East_1 영주 상태/성격 — [실측] LordInfo.personality enum → int 캐스팅
            // (Neutral=0..Cruel=6 — TerritoryData.cs 선언 순서 = VillageDrugMath 실측 순서)
            TerritoryState east1State = db.GetState(new TerritoryId(NationType.East, 1));
            TerritoryDefinition east1Def = db.GetDefinition(new TerritoryId(NationType.East, 1));
            float chance = VillageDrugMath.DepartureChance(100f, east1State.lordViolent, (int)east1Def.lord.personality);
            double roll = new System.Random(VillagerDepartureSystem.DeriveVillageSeed(77, NationType.East, 0)).NextDouble();
            int expected = roll < chance ? 1 : 0;

            VillagerDepartureSystem.RunDailyDeparture(77);
            Assert.AreEqual(expected, VillageDrugState.GetSnapshot(NationType.East, 0).departedVillagers,
                $"roll={roll:0.000} chance={chance:0.000} — 롤 규약과 RecordDeparture 연계 불일치");

            // 결정론 재현 — 상태 초기화 후 동일 시드 재실행 시 동일 결과
            VillageDrugState.ResetAll();
            VillageDrugState.SetRestaurantBuilt(NationType.East, 0, true);
            VillageDrugState.AddAddiction(NationType.East, 0, 100f);
            VillagerDepartureSystem.RunDailyDeparture(77);
            Assert.AreEqual(expected, VillageDrugState.GetSnapshot(NationType.East, 0).departedVillagers, "동일 시드 재실행 — 결정론 위반");
        }

        [Test]
        public void RunDailyDeparture_NonRestaurantVillage_SkipsEvenWhenAddicted()
        {
            // 음식점 미건설 마을 — 중독이 있어도 이탈 판정 대상 아님(스펙: 음식점 마을만)
            VillageDrugState.AddAddiction(NationType.West, 0, 100f);
            VillagerDepartureSystem.RunDailyDeparture(5);
            Assert.AreEqual(0, VillageDrugState.GetSnapshot(NationType.West, 0).departedVillagers);
        }

        // ===== LordViolenceSystem =====

        [Test]
        public void IsTaxCollectionDeficient_PureThreshold()
        {
            // 수취 < 기준 × 0.6 — 정수 연산 판정(70 × 0.6 = 42) [D4] 임계 0.5 → 0.6 상향(감독자 결정 2026-10-15)
            Assert.IsTrue(IsDeficient(41, 70));  // 41 < 42 — 0.6 미달
            Assert.IsFalse(IsDeficient(42, 70)); // 42 = 기준 × 0.6 — 엄격 미달 아님
            Assert.IsFalse(IsDeficient(49, 70)); // 계수 0.7 수취 — 정상
            Assert.IsFalse(IsDeficient(70, 70)); // 풀세수
            Assert.IsTrue(IsDeficient(0, 70));   // 무수취
        }

        /// <summary>LordViolenceSystem.IsTaxCollectionDeficient 위임 래퍼(가독성용).</summary>
        private static bool IsDeficient(int collected, int standard)
            => LordViolenceSystem.IsTaxCollectionDeficient(collected, standard);

        [Test]
        public void ApplyTaxDeficiencyToState_TurnsViolent_AndPenalizesLoyalty()
        {
            var state = new TerritoryState(new TerritoryId(NationType.East, 1)) { loyaltyToPlayer = 50f };

            bool turned = LordViolenceSystem.ApplyTaxDeficiencyToState(state, 34, 70, "East_01");

            Assert.IsTrue(turned, "미달 수취(34 < 42 = 기준 × 0.6) — 폭력화돼야 함");
            Assert.IsTrue(state.lordViolent, "lordViolent 플래그 설정");
            Assert.AreEqual(40f, state.loyaltyToPlayer, Epsilon, "충성도 −10");
        }

        [Test]
        public void ApplyTaxDeficiencyToState_AlreadyViolent_MaintainedNoDoublePenalty()
        {
            // 세탁/복구 없이 유지 — 재평가해도 플래그 유지 + 중복 페널티 없음
            var state = new TerritoryState(new TerritoryId(NationType.East, 1))
            {
                lordViolent = true,
                loyaltyToPlayer = 40f,
            };

            bool turned = LordViolenceSystem.ApplyTaxDeficiencyToState(state, 0, 70, "East_01");

            Assert.IsFalse(turned);
            Assert.IsTrue(state.lordViolent, "폭력화 유지");
            Assert.AreEqual(40f, state.loyaltyToPlayer, Epsilon, "중복 페널티 없음");
        }

        [Test]
        public void ApplyTaxDeficiencyToState_PlayerHostile_Excluded()
        {
            // playerHostile 영지 제외 — D3 발각 시스템 소관 (스펙 제외 조건)
            var state = new TerritoryState(new TerritoryId(NationType.East, 1))
            {
                playerHostile = true,
                loyaltyToPlayer = 50f,
            };

            bool turned = LordViolenceSystem.ApplyTaxDeficiencyToState(state, 0, 70, "East_01");

            Assert.IsFalse(turned);
            Assert.IsFalse(state.lordViolent, "playerHostile 영지는 폭력화 제외");
            Assert.AreEqual(50f, state.loyaltyToPlayer, Epsilon, "충성도 무변경");
        }

        [Test]
        public void ApplyTaxDeficiencyToState_NotDeficient_NoChange()
        {
            var state = new TerritoryState(new TerritoryId(NationType.East, 1)) { loyaltyToPlayer = 50f };

            bool turned = LordViolenceSystem.ApplyTaxDeficiencyToState(state, 49, 70, "East_01");

            Assert.IsFalse(turned);
            Assert.IsFalse(state.lordViolent);
            Assert.AreEqual(50f, state.loyaltyToPlayer, Epsilon);
        }

        [Test]
        public void ApplyTaxDeficiencyToState_ZeroLoyalty_ClampsAtZero()
        {
            // loyaltyToPlayer 접근자 클램프(0..100) — 0에서 −10도 0 유지(음수 방어 실측)
            var state = new TerritoryState(new TerritoryId(NationType.East, 1)) { loyaltyToPlayer = 0f };

            bool turned = LordViolenceSystem.ApplyTaxDeficiencyToState(state, 10, 70, "East_01");

            Assert.IsTrue(turned);
            Assert.IsTrue(state.lordViolent);
            Assert.AreEqual(0f, state.loyaltyToPlayer, Epsilon, "0 하한 클램프");
        }

        [Test]
        public void ApplyTaxDeficiencyToState_NullState_Safe()
        {
            Assert.DoesNotThrow(() => LordViolenceSystem.ApplyTaxDeficiencyToState(null, 0, 70, "null"));
        }
    }
}
