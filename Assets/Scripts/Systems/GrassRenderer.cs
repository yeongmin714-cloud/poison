using UnityEngine;
using System.Collections.Generic;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase G1-04: GPU Instancing grass renderer.
    /// Renders thousands of grass blades with wind animation, 30m distance culling,
    /// and biome color variation using Graphics.DrawMeshInstanced.
    /// </summary>
    public class GrassRenderer : MonoBehaviour
    {
        [Header("Meshes")]
        [SerializeField] private Mesh _grassBladeStraight;
        [SerializeField] private Mesh _grassBladeBentLeft;
        [SerializeField] private Mesh _grassBladeBentRight;

        [Header("Material")]
        [SerializeField] private Material _material;

        [Header("Wind Animation")]
        [SerializeField, Range(0.5f, 3f)] private float _windSpeed = 1.2f;
        [SerializeField, Range(0f, 15f)] private float _windAmount = 5f;

        [Header("Performance")]
        [SerializeField] private float _cullDistance = 30f;

        [Header("Biome")]
        [SerializeField] private Color _baseColor = new Color(0.2f, 0.7f, 0.15f);
        [SerializeField, Range(0f, 0.3f)] private float _colorVariation = 0.1f;

        // Instance data storage
        private struct GrassBladeInstance
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
            public int meshVariant; // 0=straight, 1=bent left, 2=bent right
            public float windOffset;
        }

        private List<GrassBladeInstance> _instances;
        private Camera _mainCamera;
        private WindZone _windZone;

        // Per-mesh-variant instance arrays (split into batches of 1023 for GPU Instancing)
        private struct MeshBatch
        {
            public Mesh mesh;
            public List<Matrix4x4> matrices;
            public MaterialPropertyBlock propertyBlock;
        }

        private List<MeshBatch> _batches;

        // Material property block for per-instance color
        private static readonly int _colorProperty = Shader.PropertyToID("_Color");

        // ================================================================
        // Properties (for tests / inspector)
        // ================================================================

        public Mesh GrassBladeStraight => _grassBladeStraight;
        public Mesh GrassBladeBentLeft => _grassBladeBentLeft;
        public Mesh GrassBladeBentRight => _grassBladeBentRight;
        public Material Material => _material;
        public float WindSpeed => _windSpeed;
        public float WindAmount => _windAmount;
        public float CullDistance => _cullDistance;
        public Color BaseColor => _baseColor;
        public float ColorVariation => _colorVariation;
        public int InstanceCount => _instances != null ? _instances.Count : 0;
        public int BatchCount => _batches != null ? _batches.Count : 0;

        // ================================================================
        // Public Methods
        // ================================================================

        /// <summary>
        /// Sets the meshes used for the three grass blade variants.
        /// </summary>
        public void SetMeshes(Mesh straight, Mesh bentLeft, Mesh bentRight)
        {
            _grassBladeStraight = straight;
            _grassBladeBentLeft = bentLeft;
            _grassBladeBentRight = bentRight;
        }

        /// <summary>
        /// Sets the shared material and enables GPU instancing.
        /// </summary>
        public void SetMaterial(Material mat)
        {
            _material = mat;
            if (_material != null)
            {
                _material.enableInstancing = true;
            }
        }

        /// <summary>
        /// Sets the base grass color for this biome region.
        /// </summary>
        public void SetBaseColor(Color color)
        {
            _baseColor = color;
        }

        /// <summary>
        /// Sets wind animation parameters.
        /// </summary>
        public void SetWind(float speed, float amount)
        {
            _windSpeed = speed;
            _windAmount = amount;
        }

        /// <summary>
        /// Sets cull distance for performance.
        /// </summary>
        public void SetCullDistance(float distance)
        {
            _cullDistance = distance;
        }

        /// <summary>
        /// Sets color variation amount (0-0.3).
        /// </summary>
        public void SetColorVariation(float variation)
        {
            _colorVariation = Mathf.Clamp(variation, 0f, 0.3f);
        }

        /// <summary>
        /// Clears all instances and rebuilds placement from a list of positions.
        /// </summary>
        public void PlaceBlades(List<Vector3> positions, int seed = 0)
        {
            if (positions == null || positions.Count == 0)
            {
                _instances = new List<GrassBladeInstance>();
                RebuildBatches();
                return;
            }

            System.Random rng = new System.Random(seed != 0 ? seed : gameObject.GetInstanceID());
            _instances = new List<GrassBladeInstance>(positions.Count);

            foreach (Vector3 pos in positions)
            {
                GrassBladeInstance inst = new GrassBladeInstance
                {
                    position = pos,
                    rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360f), 0f),
                    scale = new Vector3(
                        0.8f + (float)(rng.NextDouble() * 0.4f), // 0.8~1.2 width
                        0.8f + (float)(rng.NextDouble() * 0.4f), // 0.8~1.2 height
                        1f
                    ),
                    meshVariant = rng.Next(0, 3),
                    windOffset = (float)(rng.NextDouble() * Mathf.PI * 2f)
                };
                _instances.Add(inst);
            }

            RebuildBatches();
        }

        /// <summary>
        /// Rebuilds all GPU instance batches from current instance list.
        /// </summary>
        public void RebuildBatches()
        {
            const int maxBatchSize = 1023;

            if (_instances == null || _instances.Count == 0)
            {
                _batches = new List<MeshBatch>();
                return;
            }

            _batches = new List<MeshBatch>();

            // Group by mesh variant
            for (int variant = 0; variant < 3; variant++)
            {
                Mesh variantMesh = GetMeshForVariant(variant);
                if (variantMesh == null) continue;

                List<Matrix4x4> variantMatrices = new List<Matrix4x4>();
                MaterialPropertyBlock block = new MaterialPropertyBlock();

                foreach (var inst in _instances)
                {
                    if (inst.meshVariant != variant) continue;
                    variantMatrices.Add(Matrix4x4.TRS(inst.position, inst.rotation, inst.scale));
                }

                // Split into batches of 1023
                for (int i = 0; i < variantMatrices.Count; i += maxBatchSize)
                {
                    int count = Mathf.Min(maxBatchSize, variantMatrices.Count - i);
                    List<Matrix4x4> batchMatrices = variantMatrices.GetRange(i, count);

                    _batches.Add(new MeshBatch
                    {
                        mesh = variantMesh,
                        matrices = batchMatrices,
                        propertyBlock = new MaterialPropertyBlock()
                    });
                }
            }
        }

        // ================================================================
        // Unity Lifecycle
        // ================================================================

        private void Awake()
        {
            _mainCamera = Camera.main;
            _windZone = FindObjectOfType<WindZone>();

            if (_material != null)
            {
                _material.enableInstancing = true;
            }
        }

        private void Update()
        {
            if (_batches == null || _batches.Count == 0)
                return;

            if (_material == null)
                return;

            // Find main camera if not cached
            if (_mainCamera == null)
                _mainCamera = Camera.main;

            // Get wind strength
            float windStrength = 1f;
            Vector3 windDirection = Vector3.forward;
            if (_windZone != null)
            {
                windStrength = Mathf.Clamp01(_windZone.windMain);
                windDirection = _windZone.transform.forward;
            }

            // Compute sway axis from wind direction
            Vector3 swayAxis;
            if (windDirection.sqrMagnitude > 0.01f)
            {
                Vector3 windDir = windDirection.normalized;
                swayAxis = Vector3.Cross(windDir, Vector3.up).normalized;
                if (swayAxis.sqrMagnitude < 0.01f)
                    swayAxis = Vector3.forward;
            }
            else
            {
                swayAxis = Vector3.forward;
            }

            float cullSq = _cullDistance * _cullDistance;

            // Rebuild matrices from scratch each frame for correctness
            int batchIdx = 0;
            for (int variant = 0; variant < 3; variant++)
            {
                Mesh variantMesh = GetMeshForVariant(variant);
                if (variantMesh == null) continue;

                int idxInVariant = 0;
                foreach (var inst in _instances)
                {
                    if (inst.meshVariant != variant) continue;

                    bool culled = _mainCamera != null &&
                        (inst.position - _mainCamera.transform.position).sqrMagnitude > cullSq;

                    if (!culled)
                    {
                        // Wind animation: sine wave rotation
                        float swayTime = Time.time * _windSpeed + inst.windOffset;
                        float swayAngle = Mathf.Sin(swayTime) * _windAmount * windStrength;
                        Quaternion swayRotation = Quaternion.AngleAxis(swayAngle, swayAxis);
                        Quaternion finalRotation = inst.rotation * swayRotation;

                        _batches[batchIdx].matrices[idxInVariant % 1023] =
                            Matrix4x4.TRS(inst.position, finalRotation, inst.scale);
                    }
                    else
                    {
                        // Culled: zero matrix (invisible)
                        _batches[batchIdx].matrices[idxInVariant % 1023] = Matrix4x4.zero;
                    }

                    idxInVariant++;
                    if (idxInVariant > 0 && idxInVariant % 1023 == 0)
                        batchIdx++;
                }
            }
        }

        private void LateUpdate()
        {
            if (_batches == null || _batches.Count == 0)
                return;

            if (_material == null)
                return;

            // Draw all batches
            foreach (var batch in _batches)
            {
                if (batch.matrices == null || batch.matrices.Count == 0)
                    continue;

                Graphics.DrawMeshInstanced(
                    batch.mesh,
                    0,
                    _material,
                    batch.matrices.ToArray(),
                    batch.matrices.Count,
                    batch.propertyBlock,
                    UnityEngine.Rendering.ShadowCastingMode.On,
                    true, // receive shadows
                    gameObject.layer,
                    _mainCamera
                );
            }
        }

        // ================================================================
        // Helpers
        // ================================================================

        private Mesh GetMeshForVariant(int variant)
        {
            switch (variant)
            {
                case 0: return _grassBladeStraight;
                case 1: return _grassBladeBentLeft;
                case 2: return _grassBladeBentRight;
                default: return _grassBladeStraight;
            }
        }

        /// <summary>
        /// 내부 테스트용: 현재 윈도우 존 참조 (없으면 null).
        /// </summary>
        public WindZone CurrentWindZone => _windZone;
    }
}