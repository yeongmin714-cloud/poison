using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ProjectName.Systems;
using ProjectName.Systems.Motions;
using System.IO;

/// <summary>
/// Unity Editor 최초 실행 시 모든 수동 설정을 자동으로 적용합니다.
/// 
/// 자동 설정 항목:
///   1. URP Pipeline Asset → Quality Settings 할당
///   2. Skybox 머티리얼 생성 및 할당 (Phase 3B)
///   3. Post-processing Volume Override 7종 적용 (Phase 3C)
///   4. Sway Controller 자동 부착 (AutoSwayInstaller 연동)
/// 
/// 최초 1회만 실행되며, Tools/Re-run Auto Setup으로 재실행 가능.
/// </summary>
[InitializeOnLoad]
public static class EditorAutoSetup
{
    private const string FlagKey = "Poison_AutoSetup_Completed";
    private const string ScenePath_TopDown = "Assets/Scenes/TopDownScene.unity";
    private const string ScenePath_Main = "Assets/Scenes/MainScene.unity";
    private const string ProfilePath = "Assets/Settings/Default_VolumeProfile_Enhanced.asset";
    private const string SkyboxMatPath = "Assets/Materials/ProceduralSkybox_Dynamic.mat";

    static EditorAutoSetup()
    {
        if (!EditorPrefs.GetBool(FlagKey, false))
        {
            EditorApplication.delayCall += RunFullAutoSetup;
        }
    }

    [MenuItem("Tools/Re-run Auto Setup")]
    public static void ReRunAutoSetup()
    {
        EditorPrefs.DeleteKey(FlagKey);
        RunFullAutoSetup();
    }

    [MenuItem("Tools/Reset Auto Setup Flag")]
    public static void ResetAutoSetupFlag()
    {
        EditorPrefs.DeleteKey(FlagKey);
        Debug.Log("[AutoSetup] Flag cleared. Will run on next Editor restart.");
    }

    private static void RunFullAutoSetup()
    {
        Debug.Log("========================================");
        Debug.Log("[AutoSetup] === Auto Setup 시작 ===");
        Debug.Log("========================================");

        try
        {
            RunStep("URP Pipeline Asset 할당", AssignURPPipeline);
            RunStep("씬 열기", OpenTargetScene);
            RunStep("Skybox 설정", SetupSkybox);
            RunStep("Post-processing 설정", SetupPostProcessing);
            RunStep("Sway Controller 부착", InstallSwayControllers);
            RunStep("Sound System 설정", SetupSoundSystem);
            RunStep("Drop Table 생성", Phase1C_CreateDropTables.AutoCreateDropTables);
            RunStep("Phase 4 Recipe Assets 생성", Phase4_GenerateRecipeAssets.GenerateAllRecipes);
            RunStep("Phase 5 시스템 설정", SetupPhase5Systems);
            RunStep("Phase 6B NPC 생성", Phase6B_GenerateTerritoryNPCs.GenerateAllTerritoryNPCs);
            RunStep("GLB Model Swap + Scale", SwapAndScaleModels);

            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();

            Debug.Log("========================================");
            Debug.Log("[AutoSetup] ✅ ALL SETUP COMPLETE");
            Debug.Log("========================================");

            EditorPrefs.SetBool(FlagKey, true);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[AutoSetup] ❌ Setup failed: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private static void RunStep(string stepName, System.Action action)
    {
        try
        {
            Debug.Log($"[AutoSetup] ▶ {stepName}...");
            action();
            Debug.Log($"[AutoSetup] ✓ {stepName} 완료");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[AutoSetup] ⚠ {stepName} 실패 (계속 진행): {ex.Message}");
        }
    }

    // ================================================================
    //  Step 1: URP Pipeline Asset → Quality Settings
    // ================================================================

    private static void AssignURPPipeline()
    {
        string[] guids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset");
        if (guids.Length == 0)
        {
            Debug.LogWarning("[AutoSetup] URP Pipeline Asset not found. Skipping.");
            return;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        var pipelineAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
        if (pipelineAsset == null) return;

        int currentLevel = QualitySettings.GetQualityLevel();

        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = pipelineAsset;
        }
        QualitySettings.SetQualityLevel(currentLevel, false);
        QualitySettings.renderPipeline = pipelineAsset;

        EditorUtility.SetDirty(pipelineAsset);
        Debug.Log($"[AutoSetup] URP Pipeline assigned: {path}");
    }

    // ================================================================
    //  Step 2: Scene 열기
    // ================================================================

    private static void OpenTargetScene()
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.IsValid() && !string.IsNullOrEmpty(activeScene.path))
        {
            string name = Path.GetFileNameWithoutExtension(activeScene.path);
            if (name == "TopDownScene" || name == "MainScene")
            {
                Debug.Log($"[AutoSetup] Already in target scene: {activeScene.path}");
                return;
            }
        }

        if (File.Exists(ScenePath_TopDown))
        {
            EditorSceneManager.OpenScene(ScenePath_TopDown, OpenSceneMode.Single);
            Debug.Log($"[AutoSetup] Opened: {ScenePath_TopDown}");
            return;
        }

        if (File.Exists(ScenePath_Main))
        {
            EditorSceneManager.OpenScene(ScenePath_Main, OpenSceneMode.Single);
            Debug.Log($"[AutoSetup] Opened: {ScenePath_Main}");
            return;
        }

        Debug.LogWarning("[AutoSetup] No target scene found. Creating new empty scene.");
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
    }

    // ================================================================
    //  Step 3: Skybox 설정 (Phase 3B)
    // ================================================================

    private static void SetupSkybox()
    {
        Material skyboxMat = AssetDatabase.LoadAssetAtPath<Material>(SkyboxMatPath);

        if (skyboxMat == null)
        {
            Shader proceduralSkyboxShader = Shader.Find("Skybox/Procedural");
            if (proceduralSkyboxShader == null)
            {
                Debug.LogWarning("[AutoSetup] Skybox/Procedural shader not found. Using default skybox.");
                var defaultSkybox = Resources.GetBuiltinResource(typeof(Material), "Default-Skybox.mat") as Material;
                if (defaultSkybox != null) RenderSettings.skybox = defaultSkybox;
                return;
            }

            skyboxMat = new Material(proceduralSkyboxShader);
            skyboxMat.name = "ProceduralSkybox_Dynamic";
            skyboxMat.SetFloat("_SunSize", 0.04f);
            skyboxMat.SetFloat("_SunSizeConvergence", 5);
            skyboxMat.SetFloat("_AtmosphereThickness", 1.0f);
            skyboxMat.SetColor("_SkyTint", new Color(0.5f, 0.6f, 0.8f));
            skyboxMat.SetColor("_GroundColor", new Color(0.369f, 0.349f, 0.341f));
            skyboxMat.SetFloat("_Exposure", 1.0f);

            Directory.CreateDirectory("Assets/Materials");
            AssetDatabase.CreateAsset(skyboxMat, SkyboxMatPath);
            Debug.Log("[AutoSetup] Skybox material created.");
        }

        RenderSettings.skybox = skyboxMat;

        Camera mainCam = GameObject.Find("Main Camera")?.GetComponent<Camera>();
        if (mainCam != null)
        {
            mainCam.clearFlags = CameraClearFlags.Skybox;
        }
        else
        {
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            mainCam = camGO.AddComponent<Camera>();
            mainCam.clearFlags = CameraClearFlags.Skybox;
        }
    }

    // ================================================================
    //  Step 4: Post-processing 설정 (Phase 3C)
    // ================================================================

    private static void SetupPostProcessing()
    {
        Volume globalVolume = Object.FindAnyObjectByType<Volume>();
        GameObject volumeGO;
        if (globalVolume != null)
        {
            volumeGO = globalVolume.gameObject;
        }
        else
        {
            volumeGO = new GameObject("Global Volume");
            globalVolume = volumeGO.AddComponent<Volume>();
            globalVolume.isGlobal = true;
        }

        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Default_VolumeProfile_Enhanced";
            Directory.CreateDirectory("Assets/Settings");
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }

        AddOrUpdateOverride<Tonemapping>(profile, tm => { tm.mode.overrideState = true; tm.mode.value = TonemappingMode.ACES; });
        AddOrUpdateOverride<ColorAdjustments>(profile, ca => { ca.postExposure.overrideState = true; ca.postExposure.value = 0.3f; ca.contrast.overrideState = true; ca.contrast.value = 8f; ca.saturation.overrideState = true; ca.saturation.value = 15f; });
        AddOrUpdateOverride<Bloom>(profile, bl => { bl.threshold.overrideState = true; bl.threshold.value = 0.9f; bl.intensity.overrideState = true; bl.intensity.value = 1.5f; bl.scatter.overrideState = true; bl.scatter.value = 0.7f; bl.tint.overrideState = true; bl.tint.value = new Color(0.9f, 0.95f, 1f, 1f); });
        AddOrUpdateOverride<Vignette>(profile, vg => { vg.intensity.overrideState = true; vg.intensity.value = 0.35f; vg.smoothness.overrideState = true; vg.smoothness.value = 0.45f; vg.color.overrideState = true; vg.color.value = Color.black; });
        AddOrUpdateOverride<WhiteBalance>(profile, wb => { wb.temperature.overrideState = true; wb.temperature.value = -5f; wb.tint.overrideState = true; wb.tint.value = 0f; });
        AddOrUpdateOverride<ShadowsMidtonesHighlights>(profile, smh => { smh.shadows.overrideState = true; smh.shadows.value = new Vector4(-0.02f, -0.02f, -0.02f, 0f); smh.midtones.overrideState = true; smh.midtones.value = new Vector4(1f, 1f, 1f, 0f); smh.highlights.overrideState = true; smh.highlights.value = new Vector4(1f, 1f, 1f, 0f); });
        AddOrUpdateOverride<DepthOfField>(profile, dof => { dof.mode.overrideState = true; dof.mode.value = DepthOfFieldMode.Gaussian; });

        globalVolume.sharedProfile = profile;
        EditorUtility.SetDirty(globalVolume);

        string[] urpGuids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset");
        if (urpGuids.Length > 0)
        {
            string urpPath = AssetDatabase.GUIDToAssetPath(urpGuids[0]);
            var pipelineAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(urpPath);
            if (pipelineAsset != null)
            {
                SerializedObject serialized = new SerializedObject(pipelineAsset);
                var cgMode = serialized.FindProperty("m_ColorGradingMode");
                if (cgMode != null) cgMode.intValue = 1;
                var vpProp = serialized.FindProperty("m_VolumeProfile");
                if (vpProp != null) vpProp.objectReferenceValue = profile;
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(pipelineAsset);
            }
        }

        EditorUtility.SetDirty(profile);
        Debug.Log("[AutoSetup] Post-processing: 7 overrides applied, Color Grading → HDR");
    }

    private static void AddOrUpdateOverride<T>(VolumeProfile profile, System.Action<T> configure) where T : VolumeComponent
    {
        T component;
        if (!profile.TryGet<T>(out component))
        {
            component = profile.Add<T>(overrides: true);
        }
        configure(component);
    }

    // ================================================================
    //  Step 5: Sway Controller 부착
    // ================================================================

    private static void InstallSwayControllers()
    {
        SwayInstaller.InstallSwayControllers();
    }

    // ================================================================
    //  Step 6: Sound System 설정
    // ================================================================

    private static void SetupSoundSystem()
    {
        const string configPath = "Assets/Settings/AudioConfig.asset";

        // AudioConfig 생성/로드
        var config = AssetDatabase.LoadAssetAtPath<ProjectName.Core.AudioConfig>(configPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<ProjectName.Core.AudioConfig>();
            config.name = "AudioConfig";
            Directory.CreateDirectory("Assets/Settings");
            AssetDatabase.CreateAsset(config, configPath);

            // 기본 사운드 클립 생성
            string[] soundIds = { "footstep", "pickup", "craft_success", "craft_fail", "ui_open", "ui_close", "attack_swing", "attack_hit" };
            foreach (string id in soundIds)
            {
                var clipData = ScriptableObject.CreateInstance<ProjectName.Core.SoundClipData>();
                clipData.name = id;
                clipData.soundId = id;
                clipData.soundType = id.StartsWith("ui") ? ProjectName.Core.SoundType.UI : ProjectName.Core.SoundType.SFX;
                clipData.category = id.Contains("craft") ? "craft" : id.Contains("ui") ? "ui" : id.Contains("attack") ? "combat" : "movement";

                // 주파수 설정 (사운드 특성에 맞게)
                clipData.proceduralPitch = id switch
                {
                    "footstep" => 100f,
                    "pickup" => 660f,
                    "craft_success" => 880f,
                    "craft_fail" => 220f,
                    "ui_open" => 523f,
                    "ui_close" => 262f,
                    "attack_swing" => 200f,
                    "attack_hit" => 330f,
                    _ => 440f
                };
                clipData.proceduralDuration = id == "footstep" ? 0.15f : 0.3f;
                clipData.pitchMin = 0.9f;
                clipData.pitchMax = 1.1f;

                string clipPath = $"Assets/Settings/Sounds/{id}.asset";
                Directory.CreateDirectory("Assets/Settings/Sounds");
                AssetDatabase.CreateAsset(clipData, clipPath);
                config.clips.Add(clipData);
            }

            config.BuildLookup();
            EditorUtility.SetDirty(config);
            Debug.Log("[AutoSetup] AudioConfig created with 8 procedural sounds.");
        }

        // SoundManager GameObject 찾기/생성
        var existing = Object.FindAnyObjectByType<ProjectName.Core.SoundManager>();
        if (existing == null)
        {
            var go = new GameObject("SoundManager");
            var sm = go.AddComponent<ProjectName.Core.SoundManager>();
            sm.config = config;
            EditorUtility.SetDirty(go);
            Debug.Log("[AutoSetup] SoundManager GameObject created.");
        }
        else
        {
            existing.config = config;
            EditorUtility.SetDirty(existing.gameObject);
            Debug.Log("[AutoSetup] SoundManager already exists. Config updated.");
        }

        AssetDatabase.SaveAssets();
    }

    // ================================================================
    //  Phase 5: WarehouseSystem + ChurchSystem 자동 생성
    // ================================================================

    private static void SetupPhase5Systems()
    {
        // WarehouseSystem 찾기/생성
        var existingWarehouse = Object.FindAnyObjectByType<WarehouseSystem>();
        if (existingWarehouse == null)
        {
            var go = new GameObject("WarehouseSystem");
            go.AddComponent<WarehouseSystem>();
            EditorUtility.SetDirty(go);
            Debug.Log("[AutoSetup] WarehouseSystem GameObject created.");
        }
        else
        {
            Debug.Log("[AutoSetup] WarehouseSystem already exists.");
        }

        // ChurchSystem 찾기/생성
        var existingChurch = Object.FindAnyObjectByType<ChurchSystem>();
        if (existingChurch == null)
        {
            var go = new GameObject("ChurchSystem");
            go.AddComponent<ChurchSystem>();
            EditorUtility.SetDirty(go);
            Debug.Log("[AutoSetup] ChurchSystem GameObject created.");
        }
        else
        {
            Debug.Log("[AutoSetup] ChurchSystem already exists.");
        }
    }

    /// <summary>
    /// GLB Model Swap + 개체별 크기 조정.
    /// ModelSwapper.SwapAllModels() 호출 후 각 개체의 적절한 스케일 적용.
    /// </summary>
    private static void SwapAndScaleModels()
    {
        // 1. ModelSwapper로 Placeholder → GLB 교체
        ModelSwapper.SwapAllModels();

        // 2. 개체별 스케일 적용
        ApplyModelScales();

        // 3. Rigged 모델에 Animation Rigging 자동 부착
        AttachRiggingToModels();
    }

    private static void AttachRiggingToModels()
    {
        if (!AnimationRiggingSetup.IsAnimationRiggingAvailable)
        {
            Debug.Log("[AutoSetup] Animation Rigging package not available, skipping rigging setup.");
            return;
        }

        // Rigging이 필요한 모든 오브젝트 찾기 (Player, Soldier, NPC, Monster)
        string[] rigTargets = { "Player", "Soldier", "Lord_NPC", "NPC_", "Wolf", "Boar", "Rabbit", 
                                "Deer", "Bat", "Crow", "Snake", "Banshee", "Manticore", "Minotaur", 
                                "Griffon", "Golem", "Slime", "Salamander" };
        int rigged = 0;
        
        foreach (var targetName in rigTargets)
        {
            var objects = GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            foreach (var t in objects)
            {
                if (!t.name.Contains(targetName)) continue;
                
                // 이미 RiggingSetup이 있으면 건너뛰기
                if (t.GetComponent<AnimationRiggingSetup>() != null) continue;
                
                // 자식에 메시가 있는 경우에만 부착 (GLB 모델임을 확인)
                if (t.GetComponentInChildren<SkinnedMeshRenderer>() != null ||
                    t.GetComponentInChildren<MeshRenderer>() != null)
                {
                    // 1. Rigging Setup
                    var rigSetup = t.gameObject.AddComponent<AnimationRiggingSetup>();
                    rigSetup.FindBones();
                    rigSetup.SetupRigging();
                    
                    // 2. RigAnimationController (상태 머신)
                    var rigCtrl = t.GetComponent<RigAnimationController>();
                    if (rigCtrl == null)
                        rigCtrl = t.gameObject.AddComponent<RigAnimationController>();
                    
                    // 2.5 MotionDetector (모델 타입 자동 감지)
                    if (t.GetComponent<MotionDetector>() == null)
                        t.gameObject.AddComponent<MotionDetector>();
                    
                    // 3. AnimationMotionController + 종류별 Motion 컴포넌트
                    var motionCtrl = t.GetComponent<AnimationMotionController>();
                    if (motionCtrl == null)
                        motionCtrl = t.gameObject.AddComponent<AnimationMotionController>();
                    
                    AssignMotionsByType(motionCtrl, t.name, rigSetup.CharacterType);
                    
                    rigged++;
                    Debug.Log($"[AutoSetup] Animation Rigging + Motions attached: {t.name} (type: {rigSetup.CharacterType})");
                    break;
                }
            }
        }
        Debug.Log($"[AutoSetup] Animation Rigging applied: {rigged} models");
    }

    /// <summary>캐릭터 종류에 따라 적절한 Motion 컴포넌트 생성 및 할당</summary>
    private static void AssignMotionsByType(AnimationMotionController ctrl, string objName, CharacterType charType)
    {
        // Reflection으로 private SerializeField 설정
        var motionType = typeof(AnimationMotionController);
        
        switch (charType)
        {
            case CharacterType.Humanoid:
                TryAddMotion<IdleMotion>(ctrl, "_idleMotion", objName);
                TryAddMotion<WalkMotion>(ctrl, "_walkMotion", objName);
                TryAddMotion<RunMotion>(ctrl, "_runMotion", objName);
                TryAddMotion<JumpMotion>(ctrl, "_jumpMotion", objName);
                TryAddMotion<GatherMotion>(ctrl, "_gatherMotion", objName);
                TryAddMotion<CraftMotion>(ctrl, "_craftMotion", objName);
                TryAddMotion<AttackMotion>(ctrl, "_attackMotion", objName);
                TryAddMotion<ThrowMotion>(ctrl, "_throwMotion", objName);
                break;
                
            case CharacterType.Quadruped:
                TryAddMotion<QuadrupedIdleMotion>(ctrl, "_idleMotion", objName);
                TryAddMotion<QuadrupedWalkMotion>(ctrl, "_walkMotion", objName);
                TryAddMotion<QuadrupedRunMotion>(ctrl, "_runMotion", objName);
                TryAddMotion<AttackMotion>(ctrl, "_attackMotion", objName);
                break;
                
            case CharacterType.Monster:
                // 뱀형인지 확인
                if (objName.IndexOf("Snake", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    TryAddMotion<SnakeSlitherMotion>(ctrl, "_idleMotion", objName);
                }
                else
                {
                    TryAddMotion<IdleMotion>(ctrl, "_idleMotion", objName);
                }
                TryAddMotion<WalkMotion>(ctrl, "_walkMotion", objName);
                TryAddMotion<AttackMotion>(ctrl, "_attackMotion", objName);
                break;
        }
    }

    private static void TryAddMotion<T>(AnimationMotionController ctrl, string fieldName, string objName) where T : Component
    {
        var existing = ctrl.GetComponent<T>();
        if (existing != null) return;
        
        var motion = ctrl.gameObject.AddComponent<T>();
        
        // Reflection으로 private 필드에 할당
        var field = typeof(AnimationMotionController).GetField(fieldName, 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(ctrl, motion);
        }
        else
        {
            Debug.LogWarning($"[AutoSetup] Cannot set {fieldName} on {objName} (field not found)");
        }
    }

    /// <summary>개체 이름 기반 크기 조정 테이블</summary>
    private static readonly System.Collections.Generic.Dictionary<string, Vector3> _modelScales = 
        new System.Collections.Generic.Dictionary<string, Vector3>
    {
        // 플레이어
        { "Player",   new Vector3(1.0f, 1.0f, 1.0f) },
        
        // NPC
        { "Lord_NPC", new Vector3(1.0f, 1.0f, 1.0f) },
        { "NPC_Dracula", new Vector3(1.2f, 1.2f, 1.2f) },
        { "NPC_King", new Vector3(1.05f, 1.05f, 1.05f) },
        { "NPC_Shop", new Vector3(1.0f, 1.0f, 1.0f) },
        
        // 병사
        { "Soldier",  new Vector3(1.0f, 1.0f, 1.0f) },
        { "GuardPlaceholder", new Vector3(1.0f, 1.0f, 1.0f) },
        
        // 몬스터
        { "Rabbit",   new Vector3(0.4f, 0.4f, 0.4f) },
        { "Wolf",     new Vector3(0.8f, 0.8f, 0.8f) },
        { "Boar",     new Vector3(0.7f, 0.7f, 0.7f) },
        { "Deer",     new Vector3(0.9f, 0.9f, 0.9f) },
        { "Crow",     new Vector3(0.3f, 0.3f, 0.3f) },
        { "Bat",      new Vector3(0.25f, 0.25f, 0.25f) },
        { "Snake",    new Vector3(0.3f, 0.3f, 0.3f) },
        { "Slime",    new Vector3(0.5f, 0.5f, 0.5f) },
        { "Golem",    new Vector3(1.5f, 1.5f, 1.5f) },
        { "Banshee",  new Vector3(1.0f, 1.0f, 1.0f) },
        { "Manticore", new Vector3(1.3f, 1.3f, 1.3f) },
        { "Minotaur", new Vector3(1.5f, 1.5f, 1.5f) },
        { "Griffon",  new Vector3(1.2f, 1.2f, 1.2f) },
        { "Salamander", new Vector3(0.8f, 0.8f, 0.8f) },
        
        // 건물
        { "Hut",      new Vector3(3.0f, 3.0f, 3.0f) },
        { "Shop",     new Vector3(3.0f, 3.0f, 3.0f) },
        { "Castle",   new Vector3(5.0f, 5.0f, 5.0f) },
        { "Kingdom",  new Vector3(6.0f, 6.0f, 6.0f) },
        
        // 제작대
        { "CraftingTable", new Vector3(1.5f, 1.5f, 1.5f) },
        
        // 약초
        { "Herb",     new Vector3(0.3f, 0.3f, 0.3f) },
        
        // 포션
        { "Potion",   new Vector3(0.3f, 0.3f, 0.3f) },
        
        // 레시피북
        { "RecipeBook", new Vector3(0.5f, 0.5f, 0.5f) },
    };

    private static void ApplyModelScales()
    {
        int scaled = 0;
        foreach (var kvp in _modelScales)
        {
            var obj = GameObject.Find(kvp.Key);
            if (obj != null)
            {
                // 실제 GLB가 자식으로 들어간 경우 자식의 scale 조정
                if (obj.transform.childCount > 0 && obj.GetComponent<MeshRenderer>() == null)
                {
                    // Placeholder 패턴: 부모가 컨테이너, 첫 자식이 실제 모델
                    var child = obj.transform.GetChild(0);
                    child.localScale = kvp.Value;
                }
                else
                {
                    obj.transform.localScale = kvp.Value;
                }
                scaled++;
                Debug.Log($"[AutoSetup] Scale applied: {kvp.Key} → {kvp.Value}");
            }
        }
        Debug.Log($"[AutoSetup] Model scales applied: {scaled}/{_modelScales.Count} objects");
    }
}