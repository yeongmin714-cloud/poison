using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectName.Core;

public static class Phase3_EnvironmentalDetails
{
    [MenuItem("Tools/Phase 3.7 - Place Environmental Details")]
    public static void PlaceDetails()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene == null || string.IsNullOrEmpty(scene.path))
        {
            string[] guids = AssetDatabase.FindAssets("t:Scene TopDownScene");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }
            else
            {
                Debug.LogError("[EnvDetails] TopDownScene not found! Run 'Phase 3 - Setup Top-Down Player Scene' first.");
                return;
            }
        }

        // 기존 환경 디테일 제거 (Grass, Bush, Flower, MapBoundary)
        CleanupExisting();

        // 배치
        PlaceGrassClusters();
        PlaceBushClusters();
        PlaceFlowers();
        PlaceMapBoundary();

        EditorSceneManager.SaveScene(scene);
        Debug.Log("[EnvDetails] Environmental details placed: 500 grass, 200 bushes, 150 flowers, 4 map boundaries");
    }

    private static void CleanupExisting()
    {
        var allGOs = GameObject.FindObjectsByType<GameObject>();
        foreach (var go in allGOs)
        {
            if (go.name.StartsWith("Grass_") ||
                go.name.StartsWith("Bush_") ||
                go.name.StartsWith("Flower_") ||
                go.name == "MapBoundary")
            {
                GameObject.DestroyImmediate(go);
            }
        }
    }

    private static void PlaceGrassClusters()
    {
        int count = 500;
        System.Random rng = new System.Random(101);

        for (int i = 0; i < count; i++)
        {
            // 랜덤 위치 (플레이어 시작 위치 12m 제외)
            float x = (float)rng.NextDouble() * 1800f - 900f;
            float z = (float)rng.NextDouble() * 1800f - 900f;
            if (System.Math.Abs(x) < 12f && System.Math.Abs(z + 950) < 12f) continue;

            var cluster = new GameObject($"Grass_{i}");
            cluster.transform.position = new Vector3(x, 0, z);

            // 연두색 계열 (RGB: 0.2~0.4, 0.5~0.7, 0.1~0.2)
            float r = 0.2f + (float)rng.NextDouble() * 0.2f;
            float g = 0.5f + (float)rng.NextDouble() * 0.2f;
            float b = 0.1f + (float)rng.NextDouble() * 0.1f;
            Color grassColor = new Color(r, g, b);

            // 3~5개의 원뿔 또는 평면을 그룹으로
            int bladeCount = 3 + rng.Next(3); // 3~5
            for (int j = 0; j < bladeCount; j++)
            {
                GameObject blade;
                // Cone과 Plane 번갈아 사용 (랜덤 선택)
                if (rng.NextDouble() > 0.5f)
                    blade = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                else
                    blade = GameObject.CreatePrimitive(PrimitiveType.Plane);

                blade.name = $"Blade_{j}";
                blade.transform.SetParent(cluster.transform);

                // 랜덤 오프셋 (클러스터 내 위치)
                float ox = (float)(rng.NextDouble() - 0.5) * 0.3f;
                float oz = (float)(rng.NextDouble() - 0.5) * 0.3f;

                // 높이 0.1~0.3, 너비 0.1~0.2
                float height = 0.1f + (float)rng.NextDouble() * 0.2f;
                float width = 0.1f + (float)rng.NextDouble() * 0.1f;

                blade.transform.localPosition = new Vector3(ox, height * 0.4f, oz);
                blade.transform.localScale = new Vector3(width, height, width);

                // 랜덤 회전 (군데군데 살짝 기울어지게)
                blade.transform.localRotation = Quaternion.Euler(
                    (float)(rng.NextDouble() - 0.5) * 15f,
                    (float)rng.NextDouble() * 360f,
                    (float)(rng.NextDouble() - 0.5) * 15f
                );

                var mat = MaterialHelper.CreateLitMaterial(grassColor, $"GrassBlade_Mat_{i}_{j}");
                blade.GetComponent<MeshRenderer>().material = mat;
            }
        }
    }

    private static void PlaceBushClusters()
    {
        int count = 200;
        System.Random rng = new System.Random(202);

        for (int i = 0; i < count; i++)
        {
            float x = (float)rng.NextDouble() * 1800f - 900f;
            float z = (float)rng.NextDouble() * 1800f - 900f;
            if (System.Math.Abs(x) < 12f && System.Math.Abs(z + 950) < 12f) continue;

            var bush = new GameObject($"Bush_{i}");
            bush.transform.position = new Vector3(x, 0, z);

            // 진녹색 계열
            Color bushColor = new Color(
                0.08f + (float)rng.NextDouble() * 0.12f,
                0.25f + (float)rng.NextDouble() * 0.25f,
                0.05f + (float)rng.NextDouble() * 0.1f
            );

            // 구체 3~5개를 하나의 부모 GameObject로 그룹화
            int sphereCount = 3 + rng.Next(3); // 3~5
            float bushSize = 0.3f + (float)rng.NextDouble() * 0.3f; // 0.3~0.6

            for (int j = 0; j < sphereCount; j++)
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = $"BushPart_{j}";
                sphere.transform.SetParent(bush.transform);

                // 클러스터 내에서 구체 분산
                float ox = (float)(rng.NextDouble() - 0.5) * bushSize * 0.8f;
                float oz = (float)(rng.NextDouble() - 0.5) * bushSize * 0.8f;
                float oy = (float)rng.NextDouble() * bushSize * 0.4f;

                float sphereSize = bushSize * (0.4f + (float)rng.NextDouble() * 0.3f);

                sphere.transform.localPosition = new Vector3(ox, oy + bushSize * 0.3f, oz);
                sphere.transform.localScale = new Vector3(sphereSize, sphereSize, sphereSize);

                var mat = MaterialHelper.CreateLitMaterial(bushColor, $"BushMat_{i}_{j}");
                sphere.GetComponent<MeshRenderer>().material = mat;
            }
        }
    }

    private static void PlaceFlowers()
    {
        int count = 150;
        System.Random rng = new System.Random(303);

        // 다양한 색상
        Color[] flowerColors = new Color[]
        {
            new Color(1f, 0.2f, 0.2f),    // 빨강
            new Color(1f, 0.8f, 0.1f),    // 노랑
            new Color(0.6f, 0.2f, 0.8f),  // 보라
            new Color(1f, 0.4f, 0.7f),    // 분홍
            new Color(1f, 0.5f, 0.1f),    // 주황
            new Color(0.8f, 0.1f, 0.3f),  // 진빨강
            new Color(0.9f, 0.9f, 0.2f),  // 연노랑
        };

        for (int i = 0; i < count; i++)
        {
            float x = (float)rng.NextDouble() * 1800f - 900f;
            float z = (float)rng.NextDouble() * 1800f - 900f;
            if (System.Math.Abs(x) < 12f && System.Math.Abs(z + 950) < 12f) continue;

            var flower = new GameObject($"Flower_{i}");
            flower.transform.position = new Vector3(x, 0, z);

            Color color = flowerColors[rng.Next(flowerColors.Length)];

            // 아주 작은 구체 (0.05~0.1)
            float flowerSize = 0.05f + (float)rng.NextDouble() * 0.05f;

            // 줄기 (작은 원통)
            var stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stem.name = "Stem";
            stem.transform.SetParent(flower.transform);
            stem.transform.localPosition = new Vector3(0, 0.04f, 0);
            stem.transform.localScale = new Vector3(0.015f, 0.08f, 0.015f);
            var stemMat = MaterialHelper.CreateLitMaterial(new Color(0.2f, 0.5f, 0.1f), $"StemMat_{i}");
            stem.GetComponent<MeshRenderer>().material = stemMat;

            // 꽃잎 (구체)
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(flower.transform);
            head.transform.localPosition = new Vector3(0, 0.12f, 0);
            head.transform.localScale = new Vector3(flowerSize, flowerSize, flowerSize);
            var headMat = MaterialHelper.CreateLitMaterial(color, $"FlowerHeadMat_{i}");
            head.GetComponent<MeshRenderer>().material = headMat;
        }
    }

    private static void PlaceMapBoundary()
    {
        // 맵 경계: 맵 가장자리(±1000)에 물 또는 절벽 느낌의 반투명 평면
        // Plane을 4개의 긴 띠로 배치
        // 짙은 청색 반투명 (물 효과)
        Color waterColor = new Color(0.1f, 0.2f, 0.4f, 0.6f);

        var boundary = new GameObject("MapBoundary");
        boundary.transform.position = Vector3.zero;

        // 각 면: 2000 x 10 크기의 Plane (기본 Plane 10x10, scale 200x1)
        Vector3[] positions = new Vector3[]
        {
            new Vector3(0, -0.5f, 1000),    // 북 (Z+)
            new Vector3(0, -0.5f, -1000),   // 남 (Z-)
            new Vector3(1000, -0.5f, 0),    // 동 (X+)
            new Vector3(-1000, -0.5f, 0),   // 서 (X-)
        };

        Quaternion[] rotations = new Quaternion[]
        {
            Quaternion.identity,                    // 북 (그대로)
            Quaternion.identity,                    // 남
            Quaternion.Euler(0, 90, 0),             // 동 (90도 회전)
            Quaternion.Euler(0, 90, 0),             // 서
        };

        string[] sideNames = new string[] { "North", "South", "East", "West" };

        for (int i = 0; i < 4; i++)
        {
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = $"Boundary_{sideNames[i]}";
            plane.transform.SetParent(boundary.transform);
            plane.transform.position = positions[i];
            plane.transform.rotation = rotations[i];
            // Plane 기본 10x10, scale 200x1 = 2000x10 (2000x2000 영역 경계선용)
            plane.transform.localScale = new Vector3(200, 1, 1);

            var mat = MaterialHelper.CreateLitMaterial(waterColor, $"BoundaryMat_{sideNames[i]}");
            // 반투명을 위한 렌더 모드 설정 (Fade)
            mat.SetFloat("_Surface", 1);   // Surface Type: Transparent
            mat.SetFloat("_BlendMode", 0); // Alpha
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0);
            mat.SetFloat("_AlphaClip", 0);
            mat.renderQueue = 3000;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_BLENDMODE_ALPHA");
            // 투명도 적용
            Color c = mat.color;
            c.a = 0.6f;
            mat.color = c;

            plane.GetComponent<MeshRenderer>().material = mat;
        }
    }

    [MenuItem("Tools/Phase 3.7 - Place Environmental Details", true)]
    private static bool Validate() => true;
}