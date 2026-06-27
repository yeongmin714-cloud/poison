using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Editor
{
    /// <summary>
    /// Editor script to import herb data from GAME_DATA.md and create Herb ScriptableObject assets.
    /// Run via Assets -> Import Herb Data.
    /// </summary>
    public static class HerbDatabaseEditor
    {
        private const string MenuItemPath = "Assets/Import Herb Data";
        private const string DataFilePath = "Assets/Resources/GAME_DATA.md";
        private const string OutputFolder = "Assets/Data/Herbs";

        [MenuItem(MenuItemPath)]
        public static void ImportHerbData()
        {
            // Ensure output folder exists
            if (!AssetDatabase.IsValidFolder("Assets/Data"))
            {
                AssetDatabase.CreateFolder("Assets", "Data");
                AssetDatabase.Refresh();
            }
            if (!AssetDatabase.IsValidFolder(OutputFolder))
            {
                AssetDatabase.CreateFolder("Assets/Data", "Herbs");
            }

            // Load the markdown file
            string fullPath = Path.Combine(Application.dataPath, DataFilePath.Replace("Assets/", ""));
            if (!File.Exists(fullPath))
            {
                EditorUtility.DisplayDialog("Error", $"Cannot find {DataFilePath}", "OK");
                return;
            }

            string content = File.ReadAllText(fullPath);

            // Parse herbs (same logic as HerbDatabase but we'll reuse or copy)
            List<HerbInfo> herbs = ParseHerbs(content);
            if (herbs == null || herbs.Count == 0)
            {
                EditorUtility.DisplayDialog("Error", "No herbs found in the data file.", "OK");
                return;
            }

            // Clear existing assets in the output folder (optional)
            // string[] existingGuids = AssetDatabase.FindAssets("t:Herb", new[] { OutputFolder });
            // foreach (string guid in existingGuids)
            // {
            //     string path = AssetDatabase.GUIDToAssetPath(guid);
            //     AssetDatabase.DeleteAsset(path);
            // }

            int created = 0;
            foreach (var info in herbs)
            {
                // Check if asset with this id already exists to avoid duplicates
                string assetPath = Path.Combine(OutputFolder, $"{info.id}.asset");
                Herb existing = AssetDatabase.LoadAssetAtPath<Herb>(assetPath);
                if (existing != null)
                {
                    // Update existing
                    existing.Id = info.id;
                    existing.DisplayName = info.displayName;
                    existing.Description = info.description;
                    existing.Attribute = info.attribute;
                    existing.Index = info.index;
                    EditorUtility.SetDirty(existing);
                }
                else
                {
                    // Create new
                    Herb herb = ScriptableObject.CreateInstance<Herb>();
                    herb.Id = info.id;
                    herb.DisplayName = info.displayName;
                    herb.Description = info.description;
                    herb.Attribute = info.attribute;
                    herb.Index = info.index;
                    AssetDatabase.CreateAsset(herb, assetPath);
                    created++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Success",
                $"Imported {herbs.Count} herbs. Created {created} new assets, updated {herbs.Count - created} existing.",
                "OK");
        }

        // Reuse the parsing logic from HerbDatabase (copy-paste for simplicity)
        private class HerbInfo
        {
            public string id;
            public string displayName;
            public string description;
            public HerbAttribute attribute;
            public int index;
        }

        private static List<HerbInfo> ParseHerbs(string content)
        {
            List<HerbInfo> herbs = new List<HerbInfo>();
            const string startMarker = "## 🌿 1. 약초 (Herbs) — 4대 속성 × 10종 = 총 40종";
            int startIdx = content.IndexOf(startMarker);
            if (startIdx < 0) return herbs;

            int pos = startIdx + startMarker.Length;
            string[] lines = content.Substring(pos).Split('\n');

            HerbAttribute currentAttr = HerbAttribute.Attack;
            var attrRegex = new Regex(@"(공격성|정신성|회복성|물리성)");

            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("## "))
                {
                    break;
                }
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
                if (trimmed.StartsWith("|:-") || trimmed.StartsWith("|---"))
                    continue;
                if (trimmed.StartsWith("|") && trimmed.Contains("|"))
                {
                    string[] parts = trimmed.Split(new char[] { '|' }, System.StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        string id = parts[0].Trim();
                        string name = parts[1].Trim();
                        string desc = parts[2].Trim();
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
                        herbs.Add(new HerbInfo
                        {
                            id = id,
                            displayName = name,
                            description = desc,
                            attribute = currentAttr,
                            index = index
                        });
                    }
                }
            }
            return herbs;
        }
    }
}