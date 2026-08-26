using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class FixFinal_Complete
{
    [MenuItem("Tools/Poison/Fix Final Complete")]
    public static void FixComplete()
    {
        const string scenePath = "Assets/Scenes/MainScene.unity";
        var scene = SceneManager.GetSceneByName("MainScene");
        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(scenePath);
        }

        Debug.Log("=== FINAL COMPLETE FIX START ===");

        FixCameraBinding();
        FixVolumeProfile();
        FixURPRendererFeatures();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, scenePath, true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[Final] Scene saved to: {scenePath}");
        Debug.Log("=== FINAL COMPLETE FIX DONE ===");
    }

    static void FixCameraBinding()
    {
        var player = GameObject.Find("Player");
        var playerModel = player?.transform.Find("PlayerModel")?.gameObject;
        var mainCam = GameObject.Find("Main Camera");
        var vcamGo = mainCam?.transform.Find("Player Camera")?.gameObject;

        if (player == null || playerModel == null || mainCam == null || vcamGo == null)
        {
            Debug.LogError("[Final] Missing camera/player objects");
            return;
        }

        // CinemachineBrain
        var brain = mainCam.GetComponent<CinemachineBrain>();
        if (brain == null) brain = mainCam.AddComponent<CinemachineBrain>();
        SetProp(brain, "DefaultBlend", CreateBlendDefinition());

        // CinemachineCamera
        var vcam = vcamGo.GetComponent<CinemachineCamera>();
        if (vcam == null) vcam = vcamGo.AddComponent<CinemachineCamera>();
        vcam.Priority = 100;

        // CinemachineThirdPersonFollow - 리플렉션으로 속성 설정 (Cinemachine 3.x)
        var tpf = vcamGo.GetComponent<CinemachineThirdPersonFollow>();
        if (tpf == null) tpf = vcamGo.AddComponent<CinemachineThirdPersonFollow>();

        // Cinemachine 3.x uses different property names - try common ones
        SetProp(tpf, "FollowTarget", player.transform);
        SetProp(tpf, "LookAtTarget", playerModel.transform);
        SetProp(tpf, "TargetOffset", new Vector3(0, 1.5f, 0));
        SetProp(tpf, "CameraDistance", 25f);
        SetProp(tpf, "MinDistance", 15f);
        SetProp(tpf, "MaxDistance", 40f);
        SetProp(tpf, "ShoulderOffset", new Vector3(0.5f, 0f, 0f));

        // CinemachineInputAxisController
        var inputAxis = vcamGo.GetComponent<CinemachineInputAxisController>();
        if (inputAxis == null) inputAxis = vcamGo.AddComponent<CinemachineInputAxisController>();
        SetProp(inputAxis, "HorizontalAxisName", "Mouse X");
        SetProp(inputAxis, "VerticalAxisName", "Mouse Y");
        SetProp(inputAxis, "MaxSpeed", 300f);
        SetProp(inputAxis, "AccelTime", 0.1f);
        SetProp(inputAxis, "DecelTime", 0.1f);

        // CinemachineCollider
        var collider = vcamGo.GetComponent<CinemachineCollider>();
        if (collider == null) collider = vcamGo.AddComponent<CinemachineCollider>();
        SetProp(collider, "MinimumDistanceFromTarget", 0.5f);
        SetProp(collider, "MaximumDistanceFromTarget", 40f);
        SetProp(collider, "Radius", 0.3f);
        SetProp(collider, "CollideAgainstLayers", ~LayerMask.GetMask("Player", "Ignore Raycast"));
        
        var stratEnum = typeof(CinemachineCollider).GetNestedType("ResolutionStrategy", BindingFlags.Public);
        if (stratEnum != null)
        {
            SetProp(collider, "Strategy", Enum.Parse(stratEnum, "PreserveCameraDistance"));
        }

        Debug.Log("[Final] Camera binding attempted via reflection");
    }

    static object CreateBlendDefinition()
    {
        var blendDefType = typeof(CinemachineBlendDefinition);
        var styleEnum = blendDefType.GetNestedType("Style", BindingFlags.Public);
        if (styleEnum != null)
        {
            var easeInOut = Enum.Parse(styleEnum, "EaseInOut");
            var blendCtor = blendDefType.GetConstructor(new[] { styleEnum, typeof(float) });
            if (blendCtor != null)
                return blendCtor.Invoke(new object[] { easeInOut, 1.5f });
        }
        return null;
    }

    static void FixVolumeProfile()
    {
        var volumeGo = GameObject.Find("GlobalVolume");
        if (volumeGo == null) return;

        var volume = volumeGo.GetComponent<Volume>();
        if (volume?.profile != null)
        {
            // Ensure profile has a name
            if (string.IsNullOrEmpty(volume.profile.name))
            {
                volume.profile.name = "GlobalVolumeProfile";
            }
            Debug.Log($"[Final] Volume Profile: {volume.profile.name}, IsGlobal={volume.isGlobal}");
        }
    }

    static void FixURPRendererFeatures()
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

        // Use reflection to modify the internal list
        var featuresField = typeof(ScriptableRendererData).GetField("m_RendererFeatures", BindingFlags.NonPublic | BindingFlags.Instance);
        if (featuresField == null) return;

        var list = featuresField.GetValue(rendererData) as List<ScriptableRendererFeature>;
        if (list == null)
        {
            list = new List<ScriptableRendererFeature>();
            featuresField.SetValue(rendererData, list);
        }

        foreach (var (type, name) in featuresToAdd)
        {
            bool exists = false;
            foreach (var f in list)
            {
                if (f != null && f.GetType() == type)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                var feature = ScriptableObject.CreateInstance(type) as ScriptableRendererFeature;
                if (feature != null)
                {
                    feature.name = name;
                    list.Add(feature);
                    Debug.Log($"[Final] Added URP Renderer Feature: {name}");
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

    static void SetProp(object obj, string name, object value)
    {
        if (obj == null || value == null) return;
        var type = obj.GetType();
        var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(obj, value);
        }
        else if (field != null)
        {
            field.SetValue(obj, value);
        }
        else
        {
            // Try with different casing
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var p in props)
            {
                if (p.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && p.CanWrite)
                {
                    p.SetValue(obj, value);
                    return;
                }
            }
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var f in fields)
            {
                if (f.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    f.SetValue(obj, value);
                    return;
                }
            }
            Debug.LogWarning($"[Final] Property/Field '{name}' not found on {type.Name}");
        }
    }
}