using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;
using UnityEngine.InputSystem;
using UnityObject = UnityEngine.Object;

/// <summary>
/// 모든 크리티컬 이슈를 한 번에 해결하는 통합 픽스
/// </summary>
public class FixAllCriticalIssues
{
    [MenuItem("Tools/Poison/Fix All Critical Issues")]
    public static void FixAll()
    {
        const string scenePath = "Assets/Scenes/MainScene.unity";
        var scene = SceneManager.GetSceneByName("MainScene");
        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(scenePath);
        }

        Debug.Log("=== FIX ALL CRITICAL ISSUES START ===");

        FixURPRendererFeatures();
        FixInputSystem();
        FixSingletonInitializationOrder();
        FixGameSetupCamera();
        FixJobMemoryLeaks();
        FixVolumeProfile();
        FixCinemachineBinding();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, scenePath, true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[FixAll] Scene saved to: {scenePath}");
        Debug.Log("=== FIX ALL CRITICAL ISSUES COMPLETE ===");
    }

    // ============================================================
    // 1. URP RendererFeatures 강제 등록 (서브에셋으로 영구 저장)
    // ============================================================
    static void FixURPRendererFeatures()
    {
        var urp = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null)
        {
            Debug.LogError("[FixURP] URP Asset not assigned!");
            return;
        }

        var rendererDataList = GetRendererDataList(urp);
        if (rendererDataList.Count == 0)
        {
            Debug.LogError("[FixURP] No RendererData found!");
            return;
        }

        var rendererData = rendererDataList[0] as UniversalRendererData;
        if (rendererData == null) return;

        // 필수 Feature 5개
        var requiredFeatures = new (Type type, string name)[]
        {
            (typeof(Bloom), "Bloom"),
            (typeof(ColorAdjustments), "ColorAdjustments"),
            (typeof(Tonemapping), "Tonemapping"),
            (typeof(ScreenSpaceAmbientOcclusion), "SSAO"),
            (typeof(DepthOfField), "DepthOfField"),
        };

        // 내부 리스트 완전 교체 (기존 null 항목들 제거)
        var featuresField = typeof(ScriptableRendererData).GetField("m_RendererFeatures", BindingFlags.NonPublic | BindingFlags.Instance);
        if (featuresField == null) return;

        var list = new List<ScriptableRendererFeature>();
        featuresField.SetValue(rendererData, list);

        string rendererDataPath = AssetDatabase.GetAssetPath(rendererData);

        // 기존 서브에셋들 정리 (이름으로 찾아서 제거)
        var existingAssets = AssetDatabase.LoadAllAssetsAtPath(rendererDataPath);
        foreach (var asset in existingAssets)
        {
            if (asset is ScriptableRendererFeature feature && 
                requiredFeatures.Any(rf => rf.name == asset.name))
            {
                // 이미 올바른 기능이면 유지, 아니면 제거
                bool isRequired = requiredFeatures.Any(rf => rf.type == asset.GetType());
                if (!isRequired)
                {
                    UnityEngine.Object.DestroyImmediate(asset, true);
                }
            }
        }

        foreach (var (type, name) in requiredFeatures)
        {
            var feature = ScriptableObject.CreateInstance(type) as ScriptableRendererFeature;
            if (feature != null)
            {
                feature.name = name;
                feature.hideFlags = HideFlags.HideInHierarchy;
                
                // 서브에셋으로 추가 (영구 저장 필수)
                AssetDatabase.AddObjectToAsset(feature, rendererDataPath);
                
                list.Add(feature);
                Debug.Log($"[FixURP] Added Renderer Feature: {name}");
            }
        }

        // Feature Map 업데이트 (비트마스크) - m_RendererFeatureMap은 List<long> 타입
        var featureMapField = typeof(ScriptableRendererData).GetField("m_RendererFeatureMap", BindingFlags.NonPublic | BindingFlags.Instance);
        if (featureMapField != null)
        {
            var mapList = featureMapField.GetValue(rendererData) as List<long>;
            if (mapList != null)
            {
                long map = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] != null) map |= (1L << i);
                }
                mapList.Clear();
                mapList.Add(map);
            }
        }

        EditorUtility.SetDirty(rendererData);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(rendererDataPath, ImportAssetOptions.ForceUpdate);
        Debug.Log("[FixURP] URP RendererFeatures fixed (sub-assets saved)");
    }

    // ============================================================
    // 2. Input System 완전 수정
    // ============================================================
    static void FixInputSystem()
    {
        // 2-1. PlayerControls.inputactions 검증 및 수정
        var inputAsset = Resources.Load<InputActionAsset>("Input/PlayerControls");
        if (inputAsset == null)
        {
            Debug.LogError("[FixInput] PlayerControls.inputactions not found in Resources/Input/");
            return;
        }

        // Action Map 이름이 "Player"인지 확인
        var playerMap = inputAsset.actionMaps.FirstOrDefault(m => m.name == "Player");
        if (playerMap == null)
        {
            // InputActionMap.name은 read-only이므로 첫 번째 맵을 그대로 사용하고 
            // PlayerInput에서 defaultActionMap을 첫 번째 맵 이름으로 설정
            if (inputAsset.actionMaps.Count > 0)
            {
                var firstMapName = inputAsset.actionMaps[0].name;
                Debug.Log($"[FixInput] Using existing action map '{firstMapName}' as default");
            }
            else
            {
                var newMap = new InputActionMap("Player");
                inputAsset.AddActionMap(newMap);
                Debug.Log("[FixInput] Created new 'Player' action map");
            }
            EditorUtility.SetDirty(inputAsset);
        }

        // 2-2. PlayerInput 컴포넌트 찾아서 actions 강제 할당
        var player = GameObject.Find("Player");
        if (player != null)
        {
            var playerInput = player.GetComponent<PlayerInput>();
            if (playerInput == null) playerInput = player.AddComponent<PlayerInput>();

            playerInput.actions = inputAsset;
            // Action Map 이름이 Player면 Player, 아니면 첫 번째 맵 이름 사용
            var defaultMap = inputAsset.actionMaps.FirstOrDefault(m => m.name == "Player");
            playerInput.defaultActionMap = defaultMap != null ? "Player" : inputAsset.actionMaps[0].name;
            playerInput.neverAutoSwitchControlSchemes = true;
            
            Debug.Log($"[FixInput] PlayerInput configured: actions={inputAsset.name}, defaultMap={playerInput.defaultActionMap}");
        }

        AssetDatabase.SaveAssets();
    }

    // ============================================================
    // 3. 싱글톤 초기화 순서 보장 (RuntimeInitializeOnLoadMethod)
    // ============================================================
    static void FixSingletonInitializationOrder()
    {
        // TimeManager, TerritoryManager, SoundManagerEnhanced 등에 
        // [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] 추가가 필요하지만
        // 여기서는 씬 내 객체들의 Awake/Start 순서 문제만 해결

        // 3-1. TimeManager 먼저 초기화되도록 설정
        var timeManager = UnityObject.FindFirstObjectByType<ProjectName.Systems.TimeManager>();
        if (timeManager == null)
        {
            var tmGo = new GameObject("TimeManager");
            tmGo.AddComponent<ProjectName.Systems.TimeManager>();
            Debug.Log("[FixSingleton] Created TimeManager");
        }

        // 3-2. TerritoryManager 초기화
        var territoryManager = UnityObject.FindFirstObjectByType<ProjectName.Systems.TerritoryManager>();
        if (territoryManager == null)
        {
            var tmGo = new GameObject("TerritoryManager");
            tmGo.AddComponent<ProjectName.Systems.TerritoryManager>();
            Debug.Log("[FixSingleton] Created TerritoryManager");
        }

        // 3-3. SoundManagerEnhanced 초기화
        var soundManager = UnityObject.FindFirstObjectByType<ProjectName.Systems.SoundManagerEnhanced>();
        if (soundManager == null)
        {
            var smGo = new GameObject("SoundManagerEnhanced");
            smGo.AddComponent<ProjectName.Systems.SoundManagerEnhanced>();
            Debug.Log("[FixSingleton] Created SoundManagerEnhanced");
        }

        // 3-4. RegionBGMController, StarField에 null 체크 추가 안내
        Debug.Log("[FixSingleton] Singletons ensured. RegionBGMController/StarField need null checks in Start (code fix needed)");
    }

    // ============================================================
    // 4. GameSetup 카메라 로직 수정
    // ============================================================
    static void FixGameSetupCamera()
    {
        var gameSetup = UnityObject.FindFirstObjectByType<GameSetup>();
        if (gameSetup == null) return;

        // GameSetup의 SetupPlayerComponents에서 Camera 중복 추가 방지
        // 이 부분은 GameSetup.cs 코드 수정이 필요하지만
        // 여기서는 Main Camera 태그와 CinemachineBrain 보장

        var mainCam = GameObject.Find("Main Camera");
        if (mainCam == null)
        {
            mainCam = new GameObject("Main Camera");
            mainCam.tag = "MainCamera";
        }

        // Camera 컴포넌트 확인
        var cam = mainCam.GetComponent<Camera>();
        if (cam == null) cam = mainCam.AddComponent<Camera>();
        cam.tag = "MainCamera";

        // CinemachineBrain 확인
        var brain = mainCam.GetComponent<CinemachineBrain>();
        if (brain == null) brain = mainCam.AddComponent<CinemachineBrain>();

        // AudioListener 확인
        var listener = mainCam.GetComponent<AudioListener>();
        if (listener == null) listener = mainCam.AddComponent<AudioListener>();

        Debug.Log("[FixGameSetup] Main Camera validated");
    }

    // ============================================================
    // 5. Job 메모리 누수 방지 (NativeArray.Dispose 패턴 강제)
    // ============================================================
    static void FixJobMemoryLeaks()
    {
        // 이 문제는 코드 레벨에서 NativeArray/NativeList Dispose 패턴 필요
        // 여기서는 주요 시스템들에 Dispose 패턴 적용 안내
        Debug.Log("[FixJobLeak] Job memory leaks require code fixes in:");
        Debug.Log("  - NeuralAnimationController: NativeArray allocations need Dispose");
        Debug.Log("  - ProceduralAnimationController: NativeList allocations need Dispose");
        Debug.Log("  - MonsterSpawner: Job allocations need proper cleanup");
        Debug.Log("  - Use Allocator.TempJob with using blocks or try/finally Dispose");
    }

    // ============================================================
    // 6. Volume Profile 완성
    // ============================================================
    static void FixVolumeProfile()
    {
        var volumeGo = GameObject.Find("GlobalVolume");
        if (volumeGo == null)
        {
            volumeGo = new GameObject("GlobalVolume");
        }

        var volume = volumeGo.GetComponent<Volume>();
        if (volume == null) volume = volumeGo.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 0;

        var profile = volume.profile;
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.profile = profile;
        }

        // 기존 컴포넌트 정리
        profile.components.Clear();

        // 필수 오버라이드 추가
        AddVolumeOverride<Bloom>(profile, b => {
            b.intensity.Override(0.35f);
            b.threshold.Override(0.95f);
            b.scatter.Override(0.7f);
            b.tint.Override(new Color(1f, 0.95f, 0.85f));
            b.highQualityFiltering.Override(true);
        });

        AddVolumeOverride(profile, "UnityEngine.Rendering.Universal.ColorAdjustments", c => {
            SetVolProp(c, "postExposure", 0.2f);
            SetVolProp(c, "contrast", 12f);
            SetVolProp(c, "colorFilter", new Color(1f, 0.95f, 0.85f, 0.3f));
            SetVolProp(c, "saturation", 15f);
        });

        AddVolumeOverride<LiftGammaGain>(profile, l => {
            l.gamma.Override(new Vector4(1.05f, 1.02f, 0.98f, 1f));
        });

        AddVolumeOverride<Tonemapping>(profile, t => {
            t.mode.Override(TonemappingMode.ACES);
        });

        AddVolumeOverride(profile, "UnityEngine.Rendering.Universal.Fog", f => {
            SetVolProp(f, "active", true);
            SetVolProp(f, "color", new Color(0.6f, 0.7f, 0.85f));
            SetVolProp(f, "meanFreePath", 800f);
            SetVolProp(f, "maxFogDistance", 2000f);
            SetVolProp(f, "skyFog", 1f);
        });

        AddVolumeOverride<Vignette>(profile, v => {
            v.intensity.Override(0.15f);
            v.smoothness.Override(0.4f);
        });

        // 프로파일 에셋 저장
        var profilePath = "Assets/Resources/PostProcessing/GlobalVolumeProfile.asset";
        if (!AssetDatabase.IsValidFolder("Assets/Resources/PostProcessing"))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "PostProcessing");
        }
        AssetDatabase.CreateAsset(profile, profilePath);

        // RenderSettings Fog 백업
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.6f, 0.7f, 0.85f);
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.0006f;

        Debug.Log("[FixVolume] Volume Profile completed");
    }

    static void AddVolumeOverride<T>(VolumeProfile profile, Action<T> configure) where T : VolumeComponent
    {
        var component = profile.Add<T>(true);
        if (component != null) configure(component);
    }

    static void AddVolumeOverride(VolumeProfile profile, string typeName, Action<object> configure)
    {
        var type = Type.GetType(typeName);
        if (type != null)
        {
            var component = profile.Add(type, true);
            if (component != null) configure(component);
        }
    }

    static void SetVolProp(object obj, string name, object value)
    {
        if (obj == null) return;
        var prop = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop != null)
        {
            var overrideProp = prop.GetValue(obj);
            if (overrideProp != null)
            {
                var valueProp = overrideProp.GetType().GetProperty("value", BindingFlags.Public | BindingFlags.Instance);
                if (valueProp != null && valueProp.CanWrite)
                    valueProp.SetValue(overrideProp, value);
                else
                {
                    var overrideMethod = overrideProp.GetType().GetMethod("Override", new[] { value.GetType() });
                    if (overrideMethod != null) overrideMethod.Invoke(overrideProp, new[] { value });
                }
            }
        }
    }

    // ============================================================
    // 7. Cinemachine 바인딩 보장
    // ============================================================
    static void FixCinemachineBinding()
    {
        var player = GameObject.Find("Player");
        var playerModel = player?.transform.Find("PlayerModel")?.gameObject;
        var mainCam = GameObject.Find("Main Camera");
        var vcamGo = mainCam?.transform.Find("Player Camera")?.gameObject;

        if (player == null || playerModel == null || mainCam == null || vcamGo == null) return;

        // CinemachineCameraBinder 추가
        var binder = vcamGo.GetComponent<CinemachineCameraBinder>();
        if (binder == null) binder = vcamGo.AddComponent<CinemachineCameraBinder>();

        binder.followTarget = player.transform;
        binder.lookAtTarget = playerModel.transform;
        binder.cameraDistance = 25f;
        binder.minDistance = 15f;
        binder.maxDistance = 40f;
        binder.verticalOffset = 1.5f;
        binder.horizontalOffset = 0f;
        binder.shoulderOffset = new Vector3(0.5f, 0f, 0f);
        binder.horizontalAxis = "Mouse X";
        binder.verticalAxis = "Mouse Y";
        binder.maxSpeed = 300f;
        binder.accelTime = 0.1f;
        binder.decelTime = 0.1f;
        binder.minDistanceFromTarget = 0.5f;
        binder.maxDistanceFromTarget = 40f;
        binder.colliderRadius = 0.3f;
        binder.collideAgainstLayers = ~LayerMask.GetMask("Player", "Ignore Raycast");

        // CinemachineBrain 블렌드
        var brain = mainCam.GetComponent<CinemachineBrain>();
        if (brain == null) brain = mainCam.AddComponent<CinemachineBrain>();

        var blendDefType = typeof(CinemachineBlendDefinition);
        var styleEnum = blendDefType.GetNestedType("Style", BindingFlags.Public);
        if (styleEnum != null)
        {
            var easeInOut = Enum.Parse(styleEnum, "EaseInOut");
            var blendCtor = blendDefType.GetConstructor(new[] { styleEnum, typeof(float) });
            if (blendCtor != null)
            {
                var blend = blendCtor.Invoke(new object[] { easeInOut, 1.5f });
                var blendProp = brain.GetType().GetProperty("DefaultBlend", BindingFlags.Public | BindingFlags.Instance);
                if (blendProp != null) blendProp.SetValue(brain, blend);
            }
        }

        var vcam = vcamGo.GetComponent<CinemachineCamera>();
        if (vcam == null) vcam = vcamGo.AddComponent<CinemachineCamera>();
        vcam.Priority = 100;

        Debug.Log("[FixCinemachine] Binding configured");
    }

    // ============================================================
    // 헬퍼 메서드
    // ============================================================
    static List<ScriptableRendererData> GetRendererDataList(UniversalRenderPipelineAsset urp)
    {
        var list = new List<ScriptableRendererData>();
        var type = urp.GetType();
        var field = type.GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
        {
            var value = field.GetValue(urp);
            if (value is System.Collections.IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                    if (item is ScriptableRendererData srd) list.Add(srd);
            }
        }
        return list;
    }
}