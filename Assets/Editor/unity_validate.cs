using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// QA Validation methods for Poison Game.
/// Invoked via Unity batch mode:
///   Unity.exe -quit -batchmode -projectPath <path> -executeMethod QaValidator.RunAllChecks
/// </summary>
public static class QaValidator
{
    private static List<string> errors = new List<string>();
    private static List<string> warnings = new List<string>();
    private static string reportPath;

    public static void RunAllChecks()
    {
        reportPath = Environment.GetEnvironmentVariable("QA_REPORT_DIR") ?? ".";
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var reportFile = Path.Combine(reportPath, $"qa_report_{timestamp}.txt");

        Debug.Log($"[QA] Starting validation — report: {reportFile}");
        errors.Clear();
        warnings.Clear();

        CheckCompilationErrors();
        CheckScenes();
        CheckMissingScripts();
        CheckDuplicateAssets();

        WriteReport(reportFile);
        Debug.Log($"[QA] Validation complete — Errors: {errors.Count}, Warnings: {warnings.Count}");

        // Exit with error code if any errors found
        if (errors.Count > 0)
        {
            EditorApplication.Exit(1);
        }
    }

    /// <summary>
    /// Check 1: Compilation errors in all scripts
    /// </summary>
    private static void CheckCompilationErrors()
    {
        Debug.Log("[QA] Checking compilation errors...");

        // Force recompile by requesting script reload
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

        // Check the log for compile errors
        var logFile = Path.Combine(Application.dataPath, "../Logs/compile_errors.log");
        if (File.Exists(logFile))
        {
            var lines = File.ReadAllLines(logFile);
            foreach (var line in lines.Where(l => l.Contains("error CS")))
            {
                errors.Add($"[COMPILE] {line}");
            }
        }

        // Also check via UnityEditor internals
        var logEntriesType = Type.GetType("UnityEditor.LogEntries,UnityEditor");
        if (logEntriesType != null)
        {
            var logCount = (int)logEntriesType.GetMethod("GetCount").Invoke(null, null);
            for (int i = 0; i < logCount; i++)
            {
                var entryType = (int)logEntriesType.GetMethod("GetEntryType", new[] { typeof(int) })
                    .Invoke(null, new object[] { i });
                if (entryType == 2) // Error
                {
                    var entryStr = (string)logEntriesType.GetMethod("GetEntryMsg", new[] { typeof(int) })
                        .Invoke(null, new object[] { i });
                    if (entryStr.Contains("error CS"))
                    {
                        errors.Add($"[COMPILE] {entryStr}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Check 2: Scene integrity — all scenes load without errors
    /// </summary>
    private static void CheckScenes()
    {
        Debug.Log("[QA] Checking scene integrity...");

        var sceneGuids = AssetDatabase.FindAssets("t:Scene");
        foreach (var guid in sceneGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            Debug.Log($"[QA] Opening scene: {path}");

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                errors.Add($"[SCENE] Invalid scene: {path}");
                continue;
            }

            // Check for missing scripts in scene
            var rootObjs = scene.GetRootGameObjects();
            foreach (var obj in rootObjs)
            {
                CheckGameObjectForMissingScripts(obj, path);
            }
        }
    }

    /// <summary>
    /// Check 3: Missing scripts in prefabs and scenes
    /// </summary>
    private static void CheckMissingScripts()
    {
        Debug.Log("[QA] Checking for missing scripts...");

        var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        foreach (var guid in prefabGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                CheckGameObjectForMissingScripts(prefab, path);
            }
        }
    }

    private static void CheckGameObjectForMissingScripts(GameObject obj, string context)
    {
        var components = obj.GetComponents<Component>();
        foreach (var comp in components)
        {
            if (comp == null)
            {
                warnings.Add($"[MISSING_SCRIPT] {context} -> {GetGameObjectPath(obj)} has missing script");
            }
        }

        foreach (Transform child in obj.transform)
        {
            CheckGameObjectForMissingScripts(child.gameObject, context);
        }
    }

    /// <summary>
    /// Check 4: Duplicate asset names (potential conflicts)
    /// </summary>
    private static void CheckDuplicateAssets()
    {
        Debug.Log("[QA] Checking for duplicate asset names...");

        var allGuids = AssetDatabase.FindAssets("");
        var nameCounts = new Dictionary<string, List<string>>();
        foreach (var guid in allGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var name = Path.GetFileName(path);
            if (!nameCounts.ContainsKey(name))
                nameCounts[name] = new List<string>();
            nameCounts[name].Add(path);
        }

        foreach (var kvp in nameCounts.Where(x => x.Value.Count > 1))
        {
            warnings.Add($"[DUPLICATE] {kvp.Key} appears in multiple locations ({kvp.Value.Count}x):");
            foreach (var p in kvp.Value)
                warnings.Add($"           - {p}");
        }
    }

    private static string GetGameObjectPath(GameObject obj)
    {
        var path = obj.name;
        var parent = obj.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }

    private static void WriteReport(string path)
    {
        using (var writer = new StreamWriter(path))
        {
            writer.WriteLine("========================================");
            writer.WriteLine("  Poison Game — QA Validation Report");
            writer.WriteLine($"  Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine("========================================");
            writer.WriteLine();
            writer.WriteLine($"Errors:   {errors.Count}");
            writer.WriteLine($"Warnings: {warnings.Count}");
            writer.WriteLine();
            writer.WriteLine("--- ERRORS ---");
            foreach (var e in errors)
                writer.WriteLine($"  [ERR] {e}");
            writer.WriteLine();
            writer.WriteLine("--- WARNINGS ---");
            foreach (var w in warnings)
                writer.WriteLine($"  [WARN] {w}");
            writer.WriteLine();
            writer.WriteLine("--- ENVIRONMENT ---");
            writer.WriteLine($"  Unity Version: {Application.unityVersion}");
            writer.WriteLine($"  Project: {Application.dataPath}");
            writer.WriteLine($"  Platform: {Application.platform}");
            writer.WriteLine();
            writer.WriteLine("--- END OF REPORT ---");
        }
    }
}
