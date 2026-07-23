using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Text;

namespace ProjectName.Editor.Neural
{
    /// <summary>
    /// Editor window for inspecting Neural Animation policy models, their I/O specs,
    /// and runtime state of selected NeuralAnimationController instances.
    /// </summary>
    public class NeuralPolicyInspector : EditorWindow
    {
        [MenuItem("Tools/Neural/Policy Inspector")]
        static void ShowWindow()
        {
            GetWindow<NeuralPolicyInspector>("Neural Policy Inspector");
        }

        // ──────────────────────────────────────────────
        //  State
        // ──────────────────────────────────────────────

        Vector2 _scrollPos;
        bool _showModelInfo = true;
        bool _showRuntimeInfo = true;
        bool _showMetrics = true;
        bool _showLODInfo = true;

        GUIStyle _headerStyle;
        GUIStyle _valueStyle;
        Color _goodColor = Color.green;
        Color _warnColor = Color.yellow;
        Color _badColor = Color.red;

        // ──────────────────────────────────────────────
        //  GUI
        // ──────────────────────────────────────────────

        void OnGUI()
        {
            InitializeStyles();

            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorGUILayout.HelpBox("Select a GameObject with NeuralAnimationController or HybridAnimationController", MessageType.Info);
                return;
            }

            var neuralCtrl = selected.GetComponent<Systems.Animation.Neural.NeuralAnimationController>();
            var hybridCtrl = selected.GetComponent<Systems.Animation.Neural.HybridAnimationController>();

            if (neuralCtrl == null && hybridCtrl == null)
            {
                EditorGUILayout.HelpBox("Selected object has no NeuralAnimationController or HybridAnimationController", MessageType.Warning);
                return;
            }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawHeader($"Neural Animation Inspector — {selected.name}");

            if (hybridCtrl != null)
                DrawHybridInfo(hybridCtrl);

            if (neuralCtrl != null)
                DrawNeuralInfo(neuralCtrl);

            DrawModelInventory();

            EditorGUILayout.EndScrollView();
        }

        void InitializeStyles()
        {
            if (_headerStyle != null) return;
            _headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            _valueStyle = new GUIStyle(EditorStyles.label) { wordWrap = true };
        }

        void DrawHeader(string text)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(text, _headerStyle);
            EditorGUILayout.Space();
        }

        void DrawHybridInfo(Systems.Animation.Neural.HybridAnimationController ctrl)
        {
            _showRuntimeInfo = EditorGUILayout.Foldout(_showRuntimeInfo, "Hybrid Controller State", true);
            if (!_showRuntimeInfo) return;

            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Procedural Weight", $"{ctrl.GetType().GetField("_baseProceduralWeight", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(ctrl):P1}");
            EditorGUILayout.LabelField("Neural Weight", $"{ctrl.GetType().GetField("_baseNeuralWeight", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(ctrl):P1}");
            EditorGUILayout.LabelField("LOD Level", $"{ctrl.CurrentLODLevel}");
            EditorGUILayout.LabelField("Neural Active", $"{ctrl.IsNeuralActive}");
            EditorGUILayout.LabelField("Distance to Camera", $"{ctrl.GetType().GetField("_distanceToCamera", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(ctrl):F1}m");
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();
        }

        void DrawNeuralInfo(Systems.Animation.Neural.NeuralAnimationController ctrl)
        {
            _showModelInfo = EditorGUILayout.Foldout(_showModelInfo, "Neural Controller State", true);
            if (!_showModelInfo) return;

            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Active Policy", $"{ctrl.ActivePolicy}");
            EditorGUILayout.LabelField("Current LOD", $"{ctrl.CurrentLODLevel}");
            EditorGUILayout.LabelField("Observation Dim", $"{ctrl.GetType().GetField("_observationDim", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(ctrl)}");
            EditorGUILayout.LabelField("Action Dim", $"{ctrl.GetType().GetField("_actionDim", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(ctrl)}");
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            // Metrics
            var metrics = selected.GetComponent<Systems.Animation.Neural.Evaluation.NeuralAnimationMetrics>();
            if (metrics != null)
            {
                _showMetrics = EditorGUILayout.Foldout(_showMetrics, "Runtime Metrics", true);
                if (_showMetrics)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("FPS", $"{metrics.CurrentFPS:F1}");
                    EditorGUILayout.LabelField("Inference Latency", $"{metrics.AverageInferenceLatencyMs:F3}ms", metrics.AverageInferenceLatencyMs > 5f ? _warnStyle : _normalStyle);
                    EditorGUILayout.LabelField("Policy Switches", $"{metrics.TotalPolicySwitches}");
                    if (GUILayout.Button("Reset Metrics"))
                        metrics.ResetMetrics();
                    EditorGUI.indentLevel--;
                    EditorGUILayout.Space();
                }
            }
        }

        void DrawModelInventory()
        {
            _showLODInfo = EditorGUILayout.Foldout(_showLODInfo, "ONNX Model Inventory", true);
            if (!_showLODInfo) return;

            string[] modelGuids = AssetDatabase.FindAssets("t:ModelAsset", new[] { "Assets/Resources/NeuralModels" });
            EditorGUILayout.LabelField($"Models Found: {modelGuids.Length}");

            if (modelGuids.Length > 0)
            {
                EditorGUI.indentLevel++;
                foreach (var guid in modelGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var model = AssetDatabase.LoadAssetAtPath<ModelAsset>(path);
                    if (model != null && model.OnnxModel != null)
                    {
                        float sizeKb = model.OnnxModel.bytes.Length / 1024f;
                        EditorGUILayout.LabelField(Path.GetFileName(path), $"{sizeKb:F1} KB");
                    }
                }
                EditorGUI.indentLevel--;
            }
        }

        GUIStyle _normalStyle;
        GUIStyle _warnStyle;

        void OnEnable()
        {
            _normalStyle = new GUIStyle(EditorStyles.label);
            _warnStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = Color.yellow } };
        }
    }
}