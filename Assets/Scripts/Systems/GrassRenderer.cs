using UnityEngine;
using System.Collections.Generic;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase G1-04 + T4: GPU Instancing grass renderer with static bootstrap.
    /// Renders thousands of grass blades with wind animation, 30~45m culling,
    /// biome/nation density rules (East=dense, North=low, South/West=none,
    /// Empire 120m excluded, lake shore excluded) using Graphics.DrawMeshInstanced.
    ///
    /// T4 additions:
    ///   - public static GrassRenderer Bootstrap(Transform followTarget, Transform parent)
    ///     static bootstrapping entry point that follows the player and places grass
    ///     around them with cell-based relocation on movement.
    ///   - GLB grass loading from Resources/Models/UserProvided/terrain/grass (7 prefabs).
    ///     Mesh extracted from GLB prefab; falls back to a procedural quad if none found.
    ///   - Max instance cap (MaxInstances) enforced during placement.
    /// </summary>
    public class GrassRenderer : MonoBehaviour
    {
        private const int MaxBatchSize = 1023;
        private const int MaxInstances = 3000;

        // Singleton guard for Bootstrap duplicate protection.
        private static GrassRenderer _activeInstance;

        [Header("Meshes")]
        [SerializeField] private Mesh _grassBladeStraight;
        [SerializeField] private Mesh _grassBladeBentLeft;
        [SerializeField] private Mesh _grassBladeBentRight;

        [Header("Material")]
        [SerializeField] private Material _material;
        private Material _instancedMaterial;

        [Header("Wind Animation")]
        [SerializeField, Range(0.5f, 3f)] private float _windSpeed = 1.2f;
        [SerializeField, Range(0f, 15f)] private float _windAmount = 5f;

        [Header("Performance")]
        [SerializeField] private float _cullDistance = 45f;

        [Header("Biome")]
        [SerializeField] private Color _baseColor = new Color(0.2f, 0.7f, 0.15f);
        [SerializeField, Range(0f, 0.3f)] private float _colorVariation = 0.1f;

        // GLB grass meshes (loaded from Resources, 7 variants expected)
        private List<Mesh> _grassMeshes = new List<Mesh>();

        // Cell-follow system
        [Header("Placement (T4)")]
        [SerializeField] private float _cellSize = 5f;
        [SerializeField] private int _cellRadius = 8;          // grid radius in cells around follow target
        [SerializeField] private int _eastPerCell = 6;         // dense (초원)
        [SerializeField] private int _northPerCell = 2;        // low (설산)
        private Transform _followTarget;

        // Instance data storage
        private struct GrassBladeInstance
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
            public int meshVariant; // index into _grassMeshes
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
        }

        private List<MeshBatch> _batches;

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
        public int LoadedGrassMeshCount => _grassMeshes != null ? _grassMeshes.Count : 0;
        public static GrassRenderer ActiveInstance => _activeInstance;

        // ================================================================
        // T4 Static Bootstrap
        // ================================================================

        /// <summary>
        /// Static bootstrapping entry point. Creates (or returns existing) a GrassRenderer
        /// that follows <paramref name="followTarget"/> (typically the player) and places
        /// grass around it with cell-based relocation. Duplicate-instance guarded.
        /// </summary>
        /// <param name="followTarget">Transform the grass grid follows (player).</param>
        /// <param name="parent">Optional parent transform (e.g. a Systems root). May be null.</param>
        /// <returns>The active GrassRenderer component.</returns>
        public static GrassRenderer Bootstrap(Transform followTarget, Transform parent)
        {
            if (_activeInstance != null)
            {
                // Re-point follow target and ensure it is active.
                _activeInstance.SetFollowTarget(followTarget);
                _activeInstance.gameObject.SetActive(true);
                return _activeInstance;
            }

            // Fallback: find an existing component in the scene if singleton got lost.
            GrassRenderer existing = FindAnyObjectByType<GrassRenderer>();
            if (existing != null && existing != _activeInstance)
            {
                _activeInstance = existing;
                existing.SetFollowTarget(followTarget);
                return existing;
            }

            GameObject go = new GameObject("GrassRenderer");
            go.layer = LayerMask.NameToLayer("Ground");
            if (parent != null)
                go.transform.SetParent(parent, false);

            GrassRenderer renderer = go.AddComponent<GrassRenderer>();
            renderer.SetFollowTarget(followTarget);
            _activeInstance = renderer;
            return renderer;
        }

        /// <summary>Assigns the follow target and rebuilds the surrounding grass cells.</summary>
        public void SetFollowTarget(Transform target)
        {
            _followTarget = target;
            if (_followTarget != null)
                RefreshCells(_followTarget.position);
        }

        // ================================================================
        // Public Methods
        // ================================================================

        /// <summary>
        /// Sets the meshes used for the three grass blade variants (legacy API).
        /// </summary>
        public void SetMeshes(Mesh straight, Mesh bentLeft, Mesh bentRight)
        {
            _grassBladeStraight = straight;
            _grassBladeBentLeft = bentLeft;
            _grassBladeBentRight = bentRight;
            SyncLegacyMeshesIntoVariantPool();
        }

        /// <summary>
        /// Sets the shared material and creates an instanced copy for runtime.
        /// </summary>
        public void SetMaterial(Material mat)
        {
            _material = mat;
            if (_material != null)
            {
                CreateInstancedMaterial();
            }
            else
            {
                _instancedMaterial = null;
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
        /// Loads GLB grass meshes from Resources/Models/UserProvided/terrain/grass.
        /// Falls back to legacy blade meshes / procedural quad if none found.
        /// </summary>
        public void LoadGrassMeshes()
        {
            _grassMeshes = new List<Mesh>();

            GameObject[] grassPrefabs = Resources.LoadAll<GameObject>("Models/UserProvided/terrain/grass");
            foreach (var prefab in grassPrefabs)
            {
                if (prefab == null) continue;
                MeshFilter mf = prefab.GetComponentInChildren<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    _grassMeshes.Add(mf.sharedMesh);

                    // Try to pull a usable material from the GLB prefab.
                    if (_material == null)
                    {
                        Renderer r = prefab.GetComponentInChildren<Renderer>();
                        if (r != null && r.sharedMaterial != null)
                            _material = r.sharedMaterial;
                    }
                }
            }

            // Ensure at least one mesh: fall back to legacy blade meshes, then procedural quad.
            SyncLegacyMeshesIntoVariantPool();
            if (_grassMeshes.Count == 0)
            {
                _grassMeshes.Add(CreateFallbackQuadMesh());
                Debug.LogWarning("[GrassRenderer] No GLB grass found; using procedural quad fallback.");
            }
        }

        /// <summary>
        /// Clears all instances and rebuilds placement from a list of positions (legacy API).
        /// </summary>
        public void PlaceBlades(List<Vector3> positions, int seed = 0)
        {
            if (positions == null || positions.Count == 0)
            {
                _instances = new List<GrassBladeInstance>();
                RebuildBatches();
                return;
            }

            System.Random rng = new System.Random(seed != 0 ? seed : gameObject.GetHashCode());
            _instances = new List<GrassBladeInstance>(positions.Count);

            foreach (Vector3 pos in positions)
            {
                GrassBladeInstance inst = new GrassBladeInstance
                {
                    position = pos,
                    rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360f), 0f),
                    scale = new Vector3(
                        0.8f + (float)(rng.NextDouble() * 0.4f),
                        0.8f + (float)(rng.NextDouble() * 0.4f),
                        1f
                    ),
                    meshVariant = RandomMeshIndex(rng),
                    windOffset = (float)(rng.NextDouble() * Mathf.PI * 2f)
                };
                _instances.Add(inst);
            }

            RebuildBatches();
        }

        // ================================================================
        // Cell-based placement (T4 follow system)
        // ================================================================

        private int RandomMeshIndex(System.Random rng)
        {
            if (_grassMeshes == null || _grassMeshes.Count == 0) return 0;
            return rng.Next(0, _grassMeshes.Count);
        }

        /// <summary>
        /// Rebuilds grass around a world anchor by iterating a square cell grid with
        /// nation/hydrology density rules. Enforces MaxInstances cap.
        /// </summary>
        public void RefreshCells(Vector3 anchor)
        {
            if (_grassMeshes == null || _grassMeshes.Count == 0)
                LoadGrassMeshes();

            System.Random rng = new System.Random(20260821); // fixed deterministic seed (no UnityEngine.Random)

            _instances = new List<GrassBladeInstance>(MaxInstances);

            int radiusCells = Mathf.Max(1, _cellRadius);
            float half = _cellSize * 0.5f;

            for (int cz = -radiusCells; cz <= radiusCells; cz++)
            {
                for (int cx = -radiusCells; cx <= radiusCells; cx++)
                {
                    if (_instances.Count >= MaxInstances) break;

                    float cellCenterX = anchor.x + cx * _cellSize;
                    float cellCenterZ = anchor.z + cz * _cellSize;

                    int budget = GetGrassBudgetForCell(cellCenterX, cellCenterZ);
                    if (budget <= 0) continue;

                    for (int k = 0; k < budget; k++)
                    {
                        if (_instances.Count >= MaxInstances) break;

                        float wx = cellCenterX + ((float)(rng.NextDouble() * 2.0 - 1.0) * half);
                        float wz = cellCenterZ + ((float)(rng.NextDouble() * 2.0 - 1.0) * half);

                        if (!IsGrassAllowed(wx, wz)) continue;

                        // 월드 지표면 y = 1f + GetHeightAt(...) (기저 1f).
                        float height = 1f + TerrainGenerator.GetHeightAt(wx, wz, BiomeType.Plains, 42);

                        GrassBladeInstance inst = new GrassBladeInstance
                        {
                            position = new Vector3(wx, height, wz),
                            rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360f), 0f),
                            scale = new Vector3(
                                0.8f + (float)(rng.NextDouble() * 0.4f),
                                0.8f + (float)(rng.NextDouble() * 0.4f),
                                1f
                            ),
                            meshVariant = RandomMeshIndex(rng),
                            windOffset = (float)(rng.NextDouble() * Mathf.PI * 2f)
                        };
                        _instances.Add(inst);
                    }
                }
            }

            RebuildBatches();
        }

        /// <summary>
        /// Density rule per nation: East=high 초원, North=low 설산,
        /// South(사막)/West(화산)=0, Empire 120m=0. Returns blades per cell.
        /// </summary>
        private int GetGrassBudgetForCell(float x, float z)
        {
            Vector3 pos = new Vector3(x, 0f, z);

            // Empire central 120m excluded.
            if (pos.magnitude < 120f)
                return 0;

            NationType nation = NationTerrainController.GetNationFromPosition(pos);
            switch (nation)
            {
                case NationType.East:
                    return Mathf.Max(1, _eastPerCell);
                case NationType.North:
                    return Mathf.Max(1, _northPerCell);
                case NationType.South:
                case NationType.West:
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Disqualifies positions over/too-close to lake water (호수 waterLevel 위/해안 5m 제외).
        /// </summary>
        private bool IsGrassAllowed(float x, float z)
        {
            float height = 1f + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42);

            var lakes = TerrainGenerator.Lakes;
            if (lakes != null)
            {
                for (int i = 0; i < lakes.Count; i++)
                {
                    var lake = lakes[i];
                    float dx = x - lake.center.x;
                    float dz = z - lake.center.z;
                    float distSq = dx * dx + dz * dz;

                    // 해안 반경 5m 확장 제외 (호수 본체 + shore margin).
                    float exclusionRadius = lake.radius * 1.3f + 5f;
                    if (distSq <= exclusionRadius * exclusionRadius)
                        return false;

                    // 물 표면 아래(수중) 제외.
                    if (height <= lake.waterLevel)
                        return false;
                }
            }
            return true;
        }

        // ================================================================
        // Batch building / rendering
        // ================================================================

        /// <summary>
        /// Rebuilds all GPU instance batches from current instance list.
        /// </summary>
        public void RebuildBatches()
        {
            if (_instances == null || _instances.Count == 0)
            {
                _batches = new List<MeshBatch>();
                return;
            }

            if (_grassMeshes == null || _grassMeshes.Count == 0)
                LoadGrassMeshes();

            _batches = new List<MeshBatch>();

            // Group by mesh variant
            for (int variant = 0; variant < _grassMeshes.Count; variant++)
            {
                Mesh variantMesh = _grassMeshes[variant];
                if (variantMesh == null) continue;

                List<Matrix4x4> variantMatrices = new List<Matrix4x4>(_instances.Count / _grassMeshes.Count + 1);

                foreach (var inst in _instances)
                {
                    if (inst.meshVariant != variant) continue;
                    variantMatrices.Add(Matrix4x4.TRS(inst.position, inst.rotation, inst.scale));
                }

                if (variantMatrices.Count == 0) continue;

                // Split into batches of MaxBatchSize (1023)
                for (int i = 0; i < variantMatrices.Count; i += MaxBatchSize)
                {
                    int count = Mathf.Min(MaxBatchSize, variantMatrices.Count - i);
                    List<Matrix4x4> batchMatrices = variantMatrices.GetRange(i, count);

                    _batches.Add(new MeshBatch
                    {
                        mesh = variantMesh,
                        matrices = batchMatrices
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
            _windZone = FindAnyObjectByType<WindZone>();

            LoadGrassMeshes();
            CreateInstancedMaterial();

            if (_followTarget != null)
                RefreshCells(_followTarget.position);
        }

        private void OnEnable()
        {
            LoadGrassMeshes();
            CreateInstancedMaterial();
        }

        private void Update()
        {
            if (_batches == null || _batches.Count == 0)
                return;

            if (_material == null || _instancedMaterial == null)
                return;

            // Find main camera if not cached
            if (_mainCamera == null)
                _mainCamera = Camera.main;

            // Refresh wind zone reference if lost
            if (_windZone == null)
                _windZone = FindAnyObjectByType<WindZone>();

            // Cell-follow relocation: rebuild when follow target moved past a cell boundary.
            if (_followTarget != null)
            {
                Vector3 anchor = _followTarget.position;
                float snappedX = Mathf.Floor(anchor.x / _cellSize) * _cellSize;
                float snappedZ = Mathf.Floor(anchor.z / _cellSize) * _cellSize;
                _pendingCell = new Vector2(snappedX, snappedZ);

                if (_pendingCell != _lastCell || _lastFollowWasNull)
                {
                    RefreshCells(anchor);
                    _lastCell = _pendingCell;
                    _lastFollowWasNull = false;
                }
            }
            else
            {
                _lastFollowWasNull = true;
            }

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

            // Cull based on camera distance and apply wind animation via matrix rebuild.
            int batchIdx = 0;
            for (int variant = 0; variant < _grassMeshes.Count; variant++)
            {
                Mesh variantMesh = _grassMeshes[variant];
                if (variantMesh == null) continue;

                int idxInVariant = 0;
                foreach (var inst in _instances)
                {
                    if (inst.meshVariant != variant) continue;

                    // Advance to next batch when crossing boundary
                    int localIndex = idxInVariant % MaxBatchSize;
                    if (localIndex == 0 && idxInVariant > 0)
                        batchIdx++;

                    if (batchIdx >= _batches.Count)
                        break;

                    bool culled = _mainCamera != null &&
                        (inst.position - _mainCamera.transform.position).sqrMagnitude > cullSq;

                    if (!culled)
                    {
                        // Wind animation: sine wave rotation
                        float swayTime = Time.time * _windSpeed + inst.windOffset;
                        float swayAngle = Mathf.Sin(swayTime) * _windAmount * windStrength;
                        Quaternion swayRotation = Quaternion.AngleAxis(swayAngle, swayAxis);
                        Quaternion finalRotation = inst.rotation * swayRotation;

                        _batches[batchIdx].matrices[localIndex] =
                            Matrix4x4.TRS(inst.position, finalRotation, inst.scale);
                    }
                    else
                    {
                        // Culled: zero matrix (invisible)
                        _batches[batchIdx].matrices[localIndex] = Matrix4x4.zero;
                    }

                    idxInVariant++;
                }
            }
        }

        private void LateUpdate()
        {
            if (_batches == null || _batches.Count == 0)
                return;

            if (_instancedMaterial == null)
                return;

            // Keep mat instancing always on
            if (!_instancedMaterial.enableInstancing)
                _instancedMaterial.enableInstancing = true;

            // Draw all batches
            foreach (var batch in _batches)
            {
                if (batch.matrices == null || batch.matrices.Count == 0)
                    continue;

                Graphics.DrawMeshInstanced(
                    batch.mesh,
                    0,
                    _instancedMaterial,
                    batch.matrices.ToArray(),
                    batch.matrices.Count,
                    null,
                    UnityEngine.Rendering.ShadowCastingMode.On,
                    true, // receive shadows
                    gameObject.layer,
                    _mainCamera
                );
            }
        }

        private void OnDestroy()
        {
            if (_activeInstance == this)
                _activeInstance = null;
        }

        // ================================================================
        // Helpers
        // ================================================================

        private Vector2 _pendingCell;
        private Vector2 _lastCell = new Vector2(float.MinValue, float.MinValue);
        private bool _lastFollowWasNull = true;

        private void SyncLegacyMeshesIntoVariantPool()
        {
            if (_grassMeshes == null)
                _grassMeshes = new List<Mesh>();
            // Avoid duplicates of the same reference.
            if (_grassBladeStraight != null && !_grassMeshes.Contains(_grassBladeStraight))
                _grassMeshes.Add(_grassBladeStraight);
            if (_grassBladeBentLeft != null && !_grassMeshes.Contains(_grassBladeBentLeft))
                _grassMeshes.Add(_grassBladeBentLeft);
            if (_grassBladeBentRight != null && !_grassMeshes.Contains(_grassBladeBentRight))
                _grassMeshes.Add(_grassBladeBentRight);
        }

        private void CreateInstancedMaterial()
        {
            if (_material == null)
            {
                // Runtime fallback: build a URP/Lit instanced material.
                _material = CreateRuntimeGrassMaterial();
            }
            if (_material == null)
            {
                _instancedMaterial = null;
                return;
            }

            if (_instancedMaterial != null && _instancedMaterial.shader == _material.shader)
                return;

            _instancedMaterial = new Material(_material);
            _instancedMaterial.enableInstancing = true;
            _instancedMaterial.color = _baseColor;

#if UNITY_EDITOR
            _instancedMaterial.hideFlags = HideFlags.HideAndDontSave;
#endif
        }

        private static Material CreateRuntimeGrassMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                return null;

            Material mat = new Material(shader);
            mat.enableInstancing = true;
            mat.name = "Mat_GrassRuntime";
            return mat;
        }

        private Mesh GetMeshForVariant(int variant)
        {
            if (_grassMeshes != null && variant >= 0 && variant < _grassMeshes.Count)
                return _grassMeshes[variant];
            switch (variant)
            {
                case 0: return _grassBladeStraight;
                case 1: return _grassBladeBentLeft;
                case 2: return _grassBladeBentRight;
                default: return _grassBladeStraight;
            }
        }

        /// <summary>
        /// Procedural single-triangle-pair grass blade (1x1 quad) fallback mesh.
        /// </summary>
        private static Mesh CreateFallbackQuadMesh()
        {
            Mesh mesh = new Mesh();
            mesh.name = "FallbackGrassQuad";
            mesh.vertices = new[]
            {
                new Vector3(-0.25f, 0f, 0f),
                new Vector3(0.25f, 0f, 0f),
                new Vector3(-0.25f, 0.9f, 0f),
                new Vector3(0.25f, 0.9f, 0f),
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
            };
            mesh.triangles = new[] { 0, 1, 2, 1, 3, 2 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// 내부 테스트용: 현재 윈도우 존 참조 (없으면 null).
        /// </summary>
        public WindZone CurrentWindZone => _windZone;
    }
}