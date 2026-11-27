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
            // [119차] 방출 병사 방랑자 전환 — 시장 풀 등록 유지(AI 재고용 대상), 세계 배회 개시
            guard.BecomeWanderer("방출(시장 풀)");
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

                int hireCost = GetAIAdmissionFee(guard);
                TerritoryId affordableLord = default;
                bool foundAffordableLord = false;
                for (int offset = 0; offset < lordIds.Count; offset++)
                {
                    TerritoryId candidateId = lordIds[(i + offset) % lordIds.Count];
                    TerritoryState candidateState = TerritoryDatabase.Instance.GetState(candidateId);
                    if (candidateState == null || candidateState.ownership != TerritoryOwnership.LordOwned ||
                        candidateState.territoryGold < hireCost)
                        continue;

                    affordableLord = candidateId;
                    foundAffordableLord = true;
                    break;
                }

                if (!foundAffordableLord)
                {
                    // 재화가 부족해도 시장 병사를 잃지 않도록 풀에 복귀시킨다.
                    ReturnToMarketPool(guard);
                    Debug.Log($"[LaborMarket] AI 영주 금고 부족 — 고용 {guard.GuardName}({hireCost}G) 보류");
                    break;
                }

                if (!TryAdmitToLord(guard, affordableLord, hireCost))
                {
                    // 입단 실패(예외 롤백 완료) — 병사는 풀 앞으로 복귀, 이번 틱 고용 중단.
                    break;
                }
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

            // 시장 병사가 기존 영지에 등록돼 있었다면 이전한 뒤 AI 영지 병력으로 등록한다.
            GuardManager? manager = ResolveGuardManager();
            if (manager != null)
            {
                manager.RemoveGuardFromAllTerritories(guard);
                manager.RegisterGuard(lordId, guard);
            }
            // AI 고용 마커 — 저장 시 마커 + 영지 등록으로 AI 고용 병사 로스터를 구성한다.
            guard.IsAIHired = true;

            // [119차] 방랑자 재고용 — 방랑 해제 후 고용 영지로 이동
            if (guard.IsWanderer)
            {
                Vector3 center = ResolveTerritoryCenter(lordId);
                if (center != Vector3.zero) guard.EndWanderer(center);
            }

            _hiredCounts.TryGetValue(lordId, out int count);
            _hiredCounts[lordId] = count + 1;
            _totalHiredByAI++;

            Debug.Log($"[LaborMarket] AI 영주 재고용: {guard.GuardName} → {lordId} (영지 누적 {_hiredCounts[lordId]}명)");
        }

        /// <summary>
        /// AI 재고용 입단비 — 플레이어 편입과 동일한 단위 입단비 산식(SoldierWageSystem.PeekGuardAdmissionFee).
        /// PlayerStats를 사용하지 않는다. null 병사 → 0.
        /// </summary>
        internal static int GetAIAdmissionFee(GuardPlaceholder guard)
            => SoldierWageSystem.PeekGuardAdmissionFee(guard);

        /// <summary>
        /// 입단 게이트: [금고 확인 → 입단비 차감 → 로스터 등록] 순서. 금고 부족이면 아무것도 바꾸지 않고
        /// 병사를 풀 앞으로 복귀시킨다. 등록 중 예외가 나면 차감을 환불하고, 로스터 변경을 원래 상태로
        /// 되돌린 뒤 병사를 풀 앞으로 복귀시킨다. 성공 시 true.
        /// </summary>
        internal static bool TryAdmitToLord(GuardPlaceholder guard, TerritoryId lordId, int admissionFee)
        {
            if (guard == null) return false;

            TerritoryState state = TerritoryDatabase.Instance.GetState(lordId);
            if (state == null || state.territoryGold < admissionFee)
            {
                ReturnToMarketPool(guard);
                return false;
            }

            List<TerritoryId> priorTerritories = SnapshotRosterMemberships(guard);
            bool priorAIHired = guard.IsAIHired;
            state.territoryGold -= admissionFee;
            try
            {
                AssignToLord(guard, lordId);
                // 입단 성공 기록 (AI 입단비는 이미 lord 금고에서 차감됨 — PlayerStats 미사용).
                // 배정 실패 시 이 줄에 도달하지 않으므로 카운트는 증가하지 않는다.
                SoldierWageSystem.RecordExternalGuardAdmission(guard);
                return true;
            }
            catch (System.Exception e)
            {
                state.territoryGold += admissionFee; // 원장 환불 (롤백)
                RestoreRosterMemberships(guard, priorTerritories);
                guard.IsAIHired = priorAIHired;      // AI 마커 롤백
                ReturnToMarketPool(guard);
                Debug.LogWarning($"[LaborMarket] 입단 등록 실패 — {admissionFee}G 환불, 병사 풀 복귀: {e.Message}");
                return false;
            }
        }

        /// <summary>병사가 현재 등록된 영지 목록 스냅샷 (롤백용).</summary>
        private static List<TerritoryId> SnapshotRosterMemberships(GuardPlaceholder guard)
        {
            var result = new List<TerritoryId>();
            GuardManager? manager = ResolveGuardManager();
            TerritoryDatabase db = TerritoryDatabase.Instance;
            if (manager == null || db == null) return result;

            foreach (TerritoryDefinition def in db.GetAllDefinitions())
            {
                if (manager.GetGuardsInTerritory(def.id).Contains(guard))
                    result.Add(def.id);
            }
            return result;
        }

        /// <summary>스냅샷 기준으로 로스터 등록 상태를 복원한다 (부분 등록 제거 후 원래 영지에 재등록).</summary>
        private static void RestoreRosterMemberships(GuardPlaceholder guard, List<TerritoryId> priorTerritories)
        {
            GuardManager? manager = ResolveGuardManager();
            if (manager == null) return;
            try
            {
                manager.RemoveGuardFromAllTerritories(guard);
                foreach (TerritoryId id in priorTerritories)
                    manager.RegisterGuard(id, guard);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[LaborMarket] 로스터 롤백 실패: {e.Message}");
            }
        }

        /// <summary>
        /// 고용 실패 시 병사를 풀의 '앞'으로 되돌려 선입선출 순서를 보존한다 (중복 등록 방지, 사망 병사 제외).
        /// </summary>
        private static void ReturnToMarketPool(GuardPlaceholder guard)
        {
            if (guard == null || !guard.IsAlive || _pool.Contains(guard)) return;
            _pool.Insert(0, guard);
        }

        /// <summary>GuardManager.Instance 우선, 없으면 씬에서 검색 (로드 시점 Instance null 대응).</summary>
        private static GuardManager? ResolveGuardManager()
        {
            GuardManager? manager = GuardManager.Instance;
            if (manager == null) manager = Object.FindAnyObjectByType<GuardManager>();
            return manager;
        }

        /// <summary>에디터 편집 모드(테스트)에서도 안전하게 게임오브젝트를 제거한다.</summary>
        private static void DestroyGuardObject(GuardPlaceholder guard)
        {
            if (guard == null || guard.gameObject == null) return;
            if (Application.isPlaying) Object.Destroy(guard.gameObject);
            else Object.DestroyImmediate(guard.gameObject);
        }

        /// <summary>[119차] 영지 중심 좌표 조회 — 미발견/예외 시 Vector3.zero(호출측에서 스킵).</summary>
        private static Vector3 ResolveTerritoryCenter(TerritoryId id)
        {
            try
            {
                foreach (TerritoryDefinition def in TerritoryDatabase.Instance.GetAllDefinitions())
                    if (def.id.Equals(id)) return def.worldPosition;
            }
            catch (System.Exception) { }
            return Vector3.zero;
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

        // ===== 저장/복원 =====
        /// <summary>
        /// 고용 시장 AI 카운트를 DTO로 내보낸다 (영지 ID 문자열 오름차순 — 결정론).
        /// 방출 풀(_pool)은 Unity 병사 참조이므로 저장하지 않는다. 0 이하 카운트는 제외.
        /// </summary>
        public static LaborMarketSaveData ExportSaveData()
        {
            var data = new LaborMarketSaveData
            {
                lastProcessedDay = _lastProcessedDay,
                totalHiredByAI = _totalHiredByAI,
            };

            var keys = new List<TerritoryId>();
            foreach (var kv in _hiredCounts)
            {
                if (kv.Value > 0) keys.Add(kv.Key);
            }
            keys.Sort((a, b) => string.CompareOrdinal(a.ToString(), b.ToString()));

            foreach (TerritoryId id in keys)
            {
                data.hiredCounts.Add(new TerritoryHireCountEntry
                {
                    territoryId = id.ToString(),
                    count = _hiredCounts[id],
                });
            }

            data.hiredGuards = CollectAIHiredRosterEntries();
            return data;
        }

        /// <summary>
        /// 실제 등록된(GuardManager 영지 목록) AI 고용 마커 병사를 스캔해 로스터 DTO로 만든다.
        /// 병사 1명은 한 번만 기록하며, 사망/마커 없음 병사는 제외. 정렬: 영지ID → stableId (결정론).
        /// </summary>
        private static List<AIHiredGuardSaveEntry> CollectAIHiredRosterEntries()
        {
            var entries = new List<AIHiredGuardSaveEntry>();
            GuardManager? manager = ResolveGuardManager();
            if (manager == null) return entries;

            var seen = new HashSet<GuardPlaceholder>();
            foreach (KeyValuePair<TerritoryId, GuardPlaceholder> pair in manager.GetAllRegisteredGuards())
            {
                GuardPlaceholder guard = pair.Value;
                if (guard == null || !guard.IsAIHired || !guard.IsAlive) continue;
                if (!seen.Add(guard)) continue;

                AIHiredGuardSaveEntry? entry = guard.ToPersistentRosterEntry(pair.Key.ToString());
                if (entry != null) entries.Add(entry);
            }

            entries.Sort((a, b) =>
            {
                int byTerritory = string.CompareOrdinal(a.territoryId, b.territoryId);
                return byTerritory != 0 ? byTerritory : string.CompareOrdinal(a.stableId, b.stableId);
            });
            return entries;
        }

        /// <summary>
        /// 저장 데이터로 고용 카운트/누적/마지막 처리일을 복원한다. _hiredCounts는 먼저 비운다.
        /// 음수 값은 0으로 클램프(마지막 처리일은 -1 미만이면 -1), 유효하지 않은 영지 ID 항목은 건너뛴다.
        /// 풀(_pool)은 건드리지 않는다. data == null 이면 기본 상태(카운트 0, 미처리)로 초기화한다.
        /// </summary>
        public static void ImportSaveData(LaborMarketSaveData? data)
        {
            _hiredCounts.Clear();
            _totalHiredByAI = 0;
            _lastProcessedDay = -1;
            if (data == null)
            {
                RestoreAIHiredRoster(null);
                return;
            }

            _lastProcessedDay = Mathf.Max(-1, data.lastProcessedDay);
            _totalHiredByAI = Mathf.Max(0, data.totalHiredByAI);

            if (data.hiredCounts != null)
            {
                foreach (TerritoryHireCountEntry e in data.hiredCounts)
                {
                    if (e == null) continue;
                    if (!TryParseTerritoryId(e.territoryId, out TerritoryId id)) continue;
                    int count = Mathf.Max(0, e.count);
                    if (count == 0) continue;
                    _hiredCounts[id] = count; // 중복 키는 마지막 값으로 덮어씀 (입력 순서 의존 — 저장 측은 정렬 출력)
                }
            }

            // 구버전 세이브(hiredGuards 없음)는 null/빈 리스트 → 마커 병사 전부 정리 (세이브 상태가 권위)
            RestoreAIHiredRoster(data.hiredGuards);
        }

        /// <summary>
        /// AI 고용 병사 로스터 복원. 저장 DTO마다 (1) 같은 stableId의 기존 마커 병사가 있으면 재사용,
        /// (2) 없으면 TerritoryBuilder.SpawnAIHiredGuard로 누락 병사를 생성한다. 이후 영지에 재등록한다.
        /// 마커(IsAIHired)가 없는 기본 영지 병사는 절대 건드리지 않는다. 등록되지 않은 마커 병사는 제거한다.
        /// 사망 엔트리·stableId 없음·중복 stableId·잘못된 영지 ID는 건너뛴다.
        /// TerritoryDatabase/GuardManager가 없으면 물리 병사를 건드리지 않고 복원을 건너뛴다.
        /// </summary>
        private static void RestoreAIHiredRoster(List<AIHiredGuardSaveEntry>? entries)
        {
            GuardManager? manager = ResolveGuardManager();
            if (manager == null)
            {
                if (entries != null && entries.Count > 0)
                    Debug.LogWarning("[LaborMarket] AI 고용 병사 복원 건너뜀 — GuardManager 없음");
                return;
            }

            TerritoryDatabase? db = TerritoryDatabase.Instance;
            if (db == null && entries != null && entries.Count > 0)
                Debug.LogWarning("[LaborMarket] TerritoryDatabase 없음 — AI 고용 병사 생성 없이 마커 병사만 정리");

            // 1) 유효 엔트리 정리 (DB 없으면 생성 대상 없음 → 전부 제외되어 마커 병사만 정리됨)
            var wanted = new List<KeyValuePair<TerritoryId, AIHiredGuardSaveEntry>>();
            var wantedIds = new HashSet<string>();
            if (entries != null && db != null)
            {
                foreach (AIHiredGuardSaveEntry e in entries)
                {
                    if (e == null || !e.isAlive) continue;
                    if (string.IsNullOrEmpty(e.stableId))
                    {
                        Debug.LogWarning("[LaborMarket] stableId 없는 AI 고용 병사 엔트리 — 건너뜀");
                        continue;
                    }
                    if (!GuardManager.TryParseTerritoryKey(e.territoryId, out TerritoryId owner))
                    {
                        Debug.LogWarning($"[LaborMarket] AI 고용 병사 영지 ID 파싱 실패({e.territoryId}) — 건너뜀");
                        continue;
                    }
                    if (db.GetState(owner) == null)
                    {
                        Debug.LogWarning($"[LaborMarket] AI 고용 병사 영지 미존재({owner}) — 건너뜀");
                        continue;
                    }
                    if (!wantedIds.Add(e.stableId)) continue; // 중복 stableId 방지
                    wanted.Add(new KeyValuePair<TerritoryId, AIHiredGuardSaveEntry>(owner, e));
                }
            }

            // 2) 기존 마커 병사 분류: 재사용(stableId 일치) / 정리 대상(일치 없음·중복)
            var reuse = new Dictionary<string, GuardPlaceholder>();
            var stale = new HashSet<GuardPlaceholder>();
            foreach (KeyValuePair<TerritoryId, GuardPlaceholder> pair in manager.GetAllRegisteredGuards())
            {
                GuardPlaceholder g = pair.Value;
                if (g == null || !g.IsAIHired) continue;
                string sid = g.StableId;
                if (reuse.TryGetValue(sid, out GuardPlaceholder? prev))
                {
                    if (prev != g) stale.Add(g);
                }
                else if (wantedIds.Contains(sid))
                {
                    reuse[sid] = g;
                }
                else
                {
                    stale.Add(g);
                }
            }
            // 풀에 있는 마커 병사: 같은 stableId면 재사용, 그 외 마커 병사는 정리 대상
            foreach (GuardPlaceholder pooled in _pool.ToArray())
            {
                if (pooled == null || !pooled.IsAIHired) continue;
                if (stale.Contains(pooled)) continue;
                string sid = pooled.StableId;
                if (reuse.TryGetValue(sid, out GuardPlaceholder? kept))
                {
                    if (kept != pooled) stale.Add(pooled);
                }
                else if (wantedIds.Contains(sid))
                {
                    reuse[sid] = pooled;
                }
                else
                {
                    stale.Add(pooled);
                }
            }


            // 3) 정리: 마커 병사 중 저장 로스터에 없는 것 제거 (기본 병사 제외)
            foreach (GuardPlaceholder g in stale)
            {
                manager.RemoveGuardFromAllTerritories(g);
                _pool.Remove(g);
                DestroyGuardObject(g);
            }

            // 4) 복원/생성 후 영지 등록
            var perOwnerSlot = new Dictionary<string, int>();
            int restored = 0;
            foreach (KeyValuePair<TerritoryId, AIHiredGuardSaveEntry> pair in wanted)
            {
                TerritoryId owner = pair.Key;
                AIHiredGuardSaveEntry e = pair.Value;

                GuardPlaceholder? guard;
                if (reuse.TryGetValue(e.stableId, out GuardPlaceholder? existing) && existing != null)
                {
                    guard = existing;
                    guard.ApplyPersistentRosterEntry(e);
                    _pool.Remove(guard);
                }
                else
                {
                    string ownerKey = owner.ToString();
                    perOwnerSlot.TryGetValue(ownerKey, out int slot);
                    perOwnerSlot[ownerKey] = slot + 1;

                    guard = TerritoryBuilder.SpawnAIHiredGuard(db!.GetDefinition(owner), slot, e);
                    if (guard == null)
                    {
                        Debug.LogWarning($"[LaborMarket] AI 고용 병사 생성 실패({e.guardName}, {ownerKey}) — 물리 복원 안 됨");
                        continue;
                    }
                }

                guard.SetRecruited(false);
                guard.IsAIHired = true;
                manager.RemoveGuardFromAllTerritories(guard);
                manager.RegisterGuard(owner, guard);
                restored++;
            }

            Debug.Log($"[LaborMarket] AI 고용 병사 복원: 저장 {wanted.Count}명 중 {restored}명 물리 등록 (정리 {stale.Count}명)");
        }

        /// <summary>
        /// TerritoryId.ToString() 형식("East_01")만 유효로 인정하는 파서. 왕복 일치 검사로
        /// 비정규 표기("East_1", 숫자 국가값 등)를 거부해 키 중복을 막는다.
        /// </summary>
        private static bool TryParseTerritoryId(string? text, out TerritoryId id)
            => GuardManager.TryParseTerritoryKey(text ?? string.Empty, out id);


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
