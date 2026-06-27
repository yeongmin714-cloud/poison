using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ProjectName.Core.Data
{
    /// <summary>
    /// 중독성 레벨 (GAME_DATA.md 마약 시스템)
    /// </summary>
    public enum AddictionLevel
    {
        Low,        // 낮음
        Medium,     // 보통
        High,       // 높음
        VeryHigh,   // 매우 높음
        Extreme,    // 극도로 높음
        Fatal       // 치명적
    }

    /// <summary>
    /// Represents a single drug entry from GAME_DATA.md section 2.5.
    /// Immutable to prevent struct-copy mutation bugs.
    /// </summary>
    public readonly struct DrugInfo
    {
        public int Stage { get; }              // 1~10
        public string DrugName { get; }        // 약물 명칭
        public string Ingredients { get; }     // 조합 재료 (e.g. "향기꽃 + 맑은잎")
        public AddictionLevel Addiction { get; }
        public string Description { get; }     // 특징

        public DrugInfo(int stage, string drugName, string ingredients, AddictionLevel addiction, string description)
        {
            Stage = stage;
            DrugName = drugName;
            Ingredients = ingredients;
            Addiction = addiction;
            Description = description;
        }
    }

    /// <summary>
    /// Loads drug (마약) data from GAME_DATA.md section 2.5 at runtime.
    /// Provides lookup by stage number or drug name.
    /// Thread-safe on initialization.
    /// </summary>
    public static class DrugDatabase
    {
        private static Dictionary<int, DrugInfo> _byStage = new Dictionary<int, DrugInfo>();
        private static Dictionary<string, DrugInfo> _byName = new Dictionary<string, DrugInfo>();
        private static bool _initialized = false;
        private static readonly object _lock = new object();

        private static void Initialize()
        {
            if (_initialized) return;

            lock (_lock)
            {
                if (_initialized) return;
                _initialized = true;
            }

            TextAsset txt = Resources.Load<TextAsset>("GAME_DATA");
            if (txt == null)
            {
                Debug.LogError("[DrugDatabase] Failed to load GAME_DATA.md from Resources.");
                return;
            }

            string content = txt.text;

            // Find the drug section: "### 2.5 마약 시스템"
            const string startMarker = "### 2.5 마약 시스템";
            int startIdx = content.IndexOf(startMarker);
            if (startIdx < 0)
            {
                Debug.LogError("[DrugDatabase] Could not find drug section (2.5) in GAME_DATA.md.");
                return;
            }

            int pos = startIdx + startMarker.Length;
            string[] lines = content.Substring(pos).Split('\n');

            // Table pattern: | 단계 | 약물 명칭 | 조합 재료 | 중독성 | 특징 |
            var tableRowRegex = new Regex(@"^\s*[|]\s*.+[|]\s*$");
            var separatorRegex = new Regex(@":-{2,}");

            int loadedCount = 0;

            foreach (string lineRaw in lines)
            {
                string trimmed = lineRaw.Trim();

                // Stop at next major section or subsection
                if (trimmed.StartsWith("## ") || trimmed.StartsWith("---"))
                    break;

                // Skip separator lines like |:---:|:----------|...
                if (separatorRegex.IsMatch(trimmed))
                    continue;

                if (tableRowRegex.IsMatch(trimmed))
                {
                    string[] parts = trimmed.Split(new char[] { '|' }, System.StringSplitOptions.RemoveEmptyEntries);
                    // Expected: [단계, 약물 명칭, 조합 재료, 중독성, 특징]
                    if (parts.Length >= 5)
                    {
                        string stageStr = parts[0].Trim();
                        string drugName = parts[1].Trim();
                        string ingredients = parts[2].Trim();
                        string addictionStr = parts[3].Trim();
                        string description = parts[4].Trim();

                        // Skip header row
                        if (stageStr.Equals("단계") || drugName.Equals("약물 명칭"))
                            continue;

                        if (!int.TryParse(stageStr, out int stage))
                            continue;

                        AddictionLevel addiction = ParseAddiction(addictionStr);

                        var info = new DrugInfo(stage, drugName, ingredients, addiction, description);

                        // Warn on duplicate stage
                        if (_byStage.ContainsKey(stage))
                            Debug.LogWarning($"[DrugDatabase] Duplicate stage {stage} ('{drugName}') — overwriting previous entry.");

                        // Warn on duplicate name
                        if (_byName.ContainsKey(drugName))
                            Debug.LogWarning($"[DrugDatabase] Duplicate drug name '{drugName}' (stage {stage}) — overwriting previous entry.");

                        _byStage[stage] = info;
                        _byName[drugName] = info;
                        loadedCount++;
                    }
                }
            }

            Debug.Log($"[DrugDatabase] Loaded {loadedCount} drugs from GAME_DATA.md.");
        }

        /// <summary>
        /// Parses Korean addiction level strings into the AddictionLevel enum.
        /// Logs a warning for unrecognized values.
        /// </summary>
        private static AddictionLevel ParseAddiction(string raw)
        {
            return raw switch
            {
                "낮음" => AddictionLevel.Low,
                "보통" => AddictionLevel.Medium,
                "높음" => AddictionLevel.High,
                "매우 높음" => AddictionLevel.VeryHigh,
                "극도로 높음" => AddictionLevel.Extreme,
                "치명적" => AddictionLevel.Fatal,
                _ when string.IsNullOrEmpty(raw) => AddictionLevel.Low,
                _ => LogUnknownAddiction(raw)
            };
        }

        private static AddictionLevel LogUnknownAddiction(string raw)
        {
            Debug.LogWarning($"[DrugDatabase] Unknown addiction level '{raw}' — defaulting to Low.");
            return AddictionLevel.Low;
        }

        /// <summary>
        /// Returns drug info by stage number (1-10), or null if not found.
        /// </summary>
        public static DrugInfo? GetByStage(int stage)
        {
            if (!_initialized) Initialize();
            if (_byStage.TryGetValue(stage, out var info))
                return info;
            return null;
        }

        /// <summary>
        /// Returns drug info by drug name, or null if not found.
        /// </summary>
        public static DrugInfo? GetByName(string drugName)
        {
            if (!_initialized) Initialize();
            if (_byName.TryGetValue(drugName, out var info))
                return info;
            return null;
        }

        /// <summary>
        /// Returns all loaded drugs (read-only), ordered by stage.
        /// </summary>
        public static IReadOnlyList<DrugInfo> All
        {
            get
            {
                if (!_initialized) Initialize();
                var list = new List<DrugInfo>(_byStage.Count);
                foreach (int stage in _byStage.Keys.OrderBy(k => k))
                {
                    list.Add(_byStage[stage]);
                }
                return list;
            }
        }
    }
}