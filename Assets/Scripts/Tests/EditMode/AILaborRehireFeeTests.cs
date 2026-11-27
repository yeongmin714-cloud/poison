using NUnit.Framework;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// AI 노동시장 재고용 입단비(플레이어 편입 산식 동일) + 입단 원자성 + 전쟁 배치 경로 단일화 정책 테스트.
    /// PlayerStats를 사용하지 않는다(AI 금고 = TerritoryState.territoryGold).
    /// </summary>
    public class AILaborRehireFeeTests
    {
        private static readonly TerritoryId LordId = new TerritoryId(NationType.North, 1);

        private TerritoryState _state;
        private TerritoryOwnership _prevOwnership;
        private int _prevGold;
        private GameObject _guardGo;
        private GuardPlaceholder _guard;

        [SetUp]
        public void SetUp()
        {
            SoldierWageSystem.ResetAll();
            LaborMarketSystem.ResetAll();

            TerritoryDatabase db = TerritoryDatabase.Instance;
            _state = db.GetState(LordId);
            _prevOwnership = _state.ownership;
            _prevGold = _state.territoryGold;
            db.SetOwnership(LordId, TerritoryOwnership.LordOwned);
            _state.territoryGold = 100000;

            _guardGo = new GameObject("AIRehireFee_Guard");
            _guard = _guardGo.AddComponent<GuardPlaceholder>();
            _guard.SetGuardInfo("재고용테스트병사", 3, NationType.East);
            _guard.SetRecruited(false);
        }

        [TearDown]
        public void TearDown()
        {
            LaborMarketSystem.ResetAll();
            SoldierWageSystem.ResetAll();
            if (_guardGo != null) Object.DestroyImmediate(_guardGo);
            _state.ownership = _prevOwnership;
            _state.territoryGold = _prevGold;
        }

        // ── 입단비 산식: 플레이어 편입과 동일 ─────────────────────────

        [Test]
        public void AIRehireFee_MatchesPlayerAdmissionFormula()
        {
            int playerNextFee = SoldierWageSystem.PeekGuardAdmissionFee(_guard);
            Assert.AreEqual(playerNextFee, LaborMarketSystem.GetAIAdmissionFee(_guard));
            Assert.AreEqual(
                SoldierWageSystem.ComputeAdmissionFee(_guard, SoldierWageSystem.GetAdmissionCount(_guard.StableId) + 1),
                LaborMarketSystem.GetAIAdmissionFee(_guard));
        }

        [Test]
        public void AIRehireFee_NullGuard_IsZero()
        {
            Assert.AreEqual(0, LaborMarketSystem.GetAIAdmissionFee(null));
        }

        [Test]
        public void AIRehireFee_IsNotMaintenanceTimesFive()
        {
            // 구 산식(유지비*5)과 분리되었는지 확인: 입단비는 SoldierWage 산식(FEE_MIN 하한 포함)을 따른다.
            int fee = LaborMarketSystem.GetAIAdmissionFee(_guard);
            Assert.GreaterOrEqual(fee, SoldierWageSystem.FEE_MIN);
            Assert.AreEqual(SoldierWageSystem.PeekGuardAdmissionFee(_guard), fee);
        }

        // ── 입단 원자성 ────────────────────────────────────────────────

        [Test]
        public void TryAdmitToLord_InsufficientTreasury_DebitsNothingAndKeepsPool()
        {
            LaborMarketSystem.ReleaseGuard(_guard);
            Assert.AreEqual(1, LaborMarketSystem.MarketPoolSize());

            int fee = LaborMarketSystem.GetAIAdmissionFee(_guard);
            _state.territoryGold = fee - 1;

            bool ok = LaborMarketSystem.TryAdmitToLord(_guard, LordId, fee);

            Assert.IsFalse(ok);
            Assert.AreEqual(fee - 1, _state.territoryGold, "금고 부족 시 차감이 일어나면 안 된다");
            Assert.AreEqual(1, LaborMarketSystem.MarketPoolSize(), "풀 보존");
            Assert.AreSame(_guard, LaborMarketSystem.PeekPool(0));
            Assert.AreEqual(0, LaborMarketSystem.GetHiredCount(LordId), "로스터(고용 카운트) 변경 없음");
            Assert.AreEqual(0, LaborMarketSystem.TotalHiredByAI());
        }

        [Test]
        public void TryAdmitToLord_SufficientTreasury_DebitsExactlyFeeAndRegisters()
        {
            LaborMarketSystem.ReleaseGuard(_guard);
            int fee = LaborMarketSystem.GetAIAdmissionFee(_guard);
            _state.territoryGold = fee + 7;

            bool ok = LaborMarketSystem.TryAdmitToLord(_guard, LordId, fee);

            Assert.IsTrue(ok);
            Assert.AreEqual(7, _state.territoryGold);
            Assert.AreEqual(1, LaborMarketSystem.GetHiredCount(LordId));
            Assert.IsFalse(_guard.IsRecruited);
        }

        [Test]
        public void TryAdmitToLord_NullGuard_ReturnsFalseWithoutDebit()
        {
            _state.territoryGold = 500;
            Assert.IsFalse(LaborMarketSystem.TryAdmitToLord(null, LordId, 10));
            Assert.AreEqual(500, _state.territoryGold);
        }

        // ── AI 입단 성공 기록 (SoldierWageSystem 편입 카운트) ─────────────

        [Test]
        public void TryAdmitToLord_SuccessfulAssignment_IncrementsAdmissionCountOnce()
        {
            LaborMarketSystem.ReleaseGuard(_guard);
            int fee = LaborMarketSystem.GetAIAdmissionFee(_guard);
            _state.territoryGold = fee + 7;
            Assert.AreEqual(0, SoldierWageSystem.GetAdmissionCount(_guard.StableId));

            bool ok = LaborMarketSystem.TryAdmitToLord(_guard, LordId, fee);

            Assert.IsTrue(ok);
            Assert.AreEqual(1, SoldierWageSystem.GetAdmissionCount(_guard.StableId), "성공 입단 1회 → 카운트 1");
        }

        [Test]
        public void TryAdmitToLord_InsufficientTreasury_DoesNotIncrementAdmissionCount()
        {
            LaborMarketSystem.ReleaseGuard(_guard);
            int fee = LaborMarketSystem.GetAIAdmissionFee(_guard);
            _state.territoryGold = fee - 1;

            Assert.IsFalse(LaborMarketSystem.TryAdmitToLord(_guard, LordId, fee));
            Assert.AreEqual(0, SoldierWageSystem.GetAdmissionCount(_guard.StableId), "실패 입단은 카운트 불변");
        }

        [Test]
        public void RecordExternalGuardAdmission_NullOrNoStableId_ReturnsFalse()
        {
            Assert.IsFalse(SoldierWageSystem.RecordExternalGuardAdmission(null));
        }

        [Test]
        public void RecordExternalGuardAdmission_IncrementsByOnePerCall_KeyedByStableId()
        {
            Assert.IsTrue(SoldierWageSystem.RecordExternalGuardAdmission(_guard));
            Assert.IsTrue(SoldierWageSystem.RecordExternalGuardAdmission(_guard));
            Assert.AreEqual(2, SoldierWageSystem.GetAdmissionCount(_guard.StableId));
        }

        // ── 고용 시장 save-state export/import ───────────────────────

        [Test]
        public void LaborMarketSave_ExportImportRoundTrip_IsDeterministic()
        {
            LaborMarketSystem.ReleaseGuard(_guard);
            int fee = LaborMarketSystem.GetAIAdmissionFee(_guard);
            _state.territoryGold = fee + 1000;
            Assert.IsTrue(LaborMarketSystem.TryAdmitToLord(_guard, LordId, fee));

            LaborMarketSaveData first = LaborMarketSystem.ExportSaveData();
            LaborMarketSaveData second = LaborMarketSystem.ExportSaveData();
            Assert.AreEqual(1, first.hiredCounts.Count);
            Assert.AreEqual(LordId.ToString(), first.hiredCounts[0].territoryId);
            Assert.AreEqual(1, first.hiredCounts[0].count);
            Assert.AreEqual(1, first.totalHiredByAI);
            Assert.AreEqual(first.lastProcessedDay, second.lastProcessedDay);
            Assert.AreEqual(first.hiredCounts[0].territoryId, second.hiredCounts[0].territoryId);

            LaborMarketSystem.ResetAll();
            LaborMarketSystem.ImportSaveData(first);

            LaborMarketSaveData restored = LaborMarketSystem.ExportSaveData();
            Assert.AreEqual(first.totalHiredByAI, restored.totalHiredByAI);
            Assert.AreEqual(first.lastProcessedDay, restored.lastProcessedDay);
            Assert.AreEqual(1, LaborMarketSystem.GetHiredCount(LordId));
            Assert.AreEqual(1, LaborMarketSystem.TotalHiredByAI());
            Assert.AreEqual(first.hiredCounts.Count, restored.hiredCounts.Count);
            Assert.AreEqual(first.hiredCounts[0].count, restored.hiredCounts[0].count);
        }

        [Test]
        public void LaborMarketSave_Import_ClearsPriorCountsAndClampsNegatives()
        {
            var stale = new LaborMarketSaveData { lastProcessedDay = 5, totalHiredByAI = 9 };
            stale.hiredCounts.Add(new TerritoryHireCountEntry { territoryId = LordId.ToString(), count = 4 });
            LaborMarketSystem.ImportSaveData(stale);
            Assert.AreEqual(4, LaborMarketSystem.GetHiredCount(LordId));

            var incoming = new LaborMarketSaveData { lastProcessedDay = -7, totalHiredByAI = -3 };
            incoming.hiredCounts.Add(new TerritoryHireCountEntry { territoryId = "NoSuch_Nation_1", count = 2 });
            incoming.hiredCounts.Add(new TerritoryHireCountEntry { territoryId = "East_1", count = 2 });
            incoming.hiredCounts.Add(new TerritoryHireCountEntry { territoryId = "", count = 2 });
            incoming.hiredCounts.Add(null);
            incoming.hiredCounts.Add(new TerritoryHireCountEntry { territoryId = new TerritoryId(NationType.East, 2).ToString(), count = -5 });
            LaborMarketSystem.ImportSaveData(incoming);

            Assert.AreEqual(0, LaborMarketSystem.GetHiredCount(LordId), "이전 카운트는 제거");
            Assert.AreEqual(0, LaborMarketSystem.GetHiredCount(new TerritoryId(NationType.East, 2)), "음수 카운트는 0으로 클램프");
            Assert.AreEqual(0, LaborMarketSystem.TotalHiredByAI(), "음수 누적은 0으로 클램프");
            Assert.AreEqual(0, LaborMarketSystem.ExportSaveData().hiredCounts.Count, "유효하지 않은 ID/음수 항목은 건너뜀");
            Assert.AreEqual(-1, LaborMarketSystem.ExportSaveData().lastProcessedDay, "처리일 음수는 -1로 클램프");
        }

        [Test]
        public void LaborMarketSave_ImportNull_ResetsToDefault()
        {
            LaborMarketSystem.ImportSaveData(new LaborMarketSaveData { lastProcessedDay = 3, totalHiredByAI = 2 });
            LaborMarketSystem.ImportSaveData(null);
            Assert.AreEqual(0, LaborMarketSystem.TotalHiredByAI());
            Assert.AreEqual(-1, LaborMarketSystem.ExportSaveData().lastProcessedDay);
        }

        // ── 전쟁 배치 단일 경로 정책 ───────────────────────────────────

        [Test]
        public void LegacyGarrison_NotSpawned_WhenVisibleMarchStarted()
        {
            Assert.IsFalse(AIWarSystem.ShouldSpawnLegacyGarrison(true));
        }

        [Test]
        public void LegacyGarrison_IsFallback_WhenVisibleMarchNotStarted()
        {
            Assert.IsTrue(AIWarSystem.ShouldSpawnLegacyGarrison(false));
        }
    }
}
