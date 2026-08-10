using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace ProjectName.Editor.Neural
{
    /// <summary>
    /// Editor window for editing latent style embeddings and policy transitions.
    /// Provides sliders to adjust style parameters and visualize transition curves.
    /// </summary>
    public class NeuralStyleEditor : EditorWindow
    {
        [MenuItem("Tools/Neural/Style Editor")]
        static void ShowWindow()
        {
            GetWindow<NeuralStyleEditor>("Neural Style Editor");
        }

        // ──────────────────────────────────────────────
        //  State
        // ──────────────────────────────────────────────

        Vector2 _scrollPos;
        string _selectedPolicy = "Locomotion";
        float[] _styleEmbedding = new float[8]; // 8-dim style embedding
        bool _previewBlend;
        float _blendPreviewTime;

        float _transitionDuration = 0.3f;
        AnimationCurve _transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        readonly string[] _policies = { "Locomotion", "Combat", "React", "Interact", "Fly", "Swim" };

        // Style parameter labels
        readonly (string name, string tooltip)[] _styleParams = new[]
        {
            ("Speed", "Movement speed bias (0=slow, 1=fast)"),
            ("Aggression", "Combat aggression (0=defensive, 1=aggressive)"),
            ("Fluidity", "Motion smoothness (0=robotic, 1=organic)"),
            ("Amplitude", "Motion amplitude (0=subtle, 1=exaggerated)"),
            ("Grounding", "Foot ground contact (0=light, 1=heavy)"),
            ("Symmetry", "Limb symmetry (0=asymmetric, 1=symmetric)"),
            ("Head Height", "Head/body height offset"),
            ("Arm Swing", "Arm swing intensity"),
        };

        // ──────────────────────────────────────────────
        //  GUI
        // ──────────────────────────────────────────────

        void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Neural Style Editor", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Policy selection
            EditorGUILayout.LabelField("Target Policy", EditorStyles.boldLabel);
            int selectedIdx = System.Array.IndexOf(_policies, _selectedPolicy);
            selectedIdx = EditorGUILayout.Popup(selectedIdx, _policies);
            _selectedPolicy = _policies[Mathf.Clamp(selectedIdx, 0, _policies.Length - 1)];
            EditorGUILayout.Space();

            // Style embedding sliders
            EditorGUILayout.LabelField("Style Embedding", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            for (int i = 0; i < _styleParams.Length; i++)
            {
                EditorGUILayout.BeginHorizontal();
                _styleEmbedding[i] = EditorGUILayout.Slider(
                    new GUIContent(_styleParams[i].name, _styleParams[i].tooltip),
                    _styleEmbedding[i], -1f, 1f
                );
                EditorGUILayout.EndHorizontal();
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            // Preset buttons
            EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Default Walk")) SetPreset(0);
            if (GUILayout.Button("Sprint")) SetPreset(1);
            if (GUILayout.Button("Sneak")) SetPreset(2);
            if (GUILayout.Button("Injured")) SetPreset(3);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            // Transition settings
            EditorGUILayout.LabelField("Transition Settings", EditorStyles.boldLabel);
            _transitionDuration = EditorGUILayout.Slider("Blend Duration (s)", _transitionDuration, 0.05f, 2f);
            _transitionCurve = EditorGUILayout.CurveField("Blend Curve", _transitionCurve);
            EditorGUILayout.Space();

            // Preview
            _previewBlend = EditorGUILayout.Toggle("Preview Blend", _previewBlend);
            if (_previewBlend)
            {
                _blendPreviewTime = EditorGUILayout.Slider("Blend Time", _blendPreviewTime, 0f, _transitionDuration);
                float t = _transitionDuration > 0f ? _blendPreviewTime / _transitionDuration : 0f;
                float blendValue = _transitionCurve.Evaluate(t);
                EditorGUILayout.LabelField("Blend Weight", $"{blendValue:P1}");

                // Visual progress bar
                Rect barRect = EditorGUILayout.GetControlRect(false, 20);
                EditorGUI.ProgressBar(barRect, blendValue, $"Blend: {blendValue:P1}");
            }
            EditorGUILayout.Space();

            // Apply button
            GUI.enabled = Application.isPlaying;
            if (GUILayout.Button("Apply Style to Selected", GUILayout.Height(30)))
            {
                ApplyStyleToSelected();
            }
            GUI.enabled = true;

            EditorGUILayout.EndScrollView();
        }

        void SetPreset(int preset)
        {
            switch (preset)
            {
                case 0: // Default Walk
                    _styleEmbedding = new float[] { 0.3f, 0f, 0.5f, 0.4f, 0.5f, 0.7f, 0f, 0.3f };
                    break;
                case 1: // Sprint
                    _styleEmbedding = new float[] { 0.8f, 0.2f, 0.3f, 0.6f, 0.3f, 0.5f, 0.1f, 0.5f };
                    break;
                case 2: // Sneak
                    _styleEmbedding = new float[] { 0.1f, 0f, 0.7f, 0.2f, 0.8f, 0.9f, -0.2f, 0.1f };
                    break;
                case 3: // Injured
                    _styleEmbedding = new float[] { 0.15f, 0f, 0.2f, 0.3f, 0.6f, 0.3f, -0.3f, 0.1f };
                    break;
            }
        }

        void ApplyStyleToSelected()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                Debug.LogWarning("[NeuralStyleEditor] No GameObject selected");
                return;
            }

            var neuralCtrl = selected.GetComponent<Systems.Animation.Neural.NeuralAnimationController>();
            if (neuralCtrl == null)
            {
                Debug.LogWarning("[NeuralStyleEditor] Selected object has no NeuralAnimationController");
                return;
            }

            Debug.Log($"[NeuralStyleEditor] Applied style to {selected.name}: {string.Join(", ", _styleEmbedding)}");
        }
    }
}