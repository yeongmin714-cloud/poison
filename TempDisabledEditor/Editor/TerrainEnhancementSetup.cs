using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// C7-22: 계곡 & 물 지형 배치.
/// C7-23: 국가별 지형 텍스처 차별화.
/// </summary>
public static class TerrainEnhancementSetup
{
    // 국가별 지형 색상
    private static readonly (string name, Color ground, Color accent)[] Nations = new[]
    {
        ("East",  new Color(0.25f, 0.55f, 0.20f), new Color(0.15f, 0.40f, 0.10f)), // 동: 푸른 초원
        ("West",  new Color(0.60f, 0.45f, 0.25f), new Color(0.45f, 0.30f, 0.15f)), // 서: 황토
        ("South", new Color(0.55f, 0.25f, 0.15f), new Color(0.40f, 0.15f, 0.08f)), // 남: 붉은 흙
        ("North", new Color(0.45f, 0.48f, 0.52f), new Color(0.35f, 0.38f, 0.42f)), // 북: 툰드라
    };

    [MenuItem("Tools/C7-22/23 - Setup Terrain Enhancements")]
    public static void ApplyAll()
    {
        CreateWaterBodies();
        ApplyNationTerrain();
        Debug.Log("[TerrainEnhancement] 계곡/물 지형 + 국가별 텍스처 적용 완료!");
    }

    /// <summary>C7-22: 계곡 형태의 물 배치</summary>
    [MenuItem("Tools/C7-22 - Create Water Bodies")]
    public static void CreateWaterBodies()
    {
        // 기존 Ground 찾기
        GameObject ground = GameObject.Find("Ground");
        if (ground == null)
        {
            ground = GameObject.Find("Plane");
        }
        if (ground == null)
        {
            Debug.LogError("[WaterBody] Ground 오브젝트를 찾을 수 없습니다.");
            return;
        }

        Transform parent = ground.transform.parent;
        if (parent == null)
        {
            var root = new GameObject("_TerrainFeatures");
            parent = root.transform;
        }

        // 3개의 물 영역 생성 (계곡 형태)
        CreateWaterPlane(parent, new Vector3(-80, 0.1f, 60), new Vector3(30, 1, 20), "Water_East");
        CreateWaterPlane(parent, new Vector3(100, 0.1f, -40), new Vector3(25, 1, 15), "Water_West");
        CreateWaterPlane(parent, new Vector3(-30, 0.1f, -100), new Vector3(35, 1, 22), "Water_South");

        Debug.Log("[WaterBody] 3개 계곡 물 영역 생성 완료");
    }

    private static void CreateWaterPlane(Transform parent, Vector3 position, Vector3 scale, string name)
    {
        GameObject water = GameObject.CreatePrimitive(PrimitiveType.Plane);
        water.name = name;
        water.transform.SetParent(parent);
        water.transform.position = position;
        water.transform.localScale = scale;

        // 물 머티리얼
        Shader waterShader = Shader.Find("Universal Render Pipeline/Lit");
        if (waterShader == null) waterShader = Shader.Find("Standard");

        Material waterMat = new Material(waterShader);
        waterMat.color = new Color(0.15f, 0.35f, 0.55f, 0.75f); // 반투명 푸른색

        // URP 투명 설정
        waterMat.SetFloat("_Surface", 1); // Transparent
        waterMat.SetFloat("_Blend", 0);   // Alpha
        waterMat.renderQueue = 3000;

        var renderer = water.GetComponent<MeshRenderer>();
        renderer.material = waterMat;
        renderer.shadowCastingMode = ShadowCastingMode.Off;

        // 물 충돌 영역 (Trigger)
        var collider = water.GetComponent<MeshCollider>();
        if (collider == null)
        {
            collider = water.AddComponent<MeshCollider>();
        }
        collider.isTrigger = true;
        water.tag = "Water";

        Debug.Log($"[WaterBody] {name} 생성 완료 (pos={position})");
    }

    /// <summary>C7-23: 국가별 지형 텍스처 차별화</summary>
    [MenuItem("Tools/C7-23 - Apply Nation Terrain")]
    public static void ApplyNationTerrain()
    {
        // 4개 국가별 지형 구역 생성
        float mapSize = 500f;
        float halfSize = mapSize / 2f;
        float quarterSize = mapSize / 4f;

        Transform parent = null;
        var existing = GameObject.Find("_TerrainFeatures");
        if (existing != null) parent = existing.transform;
        else
        {
            var root = new GameObject("_TerrainFeatures");
            parent = root.transform;
        }

        // 동: +X (우측)
        CreateNationTerrain(parent, "Terrain_East", new Vector3(quarterSize, 0.02f, 0), new Vector3(halfSize, 1, mapSize), Nations[0]);
        // 서: -X (좌측)
        CreateNationTerrain(parent, "Terrain_West", new Vector3(-quarterSize, 0.02f, 0), new Vector3(halfSize, 1, mapSize), Nations[1]);
        // 남: -Z (아래)
        CreateNationTerrain(parent, "Terrain_South", new Vector3(0, 0.02f, -quarterSize), new Vector3(mapSize, 1, halfSize), Nations[2]);
        // 북: +Z (위)
        CreateNationTerrain(parent, "Terrain_North", new Vector3(0, 0.02f, quarterSize), new Vector3(mapSize, 1, halfSize), Nations[3]);

        Debug.Log("[NationTerrain] 4개 국가별 지형 텍스처 적용 완료");
    }

    private static void CreateNationTerrain(Transform parent, string name, Vector3 position, Vector3 scale, (string, Color, Color) nation)
    {
        GameObject terrain = GameObject.CreatePrimitive(PrimitiveType.Plane);
        terrain.name = name;
        terrain.transform.SetParent(parent);
        terrain.transform.position = position;
        terrain.transform.localScale = scale;

        // 국가별 색상 머티리얼
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.color = nation.Item2; // ground color
        mat.SetFloat("_Smoothness", 0.1f);

        var renderer = terrain.GetComponent<MeshRenderer>();
        renderer.material = mat;
        renderer.receiveShadows = true;

        // 기존 Ground 비활성화 (국가별 지형으로 대체)
        GameObject oldGround = GameObject.Find("Ground");
        if (oldGround == null) oldGround = GameObject.Find("Plane");
        if (oldGround != null && oldGround.GetComponent<MeshRenderer>() != null)
        {
            oldGround.GetComponent<MeshRenderer>().enabled = false;
        }

        Debug.Log($"[NationTerrain] {nation.Item1} 지형 생성 완료 (색상: {nation.Item2})");
    }
}