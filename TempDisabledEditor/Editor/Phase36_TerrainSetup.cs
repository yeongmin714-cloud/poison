using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectName.Systems;

/// <summary>
/// Phase 3.6: Skybox Setup (3.6.4) and Nation-Specific Terrain Textures (3.6.7).
/// Menu items under Tools/Phase 3.6/ for configuring the MainScene.
/// </summary>
public static class Phase36_TerrainSetup
{
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";

    // ================================================================
    //  Phase 3.6.4: Skybox
    // ================================================================

    /// <summary>
    /// Creates a warm dawn/dusk gradient procedural skybox matching the
    /// medieval/fantasy atmosphere. Uses Unity's built-in Procedural skybox
    /// shader with orange→purple→dark blue tinting.
    /// </summary>
    [MenuItem("Tools/Phase 3.6/Setup Skybox")]
    public static void SetupSkybox()
    {
        // Open or use current MainScene
        var scene = EditorSceneManager.GetActiveScene();
        if (scene == null || string.IsNullOrEmpty(scene.path) || !scene.path.Contains("MainScene"))
        {
            string[] guids = AssetDatabase.FindAssets("t:Scene MainScene");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }
            else
            {
                Debug.LogWarning("[Phase36] MainScene not found. Creating new scene...");
                scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                scene.name = "MainScene";
            }
        }

        // Check if we already have a skybox material saved
        const string skyboxMatPath = "Assets/Materials/Skybox_DawnDusk.mat";
        Material skyboxMat = AssetDatabase.LoadAssetAtPath<Material>(skyboxMatPath);

        if (skyboxMat == null)
        {
            Shader proceduralShader = Shader.Find("Skybox/Procedural");
            if (proceduralShader == null)
            {
                Debug.LogError("[Phase36] Skybox/Procedural shader not found! URP may not be configured.");
                return;
            }

            skyboxMat = new Material(proceduralShader);
            skyboxMat.name = "Skybox_DawnDusk";

            // Dawn/dusk warm gradient settings
            // _SkyTint: warm orange hue for upper sky
            skyboxMat.SetColor("_SkyTint", new Color(0.9f, 0.45f, 0.15f)); // orange
            // _GroundColor: dark purple/indigo for lower sky/ground
            skyboxMat.SetColor("_GroundColor", new Color(0.12f, 0.04f, 0.22f)); // dark purple
            // _AtmosphereThickness: denser = warmer, more dramatic colors
            skyboxMat.SetFloat("_AtmosphereThickness", 1.8f);
            // _SunSize: small, crisp sun
            skyboxMat.SetFloat("_SunSize", 0.03f);
            // _SunSizeConvergence: sharper sun edge
            skyboxMat.SetFloat("_SunSizeConvergence", 8f);
            // _Exposure: slightly brighter to show colors
            skyboxMat.SetFloat("_Exposure", 1.2f);

            // Save the material asset
            System.IO.Directory.CreateDirectory("Assets/Materials");
            AssetDatabase.CreateAsset(skyboxMat, skyboxMatPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Phase36] Skybox material created and saved to {skyboxMatPath}");
        }
        else
        {
            Debug.Log("[Phase36] Existing skybox material found, reusing.");
        }

        // Assign to RenderSettings
        RenderSettings.skybox = skyboxMat;

        // Update main camera clear flags
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            mainCam = camGO.AddComponent<Camera>();
            mainCam.clearFlags = CameraClearFlags.Skybox;
            mainCam.fieldOfView = 60f;
            mainCam.nearClipPlane = 0.1f;
            mainCam.farClipPlane = 2000f;
            Debug.Log("[Phase36] Created new Main Camera.");
        }
        else
        {
            mainCam.clearFlags = CameraClearFlags.Skybox;
        }

        // Save scene
        string scenePath = scene.path;
        if (string.IsNullOrEmpty(scenePath))
        {
            scenePath = "Assets/Scenes/MainScene.unity";
            System.IO.Directory.CreateDirectory("Assets/Scenes");
        }
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log("[Phase36] ✅ Skybox setup complete (dawn/dusk gradient).");
    }

    [MenuItem("Tools/Phase 3.6/Setup Skybox", true)]
    private static bool ValidateSetupSkybox() => true;

    // ================================================================
    //  Phase 3.6.7: Nation-Specific Terrain Textures
    // ================================================================

    /// <summary>
    /// Generates 5 nation-specific procedural terrain textures (256x256)
    /// and optionally places a NationTerrainController on the Ground object.
    /// Each texture uses Perlin noise blended with the nation's color tint
    /// over the ring-zone base pattern.
    /// 
    /// Nation colors:
    ///   East   = green  grassland
    ///   West   = yellow desert
    ///   South  = red    volcanic
    ///   North  = gray   tundra
    ///   Empire = golden
    /// </summary>
    [MenuItem("Tools/Phase 3.6/Setup Nation Terrain")]
    public static void SetupNationTerrain()
    {
        // Open MainScene
        var scene = EditorSceneManager.GetActiveScene();
        if (scene == null || string.IsNullOrEmpty(scene.path) || !scene.path.Contains("MainScene"))
        {
            string[] guids = AssetDatabase.FindAssets("t:Scene MainScene");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }
            else
            {
                Debug.LogError("[Phase36] MainScene not found! Run 'Setup Skybox' first.");
                return;
            }
        }

        // Ensure textures directory exists
        string texDir = "Assets/Textures/Terrain";
        System.IO.Directory.CreateDirectory(texDir);

        // Generate and save 5 nation textures
        string[] nationNames = { "East", "West", "South", "North", "Empire" };
        Color[] nationColors = {
            new Color(0.2f, 0.55f, 0.15f),  // East  = green grassland
            new Color(0.75f, 0.65f, 0.20f), // West  = yellow desert
            new Color(0.55f, 0.15f, 0.10f), // South = red volcanic
            new Color(0.50f, 0.50f, 0.55f), // North = gray tundra
            new Color(0.85f, 0.72f, 0.18f)  // Empire = golden
        };

        for (int i = 0; i < nationNames.Length; i++)
        {
            string texPath = $"{texDir}/Terrain_Nation_{nationNames[i]}.asset";
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

            if (existing == null)
            {
                Texture2D tex = GenerateNationTexture(nationNames[i], nationColors[i], i * 1000);
                AssetDatabase.CreateAsset(tex, texPath);
                Debug.Log($"[Phase36] Created nation texture: {texPath}");
            }
            else
            {
                Debug.Log($"[Phase36] Nation texture already exists: {texPath}");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Find or create Ground object
        GameObject ground = GameObject.Find("Ground");
        if (ground == null)
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(100f, 1f, 100f); // 1000x1000 world units

            // Create initial URP Lit material
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.name = "Ground_NationTerrain_Mat";
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0.1f);
            mat.mainTextureScale = new Vector2(200f, 200f);
            ground.GetComponent<MeshRenderer>().material = mat;

            Debug.Log("[Phase36] Created new Ground plane (1000x1000).");
        }
        else
        {
            Debug.Log("[Phase36] Found existing Ground object.");
        }

        // Add or update NationTerrainController
        NationTerrainController controller = ground.GetComponent<NationTerrainController>();
        if (controller == null)
        {
            controller = ground.AddComponent<NationTerrainController>();
            Debug.Log("[Phase36] Added NationTerrainController to Ground.");
        }
        else
        {
            Debug.Log("[Phase36] NationTerrainController already present on Ground.");
        }

        // Save scene
        string scenePath = scene.path;
        if (string.IsNullOrEmpty(scenePath))
            scenePath = "Assets/Scenes/MainScene.unity";
        EditorSceneManager.SaveScene(scene, scenePath);

        Debug.Log("[Phase36] ✅ Nation terrain setup complete (5 nation textures + controller).");
    }

    [MenuItem("Tools/Phase 3.6/Setup Nation Terrain", true)]
    private static bool ValidateSetupNationTerrain() => true;

    // ================================================================
    //  Procedural Texture Generation Helpers
    // ================================================================

    /// <summary>
    /// Generates a 256x256 procedural terrain texture for the given nation.
    /// Uses Perlin noise for organic variation, blending ring-zone base coloring
    /// with the nation's signature tint.
    /// </summary>
    private static Texture2D GenerateNationTexture(string nationName, Color nationTint, int seed)
    {
        const int texSize = 256;
        Texture2D tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, true);
        tex.name = $"Terrain_Nation_{nationName}";
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[texSize * texSize];

        for (int y = 0; y < texSize; y++)
        {
            for (int x = 0; x < texSize; x++)
            {
                // Normalized UV
                float u = (float)x / texSize;
                float v = (float)y / texSize;

                // --- Ring zone simulation ---
                // Map UV to world position on 1000x1000 terrain centered at origin
                float wx = (u - 0.5f) * 1000f;
                float wz = (v - 0.5f) * 1000f;
                float dist = Mathf.Sqrt(wx * wx + wz * wz);

                // Ring base colors
                Color ring1Color = new Color(0.45f, 0.30f, 0.15f); // brown_mud_leaves
                Color ring2Color = new Color(0.40f, 0.35f, 0.30f); // rocky_terrain
                Color ring3Color = new Color(0.70f, 0.60f, 0.40f); // coast_sand_rocks

                Color baseColor;
                float blend;
                if (dist < 350f)
                {
                    baseColor = ring1Color;
                    blend = 0f;
                }
                else if (dist < 700f)
                {
                    // Blend ring1 → ring2 (350-700)
                    float t = (dist - 350f) / 350f;
                    baseColor = Color.Lerp(ring1Color, ring2Color, t);
                    blend = t;
                }
                else
                {
                    // Blend ring2 → ring3 (700-1000)
                    float t = Mathf.Min((dist - 700f) / 300f, 1f);
                    baseColor = Color.Lerp(ring2Color, ring3Color, t);
                    blend = 1f;
                }

                // --- Perlin noise for organic detail ---
                float n1 = Mathf.PerlinNoise(x * 0.04f + seed * 0.01f, y * 0.04f + seed * 0.01f);
                float n2 = Mathf.PerlinNoise(x * 0.08f + seed * 0.1f + 100f, y * 0.08f + seed * 0.1f + 100f);
                float n3 = Mathf.PerlinNoise(x * 0.02f + seed * 0.2f + 200f, y * 0.02f + seed * 0.2f + 200f);

                // Variation from noise
                float variation = (n1 - 0.5f) * 0.25f + (n2 - 0.5f) * 0.12f;

                // Apply noise to base
                Color noisyBase = new Color(
                    Mathf.Clamp01(baseColor.r + variation),
                    Mathf.Clamp01(baseColor.g + variation * 0.8f),
                    Mathf.Clamp01(baseColor.b + variation * 0.6f),
                    1f
                );

                // --- Nation tint overlay (blend strength varies with noise for natural look) ---
                float tintStrength = 0.35f + n3 * 0.25f;

                // Also vary tint by distance from center (stronger tint near edges)
                float distFactor = Mathf.Clamp01(dist / 1000f);
                tintStrength *= (0.7f + distFactor * 0.3f);

                Color finalColor = Color.Lerp(noisyBase, nationTint, tintStrength);

                // Darken at very center (empire keeps golden)
                if (dist < 50f)
                {
                    float centerDarken = 1f - (1f - dist / 50f) * 0.15f;
                    finalColor *= centerDarken;
                }

                pixels[y * texSize + x] = finalColor;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
}