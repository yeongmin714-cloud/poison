using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [119차] 방랑자 드라이버 — 소속 상실 병사(방출/AI 감축/탈영)의 세계 배회·교전 구동.
    ///  · 배회: 랜덤 웨이포인트 40~90m 보행(SetCommandTarget(pos,false) — 기존 이동/애니/정체우회 파이프 재사용) + 도착 후 휴식 3~8s
    ///  · 첫 웨이포인트 = 원 소속 영지(ResolveTerritoryAt) 바깥 방향 — 방출 직후 원 영지 즉시 습격 방지
    ///  · 플레이어 조우 선공: 0.5s 스캔, 반경 10m → GuardHostilitySystem 3종 세트(HostileToPlayerFaction=true+SetCommandTarget(플레이어 위치,true)+SetInCombat(true))
    ///  · 영지 습격: 1s 스캔, ResolveTerritoryAt로 영지 판정 → TryCommandRaid(양측 명령 — WarMarchSimulation 선례)
    ///    수비 전멸 시 약탈: TerritoryState.territoryGold 10% 탈취(영지×게임일 1회 — ResetAll 가능) + AlarmSystem.TriggerAlert + CombatLog
    ///  · 정체 공격 명령 회수: IsAttackCommand && CurrentAttackTarget==null 4초 경과 → ClearCommand+SetInCombat(false)+배회 재개
    ///  · 결정론: 웨이포인트/휴식은 System.Random(seed=owner.GetInstanceID()) — VillagerWanderer 씨드 규약 준용
    ///  · null/예외 안전: TerritoryDatabase/GuardManager/TimeManager 부재 시 조용히 스킵(EditMode/헤드리스 방어)
    /// </summary>
    public sealed class WandererDriver : MonoBehaviour
    {
        // ===== [119차] 임계 상수 =====
        private const float WanderMinDist = 40f;              // 배회 웨이포인트 최소 거리(m)
        private const float WanderMaxDist = 90f;              // 배회 웨이포인트 최대 거리(m)
        private const float RestMinSeconds = 3f;              // 도착 후 휴식 최소(초)
        private const float RestMaxSeconds = 8f;              // 도착 후 휴식 최대(초)
        private const float PlayerScanInterval = 0.5f;        // 플레이어 조우 스캔 주기(초)
        private const float PlayerAggroRadius = 10f;          // 플레이어 조우 선공 반경(m)
        private const float RaidScanInterval = 1f;            // 영지 습격 스캔 주기(초)
        private const float StaleAttackTimeoutSeconds = 4f;   // 공격 타겟 상실 명령 회수 대기(초)
        private const float RaidLootRatio = 0.10f;            // 약탈 탈취 비율(영지 금고 10%)
        private const float WorldHalfSpan = 1500f;            // 웨이포인트 xz 클램프(WORLD_SPAN 3200 절반 근사)
        private const float FirstWaypointDist = 60f;          // 첫 웨이포인트 거리 — 원 영지 바깥 방향(m)

        private GuardPlaceholder _owner;
        private System.Random _rng = new System.Random(0);    // Begin에서 owner.GetInstanceID() 시드로 재생성
        private GameObject _playerCache;
        private bool _playerEngaged;
        private float _playerScanTimer;
        private float _raidScanTimer;
        private float _restTimer;
        private float _staleTimer;
        private string _lastRaidTerritoryKey;                 // 습격 개시 로그 영지별 1회 스로틀
        private readonly List<GuardPlaceholder> _raidDefenders = new List<GuardPlaceholder>();
        private static readonly Dictionary<string, int> _lastLooted = new Dictionary<string, int>();   // 영지 → 마지막 약탈 게임일

        // ===== 구동 개시 =====

        /// <summary>[119차] 구동 개시 — 첫 웨이포인트 = 원 소속 영지 바깥 방향 60m(영지 판정 실패 시 랜덤 방향).</summary>
        public void Begin(GuardPlaceholder owner, string reason)
        {
            _owner = owner;
            _rng = new System.Random(owner != null ? owner.GetInstanceID() : 0);
            _playerCache = null;
            _playerEngaged = false;
            _playerScanTimer = 0f;
            _raidScanTimer = 0f;
            _staleTimer = 0f;
            _restTimer = RandomRestSeconds();
            if (owner == null) return;

            Vector3 origin = owner.transform.position;
            TerritoryId? homeTerritory = ResolveTerritorySafe(origin);
            Vector3 waypoint = ClampToWorld(origin + RandomDirectionXZ() * FirstWaypointDist);
            if (homeTerritory.HasValue)
            {
                Vector3 outward = ResolveOutwardDirection(origin, homeTerritory.Value);
                if (outward.sqrMagnitude > 0.0001f)
                    waypoint = ClampToWorld(origin + outward * FirstWaypointDist);
            }
            waypoint = WithGroundY(waypoint, origin.y);
            owner.SetCommandTarget(waypoint, false);
            Debug.Log($"[Wanderer] {owner.GuardName} 배회 개시 — {reason}");
        }

        // ===== Update 파이프라인 =====

        private void Update()
        {
            // 1) 소유자 상실/사망 — 드라이버 자가 제거(Play=Destroy / Edit=DestroyImmediate)
            if (_owner == null || _owner.IsDead)
            {
                if (Application.isPlaying) Destroy(this); else DestroyImmediate(this);
                return;
            }
            float dt = Time.deltaTime;

            // 5) 정체 공격 명령 회수 — 공격 타겟 상실 4초 경과 → 명령 정리 + 배회 재개
            if (_owner.IsAttackCommand && _owner.CurrentAttackTarget == null)
            {
                _staleTimer += dt;
                if (_staleTimer >= StaleAttackTimeoutSeconds)
                {
                    _staleTimer = 0f;
                    ReleaseRaidDefenders();
                    _owner.ClearCommand();
                    _owner.SetInCombat(false);
                    _playerEngaged = false;
                    Debug.Log($"[Wanderer] {_owner.GuardName} 교전 종료 — 배회 재개");
                }
            }
            else if (_owner.CurrentAttackTarget != null)
            {
                _staleTimer = 0f;   // 유효 타겟 유지 중 — 회수 타이머 리셋
            }

            // 2) 배회 틱 — 명령 해소(도착/회수) 후 휴식 경과 시 다음 웨이포인트
            if (!_owner.HasCommand)
            {
                if (_restTimer > 0f) _restTimer -= dt;
                if (_restTimer <= 0f) IssueWanderWaypoint();
                if (_playerEngaged) _playerEngaged = false;   // 플레이어 이탈로 명령 해소 → 조우 재무장(배회 재개와 함께)
            }

            // 3) 플레이어 조우 스캔 (0.5s throttle)
            _playerScanTimer -= dt;
            if (_playerScanTimer <= 0f)
            {
                _playerScanTimer = PlayerScanInterval;
                TryScanPlayer();
            }

            // 4) 영지 습격 스캔 (1s throttle)
            _raidScanTimer -= dt;
            if (_raidScanTimer <= 0f)
            {
                _raidScanTimer = RaidScanInterval;
                TryScanRaid();
            }
        }

        private void OnDestroy()
        {
            ReleaseRaidDefenders();
        }

        // ===== 영지 습격(공용 진입점) =====

        /// <summary>
        /// [119차] 영지 습격 명령 — 방랑자↔수비병 양측 공격 명령 발행(WarMarchSimulation 선례).
        /// GuardManager 부재/수비 없음 시 예외 없이 false. 수비 후보: 생존 + 비포섭 + 비방랑자.
        /// 발행된 수비 목록은 방랑자 드라이버의 _raidDefenders에 캐시(파괴/교전 종료 시 정리).
        /// </summary>
        public static bool TryCommandRaid(GuardPlaceholder wanderer, TerritoryId territoryId)
        {
            GuardManager manager = GuardManager.Instance;
            if (wanderer == null || !wanderer.IsAlive || manager == null) return false;

            List<GuardPlaceholder> defenders = manager.GetGuardsInTerritory(territoryId);
            List<GuardPlaceholder> targets = new List<GuardPlaceholder>();
            if (defenders != null)
            {
                foreach (GuardPlaceholder defender in defenders)
                {
                    if (defender == null || defender == wanderer) continue;
                    if (!defender.IsAlive || defender.IsRecruited || defender.IsWanderer) continue;
                    targets.Add(defender);
                }
            }
            if (targets.Count == 0) return false;

            // 방랑자 → 최근접 수비병 위치로 공격 명령(GuardHostilitySystem 3종 세트)
            Vector3 origin = wanderer.transform.position;
            GuardPlaceholder nearest = targets[0];
            float nearestDist = float.MaxValue;
            foreach (GuardPlaceholder defender in targets)
            {
                float dist = Vector3.Distance(defender.transform.position, origin);
                if (dist < nearestDist) { nearestDist = dist; nearest = defender; }
            }
            wanderer.HostileToPlayerFaction = true;
            wanderer.SetCommandTarget(nearest.transform.position, true);
            wanderer.SetInCombat(true);

            // 수비병 → 방랑자 위치로 반격 명령(양측 교전 성립)
            Vector3 wandererPos = wanderer.transform.position;
            foreach (GuardPlaceholder defender in targets)
            {
                defender.SetCommandTarget(wandererPos, true);
                defender.SetInCombat(true);
            }

            // 습격 기록 캐시 — 드라이버 파괴/교전 종료 시 정리용(정적 경로 → 방랑자 부착 드라이버에 위탁)
            WandererDriver driver = wanderer.GetComponent<WandererDriver>();
            if (driver != null)
            {
                driver._raidDefenders.Clear();
                driver._raidDefenders.AddRange(targets);
            }

            Debug.Log($"[Wanderer-Raid] ⚔️ {wanderer.GuardName} → {territoryId} 습격 — 수비 {targets.Count}명 교전 명령");
            return true;
        }

        /// <summary>[119차] static 상태 초기화 — 약탈 기록(테스트 격리용).</summary>
        public static void ResetAll()
        {
            _lastLooted.Clear();
        }

        // ===== 스캔 내부 =====

        private void TryScanPlayer()
        {
            if (_owner == null || _owner.IsDead || _playerEngaged) return;

            // "Player" 태그 캐시 — 파괴 시 재탐색(태그 미정의 등 예외는 조용히 스킵)
            if (_playerCache == null)
            {
                try { _playerCache = GameObject.FindGameObjectWithTag("Player"); }
                catch (System.Exception) { _playerCache = null; }
            }
            if (_playerCache == null) return;

            if (Vector3.Distance(_playerCache.transform.position, _owner.transform.position) > PlayerAggroRadius) return;

            // 플레이어 조우 선공 — GuardHostilitySystem 3종 세트
            _owner.HostileToPlayerFaction = true;
            _owner.SetCommandTarget(_playerCache.transform.position, true);
            _owner.SetInCombat(true);
            _playerEngaged = true;
            Debug.Log($"[Wanderer] 🗡️ {_owner.GuardName} 플레이어 조우 선공 — ({_playerCache.transform.position.x:0.0},{_playerCache.transform.position.z:0.0})");
        }

        private void TryScanRaid()
        {
            if (_owner == null || _owner.IsDead) return;
            if (_owner.IsAttackCommand) return;   // 교전 중 — 습격 스캔 스킵(양측 명령 도배 방지)

            TerritoryId? territory = ResolveTerritorySafe(_owner.transform.position);
            if (!territory.HasValue) return;

            if (WandererDriver.TryCommandRaid(_owner, territory.Value))
            {
                string key = territory.Value.ToString();
                if (!string.Equals(_lastRaidTerritoryKey, key, System.StringComparison.Ordinal))
                {
                    _lastRaidTerritoryKey = key;
                    Debug.Log($"[Wanderer] ⚔️ {_owner.GuardName} 영지 습격 개시 — {key}");
                }
            }
            else
            {
                TryLoot(territory.Value);   // 수비 전멸/부재 → 약탈(영지×게임일 1회)
            }
        }

        /// <summary>[119차] 약탈 — 수비 없는 영지 금고 10% 탈취(영지×게임일 1회, ResetAll로 초기화).</summary>
        private void TryLoot(TerritoryId territoryId)
        {
            if (_owner == null || _owner.IsDead) return;
            TerritoryDatabase db = TerritoryDatabase.Instance;
            if (db == null) return;

            TerritoryState state;
            try { state = db.GetState(territoryId); }
            catch (System.Exception) { return; }
            if (state == null || state.territoryGold <= 0) return;

            int day = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 0;
            string key = territoryId.ToString();
            if (_lastLooted.TryGetValue(key, out int lootedDay) && lootedDay == day) return;   // 영지×게임일 1회 — 멱등

            int steal = Mathf.Clamp(Mathf.RoundToInt(state.territoryGold * RaidLootRatio), 0, state.territoryGold);
            if (steal <= 0) return;

            state.territoryGold -= steal;
            _lastLooted[key] = day;
            AlarmSystem.TriggerAlert(key, _owner.transform.position);
            CombatLog.AddEntry($"[Wanderer-Raid] 💰 약탈 — {key} 금고 {steal}G 탈취(잔여 {state.territoryGold}G)", LogType.Kill);
            Debug.Log($"[Wanderer-Raid] 💰 약탈 — {key} 금고 {steal}G 탈취(잔여 {state.territoryGold}G) + 경보");
        }

        // ===== 배회/정리 내부 =====

        /// <summary>배회 웨이포인트 발행 — 랜덤 방향 40~90m, xz 월드 클램프, 지면 Y 스냅. 발행 후 휴식 타이머 재충전.</summary>
        private void IssueWanderWaypoint()
        {
            if (_owner == null) return;
            Vector3 origin = _owner.transform.position;
            float dist = WanderMinDist + (float)(_rng != null ? _rng.NextDouble() : 0.5) * (WanderMaxDist - WanderMinDist);
            Vector3 waypoint = WithGroundY(ClampToWorld(origin + RandomDirectionXZ() * dist), origin.y);
            _owner.SetCommandTarget(waypoint, false);
            _restTimer = RandomRestSeconds();
        }

        /// <summary>습격 중 명령받은 수비병 정리 — 살아있는 비포섭 수비만 명령 해제(예외 무시).</summary>
        private void ReleaseRaidDefenders()
        {
            foreach (GuardPlaceholder defender in _raidDefenders)
            {
                if (defender == null) continue;
                try
                {
                    if (!defender.IsAlive || defender.IsRecruited) continue;
                    defender.ClearCommand();
                    defender.SetInCombat(false);
                }
                catch (System.Exception) { /* 정리 실패 무시 — 파괴 경로 방해 금지 */ }
            }
            _raidDefenders.Clear();
        }

        private float RandomRestSeconds()
        {
            return RestMinSeconds + (float)(_rng != null ? _rng.NextDouble() : 0.5) * (RestMaxSeconds - RestMinSeconds);
        }

        private Vector3 RandomDirectionXZ()
        {
            double angle = (_rng != null ? _rng.NextDouble() : 0.5) * 2.0 * System.Math.PI;
            return new Vector3((float)System.Math.Sin(angle), 0f, (float)System.Math.Cos(angle));
        }

        private static Vector3 ClampToWorld(Vector3 pos)
        {
            pos.x = Mathf.Clamp(pos.x, -WorldHalfSpan, WorldHalfSpan);
            pos.z = Mathf.Clamp(pos.z, -WorldHalfSpan, WorldHalfSpan);
            return pos;
        }

        /// <summary>지면 Y 근사 — TerrainGenerator 높이 + 1f, 실패 시 현재 Y 폴백(EditMode/헤드리스 안전).</summary>
        private static Vector3 WithGroundY(Vector3 pos, float fallbackY)
        {
            try
            {
                pos.y = 1f + TerrainGenerator.GetHeightAt(pos.x, pos.z, BiomeType.Plains, 42);
            }
            catch (System.Exception)
            {
                pos.y = fallbackY;
            }
            return pos;
        }

        private static TerritoryId? ResolveTerritorySafe(Vector3 pos)
        {
            TerritoryDatabase db = TerritoryDatabase.Instance;
            if (db == null) return null;
            try { return db.ResolveTerritoryAt(pos); }
            catch (System.Exception ex) { Debug.LogWarning($"[Wanderer] 영지 판정 실패 — {ex.Message}"); return null; }
        }

        private static Vector3 ResolveOutwardDirection(Vector3 origin, TerritoryId territoryId)
        {
            TerritoryDatabase db = TerritoryDatabase.Instance;
            if (db == null) return Vector3.zero;
            try
            {
                Vector3 center = db.GetWorldPosition(territoryId.nation, territoryId.index);
                Vector3 outward = origin - center;
                outward.y = 0f;
                return outward.normalized;
            }
            catch (System.Exception ex) { Debug.LogWarning($"[Wanderer] 영지 중심 조회 실패 — {ex.Message}"); return Vector3.zero; }
        }
    }
}
