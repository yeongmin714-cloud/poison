#nullable disable
using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.UI.Themes
{
    /// <summary>
    /// Medieval-themed UI resource loader.
    /// Loads PNG textures from Resources/UI/ path and caches them for memory efficiency.
    /// Provides convenience methods for panel, button, and background textures.
    /// </summary>
    public static class MedievalUIResources
    {
        private static readonly Dictionary<string, Texture2D> _textureCache = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();

        // ================================================================
        // Public API
        // ================================================================

        /// <summary>
        /// Loads a Texture2D from Resources/UI/{name}.png on first call, then caches it.
        /// Returns null and logs a warning if the texture does not exist.
        /// </summary>
        public static Texture2D GetTexture(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            string key = name.ToLowerInvariant();
            if (_textureCache.TryGetValue(key, out Texture2D cached))
                return cached;

            Texture2D tex = Resources.Load<Texture2D>("UI/" + name);
            if (tex == null)
            {
                Debug.LogWarning($"[MedievalUIResources] Texture not found at Resources/UI/{name}.png");
                return null;
            }

            _textureCache[key] = tex;
            return tex;
        }

        /// <summary>
        /// Returns a panel texture based on type string.
        /// Valid panel types: ornate, dark, gold, wide. Falls back to "ornate" on unknown type.
        /// </summary>
        public static Texture2D GetPanelTexture(string panelType)
        {
            if (string.IsNullOrEmpty(panelType))
                return null;

            string lower = panelType.ToLowerInvariant();
            switch (lower)
            {
                case "ornate":
                case "panel_ornate":
                    return GetTexture("panel_ornate");

                case "dark":
                case "panel_dark":
                    return GetTexture("panel_dark");

                case "gold":
                case "panel_gold":
                    return GetTexture("panel_gold");

                case "wide":
                case "panel_wide":
                    return GetTexture("panel_wide");

                default:
                    // Fallback to \"ornate\" instead of warning (default behavior)
                    return GetTexture("panel_ornate");
            }
        }

        /// <summary>
        /// Returns a button sprite for the given state (normal, hover, pressed, disabled).
        /// Converts the loaded Texture2D to a Sprite with center pivot on first access.
        /// </summary>
        public static Sprite GetButtonSprite(string state)
        {
            if (string.IsNullOrEmpty(state))
                return null;

            // Determine which texture to load based on state
            string texName;
            string lower = state.ToLowerInvariant();
            switch (lower)
            {
                case "normal":
                    texName = "btn_normal";
                    break;
                case "hover":
                    texName = "btn_hover";
                    break;
                case "pressed":
                    texName = "btn_pressed";
                    break;
                case "disabled":
                    texName = "btn_disabled";
                    break;
                default:
                    Debug.LogWarning($"[MedievalUIResources] Unknown button state '{state}'. " +
                                     "Valid: normal, hover, pressed, disabled. Falling back to 'normal'.");
                    texName = "btn_normal";
                    break;
            }

            // Check sprite cache
            string spriteKey = "sprite_" + texName;
            if (_spriteCache.TryGetValue(spriteKey, out Sprite cachedSprite))
                return cachedSprite;

            // Load texture and convert to sprite
            Texture2D tex = GetTexture(texName);
            if (tex == null)
                return null;

            var sprite = Sprite.Create(tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            sprite.name = texName;
            _spriteCache[spriteKey] = sprite;
            return sprite;
        }

        /// <summary>
        /// Returns a background texture for the given type (paper, wood).
        /// Falls back to "paper" on unknown type.
        /// </summary>
        public static Texture2D GetBackgroundTexture(string bgType)
        {
            if (string.IsNullOrEmpty(bgType))
                return null;

            string lower = bgType.ToLowerInvariant();
            switch (lower)
            {
                case "paper":
                case "bg_paper":
                    return GetTexture("bg_paper");

                case "wood":
                case "bg_wood":
                    return GetTexture("bg_wood");

                default:
                    // Fallback to "paper" instead of warning (default behavior)
                    return GetTexture("bg_paper");
            }
        }

        /// <summary>
        /// Clears the cached textures and sprites.
        /// Call during scene unload if needed to free memory.
        /// NOTE: Textures loaded via Resources.Load are managed by Unity's Resources system
        /// and should not be destroyed manually. Only dynamically created Sprites are cleaned up.
        /// </summary>
        public static void ClearCache()
        {
            // Destroy only sprites we created (textures from Resources.Load are Unity-managed)
            foreach (var kvp in _spriteCache)
            {
                if (kvp.Value != null)
                    Object.DestroyImmediate(kvp.Value);
            }
            _spriteCache.Clear();
            _textureCache.Clear();
        }
    }
}
