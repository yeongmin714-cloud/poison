using System;
using Unity.Mathematics;
using UnityEngine;
using Unity.Sentis;

namespace ProjectName.Systems.Animation.Neural
{
    // ─────────────────────────────────────────────────────────────────────────────
    //  PolicyMetadata
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Describes the avatar type for policy selection and routing.
    /// Maps to <see cref="CharacterType"/> in AnimationBoneDefinitions.
    /// </summary>
    public enum AvatarType
    {
        /// <summary>Two-legged humanoid (player, NPC, soldier).</summary>
        Humanoid,
        /// <summary>Four-legged creature (wolf, boar, horse, deer).</summary>
        Quadruped,
        /// <summary>Multi-legged or non-standard skeleton (spider, centipede).</summary>
        MultiLeg,
        /// <summary>Flying creature (bird, dragon).</summary>
        Flying,
        /// <summary>Swimming creature (fish, aquatic).</summary>
        Swimming,
        /// <summary>Any other type — fallback heuristic.</summary>
        Other
    }

    /// <summary>
    /// Quantization format used for the policy model.
    /// </summary>
    public enum QuantizationFormat
    {
        /// <summary>No quantization — full FP32.</summary>
        FP32,
        /// <summary>FP16 half-precision.</summary>
        FP16,
        /// <summary>Dynamic INT8 quantization.</summary>
        INT8,
        /// <summary>Unsigned INT8 quantization.</summary>
        UINT8
    }

    /// <summary>
    /// Immutable metadata describing a policy model's version, I/O specs, and quantization info.
    /// Used by <see cref="ModelRegistry"/> and <see cref="NeuralAnimationController"/>
    /// to validate compatibility at load time.
    /// </summary>
    [Serializable]
    public struct PolicyMetadata : IEquatable<PolicyMetadata>
    {
        /// <summary>Semantic version of the model (e.g. "1.2.0").</summary>
        public string ModelVersion;

        /// <summary>Target avatar type this policy was trained for.</summary>
        public AvatarType AvatarType;

        /// <summary>Human-readable policy name (e.g. "Locomotion_Biped_Base").</summary>
        public string PolicyName;

        /// <summary>Size of the observation/input tensor.</summary>
        public int ObservationSize;

        /// <summary>Size of the action/output tensor.</summary>
        public int ActionSize;

        /// <summary>Number of joints the policy expects.</summary>
        public int JointCount;

        /// <summary>Terrain heightmap resolution (e.g. 11 for 11×11). Zero if unused.</summary>
        public int TerrainHeightmapResolution;

        /// <summary>Quantization format of the stored model weights.</summary>
        public QuantizationFormat Quantization;

        /// <summary>Model file path relative to StreamingAssets or Resources.</summary>
        public string ModelPath;

        /// <summary>Optional: expected inference latency target in milliseconds (for LOD scheduling).</summary>
        public float ExpectedLatencyMs;

        /// <summary>Optional: size of the style/latent conditioning vector (0 if none).</summary>
        public int StyleEmbeddingSize;

        public bool Equals(PolicyMetadata other)
        {
            return ModelVersion == other.ModelVersion
                && AvatarType == other.AvatarType
                && PolicyName == other.PolicyName
                && ObservationSize == other.ObservationSize
                && ActionSize == other.ActionSize
                && JointCount == other.JointCount
                && TerrainHeightmapResolution == other.TerrainHeightmapResolution
                && Quantization == other.Quantization
                && ModelPath == other.ModelPath;
        }

        public override bool Equals(object obj) =>
            obj is PolicyMetadata other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(ModelVersion, (int)AvatarType, PolicyName,
                             ObservationSize, ActionSize, JointCount,
                             TerrainHeightmapResolution, (int)Quantization, ModelPath);

        public static bool operator ==(PolicyMetadata left, PolicyMetadata right) => left.Equals(right);
        public static bool operator !=(PolicyMetadata left, PolicyMetadata right) => !left.Equals(right);

        /// <summary>
        /// Validate that the metadata is internally consistent.
        /// </summary>
        public bool IsValid =>
            !string.IsNullOrEmpty(ModelVersion) &&
            !string.IsNullOrEmpty(PolicyName) &&
            ObservationSize > 0 &&
            ActionSize > 0 &&
            JointCount > 0 &&
            !string.IsNullOrEmpty(ModelPath);

        /// <summary>
        /// Create a standard locomotion policy metadata for a humanoid.
        /// </summary>
        public static PolicyMetadata CreateLocomotionBipedBase(string modelPath, int jointCount = 18)
        {
            return new PolicyMetadata
            {
                ModelVersion = "1.0.0",
                AvatarType = AvatarType.Humanoid,
                PolicyName = "Locomotion_Biped_Base",
                ObservationSize = 120,
                ActionSize = 80,
                JointCount = jointCount,
                TerrainHeightmapResolution = 11,
                Quantization = QuantizationFormat.INT8,
                ModelPath = modelPath,
                ExpectedLatencyMs = 2.0f,
                StyleEmbeddingSize = 8
            };
        }

        /// <summary>
        /// Create a standard locomotion policy metadata for a quadruped.
        /// </summary>
        public static PolicyMetadata CreateLocomotionQuadrupedBase(string modelPath, int jointCount = 24)
        {
            return new PolicyMetadata
            {
                ModelVersion = "1.0.0",
                AvatarType = AvatarType.Quadruped,
                PolicyName = "Locomotion_Quadruped_Base",
                ObservationSize = 150,
                ActionSize = 100,
                JointCount = jointCount,
                TerrainHeightmapResolution = 11,
                Quantization = QuantizationFormat.INT8,
                ModelPath = modelPath,
                ExpectedLatencyMs = 3.0f,
                StyleEmbeddingSize = 8
            };
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  IPolicy Interface
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Abstract policy interface for neural animation control.
    /// Implementations wrap runtime inference engines (Sentis, ONNX Runtime, Barracuda)
    /// and provide a uniform API for observation encoding, inference, and action decoding.
    /// </summary>
    public interface IPolicy : IDisposable
    {
        /// <summary>
        /// Returns the fixed size of the observation tensor expected by this policy.
        /// </summary>
        int GetObservationSize();

        /// <summary>
        /// Returns the fixed size of the action tensor produced by this policy.
        /// </summary>
        int GetActionSize();

        /// <summary>
        /// Run inference on the given observation tensor and produce an action tensor.
        /// Must be thread-safe if called from a job.
        /// </summary>
        /// <param name="observation">Flat float array of length <see cref="GetObservationSize"/>.</param>
        /// <param name="action">Output flat float array of length <see cref="GetActionSize"/>.</param>
        /// <returns>True if inference succeeded, false on error or fallback needed.</returns>
        bool Infer(float[] observation, float[] action);

        /// <summary>
        /// Metadata describing this policy's version, I/O specs, and quantization.
        /// </summary>
        PolicyMetadata Metadata { get; }

        /// <summary>
        /// Whether the policy has been initialized and is ready for inference.
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// Reset any internal state (e.g. recurrent state, noise seed).
        /// Called when the policy is re-bound to a new character or after a reset.
        /// </summary>
        void ResetState();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  ONNXPolicy — Sentis-backed implementation
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Concrete policy implementation using Unity Sentis (formerly NNModel).
    /// Loads an ONNX model via <see cref="ModelLoader"/>, creates a worker,
    /// and performs synchronous inference.
    ///
    /// Usage:
    /// <code>
    /// var policy = new ONNXPolicy(metadata, workerType);
    /// policy.Infer(observation, action);
    /// // ... use action ...
    /// policy.Dispose();
    /// </code>
    /// </summary>
    public sealed class ONNXPolicy : IPolicy
    {
        // ── Model / Worker ────────────────────────────────────────────────────

        private Model _model;
        private IWorker _worker;
        private readonly BackendType _backendType;

        // ── Tensor binding ────────────────────────────────────────────────────

        private TensorFloat _inputTensor;
        private readonly string _inputName;
        private readonly string _outputName;

        // ── Metadata ──────────────────────────────────────────────────────────

        private readonly PolicyMetadata _metadata;

        public PolicyMetadata Metadata => _metadata;
        public bool IsReady { get; private set; }

        // ── Constructor ───────────────────────────────────────────────────────

        /// <summary>
        /// Create a new ONNX policy from a <see cref="PolicyMetadata"/>.
        /// The model is loaded synchronously from <see cref="PolicyMetadata.ModelPath"/>.
        /// </summary>
        /// <param name="metadata">Policy metadata including model path.</param>
        /// <param name="backendType">Sentis backend (GPU/CPU preference).</param>
        /// <param name="inputName">Name of the input tensor in the ONNX graph.</param>
        /// <param name="outputName">Name of the output tensor in the ONNX graph.</param>
        public ONNXPolicy(PolicyMetadata metadata,
                          BackendType backendType = BackendType.GPUCompute,
                          string inputName = "observation",
                          string outputName = "action")
        {
            _metadata = metadata;
            _backendType = backendType;
            _inputName = inputName;
            _outputName = outputName;

            if (!LoadModel())
            {
                Debug.LogError($"[ONNXPolicy] Failed to load model: {metadata.ModelPath}");
            }
        }

        /// <summary>
        /// Create a policy from an already-loaded Sentis <see cref="Model"/>.
        /// Useful when models are cached via <see cref="ModelLoader"/>.
        /// </summary>
        public ONNXPolicy(Model model,
                          PolicyMetadata metadata,
                          BackendType backendType = BackendType.GPUCompute,
                          string inputName = "observation",
                          string outputName = "action")
        {
            _metadata = metadata;
            _backendType = backendType;
            _inputName = inputName;
            _outputName = outputName;
            _model = model;

            if (!CreateWorker())
            {
                Debug.LogError($"[ONNXPolicy] Failed to create worker for model: {metadata.PolicyName}");
            }
        }

        // ── IPolicy ───────────────────────────────────────────────────────────

        public int GetObservationSize() => _metadata.ObservationSize;
        public int GetActionSize() => _metadata.ActionSize;

        public bool Infer(float[] observation, float[] action)
        {
            if (!IsReady)
            {
                Debug.LogWarning("[ONNXPolicy] Infer called but policy is not ready.");
                return false;
            }

            if (observation == null || observation.Length < _metadata.ObservationSize)
            {
                Debug.LogError($"[ONNXPolicy] Observation array too small. Expected {_metadata.ObservationSize}, got {observation?.Length ?? 0}.");
                return false;
            }

            if (action == null || action.Length < _metadata.ActionSize)
            {
                Debug.LogError($"[ONNXPolicy] Action array too small. Expected {_metadata.ActionSize}, got {action?.Length ?? 0}.");
                return false;
            }

            try
            {
                // Create input tensor from observation array
                var inputShape = new TensorShape(1, 1, 1, _metadata.ObservationSize);
                _inputTensor?.Dispose();
                _inputTensor = new TensorFloat(inputShape, observation);

                // Execute inference
                _worker.Execute(_inputTensor);

                // Read output tensor
                using var outputTensor = _worker.PeekOutput(_outputName) as TensorFloat;
                if (outputTensor == null)
                {
                    Debug.LogError("[ONNXPolicy] Output tensor is null or wrong type.");
                    return false;
                }

                // Copy to action array
                outputTensor.ReadbackAndClone();
                var outputData = outputTensor.ToReadOnlyArray();
                int copyCount = Math.Min(outputData.Length, _metadata.ActionSize);
                Array.Copy(outputData, 0, action, 0, copyCount);

                // Zero-fill remaining if output is smaller than expected
                for (int i = copyCount; i < _metadata.ActionSize; i++)
                    action[i] = 0f;

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ONNXPolicy] Inference failed: {ex.Message}");
                return false;
            }
        }

        public void ResetState()
        {
            // For feed-forward policies this is a no-op.
            // For recurrent policies (LSTM/GRU), clear hidden state here.
            Debug.Log($"[ONNXPolicy] ResetState called for {_metadata.PolicyName}.");
        }

        public void Dispose()
        {
            _inputTensor?.Dispose();
            _inputTensor = null;
            _worker?.Dispose();
            _worker = null;
            _model?.Dispose();
            _model = null;
            IsReady = false;
        }

        // ── Internal helpers ──────────────────────────────────────────────────

        private bool LoadModel()
        {
            try
            {
                _model = SentisModelLoader.Load(_metadata.ModelPath);
                if (_model == null)
                {
                    Debug.LogError($"[ONNXPolicy] ModelLoader returned null for {_metadata.ModelPath}");
                    return false;
                }
                return CreateWorker();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ONNXPolicy] Failed to load model {_metadata.ModelPath}: {ex.Message}");
                return false;
            }
        }

        private bool CreateWorker()
        {
            try
            {
                _worker = WorkerFactory.CreateWorker(_backendType, _model);
                IsReady = _worker != null;
                return IsReady;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ONNXPolicy] Failed to create worker: {ex.Message}");
                return false;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  ObservationEncoder
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Encodes raw game state into a normalized observation tensor for the policy network.
    /// Handles:
    /// - Joint positions/velocities normalization (StandardScaler-like)
    /// - Terrain heightmap sampling and encoding
    /// - Root velocity, angular velocity, target direction/speed
    /// - Contact flags, gait phase, style embedding
    ///
    /// The encoding layout is deterministic and must match the training pipeline.
    /// </summary>
    public sealed class ObservationEncoder
    {
        // ── Normalization constants (StandardScaler: mean + std) ──────────────

        private readonly float[] _jointPositionMean;
        private readonly float[] _jointPositionStd;
        private readonly float[] _jointVelocityMean;
        private readonly float[] _jointVelocityStd;
        private readonly float _rootVelocityScale;
        private readonly float _rootAngularVelocityScale;

        // ── Terrain ───────────────────────────────────────────────────────────

        private readonly int _terrainResolution;
        private readonly float _terrainSampleRadius;
        private readonly float _terrainHeightScale;
        private readonly float _terrainHeightMean;

        // ── Configuration ─────────────────────────────────────────────────────

        private readonly int _jointCount;
        private readonly int _totalObservationSize;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Total size of the flat observation array produced by <see cref="Encode"/>.
        /// </summary>
        public int TotalObservationSize => _totalObservationSize;

        /// <summary>
        /// Create a standard observation encoder suitable for a locomotion policy.
        /// </summary>
        /// <param name="jointCount">Number of animated joints.</param>
        /// <param name="terrainResolution">Heightmap resolution (e.g. 11 for 11×11).</param>
        /// <param name="terrainSampleRadius">World-space radius to sample terrain.</param>
        /// <param name="includeStyleEmbedding">Whether to reserve space for a style vector.</param>
        /// <param name="styleEmbeddingSize">Size of the style vector if included.</param>
        public ObservationEncoder(
            int jointCount,
            int terrainResolution = 11,
            float terrainSampleRadius = 1.5f,
            bool includeStyleEmbedding = true,
            int styleEmbeddingSize = 8)
        {
            _jointCount = jointCount;
            _terrainResolution = terrainResolution;
            _terrainSampleRadius = terrainSampleRadius;
            _terrainHeightScale = 1.0f / 5.0f; // Normalize terrain height to [-1, 1]
            _terrainHeightMean = 0f;

            // Default normalization constants (tuned for humanoid locomotion)
            _rootVelocityScale = 1.0f / 10.0f;
            _rootAngularVelocityScale = 1.0f / 360.0f;

            _jointPositionMean = new float[jointCount * 3];
            _jointPositionStd = new float[jointCount * 3];
            _jointVelocityMean = new float[jointCount * 3];
            _jointVelocityStd = new float[jointCount * 3];

            // Initialize with reasonable defaults (can be overridden via LoadNormalization)
            for (int i = 0; i < jointCount * 3; i++)
            {
                _jointPositionMean[i] = 0f;
                _jointPositionStd[i] = 1f;
                _jointVelocityMean[i] = 0f;
                _jointVelocityStd[i] = 5f;
            }

            // Compute total observation size
            int rootVel = 3;               // Root linear velocity
            int rootAngVel = 3;            // Root angular velocity
            int jointPos = jointCount * 3; // Joint positions
            int jointVel = jointCount * 3; // Joint velocities
            int targetDir = 2;             // Target direction (x, z)
            int targetSpeed = 1;           // Target speed
            int terrain = terrainResolution * terrainResolution; // Heightmap
            int contactFlags = 4;          // Foot contact flags (e.g. LF, RF, LH, RH)
            int gaitPhase = 1;             // Gait phase [0, 1)
            int style = includeStyleEmbedding ? styleEmbeddingSize : 0;

            _totalObservationSize = rootVel + rootAngVel + jointPos + jointVel +
                                     targetDir + targetSpeed + terrain +
                                     contactFlags + gaitPhase + style;
        }

        /// <summary>
        /// Override normalization parameters from a trained StandardScaler.
        /// </summary>
        /// <param name="jointPositionMean">Mean for each joint position dimension.</param>
        /// <param name="jointPositionStd">Std for each joint position dimension.</param>
        /// <param name="jointVelocityMean">Mean for each joint velocity dimension.</param>
        /// <param name="jointVelocityStd">Std for each joint velocity dimension.</param>
        public void LoadNormalization(float[] jointPositionMean, float[] jointPositionStd,
                                       float[] jointVelocityMean, float[] jointVelocityStd)
        {
            if (jointPositionMean.Length == _jointPositionMean.Length)
                Array.Copy(jointPositionMean, _jointPositionMean, _jointPositionMean.Length);
            if (jointPositionStd.Length == _jointPositionStd.Length)
                Array.Copy(jointPositionStd, _jointPositionStd, _jointPositionStd.Length);
            if (jointVelocityMean.Length == _jointVelocityMean.Length)
                Array.Copy(jointVelocityMean, _jointVelocityMean, _jointVelocityMean.Length);
            if (jointVelocityStd.Length == _jointVelocityStd.Length)
                Array.Copy(jointVelocityStd, _jointVelocityStd, _jointVelocityStd.Length);
        }

        /// <summary>
        /// Encode raw game state into a flat observation array.
        /// </summary>
        /// <param name="rootVelocity">Root body linear velocity (world space).</param>
        /// <param name="rootAngularVelocity">Root body angular velocity (world space).</param>
        /// <param name="jointPositions">Joint positions relative to root (flat: jointCount × 3).</param>
        /// <param name="jointVelocities">Joint velocities (flat: jointCount × 3).</param>
        /// <param name="targetDirection">Desired movement direction (x, z) in root space.</param>
        /// <param name="targetSpeed">Desired movement speed.</param>
        /// <param name="terrainHeightmap">2D heightmap array (terrainResolution × terrainResolution).</param>
        /// <param name="contactFlags">4-element contact flag array (LF, RF, LH, RH).</param>
        /// <param name="gaitPhase">Current gait phase in [0, 1).</param>
        /// <param name="styleEmbedding">Optional style embedding vector (may be null/empty).</param>
        /// <param name="output">Pre-allocated output array of length <see cref="TotalObservationSize"/>.</param>
        /// <returns>True if encoding succeeded.</returns>
        public bool Encode(
            float3 rootVelocity,
            float3 rootAngularVelocity,
            float[] jointPositions,
            float[] jointVelocities,
            float2 targetDirection,
            float targetSpeed,
            float[,] terrainHeightmap,
            bool[] contactFlags,
            float gaitPhase,
            float[] styleEmbedding,
            float[] output)
        {
            if (output == null || output.Length < _totalObservationSize)
            {
                Debug.LogError($"[ObservationEncoder] Output array too small. Expected {_totalObservationSize}, got {output?.Length ?? 0}.");
                return false;
            }

            int idx = 0;

            // 1. Root linear velocity (normalized)
            output[idx++] = rootVelocity.x * _rootVelocityScale;
            output[idx++] = rootVelocity.y * _rootVelocityScale;
            output[idx++] = rootVelocity.z * _rootVelocityScale;

            // 2. Root angular velocity (normalized)
            output[idx++] = rootAngularVelocity.x * _rootAngularVelocityScale;
            output[idx++] = rootAngularVelocity.y * _rootAngularVelocityScale;
            output[idx++] = rootAngularVelocity.z * _rootAngularVelocityScale;

            // 3. Joint positions (StandardScaler normalization)
            int jointPosCount = math.min(jointPositions?.Length ?? 0, _jointCount * 3);
            for (int i = 0; i < _jointCount * 3; i++)
            {
                if (i < jointPosCount)
                    output[idx++] = (jointPositions[i] - _jointPositionMean[i]) / _jointPositionStd[i];
                else
                    output[idx++] = 0f; // Zero-pad missing joints
            }

            // 4. Joint velocities (StandardScaler normalization)
            int jointVelCount = math.min(jointVelocities?.Length ?? 0, _jointCount * 3);
            for (int i = 0; i < _jointCount * 3; i++)
            {
                if (i < jointVelCount)
                    output[idx++] = (jointVelocities[i] - _jointVelocityMean[i]) / _jointVelocityStd[i];
                else
                    output[idx++] = 0f;
            }

            // 5. Target direction
            output[idx++] = targetDirection.x;
            output[idx++] = targetDirection.y;

            // 6. Target speed (normalized)
            output[idx++] = targetSpeed * _rootVelocityScale;

            // 7. Terrain heightmap (flattened, normalized)
            if (terrainHeightmap != null)
            {
                int res = _terrainResolution;
                for (int z = 0; z < res; z++)
                {
                    for (int x = 0; x < res; x++)
                    {
                        float h = terrainHeightmap[z, x];
                        output[idx++] = (h - _terrainHeightMean) * _terrainHeightScale;
                    }
                }
            }
            else
            {
                int terrainCount = _terrainResolution * _terrainResolution;
                for (int i = 0; i < terrainCount; i++)
                    output[idx++] = 0f;
            }

            // 8. Contact flags (as float)
            for (int i = 0; i < 4; i++)
            {
                output[idx++] = (contactFlags != null && i < contactFlags.Length && contactFlags[i]) ? 1f : 0f;
            }

            // 9. Gait phase (sin/cos encoding for continuity)
            output[idx++] = math.sin(gaitPhase * 2f * math.PI);
            // Note: gait phase cos is not included in standard layout;
            // the policy can reconstruct phase from sin alone.

            // 10. Style embedding (optional)
            if (styleEmbedding != null && styleEmbedding.Length > 0)
            {
                int copyLen = math.min(styleEmbedding.Length, _totalObservationSize - idx);
                Array.Copy(styleEmbedding, 0, output, idx, copyLen);
                idx += copyLen;
            }

            return true;
        }

        /// <summary>
        /// Convenience: sample terrain heightmap around a world position.
        /// Uses <see cref="TerrainCache"/> or Physics.Raycast to build the heightmap.
        /// </summary>
        /// <param name="worldPosition">Center position to sample around.</param>
        /// <param name="terrainCache">Optional terrain cache for fast lookups.</param>
        /// <returns>2D array of terrain heights.</returns>
        public float[,] SampleTerrainHeightmap(float3 worldPosition, object terrainCache = null)
        {
            var heightmap = new float[_terrainResolution, _terrainResolution];
            float halfRadius = _terrainSampleRadius;
            float step = (2f * halfRadius) / (_terrainResolution - 1);

            // If a TerrainCache is provided, use it for fast lookups
            // (TerrainCache is defined in Procedural — we'll use reflection-safe string check)
            bool useTerrainCache = terrainCache != null;

            for (int z = 0; z < _terrainResolution; z++)
            {
                for (int x = 0; x < _terrainResolution; x++)
                {
                    float sampleX = worldPosition.x - halfRadius + x * step;
                    float sampleZ = worldPosition.z - halfRadius + z * step;
                    float sampleY = worldPosition.y + 10f;

                    if (useTerrainCache)
                    {
                        // If terrainCache has a SampleHeight method, call it via reflection
                        // or direct cast. For now, fall through to raycast.
                    }

                    // Fallback: Physics.Raycast downward
                    if (Physics.Raycast(new Vector3(sampleX, sampleY, sampleZ),
                                         Vector3.down, out RaycastHit hit, 20f,
                                         LayerMask.GetMask("Terrain")))
                    {
                        heightmap[z, x] = hit.point.y;
                    }
                    else
                    {
                        heightmap[z, x] = worldPosition.y; // Default to body height
                    }
                }
            }

            return heightmap;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  ActionDecoder
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Decodes the raw action tensor from a policy network into concrete
    /// animation parameters: bone local rotations, root motion delta, and IK targets.
    ///
    /// The action layout must match the training pipeline:
    /// - Bone local rotations: N × 4 (quaternions, flattened)
    /// - Root motion delta: 6 (position delta xyz + rotation delta xyz in root space)
    /// - IK targets: 4 × 3 (LF, RF, LH, RH foot positions in root space)
    /// - Optional: blend weights, stiffness, phase residuals
    /// </summary>
    public sealed class ActionDecoder
    {
        // ── Configuration ─────────────────────────────────────────────────────

        private readonly int _jointCount;
        private readonly int _actionSize;
        private readonly float _rotationScale;
        private readonly float _rootMotionScale;

        // ── Layout offsets ────────────────────────────────────────────────────

        private int _boneRotationOffset;
        private int _rootMotionOffset;
        private int _ikTargetOffset;
        private int _blendWeightOffset;
        private int _reservedOffset;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Total action size expected by this decoder.
        /// </summary>
        public int ActionSize => _actionSize;

        /// <summary>
        /// Number of joints decoded.
        /// </summary>
        public int JointCount => _jointCount;

        /// <summary>
        /// Create a standard action decoder for locomotion policies.
        /// </summary>
        /// <param name="jointCount">Number of animated joints.</param>
        /// <param name="includeIKTargets">Whether IK targets are in the action space.</param>
        /// <param name="includeBlendWeights">Whether blend weights are included.</param>
        /// <param name="rotationScale">Scale factor for rotation outputs (policy outputs in [-1, 1]).</param>
        /// <param name="rootMotionScale">Scale factor for root motion outputs.</param>
        public ActionDecoder(
            int jointCount,
            bool includeIKTargets = true,
            bool includeBlendWeights = false,
            float rotationScale = 1.0f,
            float rootMotionScale = 1.0f)
        {
            _jointCount = jointCount;
            _rotationScale = rotationScale;
            _rootMotionScale = rootMotionScale;

            // Compute layout
            _boneRotationOffset = 0;
            int boneRotationCount = jointCount * 4; // Quaternions

            _rootMotionOffset = _boneRotationOffset + boneRotationCount;
            int rootMotionCount = 6; // posΔ xyz + rotΔ xyz

            _ikTargetOffset = _rootMotionOffset + rootMotionCount;
            int ikTargetCount = includeIKTargets ? 4 * 3 : 0; // 4 feet × 3 position

            _blendWeightOffset = _ikTargetOffset + ikTargetCount;
            int blendWeightCount = includeBlendWeights ? 4 : 0; // 4 blend weights

            _reservedOffset = _blendWeightOffset + blendWeightCount;
            int reservedCount = 4; // Reserved for future use (stiffness, damping, etc.)

            _actionSize = _reservedOffset + reservedCount;
        }

        /// <summary>
        /// Decode a raw action array into structured animation outputs.
        /// </summary>
        /// <param name="action">Raw float array from policy inference.</param>
        /// <param name="boneRotations">Output: local rotation quaternions for each joint (jointCount × 4).</param>
        /// <param name="rootMotionDelta">Output: root motion delta (posΔ xyz, rotΔ xyz).</param>
        /// <param name="ikTargets">Output: IK target positions in root space (4 × 3, may be null).</param>
        /// <param name="blendWeights">Output: blend weights (may be null).</param>
        /// <returns>True if decoding succeeded.</returns>
        public bool Decode(
            float[] action,
            float[] boneRotations,
            out float3[] rootMotionDelta,
            out float3[] ikTargets,
            out float[] blendWeights)
        {
            rootMotionDelta = new float3[2]; // [positionDelta, rotationDelta]
            ikTargets = new float3[4];        // [LF, RF, LH, RH]
            blendWeights = new float[4];      // 4 blend weights

            if (action == null || action.Length < _actionSize)
            {
                Debug.LogError($"[ActionDecoder] Action array too small. Expected {_actionSize}, got {action?.Length ?? 0}.");
                return false;
            }

            // 1. Bone local rotations (quaternions, unpacked from [-1, 1])
            if (boneRotations != null)
            {
                int copyLen = math.min(boneRotations.Length, _jointCount * 4);
                for (int i = 0; i < copyLen; i++)
                {
                    boneRotations[i] = action[_boneRotationOffset + i] * _rotationScale;
                }
                // Zero-fill remaining
                for (int i = copyLen; i < _jointCount * 4; i++)
                    boneRotations[i] = 0f;
            }

            // 2. Root motion delta
            if (rootMotionDelta != null && rootMotionDelta.Length >= 2)
            {
                rootMotionDelta[0] = new float3(
                    action[_rootMotionOffset + 0] * _rootMotionScale,
                    action[_rootMotionOffset + 1] * _rootMotionScale,
                    action[_rootMotionOffset + 2] * _rootMotionScale
                );
                rootMotionDelta[1] = new float3(
                    action[_rootMotionOffset + 3] * _rotationScale,
                    action[_rootMotionOffset + 4] * _rotationScale,
                    action[_rootMotionOffset + 5] * _rotationScale
                );
            }

            // 3. IK targets (foot positions in root space)
            if (ikTargets != null && ikTargets.Length >= 4)
            {
                for (int i = 0; i < 4; i++)
                {
                    ikTargets[i] = new float3(
                        action[_ikTargetOffset + i * 3 + 0],
                        action[_ikTargetOffset + i * 3 + 1],
                        action[_ikTargetOffset + i * 3 + 2]
                    );
                }
            }

            // 4. Blend weights
            if (blendWeights != null && blendWeights.Length >= 4)
            {
                for (int i = 0; i < 4; i++)
                {
                    blendWeights[i] = math.saturate(action[_blendWeightOffset + i]);
                }
            }

            return true;
        }

        /// <summary>
        /// Decode only the root motion delta (for early-out / non-animated entities).
        /// </summary>
        public float3[] DecodeRootMotionOnly(float[] action)
        {
            if (action == null || action.Length < _actionSize)
                return new float3[] { float3.zero, float3.zero };

            return new float3[]
            {
                new float3(
                    action[_rootMotionOffset + 0] * _rootMotionScale,
                    action[_rootMotionOffset + 1] * _rootMotionScale,
                    action[_rootMotionOffset + 2] * _rootMotionScale
                ),
                new float3(
                    action[_rootMotionOffset + 3] * _rotationScale,
                    action[_rootMotionOffset + 4] * _rotationScale,
                    action[_rootMotionOffset + 5] * _rotationScale
                )
            };
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  ModelLoader — lightweight Sentis model loader
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Static helper to load Sentis models from various sources.
    /// In the full Phase 4.0 implementation, this will be replaced by
    /// <see cref="ModelRegistry"/> with caching and async loading.
    /// </summary>
    internal static class SentisModelLoader
    {
        /// <summary>
        /// Load a Sentis <see cref="Model"/> from the given path.
        /// Supports Resources paths ("path/to/model") and
        /// StreamingAssets paths ("path/to/model.onnx").
        /// </summary>
        public static Model Load(string modelPath)
        {
            if (string.IsNullOrEmpty(modelPath))
            {
                Debug.LogError("[SentisModelLoader] Model path is null or empty.");
                return null;
            }

            // Try Resources first (no extension)
            Model model = Resources.Load<Model>(modelPath);
            if (model != null)
                return model;

            // Try as .onnx in StreamingAssets or absolute path
            try
            {
                model = Unity.Sentis.ModelLoader.Load(onnxPath: modelPath);
                return model;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SentisModelLoader] Failed to load model from {modelPath}: {ex.Message}");
                return null;
            }
        }
    }
}