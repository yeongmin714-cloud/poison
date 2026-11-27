using UnityEditor;
using UnityEngine;

namespace TimeWarp.EditorTools
{
    /// <summary>
    /// Shared look-and-feel for every TimeWarp editor surface.
    ///
    /// Centralised for one reason: the manual boxes are only useful if they behave
    /// identically everywhere and remember whether you closed them. A beginner wants them
    /// open; someone on their fifth object wants them shut and staying shut. Each box
    /// keeps its own state in EditorPrefs, keyed by name, so closing "Zones - what are
    /// they?" does not also close the manual on the Character tab.
    /// </summary>
    public static class TimeWarpEditorUI
    {
        private const string PrefPrefix = "TimeWarp.Manual.";

        // --- Palette ---
        public static readonly Color AccentBlue   = new Color(0.35f, 0.72f, 1.00f);
        public static readonly Color AccentViolet = new Color(0.78f, 0.45f, 1.00f);
        public static readonly Color AccentGreen  = new Color(0.45f, 0.85f, 0.55f);
        public static readonly Color AccentAmber  = new Color(1.00f, 0.72f, 0.30f);

        /// <summary>
        /// Collapsible "user manual" block. Open by default the first time it is seen.
        /// </summary>
        public static void Manual(string key, string title, string body, string tip = null)
        {
            string prefKey = PrefPrefix + key;
            bool open = EditorPrefs.GetBool(prefKey, true);

            var headerStyle = new GUIStyle(EditorStyles.foldout)
            {
                fontStyle = FontStyle.Bold
            };

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            bool newOpen = EditorGUILayout.Foldout(open, "  " + title, true, headerStyle);
            if (newOpen != open) EditorPrefs.SetBool(prefKey, newOpen);

            if (newOpen)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField(body, EditorStyles.wordWrappedLabel);

                if (!string.IsNullOrEmpty(tip))
                {
                    EditorGUILayout.Space(3);

                    var tipStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
                    {
                        fontStyle = FontStyle.Italic
                    };
                    EditorGUILayout.LabelField("Tip: " + tip, tipStyle);
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(3);
        }

        /// <summary>Big coloured page title.</summary>
        public static void PageTitle(string title, string subtitle, Color accent)
        {
            EditorGUILayout.Space(4);

            var style = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 };
            Color previous = GUI.contentColor;
            GUI.contentColor = accent;
            EditorGUILayout.LabelField(title, style);
            GUI.contentColor = previous;

            if (!string.IsNullOrEmpty(subtitle))
                EditorGUILayout.LabelField(subtitle, EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(6);
        }

        /// <summary>Boxed section with a bold header.</summary>
        public static void BeginSection(string title)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (!string.IsNullOrEmpty(title))
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        public static void EndSection()
        {
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(3);
        }

        /// <summary>Coloured action button.</summary>
        public static bool ActionButton(string label, Color color, float height = 26f)
        {
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = color;
            bool clicked = GUILayout.Button(label, GUILayout.Height(height));
            GUI.backgroundColor = previous;
            return clicked;
        }

        /// <summary>One line of explanatory small print.</summary>
        public static void Note(string text)
        {
            EditorGUILayout.LabelField(text, EditorStyles.wordWrappedMiniLabel);
        }

        /// <summary>Labelled bullet, used for "what this will do" previews.</summary>
        public static void Bullet(string text)
        {
            EditorGUILayout.LabelField("  •  " + text, EditorStyles.wordWrappedMiniLabel);
        }
    }
}
