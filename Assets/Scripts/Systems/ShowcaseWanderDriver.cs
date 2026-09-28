// Test_11_AnimationShowcase (2026-09-20): 몬스터 22종 자율 배회 드라이버.
// AnimalAI는 Player 부재 시 Update에서 Idle+속도 0으로 되돌려 몬스터가 전부 얼어붙는다
// (AnimalAI.cs 503~514행). 쇼케이스 씬은 Player가 없으므로 AnimalAI 대신 이 드라이버를
// 부착해 각 몬스터가 자기 leash 구역 안에서 배회하며 걷기/대기 애니를 독립적으로 재생한다.
// 애니 피드는 AnimalAI.cs의 기존 연결 패턴을 그대로 재현 (컨트롤러 파일 수정 없음):
//  - 4족: QuadrupedProceduralAnimation.SetAiDriven(true) + ApplyMonsterProfile(id)
//         + SetMovementSpeed(speed) — 실제 이동/회전은 이 드라이버가 담당(AnimalAI와 동일 역할 분담)
//  - 2족: ProceduralAnimationController.SetVelocityProvider(this) + ApplyMonsterProfile(id)
//         — IVelocityProvider로 속도 공급, 컨트롤러가 보행 위상 구동(provider 연결 시 자체 이동/중력 스킵)
//  - 특수형: SpecialCreatureAnimator는 자율 애니(미피드 시에도 자체 모션 동작) — 별도 피드 없음
// [Phase B 2026-09-23] 결정론적 관측 사이클 추가 — ConfigureInspectionCycle(idle, move, offset) 호출 시
//  랜덤 idle/랜덤 대기 대신 고정 구간 [정지→이동]을 무한 반복한다(옵트인 — 미호출 시 기존 랜덤 배회 불변).
#pragma warning disable 618 // ProceduralAnimationController [Obsolete] 참조 경고 억제(AnimalAI.cs 동일)
using UnityEngine;
using ProjectName.Systems.Animation.Procedural;

namespace ProjectName.Systems
{
    /// <summary>
    /// Test_11_AnimationShowcase 씬 전용 자율 배회 드라이버.
    /// 스폰 지점 주변 leash(기본 2.5m) 안에서 랜덤 목적지로 걷고, 도착하면 1.2~2.8초 대기(idle) 후
    /// 새 목적지로 이동을 반복한다. 마리별 속도/대기시간/시작 지연을 랜덤 변주해 22종이 제각각 움직인다.
    /// ModelAnimatorAssigner가 부착한 애니 컨트롤러를 지연 탐색해 AnimalAI와 동일한 방식으로 피드한다.
    /// </summary>
    public class ShowcaseWanderDriver : MonoBehaviour, IVelocityProvider
    {
        [Header("Wander (spawn 주변 leash)")]
        [SerializeField] private float _leashRadius = 2.5f;
        [SerializeField] private float _arriveDistance = 0.2f;
        [SerializeField, Range(0.3f, 1.5f)] private float _minHopDistance = 0.5f; // 너무 가까운 목적지 재추첨 기준

        [Header("Speed (마리별 랜덤 변주)")]
        [SerializeField] private float _moveSpeedMin = 1.1f;
        [SerializeField] private float _moveSpeedMax = 1.5f;
        [SerializeField] private float _turnSpeed = 180f; // deg/s — 이동 방향 부드러운 회전

        [Header("Idle")]
        [SerializeField] private float _idleMin = 1.2f;
        [SerializeField] private float _idleMax = 2.8f;

        [Header("Identity (MonsterDatabase 키 — 보행 프로필 적용용)")]
        [SerializeField] private string _monsterId = "";

        // 이동 상태
        private Vector3 _spawnPos;
        private float _groundY;
        private float _moveSpeed;
        private Vector3 _target;
        private bool _moving;
        private float _idleTimer;
        private Rigidbody _rigidbody;

        // 애니 연결 — AnimalAI.UpdateQuadrupedLink(384~405행)/UpdateBipedLink(431~466행)와 동일한
        // 지연 탐색 패턴. ModelAnimatorAssigner.SetupQuadruped/SetupBiped가 이 컴포넌트보다 늦게
        // 부착할 수 있어 발견 시까지 최대 CONTROLLER_SEARCH_TIMEOUT초간 재시도한다.
        private QuadrupedProceduralAnimation _quadAnim;
        private bool _quadConfigured;
        private float _quadSearchElapsed;
        private ProceduralAnimationController _bipedAnim;
        private bool _bipedConfigured;
        private float _bipedSearchElapsed;
        private const float CONTROLLER_SEARCH_TIMEOUT = 3f;
        private bool _warnedNoFeedController; // 걷기/대기 피드 가능 컨트롤러가 하나도 없을 때 1회 안내 로그

        // Rig 애니(Animator controller 보유 모델) — 있으면 상태 피드, 없으면 무해 no-op
        private RigAnimationController _rigAnim;

        // [2026-09-22] 클립 구동 모드 — 병사/NPC(HumanoidClipDriver+Soldier 모드)용.
        // HumanoidClipDriver는 자기 transform 위치 델타로 Speed를 계산하므로, 이 드라이버가
        // 배회시키기만 하면 걷기/대기 클립이 자동 전환된다. 4족/2족 절차 컨트롤러 탐색/피드 생략.
        private bool _clipDriven;

        // [Phase B 2026-09-23] 결정론적 관측 사이클 — Test_11 몬스터 전용 옵트인.
        // ConfigureInspectionCycle 미호출 시 _cycleMode=false 유지 = 기존 랜덤 배회 경로 100% 불변.
        private bool _cycleMode;
        private bool _cycleStarted;              // Start 실행 여부 — Configure 호출 타이밍(Start 전/후) 분기용
        private float _cycleIdleSeconds = 2.5f;  // 정지 구간(고정값 — Random 개입 없음)
        private float _cycleMoveSeconds = 3.0f;  // 이동 구간(고정값 — Random 개입 없음)
        private float _cyclePhaseOffsetSeconds;  // 첫 정지창에 가산 — 마리별 출발 스태거용
        private float _cycleMoveDeadline;        // 이동 구간 절대 데드라인(Time.time) — 미도착 시에도 주기 보장
        private const float InspectionCycleLeashRadius = 1.2f; // 사이클용 짧은 leash — 이웃 간격(3.4m) 내 왕복

        /// <summary>클립 구동 모드 설정(병사/NPC) — 절차 컨트롤러 피드 로직을 생략한다.</summary>
        public void SetClipDriven(bool clipDriven)
        {
            _clipDriven = clipDriven;
            if (clipDriven)
            {
                _quadConfigured = true;   // 탐색/타임아웃 경로 완전 우회
                _bipedConfigured = true;
                _warnedNoFeedController = true; // 경고 억제(클립 경로가 정식 피드 담당)
            }
        }

        // ──────────────────────────────────────────────
        // [Phase B 2026-09-23] 결정론적 관측 사이클 (Test_11 몬스터 전용 옵트인)
        // 정지(_cycleIdleSeconds) → 이동(_cycleMoveSeconds) 무한 반복. 구간 길이/정지시간에는
        // Random이 개입하지 않고, 목적지 좌표만 기존 leash 추첨(랜덤)을 재사용한다.
        // 전환 로그는 phase 변경 시점에만 1행씩 출력한다.
        // ──────────────────────────────────────────────

        /// <summary>
        /// 결정론적 정지→이동→정지 사이클 활성화 (Test_11 관측 전용 — 미호출 시 기존 랜덤 배회 불변).
        /// 활성화 후 Update는 랜덤 idle(1.2~2.8s)/랜덤 목적지 대신 고정 구간을 주기 반복한다.
        /// phaseOffsetSeconds는 첫 정지창에 가산되어 마리별 이동 시작을 벌린다(스태그 — i*0.55s 등).
        /// Setup(Awake)에서 Start 이전 호출이 정상 경로(설정 저장 후 Start에서 개시).
        /// </summary>
        public void ConfigureInspectionCycle(float idleSeconds, float moveSeconds, float phaseOffsetSeconds)
        {
            _cycleIdleSeconds = Mathf.Max(0.1f, idleSeconds);
            _cycleMoveSeconds = Mathf.Max(0.1f, moveSeconds);
            _cyclePhaseOffsetSeconds = Mathf.Max(0f, phaseOffsetSeconds);
            _cycleMode = true;

            // Start 이후 호출(예외 경로)도 지원 — 루트 원점/지면값 재확정 후 즉시 사이클 개시.
            if (_cycleStarted)
                BeginInspectionCycle();
        }

        /// <summary>사이클 개시 — 루트 원점/지면값 재확정 후 첫 정지창(idle+스태거) 진입.</summary>
        private void BeginInspectionCycle()
        {
            // 루트 원점 재확정 — 현재 위치 기준 leash 재계산, y는 확정 지면값 보존(접지 규약).
            // Configure 호출 타이밍(Start 전/후 무관) 이후 항상 최신 접지 상태를 기준으로 삼는다.
            _spawnPos = transform.position;
            _spawnPos.y = _groundY;

            // 첫 정지창 = idle + 마리별 스태거 — 전 마리 wanderFeed=0 창 확보 후 순차 출발.
            // (Start의 랜덤 초기 대기 Random.Range(0.3,1.5)를 이 고정 구간으로 대체)
            StopForIdle(_cycleIdleSeconds + _cyclePhaseOffsetSeconds);
        }

        /// <summary>사이클 상태 진행 — 정지 중 결정론적 카운트다운, 이동 중 1프레임 진행+데드라인 판정.</summary>
        private void UpdateInspectionCycle()
        {
            if (!_moving)
            {
                // 정지 스테이지 — 고정 구간 카운트다운(랜덤 개입 없음)
                _idleTimer -= Time.deltaTime;
                if (_idleTimer <= 0f)
                    BeginCycleMove();
                return;
            }

            // 이동 스테이지 — 목적지는 전환 시 1회 선택, 매 프레임은 기존 이동 블록 재사용.
            // 데드라인: 미도착이어도 구간 종료 → 다음 주기 보장(도착/데드라인 동시 충족 시 1회만 전환).
            if (RunMoveStep() || Time.time >= _cycleMoveDeadline)
                EndCycleMove();
        }

        /// <summary>정지→이동 전환 — 루트 원점 재확정 + 짧은 leash 목적지 1회 선택 + 데드라인 설정.</summary>
        private void BeginCycleMove()
        {
            // 정지 자리 기준 원점 재확정(누적 드리프트 방지) — y는 지면값 보존
            _spawnPos = transform.position;
            _spawnPos.y = _groundY;

            // 짧은 leash — 사이클용 반경 clamp(이웃 간격 3.4m 내 왕복). 좌표 추첨은 기존 헬퍼 재사용.
            float leash = Mathf.Min(_leashRadius, InspectionCycleLeashRadius);
            PickRandomWaypoint(leash);

            _cycleMoveDeadline = Time.time + _cycleMoveSeconds;
            Debug.Log($"[ShowcaseWander] [Cycle] 정지→이동: {_monsterId} (이동 {_cycleMoveSeconds:0.00}s, leash {leash:0.0}m)");
        }

        /// <summary>이동→정지 전환 — 기존 정지 경로(피드 0 + 잔여 속도 정리) + 고정 대기시간.</summary>
        private void EndCycleMove()
        {
            StopForIdle(_cycleIdleSeconds);
            Debug.Log($"[ShowcaseWander] [Cycle] 이동→정지: {_monsterId} (정지 {_cycleIdleSeconds:0.00}s)");
        }

        // ===================== IVelocityProvider 구현 [AnimalAI와 동일 계약] =====================
        /// <summary>현재 이동 속도 벡터 — 2족 ProceduralAnimationController의 보행 위상/조향 판정용.</summary>
        public Vector3 CurrentVelocity { get; private set; }
        /// <summary>현재 이동 속력(m/s) — 걷기/정지 블렌드 판정용.</summary>
        public float CurrentSpeed { get; private set; }
        /// <summary>몬스터는 항상 지면 위를 이동하므로 true 고정(공중 상태 없음) — AnimalAI.IsGrounded 동일.</summary>
        public bool IsGrounded => true;

        /// <summary>몬스터 ID 설정. TestAnimationShowcaseSetup.CreateShowcaseMonster에서 호출.</summary>
        public void SetMonsterId(string id)
        {
            _monsterId = id;
        }

        /// <summary>몬스터 ID (MonsterDatabase 키). 읽기 전용.</summary>
        public string MonsterId => _monsterId;

        private void Start()
        {
            // GroundModelToY(셋업 접지)가 AddComponent '이후'에 위치를 보정하므로 spawn/지면 y는
            // Start(Awake 전부 실행 후)에 확정해야 정확하다.
            _spawnPos = transform.position;
            _groundY = _spawnPos.y;
            _rigidbody = GetComponent<Rigidbody>();
            _rigAnim = GetComponent<RigAnimationController>();

            // 개별성 — 마리마다 속도/초기 대기시간을 변주(22종 동시 출발 방지)
            _moveSpeed = Random.Range(_moveSpeedMin, _moveSpeedMax);
            _idleTimer = Random.Range(0.3f, 1.5f);

            // ModelAnimatorAssigner.Awake가 이미 컨트롤러를 붙여놨다면 즉시 연결 시도
            UpdateQuadrupedLink();
            UpdateBipedLink();

            // [Phase B] 결정론적 관측 사이클 — ConfigureInspectionCycle이 Start 전에 호출된 정상 경로.
            // 접지 확정된 지면값으로 루트 원점 재확정 후 첫 정지창(idle+스태거)을 연다(랜덤 초기 대기 대체).
            _cycleStarted = true;
            if (_cycleMode)
                BeginInspectionCycle();
        }

        private void Update()
        {
            // 지연 탐색 — 컨트롤러가 늦게 부착되는 경우 대비(AnimalAI 동일 패턴)
            UpdateQuadrupedLink();
            UpdateBipedLink();
            WarnIfNoFeedController();

            // [Phase B] 결정론적 관측 사이클 — 옵트인 시 랜덤 배회 대신 고정 구간 [정지→이동] 진행
            if (_cycleMode)
            {
                UpdateInspectionCycle();
                return;
            }

            if (_moving)
            {
                // 이동 1프레임 진행 — 도착 시 기존과 동일하게 정지(idle) 전환
                if (RunMoveStep())
                {
                    StopForIdle();
                    return;
                }
            }
            else
            {
                _idleTimer -= Time.deltaTime;
                if (_idleTimer <= 0f)
                    PickRandomWaypoint();
            }
        }

        /// <summary>
        /// 이동 1프레임 진행 — 회전 정렬 + 이동 + 속도 피드(기존 Update 이동 블록을 그대로 추출).
        /// 도착(수평 dist ≤ _arriveDistance) 시 true 반환 — 정지 전환 판정은 호출부가 담당한다.
        /// </summary>
        private bool RunMoveStep()
        {
            Vector3 toTarget = _target - transform.position;
            toTarget.y = 0f; // 수평 거리만 판정
            float dist = toTarget.magnitude;

            if (dist <= _arriveDistance)
                return true;

            Vector3 dir = dist > 0.0001f ? toTarget / dist : transform.forward;

            // 회전 — 이동 방향(수평)으로 부드럽게 정렬(AnimalAI LookRotation 역할)
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, Quaternion.LookRotation(dir), _turnSpeed * Time.deltaTime);

            MoveAlong(dir, _moveSpeed);
            PublishVelocity(dir * _moveSpeed, _moveSpeed);
            FeedAnimationSpeed(_moveSpeed);
            return false;
        }

        // ──────────────────────────────────────────────
        // 배회 코어
        // ──────────────────────────────────────────────

        /// <summary>leash 반경 내 랜덤 목적지 선택 — 너무 가까운 지점은 재추첨해 기동감 확보.</summary>
        private void PickRandomWaypoint() => PickRandomWaypoint(_leashRadius);

        /// <summary>
        /// leash 반경 지정형 목적지 선택 — [Phase B] 결정론적 사이클이 짧은 leash(1.2m clamp)로 재사용.
        /// 지면 y 고정/최소 홉 거리 재추첨 등 기존 규약은 동일하다.
        /// </summary>
        private void PickRandomWaypoint(float leashRadius)
        {
            Vector3 chosen = Vector3.zero;
            bool found = false;

            for (int attempt = 0; attempt < 4; attempt++)
            {
                Vector2 circle = Random.insideUnitCircle * leashRadius;
                Vector3 candidate = _spawnPos + new Vector3(circle.x, 0f, circle.y);
                candidate.y = _groundY; // 지면 높이 고정 — 배회 중 상하 부유 방지
                if ((candidate - transform.position).sqrMagnitude >= _minHopDistance * _minHopDistance)
                {
                    chosen = candidate;
                    found = true;
                    break;
                }
            }

            // 재추첨 실패 시에도 이동(무한 정지 방지)
            if (!found)
            {
                Vector2 fallback = Random.insideUnitCircle * leashRadius;
                chosen = _spawnPos + new Vector3(fallback.x, 0f, fallback.y);
                chosen.y = _groundY;
            }

            _target = chosen;
            _moving = true;
        }

        /// <summary>
        /// 도착 — 정지(idle) 진입, 잔여 수평 속도 정리 후 대기시간 설정.
        /// [Phase B] idleDuration ≥ 0이면 고정값(결정론적 사이클 — Random 개입 없음),
        /// 미지정(-1)이면 기존 랜덤 대기(1.2~2.8s)를 유지한다.
        /// </summary>
        private void StopForIdle(float idleDuration = -1f)
        {
            _moving = false;
            _idleTimer = idleDuration >= 0f ? idleDuration : Random.Range(_idleMin, _idleMax);

            // 물리 이동 중이던 마리의 잔여 슬라이드 정리 — y는 유지(중력/지면 접촉 보존)
            if (_rigidbody != null && !_rigidbody.isKinematic)
                _rigidbody.linearVelocity = new Vector3(0f, _rigidbody.linearVelocity.y, 0f);

            PublishVelocity(Vector3.zero, 0f);
            FeedAnimationSpeed(0f);
        }

        /// <summary>
        /// 이동 — Rigidbody가 있으면 수평 velocity 주입(물리 기반, y는 중력/지면 collider 담당),
        /// kinematic/무Rigidbody면 transform 직접 이동(AnimalAI 패턴) + 지면 y 고정.
        /// </summary>
        private void MoveAlong(Vector3 dir, float speed)
        {
            if (_rigidbody != null && !_rigidbody.isKinematic)
            {
                Vector3 v = _rigidbody.linearVelocity;
                _rigidbody.linearVelocity = new Vector3(dir.x * speed, v.y, dir.z * speed);
            }
            else
            {
                Vector3 next = transform.position + dir * (speed * Time.deltaTime);
                next.y = _groundY;
                transform.position = next;
            }
        }

        // ──────────────────────────────────────────────
        // 애니메이션 피드 (AnimalAI FeedQuadrupedSpeed 473~484행 패턴)
        // ──────────────────────────────────────────────

        /// <summary>IVelocityProvider 출력 갱신 — 2족 컨트롤러가 HandleInput에서 매 프레임 읽는다.</summary>
        private void PublishVelocity(Vector3 velocity, float speed)
        {
            CurrentVelocity = velocity;
            CurrentSpeed = Mathf.Max(0f, speed);
        }

        /// <summary>이동/정지 속도를 애니 컨트롤러에 피드(이동 시 speed, 정지 시 0).</summary>
        private void FeedAnimationSpeed(float speed)
        {
            // 4족 — AI 구동 모드에서 SetMovementSpeed가 다리 위상 구동 속도가 됨(0 → idle 자세)
            if (_quadConfigured && _quadAnim != null)
                _quadAnim.SetMovementSpeed(speed);

            // 2족 — provider 연결 완료 시 CurrentVelocity/CurrentSpeed가 이미 갱신돼
            // 컨트롤러가 보행 위상(다리 사이클)/lean을 자체 구동한다(별도 호출 불필요).

            // Rig 애니 — 보유 시에만 상태 피드(SetState는 상태 전환 코루틴 관리)
            if (_rigAnim != null)
            {
                AnimationState target = speed > 0.01f ? AnimationState.Walk : AnimationState.Idle;
                if (_rigAnim.CurrentState != target)
                    _rigAnim.SetState(target);
                _rigAnim.CurrentSpeed = speed;
            }
        }

        // ──────────────────────────────────────────────
        // 컨트롤러 지연 연결 (AnimalAI UpdateQuadrupedLink/UpdateBipedLink 동일 패턴)
        // ──────────────────────────────────────────────

        /// <summary>4족 절차 애니 연결 — 발견 시 AI 구동 모드 진입 + 종별 보행 프로필 적용.</summary>
        private void UpdateQuadrupedLink()
        {
            if (_quadConfigured) return;

            if (_quadAnim == null)
            {
                _quadAnim = GetComponent<QuadrupedProceduralAnimation>();
                if (_quadAnim == null)
                {
                    _quadSearchElapsed += Time.deltaTime;
                    if (_quadSearchElapsed < CONTROLLER_SEARCH_TIMEOUT) return;
                    _quadConfigured = true; // 4족 애니 미부착(2족/특수형 등) — 탐색 포기
                    return;
                }
            }

            // 4족 발견 → AI 구동 모드 진입 + 종별 보행 프로필 적용 (이동/회전은 이 드라이버 담당)
            _quadAnim.SetAiDriven(true);
            if (!string.IsNullOrEmpty(_monsterId))
                _quadAnim.ApplyMonsterProfile(_monsterId);
            _quadConfigured = true;
            Debug.Log($"[ShowcaseWander] 4족 절차 애니 연결 완료: {_monsterId} (AI 구동 모드 + 보행 프로필)");
        }

        /// <summary>2족 절차 애니 연결 — IVelocityProvider 연결 + 종별 보행 프로필 적용.</summary>
        private void UpdateBipedLink()
        {
            if (_bipedConfigured) return;

            if (_bipedAnim == null)
            {
                _bipedAnim = GetComponent<ProceduralAnimationController>();
                if (_bipedAnim == null)
                {
                    _bipedSearchElapsed += Time.deltaTime;
                    if (_bipedSearchElapsed < CONTROLLER_SEARCH_TIMEOUT) return;
                    _bipedConfigured = true; // 절차 애니 미부착 — 속도 피드 생략(이동만 유지)
                    return;
                }
            }

            // 2족 발견 → 실 이동 속도 공급자 연결 + 종별 보행 프로필 적용.
            // 실 이동/회전은 이 드라이버가 transform/velocity로 담당하며, 컨트롤러는 공급 속도를
            // 읽어 보행 위상(다리 사이클)·lean만 구동한다(provider 연결 시 자체 이동/중력 스킵).
            _bipedAnim.SetVelocityProvider(this);
            if (!string.IsNullOrEmpty(_monsterId))
                _bipedAnim.ApplyMonsterProfile(_monsterId);
            _bipedConfigured = true;
            Debug.Log($"[ShowcaseWander] 2족 절차 애니 연결 완료: {_monsterId} (IVelocityProvider 연결 + 보행 프로필)");
        }

        /// <summary>걷기/대기 피드 가능 컨트롤러가 하나도 없으면 1회만 안내 로그(무해 no-op 보장).</summary>
        private void WarnIfNoFeedController()
        {
            if (_warnedNoFeedController) return;
            if (!_quadConfigured || !_bipedConfigured) return; // 탐색 진행 중
            if (_quadAnim != null || _bipedAnim != null) return;

            _warnedNoFeedController = true;
            // 특수형(SpecialCreatureAnimator)은 자율 애니라 이 경고 대상이 아니므로, 4족/2족 모두
            // 미부착이면서 특수형 애니도 없는 경우(프리미티브 폴백 등)에만 출력된다.
            bool hasSpecial = GetComponent<ProjectName.Systems.Animation.Procedural.SpecialCreatureAnimator>() != null;
            if (!hasSpecial)
                Debug.LogWarning($"[ShowcaseWander] 피드 가능 애니 컨트롤러 없음 — 이동만 유지: {_monsterId}");
        }
    }
}
