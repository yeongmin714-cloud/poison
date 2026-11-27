using NUnit.Framework;
using ProjectName.Core.Utils;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [2026-10-15 D3-B — 음식점 일일 판매 순수 수학] 계약 테스트 (RestaurantSaleMath.cs).
    /// - PlanDailySale: 마약 우선 소진(수요 한도 내 마약 먼저)/골드 합산(마약=등록가, 일반=기본가)/
    ///   수요 상한(판매 ≤ 수요·재고)/수요 계수 floor 적용/방어(수요 0·재고 음수·가격 0 이하 → 무료 판매 금지).
    /// - DemandCount: 중독 단조 증가/이탈 주민 차감(단조 감소·잔존 0이면 0)/수요율 경계(0.2~0.7)/
    ///   지터 ±1(주입값 기반 결정론 — 0 수요에 지터로 가짜 수요 생성 금지)/동일 입력 동일 출력.
    /// 실행 판정은 QA 서브에이전트 증거로 확정(구현 서브에이전트는 실행하지 않음).
    /// </summary>
    public sealed class RestaurantSaleMathTests
    {
        // ════════════════════════════════════════════════════════════════════
        // 1. PlanDailySale — 마약 우선 소진 (결정론 규약)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void PlanDailySale_DemandWithinBothStocks_DruggedConsumedFirst()
        {
            // 수요 7 < 마약 5 + 일반 10 — 마약 5선 소진 후 일반 2
            (int soldNormal, int soldDrugged, int gold) = RestaurantSaleMath.PlanDailySale(
                normalStock: 10, druggedStock: 5, demandCount: 7,
                registeredPrice: 30, defaultBasePrice: 10, demandFactor: 1f);

            Assert.That(soldDrugged, Is.EqualTo(5), "수요 한도 내에서 마약 음식이 먼저 소진되어야 한다(규약).");
            Assert.That(soldNormal, Is.EqualTo(2), "마약 소진 후 잔여 수요만큼 일반 음식이 판매되어야 한다.");
            Assert.That(gold, Is.EqualTo(5 * 30 + 2 * 10), "골드 = 마약×등록가 + 일반×기본가.");
        }

        [Test]
        public void PlanDailySale_DemandBelowDruggedStock_NoNormalSale()
        {
            // 수요 3 ≤ 마약 5 — 전부 마약, 일반 0 (중독 유지 인센티브)
            (int soldNormal, int soldDrugged, int gold) = RestaurantSaleMath.PlanDailySale(
                normalStock: 10, druggedStock: 5, demandCount: 3,
                registeredPrice: 30, defaultBasePrice: 10, demandFactor: 1f);

            Assert.That(soldDrugged, Is.EqualTo(3));
            Assert.That(soldNormal, Is.EqualTo(0), "수요가 마약 재고로 충족되면 일반 음식은 팔지 않아야 한다.");
            Assert.That(gold, Is.EqualTo(90));
        }

        [Test]
        public void PlanDailySale_DemandExceedsBothStocks_SellsAllStock()
        {
            // 수요 100 ≫ 재고 — 마약 3 + 일반 2 전량 판매
            (int soldNormal, int soldDrugged, int gold) = RestaurantSaleMath.PlanDailySale(
                normalStock: 2, druggedStock: 3, demandCount: 100,
                registeredPrice: 30, defaultBasePrice: 10, demandFactor: 1f);

            Assert.That(soldDrugged, Is.EqualTo(3));
            Assert.That(soldNormal, Is.EqualTo(2));
            Assert.That(gold, Is.EqualTo(3 * 30 + 2 * 10), "재고 상한 — 보유량까지만 판매.");
        }

        [Test]
        public void PlanDailySale_NoDruggedStock_SellsNormalOnly()
        {
            (int soldNormal, int soldDrugged, int gold) = RestaurantSaleMath.PlanDailySale(
                normalStock: 4, druggedStock: 0, demandCount: 3,
                registeredPrice: 30, defaultBasePrice: 10, demandFactor: 1f);

            Assert.That(soldDrugged, Is.EqualTo(0));
            Assert.That(soldNormal, Is.EqualTo(3));
            Assert.That(gold, Is.EqualTo(30));
        }

        // ════════════════════════════════════════════════════════════════════
        // 2. PlanDailySale — 수요 계수 / 방어 규약
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void PlanDailySale_DemandFactorScalesDemandDown_Floored()
        {
            // 유효 수요 = floor(7 × 0.5) = 3 — 전부 마약에서 소진
            (int soldNormal, int soldDrugged, int _) = RestaurantSaleMath.PlanDailySale(
                normalStock: 10, druggedStock: 5, demandCount: 7,
                registeredPrice: 10, defaultBasePrice: 10, demandFactor: 0.5f);

            Assert.That(soldDrugged, Is.EqualTo(3), "수요 계수는 floor로 적용된다.");
            Assert.That(soldNormal, Is.EqualTo(0));
        }

        [Test]
        public void PlanDailySale_ZeroDemand_ReturnsZeros()
        {
            (int normal0, int drugged0, int gold0) = RestaurantSaleMath.PlanDailySale(10, 5, 0, 30, 10, 1f);
            Assert.That(normal0, Is.EqualTo(0), "수요 0 → 일반 판매 없음.");
            Assert.That(drugged0, Is.EqualTo(0), "수요 0 → 마약 판매 없음.");
            Assert.That(gold0, Is.EqualTo(0), "수요 0 → 수입 없음.");

            (int normalNeg, int druggedNeg, int goldNeg) = RestaurantSaleMath.PlanDailySale(10, 5, -3, 30, 10, 1f);
            Assert.That(normalNeg, Is.EqualTo(0), "수요 음수 → 일반 판매 없음(방어).");
            Assert.That(druggedNeg, Is.EqualTo(0), "수요 음수 → 마약 판매 없음(방어).");
            Assert.That(goldNeg, Is.EqualTo(0), "수요 음수 → 수입 없음(방어).");
        }

        [Test]
        public void PlanDailySale_NegativeStocks_DefendedToZero()
        {
            (int soldNormal, int soldDrugged, int gold) = RestaurantSaleMath.PlanDailySale(
                normalStock: -5, druggedStock: -5, demandCount: 7,
                registeredPrice: 30, defaultBasePrice: 10, demandFactor: 1f);

            Assert.That(soldDrugged, Is.EqualTo(0), "재고 음수는 0 취급(방어).");
            Assert.That(soldNormal, Is.EqualTo(0));
            Assert.That(gold, Is.EqualTo(0));
        }

        [Test]
        public void PlanDailySale_ZeroRegisteredPrice_NoDrugSale_NoFreeSale()
        {
            // 등록가 0(미등록) → 마약 무료 판매 금지 — 일반만 기본가로 판매
            (int soldNormal, int soldDrugged, int gold) = RestaurantSaleMath.PlanDailySale(
                normalStock: 10, druggedStock: 10, demandCount: 5,
                registeredPrice: 0, defaultBasePrice: 10, demandFactor: 1f);

            Assert.That(soldDrugged, Is.EqualTo(0), "등록가 0(미등록)이면 마약 음식을 팔지 않는다(무료 판매 방지).");
            Assert.That(soldNormal, Is.EqualTo(5), "일반 음식은 기본가로 정상 판매된다.");
            Assert.That(gold, Is.EqualTo(50));
        }

        [Test]
        public void PlanDailySale_ZeroBasePrice_NoNormalSale_NoFreeSale()
        {
            // 기본가 0 → 일반 무료 판매 금지 — 마약만 등록가로 판매
            (int soldNormal, int soldDrugged, int gold) = RestaurantSaleMath.PlanDailySale(
                normalStock: 10, druggedStock: 3, demandCount: 5,
                registeredPrice: 30, defaultBasePrice: 0, demandFactor: 1f);

            Assert.That(soldDrugged, Is.EqualTo(3));
            Assert.That(soldNormal, Is.EqualTo(0), "기본가 0이면 일반 음식을 팔지 않는다(무료 판매 방지).");
            Assert.That(gold, Is.EqualTo(90));
        }

        [Test]
        public void PlanDailySale_ZeroDemandFactor_SellsNothing()
        {
            (int normal0f, int drugged0f, int gold0f) = RestaurantSaleMath.PlanDailySale(10, 5, 7, 30, 10, 0f);
            Assert.That(normal0f, Is.EqualTo(0), "수요 계수 0 → 유효 수요 0 → 일반 판매 없음.");
            Assert.That(drugged0f, Is.EqualTo(0), "수요 계수 0 → 유효 수요 0 → 마약 판매 없음.");
            Assert.That(gold0f, Is.EqualTo(0), "수요 계수 0 → 수입 없음.");

            (int normalNeg, int druggedNeg, int goldNeg) = RestaurantSaleMath.PlanDailySale(10, 5, 7, 30, 10, -1f);
            Assert.That(normalNeg, Is.EqualTo(0), "수요 계수 음수 → 0 취급(방어).");
            Assert.That(druggedNeg, Is.EqualTo(0), "수요 계수 음수 → 0 취급(방어).");
            Assert.That(goldNeg, Is.EqualTo(0), "수요 계수 음수 → 수입 없음(방어).");
        }

        // ════════════════════════════════════════════════════════════════════
        // 3. DemandCount — 중독 단조 / 이탈 차감 / 결정론
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void DemandCount_MonotonicallyNonDecreasingInAddiction()
        {
            // 같은 지터값(0.7 → +1)에서 중독이 클수록 수요가 감소하지 않는다.
            int prev = RestaurantSaleMath.DemandCount(20, 0f, 0, 0.7);
            for (int addiction = 25; addiction <= 100; addiction += 25)
            {
                int current = RestaurantSaleMath.DemandCount(20, addiction, 0, 0.7);
                Assert.That(current, Is.GreaterThanOrEqualTo(prev),
                    $"중독 {addiction}% — 수요는 중독에 대해 단조 비감소여야 한다.");
                prev = current;
            }
        }

        [Test]
        public void DemandCount_DepartedVillagersReduceDemand()
        {
            int none = RestaurantSaleMath.DemandCount(10, 50f, 0, 0.7);
            int some = RestaurantSaleMath.DemandCount(10, 50f, 3, 0.7);
            int all = RestaurantSaleMath.DemandCount(10, 50f, 10, 0.7);

            Assert.That(some, Is.LessThanOrEqualTo(none), "이탈 주민은 수요를 감소시킨다(단조).");
            Assert.That(all, Is.EqualTo(0), "전원 이탈 → 잔존 0 → 수요 0(지터로 가짜 수요 생성 금지).");
        }

        [Test]
        public void DemandCount_ZeroVillagersOrNegativeDeparted_Defended()
        {
            Assert.That(RestaurantSaleMath.DemandCount(0, 80f, 0, 0.9), Is.EqualTo(0),
                "주민 0 마을 → 수요 0.");
            Assert.That(RestaurantSaleMath.DemandCount(5, 50f, -3, 0.9),
                Is.GreaterThanOrEqualTo(0), "이탈 수 음수는 0 취급(방어) — 결과는 비음수.");
        }

        [Test]
        public void DemandCount_AddictionRateBounds_TwentyToSeventyPercent()
        {
            // 수요율 규약: 중독 0 → 잔존 × 0.2, 중독 100 → 잔존 × 0.7.
            // 지터값 0.5는 "< 0.5"가 아니므로 +1 분기(경계 동작 문서화).
            int sober = RestaurantSaleMath.DemandCount(100, 0f, 0, 0.5);
            int wasted = RestaurantSaleMath.DemandCount(100, 100f, 0, 0.5);

            Assert.That(sober, Is.EqualTo(21), "중독 0: round(100×0.2)=20 + 지터(+1) = 21.");
            Assert.That(wasted, Is.EqualTo(71), "중독 100: round(100×0.7)=70 + 지터(+1) = 71.");
        }

        [Test]
        public void DemandCount_JitterShiftsByAtMostOne_AndDeterministic()
        {
            int withLowRoll = RestaurantSaleMath.DemandCount(10, 0f, 0, 0.0);   // 지터 −1
            int withHighRoll = RestaurantSaleMath.DemandCount(10, 0f, 0, 0.9);  // 지터 +1

            Assert.That(withLowRoll, Is.EqualTo(1), "round(10×0.2)=2 − 1 = 1.");
            Assert.That(withHighRoll, Is.EqualTo(3), "round(10×0.2)=2 + 1 = 3.");
            Assert.That(withHighRoll - withLowRoll, Is.EqualTo(2),
                "지터는 ±1 — 동일 기저 수요에서 롤값 양극단 차이는 최대 2.");

            // 결정론 — 동일 입력(동일 주입 난수) → 동일 출력
            Assert.That(RestaurantSaleMath.DemandCount(10, 42f, 1, 0.37),
                Is.EqualTo(RestaurantSaleMath.DemandCount(10, 42f, 1, 0.37)),
                "동일 입력은 항상 동일 수요(주입값 기반 결정론).");
        }
    }
}
