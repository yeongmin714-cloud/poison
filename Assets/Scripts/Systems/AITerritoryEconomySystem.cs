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
        private static int _lastTaxDay = -1;
        private static int _lastMaintenanceDay = -1;

        /// <summary>같은 날 중복 과세 방지. LordOwned 영지에만 세수를 적립하고 상한을 적용한다.</summary>
        public static void ApplyDailyTax(int day)
        {
            if (day <= _lastTaxDay) return;

            TerritoryDatabase db = TerritoryDatabase.Instance;
            foreach (TerritoryDefinition definition in db.GetAllDefinitions())
            {
                TerritoryState state = db.GetState(definition.id);
                if (state == null || state.ownership != TerritoryOwnership.LordOwned) continue;

                int cap = GetTreasuryCap(definition.difficulty);
                long accrued = (long)state.territoryGold + GetDailyTax(definition.difficulty);
                state.territoryGold = accrued >= cap ? cap : (int)accrued;
            }

            _lastTaxDay = day;
        }

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
                    if (manager != null) manager.RemoveGuardFromAllTerritories(guard);
                    if (guard != null) Object.Destroy(guard.gameObject);

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
            _lastTaxDay = -1;
            _lastMaintenanceDay = -1;
        }
    }
}
