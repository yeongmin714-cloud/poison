#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectName.Rendering.Editor
{
    /// <summary>Creates cloned URP assets for the Test_10-only AERO integration feasibility spike.</summary>
    public static class AeroPhase4BIsolatedSetup
    {
        private const string OutputFolder = "Assets/Settings/AEROPhase4B";
        private const string ShaderPath = "Assets/Scripts/Rendering/AeroLocalizedGas.shader";

        [MenuItem("Tools/AERO/Phase 4B/Create Isolated Test Renderer")]
        public static void CreateIsolatedAssets()
        {
            if (AssetDatabase.IsValidFolder(OutputFolder))
            {
                Debug.LogError("[AERO Phase4B] Output folder already exists. Remove the AEROPhase4B test folder to recreate it.");
                return;
            }
            EnsureFolder("Assets/Settings");
            EnsureFolder(OutputFolder);

            UniversalRenderPipelineAsset sourcePipeline = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            if (sourcePipeline == null)
                sourcePipeline = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (sourcePipeline == null)
            {
                for (int qualityIndex = 0; qualityIndex < QualitySettings.names.Length && sourcePipeline == null; qualityIndex++)
                    sourcePipeline = QualitySettings.GetRenderPipelineAssetAt(qualityIndex) as UniversalRenderPipelineAsset;
            }
            if (sourcePipeline == null || sourcePipeline.rendererDataList.Length == 0 || sourcePipeline.rendererDataList[0] == null)
            {
                Debug.LogError("[AERO Phase4B] Active project pipeline has no cloneable URP renderer.");
                return;
            }

            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
            {
                Debug.LogError("[AERO Phase4B] Project-owned custom localized-gas shader is missing: " + ShaderPath);
                return;
            }

            const string rendererPath = OutputFolder + "/AERO_Test_Renderer.asset";
            const string pipelinePath = OutputFolder + "/AERO_Test_Pipeline.asset";
            if (AssetDatabase.LoadMainAssetAtPath(rendererPath) != null || AssetDatabase.LoadMainAssetAtPath(pipelinePath) != null)
            {
                Debug.LogError("[AERO Phase4B] Output assets already exist. Remove the AEROPhase4B test folder to recreate them.");
                return;
            }

            ScriptableRendererData clonedRenderer = Object.Instantiate(sourcePipeline.rendererDataList[0]);
            clonedRenderer.name = "AERO_Test_Renderer";
            AssetDatabase.CreateAsset(clonedRenderer, rendererPath);

            UniversalRenderPipelineAsset clonedPipeline = Object.Instantiate(sourcePipeline);
            clonedPipeline.name = "AERO_Test_Pipeline";
            AssetDatabase.CreateAsset(clonedPipeline, pipelinePath);

            AeroLocalizedGasRendererFeature feature = ScriptableObject.CreateInstance<AeroLocalizedGasRendererFeature>();
            feature.name = "Project_Localized_Gas_Custom_Pass";
            SerializedObject featureData = new SerializedObject(feature);
            featureData.FindProperty("_gasShader").objectReferenceValue = shader;
            featureData.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.AddObjectToAsset(feature, clonedRenderer);
            clonedRenderer.rendererFeatures.Add(feature);
            clonedRenderer.SetDirty();
            EditorUtility.SetDirty(clonedRenderer);

            SerializedObject pipelineData = new SerializedObject(clonedPipeline);
            SerializedProperty rendererList = pipelineData.FindProperty("m_RendererDataList");
            if (rendererList == null || rendererList.arraySize == 0)
            {
                Debug.LogError("[AERO Phase4B] URP renderer list was not serialized as expected; isolated pipeline not configured.");
                AssetDatabase.DeleteAsset(pipelinePath);
                AssetDatabase.DeleteAsset(rendererPath);
                return;
            }
            rendererList.GetArrayElementAtIndex(0).objectReferenceValue = clonedRenderer;
            pipelineData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(clonedPipeline);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // The session refuses to assign global quality/Graphics pipeline settings. Create
            // the isolated assets only; activation must be an explicit, external test action.
            Debug.Log("[AERO Phase4B] Created isolated assets with the project-owned custom localized-gas pass (not Mirza AERO's fog shader/material). No global pipeline setting changed. To render this feature, explicitly activate AERO_Test_Pipeline only during a controlled Test_10 session.\n" + pipelinePath + "\n" + rendererPath);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folder = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
#endif
