using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace ProjectName.Systems.Animation.Procedural.LOD
{
    /// <summary>
    /// Level of Detail system for procedural animation.
    /// Reduces computation for distant characters.
    /// </summary>
    [BurstCompile]
    public struct LODCalculatorJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> Positions;
        [ReadOnly] public float3 CameraPosition;
        [ReadOnly] public float3 CameraForward;
        [ReadOnly] public float Lod0Distance;    // distance < Lod0Distance = full quality
        [ReadOnly] public float Lod1Distance;  // distance < Lod1Distance = medium quality
        [ReadOnly] public float Lod2Distance;  // distance < Lod2Distance = low quality
        // distance >= Lod2Distance = culled/minimal

        [WriteOnly] public NativeArray<int> OutLODLevel; // 0=full, 1=medium, 2=low, 3=culled

        public void Execute(int index)
        {
            float3 toChar = Positions[index] - CameraPosition;
            float dist = math.length(toChar);
            float distSq = math.lengthsq(toChar);

            // Frustum culling (simple cone check)
            float3 dir = math.normalize(toChar);
            float dot = math.dot(dir, CameraForward);
            bool inFront = dot > -0.5f; // 120 degree cone

            int lod = 3; // default culled
            if (inFront)
            {
                if (dist < Lod0Distance) lod = 0;
                else if (dist < Lod1Distance) lod = 1;
                else if (dist < Lod2Distance) lod = 2;
                else lod = 3;
            }

            OutLODLevel[index] = lod;
        }
    }

    /// <summary>
    /// LOD settings per quality level.
    /// </summary>
    [System.Serializable]
    public struct LODSettings
    {
        public bool ComputeFootIK;
        public bool ComputeHandIK;
        public bool ComputeSpineIK;
        public bool ComputeHeadLook;
        public bool ComputeHipShift;
        public bool ComputeSpineCounterRotation;
        public bool UpdatePhases;
        public int IKIterations;
        public float PhaseUpdateRate; // 1.0 = every frame, 0.5 = every other frame

        public static LODSettings Full() => new LODSettings
        {
            ComputeFootIK = true,
            ComputeHandIK = true,
            ComputeSpineIK = true,
            ComputeHeadLook = true,
            ComputeHipShift = true,
            ComputeSpineCounterRotation = true,
            UpdatePhases = true,
            IKIterations = 2,
            PhaseUpdateRate = 1.0f
        };

        public static LODSettings Medium() => new LODSettings
        {
            ComputeFootIK = true,
            ComputeHandIK = false,
            ComputeSpineIK = true,
            ComputeHeadLook = true,
            ComputeHipShift = true,
            ComputeSpineCounterRotation = false,
            UpdatePhases = true,
            IKIterations = 1,
            PhaseUpdateRate = 0.5f
        };

        public static LODSettings Low() => new LODSettings
        {
            ComputeFootIK = true,
            ComputeHandIK = false,
            ComputeSpineIK = false,
            ComputeHeadLook = false,
            ComputeHipShift = false,
            ComputeSpineCounterRotation = false,
            UpdatePhases = true,
            IKIterations = 1,
            PhaseUpdateRate = 0.25f
        };

        public static LODSettings Culled() => new LODSettings
        {
            ComputeFootIK = false,
            ComputeHandIK = false,
            ComputeSpineIK = false,
            ComputeHeadLook = false,
            ComputeHipShift = false,
            ComputeSpineCounterRotation = false,
            UpdatePhases = false,
            IKIterations = 0,
            PhaseUpdateRate = 0f
        };
    }

    /// <summary>
    /// LOD Manager component - attach to camera or manager object.
    /// </summary>
    public class ProceduralLODManager : MonoBehaviour
    {
        [Header("LOD Distances")]
        [SerializeField] float _lod0Distance = 15f;
        [SerializeField] float _lod1Distance = 30f;
        [SerializeField] float _lod2Distance = 50f;

        [Header("Camera Reference")]
        [SerializeField] Camera _camera;

        // Runtime
        NativeArray<float3> _positions;
        NativeArray<int> _lodLevels;
        JobHandle _lodJobHandle;

        // Tracked controllers
        ProceduralAnimationController[] _controllers;

        void Awake()
        {
            if (_camera == null) _camera = Camera.main;
            _controllers = FindObjectsOfType<ProceduralAnimationController>();
            AllocateArrays();
        }

        void AllocateArrays()
        {
            int count = _controllers.Length;
            _positions = new NativeArray<float3>(count, Allocator.Persistent);
            _lodLevels = new NativeArray<int>(count, Allocator.Persistent);
        }

        void OnDestroy()
        {
            _lodJobHandle.Complete();
            if (_positions.IsCreated) _positions.Dispose();
            if (_lodLevels.IsCreated) _lodLevels.Dispose();
        }

        void Update()
        {
            // Refresh controller list
            _controllers = FindObjectsOfType<ProceduralAnimationController>();
            if (_positions.Length != _controllers.Length)
            {
                if (_positions.IsCreated) _positions.Dispose();
                if (_lodLevels.IsCreated) _lodLevels.Dispose();
                AllocateArrays();
            }

            // Update positions
            for (int i = 0; i < _controllers.Length; i++)
            {
                if (_controllers[i] != null)
                    _positions[i] = _controllers[i].transform.position;
                else
                    _positions[i] = float3(0f, -1000f, 0f); // far away
            }

            // Schedule LOD calculation
            var job = new LODCalculatorJob
            {
                Positions = _positions,
                CameraPosition = _camera ? _camera.transform.position : float3.zero,
                CameraForward = _camera ? _camera.transform.forward : math.forward(),
                Lod0Distance = _lod0Distance,
                Lod1Distance = _lod1Distance,
                Lod2Distance = _lod2Distance,
                OutLODLevel = _lodLevels
            };

            _lodJobHandle = job.Schedule(_controllers.Length, 32, default);
        }

        void LateUpdate()
        {
            _lodJobHandle.Complete();

            // Apply LOD settings
            for (int i = 0; i < _controllers.Length; i++)
            {
                var ctrl = _controllers[i];
                if (ctrl == null) continue;

                int lod = _lodLevels[i];
                ApplyLODSettings(ctrl, lod);
            }
        }

        void ApplyLODSettings(ProceduralAnimationController ctrl, int lod)
        {
            // This would require adding LODSettings property to ProceduralAnimationController
            // For now, just a placeholder
        }
    }
}