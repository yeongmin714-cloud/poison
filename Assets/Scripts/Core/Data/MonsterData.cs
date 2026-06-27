#pragma warning disable 0414
﻿using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ProjectName.Core.Data
{
/// <summary>
/// Parses monster data from GAME_DATA.md and provides runtime lookup.
/// Handles all three monster subsections: 3.1 (Beginner), 3.2 (Intermediate), 3.3 (Advanced).
/// </summary>
public static class MonsterDataReader
{
    private static bool _initialized;
    private static readonly Dictionary<string, MonsterInfo> _byName = new Dictionary<string, MonsterInfo>();
    private static readonly Dictionary<string, MonsterInfo> _byId = new Dictionary<string, MonsterInfo>();

    public class MonsterInfo
    {
        public string Id { get; set; }   // e.g., "M01"
        public string Name { get; set; } // display name, e.g., "토끼"
        public string Description { get; set; } // from "주요 용도" column
        public string[] DropItems { get; set; } // e.g., new[] {"토끼 고기", "토끼 가죽"}
    }

    public static void Initialize()
    {
        if (_initialized) return;
        var text = Resources.Load<TextAsset>("GAME_DATA")?.text;
        if (text == null)
        {
            Debug.LogError("[MonsterDataReader] GAME_DATA.md not found in Resources!");
            return;
        }

        int totalParsed = 0;
        string[] sectionHeaders = { "## 🥩 3. 몬스터" };

        // Find the monster section
        var lines = text.Split('\n');
        int sectionStart = 0;
        while (sectionStart < lines.Length && !lines[sectionStart].Contains(sectionHeaders[0]))
            sectionStart++;

        if (sectionStart >= lines.Length)
        {
            Debug.LogError("[MonsterDataReader] Monster section not found");
            return;
        }

        // Parse all monster subsections (3.1, 3.2, 3.3) by looking for multiple table headers
        // Each subsection has its own "|| 몬스터 |" table header row.
        // We iterate through the file looking for each table header after the section start.
        int searchIdx = sectionStart;
        int idx = 1;

        while (searchIdx < lines.Length)
        {
            // Look for next table header "|| 몬스터 |" starting from current position
            int tableStart = searchIdx;
            while (tableStart < lines.Length && !lines[tableStart].Contains("|| 몬스터 |"))
                tableStart++;

            if (tableStart >= lines.Length)
                break; // No more monster tables

            // Skip header row and separator row (with dashes)
            int row = tableStart + 2; // now at first data row

            // Parse rows for this subsection
            while (row < lines.Length && lines[row].TrimStart().StartsWith("||"))
            {
                var line = lines[row].Trim();
                // Remove leading/trailing ||
                if (line.StartsWith("||")) line = line.Substring(2);
                if (line.EndsWith("||")) line = line.Substring(0, line.Length - 2);
                var parts = line.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                // Expected: 몬스터 | 획득 재료 | 주요 용도
                if (parts.Length >= 3)
                {
                    var name = parts[0].Trim();
                    var dropsRaw = parts[1].Trim();
                    var desc = parts[2].Trim();

                    var drops = Regex.Split(dropsRaw, @"[,、]+");
                    var dropList = new List<string>();
                    foreach (var d in drops)
                    {
                        var trimmed = d.Trim();
                        if (!string.IsNullOrEmpty(trimmed))
                            dropList.Add(trimmed);
                    }

                    var info = new MonsterInfo
                    {
                        Id = $"M{idx:D2}",
                        Name = name,
                        Description = desc,
                        DropItems = dropList.ToArray()
                    };
                    _byName[name] = info;
                    _byId[info.Id] = info;
                    idx++;
                    totalParsed++;
                }
                row++;
            }

            // Move search position past this table to find the next one
            searchIdx = row + 1;
        }

        _initialized = true;
        Debug.Log($"[MonsterDataReader] Loaded {totalParsed} monsters across all tiers.");
    }

    public static MonsterInfo GetMonsterInfoByName(string name)
    {
        if (!_initialized) Initialize();
        _byName.TryGetValue(name, out var info);
        return info;
    }

    public static MonsterInfo GetMonsterInfoById(string id)
    {
        if (!_initialized) Initialize();
        _byId.TryGetValue(id, out var info);
        return info;
    }

    public static IReadOnlyDictionary<string, MonsterInfo> All => _byName;
}
}