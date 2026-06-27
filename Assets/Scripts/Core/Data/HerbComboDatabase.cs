using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ProjectName.Core.Data
{
    /// <summary>
    /// Represents the result of combining two herbs.
    /// </summary>
    public struct HerbComboResult
    {
        public string resultId { get; set; }       // Could be used to look up an item (e.g., potion)
        public string resultName { get; set; }
        public string description { get; set; }
        public string effect { get; set; }         // From the "효과" column
    }

    /// <summary>
    /// Loads herb combination data from GAME_DATA.md at runtime.
    /// Provides lookup by unordered pair of herb IDs.
    /// </summary>
    public static class HerbComboDatabase
    {
        private static Dictionary<string, HerbComboResult> _combos = new Dictionary<string, HerbComboResult>();
        private static bool _initialized = false;

        private static readonly Regex SubsectionHeaderRegex = new Regex(@"^###\s*\d+\.\d+\s*");
        private static readonly Regex TableRowRegex = new Regex(@"^\s*[|]\s*.+\s*[|]\s*$");
        private static readonly Regex SeparatorRegex = new Regex(@"^\s*\|[-:\s]+\|"); // matches |:-:|, |:----|:-----| etc.

        private static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            // Load the markdown file from Resources
            TextAsset txt = Resources.Load<TextAsset>("GAME_DATA");
            if (txt == null)
            {
                Debug.LogError("[HerbComboDatabase] Failed to load GAME_DATA.md from Resources.");
                return;
            }

            string content = txt.text;

            // Find the combination section start
            const string startMarker = "## 🧪 2. 약물 조합 — 총 80종 조합법";
            int startIdx = content.IndexOf(startMarker);
            if (startIdx < 0)
            {
                Debug.LogError("[HerbComboDatabase] Could not find combination section in GAME_DATA.md.");
                return;
            }

            int pos = startIdx + startMarker.Length;
            string[] lines = content.Substring(pos).Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.None);

            // We'll parse until the next major section (starts with "## ")
            // Subsections: 2.1 공격성 조합, 2.2 정신성 조합, 2.3 회복성 조합, 2.4 물리성 조합, 2.5 마약 시스템 (we stop before 2.5)

            string currentSubsection = "";
            foreach (string lineRaw in lines)
            {
                string line = lineRaw.Trim();

                // Stop before the drug (마약 시스템) section — its table format is incompatible
                if (line.Contains("마약 시스템")) break;

                // Stop at next major section
                if (line.StartsWith("## "))
                {
                    break;
                }

                // Detect subsection header
                if (SubsectionHeaderRegex.IsMatch(line))
                {
                    currentSubsection = line;
                    continue;
                }

                // Skip separator lines
                if (SeparatorRegex.IsMatch(line))
                    continue;

                // Parse table row
                if (TableRowRegex.IsMatch(line))
                {
                    // Split by '|', remove empty first/last
                    string[] parts = line.Split(new char[] { '|' }, System.StringSplitOptions.RemoveEmptyEntries);
                    // Expected format: 재료1 | 재료2 | 결과물 | 효과
                    if (parts.Length >= 4)
                    {
                        string herb1 = parts[0].Trim();
                        string herb2 = parts[1].Trim();
                        string resultName = parts[2].Trim();
                        string effect = parts[3].Trim();

                        // Skip header rows where herb1 is "재료1" etc., or where the header has "단계"/"약물 명칭" (마약 section has different format)
                        if (herb1.Equals("재료1") || herb2.Equals("재료2") || resultName.Equals("결과물") || effect.Equals("효과"))
                            continue;
                        // 마약 section (2.5) has different table format with "단계/약물 명칭" headers — skip those rows
                        if (herb1.Equals("단계") || herb2.Equals("약물 명칭"))
                            continue;

                        // We need to map herb names to IDs. For simplicity, we assume the herb names exactly match the displayName from HerbDatabase.
                        // We'll look up the IDs by displayName.
                        var info1 = HerbDatabase.GetHerbInfoByDisplayName(herb1);
                        var info2 = HerbDatabase.GetHerbInfoByDisplayName(herb2);
                        if (!string.IsNullOrEmpty(info1.id) && !string.IsNullOrEmpty(info2.id))
                        {
                            string key = MakeKey(info1.id, info2.id);
                            // Not checking ContainsKey — last combo for a pair wins, which is intentional
                            _combos[key] = new HerbComboResult
                            {
                                resultId = $"combo_{info1.id}_{info2.id}", // placeholder
                                resultName = resultName,
                                description = $"{herb1} + {herb2} 의 결과물",
                                effect = effect
                            };
                        }
                        else
                        {
                            Debug.LogWarning($"[HerbComboDatabase] Unknown herb name in combo: '{herb1}' / '{herb2}' — skipping.");
                        }
                    }
                }
            }

            Debug.Log($"[HerbComboDatabase] Loaded {_combos.Count} herb combinations from GAME_DATA.md.");
        }

        private static string MakeKey(string id1, string id2)
        {
            // Order-independent key
            return string.CompareOrdinal(id1, id2) < 0 ? $"{id1}_{id2}" : $"{id2}_{id1}";
        }

        /// <summary>
        /// Returns the combination result for two herb IDs, or null if not found.
        /// Order of IDs does not matter.
        /// </summary>
        public static HerbComboResult? GetCombo(string herbId1, string herbId2)
        {
            Initialize();
            string key = MakeKey(herbId1, herbId2);
            if (_combos.TryGetValue(key, out var result))
                return result;
            return null;
        }

        /// <summary>
        /// Returns all loaded combinations (read-only).
        /// </summary>
        public static IReadOnlyDictionary<string, HerbComboResult> AllCombos
        {
            get
            {
                Initialize();
                return new System.Collections.ObjectModel.ReadOnlyDictionary<string, HerbComboResult>(_combos);
            }
        }
    }

    /// <summary>
    /// Extension to HerbDatabase to lookup by display name.
    /// </summary>
    public static partial class HerbDatabase
    {
        // We'll add a method to get HerbInfo by displayName.
        // Since HerbDatabase is static partial, we can add here.
        private static Dictionary<string, HerbInfo> _displayNameLookup = null;

        private static void EnsureDisplayNameLookup()
        {
            if (_displayNameLookup != null) return;
            _displayNameLookup = new Dictionary<string, HerbInfo>();
            foreach (var h in AllHerbs)
            {
                if (!string.IsNullOrEmpty(h.displayName) && !_displayNameLookup.ContainsKey(h.displayName))
                {
                    _displayNameLookup[h.displayName] = h;
                }
            }
        }

        public static HerbInfo GetHerbInfoByDisplayName(string displayName)
        {
            EnsureDisplayNameLookup();
            _displayNameLookup.TryGetValue(displayName, out var info);
            return info;
        }
    }
}