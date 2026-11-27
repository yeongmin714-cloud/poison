using UnityEngine;
using UnityEditor;
using TimeWarp.Modules.Paradox;

namespace TimeWarp.EditorScripts
{
    [CustomEditor(typeof(GhostEventReceiver))]
    public class GhostEventReceiverEditor : Editor
    {
        private SerializedProperty Bindings;

        private void OnEnable()
        {
            Bindings = serializedObject.FindProperty("Bindings");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(5);
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
            EditorGUILayout.LabelField("GHOST EVENT RECEIVER", headerStyle);
            EditorGUILayout.Space(10);

            for (int i = 0; i < Bindings.arraySize; i++)
            {
                SerializedProperty binding = Bindings.GetArrayElementAtIndex(i);
                DrawBinding(binding, i);
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("➕ Add New Event Binding", GUILayout.Width(200), GUILayout.Height(30)))
            {
                Bindings.arraySize++;
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawBinding(SerializedProperty binding, int index)
        {
            SerializedProperty EventName = binding.FindPropertyRelative("EventName");
            SerializedProperty ExpectedPayload = binding.FindPropertyRelative("ExpectedPayload");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(EventName, new GUIContent("Event Name"));
            
            // Red button for deleting the event binding
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                Bindings.DeleteArrayElementAtIndex(index);
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(ExpectedPayload, new GUIContent("Data Type (Payload)"));

            // ==========================================
            // CRITICAL UI FIX: Force serialization update
            // Ensures that when the user changes the Payload Type dropdown, 
            // the Inspector immediately updates to show the correct UnityEvent field.
            // ==========================================
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(5);

            // Magic: dynamically show ONLY the relevant UnityEvent field based on the selected payload type
            PayloadType currentType = (PayloadType)ExpectedPayload.enumValueIndex;

            switch (currentType)
            {
                case PayloadType.None:
                    EditorGUILayout.PropertyField(binding.FindPropertyRelative("OnEventTriggered"), new GUIContent("Trigger Action"));
                    break;
                case PayloadType.SingleFloat:
                    EditorGUILayout.PropertyField(binding.FindPropertyRelative("OnEventFloatTriggered"), new GUIContent("Trigger Action (Float)"));
                    break;
                case PayloadType.SingleInt:
                    EditorGUILayout.PropertyField(binding.FindPropertyRelative("OnEventIntTriggered"), new GUIContent("Trigger Action (Int)"));
                    break;
                case PayloadType.SingleVector:
                    EditorGUILayout.PropertyField(binding.FindPropertyRelative("OnEventVectorTriggered"), new GUIContent("Trigger Action (Vector3)"));
                    break;
                case PayloadType.FullPayload:
                    EditorGUILayout.PropertyField(binding.FindPropertyRelative("OnEventFullPayload"), new GUIContent("Trigger Action (Full Payload)"));
                    break;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }
    }
}