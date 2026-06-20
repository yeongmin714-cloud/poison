using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ProjectName.Core
{
    /// <summary>
    /// Represents the result of cooking a meat with a herb.
    /// </summary>
    public struct CookingResult
    {
        public string dishId;       // placeholder for future item lookup
        public string dishName;     // from 요리 명칭 column
        public string description;  // e.g., "토끼 고기 + 회복꽃"
        public string effect;       // from 주요 효과 column
    }

    /// <summary>
    /// Loads cooking recipes from GAME_DATA.md at runtime.
    /// Provides lookup by meat display name + herb display name.
    /// </summary>
    public static class CookingDatabase
    {
        private static Dictionary<string, CookingResult> _recipes = new Dictionary<string, CookingResult>();
        private static bool _initialized = false;

        private static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            // Load the markdown file from Resources
            TextAsset txt = Resources.Load<TextAsset>("GAME_DATA");
            if (txt == null)
            {
                Debug.LogError("[CookingDatabase] Failed to load GAME_DATA.md from Resources.");
                return;
            }

            string content = txt.text;

            // Find the cooking section start
            const string startMarker = "## 🍲 4. 요리 (Cooking) — 38종 레시피";
            int startIdx = content.IndexOf(startMarker);
            if (startIdx < 0)
            {
                Debug.LogError("[CookingDatabase] Could not find cooking section in GAME_DATA.md.");
                return;
            }

            int pos = startIdx + startMarker.Length;
            string[] lines = content.Substring(pos).Split('\n');

            // We'll parse until the next major section (starts with "## ")
            var tableRowRegex = new Regex(@"^\s*\|\s*.+\s*\|\s*$");
            var separatorRegex = new Regex(@"^\s*\|\s*:-|:---\s*");

            bool inTable = false;
            bool headerSkipped = false;

            foreach (string lineRaw in lines)
            {
                string line = lineRaw.Trim();

                // Stop at next major section
                if (line.StartsWith("## "))
                {
                    break;
                }

                // Detect table start: look for line with header containing "주재료"
                if (!inTable && line.Contains("| 주재료 |") && line.Contains("| 조합 재료 |"))
                {
                    inTable = true;
                    continue; // skip header line
                }

                if (!inTable)
                    continue;

                // Skip separator line
                if (separatorRegex.IsMatch(line))
                {
                    headerSkipped = true; // after separator we start data rows
                    continue;
                }

                // Parse table row
                if (tableRowRegex.IsMatch(line))
                {
                    // Skip header row if we haven't passed separator yet
                    if (!headerSkipped)
                    {
                        // This could be the header line again (some markdowns have both)
                        if (line.Contains("주재료") && line.Contains("요리 명칭"))
                            continue;
                    }

                    // Split by '|', remove empty first/last
                    string[] parts = line.Split(new char[] { '|' }, System.StringSplitOptions.RemoveEmptyEntries);
                    // Expected format: # | 주재료 | 조합 재료 | 요리 명칭 | 주요 효과
                    if (parts.Length >= 5)
                    {
                        string index = parts[0].Trim(); // not used
                        string meat = parts[1].Trim();   // e.g., "토끼 고기"
                        string herb = parts[2].Trim();   // e.g., "회복꽃"
                        string dishName = parts[3].Trim(); // e.g., "토끼 허브 구이"
                        string effect = parts[4].Trim();   // e.g., "체력 회복"

                        // Skip header-like rows where meat is "주재료"
                        if (meat.Equals("주재료") || herb.Equals("조합 재료") || dishName.Equals("요리 명칭") || effect.Equals("주요 효과"))
                            continue;

                        // Validate herb exists via HerbDatabase
                        var herbInfo = HerbDatabase.GetHerbInfoByDisplayName(herb);
                        if (string.IsNullOrEmpty(herbInfo.id))
                        {
                            Debug.Log($"[CookingDatabase] Unknown herb '{herb}' in recipe '{dishName}' (non-standard ingredient).");
                            // Still add? We'll skip for safety.
                            continue;
                        }

                        string key = MakeKey(meat, herb);
                        if (!_recipes.ContainsKey(key))
                        {
                            _recipes[key] = new CookingResult
                            {
                                dishId = $"cook_{meat.Replace(' ', '_')}_{herb.Replace(' ', '_')}", // simple placeholder
                                dishName = dishName,
                                description = $"{meat} + {herb}",
                                effect = effect
                            };
                        }
                    }
                }
            }

            Debug.Log($"[CookingDatabase] Loaded {_recipes.Count} cooking recipes from GAME_DATA.md.");
        }

        private static string MakeKey(string meat, string herb)
        {
            // Order-independent? Cooking is ordered (meat + herb). We'll keep order as given.
            // But to allow lookup regardless of order we could sort, but cooking is not commutative.
            // We'll keep exact order as meat|herb.
            return $"{meat}|{herb}";
        }

        /// <summary>
        /// Returns the cooking result for the given meat and herb, or null if not found.
        /// </summary>
        public static CookingResult? GetCooking(string meatDisplayName, string herbDisplayName)
        {
            Initialize();
            string key = MakeKey(meatDisplayName, herbDisplayName);
            if (_recipes.TryGetValue(key, out var result))
                return result;
            return null;
        }

        /// <summary>
        /// Returns all loaded recipes (read-only).
        /// </summary>
        public static IReadOnlyDictionary<string, CookingResult> AllRecipes => _recipes;
    }
}
