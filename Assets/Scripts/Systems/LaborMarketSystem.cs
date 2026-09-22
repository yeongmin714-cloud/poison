using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core.Data;
#nullable enable

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase E-2b: 고용 시장(노동 시장) 시스템
    ///
    /// - 플레이어가 방출한 병사(GuardPlaceholder)를 '고용 시장 풀'에 등록한다.
    /// - AI 영주(LordOwned 영지)가 게임일 틱(DayStart)마다 성향에 따라 풀에서 병사를 재고용한다.
    /// - 공격 성향(GetAggression)이 높은 영주일수록 적극적으로 고용하며,
    ///   매 틱 최대 0~2명만 풀에서 빼서 aggression 내림차순 상위 영주부터 배정한다.
    /// - 방출 병사 → 시장 풀 → AI 재고용 → 플레이어 위협 병력으로 이어지는 순환 루프가 성립한다.
    ///
    /// 결정론 제약: System.Random 미사용 — 게임일 기반 결정론 해시로 틱당 고용 수를 정한다.
    /// 병력 추가는 TerritoryDefinition.guardCount 변조가 아니라 별도 인메모리 카운트(_hiredCounts)로 관리.
    /// 멱등·null·중복 안전: 같은 게임일 재호출 무시, null/사망 병사 무시, 풀 중복 등록 스킵.
    /// </summary>
    public static class LaborMarketSystem
    {
        // ===== 고용 수 상수 =====
        /// <summary>매 틱(게임일) AI 영주들이 풀에서 빼가는 최대 병사 수 (0~이 값 사이에서 결정론 결정).</summary>
        public const int MAX_HIRES_PER_DAY = 2;

        // ===== 방출 풀 (고용 시장) =====
        /// <summary>방출된(플레이어가 놓아준) 병사 대기 목록. 선입선출 순서.</summary>
        private static readonly List<GuardPlaceholder> _pool = new List<GuardPlaceholder>();

        // ===== AI 고용 현황 (인메모리 카운트 — guardCount 변조 금지) =====
        /// <summary>영지별 AI 재고용 누적 병사 수. TerritoryDefinition.guardCount는 절대 건드리지 않는다.</summary>
        private static readonly Dictionary<TerritoryId, int> _hiredCounts = new Dictionary<TerritoryId, int>();
        /// <summary>AI가 고용한 총 병사 수 (디버그/하루 로그용).</summary>
        private static int _totalHiredByAI;
        /// <summary>마지막으로 고용 틱을 처리한 게임일 (같은 일차 재호출 무시 — 멱등, -1 = 미처리).</summary>
        private static int _lastProcessedDay = -1;

        // ===== 방출 (플레이어 → 시장 풀) =====
        /// <summary>
        /// 병사를 방출하여 고용 시장 풀에 등록. SetRecruited(false)로 더 이상 플레이어 아군 아님.
        /// null 병사 무시, 이미 풀에 등록된 병사는 스킵(중복 등록 방지), 사망 병사는 등록 불가.
        /// </summary>
        public static void ReleaseGuard(GuardPlaceholder guard)
        {
            if (guard == null)
            {
                Debug.LogWarning("[LaborMarket] 방출 요청 병사가 null — 무시");
                return;
            }
            if (!guard.IsAlive)
            {
                Debug.LogWarning($"[LaborMarket] {guard.GuardName} 사망 병사는 시장 등록 불가 — 스킵");
                return;
            }
            if (_pool.Contains(guard))
            {
                Debug.Log($"[LaborMarket] {guard.GuardName} 이미 시장 풀 등록 상태 — 스킵 (멱등)");
                return;
            }

            _pool.Add(guard);
            guard.SetRecruited(false); // 더 이상 플레이어 아군 아님
            Debug.Log($"[LaborMarket] 병사 방출 → 시장 풀 등록: {guard.GuardName} (Lv.{guard.Level}, 충성도 {guard.Loyalty}) — 풀 {MarketPoolSize()}명");
        }

        // ===== 풀 조회 =====
        /// <summary>현재 시장 풀에 등록된 병사 수.</summary>
        public static int MarketPoolSize() => _pool.Count;

        /// <summary>풀의 idx번째 병사 조회 (테스트/조회용). 범위 밖이면 null.</summary>
        public static GuardPlaceholder? PeekPool(int idx)
            => (idx >= 0 && idx < _pool.Count) ? _pool[idx] : null;

        // ===== AI 영주 재고용 (DayStart 틱에서 호출) =====
        /// <summary>
        /// AI 영주 재고용 처리. 같은 게임일 재호출은 무시(멱등).
        /// LordOwned 영지를 aggression 내림차순 정렬하고, 게임일 기반 결정론 고용 수(0~2명)만큼
        /// 풀에서 병사를 빼 상위(공격 성향 높은) 영주부터 배정한다. 아군 병사는 고용 대상에서 제외.
        /// 병력은 _hiredCounts(인메모리 카운트)로만 가산 — TerritoryDefinition.guardCount는 불변 유지.
        /// </summary>
        public static void ProcessAILordHiring(int currentDay)
        {
            if (currentDay <= _lastProcessedDay)
            {
                Debug.Log($"[LaborMarket] {currentDay}일차 재호출 무시 (마지막 처리 일차 {_lastProcessedDay}) — 멱등");
                return;
            }
            _lastProcessedDay = currentDay;

            if (_pool.Count == 0) return; // 풀 비었으면 아무것도 안 함

            List<TerritoryId> lordIds = CollectLordOwnedTerritories();
            if (lordIds.Count == 0)
            {
                Debug.Log("[LaborMarket] AI 영주 소유(LordOwned) 영지 없음 — 고용 스킵");
                return;
            }

            // 공격 성향 내림차순 정렬 (동점은 영지 ID 문자열 순 — 결정론 보장)
            lordIds.Sort((a, b) =>
            {
                int byAggression = LordPersonalitySystem.GetAggression(b).CompareTo(LordPersonalitySystem.GetAggression(a));
                return byAggression != 0 ? byAggression : string.CompareOrdinal(a.ToString(), b.ToString());
            });

            HashSet<GuardPlaceholder> playerGuards = CollectPlayerGuardSet();

            // 이번 틱 고용 수: 게임일 기반 결정론 해시 (0~MAX_HIRES_PER_DAY), 풀 크기 상한
            int hires = Mathf.Min(DeterministicHireQuota(currentDay), _pool.Count);
            if (hires == 0)
            {
                Debug.Log($"[LaborMarket] {currentDay}일차 AI 고용 없음 (결정론 할당 0명)");
                return;
            }

            int hired = 0;
            for (int i = 0; i < hires; i++)
            {
                GuardPlaceholder guard = TakeNextHireable(playerGuards);
                if (guard == null) break; // 풀에 고용 가능한 병사 없음

                AssignToLord(guard, lordIds[i % lordIds.Count]);
                hired++;
            }

            if (hired > 0)
            {
                Debug.Log($"[LaborMarket] {currentDay}일차 AI 영주 재고용 {hired}명 (풀 잔여 {MarketPoolSize()}명, AI 누적 고용 {TotalHiredByAI()}명)");
            }
        }

        // ===== 내부 헬퍼 =====
        /// <summary>AI 영주 소유(LordOwned) 영지 목록 수집 (데이터베이스/예외 안전).</summary>
        private static List<TerritoryId> CollectLordOwnedTerritories()
        {
            var result = new List<TerritoryId>();
            try
            {
                TerritoryDatabase db = TerritoryDatabase.Instance;
                if (db == null) return result;

                foreach (TerritoryDefinition def in db.GetAllDefinitions())
                {
                    TerritoryState state = db.GetState(def.id);
                    if (state != null && state.ownership == TerritoryOwnership.LordOwned)
                        result.Add(def.id);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[LaborMarket] 영지 목록 조회 실패: {e.Message}");
            }
            return result;
        }

        /// <summary>플레이어 아군 병사 집합 수집 (고용 제외 필터용, manager/목록/예외 안전).</summary>
        private static HashSet<GuardPlaceholder> CollectPlayerGuardSet()
        {
            var set = new HashSet<GuardPlaceholder>();
            try
            {
                List<GuardPlaceholder> playerGuards = GuardManager.Instance?.GetAllPlayerGuards();
                if (playerGuards == null) return set;

                foreach (GuardPlaceholder g in playerGuards)
                    // [Phase E-2 QA 픽스] 방출 병사(IsRecruited=false)는 아군 아님 — 고용 풀에 유지.
                    if (g != null && g.IsRecruited) set.Add(g);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[LaborMarket] 아군 목록 조회 실패: {e.Message}");
            }
            return set;
        }

        /// <summary>
        /// 풀에서 고용 가능한 병사 1명을 꺼낸다 (선입선출).
        /// 사망/파괴/아군 병사는 풀에서 제거하고 다음 후보로 넘어간다 — 이중 고용 원천 차단.
        /// 고용 가능한 병사가 없으면 null 반환.
        /// </summary>
        private static GuardPlaceholder? TakeNextHireable(HashSet<GuardPlaceholder> playerGuards)
        {
            while (_pool.Count > 0)
            {
                GuardPlaceholder candidate = _pool[0];
                _pool.RemoveAt(0);

                if (candidate == null) continue; // 파괴된 참조 — 폐기
                if (!candidate.IsAlive)
                {
                    Debug.Log($"[LaborMarket] {candidate.GuardName} 사망 — 풀에서 제외");
                    continue;
                }
                if (playerGuards.Contains(candidate))
                {
                    // 아군 상태 유지 중 — AI 고용 대상 아님 (풀에서 제거되어 이중 고용도 차단)
                    Debug.Log($"[LaborMarket] {candidate.GuardName} 아군 상태 — AI 고용 제외");
                    continue;
                }
                return candidate;
            }
            return null;
        }

        /// <summary>
        /// 병사를 AI 영주 영지에 배정 (소유권 이전 개념).
        /// 실제 GuardPlaceholder는 월드에 그대로 존재하며, SetRecruited(false) 유지로 플레이어 아군이 아님을 표기.
        /// 병력 가산은 _hiredCounts 카운트로만 기록한다.
        /// </summary>
        private static void AssignToLord(GuardPlaceholder guard, TerritoryId lordId)
        {
            guard.SetRecruited(false); // 플레이어 아군 아님 유지 — 소속만 AI 영주로 이전

            _hiredCounts.TryGetValue(lordId, out int count);
            _hiredCounts[lordId] = count + 1;
            _totalHiredByAI++;

            Debug.Log($"[LaborMarket] AI 영주 재고용: {guard.GuardName} → {lordId} (영지 누적 {_hiredCounts[lordId]}명)");
        }

        /// <summary>
        /// 이번 틱(게임일)의 AI 총 고용 수 (0~MAX_HIRES_PER_DAY).
        /// System.Random 대신 게임일 기반 결정론 해시 — 같은 게임일이면 항상 같은 값.
        /// </summary>
        private static int DeterministicHireQuota(int currentDay)
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + currentDay;
                h ^= h >> 13;
                h = h * 31 + 0x1b873593;
                h ^= h >> 16;
                h &= 0x7fffffff; // 음수 제거 (Mathf.Abs의 int.MinValue 오버플로 회피)
                return h % (MAX_HIRES_PER_DAY + 1); // 0~2
            }
        }

        // ===== 통계 =====
        /// <summary>영지별 AI 재고용 누적 병사 수 (없으면 0).</summary>
        public static int GetHiredCount(TerritoryId id)
            => _hiredCounts.TryGetValue(id, out int count) ? count : 0;

        /// <summary>AI가 고용한 총 병사 수 (디버그/하루 로그용).</summary>
        public static int TotalHiredByAI() => _totalHiredByAI;

        // ===== 리셋 (테스트) =====
        /// <summary>모든 static 상태 초기화 (테스트용).</summary>
        public static void ResetAll()
        {
            int returned = _pool.Count;
            _pool.Clear();
            _hiredCounts.Clear();
            _totalHiredByAI = 0;
            _lastProcessedDay = -1;
            Debug.Log($"[LaborMarket] 모든 상태 초기화 완료 (풀 {returned}명 반환, AI 고용 카운트 0)");
        }
    }
}
