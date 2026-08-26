using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

public class DeepDiagnoseScene
{
    private static Dictionary<string, object> report = new Dictionary<string, object>();

    [MenuItem("Tools/Poison/Deep Diagnose MainScene")]
    public static void Diagnose()
    {
        report.Clear();
        
        // Ensure MainScene is loaded
        var scene = SceneManager.GetSceneByName("MainScene");
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        }

        Debug.Log("=== DEEP DIAGNOSE START ===");
        
        LogCameraSystem();
        LogTerrainSystem();
        LogPlayerModelSystem();
        LogPostProcessingSystem();
        LogLightingFogSystem();
        LogHUDSystem();
        LogURPSystem();
        
        // Save report
        var json = JsonUtility.ToJson(new SerializableDict(report), true);
        File.WriteAllText("Assets/DiagnoseReport.json", json);
        Debug.Log($"Report saved to Assets/DiagnoseReport.json");
        
        // Capture screenshot
        ScreenCapture.CaptureScreenshot("Diagnose_BeforeFix.png");
        Debug.Log("Screenshot saved: Diagnose_BeforeFix.png");
        
        Debug.Log("=== DEEP DIAGNOSE COMPLETE ===");
    }

    static void LogCameraSystem()
    {
        var section = new Dictionary<string, object>();
        var mainCam = GameObject.Find("Main Camera");
        
        if (mainCam == null)
        {
            section["MainCamera"] = "NOT_FOUND";
            report["CameraSystem"] = section;
            Debug.LogError("[Camera] Main Camera NOT FOUND");
            return;
        }
        
        section["MainCamera"] = "FOUND";
        section["MainCamera_Position"] = mainCam.transform.position.ToString();
        section["MainCamera_Rotation"] = mainCam.transform.rotation.eulerAngles.ToString();
        
        var brain = mainCam.GetComponent<CinemachineBrain>();
        section["CinemachineBrain"] = brain != null ? "FOUND" : "MISSING";
        if (brain != null)
        {
            section["Brain_DefaultBlend"] = GetPropertyOrField(brain, "DefaultBlend")?.ToString() ?? "NULL";
        }
        
        var vcamObj = mainCam.transform.Find("Player Camera");
        if (vcamObj == null)
        {
            section["VirtualCamera"] = "NOT_FOUND";
            report["CameraSystem"] = section;
            Debug.LogError("[Camera] Player Camera NOT FOUND under Main Camera");
            return;
        }
        
        section["VirtualCamera"] = "FOUND";
        section["VCam_Position"] = vcamObj.position.ToString();
        section["VCam_Rotation"] = vcamObj.rotation.eulerAngles.ToString();
        
        // List all components on vcam
        var vcamComponents = vcamObj.GetComponents<Component>();
        var compNames = new List<string>();
        foreach (var c in vcamComponents)
        {
            if (c != null) compNames.Add(c.GetType().Name);
        }
        section["VCam_Components"] = compNames;
        
        // Use reflection to get properties from Cinemachine components
        foreach (var c in vcamComponents)
        {
            if (c == null) continue;
            var type = c.GetType();
            var typeName = type.Name;
            
            if (typeName.Contains("Follow") || typeName.Contains("ThirdPerson"))
            {
                section[$"{typeName}_FollowTarget"] = GetPropertyOrField(c, "Follow") ?? GetPropertyOrField(c, "TargetObject") ?? GetPropertyOrField(c, "FollowTarget") ?? "NULL";
                section[$"{typeName}_LookAtTarget"] = GetPropertyOrField(c, "LookAt") ?? GetPropertyOrField(c, "LookAtTarget") ?? "NULL";
                section[$"{typeName}_VerticalOffset"] = GetPropertyOrField(c, "VerticalOffset")?.ToString() ?? "NULL";
                section[$"{typeName}_HorizontalOffset"] = GetPropertyOrField(c, "HorizontalOffset")?.ToString() ?? "NULL";
                section[$"{typeName}_CameraDistance"] = GetPropertyOrField(c, "CameraDistance")?.ToString() ?? "NULL";
                section[$"{typeName}_MinCameraDistance"] = GetPropertyOrField(c, "MinCameraDistance")?.ToString() ?? "NULL";
                section[$"{typeName}_MaxCameraDistance"] = GetPropertyOrField(c, "MaxCameraDistance")?.ToString() ?? "NULL";
                section[$"{typeName}_ShoulderOffset"] = GetPropertyOrField(c, "ShoulderOffset")?.ToString() ?? "NULL";
            }
            
            if (typeName.Contains("InputAxis"))
            {
                section[$"{typeName}_HorizontalAxis"] = GetPropertyOrField(c, "HorizontalAxis")?.ToString() ?? "NULL";
                section[$"{typeName}_VerticalAxis"] = GetPropertyOrField(c, "VerticalAxis")?.ToString() ?? "NULL";
                section[$"{typeName}_MaxSpeed"] = GetPropertyOrField(c, "MaxSpeed")?.ToString() ?? "NULL";
            }
        }
        
        report["CameraSystem"] = section;
        Debug.Log($"[Camera] Components: {string.Join(", ", compNames)}");
    }

    static object GetPropertyOrField(object obj, string name)
    {
        if (obj == null) return null;
        var type = obj.GetType();
        var prop = type.GetProperty(name);
        if (prop != null) return prop.GetValue(obj);
        var field = type.GetField(name);
        if (field != null) return field.GetValue(obj);
        return null;
    }

    static void LogTerrainSystem()
    {
        var section = new Dictionary<string, object>();
        var terrainObj = GameObject.Find("Terrain");
        
        if (terrainObj == null)
        {
            section["TerrainObject"] = "NOT_FOUND";
            report["TerrainSystem"] = section;
            Debug.LogError("[Terrain] Terrain object NOT FOUND");
            return;
        }
        
        section["TerrainObject"] = "FOUND";
        section["Terrain_Position"] = terrainObj.transform.position.ToString();
        section["Terrain_Scale"] = terrainObj.transform.lossyScale.ToString();
        section["Terrain_Layer"] = LayerMask.LayerToName(terrainObj.layer);
        section["Terrain_IsStatic"] = terrainObj.isStatic;
        
        var mf = terrainObj.GetComponent<MeshFilter>();
        section["MeshFilter"] = mf != null ? "FOUND" : "MISSING";
        if (mf != null && mf.mesh != null)
        {
            section["Mesh_Name"] = mf.mesh.name;
            section["Mesh_VertexCount"] = mf.mesh.vertexCount;
            section["Mesh_TriangleCount"] = mf.mesh.triangles.Length / 3;
            section["Mesh_Bounds"] = mf.mesh.bounds.ToString();
        }
        else
        {
            section["Mesh"] = "NULL_OR_MISSING";
        }
        
        var mr = terrainObj.GetComponent<MeshRenderer>();
        section["MeshRenderer"] = mr != null ? "FOUND" : "MISSING";
        if (mr != null)
        {
            section["Materials_Count"] = mr.materials?.Length ?? 0;
            section["SharedMaterials_Count"] = mr.sharedMaterials?.Length ?? 0;
            if (mr.sharedMaterials != null)
            {
                var matNames = new List<string>();
                foreach (var m in mr.sharedMaterials)
                {
                    matNames.Add(m != null ? m.name : "NULL");
                }
                section["Material_Names"] = matNames;
            }
            section["ShadowCastingMode"] = mr.shadowCastingMode.ToString();
            section["ReceiveShadows"] = mr.receiveShadows;
        }
        
        // Check NationTerrainController
        var controller = Object.FindFirstObjectByType<ProjectName.Systems.NationTerrainController>();
        section["NationTerrainController"] = controller != null ? "FOUND" : "MISSING";
        if (controller != null)
        {
            var type = controller.GetType();
            var field = type.GetField("NationMaterials");
            var prop = type.GetProperty("NationMaterials");
            if (field != null)
            {
                var mats = field.GetValue(controller) as Material[];
                section["Controller_NationMaterials_Count"] = mats?.Length ?? 0;
                if (mats != null)
                {
                    var matNames = new List<string>();
                    foreach (var m in mats) matNames.Add(m != null ? m.name : "NULL");
                    section["Controller_Material_Names"] = matNames;
                }
            }
            else if (prop != null)
            {
                var mats = prop.GetValue(controller) as Material[];
                section["Controller_NationMaterials_Count"] = mats?.Length ?? 0;
                if (mats != null)
                {
                    var matNames = new List<string>();
                    foreach (var m in mats) matNames.Add(m != null ? m.name : "NULL");
                    section["Controller_Material_Names"] = matNames;
                }
            }
        }
        
        report["TerrainSystem"] = section;
        Debug.Log($"[Terrain] Mesh={mf?.mesh?.name ?? "NULL"}, Materials={mr?.sharedMaterials?.Length ?? 0}");
    }

    static void LogPlayerModelSystem()
    {
        var section = new Dictionary<string, object>();
        var player = GameObject.Find("Player");
        
        if (player == null)
        {
            section["Player"] = "NOT_FOUND";
            report["PlayerModelSystem"] = section;
            Debug.LogError("[Player] Player object NOT FOUND");
            return;
        }
        
        section["Player"] = "FOUND";
        section["Player_Position"] = player.transform.position.ToString();
        section["Player_Tag"] = player.tag;
        section["Player_Layer"] = LayerMask.LayerToName(player.layer);
        
        var cc = player.GetComponent<CharacterController>();
        section["CharacterController"] = cc != null ? "FOUND" : "MISSING";
        if (cc != null)
        {
            section["CC_Height"] = cc.height;
            section["CC_Radius"] = cc.radius;
            section["CC_Center"] = cc.center.ToString();
        }
        
        // Core components - use GetComponents to find them
        var allComponents = player.GetComponents<Component>();
        var compNames = new List<string>();
        foreach (var c in allComponents)
        {
            if (c != null) compNames.Add(c.GetType().Name);
        }
        section["Player_AllComponents"] = compNames;
        
        var playerInput = player.GetComponent<UnityEngine.InputSystem.PlayerInput>();
        section["PlayerInput"] = playerInput != null ? "FOUND" : "MISSING";
        if (playerInput != null)
        {
            section["PlayerInput_Actions"] = playerInput.actions != null ? playerInput.actions.name : "NULL";
            section["PlayerInput_DefaultActionMap"] = playerInput.defaultActionMap;
        }
        
        // PlayerModel
        var modelObj = player.transform.Find("PlayerModel");
        if (modelObj == null)
        {
            section["PlayerModel"] = "NOT_FOUND";
            report["PlayerModelSystem"] = section;
            Debug.LogError("[PlayerModel] PlayerModel child NOT FOUND");
            return;
        }
        
        section["PlayerModel"] = "FOUND";
        section["PlayerModel_Position"] = modelObj.localPosition.ToString();
        section["PlayerModel_Rotation"] = modelObj.localRotation.eulerAngles.ToString();
        section["PlayerModel_Scale"] = modelObj.localScale.ToString();
        
        var animator = modelObj.GetComponent<Animator>();
        section["Animator"] = animator != null ? "FOUND" : "MISSING";
        if (animator != null)
        {
            section["Animator_RuntimeController"] = animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "NULL";
            section["Animator_Avatar"] = animator.avatar != null ? animator.avatar.name : "NULL";
            section["Animator_HasTransformHierarchy"] = animator.hasTransformHierarchy;
        }
        
        var assigner = modelObj.GetComponent<ProjectName.Systems.Animation.ModelAnimatorAssigner>();
        section["ModelAnimatorAssigner"] = assigner != null ? "FOUND" : "MISSING";
        if (assigner != null)
        {
            var type = assigner.GetType();
            var field = type.GetField("modelType");
            var prop = type.GetProperty("modelType");
            if (field != null) section["Assigner_ModelType"] = field.GetValue(assigner)?.ToString() ?? "NULL";
            else if (prop != null) section["Assigner_ModelType"] = prop.GetValue(assigner)?.ToString() ?? "NULL";
        }
        
        // Check for SkinnedMeshRenderer in children
        var smrs = modelObj.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        section["SkinnedMeshRenderer_Count"] = smrs.Length;
        var smrDetails = new List<Dictionary<string, object>>();
        foreach (var smr in smrs)
        {
            var smrInfo = new Dictionary<string, object>
            {
                ["Name"] = smr.name,
                ["SharedMesh"] = smr.sharedMesh != null ? smr.sharedMesh.name : "NULL",
                ["Materials_Count"] = smr.sharedMaterials?.Length ?? 0,
                ["RootBone"] = smr.rootBone != null ? smr.rootBone.name : "NULL",
                ["Bounds"] = smr.bounds.ToString(),
                ["Enabled"] = smr.enabled
            };
            if (smr.sharedMaterials != null)
            {
                var mats = new List<string>();
                foreach (var m in smr.sharedMaterials) mats.Add(m != null ? m.name : "NULL");
                smrInfo["Material_Names"] = mats;
            }
            smrDetails.Add(smrInfo);
        }
        section["SkinnedMeshRenderers"] = smrDetails;
        
        // Also check MeshRenderers (fallback)
        var mrs = modelObj.GetComponentsInChildren<MeshRenderer>(true);
        section["MeshRenderer_Count"] = mrs.Length;
        var mrDetails = new List<Dictionary<string, object>>();
        foreach (var mr in mrs)
        {
            var mrInfo = new Dictionary<string, object>
            {
                ["Name"] = mr.name,
                ["Materials_Count"] = mr.sharedMaterials?.Length ?? 0,
                ["Bounds"] = mr.bounds.ToString(),
                ["Enabled"] = mr.enabled
            };
            if (mr.sharedMaterials != null)
            {
                var mats = new List<string>();
                foreach (var m in mr.sharedMaterials) mats.Add(m != null ? m.name : "NULL");
                mrInfo["Material_Names"] = mats;
            }
            mrDetails.Add(mrInfo);
        }
        section["MeshRenderers"] = mrDetails;
        
        report["PlayerModelSystem"] = section;
        Debug.Log($"[PlayerModel] SMRs={smrs.Length}, MRs={mrs.Length}, AnimatorController={animator?.runtimeAnimatorController?.name ?? "NULL"}");
    }

    static void LogPostProcessingSystem()
    {
        var section = new Dictionary<string, object>();
        var volumeObj = GameObject.Find("GlobalVolume");
        
        if (volumeObj == null)
        {
            section["GlobalVolume"] = "NOT_FOUND";
            report["PostProcessingSystem"] = section;
            Debug.LogError("[PostProcess] GlobalVolume NOT FOUND");
            return;
        }
        
        section["GlobalVolume"] = "FOUND";
        var volume = volumeObj.GetComponent<Volume>();
        section["Volume_Component"] = volume != null ? "FOUND" : "MISSING";
        if (volume != null)
        {
            section["IsGlobal"] = volume.isGlobal;
            section["Priority"] = volume.priority;
            section["Profile"] = volume.profile != null ? volume.profile.name : "NULL";
            if (volume.profile != null)
            {
                var components = new List<string>();
                foreach (var comp in volume.profile.components)
                {
                    if (comp != null && comp.active)
                    {
                        components.Add($"{comp.GetType().Name}(active={comp.active})");
                    }
                }
                section["Profile_Components"] = components;
            }
        }
        
        // Check RenderSettings fog
        section["RenderSettings_Fog"] = RenderSettings.fog;
        section["RenderSettings_FogMode"] = RenderSettings.fogMode.ToString();
        section["RenderSettings_FogColor"] = RenderSettings.fogColor.ToString();
        section["RenderSettings_FogDensity"] = RenderSettings.fogDensity;
        
        report["PostProcessingSystem"] = section;
        Debug.Log($"[PostProcess] IsGlobal={volume?.isGlobal}, Profile={volume?.profile?.name ?? "NULL"}, Fog={RenderSettings.fog}");
    }

    static void LogLightingFogSystem()
    {
        var section = new Dictionary<string, object>();
        
        // Sun
        var sunObj = GameObject.Find("Sun");
        if (sunObj != null)
        {
            section["Sun"] = "FOUND";
            var sun = sunObj.GetComponent<Light>();
            if (sun != null)
            {
                section["Sun_Type"] = sun.type.ToString();
                section["Sun_Intensity"] = sun.intensity;
                section["Sun_Color"] = sun.color.ToString();
                section["Sun_Shadows"] = sun.shadows.ToString();
                section["Sun_ShadowStrength"] = sun.shadowStrength;
                section["Sun_Rotation"] = sunObj.transform.rotation.eulerAngles.ToString();
            }
        }
        else
        {
            section["Sun"] = "NOT_FOUND";
        }
        
        // Moon
        var moonObj = GameObject.Find("Moon");
        if (moonObj != null)
        {
            section["Moon"] = "FOUND";
            var moon = moonObj.GetComponent<Light>();
            if (moon != null)
            {
                section["Moon_Intensity"] = moon.intensity;
                section["Moon_Color"] = moon.color.ToString();
                section["Moon_Enabled"] = moon.enabled;
            }
        }
        else
        {
            section["Moon"] = "NOT_FOUND";
        }
        
        // Skybox
        section["Skybox"] = RenderSettings.skybox != null ? RenderSettings.skybox.name : "NULL";
        section["RenderSettings_Sun"] = RenderSettings.sun != null ? RenderSettings.sun.name : "NULL";
        section["AmbientMode"] = RenderSettings.ambientMode.ToString();
        section["AmbientIntensity"] = RenderSettings.ambientIntensity;
        
        // DayNightCycle
        var cycle = Object.FindFirstObjectByType<ProjectName.Systems.DayNightCycle>();
        section["DayNightCycle"] = cycle != null ? "FOUND" : "MISSING";
        
        report["LightingFogSystem"] = section;
        Debug.Log($"[Lighting] Sun={sunObj != null}, Intensity={sunObj?.GetComponent<Light>()?.intensity}, Moon={moonObj != null}");
    }

    static void LogHUDSystem()
    {
        var section = new Dictionary<string, object>();
        var canvasObj = GameObject.Find("HUD Canvas");
        
        if (canvasObj == null)
        {
            section["HUDCanvas"] = "NOT_FOUND";
            report["HUDSystem"] = section;
            Debug.LogError("[HUD] HUD Canvas NOT FOUND");
            return;
        }
        
        section["HUDCanvas"] = "FOUND";
        var canvas = canvasObj.GetComponent<Canvas>();
        if (canvas != null)
        {
            section["RenderMode"] = canvas.renderMode.ToString();
            section["PixelPerfect"] = canvas.pixelPerfect;
            section["SortingOrder"] = canvas.sortingOrder;
        }
        
        var scaler = canvasObj.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            section["Scaler_Mode"] = scaler.uiScaleMode.ToString();
            section["Scaler_RefResolution"] = scaler.referenceResolution.ToString();
            section["Scaler_MatchMode"] = scaler.screenMatchMode.ToString();
        }
        
        // Check children
        var hearts = canvasObj.transform.Find("Hearts");
        section["Hearts"] = hearts != null ? "FOUND" : "MISSING";
        
        var minimap = canvasObj.transform.Find("Minimap");
        section["Minimap"] = minimap != null ? "FOUND" : "MISSING";
        if (minimap != null)
        {
            var rawImage = minimap.GetComponentInChildren<RawImage>();
            section["Minimap_RawImage"] = rawImage != null ? "FOUND" : "MISSING";
            if (rawImage != null)
            {
                section["Minimap_Texture"] = rawImage.texture != null ? rawImage.texture.name : "NULL";
            }
        }
        
        var buffUI = canvasObj.transform.Find("BuffUI");
        section["BuffUI"] = buffUI != null ? "FOUND" : "MISSING";
        
        report["HUDSystem"] = section;
        Debug.Log($"[HUD] Canvas={canvas?.renderMode}, Hearts={hearts != null}, Minimap={minimap != null}, BuffUI={buffUI != null}");
    }

    static void LogURPSystem()
    {
        var section = new Dictionary<string, object>();
        
        var urp = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        section["URP_Asset"] = urp != null ? urp.name : "NOT_ASSIGNED";
        
        if (urp != null)
        {
            // Use reflection to access renderer data list - avoid ReadOnlySpan issues
            var type = urp.GetType();
            var field = type.GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
            var rendererDataList = new List<ScriptableRendererData>();
            
            if (field != null)
            {
                var value = field.GetValue(urp);
                if (value is System.Collections.IEnumerable enumerable)
                {
                    foreach (var item in enumerable)
                    {
                        if (item is ScriptableRendererData srd)
                            rendererDataList.Add(srd);
                    }
                }
            }
            
            section["RendererData_Count"] = rendererDataList.Count;
            
            if (rendererDataList.Count > 0)
            {
                var rendererData = rendererDataList[0] as UniversalRendererData;
                section["RendererData"] = rendererData != null ? rendererData.name : "NULL";
                
                if (rendererData != null)
                {
                    var features = new List<string>();
                    if (rendererData.rendererFeatures != null)
                    {
                        foreach (var f in rendererData.rendererFeatures)
                        {
                            if (f != null)
                            {
                                features.Add($"{f.GetType().Name}(active={f.isActive})");
                            }
                        }
                    }
                    section["RendererFeatures"] = features;
                    section["RendererFeatures_Count"] = features.Count;
                }
            }
        }
        
        // QualitySettings
        section["Quality_RenderPipeline"] = QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline.name : "NULL";
        
        report["URPSystem"] = section;
        Debug.Log($"[URP] Asset={urp?.name}, Features={section["RendererFeatures_Count"]}");
    }

    [System.Serializable]
    private class SerializableDict
    {
        public List<string> keys = new List<string>();
        public List<string> values = new List<string>();
        
        public SerializableDict(Dictionary<string, object> dict)
        {
            foreach (var kvp in dict)
            {
                keys.Add(kvp.Key);
                values.Add(kvp.Value?.ToString() ?? "null");
            }
        }
    }
}