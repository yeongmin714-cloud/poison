using System.Collections.Generic;
using UnityEngine;
using ProjectName.Systems.Animation.Procedural.Bones;
using ProjectName.Systems.Animation.Procedural.IK;
using static ProjectName.Systems.Animation.Procedural.IK.LimbIKSolver;

namespace ProjectName.Systems
{
    /// <summary>
    /// 4족 동물 완전 프로시저럴 애니메이션 컨트롤러.
    /// - 걸음걸이 자동 선택 (Walk/Trot/Pace/Gallop)
    /// - 다리 IK + 척추 파동 + 목 안정화
    /// - 점프/공격/피격 등 액션 오버라이드
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(Rigidbody))]
    public class QuadrupedProceduralAnimation : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        // 인스펙터 설정
        // ──────────────────────────────────────────────

        [Header("Locomotion")]
        [SerializeField] private float _walkSpeed = 2f;
        [SerializeField] private float _trotSpeed = 5f;
        [SerializeField] private float _paceSpeed = 6f;
        [SerializeField] private float _gallopSpeed = 10f;
        [SerializeField] private float _acceleration = 10f;
        [SerializeField] private float _turnSpeed = 540f;

        [Header("Jump")]
        [SerializeField] private float _jumpHeight = 2f;
        [SerializeField] private float _gravity = -20f;
        [SerializeField] private float _coyoteTime = 0.1f;

        [Header("IK Weights")]
        [SerializeField, Range(0f, 1f)] private float _footIKWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float _spineIKWeight = 0.7f;
        [SerializeField, Range(0f, 1f)] private float _headLookWeight = 0.8f;

        [Header("Procedural")]
        [SerializeField] private float _stepLength = 0.6f;
        [SerializeField] private float _stepHeight = 0.15f;
        [SerializeField] private float _dutyCycle = 0.7f;
        [SerializeField] private float _bodyLeanAmount = 0.5f;

        [Header("Ground")]
        [SerializeField] private LayerMask _groundMask = ~0;
        [SerializeField] private float _groundCheckDistance = 1f;

        // ──────────────────────────────────────────────
        // 컴포넌트
        // ──────────────────────────────────────────────

        private Animator _animator;
        private Rigidbody _rigidbody;
        private ProceduralBoneMap _boneMap;
        private QuadrupedProceduralLocomotion _locomotion;

        // ──────────────────────────────────────────────
        // 이동 상태
        // ──────────────────────────────────────────────

        private Vector3 _currentVelocity;
        private Vector3 _targetVelocity;
        private float _currentSpeed;
        private float _targetSpeed;
        private bool _isGrounded;
        private float _coyoteTimer;

        // [2026-09-14(49차 후속)] 마지막으로 동기화한 gait — Locomotion.CurrentGait 변화 감지 시
        // gait 오프셋으로 실제 렌더 위상(LF/RF/LH/RH_Phase)을 재정렬하는 트리거용.
        private QuadrupedProceduralLocomotion.Gait? _syncedGait;

        // [2026-09-14(49차)] AI 구동 모드 플래그 — AnimalAI.SetAiDriven(true)로 활성화.
        // true면 HandleInput(키보드) 대신 SetMovementSpeed()로 공급된 속도를 사용하고,
        // FixedUpdate의 ApplyMovement(Rigidbody 이동)는 스킵한다(AI가 transform 직접 이동).
        private bool _aiDriven;

        // Leg phases (0~1)
        public float LF_Phase = 0f;    // Left Front
        public float RF_Phase = 0.5f;  // Right Front
        public float LH_Phase = 0.25f; // Left Hind
        public float RH_Phase = 0.75f; // Right Hind

        // IK Targets (public for locomotion module)
        public Vector3 LF_Target, RF_Target, LH_Target, RH_Target;
        public Vector3 LF_Hint, RF_Hint, LH_Hint, RH_Hint;

        // Ground detection
        private RaycastHit _lfHit, _rfHit, _lhHit, _rhHit;
        private bool _lfGrounded, _rfGrounded, _lhGrounded, _rhGrounded;

        // Body/Head
        private Vector3 _bodyLeanOffset;
        private Quaternion _bodyLeanRotation = Quaternion.identity;
        private Vector3 _headLookTarget;

        // Action override
        private ActionState _actionState = ActionState.None;
        private float _actionTimer;
        private Vector3 _actionTarget;

        private enum ActionState { None, Attack, Stagger, Eat, Sleep }

        // ──────────────────────────────────────────────
        // 공개 속성 (로코모션 모듈에서 사용)
        // ──────────────────────────────────────────────

        public Vector3 CurrentVelocity => _currentVelocity;
        public float CurrentSpeed => _currentSpeed;
        public bool IsGrounded => _isGrounded;
        public QuadrupedProceduralLocomotion LocomotionModule => _locomotion;
        public ProceduralBoneMap BoneMap => _boneMap;

        // [2026-09-14(49차 후속)] 발별 지면 접촉(레이 히트) 플래그 공개 —
        // Locomotion.UpdateLegTarget이 스윙 리프트 적용 시 타겟 갱신 여부 판단(누적 방지)에 사용.
        public bool LF_Grounded => _lfGrounded;
        public bool RF_Grounded => _rfGrounded;
        public bool LH_Grounded => _lhGrounded;
        public bool RH_Grounded => _rhGrounded;

        // ──────────────────────────────────────────────
        // Unity Lifecycle
        // ──────────────────────────────────────────────

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _rigidbody = GetComponent<Rigidbody>();
            _boneMap = GetComponent<ProceduralBoneMap>();
            _locomotion = GetComponent<QuadrupedProceduralLocomotion>();

            if (_locomotion == null)
                _locomotion = gameObject.AddComponent<QuadrupedProceduralLocomotion>();

            _animator.applyRootMotion = false;
            _animator.updateMode = AnimatorUpdateMode.Fixed;
            _animator.animatePhysics = true;

            _rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            if (_boneMap != null)
                _boneMap.Initialize(_animator);
        }

        private void Start()
        {
            InitializeIKTargets();
        }

        private void Update()
        {
            // [2026-09-14(49차)] AI 구동 분기 — 몬스터는 AnimalAI가 transform을 직접 이동하므로
            // WASD 키보드 전용 HandleInput()은 목표 속도를 절대 설정하지 못해 다리가 정지했다.
            // AI 모드에서는 SetMovementSpeed()로 공급된 속도를 사용한다.
            if (_aiDriven)
            {
                UpdateMovementAI();
            }
            else
            {
                HandleInput();   // 플레이어 테스트 전용 (키보드)
                UpdateMovement();
            }
            UpdateCoyoteTime();
        }

        private void FixedUpdate()
        {
            // [2026-09-14(49차)] AI 구동 시 이동은 AnimalAI가 transform.position으로 직접 처리하므로
            // Rigidbody 속도 재설정(ApplyMovement)을 스킵한다 — 이동 중복/충돌 방지.
            if (!_aiDriven)
                ApplyMovement();
            ApplyGravity();
        }

        private void LateUpdate()
        {
            UpdateGroundDetection();
            UpdateLegPhases();
            UpdateIKTargets();

            // [2026-09-14(49차 후속)] 지면 감지로 발 타겟(접촉점)이 확정된 '직후' gait 스윙 보정을 적용한다.
            // 기존엔 Locomotion.Update에서 타겟을 계산해도 이후 UpdateGroundDetection이 덮어써
            // _stepHeight 리프트가 화면에 반영되지 않았다(실행 순서 문제).
            if (_locomotion != null)
                _locomotion.UpdateGaitTargets();

            ApplyProceduralPose();
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0) return;
            ApplyIKToAnimator();
        }

        // ──────────────────────────────────────────────
        // 초기화
        // ──────────────────────────────────────────────

        private void InitializeIKTargets()
        {
            var lf = _boneMap.Get(BoneRole.L_Foot);
            var rf = _boneMap.Get(BoneRole.R_Foot);
            var lh = _boneMap.Get(BoneRole.L_Foot); // Hind uses same role names for now
            var rh = _boneMap.Get(BoneRole.R_Foot);

            // 임시: 앞다리/뒷다리 구분 필요시 본 역할 추가
            if (lf != null) LF_Target = lf.position;
            if (rf != null) RF_Target = rf.position;
            if (lh != null) LH_Target = lh.position;
            if (rh != null) RH_Target = rh.position;

            // 힌트
            var lfKnee = _boneMap.Get(BoneRole.L_Knee);
            var rfKnee = _boneMap.Get(BoneRole.R_Knee);
            if (lfKnee != null) LF_Hint = lfKnee.position + Vector3.right * 0.2f;
            if (rfKnee != null) RF_Hint = rfKnee.position + Vector3.left * 0.2f;
        }

        // ──────────────────────────────────────────────
        // 입력 처리
        // ──────────────────────────────────────────────

        private void HandleInput()
        {
            if (_actionState != ActionState.None) return;

            Vector2 input = Vector2.zero;
            if (Input.GetKey(KeyCode.W)) input.y += 1;
            if (Input.GetKey(KeyCode.S)) input.y -= 1;
            if (Input.GetKey(KeyCode.A)) input.x -= 1;
            if (Input.GetKey(KeyCode.D)) input.x += 1;
            input = Vector2.ClampMagnitude(input, 1f);

            bool sprint = Input.GetKey(KeyCode.LeftShift);

            Vector3 localTarget = new Vector3(input.x, 0, input.y);
            _targetVelocity = transform.TransformDirection(localTarget) * (sprint ? _gallopSpeed : _trotSpeed);
            _targetSpeed = _targetVelocity.magnitude;

            if (_targetVelocity.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(_targetVelocity.normalized);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, _turnSpeed * Time.deltaTime);
            }

            // Actions
            if (Input.GetKeyDown(KeyCode.Space)) RequestJump();
            if (Input.GetMouseButtonDown(0)) RequestAttack();
        }

        // ──────────────────────────────────────────────
        // 이동 업데이트
        // ──────────────────────────────────────────────

        private void UpdateMovement()
        {
            _currentVelocity = Vector3.MoveTowards(_currentVelocity, _targetVelocity, _acceleration * Time.deltaTime);
            _currentSpeed = _currentVelocity.magnitude;
            _locomotion.SetTargetSpeed(_targetSpeed);

            float turnInput = Input.GetKey(KeyCode.A) ? -1f : (Input.GetKey(KeyCode.D) ? 1f : 0f);
            float targetLean = turnInput * _bodyLeanAmount * 10f;
            _bodyLeanOffset = Vector3.Lerp(_bodyLeanOffset, new Vector3(targetLean, 0, 0), Time.deltaTime * 5f);
            _bodyLeanRotation = Quaternion.Lerp(_bodyLeanRotation, Quaternion.Euler(_bodyLeanOffset), Time.deltaTime * 5f);
        }

        // ──────────────────────────────────────────────
        // [2026-09-14(49차)] AI 구동 이동 (몬스터 전용)
        // ──────────────────────────────────────────────

        /// <summary>
        /// [2026-09-14(49차)] AI 구동 모드 설정. MonsterSpawner/AnimalAI에서 호출.
        /// true: HandleInput(키보드) 비활성 + SetMovementSpeed() 속도 사용 + ApplyMovement 스킵.
        /// </summary>
        public void SetAiDriven(bool on)
        {
            _aiDriven = on;
            if (on)
            {
                // 키보드 모드에서 남은 목표/현재 속도 초기화 (잔여 슬라이드 방지).
                // Rigidbody Y속도는 유지(중력/점프 보존), 수평 속도만 0화.
                _targetVelocity = Vector3.zero;
                _targetSpeed = 0f;
                _currentVelocity = Vector3.zero;
                _currentSpeed = 0f;
                if (_rigidbody != null)
                    _rigidbody.linearVelocity = new Vector3(0f, _rigidbody.linearVelocity.y, 0f);
            }
        }

        /// <summary>
        /// [2026-09-14(49차)] AI 구동용 실시간 이동 속도 설정 — HandleInput의 _targetVelocity/_targetSpeed
        /// 설정을 대체. 방향은 transform.forward 기준으로 내부 변환하며, 실제 회전은 AnimalAI가 담당하므로
        /// 여기서 transform을 회전하지 않는다(회전 중복 방지).
        /// </summary>
        public void SetMovementSpeed(float speed)
        {
            if (!_aiDriven) return; // 키보드 테스트 모드에서의 오호출 무시
            speed = Mathf.Max(0f, speed);
            _targetSpeed = speed;
            _targetVelocity = transform.forward * speed;
        }

        /// <summary>
        /// [2026-09-14(49차)] AI 모드 이동 업데이트 — UpdateMovement()의 키보드 의존부(턴 린 입력)를
        /// 제거한 버전. 회전 린은 AnimalAI 회전에 개입하지 않도록 0으로 수렴시킨다.
        /// </summary>
        private void UpdateMovementAI()
        {
            _currentVelocity = Vector3.MoveTowards(_currentVelocity, _targetVelocity, _acceleration * Time.deltaTime);
            _currentSpeed = _currentVelocity.magnitude;
            _locomotion.SetTargetSpeed(_targetSpeed);

            // AI 모드: 좌우 린 입력 없음 — 기존 린 잔량을 0으로 수렴
            _bodyLeanOffset = Vector3.Lerp(_bodyLeanOffset, Vector3.zero, Time.deltaTime * 5f);
            _bodyLeanRotation = Quaternion.Lerp(_bodyLeanRotation, Quaternion.identity, Time.deltaTime * 5f);
        }

        // ──────────────────────────────────────────────
        // [2026-09-14(49차)] 몬스터별 보행 프로필
        // ──────────────────────────────────────────────

        /// <summary>
        /// [2026-09-14(49차)] 종별 보행 파라미터 프로필 적용 — 몬스터 ID 기반.
        /// 걸음 속도 임계(walk/trot/gallop)와 스텝 길이/높이를 종 특성에 맞춘다.
        /// 값은 Locomotion 모듈에도 동기화되어 gait 선택 임계로 사용된다.
        /// </summary>
        public void ApplyMonsterProfile(string monsterId)
        {
            switch (monsterId)
            {
                case "rabbit": // 빠른 도약형 — 높이 뛰는 발, 도약 호핑 갤럽 고정
                    _walkSpeed = 2.5f; _trotSpeed = 5f; _gallopSpeed = 9f;
                    _stepLength = 0.5f; _stepHeight = 0.35f;
                    if (_locomotion != null) _locomotion.SetGaitOverride(QuadrupedProceduralLocomotion.Gait.Gallop);
                    break;
                case "wolf": // 빠른 갤럽
                    _walkSpeed = 3f; _trotSpeed = 6f; _gallopSpeed = 12f;
                    _stepLength = 0.9f; _stepHeight = 0.18f;
                    break;
                case "boar": // 무거운 트롯/충전
                    _walkSpeed = 2f; _trotSpeed = 4f; _gallopSpeed = 8f;
                    _stepLength = 0.7f; _stepHeight = 0.14f;
                    break;
                case "deer": // 우아한 갤럽 — 긴 보폭
                    _walkSpeed = 3f; _trotSpeed = 7f; _gallopSpeed = 13f;
                    _stepLength = 1.0f; _stepHeight = 0.2f;
                    break;
                case "giant_rat": // 빠른 소형
                    _walkSpeed = 2f; _trotSpeed = 5f; _gallopSpeed = 9f;
                    _stepLength = 0.5f; _stepHeight = 0.12f;
                    break;
                case "fire_lizard": // 낮게 기어가는 파충류 — 스텝 낮고 좁음(다리 폭 좁음+몸 낮음은 스텝 파라미터로 근사)
                case "salamander":
                    _walkSpeed = 1.5f; _trotSpeed = 3f; _gallopSpeed = 5f;
                    _stepLength = 0.6f; _stepHeight = 0.08f;
                    break;
                case "electric_porcupine": // 느린 고슴도치
                    _walkSpeed = 1.5f; _trotSpeed = 3f; _gallopSpeed = 5f;
                    _stepLength = 0.4f; _stepHeight = 0.1f;
                    break;
                case "swamp_croc": // 천천히 기어가는 악어 — 척추 파동 강화(몸통 굽힘)
                    _walkSpeed = 1f; _trotSpeed = 2f; _gallopSpeed = 4f;
                    _stepLength = 0.8f; _stepHeight = 0.06f;
                    if (_locomotion != null) _locomotion.SetSpineWave(0.12f, 1.5f);
                    break;
                case "griffin": // 대형 맹수
                case "manticore":
                    _walkSpeed = 2.5f; _trotSpeed = 5f; _gallopSpeed = 11f;
                    _stepLength = 1.1f; _stepHeight = 0.22f;
                    break;
                default: // 그 외 4족: 중간값(인스펙터 기본값) 유지
                    break;
            }

            // 프로필 값을 Locomotion(gait 임계/스텝 파라미터)에 동기화 — 두 클래스 일관 유지
            if (_locomotion != null)
                _locomotion.SyncProfileParams(_walkSpeed, _trotSpeed, _paceSpeed, _gallopSpeed, _stepLength, _stepHeight);

            Debug.Log($"[QuadrupedProceduralAnimation] 보행 프로필 적용: {monsterId} (walk={_walkSpeed}, trot={_trotSpeed}, gallop={_gallopSpeed}, stepLen={_stepLength}, stepH={_stepHeight})");
        }

        private void ApplyMovement()
        {
            if (!_isGrounded) return;
            Vector3 move = _currentVelocity * Time.fixedDeltaTime;
            move.y = _rigidbody.linearVelocity.y;
            _rigidbody.linearVelocity = move;
        }

        private void ApplyGravity()
        {
            if (!_isGrounded)
                _rigidbody.AddForce(Vector3.up * _gravity * _rigidbody.mass, ForceMode.Force);
        }

        private void UpdateCoyoteTime()
        {
            if (_isGrounded) _coyoteTimer = _coyoteTime;
            else _coyoteTimer -= Time.deltaTime;
        }

        // ──────────────────────────────────────────────
        // Ground Detection
        // ──────────────────────────────────────────────

        private void UpdateGroundDetection()
        {
            var lf = _boneMap.Get(BoneRole.L_Foot);
            var rf = _boneMap.Get(BoneRole.R_Foot);

            _lfGrounded = false; _rfGrounded = false; _lhGrounded = false; _rhGrounded = false;

            if (lf != null)
            {
                Vector3 origin = lf.position + Vector3.up * 0.15f;
                if (Physics.Raycast(origin, Vector3.down, out _lfHit, _groundCheckDistance, _groundMask))
                {
                    _lfGrounded = true;
                    LF_Target = _lfHit.point + Vector3.up * 0.02f;
                }
            }
            if (rf != null)
            {
                Vector3 origin = rf.position + Vector3.up * 0.15f;
                if (Physics.Raycast(origin, Vector3.down, out _rfHit, _groundCheckDistance, _groundMask))
                {
                    _rfGrounded = true;
                    RF_Target = _rfHit.point + Vector3.up * 0.02f;
                }
            }
            // Hind legs - using same foot bones for now (need separate hind foot bones)
            if (lf != null)
            {
                Vector3 origin = lf.position + Vector3.up * 0.15f + transform.forward * -0.5f;
                if (Physics.Raycast(origin, Vector3.down, out _lhHit, _groundCheckDistance, _groundMask))
                {
                    _lhGrounded = true;
                    LH_Target = _lhHit.point + Vector3.up * 0.02f;
                }
            }
            if (rf != null)
            {
                Vector3 origin = rf.position + Vector3.up * 0.15f + transform.forward * -0.5f;
                if (Physics.Raycast(origin, Vector3.down, out _rhHit, _groundCheckDistance, _groundMask))
                {
                    _rhGrounded = true;
                    RH_Target = _rhHit.point + Vector3.up * 0.02f;
                }
            }

            _isGrounded = _lfGrounded || _rfGrounded || _lhGrounded || _rhGrounded;
        }

        // ──────────────────────────────────────────────
        // 다리 위상 업데이트
        // ──────────────────────────────────────────────

        private void UpdateLegPhases()
        {
            // [2026-09-14(49차 후속)] gait 전환 감지 → Locomotion의 gait 오프셋으로 실제 렌더 위상 재정렬.
            // 공중에서도 즉시 동기화해 착지 직후 위상 끊김을 방지한다.
            if (_locomotion != null && _syncedGait != _locomotion.CurrentGait)
            {
                _locomotion.SyncLegPhases(this);
                _syncedGait = _locomotion.CurrentGait;
            }

            if (!_isGrounded) return;

            // [2026-09-14(49차 후속)] 실제 렌더 위상 진행에 현재 gait 배율을 적용한다.
            // 기존 고정 1.5f는 Locomotion.GetPhaseSpeed의 gait 배율(Walk 0.8/Trot 1.2/Pace 1.4/Gallop 2.0)과
            // 무관해 토끼 Gallop 고정 등 gait 조정이 write-only로 화면에 반영되지 않았던 문제를 수정.
            float gaitMultiplier = (_locomotion != null) ? _locomotion.GetGaitPhaseMultiplier() : 1.5f;
            float phaseSpeed = _currentSpeed / _stepLength * gaitMultiplier;
            float delta = phaseSpeed * Time.deltaTime;

            LF_Phase = Mathf.Repeat(LF_Phase + delta, 1f);
            RF_Phase = Mathf.Repeat(RF_Phase + delta, 1f);
            LH_Phase = Mathf.Repeat(LH_Phase + delta, 1f);
            RH_Phase = Mathf.Repeat(RH_Phase + delta, 1f);
        }

        // ──────────────────────────────────────────────
        // IK 타겟 업데이트
        // ──────────────────────────────────────────────

        private void UpdateIKTargets()
        {
            _headLookTarget = transform.position + transform.forward * 5f + Vector3.up * 1.5f;
        }

        // ──────────────────────────────────────────────
        // 프로시저럴 포즈 적용
        // ──────────────────────────────────────────────

        private void ApplyProceduralPose()
        {
            ApplyFootIK();
            // [2026-09-14(49차 후속)] ApplySpineIK 제거 — 척추 파동은 QuadrupedProceduralLocomotion.ApplySpineWave가
            // 단일 담당한다. 기존엔 이곳(하드코딩 0.05@2Hz)과 Locomotion.ApplySpineWave가 이중으로
            // 척추를 Rotate 누적해 croc 파동이 겹치고 SetSpineWave 조정이 묻혔다.
            ApplyHeadLook();
            ApplyBodyLean();
        }

        private void ApplyFootIK()
        {
            // Front Left
            if (_boneMap.Has(BoneRole.L_Hip) &&
                _boneMap.Has(BoneRole.L_Knee) &&
                _boneMap.Has(BoneRole.L_Ankle))
            {
                var chain = new Chain
                {
                    Root = _boneMap.Get(BoneRole.L_Hip),
                    Mid = _boneMap.Get(BoneRole.L_Knee),
                    Tip = _boneMap.Get(BoneRole.L_Ankle)
                };
                ComputeLengths(ref chain);
                var result = Solve(chain, LF_Target, LF_Hint);
                if (result.Success)
                {
                    chain.Root.rotation = result.RootRot;
                    chain.Mid.rotation = result.MidRot;
                    chain.Tip.rotation = result.TipRot;
                }
            }

            // Front Right
            if (_boneMap.Has(BoneRole.R_Hip) &&
                _boneMap.Has(BoneRole.R_Knee) &&
                _boneMap.Has(BoneRole.R_Ankle))
            {
                var chain = new Chain
                {
                    Root = _boneMap.Get(BoneRole.R_Hip),
                    Mid = _boneMap.Get(BoneRole.R_Knee),
                    Tip = _boneMap.Get(BoneRole.R_Ankle)
                };
                ComputeLengths(ref chain);
                var result = Solve(chain, RF_Target, RF_Hint);
                if (result.Success)
                {
                    chain.Root.rotation = result.RootRot;
                    chain.Mid.rotation = result.MidRot;
                    chain.Tip.rotation = result.TipRot;
                }
            }

            // Hind legs - reuse for now (need proper hind bone roles)
            if (_boneMap.Has(BoneRole.L_Hip) &&
                _boneMap.Has(BoneRole.L_Knee) &&
                _boneMap.Has(BoneRole.L_Ankle))
            {
                var chain = new Chain
                {
                    Root = _boneMap.Get(BoneRole.L_Hip),
                    Mid = _boneMap.Get(BoneRole.L_Knee),
                    Tip = _boneMap.Get(BoneRole.L_Ankle)
                };
                ComputeLengths(ref chain);
                var result = Solve(chain, LH_Target, LF_Hint); // reuse hint
                if (result.Success)
                {
                    chain.Root.rotation = result.RootRot;
                    chain.Mid.rotation = result.MidRot;
                    chain.Tip.rotation = result.TipRot;
                }
            }

            if (_boneMap.Has(BoneRole.R_Hip) &&
                _boneMap.Has(BoneRole.R_Knee) &&
                _boneMap.Has(BoneRole.R_Ankle))
            {
                var chain = new Chain
                {
                    Root = _boneMap.Get(BoneRole.R_Hip),
                    Mid = _boneMap.Get(BoneRole.R_Knee),
                    Tip = _boneMap.Get(BoneRole.R_Ankle)
                };
                ComputeLengths(ref chain);
                var result = Solve(chain, RH_Target, RF_Hint);
                if (result.Success)
                {
                    chain.Root.rotation = result.RootRot;
                    chain.Mid.rotation = result.MidRot;
                    chain.Tip.rotation = result.TipRot;
                }
            }
        }

        // [2026-09-14(49차 후속)] ApplySpineIK 메서드 삭제 — 척추 파동은 Locomotion.ApplySpineWave 단일 담당
        // (이중 Rotate 누적 제거). ApplyFootIK/ApplyHeadLook/ApplyBodyLean 등 기타 포즈 로직은 보존.

        private void ApplyHeadLook()
        {
            var head = _boneMap.Get(BoneRole.Head);
            if (head == null) return;

            Vector3 toTarget = (_headLookTarget - head.position).normalized;
            Quaternion targetRot = Quaternion.LookRotation(toTarget, Vector3.up);
            head.rotation = Quaternion.Slerp(head.rotation, targetRot, _headLookWeight * Time.deltaTime * 10f);
        }

        private void ApplyBodyLean()
        {
            var root = _boneMap.Get(BoneRole.Root);
            if (root != null)
                root.localRotation = _bodyLeanRotation;
        }

        // ──────────────────────────────────────────────
        // Animator IK
        // ──────────────────────────────────────────────

        private void ApplyIKToAnimator()
        {
            if (_lfGrounded)
            {
                _animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, _footIKWeight);
                _animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, _footIKWeight);
                _animator.SetIKPosition(AvatarIKGoal.LeftFoot, LF_Target);
                _animator.SetIKHintPositionWeight(AvatarIKHint.LeftKnee, _footIKWeight);
                _animator.SetIKHintPosition(AvatarIKHint.LeftKnee, LF_Hint);
            }
            if (_rfGrounded)
            {
                _animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, _footIKWeight);
                _animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, _footIKWeight);
                _animator.SetIKPosition(AvatarIKGoal.RightFoot, RF_Target);
                _animator.SetIKHintPositionWeight(AvatarIKHint.RightKnee, _footIKWeight);
                _animator.SetIKHintPosition(AvatarIKHint.RightKnee, RF_Hint);
            }
        }

        // ──────────────────────────────────────────────
        // 액션 요청
        // ──────────────────────────────────────────────

        public void RequestJump()
        {
            if (!_isGrounded && _coyoteTimer <= 0) return;
            if (_actionState != ActionState.None) return;

            float jumpVelocity = Mathf.Sqrt(-2f * _gravity * _jumpHeight);
            _rigidbody.linearVelocity = new Vector3(_rigidbody.linearVelocity.x, jumpVelocity, _rigidbody.linearVelocity.z);
            _coyoteTimer = 0;
        }

        public void RequestAttack(Vector3? target = null)
        {
            if (_actionState != ActionState.None) return;
            _actionState = ActionState.Attack;
            _actionTimer = 0f;
            _actionTarget = target ?? (transform.position + transform.forward * 2f);
        }

        // ──────────────────────────────────────────────
        // 디버그
        // ──────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            if (_lfGrounded) Gizmos.color = Color.green; else Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(LF_Target, 0.08f);
            if (_rfGrounded) Gizmos.color = Color.green; else Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(RF_Target, 0.08f);
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(LH_Target, 0.08f);
            Gizmos.DrawWireSphere(RH_Target, 0.08f);
        }
    }
}