#nullable disable
using ProjectName.UI.Themes;
using UnityEngine;

namespace ProjectName.UI
{
    /// <summary>
    /// Extension methods for <see cref="UIWindow"/> to apply medieval-themed backgrounds
    /// using loaded PNG textures from Resources/UI/.
    /// 
    /// Usage:
    ///   myWindow.ApplyMedievalTheme("ornate", "paper");
    /// 
    /// This stores the medieval panel config on the window's assigned UIDesignTheme.
    /// When UIWindow.OnShow() runs, it detects the medieval config and renders the
    /// panel texture via MedievalBackgroundRenderer instead of the procedural texture.
    /// </summary>
    public static class UIImageThemeExtensions
    {
        // ================================================================
        // Extension Methods
        // ================================================================

        /// <summary>
        /// Applies a medieval fantasy theme to the given UIWindow.
        /// Configures the window to use loaded PNG textures instead of procedural Perlin backgrounds.
        /// </summary>
        /// <param name="window">The UIWindow instance to theme</param>
        /// <param name="panelType">
        /// Panel texture type: "ornate", "dark", "gold", or "wide".
        /// This is the background panel drawn in OnShow().
        /// </param>
        /// <param name="bgType">
        /// Background texture type: "paper" or "wood".
        /// Used for additional background layer if needed.
        /// </param>
        public static void ApplyMedievalTheme(this UIWindow window, string panelType, string bgType)
        {
            if (window == null)
            {
                // Debug.LogError("[UIImageThemeExtensions] ApplyMedievalTheme: window is null.");
                return;
            }

            UIDesignTheme theme = window.Theme;

            // If the window has no theme assigned, log a warning and create one programmatically
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<UIDesignTheme>();
                theme.name = "MedievalTheme_Generated";
                window.ApplyTheme(theme);
                // Debug.Log($"[UIImageThemeExtensions] Created temporary UIDesignTheme for {window.name}. " +
                          // "Consider assigning a theme in the Inspector for persistent configuration.");
            }

            // Store medieval config on the theme
            theme.SetMedievalPanelTexture(panelType);
            theme.SetMedievalBackgroundTexture(bgType);

            // Debug.Log($"[UIImageThemeExtensions] Applied medieval theme to '{window.name}': " +
                  // $"panelType={panelType}, bgType={bgType}");
        }
    }
}