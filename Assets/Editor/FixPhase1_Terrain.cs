using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

public class FixPhase1_Terrain
{
    [MenuItem("Tools/Poison/Fix Phase 1 - Terrain")]
    public static void FixTerrain()
    {
        const string scenePath = "Assets/Scenes/MainScene.unity";
        var scene = SceneManager.GetSceneByName("MainScene");
        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(scenePath);
        }

        Debug.Log("=== PHASE 1: TERRAIN CREATION START ===");

        // 1. Terrain 오브젝트 생성 또는 찾기
        var terrainObj = GameObject.Find("Terrain");
        if (terrainObj == null)
        {
            terrainObj = new GameObject("Terrain");
            terrainObj.transform.position = Vector3.zero;
            Debug.Log("[Phase1] Created Terrain GameObject");
        }
        else
        {
            Debug.Log("[Phase1] Found existing Terrain GameObject");
        }

        terrainObj.layer = LayerMask.NameToLayer("Ground");
        terrainObj.isStatic = true;

        // 2. MeshFilter + MeshRenderer
        var mf = terrainObj.GetComponent<MeshFilter>();
        if (mf == null) mf = terrainObj.AddComponent<MeshFilter>();
        
        var mr = terrainObj.GetComponent<MeshRenderer>();
        if (mr == null) mr = terrainObj.AddComponent<MeshRenderer>();

        // 3. 프로시저럴 평면 메시 생성 (2000x2000, 세그먼트 200)
        var mesh = GeneratePlaneMesh(2000f, 2000f, 200);
        mesh.name = "TerrainMesh";
        mf.mesh = mesh;
        Debug.Log($"[Phase1] Generated plane mesh: {mesh.vertexCount} vertices, {mesh.triangles.Length/3} triangles");

        // 4. NationTerrainController에서 머티리얼 가져오기
        var controller = Object.FindFirstObjectByType<ProjectName.Systems.NationTerrainController>();
        Material[] nationMaterials = null;
        
        if (controller != null)
        {
            var type = controller.GetType();
            var field = type.GetField("NationMaterials");
            var prop = type.GetProperty("NationMaterials");
            
            if (field != null) nationMaterials = field.GetValue(controller) as Material[];
            else if (prop != null) nationMaterials = prop.GetValue(controller) as Material[];
        }

        // 5. 머티리얼 할당
        if (nationMaterials != null && nationMaterials.Length >= 5)
        {
            mr.sharedMaterials = nationMaterials;
            Debug.Log($"[Phase1] Assigned {nationMaterials.Length} nation materials to Terrain");
        }
        else
        {
            // 폴백: 단일 기본 머티리얼
            var fallbackMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            fallbackMat.name = "Terrain_Fallback";
            fallbackMat.color = new Color(0.4f, 0.5f, 0.3f);
            mr.sharedMaterial = fallbackMat;
            Debug.LogWarning("[Phase1] Nation materials not found, assigned fallback material");
        }

        mr.shadowCastingMode = ShadowCastingMode.On;
        mr.receiveShadows = true;

        // 6. 씬 저장 - 강제 저장
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, scenePath, true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        // 강제로 씬 파일 저장 확인
        Debug.Log($"[Phase1] Scene saved to: {scenePath}");
        Debug.Log("=== PHASE 1: TERRAIN CREATION COMPLETE ===");
    }

    static Mesh GeneratePlaneMesh(float width, float length, int segments)
    {
        var mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        int vertCount = (segments + 1) * (segments + 1);
        var vertices = new Vector3[vertCount];
        var uvs = new Vector2[vertCount];
        var normals = new Vector3[vertCount];
        var triangles = new int[segments * segments * 6];

        float halfW = width * 0.5f;
        float halfL = length * 0.5f;
        float stepW = width / segments;
        float stepL = length / segments;

        int idx = 0;
        for (int z = 0; z <= segments; z++)
        {
            for (int x = 0; x <= segments; x++)
            {
                vertices[idx] = new Vector3(-halfW + x * stepW, 0, -halfL + z * stepL);
                uvs[idx] = new Vector2((float)x / segments, (float)z / segments);
                normals[idx] = Vector3.up;
                idx++;
            }
        }

        int triIdx = 0;
        for (int z = 0; z < segments; z++)
        {
            for (int x = 0; x < segments; x++)
            {
                int v0 = z * (segments + 1) + x;
                int v1 = v0 + 1;
                int v2 = v0 + segments + 1;
                int v3 = v2 + 1;

                triangles[triIdx++] = v0;
                triangles[triIdx++] = v2;
                triangles[triIdx++] = v1;

                triangles[triIdx++] = v1;
                triangles[triIdx++] = v2;
                triangles[triIdx++] = v3;
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.normals = normals;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();

        return mesh;
    }
}