using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 완전 프로시저럴 애니메이션 컨트롤러 (메인 엔트리).
    /// - 애니메이션 클립(.anim) 전혀 사용 안 함
    /// - 모든 모션: Locomotion(보행/달리기), Jump, Attack, Gather, Roll, Climb 등을 수학적으로 실시간 합성
    /// - Animation Rigging + 커스텀 IK로 실시간 포즈 제어
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(Rigidbody))]
    public class ProceduralAnimationController : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        // 인스펙터 설정
        // ──────────────────────────────────────────────

        [Header("Locomotion")]
        [SerializeField] private float _walkSpeed = 3f;
        [SerializeField] private float _runSpeed = 7f;
        [SerializeField] private float _acceleration = 15f;
        [SerializeField] private float _turnSpeed = 720f;

        [Header("Jump")]
        [SerializeField] private float _jumpHeight = 2.5f;
        [SerializeField] private float _gravity = -25f;
        [SerializeField] private float _coyoteTime = 0.1f;

        [Header("IK Weights")]
        [SerializeField, Range(0f, 1f)] private float _footIKWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float _handIKWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float _spineIKWeight = 0.8f;
        [SerializeField, Range(0f, 1f)] private float _headLookWeight = 0.7f;

        [Header("Procedural Modifiers")]
        [SerializeField, Range(0f, 1f)] private float _bodyLeanAmount = 0.6f;
        [SerializeField, Range(0f, 1f)] private float _armSwingAmount = 0.8f;
        [SerializeField, Range(0f, 1f)] private float _headStabilization = 0.5f;

        [Header("Ground Check")]
        [SerializeField] private LayerMask _groundMask = ~0;
        [SerializeField] private float _groundCheckDistance = 1.2f;

        // ──────────────────────────────────────────────
        // 컴포넌트
        // ──────────────────────────────────────────────

        private Animator _animator;
        private Rigidbody _rigidbody;
        private ProceduralBoneMap _boneMap;
        private ProceduralAnimStateMachine _stateMachine;

        // ──────────────────────────────────────────────
        // 이동 상태
        // ──────────────────────────────────────────────

        private Vector3 _currentVelocity;
        private Vector3 _targetVelocity;
        private float _currentSpeed;
        private float _targetSpeed;
        private bool _isGrounded;
        private float _coyoteTimer;

        // 다리 위상 (0~1): 0 = 접지, 0.5 = 스윙 중간
        private float _leftLegPhase = 0f;
        private float _rightLegPhase = 0.5f;
        private float _phaseSpeed = 1f;

        // IK 타겟
        private Vector3 _leftFootTarget, _rightFootTarget;
        private Vector3 _leftHandTarget, _rightHandTarget;
        private Vector3 _leftFootHint, _rightFootHint;
        private Vector3 _leftHandHint, _rightHandHint;

        // Ground detection
        private RaycastHit _leftFootHit, _rightFootHit;
        private bool _leftFootGrounded, _rightFootGrounded;

        // Body lean
        private Vector3 _bodyLeanOffset;
        private Quaternion _bodyLeanRotation = Quaternion.identity;

        // Head look
        private Vector3 _headLookTarget;

        // Action override
        private ActionState _actionState = ActionState.None;
        private float _actionTimer;
        private Vector3 _actionTarget;
        
        public enum ActionState { None, Attack, Gather, Roll, Climb, Stagger }

        public Vector3 CurrentActionTarget => _actionTarget;
        public ActionState CurrentAction => _actionState;

        // ──────────────────────────────────────────────
        // 공개 속성
        // ──────────────────────────────────────────────

        public Vector3 CurrentVelocity => _currentVelocity;
        public float CurrentSpeed => _currentSpeed;
        public bool IsGrounded => _isGrounded;
        public ProceduralAnimStateMachine StateMachine => _stateMachine;

        // ──────────────────────────────────────────────
        // 공개 API (외부에서 호출)
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

        // ──────────────────────────────────────────────
        // Unity Lifecycle
        // ──────────────────────────────────────────────

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _rigidbody = GetComponent<Rigidbody>();
            _boneMap = GetComponent<ProceduralBoneMap>();
            _stateMachine = GetComponent<ProceduralAnimStateMachine>();

            if (_stateMachine == null)
                _stateMachine = gameObject.AddComponent<ProceduralAnimStateMachine>();

            // Animator 설정
            _animator.applyRootMotion = false;
            _animator.updateMode = AnimatorUpdateMode.Fixed;
            _animator.animatePhysics = true;

            // Rigidbody 설정
            _rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // 본 매핑
            if (_boneMap != null)
                _boneMap.Initialize(_animator);
        }

        private void Start()
        {
            InitializeIKTargets();
        }

        private void Update()
        {
            HandleInput();
            UpdateMovement();
            UpdateStateMachine();
            UpdateCoyoteTime();
        }

        private void FixedUpdate()
        {
            ApplyMovement();
            ApplyGravity();
        }

        private void LateUpdate()
        {
            // 프로시저럴 애니메이션은 LateUpdate에서 (Animator 이후)
            UpdateGroundDetection();
            UpdateLegPhases();
            UpdateIKTargets();
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
            var lFoot = _boneMap.Get(ProceduralBoneUtility.BoneRole.L_Foot);
            var rFoot = _boneMap.Get(ProceduralBoneUtility.BoneRole.R_Foot);
            var lHand = _boneMap.Get(ProceduralBoneUtility.BoneRole.L_Hand);
            var rHand = _boneMap.Get(ProceduralBoneUtility.BoneRole.R_Hand);

            if (lFoot != null) _leftFootTarget = lFoot.position;
            if (rFoot != null) _rightFootTarget = rFoot.position;
            if (lHand != null) _leftHandTarget = lHand.position;
            if (rHand != null) _rightHandTarget = rHand.position;

            // 힌트는 무릎/팔꿈치 옆
            var lKnee = _boneMap.Get(ProceduralBoneUtility.BoneRole.L_Knee);
            var rKnee = _boneMap.Get(ProceduralBoneUtility.BoneRole.R_Knee);
            var lElbow = _boneMap.Get(ProceduralBoneUtility.BoneRole.L_Elbow);
            var rElbow = _boneMap.Get(ProceduralBoneUtility.BoneRole.R_Elbow);

            if (lKnee != null) _leftFootHint = lKnee.position + Vector3.right * 0.3f;
            if (rKnee != null) _rightFootHint = rKnee.position + Vector3.left * 0.3f;
            if (lElbow != null) _leftHandHint = lElbow.position + Vector3.forward * 0.3f;
            if (rElbow != null) _rightHandHint = rElbow.position + Vector3.forward * 0.3f;
        }

        // ──────────────────────────────────────────────
        // 입력 처리
        // ──────────────────────────────────────────────

        private void HandleInput()
        {
            if (_actionState != ActionState.None) return; // 액션 중 입력 무시

            Vector2 input = Vector2.zero;
            if (Input.GetKey(KeyCode.W)) input.y += 1;
            if (Input.GetKey(KeyCode.S)) input.y -= 1;
            if (Input.GetKey(KeyCode.A)) input.x -= 1;
            if (Input.GetKey(KeyCode.D)) input.x += 1;
            input = Vector2.ClampMagnitude(input, 1f);

            bool sprint = Input.GetKey(KeyCode.LeftShift);

            // Target velocity in local space
            Vector3 localTarget = new Vector3(input.x, 0, input.y);
            _targetVelocity = transform.TransformDirection(localTarget) * (sprint ? _runSpeed : _walkSpeed);
            _targetSpeed = _targetVelocity.magnitude;

            // Rotation
            if (_targetVelocity.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(_targetVelocity.normalized);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, _turnSpeed * Time.deltaTime);
            }

            // Actions
            if (Input.GetMouseButtonDown(0)) RequestAttack();
            if (Input.GetKeyDown(KeyCode.Space)) RequestJump();
            if (Input.GetKeyDown(KeyCode.E)) RequestGather();
            if (Input.GetKeyDown(KeyCode.Q)) RequestRoll();
        }

        // ──────────────────────────────────────────────
        // 이동 업데이트
        // ──────────────────────────────────────────────

        private void UpdateMovement()
        {
            // Smooth acceleration
            _currentVelocity = Vector3.MoveTowards(_currentVelocity, _targetVelocity, _acceleration * Time.deltaTime);
            _currentSpeed = _currentVelocity.magnitude;

            // Body lean (turning)
            float turnInput = Input.GetKey(KeyCode.A) ? -1f : (Input.GetKey(KeyCode.D) ? 1f : 0f);
            float targetLean = turnInput * _bodyLeanAmount * 15f; // degrees
            _bodyLeanOffset = Vector3.Lerp(_bodyLeanOffset, new Vector3(targetLean, 0, 0), Time.deltaTime * 5f);
            _bodyLeanRotation = Quaternion.Lerp(_bodyLeanRotation, Quaternion.Euler(_bodyLeanOffset), Time.deltaTime * 5f);
        }

        private void ApplyMovement()
        {
            if (!_isGrounded) return;

            Vector3 move = _currentVelocity * Time.fixedDeltaTime;
            move.y = _rigidbody.velocity.y; // keep vertical
            _rigidbody.velocity = move;
        }

        private void ApplyGravity()
        {
            if (!_isGrounded)
            {
                _rigidbody.AddForce(Vector3.up * _gravity * _rigidbody.mass, ForceMode.Force);
            }
        }

        private void UpdateCoyoteTime()
        {
            if (_isGrounded)
                _coyoteTimer = _coyoteTime;
            else
                _coyoteTimer -= Time.deltaTime;
        }

        // ──────────────────────────────────────────────
        // 상태 머신 연동
        // ──────────────────────────────────────────────

        private void UpdateStateMachine()
        {
            var state = _stateMachine.CurrentState;

            switch (state)
            {
                case ProceduralAnimStateMachine.State.Jump:
                    if (_actionState != ActionState.None) break;
                    _actionState = ActionState.None; // Jump handled by physics
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

                case ProceduralAnimStateMachine.State.Stagger:
                    break;
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
            _rigidbody.velocity = new Vector3(_rigidbody.velocity.x, jumpVelocity, _rigidbody.velocity.z);
            _coyoteTimer = 0;
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
            if (!_isGrounded) return;

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
        // 액션 업데이트
        // ──────────────────────────────────────────────

        private void UpdateActionAttack()
        {
            _actionTimer += Time.deltaTime;

            // 상체 IK로 타겟 향해 휘두르기
            float progress = Mathf.Clamp01(_actionTimer / 0.8f);
            float swing = Mathf.Sin(progress * Mathf.PI) * 90f; // 0->90->0 degrees

            var rHand = _boneMap.Get(ProceduralBoneUtility.BoneRole.R_Hand);
            var rElbow = _boneMap.Get(ProceduralBoneUtility.BoneRole.R_Elbow);

            if (rHand != null && rElbow != null)
            {
                Vector3 swingDir = (_actionTarget - rHand.position).normalized;
                _rightHandTarget = Vector3.Lerp(_rightHandTarget, _actionTarget, progress * 2f);
                _rightHandHint = rElbow.position + swingDir * 0.5f;
            }

            if (_actionTimer > 0.8f)
                _actionState = ActionState.None;
        }

        private void UpdateActionGather()
        {
            _actionTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(_actionTimer / 1.5f);

            // 양손을 타겟으로
            var lHand = _boneMap.Get(ProceduralBoneUtility.BoneRole.L_Hand);
            var rHand = _boneMap.Get(ProceduralBoneUtility.BoneRole.R_Hand);

            if (lHand != null)
                _leftHandTarget = Vector3.Lerp(_leftHandTarget, _actionTarget, progress);
            if (rHand != null)
                _rightHandTarget = Vector3.Lerp(_rightHandTarget, _actionTarget, progress);

            // 상체 숙이기
            float bend = progress * 45f; // degrees
            _bodyLeanOffset = Vector3.Lerp(_bodyLeanOffset, new Vector3(0, 0, bend), Time.deltaTime * 3f);

            if (_actionTimer > 1.5f)
                _actionState = ActionState.None;
        }

        private void UpdateActionRoll()
        {
            _actionTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(_actionTimer / 0.6f);

            // 구르기 중: 몸체 회전 + 낮아지기
            float rollAngle = progress * 360f;
            transform.Rotate(Vector3.forward, rollAngle * Time.deltaTime / 0.6f);

            if (_actionTimer > 0.6f)
            {
                _actionState = ActionState.None;
                transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0); // Z 회전 초기화
            }
        }

        private void UpdateActionClimb()
        {
            _actionTimer += Time.deltaTime;
            // 벽 감지 후 손발 IK로 고정
            // 구현 생략 (나중에 확장)
        }

        // ──────────────────────────────────────────────
        // Ground Detection
        // ──────────────────────────────────────────────

        private void UpdateGroundDetection()
        {
            var lFoot = _boneMap.Get(ProceduralBoneUtility.BoneRole.L_Foot);
            var rFoot = _boneMap.Get(ProceduralBoneUtility.BoneRole.R_Foot);

            _leftFootGrounded = false;
            _rightFootGrounded = false;

            if (lFoot != null)
            {
                Vector3 origin = lFoot.position + Vector3.up * 0.2f;
                if (Physics.Raycast(origin, Vector3.down, out _leftFootHit, _groundCheckDistance, _groundMask))
                {
                    _leftFootGrounded = true;
                    _leftFootTarget = _leftFootHit.point;
                    _leftFootTarget.y += 0.02f; // slight offset
                }
            }

            if (rFoot != null)
            {
                Vector3 origin = rFoot.position + Vector3.up * 0.2f;
                if (Physics.Raycast(origin, Vector3.down, out _rightFootHit, _groundCheckDistance, _groundMask))
                {
                    _rightFootGrounded = true;
                    _rightFootTarget = _rightFootHit.point;
                    _rightFootTarget.y += 0.02f;
                }
            }

            _isGrounded = _leftFootGrounded || _rightFootGrounded;
        }

        // ──────────────────────────────────────────────
        // 다리 위상 업데이트
        // ──────────────────────────────────────────────

        private void UpdateLegPhases()
        {
            if (!_isGrounded)
            {
                // 공중: 위상 고정
                return;
            }

            float speedRatio = _currentSpeed / _runSpeed;
            _phaseSpeed = Mathf.Lerp(0.5f, 2.5f, speedRatio);

            float phaseDelta = _phaseSpeed * Time.deltaTime;

            // Stance phase (grounded): 0 ~ 0.6, Swing: 0.6 ~ 1.0
            _leftLegPhase = Mathf.Repeat(_leftLegPhase + phaseDelta, 1f);
            _rightLegPhase = Mathf.Repeat(_rightLegPhase + phaseDelta, 1f);

            // 위상에 따른 발 타겟 조정
            UpdateFootTargetsFromPhase();
        }

        private void UpdateFootTargetsFromPhase()
        {
            var lFoot = _boneMap.Get(ProceduralBoneUtility.BoneRole.L_Foot);
            var rFoot = _boneMap.Get(ProceduralBoneUtility.BoneRole.R_Foot);
            var lKnee = _boneMap.Get(ProceduralBoneUtility.BoneRole.L_Knee);
            var rKnee = _boneMap.Get(ProceduralBoneUtility.BoneRole.R_Knee);

            if (lFoot == null || rFoot == null) return;

            // Stance phase (0~0.6): 발 고정
            // Swing phase (0.6~1.0): 발 들기 + 앞으로

            // Left foot
            if (_leftLegPhase < 0.6f)
            {
                // Stance: keep grounded
                if (_leftFootGrounded)
                {
                    // 힌트 업데이트
                    if (lKnee != null)
                        _leftFootHint = lKnee.position + (transform.right * 0.15f);
                }
            }
            else
            {
                // Swing: 발 들어올리기 + 앞으로
                float swingProgress = (_leftLegPhase - 0.6f) / 0.4f;
                float height = Mathf.Sin(swingProgress * Mathf.PI) * 0.3f;
                float forward = swingProgress * _currentSpeed * 0.4f;

                Vector3 swingTarget = _leftFootTarget + transform.forward * forward + Vector3.up * height;
                _leftFootTarget = Vector3.Lerp(_leftFootTarget, swingTarget, Time.deltaTime * 10f);
            }

            // Right foot (opposite phase)
            if (_rightLegPhase < 0.6f)
            {
                if (_rightFootGrounded && rKnee != null)
                    _rightFootHint = rKnee.position + (transform.right * -0.15f);
            }
            else
            {
                float swingProgress = (_rightLegPhase - 0.6f) / 0.4f;
                float height = Mathf.Sin(swingProgress * Mathf.PI) * 0.3f;
                float forward = swingProgress * _currentSpeed * 0.4f;

                Vector3 swingTarget = _rightFootTarget + transform.forward * forward + Vector3.up * height;
                _rightFootTarget = Vector3.Lerp(_rightFootTarget, swingTarget, Time.deltaTime * 10f);
            }
        }

        // ──────────────────────────────────────────────
        // IK 타겟 업데이트
        // ──────────────────────────────────────────────

        private void UpdateIKTargets()
        {
            // Hand targets for actions
            UpdateHandTargets();

            // Head look target
            _headLookTarget = transform.position + transform.forward * 5f + Vector3.up * 1.5f;
            if (_actionState == ActionState.Attack || _actionState == ActionState.Gather)
            {
                _headLookTarget = _actionTarget;
            }
        }

        private void UpdateHandTargets()
        {
            // Default: arm swing during locomotion
            if (_actionState == ActionState.None && _isGrounded)
            {
                float swingPhase = _leftLegPhase; // opposite arm swing
                float swingAmount = _armSwingAmount * 0.3f * (_currentSpeed / _runSpeed);

                var lHand = _boneMap.Get(ProceduralBoneUtility.BoneRole.L_Hand);
                var rHand = _boneMap.Get(ProceduralBoneUtility.BoneRole.R_Hand);

                if (lHand != null)
                {
                    float swing = Mathf.Sin(swingPhase * Mathf.PI * 2f) * swingAmount;
                    _leftHandTarget = lHand.position + transform.right * swing + transform.forward * 0.2f;
                }
                if (rHand != null)
                {
                    float swing = -Mathf.Sin(swingPhase * Mathf.PI * 2f) * swingAmount;
                    _rightHandTarget = rHand.position + transform.right * swing + transform.forward * 0.2f;
                }
            }
        }

        // ──────────────────────────────────────────────
        // 프로시저럴 포즈 적용 (LateUpdate)
        // ──────────────────────────────────────────────

        private void ApplyProceduralPose()
        {
            ApplyFootIK();
            ApplySpineIK();
            ApplyHeadLook();
            ApplyBodyLean();
        }

        private void ApplyFootIK()
        {
            // Left Leg
            if (_boneMap.Has(ProceduralBoneUtility.BoneRole.L_Hip) &&
                _boneMap.Has(ProceduralBoneUtility.BoneRole.L_Knee) &&
                _boneMap.Has(ProceduralBoneUtility.BoneRole.L_Ankle))
            {
                var chain = new LimbIKSolver.Chain
                {
                    Root = _boneMap.Get(ProceduralBoneUtility.BoneRole.L_Hip),
                    Mid = _boneMap.Get(ProceduralBoneUtility.BoneRole.L_Knee),
                    Tip = _boneMap.Get(ProceduralBoneUtility.BoneRole.L_Ankle)
                };
                LimbIKSolver.ComputeLengths(ref chain);

                var result = LimbIKSolver.Solve(chain, _leftFootTarget, _leftFootHint);
                if (result.Success)
                {
                    chain.Root.rotation = result.RootRot;
                    chain.Mid.rotation = result.MidRot;
                    chain.Tip.rotation = result.TipRot;
                }
            }

            // Right Leg
            if (_boneMap.Has(ProceduralBoneUtility.BoneRole.R_Hip) &&
                _boneMap.Has(ProceduralBoneUtility.BoneRole.R_Knee) &&
                _boneMap.Has(ProceduralBoneUtility.BoneRole.R_Ankle))
            {
                var chain = new LimbIKSolver.Chain
                {
                    Root = _boneMap.Get(ProceduralBoneUtility.BoneRole.R_Hip),
                    Mid = _boneMap.Get(ProceduralBoneUtility.BoneRole.R_Knee),
                    Tip = _boneMap.Get(ProceduralBoneUtility.BoneRole.R_Ankle)
                };
                LimbIKSolver.ComputeLengths(ref chain);

                var result = LimbIKSolver.Solve(chain, _rightFootTarget, _rightFootHint);
                if (result.Success)
                {
                    chain.Root.rotation = result.RootRot;
                    chain.Mid.rotation = result.MidRot;
                    chain.Tip.rotation = result.TipRot;
                }
            }
        }

        private void ApplySpineIK()
        {
            if (!_boneMap.Has(ProceduralBoneUtility.BoneRole.Spine0) ||
                !_boneMap.Has(ProceduralBoneUtility.BoneRole.Spine1) ||
                !_boneMap.Has(ProceduralBoneUtility.BoneRole.Spine2))
                return;

            // 척추를 헤드 타겟 방향으로 약간 회전
            var spine0 = _boneMap.Get(ProceduralBoneUtility.BoneRole.Spine0);
            var spine1 = _boneMap.Get(ProceduralBoneUtility.BoneRole.Spine1);
            var spine2 = _boneMap.Get(ProceduralBoneUtility.BoneRole.Spine2);
            var head = _boneMap.Get(ProceduralBoneUtility.BoneRole.Head);

            if (spine0 == null || spine1 == null || spine2 == null || head == null) return;

            Vector3 toTarget = (_headLookTarget - head.position).normalized;
            Vector3 forward = head.forward;

            float angle = Vector3.SignedAngle(forward, toTarget, Vector3.up);
            angle = Mathf.Clamp(angle, -30f, 30f) * _spineIKWeight * 0.5f;

            // Distribute across spine
            spine0.Rotate(Vector3.up, angle * 0.2f, Space.World);
            spine1.Rotate(Vector3.up, angle * 0.5f, Space.World);
            spine2.Rotate(Vector3.up, angle * 0.3f, Space.World);
        }

        private void ApplyHeadLook()
        {
            var head = _boneMap.Get(ProceduralBoneUtility.BoneRole.Head);
            if (head == null) return;

            Vector3 toTarget = (_headLookTarget - head.position).normalized;
            Quaternion targetRot = Quaternion.LookRotation(toTarget, Vector3.up);
            head.rotation = Quaternion.Slerp(head.rotation, targetRot, _headLookWeight * Time.deltaTime * 10f);
        }

        private void ApplyBodyLean()
        {
            var root = _boneMap.Get(ProceduralBoneUtility.BoneRole.Root);
            var hip = _boneMap.Get(ProceduralBoneUtility.BoneRole.Hip);

            if (root != null)
            {
                root.localRotation = _bodyLeanRotation;
            }
        }

        // ──────────────────────────────────────────────
        // Animator IK 적용
        // ──────────────────────────────────────────────

        private void ApplyIKToAnimator()
        {
            // Foot IK
            if (_leftFootGrounded)
            {
                _animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, _footIKWeight);
                _animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, _footIKWeight);
                _animator.SetIKPosition(AvatarIKGoal.LeftFoot, _leftFootTarget);
                _animator.SetIKRotation(AvatarIKGoal.LeftFoot, Quaternion.LookRotation(Vector3.up, _leftFootHit.normal));
                _animator.SetIKHintPositionWeight(AvatarIKHint.LeftKnee, _footIKWeight);
                _animator.SetIKHintPosition(AvatarIKHint.LeftKnee, _leftFootHint);
            }

            if (_rightFootGrounded)
            {
                _animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, _footIKWeight);
                _animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, _footIKWeight);
                _animator.SetIKPosition(AvatarIKGoal.RightFoot, _rightFootTarget);
                _animator.SetIKRotation(AvatarIKGoal.RightFoot, Quaternion.LookRotation(Vector3.up, _rightFootHit.normal));
                _animator.SetIKHintPositionWeight(AvatarIKHint.RightKnee, _footIKWeight);
                _animator.SetIKHintPosition(AvatarIKHint.RightKnee, _rightFootHint);
            }

            // Hand IK
            if (_actionState == ActionState.Attack || _actionState == ActionState.Gather || _actionState == ActionState.Climb)
            {
                _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, _handIKWeight);
                _animator.SetIKPosition(AvatarIKGoal.LeftHand, _leftHandTarget);
                _animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, _handIKWeight);
                _animator.SetIKHintPosition(AvatarIKHint.LeftElbow, _leftHandHint);

                _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, _handIKWeight);
                _animator.SetIKPosition(AvatarIKGoal.RightHand, _rightHandTarget);
                _animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, _handIKWeight);
                _animator.SetIKHintPosition(AvatarIKHint.RightElbow, _rightHandHint);
            }
        }

        // ──────────────────────────────────────────────
        // 디버그
        // ──────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            if (_leftFootGrounded) Gizmos.DrawWireSphere(_leftFootTarget, 0.1f);
            if (_rightFootGrounded) Gizmos.DrawWireSphere(_rightFootTarget, 0.1f);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_leftHandTarget, 0.08f);
            Gizmos.DrawWireSphere(_rightHandTarget, 0.08f);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_headLookTarget, 0.15f);
        }
    }
}