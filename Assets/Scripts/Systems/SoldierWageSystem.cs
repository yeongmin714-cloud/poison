using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// 병사 입단비(편입 1회 청구) + 용병 계약비(입단비 + 개별 주간 계약비) 정책.
    ///
    /// 계약 (사용자 승인 범위):
    ///  - 일반 병사: 플레이어 편입(포섭) 시 입단비 1회. 기존 일일 급료(GuardSalarySystem)는 그대로 유지.
    ///  - 용병: 고용 시 입단비 + 첫 주 계약비(MercenaryData.hireCost) 즉시 청구.
    ///          이후 고용일 기준 7 게임일마다 hireCost 청구. 부족하면 계약 종료(해고).
    ///  - 모든 지출은 PlayerStats.SpendGold 단일 지갑 + 원장 source 태그로 기록.
    ///  - 편입 확정(상태 변경) 전에 청구하고, 청구 실패 시 상태를 바꾸지 않는다.
    ///
    /// 결정론: 무작위 미사용. 입단비 변동은 (stableId, admissionSeq) 해시로 고정.
    /// 영속화: ExportSaveData/ImportSaveData (SaveData v4 soldierWage 블록).
    ///   - 용병 다음 계약 청구일과 병사 입단 누적 카운트(stableId 키)를 저장한다.
    ///   - 병사 stableId는 세션 내 생성 순서 기반이라 재로드 후 동일 병사에 대응하지 않을 수 있다(병사 영속화 미구현).
    ///     따라서 입단 카운트 복원은 "같은 세션/같은 ID 유지" 범위에서만 의미가 있다.
    ///   - 복원은 어떤 골드도 청구하지 않는다. 같은 주기 중복 청구는 저장된 다음 청구일로 막는다.
    /// </summary>
    public static class SoldierWageSystem
    {
        // ===== 원장 source 태그 =====
        public const string SourceSoldierHireFee = "soldier_hire_fee";
        public const string SourceMercenaryHireFee = "mercenary_hire_fee";
        public const string SourceMercenaryContractFee = "mercenary_contract_fee";
        public const string SourceMercenaryHireRefund = "mercenary_hire_refund";

        // ===== 입단비 산식 계수 =====
        public const float FEE_BASE = 20f;
        /// <summary>하한 (테스트에서 조정 가능하도록 static 유지).</summary>
        public static int FEE_MIN = 5;
        public const float LEVEL_W = 8f;
        public const float MAXHP_W = 0.2f;
        public const float STAT_W = 0.5f;
        public const float FEE_VARIANCE_PCT = 0.15f;

        // ===== 계약 주기 =====
        public const int CONTRACT_INTERVAL_DAYS = 7;

        // ===== 내부 상태 =====
        /// <summary>병사 stableId → 편입 누적 횟수 (재편입마다 +1).</summary>
        private static readonly Dictionary<string, int> _admissionCounts = new Dictionary<string, int>();
        /// <summary>용병 id → 다음 계약비 청구일 (게임일).</summary>
        private static readonly Dictionary<string, int> _nextContractDay = new Dictionary<string, int>();

        // ===== 등급 계수 =====
        /// <summary>등급별 입단비 가산.</summary>
        public static int GetGradeFee(MercenaryGrade grade)
        {
            switch (grade)
            {
                case MercenaryGrade.High: return 150;
                case MercenaryGrade.Elite: return 600;
                case MercenaryGrade.Legendary: return 1500;
                default: return 0;
            }
        }

        // ===== 병사 입단비 =====

        private static int StatTotal(GuardPlaceholder g)
            => g.GetStatAttack() + g.GetStatDefense() + g.GetStatVitality() + g.GetStatAgility();

        /// <summary>변동 전 입단비 (반올림). null → 0.</summary>
        public static int ComputeRawAdmissionFee(GuardPlaceholder guard)
        {
            if (guard == null) return 0;
            float raw = FEE_BASE
                      + LEVEL_W * guard.Level
                      + MAXHP_W * guard.MaxHP
                      + STAT_W * StatTotal(guard);
            return Mathf.Max(0, Mathf.RoundToInt(raw));
        }

        /// <summary>(stableId, seq) 고정 변동 배율. 범위 [1-PCT, 1+PCT).</summary>
        public static float VarianceMultiplier(string stableId, int seq)
        {
            float h = StableHash01(stableId, seq);
            return 1f + (2f * h - 1f) * FEE_VARIANCE_PCT;
        }

        /// <summary>편입 1건의 입단비 (변동 + 하한 클램프). null → 0.</summary>
        public static int ComputeAdmissionFee(GuardPlaceholder guard, int admissionSeq)
        {
            if (guard == null) return 0;
            int raw = ComputeRawAdmissionFee(guard);
            int fee = Mathf.RoundToInt(raw * VarianceMultiplier(guard.StableId, admissionSeq));
            return Mathf.Max(FEE_MIN, fee);
        }

        /// <summary>다음 편입 건(누적 seq + 1) 기준 예상 입단비.</summary>
        public static int PeekGuardAdmissionFee(GuardPlaceholder guard)
        {
            if (guard == null) return 0;
            return ComputeAdmissionFee(guard, GetAdmissionCount(guard.StableId) + 1);
        }

        /// <summary>병사 편입 누적 횟수.</summary>
        public static int GetAdmissionCount(string stableId)
        {
            if (string.IsNullOrEmpty(stableId)) return 0;
            return _admissionCounts.TryGetValue(stableId, out int n) ? n : 0;
        }

        /// <summary>
        /// 외부(AI 고용 시장 등) 입단 성공 기록. 호출부가 입단비를 이미 자기 금고에서 차감하고
        /// 로스터 배정까지 성공한 뒤에만 호출해야 한다. PlayerStats는 건드리지 않는다.
        /// 병사 편입 누적 카운트(stableId 키)를 +1 한다. null/빈 stableId면 false.
        /// </summary>
        public static bool RecordExternalGuardAdmission(GuardPlaceholder guard)
        {
            if (guard == null) return false;
            string id = guard.StableId;
            if (string.IsNullOrEmpty(id)) return false;

            _admissionCounts.TryGetValue(id, out int current);
            _admissionCounts[id] = current + 1;
            return true;
        }

        /// <summary>
        /// 플레이어 편입 청구 (상태 변경 전 호출). 이미 소속이면 0G 성공(멱등).
        /// 골드 부족·PlayerStats 부재·대상 없음/사망 시 false, 상태 불변.
        /// 호출부는 true 반환 후에만 SetRecruited(true)를 호출해야 한다.
        /// </summary>
        public static bool TryChargeGuardAdmissionFee(GuardPlaceholder guard, string route, out int feePaid)
        {
            feePaid = 0;
            if (guard == null) return false;
            if (guard.IsRecruited) return true;          // 이미 소속 — 이중 청구 없음
            if (!guard.IsAlive) return false;

            var stats = PlayerStats.Instance;
            if (stats == null)
            {
                Debug.LogWarning("[SoldierWage] PlayerStats 없음 — 입단비 청구 불가");
                return false;
            }

            string id = guard.StableId;
            int seq = GetAdmissionCount(id) + 1;
            int fee = ComputeAdmissionFee(guard, seq);

            if (!stats.SpendGold(fee, SourceSoldierHireFee))
            {
                Debug.LogWarning($"[SoldierWage] {guard.GuardName} 입단비 {fee}G 부족 — 편입 보류 (route={route})");
                return false;
            }

            _admissionCounts[id] = seq;
            feePaid = fee;
            Debug.Log($"[SoldierWage] {guard.GuardName} 입단비 {fee}G 청구 (seq={seq}, route={route})");
            return true;
        }

        // ===== 용병 =====

        /// <summary>용병 입단비 (등급 + 스탯 기반, 변동 없음). 하한 FEE_MIN.</summary>
        public static int GetMercenaryAdmissionFee(MercenaryData merc)
        {
            float raw = FEE_BASE
                      + MAXHP_W * merc.maxHP
                      + STAT_W * (merc.attack + merc.defense)
                      + GetGradeFee(merc.grade);
            return Mathf.Max(FEE_MIN, Mathf.RoundToInt(raw));
        }

        /// <summary>주간 계약비 (데이터 개별 값). 음수는 0.</summary>
        public static int GetMercenaryWeeklyContractCost(MercenaryData merc)
            => Mathf.Max(0, merc.hireCost);

        /// <summary>고용 즉시 필요한 총액 (입단비 + 첫 주 계약비).</summary>
        public static int GetMercenaryUpfrontCost(MercenaryData merc)
            => GetMercenaryAdmissionFee(merc) + GetMercenaryWeeklyContractCost(merc);

        /// <summary>
        /// 용병 고용 청구 (상태 변경 전 호출). 골드가 총액 미만이면 아무것도 지불하지 않고 false.
        /// 입단비/첫 주 계약비를 각각 원장 태그로 기록. 두 번째 지불 실패 시 첫 지불을 환불.
        /// 성공 시 다음 계약 청구일 = 오늘 + 7.
        /// </summary>
        public static bool TryChargeMercenaryHire(MercenaryData merc, out int admissionPaid, out int firstContractPaid)
        {
            admissionPaid = 0;
            firstContractPaid = 0;
            if (!merc.IsValid) return false;

            var stats = PlayerStats.Instance;
            if (stats == null)
            {
                Debug.LogWarning("[SoldierWage] PlayerStats 없음 — 용병 고용 불가");
                return false;
            }

            int admission = GetMercenaryAdmissionFee(merc);
            int contract = GetMercenaryWeeklyContractCost(merc);
            if (stats.Gold < admission + contract)
            {
                Debug.LogWarning($"[SoldierWage] {merc.mercenaryName} 고용 비용 {admission + contract}G 부족 (보유 {stats.Gold}G)");
                return false;
            }

            if (!stats.SpendGold(admission, SourceMercenaryHireFee)) return false;

            if (contract > 0 && !stats.SpendGold(contract, SourceMercenaryContractFee))
            {
                // 롤백: 입단비 환불 (골드 증발 방지)
                stats.AddGold(admission, SourceMercenaryHireRefund);
                return false;
            }

            _nextContractDay[merc.id] = CurrentDay() + CONTRACT_INTERVAL_DAYS;
            admissionPaid = admission;
            firstContractPaid = contract;
            Debug.Log($"[SoldierWage] {merc.mercenaryName} 고용: 입단비 {admission}G + 첫 주 계약 {contract}G (다음 청구일 {_nextContractDay[merc.id]})");
            return true;
        }

        // ===== 용병 계약 due 상태 (UI 없음: 조회/승인/해지 API) =====

        /// <summary>계약 승인/해지 결과. Success 외에는 상태 불변.</summary>
        public enum MercenaryContractResult
        {
            Success = 0,
            NoMercenaryManager = 1,
            /// <summary>고용 중이 아님 (해고됨/미고용/빈 id).</summary>
            NotHired = 2,
            /// <summary>청구일이 아직 도래하지 않았거나 스케줄이 없음.</summary>
            NotDue = 3,
            /// <summary>주간 계약비 부족. 고용 유지 + due 유지.</summary>
            InsufficientFunds = 4,
            NoPlayerStats = 5,
        }

        /// <summary>due 용병 1행 스냅샷 (UI 읽기 전용). 고용 로스터 순서로 정렬.</summary>
        public struct DueMercenaryContract
        {
            public string mercenaryId;
            public string mercenaryName;
            public string jobType;
            public MercenaryGrade grade;
            /// <summary>주간 계약비 (MercenaryData.hireCost, 0 이상).</summary>
            public int weeklyCost;
            /// <summary>현재 계약 청구일 (nextContractDay, 게임일). 이 날 이하이면 due.</summary>
            public int dueDay;
            public float currentHP;
            public float maxHP;
            public bool isAlive;
        }

        /// <summary>
        /// 게임일 경계 알림. 인자: 게임일. 같은 날 중복 발화하지 않는다.
        /// 구독자는 읽기 전용으로 취급해야 한다 (UI 갱신용). 청구/해고/날짜 변경을 직접 하지 않는다.
        /// 구독 해제는 호출자 책임(테스트/UI OnDisable).
        /// </summary>
        public static event System.Action<int> OnMercenaryContractPaymentsDue;

        /// <summary>마지막으로 OnMercenaryContractPaymentsDue를 발화한 게임일 (중복 발화 방지, 비영속).</summary>
        private static int _lastDueNotifiedDay = int.MinValue;

        /// <summary>
        /// 현재 due인 고용 용병 스냅샷 (순수 조회, 상태 변경 없음).
        /// 대상: MercenaryManager.GetHiredMercenaries()(복사본)에 있고 nextContractDay &lt;= day 인 용병.
        /// 스케줄이 없는 고용 용병과 로스터에 없는 스케줄은 포함하지 않는다.
        /// </summary>
        public static DueMercenaryContract[] GetDueMercenaryContracts(int day)
        {
            var mgr = MercenaryManager.Instance;
            if (mgr == null) return new DueMercenaryContract[0];

            var rows = new List<DueMercenaryContract>();
            foreach (var inst in mgr.GetHiredMercenaries())
            {
                var merc = inst.data;
                if (!_nextContractDay.TryGetValue(merc.id, out int due)) continue;
                if (due > day) continue;

                rows.Add(new DueMercenaryContract
                {
                    mercenaryId = merc.id,
                    mercenaryName = merc.mercenaryName,
                    jobType = merc.jobType,
                    grade = merc.grade,
                    weeklyCost = GetMercenaryWeeklyContractCost(merc),
                    dueDay = due,
                    currentHP = inst.currentHP,
                    maxHP = merc.maxHP,
                    isAlive = inst.isAlive,
                });
            }
            return rows.ToArray();
        }

        /// <summary>
        /// 게임일 틱 (GuardSalaryManager.OnDayStart, 일반 병사 급료 이후 호출).
        /// 1) 스케줄이 없는 고용 용병에만 day+7 시드 (기존 스케줄은 변경하지 않음).
        /// 2) due 스냅샷 계산 (순수).
        /// 3) 같은 날 첫 호출에만 OnMercenaryContractPaymentsDue(day) 발화.
        /// 골드 청구·해고·nextDay 변경은 하지 않는다 (승인은 TryApproveMercenaryContract).
        /// 같은 날 재호출/재로드 후에도 상태는 동일하다.
        /// </summary>
        public static DueMercenaryContract[] NotifyDueMercenaryContracts(int day)
        {
            var mgr = MercenaryManager.Instance;
            if (mgr == null) return new DueMercenaryContract[0];

            foreach (var inst in mgr.GetHiredMercenaries())
            {
                string id = inst.data.id;
                if (!_nextContractDay.ContainsKey(id))
                    _nextContractDay[id] = day + CONTRACT_INTERVAL_DAYS; // 이 시스템 밖에서 고용된 용병 시드
            }

            var due = GetDueMercenaryContracts(day);

            if (day != _lastDueNotifiedDay)
            {
                _lastDueNotifiedDay = day;
                OnMercenaryContractPaymentsDue?.Invoke(day);
            }
            return due;
        }

        /// <summary>
        /// 주간 계약 승인. 조건: 고용 중 && 스케줄 존재 && nextDay &lt;= currentDay.
        /// 주간 계약비를 정확히 1회 청구(부족 시 아무것도 바꾸지 않음), 청구일을
        /// (기존 청구일 + 7k) 중 currentDay 보다 큰 첫 날로 전진. 놓친 주는 소급 청구하지 않는다.
        /// 해고는 하지 않는다. chargedAmount/nextContractDay는 out (실패 시 chargedAmount=0, nextContractDay=현재값).
        /// </summary>
        public static MercenaryContractResult TryApproveMercenaryContract(
            string mercenaryId, int currentDay, out int chargedAmount, out int nextContractDay)
        {
            chargedAmount = 0;
            nextContractDay = GetNextContractDay(mercenaryId);

            var mgr = MercenaryManager.Instance;
            if (mgr == null) return MercenaryContractResult.NoMercenaryManager;
            if (string.IsNullOrEmpty(mercenaryId) || !mgr.TryGetHiredMercenary(mercenaryId, out var inst))
                return MercenaryContractResult.NotHired;
            if (!_nextContractDay.TryGetValue(mercenaryId, out int due) || due > currentDay)
                return MercenaryContractResult.NotDue;

            int cost = GetMercenaryWeeklyContractCost(inst.data);
            if (cost > 0)
            {
                var stats = PlayerStats.Instance;
                if (stats == null)
                {
                    Debug.LogWarning("[SoldierWage] PlayerStats 없음 — 계약 승인 불가");
                    return MercenaryContractResult.NoPlayerStats;
                }
                if (!stats.SpendGold(cost, SourceMercenaryContractFee))
                {
                    Debug.LogWarning($"[SoldierWage] {inst.data.mercenaryName} 주간 계약비 {cost}G 부족 — 고용 유지, 청구 보류");
                    return MercenaryContractResult.InsufficientFunds;
                }
                chargedAmount = cost;
            }

            int next = due;
            do { next += CONTRACT_INTERVAL_DAYS; } while (next <= currentDay);
            _nextContractDay[mercenaryId] = next;
            nextContractDay = next;

            Debug.Log($"[SoldierWage] {inst.data.mercenaryName} 계약 승인: {cost}G 청구, 다음 청구일 {next}");
            return MercenaryContractResult.Success;
        }

        /// <summary>
        /// 계약 해지: 청구 없이 용병 해고 + 계약 스케줄 제거를 한 호출에서 수행한다.
        /// 고용 중이 아니면 NotHired (스케줄이 남아 있었다면 함께 제거).
        /// </summary>
        public static MercenaryContractResult ReleaseMercenaryContract(string mercenaryId)
        {
            if (string.IsNullOrEmpty(mercenaryId)) return MercenaryContractResult.NotHired;

            var mgr = MercenaryManager.Instance;
            // 스케줄 먼저 제거 (스테일 방지). FireMercenary도 같은 정리를 수행한다.
            _nextContractDay.Remove(mercenaryId);
            if (mgr == null) return MercenaryContractResult.NoMercenaryManager;
            if (!mgr.FireMercenary(mercenaryId)) return MercenaryContractResult.NotHired;
            return MercenaryContractResult.Success;
        }

        /// <summary>
        /// 용병 계약 스케줄 제거 (해고 시 MercenaryManager.FireMercenary가 호출). 골드 변동 없음.
        /// </summary>
        public static void ClearContractSchedule(string mercenaryId)
        {
            if (string.IsNullOrEmpty(mercenaryId)) return;
            _nextContractDay.Remove(mercenaryId);
        }

        /// <summary>용병의 다음 계약비 청구일 (미등록이면 -1).</summary>
        public static int GetNextContractDay(string mercenaryId)
            => !string.IsNullOrEmpty(mercenaryId) && _nextContractDay.TryGetValue(mercenaryId, out int d) ? d : -1;

        // ===== 저장/복원 =====

        /// <summary>
        /// 병사 입단 누적 카운트와 용병 다음 계약 청구일을 DTO 리스트로 내보낸다 (정렬된 순서, 결정론).
        /// </summary>
        public static SoldierWageSaveData ExportSaveData()
        {
            var data = new SoldierWageSaveData();

            var admissionIds = new List<string>(_admissionCounts.Keys);
            admissionIds.Sort(string.CompareOrdinal);
            foreach (string id in admissionIds)
            {
                data.admissionCounts.Add(new AdmissionCountSaveEntry { stableId = id, count = _admissionCounts[id] });
            }

            var contractIds = new List<string>(_nextContractDay.Keys);
            contractIds.Sort(string.CompareOrdinal);
            foreach (string id in contractIds)
            {
                data.nextContractDays.Add(new ContractDaySaveEntry { mercenaryId = id, nextContractDay = _nextContractDay[id] });
            }

            return data;
        }

        /// <summary>
        /// 저장 데이터 복원. 용병 로스터(MercenaryManager)를 먼저 복원한 뒤 호출해야 한다.
        /// data == null (v3 이하 레거시 세이브): 입단 원장은 비우고(기존 편입 병사는 IsRecruited 가드로 이중 청구 없음),
        /// 현재 고용 용병의 계약 청구일을 currentDay + CONTRACT_INTERVAL_DAYS 로 시딩하여 소급 청구를 막는다.
        /// 저장된 항목이 없는 현재 고용 용병도 같은 방식으로 시딩한다.
        /// </summary>
        public static void ImportSaveData(SoldierWageSaveData data, int currentDay)
        {
            _admissionCounts.Clear();
            _nextContractDay.Clear();

            if (data != null)
            {
                if (data.admissionCounts != null)
                {
                    foreach (var e in data.admissionCounts)
                    {
                        if (e == null || string.IsNullOrEmpty(e.stableId) || e.count <= 0) continue;
                        _admissionCounts[e.stableId] = e.count;
                    }
                }
                if (data.nextContractDays != null)
                {
                    foreach (var e in data.nextContractDays)
                    {
                        if (e == null || string.IsNullOrEmpty(e.mercenaryId)) continue;
                        _nextContractDay[e.mercenaryId] = e.nextContractDay;
                    }
                }
            }

            // 현재 고용 용병 중 계약 스케줄이 없는 용병은 "오늘부터 다음 주"로 시딩 (소급 청구 없음)
            var mgr = MercenaryManager.Instance;
            if (mgr != null)
            {
                foreach (var inst in mgr.GetHiredMercenaries())
                {
                    string id = inst.data.id;
                    if (!_nextContractDay.ContainsKey(id))
                        _nextContractDay[id] = currentDay + CONTRACT_INTERVAL_DAYS;
                }
            }
        }

        // ===== 유틸 =====

        /// <summary>현재 게임일 (TimeManager 부재 시 1).</summary>
        public static int CurrentDay()
        {
            var tm = TimeManager.Instance;
            return tm != null ? tm.CurrentDay : 1;
        }

        /// <summary>FNV-1a 기반 결정론 해시 → [0,1). string.GetHashCode 미사용(런타임 비결정).</summary>
        public static float StableHash01(string key, int seq)
        {
            unchecked
            {
                uint h = 2166136261u;
                string s = key ?? string.Empty;
                for (int i = 0; i < s.Length; i++)
                {
                    h ^= s[i];
                    h *= 16777619u;
                }
                uint q = (uint)seq;
                for (int i = 0; i < 4; i++)
                {
                    h ^= (q >> (i * 8)) & 0xFFu;
                    h *= 16777619u;
                }
                // avalanche
                h ^= h >> 16; h *= 0x7feb352du;
                h ^= h >> 15; h *= 0x846ca68bu;
                h ^= h >> 16;
                float v = (float)((double)h / 4294967296.0);
                return Mathf.Min(v, 0.99999f);
            }
        }

        // ===== 리셋 (테스트) =====
        /// <summary>모든 static 상태 초기화 (테스트용).</summary>
        public static void ResetAll()
        {
            _admissionCounts.Clear();
            _nextContractDay.Clear();
            _lastDueNotifiedDay = int.MinValue;
            OnMercenaryContractPaymentsDue = null;
        }
    }
}
