using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// 영지 병사 역할/작업 배치 — 사냥/공격/수비/채집/농경 역할을 병사에게 부여하고,
    /// 쿨다운 기반 행동 루프로 자동 수행한다.
    ///
    /// - Attack  : 플레이어를 추종(밖에서도 따라다님). SetCommandTarget(플레이어 위치, attack:false) 주기 갱신.
    /// - Defend  : 해당 영지 성문 앞 수비 위치에 주둔(hold). SetCommandTarget(성문 앞, attack:false).
    /// - Gather  : 가장 가까운 약초(풀) 노드 채집 — GatheringSystem.TryGather → 약초 인벤토리 적립.
    /// - Hunt    : 가장 가까운 몬스터 공격(IDamageable.TakeDamage) + 확률 전리품(고기) + 일부 사망 확률.
    /// - Farm    : 가장 가까운 밭(FarmPlot) 파종/수확 — FarmingSystem.Plant/Harvest.
    /// - Mine    : 가장 가까운 자원 노드(ResourceNode) 채굴 — TryAutoMine → 인벤토리 적립.
    ///
    /// 싱글턴 패턴은 FarmingSystem/GatheringSystem의 Ensure 패턴을 그대로 미러링한다.
    /// </summary>
    public class GuardTaskSystem : MonoBehaviour
    {
        const string LogTag = "[GuardTaskSystem]";

        /// <summary>병사 작업/역할 — 플레이어 소유 영지 병사에게 부여한다.</summary>
        public enum GuardTask
        {
            None,     // 작업 없음 (해제)
            Attack,   // 플레이어 추종 (동행)
            Defend,   // 성문 앞 수비
            Gather,   // 약초 채집
            Hunt,     // 몬스터 사냥
            Farm,     // 농사 (파종/수확)
            Envoy,    // 🕵️ 특사 — 적 영지 잠입 첩보 (민첩이 높을수록 발각 확률 낮음, 발각 시 처형)
            Mine      // ⛏️ 광질 — ResourceNode(Wood/Stone/IronOre) 채굴, 인벤토리 적립
        }

        /// <summary>싱글턴 — 비월드/파괴 대비.</summary>
        public static GuardTaskSystem Instance { get; private set; }

        // 작업 상태: 병사 → 배정된 역할
        readonly Dictionary<GuardPlaceholder, GuardTask> _tasks = new Dictionary<GuardPlaceholder, GuardTask>();
        // 병사 → 영지 (Defend/죽음 시 정리용, AssignTerritoryTask가 기록)
        readonly Dictionary<GuardPlaceholder, TerritoryId> _taskTerritories = new Dictionary<GuardPlaceholder, TerritoryId>();
        // 병사 → 다음 행동 허용 시각 (쿨다운)
        readonly Dictionary<GuardPlaceholder, float> _nextAction = new Dictionary<GuardPlaceholder, float>();

        // 행동 쿨다운 (초)
        const float UpdateThrottleSec = 0.4f;   // 전역 업데이트 스로틀
        const float ActionCooldownSec = 4f;     // 기본 역할 행동 쿨다운
        const float FarmCooldownSec = 3f;       // 농사 쿨다운
        const float EnvoyCooldownSec = 30f;     // 특사(정보원) 첩보 쿨다운 — 장시간 유지 (스팸 방지)
        const float MineCooldownSec = 3f;       // 광질 쿨다운 — 농사(Farm)와 동일 주기
        const float AttackSetTargetSec = 1f;    // 추종 위치 갱신 주기

        // 범위
        const float GatherRange = 15f;
        const float HuntRange = 20f;
        const float FarmRange = 12f;
        const float MineRange = 15f;            // 광질 노드 탐색 범위 — 채집(Gather)과 동일

        // 사냥 확률
        const float HuntDropChance = 0.6f;      // 60% 확률로 전리품(고기) 지급
        const float HuntDeathChance = 0.08f;    // 8% 확률로 사냥 중 병사 사망

        // 전역 스로틀 누적
        float _throttleAccum = 0f;

        /// <summary>
        /// 싱글턴 보장. parent가 있으면 그 자식으로 생성(중복 방지).
        /// FarmingSystem.Ensure와 동일 패턴.
        /// </summary>
        public static GuardTaskSystem Ensure(GameObject parent = null)
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<GuardTaskSystem>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return existing; }

            var go = new GameObject("GuardTaskSystem");
            if (parent != null) go.transform.SetParent(parent.transform, false);
            return go.AddComponent<GuardTaskSystem>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // 스로틀 — 매 프레임이 아니라 0.4s 간격으로 행동 루프만 실행.
            _throttleAccum += Time.deltaTime;
            if (_throttleAccum < UpdateThrottleSec) return;
            _throttleAccum = 0f;

            List<GuardPlaceholder> deadKeys = null;
            foreach (var kv in _tasks)
            {
                GuardPlaceholder guard = kv.Key;
                if (guard == null || !guard.IsAlive)
                {
                    if (deadKeys == null) deadKeys = new List<GuardPlaceholder>();
                    deadKeys.Add(guard);
                    continue;
                }

                // 살아있는 배정 병사만 역할 루틴 실행.
                try { RunRoleRoutine(guard, kv.Value); }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"{LogTag} 역할 루틴 예외 ({kv.Value}): {e.Message}");
                }
            }

            // 죽거나 비활성화된 병사의 작업 해제 (정리)
            if (deadKeys != null)
            {
                foreach (var g in deadKeys)
                {
                    _tasks.Remove(g);
                    _nextAction.Remove(g);
                    _taskTerritories.Remove(g);
                }
            }
        }

        // ================================================================
        // 공개 API — 작업 배정
        // ================================================================

        /// <summary>단일 병사에게 역할 배정. 기존 명령은 해제하고 작업 딕셔너리에 기록.</summary>
        public void AssignTask(GuardPlaceholder g, GuardTask task)
        {
            if (g == null) return;
            if (task == GuardTask.None)
            {
                ReleaseTask(g);
                return;
            }
            g.ClearCommand();
            _tasks[g] = task;
            _nextAction[g] = 0f;
            Debug.Log($"{LogTag} {g.GuardName} → {task}");
        }

        /// <summary>연결된 연결된 플레이어 소유 영지의 모든 병사에게 역할 배정.</summary>
        public int AssignTerritoryTask(TerritoryId id, GuardTask task)
        {
            int n = 0;
            if (GuardManager.Instance == null) return n;
            foreach (var guard in GuardManager.Instance.GetGuardsInTerritory(id))
            {
                if (guard == null) continue;
                AssignTask(guard, task);
                _taskTerritories[guard] = id;
                n++;
            }
            Debug.Log($"{LogTag} 영지 {id} 병사 {n}명 → {task}");
            return n;
        }

        /// <summary>병사의 작업 해제 — None으로 되돌리고 명령 해제.</summary>
        public void ReleaseTask(GuardPlaceholder g)
        {
            if (g == null) return;
            _tasks.Remove(g);
            _nextAction.Remove(g);
            _taskTerritories.Remove(g);
            g.ClearCommand();
        }

        /// <summary>병사의 현재 배정 역할 (없으면 None).</summary>
        public GuardTask GetTask(GuardPlaceholder g)
        {
            if (g == null) return GuardTask.None;
            return _tasks.TryGetValue(g, out var t) ? t : GuardTask.None;
        }

        /// <summary>AssigneeTerritoryTask가 기록한 병사의 영지 (없으면 찾기).</summary>
        private TerritoryId ResolveGuardTerritory(GuardPlaceholder guard)
        {
            if (_taskTerritories.TryGetValue(guard, out var cached)) return cached;

            // GuardManager의 모든 플레이어 소유 영지를 뒤져 이 병사를 찾는다.
            if (TerritoryDatabase.Instance != null && GuardManager.Instance != null)
            {
                foreach (var def in TerritoryDatabase.Instance.GetAllDefinitions())
                {
                    TerritoryState st = TerritoryDatabase.Instance.GetState(def.id);
                    if (st == null || st.ownership != TerritoryOwnership.PlayerOwned) continue;
                    foreach (var cand in GuardManager.Instance.GetGuardsInTerritory(def.id))
                    {
                        if (cand == guard)
                        {
                            _taskTerritories[guard] = def.id;
                            return def.id;
                        }
                    }
                }
            }
            return default;
        }

        // ================================================================
        // 역할 루틴
        // ================================================================

        private void RunRoleRoutine(GuardPlaceholder guard, GuardTask task)
        {
            switch (task)
            {
                case GuardTask.Attack:  RoutineAttack(guard);  break;
                case GuardTask.Defend:  RoutineDefend(guard);  break;
                case GuardTask.Gather:  RoutineGather(guard);  break;
                case GuardTask.Hunt:    RoutineHunt(guard);    break;
                case GuardTask.Farm:    RoutineFarm(guard);    break;
                case GuardTask.Envoy:   RoutineEnvoy(guard);   break;
                case GuardTask.Mine:    RoutineMine(guard);    break;
            }
        }

        /// <summary>Attack — 플레이어 추종 (밖에서도). 위치를 주기적으로 갱신해 따라다닌다.</summary>
        private void RoutineAttack(GuardPlaceholder guard)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            // 주기적으로만 명령 위치 갱신 (쿨다운 재사용)
            if (!CooldownReady(guard, AttackSetTargetSec)) return;

            // attack:false — 이동 명령 (명령 지점 도달 시 GuardPlaceholder가 이동 완료 처리)
            guard.SetCommandTarget(player.transform.position, false);
            guard.SetInCombat(false);
            CooldownStamp(guard, AttackSetTargetSec);
        }

        /// <summary>Defend — 영지 성문 앞 수비. TerritoryDeploymentSystem의 성문 산식을 재사용해 주둔.</summary>
        private void RoutineDefend(GuardPlaceholder guard)
        {
            if (!CooldownReady(guard, ActionCooldownSec)) return;

            TerritoryId tid = ResolveGuardTerritory(guard);
            if (!tid.Equals(default) && TerritoryDatabase.Instance != null)
            {
                TerritoryDefinition def = TerritoryDatabase.Instance.GetDefinition(tid);
                Vector3 center = def.worldPosition != Vector3.zero ? def.worldPosition : Vector3.zero;
                Vector3 gateFront = ComputeGateFrontFrom(center);
                guard.SetCommandTarget(gateFront, false);
            }
            else
            {
                // 영지 미식별 — 제자리 수비 (현재 위치 홀드)
                guard.SetCommandTarget(guard.transform.position, false);
            }
            guard.SetInCombat(false);
            CooldownStamp(guard, ActionCooldownSec);
        }

        /// <summary>성문 앞 위치 — TerritoryDeploymentSystem.ComputeGateFront와 동일 수식 (황제국 중심 기준 바깥 방향 + 3m).</summary>
        private static Vector3 ComputeGateFrontFrom(Vector3 center)
        {
            Vector3 c2 = new Vector3(center.x, 0, center.z);
            Vector3 gateDir = c2.sqrMagnitude < 0.0001f ? Vector3.back : Vector3.Normalize(c2);
            return center + gateDir * 3f;
        }

        /// <summary>Gather — 주변 약초(풀) 노드 채집. GatheringSystem.TryGather가 약초를 인벤토리에 적립.</summary>
        private void RoutineGather(GuardPlaceholder guard)
        {
            if (!CooldownReady(guard, ActionCooldownSec)) return;

            GameObject node = FindNearestGatherNode(guard.transform.position, GatherRange);
            if (node == null)
            {
                CooldownStamp(guard, ActionCooldownSec);
                return;
            }

            // 노드로 이동 명령 후 가까우면 채집
            if (Vector3.Distance(guard.transform.position, node.transform.position) > 2.5f)
            {
                guard.SetCommandTarget(node.transform.position, false);
            }

            // GatheringSystem에 노드 전달 → 성공 시 PlayerInventory에 약초 적립.
            if (GatheringSystem.CanGather(node))
            {
                bool ok = GatheringSystem.TryGather(node);
                Debug.Log($"{LogTag} {guard.GuardName} 🌿 약초 채집 {(ok ? "성공" : "실패/리스폰 중")} ({node.name})");
            }

            CooldownStamp(guard, ActionCooldownSec);
        }

        /// <summary>Hunt — 주변 몬스터 공격 + 확률 전리품 + 병사 사망 확률.</summary>
        private void RoutineHunt(GuardPlaceholder guard)
        {
            if (!CooldownReady(guard, ActionCooldownSec)) return;

            GameObject monster = FindNearestMonster(guard.transform.position, HuntRange);
            if (monster == null)
            {
                CooldownStamp(guard, ActionCooldownSec);
                return;
            }

            if (Vector3.Distance(guard.transform.position, monster.transform.position) > 2.5f)
            {
                guard.SetCommandTarget(monster.transform.position, true);
                CooldownStamp(guard, ActionCooldownSec);
                return;
            }

            // 근접 — 몬스터 데미지
            var dmg = monster.GetComponentInParent<IDamageable>();
            if (dmg == null || !dmg.IsAlive)
            {
                CooldownStamp(guard, ActionCooldownSec);
                return;
            }

            int atk = guard.GetAttack();
            Vector3 dir = (monster.transform.position - guard.transform.position).normalized;
            if (dir == Vector3.zero) dir = Vector3.forward;
            dmg.TakeDamage(atk, dir, "melee");
            Debug.Log($"{LogTag} {guard.GuardName} 🏹 사냥 → {monster.name} 데미지 {atk}");

            // 확률 전리품 (고기) 지급
            if (Random.value < HuntDropChance)
            {
                GrantHuntLoot(guard, monster);
            }

            // 확률 병사 사망 (사냥 중 전투 사망) — TakeDamage는 Die() 경로를 타며 GuardManager에서 제거됨.
            if (Random.value < HuntDeathChance && guard.IsAlive)
            {
                Debug.Log($"{LogTag} {guard.GuardName} 💀 사냥 중 사망!");
                guard.TakeDamage(99999f, dir, "melee");
            }

            CooldownStamp(guard, ActionCooldownSec);
        }

        /// <summary>사냥 전리품 — PlayerInventory에 고기(meat_rabbit) 지급.</summary>
        private static void GrantHuntLoot(GuardPlaceholder guard, GameObject monster)
        {
            if (PlayerInventory.Instance == null) return;
            // meat_rabbit (RabbitMeat) — 코드에 정의된 고기 아이템. 실존 id 사용.
            bool ok = PlayerInventory.Instance.AddItem(PlayerInventory.RabbitMeat, 1);
            Debug.Log($"{LogTag} {guard.GuardName} 🎁 사냥 전리품 토끼고기 x1 지급 {(ok ? "성공" : "실패(가득 참)")} ← {monster.name}");
        }

        /// <summary>
        /// ⛏️ Mine — 가장 가까운 자원 노드(ResourceNode: Wood/Stone/IronOre) 채굴.
        /// ResourceNode.TryAutoMine → PlayerInventory.AddItem로 광물 인벤토리 적립.
        /// (RoutineGather/RoutineFarm과 동일 패턴: 이동 명령 → 근접 후 작업 → 쿨다운.)
        /// </summary>
        private void RoutineMine(GuardPlaceholder guard)
        {
            if (!CooldownReady(guard, MineCooldownSec)) return;

            ResourceNode node = FindNearestResourceNode(guard.transform.position, MineRange);
            if (node == null)
            {
                CooldownStamp(guard, MineCooldownSec);
                return;
            }

            // 노드로 이동 명령 후 가까우면 채굴
            if (Vector3.Distance(guard.transform.position, node.transform.position) > 2.5f)
            {
                guard.SetCommandTarget(node.transform.position, false);
            }

            // 채굴 — 고갈 전 노드만. 성공 시 광물을 플레이어 인벤토리에 적립.
            if (node.IsAvailable && node.TryAutoMine(out PlayerInventory.ItemData item, out int yield))
            {
                if (item != null && PlayerInventory.Instance != null)
                {
                    bool ok = PlayerInventory.Instance.AddItem(item, yield);
                    Debug.Log($"{LogTag} {guard.GuardName} ⛏️ 광질 성공 {yield}({node.name}){(ok ? "" : " — 인벤 가득 참")}");
                }
            }

            CooldownStamp(guard, MineCooldownSec);
        }

        /// <summary>Farm — 가장 가까운 밭 파종/수확. FarmingSystem.Plant/Harvest 사용.</summary>
        private void RoutineFarm(GuardPlaceholder guard)
        {
            if (!CooldownReady(guard, FarmCooldownSec)) return;

            GameObject plot = FindNearestFarmPlot(guard.transform.position, FarmRange);
            if (plot == null)
            {
                CooldownStamp(guard, FarmCooldownSec);
                return;
            }

            if (Vector3.Distance(guard.transform.position, plot.transform.position) > 2.5f)
            {
                guard.SetCommandTarget(plot.transform.position, false);
            }

            // 1) 자란 밭 → 수확 (PlayerInventory에 약초 적립)
            if (FarmingSystem.IsGrown(plot))
            {
                int harvested = FarmingSystem.Harvest(plot);
                if (harvested > 0)
                    Debug.Log($"{LogTag} {guard.GuardName} 🌾 밭 수확 완료 ({plot.name}) x{harvested}");
            }
            // 2) 빈 밭 + 씨앗 보유 → 파종 (씨앗 소모)
            else if (FarmingSystem.IsPlotReady(plot))
            {
                int seedCount = PlayerInventory.Instance != null
                    ? PlayerInventory.Instance.GetItemCount(FarmingSystem.SeedItemId) : 0;
                if (seedCount > 0 && FarmingSystem.Plant(plot, FarmingSystem.SeedItemId))
                {
                    Debug.Log($"{LogTag} {guard.GuardName} 🌱 밭 파종 완료 ({plot.name})");
                }
            }

            CooldownStamp(guard, FarmCooldownSec);
        }

        /// <summary>
        /// 🕵️ Envoy(특사) — 적(미소유) 영지로 잠입해 영주/병력 정보를 수집한다.
        /// SpySystem.SendSpy를 경유하므로 민첩(GetAgility)이 높을수록 발각 확률이 낮다(SpySystem 민첩 보정).
        /// 발각되면 SpySystem이 정보원을 처형(EXECUTION_DAMAGE)하며, 안전망으로 즉시 사망 처리한다.
        /// </summary>
        private void RoutineEnvoy(GuardPlaceholder guard)
        {
            if (!CooldownReady(guard, EnvoyCooldownSec)) return;

            // 타겟: 플레이어 소유가 아닌 가장 가까운 영지 (적/무주지 잠입)
            TerritoryId targetId = FindNearestEnemyTerritoryId(guard);
            if (targetId.Equals(default) || TerritoryDatabase.Instance == null)
            {
                CooldownStamp(guard, EnvoyCooldownSec);
                return;
            }

            TerritoryDefinition targetDef = TerritoryDatabase.Instance.GetDefinition(targetId);
            string targetName = string.IsNullOrEmpty(targetDef.territoryName)
                ? targetId.ToString()
                : targetDef.territoryName;

            // 영주 정보(Lv.5+) + 병력 정보(Lv.10+) 수집 — 레벨 미달은 SpySystem이 Fail 반환.
            SpySystem.SpyResult lord = SpySystem.SendSpy(guard, targetId, SpySystem.SpyMission.LordInfo);
            SpySystem.SpyResult troop = SpySystem.SendSpy(guard, targetId, SpySystem.SpyMission.TroopInfo);

            // 발각 시 — SpySystem이 이미 처형(EXECUTION_DAMAGE). 안전망으로 즉시 사망 처리.
            if (lord.detected || troop.detected)
            {
                if (guard.IsAlive)
                    guard.TakeDamage(999999f, Vector3.zero, "melee");
                Debug.Log($"{LogTag} 💀 정보원 {guard.GuardName}이(가) {targetName} 잠입 중 발각되어 처형되었습니다.");
                CooldownStamp(guard, EnvoyCooldownSec);
                return;
            }

            // 성공한 임무의 정보를 합산한 첩보 보고서 구성
            var report = new List<string>();
            if (lord.success && !string.IsNullOrEmpty(lord.infoGathered))
                report.Add(lord.infoGathered);
            if (troop.success && !string.IsNullOrEmpty(troop.infoGathered))
                report.Add(troop.infoGathered);

            // 재화/보물 정보 추가 (TerritoryState에 실제 필드가 없어 추정 지수로 보고)
            AppendTreasuryInfo(targetDef, report);

            string summary = string.Join("\n", report.ToArray());
            if (string.IsNullOrEmpty(summary)) summary = "(수집된 정보 없음 — 레벨 부족 등)";
            Debug.Log($"{LogTag} 🕵️ {guard.GuardName} 특사 첩보 보고 [{targetName}]\n{summary}");

            CooldownStamp(guard, EnvoyCooldownSec);
        }

        /// <summary>
        /// 특사 잠입 타겟 — 플레이어 소유 영지를 제외한 가장 가까운 영지 정의 선택.
        /// (GuardTaskSystem.ResolveGuardTerritory로 자기가 소속된 영지는 제외.)
        /// </summary>
        private TerritoryId FindNearestEnemyTerritoryId(GuardPlaceholder guard)
        {
            if (TerritoryDatabase.Instance == null) return default;
            TerritoryId own = ResolveGuardTerritory(guard);
            TerritoryId best = default;
            float bestDist = float.MaxValue;
            foreach (var def in TerritoryDatabase.Instance.GetAllDefinitions())
            {
                if (def.id.Equals(own)) continue;
                TerritoryState st = TerritoryDatabase.Instance.GetState(def.id);
                if (st != null && st.ownership == TerritoryOwnership.PlayerOwned) continue; // 아군 영지 제외
                float d = Vector3.Distance(guard.transform.position, def.worldPosition);
                if (d < bestDist) { bestDist = d; best = def.id; }
            }
            return best;
        }

        /// <summary>
        /// 첩보 보고에 재화/보물 정보를 추가한다.
        /// TerritoryState에는 treasury/gold/보물 전용 공개 필드가 없어 실제 재화량은 읽지 못한다 —
        /// 대신 영지 난이도 링 기반 "보물 지수(추정)"를 보고에 담아 목표 재화/보물 안내를 남긴다.
        /// (향후 TerritoryState에 treasury 필드가 추가되면 여기서 실제 값을 조회·추가.)
        /// </summary>
        private static void AppendTreasuryInfo(TerritoryDefinition def, List<string> report)
        {
            int treasureIndex = 1 + (int)def.difficulty; // Ring1=1 ~ Empire=5
            report.Add($"재화/보물: 유력 (추정 지수 {treasureIndex})");
        }

        // ================================================================
        // 쿨다운 헬퍼
        // ================================================================

        private bool CooldownReady(GuardPlaceholder guard, float cooldownSec)
        {
            float now = Time.time;
            if (_nextAction.TryGetValue(guard, out var t) && now < t) return false;
            _nextAction[guard] = 0f;
            return true;
        }

        private void CooldownStamp(GuardPlaceholder guard, float cooldownSec)
        {
            _nextAction[guard] = Time.time + cooldownSec;
        }

        // ================================================================
        // 탐지 헬퍼
        // ================================================================

        /// <summary>가장 가까운 채집 가능한 약초/풀 노드 탐색 (HashSet/리스트 순회 — foreach).</summary>
        private static GameObject FindNearestGatherNode(Vector3 origin, float range)
        {
            Collider[] hits = Physics.OverlapSphere(origin, range);
            GameObject best = null;
            float bestDist = float.MaxValue;
            foreach (var hit in hits)
            {
                if (hit == null) continue;
                GameObject go = hit.gameObject;
                if (go == null) continue;
                if (!IsGatherNode(go)) continue;
                float d = Vector3.Distance(origin, go.transform.position);
                if (d < bestDist) { bestDist = d; best = go; }
            }
            return best;
        }

        /// <summary>풀/약초 노드 판정 — HerbPickup 컴포넌트 또는 이름/태그에 herb/풀/grass/약초 포함.</summary>
        private static bool IsGatherNode(GameObject go)
        {
            if (go.GetComponentInChildren<HerbPickup>() != null) return true;
            if (string.Equals(go.tag, "Gather", System.StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(go.tag, "Grass", System.StringComparison.OrdinalIgnoreCase)) return true;
            string lower = go.name.ToLowerInvariant();
            return lower.IndexOf("herb", System.StringComparison.Ordinal) >= 0
                || lower.IndexOf("grass", System.StringComparison.Ordinal) >= 0
                || go.name.IndexOf("풀", System.StringComparison.Ordinal) >= 0
                || go.name.IndexOf("약초", System.StringComparison.Ordinal) >= 0;
        }

        /// <summary>가장 가까운 몬스터 탐색 (tag "Monster"/"Enemy" + IDamageable·IsAlive).</summary>
        private static GameObject FindNearestMonster(Vector3 origin, float range)
        {
            Collider[] hits = Physics.OverlapSphere(origin, range);
            GameObject best = null;
            float bestDist = float.MaxValue;
            foreach (var hit in hits)
            {
                if (hit == null) continue;
                GameObject go = hit.gameObject;
                if (go == null) continue;
                if (!string.Equals(go.tag, "Monster", System.StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(go.tag, "Enemy", System.StringComparison.OrdinalIgnoreCase)) continue;
                var dmg = go.GetComponentInParent<IDamageable>();
                if (dmg == null || !dmg.IsAlive) continue;
                float d = Vector3.Distance(origin, go.transform.position);
                if (d < bestDist) { bestDist = d; best = go; }
            }
            return best;
        }

        /// <summary>가장 가까운 밭(FarmPlot) 탐색 — "FarmPlot" 태그 또는 FarmingPlot 컴포넌트.</summary>
        private static GameObject FindNearestFarmPlot(Vector3 origin, float range)
        {
            Collider[] hits = Physics.OverlapSphere(origin, range);
            GameObject best = null;
            float bestDist = float.MaxValue;
            foreach (var hit in hits)
            {
                if (hit == null) continue;
                GameObject go = hit.gameObject;
                if (go == null) continue;
                if (!string.Equals(go.tag, "FarmPlot", System.StringComparison.OrdinalIgnoreCase)
                    && go.GetComponentInChildren<FarmingPlot>() == null) continue;
                float d = Vector3.Distance(origin, go.transform.position);
                if (d < bestDist) { bestDist = d; best = go; }
            }
            return best;
        }

        /// <summary>가장 가까운 채굴 가능한 자원 노드(ResourceNode: Wood/Stone/IronOre) 탐색.</summary>
        private static ResourceNode FindNearestResourceNode(Vector3 origin, float range)
        {
            Collider[] hits = Physics.OverlapSphere(origin, range);
            ResourceNode best = null;
            float bestDist = float.MaxValue;
            foreach (var hit in hits)
            {
                if (hit == null) continue;
                GameObject go = hit.gameObject;
                if (go == null) continue;
                var node = go.GetComponentInParent<ResourceNode>();
                if (node == null || !node.IsAvailable) continue;
                float d = Vector3.Distance(origin, go.transform.position);
                if (d < bestDist) { bestDist = d; best = node; }
            }
            return best;
        }
    }
}