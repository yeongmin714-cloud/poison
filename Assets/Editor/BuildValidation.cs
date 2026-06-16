using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// C10-23: 빌드 검증 도구 — 씬/스크립트/메시/asmdef 검사
/// </summary>
public static class BuildValidation
{
    private static string _reportPath = "BuildValidationReport.txt";
    private static int _errorCount = 0;
    private static int _warningCount = 0;

    [MenuItem("Tools/Validate Build")]
    public static void ValidateBuild()
    {
        _errorCount = 0;
        _warningCount = 0;
        var report = new System.Text.StringBuilder();
        report.AppendLine("=== Build Validation Report ===");
        report.AppendLine($"Generated: {System.DateTime.Now:yyyy-MM-dd HH:mm}");
        report.AppendLine();

        // 1. Scene 검사
        report.AppendLine("--- Scene Check ---");
        var scenes = EditorBuildSettings.scenes;
        if (scenes == null || scenes.Length == 0)
        {
            report.AppendLine("[ERROR] No scenes in Build Settings!");
            _errorCount++;
        }
        else
        {
            report.AppendLine($"Found {scenes.Length} scenes in Build Settings:");
            foreach (var s in scenes)
            {
                string status = s.enabled ? "enabled" : "DISABLED";
                string exists = File.Exists(s.path) ? "exists" : "MISSING";
                report.AppendLine($"  [{status}] [{exists}] {s.path}");
                if (!File.Exists(s.path) || !s.enabled)
                {
                    report.AppendLine("  [ERROR] Scene missing or disabled!");
                    _errorCount++;
                }
            }
        }
        report.AppendLine();

        // 2. Placeholder 교체 현황
        report.AppendLine("--- Placeholder Model Check ---");
        string userProvided = "Assets/Resources/Models/UserProvided";
        if (Directory.Exists(userProvided))
        {
            var glbFiles = Directory.GetFiles(userProvided, "*.glb");
            report.AppendLine($"Found {glbFiles.Length} GLB files in UserProvided/");

            var expected = GetExpectedPlaceholders();
            int replaced = 0;
            foreach (var expectedName in expected)
            {
                bool found = false;
                foreach (var glb in glbFiles)
                {
                    string name = Path.GetFileNameWithoutExtension(glb).ToLower();
                    if (name.StartsWith(expectedName.ToLower()))
                    { found = true; break; }
                }
                if (found) replaced++;
                else report.AppendLine($"  [WARN] Missing GLB for: {expectedName}");
            }
            report.AppendLine($"Replaced: {replaced}/{expected.Count} placeholders");
            if (replaced < expected.Count)
            {
                report.AppendLine($"[WARN] {expected.Count - replaced} placeholders still need GLB models");
                _warningCount++;
            }
        }
        else
        {
            report.AppendLine("[WARN] UserProvided/ folder does not exist. No GLB models available.");
            _warningCount++;
        }
        report.AppendLine();

        // 3. asmdef 검사
        report.AppendLine("--- Assembly Definition Check ---");
        var asmdefFiles = Directory.GetFiles("Assets/Scripts", "*.asmdef", SearchOption.AllDirectories);
        report.AppendLine($"Found {asmdefFiles.Length} .asmdef files:");
        foreach (var f in asmdefFiles)
        {
            string relative = f.Replace("\\", "/");
            report.AppendLine($"  {relative}");
        }
        if (asmdefFiles.Length == 0)
        {
            report.AppendLine("[WARN] No .asmdef files found!");
            _warningCount++;
        }
        report.AppendLine();

        // 4. 최종 요약
        report.AppendLine("--- Summary ---");
        report.AppendLine($"Errors: {_errorCount}");
        report.AppendLine($"Warnings: {_warningCount}");
        report.AppendLine(_errorCount == 0 ? "RESULT: PASS" : "RESULT: FAIL");

        File.WriteAllText(_reportPath, report.ToString());
        Debug.Log(report.ToString());
        Debug.Log($"[BuildValidation] Report saved to {_reportPath}");

        EditorUtility.DisplayDialog("Build Validation",
            $"Errors: {_errorCount}\nWarnings: {_warningCount}\nReport saved to {_reportPath}",
            "OK");
    }

    private static System.Collections.Generic.List<string> GetExpectedPlaceholders()
    {
        return new System.Collections.Generic.List<string>
        {
            "player", "hut", "blue_castle", "green_castle", "purple_castle",
            "red_castle", "kingdom", "craft_blend", "craft_cook",
            "lord_npc", "soldier",
            "herb_red", "herb_green", "herb_blue", "herb_purple", "herb_yellow", "herb_silver",
            "rabbit", "wolf", "boar", "deer", "crow", "bat", "snake", "giant_rat",
            "slime", "golem", "fire_lizard", "electric_spine_hedgehog",
            "swamp_alligator", "wild_troll", "wooden_forest_spirit",
            "swamp_ogre", "banshee", "griffon", "minotaur", "manticore", "salamander", "shadow_assassin",
            "potion_heal", "potion_poison", "potion_drug", "potion_antidote", "recipebook"
        };
    }
}
