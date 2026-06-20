using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Phase 3C: Post-processing setup tool.
/// Tools/Phase 3C - Setup Post Processing 메뉴에서 실행.
/// </summary>
public static class Phase3C_PostProcessingSetup
{
    private const string MenuPath = "Tools/Phase 3C - Setup Post Processing";
    private const string ProfilePath = "Assets/Settings/Default_VolumeProfile_Enhanced.asset";

    [MenuItem(MenuPath)]
    public static void SetupPostProcessing()
    {
        // 1. 활성 씬 확인
        var scene = EditorSceneManager.GetActiveScene();
        if (scene == null || string.IsNullOrEmpty(scene.path))
        {
            Debug.LogError("[PP3C] No active scene loaded. Open a scene first.");
            return;
        }

        // 2. Global Volume 찾기 (없으면 생성)
        Volume globalVolume = Object.FindAnyObjectByType<Volume>();
        GameObject volumeGO;
        if (globalVolume != null)
        {
            volumeGO = globalVolume.gameObject;
            Debug.Log($"[PP3C] Found existing Global Volume: {volumeGO.name}");
        }
        else
        {
            volumeGO = new GameObject("Global Volume");
            globalVolume = volumeGO.AddComponent<Volume>();
            globalVolume.isGlobal = true;
            Debug.Log("[PP3C] Created new Global Volume GameObject");
        }

        // 3. Volume Profile 생성/로드
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Default_VolumeProfile_Enhanced";
            AssetDatabase.CreateAsset(profile, ProfilePath);
            Debug.Log($"[PP3C] Created new Volume Profile at {ProfilePath}");
        }
        else
        {
            Debug.Log($"[PP3C] Loaded existing Volume Profile at {ProfilePath}");
        }

        // 4. Override 추가/설정
        // 4a. Tonemapping: ACES
        var tonemapping = GetOrAddVolumeComponent<Tonemapping>(profile);
        tonemapping.mode.overrideState = true;
        tonemapping.mode.value = TonemappingMode.ACES;
        // neutralHDRRangeReductionMode & paperWhite are on HDROutputParameters which is not exposed
        // in Tonemapping directly in URP 17. We set what is available.

        // 4b. Color Adjustments
        var colorAdj = GetOrAddVolumeComponent<ColorAdjustments>(profile);
        colorAdj.postExposure.overrideState = true;
        colorAdj.postExposure.value = 0.3f;
        colorAdj.contrast.overrideState = true;
        colorAdj.contrast.value = 8f;
        colorAdj.saturation.overrideState = true;
        colorAdj.saturation.value = 15f;

        // 4c. Bloom
        var bloom = GetOrAddVolumeComponent<Bloom>(profile);
        bloom.threshold.overrideState = true;
        bloom.threshold.value = 0.9f;
        bloom.intensity.overrideState = true;
        bloom.intensity.value = 1.5f;
        bloom.scatter.overrideState = true;
        bloom.scatter.value = 0.7f;
        bloom.tint.overrideState = true;
        bloom.tint.value = new Color(0.9f, 0.95f, 1f, 1f); // 따뜻한 색
        // maxIterations is not a standard Bloom field; skip if not available
        // highQualityFiltering is not a standard Bloom field; skip if not available

        // 4d. Vignette
        var vignette = GetOrAddVolumeComponent<Vignette>(profile);
        vignette.intensity.overrideState = true;
        vignette.intensity.value = 0.35f;
        vignette.smoothness.overrideState = true;
        vignette.smoothness.value = 0.45f;
        vignette.color.overrideState = true;
        vignette.color.value = Color.black;

        // 4e. White Balance
        var wb = GetOrAddVolumeComponent<WhiteBalance>(profile);
        wb.temperature.overrideState = true;
        wb.temperature.value = -5f; // 약간 차갑게
        wb.tint.overrideState = true;
        wb.tint.value = 0f;

        // 4f. ShadowsMidtonesHighlights
        var smh = GetOrAddVolumeComponent<ShadowsMidtonesHighlights>(profile);
        smh.shadows.overrideState = true;
        smh.shadows.value = new Vector4(-0.02f, -0.02f, -0.02f, 0f); // 약간 Lift
        smh.midtones.overrideState = true;
        smh.midtones.value = new Vector4(1f, 1f, 1f, 0f);
        smh.highlights.overrideState = true;
        smh.highlights.value = new Vector4(1f, 1f, 1f, 0f);

        // 4g. DepthOfField
        var dof = GetOrAddVolumeComponent<DepthOfField>(profile);
        dof.mode.overrideState = true;
        dof.mode.value = DepthOfFieldMode.Gaussian;

        // 5. Global Volume에 프로파일 연결
        globalVolume.sharedProfile = profile;
        EditorUtility.SetDirty(globalVolume);
        Debug.Log("[PP3C] Volume Profile assigned to Global Volume");

        // 6. URP Asset 찾기
        string[] urpGuids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset");
        if (urpGuids.Length == 0)
        {
            Debug.LogError("[PP3C] No URP Pipeline Asset found!");
            return;
        }

        string urpPath = AssetDatabase.GUIDToAssetPath(urpGuids[0]);
        var pipelineAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(urpPath);
        if (pipelineAsset == null)
        {
            Debug.LogError($"[PP3C] Failed to load URP asset at {urpPath}");
            return;
        }

        // 7. Color Grading → HDR로 변경
        SerializedObject serializedUrp = new SerializedObject(pipelineAsset);
        SerializedProperty colorGradingMode = serializedUrp.FindProperty("m_ColorGradingMode");
        if (colorGradingMode != null)
        {
            int oldMode = colorGradingMode.intValue;
            colorGradingMode.intValue = 1; // 1 = High Dynamic Range
            serializedUrp.ApplyModifiedProperties();
            Debug.Log($"[PP3C] Color Grading Mode changed from {oldMode} → 1 (HDR)");
        }
        else
        {
            Debug.LogWarning("[PP3C] Could not find m_ColorGradingMode property on URP asset");
        }

        // 8. URP Asset에 Volume Profile 연결
        SerializedProperty volumeProfileProp = serializedUrp.FindProperty("m_VolumeProfile");
        if (volumeProfileProp != null)
        {
            volumeProfileProp.objectReferenceValue = profile;
            serializedUrp.ApplyModifiedProperties();
            EditorUtility.SetDirty(pipelineAsset);
            Debug.Log("[PP3C] Volume Profile linked to URP Asset's m_VolumeProfile");
        }
        else
        {
            Debug.LogWarning("[PP3C] Could not find m_VolumeProfile property on URP asset");
        }

        // 9. 프로파일과 URP 에셋 저장
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        // 10. 씬 저장
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[PP3C] Scene saved successfully.");

        Debug.Log("[PP3C] ✓ Post-processing setup complete!\n" +
                  "  - Tonemapping (ACES)\n" +
                  "  - Color Adjustments (exposure: 0.3, contrast: 8, saturation: 15)\n" +
                  "  - Bloom (threshold: 0.9, intensity: 1.5, scatter: 0.7)\n" +
                  "  - Vignette (intensity: 0.35, smoothness: 0.45)\n" +
                  "  - White Balance (temperature: -5)\n" +
                  "  - ShadowsMidtonesHighlights (shadows lift -0.02)\n" +
                  "  - DepthOfField (Gaussian)\n" +
                  "  - Color Grading → HDR mode");
    }

    /// <summary>
    /// VolumeProfile에서 특정 타입의 VolumeComponent를 찾거나 추가.
    /// </summary>
    private static T GetOrAddVolumeComponent<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (profile.TryGet<T>(out T existing))
        {
            return existing;
        }
        return profile.Add<T>(overrides: true);
    }
}