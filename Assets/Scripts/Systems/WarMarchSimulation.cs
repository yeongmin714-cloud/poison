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
    ///  1. 공격군 병사 그룹(영주 성향 배분, 영주별 동시 파견 최대 20명)을 공격측 영지에 스폰한다.
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
        public const int MAX_ATTACKING_SOLDIERS_PER_LORD = 20; // 영지 보유 수와 무관한 영주별 동시 공격 병력 상한
        private static readonly Dictionary<TerritoryId, int> _deployedAttackersByLord = new Dictionary<TerritoryId, int>();
        // 영주별로 동시 행진에 배정된 AI 재고용 로스터 stableId 집합 — 같은 로스터 병사가 두 행진에 중복 배정되지 않게 한다.
        private static readonly Dictionary<TerritoryId, HashSet<string>> _deployedRosterIdsByLord = new Dictionary<TerritoryId, HashSet<string>>();
        // 이 행진이 배정받은 로스터 stableId 목록(반납 시 해제용)과 stableId → 전투 프록시 매핑(결과 추적용).
        private readonly HashSet<string> _rosterStableIds = new HashSet<string>();
        private readonly Dictionary<string, GuardPlaceholder> _rosterProxyById = new Dictionary<string, GuardPlaceholder>();
        private const float HOLD_DURATION = 1.6f; // 도달·교전 종료 후 유지 후 정리 (달성감)

        /// <summary>영주의 남은 동시 공격 병력 한도 내에서 이번 공격 파견 수를 결정한다.</summary>
        public static int ResolveAttackForce(int plannedAttackSoldiers, int alreadyDeployed)
        {
            int remainingCapacity = Mathf.Max(0, MAX_ATTACKING_SOLDIERS_PER_LORD - alreadyDeployed);
            return Mathf.Clamp(plannedAttackSoldiers, 0, remainingCapacity);
        }


        private static int _activeMarches;

        private readonly List<GameObject> _attackers = new List<GameObject>();
        private readonly List<GameObject> _defenders = new List<GameObject>();

        private Vector3 _targetPos;
        private int _attackForce; // 영주별 동시 공격 파견 병력 수 [0, 20]
        private TerritoryId _attackerLordId;
        private float _elapsedHold;
        private bool _arrivalLogged;
        private bool _releasing;
        private TerritoryDefinition? _defenderDef;   // [55차] 방어 영지 — 정산 시 주둔군/문지기 합산용(struct — nullable)
        private int _linkedWarId = -1;              // [55차] 연동 AI 전쟁 ID(전투 판정 → SettleWarByBattle 반영)
        private bool _battleOutcomeResolved;        // ResolveBattleOutcome이 정상 완료된 경우에만 true(비정상 Release 경로에서는 결과 반영 안 함)
        private bool _rosterResultsSynced;          // 프록시 → 등록 원본 결과 반영은 1회만(멱등)

        /// <summary>
        /// 로스터 후보 stableId 중 이미 다른 행진에 배정된 것을 제외하고 앞에서부터 최대 force명 선택한다(순수 함수).
        /// force ≤ 0이면 빈 목록. 결과 개수는 MAX_ATTACKING_SOLDIERS_PER_LORD를 넘지 않는다.
        /// </summary>
        public static List<string> SelectRosterAttackerIds(IReadOnlyList<string> candidates, ISet<string> inUse, int force)
        {
            var result = new List<string>();
            if (candidates == null || force <= 0) return result;
            int cap = Mathf.Min(force, MAX_ATTACKING_SOLDIERS_PER_LORD);
            foreach (string sid in candidates)
            {
                if (result.Count >= cap) break;
                if (string.IsNullOrEmpty(sid)) continue;
                if (inUse != null && inUse.Contains(sid)) continue;
                if (result.Contains(sid)) continue;
                result.Add(sid);
            }
            return result;
        }

        /// <summary>영지 등록 기준 살아있는 AI 재고용 병사(중복 제외). GuardManager가 없으면 빈 목록.</summary>
        private static List<GuardPlaceholder> GetLiveRegisteredAIHiredGuards(TerritoryId id)
        {
            var list = new List<GuardPlaceholder>();
            if (GuardManager.Instance == null) return list;
            var seen = new HashSet<GuardPlaceholder>();
            foreach (GuardPlaceholder g in GuardManager.Instance.GetGuardsInTerritory(id))
            {
                if (g == null || !g.IsAlive || !g.IsAIHired) continue;
                if (seen.Add(g)) list.Add(g);
            }
            return list;
        }

        /// <summary>현재 진행 중인 행진 그룹 수.</summary>
        public static int ActiveMarches => _activeMarches;


        /// <summary>
        /// 가시 전쟁 행진 시작을 예약한다. 동시 진행 그룹 상한(MAX_CONCURRENT)을 넘으면 시작하지 않고 false(스킵).
        /// [55차] linkedWarId 전달 시 행진 종료 후 잔존 전력 판정이 AI 전쟁에 즉시 반영된다(점령/격퇴).
        /// </summary>
        public static bool TryStartMarch(TerritoryDefinition attackerDef, Vector3 fromPos, Vector3 targetPos,
            TerritoryDefinition defenderDef, int linkedWarId = -1)
        {
            if (_activeMarches >= MAX_CONCURRENT)
                return false;

            int plannedAttackForce = LordPersonalitySystem.GetDeploymentPlan(attackerDef.id).attackSoldiers;
            _deployedAttackersByLord.TryGetValue(attackerDef.id, out int alreadyDeployed);
            int attackForce = ResolveAttackForce(plannedAttackForce, alreadyDeployed);
            if (attackForce <= 0)
            {
                Debug.Log($"[WarMarchSimulation] 공격 파견 보류 — {attackerDef.territoryName}: 영주별 공격 병력 한도 {MAX_ATTACKING_SOLDIERS_PER_LORD}명 사용 중");
                return false;
            }

            var go = new GameObject("[WarMarchSimulation]");
            var sim = go.AddComponent<WarMarchSimulation>();
            sim._linkedWarId = linkedWarId;
            sim._attackerLordId = attackerDef.id;
            sim.Begin(attackerDef, fromPos, targetPos, defenderDef, attackForce);

            // 실제 스폰된 공격병 수만큼만 예약한다(정의 guardCount 기반 물리 병력이 계획보다 적을 수 있음).
            if (sim._attackers.Count <= 0)
            {
                Destroy(go);
                Debug.Log($"[WarMarchSimulation] 공격 파견 보류 — {attackerDef.territoryName}: 스폰 가능한 공격병 없음");
                return false;
            }
            if (sim._attackers.Count < sim._attackForce)
                sim._attackForce = sim._attackers.Count;

            _deployedAttackersByLord[attackerDef.id] = alreadyDeployed + sim._attackForce;
            if (sim._rosterStableIds.Count > 0)
            {
                if (!_deployedRosterIdsByLord.TryGetValue(attackerDef.id, out HashSet<string> inUse))
                {
                    inUse = new HashSet<string>();
                    _deployedRosterIdsByLord[attackerDef.id] = inUse;
                }
                inUse.UnionWith(sim._rosterStableIds);
            }
            _activeMarches++;
            Debug.Log($"[WarMarchSimulation] ⚔️ 행진 시작 — {attackerDef.territoryName} → {targetPos} (성향 A={LordPersonalitySystem.GetAggression(attackerDef.id):0.00}, 파견 {sim._attackForce}명 / 스폰 {sim._attackers.Count}명, 활성 {_activeMarches}/{MAX_CONCURRENT})");
            return true;
        }

        private void Begin(TerritoryDefinition attackerDef, Vector3 fromPos, Vector3 targetPos,
            TerritoryDefinition defenderDef, int attackForce)
        {
            _targetPos = targetPos;
            _defenderDef = defenderDef;   // [55차] 정산(주둔군/문지기 합산)용

            // ── 공격군 병사 그룹 스폰 ──
            // 영주 성향/보유 병력에 따른 계획을 반영해 이미 예약한 병력을 제외한 공격 파견 수를 사용.
            _attackForce = attackForce;

            // (1) 등록된 살아있는 AI 재고용 병력(로스터)을 우선 선발한다. 실제 등록 병사는 이동시키지 않고,
            //     로스터 DTO로 전투 프록시(비등록 클론)를 만든다 → 등록 목록/영지 집계에 중복 반영되지 않는다.
            //     이미 다른 동시 행진에 배정된 로스터 stableId는 제외한다.
            _attackers.Clear();
            var rosterIds = new List<string>();
            var rosterById = new Dictionary<string, GuardPlaceholder>();
            foreach (GuardPlaceholder g in GetLiveRegisteredAIHiredGuards(attackerDef.id))
            {
                string sid = g.StableId;
                if (rosterById.ContainsKey(sid)) continue;
                rosterById[sid] = g;
                rosterIds.Add(sid);
            }
            _deployedRosterIdsByLord.TryGetValue(attackerDef.id, out HashSet<string> inUse);
            List<string> chosen = SelectRosterAttackerIds(rosterIds, inUse, _attackForce);

            string lordKey = attackerDef.id.ToString();
            for (int i = 0; i < chosen.Count; i++)
            {
                GuardPlaceholder source = rosterById[chosen[i]];
                AIHiredGuardSaveEntry entry = source.ToPersistentRosterEntry(lordKey);
                if (entry == null) continue;
                GuardPlaceholder proxy = TerritoryBuilder.SpawnAIHiredGuard(attackerDef, i, entry);
                if (proxy == null) continue;
                _attackers.Add(proxy.gameObject);
                _rosterProxyById[entry.stableId] = proxy;   // 안정 ID ↔ 전투 프록시 매핑(결과 추적용)
                _rosterStableIds.Add(entry.stableId);
                proxy.SetCommandTarget(_targetPos, true);
                proxy.IsWartimeCapturable = true;
            }

            // (2) 로스터로 채우지 못한 나머지는 기존 영지 주둔군 스폰으로 채운다(기존 동작 유지).
            int garrisonNeeded = _attackForce - _attackers.Count;
            if (garrisonNeeded > 0)
            {
                var attackerGarrison = TerritoryBuilder.SpawnGarrisonCapped(attackerDef, fromPos, garrisonNeeded);
                foreach (var soldier in attackerGarrison)
                {
                    if (soldier == null) continue;
                    _attackers.Add(soldier);
                    var ph = soldier.GetComponent<GuardPlaceholder>();
                    if (ph != null)
                    {
                        ph.SetCommandTarget(_targetPos, true); // → 실화 이동(ExecuteMovement 자동)
                        // [Phase G] AI 파견 공격부대 노획 가능 표식 — 전투에서 쓰러지면 아군으로 전환
                        ph.IsWartimeCapturable = true;
                    }
                }
            }

            // 공격병이 하나도 없으면 행진 자체가 종료되지 않으므로 방어군도 스폰하지 않는다(TryStartMarch가 정리).
            if (_attackers.Count == 0) return;

            // ── 방어군 병사 스폰 (가시 교전 대상 — 공격군 도달 시 근접 공격으로 플린치 발생) ──
            var defenderGarrison = defenderDef.id.nation != NationType.None
                ? TerritoryBuilder.SpawnGarrison(defenderDef, targetPos)
                : new List<GameObject>();
            foreach (var soldier in defenderGarrison)
            {
                if (soldier == null) continue;
                _defenders.Add(soldier);
                // [55차] 수비 제자리 반격 — 홀드 상태(근접 적 자동 반격, 국가 상이 대상 포함)로
                //   침공군과 **실제 교전**이 성립한다(51차 홀드 반격 확장 재사용).
                var dph = soldier.GetComponent<GuardPlaceholder>();
                if (dph != null) dph.SetHold(true);
            }
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
                {
                    ResolveBattleOutcome();   // [55차] 영지 전쟁 승패 판정(실내 전투 대체 수치 판정) → Release 전 정산
                    Release();
                }
            }
            else if (_elapsedHold > 0f)
            {
                _elapsedHold = 0f; // 일부 병사가 다시 싸우면 카운트 리셋은 하지 않음(단순 유지로 처리)
            }
        }

        /// <summary>[55차] 영지 전쟁 승패 판정(사용자 설계) — AI 공격군은 실내 전투까지 못 하므로,
        ///   야외 교전(수비군·문지기)이 끝난 뒤 **남은 공격군(공격력·잔존 체력)**과 **방어층 잔존 전력**
        ///   (행진 방어군 + 영지 주둔군/문지기 — GuardManager 등록 기준, 중복 제외)을 비교해
        ///   승리(점령) 또는 패배(격퇴)를 판정한다.
        ///   전력 가중 = Σ(공격력, level×1.5 — PerformAttack 데미지 공식 동일 계수) + Σ(잔존 체력)×0.5.
        ///   동률은 수비 우위 원칙(방어 승). 연동 AI 전쟁이 있으면 SettleWarByBattle로 즉시 반영.</summary>
        private void ResolveBattleOutcome()
        {
            if (_releasing) return;

            int atkAlive = 0; float atkHp = 0f, atkPower = 0f;
            foreach (var s in _attackers)
            {
                if (s == null) continue;
                var ph = s.GetComponent<GuardPlaceholder>();
                if (ph == null || !ph.IsAlive) continue;
                atkAlive++;
                atkHp += ph.HP;
                atkPower += ph.Level * 1.5f;
            }

            var counted = new HashSet<GuardPlaceholder>();
            int defAlive = 0; float defHp = 0f, defPower = 0f;
            foreach (var d in _defenders)
            {
                if (d == null) continue;
                var ph = d.GetComponent<GuardPlaceholder>();
                if (ph == null || !ph.IsAlive) continue;
                counted.Add(ph);
                defAlive++;
                defHp += ph.HP;
                defPower += ph.Level * 1.5f;
            }
            if (_defenderDef != null && GuardManager.Instance != null)
            {
                foreach (var g in GuardManager.Instance.GetGuardsInTerritory(_defenderDef.Value.id))
                {
                    if (g == null || !g.IsAlive || !counted.Add(g)) continue;   // 행진 방어군 중복 제외
                    defAlive++;
                    defHp += g.HP;
                    defPower += g.Level * 1.5f;
                }
            }

            float atkScore = atkPower + atkHp * 0.5f;
            float defScore = defPower + defHp * 0.5f;
            bool attackerWon = atkScore > defScore;

            string verdict = attackerWon
                ? (_linkedWarId >= 0 ? "⚔️ 공격 승리 — 영지 점령 반영" : "⚔️ 공격 승리(연동 전쟁 없음 — 판정 로그만)")
                : "🛡️ 방어 승리 — 침공 격퇴";
            Debug.Log($"[영지전쟁 판정] {verdict} — 공격군 잔존 {atkAlive}명(공격력 {atkPower:0}, 체력 {atkHp:0}) vs 방어 잔존 {defAlive}명(공격력 {defPower:0}, 체력 {defHp:0}) | 전력 {atkScore:0} vs {defScore:0}");
            _battleOutcomeResolved = true;   // 판정 완료 — 이후 Release에서 프록시 결과를 원본에 반영한다

            if (_linkedWarId >= 0)
                AIWarSystem.SettleWarByBattle(_linkedWarId, attackerWon);
        }

        /// <summary>
        /// 전투 프록시(_rosterProxyById)의 결과를 등록 원본 AI 고용 병사에 1회 반영한다(멱등).
        ///  - 생존: HP만 프록시 값으로 갱신(마커/소유 영지는 원본 DTO 그대로 유지).
        ///  - 사망: 원본을 사망 상태로 저장하고 영지 등록에서 제거 → 이후 배치/저장(LaborMarket Export)에서 제외.
        ///  - 노획(IsRecruited): 원본의 AI 마커를 해제하고 AI 로스터(영지 등록)에서 제거, 원본은 아군(IsRecruited)으로 전환.
        ///    노획된 프록시는 기존 동작대로 Release에서 정리되며, 원본과 프록시가 동시에 살아 집계되지 않도록 원본만 로스터에서 뺀다.
        ///    (소유권 귀속 대상 영지가 불명확하므로 원본 GameObject 파괴나 신규 영지 등록은 하지 않는다 — 보수적 처리.)
        /// 원본이 이미 사라졌거나(파괴/등록 해제) AI 고용 상태가 아니면 건너뛴다. 전투 판정이 정상 완료된 경우에만 호출된다.
        /// </summary>
        private void SyncRosterProxyResults()
        {
            if (_rosterResultsSynced || !_battleOutcomeResolved) return;
            _rosterResultsSynced = true;
            if (_rosterProxyById.Count == 0) return;
            if (GuardManager.Instance == null)
            {
                Debug.LogWarning("[WarMarchSimulation] GuardManager 부재 — 로스터 결과 반영 생략");
                return;
            }

            // stableId → 등록 원본(AI 고용) 매핑 (등록 목록 1회 스캔)
            var originalById = new Dictionary<string, GuardPlaceholder>();
            foreach (var kvp in GuardManager.Instance.GetAllRegisteredGuards())
            {
                GuardPlaceholder g = kvp.Value;
                if (g == null || !g.IsAIHired) continue;
                string sid = g.StableId;
                if (!string.IsNullOrEmpty(sid) && !originalById.ContainsKey(sid)) originalById[sid] = g;
            }

            string ownerId = _attackerLordId.ToString();
            var applied = new HashSet<GuardPlaceholder>();   // 같은 원본에 두 번 반영하지 않음
            foreach (var kvp in _rosterProxyById)
            {
                GuardPlaceholder proxy = kvp.Value;
                if (proxy == null) continue;
                if (!originalById.TryGetValue(kvp.Key, out GuardPlaceholder original) || original == null) continue;
                if (!applied.Add(original)) continue;

                AIHiredGuardSaveEntry dto = original.ToPersistentRosterEntry(ownerId);
                if (dto == null) continue;

                // 프록시의 최종 상태를 원본 DTO에 반영 (마커/소유/스탯은 원본 값 유지)
                dto.hp = proxy.HP;
                dto.isAlive = proxy.IsAlive;

                if (proxy.IsRecruited)
                {
                    // 노획: 원본을 AI 로스터에서 제거 → 아군 상태로 전환(이중 집계 방지)
                    original.ApplyPersistentRosterEntry(dto);
                    GuardManager.Instance.RemoveGuardFromAllTerritories(original);
                    original.IsAIHired = false;
                    original.SetRecruited(true);
                    // 아군 standalone 목록에 등록(중복 등록은 RegisterStandalone이 무시). 입관비는 Die()의 capture 과금으로 이미 처리됨 — 재과금 없음.
                    var guardManager = GuardManager.Instance;
                    if (guardManager != null) guardManager.RegisterStandalone(original);
                    Debug.Log($"[WarMarchSimulation] 노획 결과 반영 — {original.GuardName}(sid={kvp.Key}) AI 로스터 제외 → 아군 전환");
                }
                else if (!proxy.IsAlive)
                {
                    // 사망: 사망 상태 저장 + 영지 등록 제거 (배치/Export 제외)
                    dto.isAlive = false;
                    original.ApplyPersistentRosterEntry(dto);
                    GuardManager.Instance.RemoveGuardFromAllTerritories(original);
                    Debug.Log($"[WarMarchSimulation] 전사 결과 반영 — {original.GuardName}(sid={kvp.Key}) 영지 로스터에서 제거");
                }
                else
                {
                    // 생존: HP 갱신, 등록 유지
                    original.ApplyPersistentRosterEntry(dto);
                }
            }
        }

        /// <summary>전투 종료 후 스폰된 병사들을 정리하고 그룹 반납.</summary>
        private void Release()
        {
            if (_releasing) return;
            _releasing = true;
            SyncRosterProxyResults();   // 프록시 파괴 전에 결과를 원본에 1회 반영
            foreach (var s in _attackers) if (s != null) Destroy(s);
            _attackers.Clear();
            foreach (var d in _defenders) if (d != null) Destroy(d);
            _defenders.Clear();
            if (_deployedAttackersByLord.TryGetValue(_attackerLordId, out int deployed))
            {
                int remaining = Mathf.Max(0, deployed - _attackForce);
                if (remaining == 0) _deployedAttackersByLord.Remove(_attackerLordId);
                else _deployedAttackersByLord[_attackerLordId] = remaining;
            }
            if (_deployedRosterIdsByLord.TryGetValue(_attackerLordId, out HashSet<string> rosterInUse))
            {
                rosterInUse.ExceptWith(_rosterStableIds);
                if (rosterInUse.Count == 0) _deployedRosterIdsByLord.Remove(_attackerLordId);
            }
            _rosterStableIds.Clear();
            _rosterProxyById.Clear();
            _activeMarches = Mathf.Max(0, _activeMarches - 1);
            Debug.Log($"[WarMarchSimulation] ✔ 행진 정리 완료 (활성 {_activeMarches}/{MAX_CONCURRENT})");
            Destroy(gameObject);
        }
    }
}