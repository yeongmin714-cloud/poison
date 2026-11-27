using UnityEngine;
using UnityEditor;
using TimeWarp.Core;
using TimeWarp.Core.Managers;

namespace TimeWarp.EditorTools
{
    /// <summary>
    /// Tabbed inspector for the manager, with a built-in manual on every page.
    ///
    /// The manager holds the settings that decide how time FEELS, and those settings were
    /// previously a single column of numbered headers with no explanation of what any of
    /// them do to the player's experience. Each tab here groups one decision and explains
    /// it before showing the fields.
    /// </summary>
    [CustomEditor(typeof(TimeWarpManager))]
    public class TimeWarpManagerEditor : Editor
    {
        private SerializedProperty Mode, ActionType, CaptureFramerate, MaxRecordTime;
        private SerializedProperty StartDelay, UseMaxDuration, MaxDuration;
        private SerializedProperty UseEntryCurve, EntryCurve, EntryDuration;
        private SerializedProperty UseExitCurve, ExitCurve, ExitDuration;
        private SerializedProperty RewindAccelerationCurve, RewindSpeedMultiplier, TargetSlowMoScale;
        private SerializedProperty GlobalRewindVisualQuality, MaxVFXRewindSteps, MaxParticleRamBudgetMB;
        private SerializedProperty OnGlobalRewindStart, OnGlobalRewindStop, OnGlobalTimeStop, OnGlobalSlowMoStart;

        private int _tab = 0;
        private static readonly string[] Tabs = { "Setup", "Action", "Feel", "Performance", "Events" };

        private void OnEnable()
        {
            Mode = serializedObject.FindProperty("Mode");
            ActionType = serializedObject.FindProperty("ActionType");
            CaptureFramerate = serializedObject.FindProperty("CaptureFramerate");
            MaxRecordTime = serializedObject.FindProperty("MaxRecordTime");

            StartDelay = serializedObject.FindProperty("StartDelay");
            UseMaxDuration = serializedObject.FindProperty("UseMaxDuration");
            MaxDuration = serializedObject.FindProperty("MaxDuration");

            UseEntryCurve = serializedObject.FindProperty("UseEntryCurve");
            EntryCurve = serializedObject.FindProperty("EntryCurve");
            EntryDuration = serializedObject.FindProperty("EntryDuration");
            UseExitCurve = serializedObject.FindProperty("UseExitCurve");
            ExitCurve = serializedObject.FindProperty("ExitCurve");
            ExitDuration = serializedObject.FindProperty("ExitDuration");

            RewindAccelerationCurve = serializedObject.FindProperty("RewindAccelerationCurve");
            RewindSpeedMultiplier = serializedObject.FindProperty("RewindSpeedMultiplier");
            TargetSlowMoScale = serializedObject.FindProperty("TargetSlowMoScale");

            GlobalRewindVisualQuality = serializedObject.FindProperty("GlobalRewindVisualQuality");
            MaxVFXRewindSteps = serializedObject.FindProperty("MaxVFXRewindSteps");
            MaxParticleRamBudgetMB = serializedObject.FindProperty("MaxParticleRamBudgetMB");

            OnGlobalRewindStart = serializedObject.FindProperty("OnGlobalRewindStart");
            OnGlobalRewindStop = serializedObject.FindProperty("OnGlobalRewindStop");
            OnGlobalTimeStop = serializedObject.FindProperty("OnGlobalTimeStop");
            OnGlobalSlowMoStart = serializedObject.FindProperty("OnGlobalSlowMoStart");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawHeader2();
            DrawLiveState();

            EditorGUILayout.Space(4);
            _tab = GUILayout.Toolbar(_tab, Tabs);
            EditorGUILayout.Space(6);

            switch (_tab)
            {
                case 0: DrawSetup(); break;
                case 1: DrawAction(); break;
                case 2: DrawFeel(); break;
                case 3: DrawPerformance(); break;
                case 4: DrawEvents(); break;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawHeader2()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            var style = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            Color prev = GUI.contentColor;
            GUI.contentColor = TimeWarpEditorUI.AccentBlue;
            EditorGUILayout.LabelField("Time Warp Manager", style);
            GUI.contentColor = prev;

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Hub", EditorStyles.miniButton, GUILayout.Width(46)))
                TimeWarpHub.Open();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawLiveState()
        {
            if (!Application.isPlaying) return;

            var manager = (TimeWarpManager)target;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Live", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField($"State: {manager.GlobalTimeState}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Global time scale: {manager.CurrentGlobalTimeScale:0.00}x", EditorStyles.miniLabel);

            if (manager.UseMaxDuration)
            {
                Rect r = EditorGUILayout.GetControlRect(false, 14);
                EditorGUI.ProgressBar(r, manager.ActionProgress01,
                    $"{manager.ActionTimeRemaining:0.0}s left");
            }

            EditorGUILayout.EndVertical();
            Repaint();
        }

        // ==========================================
        // TAB 0 - SETUP
        // ==========================================
        private void DrawSetup()
        {
            TimeWarpEditorUI.Manual("mgr.mode", "Engine Mode - read this first",
                "GLOBAL ONLY - time anomalies are triggered from your code and affect everything at " +
                "once. Bullet time, a rewind ability, a pause power. This is what most projects want.\n\n" +
                "LOCAL ZONES ONLY - there is no global anomaly at all; time is changed purely by Time " +
                "Zones. Puzzle games where specific rooms or fields behave differently.\n\n" +
                "BOTH - the two multiply together. An object in a 0.2x zone during a 0.5x global slow " +
                "motion ends up at 0.1x.",
                "If a global rewind seems to do nothing, check that Mode is not set to Local Zones Only.");

            TimeWarpEditorUI.BeginSection("Core");
            EditorGUILayout.PropertyField(Mode, new GUIContent("Engine Mode"));
            EditorGUILayout.PropertyField(ActionType, new GUIContent("Default Action",
                "What TriggerAction does when you do not specify an action explicitly."));
            TimeWarpEditorUI.EndSection();

            TimeWarpEditorUI.Manual("mgr.history", "How much history to keep",
                "Two numbers decide the memory cost of the entire system.\n\n" +
                "CAPTURE FRAMERATE is how many snapshots per second are recorded. It is overwritten at " +
                "runtime from the physics timestep, so treat the field as a preview rather than a " +
                "setting - change Fixed Timestep in Project Settings if you want a different rate.\n\n" +
                "MAX RECORD TIME is how far back a rewind can reach. Ten seconds is generous for most " +
                "games; a rewind ability rarely reaches past three.\n\n" +
                "Every recorded object allocates framerate x seconds of history, so halving this halves " +
                "the memory used by the whole scene.",
                "Individual objects can override this with Record Time Override if they only need a moment.");

            TimeWarpEditorUI.BeginSection("History");
            EditorGUILayout.PropertyField(CaptureFramerate, new GUIContent("Capture Framerate"));
            EditorGUILayout.PropertyField(MaxRecordTime, new GUIContent("Max Record Time (sec)"));

            DrawBufferSizeInfo();
            TimeWarpEditorUI.EndSection();
        }

        /// <summary>
        /// The buffer is rounded up to a power of two so it can index with a bitmask instead
        /// of a modulo. Good trade, but the rounding is invisible and can quietly cost a lot
        /// of memory, so it is spelled out here.
        /// </summary>
        private void DrawBufferSizeInfo()
        {
            int framerate = Mathf.Max(1, Mathf.RoundToInt(1f / Time.fixedDeltaTime));
            int rawCount = Mathf.Max(1, Mathf.CeilToInt(framerate * MaxRecordTime.floatValue));
            int actualCount = Mathf.NextPowerOfTwo(rawCount);

            const int transformSnapshotBytes = 40;
            float kbPerObject = (actualCount * transformSnapshotBytes) / 1024f;
            float wastePercent = (actualCount - rawCount) * 100f / actualCount;

            string msg = $"Buffer: {rawCount} frames requested, {actualCount} allocated ({framerate} fps).\n" +
                         $"About {kbPerObject:F0} KB per object for the transform channel alone.";

            if (wastePercent >= 20f)
            {
                float idealTime = actualCount / (float)framerate;
                EditorGUILayout.HelpBox(
                    msg + $"\n\nRounded up to a power of two, so {wastePercent:F0}% of that is unused. " +
                    $"Raising Max Record Time to {idealTime:F2}s costs nothing extra and gives you the " +
                    $"full buffer.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(msg, MessageType.Info);
            }
        }

        // ==========================================
        // TAB 1 - ACTION
        // ==========================================
        private void DrawAction()
        {
            TimeWarpEditorUI.Manual("mgr.action", "Tuning each anomaly",
                "REWIND SPEED MULTIPLIER is how fast history is consumed. At 1 a rewind plays back at " +
                "real speed; at 3 it races. Note that a fast rewind burns through Max Record Time three " +
                "times as quickly.\n\n" +
                "REWIND ACCELERATION CURVE shapes the speed over the life of the rewind. Starting slow " +
                "and speeding up reads far better than a constant rate - the player gets a moment to " +
                "understand what is happening before it accelerates.\n\n" +
                "TARGET SLOW MO SCALE is the global slow motion factor. 0.2 is a strong, obvious bullet " +
                "time; 0.5 is a subtle drag.",
                "Time stop needs no settings - it is simply scale 0.");

            TimeWarpEditorUI.BeginSection("Rewind");
            EditorGUILayout.PropertyField(RewindSpeedMultiplier, new GUIContent("Rewind Speed"));
            EditorGUILayout.PropertyField(RewindAccelerationCurve, new GUIContent("Acceleration Curve"));
            TimeWarpEditorUI.EndSection();

            TimeWarpEditorUI.BeginSection("Slow motion");
            EditorGUILayout.PropertyField(TargetSlowMoScale, new GUIContent("Target Time Scale"));
            TimeWarpEditorUI.EndSection();

            TimeWarpEditorUI.Manual("mgr.duration", "Limiting how long it lasts",
                "With USE MAX DURATION off, an anomaly runs until your code calls StopGlobalAction - " +
                "you own the timing. With it on, the engine stops the action itself after Max Duration " +
                "seconds, which is what you want for a resource-limited ability.\n\n" +
                "START DELAY inserts a pause between the trigger and the anomaly actually starting, " +
                "useful for lining up with an animation or a sound cue.",
                "The rewind HUD bar can only show remaining time when Use Max Duration is on.");

            TimeWarpEditorUI.BeginSection("Duration");
            EditorGUILayout.PropertyField(StartDelay, new GUIContent("Start Delay (sec)"));
            EditorGUILayout.PropertyField(UseMaxDuration, new GUIContent("Use Max Duration"));

            using (new EditorGUI.DisabledScope(!UseMaxDuration.boolValue))
                EditorGUILayout.PropertyField(MaxDuration, new GUIContent("Max Duration (sec)"));

            TimeWarpEditorUI.EndSection();
        }

        // ==========================================
        // TAB 2 - FEEL
        // ==========================================
        private void DrawFeel()
        {
            TimeWarpEditorUI.Manual("mgr.curves", "Why entry and exit curves matter",
                "Snapping straight from 1.0 to 0.2 is jarring - it reads as a stutter rather than a " +
                "deliberate effect. These curves ramp the global time scale in and out instead.\n\n" +
                "ENTRY is usually the slower of the two, around half a second, so the transition is felt.\n\n" +
                "EXIT is usually faster, around a third of a second, because the player has already got " +
                "the message and now wants control back.\n\n" +
                "If your post-processing pops rather than blends, these are the numbers to raise - the " +
                "volume weight follows the same ramp.",
                "Turning both off makes anomalies switch instantly, which suits stylised or retro games.");

            TimeWarpEditorUI.BeginSection("Entry");
            EditorGUILayout.PropertyField(UseEntryCurve, new GUIContent("Use Entry Curve"));
            using (new EditorGUI.DisabledScope(!UseEntryCurve.boolValue))
            {
                EditorGUILayout.PropertyField(EntryCurve, new GUIContent("Curve"));
                EditorGUILayout.PropertyField(EntryDuration, new GUIContent("Duration (sec)"));
            }
            TimeWarpEditorUI.EndSection();

            TimeWarpEditorUI.BeginSection("Exit");
            EditorGUILayout.PropertyField(UseExitCurve, new GUIContent("Use Exit Curve"));
            using (new EditorGUI.DisabledScope(!UseExitCurve.boolValue))
            {
                EditorGUILayout.PropertyField(ExitCurve, new GUIContent("Curve"));
                EditorGUILayout.PropertyField(ExitDuration, new GUIContent("Duration (sec)"));
            }
            TimeWarpEditorUI.EndSection();
        }

        // ==========================================
        // TAB 3 - PERFORMANCE
        // ==========================================
        private void DrawPerformance()
        {
            TimeWarpEditorUI.Manual("mgr.particles", "The particle RAM budget",
                "Particles are by far the most expensive thing to rewind. Recording them means storing " +
                "the full state of every live particle for every frame of history, which dwarfs the " +
                "cost of a transform.\n\n" +
                "This budget is a hard ceiling. When a particle system asks to be cached and the budget " +
                "is already spent, it is refused and falls back to PROCEDURAL playback - the system is " +
                "simulated backwards approximately instead of replayed exactly. Cheaper, less accurate, " +
                "and it never blows your memory limit.\n\n" +
                "Raise it if smoke and sparks look wrong during a rewind. Lower it on mobile.",
                "The cheapest fix is usually to turn the Particles channel off on objects that do not need it.");

            TimeWarpEditorUI.BeginSection("Particles");
            EditorGUILayout.PropertyField(MaxParticleRamBudgetMB, new GUIContent("RAM Budget (MB)"));
            TimeWarpEditorUI.EndSection();

            TimeWarpEditorUI.Manual("mgr.visual", "Visual quality during rewind",
                "GLOBAL REWIND VISUAL QUALITY is how many visual steps per second are evaluated while " +
                "rewinding. Lower values are cheaper and slightly choppier - and during a fast rewind " +
                "almost nobody notices.\n\n" +
                "MAX VFX REWIND STEPS caps how far a VFX Graph effect will be re-simulated in one go, " +
                "so a long rewind cannot stall the frame while it catches up.",
                "These only affect appearance during an anomaly - normal gameplay is untouched.");

            TimeWarpEditorUI.BeginSection("Visual quality");
            EditorGUILayout.PropertyField(GlobalRewindVisualQuality, new GUIContent("Rewind Visual Quality"));
            EditorGUILayout.PropertyField(MaxVFXRewindSteps, new GUIContent("Max VFX Rewind Steps"));
            TimeWarpEditorUI.EndSection();

            TimeWarpEditorUI.BeginSection("Profiling");
            TimeWarpEditorUI.Note(
                "The engine reports its own timings to the Profiler under the \"TimeWarp.\" prefix. " +
                "Watch TimeWarp.WaitForTransformJob - if that is high, you have more recorded objects " +
                "than the job can process in time, which is a different problem from slow user code.");
            TimeWarpEditorUI.EndSection();
        }

        // ==========================================
        // TAB 4 - EVENTS
        // ==========================================
        private void DrawEvents()
        {
            TimeWarpEditorUI.Manual("mgr.events", "Hooking your game up",
                "These fire once when a global anomaly starts or stops - the place to trigger a sound, " +
                "swap a music track, show a HUD element or start a camera shake.\n\n" +
                "For per-object reactions use the events on Time Warp Identity instead, or implement " +
                "ITimeWarpStateListener on your script to receive start, update and resume callbacks.",
                "Anything hooked here runs on unscaled time, so it works normally during a time stop.");

            TimeWarpEditorUI.BeginSection("Global events");
            EditorGUILayout.PropertyField(OnGlobalRewindStart);
            EditorGUILayout.PropertyField(OnGlobalRewindStop);
            EditorGUILayout.PropertyField(OnGlobalTimeStop);
            EditorGUILayout.PropertyField(OnGlobalSlowMoStart);
            TimeWarpEditorUI.EndSection();
        }
    }
}
