using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class AssignURPAsset
{
    [MenuItem("Tools/Assign URP Pipeline Asset")]
    public static void Assign()
    {
        // URP Pipeline Asset 찾기
        string[] guids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset");
        if (guids.Length == 0)
        {
            Debug.LogError("[AssignURP] No URP Pipeline Asset found! Create one via Assets > Create > Rendering > URP Asset (with Universal Renderer)");
            return;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        var pipelineAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
        if (pipelineAsset == null)
        {
            Debug.LogError($"[AssignURP] Failed to load asset at {path}");
            return;
        }

        // QualitySettings에 할당
        int currentLevel = QualitySettings.GetQualityLevel();
        QualitySettings.renderPipeline = pipelineAsset;

        // 모든 퀄리티 레벨에 할당
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = pipelineAsset;
        }
        QualitySettings.SetQualityLevel(currentLevel, false);
        QualitySettings.renderPipeline = pipelineAsset;

        EditorUtility.SetDirty(pipelineAsset);
        AssetDatabase.SaveAssets();

        Debug.Log($"[AssignURP] Pipeline assigned: {path} (Quality level: {QualitySettings.names[currentLevel]})");
    }
}