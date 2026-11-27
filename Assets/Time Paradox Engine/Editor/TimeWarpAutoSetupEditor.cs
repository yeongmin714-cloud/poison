using UnityEngine;
using UnityEditor;
using TimeWarp.Modules.Physics; 
using TimeWarp.Core; 
using TimeWarp.Core.Data;

namespace TimeWarp.EditorScripts
{
    [CustomEditor(typeof(TimeWarpAutoSetup))]
    public class TimeWarpAutoSetupEditor : Editor
    {
        private SerializedProperty ScanSceneOnAwake;
        private SerializedProperty Rules;
        
        private Vector2 _scrollPosition;

        private void OnEnable()
        {
            ScanSceneOnAwake = serializedObject.FindProperty("ScanSceneOnAwake");
            Rules = serializedObject.FindProperty("Rules");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("TIME WARP AUTO SETUP", headerStyle);
            EditorGUILayout.Space(10);

            EditorGUILayout.PropertyField(ScanSceneOnAwake, new GUIContent("Scan Scene On Awake"));
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Injection Rules", EditorStyles.boldLabel);

            int ruleToRemove = -1; 

            for (int i = 0; i < Rules.arraySize; i++)
            {
                SerializedProperty rule = Rules.GetArrayElementAtIndex(i);
                if (DrawRule(rule, i)) 
                {
                    ruleToRemove = i;
                }
                EditorGUILayout.Space(5);
            }

            if (ruleToRemove != -1)
            {
                Rules.DeleteArrayElementAtIndex(ruleToRemove);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("➕ Add New Rule", GUILayout.Height(25)))
            {
                Rules.arraySize++;
            }
            if (Rules.arraySize > 0 && GUILayout.Button("➖ Remove Last", GUILayout.Height(25)))
            {
                Rules.arraySize--;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(15);

            // Hide the Bake and Clear buttons while the game is running
            if (!Application.isPlaying)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("🔨 Bake Rules to Scene", GUILayout.Height(30)))
                {
                    var method = typeof(TimeWarpAutoSetup).GetMethod("BakeRulesInEditor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (method != null) method.Invoke(target, null);
                }
                
                if (GUILayout.Button("🧹 Clear Baked Rules", GUILayout.Height(30)))
                {
                    var method = typeof(TimeWarpAutoSetup).GetMethod("ClearBakedRulesInEditor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (method != null) method.Invoke(target, null);
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            serializedObject.ApplyModifiedProperties();
        }

        private bool DrawRule(SerializedProperty ruleProperty, int index)
        {
            bool deleteRequested = false;
            SerializedProperty RuleName = ruleProperty.FindPropertyRelative("RuleName");
            SerializedProperty Enabled = ruleProperty.FindPropertyRelative("Enabled");

            EditorGUILayout.BeginVertical("helpbox");
            
            Rect rect = EditorGUILayout.GetControlRect();
            ruleProperty.isExpanded = EditorGUI.Foldout(rect, ruleProperty.isExpanded, $"{RuleName.stringValue} (Rule {index + 1})", true, EditorStyles.foldoutHeader);

            if (ruleProperty.isExpanded)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("📂 Load Preset", EditorStyles.miniButtonLeft))
                {
                    LoadPreset(index);
                }
                if (GUILayout.Button("💾 Save Preset", EditorStyles.miniButtonMid))
                {
                    SavePreset(index, RuleName.stringValue);
                }
                
                // Red button for rule removal
                GUI.backgroundColor = new Color(1f, 0.4f, 0.4f); 
                if (GUILayout.Button("✖ Remove", EditorStyles.miniButtonRight, GUILayout.Width(70)))
                {
                    deleteRequested = true;
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(5);

                EditorGUILayout.PropertyField(RuleName);
                EditorGUILayout.PropertyField(Enabled);

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Targeting", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(ruleProperty.FindPropertyRelative("TargetLayer"));
                EditorGUILayout.PropertyField(ruleProperty.FindPropertyRelative("TargetTags"), true);
                EditorGUILayout.PropertyField(ruleProperty.FindPropertyRelative("ExplicitObjects"), true);

                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Action Filters & Zones", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(ruleProperty.FindPropertyRelative("ActionFilters"), true);

                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Core Settings", EditorStyles.boldLabel);
                SerializedProperty ActiveChannels = ruleProperty.FindPropertyRelative("ActiveChannels");
                EditorGUILayout.PropertyField(ActiveChannels);
                
                // ==========================================
                // CRITICAL UI FIX: Force serialization update
                // This ensures the Inspector immediately shows/hides the channel settings 
                // below right after the user toggles a bitmask flag in the dropdown.
                // ==========================================
                serializedObject.ApplyModifiedProperties();

                EditorGUILayout.PropertyField(ruleProperty.FindPropertyRelative("DefaultLocalTimeScale"));
                EditorGUILayout.PropertyField(ruleProperty.FindPropertyRelative("CustomRecordTime"));
                
                EditorGUILayout.Space(5);
                EditorGUILayout.PropertyField(ruleProperty.FindPropertyRelative("FreeMemoryOnDisable"), new GUIContent("Free Native Memory On Disable (Pooled Objects)"));

                // Read the freshly updated value
                int channels = ActiveChannels.intValue;
                bool hasTransform = (channels & (int)TimeChannels.Transform) != 0;
                bool hasVelocity = (channels & (int)TimeChannels.Velocity) != 0;
                bool hasVelocity2D = (channels & (int)TimeChannels.Velocity2D) != 0;
                bool hasCustom = (channels & (int)TimeChannels.Custom) != 0;
                bool hasParticles = (channels & (int)TimeChannels.Particles) != 0;
                bool hasAudio = (channels & (int)TimeChannels.Audio) != 0;
                bool hasAnimation = (channels & (int)TimeChannels.Animation) != 0;
                bool hasShaders = (channels & (int)TimeChannels.Shaders) != 0;
                bool hasNavigation = (channels & (int)TimeChannels.Navigation) != 0;
                bool hasEchoes = (channels & (int)TimeChannels.Echoes) != 0;

                if (hasTransform || hasVelocity || hasVelocity2D || hasCustom || hasParticles || hasAudio || hasNavigation || hasEchoes)
                {
                    EditorGUILayout.Space(10);
                    EditorGUILayout.LabelField("Channel Specific Settings", EditorStyles.boldLabel);

                    if (hasTransform) 
                    {
                        EditorGUILayout.PropertyField(ruleProperty.FindPropertyRelative("TransformSettings"), true);
                    }
                    if (hasVelocity) 
                    {
                        EditorGUILayout.PropertyField(ruleProperty.FindPropertyRelative("VelocitySettings"), new GUIContent("Velocity (3D) Settings"), true);
                    }
                    if (hasVelocity2D) 
                    {
                        EditorGUILayout.PropertyField(ruleProperty.FindPropertyRelative("Velocity2DSettings"), new GUIContent("Velocity (2D) Settings"), true);
                    }
                    if (hasCustom) 
                    {
                        EditorGUILayout.PropertyField(ruleProperty.FindPropertyRelative("CustomSettings"), true);
                    }
                    
                    if (hasParticles) 
                    {
                        SerializedProperty particleSettingsProp = ruleProperty.FindPropertyRelative("ParticleSettings");
                        if (particleSettingsProp != null)
                        {
                            EditorGUILayout.PropertyField(particleSettingsProp, new GUIContent("Shuriken (Particle) Settings"), true);
                        }

                        SerializedProperty vfxSettingsProp = ruleProperty.FindPropertyRelative("VfxSettings");
                        if (vfxSettingsProp != null)
                        {
                            EditorGUILayout.PropertyField(vfxSettingsProp, new GUIContent("VFX Graph Settings"), true);
                        }
                        else
                        {
                            EditorGUILayout.HelpBox("To see VFX Graph settings, ensure you added 'public VfxChannelSettings VfxSettings = new VfxChannelSettings();' in TimeWarpRule.cs", MessageType.Warning);
                        }
                    }

                    if (hasAudio) 
                    {
                        EditorGUILayout.PropertyField(ruleProperty.FindPropertyRelative("AudioSettings"), true);
                    }
                    if (hasNavigation) 
                    {
                        EditorGUILayout.PropertyField(ruleProperty.FindPropertyRelative("NavigationSettings"), true);
                    }
                    if (hasEchoes) 
                    {
                        EditorGUILayout.PropertyField(ruleProperty.FindPropertyRelative("EchoSettings"), new GUIContent("Paradox (Echoes) Settings"), true);
                    }
                }

                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Injection Flags (Auto-Add Modules)", EditorStyles.boldLabel);
                
                EditorGUILayout.PropertyField(ruleProperty.FindPropertyRelative("InjectTimeBody"));
                EditorGUILayout.PropertyField(ruleProperty.FindPropertyRelative("InjectKineticAccumulator"));
                
                if (hasParticles)
                {
                    SerializedProperty injectParticlesProp = ruleProperty.FindPropertyRelative("InjectTimeParticles");
                    if (injectParticlesProp != null) EditorGUILayout.PropertyField(injectParticlesProp);
                    
                    SerializedProperty injectVfxProp = ruleProperty.FindPropertyRelative("InjectTimeVFX");
                    if (injectVfxProp != null) EditorGUILayout.PropertyField(injectVfxProp);
                }

                if (hasShaders)
                {
                    EditorGUILayout.PropertyField(ruleProperty.FindPropertyRelative("InjectTimeShader"));
                    if (ruleProperty.FindPropertyRelative("InjectTimeShader").boolValue)
                    {
                        EditorGUILayout.PropertyField(ruleProperty.FindPropertyRelative("ShaderProperties"), true);
                    }
                }

                if (hasAnimation) 
                {
                    SerializedProperty injectAnimProp = ruleProperty.FindPropertyRelative("InjectTimeAnimator");
                    EditorGUILayout.PropertyField(injectAnimProp);
                    
                    if (injectAnimProp.boolValue)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(ruleProperty.FindPropertyRelative("AnimatorMode"), new GUIContent("Animator Record Mode"));
                        EditorGUILayout.PropertyField(ruleProperty.FindPropertyRelative("AnimationFramerate"), new GUIContent("Custom Record Framerate"));
                        EditorGUILayout.PropertyField(ruleProperty.FindPropertyRelative("HandleRootMotion"));
                        EditorGUI.indentLevel--;
                    }
                }

                if (hasAudio) EditorGUILayout.PropertyField(ruleProperty.FindPropertyRelative("InjectTimeAudio"));
                if (hasNavigation) EditorGUILayout.PropertyField(ruleProperty.FindPropertyRelative("InjectTimeNavMeshAgent")); 
                
                EditorGUILayout.PropertyField(ruleProperty.FindPropertyRelative("InjectTimeCinemachine"), new GUIContent("Inject Time Cinemachine"));

                if (hasEchoes) 
                {
                    EditorGUILayout.PropertyField(ruleProperty.FindPropertyRelative("InjectTimeParadoxModule"), new GUIContent("Inject Time Paradox Module")); 
                    
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(ruleProperty.FindPropertyRelative("InjectTimeEventRecorder"), new GUIContent("Inject Event Recorder"));
                    EditorGUI.indentLevel--;
                }

                if (hasTransform || hasVelocity)
                {
                    EditorGUILayout.PropertyField(ruleProperty.FindPropertyRelative("InjectTimeHingeJoint"), new GUIContent("Inject Time Hinge Joint"));
                    EditorGUILayout.Space(5);
                    EditorGUILayout.PropertyField(ruleProperty.FindPropertyRelative("ControllerAdapter"), new GUIContent("Controller Adapter Type"));
                }
                
                EditorGUILayout.Space(5);
                SerializedProperty injectLifecycle = ruleProperty.FindPropertyRelative("InjectTimeLifecycle");
                EditorGUILayout.PropertyField(injectLifecycle, new GUIContent("Inject Time Lifecycle"));
                
                if (injectLifecycle.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(ruleProperty.FindPropertyRelative("LifecycleKeepInMemory"), new GUIContent("Keep In Memory For Rewind"));
                    EditorGUILayout.PropertyField(ruleProperty.FindPropertyRelative("LifecycleDestroyBeforeBirth"), new GUIContent("Destroy Before Birth"));
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Component Manager (Kill Switch)", EditorStyles.boldLabel);
                SerializedProperty injectManager = ruleProperty.FindPropertyRelative("InjectComponentManager");
                EditorGUILayout.PropertyField(injectManager);

                if (injectManager.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(ruleProperty.FindPropertyRelative("DisableOnRewind"));
                    EditorGUILayout.PropertyField(ruleProperty.FindPropertyRelative("DisableOnTimeStop"));
                    EditorGUILayout.PropertyField(ruleProperty.FindPropertyRelative("AutoDetectForeignScripts"));
                    EditorGUILayout.PropertyField(ruleProperty.FindPropertyRelative("SpecificComponentsToDisable"), true);
                    EditorGUI.indentLevel--;
                }

                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }

            EditorGUILayout.EndVertical();
            return deleteRequested;
        }

        private void SavePreset(int index, string defaultName)
        {
            serializedObject.ApplyModifiedProperties();

            string path = EditorUtility.SaveFilePanelInProject("Save Rule Preset", defaultName + "_Preset", "asset", "Save your rule configuration");
            
            if (!string.IsNullOrEmpty(path))
            {
                TimeWarpAutoSetup autoSetup = (TimeWarpAutoSetup)target;
                TimeWarpRule sourceRule = autoSetup.Rules[index];

                TimeWarpRulePreset preset = ScriptableObject.CreateInstance<TimeWarpRulePreset>();
                
                string json = JsonUtility.ToJson(sourceRule);
                preset.Rule = JsonUtility.FromJson<TimeWarpRule>(json);

                AssetDatabase.CreateAsset(preset, path);
                AssetDatabase.SaveAssets();
                Debug.Log($"[TimeWarp] Preset saved successfully at: {path}");
            }
        }

        private void LoadPreset(int index)
        {
            string path = EditorUtility.OpenFilePanel("Select Rule Preset", "Assets", "asset");
            
            if (path.StartsWith(Application.dataPath))
            {
                string relativePath = "Assets" + path.Substring(Application.dataPath.Length);
                TimeWarpRulePreset loadedPreset = AssetDatabase.LoadAssetAtPath<TimeWarpRulePreset>(relativePath);
                
                if (loadedPreset != null)
                {
                    serializedObject.ApplyModifiedProperties();

                    // Register the action for the undo system (Ctrl+Z support)
                    Undo.RecordObject(target, "Load Rule Preset");

                    TimeWarpAutoSetup autoSetup = (TimeWarpAutoSetup)target;
                    
                    string json = JsonUtility.ToJson(loadedPreset.Rule);
                    autoSetup.Rules[index] = JsonUtility.FromJson<TimeWarpRule>(json);
                    
                    EditorUtility.SetDirty(target); 
                    
                    serializedObject.Update();

                    Debug.Log($"[TimeWarp] Preset loaded: {loadedPreset.name}");
                }
            }
        }
    }
}