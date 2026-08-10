using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace ProjectName.Editor.Neural
{
    /// <summary>
    /// Editor window for designing and previewing policy transitions.
    /// Configure blend duration, curves, and fallback policies.
    /// </summary>
    public class NeuralTransitionDesigner : EditorWindow
    {
        [MenuItem("Tools/Neural/Transition Designer")]
        static void ShowWindow()
        {
            GetWindow<NeuralTransitionDesigner>("Neural Transition Designer");
        }

        // ──────────────────────────────────────────────
        //  State
        // ──────────────────────────────────────────────

        Vector2 _scrollPos;

        // Source → Target transition
        string _sourcePolicy = "Locomotion";
        string _targetPolicy = "Combat";
        float _blendDuration = 0.3f;
        AnimationCurve _blendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        bool _useLatentBlend = true;
        float _transitionCooldown = 0.5f;
        string _fallbackPolicy = "Locomotion";

        // Preview
        bool _showPreview;
        float _previewTime;

        // Test duration
        float _testDuration = 3f;

        readonly string[] _policies = { "Locomotion", "Combat", "React", "Interact", "Fly", "Swim" };

        // Transition matrix (source × target)
        Dictionary<(string, string), float> _transitionMatrix = new Dictionary<(string, string), float>();

        // ──────────────────────────────────────────────
        //  GUI
        // ──────────────────────────────────────────────

        void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Neural Transition Designer", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Source policy
            EditorGUILayout.LabelField("Transition Configuration", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            int srcIdx = System.Array.IndexOf(_policies, _sourcePolicy);
            srcIdx = EditorGUILayout.Popup("Source Policy", Mathf.Max(0, srcIdx), _policies);
            _sourcePolicy = _policies[Mathf.Clamp(srcIdx, 0, _policies.Length - 1)];

            int tgtIdx = System.Array.IndexOf(_policies, _targetPolicy);
            tgtIdx = EditorGUILayout.Popup("Target Policy", Mathf.Max(0, tgtIdx), _policies);
            _targetPolicy = _policies[Mathf.Clamp(tgtIdx, 0, _policies.Length - 1)];

            _blendDuration = EditorGUILayout.Slider("Blend Duration (s)", _blendDuration, 0.05f, 2f);
            _blendCurve = EditorGUILayout.CurveField("Blend Curve", _blendCurve);
            _useLatentBlend = EditorGUILayout.Toggle("Use Latent Blend", _useLatentBlend);
            _transitionCooldown = EditorGUILayout.Slider("Transition Cooldown (s)", _transitionCooldown, 0f, 3f);

            int fallbackIdx = System.Array.IndexOf(_policies, _fallbackPolicy);
            fallbackIdx = EditorGUILayout.Popup("Fallback Policy", Mathf.Max(0, fallbackIdx), _policies);
            _fallbackPolicy = _policies[Mathf.Clamp(fallbackIdx, 0, _policies.Length - 1)];

            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            // Preview
            _showPreview = EditorGUILayout.Foldout(_showPreview, "Blend Preview", true);
            if (_showPreview)
            {
                EditorGUI.indentLevel++;
                _previewTime = EditorGUILayout.Slider("Time (s)", _previewTime, 0f, _blendDuration);
                float t = _blendDuration > 0f ? _previewTime / _blendDuration : 0f;
                float blendValue = _blendCurve.Evaluate(t);

                Rect barRect = EditorGUILayout.GetControlRect(false, 25);
                EditorGUI.ProgressBar(barRect, blendValue, $"Blend: {blendValue:P1}");

                EditorGUILayout.LabelField("Source Weight", $"{1f - blendValue:P1}");
                EditorGUILayout.LabelField("Target Weight", $"{blendValue:P1}");
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.Space();

            // Action buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create TransitionConfig Preset", GUILayout.Height(25)))
            {
                CreatePreset();
            }
            if (GUILayout.Button("Reset to Defaults", GUILayout.Height(25)))
            {
                _blendDuration = 0.3f;
                _blendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
                _useLatentBlend = true;
                _transitionCooldown = 0.5f;
                _fallbackPolicy = "Locomotion";
            }
            EditorGUILayout.EndHorizontal();

            // Apply to selected
            GUI.enabled = Application.isPlaying;
            if (GUILayout.Button("Test Transition on Selected", GUILayout.Height(30)))
            {
                TestTransition();
            }
            GUI.enabled = true;

            EditorGUILayout.Space();

            // Transition matrix
            DrawTransitionMatrix();

            EditorGUILayout.EndScrollView();
        }

        void DrawTransitionMatrix()
        {
            EditorGUILayout.LabelField("Transition Matrix (saved durations)", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Header row
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("From \\ To", GUILayout.Width(80));
            foreach (var p in _policies)
                EditorGUILayout.LabelField(p, GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();

            // Data rows
            foreach (var src in _policies)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(src, GUILayout.Width(80));
                foreach (var tgt in _policies)
                {
                    float val = GetTransitionDuration(src, tgt);
                    if (src == tgt)
                    {
                        EditorGUILayout.LabelField("-", GUILayout.Width(70));
                    }
                    else
                    {
                        float newVal = EditorGUILayout.FloatField(val, GUILayout.Width(70));
                        if (newVal != val)
                            _transitionMatrix[(src, tgt)] = Mathf.Clamp(newVal, 0.05f, 2f);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        float GetTransitionDuration(string src, string tgt)
        {
            if (_transitionMatrix.TryGetValue((src, tgt), out float val))
                return val;
            return _blendDuration;
        }

        void CreatePreset()
        {
            var preset = new Systems.Animation.Neural.PolicySelector.TransitionConfig
            {
                blendDuration = _blendDuration,
                useLatentBlend = _useLatentBlend,
                transitionCooldown = _transitionCooldown,
            };

            // Convert fallback string to AnimationPolicy
            var policyType = Systems.Animation.Neural.PolicySelector.PolicyTypeFromAnimationPolicy(
                Systems.Animation.Neural.PolicySelector.AnimationPolicyFromPolicyType(
                    Systems.Animation.Neural.NeuralAnimationController.PolicyType.Locomotion));

            Debug.Log($"[NeuralTransitionDesigner] Transition config created: {_blendDuration}s, latent={_useLatentBlend}, cooldown={_transitionCooldown}");
        }

        void TestTransition()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                Debug.LogWarning("[NeuralTransitionDesigner] No GameObject selected");
                return;
            }

            var neuralCtrl = selected.GetComponent<Systems.Animation.Neural.NeuralAnimationController>();
            if (neuralCtrl == null)
            {
                Debug.LogWarning("[NeuralTransitionDesigner] Selected object has no NeuralAnimationController");
                return;
            }

            // Parse target policy
            var policyMap = new Dictionary<string, Systems.Animation.Neural.NeuralAnimationController.PolicyType>
            {
                ["Locomotion"] = Systems.Animation.Neural.NeuralAnimationController.PolicyType.Locomotion,
                ["Combat"] = Systems.Animation.Neural.NeuralAnimationController.PolicyType.Combat,
                ["React"] = Systems.Animation.Neural.NeuralAnimationController.PolicyType.React,
                ["Interact"] = Systems.Animation.Neural.NeuralAnimationController.PolicyType.Interact,
                ["Fly"] = Systems.Animation.Neural.NeuralAnimationController.PolicyType.Fly,
                ["Swim"] = Systems.Animation.Neural.NeuralAnimationController.PolicyType.Swim,
            };

            if (policyMap.TryGetValue(_targetPolicy, out var policy))
            {
                neuralCtrl.SwitchPolicy(policy);
                Debug.Log($"[NeuralTransitionDesigner] Testing transition: {_sourcePolicy} → {_targetPolicy} ({_blendDuration}s)");
            }
        }
    }
}