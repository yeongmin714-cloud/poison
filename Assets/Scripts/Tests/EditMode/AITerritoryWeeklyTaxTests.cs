using NUnit.Framework;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// AITerritoryEconomySystem 주간 세수 집중 테스트.
    /// 규칙: 앵커일(1) + 7n 경계일에만 과세, 주차 단위 멱등, LordOwned 영지만 적립, 상한 적용,
    /// 세액 = 일세수 * 7 * 프로필 세율(half-up 반올림). 무작위 미사용.
    /// </summary>
    public class AITerritoryWeeklyTaxTests
    {
        private static readonly TerritoryId TestId = new TerritoryId(NationType.North, 1);

        private TerritoryState _state;
        private TerritoryOwnership _previousOwnership;
        private int _previousGold;
        private TerritoryDifficulty _difficulty;
        private int _expectedWeeklyTax;

        [SetUp]
        public void SetUp()
        {
            AITerritoryEconomySystem.ResetAll();
            VillageDrugState.ResetAll(); // [D4-B] VillageDrugState 정적 상태가 factor 1.0 하위호환을 깨지 않게 방어

            TerritoryDatabase db = TerritoryDatabase.Instance;
            _state = db.GetState(TestId);
            _previousOwnership = _state.ownership;
            _previousGold = _state.territoryGold;

            _difficulty = db.GetDefinition(TestId).difficulty;
            TerritoryDifficultyProfile profile = TerritoryDifficultyProfileSystem.Resolve(
                AITerritoryEconomySystem.GetProfileTerritoryId(TestId), TestId.nation, _difficulty);
            _expectedWeeklyTax = AITerritoryEconomySystem.GetWeeklyTax(_difficulty, profile.TaxPercent);

            db.SetOwnership(TestId, TerritoryOwnership.LordOwned);
            _state.territoryGold = 0;
        }

        [TearDown]
        public void TearDown()
        {
            AITerritoryEconomySystem.ResetAll();
            _state.ownership = _previousOwnership;
            _state.territoryGold = _previousGold;
        }

        [Test]
        public void NonBoundaryDay_DoesNotTax()
        {
            AITerritoryEconomySystem.ApplyDailyTax(2);
            AITerritoryEconomySystem.ApplyDailyTax(7);
            Assert.AreEqual(0, _state.territoryGold);
        }

        [Test]
        public void AnchorDay_TaxesOnce_AndSameDayRepeatIsIdempotent()
        {
            AITerritoryEconomySystem.ApplyDailyTax(1);
            Assert.AreEqual(_expectedWeeklyTax, _state.territoryGold);

            AITerritoryEconomySystem.ApplyDailyTax(1);
            Assert.AreEqual(_expectedWeeklyTax, _state.territoryGold);
        }

        [Test]
        public void NextWeekBoundary_TaxesAgain_ButSameWeekDoesNot()
        {
            AITerritoryEconomySystem.ApplyDailyTax(1);
            AITerritoryEconomySystem.ApplyDailyTax(8);
            Assert.AreEqual(_expectedWeeklyTax * 2, _state.territoryGold);
        }

        [Test]
        public void NonLordOwnedTerritory_IsNotTaxed()
        {
            TerritoryDatabase.Instance.SetOwnership(TestId, TerritoryOwnership.PlayerOwned);
            AITerritoryEconomySystem.ApplyDailyTax(1);
            Assert.AreEqual(0, _state.territoryGold);
        }

        [Test]
        public void Treasury_IsCappedAtTreasuryCap()
        {
            int cap = AITerritoryEconomySystem.GetTreasuryCap(_difficulty);
            _state.territoryGold = cap;
            AITerritoryEconomySystem.ApplyDailyTax(1);
            Assert.AreEqual(cap, _state.territoryGold);
        }

        [Test]
        public void ResetAll_AllowsSameWeekTaxAgain()
        {
            AITerritoryEconomySystem.ApplyDailyTax(1);
            AITerritoryEconomySystem.ResetAll();
            AITerritoryEconomySystem.ApplyDailyTax(1);
            Assert.AreEqual(_expectedWeeklyTax * 2, _state.territoryGold);
        }

        [Test]
        public void GetWeeklyTax_BaseRate_IsSevenTimesDailyTax()
        {
            Assert.AreEqual(10 * 7, AITerritoryEconomySystem.GetWeeklyTax(TerritoryDifficulty.Ring1, 100));
            Assert.AreEqual(100 * 7, AITerritoryEconomySystem.GetWeeklyTax(TerritoryDifficulty.Empire, 100));
        }

        [Test]
        public void GetWeeklyTax_RoundsHalfUp()
        {
            // 10 * 7 * 105 / 100 = 73.5 -> 74
            Assert.AreEqual(74, AITerritoryEconomySystem.GetWeeklyTax(TerritoryDifficulty.Ring1, 105));
            // 10 * 7 * 94 / 100 = 65.8 -> 66
            Assert.AreEqual(66, AITerritoryEconomySystem.GetWeeklyTax(TerritoryDifficulty.Ring1, 94));
            // 10 * 7 * 103 / 100 = 72.1 -> 72
            Assert.AreEqual(72, AITerritoryEconomySystem.GetWeeklyTax(TerritoryDifficulty.Ring1, 103));
        }

        [Test]
        public void IsTaxBoundaryDay_MatchesAnchorAndInterval()
        {
            Assert.IsFalse(AITerritoryEconomySystem.IsTaxBoundaryDay(0));
            Assert.IsTrue(AITerritoryEconomySystem.IsTaxBoundaryDay(1));
            Assert.IsFalse(AITerritoryEconomySystem.IsTaxBoundaryDay(7));
            Assert.IsTrue(AITerritoryEconomySystem.IsTaxBoundaryDay(8));
            Assert.IsTrue(AITerritoryEconomySystem.IsTaxBoundaryDay(15));
        }
    }
}
