using System.Collections.Generic;
using System.Text.RegularExpressions;
using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Core.Data
{
    /// <summary>
    /// Loads dish data from GAME_DATA.md at runtime.
    /// Provides lookup by dish name or id.
    /// </summary>
    public static class DishDatabase
    {
        private static bool _initialized;
        private static readonly object _lock = new object();
        private static readonly List<DishInfo> _all = new List<DishInfo>();
        private static readonly Dictionary<string, DishInfo> _byName = new Dictionary<string, DishInfo>();
        private static readonly Dictionary<string, DishInfo> _byId = new Dictionary<string, DishInfo>();

        private static void Initialize()
        {
            if (_initialized) return;

            lock (_lock)
            {
                if (_initialized) return; // double-check lock
                _initialized = true;
            }

            var txt = Resources.Load<TextAsset>("GAME_DATA");
            if (txt == null)
            {
                Debug.LogError("[DishDatabase] Failed to load GAME_DATA.md from Resources.");
                return;
            }

            var content = txt.text;
            // Find cooking section
            const string startMarker = "## \U0001f372 4. \uc694\ub9ac (Cooking) \u2014 38\uc885 \ub808\uc2dc\ud53c";
            int startIdx = content.IndexOf(startMarker);
            if (startIdx < 0)
            {
                Debug.LogError("[DishDatabase] Could not find cooking section.");
                return;
            }

            int pos = startIdx + startMarker.Length;
            string[] lines = content.Substring(pos).Split('\n');

            // Matches markdown table separator lines: |:-:|:------|:---------|...
            var separatorRegex = new Regex(@"^\s*\|\s*:-+\s*\|");
            // Matches any markdown table data row: starts and ends with |
            var tableRowRegex = new Regex(@"^\s*\|\s*.+\|\s*$");

            bool inTable = false;
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

                // Skip separator line (|:-:|:------|:---------|...)
                if (separatorRegex.IsMatch(line))
                {
                    continue;
                }

                // Skip any other header-like line within the table
                if (line.Contains("주재료") && line.Contains("요리 명칭"))
                {
                    continue;
                }

                // Parse table row
                if (tableRowRegex.IsMatch(line))
                {
                    string[] parts = line.Split(new[] { '|' }, System.StringSplitOptions.RemoveEmptyEntries);
                    // Expected: # | 주재료 | 조합 재료 | 요리 명칭 | 주요 효과
                    if (parts.Length >= 5)
                    {
                        string meat = parts[1].Trim();
                        string herb = parts[2].Trim();
                        string dishName = parts[3].Trim();
                        string effect = parts[4].Trim();

                        // Skip if this is still a header or already-processed duplicate
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

                        if (_byName.ContainsKey(dishName))
                        {
                            Debug.LogWarning($"[DishDatabase] Duplicate dish name \"{dishName}\" — overwriting previous entry.");
                        }
                        _byName[dishName] = dish;

                        if (_byId.ContainsKey(dish.Id))
                        {
                            Debug.LogWarning($"[DishDatabase] Duplicate dish Id \"{dish.Id}\" — overwriting previous entry.");
                        }
                        _byId[dish.Id] = dish;

                        idx++;
                    }
                }
            }

            Debug.Log($"[DishDatabase] Loaded {_all.Count} dishes from GAME_DATA.md.");
        }

        /// <summary>
        /// Returns a read-only view of all loaded dishes.
        /// </summary>
        public static IReadOnlyList<DishInfo> All
        {
            get
            {
                if (!_initialized) Initialize();
                return _all.AsReadOnly();
            }
        }

        /// <summary>
        /// Looks up a dish by its display name. Returns null if not found.
        /// Name lookup is case-sensitive.
        /// </summary>
        public static DishInfo GetDishInfoByName(string name)
        {
            if (!_initialized) Initialize();
            _byName.TryGetValue(name, out var info);
            return info;
        }

        /// <summary>
        /// Looks up a dish by its ID (e.g., "D01"). Returns null if not found.
        /// </summary>
        public static DishInfo GetDishInfoById(string id)
        {
            if (!_initialized) Initialize();
            _byId.TryGetValue(id, out var info);
            return info;
        }

        /// <summary>
        /// Convenience: returns ItemData for a dish (ready to add to inventory).
        /// Returns null if the dish name is not found.
        /// </summary>
        public static PlayerInventory.ItemData GetItemData(string dishName)
        {
            var info = GetDishInfoByName(dishName);
            return info?.ToItemData();
        }
    }
}
