using UnityEngine;
using ProjectName.Core.Data;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// P30-A: 영지 단위 '마약 오염도' 시스템.
    ///
    /// - 희귀 마약을 영지에 유통시키면 영지 오염도(drugContamination, 0~100)가 상승한다.
    ///   희귀도(ItemRarity 0~5)가 높을수록 오염량이 크다 (base = 4 + rarity * 3).
    /// - 시간 경과 시 그 영지에 배치된 병사(GuardPlaceholder)들의 중독도가 오염도 비례로 상승한다.
    /// - 영주 중독도는 영지 오염도와 일치한다(후속 단계 Phase5가 영주실 문 개폐에 사용).
    /// </summary>
    public static class TerritoryDrugSystem
    {
        // ===== 오염 상수 =====
        /// <summary>마약 유통 기본 오염량 (rarityIndex 0 기준).</summary>
        public const float CONTAMINATION_BASE = 4f;
        /// <summary>희귀도 1당 추가 오염량 (base = 4 + rarity * 3 → 최대 19).</summary>
        public const float CONTAMINATION_PER_RARITY = 3f;
        /// <summary>오염도 비례 중독 상승 계수 — 초당 gain = deltaSeconds * contamination * 0.001.</summary>
        public const float ADDICTION_GAIN_PER_SECOND_PER_POINT = 0.001f;
        /// <summary>희귀도 상한 (ItemRarity 0~5).</summary>
        public const int MAX_RARITY_INDEX = 5;

        /// <summary>
        /// 영지에 마약을 유통시킨다 — 희귀도(ItemRarity 0~5)가 높을수록 오염이 크게 증가.
        /// 예: rarity 0 → +4, rarity 5 → +19.
        /// </summary>
        public static void AddDrug(ProjectName.Core.Data.TerritoryId territoryId, int rarityIndex)
        {
            var state = TerritoryDatabase.Instance.GetState(territoryId);
            if (state == null) return;

            int rarity = Mathf.Clamp(rarityIndex, 0, MAX_RARITY_INDEX);
            float add = CONTAMINATION_BASE + rarity * CONTAMINATION_PER_RARITY;
            state.drugContamination += add;
            Debug.Log($"[TerritoryDrug] {territoryId} 마약 유통(희귀도 {rarity}): 오염도 +{add:F0} → {state.drugContamination:F1}");
        }

        /// <summary>
        /// 시간 경과 처리 — 그 영지에 배치된 병사들의 중독도를 오염도 비례로 상승시킨다.
        /// gain = deltaSeconds * drugContamination * 0.001 (희귀도 영향 미반영, 누적 상승).
        /// </summary>
        public static void ProcessContamination(ProjectName.Core.Data.TerritoryId territoryId, float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;
            var state = TerritoryDatabase.Instance.GetState(territoryId);
            if (state == null) return;

            float contamination = state.drugContamination;
            if (contamination <= 0f) return;
            if (GuardManager.Instance == null) return;

            var guards = GuardManager.Instance.GetGuardsInTerritory(territoryId);
            if (guards == null || guards.Count == 0) return;

            float gain = deltaSeconds * contamination * ADDICTION_GAIN_PER_SECOND_PER_POINT;
            for (int i = 0; i < guards.Count; i++)
            {
                var guard = guards[i];
                if (guard == null || !guard.IsAlive) continue;
                guard.Addiction += gain;   // Addiction setter가 0~MAX_ADDICTION 클램프
            }
        }

        /// <summary>영지 마약 오염도 조회 (0~100, 데이터 없으면 0).</summary>
        public static float GetTerritoryContamination(ProjectName.Core.Data.TerritoryId territoryId)
        {
            var state = TerritoryDatabase.Instance.GetState(territoryId);
            if (state == null) return 0f;
            return state.drugContamination;
        }

        /// <summary>
        /// 영주 중독도 조회 — 영지 오염도와 일치한다 (halt: return drugContamination).
        /// 후속 단계(Phase5)가 영주실 문 개폐 판정에 사용.
        /// </summary>
        public static float GetLordAddiction(ProjectName.Core.Data.TerritoryId territoryId)
        {
            return GetTerritoryContamination(territoryId);
        }

        // ===== P30-D: 문자열 영지 키 오버로드 (NPCInstance.TerritoryId = "East_01" 형식 string) =====
        // TerritoryDatabase.GetState(string key)의 파싱 폴백을 재사용 — UI에서 파서 중복 없이 직접 호출.

        /// <summary>[P30-D] 문자열 영지 키로 마약 유통 (NPCInstance.TerritoryId 등 string 키 경유).</summary>
        public static void AddDrug(string territoryKey, int rarityIndex)
        {
            var state = TerritoryDatabase.Instance.GetState(territoryKey);
            if (state == null) return;
            AddDrug(state.id, rarityIndex);
        }

        /// <summary>[P30-D] 문자열 영지 키로 마약 오염도 조회 (데이터 없으면 0).</summary>
        public static float GetTerritoryContamination(string territoryKey)
        {
            var state = TerritoryDatabase.Instance.GetState(territoryKey);
            if (state == null) return 0f;
            return state.drugContamination;
        }

        /// <summary>[P30-D] 문자열 영지 키로 영주 중독도 조회 (영지 오염도와 일치).</summary>
        public static float GetLordAddiction(string territoryKey)
        {
            return GetTerritoryContamination(territoryKey);
        }

        /// <summary>
        /// [P30-D] 전 영지 오염 진행 — drugContamination > 0인 모든 영지에 ProcessContamination.
        /// TerritoryManager.Update에서 매 프레임 호출 (밀매/선물로 오염된 영지의 병사 중독이
        /// "밀매 성공 순간부터 시간에 따라" 계속 증가하도록 하는 지속 러너).
        /// </summary>
        public static void ProcessAllContamination(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;
            var states = TerritoryDatabase.Instance.GetAllStates();
            if (states == null) return;
            foreach (var state in states)
            {
                if (state == null || state.drugContamination <= 0f) continue;
                ProcessContamination(state.id, deltaSeconds);
            }
        }
    }
}
