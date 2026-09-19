using NUnit.Framework;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C-O2-01: EconomyAuditLedger 정적 로직 테스트.
    /// 카테고리별 수입/지출 집계, 복수 카테고리 분리, 순유입, 시간당 순유입(0분 가드), 리셋, 리포트 문자열.
    /// </summary>
    public class EconomyAuditTests
    {
        [SetUp]
        public void SetUp()
        {
            EconomyAuditLedger.ResetAudit();
        }

        [TearDown]
        public void TearDown()
        {
            EconomyAuditLedger.ResetAudit();
        }

        // ── RecordIncome: 카테고리 집계 ─────────────────────────────────────

        [Test]
        public void RecordIncome_SingleCategory_Aggregates()
        {
            EconomyAuditLedger.RecordIncome("quest_reward", 100);
            EconomyAuditLedger.RecordIncome("quest_reward", 250);

            Assert.AreEqual(350, EconomyAuditLedger.GetIncome("quest_reward"));
            Assert.AreEqual(0, EconomyAuditLedger.GetOutflow("quest_reward"));
            Assert.AreEqual(350, EconomyAuditLedger.GetTotalIncome());
        }

        [Test]
        public void RecordOutflow_SingleCategory_Aggregates()
        {
            EconomyAuditLedger.RecordOutflow("arena_fee", 50);
            EconomyAuditLedger.RecordOutflow("arena_fee", 75);

            Assert.AreEqual(125, EconomyAuditLedger.GetOutflow("arena_fee"));
            Assert.AreEqual(0, EconomyAuditLedger.GetIncome("arena_fee"));
            Assert.AreEqual(125, EconomyAuditLedger.GetTotalOutflow());
        }

        [Test]
        public void RecordIncomeAndOutflow_SameCategory_TrackedIndependently()
        {
            // 같은 카테고리에서 수입/지출이 동시에 발생 (예: merchant 구매/환불)
            EconomyAuditLedger.RecordIncome("merchant_refund", 120);
            EconomyAuditLedger.RecordOutflow("merchant_refund", 80);
            EconomyAuditLedger.RecordIncome("merchant_refund", 30);

            Assert.AreEqual(150, EconomyAuditLedger.GetIncome("merchant_refund"));
            Assert.AreEqual(80, EconomyAuditLedger.GetOutflow("merchant_refund"));
            Assert.AreEqual(150, EconomyAuditLedger.GetTotalIncome());
            Assert.AreEqual(80, EconomyAuditLedger.GetTotalOutflow());
        }

        // ── 복수 카테고리 분리 ──────────────────────────────────────────────

        [Test]
        public void MultipleCategories_TrackedSeparately()
        {
            EconomyAuditLedger.RecordIncome("quest_reward", 100);
            EconomyAuditLedger.RecordIncome("drug_effect", 50);
            EconomyAuditLedger.RecordIncome("smuggle_sale", 200);
            EconomyAuditLedger.RecordOutflow("arena_fee", 30);
            EconomyAuditLedger.RecordOutflow("church_donation", 70);

            Assert.AreEqual(100, EconomyAuditLedger.GetIncome("quest_reward"));
            Assert.AreEqual(50, EconomyAuditLedger.GetIncome("drug_effect"));
            Assert.AreEqual(200, EconomyAuditLedger.GetIncome("smuggle_sale"));
            Assert.AreEqual(30, EconomyAuditLedger.GetOutflow("arena_fee"));
            Assert.AreEqual(70, EconomyAuditLedger.GetOutflow("church_donation"));
            Assert.AreEqual(350, EconomyAuditLedger.GetTotalIncome());
            Assert.AreEqual(100, EconomyAuditLedger.GetTotalOutflow());
        }

        [Test]
        public void GetIncome_UnknownSource_ReturnsZero()
        {
            EconomyAuditLedger.RecordIncome("quest_reward", 100);

            Assert.AreEqual(0, EconomyAuditLedger.GetIncome("unknown_source"));
            Assert.AreEqual(0, EconomyAuditLedger.GetOutflow("unknown_source"));
        }

        [Test]
        public void RecordIncome_NonPositiveAmount_Ignored()
        {
            EconomyAuditLedger.RecordIncome("quest_reward", 0);
            EconomyAuditLedger.RecordIncome("quest_reward", -10);

            Assert.AreEqual(0, EconomyAuditLedger.GetIncome("quest_reward"));
            Assert.AreEqual(0, EconomyAuditLedger.GetTotalIncome());
        }

        [Test]
        public void RecordOutflow_NonPositiveAmount_Ignored()
        {
            EconomyAuditLedger.RecordOutflow("arena_fee", 0);
            EconomyAuditLedger.RecordOutflow("arena_fee", -10);

            Assert.AreEqual(0, EconomyAuditLedger.GetOutflow("arena_fee"));
            Assert.AreEqual(0, EconomyAuditLedger.GetTotalOutflow());
        }

        // ── 순유입 / 시간당 순유입 ──────────────────────────────────────────

        [Test]
        public void GetNet_IncomeMinusOutflow()
        {
            EconomyAuditLedger.RecordIncome("quest_reward", 600);
            EconomyAuditLedger.RecordOutflow("arena_fee", 200);

            Assert.AreEqual(400, EconomyAuditLedger.GetNet());
        }

        [Test]
        public void GetNet_DeficitIsNegative()
        {
            EconomyAuditLedger.RecordIncome("quest_reward", 100);
            EconomyAuditLedger.RecordOutflow("arena_fee", 300);

            Assert.AreEqual(-200, EconomyAuditLedger.GetNet());
        }

        [Test]
        public void GetHourlyNetRate_ThirtyMinutes_CalculatesExpected()
        {
            // 수입 600 / 지출 200 / 30분 → (600-200)/30*60 = 800 G/h
            EconomyAuditLedger.RecordIncome("quest_reward", 600);
            EconomyAuditLedger.RecordOutflow("arena_fee", 200);

            Assert.AreEqual(800f, EconomyAuditLedger.GetHourlyNetRate(30f), 0.001f);
        }

        [Test]
        public void GetHourlyNetRate_ZeroMinutes_GuardReturnsZero()
        {
            EconomyAuditLedger.RecordIncome("quest_reward", 600);

            Assert.AreEqual(0f, EconomyAuditLedger.GetHourlyNetRate(0f), 0.001f);
        }

        [Test]
        public void GetHourlyNetRate_NegativeMinutes_GuardReturnsZero()
        {
            EconomyAuditLedger.RecordIncome("quest_reward", 600);

            Assert.AreEqual(0f, EconomyAuditLedger.GetHourlyNetRate(-5f), 0.001f);
        }

        [Test]
        public void GetHourlyNetRate_NetDeficit_NegativeRate()
        {
            EconomyAuditLedger.RecordIncome("quest_reward", 100);
            EconomyAuditLedger.RecordOutflow("arena_fee", 400);

            // (100-400)/60*60 = -300 G/h
            Assert.AreEqual(-300f, EconomyAuditLedger.GetHourlyNetRate(60f), 0.001f);
        }

        // ── 리셋 ────────────────────────────────────────────────────────────

        [Test]
        public void ResetAudit_ClearsCategoriesAndTotals()
        {
            EconomyAuditLedger.RecordIncome("quest_reward", 500);
            EconomyAuditLedger.RecordOutflow("arena_fee", 150);

            EconomyAuditLedger.ResetAudit();

            Assert.AreEqual(0, EconomyAuditLedger.GetIncome("quest_reward"));
            Assert.AreEqual(0, EconomyAuditLedger.GetOutflow("arena_fee"));
            Assert.AreEqual(0, EconomyAuditLedger.GetTotalIncome());
            Assert.AreEqual(0, EconomyAuditLedger.GetTotalOutflow());
            Assert.AreEqual(0, EconomyAuditLedger.GetNet());
        }

        // ── 리포트 문자열 ───────────────────────────────────────────────────

        [Test]
        public void GetReport_ContainsCategoriesAndTotals()
        {
            EconomyAuditLedger.RecordIncome("quest_reward", 600);
            EconomyAuditLedger.RecordOutflow("arena_fee", 200);

            string report = EconomyAuditLedger.GetReport(30f);

            Assert.IsTrue(report.Contains("quest_reward"), "리포트에 카테고리 quest_reward 포함");
            Assert.IsTrue(report.Contains("arena_fee"), "리포트에 카테고리 arena_fee 포함");
            Assert.IsTrue(report.Contains("600"), "리포트에 총수입 600 포함");
            Assert.IsTrue(report.Contains("200"), "리포트에 총지출 200 포함");
            Assert.IsTrue(report.Contains("400"), "리포트에 순유입 400 포함");
            Assert.IsTrue(report.Contains("800"), "리포트에 시간당 순유입 800 포함");
        }

        [Test]
        public void GetReport_ZeroSession_DoesNotThrow()
        {
            EconomyAuditLedger.RecordIncome("quest_reward", 100);

            Assert.DoesNotThrow(() => EconomyAuditLedger.GetReport(0f));
        }
    }
}
