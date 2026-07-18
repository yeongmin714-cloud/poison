using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using ProjectName.Systems.Animation.Procedural.Bones;
using ProjectName.Systems.Animation.Procedural.Locomotion.Biped;
using ProjectName.Systems.Animation.Procedural.Locomotion.Quadruped;
using ProjectName.Systems.Animation.Procedural.Actions;
using ProjectName.Systems.Animation.Procedural.LOD;
using ProjectName.Systems.Animation.Procedural.IK;
using static ProjectName.Systems.Animation.Procedural.IK.LimbIKSolver;

namespace ProjectName.Systems.Animation.Procedural
{
    /// <summary>
    /// 외부 이동 시스템(CharacterController 등)에서 현재 속도를 제공하는 인터페이스.
    /// ProceduralAnimationController가 자체 입력 대신 외부 속도를 사용하도록 함.
    /// </summary>
    public interface IVelocityProvider
    {
        Vector3 CurrentVelocity { get; }
        float CurrentSpeed { get; }
        bool IsGrounded { get; }
    }
    /// <summary>
    /// 완전 프로시저럴 애니메이션 컨트롤러 (모듈 합성 버전).
    /// - 애니메이션 클립(.anim) 전혀 사용 안 함
    /// - 모든 모션: Locomotion(보행/달리기), Jump, Attack, Gather, Roll, Climb 등을 수학적으로 실시간 합성
    /// - Job System + Burst로 병렬 처리
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(ProceduralBoneMap))]
    public class ProceduralAnimationController : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        // 인스펙터 설정
        // ──────────────────────────────────────────────

        [Header("Locomotion")]
        [SerializeField, Range(0f, 10f)] float walkSpeed = 3f;
        [SerializeField, Range(0f, 15f)] float runSpeed = 7f;
        [SerializeField, Range(5f, 30f)] float acceleration = 15f;
        [SerializeField, Range(360f, 1080f)] float turnSpeed = 720f;

        [Header("Jump")]
        [SerializeField, Range(0.5f, 5f)] float jumpHeight = 2.5f;
        [SerializeField, Range(-10f, -50f)] float gravity = -25f;
        [SerializeField, Range(0f, 0.2f)] float coyoteTime = 0.1f;

        [Header("IK Weights")]
        [SerializeField, Range(0f, 1f)] float footIKWeight = 1f;
        [SerializeField, Range(0f, 1f)] float handIKWeight = 1f;
        [SerializeField, Range(0f, 1f)] float spineIKWeight = 0.8f;
        [SerializeField, Range(0f, 1f)] float headLookWeight = 0.7f;

        [Header("Procedural Modifiers")]
        [SerializeField, Range(0f, 1f)] float bodyLeanAmount = 0.6f;
        [SerializeField, Range(0f, 1f)] float armSwingAmount = 0.8f;
        [SerializeField, Range(0f, 1f)] float headStabilization = 0.5f;

        [Header("Ground Check")]
        [SerializeField] LayerMask groundMask = ~0;
        [SerializeField, Range(0.5f, 2f)] float groundCheckDistance = 1.2f;

        // ──────────────────────────────────────────────
        // 컴포넌트
        // ──────────────────────────────────────────────

        Animator _animator;
        Rigidbody _rigidbody;
        ProceduralBoneMap _boneMap;
        ProceduralAnimStateMachine _stateMachine;
        ProceduralLODManager _lodManager;

        // 외부 속도 공급자 (PlayerMovement 등 CharacterController 기반 이동 시스템)
        IVelocityProvider _velocityProvider;

        // ──────────────────────────────────────────────
        // 공개 API
        // ──────────────────────────────────────────────

        /// <summary>
        /// 외부 이동 시스템에서 현재 속도를 제공받도록 설정.
        /// 설정 시 HandleInput에서 자체 입력 대신 외부 속도 사용.
        /// </summary>
        public void SetVelocityProvider(IVelocityProvider provider)
        {
            _velocityProvider = provider;
        }

        /// <summary>
        /// 외부에서 본 맵을 직접 주입 (ModelAnimatorAssigner에서 호출).
        /// Awake보다 먼저 호출되어야 함.
        /// </summary>
        public void SetBoneMap(ProceduralBoneMap boneMap)
        {
            _boneMap = boneMap;
        }

        // ──────────────────────────────────────────────
        // 네이티브 배열 (Job System용)
        // ──────────────────────────────────────────────

        NativeArray<float3> _leftFootPos, _rightFootPos, _leftHandPos, _rightHandPos;
        NativeArray<float3> _leftFootTarget, _rightFootTarget, _leftHandTarget, _rightHandTarget;
        NativeArray<float3> _leftFootHint, _rightFootHint, _leftHandHint, _rightHandHint;
        NativeArray<quaternion> _spineRotations;
        NativeArray<float3> _hipOffset;
        NativeArray<float> _hipHeightOffset;
        NativeArray<float> _leftLegPhaseArr, _rightLegPhaseArr;
        NativeArray<float> _phaseSpeedArr;
        NativeArray<bool> _leftFootGroundedArr, _rightFootGroundedArr;
        NativeArray<float3> _headLookTargetArr;

        JobHandle _locomotionJobHandle;
        JobHandle _ikJobHandle;

        // ──────────────────────────────────────────────
        // 런타임 상태
        // ──────────────────────────────────────────────

        Vector3 _currentVelocity;
        Vector3 _targetVelocity;
        float _currentSpeed;
        float _targetSpeed;
        float _coyoteTimer;

        float _leftLegPhase = 0f;
        float _rightLegPhase = 0.5f;
        const float _dutyCycle = 0.6f;

        RaycastHit _leftFootHit, _rightFootHit;
        bool _leftFootGrounded, _rightFootGrounded;

        Vector3 _bodyLeanOffset;
        Quaternion _bodyLeanRotation = Quaternion.identity;
        Vector3 _headLookTarget;

        ActionState _actionState = ActionState.None;
        float _actionTimer;
        Vector3 _actionTarget;

        public enum ActionState { None, Attack, Gather, Roll, Climb, Stagger }

        // ──────────────────────────────────────────────
        // 공개 속성 (StateMachine 등에서 사용)
        // ──────────────────────────────────────────────

        public Vector3 CurrentVelocity => _currentVelocity;
        public float CurrentSpeed => _currentSpeed;
        public bool IsGrounded => _velocityProvider?.IsGrounded ?? (_leftFootGrounded || _rightFootGrounded);
        public ProceduralAnimStateMachine StateMachine => _stateMachine;
        public float JumpHeight => jumpHeight;
        public float JumpGravity => gravity;
        public Vector3 CurrentActionTarget => _actionTarget;

        public float GetJumpGravity() => gravity;
        public float GetJumpHeight() => jumpHeight;

        // ──────────────────────────────────────────────
        // 공개 API (StateMachine/외부에서 호출)
        // ──────────────────────────────────────────────

        public void TriggerAction(string actionName)
        {
            switch (actionName.ToLower())
            {
                case "jump": RequestJump(); break;
                case "attack": RequestAttack(); break;
                case "gather": RequestGather(); break;
                case "roll": RequestRoll(); break;
                case "climb": RequestClimb(); break;
            }
        }

        public void RequestJump()
        {
            if (!IsGrounded && _coyoteTimer <= 0) return;
            if (_actionState != ActionState.None) return;

            float jumpVel = math.sqrt(-2f * gravity * jumpHeight);
            _rigidbody.linearVelocity = new Vector3(_rigidbody.linearVelocity.x, jumpVel, _rigidbody.linearVelocity.z);
            _coyoteTimer = 0f;
        }

        public void RequestAttack(Vector3? target = null)
        {
            if (_actionState != ActionState.None) return;
            _actionState = ActionState.Attack;
            _actionTimer = 0f;
            _actionTarget = target ?? (transform.position + transform.forward * 2f);
        }

        public void RequestGather(Vector3? target = null)
        {
            if (_actionState != ActionState.None) return;
            _actionState = ActionState.Gather;
            _actionTimer = 0f;
            _actionTarget = target ?? (transform.position + transform.forward * 1.5f);
        }

        public void RequestRoll()
        {
            if (_actionState != ActionState.None) return;
            if (!IsGrounded) return;

            _actionState = ActionState.Roll;
            _actionTimer = 0f;
            Vector3 dir = _currentVelocity.magnitude > 0.1f ? _currentVelocity.normalized : transform.forward;
            _rigidbody.AddForce(dir * 15f, ForceMode.VelocityChange);
        }

        public void RequestClimb()
        {
            _actionState = ActionState.Climb;
            _actionTimer = 0f;
        }

        // ──────────────────────────────────────────────
        // Unity Lifecycle
        // ──────────────────────────────────────────────

        void Awake()
        {
            _animator = GetComponent<Animator>();
            _rigidbody = GetComponent<Rigidbody>();
            _boneMap = GetComponent<ProceduralBoneMap>();
            _stateMachine = GetComponent<ProceduralAnimStateMachine>();
            _lodManager = FindAnyObjectByType<ProceduralLODManager>();

            if (_stateMachine == null)
                _stateMachine = gameObject.AddComponent<ProceduralAnimStateMachine>();

            _animator.applyRootMotion = false;
            _animator.updateMode = AnimatorUpdateMode.Fixed;
            _animator.animatePhysics = true;

            _rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            _boneMap.Initialize(_animator);
            AllocateNativeArrays();
        }

        void AllocateNativeArrays()
        {
            int spineCount = 3;

            _leftFootPos = new NativeArray<float3>(1, Allocator.Persistent);
            _rightFootPos = new NativeArray<float3>(1, Allocator.Persistent);
            _leftHandPos = new NativeArray<float3>(1, Allocator.Persistent);
            _rightHandPos = new NativeArray<float3>(1, Allocator.Persistent);

            _leftFootTarget = new NativeArray<float3>(1, Allocator.Persistent);
            _rightFootTarget = new NativeArray<float3>(1, Allocator.Persistent);
            _leftHandTarget = new NativeArray<float3>(1, Allocator.Persistent);
            _rightHandTarget = new NativeArray<float3>(1, Allocator.Persistent);

            _leftFootHint = new NativeArray<float3>(1, Allocator.Persistent);
            _rightFootHint = new NativeArray<float3>(1, Allocator.Persistent);
            _leftHandHint = new NativeArray<float3>(1, Allocator.Persistent);
            _rightHandHint = new NativeArray<float3>(1, Allocator.Persistent);

            _spineRotations = new NativeArray<quaternion>(spineCount, Allocator.Persistent);
            _hipOffset = new NativeArray<float3>(1, Allocator.Persistent);
            _hipHeightOffset = new NativeArray<float>(1, Allocator.Persistent);
            _leftLegPhaseArr = new NativeArray<float>(1, Allocator.Persistent);
            _rightLegPhaseArr = new NativeArray<float>(1, Allocator.Persistent);
            _phaseSpeedArr = new NativeArray<float>(1, Allocator.Persistent);
            _leftFootGroundedArr = new NativeArray<bool>(1, Allocator.Persistent);
            _rightFootGroundedArr = new NativeArray<bool>(1, Allocator.Persistent);
            _headLookTargetArr = new NativeArray<float3>(1, Allocator.Persistent);
        }

        void OnDestroy()
        {
            JobHandle.ScheduleBatchedJobs();
            _locomotionJobHandle.Complete();
            _ikJobHandle.Complete();

            _leftFootPos.Dispose();
            _rightFootPos.Dispose();
            _leftHandPos.Dispose();
            _rightHandPos.Dispose();
            _leftFootTarget.Dispose();
            _rightFootTarget.Dispose();
            _leftHandTarget.Dispose();
            _rightHandTarget.Dispose();
            _leftFootHint.Dispose();
            _rightFootHint.Dispose();
            _leftHandHint.Dispose();
            _rightHandHint.Dispose();
            _spineRotations.Dispose();
            _hipOffset.Dispose();
            _hipHeightOffset.Dispose();
            _leftLegPhaseArr.Dispose();
            _rightLegPhaseArr.Dispose();
            _phaseSpeedArr.Dispose();
            _leftFootGroundedArr.Dispose();
            _rightFootGroundedArr.Dispose();
            _headLookTargetArr.Dispose();
        }

        void Start()
        {
            InitializeIKTargets();
        }

        void InitializeIKTargets()
        {
            var lFoot = _boneMap.Get(BoneRole.L_Foot);
            var rFoot = _boneMap.Get(BoneRole.R_Foot);
            var lHand = _boneMap.Get(BoneRole.L_Hand);
            var rHand = _boneMap.Get(BoneRole.R_Hand);

            if (lFoot != null) _leftFootTarget[0] = lFoot.position;
            if (rFoot != null) _rightFootTarget[0] = rFoot.position;
            if (lHand != null) _leftHandTarget[0] = lHand.position;
            if (rHand != null) _rightHandTarget[0] = rHand.position;

            var lKnee = _boneMap.Get(BoneRole.L_Knee);
            var rKnee = _boneMap.Get(BoneRole.R_Knee);
            var lElbow = _boneMap.Get(BoneRole.L_Elbow);
            var rElbow = _boneMap.Get(BoneRole.R_Elbow);

            if (lKnee != null) _leftFootHint[0] = lKnee.position + transform.right * 0.3f;
            if (rKnee != null) _rightFootHint[0] = rKnee.position - transform.right * 0.3f;
            if (lElbow != null) _leftHandHint[0] = lElbow.position + transform.forward * 0.3f;
            if (rElbow != null) _rightHandHint[0] = rElbow.position + transform.forward * 0.3f;
        }

        void Update()
        {
            HandleInput();
            UpdateMovement();
            UpdateStateMachine();
            UpdateCoyoteTime();
            UpdateLegPhases();
            UpdateHeadLookTarget();

            ScheduleLocomotionJobs();
        }

        void FixedUpdate()
        {
            ApplyMovement();
            ApplyGravity();
        }

        void LateUpdate()
        {
            _locomotionJobHandle.Complete();
            UpdateGroundDetection();
            ScheduleIKJobs();
            ApplyProceduralPose();
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0) return;
            _ikJobHandle.Complete();
            ApplyIKToAnimator();
        }

        // ──────────────────────────────────────────────
        // 입력 & 이동
        // ──────────────────────────────────────────────

        void HandleInput()
        {
            if (_actionState != ActionState.None) return;

            // 외부 속도 공급자가 있으면 자체 입력 대신 외부 속도 사용
            if (_velocityProvider != null)
            {
                _targetVelocity = _velocityProvider.CurrentVelocity;
                _targetSpeed = _velocityProvider.CurrentSpeed;
                // 회전은 목표 속도 방향으로
                if (_targetVelocity.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(_targetVelocity.normalized);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
                }
            }
            else
            {
                // 기존 자체 입력 처리
                Vector2 input = Vector2.zero;
                if (Input.GetKey(KeyCode.W)) input.y += 1;
                if (Input.GetKey(KeyCode.S)) input.y -= 1;
                if (Input.GetKey(KeyCode.A)) input.x -= 1;
                if (Input.GetKey(KeyCode.D)) input.x += 1;
                input = Vector2.ClampMagnitude(input, 1f);

                bool sprint = Input.GetKey(KeyCode.LeftShift);

                Vector3 localTarget = new Vector3(input.x, 0, input.y);
                _targetVelocity = transform.TransformDirection(localTarget) * (sprint ? runSpeed : walkSpeed);
                _targetSpeed = _targetVelocity.magnitude;

                if (_targetVelocity.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(_targetVelocity.normalized);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
                }
            }

            if (Input.GetMouseButtonDown(0)) RequestAttack();
            if (Input.GetKeyDown(KeyCode.Space)) RequestJump();
            if (Input.GetKeyDown(KeyCode.E)) RequestGather();
            if (Input.GetKeyDown(KeyCode.Q)) RequestRoll();
        }

        void UpdateMovement()
        {
            _currentVelocity = Vector3.MoveTowards(_currentVelocity, _targetVelocity, acceleration * Time.deltaTime);
            _currentSpeed = _currentVelocity.magnitude;

            float turnInput = Input.GetKey(KeyCode.A) ? -1f : (Input.GetKey(KeyCode.D) ? 1f : 0f);
            float targetLean = turnInput * bodyLeanAmount * 15f;
            _bodyLeanOffset = Vector3.Lerp(_bodyLeanOffset, new Vector3(targetLean, 0, 0), Time.deltaTime * 5f);
            _bodyLeanRotation = Quaternion.Lerp(_bodyLeanRotation, Quaternion.Euler(_bodyLeanOffset), Time.deltaTime * 5f);
        }

        void ApplyMovement()
        {
            if (!IsGrounded) return;

            Vector3 move = _currentVelocity * Time.fixedDeltaTime;
            move.y = _rigidbody.linearVelocity.y;
            _rigidbody.linearVelocity = move;
        }

        void ApplyGravity()
        {
            if (!IsGrounded)
            {
                _rigidbody.AddForce(Vector3.up * gravity * _rigidbody.mass, ForceMode.Force);
            }
        }

        void UpdateCoyoteTime()
        {
            if (IsGrounded) _coyoteTimer = coyoteTime;
            else _coyoteTimer -= Time.deltaTime;
        }

        // ──────────────────────────────────────────────
        // 상태 머신
        // ──────────────────────────────────────────────

        void UpdateStateMachine()
        {
            var state = _stateMachine.CurrentState;

            switch (state)
            {
                case ProceduralAnimStateMachine.State.Jump:
                    if (_actionState != ActionState.None) break;
                    _actionState = ActionState.None;
                    break;

                case ProceduralAnimStateMachine.State.Attack:
                    UpdateActionAttack();
                    break;

                case ProceduralAnimStateMachine.State.Gather:
                    UpdateActionGather();
                    break;

                case ProceduralAnimStateMachine.State.Roll:
                    UpdateActionRoll();
                    break;

                case ProceduralAnimStateMachine.State.Climb:
                    UpdateActionClimb();
                    break;
            }
        }

        void UpdateActionAttack()
        {
            _actionTimer += Time.deltaTime;
            float progress = math.clamp(_actionTimer / 0.8f, 0f, 1f);
            float swing = math.sin(progress * math.PI) * 90f;

            var rHand = _boneMap.Get(BoneRole.R_Hand);
            var rElbow = _boneMap.Get(BoneRole.R_Elbow);

            if (rHand != null && rElbow != null)
            {
                Vector3 swingDir = (_actionTarget - rHand.position).normalized;
                _rightHandTarget[0] = Vector3.Lerp(_rightHandTarget[0], _actionTarget, progress * 2f);
                _rightHandHint[0] = rElbow.position + swingDir * 0.5f;
            }

            if (_actionTimer > 0.8f)
                _actionState = ActionState.None;
        }

        void UpdateActionGather()
        {
            _actionTimer += Time.deltaTime;
            float progress = math.clamp(_actionTimer / 1.5f, 0f, 1f);

            var lHand = _boneMap.Get(BoneRole.L_Hand);
            var rHand = _boneMap.Get(BoneRole.R_Hand);

            if (lHand != null)
                _leftHandTarget[0] = Vector3.Lerp(_leftHandTarget[0], _actionTarget, progress);
            if (rHand != null)
                _rightHandTarget[0] = Vector3.Lerp(_rightHandTarget[0], _actionTarget, progress);

            float bend = progress * 45f;
            _bodyLeanOffset = Vector3.Lerp(_bodyLeanOffset, new Vector3(0, 0, bend), Time.deltaTime * 3f);

            if (_actionTimer > 1.5f)
                _actionState = ActionState.None;
        }

        void UpdateActionRoll()
        {
            _actionTimer += Time.deltaTime;
            float progress = math.clamp(_actionTimer / 0.6f, 0f, 1f);
            float rollAngle = progress * 360f;
            transform.Rotate(Vector3.forward, rollAngle * Time.deltaTime / 0.6f);

            if (_actionTimer > 0.6f)
            {
                _actionState = ActionState.None;
                transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
            }
        }

        void UpdateActionClimb()
        {
            _actionTimer += Time.deltaTime;
        }

        // ──────────────────────────────────────────────
        // Job Scheduling
        // ──────────────────────────────────────────────

        void UpdateLegPhases()
        {
            if (!IsGrounded) return;

            float speedRatio = _currentSpeed / runSpeed;
            _phaseSpeedArr[0] = math.lerp(0.5f, 2.5f, speedRatio);

            float phaseDelta = _phaseSpeedArr[0] * Time.deltaTime;
            _leftLegPhase = math.fmod(_leftLegPhase + phaseDelta, 1f);
            _rightLegPhase = math.fmod(_rightLegPhase + phaseDelta, 1f);
            _leftLegPhaseArr[0] = _leftLegPhase;
            _rightLegPhaseArr[0] = _rightLegPhase;
        }

        void UpdateHeadLookTarget()
        {
            _headLookTarget = transform.position + transform.forward * 5f + Vector3.up * 1.5f;
            if (_actionState == ActionState.Attack || _actionState == ActionState.Gather)
            {
                _headLookTarget = _actionTarget;
            }
            _headLookTargetArr[0] = _headLookTarget;
        }

        void ScheduleLocomotionJobs(int lod = 0)
        {
            JobHandle dependency = default;

            bool computeHipShift = lod <= 1;
            bool computeSpineCounter = lod <= 1;
            bool computeFootIK = lod <= 2;
            int ikIterations = lod == 0 ? 2 : 1;

            // Foot Planner
            var footPlanner = new FootPlannerJob
            {
                BodyPosition = transform.position,
                BodyRotation = transform.rotation,
                BodyVelocity = _currentVelocity,
                BodyAngularVelocity = _rigidbody.angularVelocity,
                DeltaTime = Time.deltaTime,
                StepLength = 0.8f,
                StepWidth = 0.2f,
                MaxStepHeight = 0.3f,
                GroundCheckDistance = groundCheckDistance,
                LeftFootCurrent = _leftFootTarget[0],
                RightFootCurrent = _rightFootTarget[0],
                LeftFootGrounded = _leftFootGrounded,
                RightFootGrounded = _rightFootGrounded,
                LeftPhase = _leftLegPhase,
                RightPhase = _rightLegPhase,
                DutyCycle = _dutyCycle,
                Speed = _currentSpeed,

                OutLeftTarget = _leftFootTarget,
                OutRightTarget = _rightFootTarget,
                OutLeftHint = _leftFootHint,
                OutRightHint = _rightFootHint,
                OutLeftGroundPos = _leftFootPos,
                OutRightGroundPos = _rightFootPos,
                OutLeftCanStep = new NativeArray<bool>(1, Allocator.TempJob),
                OutRightCanStep = new NativeArray<bool>(1, Allocator.TempJob),
            };
            dependency = footPlanner.Schedule(dependency);

            // Hip Shift
            if (computeHipShift)
            {
                var hipShift = new HipShiftJob
                {
                    LeftPhase = _leftLegPhase,
                    RightPhase = _rightLegPhase,
                    DutyCycle = _dutyCycle,
                    LeftWeight = _leftFootGrounded ? 1f : 0f,
                    RightWeight = _rightFootGrounded ? 1f : 0f,
                    MaxLateralShift = 0.1f,
                    MaxVerticalShift = 0.05f,
                    Speed = _currentSpeed,
                    TurnAmount = Input.GetKey(KeyCode.A) ? -1f : (Input.GetKey(KeyCode.D) ? 1f : 0f),

                    OutHipOffset = _hipOffset,
                    OutHipHeightOffset = _hipHeightOffset,
                };
                dependency = hipShift.Schedule(dependency);
            }

            // Spine Counter-Rotation
            if (computeSpineCounter)
            {
                var spineCounter = new SpineCounterRotationJob
                {
                    LeftPhase = _leftLegPhase,
                    RightPhase = _rightLegPhase,
                    DutyCycle = _dutyCycle,
                    MaxCounterRotation = 8f,
                    BodyVelocity = _currentVelocity,
                    BodyRotation = transform.rotation,
                    SpineSegmentCount = 3,

                    OutSpineRotations = _spineRotations,
                };
                dependency = spineCounter.Schedule(dependency);
            }

            _locomotionJobHandle = dependency;
        }

        void ScheduleIKJobs()
        {
            JobHandle dependency = _locomotionJobHandle;
            UpdateBonePositions();

            // Left Leg IK
            if (_boneMap.Has(BoneRole.L_Hip) && _boneMap.Has(BoneRole.L_Knee) && _boneMap.Has(BoneRole.L_Ankle))
            {
                var chain = new Chain
                {
                    Root = _boneMap.Get(BoneRole.L_Hip),
                    Mid = _boneMap.Get(BoneRole.L_Knee),
                    Tip = _boneMap.Get(BoneRole.L_Ankle),
                };
                ComputeLengths(ref chain);

                var leftIK = new LimbIKSingleJob
                {
                    RootPos = chain.Root.position,
                    MidPos = chain.Mid.position,
                    TipPos = chain.Tip.position,
                    TargetPos = _leftFootTarget[0],
                    HintPos = _leftFootHint[0],
                    UpperLen = chain.UpperLength,
                    LowerLen = chain.LowerLength,
                    Iterations = 2,
                };
                var leftHandle = leftIK.Schedule(dependency);
                _leftIKHandles.Add(leftHandle);
                dependency = leftHandle;
            }

            // Right Leg IK
            if (_boneMap.Has(BoneRole.R_Hip) && _boneMap.Has(BoneRole.R_Knee) && _boneMap.Has(BoneRole.R_Ankle))
            {
                var chain = new Chain
                {
                    Root = _boneMap.Get(BoneRole.R_Hip),
                    Mid = _boneMap.Get(BoneRole.R_Knee),
                    Tip = _boneMap.Get(BoneRole.R_Ankle),
                };
                ComputeLengths(ref chain);

                var rightIK = new LimbIKSingleJob
                {
                    RootPos = chain.Root.position,
                    MidPos = chain.Mid.position,
                    TipPos = chain.Tip.position,
                    TargetPos = _rightFootTarget[0],
                    HintPos = _rightFootHint[0],
                    UpperLen = chain.UpperLength,
                    LowerLen = chain.LowerLength,
                    Iterations = 2,
                };
                var rightHandle = rightIK.Schedule(dependency);
                _rightIKHandles.Add(rightHandle);
                dependency = rightHandle;
            }

            _ikJobHandle = dependency;
        }

        List<JobHandle> _leftIKHandles = new List<JobHandle>();
        List<JobHandle> _rightIKHandles = new List<JobHandle>();

        void UpdateBonePositions()
        {
            var lFoot = _boneMap.Get(BoneRole.L_Foot);
            var rFoot = _boneMap.Get(BoneRole.R_Foot);
            var lHand = _boneMap.Get(BoneRole.L_Hand);
            var rHand = _boneMap.Get(BoneRole.R_Hand);

            if (lFoot != null) _leftFootPos[0] = lFoot.position;
            if (rFoot != null) _rightFootPos[0] = rFoot.position;
            if (lHand != null) _leftHandPos[0] = lHand.position;
            if (rHand != null) _rightHandPos[0] = rHand.position;
        }

        // ──────────────────────────────────────────────
        // Ground Detection
        // ──────────────────────────────────────────────

        void UpdateGroundDetection()
        {
            var lFoot = _boneMap.Get(BoneRole.L_Foot);
            var rFoot = _boneMap.Get(BoneRole.R_Foot);

            _leftFootGrounded = false;
            _rightFootGrounded = false;

            if (lFoot != null)
            {
                Vector3 origin = lFoot.position + Vector3.up * 0.2f;
                if (Physics.Raycast(origin, Vector3.down, out _leftFootHit, groundCheckDistance, groundMask))
                {
                    _leftFootGrounded = true;
                    _leftFootTarget[0] = _leftFootHit.point + Vector3.up * 0.02f;
                }
            }

            if (rFoot != null)
            {
                Vector3 origin = rFoot.position + Vector3.up * 0.2f;
                if (Physics.Raycast(origin, Vector3.down, out _rightFootHit, groundCheckDistance, groundMask))
                {
                    _rightFootGrounded = true;
                    _rightFootTarget[0] = _rightFootHit.point + Vector3.up * 0.02f;
                }
            }

            _leftFootGroundedArr[0] = _leftFootGrounded;
            _rightFootGroundedArr[0] = _rightFootGrounded;
        }

        // ──────────────────────────────────────────────
        // 프로시저럴 포즈 적용 (메인 스레드)
        // ──────────────────────────────────────────────

        void ApplyProceduralPose()
        {
            ApplyFootIK();
            ApplySpineIK();
            ApplyHeadLook();
            ApplyBodyLean();
            ApplyHipShift();
        }

        void ApplyFootIK()
        {
            // Complete and process left leg IK
            foreach (var handle in _leftIKHandles)
            {
                handle.Complete();
            }
            _leftIKHandles.Clear();

            // Complete and process right leg IK
            foreach (var handle in _rightIKHandles)
            {
                handle.Complete();
            }
            _rightIKHandles.Clear();

            // Note: With IJob structs, results are written directly to the output fields
            // when the job completes. Since we use the same LimbIKSingleJob struct
            // for scheduling, the output fields (OutRootRot, OutMidRot, OutTipRot)
            // are populated when the job completes.
            
            // Apply the results - we need to store the job structs to access outputs
            // For simplicity, apply rotation directly in the job or use main-thread IK as fallback
            ApplyFootIKMainThread();
        }

        void ApplyFootIKMainThread()
        {
            // Left leg
            if (_boneMap.Has(BoneRole.L_Hip) && _boneMap.Has(BoneRole.L_Knee) && _boneMap.Has(BoneRole.L_Ankle))
            {
                var chain = new Chain
                {
                    Root = _boneMap.Get(BoneRole.L_Hip),
                    Mid = _boneMap.Get(BoneRole.L_Knee),
                    Tip = _boneMap.Get(BoneRole.L_Ankle),
                };
                ComputeLengths(ref chain);
                var result = LimbIKSolver.Solve(chain, _leftFootTarget[0], _leftFootHint[0]);
                if (result.Success)
                {
                    chain.Root.rotation = result.RootRot;
                    chain.Mid.rotation = result.MidRot;
                    chain.Tip.rotation = result.TipRot;
                }
            }

            // Right leg
            if (_boneMap.Has(BoneRole.R_Hip) && _boneMap.Has(BoneRole.R_Knee) && _boneMap.Has(BoneRole.R_Ankle))
            {
                var chain = new Chain
                {
                    Root = _boneMap.Get(BoneRole.R_Hip),
                    Mid = _boneMap.Get(BoneRole.R_Knee),
                    Tip = _boneMap.Get(BoneRole.R_Ankle),
                };
                ComputeLengths(ref chain);
                var result = LimbIKSolver.Solve(chain, _rightFootTarget[0], _rightFootHint[0]);
                if (result.Success)
                {
                    chain.Root.rotation = result.RootRot;
                    chain.Mid.rotation = result.MidRot;
                    chain.Tip.rotation = result.TipRot;
                }
            }
        }

        void ApplySpineIK()
        {
            if (!_boneMap.Has(BoneRole.Spine0) || !_boneMap.Has(BoneRole.Spine1) || !_boneMap.Has(BoneRole.Spine2))
                return;

            var spine0 = _boneMap.Get(BoneRole.Spine0);
            var spine1 = _boneMap.Get(BoneRole.Spine1);
            var spine2 = _boneMap.Get(BoneRole.Spine2);
            var head = _boneMap.Get(BoneRole.Head);

            if (spine0 == null || spine1 == null || spine2 == null || head == null) return;

            Vector3 toTarget = (_headLookTarget - head.position).normalized;
            Vector3 forward = head.forward;

            float angle = Vector3.SignedAngle(forward, toTarget, Vector3.up);
            angle = Mathf.Clamp(angle, -30f, 30f) * spineIKWeight * 0.5f;

            spine0.Rotate(Vector3.up, angle * 0.2f, Space.World);
            spine1.Rotate(Vector3.up, angle * 0.5f, Space.World);
            spine2.Rotate(Vector3.up, angle * 0.3f, Space.World);
        }

        void ApplyHeadLook()
        {
            var head = _boneMap.Get(BoneRole.Head);
            if (head == null) return;

            Vector3 toTarget = (_headLookTarget - head.position).normalized;
            Quaternion targetRot = Quaternion.LookRotation(toTarget, Vector3.up);
            head.rotation = Quaternion.Slerp(head.rotation, targetRot, headLookWeight * Time.deltaTime * 10f);
        }

        void ApplyBodyLean()
        {
            var root = _boneMap.Get(BoneRole.Root);
            if (root != null)
                root.localRotation = _bodyLeanRotation;
        }

        void ApplyHipShift()
        {
            var hip = _boneMap.Get(BoneRole.Hip);
            if (hip != null)
            {
                hip.localPosition += (Vector3)_hipOffset[0] + Vector3.up * _hipHeightOffset[0];
            }
        }

        // ──────────────────────────────────────────────
        // Animator IK
        // ──────────────────────────────────────────────

        void ApplyIKToAnimator()
        {
            if (_leftFootGrounded)
            {
                _animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, footIKWeight);
                _animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, footIKWeight);
                _animator.SetIKPosition(AvatarIKGoal.LeftFoot, _leftFootTarget[0]);
                _animator.SetIKRotation(AvatarIKGoal.LeftFoot, Quaternion.LookRotation(Vector3.up, _leftFootHit.normal));
                _animator.SetIKHintPositionWeight(AvatarIKHint.LeftKnee, footIKWeight);
                _animator.SetIKHintPosition(AvatarIKHint.LeftKnee, _leftFootHint[0]);
            }

            if (_rightFootGrounded)
            {
                _animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, footIKWeight);
                _animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, footIKWeight);
                _animator.SetIKPosition(AvatarIKGoal.RightFoot, _rightFootTarget[0]);
                _animator.SetIKRotation(AvatarIKGoal.RightFoot, Quaternion.LookRotation(Vector3.up, _rightFootHit.normal));
                _animator.SetIKHintPositionWeight(AvatarIKHint.RightKnee, footIKWeight);
                _animator.SetIKHintPosition(AvatarIKHint.RightKnee, _rightFootHint[0]);
            }

            if (_actionState == ActionState.Attack || _actionState == ActionState.Gather || _actionState == ActionState.Climb)
            {
                _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, handIKWeight);
                _animator.SetIKPosition(AvatarIKGoal.LeftHand, _leftHandTarget[0]);
                _animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, handIKWeight);
                _animator.SetIKHintPosition(AvatarIKHint.LeftElbow, _leftHandHint[0]);

                _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, handIKWeight);
                _animator.SetIKPosition(AvatarIKGoal.RightHand, _rightHandTarget[0]);
                _animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, handIKWeight);
                _animator.SetIKHintPosition(AvatarIKHint.RightElbow, _rightHandHint[0]);
            }
        }

        // ──────────────────────────────────────────────
        // Debug
        // ──────────────────────────────────────────────

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            if (_leftFootGrounded) Gizmos.DrawWireSphere(_leftFootTarget[0], 0.1f);
            if (_rightFootGrounded) Gizmos.DrawWireSphere(_rightFootTarget[0], 0.1f);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_leftHandTarget[0], 0.08f);
            Gizmos.DrawWireSphere(_rightHandTarget[0], 0.08f);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_headLookTarget, 0.15f);
        }
    }

    // ──────────────────────────────────────────────
    // Single-instance IK Job (IJob)
    // ──────────────────────────────────────────────

    [BurstCompile]
    public struct LimbIKSingleJob : IJob
    {
        [ReadOnly] public float3 RootPos;
        [ReadOnly] public float3 MidPos;
        [ReadOnly] public float3 TipPos;
        [ReadOnly] public float3 TargetPos;
        [ReadOnly] public float3 HintPos;
        [ReadOnly] public float UpperLen;
        [ReadOnly] public float LowerLen;
        [ReadOnly] public int Iterations;

        [WriteOnly] public quaternion OutRootRot;
        [WriteOnly] public quaternion OutMidRot;
        [WriteOnly] public quaternion OutTipRot;

        public void Execute()
        {
            // Inline IK solve (Burst-compatible, adapted from LimbIKSolver.Solve)
            float3 rootPos = RootPos;
            float3 midPos = MidPos;
            float3 tipPos = TipPos;
            float3 target = TargetPos;
            float3 hint = HintPos;

            float upperLen = UpperLen;
            float lowerLen = LowerLen;
            float totalLen = upperLen + lowerLen;

            float3 rootToTarget = target - rootPos;
            float distToTarget = math.length(rootToTarget);

            bool success = true;

            // Unreachable: fully extend
            if (distToTarget > totalLen * 0.999f)
            {
                float3 dir = math.normalize(rootToTarget);
                midPos = rootPos + dir * upperLen;
                tipPos = midPos + dir * lowerLen;
                success = false;
            }
            else
            {
                // FABRIK forward + backward passes
                for (int i = 0; i < Iterations; i++)
                {
                    // Stage 1: Forward reaching (tip to target)
                    tipPos = target;

                    // Mid -> Tip constraint
                    float3 midToTip = tipPos - midPos;
                    float midTipDist = math.length(midToTip);
                    if (midTipDist > 0.0001f)
                        midPos = tipPos - math.normalize(midToTip) * lowerLen;
                    else
                        midPos = tipPos - math.up() * lowerLen;

                    // Root -> Mid constraint
                    float3 rootToMid = midPos - rootPos;
                    float rootMidDist = math.length(rootToMid);
                    if (rootMidDist > 0.0001f)
                        midPos = rootPos + math.normalize(rootToMid) * upperLen;
                    else
                        midPos = rootPos + math.up() * upperLen;

                    // Stage 2: Backward reaching (root fixed)
                    // Root stays fixed

                    // Root -> Mid constraint
                    rootToMid = midPos - rootPos;
                    rootMidDist = math.length(rootToMid);
                    if (rootMidDist > 0.0001f)
                        midPos = rootPos + math.normalize(rootToMid) * upperLen;
                    else
                        midPos = rootPos + math.up() * upperLen;

                    // Mid -> Tip constraint
                    midToTip = tipPos - midPos;
                    midTipDist = math.length(midToTip);
                    if (midTipDist > 0.0001f)
                        tipPos = midPos + math.normalize(midToTip) * lowerLen;
                    else
                        tipPos = midPos + math.up() * lowerLen;
                }
            }

            // Compute rotations
            quaternion rootRot = quaternion.LookRotationSafe(math.normalize(midPos - rootPos), math.up());
            quaternion midRot = quaternion.LookRotationSafe(math.normalize(tipPos - midPos), math.up());
            quaternion tipRot = quaternion.LookRotationSafe(math.normalize(tipPos - midPos), math.up());

            OutRootRot = rootRot;
            OutMidRot = midRot;
            OutTipRot = tipRot;
        }

        struct TransformProxy
        {
            public float3 position;
        }
    }
}