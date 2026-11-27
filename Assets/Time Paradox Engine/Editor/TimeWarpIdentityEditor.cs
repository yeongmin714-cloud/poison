using UnityEngine;
using UnityEditor;
using TimeWarp.Core;
using TimeWarp.Core.Managers;

namespace TimeWarp.EditorScripts
{
    /// <summary>
    /// Tabbed inspector for TimeWarpIdentity.
    ///
    /// The previous inspector stacked every channel's settings in one long column, so a
    /// beginner met well over a hundred fields at once with no indication of which few
    /// actually mattered for their object.
    ///
    /// Two changes carry most of the improvement:
    ///
    /// 1. TABS - each recording channel gets its own page, and a page is only offered when
    ///    that channel is switched on. Turning a channel off removes its settings from
    ///    view entirely instead of leaving dead fields on screen.
    ///
    /// 2. SIMPLE MODE (default) - hides tuning that almost nobody needs on a first pass:
    ///    action filters, memory overrides, debug readouts and the event list. It is a
    ///    view filter only; nothing is disabled and no data is lost by switching modes.
    /// </summary>
    [CustomEditor(typeof(TimeWarpIdentity))]
    [CanEditMultipleObjects]
    public class TimeWarpIdentityEditor : Editor
    {
        private const string SimpleModeKey = "TimeWarp.Inspector.SimpleMode";

        private SerializedProperty Channels;
        private SerializedProperty LocalTimeScale;
        private SerializedProperty FractionalOffset;

        private SerializedProperty ActionFilters;
        private SerializedProperty RecordTimeOverride;
        private SerializedProperty FreeMemoryOnDisable;

        private SerializedProperty TransformSettings;
        private SerializedProperty VelocitySettings;
        private SerializedProperty Velocity2DSettings;
        private SerializedProperty CustomSettings;
        private SerializedProperty ParticleSettings;
        private SerializedProperty VfxSettings;
        private SerializedProperty AudioSettings;
        private SerializedProperty NavigationSettings;
        private SerializedProperty EchoSettings;

        private SerializedProperty OnLocalRewindStart;
        private SerializedProperty OnLocalRewindStop;
        private SerializedProperty OnLocalTimeStopStart;
        private SerializedProperty OnLocalTimeStopStop;
        private SerializedProperty OnLocalSlowMoStart;
        private SerializedProperty OnLocalSlowMoStop;
        private SerializedProperty OnTimeScaleChanged;

        private int _tab = 0;
        private bool _simpleMode = true;

        private void OnEnable()
        {
            _simpleMode = EditorPrefs.GetBool(SimpleModeKey, true);

            Channels = serializedObject.FindProperty("Channels");
            LocalTimeScale = serializedObject.FindProperty("LocalTimeScale");
            FractionalOffset = serializedObject.FindProperty("FractionalOffset");

            ActionFilters = serializedObject.FindProperty("ActionFilters");
            RecordTimeOverride = serializedObject.FindProperty("RecordTimeOverride");
            FreeMemoryOnDisable = serializedObject.FindProperty("FreeMemoryOnDisable");

            TransformSettings = serializedObject.FindProperty("TransformSettings");
            VelocitySettings = serializedObject.FindProperty("VelocitySettings");
            Velocity2DSettings = serializedObject.FindProperty("Velocity2DSettings");
            CustomSettings = serializedObject.FindProperty("CustomSettings");
            ParticleSettings = serializedObject.FindProperty("ParticleSettings");
            VfxSettings = serializedObject.FindProperty("VfxSettings");
            AudioSettings = serializedObject.FindProperty("AudioSettings");
            NavigationSettings = serializedObject.FindProperty("NavigationSettings");
            EchoSettings = serializedObject.FindProperty("EchoSettings");

            OnLocalRewindStart = serializedObject.FindProperty("OnLocalRewindStart");
            OnLocalRewindStop = serializedObject.FindProperty("OnLocalRewindStop");
            OnLocalTimeStopStart = serializedObject.FindProperty("OnLocalTimeStopStart");
            OnLocalTimeStopStop = serializedObject.FindProperty("OnLocalTimeStopStop");
            OnLocalSlowMoStart = serializedObject.FindProperty("OnLocalSlowMoStart");
            OnLocalSlowMoStop = serializedObject.FindProperty("OnLocalSlowMoStop");
            OnTimeScaleChanged = serializedObject.FindProperty("OnTimeScaleChanged");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawTopBar();
            DrawSetupWarnings();

            EditorGUILayout.Space(4);

            string[] tabs = BuildTabList(out int[] tabIds);
            _tab = Mathf.Clamp(_tab, 0, tabs.Length - 1);
            _tab = GUILayout.Toolbar(_tab, tabs);

            EditorGUILayout.Space(6);

            switch (tabIds[_tab])
            {
                case 0: DrawWhatToRecord(); break;
                case 1: DrawMotion(); break;
                case 2: DrawVisuals(); break;
                case 3: DrawSoundAndAI(); break;
                case 4: DrawEchoes(); break;
                case 5: DrawCustom(); break;
                case 6: DrawAdvanced(); break;
            }

            serializedObject.ApplyModifiedProperties();
        }

        // ==========================================
        // TOP BAR
        // ==========================================
        private void DrawTopBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
            EditorGUILayout.LabelField("Time Warp Identity", titleStyle);

            GUILayout.FlexibleSpace();

            bool newMode = GUILayout.Toggle(_simpleMode,
                new GUIContent("Simple", "Hide advanced tuning. This only changes what is shown - no settings are lost."),
                EditorStyles.miniButton, GUILayout.Width(60));

            if (newMode != _simpleMode)
            {
                _simpleMode = newMode;
                EditorPrefs.SetBool(SimpleModeKey, _simpleMode);
                _tab = 0;
            }

            EditorGUILayout.EndHorizontal();

            if (Application.isPlaying && !serializedObject.isEditingMultipleObjects)
            {
                var identity = (TimeWarpIdentity)target;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Live", EditorStyles.miniBoldLabel);

                EditorGUILayout.LabelField(
                    identity.IsRewinding ? "State: REWINDING" : $"State: running at {identity.EffectiveTimeScale:0.00}x",
                    EditorStyles.miniLabel);

                if (TimeWarpManager.Instance != null && TimeWarpManager.Instance.CaptureFramerate > 0)
                {
                    float seconds = identity.FractionalOffset / TimeWarpManager.Instance.CaptureFramerate;
                    EditorGUILayout.LabelField($"Rewound: {seconds:0.00}s into the past", EditorStyles.miniLabel);
                }

                EditorGUILayout.EndVertical();
                Repaint();
            }
        }

        /// <summary>
        /// Catches the two setup mistakes that otherwise fail silently at runtime.
        /// </summary>
        private void DrawSetupWarnings()
        {
            if (serializedObject.isEditingMultipleObjects) return;

            var identity = (TimeWarpIdentity)target;

            if (!Application.isPlaying && Object.FindAnyObjectByType<TimeWarpManager>() == null)
            {
                EditorGUILayout.HelpBox(
                    "No Time Warp Manager in this scene - nothing will record or rewind.\n" +
                    "Tools > TimeWarp > Setup Wizard can create one.",
                    MessageType.Warning);
            }

            if (Channels.intValue == 0)
            {
                EditorGUILayout.HelpBox(
                    "No channels are enabled, so this object records nothing. " +
                    "Tick at least \"Position, rotation & scale\" below.",
                    MessageType.Warning);
            }

            bool wantsVelocity = HasChannel(TimeChannels.Velocity);
            if (wantsVelocity && identity.GetComponent<Rigidbody>() == null)
            {
                EditorGUILayout.HelpBox(
                    "Physics (3D) is enabled but there is no Rigidbody on this object. " +
                    "Either add one or turn that channel off.",
                    MessageType.Warning);
            }

            bool wantsVelocity2D = HasChannel(TimeChannels.Velocity2D);
            if (wantsVelocity2D && identity.GetComponent<Rigidbody2D>() == null)
            {
                EditorGUILayout.HelpBox(
                    "Physics (2D) is enabled but there is no Rigidbody2D on this object.",
                    MessageType.Warning);
            }
        }

        // ==========================================
        // TABS
        // ==========================================
        private string[] BuildTabList(out int[] ids)
        {
            var names = new System.Collections.Generic.List<string>();
            var idList = new System.Collections.Generic.List<int>();

            names.Add("Record"); idList.Add(0);

            if (HasChannel(TimeChannels.Transform) || HasChannel(TimeChannels.Velocity) || HasChannel(TimeChannels.Velocity2D))
            { names.Add("Motion"); idList.Add(1); }

            if (HasChannel(TimeChannels.Particles) || HasChannel(TimeChannels.Animation) || HasChannel(TimeChannels.Shaders))
            { names.Add("Visuals"); idList.Add(2); }

            if (HasChannel(TimeChannels.Audio) || HasChannel(TimeChannels.Navigation))
            { names.Add("Sound & AI"); idList.Add(3); }

            if (HasChannel(TimeChannels.Echoes))
            { names.Add("Echoes"); idList.Add(4); }

            if (HasChannel(TimeChannels.Custom))
            { names.Add("Variables"); idList.Add(5); }

            if (!_simpleMode) { names.Add("Advanced"); idList.Add(6); }

            ids = idList.ToArray();
            return names.ToArray();
        }

        // --- TAB 0 ---
        private void DrawWhatToRecord()
        {
            EditorGUILayout.LabelField("What should be rewound?", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Only tick what this object actually needs - every channel costs memory and CPU.",
                EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            ChannelToggle(TimeChannels.Transform, "Position, rotation & scale",
                "The basics. Almost every object wants this.");

            ChannelToggle(TimeChannels.Velocity, "Physics (3D)",
                "Restores momentum when time resumes. Needs a Rigidbody and a Time Body component.");

            ChannelToggle(TimeChannels.Velocity2D, "Physics (2D)",
                "The 2D equivalent. Needs a Rigidbody2D and a Time Body 2D component.");

            ChannelToggle(TimeChannels.Animation, "Animation",
                "Rewinds the Animator. Needs a Time Animator component.");

            ChannelToggle(TimeChannels.Particles, "Particles",
                "Rewinds particle systems. The most memory-hungry channel by far.");

            ChannelToggle(TimeChannels.Shaders, "Shader animation",
                "Rewinds animated material properties. Needs a Time Shader component.");

            ChannelToggle(TimeChannels.Audio, "Audio",
                "Pitch follows the time scale. Needs a Time Audio component.");

            ChannelToggle(TimeChannels.Navigation, "AI navigation",
                "Keeps a NavMeshAgent from fighting the rewind. Needs a Time NavMesh Agent component.");

            ChannelToggle(TimeChannels.Custom, "Your own variables",
                "Rewinds fields inside your own scripts through the Time Warp Bridge.");

            ChannelToggle(TimeChannels.Echoes, "Time Echoes (ghosts)",
                "Leaves a replaying ghost behind after a rewind. Needs a Time Paradox Module.");

            EditorGUILayout.EndVertical();

            if (!_simpleMode)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(LocalTimeScale, new GUIContent("Local Time Scale",
                    "1 = normal. Zones write to this at runtime; set it by hand only for a permanently altered object."));
            }
        }

        // --- TAB 1 ---
        private void DrawMotion()
        {
            if (HasChannel(TimeChannels.Transform))
                Section("Transform", TransformSettings, "Which parts of the transform get recorded, and how playback is smoothed.");

            if (HasChannel(TimeChannels.Velocity))
                Section("Physics (3D)", VelocitySettings, "Needs a Time Body component and a Rigidbody.");

            if (HasChannel(TimeChannels.Velocity2D))
                Section("Physics (2D)", Velocity2DSettings, "Needs a Time Body 2D component and a Rigidbody2D.");
        }

        // --- TAB 2 ---
        private void DrawVisuals()
        {
            if (HasChannel(TimeChannels.Particles))
            {
                Section("Shuriken particles", ParticleSettings,
                    "Caching particle state is the heaviest channel. The manager enforces a global RAM budget " +
                    "and falls back to procedural playback when it runs out.");

                Section("VFX Graph", VfxSettings, null);
            }

            if (HasChannel(TimeChannels.Animation))
                EditorGUILayout.HelpBox("Animation is handled by the Time Animator component on this object.", MessageType.Info);

            if (HasChannel(TimeChannels.Shaders))
                EditorGUILayout.HelpBox("Shader properties are configured on the Time Shader component on this object.", MessageType.Info);
        }

        // --- TAB 3 ---
        private void DrawSoundAndAI()
        {
            if (HasChannel(TimeChannels.Audio))
                Section("Audio", AudioSettings, "Pitch follows the object's time scale.");

            if (HasChannel(TimeChannels.Navigation))
                Section("Navigation (AI)", NavigationSettings, "Suspends the NavMeshAgent so pathfinding does not fight playback.");
        }

        // --- TAB 4 ---
        private void DrawEchoes()
        {
            EditorGUILayout.HelpBox(
                "An Echo is a ghost that replays what this object just did. It needs a Time Paradox Module " +
                "on this object.\n\nIf ghosts render bright pink, the Hologram Material below uses a shader " +
                "that does not work in your render pipeline.",
                MessageType.Info);

            Section("Echo settings", EchoSettings, null);
        }

        // --- TAB 5 ---
        private void DrawCustom()
        {
            EditorGUILayout.HelpBox(
                "Binds float fields inside your own scripts so they rewind too - ammo, health, charge and so on. " +
                "Needs a Time Warp Bridge component.",
                MessageType.Info);

            Section("Variable bindings", CustomSettings, null);
        }

        // --- TAB 6 ---
        private void DrawAdvanced()
        {
            Section("Action filters & permissions", ActionFilters,
                "Restrict which anomalies affect this object, or require it to be inside a specific zone.");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Memory", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(RecordTimeOverride, new GUIContent("Record Time Override (sec)",
                "0 = use the manager's Max Record Time. Lower it for objects that only need a short history."));

            EditorGUILayout.PropertyField(FreeMemoryOnDisable, new GUIContent("Free Memory On Disable",
                "Releases the history buffer when this object is disabled. Good for pooled objects; " +
                "the history is lost and starts fresh on re-enable."));

            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(FractionalOffset, new GUIContent("Current Offset (frames)"));

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Events", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(OnLocalRewindStart);
            EditorGUILayout.PropertyField(OnLocalRewindStop);
            EditorGUILayout.PropertyField(OnLocalTimeStopStart);
            EditorGUILayout.PropertyField(OnLocalTimeStopStop);
            EditorGUILayout.PropertyField(OnLocalSlowMoStart);
            EditorGUILayout.PropertyField(OnLocalSlowMoStop);
            EditorGUILayout.PropertyField(OnTimeScaleChanged);
        }

        // ==========================================
        // HELPERS
        // ==========================================
        private bool HasChannel(TimeChannels channel)
        {
            return (Channels.intValue & (int)channel) != 0;
        }

        private void ChannelToggle(TimeChannels channel, string label, string tooltip)
        {
            bool current = HasChannel(channel);

            EditorGUI.BeginChangeCheck();
            bool next = EditorGUILayout.ToggleLeft(new GUIContent(label, tooltip), current);

            if (EditorGUI.EndChangeCheck())
            {
                if (next) Channels.intValue |= (int)channel;
                else Channels.intValue &= ~(int)channel;
            }
        }

        private void Section(string title, SerializedProperty property, string help)
        {
            if (property == null) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

            if (!string.IsNullOrEmpty(help))
                EditorGUILayout.LabelField(help, EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.PropertyField(property, GUIContent.none, true);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }
    }
}
