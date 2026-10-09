using ProjectName.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectName.Rendering
{
    /// <summary>
    /// Opt-in runtime scope for Test_10/renderer-spike scenes. It never changes GraphicsSettings
    /// or QualitySettings: the isolated pipeline must already be active through external setup.
    /// This prevents a scene component from switching the renderer for the whole game.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AeroIsolatedRendererSession : MonoBehaviour
    {
        [SerializeField] private UniversalRenderPipelineAsset _isolatedPipeline;

        public UniversalRenderPipelineAsset IsolatedPipeline
        {
            get { return _isolatedPipeline; }
            set { _isolatedPipeline = value; }
        }

        private void OnEnable()
        {
            if (_isolatedPipeline == null)
            {
                AeroGasSprayBridge.SetRendererSessionEnabled(false);
                Debug.LogError("[AERO Phase4B] Assign an isolated cloned URP asset before enabling the session.", this);
                return;
            }

            UniversalRenderPipelineAsset activePipeline = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            if (activePipeline == null)
                activePipeline = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;

            if (activePipeline != _isolatedPipeline || SharesRendererDataWithOtherPipeline(_isolatedPipeline) ||
                !HasReadyLocalizedGasFeature(_isolatedPipeline))
            {
                AeroGasSprayBridge.SetRendererSessionEnabled(false);
                Debug.LogError("[AERO Phase4B] The project-owned localized-gas feature is not ready. The configured active URP pipeline must be this isolated clone, with renderer data not shared with another pipeline; this component will not change global pipeline settings. Keeping legacy gas visuals.", this);
                return;
            }

            // The session owns its subscriber as well as the cloned pipeline, keeping ordinary
            // gameplay scenes free of any AERO components or renderer changes.
            if (GetComponent<AeroGasSprayRuntimeHost>() == null)
                gameObject.AddComponent<AeroGasSprayRuntimeHost>();

            AeroGasSprayBridge.SetRendererSessionEnabled(true);
        }

        private static bool SharesRendererDataWithOtherPipeline(UniversalRenderPipelineAsset pipeline)
        {
            if (pipeline == null || pipeline.rendererDataList == null) return true;
            UniversalRenderPipelineAsset defaultPipeline = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (defaultPipeline != null && defaultPipeline != pipeline && SharesRendererData(pipeline, defaultPipeline))
                return true;
            int qualityCount = QualitySettings.names.Length;
            for (int qualityIndex = 0; qualityIndex < qualityCount; qualityIndex++)
            {
                UniversalRenderPipelineAsset qualityPipeline = QualitySettings.GetRenderPipelineAssetAt(qualityIndex) as UniversalRenderPipelineAsset;
                if (qualityPipeline != null && qualityPipeline != pipeline && SharesRendererData(pipeline, qualityPipeline))
                    return true;
            }
            return false;
        }

        private static bool SharesRendererData(UniversalRenderPipelineAsset first, UniversalRenderPipelineAsset second)
        {
            if (first == null || second == null || first.rendererDataList == null || second.rendererDataList == null)
                return false;
            for (int firstIndex = 0; firstIndex < first.rendererDataList.Length; firstIndex++)
            {
                ScriptableRendererData rendererData = first.rendererDataList[firstIndex];
                if (rendererData == null) continue;
                for (int secondIndex = 0; secondIndex < second.rendererDataList.Length; secondIndex++)
                    if (rendererData == second.rendererDataList[secondIndex]) return true;
            }
            return false;
        }

        private static bool HasReadyLocalizedGasFeature(UniversalRenderPipelineAsset pipeline)
        {
            if (pipeline == null || pipeline.rendererDataList == null) return false;
            for (int rendererIndex = 0; rendererIndex < pipeline.rendererDataList.Length; rendererIndex++)
            {
                ScriptableRendererData rendererData = pipeline.rendererDataList[rendererIndex];
                if (rendererData == null || rendererData.rendererFeatures == null) continue;
                for (int featureIndex = 0; featureIndex < rendererData.rendererFeatures.Count; featureIndex++)
                {
                    AeroLocalizedGasRendererFeature feature = rendererData.rendererFeatures[featureIndex] as AeroLocalizedGasRendererFeature;
                    if (feature != null && feature.IsReady) return true;
                }
            }
            return false;
        }

        private void OnDisable()
        {
            RestorePipeline();
        }

        private void OnDestroy()
        {
            RestorePipeline();
        }

        private void RestorePipeline()
        {
            AeroGasSprayBridge.SetRendererSessionEnabled(false);
        }
    }
}
