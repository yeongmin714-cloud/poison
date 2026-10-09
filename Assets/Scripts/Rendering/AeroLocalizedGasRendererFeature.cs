using System.Collections.Generic;
using ProjectName.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace ProjectName.Rendering
{
    /// <summary>
    /// Opt-in project-owned localized gas compositor for an isolated URP renderer. This is a
    /// custom URP fullscreen pass, not Mirza AERO's FullscreenCustomLightingFeature or fog shader.
    /// Install only in a cloned renderer data asset; it never edits AERO's shared material/settings.
    /// </summary>
    public sealed class AeroLocalizedGasRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader _gasShader;
        [SerializeField] private RenderPassEvent _injectionPoint = RenderPassEvent.AfterRenderingTransparents;
        private LocalGasPass _pass;

        public bool IsReady { get { return isActive && _gasShader != null; } }

        public override void Create()
        {
            _pass = new LocalGasPass(_gasShader) { renderPassEvent = _injectionPoint };
            _pass.ConfigureInput(ScriptableRenderPassInput.Depth);
            _pass.requiresIntermediateTexture = true;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null || _gasShader == null || renderingData.cameraData.camera == null)
                return;
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            if (_pass != null) _pass.Dispose();
            _pass = null;
        }

        private sealed class LocalGasPass : ScriptableRenderPass
        {
            private static readonly int CenterId = Shader.PropertyToID("_GasCenter");
            private static readonly int ForwardId = Shader.PropertyToID("_GasForward");
            private static readonly int RadiusId = Shader.PropertyToID("_GasRadius");
            private static readonly int HeightId = Shader.PropertyToID("_GasHeight");
            private static readonly int DensityId = Shader.PropertyToID("_GasDensity");
            private static readonly int ColorId = Shader.PropertyToID("_GasColor");
            private static readonly int AlphaId = Shader.PropertyToID("_GasAlpha");
            private static readonly int PlumeLengthId = Shader.PropertyToID("_GasPlumeLength");
            private static readonly int NoiseScaleId = Shader.PropertyToID("_GasNoiseScale");
            private static readonly int TimeId = Shader.PropertyToID("_GasTime");

            private readonly Shader _shader;
            private readonly Dictionary<int, Material> _cameraMaterials = new Dictionary<int, Material>();

            public LocalGasPass(Shader shader)
            {
                _shader = shader;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_shader == null || !frameData.Contains<UniversalCameraData>() ||
                    !frameData.Contains<UniversalResourceData>())
                    return;

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                Camera camera = cameraData.camera;
                if (!AeroGasSprayBridge.IsolatedRendererEnabled || camera == null || resources.isActiveTargetBackBuffer ||
                    !AeroLocalizedGasVolume.TryGetNearest(camera.transform.position, out AeroLocalizedGasVolume volume))
                    return;

                Material material = GetCameraMaterial(camera.GetEntityId());
                if (material == null) return;
                material.SetVector(CenterId, volume.WorldPosition);
                material.SetVector(ForwardId, volume.WorldDirection);
                material.SetFloat(RadiusId, volume.Radius);
                material.SetFloat(HeightId, volume.Height);
                material.SetFloat(DensityId, volume.Density);
                material.SetColor(ColorId, volume.Color);
                material.SetFloat(AlphaId, volume.Alpha);
                material.SetFloat(PlumeLengthId, volume.PlumeLength);
                material.SetFloat(NoiseScaleId, volume.NoiseScale);
                material.SetFloat(TimeId, Time.time);

                TextureHandle source = resources.activeColorTexture;
                TextureHandle depth = resources.cameraDepthTexture;
                TextureDesc descriptor = renderGraph.GetTextureDesc(source);
                descriptor.name = "Project Localized Gas Composite";
                descriptor.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(descriptor);
                using (var builder = renderGraph.AddRasterRenderPass<GasCompositePassData>(
                    "Project Localized Gas (isolated custom pass)", out GasCompositePassData passData))
                {
                    passData.source = source;
                    passData.material = material;
                    builder.UseTexture(source, AccessFlags.Read);
                    builder.UseTexture(depth, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                    builder.SetRenderFunc(static (GasCompositePassData data, RasterGraphContext context) =>
                    {
                        Blitter.BlitTexture(context.cmd, data.source, Vector2.one, data.material, 0);
                    });
                }
                resources.cameraColor = destination;
            }

            private sealed class GasCompositePassData
            {
                public TextureHandle source;
                public Material material;
            }

            private Material GetCameraMaterial(int cameraId)
            {
                if (_cameraMaterials.TryGetValue(cameraId, out Material material) && material != null)
                    return material;
                material = new Material(_shader) { name = "Project Local Gas - Camera " + cameraId, hideFlags = HideFlags.HideAndDontSave };
                _cameraMaterials[cameraId] = material;
                return material;
            }

            public void Dispose()
            {
                foreach (KeyValuePair<int, Material> entry in _cameraMaterials)
                {
                    if (entry.Value == null) continue;
                    if (Application.isPlaying) Object.Destroy(entry.Value);
                    else Object.DestroyImmediate(entry.Value);
                }
                _cameraMaterials.Clear();
            }
        }
    }
}
