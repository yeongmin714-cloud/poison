using NUnit.Framework;
using ProjectName.Core.Data;
using ProjectName.Core.Utils;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [2026-10-15 D2 — 음식점 마약 경제 순수 수학] 계약 테스트.
    /// - 발각 확률: 호감 하한(호감100+Low=0.15)/상한 클램프(호감0+Fatal≤0.95)/호감 반비례 단조/등급 가산 단조.
    /// - 적대 전환 확률: 등급별 0.40~0.65, 단조 증가, 범위 [0,1] (발각 별도 롤).
    /// - 권장가: 중독 0=기본가, 중독 100=기본가×2.5, 단조 증가, 최소 1.
    /// - 수요 계수: 권장가 이하=1.0, 과대 가격 단조 감소, 하한 0.2.
    /// - 중독 상승량: 6등급 상수표 전부 단조 증가 (판매 1건당 %p).
    /// - 이탈 확률: 중독0+비폭력+Neutral=0, 폭력 +0.20, 성격 가중(Cruel +0.10/Wise −0.10), 상한 0.9.
    /// - 수학은 확률/수치만 반환(결정론) — 발각/적대/이탈 판정 롤은 호출부(Systems)가 Random 수행.
    /// 실행 판정은 QA 서브에이전트 증거로 확정한다(구현 서브에이전트는 실행하지 않음).
    /// </summary>
    public sealed class VillageDrugMathTests
    {
        private const float Tol = 0.0001f; // 확률/수치 비교 허용 오차

        // ════════════════════════════════════════════════════════════════════
        // 0. 실측 불변 — enum 순서(수학 상수표가 enum 정수값에 의존, 재배열 시 조기 실패)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void EnumOrder_AddictionLevel_And_LordPersonality_MeasuredSequence()
        {
            // AddictionLevel (Core/Data/DrugDatabase.cs) 실측: Low=0, Medium=1, High=2, VeryHigh=3, Extreme=4, Fatal=5
            Assert.That((int)AddictionLevel.Low, Is.EqualTo(0), "AddictionLevel.Low=0 실측 불일치");
            Assert.That((int)AddictionLevel.Medium, Is.EqualTo(1), "AddictionLevel.Medium=1 실측 불일치");
            Assert.That((int)AddictionLevel.High, Is.EqualTo(2), "AddictionLevel.High=2 실측 불일치");
            Assert.That((int)AddictionLevel.VeryHigh, Is.EqualTo(3), "AddictionLevel.VeryHigh=3 실측 불일치");
            Assert.That((int)AddictionLevel.Extreme, Is.EqualTo(4), "AddictionLevel.Extreme=4 실측 불일치");
            Assert.That((int)AddictionLevel.Fatal, Is.EqualTo(5), "AddictionLevel.Fatal=5 실측 불일치");

            // LordPersonality (Core/Data/TerritoryData.cs) 실측: Neutral=0, Greedy=1, Suspicious=2, Brave=3, Cowardly=4, Wise=5, Cruel=6
            Assert.That((int)LordPersonality.Neutral, Is.EqualTo(0), "LordPersonality.Neutral=0 실측 불일치");
            Assert.That((int)LordPersonality.Greedy, Is.EqualTo(1), "LordPersonality.Greedy=1 실측 불일치");
            Assert.That((int)LordPersonality.Suspicious, Is.EqualTo(2), "LordPersonality.Suspicious=2 실측 불일치");
            Assert.That((int)LordPersonality.Brave, Is.EqualTo(3), "LordPersonality.Brave=3 실측 불일치");
            Assert.That((int)LordPersonality.Cowardly, Is.EqualTo(4), "LordPersonality.Cowardly=4 실측 불일치");
            Assert.That((int)LordPersonality.Wise, Is.EqualTo(5), "LordPersonality.Wise=5 실측 불일치");
            Assert.That((int)LordPersonality.Cruel, Is.EqualTo(6), "LordPersonality.Cruel=6 실측 불일치");
        }

        // ════════════════════════════════════════════════════════════════════
        // 1. 발각 확률 — 하한/상한/단조/클램프
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void DetectionProbability_Loyalty100_Low_ReturnsLowerBound()
        {
            // p = 0.15 + (1 − 1.00)×0.65 + 0×0.03 = 0.15 — 최호감·최약 마약의 최하 한계치
            float p = VillageDrugMath.DetectionProbability(100f, AddictionLevel.Low);
            Assert.That(p, Is.EqualTo(0.15f).Within(Tol), "호감100+Low는 발각 하한 0.15여야 한다.");
        }

        [Test]
        public void DetectionProbability_Loyalty0_Fatal_ClampedToMax095()
        {
            // raw = 0.15 + (1 − 0)×0.65 + 5×0.03 = 0.95 — 클램프 경계에서 정확히 0.95
            float p = VillageDrugMath.DetectionProbability(0f, AddictionLevel.Fatal);
            Assert.That(p, Is.EqualTo(0.95f).Within(Tol), "호감0+Fatal은 발각 상한 0.95(클램프 경계)여야 한다.");
            Assert.That(p, Is.LessThanOrEqualTo(0.95f), "발각 확률은 0.95를 초과할 수 없다.");
        }

        [Test]
        public void DetectionProbability_LoyaltyIncrease_MonotonicallyDecreases_SameGrade()
        {
            // 동일 등급(High)에서 호감↑ → 발각↓ (반비례 단조)
            float previous = float.MaxValue;
            foreach (float loyalty in new float[] { 0f, 25f, 50f, 75f, 100f })
            {
                float p = VillageDrugMath.DetectionProbability(loyalty, AddictionLevel.High);
                Assert.That(p, Is.LessThanOrEqualTo(previous + Tol),
                    $"동일 등급에서 호감 {loyalty}의 발각 확률은 직전 호감보다 높아선 안 된다.");
                previous = p;
            }
        }

        [Test]
        public void DetectionProbability_GradeIncrease_MonotonicallyIncreases_SameLoyalty()
        {
            // 동일 호감(50)에서 등급↑ → 발각↑ (가산 단조, gradeIdx당 +0.03)
            float previous = -1f;
            foreach (AddictionLevel level in new[]
            {
                AddictionLevel.Low, AddictionLevel.Medium, AddictionLevel.High,
                AddictionLevel.VeryHigh, AddictionLevel.Extreme, AddictionLevel.Fatal
            })
            {
                float p = VillageDrugMath.DetectionProbability(50f, level);
                Assert.That(p, Is.GreaterThanOrEqualTo(previous - Tol),
                    $"동일 호감에서 등급 {level}의 발각 확률은 직전 등급보다 낮아선 안 된다.");
                previous = p;
            }
        }

        [Test]
        public void DetectionProbability_AllLoyaltyAllGrades_AlwaysInsideClampRange()
        {
            // 전 조합 스윕 — 결과는 항상 [0, 0.95] 클램프 범위 안
            foreach (float loyalty in new float[] { -20f, 0f, 10f, 37.5f, 50f, 63f, 80f, 100f, 150f })
            {
                foreach (AddictionLevel level in System.Enum.GetValues(typeof(AddictionLevel)))
                {
                    float p = VillageDrugMath.DetectionProbability(loyalty, level);
                    Assert.That(p, Is.InRange(0f, 0.95f),
                        $"발각 확률 클램프 위반: loyalty={loyalty}, level={level}, p={p}.");
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // 2. 적대 전환 확률 — 등급별 기본 40% + 5%p
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void HostileConversionProbability_GradeIdx0Is040_And_GradeIdx5Is065()
        {
            Assert.That(VillageDrugMath.HostileConversionProbability(AddictionLevel.Low),
                Is.EqualTo(0.40f).Within(Tol), "Low(gradeIdx 0)는 적대 전환 0.40이어야 한다.");
            Assert.That(VillageDrugMath.HostileConversionProbability(AddictionLevel.Fatal),
                Is.EqualTo(0.65f).Within(Tol), "Fatal(gradeIdx 5)은 적대 전환 0.65이어야 한다.");
        }

        [Test]
        public void HostileConversionProbability_AllGrades_MonotonicAndInsideUnitRange()
        {
            float previous = -1f;
            foreach (AddictionLevel level in System.Enum.GetValues(typeof(AddictionLevel)))
            {
                float p = VillageDrugMath.HostileConversionProbability(level);
                Assert.That(p, Is.InRange(0f, 1f), $"적대 전환 확률은 [0,1]이어야 한다: {level}, p={p}.");
                Assert.That(p, Is.GreaterThanOrEqualTo(previous - Tol),
                    $"적대 전환 확률은 등급이 높을수록 낮아선 안 된다: {level}.");
                previous = p;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // 3. 권장가 — 중독 배율 ×1.5, 최소 1
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void RecommendedPrice_Addiction0EqualsBase_AndAddiction100Equals250Percent()
        {
            Assert.That(VillageDrugMath.RecommendedPrice(100, 0f),
                Is.EqualTo(100), "중독 0이면 권장가는 기본가와 같아야 한다.");
            Assert.That(VillageDrugMath.RecommendedPrice(100, 100f),
                Is.EqualTo(250), "중독 100이면 권장가는 기본가 ×2.5여야 한다.");
            // ceil 규약 확인: 7 × (1 + 0.5×1.5) = 12.25 → 13
            Assert.That(VillageDrugMath.RecommendedPrice(7, 50f),
                Is.EqualTo(13), "권장가는 올림(ceil)되어야 한다.");
        }

        [Test]
        public void RecommendedPrice_AddictionIncrease_MonotonicallyNonDecreasing_AndMinimum1()
        {
            float previous = 0;
            foreach (float addiction in new float[] { 0f, 10f, 25f, 40f, 55f, 70f, 85f, 100f })
            {
                int price = VillageDrugMath.RecommendedPrice(80, addiction);
                Assert.That(price, Is.GreaterThanOrEqualTo(previous),
                    $"중독 {addiction}의 권장가는 직전 중독도보다 낮아선 안 된다.");
                Assert.That(price, Is.GreaterThanOrEqualTo(1), "권장가는 최소 1골드여야 한다.");
                previous = price;
            }
            // 최소 1 방어 — 기본가 0/음수도 권장가 1
            Assert.That(VillageDrugMath.RecommendedPrice(0, 100f), Is.EqualTo(1),
                "기본가 0이라도 권장가는 최소 1이어야 한다.");
        }

        // ════════════════════════════════════════════════════════════════════
        // 4. 수요 계수 — 권장가 이하 1.0 / 과대 단조 감소 / 하한 0.2
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void DemandFactor_AtOrBelowRecommended_ReturnsFull1()
        {
            // 권장가 100 기준 — 이하(같음/미달)면 모두 풀수요 1.0
            Assert.That(VillageDrugMath.DemandFactor(100, 100), Is.EqualTo(1.0f).Within(Tol),
                "등록가 == 권장가면 수요 1.0이어야 한다.");
            Assert.That(VillageDrugMath.DemandFactor(60, 100), Is.EqualTo(1.0f).Within(Tol),
                "등록가 < 권장가면 수요 1.0이어야 한다.");
            Assert.That(VillageDrugMath.DemandFactor(0, 100), Is.EqualTo(1.0f).Within(Tol),
                "등록가 0(무료)도 수요 1.0이어야 한다.");
        }

        [Test]
        public void DemandFactor_Overpriced_MonotonicallyDecreases_WithLowerBound0p2()
        {
            // 권장가 100 초과 구간 — (초과분/100)×2.0 감소, 0.2 하한
            float previous = float.MaxValue;
            foreach (int playerPrice in new int[] { 110, 125, 150, 200, 500, 1000 })
            {
                float factor = VillageDrugMath.DemandFactor(playerPrice, 100);
                Assert.That(factor, Is.LessThanOrEqualTo(previous + Tol),
                    $"과대 가격 {playerPrice}의 수요는 직전 가격보다 높아선 안 된다.");
                Assert.That(factor, Is.GreaterThanOrEqualTo(0.2f - Tol),
                    $"수요 계수는 하한 0.2 미만으로 내려가선 안 된다: price={playerPrice}.");
                previous = factor;
            }
            // 계산값 검증: 125 → 1 − 25/100×2.0 = 0.5
            Assert.That(VillageDrugMath.DemandFactor(125, 100), Is.EqualTo(0.5f).Within(Tol),
                "과대 25%에서 수요는 0.5(1 − 0.5×2.0)여야 한다.");
            // 하한 클램프 검증: 150 → 1 − 50/100×2.0 = 0.0 → 0.2
            Assert.That(VillageDrugMath.DemandFactor(150, 100), Is.EqualTo(0.2f).Within(Tol),
                "과대 50% 이상은 수요 하한 0.2로 클램프되어야 한다.");
            // 권장가 0 방어 — 분모 max(권장가,1)
            Assert.That(VillageDrugMath.DemandFactor(5, 0), Is.EqualTo(0.2f).Within(Tol),
                "권장가 0인 경우에도 하한 0.2로 방어되어야 한다.");
        }

        // ════════════════════════════════════════════════════════════════════
        // 5. 중독 상승량 — 6등급 상수표 (판매 1건당 %p)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void AddictionGainPerSale_SixLevelTable_MatchesSpecAndMonotonic()
        {
            // 스펙 상수표 전부 — 판매 1건당 %p
            Assert.That(VillageDrugMath.AddictionGainPerSale(AddictionLevel.Low), Is.EqualTo(2.0f).Within(Tol), "Low는 +2.0%p");
            Assert.That(VillageDrugMath.AddictionGainPerSale(AddictionLevel.Medium), Is.EqualTo(4.5f).Within(Tol), "Medium은 +4.5%p");
            Assert.That(VillageDrugMath.AddictionGainPerSale(AddictionLevel.High), Is.EqualTo(8.0f).Within(Tol), "High는 +8.0%p");
            Assert.That(VillageDrugMath.AddictionGainPerSale(AddictionLevel.VeryHigh), Is.EqualTo(12.0f).Within(Tol), "VeryHigh는 +12.0%p");
            Assert.That(VillageDrugMath.AddictionGainPerSale(AddictionLevel.Extreme), Is.EqualTo(17.0f).Within(Tol), "Extreme는 +17.0%p");
            Assert.That(VillageDrugMath.AddictionGainPerSale(AddictionLevel.Fatal), Is.EqualTo(23.0f).Within(Tol), "Fatal은 +23.0%p");

            // 희귀할수록 중독 가속 — 등급 단조 증가
            float previous = -1f;
            foreach (AddictionLevel level in System.Enum.GetValues(typeof(AddictionLevel)))
            {
                float gain = VillageDrugMath.AddictionGainPerSale(level);
                Assert.That(gain, Is.GreaterThanOrEqualTo(previous + Tol),
                    $"중독 상승량은 등급이 높을수록 낮아선 안 된다: {level}.");
                previous = gain;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // 6. 이탈 확률 — 중독/폭력화/성격 가중, 상한 0.9
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void DepartureChance_ZeroAddictionNonViolentNeutral_IsZero()
        {
            // 중독 0 + 비폭력 + Neutral → 0.0 (평화로운 마을은 아무도 떠나지 않는다)
            float chance = VillageDrugMath.DepartureChance(0f, false, (int)LordPersonality.Neutral);
            Assert.That(chance, Is.EqualTo(0f).Within(Tol), "중독0+비폭력+Neutral의 이탈 확률은 0이어야 한다.");
        }

        [Test]
        public void DepartureChance_ViolentLord_AddsFlat020()
        {
            // 중독 50 + Neutral: 비폭력 0.15 / 폭력 0.35 — 폭력화 단독 가산 +0.20
            Assert.That(VillageDrugMath.DepartureChance(50f, false, (int)LordPersonality.Neutral),
                Is.EqualTo(0.15f).Within(Tol), "중독50+비폭력은 0.50×0.30=0.15여야 한다.");
            Assert.That(VillageDrugMath.DepartureChance(50f, true, (int)LordPersonality.Neutral),
                Is.EqualTo(0.35f).Within(Tol), "중독50+폭력은 0.15+0.20=0.35여야 한다.");
        }

        [Test]
        public void DepartureChance_PersonalityWeights_CruelPlus010_WiseMinus010()
        {
            // 중독 50 + 비폭력 기준(0.15): Cruel(6)=+0.10 → 0.25 / Wise(5)=−0.10 → 0.05
            Assert.That(VillageDrugMath.DepartureChance(50f, false, (int)LordPersonality.Cruel),
                Is.EqualTo(0.25f).Within(Tol), "Cruel 영주는 이탈 +0.10 가중이어야 한다.");
            Assert.That(VillageDrugMath.DepartureChance(50f, false, (int)LordPersonality.Wise),
                Is.EqualTo(0.05f).Within(Tol), "Wise 영주는 이탈 −0.10 가중이어야 한다.");
            // 그 외 성격은 0 가중 — 기준값과 동일
            foreach (int personalityIdx in new int[] { 0, 1, 2, 3, 4 })
            {
                Assert.That(VillageDrugMath.DepartureChance(50f, false, personalityIdx),
                    Is.EqualTo(0.15f).Within(Tol),
                    $"성격 인덱스 {personalityIdx}는 이탈 가중 0이어야 한다.");
            }
        }

        [Test]
        public void DepartureChance_ExtremeInputs_ClampedInside0To0p9()
        {
            // 최악 조합(중독 100 + 폭력 + Cruel)도 상한 0.9 안, 음수 중독도 0 이상
            float worst = VillageDrugMath.DepartureChance(100f, true, (int)LordPersonality.Cruel);
            Assert.That(worst, Is.EqualTo(0.60f).Within(Tol), "최악 조합은 0.30+0.20+0.10=0.60이어야 한다.");
            Assert.That(worst, Is.LessThanOrEqualTo(0.9f), "이탈 확률은 상한 0.9를 초과할 수 없다.");

            foreach (float addiction in new float[] { -10f, 0f, 25f, 50f, 75f, 100f, 130f })
            {
                foreach (int personalityIdx in new int[] { -1, 0, 3, 5, 6, 9 })
                {
                    float chance = VillageDrugMath.DepartureChance(addiction, true, personalityIdx);
                    Assert.That(chance, Is.InRange(0f, 0.9f),
                        $"이탈 확률 클램프 위반: addiction={addiction}, personality={personalityIdx}, p={chance}.");
                }
            }
        }
    }
}
