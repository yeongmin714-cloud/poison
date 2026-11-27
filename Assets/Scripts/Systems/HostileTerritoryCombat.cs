using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [D4-C] 적대 영지 병사 전투 — TerritoryState.playerHostile가 true로 전환된 영지의
    /// 주둔 병사들에게 플레이어 공격 명령을 발행하는 정적 진입점(MonoBehaviour 없음 — 이벤트 기반 호출).
    ///
    /// [사전 실측 — 기존 경로 재사용, 신규 전투 파이프 없음]
    ///  · 병사→플레이어 공격의 확립 경로는 GuardHostilitySystem.InitiateAttackVsPlayerAndSoldiers의
    ///    3종 세트(HostileToPlayerFaction = true + SetCommandTarget(플레이어 위치, true) + SetInCombat(true))
    ///    이며, PlayerCombat.AttackTarget → NotifyPlayerAttack에서 실사용 중이다.
    ///  · GuardPlaceholder.ResolveAttackTarget은 HostileToPlayerFaction == true일 때만 플레이어를
    ///    유효 공격 후보로 인정한다(Physics.OverlapSphere → IDamageable 스캔 게이트).
    ///    플레이어는 PlayerHealth(MonoBehaviour, IDamageable)로 데미지 파이프라인에 자연 연계된다.
    ///  · 영지 병사 조회는 GuardManager.GetGuardsInTerritory(TerritoryId) public API(키 = id.ToString()).
    ///
    /// 트리거: RestaurantSystem.RunDailySale의 적대 전환 롤 성공 지점(territory.playerHostile = true 직후)
    ///         에서 NotifyHostile(nation, territoryIndex) 1줄 훅([D4-C] 태그 주석 참조).
    ///
    /// 해제: playerHostile = false 복귀 시의 병사 명령 해제 경로는 본 단계에서 제공하지 않는다
    ///       (계획서 D4-C — 사용자 지시 전까지 유지). 기존 GuardHostilitySystem에도 적대 해제
    ///       경로가 없는 것과 동일 규약이므로, 해제가 필요해지면 후속 단계에서 별도 설계한다.
    /// </summary>
    public static class HostileTerritoryCombat
    {
        /// <summary>
        /// 적대 전환된 영지의 주둔 병사 전원에게 플레이어 공격 명령을 발행한다.
        /// GuardManager 미건설 / 빈 영지 / 플레이어 부재에서는 예외 없이 안전 반환한다
        /// (EditMode/헤드리스/씬 미준비 방어 — RestaurantSystem 일일 틱에서 호출되므로 절대 던지지 않는다).
        /// </summary>
        /// <param name="nation">적대 전환 영지의 소속 국가.</param>
        /// <param name="territoryIndex">영지 인덱스(1~20 — [T3 실측] 마을 인덱스 0~19와 +1 오프셋).</param>
        public static void NotifyHostile(NationType nation, int territoryIndex)
        {
            // 영지 ID 구성 — [실측] GuardManager 등록/조회 키는 territoryId.ToString() ("{nation}_{index:D2}")
            var territoryId = new TerritoryId(nation, territoryIndex);

            // ① GuardManager 미건설 — 씬 부재/EditMode 환경 방어(안전 반환 + 로그 1회)
            GuardManager manager = GuardManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning($"[HostileTerritoryCombat] {territoryId} 적대 전환 — GuardManager 미건설, 공격 명령 스킵");
                return;
            }

            // ② 플레이어 실체 확인 — 부재 시 명령 발행 불가(안전 반환)
            Transform playerTransform = FindPlayerTransform();
            if (playerTransform == null)
            {
                Debug.LogWarning($"[HostileTerritoryCombat] {territoryId} 적대 전환 — 플레이어 부재, 공격 명령 스킵");
                return;
            }

            // ③ 영지 병사 조회 — [실측] public API(GetGuardsInTerritory). 미등록 영지는 빈 목록 반환.
            List<GuardPlaceholder> guards = manager.GetGuardsInTerritory(territoryId);
            if (guards == null || guards.Count == 0)
            {
                Debug.Log($"[HostileTerritoryCombat] {territoryId} 적대 전환 — 주둔 병사 없음, 명령 대상 0명");
                return;
            }

            // ④ 플레이어 공격 명령 — GuardHostilitySystem.InitiateAttackVsPlayerAndSoldiers와
            //    동일 3종 세트. 명령 시점의 플레이어 위치를 목표로 고정한다(주기 재발행 없음 — 최소 구현;
            //    병사는 ResolveAttackTarget이 명령 지점 근처에서 플레이어를 타겟으로 확정한다).
            int ordered = 0;
            foreach (GuardPlaceholder guard in guards)
            {
                if (guard == null || !guard.IsAlive) continue;  // 사망/파괴된 실체 제외
                if (guard.IsRecruited) continue;                // 플레이어 포섭 병사 제외(GuardHostilitySystem.NotifyPlayerAttack 규약 준용)

                guard.HostileToPlayerFaction = true;                    // ResolveAttackTarget 플레이어 타겟 게이트 해제([69차 후속15] 정책)
                guard.SetCommandTarget(playerTransform.position, true); // 공격 명령(목표 = 플레이어 현재 위치, true = 공격)
                guard.SetInCombat(true);                                // 전투 상태 진입(명령 유지)
                ordered++;
            }

            Debug.LogWarning($"[HostileTerritoryCombat] ⚔️ {territoryId} 적대 영지 — 병사 {ordered}/{guards.Count}명 플레이어 공격 명령 발행");
        }

        /// <summary>
        /// 플레이어 Transform 조회 — PlayerHealth.Instance(싱글턴) 우선, 없으면 "Player" 태그 폴백
        /// (GuardHostilitySystem.ProcessHostility의 플레이어 탐색 방식 준용). 모두 실패 시 null.
        /// </summary>
        private static Transform FindPlayerTransform()
        {
            if (PlayerHealth.Instance != null)
                return PlayerHealth.Instance.transform;

            GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
            return playerGo != null ? playerGo.transform : null;
        }
    }
}
