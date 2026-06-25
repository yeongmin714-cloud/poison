using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ProjectName.Core;

public static class Phase3_TerrainUpgrade
{
    [MenuItem("Tools/Phase 3.6 - Upgrade Terrain Graphics")]
    public static void UpgradeTerrain()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene == null || string.IsNullOrEmpty(scene.path))
        {
            // TopDownScene 열기
            string[] guids = AssetDatabase.FindAssets("t:Scene TopDownScene");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }
            else
            {
                Debug.LogError("[TerrainUpgrade] TopDownScene not found! Run 'Phase 3 - Setup Top-Down Player Scene' first.");
                return;
            }
        }

        // ===== 1. 안개 (Fog via URP Volume) =====
        SetupFog();

        // ===== 2. 지면 텍스처 (프로그래매틱 생성) =====
        ApplyGroundTexture();

        // ===== 3. 방향광 튜닝 =====
        TuneLighting();

        // ===== 4. 환경 조형물 (나무 + 바위) =====
        PlaceEnvironmentObjects();

        EditorSceneManager.SaveScene(scene);
        Debug.Log("[TerrainUpgrade] Terrain graphics upgraded!");
    }

    private static void SetupFog()
    {
        // 글로벌 볼륨 찾기 또는 생성
        var volumeGO = GameObject.Find("Global Volume");
        if (volumeGO == null)
        {
            volumeGO = new GameObject("Global Volume");
            var volume = volumeGO.AddComponent<Volume>();
            volume.isGlobal = true;
        }

        var vol = volumeGO.GetComponent<Volume>();
        if (vol.profile == null)
        {
            vol.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            vol.profile.name = "Terrain_VolumeProfile";
        }

        var profile = vol.profile;

        // URP Volume Override는 직접 타입 참조 불가 → VolumeManager로 설정
        // 대신 RenderSettings Fog 사용 (전역 안개)
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.008f;
        RenderSettings.fogColor = new Color(0.6f, 0.7f, 0.75f);

        Debug.Log("[TerrainUpgrade] Fog set via RenderSettings");
    }

    private static void ApplyGroundTexture()
    {
        // 지면(Ground) 찾기
        var ground = GameObject.Find("Ground");
        if (ground == null)
        {
            Debug.LogWarning("[TerrainUpgrade] Ground not found! Run Scene Setup first.");
            return;
        }

        // 텍스처 생성
        var tex = new Texture2D(256, 256, TextureFormat.RGB24, true);
        tex.name = "Ground_Grass_Texture";
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;

        // 노이즈 기반 잔디 텍스처 생성
        Color[] pixels = new Color[256 * 256];
        for (int y = 0; y < 256; y++)
        {
            for (int x = 0; x < 256; x++)
            {
                // Perlin-like noise for grass variation
                float n1 = Mathf.PerlinNoise(x * 0.05f, y * 0.05f);
                float n2 = Mathf.PerlinNoise(x * 0.1f + 100, y * 0.1f + 100);
                float n3 = Mathf.PerlinNoise(x * 0.02f + 200, y * 0.02f + 200);

                // Base grass color with variation
                float r = 0.25f + n1 * 0.1f + n2 * 0.05f;
                float g = 0.55f + n1 * 0.15f + n3 * 0.1f;
                float b = 0.15f + n2 * 0.08f;

                // Darker spots for dirt patches
                if (n3 < 0.3f)
                {
                    r *= 0.7f;
                    g *= 0.6f;
                    b *= 0.5f;
                }

                pixels[y * 256 + x] = new Color(r, g, b, 1);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();

        // 텍스처를 프로젝트에 저장 (Runtime에 로드 가능하게)
        string texPath = "Assets/Textures/Ground_Grass.asset";
        System.IO.Directory.CreateDirectory("Assets/Textures");
        
        // 기존 텍스처가 있으면 업데이트
        var existingTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (existingTex != null)
        {
            EditorUtility.CopySerialized(tex, existingTex);
        }
        else
        {
            AssetDatabase.CreateAsset(tex, texPath);
        }
        AssetDatabase.SaveAssets();

        // 머티리얼 생성 (URP Lit)
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.name = "Ground_Grass_Mat";
        mat.mainTexture = tex;
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", 0.1f);
        mat.mainTextureScale = new Vector2(200, 200);  // 타일링 (2000×2000 지형)

        string matPath = "Assets/Materials/Ground_Grass_Mat.mat";
        System.IO.Directory.CreateDirectory("Assets/Materials");
        var existingMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (existingMat != null)
        {
            EditorUtility.CopySerialized(mat, existingMat);
        }
        else
        {
            AssetDatabase.CreateAsset(mat, matPath);
        }
        AssetDatabase.SaveAssets();

        // 지면에 적용
        var renderer = ground.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.material = mat;
        }

        Debug.Log("[TerrainUpgrade] Ground texture applied");
    }

    private static void TuneLighting()
    {
        var lightGO = GameObject.Find("Directional Light");
        if (lightGO == null) return;

        var light = lightGO.GetComponent<Light>();
        if (light == null) return;

        light.intensity = 1.5f;
        light.color = new Color(1f, 0.95f, 0.85f);  // 따뜻한 태양광
        light.shadowStrength = 0.5f;
    }

    private static void PlaceEnvironmentObjects()
    {
        // 기존 환경 오브젝트 제거
        var oldPillars = GameObject.FindObjectsByType<GameObject>();
        foreach (var go in oldPillars)
        {
            if (go.name.StartsWith("Pillar_"))
                GameObject.DestroyImmediate(go);
        }

        // 나무 + 바위 랜덤 배치
        int treeCount = 300;
        int rockCount = 200;

        // 나무 색상
        Color trunkColor = new Color(0.35f, 0.25f, 0.15f);
        Color leafColor1 = new Color(0.15f, 0.5f, 0.1f);
        Color leafColor2 = new Color(0.2f, 0.45f, 0.08f);

        System.Random rng = new System.Random(42); // fixed seed for consistency

        for (int i = 0; i < treeCount; i++)
        {
            float x = (float)rng.NextDouble() * 1800f - 900f;
            float z = (float)rng.NextDouble() * 1800f - 900f;
            // Center area (player spawn at 0,-950) clear
            if (System.Math.Abs(x) < 12f && System.Math.Abs(z + 950) < 12f) continue;

            float height = 2f + (float)rng.NextDouble() * 1.5f;

            var tree = new GameObject($"Tree_{i}");
            tree.transform.position = new Vector3(x, 0, z);

            // 기둥 (나무 줄기)
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(tree.transform);
            trunk.transform.localPosition = new Vector3(0, height * 0.3f, 0);
            trunk.transform.localScale = new Vector3(0.15f, height * 0.3f, 0.15f);
            var trunkMat = MaterialHelper.CreateLitMaterial(trunkColor, "Trunk_Mat");
            trunk.GetComponent<MeshRenderer>().material = trunkMat;

            // 구체 (나뭇잎)
            var leaf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            leaf.name = "Leaves";
            leaf.transform.SetParent(tree.transform);
            leaf.transform.localPosition = new Vector3(0, height * 0.7f, 0);
            float leafSize = 0.8f + (float)rng.NextDouble() * 0.4f;
            leaf.transform.localScale = new Vector3(leafSize, leafSize, leafSize);
            var leafMat = MaterialHelper.CreateLitMaterial(
                rng.NextDouble() > 0.5f ? leafColor1 : leafColor2, "Leaf_Mat"
            );
            leaf.GetComponent<MeshRenderer>().material = leafMat;
        }

        for (int i = 0; i < rockCount; i++)
        {
            float x = (float)rng.NextDouble() * 1800f - 900f;
            float z = (float)rng.NextDouble() * 1800f - 900f;
            if (System.Math.Abs(x) < 12f && System.Math.Abs(z + 950) < 12f) continue;

            float rockSize = 0.3f + (float)rng.NextDouble() * 0.6f;
            Color rockColor = new Color(
                0.3f + (float)rng.NextDouble() * 0.15f,
                0.28f + (float)rng.NextDouble() * 0.12f,
                0.25f + (float)rng.NextDouble() * 0.1f
            );

            var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = $"Rock_{i}";
            rock.transform.position = new Vector3(x, rockSize * 0.3f, z);
            rock.transform.localScale = new Vector3(rockSize, rockSize * 0.6f, rockSize);
            // Slight random rotation
            rock.transform.rotation = Random.rotationUniform;
            var rockMat = MaterialHelper.CreateLitMaterial(rockColor, "Rock_Mat");
            rock.GetComponent<MeshRenderer>().material = rockMat;
        }

        Debug.Log($"[TerrainUpgrade] {treeCount} trees + {rockCount} rocks placed");
    }

    [MenuItem("Tools/Phase 3.6 - Upgrade Terrain Graphics", true)]
    private static bool Validate() => true;
}