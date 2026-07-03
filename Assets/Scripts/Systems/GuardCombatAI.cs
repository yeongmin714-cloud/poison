using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C9-21: 동행 병사 전투 AI — 플레이어 공격 시 합세, 자동 추종
    /// 
    /// 동행 모드의 포섭된 병사들이 플레이어의 전투 행동을 감지하고
    /// 함께 공격하며, 전투 종료 후 자동으로 플레이어를 따라옵니다.
    /// </summary>
    public static class GuardCombatAI
    {
        // 감지 범위
        public const float FOLLOW_DISTANCE = 3f;
        public const float MAX_FOLLOW_DISTANCE = 8f;
        public const float COMBAT_DETECT_RANGE = 15f;
        public const float RETURN_AFTER_COMBAT_DELAY = 2f;

        /// <summary>
        /// 플레이어의 공격 타겟을 감지하고 주변 동행 병사들에게 공격 명령
        /// PlayerCombat 등에서 호출
        /// </summary>
        public static void NotifyPlayerAttack(GameObject target)
        {
            if (target == null) return;

            var damageable = target.GetComponent<IDamageable>();
            if (damageable == null || !damageable.IsAlive) return;

            var guards = Object.FindObjectsByType<GuardPlaceholder>();
            foreach (var guard in guards)
            {
                if (!guard.IsAlive || !guard.IsRecruited) continue;
                if (!GuardStatusSystem.CanFight(guard.Role)) continue;

                float dist = Vector3.Distance(guard.transform.position, target.transform.position);
                if (dist <= COMBAT_DETECT_RANGE)
                {
                    guard.SetCommandTarget(target.transform.position, true);
                    guard.SetInCombat(true);
                    Debug.Log($"[GuardCombatAI] {guard.GuardName} 플레이어 합세! → {target.name}");
                }
            }
        }

        /// <summary>
        /// 병사 전투 상태 업데이트 (매 프레임, GuardPlaceholder.Update에서 호출 권장)
        /// </summary>
        public static void UpdateGuardBehavior(GuardPlaceholder guard, Transform playerTransform)
        {
            if (guard == null || !guard.IsAlive || !guard.IsRecruited || playerTransform == null)
                return;

            // 전투 중이면 귀환 시간 체크
            if (guard.IsInCombat)
            {
                // 타겟이 죽었거나 없으면 전투 종료 타이머 시작
                if (!guard.HasCommand || !guard.IsAttackCommand)
                {
                    guard.UpdateCombatTimer(Time.deltaTime);
                    if (guard.CombatTimer >= RETURN_AFTER_COMBAT_DELAY)
                    {
                        guard.SetInCombat(false);
                        guard.ClearCommand();
                        Debug.Log($"[GuardCombatAI] {guard.GuardName} 전투 종료, 귀환");
                    }
                }
                else
                {
                    // 명령 지점에 도착했으면 명령 해제 → 타이머 시작
                    float distToTarget = Vector3.Distance(guard.transform.position, guard.CommandTarget);
                    if (distToTarget <= 1.5f)
                    {
                        guard.ClearCommand();
                    }
                    else
                    {
                        guard.ResetCombatTimer();
                    }
                }
                return;
            }

            // 비전투 상태: 플레이어 추종
            float dist = Vector3.Distance(guard.transform.position, playerTransform.position);

            if (dist > MAX_FOLLOW_DISTANCE)
            {
                // 너무 멀어지면 이동 명령
                guard.SetCommandTarget(playerTransform.position, false);
            }
            else if (dist <= FOLLOW_DISTANCE && guard.HasCommand && !guard.IsAttackCommand)
            {
                // 충분히 가까우면 명령 해제
                guard.ClearCommand();
            }
        }

        /// <summary>
        /// 모든 동행 병사에게 귀환 명령
        /// </summary>
        public static void RecallAll(Transform playerTransform)
        {
            if (playerTransform == null) return;

            var guards = Object.FindObjectsByType<GuardPlaceholder>();
            foreach (var guard in guards)
            {
                if (!guard.IsAlive || !guard.IsRecruited) continue;
                guard.SetCommandTarget(playerTransform.position, false);
                guard.SetInCombat(false);
            }
            Debug.Log("[GuardCombatAI] 모든 병사 귀환 명령");
        }
    }
}