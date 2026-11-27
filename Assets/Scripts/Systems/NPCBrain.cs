using System.Collections.Generic;
using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 2026-10-15: NPC 자율 판단 브레인 (로컬 유틸리티 AI — clarify ① 선택, 무응답 10분 경과로
    /// 60차 선례 "기존검증 최소설계 진행" 적용).
    ///
    /// [문제] 기존 NPC 시스템이 각자 사일로로 반응: Awareness(경계 감지만)/WeatherBehavior(은신=텔레포트)/
    /// DailyCycle(밤 SetActive off)/AmbientDialogue(대사 버블) — 전체를 읽고 우선순위를 매기는 판단층이 없었고,
    /// NPC는 걸어가는 이동 자체가 없었다.
    ///
    /// [설계] NPCBrain = 유일한 판단층. 기존 시스템의 출력을 "신호"로 소비(대체 아님, 중복 충돌 방지):
    ///  - 시간: NPCDailyCycle.CurrentPeriod (브레인은 SetActive를 절대 건드리지 않음 — DailyCycle 소유)
    ///  - 날씨: WeatherManager.CurrentWeather (Rain/Snow/StrongWind → 은신)
    ///  - 위협: MonsterAggroSystem.AllMonsters(IsInCombat) 반경 + 같은 GO Awareness Detected 상태
    ///  - 플레이어: 근접 → 인사(바라보기)
    ///  - 역할: 상점 NPC(Day)=근무 자리 유지 / 일반(Day)=주변 배회
    /// 우선순위: 도피 > 은신 > 귀가 > 인사 > 근무 > 배회 > 대기
    ///
    /// [이동] NPCSteeringMover — NavMeshAgent 불필요(주민은 agent 없음): 직선 스티어링 + 지면 레이캐스트 스냅
    /// (LootBasket.Create 선례) + 막힘 감지(진전 없으면 의도 포기). HumanoidClipDriver(Soldier)는
    /// transform 위치 델타로 Speed를 자동 계산하므로 이동만 하면 걷기 애니가 따라온다(드라이버 직접 조작 없음).
    ///
    /// [부착] EnsureOn(go) — 멱등. 병사/게이트가드/스켈레톤/몬스터/플레이어는 제외 가드.
    /// TerritoryNPCSpawner.SpawnNPC(신규 주민) + NPCDailyCycle.FindAllNPCs(씬 내 기존 NPC)에서 호출.
    /// </summary>
    public class NPCBrain : MonoBehaviour
    {
        public enum Intent
        {
            Idle,        // 대기 — 기본
            Wander,      // 주변 배회 (Day 일반 NPC)
            Work,        // 근무 자리 유지 (Day 상점 NPC)
            GreetPlayer, // 플레이어 인사 — 바라보기
            WatchCombat, // 위협 감시 — 바라보기
            SeekShelter, // 은신처 이동 (악천후)
            GoHome,      // 귀가 (Evening)
            FleeThreat,  // 위협 도피 (최우선)
        }

        // ===== 튜닝 상수 =====
        private const float TickInterval = 0.5f;
        private const float ThreatRadius = 14f;
        private const float WanderRadius = 6f;
        private const float GreetRadius = 4f;
        private const float GreetCooldown = 8f;
        private const float ArriveDistance = 0.35f;
        private const float WalkSpeed = 1.4f;
        private const float FleeSpeed = 3.2f;
        private const float WanderDwellMin = 4f;
        private const float WanderDwellMax = 10f;
        private const float StuckDetectSeconds = 1.5f;

        /// <summary>은신처 태그 — NPCWeatherBehavior 기본값과 동일(단일 소스는 그쪽, 여기선 스캔용 사본).</summary>
        private static readonly string[] ShelterTags = { "Building", "House", "Shelter", "Shop", "Interior" };

        // ===== 신호 소스 (모두 null 허용 — 부재 시 해당 신호 스킵) =====
        private NPCAwarenessSystem _awareness;

        // ===== 상태 =====
        private Vector3 _home;
        private bool _homeCaptured; // 최초 1회만 캡처 — 밤 비활성→재활성 시 이동 중 위치가 홈을 오염하는 것 방지
        private float _tickTimer;
        private Intent _current = Intent.Idle;
        private Vector3 _moveTarget;
        private float _moveSpeed = WalkSpeed;
        private bool _hasMoveTarget;
        private Vector3 _faceTarget;
        private bool _hasFaceTarget; // GreetPlayer/WatchCombat — 이동 없이 바라보기
        private float _wanderDwellUntil;
        private float _lastGreetTime = -999f;
        private float _externalMoveUntil = -999f; // 외부(WeatherBehavior) 이동 위임 우선권
        private Vector3 _stuckCheckPos;
        private float _stuckCheckTime;
        private bool _fleeLogged;
        private bool _shelterLogged;

        public Intent CurrentIntent => _current;

        // ================================================================
        // 부착 (멱등 — 스포너/데일리싸이클 양쪽에서 호출 가능)
        // ================================================================

        /// <summary>NPC에 브레인이 없으면 부착. 병사/게이트가드/스켈레톤/몬스터/플레이어는 제외(전투 AI 별도 보유).</summary>
        public static void EnsureOn(GameObject go)
        {
            if (go == null) return;
            if (go.GetComponent<NPCBrain>() != null) return;

            // 제외 가드 — 전투 AI가 이미 있는 계열(병사/몬스터/보스)과 플레이어
            if (go.GetComponent<GuardPlaceholder>() != null) return;
            if (go.GetComponent<GateGuardPlaceholder>() != null) return;
            if (go.GetComponent<SkeletonGuardPlaceholder>() != null) return;
            if (go.GetComponent<AnimalAI>() != null) return;
            if (go.GetComponent<DraculaLord>() != null) return;
            if (go.CompareTag("Player")) return;

            var brain = go.AddComponent<NPCBrain>();
            Debug.Log($"[NPCBrain] 부착: {go.name}");
        }

        private void OnEnable()
        {
            if (!_homeCaptured) { _home = transform.position; _homeCaptured = true; }
            _tickTimer = Random.value * TickInterval; // 틱 스태거 — 전 NPC가 같은 프레임에 몰리지 않게
            _awareness = GetComponent<NPCAwarenessSystem>();
            _hasMoveTarget = false;
            _hasFaceTarget = false;
            _current = Intent.Idle;
        }

        // ================================================================
        // 판단 (틱) + 실행 (매 프레임) — 업데이트 분리
        // ================================================================

        private void Update()
        {
            // ① 판단 틱 — 0.5s 간격
            _tickTimer += Time.deltaTime;
            if (_tickTimer >= TickInterval)
            {
                _tickTimer = 0f;
                Decide();
            }

            // ② 이동 실행 — 매 프레임
            ExecuteMovement();
        }

        /// <summary>신호 수집 → 유틸리티 우선순위 판단 → 의도 전환.</summary>
        private void Decide()
        {
            // 외부 이동 위임(은신 등)이 유효한 동안 판단 억제 — 단일 이동 소유자 보장
            if (Time.time < _externalMoveUntil) return;

            var pos = transform.position;

            // ── 신호 수집 ──
            bool detected = _awareness != null && _awareness.CurrentAwarenessState == NPCAwarenessSystem.AwarenessState.Detected;
            Vector3 threatPos;
            bool threatNear = TryFindNearbyCombatThreat(pos, ThreatRadius, out threatPos);

            bool badWeather = false;
            var wm = WeatherManager.Instance;
            if (wm != null)
            {
                var w = wm.CurrentWeather;
                badWeather = w == WeatherManager.WeatherType.Rain
                          || w == WeatherManager.WeatherType.Snow
                          || w == WeatherManager.WeatherType.StrongWind;
            }

            bool evening = NPCDailyCycle.Instance != null
                        && NPCDailyCycle.Instance.CurrentPeriod == NPCDailyCycle.TimePeriod.Evening;

            bool isShop = IsShopNPC();

            Transform playerT = null;
            var playerGo = GameObject.FindWithTag("Player");
            if (playerGo != null) playerT = playerGo.transform;
            bool playerNear = playerT != null
                           && (playerT.position - pos).sqrMagnitude <= GreetRadius * GreetRadius
                           && Time.time - _lastGreetTime >= GreetCooldown;

            // ── 우선순위 판단 (도피 > 은신 > 귀가 > 인사 > 근무/배회) ──
            Intent next;
            Vector3 moveTarget = Vector3.zero;
            bool hasMove = false;
            float speed = WalkSpeed;

            if (detected || threatNear)
            {
                next = Intent.FleeThreat;
                // 위협 반대편 + 홈 방향 블렌딩 — 맵 밖으로 달려나가지 않게 홈 쪽으로 기울인다
                Vector3 away = (pos - threatPos).normalized;
                if (float.IsNaN(away.x)) away = Vector3.back;
                Vector3 toHome = (_home - pos).normalized;
                moveTarget = pos + (away * 0.6f + toHome * 0.4f).normalized * 6f;
                hasMove = true;
                speed = FleeSpeed;
                _hasFaceTarget = false;
                if (!_fleeLogged) { _fleeLogged = true; Debug.Log($"[NPCBrain] {name}: 위협 도피 시작"); }
            }
            else
            {
                _fleeLogged = false;
                if (badWeather)
                {
                    next = Intent.SeekShelter;
                    _hasFaceTarget = false;
                    Transform shelter = FindNearestShelter(pos);
                    if (shelter != null)
                    {
                        moveTarget = shelter.position + shelter.forward * 2f;
                        hasMove = true;
                        if (!_shelterLogged) { _shelterLogged = true; Debug.Log($"[NPCBrain] {name}: 은신 이동 ({shelter.name})"); }
                    }
                }
                else
                {
                    _shelterLogged = false;

                    // 경계 신호 — Awareness 의심/수색 중이면 위협 방향 감시(바라보기)
                    bool suspicious = _awareness != null
                        && (_awareness.CurrentAwarenessState == NPCAwarenessSystem.AwarenessState.Suspicious
                         || _awareness.CurrentAwarenessState == NPCAwarenessSystem.AwarenessState.Searching);

                    if (evening)
                    {
                        next = Intent.GoHome;
                        _hasFaceTarget = false;
                        moveTarget = _home;
                        hasMove = (transform.position - _home).sqrMagnitude > ArriveDistance * ArriveDistance;
                    }
                    else if (suspicious)
                    {
                        next = Intent.WatchCombat;
                        _faceTarget = _awareness.SuspicionOrigin;
                        _hasFaceTarget = true;
                        hasMove = false;
                    }
                    else if (playerNear && playerT != null)
                    {
                        next = Intent.GreetPlayer;
                        _faceTarget = playerT.position;
                        _hasFaceTarget = true;
                        _lastGreetTime = Time.time;
                        // 이동 없음 — 바라보기만
                    }
                    else if (isShop)
                    {
                        next = Intent.Work;
                        _hasFaceTarget = false;
                        hasMove = false; // 근무 자리 유지 — 이미 자리에 있음
                    }
                    else if (Time.time >= _wanderDwellUntil)
                    {
                        next = Intent.Wander;
                        _hasFaceTarget = false;
                        Vector2 r = Random.insideUnitCircle * WanderRadius;
                        moveTarget = _home + new Vector3(r.x, 0f, r.y);
                        _wanderDwellUntil = Time.time + Random.Range(WanderDwellMin, WanderDwellMax);
                        hasMove = true;
                    }
                    else
                    {
                        next = Intent.Wander; // 배회 도착 후 dwel — 이동 없음(목표 유지)
                        moveTarget = _hasMoveTarget ? _moveTarget : _home;
                        hasMove = (transform.position - moveTarget).sqrMagnitude > ArriveDistance * ArriveDistance;
                        if (!hasMove) _hasFaceTarget = false; // 도착 + 대기 — 감시/인사 외 바라보기 해제
                    }
                }
            }

            // 의도 전환 적용 — 이동 중 의도 변경 시 목표 갱신
            bool intentChanged = next != _current;
            _current = next;
            if (hasMove)
            {
                _moveTarget = moveTarget;
                _moveSpeed = speed;
                _hasMoveTarget = true;
                _stuckCheckPos = pos;
                _stuckCheckTime = Time.time;
            }
            else if (intentChanged)
            {
                _hasMoveTarget = false;
            }
        }

        // ================================================================
        // 이동 실행 — 스티어링 + 지면 스냅 + 막힘 감지
        // ================================================================

        private void ExecuteMovement()
        {
            if (!_hasMoveTarget)
            {
                // 이동 없는 의도 — 인사/감시 대상 바라보기 (부드럽게)
                if (_hasFaceTarget)
                {
                    Vector3 faceDir = _faceTarget - transform.position;
                    faceDir.y = 0f;
                    if (faceDir.sqrMagnitude > 0.01f)
                    {
                        Quaternion target = Quaternion.LookRotation(faceDir.normalized);
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, 120f * Time.deltaTime);
                    }
                }
                return;
            }

            var pos = transform.position;
            Vector3 toTarget = _moveTarget - pos;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;

            // 도착
            if (dist <= ArriveDistance)
            {
                _hasMoveTarget = false;
                return;
            }

            // 막힘 감지 — StuckDetectSeconds 동안 진전 없으면 의도 포기(배회/은신 좌절 방지)
            if (Time.time - _stuckCheckTime >= StuckDetectSeconds)
            {
                if ((pos - _stuckCheckPos).sqrMagnitude < 0.04f) // 20cm 미만 진전
                {
                    _hasMoveTarget = false;
                    _wanderDwellUntil = Time.time + 3f;
                    return;
                }
                _stuckCheckPos = pos;
                _stuckCheckTime = Time.time;
            }

            Vector3 dir = toTarget / dist;
            transform.position += dir * (_moveSpeed * Time.deltaTime);

            // 지면 스냅 — 지형 높이 따라가기 (LootBasket.Create 하향 레이캐스트 선례)
            if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 5f))
                transform.position = new Vector3(transform.position.x, hit.point.y, transform.position.z);

            // 이동 방향 부드러운 회전 — 120°/s
            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion target = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, 120f * Time.deltaTime);
            }
        }

        // ================================================================
        // 외부 이동 위임 (NPCWeatherBehavior → 브레인 — 단일 이동 소유자)
        // ================================================================

        /// <summary>
        /// 외부 시스템(날씨 은신 등)이 이 NPC 이동을 위임한다. 10초간 판단 억제 후 자동 복원.
        /// </summary>
        public bool RequestExternalMove(Vector3 target)
        {
            if (!isActiveAndEnabled) return false;
            _moveTarget = target;
            _moveSpeed = WalkSpeed;
            _hasMoveTarget = true;
            _current = Intent.SeekShelter;
            _externalMoveUntil = Time.time + 10f;
            _stuckCheckPos = transform.position;
            _stuckCheckTime = Time.time;
            return true;
        }

        // ================================================================
        // 헬퍼
        // ================================================================

        /// <summary>반경 내 전투 중 몬스터 탐색 (MonsterAggroSystem 등록분 — AggroSystem 부재 시 false).</summary>
        private static bool TryFindNearbyCombatThreat(Vector3 pos, float radius, out Vector3 threatPos)
        {
            threatPos = Vector3.zero;
            var aggro = MonsterAggroSystem.Instance;
            if (aggro == null) return false;

            float rSq = radius * radius;
            foreach (var m in aggro.AllMonsters)
            {
                if (m == null || !m.IsInCombat) continue;
                if (m is Component c && c.transform != null)
                {
                    Vector3 mp = c.transform.position;
                    if ((mp - pos).sqrMagnitude <= rSq)
                    {
                        threatPos = mp;
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>가장 가까운 은신처 — ShelterTags 태그 스캔 (NPCWeatherBehavior.FindNearestShelter와 동일 로직, 정적 헬퍼).</summary>
        private static Transform FindNearestShelter(Vector3 npcPosition)
        {
            Transform nearest = null;
            float nearestDist = float.MaxValue;
            foreach (string tag in ShelterTags)
            {
                GameObject[] shelters;
                try { shelters = GameObject.FindGameObjectsWithTag(tag); }
                catch { continue; } // 태그 미정의 예외 — 다음 태그로
                foreach (var shelter in shelters)
                {
                    if (shelter == null) continue;
                    float d = Vector3.Distance(npcPosition, shelter.transform.position);
                    if (d < nearestDist)
                    {
                        nearestDist = d;
                        nearest = shelter.transform;
                    }
                }
            }
            return nearest;
        }

        /// <summary>상점 NPC 판정 — TerritoryNPCBehaviour.IsShopNPC(UI 어셈블리 — 순환참조 방지 리플렉션, DailyCycle 선례)
        /// 우선, 이름 기반 폴백. 타입/프로퍼티는 정적 캐시(틱마다 리플렉션 비용 회피).</summary>
        private static System.Type _behaviourType;
        private static System.Reflection.PropertyInfo _isShopProp;
        private static bool _reflectionResolved;

        private bool IsShopNPC()
        {
            // UI 어셈블리 타입 리플렉션 (1회 해상 후 캐시)
            if (!_reflectionResolved)
            {
                _reflectionResolved = true;
                _behaviourType = System.Type.GetType("ProjectName.UI.TerritoryNPCBehaviour, ProjectName.UI");
                _isShopProp = _behaviourType?.GetProperty("IsShopNPC");
            }
            if (_behaviourType != null && _isShopProp != null)
            {
                var behaviour = GetComponent(_behaviourType);
                if (behaviour != null)
                {
                    try
                    {
                        // [2026-10-15 유령 플래그 방지] NPCInstance.IsShopNPC가 populate 안 되는 경우가 있다
                        // (10-02 실측 — 스킬 참조). false여도 이름 폴백(상인/행상인 등)을 OR로 시도한다.
                        if (_isShopProp.GetValue(behaviour) is bool b && b) return true;
                    }
                    catch { /* 리플렉션 실패 → 이름 폴백 */ }
                }
            }

            string n = name.ToLowerInvariant();
            return n.Contains("shop") || n.Contains("상점") || n.Contains("상인") || n.Contains("merchant");
        }
    }
}
