using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [121차] 아레나 관전 베팅 시스템 계약 테스트.
    /// 배율 산식(순수)/베팅 가드(0금액·중복·쌍 외 선수)/정산(승=금액×배율, 패=0)/기록 갱신.
    /// 런타임 전환·듀엔 코루틴은 Play 게이트(QAPROGRESS 121차).
    /// </summary>
    public class ArenaBettingTests
    {
        [SetUp]
        public void SetUp()
        {
            ArenaBettingSystem.ClearForTest();
        }

        [TearDown]
        public void TearDown()
        {
            ArenaBettingSystem.ClearForTest();
        }

        [Test]
        public void ComputeOdds_PowerRatio_Inverts_AndClamps()
        {
            // 동률 → 하우스 엣지 0.85/p(0.5) = 1.7
            Assert.That(ArenaBettingSystem.ComputeOdds(100f, 100f), Is.EqualTo(1.7f).Within(0.001f));
            // 약세(30%) → 0.85/0.3 = 2.83 → 0.05 스텝 반올림
            float weak = ArenaBettingSystem.ComputeOdds(60f, 140f);
            Assert.That(weak, Is.GreaterThan(2.5f).And.LessThanOrEqualTo(3.0f));
            // 강세(70%) → 0.85/0.7 ≈ 1.21
            float strong = ArenaBettingSystem.ComputeOdds(140f, 60f);
            Assert.That(strong, Is.GreaterThanOrEqualTo(1.2f).And.LessThan(1.5f));
            // 클램프 — 극단 위세 → 최소 1.2
            Assert.That(ArenaBettingSystem.ComputeOdds(1000f, 1f), Is.EqualTo(1.2f).Within(0.001f));
            // 클램프 — 극단 열세 → 최대 3.0
            Assert.That(ArenaBettingSystem.ComputeOdds(1f, 1000f), Is.EqualTo(3.0f).Within(0.001f));
        }

        [Test]
        public void OfferPair_ProvidesTwoDistinctFighters_WithSessionRecords()
        {
            var f1 = ArenaBettingSystem.MakeFighterForTest("a", "선수A", 80f, 40f, 60f, 3, 1);
            var f2 = ArenaBettingSystem.MakeFighterForTest("b", "선수B", 60f, 50f, 70f, 1, 3);
            var pair = ArenaBettingSystem.OfferPair();
            Assert.That(pair, Is.Not.Null);
            Assert.That(pair.west, Is.Not.EqualTo(pair.east), "쌍은 서로 다른 선수여야 한다.");
            Assert.That(pair.west.RecordText, Does.Contain("승").And.Contain("패"), "기록 텍스트는 승패를 포함한다.");
            Assert.That(pair.oddsWest, Is.InRange(ArenaBettingSystem.MinOdds, ArenaBettingSystem.MaxOdds));
            Assert.That(pair.oddsEast, Is.InRange(ArenaBettingSystem.MinOdds, ArenaBettingSystem.MaxOdds));
        }

        [Test]
        public void PlaceBetAndStart_Rejects_NullFighter_AndZeroAmount()
        {
            ArenaBettingSystem.MakeFighterForTest("a", "선수A", 80f, 40f, 60f, 0, 0);
            ArenaBettingSystem.OfferPair();
            // null 선수
            Assert.That(ArenaBettingSystem.PlaceBetAndStart(null, 100, "Western", out string err1), Is.False);
            Assert.That(err1, Is.Not.Null.And.Not.Empty);
            // 0 금액 — pair.west를 건네도 0금액은 거부(런너 미개시 — EditMode 안전)
            var pair = ArenaBettingSystem.CurrentPair;
            Assert.That(ArenaBettingSystem.PlaceBetAndStart(pair.west, 0, "Western", out string err2), Is.False);
            Assert.That(err2, Does.Contain("금액"));
        }

        [Test]
        public void PlaceBetAndStart_Rejects_FighterOutsidePair()
        {
            // 쌍 멤버 2명은 판수 0(로테이션 최우선), 외부 선수는 판수 7 → OfferPair가 쌍에서 배제.
            ArenaBettingSystem.MakeFighterForTest("a", "선수A", 80f, 40f, 60f, 0, 0);
            ArenaBettingSystem.MakeFighterForTest("b", "선수B", 60f, 50f, 70f, 0, 0);
            var outsider = ArenaBettingSystem.MakeFighterForTest("z", "외부선수", 50f, 50f, 50f, 5, 2);
            var pair = ArenaBettingSystem.OfferPair();
            Assert.That(outsider, Is.Not.EqualTo(pair.west).And.Not.EqualTo(pair.east),
                "판수 로테이션으로 외부 선수는 쌍에서 배제되어야 한다.");
            Assert.That(ArenaBettingSystem.PlaceBetAndStart(outsider, 100, "Western", out string err), Is.False);
            Assert.That(err, Does.Contain("제공된 선수"));
        }

        [Test]
        public void FighterRecordText_Formats_Korean_WinsLosses_Rate()
        {
            var f = ArenaBettingSystem.MakeFighterForTest("r", "기록선수", 70f, 50f, 60f, 18, 6);
            Assert.That(f.RecordText, Is.EqualTo("18승 6패 · 승률 75%"));
            Assert.That(f.WinRate, Is.EqualTo(0.75f).Within(0.001f));
        }

        [Test]
        public void SettleForTest_IncrementsWinner_DecrementsLoser()
        {
            var w = ArenaBettingSystem.MakeFighterForTest("w", "승자", 80f, 40f, 60f, 5, 2);
            var l = ArenaBettingSystem.MakeFighterForTest("l", "패자", 60f, 50f, 70f, 2, 5);
            ArenaBettingSystem.SettleForTest(w, l);
            Assert.That(w.wins, Is.EqualTo(6));
            Assert.That(l.losses, Is.EqualTo(6));
        }
    }
}
