using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

/// <summary>
/// Poly Haven 텍스처 JPG를 TopDownScene 지형에 구역별로 적용 (v2 - URP 수정).
/// </summary>
public static class Phase3_TerrainTextureSetup
{
    private const string POLYHAVEN_PATH = "Assets/Resources/Models/PolyHeven";

    [MenuItem("Tools/Phase 3.10 - 지형 텍스처 적용")]
    public static void SetupTerrainTextures()
    {
        string scenePath = "Assets/Scenes/TopDownScene.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // 1. 씬 정리: 기존 Ground 및 이전 링 제거
        CleanupOldGrounds();

        // 2. 텍스처 JPG 검색 (외부 경로에서도 검색)
        Texture2D mudTex = FindTexture("brown_mud_leaves");
        Texture2D rockyTex = FindTexture("rocky_terrain");
        Texture2D coastTex = FindTexture("coast_sand_rocks");

        // 3. 텍스처가 null이면 PolyHaven 내 모든 _diff JPG 검색
        if (mudTex == null || rockyTex == null || coastTex == null)
        {
            Debug.Log("[TerrainTextures] 일부 텍스처 누락 → Assets 내 모든 _diff JPG 검색");
            var allTex = AssetDatabase.FindAssets("_diff t:texture2d", new[] { "Assets" });
            foreach (string guid in allTex)
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.Contains("PolyHeven") || p.Contains("polyhaven"))
                {
                    Texture2D t = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                    if (t == null) continue;
                    if (mudTex == null && p.Contains("mud")) mudTex = t;
                    else if (rockyTex == null && p.Contains("rocky")) rockyTex = t;
                    else if (coastTex == null && p.Contains("coast")) coastTex = t;
                }
            }
        }

        Debug.Log($"[TerrainTextures] mud={(mudTex != null ? mudTex.name : "null")}, " +
                  $"rocky={(rockyTex != null ? rockyTex.name : "null")}, " +
                  $"coast={(coastTex != null ? coastTex.name : "null")}");

        // 4. 구역별 지반 생성 (URP BaseMap으로 직접 할당)
        CreateURPZone("Ground_Inner", 350f, mudTex, new Vector2(15, 15));
        CreateURPZone("Ground_Mid", 700f, rockyTex ?? mudTex, new Vector2(20, 20));
        CreateURPZone("Ground_Outer", 1000f, coastTex ?? rockyTex ?? mudTex, new Vector2(25, 25));

        // 5. 바닥 위치 보정
        foreach (string name in new[] { "Ground_Inner", "Ground_Mid", "Ground_Outer" })
        {
            var go = GameObject.Find(name);
            if (go != null) go.transform.position = Vector3.zero;
        }

        // 6. 카메라 설정
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.farClipPlane = 2000f;
            cam.backgroundColor = new Color(0.4f, 0.6f, 0.8f); // 연한 하늘색
        }

        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log("[TerrainTextures] ✅ v2 적용 완료!");
        EditorApplication.Exit(0);
    }

    /// <summary>
    /// Ground/Ground_Inner/Ground_Mid/Ground_Outer 전부 제거
    /// </summary>
    private static void CleanupOldGrounds()
    {
        string[] names = { "Ground", "Ground_Inner", "Ground_Mid", "Ground_Outer" };
        foreach (string n in names)
        {
            var go = GameObject.Find(n);
            if (go != null) Object.DestroyImmediate(go);
        }
    }

    /// <summary>
    /// URP Lit 머티리얼로 구역 생성 (BaseMap 직접 할당)
    /// </summary>
    private static GameObject CreateURPZone(string name, float radius, Texture2D texture, Vector2 tiling)
    {
        var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.name = name;
        float scale = radius / 5f;
        plane.transform.localScale = new Vector3(scale, 1, scale);
        plane.transform.position = Vector3.zero;

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.name = $"Mat_{name}";

        if (texture != null)
        {
            // URP: _BaseMap이 올바른 프로퍼티
            mat.SetTexture("_BaseMap", texture);
            mat.SetTextureScale("_BaseMap", tiling);
            mat.SetFloat("_Smoothness", 0.2f);
            mat.SetFloat("_Metallic", 0f);
        }
        else
        {
            // 텍스처 없으면 갈색 계열 (파란색 방지)
            mat.color = new Color(0.35f, 0.55f, 0.25f);
        }

        plane.GetComponent<MeshRenderer>().material = mat;
        return plane;
    }

    /// <summary>
    /// 텍스처 JPG 찾기
    /// </summary>
    private static Texture2D FindTexture(string keyword)
    {
        // 1. _diff 키워드로 1차 검색
        string[] assets = AssetDatabase.FindAssets($"{keyword}_diff t:texture2D", new[] { POLYHAVEN_PATH });
        if (assets.Length == 0)
            assets = AssetDatabase.FindAssets($"{keyword} t:texture2D", new[] { POLYHAVEN_PATH });
        if (assets.Length == 0)
            assets = AssetDatabase.FindAssets($"{keyword} t:texture2D", new[] { "Assets" });

        foreach (string guid in assets)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("_arm") || path.Contains("_nor_gl")) continue;
            if (path.Contains("_rough")) continue;
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null)
                return tex;
        }
        return null;
    }

    [MenuItem("Tools/Phase 3.10 - 지형 텍스처 적용", true)]
    private static bool Validate() => true;
}