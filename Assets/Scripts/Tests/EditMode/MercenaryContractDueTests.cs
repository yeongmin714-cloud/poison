using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// 용병 주간 계약 due 상태 머신 테스트 (UI 없음).
    /// 대상: SoldierWageSystem.NotifyDueMercenaryContracts / GetDueMercenaryContracts /
    ///       TryApproveMercenaryContract / ReleaseMercenaryContract, MercenaryManager.FireMercenary 스케줄 정리.
    /// 기준 날짜: TimeManager 부재 시 CurrentDay()=1 → 고용 시 nextDay = 1 + 7 = 8.
    /// 결정론: 무작위 미사용.
    /// </summary>
    public class MercenaryContractDueTests
    {
        private const string MercId = "merc_soldier_01";   // hireCost 100, Soldier
        private const int HireDay = 1;                      // CurrentDay() 기본값 (TimeManager 없음)
        private const int FirstDue = HireDay + SoldierWageSystem.CONTRACT_INTERVAL_DAYS; // 8

        private GameObject _statsGo;
        private PlayerStats _stats;
        private GameObject _mercMgrGo;
        private MercenaryManager _mercMgr;
        private GameObject _auditGo;
        private EconomyAuditSystem _audit;
        private System.Delegate _savedGoldListeners;
        private int _notifyCount;

        [SetUp]
        public void SetUp()
        {
            SoldierWageSystem.ResetAll();
            GuardSalarySystem.ResetAll();
            EconomyAuditLedger.ResetAudit();

            _savedGoldListeners = GetGoldListeners();
            SetGoldListeners(null);

            _statsGo = new GameObject("MercDue_PlayerStats");
            _stats = _statsGo.AddComponent<PlayerStats>();
            if (PlayerStats.Instance != _stats) SetStaticInstance<PlayerStats>(_stats);
            SetField(_stats, "_gold", 100000);
            SetField(_stats, "_level", 5);

            SetStaticInstance<MercenaryManager>(null);
            _mercMgrGo = new GameObject("MercDue_MercManager");
            _mercMgr = _mercMgrGo.AddComponent<MercenaryManager>();
            _mercMgr.EnsureDatabase();
            SetStaticInstance(_mercMgr);

            SetStaticInstance<EconomyAuditSystem>(null);
            _auditGo = new GameObject("MercDue_EconomyAudit");
            _audit = _auditGo.AddComponent<EconomyAuditSystem>();
            _audit.EnsureSubscribed();

            _notifyCount = 0;
        }

        [TearDown]
        public void TearDown()
        {
            SoldierWageSystem.ResetAll(); // 이벤트 구독 해제 포함
            if (_auditGo != null) Object.DestroyImmediate(_auditGo);
            if (_mercMgrGo != null) Object.DestroyImmediate(_mercMgrGo);
            if (_statsGo != null) Object.DestroyImmediate(_statsGo);
            PlayerStats.ResetInstance();
            SetStaticInstance<PlayerStats>(null);
            SetStaticInstance<EconomyAuditSystem>(null);
            SetStaticInstance<MercenaryManager>(null);
            SetGoldListeners(FilterLiveListeners(_savedGoldListeners));
            _savedGoldListeners = null;
            EconomyAuditLedger.ResetAudit();
            GuardSalarySystem.ResetAll();
        }

        // ── 헬퍼 ────────────────────────────────────────────────────────

        private int Gold => _stats.Gold;

        private void HireBaseline()
        {
            Assert.IsTrue(_mercMgr.HireMercenary(MercId), "고용 준비 실패");
            Assert.AreEqual(FirstDue, SoldierWageSystem.GetNextContractDay(MercId), "고용 직후 청구일 = 오늘+7");
        }

        private int HireCost
        {
            get
            {
                _mercMgr.TryGetMercenaryData(MercId, out var merc);
                return SoldierWageSystem.GetMercenaryWeeklyContractCost(merc);
            }
        }

        private int ContractLedger => EconomyAuditLedger.GetOutflow(SoldierWageSystem.SourceMercenaryContractFee);

        private void SetGold(int amount) => SetField(_stats, "_gold", amount);

        private static void SetField(object target, string field, int value)
        {
            var f = target.GetType().GetField(field,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(f, field + " 필드");
            f.SetValue(target, value);
        }

        private static System.Delegate GetGoldListeners()
        {
            var field = typeof(PlayerStats).GetField("GoldChanged",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, "PlayerStats.GoldChanged 백킹 필드");
            return (System.Delegate)field.GetValue(null);
        }

        private static void SetGoldListeners(System.Delegate value)
        {
            var field = typeof(PlayerStats).GetField("GoldChanged",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, "PlayerStats.GoldChanged 백킹 필드");
            field.SetValue(null, value);
        }

        private static System.Delegate FilterLiveListeners(System.Delegate chain)
        {
            if (chain == null) return null;
            System.Delegate live = null;
            foreach (var d in chain.GetInvocationList())
            {
                if (d.Target is Object uo && uo == null) continue;
                live = System.Delegate.Combine(live, d);
            }
            return live;
        }

        private static void SetStaticInstance<T>(T value) where T : class
        {
            var field = typeof(T).GetField("<Instance>k__BackingField",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, typeof(T).Name + ".Instance 백킹 필드");
            field.SetValue(null, value);
        }

        // ── due / non-due 조회 ─────────────────────────────────────────

        [Test]
        public void Due_NotDueBeforeScheduledDay_EmptySnapshot()
        {
            HireBaseline();
            Assert.AreEqual(0, SoldierWageSystem.GetDueMercenaryContracts(FirstDue - 1).Length);
        }

        [Test]
        public void Due_OnScheduledDay_ReturnsRowWithFields()
        {
            HireBaseline();
            var rows = SoldierWageSystem.GetDueMercenaryContracts(FirstDue);
            Assert.AreEqual(1, rows.Length);
            var row = rows[0];
            Assert.AreEqual(MercId, row.mercenaryId);
            Assert.AreEqual("카림", row.mercenaryName);
            Assert.AreEqual("Soldier", row.jobType);
            Assert.AreEqual(MercenaryGrade.Normal, row.grade);
            Assert.AreEqual(HireCost, row.weeklyCost);
            Assert.AreEqual(FirstDue, row.dueDay);
            Assert.AreEqual(80f, row.maxHP, 0.0001f);
            Assert.AreEqual(80f, row.currentHP, 0.0001f);
            Assert.IsTrue(row.isAlive);
        }

        [Test]
        public void Due_SnapshotIsPure_DoesNotChargeOrMutate()
        {
            HireBaseline();
            int gold = Gold;
            int ledger = ContractLedger;
            SoldierWageSystem.GetDueMercenaryContracts(FirstDue + 50);
            Assert.AreEqual(gold, Gold);
            Assert.AreEqual(ledger, ContractLedger);
            Assert.AreEqual(FirstDue, SoldierWageSystem.GetNextContractDay(MercId), "조회만으로 nextDay 불변");
        }

        // ── 알림 (중복 발화 / 상태 불변) ───────────────────────────────

        [Test]
        public void Notify_DoesNotChargeOrFire_EvenWhenDue()
        {
            HireBaseline();
            int gold = Gold;
            SoldierWageSystem.NotifyDueMercenaryContracts(FirstDue);
            Assert.AreEqual(gold, Gold, "알림은 청구하지 않는다");
            Assert.IsTrue(_mercMgr.TryGetHiredMercenary(MercId, out _), "알림은 해고하지 않는다");
            Assert.AreEqual(FirstDue, SoldierWageSystem.GetNextContractDay(MercId), "알림은 nextDay를 바꾸지 않는다");
        }

        [Test]
        public void Notify_DuplicateSameDay_FiresOnceAndDoesNotMutate()
        {
            HireBaseline();
            SoldierWageSystem.OnMercenaryContractPaymentsDue += d => _notifyCount++;

            SoldierWageSystem.NotifyDueMercenaryContracts(FirstDue);
            int gold = Gold;
            int ledger = ContractLedger;
            int hired = _mercMgr.HiredCount;
            var first = SoldierWageSystem.GetDueMercenaryContracts(FirstDue);

            var second = SoldierWageSystem.NotifyDueMercenaryContracts(FirstDue);

            Assert.AreEqual(1, _notifyCount, "같은 날 중복 알림은 1회만 발화");
            Assert.AreEqual(gold, Gold);
            Assert.AreEqual(ledger, ContractLedger);
            Assert.AreEqual(hired, _mercMgr.HiredCount);
            Assert.AreEqual(FirstDue, SoldierWageSystem.GetNextContractDay(MercId));
            Assert.AreEqual(first.Length, second.Length);
            Assert.AreEqual(first[0].mercenaryId, second[0].mercenaryId);
            Assert.AreEqual(first[0].dueDay, second[0].dueDay);
        }

        [Test]
        public void Notify_NewDay_FiresAgain()
        {
            HireBaseline();
            SoldierWageSystem.OnMercenaryContractPaymentsDue += d => _notifyCount++;
            SoldierWageSystem.NotifyDueMercenaryContracts(FirstDue);
            SoldierWageSystem.NotifyDueMercenaryContracts(FirstDue + 1);
            Assert.AreEqual(2, _notifyCount);
        }

        [Test]
        public void Notify_ReloadWithSameDueDay_DoesNotMoveSchedule()
        {
            HireBaseline();
            SoldierWageSystem.NotifyDueMercenaryContracts(FirstDue);
            SoldierWageSystem.ImportSaveData(SoldierWageSystem.ExportSaveData(), FirstDue);
            SoldierWageSystem.NotifyDueMercenaryContracts(FirstDue);
            Assert.AreEqual(FirstDue, SoldierWageSystem.GetNextContractDay(MercId), "재로드 후 틱에서도 청구일 불변");
        }

        // ── 승인 (TryApproveMercenaryContract) ─────────────────────────

        [Test]
        public void Approve_NotDue_ReturnsNotDue_NoChange()
        {
            HireBaseline();
            int gold = Gold;
            var r = SoldierWageSystem.TryApproveMercenaryContract(MercId, FirstDue - 1, out int charged, out int next);
            Assert.AreEqual(SoldierWageSystem.MercenaryContractResult.NotDue, r);
            Assert.AreEqual(0, charged);
            Assert.AreEqual(FirstDue, next);
            Assert.AreEqual(gold, Gold);
        }

        [Test]
        public void Approve_NotHired_ReturnsNotHired()
        {
            var r = SoldierWageSystem.TryApproveMercenaryContract(MercId, FirstDue, out int charged, out _);
            Assert.AreEqual(SoldierWageSystem.MercenaryContractResult.NotHired, r);
            Assert.AreEqual(0, charged);
        }

        [Test]
        public void Approve_DueSufficientGold_ChargesOnceAndAdvancesSevenDays()
        {
            HireBaseline();
            int gold = Gold;
            int ledger = ContractLedger;
            int cost = HireCost;

            var r = SoldierWageSystem.TryApproveMercenaryContract(MercId, FirstDue, out int charged, out int next);

            Assert.AreEqual(SoldierWageSystem.MercenaryContractResult.Success, r);
            Assert.AreEqual(cost, charged);
            Assert.AreEqual(gold - cost, Gold);
            Assert.AreEqual(ledger + cost, ContractLedger, "계약비 원장 +1회");
            Assert.AreEqual(FirstDue + 7, next);
            Assert.AreEqual(FirstDue + 7, SoldierWageSystem.GetNextContractDay(MercId));
            Assert.IsTrue(_mercMgr.TryGetHiredMercenary(MercId, out _), "승인은 고용 유지");
        }

        [Test]
        public void Approve_DoubleApproval_NoDoubleChargeOrDateAdvance()
        {
            HireBaseline();
            SoldierWageSystem.TryApproveMercenaryContract(MercId, FirstDue, out _, out _);
            int gold = Gold;
            int ledger = ContractLedger;
            int next = SoldierWageSystem.GetNextContractDay(MercId);

            var r = SoldierWageSystem.TryApproveMercenaryContract(MercId, FirstDue, out int charged, out int nextAfter);

            Assert.AreEqual(SoldierWageSystem.MercenaryContractResult.NotDue, r, "같은 날 재승인은 due가 아님");
            Assert.AreEqual(0, charged);
            Assert.AreEqual(next, nextAfter);
            Assert.AreEqual(gold, Gold, "이중 청구 없음");
            Assert.AreEqual(ledger, ContractLedger, "원장 이중 기록 없음");
            Assert.AreEqual(next, SoldierWageSystem.GetNextContractDay(MercId), "날짜 이중 전진 없음");
        }

        [Test]
        public void Approve_InsufficientGold_StaysHiredAndDue_NoCharge()
        {
            HireBaseline();
            int cost = HireCost;
            SetGold(cost - 1);
            int ledger = ContractLedger;

            var r = SoldierWageSystem.TryApproveMercenaryContract(MercId, FirstDue, out int charged, out int next);

            Assert.AreEqual(SoldierWageSystem.MercenaryContractResult.InsufficientFunds, r);
            Assert.AreEqual(0, charged);
            Assert.AreEqual(FirstDue, next, "청구일 불변 (여전히 due)");
            Assert.AreEqual(cost - 1, Gold, "골드 불변");
            Assert.AreEqual(ledger, ContractLedger, "실패는 원장 불변");
            Assert.IsTrue(_mercMgr.TryGetHiredMercenary(MercId, out _), "부족해도 해고하지 않는다");
            Assert.AreEqual(1, SoldierWageSystem.GetDueMercenaryContracts(FirstDue).Length, "여전히 due 목록에 있음");
        }

        [Test]
        public void Approve_InsufficientGold_ThenTopUp_ApprovalSucceeds()
        {
            HireBaseline();
            int cost = HireCost;
            SetGold(cost - 1);
            SoldierWageSystem.TryApproveMercenaryContract(MercId, FirstDue, out _, out _);
            SetGold(cost);
            var r = SoldierWageSystem.TryApproveMercenaryContract(MercId, FirstDue, out int charged, out int next);
            Assert.AreEqual(SoldierWageSystem.MercenaryContractResult.Success, r);
            Assert.AreEqual(cost, charged);
            Assert.AreEqual(FirstDue + 7, next);
        }

        [Test]
        public void Approve_MissedMultipleWeeks_ChargesOnceAndAdvancesBeyondCurrentDay_NoBackBilling()
        {
            HireBaseline();
            int cost = HireCost;
            int gold = Gold;
            int ledger = ContractLedger;
            int currentDay = FirstDue + 21; // 3주 경과 (due=8 → 29)

            var r = SoldierWageSystem.TryApproveMercenaryContract(MercId, currentDay, out int charged, out int next);

            Assert.AreEqual(SoldierWageSystem.MercenaryContractResult.Success, r);
            Assert.AreEqual(cost, charged, "놓친 주 전부가 아니라 1회 주간 비용만 청구");
            Assert.AreEqual(gold - cost, Gold);
            Assert.AreEqual(ledger + cost, ContractLedger);
            Assert.Greater(next, currentDay, "다음 청구일은 현재일보다 뒤");
            Assert.AreEqual(currentDay + 7, next, "due+7k 중 현재일 초과 첫 날 (36)");
            Assert.AreEqual(next, SoldierWageSystem.GetNextContractDay(MercId));
        }

        [Test]
        public void Approve_UnknownOrEmptyId_ReturnsNotHired()
        {
            Assert.AreEqual(SoldierWageSystem.MercenaryContractResult.NotHired,
                SoldierWageSystem.TryApproveMercenaryContract("merc_nope", 100, out _, out _));
            Assert.AreEqual(SoldierWageSystem.MercenaryContractResult.NotHired,
                SoldierWageSystem.TryApproveMercenaryContract(null, 100, out _, out _));
        }

        // ── 해지 (ReleaseMercenaryContract) ───────────────────────────

        [Test]
        public void Release_FiresWithoutCharge_AndClearsSchedule()
        {
            HireBaseline();
            int gold = Gold;
            int ledger = ContractLedger;

            var r = SoldierWageSystem.ReleaseMercenaryContract(MercId);

            Assert.AreEqual(SoldierWageSystem.MercenaryContractResult.Success, r);
            Assert.AreEqual(gold, Gold, "해지는 청구하지 않는다");
            Assert.AreEqual(ledger, ContractLedger);
            Assert.IsFalse(_mercMgr.TryGetHiredMercenary(MercId, out _));
            Assert.AreEqual(-1, SoldierWageSystem.GetNextContractDay(MercId), "스케줄 원자적 제거");
            Assert.AreEqual(0, SoldierWageSystem.GetDueMercenaryContracts(FirstDue + 100).Length);
        }

        [Test]
        public void Release_NotHired_ReturnsNotHired_NoCharge()
        {
            int gold = Gold;
            var r = SoldierWageSystem.ReleaseMercenaryContract(MercId);
            Assert.AreEqual(SoldierWageSystem.MercenaryContractResult.NotHired, r);
            Assert.AreEqual(gold, Gold);
        }

        [Test]
        public void FireMercenary_ClearsContractSchedule()
        {
            HireBaseline();
            Assert.IsTrue(_mercMgr.FireMercenary(MercId));
            Assert.AreEqual(-1, SoldierWageSystem.GetNextContractDay(MercId), "직접 해고도 스케줄을 남기지 않는다");
        }

        [Test]
        public void Approve_AfterRelease_ReturnsNotHired()
        {
            HireBaseline();
            SoldierWageSystem.ReleaseMercenaryContract(MercId);
            int gold = Gold;
            var r = SoldierWageSystem.TryApproveMercenaryContract(MercId, FirstDue, out int charged, out _);
            Assert.AreEqual(SoldierWageSystem.MercenaryContractResult.NotHired, r);
            Assert.AreEqual(0, charged);
            Assert.AreEqual(gold, Gold);
        }
    }
}
