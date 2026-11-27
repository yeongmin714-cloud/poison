using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// 영지 병사 역할/작업 배치 — 사냥/공격/수비/채집/농경 역할을 병사에게 부여하고 자동 수행한다.
    ///
    /// - Attack  : 플레이어를 추종(밖에서도 따라다님). SetCommandTarget(플레이어 위치, attack:false) 주기 갱신.
    /// - Defend  : 해당 영지 성문 앞 수비 위치에 주둔(hold). SetCommandTarget(성문 앞, attack:false).
    /// - Farm    : 가장 가까운 밭(FarmPlot) 파종/수확 — FarmingSystem.Plant/Harvest. (실시간 루프 유지)
    /// - Envoy   : 적 영지 잠입 첩보 — SpySystem.SendSpy (발각 시 처형).
    /// - Gather/Hunt/Mine : [122차 R1] 실시간 쿨다운 루프 폐기 → 하루 1회 가시 원정 사이클.
    ///   TimeManager.OnDayStart → StartDailyWorkCycle: ①집결(영지 성문 앞) ②대상 지점 보행
    ///   ③작업 연출(채집 채널 바 2s / 채굴 3s / 사냥 몬스터 2타) ④수확 집계(병사별) ⑤귀환(성문 앞 보행)
    ///   ⑥WarehouseSystem.AddItem(영지 키, 품목당 1회) 적립 ⑦완료 로그.
    ///   수확물은 플레이어 인벤이 아니라 소속 영지 저장고로 적립된다.
    ///   사냥 사망은 병사 1인당 하루 1회 판정(기존 HuntDeathChance 8% 재사용 — 기존 Die()/GuardManager 경로).
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

        // [Milestone E] 채널링(캐스팅) 상태 — 광물/채집 노드 근접 시 N초 진행(진행도 바) 후 1회 수확.
        //   [122차 R1] 실시간 루프 폐기 후 채널 연출은 일일 원정 사이클 워커가 재사용.
        private const float MineChannelSec = 3f;      // 광질 캐스팅 시간 — [122차 R1] 원정 작업 연출 3s(계약)
        private const float GatherChannelSec = 2.0f;  // 채집 캐스팅 시간 — [122차 R1] 원정 작업 연출 2s(계약)
        private readonly Dictionary<GuardPlaceholder, float> _channelStart = new Dictionary<GuardPlaceholder, float>();
        private readonly Dictionary<GuardPlaceholder, GameObject> _channelNode = new Dictionary<GuardPlaceholder, GameObject>();
        private readonly Dictionary<GuardPlaceholder, float> _channelDur = new Dictionary<GuardPlaceholder, float>();
        private readonly Dictionary<GuardPlaceholder, WorkProgressBar> _channelBar = new Dictionary<GuardPlaceholder, WorkProgressBar>();

        // 행동 쿨다운 (초)
        const float UpdateThrottleSec = 0.4f;   // 전역 업데이트 스로틀
        const float ActionCooldownSec = 4f;     // 기본 역할 행동 쿨다운
        const float FarmCooldownSec = 3f;       // 농사 쿨다운
        const float EnvoyCooldownSec = 30f;     // 특사(정보원) 첩보 쿨다운 — 장시간 유지 (스팸 방지)
        // [122차 R1] MineCooldownSec 폐기(광질 실시간 루프 제거) — 채광은 일일 원정 사이클로 이동.
        const float AttackSetTargetSec = 1f;    // 추종 위치 갱신 주기

        // 범위
        const float GatherRange = 15f;
        const float HuntRange = 20f;
        const float FarmRange = 12f;
        const float MineRange = 15f;            // 광질 노드 탐색 범위 — 채집(Gather)과 동일

        // 사냥 확률
        const float HuntDropChance = 0.6f;      // 60% 확률로 전리품(고기) 지급 — [122차 R1] 원정 1타당 판정
        const float HuntDeathChance = 0.08f;    // 8% 확률로 사냥 중 병사 사망 — [122차 R1] 병사 1인당 하루 1회 판정

        // ===== [122차 R1] 일일 원정 사이클 상수 =====
        const float CycleAssembleRadius = 3f;        // 집결/귀환 도착 판정 반경(성문 앞 3m 지점)
        const float CycleWorkArriveRadius = 2.5f;    // 작업 지점 도착 판정 반경(기존 근접 임계 동일)
        const float CycleAssembleTimeoutSec = 10f;   // 집결 대기 타임아웃
        const float CycleWalkTimeoutSec = 30f;       // 대상 지점 보행 타임아웃
        const float CycleReturnTimeoutSec = 30f;     // 귀환 보행 타임아웃
        const float CycleHuntAttackIntervalSec = 1f; // 사냥 공격 간격
        const int   CycleHuntAttacks = 2;            // 사냥 공격 횟수(1~2회 — 대상 소멸/사망 시 조기 종료)
        const int   DailyGatherBaseYield = 1;        // 기존 TryGather 시도당 약초 1개(실측) → 일일 총량 산식 기본량

        // ===== [122차 R1] 일일 원정 사이클 상태 =====
        Coroutine _cycleRoutine;      // 진행 중 사이클 루틴(null=유휴 — 멱등 가드/상태 조회 원천)
        bool _dayStartSubscribed;     // TimeManager.OnDayStart 구독 플래그(지연 구독 재시도용)

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

        // ===== [122차 R1] TimeManager.OnDayStart 구독 — GuardSalaryManager.OnDayStart 패턴 준용 =====

        private void OnEnable() => TrySubscribeDayStart();

        private void OnDisable()
        {
            if (_dayStartSubscribed && TimeManager.Instance != null)
                TimeManager.Instance.OnDayStart -= OnDayStartHandler;
            _dayStartSubscribed = false;
        }

        /// <summary>TimeManager.OnDayStart 구독(멱등). Instance 부재 시 무시 — Update에서 재시도.</summary>
        private void TrySubscribeDayStart()
        {
            if (_dayStartSubscribed) return;
            var tm = TimeManager.Instance;
            if (tm == null) return;
            tm.OnDayStart += OnDayStartHandler;
            _dayStartSubscribed = true;
        }

        /// <summary>TimeManager.OnDayStart 구독 핸들러 — 낮 시작(하루 경계)마다 일일 원정 사이클 개시.</summary>
        private void OnDayStartHandler()
        {
            try { StartDailyWorkCycle(); }
            catch (System.Exception e) { Debug.LogWarning($"{LogTag} 일일 원정 개시 실패: {e.Message}"); }
        }

        private void Update()
        {
            // [122차 R1] OnDayStart 지연 구독 — TimeManager가 본 시스템보다 늦게 생성되는 배선 커버
            //   (GuardSalaryManager.OnEnable은 즉시 구독 1회 시도 → 여기서 미구독 시 재시도 보완).
            if (!_dayStartSubscribed) TrySubscribeDayStart();

            // [42차] 실내에서는 역할 루틴 정지 — "실내 병사 = 전원 침대 휴식"(사용자 계약).
            //   Attack(플레이어 추종) 등 루틴이 실내에서 돌면 침대 병사가 따라다니는 버그(실측).
            //   역할 명령은 성 퇴출 시 AttackSquadDeployment가 월드에서 개시한다(41차 출동 배지).
            if (ProjectName.Core.UITransitionState.IndoorActive) return;

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
                    EndChannel(g);      // [Milestone E] 사망/비활성 시 캐스팅 취소
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
            EndChannel(g);              // [Milestone E] 작업 전환 시 캐스팅 취소
            _tasks[g] = task;
            _nextAction[g] = 0f;

            // [43차 사용자 요구 복원 — "병사가 침대에 누워있다가 배치가 되면 움직일 수 있게"]
            //   39차 기상+보행을 복원하되, 자동 역할 루틴(Update 루프)은 [42차] 실내 게이트를
            //   유지한다 — 배치 = 즉시 기상 + 실내 목적지 보행(수비=막사 앞, 나머지=로비 문 앞),
            //   이후 자동 루틴은 월드에서만 개시(퇴출 시 AttackSquadDeployment). 사용자가
            //   UI에서 의도한 배치 명령(IndoorMoveExempt)이라 P14 게이트와 공존한다.
            // [45차 사용자 요구 복원 — "병사가 침대에 누워있다가 배치가 되면 움직일 수 있게"]
            //   [46차 근원 수리 — "몇명은 이동하고 몇명은 안 함"] 이전 조건(g.IndoorBedIndex > 0)은
            //   침대 배치 실패 폴백 병사(bedIndex=-1)와 재배정 병사(1차 배정으로 기상 후 -1)에게
            //   명령을 안 보냈다 — 이동 안 함의 원인. 이제 실내+역할 배정이면 항상 명령 발행.
            //   [46차 겹침 수리 → 51차 확장] 슬롯을 배정 시점마다 계산하면 간격 불균일/한쪽 쏠림 →
            //   **RelayoutAssembly**: 실내 배정 병사 전원(역할 무관 — 51차)을 매 배정 후 전체 재배치
            //   (중앙 정렬·균일 간격 라인 — 4명/행, 5명부터 뒷행).
            if (ProjectName.Core.UITransitionState.IndoorActive)
            {
                if (g.IndoorBedIndex > 0)
                    TerritoryBuilder.WakeFromBed(g);           // 침대 기상 (강제자 제거+기립)
                RelayoutAssembly();                            // [51차] 역할 무관 전원 재배치(본인 포함 — 명령 발행)
                return;
            }
            Debug.Log($"{LogTag} {g.GuardName} → {task} (출동 예약 — 퇴출 시 월드에서 수행)");
        }

        /// <summary>[45차→51차] 집결 라인 재배치 — **역할 무관 전체 슬롯 풀**.
        ///   [51차 사용자 지적 — 실내 겹침 근원] 기존은 "같은 역할"끼리만 슬롯 계산 → 역할이 서로 다르면
        ///   전원이 로비 중앙 동일 좌표(각 역할 슬롯 rank 0)에 섰다. 수리: 실내 배정 병사 **전원**(역할 무관)을
        ///   하나의 결정론 슬롯 풀(이름순)로 재배치 — 목적지가 역할 무관 로비 중앙(48차 통일)이라 단일 풀이 정합.
        ///   레이아웃: 행당 최대 4명, x 간격 1.5m, 행 간격 2.2m(뒷행은 z −2.2), 행별 중앙 정렬.</summary>
        private void RelayoutAssembly()
        {
            var members = new List<GuardPlaceholder>();
            foreach (var kv in _tasks)
                if (kv.Key != null && kv.Value != GuardTask.None)
                    members.Add(kv.Key);
            if (members.Count == 0) return;
            members.Sort((a, b) => string.CompareOrdinal(a.GuardName, b.GuardName));   // 결정론 순번

            // 목적지는 역할 무관(48차 통일 — TryGetIndoorPost가 task를 참조하지 않음) → 대표 1회 조회
            if (!TerritoryBuilder.TryGetIndoorPost(members[0], GuardTask.Attack, out var post)) return;
            for (int i = 0; i < members.Count; i++)
            {
                int row = i / 4;
                int col = i % 4;
                int colsInRow = Mathf.Min(members.Count - row * 4, 4);
                float x = (col - (colsInRow - 1) * 0.5f) * 1.5f;
                float z = -row * 2.2f;
                var g = members[i];
                g.SetCommandTarget(post + new Vector3(x, 0f, z), false);
                g.IndoorMoveExempt = true;             // P14 게이트 예외 — 배치 UI 의도 이동
                Debug.Log($"{LogTag} {g.GuardName} 기상+집결 → 슬롯 {i + 1}/{members.Count} (x{x:+0.0;-0.0} z{z:+0.0;-0.0} — 간격 1.5m, 역할 무관 풀)");
            }
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
            var oldTask = _tasks.TryGetValue(g, out var t) ? t : GuardTask.None;
            _tasks.Remove(g);
            _nextAction.Remove(g);
            _taskTerritories.Remove(g);
            EndChannel(g);              // [Milestone E] 작업 해제 시 캐스팅 취소
            g.ClearCommand();
            // [46차] 해제 = 휴식 복귀 + 같은 역할 잔여 인원 라인 재배치(빈 슬롯 메우기 — 겹침/구멍 방지)
            if (ProjectName.Core.UITransitionState.IndoorActive)
            {
                Debug.Log($"{LogTag} {g.GuardName} 임무 해제 — 실내 침대 휴식 복귀(자가수리 스윕)");
                if (oldTask != GuardTask.None)
                    RelayoutAssembly();   // [51차] 역할 무관 전체 슬롯 풀 재배치(빈 슬롯 메우기)
            }
        }

        /// <summary>병사의 현재 배정 역할 (없으면 None).</summary>
        public GuardTask GetTask(GuardPlaceholder g)
        {
            if (g == null) return GuardTask.None;
            return _tasks.TryGetValue(g, out var t) ? t : GuardTask.None;
        }

        /// <summary>
        /// [툴팁 1차] 영지별 역할 배정 집계 — 월드맵 호버 툴팁/요약 UI용.
        /// GuardManager의 해당 영지 병사 전원을 기준으로 배정 역할별 인원을 집계한다.
        /// 배정 없는 병사는 None(휴식)으로 집계. 시스템 미초기화 시 빈 집계 반환
        /// (정보 없음 표기는 포맷터 TerritoryTooltipContent 몫).
        /// </summary>
        public Dictionary<GuardTask, int> GetTaskCountsByTerritory(TerritoryId id)
        {
            var counts = new Dictionary<GuardTask, int>();
            if (GuardManager.Instance == null) return counts;
            foreach (var guard in GuardManager.Instance.GetGuardsInTerritory(id))
            {
                if (guard == null) continue;
                var task = _tasks.TryGetValue(guard, out var t) ? t : GuardTask.None;
                counts.TryGetValue(task, out int n);
                counts[task] = n + 1;
            }
            return counts;
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
                case GuardTask.Farm:    RoutineFarm(guard);    break;
                case GuardTask.Envoy:   RoutineEnvoy(guard);   break;
                // [122차 R1] Gather/Hunt/Mine — 실시간 쿨다운 루프 폐기 →
                //   일일 원정 사이클(TimeManager.OnDayStart → StartDailyWorkCycle)로 교체. Attack/Defend/Farm/Envoy 무변경.
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

        /// <summary>Defend — 영지 수비. [41차] 수비 위치 = 성문 앞(바깥) → **영지 안(중앙 인측)** 으로
        ///   변경(사용자 요구: "영지 밖에서 돌아와서 영지 안으로") — 병사가 문을 통과해 안쪽에 주둔.</summary>
        private void RoutineDefend(GuardPlaceholder guard)
        {
            if (!CooldownReady(guard, ActionCooldownSec)) return;

            TerritoryId tid = ResolveGuardTerritory(guard);
            if (!tid.Equals(default) && TerritoryDatabase.Instance != null)
            {
                TerritoryDefinition def = TerritoryDatabase.Instance.GetDefinition(tid);
                Vector3 center = def.worldPosition != Vector3.zero ? def.worldPosition : Vector3.zero;
                Vector3 gateDir = ComputeGateDir(center);
                // 영지 안쪽 — 중심에서 문 반대 방향 2m (문을 들어온 안쪽 근무지)
                Vector3 insidePost = center - gateDir * 2f;
                guard.SetCommandTarget(insidePost, false);
            }
            else
            {
                // 영지 미식별 — 제자리 수비 (현재 위치 홀드)
                guard.SetCommandTarget(guard.transform.position, false);
            }
            guard.SetInCombat(false);
            CooldownStamp(guard, ActionCooldownSec);
        }

        /// <summary>성문 방향 — TerritoryDeploymentSystem.ComputeGateFront와 동일 수식(중심 기준 바깥 단위벡터).</summary>
        private static Vector3 ComputeGateDir(Vector3 center)
        {
            Vector3 c2 = new Vector3(center.x, 0, center.z);
            return c2.sqrMagnitude < 0.0001f ? Vector3.back : Vector3.Normalize(c2);
        }

        // ================================================================
        // [122차 R1] 일일 원정 사이클 — 채집/채광/사냥을 하루 1회 가시 원정으로 교체.
        //   흐름: TimeManager.OnDayStart → StartDailyWorkCycle →
        //   ①집결(영지 성문 앞) ②대상 지점 보행(SetCommandTarget 기존 파이프) ③작업 연출
        //   (채집=채널 바 2s, 채굴=3s, 사냥=몬스터 공격 1~2회) ④수확 집계(병사별)
        //   ⑤귀환(성문 앞 보행) ⑥WarehouseSystem.AddItem(영지 키, 품목당 1회) ⑦완료 로그.
        // ================================================================

        /// <summary>원정 작업 단위 — 병사 1인의 역할/소속 영지/대상/수확 집계(ItemData+count 목록).</summary>
        private sealed class ExpeditionJob
        {
            public GuardPlaceholder guard;
            public GuardTask task;
            public TerritoryId territory;
            public Vector3 homePos;                // 집결/귀환 지점 — 영지 성문 앞(바깥 3m)
            public GameObject node;                // 대상 — 약초 노드/자원 노드/몬스터
            public ResourceNode resourceNode;      // Mine 전용 — TryAutoMine/TryRollRareBonus 호출용
            public readonly List<KeyValuePair<PlayerInventory.ItemData, int>> yield =
                new List<KeyValuePair<PlayerInventory.ItemData, int>>();
            public bool died;                      // 사냥 중 사망 — 귀환 스킵(집계분 수확은 적립)
        }

        /// <summary>일일 원정 사이클 진행 중? (멱등 가드/테스트·진단 조회)</summary>
        public bool IsCycleRunning => _cycleRoutine != null;

        /// <summary>
        /// 일일 원정 사이클 개시 — TimeManager.OnDayStart에서 호출(테스트/수동 트리거 허용 public).
        /// 진행 중 재개시는 무시(멱등). 실내(IndoorActive) 중엔 개시하지 않는다
        /// ("실내 병사 = 침대 휴식" 계약 — 원정은 월드에서만 성립).
        /// </summary>
        public void StartDailyWorkCycle()
        {
            if (_cycleRoutine != null)
            {
                Debug.Log($"{LogTag} ☀️ 일일 원정 이미 진행 중 — 재개시 무시(멱등)");
                return;
            }
            if (ProjectName.Core.UITransitionState.IndoorActive)
            {
                Debug.Log($"{LogTag} ☀️ 실내 중 — 일일 원정 개시 스킵(월드 복귀 후 다음 낮 시작에 재시도)");
                return;
            }
            _cycleRoutine = StartCoroutine(RunDailyWorkCycleRoutine());
        }

        /// <summary>TimeManager.OnDayStart 연동 본체 — 역할별 그룹핑 → 집결 → 병행 원정 → 귀환 → 저장고 적립.</summary>
        private IEnumerator RunDailyWorkCycleRoutine()
        {
            // 편성(동기 — 예외 캡처로 코루틴 사망 방지; yield 없는 try/catch)
            List<ExpeditionJob> jobs;
            int nGather = 0, nHunt = 0, nMine = 0;   // [CS0165] try 예외 경로 대비 선언부 초기화
            try { jobs = BuildDailyJobs(out nGather, out nHunt, out nMine); }
            catch (System.Exception e)
            {
                jobs = null;
                Debug.LogWarning($"{LogTag} 원정 편성 실패: {e.Message}");
            }

            if (jobs == null || jobs.Count == 0)
            {
                Debug.Log($"{LogTag} ☀️ 일일 원정 개시 — 채집/채광/사냥 배정 병사 또는 대상 부재 → 사이클 스킵");
                _cycleRoutine = null;
                yield break;
            }
            Debug.Log($"{LogTag} ☀️ 일일 원정 개시 — 채집 {nGather} / 사냥 {nHunt} / 채광 {nMine} (총 {jobs.Count}명)");

            // ① 집결 — 각자 소속 영지 성문 앞(바깥 3m) 보행
            foreach (var job in jobs)
                if (job.guard != null) job.guard.SetCommandTarget(job.homePos, false);
            yield return WaitCycleArrivals(jobs, CycleAssembleTimeoutSec, CycleAssembleRadius, "집결");

            // ②~④ 병행 원정 — 대상 보행 → 작업 연출 → 수확 집계(병사별)
            var runners = new List<Coroutine>(jobs.Count);
            foreach (var job in jobs)
            {
                if (job.guard == null) continue;
                var worker = ExpeditionWorker(job);
                if (worker != null) runners.Add(StartCoroutine(worker));
            }
            foreach (var runner in runners)
                if (runner != null) yield return runner;

            // 워커 예외 사망 대비 — 진행 바 잔존 정리(멱등)
            foreach (var job in jobs)
                if (job.guard != null) EndChannel(job.guard);

            if (ProjectName.Core.UITransitionState.IndoorActive)
                Debug.Log($"{LogTag} 원정 중 실내 전환 감지 — 조기 종료(집계분만 적립)");

            // ⑤ 귀환 — 성문 앞 보행(사망자 제외)
            foreach (var job in jobs)
                if (job.guard != null && job.guard.IsAlive && !job.died)
                    job.guard.SetCommandTarget(job.homePos, false);

            // ⑥ 저장고 적립 — 영지별 품목 합산 → AddItem 품목당 1회(플레이어 인벤 경로 아님)
            int committedKinds = 0, committedCount = 0;
            try { CommitExpeditionYields(jobs, out committedKinds, out committedCount); }
            catch (System.Exception e) { Debug.LogWarning($"{LogTag} 저장고 적립 실패: {e.Message}"); }

            // 귀환 도착 대기(가시) — 타임아웃 내 도착/미도착 요약
            yield return WaitCycleArrivals(jobs, CycleReturnTimeoutSec, CycleAssembleRadius, "귀환");

            // ⑦ 완료 로그
            Debug.Log($"{LogTag} 📦 일일 원정 사이클 완료 — 원정 {jobs.Count}명 · 저장고 적립 {committedKinds}종 {committedCount}개");
            _cycleRoutine = null;
        }

        /// <summary>
        /// 역할별 그룹핑 + 대상 선정 — Gather/Hunt/Mine 배정 병사만 원정 편성.
        /// 소속 영지 키 도출 실패/대상 부재(기존 탐색 범위 내) 병사는 스킵+경고 로그(무너지지 않게).
        /// </summary>
        private List<ExpeditionJob> BuildDailyJobs(out int gatherN, out int huntN, out int mineN)
        {
            gatherN = huntN = mineN = 0;
            var jobs = new List<ExpeditionJob>();
            foreach (var kv in _tasks)
            {
                GuardPlaceholder g = kv.Key;
                GuardTask task = kv.Value;
                if (task != GuardTask.Gather && task != GuardTask.Hunt && task != GuardTask.Mine) continue;
                if (g == null || !g.IsAlive) continue;

                // 소속 영지 키 도출 — AssignTerritoryTask 기록 → GuardManager 전체 소유 영지 스캔 폴백(기존 경로)
                TerritoryId tid = ResolveGuardTerritory(g);
                if (tid.Equals(default))
                {
                    Debug.LogWarning($"{LogTag} {g.GuardName} 소속 영지 키 도출 실패 — 원정 스킵");
                    continue;
                }

                // 대상 선정 — 기존 탐지 헬퍼/범위 재사용(약초/ResourceNode/몬스터)
                GameObject node = null;
                ResourceNode rnode = null;
                switch (task)
                {
                    case GuardTask.Gather: node = FindNearestGatherNode(g.transform.position, GatherRange); break;
                    case GuardTask.Hunt:   node = FindNearestMonster(g.transform.position, HuntRange); break;
                    case GuardTask.Mine:
                        rnode = FindNearestResourceNode(g.transform.position, MineRange);
                        node = rnode != null ? rnode.gameObject : null;
                        break;
                }
                if (node == null)
                {
                    Debug.LogWarning($"{LogTag} {g.GuardName} {task} 대상 부재(사정거리 내) — 원정 스킵");
                    continue;
                }

                jobs.Add(new ExpeditionJob
                {
                    guard = g,
                    task = task,
                    territory = tid,
                    homePos = ComputeGateFrontPos(tid),
                    node = node,
                    resourceNode = rnode
                });
                if (task == GuardTask.Gather) gatherN++;
                else if (task == GuardTask.Hunt) huntN++;
                else mineN++;
            }
            return jobs;
        }

        /// <summary>역할별 원정 워커 디스패치.</summary>
        private IEnumerator ExpeditionWorker(ExpeditionJob job)
        {
            switch (job.task)
            {
                case GuardTask.Gather: return ExpeditionWorkerGather(job);
                case GuardTask.Hunt:   return ExpeditionWorkerHunt(job);
                case GuardTask.Mine:   return ExpeditionWorkerMine(job);
                default: return null;
            }
        }

        /// <summary>대상 지점 보행 — SetCommandTarget 기존 파이프(가시 보행). 도착/타임아웃/사망/대상 소멸/실내 전환 시 종료.</summary>
        private IEnumerator WalkToTarget(GuardPlaceholder g, GameObject target, bool attackCommand, float arriveRadius)
        {
            float deadline = Time.time + CycleWalkTimeoutSec;
            while (Time.time < deadline)
            {
                if (ProjectName.Core.UITransitionState.IndoorActive) yield break;   // 실내 전환 — 원정 중단
                if (g == null || !g.IsAlive) yield break;                          // 병사 사망 — 스킵
                if (target == null) yield break;                                   // 대상 소멸 — 스킵
                if (Vector3.Distance(g.transform.position, target.transform.position) <= arriveRadius)
                    yield break;                                                   // 도착
                g.SetCommandTarget(target.transform.position, attackCommand);      // 대상 이동 대응 재발행
                yield return null;
            }
        }

        /// <summary>채집 워커 — 접근 → 채널 바 2s 연출 → 일일 약초 수확량 집계(적립은 커밋 단계).</summary>
        private IEnumerator ExpeditionWorkerGather(ExpeditionJob job)
        {
            yield return null;   // 워커 격리 — 동기 전처리 예외가 메인 사이클로 전파되지 않도록 1프레임 유보
            var g = job.guard;
            if (g == null || !g.IsAlive || job.node == null) yield break;

            yield return WalkToTarget(g, job.node, false, CycleWorkArriveRadius);
            if (g == null || !g.IsAlive || job.node == null) yield break;
            if (Vector3.Distance(g.transform.position, job.node.transform.position) > CycleWorkArriveRadius)
            {
                Debug.LogWarning($"{LogTag} {g.GuardName} 🌿 채집 대상 미도착 — 스킵 ({job.node.name})");
                yield break;
            }
            if (!GatheringSystem.CanGather(job.node))
            {
                Debug.LogWarning($"{LogTag} {g.GuardName} 🌿 채집 대상 리스폰 중 — 스킵 ({job.node.name})");
                yield break;
            }

            // 작업 연출 — 기존 채널 바(2s, 녹색) 재사용
            BeginChannel(g, job.node, GatherChannelSec, new Color(0.4f, 0.8f, 0.4f));
            float deadline = Time.time + GatherChannelSec + 0.5f;
            while (Time.time < deadline && !ChannelElapsed(g))
            {
                if (ProjectName.Core.UITransitionState.IndoorActive || g == null || !g.IsAlive)
                {
                    if (g != null) EndChannel(g);
                    yield break;
                }
                yield return null;
            }
            if (g != null) EndChannel(g);
            if (g == null || !g.IsAlive || job.node == null) yield break;
            if (!GatheringSystem.CanGather(job.node))
            {
                Debug.LogWarning($"{LogTag} {g.GuardName} 🌿 채널 중 대상 선점(플레이어 채집) — 스킵 ({job.node.name})");
                yield break;
            }

            // 수확 집계 — 기존 시도당 약초 1개(실측: TryGather) → 일일 총량 = 기본량 × (1 + Lv×0.1)
            int n = ComputeDailyYieldForTest(GuardTask.Gather, g.Level, DailyGatherBaseYield);
            job.yield.Add(new KeyValuePair<PlayerInventory.ItemData, int>(PlayerInventory.Herb_Yakcho, n));
            Debug.Log($"{LogTag} {g.GuardName} 🌿 원정 채집 완료 — 약초 x{n} ({job.node.name}, Lv.{g.Level})");
        }

        /// <summary>채광 워커 — 접근 → 채널 바 3s 연출 → ResourceNode.TryAutoMine 실시행(고갈/리스폰·희귀 보너스 유지) → 일일 채굴량 집계.</summary>
        private IEnumerator ExpeditionWorkerMine(ExpeditionJob job)
        {
            yield return null;
            var g = job.guard;
            if (g == null || !g.IsAlive || job.node == null || job.resourceNode == null) yield break;

            yield return WalkToTarget(g, job.node, false, CycleWorkArriveRadius);
            if (g == null || !g.IsAlive || job.node == null) yield break;
            if (Vector3.Distance(g.transform.position, job.node.transform.position) > CycleWorkArriveRadius)
            {
                Debug.LogWarning($"{LogTag} {g.GuardName} ⛏️ 채굴 대상 미도착 — 스킵 ({job.node.name})");
                yield break;
            }
            var node = job.resourceNode;
            if (node == null || !node.IsAvailable)
            {
                Debug.LogWarning($"{LogTag} {g.GuardName} ⛏️ 채굴 대상 고갈/소멸 — 스킵");
                yield break;
            }

            // 작업 연출 — 기존 채널 바(3s, 주황) 재사용
            BeginChannel(g, job.node, MineChannelSec, new Color(0.85f, 0.6f, 0.3f));
            float deadline = Time.time + MineChannelSec + 0.5f;
            while (Time.time < deadline && !ChannelElapsed(g))
            {
                if (ProjectName.Core.UITransitionState.IndoorActive || g == null || !g.IsAlive)
                {
                    if (g != null) EndChannel(g);
                    yield break;
                }
                yield return null;
            }
            if (g != null) EndChannel(g);
            if (g == null || !g.IsAlive) yield break;

            // 채굴 실시행 — 기존 TryAutoMine 계열(노드 고갈/리스폰 진행, 지급처만 저장고로)
            node = job.resourceNode;
            if (node == null || !node.IsAvailable)
            {
                Debug.LogWarning($"{LogTag} {g.GuardName} ⛏️ 채널 중 대상 고갈/선점 — 스킵");
                yield break;
            }
            if (!node.TryAutoMine(out var item, out int baseYield))
            {
                Debug.LogWarning($"{LogTag} {g.GuardName} ⛏️ 채굴 실패 — 스킵 ({node.name})");
                yield break;
            }
            int n = ComputeDailyYieldForTest(GuardTask.Mine, g.Level, baseYield);
            if (item != null)
            {
                job.yield.Add(new KeyValuePair<PlayerInventory.ItemData, int>(item, n));
                Debug.Log($"{LogTag} {g.GuardName} ⛏️ 원정 채굴 완료 — {item.displayName} x{n} ({node.name}, 기본 {baseYield}, Lv.{g.Level})");
            }

            if (node.TryRollRareBonus(out var bonus, out int bonusYield) && bonus != null)
            {
                int bn = ComputeDailyYieldForTest(GuardTask.Mine, g.Level, bonusYield);
                job.yield.Add(new KeyValuePair<PlayerInventory.ItemData, int>(bonus, bn));
                Debug.Log($"{LogTag} {g.GuardName} 💎 희귀 광물 {bonus.displayName} x{bn}!");
            }
        }

        /// <summary>
        /// 사냥 워커 — 접근(공격 명령) → 몬스터 공격 1~2회(실제 TakeDamage) → 타당 확률 전리품 집계 →
        /// 사망 판정(병사 1인당 하루 1회 — 기존 HuntDeathChance, 기존 Die()/GuardManager 경로).
        /// </summary>
        private IEnumerator ExpeditionWorkerHunt(ExpeditionJob job)
        {
            yield return null;
            var g = job.guard;
            if (g == null || !g.IsAlive || job.node == null) yield break;

            yield return WalkToTarget(g, job.node, true, CycleWorkArriveRadius);
            if (g == null || !g.IsAlive) yield break;
            if (job.node == null)
            {
                Debug.LogWarning($"{LogTag} {g.GuardName} 🏹 사냥 대상 소멸 — 스킵");
                yield break;
            }
            if (Vector3.Distance(g.transform.position, job.node.transform.position) > CycleWorkArriveRadius)
            {
                Debug.LogWarning($"{LogTag} {g.GuardName} 🏹 사냥 대상 미도착 — 스킵 ({job.node.name})");
                yield break;
            }

            int atk = g.GetAttack();
            Vector3 dir = Vector3.forward;
            for (int i = 0; i < CycleHuntAttacks; i++)
            {
                if (ProjectName.Core.UITransitionState.IndoorActive) yield break;
                if (g == null || !g.IsAlive) yield break;
                if (job.node == null) break;                       // 대상 소멸 — 조기 종료
                var dmg = job.node.GetComponentInParent<IDamageable>();
                if (dmg == null || !dmg.IsAlive) break;
                dir = (job.node.transform.position - g.transform.position).normalized;
                if (dir == Vector3.zero) dir = Vector3.forward;
                dmg.TakeDamage(atk, dir, "melee");
                Debug.Log($"{LogTag} {g.GuardName} 🏹 원정 사냥 {i + 1}타 → {job.node.name} 데미지 {atk}");

                // 전리품 — 기존 시도당 확률(60%) → 레벨 보정 확률 판정, 고기는 커밋 단계에서 저장고로
                if (Random.value < ComputeDailyDropChanceForTest(g.Level, HuntDropChance))
                {
                    job.yield.Add(new KeyValuePair<PlayerInventory.ItemData, int>(PlayerInventory.RabbitMeat, 1));
                    Debug.Log($"{LogTag} {g.GuardName} 🎁 원정 전리품 — 토끼고기 x1 (누적 집계)");
                }

                if (i < CycleHuntAttacks - 1) yield return new WaitForSeconds(CycleHuntAttackIntervalSec);
            }

            // 사망 판정 — 사냥병사 1인당 하루 1회(기존 HuntDeathChance 재사용 — TakeDamage는 Die() 경로, GuardManager 제거)
            if (g != null && g.IsAlive && Random.value < HuntDeathChance)
            {
                Debug.Log($"{LogTag} {g.GuardName} 💀 원정 사냥 중 사망!(하루 1회 판정 — {HuntDeathChance:P0})");
                g.TakeDamage(99999f, dir, "melee");
                job.died = true;
            }
        }

        /// <summary>집결/귀환 병행 대기 — 전원 도착(반경 내) 또는 타임아웃. 사망/사망판정 병사는 즉시 통과.</summary>
        private static IEnumerator WaitCycleArrivals(List<ExpeditionJob> jobs, float timeoutSec, float arriveRadius, string phase)
        {
            float deadline = Time.time + timeoutSec;
            while (Time.time < deadline)
            {
                bool allArrived = true;
                foreach (var job in jobs)
                {
                    var g = job.guard;
                    if (g == null || !g.IsAlive || job.died) continue;
                    if (Vector3.Distance(g.transform.position, job.homePos) > arriveRadius)
                    {
                        allArrived = false;
                        break;
                    }
                }
                if (allArrived) yield break;
                yield return null;
            }
            int notArrived = 0;
            foreach (var job in jobs)
            {
                var g = job.guard;
                if (g == null || !g.IsAlive || job.died) continue;
                if (Vector3.Distance(g.transform.position, job.homePos) > arriveRadius) notArrived++;
            }
            if (notArrived > 0)
                Debug.LogWarning($"{LogTag} 원정 {phase} 미완료 {notArrived}명 — 타임아웃({timeoutSec:0}s) 후 다음 단계 진행");
        }

        /// <summary>
        /// 수확 적립 — 병사별 수확(job.yield)을 영지 키(territory.ToString() — "East_01" 형식) 기준으로 합산해
        /// WarehouseSystem.AddItem(territoryKey, item, count) 품목당 1회 일괄 적립.
        /// </summary>
        private void CommitExpeditionYields(List<ExpeditionJob> jobs, out int committedKinds, out int committedCount)
        {
            committedKinds = 0;
            committedCount = 0;

            var byTerritory = new Dictionary<string, Dictionary<string, KeyValuePair<PlayerInventory.ItemData, int>>>();
            foreach (var job in jobs)
            {
                if (job.yield.Count == 0) continue;
                string tKey = job.territory.ToString();
                if (!byTerritory.TryGetValue(tKey, out var items))
                    byTerritory[tKey] = items = new Dictionary<string, KeyValuePair<PlayerInventory.ItemData, int>>();
                foreach (var kv in job.yield)
                {
                    if (kv.Key == null || kv.Value <= 0) continue;
                    items.TryGetValue(kv.Key.id, out var cur);
                    items[kv.Key.id] = new KeyValuePair<PlayerInventory.ItemData, int>(kv.Key, cur.Value + kv.Value);
                }
            }
            if (byTerritory.Count == 0) return;

            var ws = WarehouseSystem.Instance != null ? WarehouseSystem.Instance : WarehouseSystem.GetOrCreate();
            if (ws == null)
            {
                Debug.LogWarning($"{LogTag} WarehouseSystem 부재 — 수확물 적립 스킵");
                return;
            }
            foreach (var tKv in byTerritory)
            {
                foreach (var iKv in tKv.Value)
                {
                    var item = iKv.Value.Key;
                    int count = iKv.Value.Value;
                    bool ok = ws.AddItem(tKv.Key, item, count);
                    committedKinds++;
                    committedCount += count;
                    Debug.Log($"{LogTag} 📦 {tKv.Key} 저장고 적립 — {item.displayName} x{count} {(ok ? "성공" : "실패(저장고 가득)")}");
                }
            }
        }

        /// <summary>영지 성문 앞(바깥 3m) — TerritoryDeploymentSystem.ComputeGateFront 동일 수식(중심 기준 바깥 방향).</summary>
        private static Vector3 ComputeGateFrontPos(TerritoryId tid)
        {
            Vector3 center = Vector3.zero;
            if (TerritoryDatabase.Instance != null)
            {
                TerritoryDefinition def = TerritoryDatabase.Instance.GetDefinition(tid);
                if (def.worldPosition != Vector3.zero) center = def.worldPosition;
            }
            return center + ComputeGateDir(center) * 3f;
        }

        /// <summary>
        /// [122차 R1 테스트 훅] 일일 수확량 순수 산식 — 기본량 × (1 + level×0.1), 반올림, 기본량이 양수면 최소 1 보장.
        /// 기본량 실측: 채집=1(GatheringSystem.TryGather 시도당 약초 1), 채굴=ResourceNode.TryAutoMine 랜덤 y.
        /// 사냥은 확률형이라 별도 산식(ComputeDailyDropChanceForTest) 사용.
        /// </summary>
        internal static int ComputeDailyYieldForTest(GuardTask task, int level, int baseYield)
        {
            if (baseYield <= 0) return 0;
            float boosted = baseYield * (1f + Mathf.Max(0, level) * 0.1f);
            return Mathf.Max(1, Mathf.RoundToInt(boosted));
        }

        /// <summary>[122차 R1 테스트 훅] 사냥 전리품 확률 산식 — 기본 확률 × (1 + level×0.1), 0~1 클램프.</summary>
        internal static float ComputeDailyDropChanceForTest(int level, float baseChance)
        {
            return Mathf.Clamp01(baseChance * (1f + Mathf.Max(0, level) * 0.1f));
        }

        // [122차 R1] RoutineMine/PerformMine 폐기 — 실시간 쿨다운 채굴 → 일일 원정 사이클
        //   (ExpeditionWorkerMine — 채널 연출 후 TryAutoMine 실시행, 지급처는 영지 저장고).

        /// <summary>[Milestone E] 채널 경과 완료 여부.</summary>
        private bool ChannelElapsed(GuardPlaceholder g)
        {
            if (!_channelStart.TryGetValue(g, out float s)) return false;
            float dur = _channelDur.TryGetValue(g, out float d) ? d : 2f;
            return Time.time - s >= dur;
        }

        /// <summary>[Milestone E] 캐스팅 시작 — 진행도 바 표시.</summary>
        private void BeginChannel(GuardPlaceholder g, GameObject node, float dur, Color color)
        {
            if (node == null) return;
            _channelStart[g] = Time.time;
            _channelNode[g] = node;
            _channelDur[g] = dur;
            if (_channelBar.TryGetValue(g, out var old) && old != null) old.Close();
            _channelBar[g] = WorkProgressBar.Show(g.transform, dur, color);
            // [Milestone F] 캐스팅 동안 채집/광질 Gather 애니 재생
            var rig = g.GetComponent<RigAnimationController>();
            if (rig != null) rig.SetState(AnimationState.Gather);
        }

        /// <summary>[Milestone E] 캐스팅 종료(완료/취소) — 진행도 바 정리.</summary>
        private void EndChannel(GuardPlaceholder g)
        {
            if (_channelStart.ContainsKey(g)) _channelStart.Remove(g);
            if (_channelNode.ContainsKey(g)) _channelNode.Remove(g);
            if (_channelDur.ContainsKey(g)) _channelDur.Remove(g);
            if (_channelBar.TryGetValue(g, out var bar) && bar != null) bar.Close();
            if (_channelBar.ContainsKey(g)) _channelBar.Remove(g);
            // [Milestone F] 캐스팅 종료 시 Idle 복귀
            var rig = g.GetComponent<RigAnimationController>();
            if (rig != null && rig.CurrentState == AnimationState.Gather) rig.SetState(AnimationState.Idle);
        }

        // [122차 R1] RoutineGather/PerformGather 폐기 — 실시간 쿨다운 채집 → 일일 원정 사이클
        //   (ExpeditionWorkerGather — 채널 연출 후 수확 집계. GatheringSystem.TryGather는
        //    PlayerInventory 직접 지급 경로라 호출하지 않고, 기본량 1 산식으로 집계해 저장고 적립.)

        /// <summary>
        /// Farm [26차 Phase 5 — 병사 완전 자동 농경]. 가장 가까운 밭(FarmPlot 컴포넌트 — FarmingManager 경유)을
        /// 찾아 전 과정을 스스로 수행: Grass/Dirt면 굉이질 → Tilled면 파종 → Planted/Growing이면 물 부족 시 물주기
        /// (병사는 물뿌리개/저장량 개념 없음 — 무제한) → Ready면 수확(부착된 HerbPickup 경로) → 반복.
        /// 구 FarmingSystem(FarmingPlot 캡슐 밭) 레거시 경로도 유지 병행 — 레거시 밭은 기존 로직 그대로.
        /// </summary>
        private void RoutineFarm(GuardPlaceholder guard)
        {
            if (!CooldownReady(guard, FarmCooldownSec)) return;

            // 신규 경로 — FarmPlot 컴포넌트 밭(농경 업그레이드 8상태)
            FarmPlot fp = FindNearestFarmPlotComponent(guard.transform.position, FarmRange);
            if (fp != null)
            {
                if (Vector3.Distance(guard.transform.position, fp.transform.position) > 2.5f)
                {
                    guard.SetCommandTarget(fp.transform.position, false);
                }
                else if (fp.IsOwned)
                {
                    switch (fp.Phase)
                    {
                        case FarmPlot.CropPhase.Grass:
                        case FarmPlot.CropPhase.Dirt:
                            fp.CompleteTill();
                            Debug.Log($"{LogTag} {guard.GuardName} ⛏️ 굉이질 완료 ({fp.name})");
                            break;

                        case FarmPlot.CropPhase.Tilled:
                            fp.CompletePlant();   // void — 씨앗 부족 시 내부에서 Tilled 유지(피드백 로그)
                            Debug.Log($"{LogTag} {guard.GuardName} 🌱 파종 시도 ({fp.name})");
                            break;

                        case FarmPlot.CropPhase.Planted:
                        case FarmPlot.CropPhase.Growing:
                            // 물 부족 판정은 FarmPlot 내부(리플렉션 폴링) 대신 단순 규칙: 쿨다운마다 물주기(1일 1회 이상 무해 — CompleteWater는 수분 100% 충전).
                            fp.CompleteWater();
                            Debug.Log($"{LogTag} {guard.GuardName} 💧 물주기 ({fp.name})");
                            break;

                        case FarmPlot.CropPhase.Ready:
                            fp.HarvestDirectly();
                            Debug.Log($"{LogTag} {guard.GuardName} 🌾 수확 완료 ({fp.name})");
                            break;

                        case FarmPlot.CropPhase.Withered:
                        case FarmPlot.CropPhase.Rotted:
                            fp.CompleteClear();
                            Debug.Log($"{LogTag} {guard.GuardName} 🧹 작물 정리 ({fp.name})");
                            break;
                    }
                }
                CooldownStamp(guard, FarmCooldownSec);
                return;
            }

            // 레거시 경로 — FarmingSystem(FarmingPlot 캡슐) 밭 기존 로직 그대로
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

        /// <summary>[26차 Phase 5] 가장 가까운 FarmPlot 컴포넌트 밭 탐색 (소유 밭만). 없으면 null.</summary>
        private static FarmPlot FindNearestFarmPlotComponent(Vector3 origin, float range)
        {
            var plots = Object.FindObjectsByType<FarmPlot>(FindObjectsSortMode.None);
            FarmPlot best = null;
            float bestDist = float.MaxValue;
            foreach (var p in plots)
            {
                if (p == null || !p.IsOwned) continue;
                float d = Vector3.Distance(origin, p.transform.position);
                if (d <= range && d < bestDist) { bestDist = d; best = p; }
            }
            return best;
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

        /// <summary>풀/약초 노드 판정 — HerbPickup 컴포넌트 또는 이름/태그에 herb/풀/grass/약초 포함.
        /// [무번호 73-E2] internal 노출 — FxEnvReaction 스윙 대상 판정식 동일 조건 재사용(중복 정의 금지 계약).
        /// 기존 호출부(FindNearestGatherNode) 시그니처·판정 리터럴 무손상.</summary>
        internal static bool IsGatherNode(GameObject go)
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