using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [P30 후속①] 문지기(gatekeeper) 전원 포섭 판정 — 소프트 게이트.
    /// 오픈월드 존 방식(물리 진입 차단 없음)이므로 "영지 출입 가능" = 적대 게이트 해제:
    /// 특정 영지의 모든 문지기(IsGatekeeper)가 포섭(IsRecruited)되면 그 영지는 안전 통행 상태.
    /// 소비처: GuardHostilitySystem — 게이트 열린 영지 병사는 플레이어 선공/경보하지 않음.
    /// (플레이어가 직접 공격한 경우 NotifyPlayerAttack은 게이트를 무시 — 무조건 적대)
    /// </summary>
    public static class TerritoryGateSystem
    {
        /// <summary>
        /// 해당 영지의 모든 문지기가 포섭되었는지 판정.
        /// 문지기가 하나도 없으면 true(출입 가능), 미포섭 문지기가 하나라도 있으면 false.
        /// GuardManager 부재 등 판정 불가 시 true(오픈월드 기본 개방 — 게이트로 막지 않음).
        /// </summary>
        public static bool AreAllGatekeepersRecruited(TerritoryId territoryId)
        {
            var manager = GuardManager.Instance ?? Object.FindAnyObjectByType<GuardManager>();
            if (manager == null) return true;   // 판정 불가 → 기본 개방

            var guards = manager.GetGuardsInTerritory(territoryId);
            if (guards == null || guards.Count == 0) return true;   // 병사 없음 → 통행 가능

            for (int i = 0; i < guards.Count; i++)
            {
                var guard = guards[i];
                if (guard == null || !guard.IsGatekeeper) continue;   // 문지기만 대상
                if (!guard.IsRecruited) return false;                 // 미포섭 문지기 존재 → 출입 불가
            }

            // 문지기가 없거나 전원 포섭 → 출입 가능
            return true;
        }

        /// <summary>
        /// 해당 영지 통행 가능 여부(소프트 게이트).
        /// 문지기 전원 포섭(또는 문지기 없음) 시 true — 병사가 플레이어를 선공/경보하지 않음.
        /// </summary>
        public static bool CanPassTerritory(TerritoryId territoryId)
        {
            return AreAllGatekeepersRecruited(territoryId);
        }
    }
}
