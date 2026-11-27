using System.Collections.Generic;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// AI(LordOwned) 영지 전용 일일 세입/병력 유지 경제.
    /// 플레이어 소유 영지와 플레이어 급료 시스템은 수정하지 않는다.
    /// </summary>
    public static class AITerritoryEconomySystem
    {
        public const int HIRE_COST_DAYS = 5;

        /// <summary>세수 주기(게임일). 앵커일부터 7일마다 1회 과세한다 (주급 문서와 동일 규약).</summary>
        public const int TAX_INTERVAL_DAYS = 7;
        public const int TAX_ANCHOR_DAY = 1;

        // 멱등 상태 (SaveData v4 aiEconomy 블록으로 저장/복원 — ExportSaveData/ImportSaveData 참조).
        private static int _lastTaxWeek = -1;
        private static int _lastMaintenanceDay = -1;

        /// <summary>
        /// 매일 호출 가능. 주 경계일(TAX_ANCHOR_DAY + n * TAX_INTERVAL_DAYS)이 아니면 무시하고,
        /// 같은 주차는 한 번만 과세한다. LordOwned 영지에만 주간 세수를 적립하고 상한을 적용한다.
        /// 세액 = 난이도 일세수 * 7 * 프로필 세율(TerritoryDifficultyProfileSystem) × 마을 경제 계수
        /// (VillageEconomySystem — [D4-B] 세금 타격, 마을 상태 없으면 계수 1.0), 정수 반올림(half-up).
        /// </summary>
        public static void ApplyDailyTax(int day)
        {
            if (!IsTaxBoundaryDay(day)) return;

            int week = GetTaxWeekIndex(day);
            if (week <= _lastTaxWeek) return;
            _lastTaxWeek = week;

            TerritoryDatabase db = TerritoryDatabase.Instance;
            foreach (TerritoryDefinition definition in db.GetAllDefinitions())
            {
                TerritoryState state = db.GetState(definition.id);
                if (state == null || state.ownership != TerritoryOwnership.LordOwned) continue;

                TerritoryDifficultyProfile profile = TerritoryDifficultyProfileSystem.Resolve(
                    GetProfileTerritoryId(definition.id), definition.id.nation, definition.difficulty);

                int cap = GetTreasuryCap(definition.difficulty);
                // [D4-B] 마을 경제 반영 세금 타격 — 주간 세액에 WeeklyTaxFactor(0.55~1.0)를 적용한다.
                // 상태 없는 마을(중독 0/이탈 0)은 factor 1.0 → D4 이전 세수와 동일(하위호환).
                int weeklyCollected = VillageEconomySystem.GetWeeklyCollectedTax(
                    definition.difficulty, profile.TaxPercent, definition.id.nation, definition.id.index);
                long accrued = (long)state.territoryGold + weeklyCollected;
                state.territoryGold = accrued >= cap ? cap : (int)accrued;
            }
        }

        /// <summary>주 경계일 여부. 앵커일 이전이거나 주기에 맞지 않으면 false.</summary>
        public static bool IsTaxBoundaryDay(int day)
            => day >= TAX_ANCHOR_DAY && (day - TAX_ANCHOR_DAY) % TAX_INTERVAL_DAYS == 0;

        /// <summary>과세 주차 인덱스 (앵커일 주차 = 0). 경계일에만 의미가 있다.</summary>
        public static int GetTaxWeekIndex(int day)
            => (day - TAX_ANCHOR_DAY) / TAX_INTERVAL_DAYS;

        /// <summary>
        /// 주간 세액 = 일세수 * 7 * taxPercent / 100, 정수 half-up 반올림.
        /// taxPercent 100이면 정확히 일세수 * 7.
        /// </summary>
        public static int GetWeeklyTax(TerritoryDifficulty difficulty, int taxPercent)
        {
            long scaled = (long)GetDailyTax(difficulty) * TAX_INTERVAL_DAYS * taxPercent;
            return (int)((scaled + 50) / 100);
        }

        /// <summary>
        /// TerritoryDifficultyProfileSystem.Resolve에 넘길 안정적 정수 ID.
        /// 국가 enum 값 * 100 + 영지 인덱스 (인덱스는 1~20).
        /// </summary>
        public static int GetProfileTerritoryId(TerritoryId id)
            => (int)id.nation * 100 + id.index;

        /// <summary>AI 영지 난이도별 일일 세수.</summary>
        public static int GetDailyTax(TerritoryDifficulty difficulty)
        {
            switch (difficulty)
            {
                case TerritoryDifficulty.Ring1: return 10;
                case TerritoryDifficulty.Ring2: return 20;
                case TerritoryDifficulty.Ring3: return 35;
                case TerritoryDifficulty.Ring4: return 55;
                case TerritoryDifficulty.Empire: return 100;
                default: return 0;
            }
        }

        /// <summary>AI 영지별 금고 상한.</summary>
        public static int GetTreasuryCap(TerritoryDifficulty difficulty)
        {
            switch (difficulty)
            {
                case TerritoryDifficulty.Ring1: return 1000;
                case TerritoryDifficulty.Ring2: return 2000;
                case TerritoryDifficulty.Ring3: return 3500;
                case TerritoryDifficulty.Ring4: return 5500;
                case TerritoryDifficulty.Empire: return 10000;
                default: return 0;
            }
        }

        /// <summary>
        /// AI 병사 1명의 일일 유지비. GetDailyCost는 미포섭 병사에게 0을 주므로
        /// AI 병사는 별도 레벨 산식(2 + Level * 2)을 사용한다.
        /// </summary>
        public static int GetAIMaintenance(GuardPlaceholder guard)
        {
            if (guard == null || !guard.IsAlive) return 0;
            return guard.IsRecruited
                ? GuardSalarySystem.GetDailyCost(guard)
                : Mathf.Max(0, 2 + guard.Level * 2);
        }

        /// <summary>AI 병사 1회 고용비(일일 유지비 5일분).</summary>
        public static int GetHireCost(GuardPlaceholder guard)
            => GetAIMaintenance(guard) * HIRE_COST_DAYS;

        /// <summary>
        /// AI 영주 영지 유지비를 지불한다. 잔액이 부족하면 유지비가 높은 병사부터
        /// 영지 병력에서 제거한 뒤 남은 유지비를 지불한다. 반환값은 실제 지불액.
        /// </summary>
        public static float PayAILordMaintenance(int day)
        {
            if (day <= _lastMaintenanceDay) return 0f;
            // 예외가 발생해도 같은 게임일에 부분 차감이 재실행되지 않도록 먼저 잠근다.
            _lastMaintenanceDay = day;

            TerritoryDatabase db = TerritoryDatabase.Instance;
            GuardManager manager = GuardManager.Instance;
            float totalPaid = 0f;

            foreach (TerritoryDefinition definition in db.GetAllDefinitions())
            {
                TerritoryState state = db.GetState(definition.id);
                if (state == null || state.ownership != TerritoryOwnership.LordOwned) continue;

                List<GuardPlaceholder> guards = GetAIGuards(definition.id, manager);
                int totalCost = 0;
                foreach (GuardPlaceholder guard in guards)
                    totalCost += GetAIMaintenance(guard);

                if (totalCost <= state.territoryGold)
                {
                    state.territoryGold -= totalCost;
                    totalPaid += totalCost;
                    continue;
                }

                // 가장 비싼 병사부터 감축해 금고 잔액에 맞춘다. 미포섭 AI 병사는
                // 시장 풀에 넣지 않는다(ReleaseGuard는 플레이어 병사 방출용 API).
                guards.Sort((a, b) => GetAIMaintenance(b).CompareTo(GetAIMaintenance(a)));
                for (int i = 0; i < guards.Count && totalCost > state.territoryGold; i++)
                {
                    GuardPlaceholder guard = guards[i];
                    int cost = GetAIMaintenance(guard);
                    if (cost <= 0) continue;

                    totalCost -= cost;

                    // [119차] 감축 병사 방랑자 전환 — 일반 병사는 파괴 대신 세계 배회로 이동.
                    // 게이트키퍼는 TerritoryGateSystem 구조 보호를 위해 기존 파괴 유지.
                    if (guard != null && !guard.IsGatekeeper)
                    {
                        if (manager != null) manager.RemoveGuardFromAllTerritories(guard);
                        guard.BecomeWanderer($"AI 감축({definition.id})");
                    }
                    else if (guard != null)
                    {
                        Object.Destroy(guard.gameObject);
                    }

                    int baseCount = Mathf.Max(1, definition.guardCount);
                    state.guardAliveRatio = Mathf.Max(0f, state.guardAliveRatio - 1f / baseCount);
                    Debug.Log($"[AITerritoryEconomy] {definition.id} 유지비 부족 — 병력 감축: {guard.GuardName} (절감 {cost}G)");
                }

                int paid = Mathf.Min(state.territoryGold, totalCost);
                state.territoryGold -= paid;
                totalPaid += paid;
            }

            return totalPaid;
        }

        private static List<GuardPlaceholder> GetAIGuards(TerritoryId id, GuardManager manager)
        {
            var guards = new List<GuardPlaceholder>();
            var seen = new HashSet<GuardPlaceholder>();

            // Manager 목록에는 전쟁/배치/재고용 병력이 등록될 수 있다.
            if (manager != null)
            {
                List<GuardPlaceholder> registered = manager.GetGuardsInTerritory(id);
                if (registered != null)
                {
                    foreach (GuardPlaceholder guard in registered)
                        AddAIGuard(guard, seen, guards);
                }
            }

            // 기본 문지기는 TerritoryBuilder가 Territory_<Nation>_<NN> 부모 아래 생성하지만
            // GuardManager에 등록하지 않으므로 실제 계층을 스캔해 누락을 보완한다.
            string territoryObjectName = $"Territory_{id.nation}_{id.index:D2}";
            GameObject territoryRoot = GameObject.Find(territoryObjectName);
            if (territoryRoot != null)
            {
                GuardPlaceholder[] hierarchyGuards = territoryRoot.GetComponentsInChildren<GuardPlaceholder>(true);
                foreach (GuardPlaceholder guard in hierarchyGuards)
                    AddAIGuard(guard, seen, guards);
            }

            // 전쟁 시 SpawnGarrison은 TerritoryBuilder 아래가 아닌 루트에 생성될 수 있다.
            string garrisonPrefix = $"Garrison_{id.nation}_{id.index}_";
            GuardPlaceholder[] activeGuards = Object.FindObjectsByType<GuardPlaceholder>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (GuardPlaceholder guard in activeGuards)
            {
                if (guard != null && guard.name.StartsWith(garrisonPrefix))
                    AddAIGuard(guard, seen, guards);
            }

            return guards;
        }

        private static void AddAIGuard(GuardPlaceholder guard, HashSet<GuardPlaceholder> seen,
            List<GuardPlaceholder> result)
        {
            if (guard == null || !guard.IsAlive || !guard.gameObject.activeInHierarchy || guard.IsRecruited) return;
            if (seen.Add(guard)) result.Add(guard);
        }

        /// <summary>테스트용: 일일 처리 멱등 상태 초기화.</summary>
        public static void ResetAll()
        {
            _lastTaxWeek = -1;
            _lastMaintenanceDay = -1;
        }

        // ===== 저장/복원 =====
        // 영지 금고 잔액 자체는 TerritoryState.territoryGold(SaveData.territories[].territoryGold)에 저장된다.
        // 이 블록은 "이미 청구한 주/일"만 저장해 로드 후 같은 주기 중복 청구를 막는다.

        /// <summary>현재 멱등 상태(마지막 과세 주차, 마지막 유지비 일)를 DTO로 내보낸다.</summary>
        public static AIEconomySaveData ExportSaveData()
        {
            return new AIEconomySaveData
            {
                lastTaxWeek = _lastTaxWeek,
                lastMaintenanceDay = _lastMaintenanceDay
            };
        }

        /// <summary>
        /// 멱등 상태 복원. 영지 금고(territoryGold) 복원 이후에 호출한다.
        /// data == null (v3 이하 레거시 세이브): 현재 게임일 기준으로 시딩하여 소급 과세/유지비를 막는다.
        ///   - 과세: 현재 날짜가 속한 주차를 "이미 과세됨"으로 표시 → 다음 경계일부터 과세.
        ///   - 유지비: 현재 게임일을 "이미 지불됨"으로 표시 → 다음 날부터 지불.
        /// </summary>
        public static void ImportSaveData(AIEconomySaveData data, int currentDay)
        {
            if (data == null)
            {
                _lastTaxWeek = currentDay >= TAX_ANCHOR_DAY ? GetTaxWeekIndex(currentDay) : -1;
                _lastMaintenanceDay = currentDay;
                return;
            }

            _lastTaxWeek = data.lastTaxWeek;
            _lastMaintenanceDay = data.lastMaintenanceDay;
        }
    }
}
