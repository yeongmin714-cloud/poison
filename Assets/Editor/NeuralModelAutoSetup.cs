using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectName.Systems.Animation.Neural;

/// <summary>
/// Editor utility for automatically discovering ONNX models in
/// Assets/Resources/NeuralModels/ and creating/populating a
/// NeuralModelDatabase asset.
///
/// Usage: Tools → Neural → Auto-Setup Model Database
/// </summary>
public static class NeuralModelAutoSetup
{
    // ──────────────────────────────────────────────
    // Constants
    // ──────────────────────────────────────────────

    private const string ModelsResourcesPath = "Assets/Resources/NeuralModels";
    private const string DatabaseAssetPath = "Assets/Resources/NeuralModelDatabase.asset";

    // Known model specs: file prefix → (obs, act, jointCount, avatarType)
    // Biped base models: obs=120, act=80, joints=18
    // Quadruped model:   obs=150, act=100, joints=24
    private static readonly Dictionary<string, ModelSpec> KnownSpecs = new()
    {
        ["locomotion_biped_base"] = new ModelSpec(120, 80, 18, AvatarType.Humanoid,
            "Locomotion_Biped_Base", "1.0.0", QuantizationFormat.INT8, 11, 2.0f, 8),
        ["locomotion_biped"] = new ModelSpec(120, 80, 18, AvatarType.Humanoid,
            "Locomotion_Biped_Base", "1.0.0", QuantizationFormat.INT8, 11, 2.0f, 8),
        ["combat_biped"] = new ModelSpec(120, 80, 18, AvatarType.Humanoid,
            "Combat_Biped", "1.0.0", QuantizationFormat.INT8, 11, 2.0f, 8),
        ["react_biped"] = new ModelSpec(120, 80, 18, AvatarType.Humanoid,
            "React_Biped", "1.0.0", QuantizationFormat.INT8, 11, 2.0f, 8),
        ["interact_biped"] = new ModelSpec(120, 80, 18, AvatarType.Humanoid,
            "Interact_Biped", "1.0.0", QuantizationFormat.INT8, 11, 2.0f, 8),
        ["locomotion_quadruped"] = new ModelSpec(150, 100, 24, AvatarType.Quadruped,
            "Locomotion_Quadruped_Base", "1.0.0", QuantizationFormat.INT8, 11, 3.0f, 8),
    };

    // Policy type mapping: file prefix → PolicyType
    private static readonly Dictionary<string, NeuralAnimationController.PolicyType> PolicyTypeMap = new()
    {
        ["locomotion_biped_base"] = NeuralAnimationController.PolicyType.Locomotion,
        ["locomotion_biped"] = NeuralAnimationController.PolicyType.Locomotion,
        ["combat_biped"] = NeuralAnimationController.PolicyType.Combat,
        ["react_biped"] = NeuralAnimationController.PolicyType.React,
        ["interact_biped"] = NeuralAnimationController.PolicyType.Interact,
        ["locomotion_quadruped"] = NeuralAnimationController.PolicyType.Locomotion,
    };

    // ──────────────────────────────────────────────
    // Menu Item
    // ──────────────────────────────────────────────

    /// <summary>
    /// Auto-discover ONNX models and populate the NeuralModelDatabase.
    /// </summary>
    [MenuItem("Tools/Neural/Auto-Setup Model Database")]
    public static void AutoSetupModelDatabase()
    {
        // 1. Ensure the Resources/NeuralModels folder exists
        if (!AssetDatabase.IsValidFolder(ModelsResourcesPath))
        {
            Debug.LogWarning($"[NeuralModelAutoSetup] {ModelsResourcesPath} does not exist. Creating.");
            string parent = "Assets/Resources";
            if (!AssetDatabase.IsValidFolder(parent))
                AssetDatabase.CreateFolder("Assets", "Resources");
            AssetDatabase.CreateFolder(parent, "NeuralModels");
        }

        // 2. Find all .onnx files in the folder
        string[] onnxGuids = AssetDatabase.FindAssets("t:DefaultAsset", new[] { ModelsResourcesPath });
        var onnxFiles = onnxGuids
            .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
            .Where(path => path.EndsWith(".onnx") || path.EndsWith(".onnx.bytes"))
            .ToList();

        if (onnxFiles.Count == 0)
        {
            // Try searching by extension more broadly
            string[] allGuids = AssetDatabase.FindAssets("", new[] { ModelsResourcesPath });
            onnxFiles = allGuids
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Where(path => Path.GetExtension(path).ToLowerInvariant() == ".onnx" ||
                               path.EndsWith(".onnx.bytes"))
                .ToList();
        }

        if (onnxFiles.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Neural Model Auto-Setup",
                $"No .onnx files found in {ModelsResourcesPath}.\n\n" +
                "Place ONNX model files in Assets/Resources/NeuralModels/ and try again.",
                "OK");
            return;
        }

        Debug.Log($"[NeuralModelAutoSetup] Found {onnxFiles.Count} ONNX model(s).");

        // 3. Build policy entries
        var entries = new List<NeuralModelDatabase.PolicyEntry>();
        int matched = 0, unmatched = 0;

        foreach (string filePath in onnxFiles)
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            // Handle .onnx.bytes extension
            if (fileName.EndsWith(".onnx"))
                fileName = Path.GetFileNameWithoutExtension(fileName);

            // Resources-relative path (without extension)
            string resourcesPath = "NeuralModels/" + fileName;

            // Find matching prefix
            string prefix = FindMatchingPrefix(fileName);
            if (prefix == null)
            {
                Debug.LogWarning($"[NeuralModelAutoSetup] No known spec for '{fileName}'. " +
                    $"Skipping. Add entry to KnownSpecs in NeuralModelAutoSetup.cs if needed.");
                unmatched++;
                continue;
            }

            if (!PolicyTypeMap.TryGetValue(prefix, out var policyType))
            {
                Debug.LogWarning($"[NeuralModelAutoSetup] No PolicyType mapping for '{prefix}'. Skipping.");
                unmatched++;
                continue;
            }

            ModelSpec spec = KnownSpecs[prefix];
            var metadata = new PolicyMetadata
            {
                ModelVersion = spec.Version,
                AvatarType = spec.AvatarType,
                PolicyName = spec.PolicyName,
                ObservationSize = spec.ObservationSize,
                ActionSize = spec.ActionSize,
                JointCount = spec.JointCount,
                TerrainHeightmapResolution = spec.TerrainHeightmapResolution,
                Quantization = spec.Quantization,
                ModelPath = resourcesPath,
                ExpectedLatencyMs = spec.ExpectedLatencyMs,
                StyleEmbeddingSize = spec.StyleEmbeddingSize
            };

            entries.Add(new NeuralModelDatabase.PolicyEntry
            {
                policyType = policyType,
                modelPath = resourcesPath,
                metadata = metadata
            });

            Debug.Log($"[NeuralModelAutoSetup]  ✓ {policyType}: {resourcesPath} " +
                $"(obs={spec.ObservationSize}, act={spec.ActionSize}, joints={spec.JointCount})");
            matched++;
        }

        // 4. Create or load the database asset
        var db = AssetDatabase.LoadAssetAtPath<NeuralModelDatabase>(DatabaseAssetPath);
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<NeuralModelDatabase>();
            AssetDatabase.CreateAsset(db, DatabaseAssetPath);
            Debug.Log($"[NeuralModelAutoSetup] Created new database asset at {DatabaseAssetPath}");
        }

        db.SetPolicies(entries.ToArray());
        db.DefaultAvatarType = AvatarType.Humanoid;
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        // 5. Report
        string summary = $"Auto-Setup complete!\n\n" +
            $"  • Models found: {onnxFiles.Count}\n" +
            $"  • Matched: {matched}\n" +
            $"  • Skipped (unknown): {unmatched}\n" +
            $"  • Database: {DatabaseAssetPath}\n\n" +
            $"Entries:\n";

        foreach (var entry in entries)
        {
            summary += $"  [{entry.policyType}] {entry.modelPath}\n";
        }

        Debug.Log($"[NeuralModelAutoSetup] {summary.Replace("\n", " | ")}");
        EditorUtility.DisplayDialog("Neural Model Auto-Setup", summary, "OK");
    }

    /// <summary>
    /// Validate the existing database against files on disk.
    /// </summary>
    [MenuItem("Tools/Neural/Validate Model Database")]
    public static void ValidateModelDatabase()
    {
        var db = AssetDatabase.LoadAssetAtPath<NeuralModelDatabase>(DatabaseAssetPath);
        if (db == null)
        {
            EditorUtility.DisplayDialog("Validate Model Database",
                "No NeuralModelDatabase.asset found at:\n" + DatabaseAssetPath +
                "\n\nRun Tools/Neural/Auto-Setup Model Database first.", "OK");
            return;
        }

        db.Validate();

        int validCount = 0;
        foreach (var entry in db.Policies)
        {
            // Check the model file exists
            string fullPath = Path.Combine(Application.dataPath, "Resources", entry.modelPath);
            string fullPathWithBytes = fullPath + ".bytes";
            bool exists = File.Exists(fullPath) || File.Exists(fullPath + ".onnx") ||
                          File.Exists(fullPathWithBytes) ||
                          File.Exists(fullPath.Replace(".onnx", ".onnx.bytes"));

            string status = exists ? "✓" : "✗ MISSING";
            if (exists) validCount++;

            Debug.Log($"[Validate] [{status}] {entry.policyType}: {entry.modelPath} " +
                $"(obs={entry.metadata.ObservationSize}, act={entry.metadata.ActionSize})");
        }

        EditorUtility.DisplayDialog("Validate Model Database",
            $"Validation complete.\n\n  • Total entries: {db.Policies.Length}\n" +
            $"  • Valid files: {validCount}\n  • Missing files: {db.Policies.Length - validCount}\n\n" +
            "See Console for details.", "OK");
    }

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────

    /// <summary>
    /// Find the longest matching prefix key from KnownSpecs.
    /// </summary>
    private static string FindMatchingPrefix(string fileName)
    {
        // Sort by length descending so we match the most specific prefix first
        var prefixes = KnownSpecs.Keys.OrderByDescending(k => k.Length);
        foreach (string prefix in prefixes)
        {
            if (fileName.StartsWith(prefix))
                return prefix;
        }
        return null;
    }

    /// <summary>
    /// Internal model specification helper.
    /// </summary>
    private readonly struct ModelSpec
    {
        public readonly int ObservationSize;
        public readonly int ActionSize;
        public readonly int JointCount;
        public readonly AvatarType AvatarType;
        public readonly string PolicyName;
        public readonly string Version;
        public readonly QuantizationFormat Quantization;
        public readonly int TerrainHeightmapResolution;
        public readonly float ExpectedLatencyMs;
        public readonly int StyleEmbeddingSize;

        public ModelSpec(
            int observationSize,
            int actionSize,
            int jointCount,
            AvatarType avatarType,
            string policyName,
            string version,
            QuantizationFormat quantization,
            int terrainHeightmapResolution,
            float expectedLatencyMs,
            int styleEmbeddingSize)
        {
            ObservationSize = observationSize;
            ActionSize = actionSize;
            JointCount = jointCount;
            AvatarType = avatarType;
            PolicyName = policyName;
            Version = version;
            Quantization = quantization;
            TerrainHeightmapResolution = terrainHeightmapResolution;
            ExpectedLatencyMs = expectedLatencyMs;
            StyleEmbeddingSize = styleEmbeddingSize;
        }
    }
}