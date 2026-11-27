using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Utils;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [2026-10-17 100차 P1 — 음식점 가격·수요 순수 수학] 계약 테스트 (RestaurantDemandMath.cs).
    /// - VisitorDemandFactor: 가격&lt;기준가 → &gt;1.0 / 등가=1.0 / 가격&gt;기준가 → &lt;1.0 /
    ///   비율 클램프 [0.4, 2.0] / 무료(price&lt;=0)=최대 2.0(0.4 하한 예외 — 무료=최대 수요).
    /// - VisitorRatio: 기본 방문율 0.12×계수 / 클램프 [0.03, 0.5](정수 방문 수 반올림은 호출부 책임).
    /// - GradeBasePrice: 등급 배수(Common 1.0/Uncommon 1.3/Rare 1.7/Epic 2.2/Legendary 3.0) 반올림 /
    ///   무가격(&lt;=0) → EconomyPricing 등급표 폴백 그대로 / 결정론(동일 입력 동일 출력, Random 미사용).
    /// 실행 판정은 QA 서브에이전트 증거로 확정(구현 서브에이전트는 실행하지 않음).
    /// </summary>
    public sealed class RestaurantDemandMathTests
    {
        private const float Tol = 0.0001f; // float 수요 계수/비율 비교 허용 오차

        // ════════════════════════════════════════════════════════════════════
        // 1. VisitorDemandFactor — 가격 대비 수요 계수
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void VisitorDemandFactor_PriceEqualsBase_ReturnsOne()
        {
            Assert.That(RestaurantDemandMath.VisitorDemandFactor(10, 10), Is.EqualTo(1.0f).Within(Tol),
                "기준가 등가 — 수요 계수 1.0(중립).");
        }

        [Test]
        public void VisitorDemandFactor_PriceBelowBase_IncreasesDemand()
        {
            float factor = RestaurantDemandMath.VisitorDemandFactor(5, 10);
            Assert.That(factor, Is.GreaterThan(1.0f), "가격<기준가 → 수요 증가(>1.0).");
            Assert.That(factor, Is.LessThanOrEqualTo(2.0f), "계수는 상한 2.0을 넘지 않는다(클램프).");
        }

        [Test]
        public void VisitorDemandFactor_PriceAboveBase_DecreasesDemand()
        {
            float factor = RestaurantDemandMath.VisitorDemandFactor(20, 10);
            Assert.That(factor, Is.LessThan(1.0f), "가격>기준가 → 수요 감소(<1.0).");
            Assert.That(factor, Is.GreaterThanOrEqualTo(0.4f), "계수는 하한 0.4 미만으로 내려가지 않는다(클램프).");
        }

        [Test]
        public void VisitorDemandFactor_RatioClampedToFortyToTwoHundredPercent()
        {
            Assert.That(RestaurantDemandMath.VisitorDemandFactor(1, 10), Is.EqualTo(2.0f).Within(Tol),
                "비율 10.0 → 상한 클램프 2.0.");
            Assert.That(RestaurantDemandMath.VisitorDemandFactor(1000, 10), Is.EqualTo(0.4f).Within(Tol),
                "비율 0.01 → 하한 클램프 0.4.");
        }

        [Test]
        public void VisitorDemandFactor_FreeOrNegativePrice_ReturnsMaxDemand()
        {
            Assert.That(RestaurantDemandMath.VisitorDemandFactor(0, 10), Is.EqualTo(2.0f).Within(Tol),
                "무료(0원) = 최대 수요 — 0.4 하한 예외(규약: 실정산가는 호출부가 basePrice 유지).");
            Assert.That(RestaurantDemandMath.VisitorDemandFactor(-3, 10), Is.EqualTo(2.0f).Within(Tol),
                "음수 가격도 무료 취급 — 최대 수요.");
        }

        [Test]
        public void VisitorDemandFactor_Deterministic()
        {
            Assert.That(RestaurantDemandMath.VisitorDemandFactor(7, 12),
                Is.EqualTo(RestaurantDemandMath.VisitorDemandFactor(7, 12)),
                "동일 입력 → 항상 동일 계수(결정론 — Random 미사용).");
        }

        // ════════════════════════════════════════════════════════════════════
        // 2. VisitorRatio — 기본 방문율 × 계수 (클램프 [0.03, 0.5])
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void VisitorRatio_NeutralFactor_BaseTwelvePercent()
        {
            Assert.That(RestaurantDemandMath.VisitorRatio(8, 1.0f), Is.EqualTo(0.12f).Within(Tol),
                "기본 방문율 0.12 × 계수 1.0 = 0.12.");
        }

        [Test]
        public void VisitorRatio_ClampedToThreeToFiftyPercent()
        {
            Assert.That(RestaurantDemandMath.VisitorRatio(8, 0.2f), Is.EqualTo(0.03f).Within(Tol),
                "0.12×0.2=0.024 → 하한 클램프 0.03.");
            Assert.That(RestaurantDemandMath.VisitorRatio(8, 10f), Is.EqualTo(0.5f).Within(Tol),
                "0.12×10=1.2 → 상한 클램프 0.5.");
            Assert.That(RestaurantDemandMath.VisitorRatio(8, 0.5f), Is.EqualTo(0.06f).Within(Tol),
                "0.12×0.5=0.06 — 밴드 내부는 그대로 통과.");
        }

        [Test]
        public void VisitorRatio_Deterministic()
        {
            Assert.That(RestaurantDemandMath.VisitorRatio(12, 1.7f),
                Is.EqualTo(RestaurantDemandMath.VisitorRatio(12, 1.7f)),
                "동일 입력 → 항상 동일 방문율(결정론).");
        }

        // ════════════════════════════════════════════════════════════════════
        // 3. GradeBasePrice — 등급 배수 / 등급표 폴백
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void GradeBasePrice_RarityMultipliers_AppliedAndRounded()
        {
            Assert.That(RestaurantDemandMath.GradeBasePrice(ItemRarity.Common, 10), Is.EqualTo(10),
                "Common ×1.0.");
            Assert.That(RestaurantDemandMath.GradeBasePrice(ItemRarity.Uncommon, 10), Is.EqualTo(13),
                "Uncommon ×1.3.");
            Assert.That(RestaurantDemandMath.GradeBasePrice(ItemRarity.Rare, 10), Is.EqualTo(17),
                "Rare ×1.7.");
            Assert.That(RestaurantDemandMath.GradeBasePrice(ItemRarity.Epic, 10), Is.EqualTo(22),
                "Epic ×2.2.");
            Assert.That(RestaurantDemandMath.GradeBasePrice(ItemRarity.Legendary, 10), Is.EqualTo(30),
                "Legendary ×3.0.");
        }

        [Test]
        public void GradeBasePrice_ZeroOrNegativeBasePrice_FallsBackToEconomyGradeTable()
        {
            Assert.That(RestaurantDemandMath.GradeBasePrice(ItemRarity.Common, 0), Is.EqualTo(20),
                "무가격 폴백 — EconomyPricing 등급표 Common 20.");
            Assert.That(RestaurantDemandMath.GradeBasePrice(ItemRarity.Uncommon, 0), Is.EqualTo(60),
                "무가격 폴백 — Uncommon 60.");
            Assert.That(RestaurantDemandMath.GradeBasePrice(ItemRarity.Rare, 0), Is.EqualTo(150),
                "무가격 폴백 — Rare 150.");
            Assert.That(RestaurantDemandMath.GradeBasePrice(ItemRarity.Epic, 0), Is.EqualTo(400),
                "무가격 폴백 — Epic 400.");
            Assert.That(RestaurantDemandMath.GradeBasePrice(ItemRarity.Legendary, 0), Is.EqualTo(1000),
                "무가격 폴백 — Legendary 1000.");
            Assert.That(RestaurantDemandMath.GradeBasePrice(ItemRarity.Unique, 0), Is.EqualTo(2500),
                "무가격 폴백 — Unique 2500.");
            Assert.That(RestaurantDemandMath.GradeBasePrice(ItemRarity.Common, -5), Is.EqualTo(20),
                "음수 무가격도 폴백 경로(<=0 규약).");
        }

        [Test]
        public void GradeBasePrice_Deterministic()
        {
            Assert.That(RestaurantDemandMath.GradeBasePrice(ItemRarity.Rare, 7),
                Is.EqualTo(RestaurantDemandMath.GradeBasePrice(ItemRarity.Rare, 7)),
                "동일 입력 → 항상 동일 가격(결정론 — Random 미사용).");
        }
    }
}
