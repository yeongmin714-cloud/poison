using System.Collections.Generic;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Core
{
    /// <summary>
    /// Tracks which recipes the player has discovered.
    /// Recipes are identified by their display name.
    /// Data is persisted via PlayerPrefs using pipe-delimited encoding.
    /// </summary>
    public static class RecipeDiscoverySystem
    {
        private static HashSet<string> _discovered = new HashSet<string>();
        private static bool _initialized = false;

        /// <summary>
        /// Separator used for serializing recipe names.
        /// Pipe character chosen over comma to allow commas in display names.
        /// </summary>
        private const char SEPARATOR = '|';
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
                string[] names = saved.Split(SEPARATOR);
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
        /// Null or empty names are silently ignored.
        /// </summary>
        public static void MarkDiscovered(string recipeName)
        {
            if (string.IsNullOrEmpty(recipeName))
            {
                Debug.LogWarning("[RecipeDiscoverySystem] MarkDiscovered called with null/empty name — ignored.");
                return;
            }

            if (!_initialized) Initialize();

            if (_discovered.Add(recipeName))
            {
                Save();
                Debug.Log($"[RecipeDiscoverySystem] 📖 New recipe discovered: {recipeName}");
            }
        }

        /// <summary>
        /// Check if a recipe has been discovered.
        /// Returns false for null or empty names.
        /// </summary>
        public static bool IsDiscovered(string recipeName)
        {
            if (string.IsNullOrEmpty(recipeName)) return false;
            if (!_initialized) Initialize();
            return _discovered.Contains(recipeName);
        }

        /// <summary>
        /// Get all discovered recipe names (defensive copy).
        /// </summary>
        public static IReadOnlyCollection<string> GetAllDiscovered()
        {
            if (!_initialized) Initialize();
            return new HashSet<string>(_discovered);
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
            string joined = string.Join(SEPARATOR.ToString(), _discovered);
            PlayerPrefs.SetString(PREFS_KEY, joined);
            PlayerPrefs.Save();
        }
    }
}