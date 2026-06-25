#if false
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace ProjectName.Editor
{
    /// <summary>
    /// Phase G1-06: LOD System for 245 3D models.
    /// Scans all GameObjects in MainScene for 3D models by name prefix,
    /// adds LODGroup components with 3 LOD levels (plus culled).
    /// Provides simplified meshes for lower LOD levels via vertex decimation.
    ///
    /// Poly Haven model name prefixes (245 total):
    ///   fir_tree_01_1k           (25 trees)
    ///   jacaranda_tree_1k        (25 trees)
    ///   tree_small_02_1k         (25 trees)
    ///   boulder_01_1k            (30 boulders)
    ///   namaqualand_boulder_02_1k (30 boulders)
    ///   namaqualand_cliff_02_1k  (30 cliffs)
    ///   periwinkle_plant_1k      (40 plants)
    ///   searsia_lucida_1k        (40 shrubs)
    /// </summary>
    public static class PhaseG1_LODGenerator
    {
        private const string LODHolderName = "_LOD_Holder";

        /// <summary>
        /// All Poly Haven model name prefixes used to identify models in the scene.
        /// </summary>
        public static readonly string[] ModelPrefixes =
        {
            "fir_tree_01_1k",
            "jacaranda_tree_1k",
            "tree_small_02_1k",
            "boulder_01_1k",
            "namaqualand_boulder_02_1k",
            "namaqualand_cliff_02_1k",
            "periwinkle_plant_1k",
            "searsia_lucida_1k"
        };

        // LOD screen-relative transition heights (distance-based equivalents)
        // LOD 0: 0~30m  -> 0.3
        // LOD 1: 30~80m -> 0.1
        // LOD 2: 80~200m -> 0.02
        // Culled: 200m+
        private const float LOD0_Transition = 0.3f;
        private const float LOD1_Transition = 0.1f;
        private const float LOD2_Transition = 0.02f;

        /// <summary>
        /// Cached simplified meshes keyed by original mesh instance ID,
        /// so we don't re-generate the same simplified mesh multiple times.
        /// </summary>
        private static readonly Dictionary<int, Mesh> _simplifiedMeshCache = new Dictionary<int, Mesh>();

        // ================================================================
        //  Apply LOD Groups
        // ================================================================

        /// <summary>
        /// Scans MainScene for all 3D models and adds LODGroup components.
        /// Menu: Tools/Phase G1/Apply LOD Groups
        /// </summary>
        [MenuItem("Tools/Phase G1/Apply LOD Groups")]
        public static void ApplyLODGroups()
        {
            var scene = GetOrOpenMainScene();
            if (scene == null)
            {
                Debug.LogError("[PhaseG1_LODGenerator] MainScene not found.");
                EditorUtility.DisplayDialog("Phase G1-06", "MainScene not found.", "OK");
                return;
            }

            var sceneObjects = FindAllSceneModels();
                        if (sceneObjects.Count == 0)
                        {
                            Debug.LogWarning("[PhaseG1_LODGenerator] No 3D models found in scene.");
                            EditorUtility.DisplayDialog("Phase G1-06", "No 3D models found in scene.", "OK");
                        }

                        foreach (var go in sceneObjects)
            // Clear cache from previous runs
            _simplifiedMeshCache.Clear();

            int applied = 0;
            int skipped = 0;
            var errors = new List<string>();

            foreach (var go in polyHavenObjects)
            {
                try
                {
                    if (ApplyLODGroupToObject(go))
                        applied++;
                    else
                        skipped++;
                }
                catch (System.Exception ex)
                {
                    errors.Add($"{go.name}: {ex.Message}");
                    skipped++;
                }
            }

            // Save scene
            EditorSceneManager.SaveScene(scene, scene.path);

            string summary = $"LOD Groups applied: {applied} models\n" +
                             $"Skipped: {skipped} models\n" +
                             $"Total Poly Haven objects found: {polyHavenObjects.Count}";

            if (errors.Count > 0)
            {
                summary += "\n\nErrors:\n" + string.Join("\n", errors);
            }

            Debug.Log($"[PhaseG1_LODGenerator] ✅ {summary}");
            EditorUtility.DisplayDialog("Phase G1-06", summary, "OK");
        }

        [MenuItem("Tools/Phase G1/Apply LOD Groups", true)]
        private static bool ValidateApplyLODGroups() => true;

        // ================================================================
        //  Clear LOD Groups
        // ================================================================

        /// <summary>
        /// Removes all LODGroup components from Poly Haven models in the scene.
        /// Menu: Tools/Phase G1/Clear LOD Groups
        /// </summary>
        [MenuItem("Tools/Phase G1/Clear LOD Groups")]
        public static void ClearLODGroups()
        {
            var scene = GetOrOpenMainScene();
            if (scene == null)
            {
                Debug.LogError("[PhaseG1_LODGenerator] MainScene not found.");
                EditorUtility.DisplayDialog("Phase G1-06", "MainScene not found.", "OK");
                return;
            }

            var polyHavenObjects = FindAllPolyHavenObjects();
            int removed = 0;

            foreach (var go in polyHavenObjects)
            {
                var lodGroup = go.GetComponent<LODGroup>();
                if (lodGroup != null)
                {
                    UnityEngine.Object.DestroyImmediate(lodGroup);
                    removed++;
                }
            }

            // Also clean up LOD holder objects if empty
            CleanupLODHolders();

            EditorSceneManager.SaveScene(scene, scene.path);

            string msg = $"LOD Groups removed: {removed}";
            Debug.Log($"[PhaseG1_LODGenerator] ✅ {msg}");
            EditorUtility.DisplayDialog("Phase G1-06", msg, "OK");
        }

        [MenuItem("Tools/Phase G1/Clear LOD Groups", true)]
        private static bool ValidateClearLODGroups() => true;

        // ================================================================
        //  Core LOD Application Logic
        // ================================================================

        /// <summary>
        /// Applies an LODGroup to a single Poly Haven model GameObject.
        /// Returns true if successful, false if skipped.
        /// </summary>
        private static bool ApplyLODGroupToObject(GameObject go)
        {
            // Get the MeshFilter and MeshRenderer
            var meshFilter = go.GetComponent<MeshFilter>();
            var meshRenderer = go.GetComponent<MeshRenderer>();

            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                Debug.LogWarning($"[PhaseG1_LODGenerator] '{go.name}' has no MeshFilter or mesh, skipping.");
                return false;
            }

            if (meshRenderer == null)
            {
                Debug.LogWarning($"[PhaseG1_LODGenerator] '{go.name}' has no MeshRenderer, skipping.");
                return false;
            }

            // Remove existing LODGroup if present
            var existingLOD = go.GetComponent<LODGroup>();
            if (existingLOD != null)
            {
                UnityEngine.Object.DestroyImmediate(existingLOD);
            }

            // Add LODGroup component
            var lodGroup = go.AddComponent<LODGroup>();

            // Get original mesh and renderer
            Mesh originalMesh = meshFilter.sharedMesh;

            // LOD 0: Original mesh, full detail (0~30m)
            var lod0 = new LOD(LOD0_Transition, new Renderer[] { meshRenderer });

            // LOD 1: Same renderer but with lower screen-relative height (30~80m)
            // We use the same renderer for LOD 1; Unity handles culling
            var lod1 = new LOD(LOD1_Transition, new Renderer[] { meshRenderer });

            // LOD 2: Simplified mesh (80~200m)
            // Create a simplified version by taking 1/4 of vertices
            Mesh simplifiedMesh = GetOrCreateSimplifiedMesh(originalMesh, go.name);
            Renderer lod2Renderer = null;

            if (simplifiedMesh != null && simplifiedMesh != originalMesh)
            {
                // We need a separate renderer for LOD 2 with the simplified mesh
                // Create a child GameObject to hold the simplified mesh renderer
                GameObject lod2Go = CreateLODChild(go, "_LOD2");
                var lod2Filter = lod2Go.GetComponent<MeshFilter>();
                if (lod2Filter == null)
                    lod2Filter = lod2Go.AddComponent<MeshFilter>();
                lod2Filter.sharedMesh = simplifiedMesh;

                var lod2RendererComp = lod2Go.GetComponent<MeshRenderer>();
                if (lod2RendererComp == null)
                    lod2RendererComp = lod2Go.AddComponent<MeshRenderer>();

                // Copy material from original
                lod2RendererComp.sharedMaterials = meshRenderer.sharedMaterials;

                lod2Renderer = lod2RendererComp;
            }
            else
            {
                // Fallback: use the original renderer
                lod2Renderer = meshRenderer;
            }

            var lod2 = new LOD(LOD2_Transition, new Renderer[] { lod2Renderer });

            // Set LODs
            lodGroup.SetLODs(new LOD[] { lod0, lod1, lod2 });

            // Recalculate bounds based on renderer bounds
            lodGroup.RecalculateBounds();

            return true;
        }

        // ================================================================
        //  Mesh Simplification (Vertex Decimation)
        // ================================================================

        /// <summary>
        /// Creates a simplified mesh by taking approximately 1/4 of the vertices
        /// (skipping 3 out of every 4). Results are cached by original mesh instance ID.
        /// </summary>
        public static Mesh GetOrCreateSimplifiedMesh(Mesh original, string objectName = "")
        {
            if (original == null)
                return null;

            int instanceId = original.GetInstanceID();

            // Return cached version if available
            if (_simplifiedMeshCache.TryGetValue(instanceId, out Mesh cached))
                return cached;

            // Create simplified mesh
            Mesh simplified = CreateSimplifiedMesh(original, objectName);

            // Cache it
            _simplifiedMeshCache[instanceId] = simplified;
            return simplified;
        }

        /// <summary>
        /// Creates a simplified copy of the given mesh by keeping
        /// approximately 1/4 of the vertices and remapping indices.
        /// </summary>
        private static Mesh CreateSimplifiedMesh(Mesh original, string objectName)
        {
            if (original == null || original.vertexCount < 4)
                return original;

            Vector3[] origVerts = original.vertices;
            Vector2[] origUv = original.uv;
            Vector3[] origNormals = original.normals;
            Vector4[] origTangents = original.tangents;
            int[] origTris = original.triangles;
            int[] origSubMeshCounts = Enumerable.Range(0, original.subMeshCount)
                .Select(sm => (int)original.GetTriangles(sm)?.Length ?? 0)
                .ToArray();

            // Strategy: Create a simplified mesh by keeping 1 out of every 4 vertices
            // Map old vertex index -> new vertex index (or -1 if skipped)
            int vertexStep = 4; // Keep 1 out of 4 vertices
            int newVertexCount = Mathf.Max(3, origVerts.Length / vertexStep);

            var newVerts = new List<Vector3>(newVertexCount);
            var newUvs = new List<Vector2>(newVertexCount);
            var newNormals = new List<Vector3>(newVertexCount);
            var newTangents = new List<Vector4>(newVertexCount);
            var oldToNew = new int[origVerts.Length];
            for (int i = 0; i < oldToNew.Length; i++) oldToNew[i] = -1;

            for (int i = 0; i < origVerts.Length; i += vertexStep)
            {
                int newIdx = newVerts.Count;
                newVerts.Add(origVerts[i]);
                oldToNew[i] = newIdx;

                if (origUv != null && origUv.Length > i)
                    newUvs.Add(origUv[i]);
                else
                    newUvs.Add(Vector2.zero);

                if (origNormals != null && origNormals.Length > i)
                    newNormals.Add(origNormals[i]);
                else
                    newNormals.Add(Vector3.up);

                if (origTangents != null && origTangents.Length > i)
                    newTangents.Add(origTangents[i]);
                else
                    newTangents.Add(new Vector4(1, 0, 0, 1));
            }

            // If we have too few vertices, add a fallback
            if (newVerts.Count < 3)
            {
                // Add the first 3 vertices
                for (int i = 0; i < 3 && i < origVerts.Length; i++)
                {
                    if (oldToNew[i] < 0)
                    {
                        int newIdx = newVerts.Count;
                        newVerts.Add(origVerts[i]);
                        oldToNew[i] = newIdx;
                        newUvs.Add(origUv != null && origUv.Length > i ? origUv[i] : Vector2.zero);
                        newNormals.Add(origNormals != null && origNormals.Length > i ? origNormals[i] : Vector3.up);
                        newTangents.Add(origTangents != null && origTangents.Length > i ? origTangents[i] : new Vector4(1, 0, 0, 1));
                    }
                }
            }

            // Remap triangle indices
            var newTris = new List<int>(origTris.Length / vertexStep);
            for (int i = 0; i < origTris.Length; i++)
            {
                int oldIdx = origTris[i];
                int newIdx = oldToNew[oldIdx];
                if (newIdx >= 0)
                {
                    newTris.Add(newIdx);
                }
                else
                {
                    // Find the nearest kept vertex
                    int nearestKept = FindNearestKeptVertex(oldIdx, oldToNew, vertexStep);
                    newTris.Add(nearestKept);
                }
            }

            // Build the simplified mesh
            Mesh simplified = new Mesh();
            simplified.name = string.IsNullOrEmpty(objectName)
                ? $"{original.name}_LOD2_Simplified"
                : $"{objectName}_LOD2_Simplified";

            simplified.vertices = newVerts.ToArray();
            simplified.uv = newUvs.ToArray();
            simplified.normals = newNormals.ToArray();
            simplified.tangents = newTangents.ToArray();
            simplified.triangles = newTris.ToArray();

            // Recalculate normals for the simplified mesh
            simplified.RecalculateNormals();
            simplified.RecalculateBounds();

            return simplified;
        }

        /// <summary>
        /// Finds the nearest kept vertex index for remapping.
        /// Searches forward and backward from the given index.
        /// </summary>
        private static int FindNearestKeptVertex(int oldIdx, int[] oldToNew, int step)
        {
            // Search forward
            for (int d = 1; d < step; d++)
            {
                int forward = oldIdx + d;
                if (forward < oldToNew.Length && oldToNew[forward] >= 0)
                    return oldToNew[forward];

                int backward = oldIdx - d;
                if (backward >= 0 && oldToNew[backward] >= 0)
                    return oldToNew[backward];
            }

            // Fallback: return the first kept vertex
            for (int i = 0; i < oldToNew.Length; i++)
            {
                if (oldToNew[i] >= 0)
                    return oldToNew[i];
            }

            return 0;
        }

        // ================================================================
        //  LOD Child Helpers
        // ================================================================

        /// <summary>
        /// Creates (or finds) a child GameObject under the parent for LOD
        /// simplified mesh rendering.
        /// </summary>
        private static GameObject CreateLODChild(GameObject parent, string suffix)
        {
            string childName = $"{parent.name}{suffix}";
            Transform existing = parent.transform.Find(childName);
            if (existing != null)
                return existing.gameObject;

            GameObject child = new GameObject(childName);
            child.transform.SetParent(parent.transform);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;

            return child;
        }

        /// <summary>
        /// Cleans up empty LOD holder objects in the scene.
        /// </summary>
        private static void CleanupLODHolders()
        {
            var holders = GameObject.FindObjectsOfType<Transform>(true)
                .Where(t => t.name.Contains("_LOD2") || t.name.Contains("_LOD_Holder"))
                .Select(t => t.gameObject)
                .ToList();

            foreach (var h in holders)
            {
                if (h != null && h.GetComponents<Component>().Length <= 1) // Only Transform
                {
                    UnityEngine.Object.DestroyImmediate(h);
                }
            }
        }

        // ================================================================
        //  Poly Haven Object Detection
        // ================================================================

        /// <summary>
        /// Finds all GameObjects in the active scene whose names start with
        /// any of the known Poly Haven model prefixes.
        /// </summary>
        public static List<GameObject> FindAllSceneModels()
        {
            var results = new List<GameObject>();
            var allObjects = GameObject.FindObjectsOfType<GameObject>(true);

            foreach (var go in allObjects)
            {
                if (go.scene.isLoaded == false)
                    continue;

                foreach (var prefix in ModelPrefixes)
                {
                    if (go.name.StartsWith(prefix) || go.name.Contains(prefix))
                    {
                        results.Add(go);
                        break;
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Counts how many Poly Haven models exist per type prefix.
        /// </summary>
        public static Dictionary<string, int> CountModelsByPrefix()
        {
            var counts = new Dictionary<string, int>();
            foreach (var prefix in ModelPrefixes)
                counts[prefix] = 0;

            var objects = FindAllSceneModels();
            foreach (var go in objects)
            {
                foreach (var prefix in ModelPrefixes)
                {
                    if (go.name.StartsWith(prefix) || go.name.Contains(prefix))
                    {
                        counts[prefix]++;
                        break;
                    }
                }
            }

            return counts;
        }

        // ================================================================
        //  Scene Helpers
        // ================================================================

        private static UnityEngine.SceneManagement.Scene? GetOrOpenMainScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(scene.path) && scene.path.Contains("MainScene"))
                return scene;

            string[] guids = AssetDatabase.FindAssets("t:Scene MainScene");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }

            Debug.LogWarning("[PhaseG1_LODGenerator] MainScene not found. Creating new scene...");
            scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            return scene;
        }

        // ================================================================
        //  Public Test Helpers
        // ================================================================

        /// <summary>
        /// Public wrapper for ApplyLODGroupToObject for testing.
        /// Returns true if LODGroup was successfully applied.
        /// </summary>
        public static bool ApplyLODGroupToObjectTest(GameObject go)
        {
            _simplifiedMeshCache.Clear();
            return ApplyLODGroupToObject(go);
        }

        /// <summary>
        /// Returns the LOD transition thresholds for test validation.
        /// </summary>
        public static float[] GetLODTransitions()
        {
            return new[] { LOD0_Transition, LOD1_Transition, LOD2_Transition };
        }

        /// <summary>
        /// Get the count of cached simplified meshes.
        /// </summary>
        public static int GetSimplifiedMeshCacheCount()
        {
            return _simplifiedMeshCache.Count;
        }

        /// <summary>
        /// Clears the simplified mesh cache.
        /// </summary>
        public static void ClearSimplifiedMeshCache()
        {
            _simplifiedMeshCache.Clear();
        }
    }
}
#endif