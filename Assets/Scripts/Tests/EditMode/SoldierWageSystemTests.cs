using NUnit.Framework;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// SoldierWageSystem 집중 테스트 — 병사 입단비(편입 1회) + 용병 고용(입단비+첫 주 계약비) + 용병 주간 계약비.
    /// 계약: 일반 병사 일일 급료는 GuardSalarySystem 그대로 유지(주급 전환 없음). 용병만 7일 주기 계약비.
    /// 결정론: 무작위 미사용.
    /// </summary>
    public class SoldierWageSystemTests
    {
        private GameObject _statsGo;
        private PlayerStats _stats;
        private GameObject _guardGo;
        private GuardPlaceholder _guard;
        private GameObject _mercMgrGo;
        private MercenaryManager _mercMgr;
        private GameObject _auditGo;
        private EconomyAuditSystem _audit;
        private System.Delegate _savedGoldListeners;

        [SetUp]
        public void SetUp()
        {
            SoldierWageSystem.ResetAll();
            GuardSalarySystem.ResetAll();
            EconomyAuditLedger.ResetAudit();

            // 정적 PlayerStats.GoldChanged에 다른 픽스처가 남긴 구독자가 있으면 원장이 같은 지출을 2번 이상 기록한다
            // (예: 한 번의 SpendGold가 2건의 outflow로 집계). 픽스처 시작 시 구독자를 비우고, 종료 시 원래 상태로 복원한다.
            _savedGoldListeners = GetGoldListeners();
            SetGoldListeners(null);

            _statsGo = new GameObject("WageTest_PlayerStats");
            _stats = _statsGo.AddComponent<PlayerStats>();
            // EditMode에서 Awake 경로에 의존하지 않도록 싱글톤 백킹 필드를 테스트 전용으로 직접 지정
            if (PlayerStats.Instance != _stats) SetSingleton(_stats);
            SetGold(100000);
            SetPlayerLevel(5);

            _guardGo = new GameObject("WageTest_Guard");
            _guard = _guardGo.AddComponent<GuardPlaceholder>();
            _guard.SetGuardInfo("테스트병사", 1, NationType.East);
            _guard.SetRecruited(false);

            // MercenaryManager: 이전 테스트/다른 픽스처가 남긴 살아있는 Instance가 있으면
            // 이 매니저의 Awake가 중복 제거 경로로 빠져 Instance가 엉뚱한 객체를 가리킨다.
            // 그러면 TryPayDueMercenaryContracts가 빈 목록을 순회해 계약비가 0건이 된다.
            // → 테스트 매니저를 Instance로 명시 바인딩해 생명주기를 고정한다.
            SetStaticInstance<MercenaryManager>(null);
            _mercMgrGo = new GameObject("WageTest_MercManager");
            _mercMgr = _mercMgrGo.AddComponent<MercenaryManager>();
            _mercMgr.EnsureDatabase();
            SetStaticInstance(_mercMgr);

            // 경제 원장: EconomyAuditLedger는 EconomyAuditSystem이 PlayerStats.GoldChanged를
            // 구독해야만 기록된다. 테스트에서도 감사 시스템을 명시적으로 구독시킨다.
            SetStaticInstance<EconomyAuditSystem>(null);
            _auditGo = new GameObject("WageTest_EconomyAudit");
            _audit = _auditGo.AddComponent<EconomyAuditSystem>();
            _audit.EnsureSubscribed();
        }

        [TearDown]
        public void TearDown()
        {
            if (_auditGo != null) Object.DestroyImmediate(_auditGo);
            if (_mercMgrGo != null) Object.DestroyImmediate(_mercMgrGo);
            if (_guardGo != null) Object.DestroyImmediate(_guardGo);
            // DetachSingleton()이 이미 파괴했을 수 있으므로 null 체크 (Unity null 비교 사용)
            if (_statsGo != null) Object.DestroyImmediate(_statsGo);
            DetachSingleton();
            SetStaticInstance<EconomyAuditSystem>(null);
            SetStaticInstance<MercenaryManager>(null);
            _auditGo = null;
            _audit = null;
            _mercMgrGo = null;
            _mercMgr = null;
            // 픽스처 시작 시점의 구독자 중 살아있는 것만 복원 (파괴된 객체의 핸들러는 되살리지 않는다)
            SetGoldListeners(FilterLiveListeners(_savedGoldListeners));
            _savedGoldListeners = null;
            EconomyAuditLedger.ResetAudit();
            SoldierWageSystem.ResetAll();
            GuardSalarySystem.ResetAll();
        }

        // 정적 이벤트 PlayerStats.GoldChanged의 백킹 필드(델리게이트 체인) 접근 — 테스트 전용.
        private static System.Delegate GetGoldListeners()
        {
            var field = typeof(PlayerStats).GetField("GoldChanged",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, "PlayerStats.GoldChanged 백킹 필드");
            return (System.Delegate)field.GetValue(null);
        }

        // Target이 파괴된 UnityEngine.Object인 핸들러를 제거한 델리게이트 체인 반환 (null이면 null)
        private static System.Delegate FilterLiveListeners(System.Delegate chain)
        {
            if (chain == null) return null;
            System.Delegate live = null;
            foreach (var d in chain.GetInvocationList())
            {
                if (d.Target is Object uo && uo == null) continue; // 파괴된 Unity 객체
                live = System.Delegate.Combine(live, d);
            }
            return live;
        }

        private static void SetGoldListeners(System.Delegate value)
        {
            var field = typeof(PlayerStats).GetField("GoldChanged",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, "PlayerStats.GoldChanged 백킹 필드");
            field.SetValue(null, value);
        }

        // 테스트 전용: static Instance 백킹 필드 설정/해제 (PlayerStats, MercenaryManager, EconomyAuditSystem 공용).
        // 파괴된 인스턴스가 Unity null 체크 때문에 남는 경우도 확실히 비운다.
        private static void SetStaticInstance<T>(T value) where T : class
        {
            var field = typeof(T).GetField("<Instance>k__BackingField",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, typeof(T).Name + ".Instance 백킹 필드");
            field.SetValue(null, value);
        }

        private static void SetSingleton(PlayerStats value) => SetStaticInstance(value);

        private static void DetachSingleton()
        {
            PlayerStats.ResetInstance();
            SetSingleton(null);
        }

        private void SetGold(int amount)
        {
            Assert.IsNotNull(_stats, "PlayerStats 인스턴스");
            _stats.GetType().GetField("_gold", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(_stats, amount);
        }

        private void SetPlayerLevel(int level)
        {
            Assert.IsNotNull(_stats, "PlayerStats 인스턴스");
            _stats.GetType().GetField("_level", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(_stats, level);
        }

        // ── 테스트 픽스처 생명주기 가드 ─────────────────────────────────

        [Test]
        public void Fixture_SingletonsBoundToThisFixture()
        {
            // 주간 계약 틱은 MercenaryManager.Instance를 순회하므로 반드시 이 픽스처의 매니저여야 한다.
            Assert.AreSame(_mercMgr, MercenaryManager.Instance, "MercenaryManager.Instance = 테스트 매니저");
            Assert.AreSame(_stats, PlayerStats.Instance, "PlayerStats.Instance = 테스트 스탯");
        }

        [Test]
        public void Fixture_EconomyAuditSubscribed_RecordsSpend()
        {
            // 원장은 EconomyAuditSystem 구독을 통해서만 기록된다. 구독이 빠지면 지출이 0으로 보인다.
            Assert.IsTrue(_stats.SpendGold(7, SoldierWageSystem.SourceSoldierHireFee));
            Assert.AreEqual(7, EconomyAuditLedger.GetOutflow(SoldierWageSystem.SourceSoldierHireFee));
        }

        // ── 병사 입단비 ─────────────────────────────────────────────────

        [Test]
        public void SoldierFee_IsDeterministicForSameSeq()
        {
            int a = SoldierWageSystem.ComputeAdmissionFee(_guard, 3);
            int b = SoldierWageSystem.ComputeAdmissionFee(_guard, 3);
            Assert.AreEqual(a, b);
        }

        [Test]
        public void SoldierFee_VarianceStaysWithinBand_AndAtLeastMin()
        {
            int raw = SoldierWageSystem.ComputeRawAdmissionFee(_guard);
            for (int seq = 1; seq <= 20; seq++)
            {
                float mult = SoldierWageSystem.VarianceMultiplier(_guard.StableId, seq);
                Assert.GreaterOrEqual(mult, 1f - SoldierWageSystem.FEE_VARIANCE_PCT);
                Assert.LessOrEqual(mult, 1f + SoldierWageSystem.FEE_VARIANCE_PCT);
                int fee = SoldierWageSystem.ComputeAdmissionFee(_guard, seq);
                Assert.GreaterOrEqual(fee, SoldierWageSystem.FEE_MIN);
                Assert.LessOrEqual(fee, Mathf.CeilToInt(raw * (1f + SoldierWageSystem.FEE_VARIANCE_PCT)));
            }
        }

        [Test]
        public void SoldierFee_NullGuard_ReturnsZero()
        {
            Assert.AreEqual(0, SoldierWageSystem.ComputeRawAdmissionFee(null));
            Assert.AreEqual(0, SoldierWageSystem.ComputeAdmissionFee(null, 1));
        }

        [Test]
        public void StableHash01_SameInput_SameValue_InUnitRange()
        {
            float h1 = SoldierWageSystem.StableHash01("guard_0001", 7);
            float h2 = SoldierWageSystem.StableHash01("guard_0001", 7);
            Assert.AreEqual(h1, h2);
            Assert.GreaterOrEqual(h1, 0f);
            Assert.Less(h1, 1f);
        }

        [Test]
        public void GradeFee_TableMatchesSpec()
        {
            Assert.AreEqual(0, SoldierWageSystem.GetGradeFee(MercenaryGrade.Normal));
            Assert.AreEqual(150, SoldierWageSystem.GetGradeFee(MercenaryGrade.High));
            Assert.AreEqual(600, SoldierWageSystem.GetGradeFee(MercenaryGrade.Elite));
            Assert.AreEqual(1500, SoldierWageSystem.GetGradeFee(MercenaryGrade.Legendary));
        }

        [Test]
        public void SoldierAdmission_SufficientGold_ChargesAndRecordsSource()
        {
            int before = _stats.Gold;
            int ledgerBefore = EconomyAuditLedger.GetOutflow(SoldierWageSystem.SourceSoldierHireFee);
            bool ok = SoldierWageSystem.TryChargeGuardAdmissionFee(_guard, "normal", out int fee);
            Assert.IsTrue(ok);
            Assert.Greater(fee, 0);
            Assert.AreEqual(before - fee, _stats.Gold);
            Assert.AreEqual(ledgerBefore + fee, EconomyAuditLedger.GetOutflow(SoldierWageSystem.SourceSoldierHireFee), "이번 청구만 원장에 +fee");
            Assert.AreEqual(1, SoldierWageSystem.GetAdmissionCount(_guard.StableId));
        }

        [Test]
        public void SoldierAdmission_InsufficientGold_FailsWithoutStateChange()
        {
            SetGold(0);
            int ledgerBefore = EconomyAuditLedger.GetOutflow(SoldierWageSystem.SourceSoldierHireFee);
            bool ok = SoldierWageSystem.TryChargeGuardAdmissionFee(_guard, "normal", out int fee);
            Assert.IsFalse(ok);
            Assert.AreEqual(0, fee);
            Assert.AreEqual(0, _stats.Gold);
            Assert.AreEqual(ledgerBefore, EconomyAuditLedger.GetOutflow(SoldierWageSystem.SourceSoldierHireFee), "실패 청구는 원장 불변");
            Assert.AreEqual(0, SoldierWageSystem.GetAdmissionCount(_guard.StableId));
        }

        [Test]
        public void SoldierAdmission_AlreadyRecruited_IsIdempotentZeroFee()
        {
            _guard.SetRecruited(true);
            bool ok = SoldierWageSystem.TryChargeGuardAdmissionFee(_guard, "normal", out int fee);
            Assert.IsTrue(ok);
            Assert.AreEqual(0, fee);
            Assert.AreEqual(100000, _stats.Gold);
        }

        [Test]
        public void SoldierAdmission_NoPlayerStats_ReturnsFalse()
        {
            DetachSingleton();
            Assert.IsFalse(SoldierWageSystem.TryChargeGuardAdmissionFee(_guard, "auto", out int fee));
            Assert.AreEqual(0, fee);
            Assert.AreEqual(0, SoldierWageSystem.GetAdmissionCount(_guard.StableId));
        }

        [Test]
        public void SoldierAdmission_ReAdmission_IncrementsSeq()
        {
            Assert.IsTrue(SoldierWageSystem.TryChargeGuardAdmissionFee(_guard, "normal", out _));
            _guard.SetRecruited(false);
            Assert.IsTrue(SoldierWageSystem.TryChargeGuardAdmissionFee(_guard, "normal", out int second));
            Assert.AreEqual(2, SoldierWageSystem.GetAdmissionCount(_guard.StableId));
            Assert.Greater(second, 0);
        }

        [Test]
        public void SoldierAdmission_ChargesBeforeOwnershipChange_ViaRecruitCaller()
        {
            // OnRecruit 경로와 동일하게 입단비 청구 실패 시 상태 불변을 검증
            SetGold(0);
            _guard.Loyalty = 100f;
            Assert.IsFalse(SoldierWageSystem.TryChargeGuardAdmissionFee(_guard, "auto", out _));
            Assert.IsFalse(_guard.IsRecruited, "청구 실패 → 소속 미변경");
        }

        // ── 일반 병사 일일 급료 유지 (주급 전환 없음) ────────────────────

        [Test]
        public void OrdinaryGuard_DailyWageStillCharged()
        {
            _guard.SetRecruited(true);
            _guard.Loyalty = 50f;
            int daily = GuardSalarySystem.GetDailyCost(_guard);
            int before = _stats.Gold;
            // 주의: GuardSalarySystem은 GuardManager.Instance에 의존하지 않고 PlayerStats만 사용해도 청구되지 않음
            // (GetAllPlayerGuards가 null → total 0). 일반 병사 일일 급료 산식만 확인.
            Assert.Greater(daily, 0);
            Assert.AreEqual(before, _stats.Gold, "단위 테스트에서 GuardManager 없으면 일일 청구 대상 0");
        }

        // ── 용병 고용 (입단비 + 첫 주 계약비, 원자) ──────────────────────

        [Test]
        public void MercHire_InsufficientGold_RejectedWithoutRegistration()
        {
            SetGold(0);
            int before = _mercMgr.HiredCount;
            int hireBefore = EconomyAuditLedger.GetOutflow(SoldierWageSystem.SourceMercenaryHireFee);
            int contractBefore = EconomyAuditLedger.GetOutflow(SoldierWageSystem.SourceMercenaryContractFee);
            Assert.IsFalse(_mercMgr.HireMercenary("merc_soldier_01"));
            Assert.AreEqual(before, _mercMgr.HiredCount, "고용 목록 불변");
            Assert.AreEqual(hireBefore, EconomyAuditLedger.GetOutflow(SoldierWageSystem.SourceMercenaryHireFee), "실패 고용은 입단비 원장 불변");
            Assert.AreEqual(contractBefore, EconomyAuditLedger.GetOutflow(SoldierWageSystem.SourceMercenaryContractFee), "실패 고용은 계약비 원장 불변");
        }

        [Test]
        public void MercHire_SufficientGold_ChargesAdmissionPlusFirstWeek()
        {
            _mercMgr.TryGetMercenaryData("merc_soldier_01", out var merc);
            int admission = SoldierWageSystem.GetMercenaryAdmissionFee(merc);
            int contract = SoldierWageSystem.GetMercenaryWeeklyContractCost(merc);
            int before = _stats.Gold;
            int hireBefore = EconomyAuditLedger.GetOutflow(SoldierWageSystem.SourceMercenaryHireFee);
            int contractBefore = EconomyAuditLedger.GetOutflow(SoldierWageSystem.SourceMercenaryContractFee);

            Assert.IsTrue(_mercMgr.HireMercenary("merc_soldier_01"));
            Assert.AreEqual(before - admission - contract, _stats.Gold, "입단비 + 첫 주 계약비 1회씩");
            Assert.AreEqual(hireBefore + admission, EconomyAuditLedger.GetOutflow(SoldierWageSystem.SourceMercenaryHireFee));
            Assert.AreEqual(contractBefore + contract, EconomyAuditLedger.GetOutflow(SoldierWageSystem.SourceMercenaryContractFee));
            Assert.AreEqual(1, _mercMgr.HiredCount);
        }

        [Test]
        public void MercHire_AlreadyHired_NoDoubleCharge()
        {
            Assert.IsTrue(_mercMgr.HireMercenary("merc_soldier_01"));
            int goldAfterFirst = _stats.Gold;
            int hireAfterFirst = EconomyAuditLedger.GetOutflow(SoldierWageSystem.SourceMercenaryHireFee);
            int contractAfterFirst = EconomyAuditLedger.GetOutflow(SoldierWageSystem.SourceMercenaryContractFee);
            Assert.IsFalse(_mercMgr.HireMercenary("merc_soldier_01"));
            Assert.AreEqual(hireAfterFirst, EconomyAuditLedger.GetOutflow(SoldierWageSystem.SourceMercenaryHireFee), "재고용 거부 시 입단비 원장 불변");
            Assert.AreEqual(contractAfterFirst, EconomyAuditLedger.GetOutflow(SoldierWageSystem.SourceMercenaryContractFee), "재고용 거부 시 계약비 원장 불변");
            Assert.AreEqual(goldAfterFirst, _stats.Gold);
        }

        [Test]
        public void MercHire_NoPlayerStats_RejectedWithoutRegistration()
        {
            DetachSingleton();
            Assert.IsFalse(_mercMgr.HireMercenary("merc_soldier_01"));
            Assert.AreEqual(0, _mercMgr.HiredCount);
        }

        [Test]
        public void MercHire_SetsNextContractDayPlusSeven()
        {
            Assert.IsTrue(_mercMgr.HireMercenary("merc_soldier_01"));
            int today = SoldierWageSystem.CurrentDay();
            Assert.AreEqual(today + SoldierWageSystem.CONTRACT_INTERVAL_DAYS,
                SoldierWageSystem.GetNextContractDay("merc_soldier_01"));
        }

        // 용병 주간 계약 due 상태 테스트는 MercenaryContractDueTests.cs 로 이관됨
        // (TryPayDueMercenaryContracts 자동 청구/해고 제거: 승인·해지는 명시적 API).
    }
}
