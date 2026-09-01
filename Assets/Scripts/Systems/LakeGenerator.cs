using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core.Utils;

namespace ProjectName.Systems
{
    /// <summary>
    /// C22-05: Perlin noise-based irregular lake generator.
    /// Creates an organic lake shape (concave depression + water surface)
    /// using Perlin noise threshold sampling. Integrates with WaterBody wave animation style.
    /// </summary>
    public class LakeGenerator : MonoBehaviour
    {
        [Header("Lake Dimensions")]
        [SerializeField] private float _radius = 5f;
        [SerializeField] private float _depth = 0.5f;
        [SerializeField] private float _surfaceY = 0f;

        [Header("Noise Settings")]
        [SerializeField] private float _noiseScale = 3f;
        [SerializeField] private float _noiseThreshold = 0.45f;
        [SerializeField] private int _noiseSeed = 42;

        [Header("Wave Animation")]
        [SerializeField] private float _waveSpeed = 1.2f;
        [SerializeField] private float _waveAmplitude = 0.03f;

        [Header("Visuals")]
        [SerializeField] private Color _waterColor = new Color(0.2f, 0.5f, 0.8f, 0.6f);
        [SerializeField] private Color _lakeBedColor = new Color(0.35f, 0.25f, 0.15f);

        [Header("Slow Collision")]
        [SerializeField] private float _slowFactor = 0.5f;
        [SerializeField] private string _playerTag = "Player";

        private GameObject _waterSurface;
        private GameObject _lakeBed;
        private GameObject _collisionVolume;
        private MeshRenderer _surfaceRenderer;
        private Material _surfaceMaterial;
        private float _baseY;
        private bool _constructed;
        private bool _configured;
        private Vector3 _center;
        private static TerrainGenerator.TerrainLakeDef? _pendingDef = null; // nullable struct (구조체 null 배정 컴파일 에러 방지)

        // Cached entry speeds per Rigidbody to prevent exponential velocity decay (same as WaterBody)
        private readonly Dictionary<Rigidbody, float> _entrySpeeds = new Dictionary<Rigidbody, float>();

        /// <summary>Public accessor for the water surface (for testing).</summary>
        public GameObject WaterSurface => _waterSurface;

        /// <summary>Public accessor for the lake bed (for testing).</summary>
        public GameObject LakeBed => _lakeBed;

        /// <summary>Public accessor for the collision volume (for testing).</summary>
        public GameObject CollisionVolume => _collisionVolume;

        /// <summary>Public accessor for the current surface Material (for testing/editor upgrades).</summary>
        public Material SurfaceMaterial => _surfaceMaterial;

        /// <summary>Radius of the lake.</summary>
        public float Radius => _radius;

        /// <summary>Noise threshold for lake shape.</summary>
        public float NoiseThreshold => _noiseThreshold;

        /// <summary>
        /// TerrainGenerator.Lakes(6개 호수) 정의를 순회하며 각 호수 GameObject를 생성하고
        /// LakeGenerator를 부착 + 파라미터 설정 + ConstructLake를 실행한다.
        /// Awake 자기실행 경로가 올바른 파라미터를 쓰도록 AddComponent 직전에
        /// _pendingDef를 세팅한다. waterLevel y에 물 표면이 놓인다.
        /// Random 언시드 없음 — 결정론적, 고정 시드 사용(호수별 위치 기반 파생).
        /// </summary>
        public static void GenerateAllLakes(Transform parent)
        {
            // 중복 가드: 이미 Lake_* 오브젝트가 있으면 스킵 (에디터 생성 + 런타임 호출 이중 방지)
            if (parent != null && parent.Find("Lake_0") != null)
            {
                Debug.Log("[LakeGenerator] GenerateAllLakes: 기존 Lake_* 존재 — 스킵");
                return;
            }

            var lakes = TerrainGenerator.Lakes;
            if (lakes == null || lakes.Count == 0)
            {
                Debug.LogWarning("[LakeGenerator] TerrainGenerator.Lakes가 비어 있음 — 호수 생성 스킵");
                return;
            }
            for (int i = 0; i < lakes.Count; i++)
            {
                var def = lakes[i];
                var go = new GameObject($"Lake_{i}");
                if (parent != null)
                    go.transform.SetParent(parent, false);
                // ConstructLake가 transform.position.y를 _surfaceY(= waterLevel)로 맞춤
                go.transform.position = new Vector3(def.center.x, def.waterLevel, def.center.z);
                var gen = go.AddComponent<LakeGenerator>();
                // _pendingDef static 경로는 에디터(AddComponent 시 Awake 미호출)에서 유실되므로
                // ConfigureLake로 직접 구성 — 에디터/런타임 양쪽 모두 확실.
                gen.ConfigureLake(def);
            }
            Debug.Log($"[LakeGenerator] GenerateAllLakes: {lakes.Count} lakes 확정");
        }

        /// <summary>
        /// TerrainGenerator.TerrainLakeDef 정의를 이 컴포넌트 파라미터에 적용한다.
        /// 팩토리(GenerateAllLakes)가 AddComponent 직전에 _pendingDef를 세팅하면 Awake가
        /// 이 메서드로 소비해 기본값(반경 5m 등)으로 잡다한 호수를 만들지 않는다.
        /// 호수별 고정 시드 파생(UnityEngine.Random 미사용, 결정론적)으로 모양이 다양하다.
        /// </summary>
        private void ApplyDef(TerrainGenerator.TerrainLakeDef def)
        {
            _center = def.center;
            _radius = def.radius;
            _depth = def.depth;
            _surfaceY = def.waterLevel;
            _configured = true;
            // 호수마다 고정 시드 — 위치 기반 파생 (재실행 시 항상 동일)
            _noiseSeed = 1000 + (int)(def.center.x * 0.41f + def.center.z * 0.73f);
        }

        /// <summary>
        /// TerrainLakeDef 기반 파라미터 설정 후 ConstructLake 실행 (재구성/테스트용).
        /// 이미 구성됐으면 기존 자식(표면/바닥/콜라이더/재질)을 파괴 후 재구성한다.
        /// GenerateAllLakes는 AddComponent 직전 _pendingDef 경로로 (Awake에서) 동일하게
        /// 설정하므로 보통 직접 호출은 테스트/재구성 용도다.
        /// </summary>
        public void ConfigureLake(TerrainGenerator.TerrainLakeDef def)
        {
            if (_constructed)
            {
                DestroyConstructedChildren();
                _constructed = false;
            }
            ApplyDef(def);
            ConstructLake();
        }

        /// <summary>생성된 자식 오브젝트(표면/바닥/콜라이더)와 표면 재질을 파괴한다.</summary>
        private void DestroyConstructedChildren()
        {
            if (_surfaceMaterial != null) { Destroy(_surfaceMaterial); _surfaceMaterial = null; }
            if (_waterSurface != null) { Destroy(_waterSurface); _waterSurface = null; }
            if (_lakeBed != null) { Destroy(_lakeBed); _lakeBed = null; }
            if (_collisionVolume != null) { Destroy(_collisionVolume); _collisionVolume = null; }
        }

        /// <summary>
        /// Upgrades the water surface material with reflection probe keywords,
        /// metallic=0.0, and smoothness=0.8 for high-quality reflections.
        /// Called by Phase G1-03 editor tooling.
        /// </summary>
        public void UpgradeReflectionMaterial()
        {
            if (_surfaceMaterial == null) return;
            _surfaceMaterial.EnableKeyword("_REFLECTION_PROBE_BLENDING");
            _surfaceMaterial.EnableKeyword("_REFLECTION_PROBE_BOX_PROJECTION");
            _surfaceMaterial.SetFloat("_Metallic", 0.0f);
            _surfaceMaterial.SetFloat("_Smoothness", 0.8f);
            _surfaceMaterial.SetFloat("_Surface", 1f);
            _surfaceMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        private void Awake()
        {
            // 팩토리(GenerateAllLakes)가 AddComponent 직전에 _pendingDef를 세팅하면
            // 여기서 소비해 올바른 파라미터로 자기실행 ConstructLake를 수행한다
            // (기본값 반경 5m의 잡다한 호수를 만들지 않음).
            if (_pendingDef != null)
            {
                ApplyDef(_pendingDef.Value);
                _pendingDef = null;
            }
            // 파라미터가 설정된 경우에만 ConstructLake.
            // 미설정 컴포넌트(레거시 CreateWaterSystem이 Ground에 첨부하는 기본값 LakeGenerator)는
            // 접촉 방지 — 잡다한 호수를 만들지 않도록 스킵.
            if (_configured || _radius != 5f || _depth != 0.5f || _surfaceY != 0f)
            {
                ConstructLake();
            }
        }

        private void ConstructLake()
        {
            // Guard: prevent duplicate construction
            if (_constructed) return;
            _constructed = true;

            // --- Step 1: Sample Perlin noise grid to determine lake shape extent ---
            // We create a 16x16 sample grid within the lake radius to determine
            // which cells are "water" (noise below threshold) vs "land" (noise above threshold).
            int gridRes = 16;
            float cellSize = (_radius * 2f) / gridRes;

            // Compute center offset for noise (seeded)
            float noiseOffsetX = _noiseSeed * 0.33f;
            float noiseOffsetZ = _noiseSeed * 0.67f;

            // Pre-compute which grid cells are water
            bool[,] isWater = new bool[gridRes, gridRes];
            int waterCellCount = 0;
            for (int gy = 0; gy < gridRes; gy++)
            {
                for (int gx = 0; gx < gridRes; gx++)
                {
                    float wx = (gx + 0.5f) * cellSize - _radius;
                    float wz = (gy + 0.5f) * cellSize - _radius;
                    float distFromCenter = Mathf.Sqrt(wx * wx + wz * wz);

                    // Only consider positions within the radius (circular mask)
                    if (distFromCenter > _radius)
                    {
                        isWater[gy, gx] = false;
                        continue;
                    }

                    // Sample Perlin noise at this grid position
                    float noiseVal = Mathf.PerlinNoise(
                        wx / _noiseScale + noiseOffsetX,
                        wz / _noiseScale + noiseOffsetZ
                    );

                    // Noise below threshold = water (depression)
                    isWater[gy, gx] = noiseVal < _noiseThreshold;
                    if (isWater[gy, gx])
                        waterCellCount++;
                }
            }

            // If too few water cells, expand threshold slightly
            float totalCells = gridRes * gridRes;
            float waterRatio = (float)waterCellCount / totalCells;
            if (waterRatio < 0.08f && _noiseThreshold < 0.9f)
            {
                // Fallback: at least ensure some water exists
                for (int gy = 0; gy < gridRes; gy++)
                {
                    for (int gx = 0; gx < gridRes; gx++)
                    {
                        if (isWater[gy, gx]) continue; // Already counted

                        float wx = (gx + 0.5f) * cellSize - _radius;
                        float wz = (gy + 0.5f) * cellSize - _radius;
                        float distFromCenter = Mathf.Sqrt(wx * wx + wz * wz);
                        if (distFromCenter > _radius) continue;

                        float noiseVal = Mathf.PerlinNoise(
                            wx / _noiseScale + noiseOffsetX,
                            wz / _noiseScale + noiseOffsetZ
                        );

                        if (noiseVal < _noiseThreshold + 0.15f)
                        {
                            isWater[gy, gx] = true;
                            waterCellCount++;
                        }
                    }
                }
            }

            // --- Step 2: Create lake bed (concave depression) ---
            _lakeBed = new GameObject($"{gameObject.name}_LakeBed");
            _lakeBed.transform.SetParent(transform, false);
            _lakeBed.transform.localPosition = Vector3.zero;

            // Build the bed as a grid of small cubes to approximate organic depression
            float bedCubeSize = cellSize * 0.85f;
            float baseDepth = _depth * 0.8f;
            for (int gy = 0; gy < gridRes; gy++)
            {
                for (int gx = 0; gx < gridRes; gx++)
                {
                    if (!isWater[gy, gx]) continue;

                    float wx = (gx + 0.5f) * cellSize - _radius;
                    float wz = (gy + 0.5f) * cellSize - _radius;

                    // Vary depth based on noise (center deeper, edges shallower)
                    float distFactor = 1f - Mathf.Clamp01(Mathf.Sqrt(wx * wx + wz * wz) / _radius);
                    float noiseSample = Mathf.PerlinNoise(
                        wx * 0.5f + noiseOffsetX + 10f,
                        wz * 0.5f + noiseOffsetZ + 10f
                    );
                    float depthFactor = 0.3f + distFactor * 0.5f + noiseSample * 0.2f;
                    float cubeHeight = baseDepth * depthFactor;

                    var bedCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    bedCube.name = $"LakeBed_Cube_{gy}_{gx}";
                    bedCube.transform.SetParent(_lakeBed.transform);

                    // Use 0 for local Y — parent transform handles world Y positioning
                    bedCube.transform.localPosition = new Vector3(wx, -cubeHeight * 0.5f, wz);
                    bedCube.transform.localScale = new Vector3(bedCubeSize, Mathf.Max(0.05f, cubeHeight), bedCubeSize);

                    // Remove unnecessary collider — bed cubes are visual only
                    var cubeCollider = bedCube.GetComponent<BoxCollider>();
                    if (cubeCollider != null) Destroy(cubeCollider);

                    var renderer = bedCube.GetComponent<MeshRenderer>();
                    renderer.material = MaterialHelper.CreateLitMaterial(
                        Color.Lerp(_lakeBedColor, Color.black, depthFactor * 0.3f),
                        $"LakeBedMat_{gy}_{gx}"
                    );
                }
            }

            // --- Step 3: Create water surface (transparent plane, similar to WaterBody) ---
            _waterSurface = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _waterSurface.name = $"{gameObject.name}_LakeSurface";
            _waterSurface.transform.SetParent(transform, false);
            _waterSurface.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            _waterSurface.transform.localPosition = Vector3.zero;

            float scale = _radius * 2f / 10f;
            _waterSurface.transform.localScale = new Vector3(scale, scale, scale);

            // Remove default collider from visual plane
            var meshCollider = _waterSurface.GetComponent<MeshCollider>();
            if (meshCollider != null) Destroy(meshCollider);

            // URP Lit transparent material
            _surfaceRenderer = _waterSurface.GetComponent<MeshRenderer>();
            _surfaceRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _surfaceRenderer.receiveShadows = false;

            _surfaceMaterial = MaterialHelper.CreateLitMaterial(_waterColor, $"{gameObject.name}_LakeMat");
            if (_surfaceMaterial != null)
            {
                _surfaceMaterial.SetFloat("_Surface", 1f);
                _surfaceMaterial.SetFloat("_BlendMode", 0f);
                _surfaceMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _surfaceMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _surfaceMaterial.SetFloat("_ZWrite", 0f);
                _surfaceMaterial.SetFloat("_AlphaClip", 0f);
                _surfaceMaterial.renderQueue = 3000;
                _surfaceMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                _surfaceMaterial.EnableKeyword("_BLENDMODE_ALPHA");

                Color c = _surfaceMaterial.color;
                c.a = _waterColor.a;
                _surfaceMaterial.color = c;

                _surfaceRenderer.material = _surfaceMaterial;
            }

            // --- Step 4: Create collision volume ---
            _collisionVolume = new GameObject($"{gameObject.name}_LakeVolume");
            _collisionVolume.transform.SetParent(transform, false);
            _collisionVolume.transform.localPosition = Vector3.zero;

            BoxCollider collider = _collisionVolume.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            float volumeSize = _radius * 2f;
            float volumeHeight = Mathf.Max(1f, _depth);
            collider.size = new Vector3(volumeSize, volumeHeight, volumeSize);
            collider.center = new Vector3(0f, -volumeHeight * 0.5f, 0f);

            try { _collisionVolume.tag = "Water"; }
            catch (UnityException) { Debug.LogWarning("[LakeGenerator] 'Water' 태그 미정의 — Untagged 유지 (TagManager에 Water 태그 추가 권장)"); }
            // --- Step 5: Position parent at the desired surface Y ---
            _baseY = _surfaceY;
            transform.position = new Vector3(transform.position.x, _baseY, transform.position.z);
        }

        private void Update()
        {
            if (_waterSurface == null) return;

            // Sine wave animation (same as WaterBody)
            float waveOffset = Mathf.Sin(Time.time * _waveSpeed) * _waveAmplitude;
            Vector3 pos = _waterSurface.transform.localPosition;
            pos.y = waveOffset;
            _waterSurface.transform.localPosition = pos;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(_playerTag)) return;

            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb != null && !_entrySpeeds.ContainsKey(rb))
            {
                // Cache entry speed so we can clamp instead of exponential decay
                _entrySpeeds[rb] = rb.linearVelocity.magnitude;
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (!other.CompareTag(_playerTag)) return;

            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb != null && _entrySpeeds.TryGetValue(rb, out float entrySpeed))
            {
                // Clamp velocity magnitude to _slowFactor * entry speed
                // (not per-frame multiplication which decays exponentially to zero)
                Vector3 velocity = rb.linearVelocity;
                float maxSpeed = entrySpeed * _slowFactor;
                if (velocity.magnitude > maxSpeed)
                {
                    rb.linearVelocity = velocity.normalized * maxSpeed;
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(_playerTag)) return;

            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb != null)
            {
                _entrySpeeds.Remove(rb);
            }
        }

        private void OnDestroy()
        {
            if (_surfaceMaterial != null)
            {
                Destroy(_surfaceMaterial);
                _surfaceMaterial = null;
            }

            _entrySpeeds.Clear();

            // Destroy created child objects explicitly (belt-and-suspenders cleanup)
            if (_waterSurface != null)
            {
                Destroy(_waterSurface);
                _waterSurface = null;
            }
            if (_lakeBed != null)
            {
                Destroy(_lakeBed);
                _lakeBed = null;
            }
            if (_collisionVolume != null)
            {
                Destroy(_collisionVolume);
                _collisionVolume = null;
            }
        }
    }
}