using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class FixPhase5_PostProcess
{
    [MenuItem("Tools/Poison/Fix Phase 5 - PostProcess")]
    public static void FixPostProcess()
    {
        const string scenePath = "Assets/Scenes/MainScene.unity";
        var scene = SceneManager.GetSceneByName("MainScene");
        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(scenePath);
        }

        Debug.Log("=== PHASE 5: POST PROCESS START ===");

        // 1. Global Volume 생성/찾기
        var volumeGo = GameObject.Find("GlobalVolume");
        if (volumeGo == null)
        {
            volumeGo = new GameObject("GlobalVolume");
            Debug.Log("[Phase5] Created GlobalVolume");
        }

        var volume = volumeGo.GetComponent<Volume>();
        if (volume == null) volume = volumeGo.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 0;

        // 2. Volume Profile 생성
        var profile = volume.profile;
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.profile = profile;
        }

        // Clear existing overrides
        profile.components.Clear();

        // 3. Bloom (BotW 스타일 따뜻한 빛)
        AddOverride(profile, typeof(Bloom), (b) => {
            SetVolProp(b, "intensity", 0.35f);
            SetVolProp(b, "threshold", 0.95f);
            SetVolProp(b, "scatter", 0.7f);
            SetVolProp(b, "tint", new Color(1f, 0.95f, 0.85f));
            SetVolProp(b, "highQualityFiltering", true);
        });

        // 4. Color Adjustments (BotW 따뜻한 톤) - URP에서는 ColorAdjustments
        AddOverride(profile, "UnityEngine.Rendering.Universal.ColorAdjustments", (c) => {
            SetVolProp(c, "postExposure", 0.2f);
            SetVolProp(c, "contrast", 12f);
            SetVolProp(c, "colorFilter", new Color(1f, 0.95f, 0.85f, 0.3f));
            SetVolProp(c, "saturation", 15f);
        });

        // 5. LiftGammaGain
        AddOverride(profile, "UnityEngine.Rendering.Universal.LiftGammaGain", (l) => {
            SetVolProp(l, "gamma", new Vector4(1.05f, 1.02f, 0.98f, 1f));
        });

        // 6. Tonemapping (ACES)
        AddOverride(profile, typeof(Tonemapping), (t) => {
            SetVolProp(t, "mode", TonemappingMode.ACES);
        });

        // 7. Fog (Volume-based 대기 원근법) - URP Fog
        AddOverride(profile, "UnityEngine.Rendering.Universal.Fog", (f) => {
            SetVolProp(f, "active", true);
            SetVolProp(f, "color", new Color(0.6f, 0.7f, 0.85f));
            SetVolProp(f, "meanFreePath", 800f);
            SetVolProp(f, "maxFogDistance", 2000f);
            SetVolProp(f, "skyFog", 1f);
        });

        // 8. Vignette (약간)
        AddOverride(profile, typeof(Vignette), (v) => {
            SetVolProp(v, "intensity", 0.15f);
            SetVolProp(v, "smoothness", 0.4f);
        });

        // 9. 프로파일 에셋으로 저장
        var profilePath = "Assets/Resources/PostProcessing/GlobalVolumeProfile.asset";
        if (!AssetDatabase.IsValidFolder("Assets/Resources/PostProcessing"))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "PostProcessing");
        }
        AssetDatabase.CreateAsset(profile, profilePath);

        // 10. RenderSettings Fog (백업)
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.6f, 0.7f, 0.85f);
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.0006f;

        // 11. URP Renderer Features 추가
        AddURPRendererFeatures();

        // 12. 씬 저장
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, scenePath, true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[Phase5] Scene saved to: {scenePath}");
        Debug.Log("=== PHASE 5: POST PROCESS COMPLETE ===");
    }

    static void AddOverride(VolumeProfile profile, Type type, Action<object> configure)
    {
        var component = profile.Add(type, true);
        if (component != null)
        {
            configure(component);
        }
    }

    static void AddOverride(VolumeProfile profile, string typeName, Action<object> configure)
    {
        var type = Type.GetType(typeName);
        if (type != null)
        {
            var component = profile.Add(type, true);
            if (component != null)
            {
                configure(component);
            }
        }
        else
        {
            Debug.LogWarning($"[Phase5] Type not found: {typeName}");
        }
    }

    static void SetVolProp(object obj, string name, object value)
    {
        if (obj == null) return;
        var type = obj.GetType();
        var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop != null && prop.CanWrite)
        {
            var overrideProp = prop.GetValue(obj);
            if (overrideProp != null)
            {
                var valueProp = overrideProp.GetType().GetProperty("value", BindingFlags.Public | BindingFlags.Instance);
                if (valueProp != null && valueProp.CanWrite)
                {
                    valueProp.SetValue(overrideProp, value);
                }
                else
                {
                    var overrideMethod = overrideProp.GetType().GetMethod("Override", new[] { value.GetType() });
                    if (overrideMethod != null)
                    {
                        overrideMethod.Invoke(overrideProp, new[] { value });
                    }
                }
            }
        }
    }

    static void AddURPRendererFeatures()
    {
        var urp = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null) return;

        var rendererDataList = GetRendererDataList(urp);
        if (rendererDataList.Count == 0) return;

        var rendererData = rendererDataList[0] as UniversalRendererData;
        if (rendererData == null) return;

        var featuresToAdd = new (Type type, string name)[]
        {
            (typeof(Bloom), "Bloom"),
            (typeof(ColorAdjustments), "ColorAdjustments"),
            (typeof(Tonemapping), "Tonemapping"),
            (typeof(ScreenSpaceAmbientOcclusion), "SSAO"),
            (typeof(DepthOfField), "DepthOfField"),
        };

        foreach (var (type, name) in featuresToAdd)
        {
            bool exists = false;
            if (rendererData.rendererFeatures != null)
            {
                foreach (var f in rendererData.rendererFeatures)
                {
                    if (f != null && f.GetType() == type)
                    {
                        exists = true;
                        break;
                    }
                }
            }

            if (!exists)
            {
                var feature = ScriptableObject.CreateInstance(type) as ScriptableRendererFeature;
                if (feature != null)
                {
                    feature.name = name;
                    // rendererFeatures is read-only, use reflection to add to the underlying list
                    var featuresField = typeof(ScriptableRendererData).GetField("m_RendererFeatures", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (featuresField != null)
                    {
                        var list = featuresField.GetValue(rendererData) as List<ScriptableRendererFeature>;
                        if (list == null)
                        {
                            list = new List<ScriptableRendererFeature>();
                            featuresField.SetValue(rendererData, list);
                        }
                        list.Add(feature);
                    }
                    Debug.Log($"[Phase5] Added URP Renderer Feature: {name}");
                }
            }
        }

        EditorUtility.SetDirty(rendererData);
        AssetDatabase.SaveAssets();
    }

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
                {
                    if (item is ScriptableRendererData srd)
                        list.Add(srd);
                }
            }
        }
        return list;
    }
}