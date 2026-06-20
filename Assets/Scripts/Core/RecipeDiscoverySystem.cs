using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Core
{
    /// <summary>
    /// Tracks which recipes the player has discovered.
    /// Recipes are identified by their display name.
    /// Data is persisted via PlayerPrefs.
    /// </summary>
    public static class RecipeDiscoverySystem
    {
        private static HashSet<string> _discovered = new HashSet<string>();
        private static bool _initialized = false;

        private const string PREFS_KEY = "DiscoveredRecipes";

        /// <summary>
        /// Load discovered recipes from PlayerPrefs.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            string saved = PlayerPrefs.GetString(PREFS_KEY, "");
            if (!string.IsNullOrEmpty(saved))
            {
                string[] names = saved.Split(',');
                foreach (string name in names)
                {
                    string trimmed = name.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                        _discovered.Add(trimmed);
                }
            }
            Debug.Log($"[RecipeDiscoverySystem] Loaded {_discovered.Count} discovered recipes.");
        }

        /// <summary>
        /// Mark a recipe as discovered and save.
        /// </summary>
        public static void MarkDiscovered(string recipeName)
        {
            if (!_initialized) Initialize();

            if (_discovered.Add(recipeName))
            {
                Save();
                Debug.Log($"[RecipeDiscoverySystem] 📖 New recipe discovered: {recipeName}");
            }
        }

        /// <summary>
        /// Check if a recipe has been discovered.
        /// </summary>
        public static bool IsDiscovered(string recipeName)
        {
            if (!_initialized) Initialize();
            return _discovered.Contains(recipeName);
        }

        /// <summary>
        /// Get all discovered recipe names.
        /// </summary>
        public static IReadOnlyCollection<string> GetAllDiscovered()
        {
            if (!_initialized) Initialize();
            return _discovered;
        }

        /// <summary>
        /// Get total count of discovered recipes.
        /// </summary>
        public static int DiscoveredCount
        {
            get
            {
                if (!_initialized) Initialize();
                return _discovered.Count;
            }
        }

        /// <summary>
        /// Reset all discovered recipes (for testing).
        /// </summary>
        public static void Reset()
        {
            _discovered.Clear();
            Save();
            Debug.Log("[RecipeDiscoverySystem] All discovered recipes reset.");
        }

        private static void Save()
        {
            string joined = string.Join(",", _discovered);
            PlayerPrefs.SetString(PREFS_KEY, joined);
            PlayerPrefs.Save();
        }
    }
}