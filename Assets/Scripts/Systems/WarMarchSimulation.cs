using System.Collections.Generic;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 3.7: 가시적 전쟁 행진 시뮬레이션.
    /// 전쟁이 콘솔 로그가 아니라 '병사가 실제로 움직이는' 장면으로 보이도록 한다.
    ///
    /// 흐름:
    ///  1. 공격군 병사 그룹(Phase B: 영주 성향 A에 비례한 1~5명)을 공격측 영지에 스폰한다.
    ///  2. 각 공격 병사에 SetCommandTarget(방어측 영지, 공격 명령=true)를 발동해 목표 영지로 행진(실제 이동).
    ///  3. GuardPlaceholder의 ExecuteMovement가 프레임마다 자동 이동 → 목표 도달 시 자동 근접 공격(플린치 파티클).
    ///  3'. 방어군 병사도 방어측 영지에 스폰하여 가시적 교전 대상으로 삼는다.
    ///  4. 모든 공격군(도달 후 교전)이 종료되면 병사들을 정리하고 그룹을 반납한다(동시 행진 ≤2).
    ///
    /// 기존 전쟁 수치 결과(TerritoryWarManager 제어) 흐름은 온전히 보존한다(회귀 없음).
    /// </summary>
    public sealed class WarMarchSimulation : MonoBehaviour
    {
        private const int MAX_CONCURRENT = 2;      // 동시 행진 그룹 상한(저사양 캡)
        private const int MAX_SOLDIERS = 5;       // 공격군 병사 상한
        private const float HOLD_DURATION = 1.6f; // 도달·교전 종료 후 유지 후 정리 (달성감)

        private static int _activeMarches;

        private readonly List<GameObject> _attackers = new List<GameObject>();
        private readonly List<GameObject> _defenders = new List<GameObject>();

        private Vector3 _targetPos;
        private int _attackForce; // Phase B: 영주 성향(A) 기반 공격 파견 병력 수 [1, MAX_SOLDIERS]
        private float _elapsedHold;
        private bool _arrivalLogged;
        private bool _releasing;

        /// <summary>현재 진행 중인 행진 그룹 수.</summary>
        public static int ActiveMarches => _activeMarches;

        /// <summary>
        /// 가시 전쟁 행진 시작을 예약한다. 동시 진행 그룹 상한(MAX_CONCURRENT)을 넘으면 시작하지 않고 false(스킵).
        /// </summary>
        public static bool TryStartMarch(TerritoryDefinition attackerDef, Vector3 fromPos, Vector3 targetPos,
            TerritoryDefinition defenderDef)
        {
            if (_activeMarches >= MAX_CONCURRENT)
                return false;

            var go = new GameObject("[WarMarchSimulation]");
            var sim = go.AddComponent<WarMarchSimulation>();
            sim.Begin(attackerDef, fromPos, targetPos, defenderDef);
            _activeMarches++;
            Debug.Log($"[WarMarchSimulation] ⚔️ 행진 시작 — {attackerDef.territoryName} → {targetPos} (성향 A={LordPersonalitySystem.GetAggression(attackerDef.id):0.00}, 파견 {sim._attackForce}명 / 스폰 {sim._attackers.Count}명, 활성 {_activeMarches}/{MAX_CONCURRENT})");
            return true;
        }

        private void Begin(TerritoryDefinition attackerDef, Vector3 fromPos, Vector3 targetPos,
            TerritoryDefinition defenderDef)
        {
            _targetPos = targetPos;

            // ── 공격군 병사 그룹 스폰 ──
            // Phase B: 공격 파견 수를 영주 성향 A에 비례 — attackSoldiers(전체 병력 중 공격 파견 몫)를
            // 행진 그룹 규모 파라미터로 [1, MAX_SOLDIERS] 클램프해 사용 (공격적 영주 = 대규모 파견).
            _attackForce = Mathf.Clamp(LordPersonalitySystem.GetDeploymentPlan(attackerDef.id).attackSoldiers, 1, MAX_SOLDIERS);
            var attackerGarrison = TerritoryBuilder.SpawnGarrison(attackerDef, fromPos);
            for (int i = attackerGarrison.Count - 1; i >= _attackForce; i--)
            {
                var extra = attackerGarrison[i];
                if (extra != null) Destroy(extra);
                attackerGarrison.RemoveAt(i);
            }
            foreach (var soldier in attackerGarrison)
            {
                if (soldier == null) continue;
                _attackers.Add(soldier);
                var ph = soldier.GetComponent<GuardPlaceholder>();
                if (ph != null)
                    ph.SetCommandTarget(_targetPos, true); // → 실화 이동(ExecuteMovement 자동)
            }

            // ── 방어군 병사 스폰 (가시 교전 대상 — 공격군 도달 시 근접 공격으로 플린치 발생) ──
            var defenderGarrison = defenderDef.id.nation != NationType.None
                ? TerritoryBuilder.SpawnGarrison(defenderDef, targetPos)
                : new List<GameObject>();
            _defenders.AddRange(defenderGarrison);
        }

        private void Update()
        {
            if (_releasing) return;

            // ── 공격군 상태 검사: 도달(교전 종료·도달) 판정 ──
            int finishedCount = 0;
            foreach (var soldier in _attackers)
            {
                if (soldier == null) { finishedCount++; continue; }
                var ph = soldier.GetComponent<GuardPlaceholder>();
                if (ph == null) { finishedCount++; continue; }
                if (!ph.HasCommand) finishedCount++; // HasCommand false → 도달/교전 완료 판정
            }

            // 첫 도달 시 도달 버스트 + 로그 (가시적 충돌 파티클)
            if (finishedCount > 0 && !_arrivalLogged)
            {
                _arrivalLogged = true;
                float y = TerrainGenerator.GetHeightAt(_targetPos.x, _targetPos.z, BiomeType.Plains, 42);
                var hitPos = new Vector3(_targetPos.x, y + 1.2f, _targetPos.z);
                try
                {
                    CombatFXGate.PlayHitFX(hitPos, Vector3.up, CombatHitType.Organic,
                        false, 1f, new Color(1f, 0.75f, 0.2f, 1f));
                }
                catch {/* 이펙트 게이트 예산 등에 걸리면 무시 — 전쟁 흐름 무손상 */}
                Debug.Log($"[WarMarchSimulation] 🏴 병사 도달! {finishedCount}/{_attackers.Count}명 목표 영지 도달·교전!");
            }

            // ── 전원 도달/교전 종료 → 잠시 유지 후 정리 ──
            bool allFinished = _attackers.Count > 0 && finishedCount >= _attackers.Count;
            if (allFinished)
            {
                if (_elapsedHold <= 0f)
                    _elapsedHold = HOLD_DURATION; // 첫 완료 프레임에서 카운트 시작
                else
                    _elapsedHold -= Time.deltaTime;

                if (_elapsedHold <= 0f)
                    Release();
            }
            else if (_elapsedHold > 0f)
            {
                _elapsedHold = 0f; // 일부 병사가 다시 싸우면 카운트 리셋은 하지 않음(단순 유지로 처리)
            }
        }

        /// <summary>전투 종료 후 스폰된 병사들을 정리하고 그룹 반납.</summary>
        private void Release()
        {
            if (_releasing) return;
            _releasing = true;
            foreach (var s in _attackers) if (s != null) Destroy(s);
            _attackers.Clear();
            foreach (var d in _defenders) if (d != null) Destroy(d);
            _defenders.Clear();
            _activeMarches = Mathf.Max(0, _activeMarches - 1);
            Debug.Log($"[WarMarchSimulation] ✔ 행진 정리 완료 (활성 {_activeMarches}/{MAX_CONCURRENT})");
            Destroy(gameObject);
        }
    }
}