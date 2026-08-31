using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

public static class DiagnoseTerrain
{
    [MenuItem("Tools/Debug/Diagnose Terrain Rendering")]
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        Debug.Log("=== DIAGNOSE TERRAIN START ===");

        // 1. RenderSettings fog
        Debug.Log($"[Diag] RenderSettings.fog={RenderSettings.fog} mode={(RenderSettings.fog ? RenderSettings.fogMode.ToString() : "off")} density={RenderSettings.fogDensity} color={RenderSettings.fogColor}");

        // 2. Lights
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            Debug.Log($"[Diag] Light name={l.gameObject.name} type={l.type} enabled={l.enabled} intensity={l.intensity} color={l.color}");
        }

        // 3. Ground_Inner material
        var ground = GameObject.Find("Ground_Inner");
        if (ground == null) { Debug.LogError("[Diag] Ground_Inner NOT FOUND"); return; }
        var mr = ground.GetComponent<MeshRenderer>();
        if (mr == null) { Debug.LogError("[Diag] Ground_Inner has no MeshRenderer"); return; }
        var mat = mr.sharedMaterial;
        Debug.Log("[Diag] Ground_Inner MeshRenderer enabled=" + mr.enabled + " shadows=" + mr.shadowCastingMode);
        Debug.Log("[Diag] Ground_Inner sharedMaterial name=" + (mat?.name ?? "NULL") + " shader=" + (mat?.shader?.name ?? "NULL") + " _BaseColor=" + (mat != null ? mat.GetColor("_BaseColor").ToString() : "NULL"));
        Debug.Log("[Diag] Ground_Inner _BaseMap=" + (mat?.GetTexture("_BaseMap")?.name ?? "NULL") + " _BaseMapNull=" + (mat?.GetTexture("_BaseMap") == null));
                Debug.Log("[Diag] Ground_Inner _MainTex=" + (mat?.GetTexture("_MainTex")?.name ?? "NULL"));

        // 4. Which material file does the scene reference? (asset path)
        string path = AssetDatabase.GetAssetPath(mat);
        Debug.Log($"[Diag] Ground_Inner references material asset at: '{path}'");
        var matRef = AssetDatabase.LoadAssetAtPath<Material>("Assets/URP/Ground_Grass_Mat.mat");
        Debug.Log("[Diag] Assets/URP/Ground_Grass_Mat.mat _BaseMap=" + (matRef != null && matRef.GetTexture("_BaseMap") != null ? matRef.GetTexture("_BaseMap").name : "NULL") + " shader=" + (matRef?.shader?.name ?? "NULL"));

        // 5. URP asset & renderer data
        var urpAsset = GraphicsSettings.renderPipelineAsset;
        Debug.Log("[Diag] GraphicsSettings.renderPipelineAsset=" + (urpAsset != null ? urpAsset.name : "NULL"));
        Debug.Log("[Diag] QualitySettings.renderPipeline=" + (QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline.name : "NULL"));

        // 6. TerrainTextureApplier on ground
        var applier = ground.GetComponent<ProjectName.Systems.TerrainTextureApplier>();
        if (applier != null)
        {
            Debug.Log("[Diag] TerrainTextureApplier found: CurrentNation=" + applier.CurrentNation);
            Debug.Log("[Diag]   nationTextureCount East=" + applier.NationTextureCount(ProjectName.Core.Data.NationType.East) + " West=" + applier.NationTextureCount(ProjectName.Core.Data.NationType.West) + " North=" + applier.NationTextureCount(ProjectName.Core.Data.NationType.North) + " South=" + applier.NationTextureCount(ProjectName.Core.Data.NationType.South) + " Empire=" + applier.NationTextureCount(ProjectName.Core.Data.NationType.Empire));

            // Simulate Play: LoadTextures + CreateMaterials + ApplyMaterialForNation(East)
            // to reproduce exactly what runs in Play Mode.
            Debug.Log("[Diag] --- Simulating Play Mode terrain material application ---");
            var mr2 = ground.GetComponent<MeshRenderer>();
            var applierType = applier.GetType();
            var loadM = applierType.GetMethod("LoadTextures", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            var createM = applierType.GetMethod("CreateMaterials", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            var applyM = applierType.GetMethod("ApplyMaterialForNation", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (loadM != null) { loadM.Invoke(applier, null); Debug.Log("[Diag] LoadTextures() simulated"); }
            if (createM != null) { createM.Invoke(applier, null); Debug.Log("[Diag] CreateMaterials() simulated"); }
            Debug.Log("[Diag]   post-load nationTextureCount East=" + applier.NationTextureCount(ProjectName.Core.Data.NationType.East) + " West=" + applier.NationTextureCount(ProjectName.Core.Data.NationType.West) + " North=" + applier.NationTextureCount(ProjectName.Core.Data.NationType.North) + " South=" + applier.NationTextureCount(ProjectName.Core.Data.NationType.South) + " Empire=" + applier.NationTextureCount(ProjectName.Core.Data.NationType.Empire));
            if (applyM != null) { applyM.Invoke(applier, new object[] { ProjectName.Core.Data.NationType.East }); Debug.Log("[Diag] ApplyMaterialForNation(East) simulated"); }

            // Inspect the created Terrain_East_Mat from _nationMaterials dictionary directly
            var dictField = applierType.GetField("_nationMaterials", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (dictField != null)
            {
                var dict = dictField.GetValue(applier) as System.Collections.Generic.Dictionary<ProjectName.Core.Data.NationType, Material>;
                if (dict != null && dict.ContainsKey(ProjectName.Core.Data.NationType.East))
                {
                    Material eastMat = dict[ProjectName.Core.Data.NationType.East];
                    Debug.Log("[Diag] >>> _nationMaterials[East] name=" + eastMat.name + " shader=" + (eastMat.shader != null ? eastMat.shader.name : "NULL"));
                    Debug.Log("[Diag] >>> East _BaseMap=" + (eastMat.GetTexture("_BaseMap") != null ? eastMat.GetTexture("_BaseMap").name : "NULL") + " null=" + (eastMat.GetTexture("_BaseMap") == null));
                    Debug.Log("[Diag] >>> East _MainTex=" + (eastMat.GetTexture("_MainTex") != null ? eastMat.GetTexture("_MainTex").name : "NULL") + " mainTexture=" + (eastMat.mainTexture != null ? eastMat.mainTexture.name : "NULL"));
                    Debug.Log("[Diag] >>> East _BaseColor=" + eastMat.GetColor("_BaseColor").ToString() + " mainTextureScale=" + eastMat.mainTextureScale.ToString());
                    Debug.Log("[Diag] >>> East _Surface=" + eastMat.GetFloat("_Surface").ToString() + " keywords=" + string.Join(";", eastMat.shaderKeywords) + " hasPropBaseMap=" + eastMat.HasProperty("_BaseMap"));
                }
                else
                {
                    Debug.Log("[Diag] >>> _nationMaterials does NOT contain East (count=" + (dict != null ? dict.Count : -1) + ")");
                }
            }
            else Debug.Log("[Diag] >>> _nationMaterials field NOT found via reflection");

            // Now inspect the ACTUAL material on the renderer (what Play Mode shows)
            Material playMat = mr2.sharedMaterial;
            Debug.Log("[Diag] Ground_Inner material AFTER Play-sim: name=" + (playMat != null ? playMat.name : "NULL") + " shader=" + (playMat != null && playMat.shader != null ? playMat.shader.name : "NULL"));
            Debug.Log("[Diag]   _BaseMap=" + (playMat != null && playMat.GetTexture("_BaseMap") != null ? playMat.GetTexture("_BaseMap").name : "NULL") + " null=" + (playMat != null && playMat.GetTexture("_BaseMap") == null));
            Debug.Log("[Diag]   _MainTex=" + (playMat != null && playMat.GetTexture("_MainTex") != null ? playMat.GetTexture("_MainTex").name : "NULL"));
            Debug.Log("[Diag]   mainTexture=" + (playMat != null && playMat.mainTexture != null ? playMat.mainTexture.name : "NULL"));
            Debug.Log("[Diag]   _BaseColor=" + (playMat != null ? playMat.GetColor("_BaseColor").ToString() : "NULL"));
            Debug.Log("[Diag]   mainTextureScale=" + (playMat != null ? playMat.mainTextureScale.ToString() : "NULL"));
            Debug.Log("[Diag]   _Surface=" + (playMat != null ? playMat.GetFloat("_Surface").ToString() : "NULL") + " renderQueue=" + (playMat != null ? playMat.renderQueue.ToString() : "NULL"));
            Debug.Log("[Diag]   HasProperty _BaseMap=" + (playMat != null && playMat.HasProperty("_BaseMap")) + " _MainTex=" + (playMat != null && playMat.HasProperty("_MainTex")));
            Debug.Log("[Diag]   keywords=" + (playMat != null ? string.Join(";", playMat.shaderKeywords) : "NULL"));
            Debug.Log("[Diag]   enableInstancing=" + (playMat != null ? playMat.enableInstancing.ToString() : "NULL"));
        }
        else Debug.LogError("[Diag] TerrainTextureApplier NOT on Ground_Inner");

        Debug.Log("=== DIAGNOSE TERRAIN END ===");
        EditorSceneManager.SaveScene(scene);
    }
}