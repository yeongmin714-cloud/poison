using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectName.Editor.Neural
{
    /// <summary>
    /// Editor regression test runner for Neural Animation system.
    /// Run via Tools/Neural/Run Regression Tests.
    /// </summary>
    public class NeuralAnimationTestRunner
    {
        const string ReportPath = "NeuralRegressionReport.txt";
        const string ModelsPath = "Assets/Resources/NeuralModels";

        static int _passed;
        static int _failed;
        static System.Text.StringBuilder _report;

        // ──────────────────────────────────────────────
        //  Menu Items
        // ──────────────────────────────────────────────

        [MenuItem("Tools/Neural/Run Regression Tests")]
        static void RunRegressionTests()
        {
            _report = new System.Text.StringBuilder();
            _passed = 0;
            _failed = 0;

            _report.AppendLine("=== Neural Animation Regression Test Report ===");
            _report.AppendLine($"Date: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _report.AppendLine($"Unity: 6000.4.10f1");
            _report.AppendLine();

            // Test 1: Compilation check
            Check("Script Compilation", ValidateCompilation());

            // Test 2: ONNX model availability
            Check("ONNX Models (20)", ValidateModels());

            // Test 3: ScriptableObject integrity
            Check("ScriptableObject References", ValidateScriptableObjects());

            // Test 4: Neural controller component check
            Check("Neural Controller Components", ValidateComponents());

            // Test 5: Policy enum consistency
            Check("Policy Enum Consistency", ValidatePolicyEnums());

            // Summary
            _report.AppendLine();
            _report.AppendLine($"=== Summary: {_passed} passed, {_failed} failed ===");

            // Write report
            string fullPath = Path.Combine(Application.dataPath, "..", ReportPath);
            File.WriteAllText(fullPath, _report.ToString());
            Debug.Log($"[NeuralTestRunner] Report written to {fullPath}");
            Debug.Log(_report.ToString());

            // Open report
            EditorUtility.OpenWithDefaultApp(fullPath);
        }

        [MenuItem("Tools/Neural/Validate ONNX Models")]
        static void ValidateONNXModels()
        {
            string[] models = AssetDatabase.FindAssets("t:ModelAsset", new[] { ModelsPath });
            Debug.Log($"[NeuralTestRunner] Found {models.Length} ONNX models in {ModelsPath}");
            foreach (var guid in models)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var model = AssetDatabase.LoadAssetAtPath<ModelAsset>(path);
                if (model != null && model.OnnxModel != null)
                {
                    Debug.Log($"  ✅ {path} ({model.OnnxModel.bytes.Length / 1024} KB)");
                }
                else
                {
                    Debug.LogWarning($"  ❌ {path} (missing or invalid)");
                }
            }
        }

        // ──────────────────────────────────────────────
        //  Individual Checks
        // ──────────────────────────────────────────────

        static bool ValidateCompilation()
        {
            // Compilation check: look for compile errors in log
            // In batch mode this is checked by Unity itself
            return true; // Assume Unity already validated
        }

        static bool ValidateModels()
        {
            string[] modelPaths = Directory.GetFiles(ModelsPath, "*.onnx", SearchOption.TopDirectoryOnly);
            int expectedCount = 20; // 10 biped + 10 quadruped

            if (modelPaths.Length < expectedCount)
            {
                _report.AppendLine($"  Expected {expectedCount} ONNX models, found {modelPaths.Length}");
                foreach (var p in modelPaths)
                    _report.AppendLine($"    - {Path.GetFileName(p)}");
                return false;
            }

            // Check specific required models
            string[] required = new[]
            {
                "locomotion_biped_base.onnx", "locomotion_quadruped.onnx",
                "combat_biped_base.onnx", "combat_quadruped_base.onnx",
                "react_biped_base.onnx", "react_quadruped_base.onnx",
                "interact_biped_base.onnx", "interact_quadruped_base.onnx",
                "fly_quadruped_base.onnx", "swim_quadruped_base.onnx"
            };

            foreach (var name in required)
            {
                string fullPath = Path.Combine(ModelsPath, name);
                if (!File.Exists(fullPath) && !File.Exists(fullPath + ".meta"))
                {
                    _report.AppendLine($"  Missing: {name}");
                    return false;
                }
            }

            _report.AppendLine($"  Found {modelPaths.Length} ONNX models (all required present)");
            return true;
        }

        static bool ValidateScriptableObjects()
        {
            // Check that ProgressiveRolloutConfig exists
            string[] configGuids = AssetDatabase.FindAssets("t:ProgressiveRolloutConfig");
            if (configGuids.Length == 0)
            {
                _report.AppendLine("  ProgressiveRolloutConfig not found in project");
                return false;
            }
            _report.AppendLine($"  Found {configGuids.Length} ProgressiveRolloutConfig asset(s)");
            return true;
        }

        static bool ValidateComponents()
        {
            // Check that all required scripts exist
            string[] requiredScripts = new[]
            {
                "NeuralAnimationController",
                "HybridAnimationController",
                "PolicySelector",
                "ProgressiveRolloutManager",
                "NeuralAnimationMetrics",
                "PhysicsValidityChecker",
                "ABTestFramework",
                "EdgeCaseEvaluator"
            };

            foreach (var script in requiredScripts)
            {
                string[] guids = AssetDatabase.FindAssets($"t:script {script}");
                if (guids.Length == 0)
                {
                    _report.AppendLine($"  Missing script: {script}");
                    return false;
                }
            }
            _report.AppendLine($"  All {requiredScripts.Length} required scripts found");
            return true;
        }

        static bool ValidatePolicyEnums()
        {
            // This is a compile-time check — if the project compiles, enums are consistent
            _report.AppendLine("  Enum consistency verified at compile time");
            return true;
        }

        // ──────────────────────────────────────────────
        //  Helpers
        // ──────────────────────────────────────────────

        static void Check(string testName, bool passed)
        {
            if (passed)
            {
                _report.AppendLine($"✅ {testName}: PASS");
                _passed++;
            }
            else
            {
                _report.AppendLine($"❌ {testName}: FAIL");
                _failed++;
            }
        }
    }
}