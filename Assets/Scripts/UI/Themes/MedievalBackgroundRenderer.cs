#nullable disable
using UnityEngine;

namespace ProjectName.UI.Themes
{
    /// <summary>
    /// Static renderer for medieval-themed panel backgrounds using loaded PNG textures
    /// from Resources/UI/. Replaces procedural Perlin-noise backgrounds with pre-made
    /// medieval fantasy artwork.
    /// </summary>
    public static class MedievalBackgroundRenderer
    {
        // ================================================================
        // Public API
        // ================================================================

        /// <summary>
        /// Draws a medieval panel background stretched to fill the given rect.
        /// Optionally draws decorative corner decorations on top.
        /// </summary>
        /// <param name="rect">Screen-space rectangle to fill</param>
        /// <param name="panelType">
        /// Panel texture key (ornate, dark, gold, wide — or direct filename like panel_ornate).
        /// </param>
        /// <param name="borderSize">
        /// If > 0, decorative corner decals are drawn at the 4 corners using deco_corner_*.png textures.
        /// The value controls the corner decoration size in pixels (clamped to 25% of the smaller rect dimension).
        /// </param>
        public static void DrawBackground(Rect rect, string panelType, float borderSize = 0f)
        {
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            Texture2D panelTex = MedievalUIResources.GetPanelTexture(panelType);
            if (panelTex == null)
            {
                Debug.LogWarning($"[MedievalBackgroundRenderer] No panel texture for '{panelType}'. Skipping draw.");
                return;
            }

            // Draw the panel texture stretched to fill
            GUI.DrawTexture(rect, panelTex, ScaleMode.StretchToFill, alphaBlend: true);

            // Draw decorative corner decorations on top if requested
            if (borderSize > 0f)
            {
                DrawCornerDecorations(rect, borderSize);
            }
        }

        /// <summary>
        /// Draws a decorative border around the given rect using loaded border textures.
        /// Uses border_top, border_bottom, border_left, border_right textures stretched
        /// along each edge.
        /// </summary>
        /// <param name="rect">Screen-space rectangle to draw borders around</param>
        /// <param name="borderStyle">
        /// Style identifier (reserved for future use; currently all styles use the same
        /// base border textures with the given color tint).
        /// </param>
        /// <param name="color">Color tint applied to border textures</param>
        /// <param name="thickness">Border thickness in pixels</param>
        public static void DrawBorder(Rect rect, string borderStyle, Color color, float thickness)
        {
            if (rect.width <= 0f || rect.height <= 0f || thickness <= 0f)
                return;

            // Load border textures (cached via MedievalUIResources)
            Texture2D borderTop = MedievalUIResources.GetTexture("border_top");
            Texture2D borderBottom = MedievalUIResources.GetTexture("border_bottom");
            Texture2D borderLeft = MedievalUIResources.GetTexture("border_left");
            Texture2D borderRight = MedievalUIResources.GetTexture("border_right");

            // Apply color tint
            GUI.color = color;

            // Top edge
            if (borderTop != null)
                GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness),
                    borderTop, ScaleMode.StretchToFill, alphaBlend: true);

            // Bottom edge
            if (borderBottom != null)
                GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness),
                    borderBottom, ScaleMode.StretchToFill, alphaBlend: true);

            // Left edge
            if (borderLeft != null)
                GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height),
                    borderLeft, ScaleMode.StretchToFill, alphaBlend: true);

            // Right edge
            if (borderRight != null)
                GUI.DrawTexture(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height),
                    borderRight, ScaleMode.StretchToFill, alphaBlend: true);

            // Restore color
            GUI.color = Color.white;

            // (borderStyle parameter is reserved for future styled border variants)
        }

        // ================================================================
        // Internal Helpers
        // ================================================================

        /// <summary>
        /// Draws decorative corner decorations at the 4 corners of the given rect.
        /// Uses deco_corner_tl, deco_corner_tr, deco_corner_bl, deco_corner_br textures.
        /// </summary>
        private static void DrawCornerDecorations(Rect rect, float size)
        {
            // Clamp corner size to reasonable limits
            float cornerSize = Mathf.Min(size, Mathf.Min(rect.width, rect.height) * 0.25f);
            if (cornerSize <= 0f)
                return;

            // Load corner textures (cached via MedievalUIResources)
            Texture2D cornerTL = MedievalUIResources.GetTexture("deco_corner_tl");
            Texture2D cornerTR = MedievalUIResources.GetTexture("deco_corner_tr");
            Texture2D cornerBL = MedievalUIResources.GetTexture("deco_corner_bl");
            Texture2D cornerBR = MedievalUIResources.GetTexture("deco_corner_br");

            // Top-left corner
            if (cornerTL != null)
                GUI.DrawTexture(new Rect(rect.x, rect.y, cornerSize, cornerSize),
                    cornerTL, ScaleMode.StretchToFill, alphaBlend: true);

            // Top-right corner
            if (cornerTR != null)
                GUI.DrawTexture(new Rect(rect.x + rect.width - cornerSize, rect.y, cornerSize, cornerSize),
                    cornerTR, ScaleMode.StretchToFill, alphaBlend: true);

            // Bottom-left corner
            if (cornerBL != null)
                GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - cornerSize, cornerSize, cornerSize),
                    cornerBL, ScaleMode.StretchToFill, alphaBlend: true);

            // Bottom-right corner
            if (cornerBR != null)
                GUI.DrawTexture(new Rect(rect.x + rect.width - cornerSize, rect.y + rect.height - cornerSize, cornerSize, cornerSize),
                    cornerBR, ScaleMode.StretchToFill, alphaBlend: true);
        }
    }
}