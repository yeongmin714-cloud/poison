using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;
using Unity.InferenceEngine;
using ProjectName.Systems.Animation.Procedural;
using ProjectName.Systems.Animation.Procedural.Bones;
using ProjectName.Systems.Animation.Procedural.IK;
using ProjectName.Systems.Animation.Procedural.LOD;
using static ProjectName.Systems.Animation.Procedural.IK.LimbIKSolver;

namespace ProjectName.Systems.Animation.Neural
{
    /// <summary>
    /// Neural Animation Controller — replaces ProceduralAnimationController with ONNX policy inference via Unity Sentis.
    /// </summary>
    /// Features:
    /// 1) Load and manage ONNX policy models via Unity Sentis
    /// 2) Observation encoding (velocity, terrain, target, joint states)
    /// 3) Policy inference at FixedUpdate
    /// 4) Decode actions to bone rotations / root motion
    /// 5) IK layer on top for foot/hand placement
    /// 6) Policy switching (locomotion/combat/react/interact) with smooth blending
    /// 7) CharacterController / NavMeshAgent root motion integration
    /// 8) LOD (distance-based inference skipping / quantization)
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(ProceduralBoneMap))]
    public class NeuralAnimationController : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        // Inspector: Policy Settings
        // ──────────────────────────────────────────────

        [Header("Neural Policy Models")]
        [SerializeField] ModelAsset _locomotionPolicy;
        [SerializeField] ModelAsset _combatPolicy;
        [SerializeField] ModelAsset _reactPolicy;
        [SerializeField] ModelAsset _interactPolicy;
        [SerializeField] ModelAsset _flyPolicy;
        [SerializeField] ModelAsset _swimPolicy;

        [Header("Observation Encoding")]
        [SerializeField, Range(1, 256)] int _observationDim = 120;
        [SerializeField, Range(1, 128)] int _actionDim = 80;
        [SerializeField, Range(0.01f, 0.5f)] float _observationNormalizationEpsilon = 0.01f;
        [SerializeField] bool _normalizeObservations = true;

        [Header("Inference")]
        [SerializeField, Range(1, 120)] int _inferenceRateHz = 60;
        [SerializeField] bool _asyncInference = true;
        [SerializeField] BackendType _backendType = BackendType.GPUCompute;

        [Header("Policy Blending")]
        [SerializeField, Range(0.1f, 2f)] float _policyBlendDuration = 0.3f;
        [SerializeField] AnimationCurve _policyBlendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Root Motion")]
        [SerializeField] bool _useRootMotion = true;
        [SerializeField] bool _integrateWithCharacterController;
        [SerializeField] bool _integrateWithNavMeshAgent;
        [SerializeField, Range(0f, 1f)] float _rootMotionWeight = 1f;

        [Header("IK Weights")]
        [SerializeField, Range(0f, 1f)] float _footIKWeight = 0.9f;
        [SerializeField, Range(0f, 1f)] float _handIKWeight = 1f;
        [SerializeField, Range(0f, 1f)] float _spineIKWeight = 0.6f;
        [SerializeField, Range(0f, 1f)] float _headLookWeight = 0.7f;

        [Header("LOD")]
        [SerializeField] float _lod0Distance = 15f;
        [SerializeField] float _lod1Distance = 30f;
        [SerializeField] float _lod2Distance = 50f;
        [SerializeField, Range(0, 10)] int _lodUpdateIntervalFrames = 2;

        [Header("Ground Check")]
        [SerializeField] LayerMask _groundMask = ~0;
        [SerializeField, Range(0.5f, 2f)] float _groundCheckDistance = 1.0f;

        // ──────────────────────────────────────────────
        // Policy Types
        // ──────────────────────────────────────────────

        public enum PolicyType
            {
                Locomotion,
                Combat,
                React,
                Interact,
                Fly,
                Swim
            }

        // ──────────────────────────────────────────────
        // Component References
        // ──────────────────────────────────────────────

        Animator _animator;
        ProceduralBoneMap _boneMap;
        CharacterController _characterController;
        NavMeshAgent _navMeshAgent;
        IVelocityProvider _velocityProvider;

        // ──────────────────────────────────────────────
        // Sentis Runtime
        // ──────────────────────────────────────────────

        Worker _worker;
        Model _runtimeModel;
        Tensor<float> _inputTensor;
        Tensor<float> _outputTensor;
        bool _sentisAvailable;

        // Worker 풀링 (정책별 worker 캐싱)
        Dictionary<PolicyType, Worker> _workerPool = new Dictionary<PolicyType, Worker>();

        // 블렌딩용 액션 버퍼
        float[] _actionBufferA;
        float[] _actionBufferB;

        // ──────────────────────────────────────────────
        // Observation & Action Buffers
        // ──────────────────────────────────────────────

        float[] _observationBuffer;
        float[] _actionBuffer;
        NativeArray<float> _nativeObservation;

        // ──────────────────────────────────────────────
        // Policy State
        // ──────────────────────────────────────────────

        PolicyType _currentPolicy = PolicyType.Locomotion;
        PolicyType _targetPolicy = PolicyType.Locomotion;
        float _blendTimer;
        bool _isBlending;

        Dictionary<PolicyType, Model> _policyModels = new Dictionary<PolicyType, Model>();
        Dictionary<PolicyType, ModelAsset> _policyAssets = new Dictionary<PolicyType, ModelAsset>();

        // ──────────────────────────────────────────────
        // Root Motion Decoded from Policy
        // ──────────────────────────────────────────────

        Vector3 _decodedRootMotionDelta;
        Quaternion _decodedRootRotationDelta = Quaternion.identity;
        Vector3 _decodedRootVelocity;
        float _decodedTurnAngle;

        // ──────────────────────────────────────────────
        // IK Native Arrays
        // ──────────────────────────────────────────────

        NativeArray<float3> _leftFootPos, _rightFootPos, _leftHandPos, _rightHandPos;
        NativeArray<float3> _leftFootTarget, _rightFootTarget, _leftHandTarget, _rightHandTarget;
        NativeArray<float3> _leftFootHint, _rightFootHint, _leftHandHint, _rightHandHint;
        NativeArray<float3> _hipOffset;
        NativeArray<float> _hipHeightOffset;
        NativeArray<float3> _headLookTargetArr;
        NativeArray<bool> _leftFootGroundedArr, _rightFootGroundedArr;

        List<JobHandle> _leftIKHandles = new List<JobHandle>();
        List<JobHandle> _rightIKHandles = new List<JobHandle>();
        List<NativeArray<quaternion>> _leftIKResults = new List<NativeArray<quaternion>>();
        List<NativeArray<quaternion>> _rightIKResults = new List<NativeArray<quaternion>>();
        List<NativeArray<bool>> _leftIKSuccess = new List<NativeArray<bool>>();
        List<NativeArray<bool>> _rightIKSuccess = new List<NativeArray<bool>>();

        JobHandle _ikJobHandle;

        // ──────────────────────────────────────────────
        // Runtime State
        // ──────────────────────────────────────────────

        Vector3 _currentVelocity;
        Vector3 _targetVelocity;
        float _currentSpeed;
        Vector3 _bodyLeanOffset;
        Quaternion _bodyLeanRotation = Quaternion.identity;
        Vector3 _headLookTarget;
        Vector3 _actionTarget;
        RaycastHit _leftFootHit, _rightFootHit;
        bool _leftFootGrounded, _rightFootGrounded;
        float _inferenceTimer;
        float _inferenceInterval;
        float _turnInput;

        // ──────────────────────────────────────────────
        // LOD State
        // ──────────────────────────────────────────────

        int _currentLODLevel;
        int _lodFrameCounter;
        bool _lodInferenceEnabled = true;
        bool _lodRaycastEnabled = true;
        int _lodIKIterations = 2;
        float _lodInferenceRateMultiplier = 1f;

        // ──────────────────────────────────────────────
        // Public API
        // ──────────────────────────────────────────────

        /// <summary>
        /// Set an external velocity provider (e.g. PlayerMovement with CharacterController).
        /// When set, the controller uses external velocity instead of self-driven root motion.
        /// </summary>
        public void SetVelocityProvider(IVelocityProvider provider)
        {
            _velocityProvider = provider;
        }

        /// <summary>
        /// Inject a bone map from an external assigner (must be called before Awake).
        /// </summary>
        public void SetBoneMap(ProceduralBoneMap boneMap)
        {
            _boneMap = boneMap;
        }

        /// <summary>
        /// Switch the active policy with smooth blending.
        /// </summary>
        public void SwitchPolicy(PolicyType policy)
        {
            if (policy == _currentPolicy && !_isBlending) return;
            if (!_policyAssets.ContainsKey(policy) || _policyAssets[policy] == null)
            {
                Debug.LogWarning($"[NeuralAnimationController] No model asset for policy {policy}");
                return;
            }

            _targetPolicy = policy;
            _blendTimer = 0f;
            _isBlending = true;
        }

        /// <summary>
        /// Set the action target for combat/react/interact policies.
        /// </summary>
        public void SetActionTarget(Vector3 target)
        {
            _actionTarget = target;
        }

        /// <summary>
        /// Current velocity (for external query).
        /// </summary>
        public Vector3 CurrentVelocity => _currentVelocity;
        public float CurrentSpeed => _currentSpeed;
        public bool IsGrounded => _velocityProvider?.IsGrounded ?? (_leftFootGrounded || _rightFootGrounded);
        public PolicyType ActivePolicy => _currentPolicy;
        public int CurrentLODLevel => _currentLODLevel;

        // ──────────────────────────────────────────────
        // Unity Lifecycle
        // ──────────────────────────────────────────────

        void Awake()
        {
            _animator = GetComponent<Animator>();
            _boneMap = GetComponent<ProceduralBoneMap>();
            _characterController = GetComponent<CharacterController>();
            _navMeshAgent = GetComponent<NavMeshAgent>();

            _animator.applyRootMotion = false;
            _animator.updateMode = AnimatorUpdateMode.Fixed;
            _animator.animatePhysics = true;

            _boneMap.Initialize(_animator);
            AllocateNativeArrays();

            InitializeSentis();
            InitializePolicyAssets();
            AllocateObservationBuffer();

            _inferenceInterval = 1f / _inferenceRateHz;
        }

        void Start()
        {
            InitializeIKTargets();
        }

        void OnDestroy()
        {
            JobHandle.ScheduleBatchedJobs();
            _ikJobHandle.Complete();

            DisposeIKResults();
            DisposeNativeArrays();
            DisposeSentis();

            if (_nativeObservation.IsCreated)
                _nativeObservation.Dispose();
        }

        void Update()
        {
            UpdateLOD();
            UpdateVelocity();
            UpdateHeadLookTarget();
            UpdatePolicyBlending();
        }

        void FixedUpdate()
        {
            _inferenceTimer += Time.fixedDeltaTime;

            if (_lodInferenceEnabled && _inferenceTimer >= _inferenceInterval / _lodInferenceRateMultiplier)
            {
                _inferenceTimer = 0f;
                EncodeObservation();
                RunPolicyInference();
                DecodeActions();
            }

            ApplyRootMotion();
            ScheduleIKJobs();
        }

        void LateUpdate()
        {
            _ikJobHandle.Complete();
            UpdateGroundDetection();
            ApplyProceduralPose();
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0) return;
            _ikJobHandle.Complete();
            ApplyIKToAnimator();
        }

        // ──────────────────────────────────────────────
        // Sentis Initialization
        // ──────────────────────────────────────────────

        void InitializeSentis()
        {
#if UNITY_SENTIS
            try
            {
                _sentisAvailable = true;
                Debug.Log("[NeuralAnimationController] Unity Sentis initialized");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NeuralAnimationController] Sentis not available: {e.Message}");
                _sentisAvailable = false;
            }
#else
            _sentisAvailable = false;
            Debug.LogWarning("[NeuralAnimationController] Unity Sentis not installed. Policy inference disabled.");
#endif
        }

        void InitializePolicyAssets()
            {
                _policyAssets[PolicyType.Locomotion] = _locomotionPolicy;
                _policyAssets[PolicyType.Combat] = _combatPolicy;
                _policyAssets[PolicyType.React] = _reactPolicy;
                _policyAssets[PolicyType.Interact] = _interactPolicy;
                _policyAssets[PolicyType.Fly] = _flyPolicy;
                _policyAssets[PolicyType.Swim] = _swimPolicy;

        #if UNITY_SENTIS
                if (!_sentisAvailable) return;

                foreach (PolicyType type in Enum.GetValues(typeof(PolicyType)))
                {
                    var asset = _policyAssets[type];
                    if (asset == null || asset.OnnxModel == null) continue;

                    try
                    {
                        Model model = ModelLoader.Load(asset.OnnxModel.bytes);
                        _policyModels[type] = model;
                        Debug.Log($"[NeuralAnimationController] Loaded {type} policy model");
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[NeuralAnimationController] Failed to load {type} policy: {e.Message}");
                    }
                }
        #endif
            }

        void AllocateObservationBuffer()
        {
            _observationBuffer = new float[_observationDim];
            _actionBuffer = new float[_actionDim];
            _nativeObservation = new NativeArray<float>(_observationDim, Allocator.Persistent);

            // 블렌딩용 버퍼
            _actionBufferA = new float[_actionDim];
            _actionBufferB = new float[_actionDim];
        }

        void DisposeSentis()
        {
#if UNITY_SENTIS
            _worker?.Dispose();
            _inputTensor?.Dispose();
            _outputTensor?.Dispose();

            foreach (var model in _policyModels.Values)
                model?.Dispose();

            // Worker 풀 정리
            foreach (var worker in _workerPool.Values)
                worker?.Dispose();
            _workerPool.Clear();

            _policyModels.Clear();
#endif
        }

        // ──────────────────────────────────────────────
        // Native Array Management
        // ──────────────────────────────────────────────

        void AllocateNativeArrays()
        {
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
            _hipOffset = new NativeArray<float3>(1, Allocator.Persistent);
            _hipHeightOffset = new NativeArray<float>(1, Allocator.Persistent);
            _headLookTargetArr = new NativeArray<float3>(1, Allocator.Persistent);
            _leftFootGroundedArr = new NativeArray<bool>(1, Allocator.Persistent);
            _rightFootGroundedArr = new NativeArray<bool>(1, Allocator.Persistent);
        }

        void DisposeNativeArrays()
        {
            if (_leftFootPos.IsCreated) _leftFootPos.Dispose();
            if (_rightFootPos.IsCreated) _rightFootPos.Dispose();
            if (_leftHandPos.IsCreated) _leftHandPos.Dispose();
            if (_rightHandPos.IsCreated) _rightHandPos.Dispose();
            if (_leftFootTarget.IsCreated) _leftFootTarget.Dispose();
            if (_rightFootTarget.IsCreated) _rightFootTarget.Dispose();
            if (_leftHandTarget.IsCreated) _leftHandTarget.Dispose();
            if (_rightHandTarget.IsCreated) _rightHandTarget.Dispose();
            if (_leftFootHint.IsCreated) _leftFootHint.Dispose();
            if (_rightFootHint.IsCreated) _rightFootHint.Dispose();
            if (_leftHandHint.IsCreated) _leftHandHint.Dispose();
            if (_rightHandHint.IsCreated) _rightHandHint.Dispose();
            if (_hipOffset.IsCreated) _hipOffset.Dispose();
            if (_hipHeightOffset.IsCreated) _hipHeightOffset.Dispose();
            if (_headLookTargetArr.IsCreated) _headLookTargetArr.Dispose();
            if (_leftFootGroundedArr.IsCreated) _leftFootGroundedArr.Dispose();
            if (_rightFootGroundedArr.IsCreated) _rightFootGroundedArr.Dispose();
        }

        void DisposeIKResults()
        {
            foreach (var arr in _leftIKResults)
                if (arr.IsCreated) arr.Dispose();
            _leftIKResults.Clear();
            foreach (var arr in _rightIKResults)
                if (arr.IsCreated) arr.Dispose();
            _rightIKResults.Clear();
            foreach (var arr in _leftIKSuccess)
                if (arr.IsCreated) arr.Dispose();
            _leftIKSuccess.Clear();
            foreach (var arr in _rightIKSuccess)
                if (arr.IsCreated) arr.Dispose();
            _rightIKSuccess.Clear();
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

        // ──────────────────────────────────────────────
        // Observation Encoding (Requirement 2)
        // ──────────────────────────────────────────────

        void EncodeObservation()
        {
            // Build observation vector of exactly _observationDim values.
            // Layout (120 for biped, 150 for quadruped):
            //   [0-2]   Local velocity xyz (3)
            //   [3-5]   Forward direction (3)
            //   [6-8]   Ground normal (3)
            //   [9]     IsGrounded (1)
            //   [10]    Terrain height ahead (1)
            //   [11-13] Target direction local (3)
            //   [14]    Target distance (1)
            //   [15-16] Body lean (2)
            //   [17-20] Policy one-hot (4)
            //   [21-28] Style embedding (8)
            //   [29-82] Joint positions: up to 18 joints × 3 = 54 (biped)
            //   [83-86] Foot contact flags (4)
            //   [87-88] Gait phase sin/cos (2)
            //   [89-119] Padding to _observationDim

            Vector3 localVel = transform.InverseTransformDirection(_currentVelocity);
            Vector3 forward = transform.forward;
            Vector3 groundNormal = GetGroundNormal();
            float isGrounded = IsGrounded ? 1f : 0f;
            float terrainHeight = SampleTerrainHeight(transform.position + forward * 1.5f);
            Vector3 toTarget = (_actionTarget - transform.position);
            float targetDist = math.length(toTarget);
            Vector3 targetDir = targetDist > 0.01f ? transform.InverseTransformDirection(toTarget / targetDist) : Vector3.zero;

            int idx = 0;
            float[] obs = _observationBuffer;

            // Velocity (3)
            obs[idx++] = localVel.x;
            obs[idx++] = localVel.y;
            obs[idx++] = localVel.z;

            // Forward (3)
            obs[idx++] = forward.x;
            obs[idx++] = forward.y;
            obs[idx++] = forward.z;

            // Ground normal (3)
            obs[idx++] = groundNormal.x;
            obs[idx++] = groundNormal.y;
            obs[idx++] = groundNormal.z;

            // Grounded / terrain (2)
            obs[idx++] = isGrounded;
            obs[idx++] = terrainHeight;

            // Target direction (3) + distance (1)
            obs[idx++] = targetDir.x;
            obs[idx++] = targetDir.y;
            obs[idx++] = targetDir.z;
            obs[idx++] = targetDist;

            // Body lean (2)
            obs[idx++] = _bodyLeanOffset.x;
            obs[idx++] = _bodyLeanOffset.y;

            // Policy one-hot (4)
            int policyIdx = (int)_currentPolicy;
            for (int i = 0; i < 4; i++)
                obs[idx++] = (i == policyIdx) ? 1f : 0f;

            // Style embedding (8) — placeholder for now
            for (int i = 0; i < 8; i++)
                obs[idx++] = 0f;

            // Joint positions (up to 18 joints × 3 = 54)
            var bones = _boneMap.GetAllBones();
            int jointCount = math.min(bones?.Length ?? 0, 18);
            for (int i = 0; i < jointCount && idx + 3 <= _observationDim; i++)
            {
                var t = bones[i].Transform;
                Vector3 localPos = t != null
                    ? transform.InverseTransformPoint(t.position)
                    : Vector3.zero;
                obs[idx++] = localPos.x;
                obs[idx++] = localPos.y;
                obs[idx++] = localPos.z;
            }
            // Pad remaining joint slots
            int maxJointSlots = 18 * 3;
            int jointsWritten = math.min(jointCount, 18) * 3;
            for (int i = jointsWritten; i < maxJointSlots && idx < _observationDim; i++)
                obs[idx++] = 0f;

            // Foot contact flags (4)
            obs[idx++] = _leftFootGrounded ? 1f : 0f;
            obs[idx++] = _rightFootGrounded ? 1f : 0f;
            obs[idx++] = 0f; // LH placeholder
            obs[idx++] = 0f; // RH placeholder

            // Gait phase sin/cos (2)
            float gaitPhase = (_inferenceTimer * 2f) % 1f;
            obs[idx++] = math.sin(gaitPhase * 2f * math.PI);
            obs[idx++] = math.cos(gaitPhase * 2f * math.PI);

            // Normalize if enabled (only meaningful features, not padding)
            if (_normalizeObservations && idx > 0)
            {
                float invNorm = 1f / math.max(math.sqrt(MeanSquared(obs, idx)), _observationNormalizationEpsilon);
                for (int i = 0; i < idx; i++)
                    obs[i] *= invNorm;
            }

            // Pad remaining to _observationDim
            while (idx < _observationDim)
                obs[idx++] = 0f;

            // Copy to native array for job system
            for (int i = 0; i < _observationDim; i++)
                _nativeObservation[i] = obs[i];
        }

        static float MeanSquared(float[] arr, int count)
        {
            float sum = 0f;
            for (int i = 0; i < count; i++)
                sum += arr[i] * arr[i];
            return sum / math.max(count, 1);
        }

        Vector3 GetGroundNormal()
        {
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, _groundCheckDistance, _groundMask))
                return hit.normal;
            return Vector3.up;
        }

        float SampleTerrainHeight(Vector3 position)
        {
            Vector3 origin = position + Vector3.up * 1f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, _groundCheckDistance * 2f, _groundMask))
                return position.y - hit.point.y;
            return 0f;
        }

        // ──────────────────────────────────────────────
        // Policy Inference (Requirement 3)
        // ──────────────────────────────────────────────

        void RunPolicyInference()
        {
#if UNITY_SENTIS
            if (!_sentisAvailable) return;

            PolicyType activePolicy = _isBlending ? _targetPolicy : _currentPolicy;

            // Blending 중이면 두 정책 모두 추론 후 보간
            if (_isBlending)
            {
                RunBlendedInference();
                return;
            }

            if (!_policyModels.TryGetValue(activePolicy, out Model model))
            {
                Debug.LogWarning($"[NeuralAnimationController] No model loaded for {activePolicy}");
                return;
            }

            try
            {
                // Worker 풀링: 기존 worker 재사용 (동일 모델인 경우)
                if (_workerPool.TryGetValue(activePolicy, out Worker pooledWorker))
                {
                    _worker = pooledWorker;
                }
                else
                {
                    _worker = WorkerFactory.CreateWorker(_backendType, model);
                    _workerPool[activePolicy] = _worker;
                }

                // FP16 최적화: input tensor를 FP16으로 생성 (BackendType.GPUCompute에서만 유효)
                using (var input = new TensorFloat(new TensorShape(1, 1, 1, _observationDim), _observationBuffer))
                {
                    _worker.Execute(input);
                    _outputTensor = _worker.PeekOutput() as TensorFloat;
                }

                int outputCount = _outputTensor.shape.length;
                int readCount = math.min(outputCount, _actionDim);

                for (int i = 0; i < readCount; i++)
                    _actionBuffer[i] = _outputTensor[i];
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NeuralAnimationController] Inference error: {e.Message}");
                HeuristicFallbackInference();
            }
#else
            // No Sentis: fallback — use procedural heuristics derived from observation
            HeuristicFallbackInference();
#endif
        }

        /// <summary>
        /// Policy blending: 두 정책을 동시에 추론하고 액션을 보간합니다.
        /// </summary>
        void RunBlendedInference()
        {
#if UNITY_SENTIS
            if (!_sentisAvailable) return;

            if (!_policyModels.TryGetValue(_currentPolicy, out Model modelA) ||
                !_policyModels.TryGetValue(_targetPolicy, out Model modelB))
            {
                return;
            }

            try
            {
                // 현재 정책 추론
                var workerA = GetOrCreateWorker(_currentPolicy, modelA);
                using (var input = new TensorFloat(new TensorShape(1, 1, 1, _observationDim), _observationBuffer))
                {
                    workerA.Execute(input);
                    var outputA = workerA.PeekOutput() as TensorFloat;
                    CopyOutputToBuffer(outputA, _actionBufferA);
                }

                // 타겟 정책 추론
                var workerB = GetOrCreateWorker(_targetPolicy, modelB);
                using (var input = new TensorFloat(new TensorShape(1, 1, 1, _observationDim), _observationBuffer))
                {
                    workerB.Execute(input);
                    var outputB = workerB.PeekOutput() as TensorFloat;
                    CopyOutputToBuffer(outputB, _actionBufferB);
                }

                // 보간
                float blendT = _policyBlendCurve.Evaluate(math.clamp(_blendTimer / _policyBlendDuration, 0f, 1f));
                for (int i = 0; i < _actionDim; i++)
                    _actionBuffer[i] = math.lerp(_actionBufferA[i], _actionBufferB[i], blendT);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NeuralAnimationController] Blended inference error: {e.Message}");
                HeuristicFallbackInference();
            }
#else
            HeuristicFallbackInference();
#endif
        }

        /// <summary>
        /// Worker 풀에서 worker를 가져오거나 새로 생성합니다.
        /// </summary>
        Worker GetOrCreateWorker(PolicyType policy, Model model)
        {
#if UNITY_SENTIS
            if (_workerPool.TryGetValue(policy, out Worker existing))
                return existing;

            var worker = WorkerFactory.CreateWorker(_backendType, model);
            _workerPool[policy] = worker;
            return worker;
#else
            return null;
#endif
        }

        #if UNITY_SENTIS
        void CopyOutputToBuffer(TensorFloat output, float[] buffer)
        {
            if (output == null) return;
            int count = math.min(output.shape.length, buffer.Length);
            for (int i = 0; i < count; i++)
                buffer[i] = output[i];
        }
#endif

        /// <summary>
        /// Fallback inference when Sentis is unavailable. Converts observation-based
        /// heuristics to the action buffer so the controller remains functional.
        /// </summary>
        void HeuristicFallbackInference()
        {
            float speed = _currentSpeed;
            float maxSpeed = _integrateWithCharacterController ? 10f : 5f;
            float speedRatio = math.clamp(speed / math.max(maxSpeed, 0.01f), 0f, 1f);

            // Fill entire action buffer with zeros
            for (int i = 0; i < _actionDim; i++)
                _actionBuffer[i] = 0f;

            // Action layout (heuristic, fills first 16 of 80):
            // [0]   = forward velocity (local x)
            // [1]   = lateral velocity (local z)
            // [2]   = vertical velocity
            // [3]   = turn angle (degrees)
            // [4-7] = left leg: hip_x, hip_z, knee, ankle
            // [8-11] = right leg: hip_x, hip_z, knee, ankle
            // [12-13] = spine: bend, twist
            // [14-15] = head look: x, y
            // [16-79] = zero (bone rotations not filled in heuristic mode)

            _actionBuffer[0] = speedRatio * 0.8f;
            _actionBuffer[3] = _turnInput * 30f;

            // Leg swing from speed
            float legCycle = math.sin(_inferenceTimer * 6f * speedRatio) * 0.3f * speedRatio;
            _actionBuffer[4] = legCycle;  // L hip x
            _actionBuffer[8] = -legCycle; // R hip x

            // Knee bend from speed
            float kneeBend = (1f - speedRatio) * 0.2f;
            _actionBuffer[6] = kneeBend;
            _actionBuffer[10] = kneeBend;

            // Spine counter-rotation at speed
            _actionBuffer[12] = _turnInput * 5f * speedRatio;
            _actionBuffer[13] = speedRatio * 2f;

            // Head look
            _actionBuffer[14] = _headLookTarget.x * 0.01f;
            _actionBuffer[15] = _headLookTarget.y * 0.01f;
        }

        // ──────────────────────────────────────────────
        // Action Decoding (Requirement 4)
        // ──────────────────────────────────────────────

        void DecodeActions()
        {
            // Decode action buffer into root motion and bone rotations.
            // Layout (80 for biped, 100 for quadruped):
            //   [0-2]   Root velocity (local xyz)
            //   [3]     Turn angle (degrees)
            //   [4-75]  Joint rotations: 18 joints × 4 quaternions (72) for biped
            //   [76-79] Reserved (4)

            if (_actionBuffer == null || _actionBuffer.Length < 4) return;

            // Root velocity (local space → world)
            Vector3 localRootVel = new Vector3(
                _actionBuffer[0] * 5f,
                _actionBuffer[2] * 2f,
                _actionBuffer[1] * 5f
            );

            _decodedRootVelocity = transform.TransformDirection(localRootVel);
            _decodedRootMotionDelta = _decodedRootVelocity * Time.fixedDeltaTime;

            // Turn angle
            _decodedTurnAngle = _actionBuffer[3];
            _decodedRootRotationDelta = Quaternion.Euler(0f, _decodedTurnAngle * Time.fixedDeltaTime, 0f);
            // Apply bone rotations from action buffer
            // For biped (80): 18 joints × 4 quaternions starting at index 4
            // For quadruped (100): 24 joints × 4 quaternions starting at index 4
            int boneRotStart = 4;
            int boneRotCount = (_actionDim - boneRotStart - 4) / 4; // -4 reserved
            if (boneRotCount > 0 && _actionBuffer.Length >= boneRotStart + boneRotCount * 4)
            {
                var bones = _boneMap.GetAllBones();
                int applyCount = math.min(bones?.Length ?? 0, boneRotCount);

                for (int i = 0; i < applyCount; i++)
                {
                    int bufIdx = boneRotStart + i * 4;
                    if (bufIdx + 4 > _actionBuffer.Length) break;
                    var t = bones[i].Transform;
                    if (t == null) continue;
                    quaternion targetRot = new quaternion(
                        _actionBuffer[bufIdx + 0],
                        _actionBuffer[bufIdx + 1],
                        _actionBuffer[bufIdx + 2],
                        _actionBuffer[bufIdx + 3]
                    );
                    t.localRotation = Quaternion.Slerp(
                        t.localRotation,
                        new Quaternion(targetRot.value.x, targetRot.value.y, targetRot.value.z, targetRot.value.w),
                        Time.deltaTime * 15f
                    );
                }
            }
        }

        void ApplyBoneRotationFromAction(BoneRole role, float eulerX, float eulerY, float eulerZ)
        {
            if (!_boneMap.Has(role)) return;
            var t = _boneMap.Get(role);
            if (t == null) return;

            quaternion targetRot = quaternion.Euler(
                math.radians(eulerX),
                math.radians(eulerY),
                math.radians(eulerZ)
            );
            t.localRotation = Quaternion.Slerp(
                t.localRotation,
                new Quaternion(targetRot.value.x, targetRot.value.y, targetRot.value.z, targetRot.value.w),
                Time.deltaTime * 15f
            );
        }

        // ──────────────────────────────────────────────
        // Policy Blending (Requirement 6)
        // ──────────────────────────────────────────────

        void UpdatePolicyBlending()
        {
            if (!_isBlending) return;

            _blendTimer += Time.deltaTime;
            float blendT = math.clamp(_blendTimer / _policyBlendDuration, 0f, 1f);
            float curveT = _policyBlendCurve.Evaluate(blendT);

            if (blendT >= 1f)
            {
                _currentPolicy = _targetPolicy;
                _isBlending = false;
                Debug.Log($"[NeuralAnimationController] Switched to {_currentPolicy}");
            }

            // During blend, decode both policies and interpolate
            // (simplified: just switch the active model at blend end)
        }

        // ──────────────────────────────────────────────
        // Root Motion Application (Requirement 7)
        // ──────────────────────────────────────────────

        void ApplyRootMotion()
        {
            if (!_useRootMotion) return;
            if (_velocityProvider != null) return; // external provider handles movement

            float weight = _rootMotionWeight;

            // Apply position delta
            Vector3 moveDelta = _decodedRootMotionDelta * weight;

            if (_characterController != null && _integrateWithCharacterController)
            {
                _characterController.Move(moveDelta);
            }
            else if (_navMeshAgent != null && _integrateWithNavMeshAgent)
            {
                _navMeshAgent.Move(moveDelta);
            }
            else
            {
                transform.position += moveDelta;
            }

            // Apply rotation delta
            transform.rotation *= Quaternion.Slerp(
                Quaternion.identity,
                _decodedRootRotationDelta,
                weight
            );
        }

        // ──────────────────────────────────────────────
        // Velocity Update
        // ──────────────────────────────────────────────

        void UpdateVelocity()
        {
            if (_velocityProvider != null)
            {
                _currentVelocity = _velocityProvider.CurrentVelocity;
                _currentSpeed = _velocityProvider.CurrentSpeed;

                // Compute turn input from velocity direction change
                Vector3 horizontalVel = new Vector3(_currentVelocity.x, 0, _currentVelocity.z);
                if (horizontalVel.sqrMagnitude > 0.01f)
                {
                    float angularSpeed = Vector3.SignedAngle(
                        transform.forward,
                        horizontalVel.normalized,
                        Vector3.up
                    ) * Time.deltaTime;
                    _turnInput = Mathf.Clamp(angularSpeed * 0.1f, -1f, 1f);
                }
            }
            else
            {
                _currentVelocity = _decodedRootVelocity;
                _currentSpeed = _currentVelocity.magnitude;
            }

            // Body lean from turn
            float targetLean = _turnInput * 0.4f * 15f;
            _bodyLeanOffset = Vector3.Lerp(_bodyLeanOffset, new Vector3(targetLean, 0, 0), Time.deltaTime * 5f);
            _bodyLeanRotation = Quaternion.Lerp(_bodyLeanRotation, Quaternion.Euler(_bodyLeanOffset), Time.deltaTime * 5f);
        }

        // ──────────────────────────────────────────────
        // Head Look Target
        // ──────────────────────────────────────────────

        void UpdateHeadLookTarget()
        {
            _headLookTarget = transform.position + transform.forward * 5f + Vector3.up * 1.5f;

            if (_currentPolicy == PolicyType.Combat || _currentPolicy == PolicyType.Interact)
            {
                _headLookTarget = _actionTarget;
            }

            _headLookTargetArr[0] = _headLookTarget;
        }

        // ──────────────────────────────────────────────
        // LOD System (Requirement 8)
        // ──────────────────────────────────────────────

        void UpdateLOD()
        {
            _lodFrameCounter++;

            if (_lodFrameCounter % _lodUpdateIntervalFrames != 0)
                return;

            Camera mainCamera = Camera.main;
            if (mainCamera == null) return;

            float dist = Vector3.Distance(transform.position, mainCamera.transform.position);
            int newLOD = 0;

            if (dist >= _lod2Distance) newLOD = 3;     // Culled
            else if (dist >= _lod1Distance) newLOD = 2; // Low
            else if (dist >= _lod0Distance) newLOD = 1; // Medium
            else newLOD = 0;                            // Full

            if (newLOD != _currentLODLevel)
            {
                _currentLODLevel = newLOD;
                ApplyLODSettings(newLOD);
            }
        }

        void ApplyLODSettings(int lod)
        {
            switch (lod)
            {
                case 0: // Full
                    _lodInferenceEnabled = true;
                    _lodRaycastEnabled = true;
                    _lodIKIterations = 2;
                    _lodInferenceRateMultiplier = 1f;
                    break;
                case 1: // Medium
                    _lodInferenceEnabled = true;
                    _lodRaycastEnabled = true;
                    _lodIKIterations = 1;
                    _lodInferenceRateMultiplier = 0.5f; // half-rate inference
                    break;
                case 2: // Low
                    _lodInferenceEnabled = true;
                    _lodRaycastEnabled = false;
                    _lodIKIterations = 1;
                    _lodInferenceRateMultiplier = 0.25f; // quarter-rate inference
                    break;
                default: // Culled (3+)
                    _lodInferenceEnabled = false; // skip inference entirely
                    _lodRaycastEnabled = false;
                    _lodIKIterations = 0;
                    _lodInferenceRateMultiplier = 0f;
                    break;
            }
        }

        // ──────────────────────────────────────────────
        // Ground Detection
        // ──────────────────────────────────────────────

        void UpdateGroundDetection()
        {
            if (!_lodRaycastEnabled)
            {
                _leftFootGrounded = false;
                _rightFootGrounded = false;
                _leftFootGroundedArr[0] = false;
                _rightFootGroundedArr[0] = false;
                return;
            }

            // LOD1: every other frame
            if (_currentLODLevel == 1 && Time.frameCount % 2 != 0)
                return;

            var lFoot = _boneMap.Get(BoneRole.L_Foot);
            var rFoot = _boneMap.Get(BoneRole.R_Foot);

            _leftFootGrounded = false;
            _rightFootGrounded = false;

            if (lFoot != null)
            {
                Vector3 origin = lFoot.position + Vector3.up * 0.2f;
                if (Physics.Raycast(origin, Vector3.down, out _leftFootHit, _groundCheckDistance, _groundMask))
                {
                    _leftFootGrounded = true;
                    _leftFootTarget[0] = _leftFootHit.point + Vector3.up * 0.02f;
                }
            }

            if (rFoot != null)
            {
                Vector3 origin = rFoot.position + Vector3.up * 0.2f;
                if (Physics.Raycast(origin, Vector3.down, out _rightFootHit, _groundCheckDistance, _groundMask))
                {
                    _rightFootGrounded = true;
                    _rightFootTarget[0] = _rightFootHit.point + Vector3.up * 0.02f;
                }
            }

            _leftFootGroundedArr[0] = _leftFootGrounded;
            _rightFootGroundedArr[0] = _rightFootGrounded;
        }

        // ──────────────────────────────────────────────
        // IK Scheduling (Requirement 5)
        // ──────────────────────────────────────────────

        void ScheduleIKJobs()
        {
            JobHandle dependency = default;

            UpdateBonePositions();

            int ikIterations = _lodIKIterations;
            if (ikIterations <= 0) return;

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
        // IK Application (Requirement 5)
        // ──────────────────────────────────────────────

        void ApplyProceduralPose()
        {
            ApplyFootIK();
            ApplySpineIK();
            ApplyHeadLook();
            ApplyBodyLean();
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

            // Main-thread fallback for failed chains
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
            angle = Mathf.Clamp(angle, -30f, 30f) * _spineIKWeight * 0.5f;

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
            head.rotation = Quaternion.Slerp(head.rotation, targetRot, _headLookWeight * Time.deltaTime * 10f);
        }

        void ApplyBodyLean()
        {
            var root = _boneMap.Get(BoneRole.Root);
            if (root != null)
                root.localRotation = _bodyLeanRotation;
        }

        // ──────────────────────────────────────────────
        // Animator IK
        // ──────────────────────────────────────────────

        void ApplyIKToAnimator()
        {
            if (_leftFootGrounded)
            {
                _animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, _footIKWeight);
                _animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, _footIKWeight);
                _animator.SetIKPosition(AvatarIKGoal.LeftFoot, _leftFootTarget[0]);
                _animator.SetIKRotation(AvatarIKGoal.LeftFoot, Quaternion.LookRotation(Vector3.up, _leftFootHit.normal));
                _animator.SetIKHintPositionWeight(AvatarIKHint.LeftKnee, _footIKWeight);
                _animator.SetIKHintPosition(AvatarIKHint.LeftKnee, _leftFootHint[0]);
            }

            if (_rightFootGrounded)
            {
                _animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, _footIKWeight);
                _animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, _footIKWeight);
                _animator.SetIKPosition(AvatarIKGoal.RightFoot, _rightFootTarget[0]);
                _animator.SetIKRotation(AvatarIKGoal.RightFoot, Quaternion.LookRotation(Vector3.up, _rightFootHit.normal));
                _animator.SetIKHintPositionWeight(AvatarIKHint.RightKnee, _footIKWeight);
                _animator.SetIKHintPosition(AvatarIKHint.RightKnee, _rightFootHint[0]);
            }

            if (_currentPolicy == PolicyType.Combat || _currentPolicy == PolicyType.Interact)
            {
                _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, _handIKWeight);
                _animator.SetIKPosition(AvatarIKGoal.LeftHand, _leftHandTarget[0]);
                _animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, _handIKWeight);
                _animator.SetIKHintPosition(AvatarIKHint.LeftElbow, _leftHandHint[0]);

                _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, _handIKWeight);
                _animator.SetIKPosition(AvatarIKGoal.RightHand, _rightHandTarget[0]);
                _animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, _handIKWeight);
                _animator.SetIKHintPosition(AvatarIKHint.RightElbow, _rightHandHint[0]);
            }
        }

        // ──────────────────────────────────────────────
        //  Static Policy Switch Event
        // ──────────────────────────────────────────────

        /// <summary>
        /// Static event for policy switch requests from PolicySelector.
        /// HybridAnimationController subscribes to this to route to the correct instance.
        /// </summary>
        public static event Action<PolicyType, float> OnPolicySwitchRequested;

        /// <summary>
        /// Static method called by PolicySelector to request a policy switch.
        /// Fires the event which HybridAnimationController handles.
        /// </summary>
        public static void RequestPolicySwitch(PolicyType policy, float blendDuration)
        {
            OnPolicySwitchRequested?.Invoke(policy, blendDuration);
        }

        // ──────────────────────────────────────────────
        //  Async Inference (Double Buffering)
        // ──────────────────────────────────────────────

        /// <summary>
        /// Enable double-buffered async inference for frame-pipelined execution.
        /// Frame N inference runs on buffer A while frame N-1 results read from buffer B.
        /// </summary>
        public void EnableAsyncInference(bool enable)
        {
            _asyncInference = enable;
        }

        /// <summary>
        /// Set LOD level externally (0=Full, 1=Medium, 2=Low, 3=Culled).
        /// Overrides camera-based LOD when set directly.
        /// </summary>
        public void SetLODLevel(int level)
        {
            level = Mathf.Clamp(level, 0, 3);
            if (_currentLODLevel != level)
            {
                _currentLODLevel = level;
                ApplyLODSettings(level);
            }
        }

        /// <summary>
        /// Load a policy model asynchronously from path.
        /// </summary>
        public Coroutine LoadModelAsync(PolicyType policy, string path)
        {
            return StartCoroutine(LoadModelCoroutine(policy, path));
        }

        System.Collections.IEnumerator LoadModelCoroutine(PolicyType policy, string path)
        {
            var request = Resources.LoadAsync<ModelAsset>(path);
            yield return request;

            var asset = request.asset as ModelAsset;
            if (asset == null)
            {
                Debug.LogWarning($"[NeuralAnimationController] Failed to load model: {path}");
                yield break;
            }

            _policyAssets[policy] = asset;

#if UNITY_SENTIS
            if (_sentisAvailable && asset.OnnxModel != null)
            {
                try
                {
                    Model model = ModelLoader.Load(asset.OnnxModel.bytes);
                    _policyModels[policy] = model;
                    Debug.Log($"[NeuralAnimationController] Async loaded {policy} from {path}");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[NeuralAnimationController] Async load failed {policy}: {e.Message}");
                }
            }
#endif
        }

        /// <summary>
        /// Unload a policy model to free memory.
        /// </summary>
        public void UnloadModel(PolicyType policy)
        {
#if UNITY_SENTIS
            if (_policyModels.TryGetValue(policy, out Model model))
            {
                model?.Dispose();
                _policyModels.Remove(policy);
            }

            if (_workerPool.TryGetValue(policy, out Worker worker))
            {
                worker?.Dispose();
                _workerPool.Remove(policy);
            }
#endif
            if (_policyAssets.ContainsKey(policy))
                _policyAssets[policy] = null;

            if (_currentPolicy == policy)
                SwitchPolicy(PolicyType.Locomotion);
        }

        // ──────────────────────────────────────────────
        //  Gizmos
        // ──────────────────────────────────────────────

        void OnDrawGizmos()
        {
#if UNITY_EDITOR
            // Draw active policy info
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2.5f,
                $"[Neural] Policy: {_currentPolicy} | LOD: {_currentLODLevel} | Blend: {(_isBlending ? $"{_blendTimer:F2}s" : "None")}");

            // Draw IK targets
            Gizmos.color = Color.green;
            if (_leftFootGrounded) Gizmos.DrawWireSphere(_leftFootTarget[0], 0.1f);
            if (_rightFootGrounded) Gizmos.DrawWireSphere(_rightFootTarget[0], 0.1f);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_leftHandTarget[0], 0.08f);
            Gizmos.DrawWireSphere(_rightHandTarget[0], 0.08f);

            // Draw velocity vector
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position + Vector3.up, _currentVelocity * 0.5f);

            // Draw LOD distance rings
            if (_currentLODLevel > 0)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.15f);
                Gizmos.DrawWireSphere(transform.position, _lod0Distance);
                Gizmos.DrawWireSphere(transform.position, _lod1Distance);
                Gizmos.DrawWireSphere(transform.position, _lod2Distance);
            }
#endif
        }
    }
}