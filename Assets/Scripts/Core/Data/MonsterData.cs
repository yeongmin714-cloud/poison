using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Core
{
/// <summary>
/// Parses monster data from GAME_DATA.md and provides runtime lookup.
/// </summary>
public static class MonsterDataReader
{
    private static bool _initialized;
    private static readonly Dictionary<string, MonsterInfo> _byName = new Dictionary<string, MonsterInfo>();
    private static readonly Dictionary<string, MonsterInfo> _byId = new Dictionary<string, MonsterInfo>();

    public class MonsterInfo
    {
        public string Id;   // e.g., "M01"
        public string Name; // display name, e.g., "토끼"
        public string Description; // from "주요 용도" column maybe
        public string[] DropItems; // e.g., new[] {"토끼 고기", "토끼 가죽"}
    }

    public static void Initialize()
    {
        if (_initialized) return;
        var text = Resources.Load<TextAsset>("GAME_DATA")?.text;
        if (text == null)
        {
            Debug.LogError("[MonsterDatabase] GAME_DATA.md not found in Resources!");
            return;
        }

        // Find monster section
        var lines = text.Split('\n');
        int i = 0;
        while (i < lines.Length && !lines[i].Contains("## 🥩 3. 몬스터")) i++;
        if (i >= lines.Length) { Debug.LogError("[MonsterDatabase] Monster section not found"); return; }

        // Skip to table start (look for line with "|| 몬스터 |")
        while (i < lines.Length && !lines[i].Contains("|| 몬스터 |")) i++;
        if (i >= lines.Length) { Debug.LogError("[MonsterDatabase] Monster table not found"); return; }
        i++; // skip header separator line (the line with dashes)
        i++; // now at first row

        int idx = 1;
        while (i < lines.Length && lines[i].TrimStart().StartsWith("||"))
        {
            var line = lines[i].Trim();
            // Remove leading/trailing ||
            if (line.StartsWith("||")) line = line.Substring(2);
            if (line.EndsWith("||")) line = line.Substring(0, line.Length - 2);
            var parts = line.Split(new[] { '|' }, System.StringSplitOptions.RemoveEmptyEntries);
            // Expected: 몬스터 | 획득 재료 | 주요 용도
            if (parts.Length >= 3)
            {
                var name = parts[0].Trim();
                var dropsRaw = parts[1].Trim();
                var desc = parts[2].Trim();

                var drops = System.Text.RegularExpressions.Regex.Split(dropsRaw, @"[,、]+");
                var dropList = new System.Collections.Generic.List<string>();
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
            }
            i++;
        }

        _initialized = true;
        Debug.Log($"[MonsterDatabase] Loaded {_byName.Count} monsters.");
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
