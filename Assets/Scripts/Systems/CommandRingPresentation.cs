using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectName.Systems
{
    /// <summary>
    /// Single visual owner for move, selected-guard, and explicit-target ground rings.
    /// Geometry/thickness comes from MoveTargetRing; scale, transparency, and pulse are shared.
    /// </summary>
    public static class CommandRingPresentation
    {
        public const string TextureResourcePath = "UI/MoveTargetRing";
        // Archived Figma 156:18/172:2 core-ring footprint is 400×120; preserve the floor-plane oval ratio.
        public const float BaseScale = 1.9f;
        public const float FigmaCoreRingAspect = 400f / 120f;
        public const float PulseAmplitude = 0.06f;
        public const float PulseFrequency = 2.6f;
        // The baked MoveTargetRing texture owns ring thickness; this ratio is documented here
        // as the shared texture-space thickness convention (outer radius 0.9, inner radius 0.68).
        public const float RingThickness = 0.22f;

        public static readonly Color MoveTargetColor = new Color(1f, 0.82f, 0.35f, 1f);
        public static readonly Color SelectedGuardColor = new Color(0.345f, 0.651f, 1f, 1f);
        public static readonly Color ExplicitTargetColor = new Color(0.973f, 0.318f, 0.286f, 1f);

        /// <summary>Create the common transparent Unlit material, differing only by tint.</summary>
        public static Material CreateMaterial(Color tint)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (shader == null) return null;

            var material = new Material(shader) { name = "CommandRing_" + ColorUtility.ToHtmlStringRGB(tint) };
            var texture = Resources.Load<Texture2D>(TextureResourcePath);
            if (texture != null) material.mainTexture = texture;
            material.color = tint;
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = 3000;
            return material;
        }

        /// <summary>Apply the common material to an existing colliderless ring renderer.</summary>
        public static Material Apply(Renderer renderer, Color tint)
        {
            if (renderer == null) return null;
            var material = CreateMaterial(tint);
            if (material != null) renderer.sharedMaterial = material;
            return material;
        }

        /// <summary>Shared slow pulse; caller supplies the untinted visual's initial scale.</summary>
        public static Vector3 GetPulseScale(Vector3 baseScale, float elapsed)
        {
            float pulse = 1f + PulseAmplitude * Mathf.Sin(elapsed * PulseFrequency);
            return baseScale * pulse;
        }
    }
}
