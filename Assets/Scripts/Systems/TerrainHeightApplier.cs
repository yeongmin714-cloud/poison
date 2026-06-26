using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase G1-01: Runtime terrain heightmap controller.
    /// Generates a Perlin noise-based heightmap mesh at runtime and provides
    /// fast height lookup at any world position via a cached 2D height array.
    /// </summary>
    public class TerrainHeightApplier : MonoBehaviour
    {
        [Header("Terrain Settings")]
        [SerializeField] private int _resolution = 50;
        [SerializeField] private float _size = 1000f;
        [SerializeField] private float _maxHeight = 40f;
        [SerializeField] private float _noiseFrequency = 0.15f;
        [SerializeField] private int _seed = 1337;

        [Header("Biome Settings")]
        [SerializeField] private BiomeType _biomeType = BiomeType.Mountain;

        // Cached height data for fast lookup
        private float[,] _heightMap;
        private Mesh _terrainMesh;
        private bool _isInitialized;

        // Public read-only accessors
        public int Resolution => _resolution;
        public float Size => _size;
        public float MaxHeight => _maxHeight;
        public float NoiseFrequency => _noiseFrequency;
        public int Seed => _seed;
        public bool IsInitialized => _isInitialized;
        public Mesh TerrainMesh => _terrainMesh;
        public float[,] HeightMap => _heightMap;

        // ================================================================
        //  Unity Lifecycle
        // ================================================================

        private void Awake()
        {
            GenerateAndApplyTerrain();
        }

        // ================================================================
        //  Public API
        // ================================================================

        /// <summary>
        /// Generates the heightmap mesh and applies it to this GameObject.
        /// Also builds a cached 2D height array for fast height lookups.
        /// </summary>
        public void GenerateAndApplyTerrain()
        {
            // Create custom BiomeDefinition for heightmap
            BiomeDefinition heightmapDef = new BiomeDefinition
            {
                type = _biomeType,
                displayName = "Heightmap",
                surfaceColor = new Color(0.4f, 0.6f, 0.3f),
                noiseAmplitude = _maxHeight,
                noiseFrequency = _noiseFrequency,
                waterThreshold = 0f,
                waterColor = Color.clear,
                moveSpeedModifier = 1.0f
            };

            // Generate mesh via TerrainGenerator
            var (terrainMesh, _) = TerrainGenerator.GenerateTerrainWithDefinition(
                heightmapDef, _seed, _resolution, _size);

            if (terrainMesh == null)
            {
                Debug.LogError("[TerrainHeightApplier] Failed to generate terrain mesh.");
                return;
            }

            _terrainMesh = terrainMesh;
            _terrainMesh.name = $"Terrain_Heightmap_{_resolution}x{_resolution}";

            // Apply mesh to this GameObject
            MeshFilter mf = GetComponent<MeshFilter>();
            if (mf == null)
                mf = gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = _terrainMesh;

            // Update collider
            MeshCollider mc = GetComponent<MeshCollider>();
            if (mc == null)
                mc = gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = _terrainMesh;
            mc.convex = false;

            // Build cached height map
            BuildHeightMap();

            _isInitialized = true;
            Debug.Log($"[TerrainHeightApplier] Terrain generated: {_resolution}x{_resolution}, {_size}m, max height {_maxHeight}m.");
        }

        /// <summary>
        /// Regenerates the terrain (can be called at runtime to refresh).
        /// </summary>
        public void RebuildTerrain()
        {
            GenerateAndApplyTerrain();
        }

        /// <summary>
        /// Returns the terrain height at the given world position by sampling
        /// the cached height map. Uses bilinear interpolation for smooth results.
        /// Returns 0 if not initialized or position is outside terrain bounds.
        /// </summary>
        /// <param name="worldPos">World position to query</param>
        /// <returns>Height (Y value) at the given world position</returns>
        public float GetHeightAtPosition(Vector3 worldPos)
        {
            if (!_isInitialized || _heightMap == null)
                return 0f;

            float halfSize = _size * 0.5f;

            // Convert world position to UV coordinates (0-1)
            float u = (worldPos.x + halfSize) / _size;
            float v = (worldPos.z + halfSize) / _size;

            // Clamp to terrain bounds
            u = Mathf.Clamp01(u);
            v = Mathf.Clamp01(v);

            // Convert UV to heightmap indices
            float fx = u * (_resolution - 1);
            float fz = v * (_resolution - 1);

            int x0 = Mathf.FloorToInt(fx);
            int x1 = Mathf.Min(x0 + 1, _resolution - 1);
            int z0 = Mathf.FloorToInt(fz);
            int z1 = Mathf.Min(z0 + 1, _resolution - 1);

            // Bilinear interpolation weights
            float tx = fx - x0;
            float tz = fz - z0;

            // Sample four corners
            float h00 = _heightMap[z0, x0];
            float h10 = _heightMap[z0, x1];
            float h01 = _heightMap[z1, x0];
            float h11 = _heightMap[z1, x1];

            // Interpolate along X, then Z
            float h0 = Mathf.Lerp(h00, h10, tx);
            float h1 = Mathf.Lerp(h01, h11, tx);
            float height = Mathf.Lerp(h0, h1, tz);

            return height;
        }

        /// <summary>
        /// Returns the height at a position using only X and Z components.
        /// </summary>
        public float GetHeightAt(float worldX, float worldZ)
        {
            return GetHeightAtPosition(new Vector3(worldX, 0f, worldZ));
        }

        // ================================================================
        //  Internal
        // ================================================================

        /// <summary>
        /// Builds the cached 2D height array from the generated mesh vertices.
        /// </summary>
        private void BuildHeightMap()
        {
            if (_terrainMesh == null) return;

            _heightMap = new float[_resolution, _resolution];
            Vector3[] vertices = _terrainMesh.vertices;

            for (int z = 0; z < _resolution; z++)
            {
                for (int x = 0; x < _resolution; x++)
                {
                    int index = z * _resolution + x;
                    if (index < vertices.Length)
                    {
                        _heightMap[z, x] = vertices[index].y;
                    }
                }
            }
        }
    }
}