using UnityEditor;
using UnityEngine;

/// <summary>
/// C7-18: 스카이박스 설정 도구.
/// RenderSettings에 절차적 하늘 머티리얼 + 방향광 매칭 적용.
/// </summary>
public static class SkyboxSetup
{
    [MenuItem("Tools/C7-18 - Setup Skybox")]
    public static void ApplySkybox()
    {
        // 절차적 스카이박스 셰이더
        Shader skyShader = Shader.Find("Skybox/Procedural");
        if (skyShader == null)
        {
            // Fallback: 기본 Lit 셰이더로 Material 생성
            skyShader = Shader.Find("Universal Render Pipeline/Lit");
            if (skyShader == null)
            {
                Debug.LogError("[SkyboxSetup] 적합한 셰이더를 찾을 수 없습니다.");
                return;
            }
        }

        Material skyMat = new Material(skyShader);

        // Procedural Skybox 파라미터
        skyMat.SetFloat("_AtmosphereThickness", 1f);
        skyMat.SetFloat("_SunSize", 0.04f);
        skyMat.SetFloat("_SunSizeConvergence", 5f);
        skyMat.SetColor("_SkyTint", new Color(0.5f, 0.7f, 1.0f));   // 하늘색
        skyMat.SetColor("_GroundColor", new Color(0.4f, 0.3f, 0.2f)); // 지평선 색
        skyMat.SetFloat("_Exposure", 1.3f);

        // RenderSettings 적용
        RenderSettings.skybox = skyMat;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;

        // Directional Light 색상 매칭
        Light sun = Object.FindAnyObjectByType<Light>();
        if (sun != null && sun.type == LightType.Directional)
        {
            sun.color = new Color(1f, 0.95f, 0.85f); // 따뜻한 햇빛
            sun.intensity = 1.2f;
        }

        Debug.Log("[SkyboxSetup] 스카이박스 설정 완료!");
        EditorApplication.delayCall += () =>
        {
            EditorUtility.SetDirty(skyMat);
            AssetDatabase.Refresh();
        };
    }

    [MenuItem("Tools/C7-18 - Setup Skybox", true)]
    private static bool Validate() => !Application.isPlaying;
}
