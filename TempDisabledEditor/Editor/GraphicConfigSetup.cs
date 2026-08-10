using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP 품질 설정을 자동으로 구성하는 에디터 도구
/// Tools/Graphics/Apply URP Quality Settings 메뉴에서 실행
/// </summary>
public static class GraphicConfigSetup
{
    private const string MenuPath = "Tools/Graphics/Apply URP Quality Settings";

    [MenuItem(MenuPath)]
    public static void ApplyURPQualitySettings()
    {
        // === 1. URP Asset 찾기 ===
        string[] urpGuids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset");
        if (urpGuids.Length == 0)
        {
            Debug.LogError("[GraphicConfig] URP Pipeline Asset을 찾을 수 없습니다!");
            return;
        }

        string urpPath = AssetDatabase.GUIDToAssetPath(urpGuids[0]);
        var pipelineAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(urpPath);
        if (pipelineAsset == null)
        {
            Debug.LogError($"[GraphicConfig] URP Asset 로드 실패: {urpPath}");
            return;
        }

        Debug.Log($"[GraphicConfig] URP Asset 발견: {urpPath}");
        var changes = new System.Collections.Generic.List<string>();

        // === 2. SerializedObject로 URP Asset 속성 변경 ===
        var so = new SerializedObject(pipelineAsset);

        // 2a. MSAA 설정
        var msaaProp = so.FindProperty("m_MSAA");
        if (msaaProp != null)
        {
            int oldMsaa = msaaProp.intValue;
            int targetMsaa = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Vulkan
                ? 4  // Vulkan 성능 양호
                : SystemInfo.processorCount >= 8 ? 4 : 2;

            msaaProp.intValue = targetMsaa;
            so.ApplyModifiedProperties();
            string msg = $"MSAA: {oldMsaa}x → {targetMsaa}x";
            changes.Add(msg);
            Debug.Log($"[GraphicConfig] {msg}");
        }

        // 2b. SMAA Anti-aliasing (URP 17에서는 m_DilatedBPP/bufferStorage 컨트롤과 분리)
        var antialiasingProp = so.FindProperty("m_AntiAliasing");
        if (antialiasingProp != null)
        {
            int oldAa = antialiasingProp.intValue;
            antialiasingProp.intValue = 2; // 2 = SMAA
            so.ApplyModifiedProperties();
            string msg = $"Anti-aliasing: {(AntialiasingQuality)oldAa} → SMAA";
            changes.Add(msg);
            Debug.Log($"[GraphicConfig] {msg}");
        }

        // 2c. SMAA 품질 (High)
        var aaQualityProp = so.FindProperty("m_AntiAliasingQuality");
        if (aaQualityProp != null)
        {
            int oldQ = aaQualityProp.intValue;
            aaQualityProp.intValue = 2; // 2 = High
            so.ApplyModifiedProperties();
            string msg = $"SMAA 품질: {(AntialiasingQuality)oldQ} → High";
            changes.Add(msg);
            Debug.Log($"[GraphicConfig] {msg}");
        }

        EditorUtility.SetDirty(pipelineAsset);

        // === 3. Global Volume 찾기 / 생성 ===
        Volume globalVolume = Object.FindAnyObjectByType<Volume>();
        GameObject volumeGO;
        if (globalVolume != null)
        {
            volumeGO = globalVolume.gameObject;
            Debug.Log($"[GraphicConfig] 기존 Global Volume 발견: {volumeGO.name}");
        }
        else
        {
            volumeGO = new GameObject("Global Volume");
            globalVolume = volumeGO.AddComponent<Volume>();
            globalVolume.isGlobal = true;
            Debug.Log("[GraphicConfig] 새 Global Volume GameObject 생성");
            changes.Add("Global Volume 생성");
        }

        // Volume Profile 생성/로드
        string profilePath = "Assets/Settings/URP_GraphicConfig_Profile.asset";
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "URP_GraphicConfig_Profile";
            // Settings 폴더가 없을 경우 생성
            string dir = System.IO.Path.GetDirectoryName(profilePath);
            if (!System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);
            AssetDatabase.CreateAsset(profile, profilePath);
            Debug.Log($"[GraphicConfig] 새 Volume Profile 생성: {profilePath}");
            changes.Add("Volume Profile 생성");
        }
        else
        {
            Debug.Log("[GraphicConfig] 기존 Volume Profile 로드");
        }

        // === 4. Shadows Override (Contact Shadows) ===
        // 참고: URP 17에서 Shadows 볼륨 컴포넌트가 제거되었습니다.
        // Contact Shadows는 이제 Renderer Feature로 설정하거나
        // Universal Render Pipeline Asset의 Main/Additional Light 설정에서 제어합니다.
        Debug.Log("[GraphicConfig] Shadows 볼륨 컴포넌트는 URP 17에서 제거됨 (생략)");

        // === 5. Bloom Override 추가 ===
        var bloom = GetOrAddVolumeComponent<Bloom>(profile);
        if (bloom != null)
        {
            if (!bloom.threshold.overrideState)
            {
                bloom.threshold.overrideState = true;
                bloom.threshold.value = 0.9f;
                bloom.intensity.overrideState = true;
                bloom.intensity.value = 1.0f;
                bloom.scatter.overrideState = true;
                bloom.scatter.value = 0.5f;
                changes.Add("Bloom Override 추가");
                Debug.Log("[GraphicConfig] Bloom Override 추가됨");
            }
            else
            {
                changes.Add("Bloom Override 이미 존재");
                Debug.Log("[GraphicConfig] Bloom Override 이미 존재");
            }
        }

        // === 6. Volume Profile 연결 ===
        globalVolume.sharedProfile = profile;
        EditorUtility.SetDirty(globalVolume);
        EditorUtility.SetDirty(profile);

        AssetDatabase.SaveAssets();

        // === 7. 결과 로그 ===
        Debug.Log("[GraphicConfig] ===== URP 품질 설정 완료 =====");
        foreach (string c in changes)
            Debug.Log($"[GraphicConfig]   ✓ {c}");

        EditorUtility.DisplayDialog("URP 품질 설정",
            $"적용 완료!\n\n변경 사항:\n{string.Join("\n", changes.ConvertAll(c => $"• {c}"))}",
            "OK");
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateApplyURPQualitySettings() => true;

    /// <summary>
    /// VolumeProfile에서 특정 타입의 VolumeComponent를 찾거나 추가
    /// </summary>
    private static T GetOrAddVolumeComponent<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (profile.TryGet<T>(out T existing))
        {
            Debug.Log($"[GraphicConfig] 기존 {typeof(T).Name} Override 발견");
            return existing;
        }
        return profile.Add<T>(overrides: true);
    }
}
