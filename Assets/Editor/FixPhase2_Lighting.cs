using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using System.Reflection;

public class FixPhase2_Lighting
{
    [MenuItem("Tools/Poison/Fix Phase 2 - Lighting")]
    public static void FixLighting()
    {
        const string scenePath = "Assets/Scenes/MainScene.unity";
        var scene = SceneManager.GetSceneByName("MainScene");
        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(scenePath);
        }

        Debug.Log("=== PHASE 2: LIGHTING CREATION START ===");

        // 1. Sun (주간 메인 라이트)
        var sunObj = GameObject.Find("Sun");
        if (sunObj == null)
        {
            sunObj = new GameObject("Sun");
            Debug.Log("[Phase2] Created Sun GameObject");
        }
        else
        {
            Debug.Log("[Phase2] Found existing Sun GameObject");
        }

        var sun = sunObj.GetComponent<Light>();
        if (sun == null) sun = sunObj.AddComponent<Light>();

        sun.type = LightType.Directional;
        sun.intensity = 1.3f;
        sun.color = new Color(1f, 0.96f, 0.88f); // 따뜻한 햇살
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.8f;
        sun.shadowNormalBias = 0.3f;
        sun.shadowNearPlane = 0.2f;
        sun.renderMode = LightRenderMode.Auto;
        sunObj.transform.rotation = Quaternion.Euler(50f, -30f, 0); // BotW 스타일 각도

        // 2. Moon (야간 라이트)
        var moonObj = GameObject.Find("Moon");
        if (moonObj == null)
        {
            moonObj = new GameObject("Moon");
            Debug.Log("[Phase2] Created Moon GameObject");
        }
        else
        {
            Debug.Log("[Phase2] Found existing Moon GameObject");
        }

        var moon = moonObj.GetComponent<Light>();
        if (moon == null) moon = moonObj.AddComponent<Light>();

        moon.type = LightType.Directional;
        moon.intensity = 0.15f;
        moon.color = new Color(0.6f, 0.65f, 0.85f); // 차가운 달빛
        moon.shadows = LightShadows.None;
        moonObj.transform.rotation = Quaternion.Euler(-40f, 150f, 0);
        moon.enabled = false; // DayNightCycle이 제어

        // 3. Skybox 설정
        var skyboxMat = new Material(Shader.Find("Skybox/Procedural"));
        RenderSettings.skybox = skyboxMat;
        RenderSettings.sun = sun;
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.reflectionIntensity = 0.8f;

        // 4. Fog (RenderSettings 백업)
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.6f, 0.7f, 0.85f);
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.0006f;

        // 5. DayNightCycle 연결 (리플렉션으로 private 필드 설정)
        var cycle = sunObj.GetComponent<ProjectName.Systems.DayNightCycle>();
        if (cycle == null) cycle = sunObj.AddComponent<ProjectName.Systems.DayNightCycle>();
        
        var cycleType = cycle.GetType();
        var sunField = cycleType.GetField("_sunLight", BindingFlags.NonPublic | BindingFlags.Instance);
        var moonField = cycleType.GetField("_moonLight", BindingFlags.NonPublic | BindingFlags.Instance);
        var durationField = cycleType.GetField("_dayDuration", BindingFlags.NonPublic | BindingFlags.Instance) 
                         ?? cycleType.GetField("dayDuration", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
        var sunriseField = cycleType.GetField("_sunriseHour", BindingFlags.NonPublic | BindingFlags.Instance)
                          ?? cycleType.GetField("sunriseHour", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
        var sunsetField = cycleType.GetField("_sunsetHour", BindingFlags.NonPublic | BindingFlags.Instance)
                         ?? cycleType.GetField("sunsetHour", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
        
        if (sunField != null) sunField.SetValue(cycle, sun);
        if (moonField != null) moonField.SetValue(cycle, moon);
        if (durationField != null) durationField.SetValue(cycle, 1200f);
        if (sunriseField != null) sunriseField.SetValue(cycle, 6f);
        if (sunsetField != null) sunsetField.SetValue(cycle, 18f);
        
        Debug.Log("[Phase2] DayNightCycle configured via reflection");

        // 6. 씬 저장
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, scenePath, true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[Phase2] Scene saved to: {scenePath}");
        Debug.Log("=== PHASE 2: LIGHTING CREATION COMPLETE ===");
    }
}