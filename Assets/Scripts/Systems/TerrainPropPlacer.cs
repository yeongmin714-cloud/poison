using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// GLB 3D 모델을 지형에 랜덤 배치하는 시스템.
    /// Resources/Models/UserProvided/terrain/ 에서 나무(trees/), 바위(rocks/), 풀(grass/) GLB를 로드하여
    /// 전 국가 영역에 분산 배치한다.
    /// GLB 로드 실패 시 Primitive 폴백을 사용한다.
    /// </summary>
    public class TerrainPropPlacer : MonoBehaviour
    {
        [Header("Resources")]
        [SerializeField] private string _resourcesPath = "Models/UserProvided/terrain/";

        [Header("Placement Settings")]
        [SerializeField] private int _seed = 100;
        [SerializeField] private float _spawnExclusionRadius = 30f;
        [SerializeField] private float _terrainSize = 1000f;

        [Header("Tree Settings")]
        [SerializeField] private int _treeMin = 50;
        [SerializeField] private int _treeMax = 80;
        [SerializeField] private float _treeScaleMin = 0.8f;
        [SerializeField] private float _treeScaleMax = 1.5f;

        [Header("Rock Settings")]
        [SerializeField] private int _rockMin = 60;
        [SerializeField] private int _rockMax = 100;
        [SerializeField] private float _rockScaleMin = 0.5f;
        [SerializeField] private float _rockScaleMax = 2.0f;

        [Header("Grass Settings")]
        [SerializeField] private int _grassMin = 100;
        [SerializeField] private int _grassMax = 200;
        [SerializeField] private float _grassScaleMin = 0.3f;
        [SerializeField] private float _grassScaleMax = 0.8f;

        [Header("Runtime")]
        [SerializeField] private Transform _propsParent;

        // Cached prefabs
        private List<GameObject> _treePrefabs;
        private List<GameObject> _rockPrefabs;
        private List<GameObject> _grassPrefabs;

        // Primitive fallbacks
        private GameObject _treeFallback;
        private GameObject _rockFallback;
        private GameObject _grassFallback;

        // Placed instances
        private List<GameObject> _placedProps;

        /// <summary>All placed prop instances (readonly for tests).</summary>
        public IReadOnlyList<GameObject> PlacedProps => _placedProps;

        /// <summary>Tree prefabs loaded (readonly for tests).</summary>
        public IReadOnlyList<GameObject> TreePrefabs => _treePrefabs;

        /// <summary>Rock prefabs loaded (readonly for tests).</summary>
        public IReadOnlyList<GameObject> RockPrefabs => _rockPrefabs;

        /// <summary>Grass prefabs loaded (readonly for tests).</summary>
        public IReadOnlyList<GameObject> GrassPrefabs => _grassPrefabs;

        /// <summary>Tree fallback object (readonly for tests).</summary>
        public GameObject TreeFallback => _treeFallback;

        /// <summary>Rock fallback object (readonly for tests).</summary>
        public GameObject RockFallback => _rockFallback;

        /// <summary>Grass fallback object (readonly for tests).</summary>
        public GameObject GrassFallback => _grassFallback;

        // ================================================================
        //  Unity Lifecycle
        // ================================================================

        private void Awake()
        {
            LoadGLBs();
            CreateFallbacks();
            PlaceProps();
        }

        // ================================================================
        //  GLB Loading
        // ================================================================

        /// <summary>
        /// Loads all GLB prefabs from the resources subdirectories.
        /// </summary>
        public void LoadGLBs()
        {
            // Load trees
            _treePrefabs = LoadPrefabsFromFolder(_resourcesPath + "trees/", "tree");
            Debug.Log($"[TerrainPropPlacer] Loaded {_treePrefabs.Count} tree prefabs.");

            // Load rocks
            _rockPrefabs = LoadPrefabsFromFolder(_resourcesPath + "rocks/", "rock");
            Debug.Log($"[TerrainPropPlacer] Loaded {_rockPrefabs.Count} rock prefabs.");

            // Load grass
            _grassPrefabs = LoadPrefabsFromFolder(_resourcesPath + "grass/", "grass");
            Debug.Log($"[TerrainPropPlacer] Loaded {_grassPrefabs.Count} grass prefabs.");
        }

        private List<GameObject> LoadPrefabsFromFolder(string folderPath, string debugPrefix)
        {
            List<GameObject> prefabs = new List<GameObject>();
            GameObject[] allPrefabs = Resources.LoadAll<GameObject>(folderPath);
            if (allPrefabs != null)
            {
                prefabs.AddRange(allPrefabs.Where(p => p != null));
            }
            return prefabs;
        }

        // ================================================================
        //  Primitive Fallbacks
        // ================================================================

        /// <summary>
        /// Creates primitive fallback objects for when GLB loading fails.
        /// Tree = green cylinder, Rock = gray box, Grass = green sphere.
        /// </summary>
        public void CreateFallbacks()
        {
            // Tree fallback: green cylinder
            _treeFallback = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _treeFallback.name = "Tree_Fallback";
            _treeFallback.GetComponent<Renderer>().material.color = new Color(0.2f, 0.6f, 0.1f);
            _treeFallback.transform.localScale = new Vector3(0.5f, 1f, 0.5f);
            Object.DestroyImmediate(_treeFallback.GetComponent<CapsuleCollider>());
            _treeFallback.SetActive(false);

            // Rock fallback: gray box
            _rockFallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _rockFallback.name = "Rock_Fallback";
            _rockFallback.GetComponent<Renderer>().material.color = new Color(0.4f, 0.4f, 0.4f);
            _rockFallback.transform.localScale = new Vector3(0.8f, 0.6f, 0.8f);
            Object.DestroyImmediate(_rockFallback.GetComponent<BoxCollider>());
            _rockFallback.SetActive(false);

            // Grass fallback: green sphere
            _grassFallback = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _grassFallback.name = "Grass_Fallback";
            _grassFallback.GetComponent<Renderer>().material.color = new Color(0.2f, 0.8f, 0.2f);
            _grassFallback.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            Object.DestroyImmediate(_grassFallback.GetComponent<SphereCollider>());
            _grassFallback.SetActive(false);
        }

        // ================================================================
        //  Prop Placement
        // ================================================================

        /// <summary>
        /// Places props across all nation territories with the configured
        /// random distribution. Excludes center area (player spawn).
        /// </summary>
        public void PlaceProps()
        {
            // Ensure parent object
            if (_propsParent == null)
            {
                GameObject parentGO = new GameObject("TerrainProps");
                parentGO.transform.SetParent(transform);
                _propsParent = parentGO.transform;
            }

            // Clear previous props if any
            ClearProps();

            _placedProps = new List<GameObject>();
            System.Random rng = new System.Random(_seed);

            // Place trees
            int treeCount = rng.Next(_treeMin, _treeMax + 1);
            for (int i = 0; i < treeCount; i++)
            {
                Vector3 position = GetRandomPosition(rng);
                float scale = RandomRange(rng, _treeScaleMin, _treeScaleMax);
                float rotationY = RandomRange(rng, 0f, 360f);

                if (_treePrefabs.Count > 0)
                {
                    GameObject prefab = _treePrefabs[rng.Next(_treePrefabs.Count)];
                    PlaceProp(prefab, position, scale, rotationY);
                }
                else
                {
                    PlaceFallback(_treeFallback, position, scale, rotationY);
                }
            }

            // Place rocks
            int rockCount = rng.Next(_rockMin, _rockMax + 1);
            for (int i = 0; i < rockCount; i++)
            {
                Vector3 position = GetRandomPosition(rng);
                float scale = RandomRange(rng, _rockScaleMin, _rockScaleMax);
                float rotationY = RandomRange(rng, 0f, 360f);

                if (_rockPrefabs.Count > 0)
                {
                    GameObject prefab = _rockPrefabs[rng.Next(_rockPrefabs.Count)];
                    PlaceProp(prefab, position, scale, rotationY);
                }
                else
                {
                    PlaceFallback(_rockFallback, position, scale, rotationY);
                }
            }

            // Place grass
            int grassCount = rng.Next(_grassMin, _grassMax + 1);
            for (int i = 0; i < grassCount; i++)
            {
                Vector3 position = GetRandomPosition(rng);
                float scale = RandomRange(rng, _grassScaleMin, _grassScaleMax);
                float rotationY = RandomRange(rng, 0f, 360f);

                if (_grassPrefabs.Count > 0)
                {
                    GameObject prefab = _grassPrefabs[rng.Next(_grassPrefabs.Count)];
                    PlaceProp(prefab, position, scale, rotationY);
                }
                else
                {
                    PlaceFallback(_grassFallback, position, scale, rotationY);
                }
            }

            Debug.Log($"[TerrainPropPlacer] Placed {_placedProps.Count} props total " +
                      $"(trees: {treeCount}, rocks: {rockCount}, grass: {grassCount}).");
        }

        private void PlaceProp(GameObject prefab, Vector3 position, float scale, float rotationY)
        {
            GameObject instance = Instantiate(prefab, position, Quaternion.identity, _propsParent);
            instance.transform.localScale = Vector3.one * scale;
            instance.transform.rotation = Quaternion.Euler(0f, rotationY, 0f);
            instance.SetActive(true);
            _placedProps.Add(instance);
        }

        private void PlaceFallback(GameObject fallback, Vector3 position, float scale, float rotationY)
        {
            if (fallback == null)
            {
                Debug.LogError("[TerrainPropPlacer] Fallback object is null! Skipping placement.");
                return;
            }
            PlaceProp(fallback, position, scale, rotationY);
        }

        // ================================================================
        //  Position Generation
        // ================================================================

        /// <summary>
        /// Generates a random world position on the terrain, excluding the
        /// center spawn area (within spawnExclusionRadius).
        /// Ensures distribution across all nation territories.
        /// </summary>
        private Vector3 GetRandomPosition(System.Random rng)
        {
            float halfSize = _terrainSize / 2f;
            float exclusionRadiusSqr = _spawnExclusionRadius * _spawnExclusionRadius;

            for (int attempt = 0; attempt < 100; attempt++)
            {
                float x = RandomRange(rng, -halfSize, halfSize);
                float z = RandomRange(rng, -halfSize, halfSize);

                // Exclude center spawn area (comparison against sqrMagnitude)
                if (x * x + z * z < exclusionRadiusSqr)
                    continue;

                return new Vector3(x, 0f, z);
            }

            // Absolute last resort: place at edge of exclusion zone
            Debug.LogWarning("[TerrainPropPlacer] Could not find valid spawn position after 100 attempts. " +
                             "Check that terrainSize > spawnExclusionRadius.");
            float edgeDist = _spawnExclusionRadius + 1f;
            return new Vector3(edgeDist, 0f, 0f);
        }

        // ================================================================
        //  Utility
        // ================================================================

        private static float RandomRange(System.Random rng, float min, float max)
        {
            return (float)(rng.NextDouble() * (max - min) + min);
        }

        /// <summary>
        /// Clears all placed props from the scene.
        /// </summary>
        public void ClearProps()
        {
            if (_placedProps == null)
            {
                _placedProps = new List<GameObject>();
                return;
            }

            for (int i = _placedProps.Count - 1; i >= 0; i--)
            {
                if (_placedProps[i] != null)
                {
                    if (Application.isPlaying)
                        Destroy(_placedProps[i]);
                    else
                        DestroyImmediate(_placedProps[i]);
                }
            }
            _placedProps.Clear();

            // Also clear any children of the parent
            if (_propsParent != null)
            {
                for (int i = _propsParent.childCount - 1; i >= 0; i--)
                {
                    if (Application.isPlaying)
                        Destroy(_propsParent.GetChild(i).gameObject);
                    else
                        DestroyImmediate(_propsParent.GetChild(i).gameObject);
                }
            }
        }

        /// <summary>Total number of placed props.</summary>
        public int PropCount => _placedProps?.Count ?? 0;

        /// <summary>
        /// Sets the parent transform for all placed props.
        /// Call before PlaceProps() to override the default parent.
        /// </summary>
        public void SetPropsParent(Transform parent)
        {
            _propsParent = parent;
        }
    }
}