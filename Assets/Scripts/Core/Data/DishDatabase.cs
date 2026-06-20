using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ProjectName.Core
{
    /// <summary>
    /// Loads dish data from GAME_DATA.md at runtime.
    /// Provides lookup by dish name or id.
    /// </summary>
    public static class DishDatabase
    {
        private static bool _initialized;
        private static readonly List<DishInfo> _all = new List<DishInfo>();
        private static readonly Dictionary<string, DishInfo> _byName = new Dictionary<string, DishInfo>();
        private static readonly Dictionary<string, DishInfo> _byId = new Dictionary<string, DishInfo>();

        private static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            var txt = Resources.Load<TextAsset>("GAME_DATA");
            if (txt == null)
            {
                Debug.LogError("[DishDatabase] Failed to load GAME_DATA.md from Resources.");
                return;
            }

            var content = txt.text;
            // Find cooking section
            const string startMarker = "## 🍲 4. 요리 (Cooking) — 38종 레시피";
            int startIdx = content.IndexOf(startMarker);
            if (startIdx < 0)
            {
                Debug.LogError("[DishDatabase] Could not find cooking section.");
                return;
            }

            int pos = startIdx + startMarker.Length;
            string[] lines = content.Substring(pos).Split('\n');

            var tableRowRegex = new Regex(@"^\s*\|\s*.+\s*\|\s*$");
            var separatorRegex = new Regex(@"^\s*\|\s*:-|:---\s*");

            bool inTable = false;
            bool headerSkipped = false;
            int idx = 1;

            foreach (string lineRaw in lines)
            {
                string line = lineRaw.Trim();

                // Stop at next major section
                if (line.StartsWith("## "))
                {
                    break;
                }

                // Detect table start: line with header containing "주재료"
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
                    headerSkipped = true;
                    continue;
                }

                // Parse table row
                if (tableRowRegex.IsMatch(line))
                {
                    if (!headerSkipped)
                    {
                        // Could be header line again
                        if (line.Contains("주재료") && line.Contains("요리 명칭"))
                            continue;
                    }

                    string[] parts = line.Split(new[] { '|' }, System.StringSplitOptions.RemoveEmptyEntries);
                    // Expected: # | 주재료 | 조합 재료 | 요리 명칭 | 주요 효과
                    if (parts.Length >= 5)
                    {
                        string index = parts[0].Trim(); // not used
                        string meat = parts[1].Trim();
                        string herb = parts[2].Trim();
                        string dishName = parts[3].Trim();
                        string effect = parts[4].Trim();

                        if (meat.Equals("주재료") || herb.Equals("조합 재료") || dishName.Equals("요리 명칭") || effect.Equals("주요 효과"))
                            continue;

                        var dish = new DishInfo
                        {
                            Id = $"D{idx:D2}",
                            DisplayName = dishName,
                            Description = $"{meat} + {herb}",
                            Effect = effect,
                            Icon = null // could be loaded from Resources/Dishes/{dishName} if available
                        };
                        _all.Add(dish);
                        _byName[dishName] = dish;
                        _byId[dish.Id] = dish;
                        idx++;
                    }
                }
            }

            Debug.Log($"[DishDatabase] Loaded {_all.Count} dishes from GAME_DATA.md.");
        }

        public static IReadOnlyList<DishInfo> All => _all;
        public static DishInfo GetDishInfoByName(string name)
        {
            if (!_initialized) Initialize();
            _byName.TryGetValue(name, out var info);
            return info;
        }
        public static DishInfo GetDishInfoById(string id)
        {
            if (!_initialized) Initialize();
            _byId.TryGetValue(id, out var info);
            return info;
        }

        /// <summary>
        /// Convenience: returns ItemData for a dish (ready to add to inventory).
        /// </summary>
        public static PlayerInventory.ItemData GetItemData(string dishName)
        {
            var info = GetDishInfoByName(dishName);
            return info?.ToItemData();
        }
    }
}