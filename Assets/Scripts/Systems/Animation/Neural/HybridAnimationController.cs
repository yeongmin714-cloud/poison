using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using ProjectName.Systems.Animation.Procedural;
using ProjectName.Systems.Animation.Procedural.Bones;
using ProjectName.Systems.Animation.Procedural.LOD;

namespace ProjectName.Systems.Animation.Neural
{
    /// <summary>
    /// Hybrid Animation Controller — bridges Procedural (Phase 3.9) and Neural (Phase 4) animation systems.
    /// Blends procedural and neural outputs based on policy overrides and LOD distance.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(ProceduralBoneMap))]
    public class HybridAnimationController : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        // Inspector: Controller References
        // ──────────────────────────────────────────────

        [Header("Controller References")]
        [SerializeField] ProceduralAnimationController _proceduralController;
        [SerializeField] NeuralAnimationController _neuralController;

        [Header("Blending Weights")]
        [SerializeField, Range(0f, 1f)] float _baseProceduralWeight = 0.5f;
        [SerializeField, Range(0f, 1f)] float _baseNeuralWeight = 0.5f;

        [Header("Policy Override System")]
        [SerializeField] bool _enablePolicyOverrides = true;
        [Tooltip("Policies that force Neural-only (weight = 1.0 for Neural, 0.0 for Procedural)")]
        [SerializeField] NeuralAnimationController.PolicyType[] _neuralOnlyPolicies = new NeuralAnimationController.PolicyType[]
        {
            NeuralAnimationController.PolicyType.Combat,
            NeuralAnimationController.PolicyType.React,
            NeuralAnimationController.PolicyType.Fly,
            NeuralAnimationController.PolicyType.Swim,
            NeuralAnimationController.PolicyType.Mount,
            NeuralAnimationController.PolicyType.Climb,
            NeuralAnimationController.PolicyType.LargeMonster
        };

        [Header("Policy Blending")]
        [SerializeField, Range(0.05f, 1f)] float _policySwitchBlendDuration = 0.2f;
        [SerializeField] AnimationCurve _policySwitchBlendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("LOD Integration")]
        [SerializeField] float _lodNeuralWeightThreshold = 30f; // Distance where neural weight starts reducing
        [SerializeField] float _lodProceduralOnlyDistance = 60f; // Distance where neural weight = 0
        [SerializeField] bool _useLODManager = true;
        [SerializeField] Camera _lodCamera;

        [Header("Root Motion Blending")]
        [SerializeField] bool _blendRootMotion = true;
        [SerializeField, Range(0f, 1f)] float _rootMotionProceduralWeight = 0.5f;

        // ──────────────────────────────────────────────
        // Runtime State
        // ──────────────────────────────────────────────

        // Policy override table: policy → useNeural (true = neural only, false = procedural only, null = auto blend)
        Dictionary<NeuralAnimationController.PolicyType, bool?> _policyOverrides = new Dictionary<NeuralAnimationController.PolicyType, bool?>();

        // Current blended weights (sum = 1.0)
        float _currentProceduralWeight;
        float _currentNeuralWeight;

        // Policy switching state
        NeuralAnimationController.PolicyType _lastKnownPolicy;
        float _policySwitchTimer;
        bool _isPolicySwitching;

        // LOD state
        int _currentLODLevel = 0;
        float _distanceToCamera;
        bool _lodNeuralActive = true;

        // Component references
        Animator _animator;
        ProceduralBoneMap _boneMap;

        // Blended outputs
        Vector3 _blendedRootMotionDelta;
        Quaternion _blendedRootRotationDelta = Quaternion.identity;
        Vector3 _blendedRootVelocity;

        // Bone rotation blending buffers
        NativeArray<quaternion> _proceduralBoneRotations;
        NativeArray<quaternion> _neuralBoneRotations;
        NativeArray<quaternion> _blendedBoneRotations;
        int _boneCount;

        JobHandle _blendJobHandle;

        // ──────────────────────────────────────────────
        // Public API
        // ──────────────────────────────────────────────

        /// <summary>
        /// Current blended procedural weight (0-1).
        /// </summary>
        public float ProceduralWeight => _currentProceduralWeight;

        /// <summary>
        /// Current blended neural weight (0-1).
        /// </summary>
        public float NeuralWeight => _currentNeuralWeight;

        /// <summary>
        /// Current LOD level (0=full, 1=medium, 2=low, 3=culled).
        /// </summary>
        public int CurrentLODLevel => _currentLODLevel;

        /// <summary>
        /// Distance to LOD camera.
        /// </summary>
        public float DistanceToCamera => _distanceToCamera;

        /// <summary>
        /// Whether neural inference is currently active (not culled by LOD).
        /// </summary>
        public bool IsNeuralActive => _lodNeuralActive && _neuralController != null && _neuralController.enabled;

        /// <summary>
        /// Set a policy override at runtime.
        /// </summary>
        /// <param name="policy">The policy type to override.</param>
        /// <param name="useNeural">True = force neural only, false = procedural only.</param>
        public void SetPolicyOverride(NeuralAnimationController.PolicyType policy, bool useNeural)
        {
            _policyOverrides[policy] = useNeural;
            Debug.Log($"[HybridAnimationController] Policy override set: {policy} -> {(useNeural ? "Neural" : "Procedural")}");
        }

        /// <summary>
        /// Clear a policy override, returning to automatic blending.
        /// </summary>
        public void ClearPolicyOverride(NeuralAnimationController.PolicyType policy)
        {
            if (_policyOverrides.ContainsKey(policy))
            {
                _policyOverrides.Remove(policy);
                Debug.Log($"[HybridAnimationController] Policy override cleared: {policy}");
            }
        }

        /// <summary>
        /// Clear all policy overrides.
        /// </summary>
        public void ClearAllPolicyOverrides()
        {
            _policyOverrides.Clear();
            Debug.Log("[HybridAnimationController] All policy overrides cleared");
        }

        /// <summary>
        /// Set the base blend weights for procedural vs neural.
        /// proceduralWeight + neuralWeight will be normalized to 1.0.
        /// </summary>
        public void SetBaseWeights(float proceduralWeight, float neuralWeight)
        {
            float sum = proceduralWeight + neuralWeight;
            if (sum > 0f)
            {
                _baseProceduralWeight = proceduralWeight / sum;
                _baseNeuralWeight = neuralWeight / sum;
            }
            else
            {
                _baseProceduralWeight = 0.5f;
                _baseNeuralWeight = 0.5f;
            }
        }

        /// <summary>
        /// Set the LOD distance threshold where neural weight starts reducing.
        /// </summary>
        public void SetLODThreshold(float threshold)
        {
            _lodNeuralWeightThreshold = Mathf.Max(0f, threshold);
            _lodProceduralOnlyDistance = Mathf.Max(_lodNeuralWeightThreshold, _lodProceduralOnlyDistance);
        }

        /// <summary>
        /// Get the effective control mode for a policy (Neural, Procedural, or Blended).
        /// </summary>
        public ControlMode GetEffectiveControlMode(NeuralAnimationController.PolicyType policy)
        {
            if (_policyOverrides.TryGetValue(policy, out bool? overrideValue) && overrideValue.HasValue)
            {
                return overrideValue.Value ? ControlMode.Neural : ControlMode.Procedural;
            }

            if (_enablePolicyOverrides)
            {
                foreach (var neuralOnlyPolicy in _neuralOnlyPolicies)
                {
                    if (policy == neuralOnlyPolicy)
                        return ControlMode.Neural;
                }
            }

            return ControlMode.Blended;
        }

        /// <summary>
        /// Control mode for a specific policy.
        /// </summary>
        public enum ControlMode
        {
            Procedural,
            Neural,
            Blended
        }

        // ──────────────────────────────────────────────
        // Unity Lifecycle
        // ──────────────────────────────────────────────

        void Awake()
        {
            _animator = GetComponent<Animator>();
            _boneMap = GetComponent<ProceduralBoneMap>();

            // Auto-find controllers if not assigned
            if (_proceduralController == null)
                _proceduralController = GetComponent<ProceduralAnimationController>();
            if (_neuralController == null)
                _neuralController = GetComponent<NeuralAnimationController>();

            // Validate
            if (_proceduralController == null)
                Debug.LogError("[HybridAnimationController] ProceduralAnimationController not found!");
            if (_neuralController == null)
                Debug.LogError("[HybridAnimationController] NeuralAnimationController not found!");

            // Setup animator
            _animator.applyRootMotion = false;
            _animator.updateMode = AnimatorUpdateMode.Fixed;
            _animator.animatePhysics = true;

            // Initialize bone map
            _boneMap.Initialize(_animator);

            // Initialize LOD camera
            if (_lodCamera == null)
                _lodCamera = Camera.main;

            // Allocate bone rotation buffers
            InitializeBoneBuffers();

            // Initialize policy overrides from inspector defaults
            InitializePolicyOverrides();

            // Initialize weights
            NormalizeBaseWeights();
            _currentProceduralWeight = _baseProceduralWeight;
            _currentNeuralWeight = _baseNeuralWeight;
        }

        void Start()
        {
            _lastKnownPolicy = _neuralController?.ActivePolicy ?? NeuralAnimationController.PolicyType.Locomotion;
        }

        void OnDestroy()
        {
            _blendJobHandle.Complete();
            DisposeBoneBuffers();
        }

        void Update()
        {
            UpdateLOD();
            UpdatePolicySwitching();
            UpdateBlendedWeights();
        }

        void FixedUpdate()
        {
            // Run both controllers
            if (_proceduralController != null && _proceduralController.enabled && _currentProceduralWeight > 0.001f)
            {
                // Procedural controller runs in its own FixedUpdate
            }

            if (_neuralController != null && _neuralController.enabled && _currentNeuralWeight > 0.001f && _lodNeuralActive)
            {
                // Neural controller runs in its own FixedUpdate
            }

            // Blend outputs after both have run
            BlendOutputs();
        }

        void LateUpdate()
        {
            _blendJobHandle.Complete();
            ApplyBlendedPose();
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0) return;
            _blendJobHandle.Complete();
            ApplyBlendedIK();
        }

        // ──────────────────────────────────────────────
        // Initialization
        // ──────────────────────────────────────────────

        void NormalizeBaseWeights()
        {
            float sum = _baseProceduralWeight + _baseNeuralWeight;
            if (sum > 0f)
            {
                _baseProceduralWeight /= sum;
                _baseNeuralWeight /= sum;
            }
            else
            {
                _baseProceduralWeight = 0.5f;
                _baseNeuralWeight = 0.5f;
            }
        }

        void InitializePolicyOverrides()
        {
            _policyOverrides.Clear();
            if (_enablePolicyOverrides)
            {
                foreach (var policy in _neuralOnlyPolicies)
                {
                    _policyOverrides[policy] = true; // Force neural
                }
            }
        }

        void InitializeBoneBuffers()
        {
            var bones = _boneMap.GetAllBones();
            _boneCount = bones?.Length ?? 0;

            if (_boneCount > 0)
            {
                _proceduralBoneRotations = new NativeArray<quaternion>(_boneCount, Allocator.Persistent);
                _neuralBoneRotations = new NativeArray<quaternion>(_boneCount, Allocator.Persistent);
                _blendedBoneRotations = new NativeArray<quaternion>(_boneCount, Allocator.Persistent);
            }
        }

        void DisposeBoneBuffers()
        {
            if (_proceduralBoneRotations.IsCreated) _proceduralBoneRotations.Dispose();
            if (_neuralBoneRotations.IsCreated) _neuralBoneRotations.Dispose();
            if (_blendedBoneRotations.IsCreated) _blendedBoneRotations.Dispose();
        }

        // ──────────────────────────────────────────────
        // LOD Integration
        // ──────────────────────────────────────────────

        void UpdateLOD()
        {
            if (_lodCamera == null)
            {
                _lodCamera = Camera.main;
                if (_lodCamera == null) return;
            }

            // Calculate distance to camera
            _distanceToCamera = Vector3.Distance(transform.position, _lodCamera.transform.position);

            // Determine LOD level based on distance
            if (_distanceToCamera < 15f) _currentLODLevel = 0;
            else if (_distanceToCamera < 30f) _currentLODLevel = 1;
            else if (_distanceToCamera < 50f) _currentLODLevel = 2;
            else _currentLODLevel = 3;

            // Determine neural activation based on distance
            if (_distanceToCamera >= _lodProceduralOnlyDistance)
            {
                _lodNeuralActive = false;
            }
            else if (_distanceToCamera >= _lodNeuralWeightThreshold)
            {
                _lodNeuralActive = true;
                // Reduce neural weight based on distance
                float t = math.clamp((_distanceToCamera - _lodNeuralWeightThreshold) /
                                    (_lodProceduralOnlyDistance - _lodNeuralWeightThreshold), 0f, 1f);
                float neuralWeightMultiplier = 1f - t;
                _currentNeuralWeight = _baseNeuralWeight * neuralWeightMultiplier;
                _currentProceduralWeight = 1f - _currentNeuralWeight;
            }
            else
            {
                _lodNeuralActive = true;
                // At close distance, use base weights (will be modified by policy overrides)
            }

            // Sync LOD to procedural controller
            if (_proceduralController != null)
            {
                _proceduralController.CurrentLODLevel = _currentLODLevel;
            }
        }

        // ──────────────────────────────────────────────
        // Policy Switching & Weight Blending
        // ──────────────────────────────────────────────

        void UpdatePolicySwitching()
        {
            if (_neuralController == null) return;

            NeuralAnimationController.PolicyType currentPolicy = _neuralController.ActivePolicy;

            // Detect policy change
            if (currentPolicy != _lastKnownPolicy && !_isPolicySwitching)
            {
                _isPolicySwitching = true;
                _policySwitchTimer = 0f;
                _lastKnownPolicy = currentPolicy;
            }

            if (_isPolicySwitching)
            {
                _policySwitchTimer += Time.deltaTime;
                float t = math.clamp(_policySwitchTimer / _policySwitchBlendDuration, 0f, 1f);
                float curveT = _policySwitchBlendCurve.Evaluate(t);

                if (t >= 1f)
                {
                    _isPolicySwitching = false;
                }
            }
        }

        void UpdateBlendedWeights()
        {
            if (_neuralController == null) return;

            NeuralAnimationController.PolicyType currentPolicy = _neuralController.ActivePolicy;
            ControlMode mode = GetEffectiveControlMode(currentPolicy);

            float targetProceduralWeight, targetNeuralWeight;

            switch (mode)
            {
                case ControlMode.Procedural:
                    targetProceduralWeight = 1f;
                    targetNeuralWeight = 0f;
                    break;
                case ControlMode.Neural:
                    targetProceduralWeight = 0f;
                    targetNeuralWeight = 1f;
                    break;
                case ControlMode.Blended:
                default:
                    // Use LOD-adjusted base weights
                    targetProceduralWeight = _currentProceduralWeight;
                    targetNeuralWeight = _currentNeuralWeight;
                    break;
            }

            // Smooth blend towards target weights
            float blendSpeed = _isPolicySwitching ? (1f / _policySwitchBlendDuration) : 5f;
            _currentProceduralWeight = Mathf.Lerp(_currentProceduralWeight, targetProceduralWeight, blendSpeed * Time.deltaTime);
            _currentNeuralWeight = Mathf.Lerp(_currentNeuralWeight, targetNeuralWeight, blendSpeed * Time.deltaTime);

            // Renormalize to ensure sum = 1.0
            float sum = _currentProceduralWeight + _currentNeuralWeight;
            if (sum > 0.001f)
            {
                _currentProceduralWeight /= sum;
                _currentNeuralWeight /= sum;
            }
        }

        // ──────────────────────────────────────────────
        // Output Blending
        // ──────────────────────────────────────────────

        void BlendOutputs()
        {
            // Capture procedural bone rotations
            CaptureProceduralBoneRotations();

            // Capture neural bone rotations (from neural controller's decoded actions)
            CaptureNeuralBoneRotations();

            // Blend root motion
            BlendRootMotion();

            // Schedule bone rotation blending job
            ScheduleBoneBlendJob();
        }

        void CaptureProceduralBoneRotations()
        {
            if (_proceduralController == null || _boneCount == 0) return;

            var bones = _boneMap.GetAllBones();
            if (bones == null) return;

            for (int i = 0; i < _boneCount && i < bones.Length; i++)
            {
                var t = bones[i].Transform;
                if (t != null)
                {
                    _proceduralBoneRotations[i] = t.localRotation;
                }
            }
        }

        void CaptureNeuralBoneRotations()
        {
            if (_neuralController == null || _boneCount == 0) return;

            var bones = _boneMap.GetAllBones();
            if (bones == null) return;

            // Neural controller applies bone rotations directly to transforms in its LateUpdate/OnAnimatorIK
            // We need to capture them before they get blended
            // Since neural controller applies to the same transforms, we capture current state
            // which already has neural rotations applied if neural weight > 0

            // Alternative: read from neural controller's action buffer if accessible
            // For now, capture from transforms (they hold the neural result after neural FixedUpdate)
            for (int i = 0; i < _boneCount && i < bones.Length; i++)
            {
                var t = bones[i].Transform;
                if (t != null)
                {
                    _neuralBoneRotations[i] = t.localRotation;
                }
            }
        }

        void BlendRootMotion()
        {
            Vector3 procRootVel = Vector3.zero;
            Quaternion procRootRot = Quaternion.identity;
            Vector3 neuralRootVel = Vector3.zero;
            Quaternion neuralRootRot = Quaternion.identity;

            // Get procedural root motion
            if (_proceduralController != null)
            {
                procRootVel = _proceduralController.CurrentVelocity;
                // Procedural controller doesn't expose root rotation delta directly
                // Use turn input as approximation
                float turnAngle = 0f; // Would need access to internal state
                procRootRot = Quaternion.Euler(0f, turnAngle * Time.fixedDeltaTime, 0f);
            }

            // Get neural root motion
            if (_neuralController != null && _lodNeuralActive)
            {
                // Access neural controller's decoded root motion via reflection or public properties
                // For now, use the public CurrentVelocity
                neuralRootVel = _neuralController.CurrentVelocity;
            }

            // Blend root motion
            if (_blendRootMotion)
            {
                _blendedRootVelocity = Vector3.Lerp(procRootVel, neuralRootVel, _currentNeuralWeight);
                _blendedRootMotionDelta = _blendedRootVelocity * Time.fixedDeltaTime;
                _blendedRootRotationDelta = Quaternion.Slerp(procRootRot, neuralRootRot, _currentNeuralWeight);
            }
            else
            {
                // Use whichever controller has higher weight
                if (_currentProceduralWeight > _currentNeuralWeight)
                {
                    _blendedRootVelocity = procRootVel;
                    _blendedRootRotationDelta = procRootRot;
                }
                else
                {
                    _blendedRootVelocity = neuralRootVel;
                    _blendedRootRotationDelta = neuralRootRot;
                }
                _blendedRootMotionDelta = _blendedRootVelocity * Time.fixedDeltaTime;
            }
        }

        void ScheduleBoneBlendJob()
        {
            if (_boneCount == 0) return;

            var job = new BoneBlendJob
            {
                ProceduralRotations = _proceduralBoneRotations,
                NeuralRotations = _neuralBoneRotations,
                BlendedRotations = _blendedBoneRotations,
                ProceduralWeight = _currentProceduralWeight,
                NeuralWeight = _currentNeuralWeight,
                BoneCount = _boneCount
            };

            _blendJobHandle = job.Schedule(_boneCount, 32, default);
        }

        void ApplyBlendedPose()
        {
            if (_boneCount == 0) return;

            var bones = _boneMap.GetAllBones();
            if (bones == null) return;

            for (int i = 0; i < _boneCount && i < bones.Length; i++)
            {
                var t = bones[i].Transform;
                if (t != null)
                {
                    t.localRotation = _blendedBoneRotations[i];
                }
            }
        }

        void ApplyBlendedIK()
        {
            // IK is handled by each controller individually
            // The hybrid just blends the final bone positions
            // No additional IK blending needed here
        }

        // ──────────────────────────────────────────────
        // Bone Blend Job (Burst Compiled)
        // ──────────────────────────────────────────────

        [BurstCompile]
        struct BoneBlendJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<quaternion> ProceduralRotations;
            [ReadOnly] public NativeArray<quaternion> NeuralRotations;
            [WriteOnly] public NativeArray<quaternion> BlendedRotations;
            [ReadOnly] public float ProceduralWeight;
            [ReadOnly] public float NeuralWeight;
            [ReadOnly] public int BoneCount;

            public void Execute(int index)
            {
                if (index >= BoneCount) return;

                quaternion proc = ProceduralRotations[index];
                quaternion neur = NeuralRotations[index];

                // Slerp in quaternion space for smooth blending
                BlendedRotations[index] = math.slerp(proc, neur, NeuralWeight);
            }
        }

        // ──────────────────────────────────────────────
        // Public Utility Methods
        // ──────────────────────────────────────────────

        /// <summary>
        /// Force immediate policy switch with blending.
        /// </summary>
        public void SwitchPolicy(NeuralAnimationController.PolicyType policy)
        {
            if (_neuralController != null)
            {
                _neuralController.SwitchPolicy(policy);
            }
        }

        /// <summary>
        /// Set action target for both controllers.
        /// </summary>
        public void SetActionTarget(Vector3 target)
        {
            if (_neuralController != null)
                _neuralController.SetActionTarget(target);
            if (_proceduralController != null)
                _proceduralController.RequestAttack(target); // Use attack as generic action
        }

        /// <summary>
        /// Set velocity provider for both controllers.
        /// </summary>
        public void SetVelocityProvider(IVelocityProvider provider)
        {
            if (_proceduralController != null)
                _proceduralController.SetVelocityProvider(provider);
            if (_neuralController != null)
                _neuralController.SetVelocityProvider(provider);
        }

        /// <summary>
        /// Set bone map for both controllers (must be called before Awake).
        /// </summary>
        public void SetBoneMap(ProceduralBoneMap boneMap)
        {
            if (_proceduralController != null)
                _proceduralController.SetBoneMap(boneMap);
            if (_neuralController != null)
                _neuralController.SetBoneMap(boneMap);
            _boneMap = boneMap;
        }

        /// <summary>
        /// Get the active procedural controller.
        /// </summary>
        public ProceduralAnimationController GetProceduralController() => _proceduralController;

        /// <summary>
        /// Get the active neural controller.
        /// </summary>
        public NeuralAnimationController GetNeuralController() => _neuralController;

        /// <summary>
        /// Check if a specific policy is overridden.
        /// </summary>
        public bool IsPolicyOverridden(NeuralAnimationController.PolicyType policy)
        {
            return _policyOverrides.ContainsKey(policy) && _policyOverrides[policy].HasValue;
        }

        /// <summary>
        /// Get the override value for a policy (null if not overridden).
        /// </summary>
        public bool? GetPolicyOverride(NeuralAnimationController.PolicyType policy)
        {
            return _policyOverrides.TryGetValue(policy, out bool? value) ? value : null;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            NormalizeBaseWeights();

            // Clamp LOD distances
            _lodNeuralWeightThreshold = Mathf.Max(0f, _lodNeuralWeightThreshold);
            _lodProceduralOnlyDistance = Mathf.Max(_lodNeuralWeightThreshold, _lodProceduralOnlyDistance);
        }
#endif
    }
}