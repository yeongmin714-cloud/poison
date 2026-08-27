using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System;
using ProjectName.Core;

public static class FixMainScene
{
    // ================================================================
    // 0. Purge All DontDestroyOnLoad + Singletons + AutoCreates
    // ================================================================
    static void PurgeAllDontDestroyOnLoadAndSingletons()
    {
        var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();

        // 1. DontDestroyOnLoad 씬 직접 파괴
        var ddoScene = SceneManager.GetSceneByName("DontDestroyOnLoad");
        if (ddoScene.IsValid())
        {
            foreach (var go in ddoScene.GetRootGameObjects())
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }

        // 2. HideFlags.DontSave / DontDestroyOnLoad 플래그 가진 모든 오브젝트 파괴
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go != null && ((go.hideFlags & HideFlags.DontSave) != 0 || go.scene.name == "DontDestroyOnLoad"))
                UnityEngine.Object.DestroyImmediate(go);
        }

        // 3. 모든 알려진 싱글톤 정적 필드 강제 null (리플렉션)
        string[] singletonTypes = new[]
        {
            // Core
            "ProjectName.Core.GameManager",
            "ProjectName.Core.PlayerHealth",
            "ProjectName.Core.PlayerStats",
            "ProjectName.Core.PlayerInventory",
            "ProjectName.Core.PersistentManager",
            "ProjectName.Core.BuffManager",
            "ProjectName.Core.CameraShake",
            "ProjectName.Core.SoundManager",
            "ProjectName.Core.SoundManagerEnhanced",
            "ProjectName.Core.QuestManager",
            "ProjectName.Core.DropTableManager",
            "ProjectName.Core.TelegramNotifier",
            // Systems
            "ProjectName.Systems.TerritoryManager",
            "ProjectName.Systems.GuardManager",
            "ProjectName.Systems.MonsterSpawner",
            "ProjectName.Systems.DayNightCycle",
            "ProjectName.Systems.WeatherManager",
            "ProjectName.Systems.AmbientEffectManager",
            "ProjectName.Systems.AmbientDialogueManager",
            "ProjectName.Systems.EnvironmentParticleController",
            "ProjectName.Systems.DecalSpawner",
            "ProjectName.Systems.EmblemManager",
            "ProjectName.Systems.EncyclopediaManager",
            "ProjectName.Systems.EquipmentManager",
            "ProjectName.Systems.FadeManager",
            "ProjectName.Systems.BackgroundMusicManager",
            "ProjectName.Systems.BardBuffManager",
            "ProjectName.Systems.BackSlotSystem",
            "ProjectName.Systems.ControllerSupport",
            "ProjectName.Systems.CraftPresetManager",
            "ProjectName.Systems.AutoMissionManager",
            "ProjectName.Systems.ArenaSystem",
            "ProjectName.Systems.AssassinationCutscene",
            "ProjectName.Systems.Animation.Neural.BatchInferenceManager",
            "ProjectName.Systems.Animation.Neural.MLRuntimeManager",
            "ProjectName.Systems.Animation.Neural.ProgressiveRolloutManager",
            // UI
            "ProjectName.UI.UIManager",
            "ProjectName.UI.AchievementSystem",
            "ProjectName.UI.SettingsMenuUI",
            "ProjectName.UI.EscMenuUI",
            "ProjectName.UI.DeathScreenUI",
            "ProjectName.UI.LoadingScreenUI",
            "ProjectName.UI.MinimapUI",
            // Effects
            "ProjectName.Systems.DeathEffects",
            "ProjectName.Systems.PoisonVFX"
        };

        foreach (var typeName in singletonTypes)
        {
            foreach (var asm in assemblies)
            {
                var type = asm.GetType(typeName);
                if (type != null)
                {
                    var instField = type.GetField("Instance", flags);
                    if (instField != null) { instField.SetValue(null, null); continue; }
                    var privField = type.GetField("_instance", flags);
                    if (privField != null) { privField.SetValue(null, null); continue; }
                    var quitField = type.GetField("_instanceQuitting", flags);
                    if (quitField != null) { quitField.SetValue(null, false); }
                }
            }
        }

        // 4. RuntimeInitializeOnLoadMethod가 생성한 AutoCreate 즉시 파괴
        var autoNames = new[] { "PlayerHealth", "PlayerStats", "PlayerInventory" };
        foreach (var name in autoNames)
        {
            var go = GameObject.Find(name);
            if (go != null && go.scene.name == "DontDestroyOnLoad")
                DestroyImmediate(go);
        }

        Debug.Log("[FixMainScene] 🧹 Complete purge: DontDestroyOnLoad + Singletons + AutoCreates");
    }

    // ================================================================
    // Helper: Force register Player singletons to NEW instances
    // ================================================================
    static void ForceRegisterPlayerSingletons(GameObject player)
    {
        var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        var health = player.GetComponent<PlayerHealth>();
        if (health != null)
        {
            var field = typeof(PlayerHealth).GetField("Instance", flags);
            field?.SetValue(null, health);
        }

        var stats = player.GetComponent<PlayerStats>();
        if (stats != null)
        {
            var field = typeof(PlayerStats).GetField("Instance", flags);
            field?.SetValue(null, stats);
        }

        var inv = player.GetComponent<PlayerInventory>();
        if (inv != null)
        {
            var field = typeof(PlayerInventory).GetField("Instance", flags);
            field?.SetValue(null, inv);
        }

        Debug.Log("[FixMainScene] ✅ Player singletons force-registered to NEW instances");
    }

    [MenuItem("Tools/Poison/Fix MainScene")]
    public static void Fix()
    {
        // === 0단계: 기존 DontDestroyOnLoad + 싱글톤 + AutoCreate 완전 정화 ===
        PurgeAllDontDestroyOnLoadAndSingletons();

        // Create new scene
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "MainScene";

        // ================================================================
        // 0. Ensure "Player" layer exists (Critical for camera culling/physics)
        // ================================================================
        EnsurePlayerLayerExists();

        // ================================================================
        // 1. URP Pipeline Setup - create proper URP asset with renderer
        // ================================================================
        var urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/URP/URPAsset.asset");
        if (urpAsset == null)
        {
            urpAsset = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
            System.IO.Directory.CreateDirectory("Assets/URP");
            AssetDatabase.CreateAsset(urpAsset, "Assets/URP/URPAsset.asset");
        }

        // Create and assign UniversalRendererData if missing
        var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/URP/UniversalRendererData.asset");
        if (rendererData == null)
        {
            rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            rendererData.name = "UniversalRendererData";
            AssetDatabase.CreateAsset(rendererData, "Assets/URP/UniversalRendererData.asset");
        }

        // Assign renderer to URP asset via SerializedObject
        var so = new SerializedObject(urpAsset);
        var rendererList = so.FindProperty("m_RendererDataList");
        rendererList.arraySize = 1;
        rendererList.GetArrayElementAtIndex(0).objectReferenceValue = rendererData;
        so.FindProperty("m_DefaultRendererIndex").intValue = 0;
        so.ApplyModifiedProperties();

        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = urpAsset;
        QualitySettings.renderPipeline = urpAsset;

        // ================================================================
        // 2. Post-Processing Volume (BotW Style: Bloom + ColorGrading + Fog)
        // ================================================================
        CreatePostProcessingVolume();

        // ================================================================
        // 3. Heightmap Terrain Generation (2000x2000, seed=42)
        // ================================================================
        var ground = CreateHeightmapTerrain();

        // ================================================================
        // 4. Terrain GLB Models Placement (GPU Instancing, 3 Rings)
        // ================================================================
        // NEW: TerrainModelPlacer.Place() is now called inside CreateHeightmapTerrain()
        // PlaceTerrainModels(ground); // OLD - REMOVED: duplicate Environment creation

        // ================================================================
        // 5. Player Setup (Player_Rigged.glb + Full Animation Stack)
        // ================================================================
        var player = CreatePlayer();

        // === 핵심: Player 싱글톤 강제 재등록 ===
        ForceRegisterPlayerSingletons(player);

        // ================================================================
        // 6. Camera System - CORRECT Cinemachine 3.x (Shoulder View + Zoom)
        // ================================================================
        CreateCameraSystem(player);

        // ================================================================
        // 7. Lighting System (Directional Light + Moon Light)
        // ================================================================
        CreateLightingSystem();

        // ================================================================
        // 8. HUD (BotW Hearts), MinimapUI
        // ================================================================
        CreateHUDSystem();

        // ================================================================
        // 9. EventSystem (InputSystemUIInputModule)
        // ================================================================
        CreateEventSystem();

        // ================================================================
        // 10. Core Game Systems
        // ================================================================
        CreateCoreGameSystems();

        // ================================================================
        // 11. Environment Systems
        // ================================================================
        InitializeEnvironmentSystems();

        // ================================================================
        // 12. MonsterSpawner (Territory Difficulty Based)
        // ================================================================
        var spawnerObj = new GameObject("MonsterSpawner");
        spawnerObj.AddComponent<ProjectName.Systems.MonsterSpawner>();

        // ================================================================
        // 13. MountSystem (Disabled - Horse_Rigged.glb not provided)
        // ================================================================
        var mountSysObj = new GameObject("MountSystem");
        var mountSys = mountSysObj.AddComponent<ProjectName.Systems.MountSystem>();
        mountSys.enabled = false;

        // ================================================================
        // 14. NeuralModelAutoSetup (Editor script, no namespace)
        // ================================================================
        NeuralModelAutoSetup.AutoSetupModelDatabase();

        // ================================================================
        // 15. SpecialCreatureAnimator Stub (if missing)
        // ================================================================
        CreateSpecialCreatureAnimatorStub();

        // ================================================================
        // 16. Save scene
        // ================================================================
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/MainScene.unity");
        AssetDatabase.SaveAssets();

        // === Editor 강제 리로드 (배치모드 아닐 때만) ===
#if UNITY_EDITOR
        if (!Application.isBatchMode)
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity", OpenSceneMode.Single);
            EditorApplication.RepaintHierarchyWindow();
            Debug.Log("[FixMainScene] 🔁 Editor scene force-reloaded");
        }
#endif

        Debug.Log("MainScene fixed and saved with BotW-style setup!");
    }

    // ================================================================
        // Post-Processing Volume (BotW Style)
        // ================================================================
        static void CreatePostProcessingVolume()
        {
            var volumeObj = new GameObject("GlobalVolume");
            var volume = volumeObj.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100;

            // Create and save profile asset FIRST, then load and assign
            System.IO.Directory.CreateDirectory("Assets/URP");
            string profilePath = "Assets/URP/GlobalVolumeProfile.asset";

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Reload from asset database to get proper instance ID
            profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);

            volume.profile = profile;
            volume.sharedProfile = profile; // Ensure both are set

            // Create all components as sub-assets and add to profile via SerializedObject
            var so = new SerializedObject(profile);
            var componentsProp = so.FindProperty("components");

            // Create all components first
            var bloom = ScriptableObject.CreateInstance<Bloom>();
            bloom.name = "Bloom";
            bloom.active = true;
            bloom.intensity.Override(0.3f);
            bloom.threshold.Override(1.0f);
            bloom.scatter.Override(0.7f);
            bloom.tint.Override(new Color(1f, 0.95f, 0.85f));
            AssetDatabase.AddObjectToAsset(bloom, profile);

            var colorAdjustments = ScriptableObject.CreateInstance<ColorAdjustments>();
            colorAdjustments.name = "ColorAdjustments";
            colorAdjustments.active = true;
            colorAdjustments.postExposure.Override(0.2f);
            colorAdjustments.contrast.Override(10f);
            colorAdjustments.colorFilter.Override(new Color(1f, 0.95f, 0.85f));
            colorAdjustments.hueShift.Override(0f);
            colorAdjustments.saturation.Override(15f);
            AssetDatabase.AddObjectToAsset(colorAdjustments, profile);

            var tonemapping = ScriptableObject.CreateInstance<Tonemapping>();
            tonemapping.name = "Tonemapping";
            tonemapping.active = true;
            tonemapping.mode.Override(TonemappingMode.ACES);
            AssetDatabase.AddObjectToAsset(tonemapping, profile);

            var vignette = ScriptableObject.CreateInstance<Vignette>();
            vignette.name = "Vignette";
            vignette.active = true;
            vignette.intensity.Override(0.15f);
            vignette.smoothness.Override(0.4f);
            AssetDatabase.AddObjectToAsset(vignette, profile);

            var liftGammaGain = ScriptableObject.CreateInstance<LiftGammaGain>();
            liftGammaGain.name = "LiftGammaGain";
            liftGammaGain.active = true;
            liftGammaGain.gamma.Override(new Vector4(1.05f, 1.02f, 0.98f, 1f));
            AssetDatabase.AddObjectToAsset(liftGammaGain, profile);

            // Now add them to the components array
            componentsProp.arraySize = 5;
            componentsProp.GetArrayElementAtIndex(0).objectReferenceValue = bloom;
            componentsProp.GetArrayElementAtIndex(1).objectReferenceValue = colorAdjustments;
            componentsProp.GetArrayElementAtIndex(2).objectReferenceValue = tonemapping;
            componentsProp.GetArrayElementAtIndex(3).objectReferenceValue = vignette;
            componentsProp.GetArrayElementAtIndex(4).objectReferenceValue = liftGammaGain;

            // Apply modified properties to ensure components are serialized
            so.ApplyModifiedProperties();

            // Force save the profile with components
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Reload profile to ensure components are loaded
            profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/URP/GlobalVolumeProfile.asset");

            volume.profile = profile;
            volume.sharedProfile = profile;

            EditorUtility.SetDirty(volumeObj);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

    // ================================================================
    // Heightmap Terrain (2000x2000, seed=42)
    // ================================================================
    static GameObject CreateHeightmapTerrain()
    {
        var biome = ProjectName.Core.Data.BiomeType.Plains;
        var (terrainMesh, waterMesh) = ProjectName.Systems.TerrainGenerator.GenerateTerrain(biome, 42, 100, 2000f);

        var ground = new GameObject("Ground_Inner");
        ground.layer = LayerMask.NameToLayer("Ground");

        var mf = ground.AddComponent<MeshFilter>();
        mf.sharedMesh = terrainMesh;

        var mr = ground.AddComponent<MeshRenderer>();

        // CRITICAL: Create and assign procedural textures so terrain is visible in Editor/PlayMode
                // before NationTerrainController generates runtime textures
                // Step 1: Create and save textures FIRST
                var controlMap = CreateProceduralControlMap(256);
                var grassTex = CreateProceduralGrassTexture(256);
                var dirtTex = CreateProceduralDirtTexture(256);
                var normalTex = CreateProceduralNormalTexture(256);

                AssetDatabase.CreateAsset(controlMap, "Assets/URP/Terrain_ControlMap.asset");
                AssetDatabase.CreateAsset(grassTex, "Assets/URP/Terrain_Grass.asset");
                AssetDatabase.CreateAsset(dirtTex, "Assets/URP/Terrain_Dirt.asset");
                AssetDatabase.CreateAsset(normalTex, "Assets/URP/Terrain_Normal.asset");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // Step 2: Reload textures from asset database
                controlMap = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/URP/Terrain_ControlMap.asset");
                grassTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/URP/Terrain_Grass.asset");
                dirtTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/URP/Terrain_Dirt.asset");
                normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/URP/Terrain_Normal.asset");

                // Step 3: Create material and assign textures
                var groundMat = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
                if (groundMat == null) groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                groundMat.name = "Ground_Grass_Mat";

                groundMat.SetTexture("_Control", controlMap);
                groundMat.SetTexture("_Splat0", grassTex);
                groundMat.SetTexture("_Splat1", dirtTex);
                groundMat.SetTexture("_Normal0", normalTex);
                groundMat.SetFloat("_Splat0TileSize", 10f);
                groundMat.SetFloat("_Splat1TileSize", 10f);
                groundMat.SetFloat("_NumLayersCount", 2f); // 2 layers: grass + dirt

                // Save material as asset for proper keyword persistence
                        AssetDatabase.CreateAsset(groundMat, "Assets/URP/Ground_Grass_Mat.mat");
                        AssetDatabase.SaveAssets();
                        groundMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/URP/Ground_Grass_Mat.mat");

                        // Force keyword persistence - use shaderKeywords API (works in Unity 2021+)
                        var keywords = new List<string>(groundMat.shaderKeywords);
                        if (!keywords.Contains("_TERRAIN_NORMAL_MAP"))
                        {
                            keywords.Add("_TERRAIN_NORMAL_MAP");
                            groundMat.shaderKeywords = keywords.ToArray();
                        }
                        groundMat.EnableKeyword("_TERRAIN_NORMAL_MAP");
                        EditorUtility.SetDirty(groundMat);
                        AssetDatabase.SaveAssetIfDirty(groundMat);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();
        
                        // Force reimport to ensure keyword serialization
                        AssetDatabase.ImportAsset("Assets/URP/Ground_Grass_Mat.mat", ImportAssetOptions.ForceUpdate);

                mr.sharedMaterial = groundMat;
                EditorUtility.SetDirty(groundMat);

        // MeshCollider for physics
        var mc = ground.AddComponent<MeshCollider>();
        mc.sharedMesh = terrainMesh;

        // Water mesh if generated
        if (waterMesh != null)
        {
            var waterObj = new GameObject("Water");
            waterObj.transform.SetParent(ground.transform);
            var wmf = waterObj.AddComponent<MeshFilter>();
            wmf.sharedMesh = waterMesh;
            var wmr = waterObj.AddComponent<MeshRenderer>();
            var waterMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            waterMat.color = new Color(0.1f, 0.3f, 0.6f, 0.5f);
            waterMat.SetFloat("_Surface", 1.0f);
            waterMat.SetFloat("_Blend", 0.0f);
            waterMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            waterMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            waterMat.SetInt("_ZWrite", 0);
            waterMat.renderQueue = 3000;
            waterMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            wmr.sharedMaterial = waterMat;
        }

        // TerrainTextureApplier + NationTerrainController
        ground.AddComponent<ProjectName.Systems.TerrainTextureApplier>();
        ground.AddComponent<ProjectName.Systems.NationTerrainController>();

        // NEW: TerrainModelPlacer로 GLB 환경 모델 배치 (GPU Instancing + 3링 + 국가별)
        ProjectName.Systems.TerrainModelPlacer.Place(ground);

        // NEW: 물 시스템 연동 (LakeGenerator + WaterBody) - 저지대 자동 물 메시 생성
        CreateWaterSystem(ground);

        return ground;
    }

    // ================================================================
    // Terrain GLB Models Placement (GPU Instancing, 3 Rings)
    // ================================================================
    static void PlaceTerrainModels(GameObject ground)
    {
        var envParent = new GameObject("Environment");
        envParent.transform.SetParent(ground.transform);

        // Try GPU Instancing placer first
        var placerType = System.Type.GetType("ProjectName.Systems.EnvironmentModelPlacer, Assembly-CSharp");
        if (placerType != null)
        {
            var placer = envParent.AddComponent(placerType);
            var setupMethod = placerType.GetMethod("SetupAndPlace");
            if (setupMethod != null)
            {
                setupMethod.Invoke(placer, new object[] { ground });
            }
        }
        else
        {
            // Fallback: Simple instancing placement
            PlaceModelsWithInstancing(envParent, ground);
        }
    }

    static void PlaceModelsWithInstancing(GameObject parent, GameObject ground)
    {
        // Load GLB models from Resources
        var grassModels = Resources.LoadAll<GameObject>("Models/UserProvided/terrain/grass");
        var rockModels = Resources.LoadAll<GameObject>("Models/UserProvided/terrain/rocks");
        var treeModels = Resources.LoadAll<GameObject>("Models/UserProvided/terrain/trees");

        if (grassModels.Length == 0 || rockModels.Length == 0 || treeModels.Length == 0)
        {
            Debug.LogWarning("[FixMainScene] GLB terrain models not found in Resources/Models/UserProvided/terrain/");
            return;
        }

        var groundCollider = ground.GetComponent<MeshCollider>();
        if (groundCollider == null) return;

        // Ring 1 (0-50m): Dense grass + small rocks
        PlaceModelsInRingInstanced(parent, groundCollider, grassModels, 0f, 50f, 500, 0.05f, 0.2f);
        PlaceModelsInRingInstanced(parent, groundCollider, rockModels, 0f, 50f, 100, 0f, 0.1f);

        // Ring 2 (50-150m): Grass + trees + rocks
        PlaceModelsInRingInstanced(parent, groundCollider, grassModels, 50f, 150f, 300, 0.05f, 0.2f);
        PlaceModelsInRingInstanced(parent, groundCollider, treeModels, 50f, 150f, 80, 0f, 0f);
        PlaceModelsInRingInstanced(parent, groundCollider, rockModels, 50f, 150f, 60, 0f, 0.1f);

        // Ring 3 (150-300m): Sparse grass + large trees + large rocks
        PlaceModelsInRingInstanced(parent, groundCollider, grassModels, 150f, 300f, 150, 0.05f, 0.2f);
        PlaceModelsInRingInstanced(parent, groundCollider, treeModels, 150f, 300f, 50, 0f, 0f);
        PlaceModelsInRingInstanced(parent, groundCollider, rockModels, 150f, 300f, 40, 0f, 0.1f);
    }

    static void PlaceModelsInRingInstanced(GameObject parent, MeshCollider groundCollider, GameObject[] models,
        float innerR, float outerR, int count, float yMinOffset, float yMaxOffset)
    {
        for (int i = 0; i < count; i++)
        {
            for (int attempts = 0; attempts < 5; attempts++)
            {
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float radius = Random.Range(innerR, outerR);
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;

                var ray = new Ray(new Vector3(x, 1000f, z), Vector3.down);
                if (groundCollider.Raycast(ray, out var hit, 2000f))
                {
                    var go = UnityEngine.Object.Instantiate(models[Random.Range(0, models.Length)], parent.transform);
                    go.transform.position = new Vector3(x, hit.point.y + Random.Range(yMinOffset, yMaxOffset), z);
                    go.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                    go.transform.localScale *= Random.Range(0.8f, 1.2f);

                    // Enable GPU Instancing on materials
                    var renderers = go.GetComponentsInChildren<Renderer>();
                    foreach (var r in renderers)
                    {
                        if (r.sharedMaterial != null)
                        {
                            r.sharedMaterial.enableInstancing = true;
                        }
                    }
                    return;
                }
            }
        }
    }

    // ================================================================
    // Player with GLB + Full Animation Stack (Neural + Procedural + Hybrid)
    // ================================================================
    static GameObject CreatePlayer()
    {
        var player = new GameObject("Player");
        player.tag = "Player";
        player.layer = LayerMask.NameToLayer("Player"); // Ensure Player layer
        player.transform.position = new Vector3(0, 2, 0);

        var controller = player.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.4f;
        controller.center = new Vector3(0, 0.9f, 0);

        // Core components
        player.AddComponent<ProjectName.Systems.PlayerMovement>();
        player.AddComponent<ProjectName.Core.PlayerHealth>();
        player.AddComponent<ProjectName.Core.PlayerStats>();
        player.AddComponent<ProjectName.Core.PlayerInventory>();
        player.AddComponent<ProjectName.Systems.PlayerCombat>();
        player.AddComponent<ProjectName.Systems.BombThrower>();
        player.AddComponent<ProjectName.Core.BuffManager>();

                // PlayerInput (Editor-time) - use helper for safe actions assignment
                var inputActions = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/Resources/Input/PlayerControls.inputactions");
                if (inputActions == null)
                {
                    Debug.LogError("[FixMainScene] Input actions asset not found!");
                }
                else
                {
                    // Try helper first
                    System.Type helperType = System.Type.GetType("ProjectName.Core.PlayerInputHelper, ProjectName.Core");
                    if (helperType == null)
                    {
                        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                        {
                            helperType = asm.GetType("ProjectName.Core.PlayerInputHelper");
                            if (helperType != null) break;
                        }
                    }

                    bool setupDone = false;
                    if (helperType != null)
                    {
                        var setupMethod = helperType.GetMethod("SetupPlayerInput", new[] { typeof(GameObject), typeof(UnityEngine.InputSystem.InputActionAsset) });
                        if (setupMethod != null)
                        {
                            try
                            {
                                setupMethod.Invoke(null, new object[] { player, inputActions });
                                setupDone = true;
                                Debug.Log($"[FixMainScene] PlayerInput setup invoked successfully");
                            }
                            catch (System.Exception e)
                            {
                                Debug.LogError($"[FixMainScene] PlayerInput setup failed: {e}");
                            }
                        }
                    }

                    // Fallback: Ensure PlayerInput component exists
                    if (!setupDone)
                    {
                        var pi = player.GetComponent<UnityEngine.InputSystem.PlayerInput>() ?? player.AddComponent<UnityEngine.InputSystem.PlayerInput>();
                        pi.actions = inputActions;
                        pi.defaultActionMap = "Player";
                        pi.notificationBehavior = UnityEngine.InputSystem.PlayerNotifications.InvokeUnityEvents;
                        Debug.Log("[FixMainScene] PlayerInput added via fallback");
                    }
                }

                // Player GLB Model
        var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Models/UserProvided/Player_Rigged.glb");
        if (modelPrefab != null)
        {
            // GLB is a Model Asset (not Prefab), use Object.Instantiate and ensure scene persistence
            var modelInstance = (GameObject)Object.Instantiate(modelPrefab, player.transform);
            modelInstance.name = "PlayerModel";
            modelInstance.transform.localPosition = new Vector3(0, 0.9f, 0);
            modelInstance.transform.localScale = Vector3.one;
            
            // CRITICAL: Set PlayerModel AND all children to Player layer (8) for camera culling
            SetLayerRecursive(modelInstance, LayerMask.NameToLayer("Player"));
            
            // CRITICAL: Mark as dirty for scene persistence (since not a Prefab)
            EditorUtility.SetDirty(modelInstance);
            foreach (Transform child in modelInstance.transform)
            {
                EditorUtility.SetDirty(child.gameObject);
            }
            
            Debug.Log($"[FixMainScene] Created PlayerModel: {modelInstance.name}, parent: {modelInstance.transform.parent?.name}, active: {modelInstance.activeInHierarchy}");

            // Remove Rigidbody/Animator from GLB (duplicate components cause issues)
            var rbs = modelInstance.GetComponentsInChildren<Rigidbody>();
            foreach (var rb in rbs) UnityEngine.Object.DestroyImmediate(rb);
            var anims = modelInstance.GetComponentsInChildren<Animator>();
            foreach (var anim in anims) UnityEngine.Object.DestroyImmediate(anim);

            // CRITICAL: Force SkinnedMeshRenderer bounds recalculation (zero AABB causes culling)
            var skinnedRenderers = modelInstance.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var smr in skinnedRenderers)
            {
                smr.enabled = true;
                smr.updateWhenOffscreen = true;
                // Force bounds recalculation
                smr.localBounds = new Bounds(Vector3.zero, new Vector3(2f, 4f, 2f));
            }

            // Also ensure MeshRenderers are enabled
            var meshRenderers = modelInstance.GetComponentsInChildren<MeshRenderer>();
            foreach (var mr in meshRenderers)
            {
                mr.enabled = true;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                mr.receiveShadows = true;
            }

            // Add ModelAnimatorAssigner for full animation stack
            var assigner = modelInstance.AddComponent<ProjectName.Systems.Animation.ModelAnimatorAssigner>();
            // FORCE BIPED for Player (GLB may import as Generic initially)
            assigner.ForceBiped(true);
            // Call public Setup method after adding (ForceBiped already calls SetupAnimationSystem)
            // assigner.SetupAnimationSystem();
            Debug.Log($"[FixMainScene] PlayerModel setup complete with {skinnedRenderers.Length} skinned renderers, {meshRenderers.Length} mesh renderers");
        }
        else
        {
            Debug.LogWarning("[FixMainScene] Player_Rigged.glb not found, using cube proxy");
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "PlayerModel";
            cube.transform.SetParent(player.transform);
            cube.transform.localPosition = new Vector3(0, 0.9f, 0);
            cube.transform.localScale = new Vector3(0.8f, 1.8f, 0.8f);
            cube.layer = LayerMask.NameToLayer("Player"); // Ensure Player layer
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = Color.blue;
            cube.GetComponent<MeshRenderer>().sharedMaterial = mat;
            UnityEngine.Object.DestroyImmediate(cube.GetComponent<BoxCollider>());
        }

        // Cleanup duplicate components on player root
        CleanupDuplicateComponents(player);

        // AudioListener on player
        player.AddComponent<AudioListener>();

        return player;
    }

    static void CleanupDuplicateComponents(GameObject player)
    {
        // Remove duplicate Rigidbodies
        var rbs = player.GetComponents<Rigidbody>();
        for (int i = 1; i < rbs.Length; i++) UnityEngine.Object.DestroyImmediate(rbs[i]);

        // Remove Animators from root
        var anims = player.GetComponents<Animator>();
        foreach (var anim in anims) UnityEngine.Object.DestroyImmediate(anim);

        // Ensure PlayerInput has actions using helper via reflection
        var input = player.GetComponent<UnityEngine.InputSystem.PlayerInput>();
        if (input != null && input.actions == null)
        {
            System.Type helperType = System.Type.GetType("ProjectName.Core.PlayerInputHelper, ProjectName.Core");
            if (helperType != null)
            {
                var setupMethod = helperType.GetMethod("SetupPlayerInput", new[] { typeof(GameObject), typeof(UnityEngine.InputSystem.InputActionAsset) });
                if (setupMethod != null)
                {
                    setupMethod.Invoke(null, new object[] { input.gameObject, AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/Resources/Input/PlayerControls.inputactions") });
                }
            }
            else
            {
                Debug.LogError("[FixMainScene] PlayerInputHelper not found!");
            }
        }
    }

    // ================================================================
        static void CreateCameraSystem(GameObject player)
        {
            // Main Camera (rendering camera) with CinemachineBrain
            var mainCamObj = new GameObject("Main Camera");
            mainCamObj.tag = "MainCamera";
            var mainCam = mainCamObj.AddComponent<Camera>();
            mainCam.clearFlags = CameraClearFlags.Skybox;
            mainCam.nearClipPlane = 0.1f;
            mainCam.farClipPlane = 1000f;
            mainCam.cullingMask = -1; // Render all layers
            var cmBrain = mainCamObj.AddComponent<CinemachineBrain>();
            cmBrain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);

            // AudioListener on Main Camera
            mainCamObj.AddComponent<AudioListener>();

            // Player Camera (Virtual Camera) - SEPARATE GameObject, NOT child of Main Camera or Player
            var vcamObj = new GameObject("Player Camera");
            // Ensure it's a root object (no parent)
            vcamObj.transform.SetParent(null);
            // Position at player shoulder level initially
            vcamObj.transform.position = player.transform.position + new Vector3(2.5f, 3f, -5f);
            vcamObj.transform.rotation = Quaternion.Euler(15, 0, 0);

            var cmCam = vcamObj.AddComponent<CinemachineCamera>();
            cmCam.Follow = player.transform;
            cmCam.LookAt = player.transform;
            cmCam.Priority = 100;

            // Third Person Follow (Cinemachine 3.x) - BotW style shoulder camera
            var tpFollow = vcamObj.AddComponent<CinemachineThirdPersonFollow>();
            tpFollow.CameraDistance = 25f;        // 25m distance as requested
            tpFollow.VerticalArmLength = 8f;      // Height offset
            tpFollow.ShoulderOffset = new Vector3(2.5f, 0f, 0f); // Right shoulder
            tpFollow.CameraSide = 1;              // Right side (1 = right, -1 = left)
            tpFollow.Damping = new Vector3(1f, 0.5f, 1f); // X, Y, Z damping

            // Input Axis Controller for mouse orbit (Cinemachine 3.x) - uses legacy input by default
            var inputAxis = vcamObj.AddComponent<CinemachineInputAxisController>();

            // Add runtime zoom controller (not Editor-only)
            vcamObj.AddComponent<ProjectName.Systems.CameraZoomControllerRuntime>();
        }

    // ================================================================
    // Lighting System (Directional Light + Moon Light)
    // ================================================================
    static void CreateLightingSystem()
    {
        // Directional Light (Sun)
        var sunObj = new GameObject("Directional Light (Sun)");
        var sun = sunObj.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.2f;
        sun.color = new Color(1f, 0.95f, 0.85f);
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 1f;
        sun.shadowResolution = UnityEngine.Rendering.LightShadowResolution.VeryHigh;
        sun.shadowBias = 0.05f;
        sun.shadowNormalBias = 0.4f;
        sun.shadowNearPlane = 0.1f;
        sunObj.transform.rotation = Quaternion.Euler(50, -30, 0);

        // Moon Light (for night)
        var moonObj = new GameObject("Directional Light (Moon)");
        var moon = moonObj.AddComponent<Light>();
        moon.type = LightType.Directional;
        moon.intensity = 0.15f;
        moon.color = new Color(0.6f, 0.65f, 0.8f);
        moon.shadows = LightShadows.Soft;
        moon.shadowStrength = 0.5f;
        moon.shadowResolution = UnityEngine.Rendering.LightShadowResolution.Medium;
        moonObj.transform.rotation = Quaternion.Euler(-50, 150, 0);
        moon.enabled = false;

        // Connect to DayNightCycle
        var dnc = Object.FindFirstObjectByType<ProjectName.Systems.DayNightCycle>();
        if (dnc != null)
        {
            var so = new SerializedObject(dnc);
            so.FindProperty("_sunLight").objectReferenceValue = sun;
            so.FindProperty("_moonLight").objectReferenceValue = moon;
            so.ApplyModifiedProperties();
        }

        // Skybox
        RenderSettings.skybox = new Material(Shader.Find("Skybox/Procedural"));
        RenderSettings.sun = sun;
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 1f;

        // Fog (RenderSettings since URP 17 removed Volume Fog)
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.0008f;
        RenderSettings.fogColor = new Color(0.6f, 0.7f, 0.85f, 1f);
    }

    // ================================================================
    // HUD System (BotW Hearts + Minimap)
    // ================================================================
    static void CreateHUDSystem()
    {
        var hudObj = new GameObject("HUD");
        hudObj.AddComponent<ProjectName.UI.HUD>();

        var mmObj = new GameObject("MinimapUI");
        mmObj.AddComponent<ProjectName.UI.MinimapUI>();
    }

    // ================================================================
    // EventSystem
    // ================================================================
    static void CreateEventSystem()
    {
        var esObj = new GameObject("EventSystem");
        esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
        esObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    // ================================================================
    // Core Game Systems
    // ================================================================
    static void CreateCoreGameSystems()
    {
        var setupObj = new GameObject("GameSetup");
        setupObj.AddComponent<GameSetup>();

        var loadObj = new GameObject("LoadingManager");
        loadObj.AddComponent<ProjectName.Systems.LoadingManager>();

        var territoryObj = new GameObject("TerritoryManager");
        territoryObj.AddComponent<ProjectName.Systems.TerritoryManager>();

        var guardMgrObj = new GameObject("GuardManager");
        guardMgrObj.AddComponent<ProjectName.Systems.GuardManager>();

        ProjectName.Core.QuestManager.Initialize();

        var cpmObj = new GameObject("CraftPresetManager");
        cpmObj.AddComponent<ProjectName.Systems.CraftPresetManager>();
    }

    // ================================================================
    // Environment Systems
    // ================================================================
    static void InitializeEnvironmentSystems()
    {
        var timeObj = new GameObject("TimeManager");
        timeObj.AddComponent<ProjectName.Systems.TimeManager>();

        var dncObj = new GameObject("DayNightCycle");
        dncObj.AddComponent<ProjectName.Systems.DayNightCycle>();

        var wmObj = new GameObject("WeatherManager");
        wmObj.AddComponent<ProjectName.Systems.WeatherManager>();

        var wpcObj = new GameObject("WeatherParticleController");
        wpcObj.AddComponent<ProjectName.Systems.WeatherParticleController>();

        var bgmObj = new GameObject("RegionBGMController");
        bgmObj.AddComponent<ProjectName.Systems.RegionBGMController>();

        var epcObj = new GameObject("EnvironmentParticleController");
        epcObj.AddComponent<ProjectName.Systems.EnvironmentParticleController>();

        var aemObj = new GameObject("AmbientEffectManager");
        aemObj.AddComponent<ProjectName.Systems.AmbientEffectManager>();

        var starObj = new GameObject("StarField");
        starObj.AddComponent<ProjectName.Systems.StarField>();

        var dsObj = new GameObject("DecalSpawner");
        dsObj.AddComponent<ProjectName.Systems.DecalSpawner>();
        var dsiObj = new GameObject("DecalSpawnerIntegration");
        dsiObj.AddComponent<ProjectName.Systems.DecalSpawnerIntegration>();

        var secObj = new GameObject("SpecialEffectsController");
        secObj.AddComponent<ProjectName.Systems.SpecialEffectsController>();

        var wemObj = new GameObject("WorldEventManager");
        wemObj.AddComponent<ProjectName.Systems.WorldEventManager>();

        var twmObj = new GameObject("TerritoryWarManager");
        twmObj.AddComponent<ProjectName.Systems.TerritoryWarManager>();

        var ammObj = new GameObject("AutoMoveManager");
        ammObj.AddComponent<ProjectName.Systems.AutoMoveManager>();

        var ftsObj = new GameObject("FastTravelSystem");
        ftsObj.AddComponent<ProjectName.Systems.FastTravelSystem>();

        var ssObj = new GameObject("StealthSystem");
        ssObj.AddComponent<ProjectName.Systems.StealthSystem>();

        var smeObj = new GameObject("SoundManagerEnhanced");
        smeObj.AddComponent<ProjectName.Systems.SoundManagerEnhanced>();

        var tempObj = new GameObject("TemperatureSystem");
        tempObj.AddComponent<ProjectName.Systems.TemperatureSystem>();

        var soundObj = new GameObject("SoundSystem");
        soundObj.AddComponent<ProjectName.Systems.SoundSystem>();
    }

    // ================================================================
    // NeuralModelAutoSetup & SpecialCreatureAnimator Stub
    // ================================================================
    static void CreateSpecialCreatureAnimatorStub()
    {
        var stubPath = "Assets/Scripts/Systems/Animation/Procedural/SpecialCreatureAnimator.cs";
        if (!System.IO.File.Exists(stubPath))
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(stubPath));
            var stubContent = @"using UnityEngine;
using ProjectName.Systems.Animation.Procedural.Bones;
using ProjectName.Systems.Animation.Procedural.IK;
using static ProjectName.Systems.Animation.Procedural.IK.LimbIKSolver;

namespace ProjectName.Systems.Animation.Procedural
{
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(ProceduralBoneMap))]
    public class SpecialCreatureAnimator : MonoBehaviour
    {
        public enum CreatureType { Spider, Clam, Slime, Spirit, LargeMonster }

        [Header(""Creature Type"")]
        public CreatureType creatureType = CreatureType.Spider;

        [Header(""Locomotion"")]
        [SerializeField] float _moveSpeed = 3f;
        [SerializeField] float _turnSpeed = 360f;

        Animator _animator;
        ProceduralBoneMap _boneMap;
        Rigidbody _rigidbody;

        void Awake()
        {
            _animator = GetComponent<Animator>();
            _boneMap = GetComponent<ProceduralBoneMap>();
            _rigidbody = GetComponent<Rigidbody>();
            _animator.applyRootMotion = false;
            _animator.updateMode = AnimatorUpdateMode.Fixed;
            _animator.animatePhysics = true;
            _boneMap.Initialize(_animator);
        }

        void Update()
        {
        }
    }
}";
            System.IO.File.WriteAllText(stubPath, stubContent);
            AssetDatabase.ImportAsset(stubPath);
        }
    }

    // ================================================================
    // Validation
    // ================================================================
    [MenuItem("Tools/Poison/Verify MainScene")]
    public static void Verify()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity", OpenSceneMode.Single);
        Debug.Log($"Scene: {scene.name}");

        var player = GameObject.Find("Player");
        if (player != null)
        {
            Debug.Log("Player found!");
            foreach (var c in player.GetComponents<Component>())
                Debug.Log($"  - {c.GetType().Name}");
            var model = player.transform.Find("PlayerModel");
            if (model != null)
            {
                Debug.Log("PlayerModel found!");
                foreach (var c in model.GetComponents<Component>())
                    Debug.Log($"    - {c.GetType().Name}");
            }
        }

        var mainCam = GameObject.Find("Main Camera");
        if (mainCam != null)
        {
            Debug.Log("Main Camera found!");
            foreach (var c in mainCam.GetComponents<Component>())
                Debug.Log($"  - {c.GetType().Name}");
            var vcam = mainCam.transform.Find("Player Camera");
            if (vcam != null)
            {
                Debug.Log("Player Camera found!");
                var vcamComponents = vcam.GetComponents<Component>();
                if (vcamComponents != null)
                {
                    foreach (var c in vcamComponents)
                    {
                        if (c != null)
                            Debug.Log($"    - {c.GetType().Name}");
                    }
                }
            }
        }

        var urp = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        if (urp != null)
        {
            try
            {
                var so = new SerializedObject(urp);
                var list = so.FindProperty("m_RendererDataList");
                Debug.Log($"URP Asset: {urp.name}, Renderers: {list.arraySize}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"URP Asset check failed: {e.Message}");
            }
        }

        var db = Resources.Load<ProjectName.Systems.Animation.Neural.NeuralModelDatabase>("NeuralModelDatabase");
        if (db != null)
            Debug.Log($"NeuralModelDatabase: {db.Count} policies registered");
        else
            Debug.LogWarning("NeuralModelDatabase not found!");

        // Save the scene
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/MainScene.unity");
        AssetDatabase.SaveAssets();
        Debug.Log("[FixMainScene] Scene saved successfully!");

        EditorApplication.Exit(0);
    }

    // ================================================================
    // 0. Ensure "Player" layer exists (Critical for camera culling/physics)
    // ================================================================
    static void EnsurePlayerLayerExists()
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer == -1)
        {
            // Find first empty layer slot (8-31)
            var tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
            var so = new SerializedObject(tagManager);
            var layers = so.FindProperty("layers");
            
            for (int i = 8; i < 32; i++)
            {
                var layerProp = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(layerProp.stringValue))
                {
                    layerProp.stringValue = "Player";
                    so.ApplyModifiedProperties();
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[FixMainScene] Created 'Player' layer at index {i}");
                    return;
                }
            }
            Debug.LogError("[FixMainScene] No empty layer slot available for 'Player' layer!");
        }
        else
        {
            Debug.Log($"[FixMainScene] 'Player' layer already exists at index {playerLayer}");
        }
    }

    // ================================================================
    // Helper: Recursively set layer on GameObject and all children
    // ================================================================
    static void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursive(child.gameObject, layer);
        }
    }

    // ================================================================
    // Helper: Procedural texture generation for terrain
    // ================================================================
    static Texture2D CreateProceduralControlMap(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        tex.name = "Terrain_ControlMap";
        var pixels = new Color[size * size];
        // R channel = Splat0 (grass), G channel = Splat1 (dirt)
        // Fill with grass (R=1) mostly, some dirt (G=1) at edges
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = x / (float)size;
                float ny = y / (float)size;
                // Center is grass, edges have some dirt
                float distFromCenter = Mathf.Max(Mathf.Abs(nx - 0.5f), Mathf.Abs(ny - 0.5f)) * 2f;
                float grass = Mathf.Clamp01(1f - distFromCenter * 0.5f);
                float dirt = 1f - grass;
                pixels[y * size + x] = new Color(grass, dirt, 0, 0);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    static Texture2D CreateProceduralGrassTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        tex.name = "Terrain_Grass";
        var pixels = new Color[size * size];
        System.Random rng = new System.Random(42);
        for (int i = 0; i < pixels.Length; i++)
        {
            // Green grass with variation
            float v = 0.3f + (float)rng.NextDouble() * 0.15f;
            pixels[i] = new Color(v * 0.6f, v, v * 0.4f, 1f);
        }
        tex.SetPixels(pixels);
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;
        return tex;
    }

    static Texture2D CreateProceduralDirtTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        tex.name = "Terrain_Dirt";
        var pixels = new Color[size * size];
        System.Random rng = new System.Random(43);
        for (int i = 0; i < pixels.Length; i++)
        {
            // Brown dirt with variation
            float v = 0.25f + (float)rng.NextDouble() * 0.1f;
            pixels[i] = new Color(v * 1.2f, v * 0.9f, v * 0.6f, 1f);
        }
        tex.SetPixels(pixels);
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;
        return tex;
    }

    static Texture2D CreateProceduralNormalTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        tex.name = "Terrain_Normal";
        var pixels = new Color[size * size];
        // Flat normal (pointing up)
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color(0.5f, 0.5f, 1f, 1f); // Normal map: (0,0,1) in tangent space
        }
        tex.SetPixels(pixels);
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;
        return tex;
    }

    // ================================================================
    // NEW: Water System (LakeGenerator + WaterBody) - 저지대 자동 물 메시 생성
    // ================================================================
    static void CreateWaterSystem(GameObject ground)
    {
        var lakeGenType = System.Type.GetType("ProjectName.Systems.LakeGenerator, Assembly-CSharp");
        if (lakeGenType != null)
        {
            var lakeGen = ground.AddComponent(lakeGenType);
            var genMethod = lakeGenType.GetMethod("GenerateLakes");
            if (genMethod != null)
            {
                genMethod.Invoke(lakeGen, new object[] { ground });
                Debug.Log("[FixMainScene] LakeGenerator.GenerateLakes() invoked");
            }
        }
        else
        {
            // Fallback: Simple water planes at low elevation
            CreateSimpleWaterPlanes(ground);
        }
    }

    static void CreateSimpleWaterPlanes(GameObject ground)
    {
        var groundCollider = ground.GetComponent<MeshCollider>();
        if (groundCollider == null) return;

        var waterParent = new GameObject("WaterBodies");
        waterParent.transform.SetParent(ground.transform);

        var waterMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        waterMat.color = new Color(0.1f, 0.3f, 0.6f, 0.5f);
        waterMat.SetFloat("_Surface", 1.0f);
        waterMat.SetFloat("_Blend", 0.0f);
        waterMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        waterMat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        waterMat.SetInt("_ZWrite", 0);
        waterMat.renderQueue = 3000;
        waterMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        // 낮은 고도 지점들에 물 평면 생성 (y < 5m)
        for (int i = 0; i < 10; i++)
        {
            float x = Random.Range(-500f, 500f);
            float z = Random.Range(-500f, 500f);
            var ray = new Ray(new Vector3(x, 100f, z), Vector3.down);
            if (groundCollider.Raycast(ray, out var hit, 200f))
            {
                if (hit.point.y < 5f) // 낮은 지대만
                {
                    var waterPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
                    waterPlane.name = $"WaterPlane_{i}";
                    waterPlane.transform.SetParent(waterParent.transform);
                    waterPlane.transform.position = new Vector3(x, hit.point.y + 0.1f, z);
                    waterPlane.transform.localScale = Vector3.one * Random.Range(5f, 20f);
                    Object.DestroyImmediate(waterPlane.GetComponent<MeshCollider>());
                    var mr = waterPlane.GetComponent<MeshRenderer>();
                    mr.sharedMaterial = waterMat;
                }
            }
        }
    }
}

// ================================================================
// Camera Zoom Controller (Mouse Wheel)
// ================================================================
public class CameraZoomController : MonoBehaviour
{
    public float minDistance = 15f;
    public float maxDistance = 40f;
    public float zoomSpeed = 5f;
    public CinemachineThirdPersonFollow targetFollow;

    void Update()
    {
        if (targetFollow == null) return;

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            float newDistance = targetFollow.CameraDistance - scroll * zoomSpeed;
            targetFollow.CameraDistance = Mathf.Clamp(newDistance, minDistance, maxDistance);
        }
    }
}