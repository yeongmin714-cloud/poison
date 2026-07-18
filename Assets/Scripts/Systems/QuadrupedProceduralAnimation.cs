using System.Collections.Generic;
using UnityEngine;
using ProjectName.Systems.Animation.Procedural.Bones;
using ProjectName.Systems.Animation.Procedural.IK;

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
            HandleInput();
            UpdateMovement();
            UpdateCoyoteTime();
        }

        private void FixedUpdate()
        {
            ApplyMovement();
            ApplyGravity();
        }

        private void LateUpdate()
        {
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

        private void ApplyMovement()
        {
            if (!_isGrounded) return;
            Vector3 move = _currentVelocity * Time.fixedDeltaTime;
            move.y = _rigidbody.velocity.y;
            _rigidbody.velocity = move;
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
            if (!_isGrounded) return;

            float phaseSpeed = _currentSpeed / _stepLength * 1.5f;
            float delta = phaseSpeed * Time.deltaTime;

            LF_Phase = Mathf.Repeat(LF_Phase + delta, 1f);
            RF_Phase = Mathf.Repeat(RF_Phase + delta, 1f);
            LH_Phase = Mathf.Repeat(LH_Phase + delta, 1f);
            RH_Phase = Mathf.Repeat(RH_Phase + delta, 1f);

            // Locomotion 모듈에서 gait offsets 적용
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
            ApplySpineIK();
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

        private void ApplySpineIK()
        {
            if (!_boneMap.Has(BoneRole.Spine0) ||
                !_boneMap.Has(BoneRole.Spine1) ||
                !_boneMap.Has(BoneRole.Spine2))
                return;

            var spine0 = _boneMap.Get(BoneRole.Spine0);
            var spine1 = _boneMap.Get(BoneRole.Spine1);
            var spine2 = _boneMap.Get(BoneRole.Spine2);

            if (spine0 == null || spine1 == null || spine2 == null) return;

            // Spine wave from locomotion module
            float time = Time.time * 2f;
            float wave = Mathf.Sin(time) * 0.05f;

            spine0.Rotate(Vector3.up, wave * 0.3f, Space.Self);
            spine1.Rotate(Vector3.up, wave * 0.6f, Space.Self);
            spine2.Rotate(Vector3.up, wave * 0.1f, Space.Self);
        }

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