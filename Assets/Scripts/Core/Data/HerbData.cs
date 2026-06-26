using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ProjectName.Core.Data
{
    public enum HerbAttribute
    {
        Attack,
        Mental,
        Recovery,
        Physical
    }

    public struct HerbInfo
    {
        public readonly string id;        // e.g., "A1"
        public readonly string displayName; // e.g., "쓴풀"
        public readonly string description;
        public readonly HerbAttribute attribute;
        public readonly int index;        // 1-10 within attribute

        public HerbInfo(string id, string displayName, string description, HerbAttribute attribute, int index)
        {
            this.id = id;
            this.displayName = displayName;
            this.description = description;
            this.attribute = attribute;
            this.index = index;
        }
    }

    /// <summary>
    /// Loads herb data from GAME_DATA.md at runtime.
    /// Provides lookup by herb id.
    /// </summary>
    public static partial class HerbDatabase
    {
        private static List<HerbInfo> _herbs = new List<HerbInfo>();
        private static bool _initialized = false;

        private static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            // Load the markdown file from Resources
            TextAsset txt = Resources.Load<TextAsset>("GAME_DATA");
            if (txt == null)
            {
                Debug.LogError("[HerbDatabase] Failed to load GAME_DATA.md from Resources.");
                return;
            }

            string content = txt.text;
            // Find the herb section
            const string startMarker = "## 🌿 1. 약초 (Herbs) — 4대 속성 × 10종 = 총 40종";
            int startIdx = content.IndexOf(startMarker);
            if (startIdx < 0)
            {
                Debug.LogError("[HerbDatabase] Could not find herb section in GAME_DATA.md.");
                return;
            }

            // Move to the start of the section
            int pos = startIdx + startMarker.Length;
            // We'll parse until next "## " (next major section) or end of file.
            // For simplicity, we'll parse lines until we hit a line that starts with "## " and not part of a subsection.
            string[] lines = content.Substring(pos).Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);

            HerbAttribute currentAttr = HerbAttribute.Attack; // default
            // Regex to detect subsection lines like "### 1.1 공격성 (🔴 붉은색 계열) — 독살 및 타격"
            // Actually Korean: 공격성, 정신성, 회복성, 물리성
            var attrRegex = new Regex(@"(공격성|정신성|회복성|물리성)");
            // Table row pattern: starts with || or | then cells separated by |

            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                // Detect new major section (starts with "## ") -> stop parsing herbs
                if (trimmed.StartsWith("## "))
                {
                    break;
                }
                // Detect subsection
                if (trimmed.StartsWith("###"))
                {
                    Match m = attrRegex.Match(trimmed);
                    if (m.Success)
                    {
                        string attrName = m.Value;
                        switch (attrName)
                        {
                            case "공격성": currentAttr = HerbAttribute.Attack; break;
                            case "정신성": currentAttr = HerbAttribute.Mental; break;
                            case "회복성": currentAttr = HerbAttribute.Recovery; break;
                            case "물리성": currentAttr = HerbAttribute.Physical; break;
                        }
                    }
                    continue;
                }
                // Skip separator lines (like |:-:|:----|:-----|)
                if (trimmed.StartsWith("|:-") || trimmed.StartsWith("|---"))
                    continue;
                // Parse row if it looks like a table row
                if (trimmed.StartsWith("|") && trimmed.Contains("|"))
                {
                    // Split by '|', ignore first and last empty if present
                    string[] parts = trimmed.Split(new char[] { '|' }, System.StringSplitOptions.RemoveEmptyEntries);
                    // Expect 3 parts: #, 이름, 설명
                    if (parts.Length >= 3)
                    {
                        string id = parts[0].Trim();
                        string name = parts[1].Trim();
                        string desc = parts[2].Trim();
                        // Filter out header rows where id is "#" or empty
                        if (id == "#" || string.IsNullOrEmpty(id))
                            continue;
                        // Parse index from id (e.g., A1 -> 1, H10 -> 10)
                        int index = 0;
                        if (id.Length >= 2)
                        {
                            string numPart = id.Substring(1);
                            if (int.TryParse(numPart, out int parsed))
                            {
                                index = parsed;
                            }
                        }
                        // Add herb
                        _herbs.Add(new HerbInfo(
                            id,
                            name,
                            desc,
                            currentAttr,
                            index
                        ));
                    }
                }
            }

            Debug.Log($"[HerbDatabase] Loaded {_herbs.Count} herbs from GAME_DATA.md.");
        }

        public static HerbInfo GetHerbInfo(string herbId)
        {
            Initialize();
            foreach (var h in _herbs)
            {
                if (h.id.Equals(herbId))
                    return h;
            }
            return new HerbInfo(herbId, "", "", default, 0); // return empty
        }

        public static IReadOnlyList<HerbInfo> AllHerbs => _herbs;
    }
}