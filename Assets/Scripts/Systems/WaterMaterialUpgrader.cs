using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase G1-09: Static utility for upgrading WaterBody and LakeGenerator materials
    /// to high-quality URP Lit water with reflection probe support, SSR compatibility,
    /// 2-axis wave animation, and depth-based color blending.
    /// </summary>
    public static class WaterMaterialUpgrader
    {
        /// <summary>Shallow water base color.</summary>
        public static readonly Color ShallowColor = new Color(0.1f, 0.4f, 0.7f, 0.6f);

        /// <summary>Deep water base color.</summary>
        public static readonly Color DeepColor = new Color(0.0f, 0.1f, 0.3f, 0.8f);

        /// <summary>Target smoothness for reflection clarity.</summary>
        private const float TargetSmoothness = 0.8f;

        /// <summary>Target metallic (0 = non-metallic for proper reflections).</summary>
        private const float TargetMetallic = 0.0f;

        /// <summary>Render queue for transparent materials.</summary>
        private const int TransparentQueue = 3000;

        /// <summary>
        /// Creates a URP Lit water material configured for reflection probes,
        /// SSR support, depth-based coloring, and transparent rendering.
        /// </summary>
        /// <param name="materialName">Name for the new material asset.</param>
        /// <param name="shallowWeight">Blend weight toward shallow color (0 = deep, 1 = shallow).</param>
        /// <returns>A fully configured URP Lit water material, or a fallback material if URP Lit shader is unavailable.</returns>
        public static Material CreateUpgradedWaterMaterial(string materialName, float shallowWeight = 0.5f)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Material mat;

            if (shader != null)
            {
                mat = new Material(shader);
            }
            else
            {
                // Fallback: try URP pipeline default material
                var pipeline = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
                if (pipeline != null && pipeline.defaultMaterial != null)
                {
                    mat = new Material(pipeline.defaultMaterial);
                }
                else
                {
                    shader = Shader.Find("Standard");
                    mat = shader != null ? new Material(shader) : new Material(Shader.Find("Diffuse"));
                }
            }

            mat.name = string.IsNullOrEmpty(materialName) ? "Upgraded_Water_Mat" : materialName;

            // Depth-based color: blend between shallow and deep
            Color waterColor = Color.Lerp(DeepColor, ShallowColor, Mathf.Clamp01(shallowWeight));
            mat.color = waterColor;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", waterColor);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", waterColor);

            // Smoothness and Metallic for reflection fidelity
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", TargetSmoothness);
            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", TargetMetallic);

            // Reflection probe keywords
            mat.EnableKeyword("_REFLECTION_PROBE_BLENDING");
            mat.EnableKeyword("_REFLECTION_PROBE_BOX_PROJECTION");

            // Transparent surface type (URP Lit manages blend state internally via _BlendMode)
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_BlendMode", 0f);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_AlphaClip", 0f);
            mat.renderQueue = TransparentQueue;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_BLENDMODE_ALPHA");

            // Set alpha in color to ensure transparency
            Color finalColor = mat.color;
            finalColor.a = waterColor.a;
            mat.color = finalColor;

            return mat;
        }

        /// <summary>
        /// Creates a simple transparent material (original fallback style).
        /// Used by the Reset operation to restore the pre-upgrade appearance.
        /// </summary>
        /// <param name="materialName">Name for the material.</param>
        /// <param name="color">Base color (with alpha for transparency).</param>
        /// <returns>A simple transparent material.</returns>
        public static Material CreateSimpleWaterMaterial(string materialName, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Material mat;

            if (shader != null)
            {
                mat = new Material(shader);
            }
            else
            {
                var pipeline = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
                if (pipeline != null && pipeline.defaultMaterial != null)
                {
                    mat = new Material(pipeline.defaultMaterial);
                }
                else
                {
                    shader = Shader.Find("Standard");
                    mat = shader != null ? new Material(shader) : new Material(Shader.Find("Diffuse"));
                }
            }

            mat.name = string.IsNullOrEmpty(materialName) ? "Simple_Water_Mat" : materialName;
            mat.color = color;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);

            // Simple transparent setup without reflection keywords (URP Lit manages blend state via _BlendMode)
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_BlendMode", 0f);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_AlphaClip", 0f);
            mat.renderQueue = TransparentQueue;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_BLENDMODE_ALPHA");

            // Disable reflection probe keywords
            mat.DisableKeyword("_REFLECTION_PROBE_BLENDING");
            mat.DisableKeyword("_REFLECTION_PROBE_BOX_PROJECTION");

            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", 0.5f);
            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", 0.0f);

            return mat;
        }

        /// <summary>
        /// Computes a 2-axis sine wave offset using both X and Z spatial dimensions.
        /// Produces a more natural water surface animation than single-axis waves.
        /// </summary>
        /// <param name="time">Current time value (typically Time.time).</param>
        /// <param name="speed">Wave animation speed.</param>
        /// <param name="amplitude">Wave height amplitude.</param>
        /// <param name="xPos">X position of the surface point.</param>
        /// <param name="zPos">Z position of the surface point.</param>
        /// <returns>Y-offset value combining X-axis and Z-axis wave contributions.</returns>
        public static float Compute2AxisWaveOffset(float time, float speed, float amplitude, float xPos, float zPos)
        {
            // X-axis wave: sine based on time and X position
            float waveX = Mathf.Sin(time * speed + xPos * 0.5f) * amplitude;

            // Z-axis wave: cosine-based with slightly different speed and Z position
            float waveZ = Mathf.Cos(time * speed * 0.8f + zPos * 0.3f) * amplitude;

            // Blend both axes for a combined wave effect
            return (waveX + waveZ) * 0.5f;
        }

        /// <summary>
        /// Applies a subtle vertex color normal-map effect to a mesh by varying
        /// vertex colors with a slight blue-green tint offset. This creates a
        /// perceived normal variation on URP Lit surfaces that use vertex colors.
        /// </summary>
        /// <param name="mesh">The mesh to modify.</param>
        /// <param name="offsetMagnitude">Magnitude of the color offset (default 0.05).</param>
        public static void ApplyVertexColorNormalEffect(Mesh mesh, float offsetMagnitude = 0.05f)
        {
            if (mesh == null) return;

            Vector3[] vertices = mesh.vertices;
            Color[] colors = new Color[vertices.Length];

            for (int i = 0; i < vertices.Length; i++)
            {
                // Slight color variation based on vertex position to simulate normal variation
                float rOffset = Mathf.Sin(vertices[i].x * 2.3f + vertices[i].z * 1.7f) * offsetMagnitude;
                float gOffset = Mathf.Cos(vertices[i].z * 2.1f + vertices[i].x * 1.3f) * offsetMagnitude;
                float bOffset = Mathf.Sin((vertices[i].x + vertices[i].z) * 1.9f) * offsetMagnitude;

                colors[i] = new Color(
                    0.5f + rOffset,
                    0.5f + gOffset,
                    0.5f + bOffset,
                    1.0f
                );
            }

            mesh.colors = colors;
        }

        /// <summary>
        /// Returns true if the given material has all the upgraded water material properties.
        /// </summary>
        public static bool IsUpgradedWaterMaterial(Material mat)
        {
            if (mat == null) return false;

            bool hasReflectionBlending = mat.IsKeywordEnabled("_REFLECTION_PROBE_BLENDING");
            bool isTransparent = mat.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT");
            bool hasCorrectQueue = mat.renderQueue == TransparentQueue;

            float smoothness = mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness") : 0f;
            float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;

            bool hasSmoothness = Mathf.Abs(smoothness - TargetSmoothness) < 0.01f;
            bool hasMetallic = Mathf.Abs(metallic - TargetMetallic) < 0.01f;

            return hasReflectionBlending && isTransparent && hasCorrectQueue && hasSmoothness && hasMetallic;
        }
    }
}