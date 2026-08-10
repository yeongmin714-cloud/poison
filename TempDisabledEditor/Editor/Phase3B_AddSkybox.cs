using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class Phase3B_AddSkybox
{
    [MenuItem("Tools/Phase 3B - Add Skybox to Top-Down Scene")]
    public static void AddSkyboxToTopDownScene()
    {
        // ===== 1. 기존 TopDownScene 열기 =====
        string scenePath = "Assets/Scenes/TopDownScene.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError($"[Phase3B] Failed to open scene: {scenePath}");
            return;
        }

        // ===== 2. RenderSettings.skybox에 Procedural Skybox 머티리얼 할당 =====
        Shader proceduralSkyboxShader = Shader.Find("Skybox/Procedural");
        Material skyboxMat = null;

        if (proceduralSkyboxShader != null)
        {
            skyboxMat = new Material(proceduralSkyboxShader);
            skyboxMat.name = "ProceduralSkybox_Dynamic";

            // Procedural Skybox 파라미터 설정
            // Sun (디렉셔널 라이트 방향에서 태양 위치 결정)
            skyboxMat.SetFloat("_SunSize", 0.04f);
            skyboxMat.SetFloat("_SunSizeConvergence", 5);
            skyboxMat.SetFloat("_AtmosphereThickness", 1.0f);
            skyboxMat.SetColor("_SkyTint", new Color(0.5f, 0.6f, 0.8f));   // 하늘 색조 (밝은 파랑)
            skyboxMat.SetColor("_GroundColor", new Color(0.369f, 0.349f, 0.341f)); // 지평선 아래 색
            skyboxMat.SetFloat("_Exposure", 1.0f);

            RenderSettings.skybox = skyboxMat;
            Debug.Log("[Phase3B] Procedural Skybox material created and assigned.");
        }
        else
        {
            Debug.LogWarning("[Phase3B] Skybox/Procedural shader not found. Using default skybox.");
            // Fallback: built-in default skybox
            var defaultSkybox = Resources.GetBuiltinResource(typeof(Material), "Default-Skybox.mat") as Material;
            if (defaultSkybox != null)
            {
                RenderSettings.skybox = defaultSkybox;
                Debug.Log("[Phase3B] Default skybox assigned.");
            }
        }

        // ===== 3. Main Camera clearFlags를 Skybox로 변경 =====
        Camera mainCam = GameObject.Find("Main Camera")?.GetComponent<Camera>();
        if (mainCam != null)
        {
            mainCam.clearFlags = CameraClearFlags.Skybox;
            Debug.Log("[Phase3B] Camera clearFlags set to Skybox.");
        }
        else
        {
            Debug.LogWarning("[Phase3B] Main Camera not found. Creating one...");
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            mainCam = camGO.AddComponent<Camera>();
            mainCam.clearFlags = CameraClearFlags.Skybox;
            mainCam.orthographic = false;
            mainCam.fieldOfView = 30f;
            mainCam.nearClipPlane = 0.1f;
            mainCam.farClipPlane = 500f;
        }

        // ===== 4. Global Volume에 Tonemapping + Color Adjustments 추가 =====
        Volume volume = GameObject.Find("Global Volume")?.GetComponent<Volume>();
        VolumeProfile volProfile = null;

        if (volume != null)
        {
            if (volume.profile != null)
            {
                volProfile = volume.profile;
                Debug.Log("[Phase3B] Using existing Volume profile.");
            }
            else
            {
                volProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                volProfile.name = "Default_VolumeProfile_Enhanced";
                volume.profile = volProfile;
                Debug.Log("[Phase3B] Created new Volume profile.");
            }
        }
        else
        {
            Debug.LogWarning("[Phase3B] Global Volume not found. Creating one...");
            var volumeGO = new GameObject("Global Volume");
            volume = volumeGO.AddComponent<Volume>();
            volume.isGlobal = true;
            volProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            volProfile.name = "Default_VolumeProfile_Enhanced";
            volume.profile = volProfile;
        }

        // Tonemapping 추가 (ACES - 가장 자연스러운 톤 매핑)
        if (!volProfile.Has<Tonemapping>())
        {
            var tonemapping = volProfile.Add<Tonemapping>(overrides: true);
            tonemapping.mode.overrideState = true;
            tonemapping.mode.value = TonemappingMode.ACES;
            Debug.Log("[Phase3B] Tonemapping (ACES) added to Volume profile.");
        }
        else
        {
            Debug.Log("[Phase3B] Tonemapping already exists in Volume profile.");
        }

        // Color Adjustments 추가 (contrast/saturation 미세 조정)
        if (!volProfile.Has<ColorAdjustments>())
        {
            var colorAdj = volProfile.Add<ColorAdjustments>(overrides: true);
            colorAdj.postExposure.overrideState = true;
            colorAdj.postExposure.value = 0.0f;
            colorAdj.contrast.overrideState = true;
            colorAdj.contrast.value = 5.0f;     // 약간 대비 증가
            colorAdj.saturation.overrideState = true;
            colorAdj.saturation.value = 10.0f;   // 약간 채도 증가
            colorAdj.hueShift.overrideState = true;
            colorAdj.hueShift.value = 0.0f;
            Debug.Log("[Phase3B] Color Adjustments added to Volume profile.");
        }
        else
        {
            Debug.Log("[Phase3B] Color Adjustments already exists in Volume profile.");
        }

        // ===== 5. 씬 저장 =====
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log($"[Phase3B] Skybox + Volume effects applied → {scenePath}");

        // 생성한 에셋 저장 (메모리에서 사라지지 않도록)
        if (skyboxMat != null)
        {
            string matPath = "Assets/Materials/ProceduralSkybox_Dynamic.mat";
            System.IO.Directory.CreateDirectory("Assets/Materials");
            AssetDatabase.CreateAsset(skyboxMat, matPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Phase3B] Skybox material saved to: {matPath}");
        }

        // Volume 프로필 에셋 저장
        if (volProfile != null && !AssetDatabase.Contains(volProfile))
        {
            string profilePath = "Assets/Settings/Default_VolumeProfile_Enhanced.asset";
            System.IO.Directory.CreateDirectory("Assets/Settings");
            AssetDatabase.CreateAsset(volProfile, profilePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Phase3B] Volume profile saved to: {profilePath}");
        }
    }

    [MenuItem("Tools/Phase 3B - Add Skybox to Top-Down Scene", true)]
    private static bool Validate() => true;
}
