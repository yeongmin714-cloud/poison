using ProjectName.Core;
using UnityEngine;

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
            ConstructLake();
        }

        private void ConstructLake()
        {
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
                    bedCube.transform.localPosition = new Vector3(wx, _surfaceY - cubeHeight * 0.5f, wz);
                    bedCube.transform.localScale = new Vector3(bedCubeSize, Mathf.Max(0.05f, cubeHeight), bedCubeSize);

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
            DestroyImmediate(_waterSurface.GetComponent<MeshCollider>());

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
            collider.size = new Vector3(volumeSize, Mathf.Max(1f, _depth), volumeSize);

            _collisionVolume.tag = "Water";

            // --- Step 5: Store base Y for wave animation ---
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

        private void OnTriggerStay(Collider other)
        {
            if (other.CompareTag(_playerTag))
            {
                Rigidbody rb = other.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity *= _slowFactor;
                }
            }
        }

        private void OnDestroy()
        {
            if (_surfaceMaterial != null)
            {
                Destroy(_surfaceMaterial);
                _surfaceMaterial = null;
            }
        }
    }
}