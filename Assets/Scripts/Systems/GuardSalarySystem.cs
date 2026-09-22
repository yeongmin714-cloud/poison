using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase E-2a: 병사 유지비(일일 급료) 시스템
    ///
    /// - 포섭된 병사는 매일 유지비를 요구한다 (레벨/체력/스탯↑ = 유지비↑, 충성도↓ = 유지비↑).
    /// - 골드가 부족하면 가장 비싼 병사부터 LaborMarketSystem.ReleaseGuard로 방출하며 재시도.
    /// - 충성도 임계 이상 병사는 급료 인상을 요구할 수 있고, 수락 시 유지비가 영구 인상된다.
    /// 결정론적 산식만 사용 (무작위 없음, System.Random 미사용).
    /// </summary>
    public static class GuardSalarySystem
    {
        // ===== 유지비 산식 상수 =====
        public const int BASE_DAILY_COST = 2;            // 병사당 기본 유지비
        public const int COST_PER_LEVEL = 2;             // 레벨 1당 추가 유지비
        public const float COST_PER_MAXHP = 0.05f;       // MaxHP 1당 추가 유지비
        public const int STAT_POINTS_PER_COST = 2;       // 스탯 총합 2점당 +1 (강할수록 가산)
        public const int RAISE_BONUS_COST = 5;           // 급료 인상 수락 시 영구 가산액

        // ===== 충성도 보정 =====
        public const int ACCEPT_LOYALTY_BONUS = 10;      // 인상 수락 시 충성도 +
        public const int REJECT_LOYALTY_PENALTY = 20;    // 인상 거절 시 충성도 -
        public const float PAYRAISE_DEMAND_LOYALTY_AT = 50f; // 이 값 이상 충성도 → 인상 요구 가능

        // ===== 방출 폴백 =====
        public const int MAX_RELEASE_RETRIES = 100;      // 방출 재시도 상한 (무한루프 방지)

        // ===== 내부 상태 =====
        /// <summary>급료 인상을 수락하여 유지비가 영구 인상된 병사 집합.</summary>
        private static readonly HashSet<GuardPlaceholder> _raisedGuards = new HashSet<GuardPlaceholder>();
        /// <summary>인상을 요구했으나 아직 응답받지 못한 병사 집합.</summary>
        private static readonly HashSet<GuardPlaceholder> _pendingRaiseGuards = new HashSet<GuardPlaceholder>();
        /// <summary>마지막으로 급료를 지불한 일차 (중복 청구 방지, -1 = 미지불).</summary>
        private static int _lastPaidDay = -1;

        // ===== 유지비 산식 =====
        /// <summary>
        /// 병사 1명의 일일 유지비. 레벨·체력·스탯이 높을수록, 충성도가 낮을수록 비싸다.
        /// null/사망 병사는 0, 그 외에는 최소 1을 반환한다.
        /// </summary>
        public static int GetDailyCost(GuardPlaceholder guard)
        {
            if (guard == null) return 0;
            if (!guard.IsAlive) return 0;
            // [Phase E-2 QA 픽스] 방출(SetRecruited(false))된 병사는 유지비 미부과 — 방출 즉시 비용 0.
            // (방출 후에도 GetAllPlayerGuards에 잔존하므로 IsRecruited 기준으로 걸러야 익일 재부과 방지)
            if (!guard.IsRecruited) return 0;

            int cost = BASE_DAILY_COST
                     + guard.Level * COST_PER_LEVEL
                     + Mathf.RoundToInt(guard.MaxHP * COST_PER_MAXHP);

            // 강할수록 가산 (순수 스탯 총합 기반) — 기존 getter 재사용, 예외 시 가산 없이 진행
            try
            {
                int statTotal = guard.GetStatAttack() + guard.GetStatDefense()
                              + guard.GetStatVitality() + guard.GetStatAgility();
                cost += statTotal / STAT_POINTS_PER_COST;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GuardSalary] 스탯 조회 실패 — 가산 없이 진행: {e.Message}");
            }

            // 충성도 낮을수록 가산 (불만 보상) — 3단계
            float loyalty = guard.Loyalty;
            if (loyalty < -50f) cost += 3;
            else if (loyalty < 0f) cost += 2;
            else if (loyalty < 30f) cost += 1;

            // 급료 인상 수락분 영구 가산
            if (_raisedGuards.Contains(guard)) cost += RAISE_BONUS_COST;

            return Mathf.Max(1, cost);
        }

        /// <summary>병사 컬렉션의 일일 유지비 총합 (null 항목/컬렉션 안전).</summary>
        public static int GetTotalDailyCostFor(IEnumerable<GuardPlaceholder> guards)
        {
            if (guards == null) return 0;
            int total = 0;
            try
            {
                foreach (var guard in guards)
                    total += GetDailyCost(guard);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GuardSalary] 유지비 합산 중 오류: {e.Message}");
            }
            return total;
        }

        /// <summary>포섭 병사 전체의 일일 유지비 총합 (manager/목록 null 안전).</summary>
        public static int GetTotalDailyCost()
        {
            return GetTotalDailyCostFor(TryGetAllPlayerGuards());
        }

        // ===== 일일 급료 청구 =====
        /// <summary>
        /// 일일 급료 청구. 같은 일차 재호출 시 true 반환(멱등).
        /// 골드 부족 시 가장 비싼 병사부터 LaborMarketSystem.ReleaseGuard로 방출해
        /// 유지비를 낮추며 재시도(최대 MAX_RELEASE_RETRIES회), 그래도 부족하면
        /// 방출된 상태 그대로 false 반환(미납).
        /// </summary>
        public static bool TryPayDailyWages(int currentDay)
        {
            if (currentDay == _lastPaidDay)
            {
                Debug.Log($"[GuardSalary] {currentDay}일차 급료는 이미 청구됨 — 스킵");
                return true;
            }

            var player = PlayerStats.Instance;
            if (player == null)
            {
                Debug.LogWarning("[GuardSalary] PlayerStats.Instance 없음 — 급료 청구 불가");
                return false;
            }

            int total = GetTotalDailyCost();
            if (total <= 0)
            {
                _lastPaidDay = currentDay;
                Debug.Log("[GuardSalary] 유지비 대상 병사 없음 — 청구 완료 처리");
                return true;
            }

            // 1차: 전액 지불 시도
            if (player.SpendGold(total, "daily_wages"))
            {
                _lastPaidDay = currentDay;
                Debug.Log($"[GuardSalary] {currentDay}일차 급료 지불 완료 ({total}G)");
                return true;
            }

            // 폴백: 가장 비싼 병사부터 방출하며 유지비를 낮춘 뒤 재시도
            var manager = GuardManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning("[GuardSalary] GuardManager.Instance 없음 — 방출 폴백 불가");
                return false;
            }

            int remaining = total;
            var released = new HashSet<GuardPlaceholder>();

            for (int attempt = 0; attempt < MAX_RELEASE_RETRIES; attempt++)
            {
                // 방출 대상: 아직 방출하지 않은 살아있는 병사 중 최고 유지비
                GuardPlaceholder candidate = null;
                int candidateCost = 0;

                var guards = TryGetAllPlayerGuards();
                if (guards != null)
                {
                    foreach (var guard in guards)
                    {
                        if (guard == null || !guard.IsAlive) continue;
                        if (released.Contains(guard)) continue; // 이번 청구에서 이미 방출됨
                        int c = GetDailyCost(guard);
                        if (c > candidateCost) { candidateCost = c; candidate = guard; }
                    }
                }

                if (candidate == null) break; // 방출 대상 소진

                Debug.Log($"[GuardSalary] 골드 부족 — 최고가 병사 방출: {candidate.GuardName} ({candidateCost}G)");
                try
                {
                    LaborMarketSystem.ReleaseGuard(candidate);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[GuardSalary] 방출 처리 실패 — 폴백 중단: {e.Message}");
                    break;
                }
                released.Add(candidate);
                remaining = Mathf.Max(0, remaining - candidateCost);

                // 유지비가 낮아진 상태로 재시도
                if (player.SpendGold(remaining, "daily_wages"))
                {
                    _lastPaidDay = currentDay;
                    Debug.Log($"[GuardSalary] 병사 {released.Count}명 방출 후 급료 지불 완료 ({remaining}G)");
                    return true;
                }
            }

            Debug.LogWarning($"[GuardSalary] {currentDay}일차 급료 지불 실패 — 골드 부족 (방출 {released.Count}명, 미납 {remaining}G)");
            return false;
        }

        // ===== 급료 인상 이벤트 =====
        /// <summary>인상 요구 조건 충족 여부 (충성도 임계 기반, 배선용 판정 헬퍼).</summary>
        public static bool ShouldRequestPayRaise(GuardPlaceholder guard)
        {
            if (guard == null) return false;
            if (!guard.IsAlive || !guard.IsRecruited) return false;
            if (_raisedGuards.Contains(guard) || _pendingRaiseGuards.Contains(guard)) return false;
            return guard.Loyalty >= PAYRAISE_DEMAND_LOYALTY_AT;
        }

        /// <summary>
        /// 병사가 급료 인상을 요구. 충성도 임계 이상일 때만 대기 목록에 등록(멱등).
        /// 실제 수락/거절 처리는 HandlePayRaiseResponse에서 수행.
        /// </summary>
        public static void RequestPayRaise(GuardPlaceholder guard)
        {
            if (guard == null) return;
            if (!guard.IsAlive) return;
            if (_raisedGuards.Contains(guard)) return;       // 이미 인상됨
            if (_pendingRaiseGuards.Contains(guard)) return; // 이미 대기 중 — 멱등

            if (guard.Loyalty < PAYRAISE_DEMAND_LOYALTY_AT)
            {
                Debug.Log($"[GuardSalary] {guard.GuardName} 인상 요구 조건 미달 (충성도 {guard.Loyalty} < {PAYRAISE_DEMAND_LOYALTY_AT})");
                return;
            }

            _pendingRaiseGuards.Add(guard);
            Debug.Log($"[GuardSalary] {guard.GuardName} 급료 인상 요구 (충성도 {guard.Loyalty})");
        }

        /// <summary>
        /// 인상 요구에 대한 응답 처리.
        /// 수락 → 유지비 영구 인상(_raisedGuards 등록, 일일 +RAISE_BONUS_COST) + 충성도 +10.
        /// 거절 → 충성도 -20. 대기 중인 요구가 없으면 무시(멱등).
        /// </summary>
        public static void HandlePayRaiseResponse(GuardPlaceholder guard, bool accepted)
        {
            if (guard == null) return;
            if (!_pendingRaiseGuards.Remove(guard)) return; // 대기 중인 요구 없음 — 중복 응답 무시

            if (accepted)
            {
                _raisedGuards.Add(guard);
                guard.Loyalty += ACCEPT_LOYALTY_BONUS; // setter에서 -100~100 클램프
                Debug.Log($"[GuardSalary] {guard.GuardName} 인상 수락 — 일일 유지비 +{RAISE_BONUS_COST}G, 충성도 +{ACCEPT_LOYALTY_BONUS}");
            }
            else
            {
                guard.Loyalty -= REJECT_LOYALTY_PENALTY; // setter에서 -100~100 클램프
                Debug.Log($"[GuardSalary] {guard.GuardName} 인상 거절 — 충성도 -{REJECT_LOYALTY_PENALTY}");
            }
        }

        /// <summary>아직 응답받지 않은 인상 요구가 대기 중인지.</summary>
        public static bool HasPendingRaise(GuardPlaceholder guard)
            => guard != null && _pendingRaiseGuards.Contains(guard);

        /// <summary>이미 인상이 수락되어 유지비가 영구 인상되었는지.</summary>
        public static bool HasRaised(GuardPlaceholder guard)
            => guard != null && _raisedGuards.Contains(guard);

        // ===== 내부 헬퍼 =====
        /// <summary>포섭 병사 목록 안전 조회 (manager/목록/예외 시 null 반환).</summary>
        private static List<GuardPlaceholder> TryGetAllPlayerGuards()
        {
            try
            {
                return GuardManager.Instance?.GetAllPlayerGuards();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GuardSalary] 병사 목록 조회 실패: {e.Message}");
                return null;
            }
        }

        // ===== 리셋 (테스트) =====
        /// <summary>모든 static 상태 초기화 (테스트용).</summary>
        public static void ResetAll()
        {
            _raisedGuards.Clear();
            _pendingRaiseGuards.Clear();
            _lastPaidDay = -1;
            Debug.Log("[GuardSalary] 모든 상태 초기화 완료");
        }
    }
}
