using System.Collections.Generic;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 플레이어 소유 영지의 병사를 '공격/수비/해제'로 배치하는 정적 시스템.
    /// - 기존 병사 이동 명령(GuardPlaceholder.SetCommandTarget/ClearCommand)과
    ///   TerritoryBuilder.SpawnGarrison / GuardManager.RegisterGuard를 재활용한다.
    /// - 배치 상태(garrisonRole / attackTargetId)는 TerritoryState에 기록하되
    ///   저장/복원은 런타임 배치만 다룬다 (세이브 스킵).
    /// </summary>
    public static class TerritoryDeploymentSystem
    {
        /// <summary>
        /// source 영지의 병사를 target 영지로 공격 배치.
        /// source에 스폰된 병사가 있으면 재활용하고, 없으면 SpawnGarrison으로 주둔군 생성 후
        /// 각 병사에게 SetCommandTarget(target.worldPosition, attack:true)를 발동시킨다.
        /// </summary>
        public static void DeployAttack(TerritoryId sourceTerritoryId, TerritoryId targetTerritoryId)
        {
            var db = TerritoryDatabase.Instance;
            if (db == null) { Debug.LogError("[TerritoryDeploymentSystem] TerritoryDatabase 인스턴스 없음"); return; }

            TerritoryState sourceState = db.GetState(sourceTerritoryId);
            if (sourceState == null || sourceState.ownership != TerritoryOwnership.PlayerOwned)
            {
                Debug.LogWarning($"[TerritoryDeploymentSystem] 공격 배치 실패 — source {sourceTerritoryId} 는 플레이어 소유가 아닙니다.");
                return;
            }

            TerritoryDefinition targetDef = db.GetDefinition(targetTerritoryId);
            Vector3 targetPos = targetDef.worldPosition;
            if (targetPos == Vector3.zero)
            {
                Debug.LogWarning($"[TerritoryDeploymentSystem] target {targetTerritoryId} 정의/위치 없음 — {targetDef.territoryName}");
            }

            // ── 병사 확보: GuardManager 기존 등록 병사 재활용, 없으면 SpawnGarrison으로 생성 ──
            List<GuardPlaceholder> guards = GuardManager.Instance != null
                ? GuardManager.Instance.GetGuardsInTerritory(sourceTerritoryId)
                : new List<GuardPlaceholder>();

            if (guards.Count == 0)
            {
                TerritoryDefinition sourceDef = db.GetDefinition(sourceTerritoryId);
                Vector3 center = TryGetTerritoryCenter(sourceDef, sourceTerritoryId);
                var spawned = TerritoryBuilder.SpawnGarrison(sourceDef, center);
                foreach (var go in spawned)
                {
                    var guard = go != null ? go.GetComponent<GuardPlaceholder>() : null;
                    if (guard == null) continue;
                    guards.Add(guard);
                    if (GuardManager.Instance != null)
                        GuardManager.Instance.RegisterGuard(sourceTerritoryId, guard);
                }
            }

            // ── 각 병사에게 공격 명령 발동 ──
            int commanded = 0;
            foreach (var guard in guards)
            {
                if (guard == null) continue;
                guard.SetCommandTarget(targetPos, true);
                commanded++;
            }

            // ── 배치 상태 기록 ──
            sourceState.garrisonRole = GarrisonRole.Attack;
            sourceState.attackTargetId = targetTerritoryId;

            Debug.Log($"[TerritoryDeploymentSystem] ⚔️ 공격 배치: {sourceTerritoryId} → {targetTerritoryId} ({targetDef.territoryName}, {commanded}명)");
        }

        /// <summary>
        /// 영지 문지기/주둔군을 수비 태세로 배치.
        /// GuardManager 기존 병사를 재활용하고, 없으면 SpawnGarrison으로 생성 후
        /// 각 병사에게 SetCommandTarget(문 앞, attack:false)로 수비 명령을 내린다.
        /// </summary>
        public static void DeployDefense(TerritoryId territoryId)
        {
            var db = TerritoryDatabase.Instance;
            if (db == null) { Debug.LogError("[TerritoryDeploymentSystem] TerritoryDatabase 인스턴스 없음"); return; }

            TerritoryState state = db.GetState(territoryId);
            if (state == null || state.ownership != TerritoryOwnership.PlayerOwned)
            {
                Debug.LogWarning($"[TerritoryDeploymentSystem] 수비 배치 실패 — {territoryId} 는 플레이어 소유가 아닙니다.");
                return;
            }

            // ── 병사 확보 ──
            List<GuardPlaceholder> guards = GuardManager.Instance != null
                ? GuardManager.Instance.GetGuardsInTerritory(territoryId)
                : new List<GuardPlaceholder>();

            if (guards.Count == 0)
            {
                TerritoryDefinition sourceDef = db.GetDefinition(territoryId);
                Vector3 center = TryGetTerritoryCenter(sourceDef, territoryId);
                var spawned = TerritoryBuilder.SpawnGarrison(sourceDef, center);
                foreach (var go in spawned)
                {
                    var guard = go != null ? go.GetComponent<GuardPlaceholder>() : null;
                    if (guard == null) continue;
                    guards.Add(guard);
                    if (GuardManager.Instance != null)
                        GuardManager.Instance.RegisterGuard(territoryId, guard);
                }
            }

            // ── 문 앞 수비 위치 산출 ──
            Vector3 gateFront = ComputeGateFront(territoryId);

            int commanded = 0;
            foreach (var guard in guards)
            {
                if (guard == null) continue;
                guard.SetCommandTarget(gateFront, false);
                commanded++;
            }

            state.garrisonRole = GarrisonRole.Defense;
            state.attackTargetId = default;

            Debug.Log($"[TerritoryDeploymentSystem] 🛡️ 수비 배치: {territoryId} 문 앞 {gateFront}, {commanded}명 수비 태세");
        }

        /// <summary>
        /// 영지 병사의 배치를 해제한다. 모든 병사에게 ClearCommand 발동.
        /// </summary>
        public static void Undeploy(TerritoryId territoryId)
        {
            var db = TerritoryDatabase.Instance;
            if (db == null) { Debug.LogError("[TerritoryDeploymentSystem] TerritoryDatabase 인스턴스 없음"); return; }

            int cleared = 0;
            if (GuardManager.Instance != null)
            {
                foreach (var guard in GuardManager.Instance.GetGuardsInTerritory(territoryId))
                {
                    if (guard == null) continue;
                    guard.ClearCommand();
                    cleared++;
                }
            }

            TerritoryState state = db.GetState(territoryId);
            if (state != null)
            {
                state.garrisonRole = GarrisonRole.None;
                state.attackTargetId = default;
            }

            Debug.Log($"[TerritoryDeploymentSystem] ⏹️ 배치 해제: {territoryId} ({cleared}명 명령 해제)");
        }

        // ================================================================
        // 헬퍼
        // ================================================================

        /// <summary>영지 중심 좌표 (정의 worldPosition 우선, 실패 시 TerritoryManager 폴백)</summary>
        private static Vector3 TryGetTerritoryCenter(TerritoryDefinition def, TerritoryId id)
        {
            if (def.worldPosition != Vector3.zero)
                return def.worldPosition;
            if (TerritoryManager.Instance != null)
                return TerritoryManager.Instance.GetTerritoryCenter(id);
            return Vector3.zero;
        }

        /// <summary>
        /// 영지 성문 앞(접근 방향) 위치 계산. BuildGuardsAt와 동일한 gateDir 로직 재사용:
        /// 황제국 중심(0,0,0) 기준 바깥(반대) 방향 + 3m.
        /// </summary>
        private static Vector3 ComputeGateFront(TerritoryId territoryId)
        {
            Vector3 center = Vector3.zero;
            var db = TerritoryDatabase.Instance;
            if (db != null)
            {
                TerritoryDefinition def = db.GetDefinition(territoryId);
                center = TryGetTerritoryCenter(def, territoryId);
            }

            Vector3 c2 = new Vector3(center.x, 0, center.z);
            Vector3 gateDir = c2.sqrMagnitude < 0.0001f ? Vector3.back : Vector3.Normalize(c2);
            return center + gateDir * 3f;
        }
    }
}