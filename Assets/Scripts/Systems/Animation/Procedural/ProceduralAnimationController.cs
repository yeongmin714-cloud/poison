using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using ProjectName.Systems.Animation.Procedural.IK;
using ProjectName.Systems.Animation.Procedural.Locomotion.Biped;
using ProjectName.Systems.Animation.Procedural.Locomotion.Quadruped;
using ProjectName.Systems.Animation.Procedural.Actions;
using ProjectName.Systems.Animation.Procedural.Bones;

namespace ProjectName.Systems.Animation.Procedural
{
    /// <summary>
    /// Main procedural animation controller.
    /// Composes modular IK/locomotion/action jobs for biped characters.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(ProceduralBoneMap))]
    public class ProceduralAnimationController : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        // Inspector Settings
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
        // Components
        // ──────────────────────────────────────────────
        Animator _animator;
        Rigidbody _rigidbody;
        ProceduralBoneMap _boneMap;
        ProceduralAnimStateMachine _stateMachine;

        // ──────────────────────────────────────────────
        // Native Arrays for Job System
        // ──────────────────────────────────────────────
        NativeArray<float3> _leftFootPos, _rightFootPos, _leftHandPos, _rightHandPos;
        NativeArray<float3> _leftFootTarget, _rightFootTarget, _leftHandTarget, _rightHandTarget;
        NativeArray<float3> _leftFootHint, _rightFootHint, _leftHandHint, _rightHandHint;
        NativeArray<quaternion> _spineRotations;
        NativeArray<float3> _hipOffset;
        NativeArray<float> _hipHeightOffset;

        // Job handles
        JobHandle _locomotionJobHandle;
        JobHandle _ikJobHandle;
        JobHandle _actionJobHandle;

        // ──────────────────────────────────────────────
        // Runtime State
        // ──────────────────────────────────────────────
        Vector3 _currentVelocity;
        Vector3 _targetVelocity;
        float _currentSpeed;
        float _targetSpeed;
        bool _isGrounded;
        float _coyoteTimer;

        // Leg phases
        float _leftLegPhase = 0f;
        float _rightLegPhase = 0.5f;
        float _phaseSpeed = 1f;
        const float _dutyCycle = 0.6f; // 60% stance

        // Ground detection
        RaycastHit _leftFootHit, _rightFootHit;
        bool _leftFootGrounded, _rightFootGrounded;

        // Body lean
        Vector3 _bodyLeanOffset;
        Quaternion _bodyLeanRotation = Quaternion.identity;

        // Head look
        Vector3 _headLookTarget;

        // Action override
        ActionState _actionState = ActionState.None;
        float _actionTimer;
        Vector3 _actionTarget;

        public enum ActionState { None, Attack, Gather, Roll, Climb, Stagger }

        // ──────────────────────────────────────────────
        // Public Properties
        // ──────────────────────────────────────────────
        public Vector3 CurrentVelocity => _currentVelocity;
        public float CurrentSpeed => _currentSpeed;
        public bool IsGrounded => _isGrounded;
        public ProceduralAnimStateMachine StateMachine => _stateMachine;

        // ──────────────────────────────────────────────
        // Unity Lifecycle
        // ──────────────────────────────────────────────

        void Awake()
        {
            _animator = GetComponent<Animator>();
            _rigidbody = GetComponent<Rigidbody>();
            _boneMap = GetComponent<ProceduralBoneMap>();
            _stateMachine = GetComponent<ProceduralAnimStateMachine>();

            if (_stateMachine == null)
                _stateMachine = gameObject.AddComponent<ProceduralAnimStateMachine>();

            // Animator setup
            _animator.applyRootMotion = false;
            _animator.updateMode = AnimatorUpdateMode.Fixed;
            _animator.animatePhysics = true;

            // Rigidbody setup
            _rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // Initialize bone map
            _boneMap.Initialize(_animator);

            // Allocate native arrays
            AllocateNativeArrays();
        }

        void AllocateNativeArrays()
        {
            int spineCount = 3; // Spine0, Spine1, Spine2

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
        }

        void OnDestroy()
        {
            // Complete any pending jobs
            JobHandle.ScheduleBatchedJobs();
            _locomotionJobHandle.Complete();
            _ikJobHandle.Complete();
            _actionJobHandle.Complete();

            // Dispose native arrays
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

            // Hints
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

            // Head look target
            _headLookTarget = transform.position + transform.forward * 5f + Vector3.up * 1.5f;
            if (_actionState == ActionState.Attack || _actionState == ActionState.Gather)
            {
                _headLookTarget = _actionTarget;
            }

            // Schedule locomotion jobs
            ScheduleLocomotionJobs();
        }

        void FixedUpdate()
        {
            ApplyMovement();
            ApplyGravity();
        }

        void LateUpdate()
        {
            // Complete locomotion jobs before IK
            _locomotionJobHandle.Complete();

            // Update ground detection (main thread for raycasts)
            UpdateGroundDetection();

            // Schedule IK jobs
            ScheduleIKJobs();

            // Apply procedural pose (main thread for Transform access)
            ApplyProceduralPose();

            // Apply to Animator IK
            // Done in OnAnimatorIK
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0) return;

            // Complete IK jobs
            _ikJobHandle.Complete();

            ApplyIKToAnimator();
        }

        // ──────────────────────────────────────────────
        // Input & Movement
        // ──────────────────────────────────────────────

        void HandleInput()
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
            _targetVelocity = transform.TransformDirection(localTarget) * (sprint ? runSpeed : walkSpeed);
            _targetSpeed = _targetVelocity.magnitude;

            // Rotation
            if (_targetVelocity.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(_targetVelocity.normalized);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
            }

            // Actions
            if (Input.GetMouseButtonDown(0)) RequestAttack();
            if (Input.GetKeyDown(KeyCode.Space)) RequestJump();
            if (Input.GetKeyDown(KeyCode.E)) RequestGather();
            if (Input.GetKeyDown(KeyCode.Q)) RequestRoll();
        }

        void UpdateMovement()
        {
            _currentVelocity = Vector3.MoveTowards(_currentVelocity, _targetVelocity, acceleration * Time.deltaTime);
            _currentSpeed = _currentVelocity.magnitude;

            // Body lean (turning)
            float turnInput = Input.GetKey(KeyCode.A) ? -1f : (Input.GetKey(KeyCode.D) ? 1f : 0f);
            float targetLean = turnInput * bodyLeanAmount * 15f;
            _bodyLeanOffset = Vector3.Lerp(_bodyLeanOffset, new Vector3(targetLean, 0, 0), Time.deltaTime * 5f);
            _bodyLeanRotation = Quaternion.Lerp(_bodyLeanRotation, Quaternion.Euler(_bodyLeanOffset), Time.deltaTime * 5f);
        }

        void ApplyMovement()
        {
            if (!_isGrounded) return;

            Vector3 move = _currentVelocity * Time.fixedDeltaTime;
            move.y = _rigidbody.velocity.y;
            _rigidbody.velocity = move;
        }

        void ApplyGravity()
        {
            if (!_isGrounded)
            {
                _rigidbody.AddForce(Vector3.up * gravity * _rigidbody.mass, ForceMode.Force);
            }
        }

        void UpdateCoyoteTime()
        {
            if (_isGrounded) _coyoteTimer = coyoteTime;
            else _coyoteTimer -= Time.deltaTime;
        }

        // ──────────────────────────────────────────────
        // State Machine
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

        // ──────────────────────────────────────────────
        // Job Scheduling
        // ──────────────────────────────────────────────

        void ScheduleLocomotionJobs()
        {
            JobHandle dependency = default;

            // Foot planner
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

            // Hip shift
            var hipShift = new HipShiftJob
            {
                BodyVelocity = _currentVelocity,
                BodyRotation = transform.rotation,
                LeftPhase = _leftLegPhase,
                RightPhase = _rightLegPhase,
                DutyCycle = _dutyCycle,
                MaxLateralShift = 0.1f,
                MaxVerticalShift = 0.05f,
                Speed = _currentSpeed,

                OutHipOffset = _hipOffset,
                OutHipHeightOffset = _hipHeightOffset,
            };
            dependency = hipShift.Schedule(dependency);

            // Spine counter-rotation
            var spineCounter = new SpineCounterRotationJob
            {
                PelvisRotation = transform.rotation,
                BodyAngularVelocity = _rigidbody.angularVelocity,
                LeftPhase = _leftLegPhase,
                RightPhase = _rightLegPhase,
                DutyCycle = _dutyCycle,
                MaxCounterRotation = 8f,
                Speed = _currentSpeed,

                OutSpineRotation = _spineRotations,
            };
            dependency = spineCounter.Schedule(dependency);

            _locomotionJobHandle = dependency;
        }

        void ScheduleIKJobs()
        {
            JobHandle dependency = _locomotionJobHandle;

            // Get current bone positions
            UpdateBonePositions();

            // Left leg IK
            var leftLegChain = new LimbIKSolver.Chain
            {
                Root = _boneMap.Get(BoneRole.L_Hip),
                Mid = _boneMap.Get(BoneRole.L_Knee),
                Tip = _boneMap.Get(BoneRole.L_Ankle),
            };
            if (leftLegChain.Root != null)
            {
                LimbIKSolver.ComputeLengths(ref leftLegChain);

                var leftIK = new LimbIKSolver.LimbIKJob
                {
                    RootPos = leftLegChain.Root.position,
                    MidPos = leftLegChain.Mid.position,
                    TipPos = leftLegChain.Tip.position,
                    TargetPos = _leftFootTarget[0],
                    HintPos = _leftFootHint[0],
                    UpperLen = leftLegChain.UpperLength,
                    LowerLen = leftLegChain.LowerLength,
                    Iterations = 2,

                    OutRootPos = new NativeArray<float3>(1, Allocator.TempJob),
                    OutMidPos = new NativeArray<float3>(1, Allocator.TempJob),
                    OutTipPos = new NativeArray<float3>(1, Allocator.TempJob),
                    OutRootRot = new NativeArray<quaternion>(1, Allocator.TempJob),
                    OutMidRot = new NativeArray<quaternion>(1, Allocator.TempJob),
                    OutTipRot = new NativeArray<quaternion>(1, Allocator.TempJob),
                    OutSuccess = new NativeArray<bool>(1, Allocator.TempJob),
                };
                dependency = leftIK.Schedule(dependency);
            }

            // Right leg IK (similar)
            var rightLegChain = new LimbIKSolver.Chain
            {
                Root = _boneMap.Get(BoneRole.R_Hip),
                Mid = _boneMap.Get(BoneRole.R_Knee),
                Tip = _boneMap.Get(BoneRole.R_Ankle),
            };
            if (rightLegChain.Root != null)
            {
                LimbIKSolver.ComputeLengths(ref rightLegChain);

                var rightIK = new LimbIKSolver.LimbIKJob
                {
                    RootPos = rightLegChain.Root.position,
                    MidPos = rightLegChain.Mid.position,
                    TipPos = rightLegChain.Tip.position,
                    TargetPos = _rightFootTarget[0],
                    HintPos = _rightFootHint[0],
                    UpperLen = rightLegChain.UpperLength,
                    LowerLen = rightLegChain.LowerLength,
                    Iterations = 2,

                    OutRootPos = new NativeArray<float3>(1, Allocator.TempJob),
                    OutMidPos = new NativeArray<float3>(1, Allocator.TempJob),
                    OutTipPos = new NativeArray<float3>(1, Allocator.TempJob),
                    OutRootRot = new NativeArray<quaternion>(1, Allocator.TempJob),
                    OutMidRot = new NativeArray<quaternion>(1, Allocator.TempJob),
                    OutTipRot = new NativeArray<quaternion>(1, Allocator.TempJob),
                    OutSuccess = new NativeArray<bool>(1, Allocator.TempJob),
                };
                dependency = rightIK.Schedule(dependency);
            }

            _ikJobHandle = dependency;
        }

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

            _isGrounded = _leftFootGrounded || _rightFootGrounded;
        }

        // ──────────────────────────────────────────────
        // Procedural Pose Application
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
            // Left leg
            if (_boneMap.Has(BoneRole.L_Hip) && _boneMap.Has(BoneRole.L_Knee) && _boneMap.Has(BoneRole.L_Ankle))
            {
                var chain = new LimbIKSolver.Chain
                {
                    Root = _boneMap.Get(BoneRole.L_Hip),
                    Mid = _boneMap.Get(BoneRole.L_Knee),
                    Tip = _boneMap.Get(BoneRole.L_Ankle),
                };
                LimbIKSolver.ComputeLengths(ref chain);
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
                var chain = new LimbIKSolver.Chain
                {
                    Root = _boneMap.Get(BoneRole.R_Hip),
                    Mid = _boneMap.Get(BoneRole.R_Knee),
                    Tip = _boneMap.Get(BoneRole.R_Ankle),
                };
                LimbIKSolver.ComputeLengths(ref chain);
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
            var hip = _boneMap.Get(BoneRole.Hip);

            if (root != null)
                root.localRotation = _bodyLeanRotation;
        }

        void ApplyHipShift()
        {
            var hip = _boneMap.Get(BoneRole.Hip);
            if (hip != null)
            {
                hip.localPosition += _hipOffset[0] + Vector3.up * _hipHeightOffset[0];
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

            // Hand IK during actions
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
        // Action Requests
        // ──────────────────────────────────────────────

        public void RequestJump()
        {
            if (!_isGrounded && _coyoteTimer <= 0) return;
            if (_actionState != ActionState.None) return;

            float jumpVel = Mathf.Sqrt(-2f * gravity * jumpHeight);
            _rigidbody.velocity = new Vector3(_rigidbody.velocity.x, jumpVel, _rigidbody.velocity.z);
            _coyoteTimer = 0;
        }

        public void RequestAttack(Vector3? target = null)
        {
            if (_actionState != ActionState.None) return;
            _actionState = ActionState.Attack;
            _actionTimer = 0f;
            _actionTarget = target ?? (transform.position + transform.forward * 2f);
            _stateMachine.RequestAttack();
        }

        public void RequestGather(Vector3? target = null)
        {
            if (_actionState != ActionState.None) return;
            _actionState = ActionState.Gather;
            _actionTimer = 0f;
            _actionTarget = target ?? (transform.position + transform.forward * 1.5f);
            _stateMachine.RequestGather();
        }

        public void RequestRoll()
        {
            if (_actionState != ActionState.None) return;
            if (!_isGrounded) return;

            _actionState = ActionState.Roll;
            _actionTimer = 0f;
            Vector3 dir = _currentVelocity.magnitude > 0.1f ? _currentVelocity.normalized : transform.forward;
            _rigidbody.AddForce(dir * 15f, ForceMode.VelocityChange);
            _stateMachine.RequestRoll();
        }

        public void RequestClimb()
        {
            _actionState = ActionState.Climb;
            _actionTimer = 0f;
        }

        void UpdateActionAttack()
        {
            _actionTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(_actionTimer / 0.8f);
            float swing = Mathf.Sin(progress * Mathf.PI) * 90f;

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
            float progress = Mathf.Clamp01(_actionTimer / 1.5f);

            var lHand = _boneMap.Get(BoneRole.L_Hand);
            var rHand = _boneMap.Get(BoneRole.R_Hand);

            if (lHand != null)
                _leftHandTarget[0] = Vector3.Lerp(_leftHandTarget[0], _actionTarget, progress);
            if (rHand != null)
                _rightHandTarget[0] = Vector3.Lerp(_rightHandTarget[0], _actionTarget, progress);

            // Spine bend
            float bend = progress * 45f;
            _bodyLeanOffset = Vector3.Lerp(_bodyLeanOffset, new Vector3(0, 0, bend), Time.deltaTime * 3f);

            if (_actionTimer > 1.5f)
                _actionState = ActionState.None;
        }

        void UpdateActionRoll()
        {
            _actionTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(_actionTimer / 0.6f);
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
            // Wall detection + hand/foot IK would go here
        }

        // ──────────────────────────────────────────────
        // Leg Phase Update
        // ──────────────────────────────────────────────

        void UpdateLegPhases()
        {
            if (!_isGrounded) return;

            float speedRatio = _currentSpeed / runSpeed;
            _phaseSpeed = Mathf.Lerp(0.5f, 2.5f, speedRatio);

            float phaseDelta = _phaseSpeed * Time.deltaTime;
            _leftLegPhase = Mathf.Repeat(_leftLegPhase + phaseDelta, 1f);
            _rightLegPhase = Mathf.Repeat(_rightLegPhase + phaseDelta, 1f);
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
}