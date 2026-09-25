// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 414
using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectName.Systems.Animation.Procedural.Bones;
using ProjectName.Systems.Animation.Procedural.Locomotion.Biped;
using ProjectName.Systems.Animation.Procedural.Locomotion.Quadruped;
using ProjectName.Systems.Animation.Procedural.Actions;
using ProjectName.Systems.Animation.Procedural.LOD;
using ProjectName.Systems;
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
    [Obsolete("Legacy path — use the clip/procedural pipeline (HumanoidClipDriver+*_AC or Quadruped/Procedural controllers).", false)]
    public class ProceduralAnimationController : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        // 인스펙터 설정
        // ──────────────────────────────────────────────

        [Header("Locomotion")]
        [SerializeField, Range(0f, 10f)] float walkSpeed = 5f;
        [SerializeField, Range(0f, 15f)] float runSpeed = 10f;
        [SerializeField, Range(5f, 30f)] float acceleration = 20f;
        [SerializeField, Range(360f, 1080f)] float turnSpeed = 540f;

        [Header("Jump")]
        [SerializeField, Range(0.5f, 5f)] float jumpHeight = 2.0f;
        [SerializeField, Range(-10f, -50f)] float gravity = -9.81f;
        [SerializeField, Range(0f, 0.2f)] float coyoteTime = 0.1f;

        [Header("IK Weights")]
        [SerializeField, Range(0f, 1f)] float footIKWeight = 0.9f;
        [SerializeField, Range(0f, 1f)] float handIKWeight = 1f;
        [SerializeField, Range(0f, 1f)] float spineIKWeight = 0.6f;
        [SerializeField, Range(0f, 1f)] float headLookWeight = 0.7f;

        [Header("Procedural Modifiers")]
        [SerializeField, Range(0f, 1f)] float bodyLeanAmount = 0.4f;
        [SerializeField, Range(0f, 1f)] float armSwingAmount = 0.6f;
        [SerializeField, Range(0f, 1f)] float headStabilization = 0.5f;

        [Header("Ground Check")]
        [SerializeField] LayerMask groundMask = ~0;
        [SerializeField, Range(0.5f, 2f)] float groundCheckDistance = 1.0f;

        [Header("Job IK / Rotation Gait")]
        // [P-ANIM6 Phase1] 잡(IJob) IK 체인 게이트 — 기본 꺼짐. 익명 리그(isHuman=false, 미노타우르스 등)
        // 2족은 회전 기반 보행으로 전환해 프레임당 잡 5종(footPlanner/hipShift/spineCounter/leftIK/rightIK)
        // 미스케줄 = JobTempAlloc temp 누수 0건. 휴머노이드 아바타(플레이어)는 이 값과 무관하게 항상
        // 잡 경로 유지(UseJobIK 게이트 — 걷기 회귀 방지). true로 두면 익명 리그에서도 잡 경로 강제(디버그용).
        [SerializeField] bool _useJobIK = false;

        // [P-ANIM6 Phase3 튜닝용] 회전 보행 파라미터 — 4족 ApplyRotationGait(P-ANIM5-B) 패턴 이식.
        [SerializeField, Range(1f, 12f)] float _gaitSwingSpeedFactor = 6f;  // 스윙각 = 속도×이 계수
        [SerializeField, Range(0f, 45f)] float _gaitMinSwingDeg = 10f;      // 최소 스윙각(저속 하한)
        [SerializeField, Range(5f, 60f)] float _gaitMaxSwingDeg = 32f;      // 최대 스윙각(고속 상한)
        [SerializeField, Range(0f, 1.5f)] float _gaitKneeBendFactor = 0.5f; // 무릎 굽힘량(스윙각 대비)
        [SerializeField, Range(0f, 1.5f)] float _gaitArmSwingFactor = 0.4f; // 팔 스윙량(다리 진폭 대비)
        // [P-ANIM8 Phase2] 보행 시 골반 체중이동 진폭, 척추는 역위상으로 일부 상쇄.
        [SerializeField, Range(0f, 8f)] float _gaitPelvisRollDeg = 2.5f;
        [SerializeField, Range(0f, 1f)] float _gaitSpineCounterFactor = 0.5f;

        // [P-ANIM7 Phase2] 정지 idle 호흡 파라미터 — 어깨 롤+척추 피치(익명 리그 2족 전용).
        [SerializeField, Range(0.5f, 3f)] float _idleHz = 1.1f;         // 호흡 주파수(Hz)
        [SerializeField, Range(0f, 8f)] float _idleBreathDeg = 1.8f;    // 호흡 진폭(도)

        // ──────────────────────────────────────────────
        // 컴포넌트
        // ──────────────────────────────────────────────

        Animator _animator;
        Rigidbody _rigidbody;
        ProceduralBoneMap _boneMap;
        ProceduralAnimStateMachine _stateMachine;
        ProceduralLODManager _lodManager;

        // [P-ANIM6 Phase1] 회전 보행 상태 — 기준 localRotation 캐시(4족 _gaitBaseRot 동일 패턴).
        // 적용 중 플래그: 정지/액션 전환 시 1회 기본 포즈 복원(스윙 자세 잔존 방지).
        readonly Dictionary<Transform, Quaternion> _gaitBaseRot = new Dictionary<Transform, Quaternion>();
        bool _gaitActive;

        // [P-ANIM7 Phase2] 정지 idle 상태 — 어깨 호흡+척추 미세 피치(조각상 해소). 별도 캐시로
        // gait 캐시와 분리 — idle↔gait 전환 시 각각 base 복원 후 재포착한다.
        readonly Dictionary<Transform, Quaternion> _idleBaseRot = new Dictionary<Transform, Quaternion>();
        bool _idleActive;

        /// <summary>
        /// [P-ANIM6 Phase1] 잡 경로 게이트 — 플레이어 보호: 휴머노이드 아바타(isHuman=true)는
        /// _useJobIK 값과 무관하게 항상 잡 경로 유지. 익명 리그(2족 몬스터)만 회전 보행으로 전환.
        /// </summary>
        bool UseJobIK => (_animator != null && _animator.isHuman) || _useJobIK;

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

        /// <summary>
        /// [2026-09-14(50차)] 종별 보행 프로필 적용 — 2족 몬스터 보행 파라미터(walk/run/accel)를 종 특성에 맞춘다.
        /// AnimalAI.UpdateBipedLink가 지연 탐색 후 SetVelocityProvider와 함께 호출(몬스터당 1회).
        /// 기록된 값은 HandleInput(외부 속도 수용), UpdateMovement(acceleration으로 속도 수렴),
        /// UpdateLegPhases(_currentSpeed/runSpeed 비율로 위상 속도 결정)에 그대로 반영된다.
        /// </summary>
        public void ApplyMonsterProfile(string monsterId)
        {
            switch (monsterId)
            {
                // 대형·무거운 괴수 — 느리고 무거운 발걸음(보폭 큼)
                case "wild_troll":
                case "ogre":
                case "stone_golem":
                    walkSpeed = 3f; runSpeed = 6f; acceleration = 12f;
                    break;
                // 돌진형 중간 체격
                case "minotaur":
                    walkSpeed = 4f; runSpeed = 8f; acceleration = 15f;
                    break;
                // 민첩형 — 빠른 보행
                case "shadow_assassin":
                case "banshee":
                    walkSpeed = 6f; runSpeed = 12f; acceleration = 25f;
                    break;
                default:
                    return; // 그 외 2족: 기본값(walk 5 / run 10 / accel 20) 유지
            }
            UnityEngine.Debug.Log($"[ProceduralAnimationController] 종별 보행 프로필 적용: {monsterId} (walk={walkSpeed}, run={runSpeed}, accel={acceleration})");
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

        // TempJob array tracking for proper disposal
        List<NativeArray<float3>> _locomotionFloat3Arrays = new List<NativeArray<float3>>();
        List<NativeArray<quaternion>> _locomotionQuaternionArrays = new List<NativeArray<quaternion>>();
        List<NativeArray<float>> _locomotionFloatArrays = new List<NativeArray<float>>();
        List<NativeArray<bool>> _locomotionBoolArrays = new List<NativeArray<bool>>();
        List<NativeArray<int>> _locomotionIntArrays = new List<NativeArray<int>>();

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
        float _turnInput;

        ActionState _actionState = ActionState.None;
        float _actionTimer;
        Vector3 _actionTarget;

        public enum ActionState { None, Attack, Gather, Roll, Climb, Stagger, Mount, Charge, Parry }

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

        // ──────────────────────────────────────────────
        // LOD Settings (set by ProceduralLODManager)
        // ──────────────────────────────────────────────

        int _currentLODLevel = 0;
        bool _lodRaycastEnabled = true;
        int _lodIKIterations = 2;
        bool _lodSpineWaveEnabled = true;
        bool _lodSpineCounterEnabled = true;
        bool _lodHipShiftEnabled = true;
        float _lodPhaseUpdateRate = 1f;

        /// <summary>
        /// Current LOD level assigned by ProceduralLODManager.
        /// 0=full, 1=medium, 2=low, 3=culled.
        /// </summary>
        public int CurrentLODLevel
        {
            get => _currentLODLevel;
            set
            {
                _currentLODLevel = value;
                ApplyLODSettings(value);
            }
        }

        void ApplyLODSettings(int lod)
        {
            switch (lod)
            {
                case 0: // Full
                    _lodRaycastEnabled = true;
                    _lodIKIterations = 2;
                    _lodSpineWaveEnabled = true;
                    _lodSpineCounterEnabled = true;
                    _lodHipShiftEnabled = true;
                    _lodPhaseUpdateRate = 1f;
                    break;
                case 1: // Medium
                    _lodRaycastEnabled = true;
                    _lodIKIterations = 1;
                    _lodSpineWaveEnabled = true;
                    _lodSpineCounterEnabled = false;
                    _lodHipShiftEnabled = true;
                    _lodPhaseUpdateRate = 0.5f;
                    break;
                case 2: // Low
                    _lodRaycastEnabled = false; // no raycasts, use fallback
                    _lodIKIterations = 1;
                    _lodSpineWaveEnabled = false;
                    _lodSpineCounterEnabled = false;
                    _lodHipShiftEnabled = false;
                    _lodPhaseUpdateRate = 0.25f;
                    break;
                default: // Culled (3+)
                    _lodRaycastEnabled = false;
                    _lodIKIterations = 0;
                    _lodSpineWaveEnabled = false;
                    _lodSpineCounterEnabled = false;
                    _lodHipShiftEnabled = false;
                    _lodPhaseUpdateRate = 0f;
                    break;
            }
        }

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
                case "stagger": RequestStagger(); break;
                case "death": RequestDeath(); break;
                case "mount": RequestMount(); break;
                case "dismount": RequestDismount(); break;
                case "charge": RequestCharge(); break;
                case "parry": RequestParry(); break;
                // [Phase 1-1/1-2] 상태 머신 상태가 없는 순수 패턴일 뿐 — no-op 폴백 (FBX 클립 교체 시 여기서 대체)
                case "charge_end": case "parry_end": case "parry_success":
                    break;
            }
        }

        /// <summary>[Phase 1-1] 차지(강공 충전) 절차 요청.</summary>
        public void RequestCharge()
        {
            var sm = GetComponent<ProceduralAnimStateMachine>();
            if (sm != null) { sm.RequestCharge(); return; }
            // 상태 머신 없으면 직접 절차 동작 플래그 (후일 FBX 클립 "charge" 재생 포인트)
            _actionState = ActionState.Charge;
        }

        /// <summary>[Phase 1-2] 패링(근접 방어) 절차 요청.</summary>
        public void RequestParry()
        {
            var sm = GetComponent<ProceduralAnimStateMachine>();
            if (sm != null) { sm.RequestParry(); return; }
            // 상태 머신 없으면 직접 절차 동작 플래그 (후일 FBX 클립 "parry" 재생 포인트)
            _actionState = ActionState.Parry;
        }

        public void RequestJump()
        {
            if (_actionState != ActionState.None) return;

            if (_velocityProvider != null)
            {
                // 부모(PlayerMovement)가 점프 처리 — 애니메이션 상태는 UpdateLegPhases()에서 IsGrounded=false로 자연 처리
                return;
            }

            if (!IsGrounded && _coyoteTimer <= 0) return;

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
            if (this == null || !gameObject || !gameObject.activeInHierarchy) return; // 파괴 후 접근 가드 (Play 실측 MissingReferenceException)
            if (_actionState != ActionState.None) return;
            _actionState = ActionState.Gather;
            _actionTimer = 0f;
            _actionTarget = target ?? (transform.position + transform.forward * 1.5f);
        }

        public void RequestRoll()
        {
            if (_actionState != ActionState.None) return;

            if (_velocityProvider != null)
            {
                // 부모(PlayerMovement)가 구르기 처리 — 시각적 롤 애니메이션만 실행
                _actionState = ActionState.Roll;
                _actionTimer = 0f;
                return;
            }

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

        public void RequestStagger()
        {
            if (_actionState != ActionState.None) return;
            _actionState = ActionState.Stagger;
            _actionTimer = 0f;
            _actionTarget = transform.position - transform.forward * 0.5f; // 뒤로 밀림
        }

        public void RequestDeath()
        {
            _actionState = ActionState.Stagger; // 사망도 경직 애니메이션 재사용
            _actionTimer = 0f;
            Destroy(gameObject, 5f); // 5초 후 파괴
        }

        public void RequestMount()
        {
            _actionState = ActionState.Mount;
            _actionTimer = 0f;
        }
        public void RequestDismount()
        {
            _actionState = ActionState.None;
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

            _boneMap.Initialize(_animator, BoneFamilyHint.Biped);
            AllocateNativeArrays();
            NormalizeArmPose(); // [P-ANIM4 Phase 3] 팔 포즈 자동 교정
        }

        /// <summary>
        /// [P-ANIM4 Phase 3] 팔 포즈 자동 교정 — 익명 리그의 팔이 몸 뒤쪽으로 쓸린 바인드 포즈
        /// (미노타우르스 실측)를 수직 아래로 편다. 팔이 이미 자연스럽게 내려와 있으면(전방 도트 ≥ 임계)
        /// no-op — 플레이어 같은 정상 휴머노이드 무영향.
        /// </summary>
        private void NormalizeArmPose()
        {
            if (_boneMap == null) return;
            var lSh = _boneMap.Get(BoneRole.L_Shoulder);
            var lHand = _boneMap.Get(BoneRole.L_Hand);
            var rSh = _boneMap.Get(BoneRole.R_Shoulder);
            var rHand = _boneMap.Get(BoneRole.R_Hand);
            if (lSh == null || lHand == null) return; // 팔 미매핑 — 정상 폴백

            Vector3 cur = (lHand.position - lSh.position).normalized;
            // 팔이 몸 '뒤'로 쓸린 경우만 교정 (진행 방향과 반대 도트)
            if (Vector3.Dot(cur, transform.forward) < -0.05f)
            {
                Quaternion delta = Quaternion.FromToRotation(cur, Vector3.down);
                lSh.localRotation = delta * lSh.localRotation;
                if (rSh != null) rSh.localRotation = delta * rSh.localRotation;
                UnityEngine.Debug.Log($"[ProceduralAnimationController] 팔 포즈 교정 적용 — 뒤로 쓸린 팔을 수직 아래로 ({transform.name})");
            }
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

            // Dispose IK result arrays
            foreach (var arr in _leftIKResults)
                if (arr.IsCreated) arr.Dispose();
            _leftIKResults.Clear();
            foreach (var arr in _rightIKResults)
                if (arr.IsCreated) arr.Dispose();
            _rightIKResults.Clear();

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

            // [P-ANIM6 Phase1] 회전 보행 모드(익명 리그)에선 잡 0 스케줄 — JobTempAlloc temp 누수 원천 차단.
            if (UseJobIK)
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
            DisposeLocomotionTempArrays();
            UpdateGroundDetection();

            // [P-ANIM6 Phase1] 잡 경로(isHuman 플레이어=기존 동작 무변경) vs 회전 보행(익명 리그) 분기.
            if (UseJobIK)
            {
                ScheduleIKJobs();
                ApplyProceduralPose();
            }
            else
            {
                ApplyBipedRotationGait();
            }
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0) return;
            if (!UseJobIK) return; // [P-ANIM6] 회전 보행 모드 — 잡 결과가 없으므로 Animator IK 미적용
            _ikJobHandle.Complete();
            ApplyIKToAnimator();
        }

        // ──────────────────────────────────────────────
        // 입력 & 이동
        // ──────────────────────────────────────────────

        void HandleInput()
        {
            if (_actionState != ActionState.None && _actionState != ActionState.Mount) return;

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
                // 기존 자체 입력 처리 - Input System 사용
                Vector2 input = Vector2.zero;
                if (Keyboard.current != null)
                {
                    if (Keyboard.current.wKey.isPressed) input.y += 1;
                    if (Keyboard.current.sKey.isPressed) input.y -= 1;
                    if (Keyboard.current.aKey.isPressed) input.x -= 1;
                    if (Keyboard.current.dKey.isPressed) input.x += 1;
                }
                input = Vector2.ClampMagnitude(input, 1f);

                bool sprint = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;

                Vector3 localTarget = new Vector3(input.x, 0, input.y);
                _targetVelocity = transform.TransformDirection(localTarget) * (sprint ? runSpeed : walkSpeed);
                _targetSpeed = _targetVelocity.magnitude;

                if (_targetVelocity.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(_targetVelocity.normalized);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
                }
            }

            if (_velocityProvider == null)
            {
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) RequestAttack();
                if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) RequestJump();
                if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) RequestGather();
                if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame) RequestRoll();
            }
        }

        void UpdateMovement()
        {
            _currentVelocity = Vector3.MoveTowards(_currentVelocity, _targetVelocity, acceleration * Time.deltaTime);
            _currentSpeed = _currentVelocity.magnitude;

            if (_velocityProvider != null)
            {
                // 속도 방향 전환율로 lean 계산
                Vector3 horizontalVel = new Vector3(_currentVelocity.x, 0, _currentVelocity.z);
                float angularSpeed = Vector3.SignedAngle(transform.forward, horizontalVel.normalized, Vector3.up) * Time.deltaTime;
                _turnInput = Mathf.Clamp(angularSpeed * 0.1f, -1f, 1f);
            }
            else
            {
                _turnInput = (Keyboard.current != null && Keyboard.current.aKey.isPressed) ? -1f : 
                             (Keyboard.current != null && Keyboard.current.dKey.isPressed) ? 1f : 0f;
            }
            float targetLean = _turnInput * bodyLeanAmount * 15f;
            _bodyLeanOffset = Vector3.Lerp(_bodyLeanOffset, new Vector3(targetLean, 0, 0), Time.deltaTime * 5f);
            _bodyLeanRotation = Quaternion.Lerp(_bodyLeanRotation, Quaternion.Euler(_bodyLeanOffset), Time.deltaTime * 5f);
        }

        void ApplyMovement()
        {
            if (_velocityProvider != null) return; // 부모(PlayerMovement)가 이동 처리
            if (!IsGrounded) return;

            Vector3 move = _currentVelocity * Time.fixedDeltaTime;
            move.y = _rigidbody.linearVelocity.y;
            _rigidbody.linearVelocity = move;
        }

        void ApplyGravity()
        {
            if (_velocityProvider != null) return; // 부모(PlayerMovement)가 중력 처리
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
                case ProceduralAnimStateMachine.State.Locomotion:
                    if (_actionState == ActionState.Mount)
                        UpdateActionMount();
                    break;
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

                case ProceduralAnimStateMachine.State.Stagger:
                    UpdateActionStagger();
                    break;
                case ProceduralAnimStateMachine.State.Death:
                    UpdateActionDeath();
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

        void UpdateActionMount()
        {
            // 탑승 자세: 무릎 구부리기 (사인파로 부드럽게 전환)
            float progress = math.clamp(_actionTimer / 0.3f, 0f, 1f);
            float sitAmount = math.sin(progress * math.PI * 0.5f) * 45f; // 0→45도
            
            var lKnee = _boneMap.Get(BoneRole.L_Knee);
            var rKnee = _boneMap.Get(BoneRole.R_Knee);
            var lElbow = _boneMap.Get(BoneRole.L_Elbow);
            var rElbow = _boneMap.Get(BoneRole.R_Elbow);
            
            if (lKnee != null) lKnee.localRotation *= Quaternion.Euler(sitAmount, 0, 0);
            if (rKnee != null) rKnee.localRotation *= Quaternion.Euler(sitAmount, 0, 0);
            // 팔은 앞으로 (고삐 잡는 자세)
            if (lElbow != null) lElbow.localRotation *= Quaternion.Euler(0, 0, 30f);
            if (rElbow != null) rElbow.localRotation *= Quaternion.Euler(0, 0, -30f);
            
            _actionTimer += Time.deltaTime;
        }

        void UpdateActionStagger()
        {
            _actionTimer += Time.deltaTime;
            float progress = math.clamp(_actionTimer / 0.5f, 0f, 1f);
            
            // 뒤로 기울임 (간단한 사인파)
            float staggerAngle = math.sin(progress * math.PI) * 15f;
            var spine1 = _boneMap.Get(BoneRole.Spine1);
            if (spine1 != null)
            {
                spine1.localRotation *= Quaternion.Euler(staggerAngle, 0, 0);
            }
            
            if (_actionTimer > 0.5f)
                _actionState = ActionState.None;
        }

        void UpdateActionDeath()
        {
            // 천천히 바닥으로 내려감
            transform.position += Vector3.down * Time.deltaTime * 0.5f;
            // 약간 뒤로 넘어짐
            transform.rotation = Quaternion.Slerp(transform.rotation, 
                Quaternion.Euler(90f, transform.eulerAngles.y, 0), Time.deltaTime * 2f);
        }

        // ──────────────────────────────────────────────
        // Job Scheduling
        // ──────────────────────────────────────────────

        void UpdateLegPhases()
        {
            if (!IsGrounded) return;

            // LOD phase update rate: LOD3=culled→skip; LOD2=every 4th frame; LOD1=every other frame
            if (_lodPhaseUpdateRate < 1f && Time.frameCount % (int)(1f / math.max(_lodPhaseUpdateRate, 0.01f)) != 0)
            {
                _leftLegPhaseArr[0] = _leftLegPhase;
                _rightLegPhaseArr[0] = _rightLegPhase;
                return;
            }

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

        void ScheduleLocomotionJobs()
        {
            // Clear previous frame's locomotion TempJob arrays
            DisposeLocomotionTempArrays();

            JobHandle dependency = default;

            bool computeHipShift = _lodHipShiftEnabled;
            bool computeSpineCounter = _lodSpineCounterEnabled;
            int ikIterations = _lodIKIterations;

            // --- Foot Planner (IJobParallelFor, size 1) ---
            var footPlanner = new FootPlannerJob
            {
                BodyPositions = Track(new NativeArray<float3>(1, Allocator.TempJob) { [0] = transform.position }),
                BodyRotations = Track(new NativeArray<quaternion>(1, Allocator.TempJob) { [0] = transform.rotation }),
                BodyVelocities = Track(new NativeArray<float3>(1, Allocator.TempJob) { [0] = _currentVelocity }),
                BodyAngularVelocities = Track(new NativeArray<float3>(1, Allocator.TempJob) { [0] = _rigidbody.angularVelocity }),
                DeltaTimes = Track(new NativeArray<float>(1, Allocator.TempJob) { [0] = Time.deltaTime }),
                StepLengths = Track(new NativeArray<float>(1, Allocator.TempJob) { [0] = 0.6f }),
                StepWidths = Track(new NativeArray<float>(1, Allocator.TempJob) { [0] = 0.15f }),
                MaxStepHeights = Track(new NativeArray<float>(1, Allocator.TempJob) { [0] = 0.25f }),
                GroundCheckDistances = Track(new NativeArray<float>(1, Allocator.TempJob) { [0] = groundCheckDistance }),
                LeftFootCurrents = Track(new NativeArray<float3>(1, Allocator.TempJob) { [0] = _leftFootTarget[0] }),
                RightFootCurrents = Track(new NativeArray<float3>(1, Allocator.TempJob) { [0] = _rightFootTarget[0] }),
                LeftFootGroundedFlags = Track(new NativeArray<bool>(1, Allocator.TempJob) { [0] = _leftFootGrounded }),
                RightFootGroundedFlags = Track(new NativeArray<bool>(1, Allocator.TempJob) { [0] = _rightFootGrounded }),
                LeftPhases = Track(new NativeArray<float>(1, Allocator.TempJob) { [0] = _leftLegPhase }),
                RightPhases = Track(new NativeArray<float>(1, Allocator.TempJob) { [0] = _rightLegPhase }),
                DutyCycles = Track(new NativeArray<float>(1, Allocator.TempJob) { [0] = _dutyCycle }),
                Speeds = Track(new NativeArray<float>(1, Allocator.TempJob) { [0] = _currentSpeed }),

                OutLeftTargets = _leftFootTarget,
                OutRightTargets = _rightFootTarget,
                OutLeftHints = _leftFootHint,
                OutRightHints = _rightFootHint,
                OutLeftGroundPositions = _leftFootPos,
                OutRightGroundPositions = _rightFootPos,
                OutLeftCanStepFlags = Track(new NativeArray<bool>(1, Allocator.TempJob)),
                OutRightCanStepFlags = Track(new NativeArray<bool>(1, Allocator.TempJob)),
            };
            dependency = footPlanner.Schedule(1, 1, dependency);

            // --- Hip Shift ---
            if (computeHipShift)
            {
                var hipShift = new HipShiftJob
                {
                    LeftPhases = Track(new NativeArray<float>(1, Allocator.TempJob) { [0] = _leftLegPhase }),
                    RightPhases = Track(new NativeArray<float>(1, Allocator.TempJob) { [0] = _rightLegPhase }),
                    DutyCycles = Track(new NativeArray<float>(1, Allocator.TempJob) { [0] = _dutyCycle }),
                    LeftWeights = Track(new NativeArray<float>(1, Allocator.TempJob) { [0] = _leftFootGrounded ? 1f : 0f }),
                    RightWeights = Track(new NativeArray<float>(1, Allocator.TempJob) { [0] = _rightFootGrounded ? 1f : 0f }),
                    MaxLateralShifts = Track(new NativeArray<float>(1, Allocator.TempJob) { [0] = 0.1f }),
                    MaxVerticalShifts = Track(new NativeArray<float>(1, Allocator.TempJob) { [0] = 0.05f }),
                    Speeds = Track(new NativeArray<float>(1, Allocator.TempJob) { [0] = _currentSpeed }),
                    TurnAmounts = Track(new NativeArray<float>(1, Allocator.TempJob) { [0] = _turnInput }),

                    OutHipOffsets = _hipOffset,
                    OutHipHeightOffsets = _hipHeightOffset,
                    OutHipRotations = Track(new NativeArray<quaternion>(1, Allocator.TempJob)),
                };
                dependency = hipShift.Schedule(1, 1, dependency);
            }

            // --- Spine Counter-Rotation ---
            if (computeSpineCounter)
            {
                var spineCounter = new SpineCounterRotationJob
                {
                    LeftPhases = Track(new NativeArray<float>(1, Allocator.TempJob) { [0] = _leftLegPhase }),
                    RightPhases = Track(new NativeArray<float>(1, Allocator.TempJob) { [0] = _rightLegPhase }),
                    DutyCycles = Track(new NativeArray<float>(1, Allocator.TempJob) { [0] = _dutyCycle }),
                    MaxCounterRotations = Track(new NativeArray<float>(1, Allocator.TempJob) { [0] = 8f }),
                    BodyVelocities = Track(new NativeArray<float3>(1, Allocator.TempJob) { [0] = _currentVelocity }),
                    BodyRotations = Track(new NativeArray<quaternion>(1, Allocator.TempJob) { [0] = transform.rotation }),
                    SpineSegmentCounts = Track(new NativeArray<int>(1, Allocator.TempJob) { [0] = 3 }),
                    OutSpineRotations = _spineRotations,
                    MaxSpineSegments = 3,
                };
                dependency = spineCounter.Schedule(dependency);
            }

            _locomotionJobHandle = dependency;
        }

        void ScheduleIKJobs()
        {
            JobHandle dependency = _locomotionJobHandle;
            UpdateBonePositions();

            int ikIterations = _lodIKIterations;

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

                // Use IJobParallelFor batch IK (size 1)
                var leftRootRotations = new NativeArray<quaternion>(1, Allocator.TempJob);
                var leftMidRotations = new NativeArray<quaternion>(1, Allocator.TempJob);
                var leftTipRotations = new NativeArray<quaternion>(1, Allocator.TempJob);
                var leftSuccess = new NativeArray<bool>(1, Allocator.TempJob);

                var leftIK = new LimbIKJob
                {
                    RootPositions = new NativeArray<float3>(1, Allocator.TempJob) { [0] = chain.Root.position },
                    MidPositions = new NativeArray<float3>(1, Allocator.TempJob) { [0] = chain.Mid.position },
                    TipPositions = new NativeArray<float3>(1, Allocator.TempJob) { [0] = chain.Tip.position },
                    TargetPositions = new NativeArray<float3>(1, Allocator.TempJob) { [0] = _leftFootTarget[0] },
                    HintPositions = new NativeArray<float3>(1, Allocator.TempJob) { [0] = _leftFootHint[0] },
                    UpperLengths = new NativeArray<float>(1, Allocator.TempJob) { [0] = chain.UpperLength },
                    LowerLengths = new NativeArray<float>(1, Allocator.TempJob) { [0] = chain.LowerLength },
                    OutRootPositions = new NativeArray<float3>(1, Allocator.TempJob),
                    OutMidPositions = new NativeArray<float3>(1, Allocator.TempJob),
                    OutTipPositions = new NativeArray<float3>(1, Allocator.TempJob),
                    OutRootRotations = leftRootRotations,
                    OutMidRotations = leftMidRotations,
                    OutTipRotations = leftTipRotations,
                    OutSuccess = leftSuccess,
                    Iterations = ikIterations,
                };
                var leftHandle = leftIK.Schedule(1, 1, dependency);
                _leftIKHandles.Add(leftHandle);
                _leftIKResults.Add(leftRootRotations);
                _leftIKResults.Add(leftMidRotations);
                _leftIKResults.Add(leftTipRotations);
                _leftIKSuccess.Add(leftSuccess);
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

                var rightRootRotations = new NativeArray<quaternion>(1, Allocator.TempJob);
                var rightMidRotations = new NativeArray<quaternion>(1, Allocator.TempJob);
                var rightTipRotations = new NativeArray<quaternion>(1, Allocator.TempJob);
                var rightSuccess = new NativeArray<bool>(1, Allocator.TempJob);

                var rightIK = new LimbIKJob
                {
                    RootPositions = new NativeArray<float3>(1, Allocator.TempJob) { [0] = chain.Root.position },
                    MidPositions = new NativeArray<float3>(1, Allocator.TempJob) { [0] = chain.Mid.position },
                    TipPositions = new NativeArray<float3>(1, Allocator.TempJob) { [0] = chain.Tip.position },
                    TargetPositions = new NativeArray<float3>(1, Allocator.TempJob) { [0] = _rightFootTarget[0] },
                    HintPositions = new NativeArray<float3>(1, Allocator.TempJob) { [0] = _rightFootHint[0] },
                    UpperLengths = new NativeArray<float>(1, Allocator.TempJob) { [0] = chain.UpperLength },
                    LowerLengths = new NativeArray<float>(1, Allocator.TempJob) { [0] = chain.LowerLength },
                    OutRootPositions = new NativeArray<float3>(1, Allocator.TempJob),
                    OutMidPositions = new NativeArray<float3>(1, Allocator.TempJob),
                    OutTipPositions = new NativeArray<float3>(1, Allocator.TempJob),
                    OutRootRotations = rightRootRotations,
                    OutMidRotations = rightMidRotations,
                    OutTipRotations = rightTipRotations,
                    OutSuccess = rightSuccess,
                    Iterations = ikIterations,
                };
                var rightHandle = rightIK.Schedule(1, 1, dependency);
                _rightIKHandles.Add(rightHandle);
                _rightIKResults.Add(rightRootRotations);
                _rightIKResults.Add(rightMidRotations);
                _rightIKResults.Add(rightTipRotations);
                _rightIKSuccess.Add(rightSuccess);
                dependency = rightHandle;
            }

            _ikJobHandle = dependency;
        }

        List<JobHandle> _leftIKHandles = new List<JobHandle>();
        List<JobHandle> _rightIKHandles = new List<JobHandle>();
        List<NativeArray<quaternion>> _leftIKResults = new List<NativeArray<quaternion>>();
        List<NativeArray<quaternion>> _rightIKResults = new List<NativeArray<quaternion>>();
        List<NativeArray<bool>> _leftIKSuccess = new List<NativeArray<bool>>();
        List<NativeArray<bool>> _rightIKSuccess = new List<NativeArray<bool>>();

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
            // LOD raycast reduction: skip raycasts based on level
            // LOD0: every frame, LOD1: every other frame, LOD2+: never
            if (!_lodRaycastEnabled)
            {
                // Use simple fallback: project feet down by fixed amount
                _leftFootGrounded = false;
                _rightFootGrounded = false;
                _leftFootGroundedArr[0] = false;
                _rightFootGroundedArr[0] = false;
                return;
            }

            // LOD1: raycast every other frame
            if (_currentLODLevel == 1 && Time.frameCount % 2 != 0)
                return;

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
        // TempJob Array Tracking & Disposal
        // ──────────────────────────────────────────────

        T Track<T>(T array) where T : struct, System.IDisposable
        {
            if (array is NativeArray<float3> f3) _locomotionFloat3Arrays.Add(f3);
            else if (array is NativeArray<quaternion> q) _locomotionQuaternionArrays.Add(q);
            else if (array is NativeArray<float> f) _locomotionFloatArrays.Add(f);
            else if (array is NativeArray<bool> b) _locomotionBoolArrays.Add(b);
            else if (array is NativeArray<int> i) _locomotionIntArrays.Add(i);
            return array;
        }

        void DisposeLocomotionTempArrays()
        {
            foreach (var arr in _locomotionFloat3Arrays) if (arr.IsCreated) arr.Dispose();
            _locomotionFloat3Arrays.Clear();

            foreach (var arr in _locomotionQuaternionArrays) if (arr.IsCreated) arr.Dispose();
            _locomotionQuaternionArrays.Clear();

            foreach (var arr in _locomotionFloatArrays) if (arr.IsCreated) arr.Dispose();
            _locomotionFloatArrays.Clear();

            foreach (var arr in _locomotionBoolArrays) if (arr.IsCreated) arr.Dispose();
            _locomotionBoolArrays.Clear();

            foreach (var arr in _locomotionIntArrays) if (arr.IsCreated) arr.Dispose();
            _locomotionIntArrays.Clear();
        }

        // ──────────────────────────────────────────────
        // 프로시저럴 포즈 적용 (메인 스레드)
        // ──────────────────────────────────────────────

        void ApplyProceduralPose()
        {
            // 플레이어는 HumanoidClipDriver(클립 애니메이션)가 같은 골격을 구동 → 여기가 매 프레임
            // 이 프로시저럴 포즈를 덮어쓰면 두 시스템이 충돌해 끊긴다. 클립 드라이버가 있으면 스킵.
            // (병사/몬스터처럼 HumanoidClipDriver가 없는 객체는 프로시저럴 동작 그대로 유지)
            // HumanoidClipDriver는 플레이어 루트가 아닌 자식 PlayerBody에 붙으므로(Assets/GameSetup.cs),
            // InParent(자기+조상)만으론 자기 자신/자식의 드라이버를 못 찾는다. InChildren 병행으로 커버.
            if (GetComponentInParent<HumanoidClipDriver>() != null ||
                GetComponentInChildren<HumanoidClipDriver>() != null) return;

            ApplyFootIK();
            ApplySpineIK();
            ApplyHeadLook();
            ApplyBodyLean();
            ApplyHipShift();
        }

        // ──────────────────────────────────────────────
        // [P-ANIM6 Phase1] 회전 기반 2족 보행 — 잡(IJob) 체인 완전 우회
        // 4족 ApplyRotationGait(P-ANIM5-B) 패턴 이식: 월드 기준 회전 + 기준 localRotation 캐시.
        // 익명 리그(isHuman=false, 미노타우르스 등) 전용 — 플레이어 휴머노이드는 UseJobIK 게이트로 잡 경로 유지.
        // 다리=L_Hip/R_Hip 전후 스윙(좌우 0.5 위상 교차), 무릎=스윙 전반부(발 들기) 굽힘,
        // 팔=L_Shoulder/R_Shoulder 다리 역위상 스윙(진폭 40%). Root 본은 건드리지 않는다(바운스 경합 방지).
        // ──────────────────────────────────────────────

        void ApplyBipedRotationGait()
        {
            // HumanoidClipDriver 충돌 회피 — ApplyProceduralPose와 동일 규약(클립 애니와 골격 공유 방지).
            if (GetComponentInParent<HumanoidClipDriver>() != null ||
                GetComponentInChildren<HumanoidClipDriver>() != null) return;

            bool canGait = _actionState == ActionState.None && IsGrounded && _currentSpeed > 0.1f;
            if (!canGait)
            {
                // 정지/공중/액션 — 회전 보행 중단. 스윙 자세 잔존 방지로 1회 기본 포즈 복원.
                if (_gaitActive) RestoreGaitBase();
                // [P-ANIM7 Phase2] 지상 정지 중엔 idle 호흡 인수(액션/공중은 본 건드리지 않음).
                if (_actionState == ActionState.None && IsGrounded && _currentSpeed <= 0.1f)
                    ApplyBipedIdle();
                else if (_idleActive)
                    RestoreIdleBase(); // 액션 시작/이륙 — idle 자세 원복(Action 코드에 포즈 인계)
                return;
            }

            // idle → gait 전환: idle 자세 원복 후 스윙(base 재포착).
            if (_idleActive) RestoreIdleBase();

            // [P-ANIM2/4족 수리 동일] 스윙 각도 = 실속도 비례 클램프 — 보폭과 이동속도 동기(발 미끄러짐 제거)
            float swingDeg = Mathf.Clamp(_currentSpeed * _gaitSwingSpeedFactor, _gaitMinSwingDeg, _gaitMaxSwingDeg);
            Vector3 axis = transform.right; // 진행 방향에 수직 — 전후 스윙

            // 위상은 UpdateLegPhases의 _leftLegPhase(초기 0)/_rightLegPhase(초기 0.5) 재사용 —
            // 좌우 이미 0.5 교차 상태라 별도 오프셋 없이 소비(속도/스텝길이 동기 유지 = 발 미끄러짐 방지).
            SwingBipedLeg(BoneRole.L_Hip, BoneRole.L_Knee, _leftLegPhase, axis, swingDeg);
            SwingBipedLeg(BoneRole.R_Hip, BoneRole.R_Knee, _rightLegPhase, axis, swingDeg);

            // 팔 — 다리와 역위상(좌팔=우다리 위상, 우팔=좌다리 위상): 자연스러운 팔 흔들기.
            SwingBipedArm(BoneRole.L_Shoulder, _rightLegPhase, axis, swingDeg);
            SwingBipedArm(BoneRole.R_Shoulder, _leftLegPhase, axis, swingDeg);
            ApplyBipedGaitUpperBody(_leftLegPhase, transform.forward);

            _gaitActive = true;
        }

        /// <summary>[P-ANIM6] 다리 스윙 — 힙 전후 스윙(±sin) + 무릎 굽힘(스윙 전반부=발 들기). </summary>
        void SwingBipedLeg(BoneRole hipRole, BoneRole kneeRole, float phase, Vector3 axis, float swingDeg)
        {
            Transform hip = _boneMap.Has(hipRole) ? _boneMap.Get(hipRole) : null;

            // [P-ANIM9] 축 불변 다리 스윙 — 진행 방향과 다리 축에 수직인 축.(루트 lateral 무관).
            Transform kneeRef = _boneMap.Has(kneeRole) ? _boneMap.Get(kneeRole) : null;
            Vector3 swingAxis = axis; // fallback = transform.right
            if (hip != null && kneeRef != null)
            {
                Vector3 legDir = kneeRef.position - hip.position;
                if (legDir.sqrMagnitude > 1e-6f)
                {
                    Vector3 c = Vector3.Cross(legDir.normalized, transform.forward);
                    if (c.sqrMagnitude > 1e-4f)
                    {
                        swingAxis = c.normalized;
                        if (Vector3.Dot(swingAxis, transform.right) < 0f) swingAxis = -swingAxis;
                    }
                }
            }

            // 다리 스윙 — 4족 SwingLeg와 동일한 월드 기준 회전 합성(축 불변).
            if (hip != null && hip.parent != null)
            {
                if (!_gaitBaseRot.TryGetValue(hip, out var hipBase))
                {
                    _gaitBaseRot[hip] = hip.localRotation; // 첫 프레임은 기준 포착만
                }
                else
                {
                    // 양의 회전각=발 후방(Unity 오른손 축 관례) — 전방 스윙이 음의 각.
                    float hipAngle = Mathf.Sin(phase * Mathf.PI * 2f) * swingDeg;
                    Quaternion hipBaseWorld = hip.parent.rotation * hipBase;
                    Quaternion hipTargetWorld = hipBaseWorld * Quaternion.AngleAxis(hipAngle, swingAxis);
                    hip.localRotation = Quaternion.Inverse(hip.parent.rotation) * hipTargetWorld;
                }
            }

            // 무릎 굽힘 — 다리가 후방→전방으로 지나가는 전반부만 굽힘(발 들기 흉내).
            // 다리가 중립을 전방 방향으로 통과할 때(cos 피크) 굽힘 최대 — 보행 사이클과 정합.
            Transform knee = _boneMap.Has(kneeRole) ? _boneMap.Get(kneeRole) : null;
            if (knee != null && knee.parent != null)
            {
                if (!_gaitBaseRot.TryGetValue(knee, out var kneeBase))
                {
                    _gaitBaseRot[knee] = knee.localRotation; // 첫 프레임은 기준 포착만
                }
                else
                {
                    float bend = Mathf.Max(0f, -Mathf.Cos(phase * Mathf.PI * 2f))
                               * _gaitKneeBendFactor * swingDeg;
                    Quaternion kneeBaseWorld = knee.parent.rotation * kneeBase;
                    Quaternion kneeTargetWorld = kneeBaseWorld * Quaternion.AngleAxis(bend, swingAxis);
                    knee.localRotation = Quaternion.Inverse(knee.parent.rotation) * kneeTargetWorld;
                }
            }
        }

        /// <summary>[P-ANIM6] 팔 스윙 — 같은 축 전후 스윙, 진폭은 다리의 _gaitArmSwingFactor 배(기본 40%).</summary>
        void SwingBipedArm(BoneRole shoulderRole, float phase, Vector3 axis, float swingDeg)
        {
            Transform shoulder = _boneMap.Has(shoulderRole) ? _boneMap.Get(shoulderRole) : null;
            if (shoulder == null || shoulder.parent == null) return;

            // [P-ANIM9] 축 불변 팔 스윙 — 팔 본축과 진행 방향에 수직인 축(어깨→손 방향)으로 회전.
            // 익명 리그에서 팔이 옆/뒤로 붙어 있어 루트 lateral 축 회전이 팔을 몸 안쪽으로 비틀던 문제 수정.
            BoneRole handRole = (shoulderRole == BoneRole.L_Shoulder) ? BoneRole.L_Hand : BoneRole.R_Hand;
            Transform handRef = _boneMap.Has(handRole) ? _boneMap.Get(handRole) : null;
            Vector3 swingAxis = axis; // fallback = transform.right
            if (handRef != null)
            {
                Vector3 armDir = handRef.position - shoulder.position;
                if (armDir.sqrMagnitude > 1e-6f)
                {
                    Vector3 c = Vector3.Cross(armDir.normalized, transform.forward);
                    if (c.sqrMagnitude > 1e-4f)
                    {
                        swingAxis = c.normalized;
                        if (Vector3.Dot(swingAxis, transform.right) < 0f) swingAxis = -swingAxis;
                    }
                }
            }

            if (!_gaitBaseRot.TryGetValue(shoulder, out var baseLocal))
            {
                _gaitBaseRot[shoulder] = shoulder.localRotation; // 첫 프레임은 기준 포착만
                return;
            }

            float angle = Mathf.Sin(phase * Mathf.PI * 2f) * swingDeg * _gaitArmSwingFactor;
            Quaternion baseWorld = shoulder.parent.rotation * baseLocal;
            Quaternion targetWorld = baseWorld * Quaternion.AngleAxis(angle, swingAxis);
            shoulder.localRotation = Quaternion.Inverse(shoulder.parent.rotation) * targetWorld;
        }

        /// <summary>[P-ANIM8 Phase2] 이동 중 골반 체중이동 + 척추 카운터 롤. Root object 자체는 변경하지 않는다.</summary>
        void ApplyBipedGaitUpperBody(float phase, Vector3 rollAxis)
        {
            Transform pelvis = _boneMap.Get(BoneRole.Root);
            if (pelvis != null && pelvis != transform && pelvis.parent != null)
                ApplyGaitRoll(pelvis, BipedGaitUpperBody.PelvisWave(phase) * _gaitPelvisRollDeg, rollAxis);

            Transform spine = _boneMap.Get(BoneRole.Spine0);
            bool spineAlreadyDrivenByHipSwing = spine == _boneMap.Get(BoneRole.L_Hip)
                || spine == _boneMap.Get(BoneRole.R_Hip);
            if (spine != null && spine != transform && spine != pelvis && !spineAlreadyDrivenByHipSwing && spine.parent != null)
                ApplyGaitRoll(spine, BipedGaitUpperBody.SpineWave(phase)
                    * _gaitPelvisRollDeg * _gaitSpineCounterFactor, rollAxis);
        }

        void ApplyGaitRoll(Transform bone, float angle, Vector3 axis)
        {
            if (!_gaitBaseRot.TryGetValue(bone, out var baseLocal))
            {
                _gaitBaseRot[bone] = bone.localRotation;
                return;
            }
            Quaternion baseWorld = bone.parent.rotation * baseLocal;
            Quaternion targetWorld = baseWorld * Quaternion.AngleAxis(angle, axis);
            bone.localRotation = Quaternion.Inverse(bone.parent.rotation) * targetWorld;
        }

        /// <summary>[P-ANIM6] 회전 보행 종료/정지 시 캐시된 기준 localRotation 복원(스윙 자세 잔존 방지).
        /// 캐시까지 클리어 — 다음 보행 세션에서 골격 기저 변화(프로필 재적용 등)를 재포착한다.</summary>
        void RestoreGaitBase()
        {
            foreach (var kvp in _gaitBaseRot)
            {
                if (kvp.Key != null) kvp.Key.localRotation = kvp.Value;
            }
            _gaitBaseRot.Clear();
            _gaitActive = false;
        }

        // ──────────────────────────────────────────────
        // [P-ANIM7 Phase2] 정지 idle — 어깨 호흡 + 척추 미세 피치
        // 익명 리그 2족 전용(UseJobIK=false 경로에서만 호출 — 플레이어는 잡 경로라 무영향).
        // 회전 모드에선 다른 시스템이 어깨/척추를 구동하지 않아 경합 없음. base 캐시 절대 세팅(드리프트 없음).
        // ──────────────────────────────────────────────

        /// <summary>정지 중 생동감 — 어깨 미세 롤(호흡) + 척추0 미세 피치. 매 프레임 절대 세팅(누적 없음).</summary>
        void ApplyBipedIdle()
        {
            float t = Time.time * _idleHz * 2f * Mathf.PI;
            float breath = Mathf.Sin(t);
            BreatheBone(BoneRole.L_Shoulder, _idleBreathDeg, breath);
            BreatheBone(BoneRole.R_Shoulder, _idleBreathDeg, breath);
            BreatheBone(BoneRole.Spine0, _idleBreathDeg * 0.5f, Mathf.Sin(t * 0.5f));
            _idleActive = true;
        }

        /// <summary>idle 호흡 단일 본 적용 — 첫 프레임은 기준 포착만(2족 gait 캐시와 동일 규약).</summary>
        void BreatheBone(BoneRole role, float ampDeg, float wave)
        {
            Transform b = _boneMap.Has(role) ? _boneMap.Get(role) : null;
            if (b == null || b.parent == null) return;
            if (!_idleBaseRot.TryGetValue(b, out var baseLocal))
            {
                _idleBaseRot[b] = b.localRotation;
                return;
            }
            b.localRotation = baseLocal * Quaternion.Euler(wave * ampDeg, 0f, wave * ampDeg * 0.3f);
        }

        /// <summary>[P-ANIM7] idle 종료 시 캐시된 기준 localRotation 복원 + 클리어.</summary>
        void RestoreIdleBase()
        {
            foreach (var kvp in _idleBaseRot)
            {
                if (kvp.Key != null) kvp.Key.localRotation = kvp.Value;
            }
            _idleBaseRot.Clear();
            _idleActive = false;
        }

        void ApplyFootIK()
        {
            // Complete left leg IK handles
            foreach (var handle in _leftIKHandles)
                handle.Complete();
            _leftIKHandles.Clear();

            // Complete right leg IK handles
            foreach (var handle in _rightIKHandles)
                handle.Complete();
            _rightIKHandles.Clear();

            // Read LimbIKJob outputs and dispose
            if (_leftIKResults.Count >= 3 && _leftIKSuccess.Count > 0)
            {
                bool leftSuccess = _leftIKSuccess[0].IsCreated && _leftIKSuccess[0][0];
                if (leftSuccess)
                {
                    var rootRot = _leftIKResults[0][0];
                    var midRot = _leftIKResults[1][0];
                    var tipRot = _leftIKResults[2][0];

                    if (_boneMap.Has(BoneRole.L_Hip))
                        _boneMap.Get(BoneRole.L_Hip).rotation = rootRot;
                    if (_boneMap.Has(BoneRole.L_Knee))
                        _boneMap.Get(BoneRole.L_Knee).rotation = midRot;
                    if (_boneMap.Has(BoneRole.L_Ankle))
                        _boneMap.Get(BoneRole.L_Ankle).rotation = tipRot;
                }

                foreach (var arr in _leftIKResults)
                    if (arr.IsCreated) arr.Dispose();
                foreach (var arr in _leftIKSuccess)
                    if (arr.IsCreated) arr.Dispose();
                _leftIKResults.Clear();
                _leftIKSuccess.Clear();
            }

            if (_rightIKResults.Count >= 3 && _rightIKSuccess.Count > 0)
            {
                bool rightSuccess = _rightIKSuccess[0].IsCreated && _rightIKSuccess[0][0];
                if (rightSuccess)
                {
                    var rootRot = _rightIKResults[0][0];
                    var midRot = _rightIKResults[1][0];
                    var tipRot = _rightIKResults[2][0];

                    if (_boneMap.Has(BoneRole.R_Hip))
                        _boneMap.Get(BoneRole.R_Hip).rotation = rootRot;
                    if (_boneMap.Has(BoneRole.R_Knee))
                        _boneMap.Get(BoneRole.R_Knee).rotation = midRot;
                    if (_boneMap.Has(BoneRole.R_Ankle))
                        _boneMap.Get(BoneRole.R_Ankle).rotation = tipRot;
                }

                foreach (var arr in _rightIKResults)
                    if (arr.IsCreated) arr.Dispose();
                foreach (var arr in _rightIKSuccess)
                    if (arr.IsCreated) arr.Dispose();
                _rightIKResults.Clear();
                _rightIKSuccess.Clear();
            }

            // Also apply main-thread IK as fallback for success=false chains
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

    /// <summary>Phase-synchronous biped gait torso weight-shift waves.</summary>
    internal static class BipedGaitUpperBody
    {
        internal static float PelvisWave(float phase) => Mathf.Sin(Mathf.Repeat(phase, 1f) * Mathf.PI * 2f);
        internal static float SpineWave(float phase) => -PelvisWave(phase);
    }
}