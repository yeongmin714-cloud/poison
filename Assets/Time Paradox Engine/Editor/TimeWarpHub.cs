using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using TimeWarp.Core;
using TimeWarp.Core.Managers;
using TimeWarp.Modules.Physics.Dynamics;
using TimeWarp.Modules.Animation;
using TimeWarp.Modules.Particles;
using TimeWarp.Modules.Visuals;
using TimeWarp.Modules.Audio;
using TimeWarp.Modules.Navigation;
using TimeWarp.Modules.Gameplay;
using TimeWarp.Modules.Paradox;
using TimeWarp.Modules.Zones;
using TimeWarp.Modules.Zones.Components;
using TimeWarp.Modules.Zones.Profiles;

namespace TimeWarp.EditorTools
{
    /// <summary>
    /// One window for everything you would otherwise have to hunt for: setting up objects,
    /// building zones, wiring a character, adding the rewind UI, and hooking up the
    /// post-processing volume.
    ///
    /// Design intent: the engine is capable but wide, and its features were only
    /// discoverable by reading a 96-page PDF. Every page here explains what the feature is
    /// and when to use it BEFORE offering the button, and every button previews what it is
    /// about to do. Nothing happens silently and everything is a single Undo step.
    /// </summary>
    public class TimeWarpHub : EditorWindow
    {
        private enum Page
        {
            Start,
            Manual,
            Objects,
            Character,
            Controllers,
            Zones,
            Kinetic,
            TwoD,
            Particles,
            NavMesh,
            Shaders,
            Interface,
            Logic,
            PostFx,
            Extras,
            Troubleshoot
        }

        /// <summary>Sidebar grouping. A flat list of sixteen entries is unreadable.</summary>
        private static readonly (string group, Page page, string label)[] Nav =
        {
            ("GETTING STARTED", Page.Start,       "Start here"),
            ("GETTING STARTED", Page.Manual,      "User Manual"),

            ("SETUP",           Page.Objects,     "Objects"),
            ("SETUP",           Page.Character,   "Character"),
            ("SETUP",           Page.Controllers, "Controllers"),
            ("SETUP",           Page.TwoD,        "2D"),

            ("WORLD",           Page.Zones,       "Time Zones"),
            ("WORLD",           Page.Kinetic,     "Kinetic Force"),
            ("WORLD",           Page.NavMesh,     "NavMesh / AI"),

            ("VISUALS",         Page.Particles,   "VFX & Particles"),
            ("VISUALS",         Page.Shaders,     "Shaders"),
            ("VISUALS",         Page.PostFx,      "Post-processing"),

            ("CODE",            Page.Logic,       "Logic, UI & Bridge"),
            ("CODE",            Page.Interface,   "Interface / HUD"),
            ("CODE",            Page.Extras,      "Extras & Edge Cases"),

            ("HELP",            Page.Troubleshoot,"Troubleshooting")
        };

        private Page _page = Page.Start;
        private Vector2 _sideScroll;
        private Vector2 _mainScroll;

        [MenuItem("Tools/TimeWarp/Hub (Setup && Manual)", false, 0)]
        public static void Open()
        {
            var window = GetWindow<TimeWarpHub>(false, "TimeWarp Hub");
            window.minSize = new Vector2(780, 600);
            window.Show();
        }

        [MenuItem("Tools/TimeWarp/Setup Wizard", false, 1)]
        public static void OpenObjects()
        {
            var window = GetWindow<TimeWarpHub>(false, "TimeWarp Hub");
            window.minSize = new Vector2(780, 600);
            window._page = Page.Objects;
            window.Show();
        }

        private void OnSelectionChange() => Repaint();

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            DrawSidebar();
            DrawMain();
            EditorGUILayout.EndHorizontal();
        }

        // ==========================================
        // SIDEBAR
        // ==========================================
        private void DrawSidebar()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(180));
            _sideScroll = EditorGUILayout.BeginScrollView(_sideScroll);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("TIME PARADOX", EditorStyles.miniBoldLabel);
            EditorGUILayout.Space(4);

            string lastGroup = null;

            foreach (var entry in Nav)
            {
                if (entry.group != lastGroup)
                {
                    EditorGUILayout.Space(6);
                    EditorGUILayout.LabelField(entry.group, EditorStyles.miniBoldLabel);
                    lastGroup = entry.group;
                }

                bool selected = _page == entry.page;

                var style = new GUIStyle(EditorStyles.miniButton)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fixedHeight = 22,
                    fontStyle = selected ? FontStyle.Bold : FontStyle.Normal
                };

                if (GUILayout.Toggle(selected, "  " + entry.label, style) && !selected)
                {
                    _page = entry.page;
                    GUI.FocusControl(null);
                }
            }

            EditorGUILayout.Space(10);
            DrawSidebarStatus();

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawSidebarStatus()
        {
            var manager = Object.FindAnyObjectByType<TimeWarpManager>();
            int identities = Object.FindObjectsByType<TimeWarpIdentity>(FindObjectsInactive.Include).Length;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Scene", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(manager != null ? "Manager: yes" : "Manager: MISSING", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Objects: {identities}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        // ==========================================
        // MAIN
        // ==========================================
        private void DrawMain()
        {
            EditorGUILayout.BeginVertical();
            _mainScroll = EditorGUILayout.BeginScrollView(_mainScroll);

            switch (_page)
            {
                case Page.Start:        DrawStart(); break;
                case Page.Manual:       DrawManual(); break;
                case Page.Objects:      DrawObjects(); break;
                case Page.Character:    DrawCharacter(); break;
                case Page.Controllers:  DrawControllers(); break;
                case Page.TwoD:         DrawTwoD(); break;
                case Page.Zones:        DrawZones(); break;
                case Page.Kinetic:      DrawKinetic(); break;
                case Page.NavMesh:      DrawNavMesh(); break;
                case Page.Particles:    DrawParticles(); break;
                case Page.Shaders:      DrawShaders(); break;
                case Page.Interface:    DrawInterface(); break;
                case Page.Logic:        DrawLogic(); break;
                case Page.PostFx:       DrawPostFx(); break;
                case Page.Extras:       DrawExtras(); break;
                case Page.Troubleshoot: DrawTroubleshoot(); break;
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        // ==========================================
        // PAGE: START
        // ==========================================
        private void DrawStart()
        {
            TimeWarpEditorUI.PageTitle("Time Paradox Engine",
                "Rewind, slow down and freeze time - per object, not just globally.",
                TimeWarpEditorUI.AccentBlue);

            TimeWarpEditorUI.Manual("start.how", "How the engine is put together",
                "Three pieces, and nothing works without the first two:\n\n" +
                "1. THE MANAGER - one per scene. It owns the clock, decides what a rewind or a slow " +
                "motion means globally, and drives every recorded object in one batched job.\n\n" +
                "2. AN IDENTITY - one per object that should react to time. It records a rolling " +
                "history of that object. An object without an Identity simply ignores time entirely.\n\n" +
                "3. MODULES - small components that teach an Identity what else to record: physics, " +
                "animation, particles, audio, AI. You only add the ones an object actually needs.\n\n" +
                "Zones, echoes and post-processing all sit on top of those three.",
                "Use the Objects page rather than adding modules by hand - it inspects the object and " +
                "only adds what fits.");

            TimeWarpEditorUI.Manual("start.time", "Global time vs local time",
                "The engine multiplies two numbers to get an object's speed:\n\n" +
                "GLOBAL scale comes from the manager - a rewind or a slow motion you trigger from code.\n\n" +
                "LOCAL scale comes from Time Zones and affects only the objects inside them.\n\n" +
                "That is why a character standing in a slow-motion zone during a global rewind is a " +
                "genuinely different state from either one alone - and why the engine never touches " +
                "Unity's Time.timeScale. Nothing outside the engine is affected.",
                "Because Time.timeScale is untouched, your UI and menus keep running at full speed for free.");

            TimeWarpEditorUI.BeginSection("Step 1 - the manager");

            var manager = Object.FindAnyObjectByType<TimeWarpManager>();

            if (manager != null)
            {
                TimeWarpEditorUI.Note("This scene already has a manager.");
                if (GUILayout.Button("Select it")) Selection.activeGameObject = manager.gameObject;
            }
            else
            {
                EditorGUILayout.HelpBox("No manager in this scene. Nothing can record or rewind.", MessageType.Warning);

                if (TimeWarpEditorUI.ActionButton("Create Time Warp Manager", TimeWarpEditorUI.AccentGreen))
                {
                    var go = new GameObject("Time Warp Manager");
                    go.AddComponent<TimeWarpManager>();
                    Undo.RegisterCreatedObjectUndo(go, "Create Time Warp Manager");
                    Selection.activeGameObject = go;
                }
            }

            TimeWarpEditorUI.EndSection();

            TimeWarpEditorUI.BeginSection("Step 2 - make something rewindable");
            TimeWarpEditorUI.Note("Open the Objects page, select something in the scene, and pick what it is.");
            if (GUILayout.Button("Go to Objects")) _page = Page.Objects;
            TimeWarpEditorUI.EndSection();

            TimeWarpEditorUI.BeginSection("Full manual");
            TimeWarpEditorUI.Note(
                "DOCUMENTATION.md in the Time Paradox Engine folder is the complete reference: " +
                "every component, the scripting API, performance tuning and deployment notes.");

            if (GUILayout.Button("Open DOCUMENTATION.md"))
            {
                string[] found = AssetDatabase.FindAssets("DOCUMENTATION t:TextAsset");
                bool opened = false;

                foreach (string guid in found)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!path.EndsWith("DOCUMENTATION.md")) continue;

                    var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                    if (asset != null) { AssetDatabase.OpenAsset(asset); opened = true; }
                    break;
                }

                if (!opened)
                    EditorUtility.DisplayDialog("TimeWarp",
                        "DOCUMENTATION.md was not found. It ships in the Time Paradox Engine folder.", "OK");
            }
            TimeWarpEditorUI.EndSection();

            TimeWarpEditorUI.BeginSection("Step 3 - trigger it from your code");
            EditorGUILayout.SelectableLabel(
                "TimeWarpManager.Instance.TriggerAction(GlobalAction.Rewind);\n" +
                "TimeWarpManager.Instance.StopGlobalAction();",
                EditorStyles.textArea, GUILayout.Height(34));
            TimeWarpEditorUI.Note("That is the whole API for a basic rewind. See Control System/RewindTester.cs for a working example.");
            TimeWarpEditorUI.EndSection();
        }

        // ==========================================
        // PAGE: OBJECTS
        // ==========================================
        private enum ObjectRole { PlayerCharacter, EnemyAI, PhysicsProp, Projectile, Decoration, Character2D }

        private ObjectRole _role = ObjectRole.PhysicsProp;
        private bool _addEchoes = false;

        private static readonly Dictionary<ObjectRole, (string title, string desc, string example)> RoleInfo =
            new Dictionary<ObjectRole, (string, string, string)>
        {
            { ObjectRole.PlayerCharacter, ("Player character",
                "Records movement and animation, and switches your control scripts off while time rewinds so they cannot fight the playback.",
                "Player prefab, driveable vehicle") },
            { ObjectRole.EnemyAI, ("Enemy / AI",
                "Same as the player, plus NavMeshAgent handling so pathfinding does not fight the rewind.",
                "Patrolling guard, chasing monster") },
            { ObjectRole.PhysicsProp, ("Physics object",
                "Records position and velocity, and hands momentum back correctly when time resumes.",
                "Crate, barrel, debris, ragdoll") },
            { ObjectRole.Projectile, ("Projectile",
                "A physics object that also leaves a Time Echo - a ghost replaying its flight path.",
                "Bullet, arrow, grenade") },
            { ObjectRole.Decoration, ("Decoration / VFX",
                "No physics. Rewinds particles and shader animation only - the cheapest option.",
                "Torch, waterfall, smoke, animated sign") },
            { ObjectRole.Character2D, ("2D character",
                "The 2D counterpart of the player setup, using Rigidbody2D.",
                "Platformer hero, 2D enemy") }
        };

        private void DrawObjects()
        {
            TimeWarpEditorUI.PageTitle("Make objects react to time",
                "Select objects in the scene, say what they are, and let the wizard add the right modules.",
                TimeWarpEditorUI.AccentBlue);

            TimeWarpEditorUI.Manual("objects.why", "Why not just add components by hand?",
                "There are seventeen module components and eleven recording channels. Which ones an " +
                "object needs depends on what is already on it - a crate needs Time Body, a torch needs " +
                "Time Particles, and giving either one the other wastes memory for nothing.\n\n" +
                "This page inspects the object first. If there is no Rigidbody it will not add Time Body; " +
                "if there is no Animator it will not add Time Animator. It tells you what it skipped and why.",
                "Everything here is one Undo step, so it is safe to try a role and change your mind.");

            GameObject[] targets = GetValidTargets();

            TimeWarpEditorUI.BeginSection("Selection");
            if (targets.Length == 0)
                EditorGUILayout.HelpBox("Select one or more objects in the scene.", MessageType.Info);
            else if (targets.Length == 1)
                TimeWarpEditorUI.Note("Selected: " + targets[0].name);
            else
                TimeWarpEditorUI.Note($"Selected: {targets.Length} objects");
            TimeWarpEditorUI.EndSection();

            if (targets.Length == 0) return;

            TimeWarpEditorUI.BeginSection("What is it?");
            foreach (var pair in RoleInfo)
            {
                bool selected = _role == pair.Key;
                var style = new GUIStyle(EditorStyles.miniButton)
                {
                    alignment = TextAnchor.MiddleLeft, fixedHeight = 22,
                    fontStyle = selected ? FontStyle.Bold : FontStyle.Normal
                };

                if (GUILayout.Toggle(selected, "  " + pair.Value.title, style) && !selected)
                {
                    _role = pair.Key;
                    GUI.FocusControl(null);
                }
            }

            EditorGUILayout.Space(4);
            TimeWarpEditorUI.Note(RoleInfo[_role].desc);
            TimeWarpEditorUI.Note("Typical use: " + RoleInfo[_role].example);

            if (_role != ObjectRole.Projectile && _role != ObjectRole.Decoration)
            {
                EditorGUILayout.Space(4);
                _addEchoes = EditorGUILayout.ToggleLeft(
                    new GUIContent("Also leave Time Echoes (ghosts)",
                        "Spawns a replaying ghost after a rewind. Costs extra memory and CPU."), _addEchoes);
            }
            TimeWarpEditorUI.EndSection();

            TimeWarpEditorUI.BeginSection("What will happen");
            foreach (string line in BuildPlan(targets[0])) TimeWarpEditorUI.Bullet(line);

            if (targets.Length > 1)
                TimeWarpEditorUI.Note("Preview shows the first object; each one is inspected separately.");

            EditorGUILayout.Space(6);
            if (TimeWarpEditorUI.ActionButton("Apply setup", TimeWarpEditorUI.AccentGreen, 30))
            {
                foreach (var go in targets) ApplyRole(go, _role, _addEchoes);
                EditorUtility.DisplayDialog("TimeWarp",
                    $"Setup applied to {targets.Length} object(s).", "OK");
            }
            TimeWarpEditorUI.Note("Single Undo step (Ctrl+Z).");
            TimeWarpEditorUI.EndSection();
        }

        // ==========================================
        // PAGE: CHARACTER
        // ==========================================
        private void DrawCharacter()
        {
            TimeWarpEditorUI.PageTitle("Character setup",
                "The one part of the engine that needs a decision from you.",
                TimeWarpEditorUI.AccentGreen);

            TimeWarpEditorUI.Manual("char.problem", "The problem with characters",
                "A crate is easy: the engine writes its position every frame and nothing argues.\n\n" +
                "A character is different, because your own movement script is ALSO writing to the " +
                "transform every frame. During a rewind both are writing at once, and the result is a " +
                "character that jitters, refuses to move, or drifts away from the recorded path.\n\n" +
                "The fix is to switch your control scripts off for the duration of the anomaly and back " +
                "on afterwards. That is what the Time Warp Component Manager does.",
                "This is why the Player role adds a Component Manager and no other role does.");

            TimeWarpEditorUI.Manual("char.killswitch", "The kill switch, in detail",
                "Time Warp Component Manager holds a list called Target Components. Everything in that " +
                "list is disabled when the object starts rewinding (or freezes) and re-enabled the " +
                "moment it stops.\n\n" +
                "Put in: movement controllers, input handlers, shooting, ability scripts, anything that " +
                "moves or acts on its own.\n\n" +
                "Leave out: the Animator, colliders, renderers, and anything from TimeWarp itself - the " +
                "engine drives those directly and disabling them breaks playback.\n\n" +
                "If you need finer control, implement ITimeWarpStateListener on your script and you will " +
                "be told when an anomaly starts, updates and ends instead of just being switched off.",
                "Auto-Fill picks up your scripts and skips Unity and TimeWarp components automatically.");

            GameObject[] targets = GetValidTargets();

            if (targets.Length == 0)
            {
                EditorGUILayout.HelpBox("Select your character in the scene.", MessageType.Info);
                return;
            }

            GameObject go = targets[0];

            TimeWarpEditorUI.BeginSection("Checklist for " + go.name);

            bool hasIdentity = go.GetComponent<TimeWarpIdentity>() != null;
            var killSwitch = go.GetComponent<TimeWarpComponentManager>();
            bool hasBody = go.GetComponent<TimeBody>() != null || go.GetComponent<TimeBody2D>() != null;
            bool hasAnimatorModule = go.GetComponent<TimeAnimator>() != null;
            bool hasAnimator = go.GetComponentInChildren<Animator>(true) != null;

            Check(hasIdentity, "Time Warp Identity", "The recorder. Nothing works without it.");
            Check(killSwitch != null, "Component Manager (kill switch)", "Disables your scripts while time is odd.");
            Check(!hasAnimator || hasAnimatorModule, "Time Animator", hasAnimator
                ? "This object has an Animator, so animation should be rewound too."
                : "No Animator on this object - not needed.");
            Check(hasBody || go.GetComponent<Rigidbody>() == null, "Time Body",
                go.GetComponent<Rigidbody>() != null
                    ? "This object has a Rigidbody, so physics should be recorded."
                    : "No Rigidbody - movement comes from the Transform, which is fine.");

            TimeWarpEditorUI.EndSection();

            if (!hasIdentity || killSwitch == null)
            {
                TimeWarpEditorUI.BeginSection("Fix it");
                if (TimeWarpEditorUI.ActionButton("Run character setup on " + go.name, TimeWarpEditorUI.AccentGreen))
                {
                    ApplyRole(go, go.GetComponent<Rigidbody2D>() != null
                        ? ObjectRole.Character2D : ObjectRole.PlayerCharacter, false);
                }
                TimeWarpEditorUI.EndSection();
                return;
            }

            TimeWarpEditorUI.BeginSection("Scripts switched off during anomalies");

            if (killSwitch.TargetComponents.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "The list is EMPTY. Your control scripts will keep running during a rewind and " +
                    "fight the playback - this is the single most common cause of a character that " +
                    "jitters or will not move.",
                    MessageType.Warning);
            }
            else
            {
                foreach (var comp in killSwitch.TargetComponents)
                    TimeWarpEditorUI.Bullet(comp != null ? comp.GetType().Name : "<missing>");
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Auto-fill from this object"))
            {
                Undo.RecordObject(killSwitch, "Auto-fill kill switch");
                killSwitch.AutoFillComponents();
                EditorUtility.SetDirty(killSwitch);
            }

            if (GUILayout.Button("Select component"))
                Selection.activeGameObject = killSwitch.gameObject;

            EditorGUILayout.EndHorizontal();
            TimeWarpEditorUI.EndSection();
        }

        private static void Check(bool ok, string label, string help)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(ok ? "OK" : "--", GUILayout.Width(28));
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("        " + help, EditorStyles.wordWrappedMiniLabel);
        }

        // ==========================================
        // PAGE: ZONES
        // ==========================================
        private TimeEffectType _zoneEffect = TimeEffectType.SlowMotion;
        private float _zoneScale = 0.2f;
        private float _zoneRewindSpeed = 1f;
        private ExitCondition _zoneExit = ExitCondition.OnLeaveTrigger;
        private float _zoneDuration = 2f;
        private bool _zone2D = false;
        private string _zoneName = "Slow Motion Field";

        private void DrawZones()
        {
            TimeWarpEditorUI.PageTitle("Time Zones",
                "Regions of space where time behaves differently.",
                TimeWarpEditorUI.AccentViolet);

            TimeWarpEditorUI.Manual("zones.what", "What a Time Zone is",
                "A trigger collider that changes the LOCAL time scale of anything inside it. Walk a " +
                "crate into a slow-motion field and the crate slows down - the rest of the world does not.\n\n" +
                "Three flavours:\n\n" +
                "SLOW MOTION - objects inside run at a fraction of normal speed.\n\n" +
                "STASIS - objects inside are frozen solid at scale 0.\n\n" +
                "REWIND - objects inside continuously run backwards through their own history, without " +
                "you triggering anything globally.\n\n" +
                "The behaviour lives in a Time Profile asset, not on the zone. Several zones can share " +
                "one profile, so tuning the profile retunes every zone at once.",
                "A zone with no profile assigned does nothing at all - the gizmo turns red to warn you.");

            TimeWarpEditorUI.Manual("zones.filter", "Deciding what a zone affects",
                "The profile filters by LAYER, and optionally by TAG as well - the two are combined with " +
                "OR, so an object matches if either matches.\n\n" +
                "Layers are limited (32 in total, and most projects need them for the collision matrix), " +
                "so filtering by tag is often the better choice for gameplay categories.\n\n" +
                "One caveat: the scan that catches objects ALREADY STANDING inside the zone when the " +
                "scene loads is a physics query, and physics queries can only filter by layer. A " +
                "tag-only zone still catches anything that walks in, but not what was already there.",
                "Keep the layer mask on Everything and narrow with tags - you get both behaviours.");

            TimeWarpEditorUI.BeginSection("Create a zone");

            _zoneName = EditorGUILayout.TextField("Name", _zoneName);
            _zone2D = EditorGUILayout.Toggle(new GUIContent("2D zone", "Uses a BoxCollider2D instead of a BoxCollider."), _zone2D);

            EditorGUI.BeginChangeCheck();
            _zoneEffect = (TimeEffectType)EditorGUILayout.EnumPopup("Effect", _zoneEffect);
            if (EditorGUI.EndChangeCheck())
            {
                _zoneName = _zoneEffect == TimeEffectType.SlowMotion ? "Slow Motion Field"
                          : _zoneEffect == TimeEffectType.Stasis ? "Stasis Field"
                          : "Rewind Field";
            }

            switch (_zoneEffect)
            {
                case TimeEffectType.SlowMotion:
                    _zoneScale = EditorGUILayout.Slider(new GUIContent("Time scale inside",
                        "0.2 means objects run at one fifth speed."), _zoneScale, 0.01f, 1f);
                    break;

                case TimeEffectType.Stasis:
                    TimeWarpEditorUI.Note("Stasis always freezes at 0 - nothing to configure.");
                    break;

                case TimeEffectType.Rewind:
                    _zoneRewindSpeed = EditorGUILayout.Slider(new GUIContent("Rewind speed",
                        "How many seconds of history are consumed per real second."), _zoneRewindSpeed, 0.1f, 5f);
                    break;
            }

            _zoneExit = (ExitCondition)EditorGUILayout.EnumPopup(new GUIContent("Ends when",
                "On Leave Trigger - lasts while inside. Timed - fixed duration. Manual - until you say so."), _zoneExit);

            if (_zoneExit != ExitCondition.ManualOrExternal)
                _zoneDuration = EditorGUILayout.FloatField(new GUIContent("Duration (sec)",
                    _zoneExit == ExitCondition.Timed ? "How long the effect lasts." : "Grace period after leaving."), _zoneDuration);

            EditorGUILayout.Space(6);
            TimeWarpEditorUI.Note("Creates a Time Profile asset in Assets/TimeWarp Profiles/ plus a trigger object in the scene.");

            if (TimeWarpEditorUI.ActionButton("Create zone + profile", TimeWarpEditorUI.AccentViolet, 28))
                CreateZone();

            TimeWarpEditorUI.EndSection();
        }

        private void CreateZone()
        {
            const string folder = "Assets/TimeWarp Profiles";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets", "TimeWarp Profiles");

            var profile = ScriptableObject.CreateInstance<TimeProfile>();
            profile.EffectType = _zoneEffect;
            profile.TargetTimeScale = _zoneScale;
            profile.RewindSpeed = _zoneRewindSpeed;
            profile.ExitRule = _zoneExit;
            profile.Duration = _zoneDuration;

            string path = AssetDatabase.GenerateUniqueAssetPath(
                Path.Combine(folder, _zoneName.Replace(" ", "") + "Profile.asset"));

            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();

            var go = new GameObject(_zoneName);

            if (_zone2D)
            {
                var col = go.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
                col.size = Vector2.one * 5f;

                var zone = go.AddComponent<TimeWarpZone2D>();
                zone.Profile = profile;
            }
            else
            {
                var col = go.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.size = Vector3.one * 5f;

                var zone = go.AddComponent<TimeWarpZone>();
                zone.Profile = profile;
            }

            Undo.RegisterCreatedObjectUndo(go, "Create Time Zone");
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(profile);
        }

        // ==========================================
        // PAGE: KINETIC
        // ==========================================
        private void DrawKinetic()
        {
            TimeWarpEditorUI.PageTitle("Kinetic Force",
                "Freeze time, land twenty hits, unfreeze - and let all of them land at once.",
                TimeWarpEditorUI.AccentAmber);

            TimeWarpEditorUI.Manual("kinetic.what", "What the Kinetic Accumulator does",
                "While time is stopped a Rigidbody cannot be pushed - it is frozen, so any force you " +
                "apply is simply lost. That ruins the classic set piece where you stop time, attack " +
                "something repeatedly, and watch everything connect the instant time resumes.\n\n" +
                "The Kinetic Accumulator sits on the target and remembers every impact it receives " +
                "while frozen - both scripted hits and real collisions. When time resumes it releases " +
                "what it stored.\n\n" +
                "COMBINED IMPULSE - everything lands as one enormous hit.\n\n" +
                "SEQUENTIAL HITS - the stored hits fire one after another with a small delay, which " +
                "reads as a rapid combo rather than a single explosion.",
                "Max Force Cap is your safety rail - without it a long freeze can launch objects out of the level.");

            TimeWarpEditorUI.Manual("kinetic.zone", "Kinetic Zones are a different thing",
                "Do not confuse the two. A Kinetic ZONE derives an object's time scale from HOW FAST IT " +
                "IS MOVING: stand still and time crawls, sprint and time runs normally.\n\n" +
                "It is the mechanic from games where the world only moves when you do. It has no Time " +
                "Profile - the speed-to-time mapping is configured on the zone itself.",
                "Idle Time Scale near 0 with a low Speed Required makes the effect very obvious.");

            GameObject[] targets = GetValidTargets();

            TimeWarpEditorUI.BeginSection("Add an accumulator");

            if (targets.Length == 0)
            {
                EditorGUILayout.HelpBox("Select the objects that should store hits (targets, not the attacker).", MessageType.Info);
            }
            else
            {
                TimeWarpEditorUI.Note($"{targets.Length} object(s) selected.");

                if (TimeWarpEditorUI.ActionButton("Add Kinetic Accumulator", TimeWarpEditorUI.AccentAmber))
                {
                    int added = 0, skipped = 0;

                    foreach (var go in targets)
                    {
                        if (go.GetComponent<Rigidbody>() != null)
                        {
                            if (go.GetComponent<KineticAccumulator>() == null) Undo.AddComponent<KineticAccumulator>(go);
                            added++;
                        }
                        else if (go.GetComponent<Rigidbody2D>() != null)
                        {
                            if (go.GetComponent<KineticAccumulator2D>() == null) Undo.AddComponent<KineticAccumulator2D>(go);
                            added++;
                        }
                        else skipped++;
                    }

                    EditorUtility.DisplayDialog("TimeWarp",
                        $"Added to {added} object(s)." +
                        (skipped > 0 ? $"\n\nSkipped {skipped} without a Rigidbody - the accumulator needs one." : ""),
                        "OK");
                }
            }

            TimeWarpEditorUI.EndSection();

            TimeWarpEditorUI.BeginSection("Create a Kinetic Zone");
            TimeWarpEditorUI.Note("Time inside runs at a speed derived from how fast each object is moving.");

            if (TimeWarpEditorUI.ActionButton("Create Kinetic Zone", TimeWarpEditorUI.AccentAmber))
            {
                var go = new GameObject("Kinetic Zone");
                var col = go.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.size = Vector3.one * 10f;
                go.AddComponent<TimeKineticZone>();

                Undo.RegisterCreatedObjectUndo(go, "Create Kinetic Zone");
                Selection.activeGameObject = go;
            }

            TimeWarpEditorUI.EndSection();
        }

        // ==========================================
        // PAGE: INTERFACE
        // ==========================================
        private void DrawInterface()
        {
            TimeWarpEditorUI.PageTitle("Interface / HUD",
                "Showing the player how much rewind they have left.",
                TimeWarpEditorUI.AccentBlue);

            TimeWarpEditorUI.Manual("ui.what", "The rewind bar",
                "Time Warp Rewind Bar is a small component that drives an Image, a Slider or a " +
                "CanvasGroup from the engine's live state - the mana-bar readout most projects want " +
                "within an hour of getting the engine running.\n\n" +
                "Three sources:\n\n" +
                "ACTION TIME REMAINING - how much of a duration-limited rewind is left. Needs Use Max " +
                "Duration switched on in the manager.\n\n" +
                "REWIND DEPTH - how far into the past a tracked object currently is.\n\n" +
                "AVAILABLE HISTORY - how much recorded history that object still has.",
                "It smooths on unscaled time deliberately, so the bar keeps animating during a time stop.");

            TimeWarpEditorUI.Manual("ui.text", "Adding a text label",
                "The component has no TextMeshPro dependency on purpose - forcing that package on every " +
                "project would be rude.\n\n" +
                "Instead it pushes the formatted string through an event called On Label Changed. Drag " +
                "your TMP_Text (or a plain UI Text) into that event and pick the text setter. It fires " +
                "only when the visible string actually changes, so it does not allocate every frame.",
                "Label Format understands {0} current seconds, {1} total seconds, {2} percent.");

            TimeWarpEditorUI.BeginSection("Create the bar");
            EditorGUILayout.HelpBox(
                "The UI tools live in the Modules/UI folder so that projects without uGUI can delete " +
                "them cleanly.\n\nUse the menu: GameObject > TimeWarp > UI > Rewind Bar",
                MessageType.Info);

            if (GUILayout.Button("Open that menu item"))
            {
                if (!EditorApplication.ExecuteMenuItem("GameObject/TimeWarp/UI/Rewind Bar"))
                {
                    EditorUtility.DisplayDialog("TimeWarp",
                        "The UI module is not present in this project. It lives in Modules/UI and " +
                        "requires the uGUI package.", "OK");
                }
            }
            TimeWarpEditorUI.EndSection();
        }

        // ==========================================
        // PAGE: POST FX
        // ==========================================
        private void DrawPostFx()
        {
            TimeWarpEditorUI.PageTitle("Post-processing",
                "Making an anomaly look like an anomaly.",
                TimeWarpEditorUI.AccentViolet);

            TimeWarpEditorUI.Manual("fx.how", "How it works",
                "The engine does not create effects for you - it drives the WEIGHT of a post-processing " +
                "Volume you author yourself.\n\n" +
                "You build a Volume containing whatever sells the effect in your art style: chromatic " +
                "aberration, vignette, colour grading, lens distortion. Set its weight to 0 so it is " +
                "invisible normally. Time Warp Volume Controller then raises that weight when time " +
                "becomes strange and lowers it again afterwards, with separate targets for rewind, " +
                "time stop and slow motion.\n\n" +
                "The advantage is that the look stays entirely yours and works in URP and HDRP alike.",
                "Set the Volume to Global and its weight to 0 - the controller owns the weight from then on.");

            TimeWarpEditorUI.Manual("fx.tuning", "Sensible starting values",
                "REWIND at full strength - the player should be in no doubt that time is running backwards.\n\n" +
                "TIME STOP slightly weaker, around 0.8, so the frozen world stays readable.\n\n" +
                "SLOW MOTION much weaker, around 0.3-0.4, and scaled by how slow time actually is. Slow " +
                "motion often lasts a long time, and an aggressive effect becomes tiring fast.",
                "If the effect pops rather than blends, raise the entry and exit durations on the manager.");

            TimeWarpEditorUI.BeginSection("Create the volume");
            TimeWarpEditorUI.Note("Adds a global Volume with weight 0 and the controller that drives it. You then add the effect overrides you want.");

            if (TimeWarpEditorUI.ActionButton("Create Anomaly Volume", TimeWarpEditorUI.AccentViolet))
            {
                var go = new GameObject("TimeWarp Anomaly Volume");

                var volume = go.AddComponent<UnityEngine.Rendering.Volume>();
                volume.isGlobal = true;
                volume.weight = 0f;
                volume.priority = 10f;

                go.AddComponent<TimeWarpVolumeController>();

                Undo.RegisterCreatedObjectUndo(go, "Create Anomaly Volume");
                Selection.activeGameObject = go;

                EditorUtility.DisplayDialog("TimeWarp",
                    "Volume created.\n\nNext: assign a Volume Profile to it and add the overrides you " +
                    "want (chromatic aberration, vignette, colour grading). Leave the weight at 0 - the " +
                    "controller drives it.", "OK");
            }

            TimeWarpEditorUI.EndSection();
        }

        // ==========================================
        // PAGE: TROUBLESHOOT
        // ==========================================
        private void DrawTroubleshoot()
        {
            TimeWarpEditorUI.PageTitle("Troubleshooting",
                "The failure modes that produce no error message.",
                TimeWarpEditorUI.AccentAmber);

            if (TimeWarpEditorUI.ActionButton("Run scene validation", TimeWarpEditorUI.AccentGreen))
                TimeWarpMenuItems.ValidateScene();

            EditorGUILayout.Space(6);

            TimeWarpEditorUI.Manual("ts.nothing", "Nothing rewinds at all",
                "In order: is there a manager in the scene? Does the object have a Time Warp Identity? " +
                "Are any channels ticked on it - an Identity with everything off records nothing?\n\n" +
                "If all three look right, check the Console. A module that throws three times is " +
                "switched off automatically and says so, naming the object and the script.", null);

            TimeWarpEditorUI.Manual("ts.character", "The character jitters or will not move",
                "Your control script is fighting the playback. Add a Time Warp Component Manager and put " +
                "that script in its Target Components list - the Character page has a checklist.", null);

            TimeWarpEditorUI.Manual("ts.stuck", "Scripts stay disabled after a rewind",
                "Historically this happened when rewinding INSIDE a slow-motion zone: the engine only " +
                "re-enabled scripts when time returned to exactly normal, and leaving a rewind into a " +
                "slow-motion zone is not exactly normal. That is fixed - if you still see it, confirm " +
                "nothing else is disabling the script.", null);

            TimeWarpEditorUI.Manual("ts.pink", "Ghosts render bright pink",
                "Magenta is Unity's error shader. The Hologram Material on the Paradox module uses a " +
                "shader that does not work in your render pipeline - typically a Built-in shader in a " +
                "URP or HDRP project. The Console warns about this by name.", null);

            TimeWarpEditorUI.Manual("ts.teleport", "Rewind drags the object back through walls",
                "Teleporting, respawning or loading a save leaves the old route in the history buffer. " +
                "Call ResetHistory() on the object's Identity right after moving it and the buffer is " +
                "refilled with the new pose.", null);

            TimeWarpEditorUI.Manual("ts.postfx", "Post-processing weight changes but I see nothing",
                "The controller is almost never the problem - five separate setup conditions make a " +
                "volume invisible while its weight animates happily in the inspector.\n\n" +
                "1. The Volume has NO PROFILE assigned.\n" +
                "2. The profile is EMPTY - no Chromatic Aberration, Vignette, Color Adjustments etc.\n" +
                "3. The Volume is set to Local but has no Collider, so it covers no space.\n" +
                "4. POST PROCESSING IS UNCHECKED ON THE CAMERA. This is the most common one by far.\n" +
                "5. The camera's VOLUME MASK does not include the volume object's layer.\n\n" +
                "The controller now detects 1, 2 and 3 and names them in the Console. It cannot see the " +
                "camera settings without taking a hard URP dependency, so check those by hand.",
                "Also confirm post-processing is enabled on the Renderer asset itself, not just the camera.");

            TimeWarpEditorUI.Manual("ts.shader", "My shader keeps animating during a time stop",
                "It is using Unity's _Time, which is the real wall clock and ignores the engine " +
                "completely. Replace it with your own _LocalTime float fed by a script. Full recipe on " +
                "the Shaders page.", null);

            TimeWarpEditorUI.Manual("ts.enemy", "The enemy vibrates or slides during a rewind",
                "A NavMeshAgent owns the transform and is fighting the playback. Add Time NavMesh " +
                "Agent, enable the Navigation channel, and put your AI script - NOT the agent itself - " +
                "into the kill switch list.", null);

            TimeWarpEditorUI.Manual("ts.ammo", "Position rewinds but my ammo / health does not",
                "Only the channels you enable are recorded, and your own variables are not one of them " +
                "by default. Add Time Warp Bridge and bind the field. See the Logic page.", null);

            TimeWarpEditorUI.Manual("ts.2d", "Nothing happens on my 2D object",
                "Almost always a 3D component on a 2D object. Time Body needs a Rigidbody; Rigidbody2D " +
                "needs Time Body 2D. Zones need Time Warp Zone 2D with a Collider2D. None of these " +
                "throw - they simply record nothing.", null);

            TimeWarpEditorUI.Manual("ts.lurch", "The character lurches on the first frame after a rewind",
                "Your controller keeps velocity in its own field, which still holds the value from the " +
                "future. Bind that field through the Bridge with Record.", null);

            TimeWarpEditorUI.Manual("ts.particles", "Particles look wrong when rewinding",
                "The system was refused a cache slot and fell back to procedural playback, which is " +
                "approximate. Raise the particle RAM budget on the manager, or lower Max Particles on " +
                "the system. VFX Graph is always approximate by nature - use Shuriken for hero effects " +
                "that must reverse exactly.", null);

            TimeWarpEditorUI.Manual("ts.forcefield", "The force field does not react to anything",
                "It needs a trigger Collider to detect anything, and the incoming object must match " +
                "Impact Layers or Impact Tags. If the pattern is frozen but everything else works, the " +
                "material is not using the TimeWarp/Force Field shader - the component warns about this " +
                "in the Console.", null);

            TimeWarpEditorUI.Manual("ts.memory", "Memory use is too high",
                "Particles are by far the most expensive channel - turn them off on objects that do not " +
                "need them. Lower Max Record Time, or set a shorter Record Time Override on individual " +
                "objects. The manager's inspector shows exactly how much buffer each object allocates.", null);
        }


        // ==========================================
        // PAGE: USER MANUAL
        // ==========================================
        private void DrawManual()
        {
            TimeWarpEditorUI.PageTitle("User Manual",
                "The whole engine, explained. Every section is collapsible - open what you need.",
                TimeWarpEditorUI.AccentBlue);

            if (GUILayout.Button("Open the full written manual (DOCUMENTATION.md)"))
                OpenDocumentation();

            EditorGUILayout.Space(6);

            TimeWarpEditorUI.Manual("man.1", "1. The three pieces",
                "MANAGER - one per scene. Owns the clock, decides what a global rewind or slow motion " +
                "means, and drives every recorded object in one batched job.\n\n" +
                "IDENTITY - one per object that should react to time. Records a rolling history. An " +
                "object without one ignores time completely.\n\n" +
                "MODULES - small components that teach an Identity what else to record: physics, " +
                "animation, particles, audio, AI. Add only what an object actually needs.", null);

            TimeWarpEditorUI.Manual("man.2", "2. How time is calculated",
                "Effective scale = Local x Global.\n\n" +
                "GLOBAL comes from the manager - the rewind or slow motion you trigger from code.\n\n" +
                "LOCAL comes from Time Zones and affects only objects inside them.\n\n" +
                "So an object in a 0.2x zone during a 0.5x global slow motion runs at 0.1x. This is " +
                "also why 'rewinding while inside a slow-motion zone' is its own distinct state.\n\n" +
                "Unity's Time.timeScale is READ but never written. Your UI, menus and music keep " +
                "running at full speed during a time stop, and the engine can never leave your editor " +
                "in a modified state.", null);

            TimeWarpEditorUI.Manual("man.3", "3. The history buffer",
                "Each Identity allocates CaptureFramerate x MaxRecordTime frames. The size is rounded " +
                "UP to a power of two so indexing can use a bitmask instead of a modulo - fast, but it " +
                "means 720 requested frames allocate 1024. The manager inspector shows the real number " +
                "and warns when the rounding wastes a lot.\n\n" +
                "CaptureFramerate is overwritten at runtime from the physics timestep. Treat the field " +
                "as a preview; change Fixed Timestep in Project Settings to alter it.", null);

            TimeWarpEditorUI.Manual("man.4", "4. Channels",
                "A channel is one KIND of data an Identity records. Position, physics, animation, " +
                "particles, shaders, audio, navigation, your own variables, echoes.\n\n" +
                "Every channel costs memory and CPU, and Particles costs dramatically more than the " +
                "rest. Tick only what the object needs - the Identity inspector hides the settings for " +
                "channels you have switched off.", null);

            TimeWarpEditorUI.Manual("man.5", "5. Triggering anomalies from code",
                "TimeWarpManager.Instance.TriggerAction(GlobalAction.Rewind);\n" +
                "TimeWarpManager.Instance.TriggerAction(GlobalAction.SlowMotion, targetScale: 0.2f);\n" +
                "TimeWarpManager.Instance.TriggerAction(GlobalAction.TimeStop, duration: 10f);\n" +
                "TimeWarpManager.Instance.StopGlobalAction();\n\n" +
                "duration, delay and targetScale all default to -1, meaning 'use the inspector value'.", null);

            TimeWarpEditorUI.Manual("man.6", "6. Reading state",
                "mgr.GlobalTimeState      Normal, PreAction, Rewinding, TimeStop, SlowMotion, PostAction\n" +
                "mgr.CurrentGlobalTimeScale\n" +
                "mgr.ActionProgress01     0..1, or 0 when unlimited\n" +
                "mgr.ActionTimeRemaining  seconds, or -1 when unlimited\n\n" +
                "identity.IsRewinding\n" +
                "identity.EffectiveTimeScale\n" +
                "identity.FractionalOffset   frames into the past\n" +
                "identity.ResetHistory()     after a teleport or respawn", null);

            TimeWarpEditorUI.Manual("man.7", "7. Writing your own module",
                "Implement ITimeWarpModule on a component sitting next to an Identity and it is ticked " +
                "automatically:\n\n" +
                "public void OnWarpFixedUpdate() { /* record */ }\n" +
                "public void OnWarpUpdate()      { /* apply  */ }\n\n" +
                "Exceptions are caught and counted. A module that throws three times is disabled for " +
                "the session and logs which object and script were responsible - one bad module can " +
                "never stall the rest of the scene.", null);

            TimeWarpEditorUI.Manual("man.8", "8. Performance in one paragraph",
                "Transforms run through Burst jobs over a TransformAccessArray; history lives in native " +
                "circular buffers. The scaling limit is the NUMBER of recorded objects, not the cost of " +
                "any single one.\n\n" +
                "Watch TimeWarp.WaitForTransformJob in the Profiler. If that is high, the job could not " +
                "finish in time - a different problem from slow user code.", null);
        }

        private static void OpenDocumentation()
        {
            foreach (string guid in AssetDatabase.FindAssets("DOCUMENTATION t:TextAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith("DOCUMENTATION.md")) continue;

                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (asset != null) { AssetDatabase.OpenAsset(asset); return; }
            }

            EditorUtility.DisplayDialog("TimeWarp",
                "DOCUMENTATION.md was not found. It ships in the Time Paradox Engine folder.", "OK");
        }

        // ==========================================
        // PAGE: CONTROLLERS
        // ==========================================
        private void DrawControllers()
        {
            TimeWarpEditorUI.PageTitle("Character controllers",
                "What the engine supports out of the box, and how to adapt your own.",
                TimeWarpEditorUI.AccentGreen);

            TimeWarpEditorUI.Manual("ctrl.principle", "The one principle",
                "The engine does not care WHICH controller you use. It cares about one thing only:\n\n" +
                "    while time is abnormal, nothing except the engine may write to the transform.\n\n" +
                "Every recipe below is just a different way of guaranteeing that. Once you understand " +
                "this, adapting any controller - including one you wrote yourself - is a five minute job.",
                "If your character jitters during a rewind, something is still writing. That is always the cause.");

            TimeWarpEditorUI.Manual("ctrl.rb", "Rigidbody controllers",
                "The easiest case, and the best supported.\n\n" +
                "Setup: Identity with Physics (3D) + Time Body, and your movement script in the " +
                "Component Manager's list.\n\n" +
                "Time Body handles the hard part - it makes the body kinematic during playback and " +
                "hands momentum back when time resumes, so the character does not simply drop when a " +
                "rewind ends mid-jump.", null);

            TimeWarpEditorUI.Manual("ctrl.cc", "CharacterController (Unity's built-in)",
                "CharacterController is NOT a Rigidbody. It ignores physics and writes the transform " +
                "directly through Move(), which means it fights playback harder than anything else.\n\n" +
                "Setup: Identity WITHOUT the Physics channel - position comes from the Transform " +
                "channel alone - and the controller script in the Component Manager's list.\n\n" +
                "IMPORTANT: disable your movement SCRIPT, not the CharacterController component " +
                "itself. Disabling the component makes it forget its grounded state and the character " +
                "pops when it comes back.", "This also covers Unity's Starter Assets and most asset-store FPS kits.");

            TimeWarpEditorUI.Manual("ctrl.root", "Root motion / animation-driven",
                "Here the Animator writes the transform, so switching your script off is not enough - " +
                "the Animator keeps driving movement.\n\n" +
                "Setup: enable the Animation channel and add Time Animator. It takes control of the " +
                "Animator during anomalies, so root motion stops being applied and animation rewinds " +
                "with everything else.", null);

            TimeWarpEditorUI.Manual("ctrl.custom", "Adapting YOUR OWN controller",
                "Two options, in order of preference.\n\n" +
                "OPTION 1 - the kill switch. Add Time Warp Component Manager and drop your controller " +
                "into Target Components. Zero code. Works for the large majority of controllers.\n\n" +
                "OPTION 2 - the listener, when you need the script to keep running (to play an idle " +
                "animation, hold a camera, keep UI alive) but stop MOVING. Implement " +
                "ITimeWarpStateListener:\n\n" +
                "  OnTimeWarpRewindStart()   stop writing to the transform now\n" +
                "  OnTimeWarpTimeStopStart()\n" +
                "  OnTimeWarpSlowMoStart(scale)\n" +
                "  OnTimeWarpAnomalyUpdate(scale, rewindDeltaTime)\n" +
                "  OnTimeWarpResume()        safe to move again\n\n" +
                "Guard your movement with a simple bool set by those callbacks and you are done.",
                "rewindDeltaTime is NEGATIVE while rewinding - it is real seconds of history being consumed.");

            TimeWarpEditorUI.Manual("ctrl.velocity", "Keeping your own velocity in sync",
                "Controllers that track velocity in a private field have a specific failure: after a " +
                "rewind the character is in the past but its velocity variable still holds the value " +
                "from the future, so it lurches on the first frame back.\n\n" +
                "Fix it with the Bridge - bind that field with Record so it rewinds along with " +
                "everything else. See the Logic page.", null);

            TimeWarpEditorUI.BeginSection("Checklist");
            TimeWarpEditorUI.Bullet("Rigidbody controller: Physics channel + Time Body + kill switch");
            TimeWarpEditorUI.Bullet("CharacterController: Transform channel only + kill switch on your SCRIPT");
            TimeWarpEditorUI.Bullet("Root motion: Animation channel + Time Animator");
            TimeWarpEditorUI.Bullet("Custom velocity fields: bind them through the Bridge");
            TimeWarpEditorUI.EndSection();
        }

        // ==========================================
        // PAGE: 2D
        // ==========================================
        private void DrawTwoD()
        {
            TimeWarpEditorUI.PageTitle("2D",
                "Everything works in 2D - the components just have different names.",
                TimeWarpEditorUI.AccentBlue);

            TimeWarpEditorUI.Manual("2d.map", "The 3D to 2D map",
                "Rigidbody          -> Rigidbody2D\n" +
                "Time Body          -> Time Body 2D\n" +
                "Physics (3D)       -> Physics (2D) channel\n" +
                "Time Warp Zone     -> Time Warp Zone 2D\n" +
                "BoxCollider        -> BoxCollider2D (still Is Trigger)\n" +
                "Kinetic Accumulator-> Kinetic Accumulator 2D\n\n" +
                "The manager, Identity, channels, echoes, zones, profiles, the Bridge and the HUD are " +
                "all shared - there is no separate 2D engine to learn.",
                "Mixing them is the classic mistake: a Rigidbody2D with a 3D Time Body records nothing.");

            TimeWarpEditorUI.Manual("2d.gotchas", "2D-specific gotchas",
                "TIME BODY 2D needs Rigidbody2D. Using the 3D Time Body on a 2D object fails silently " +
                "- nothing throws, the object simply never records velocity.\n\n" +
                "ZONES need a Collider2D with Is Trigger on, and a Time Warp Zone 2D component. The 3D " +
                "zone will never fire OnTriggerEnter2D.\n\n" +
                "SORTING LAYERS are untouched by the engine. Rewinding does not change draw order.\n\n" +
                "Z POSITION is still recorded by the Transform channel, which is what you want for " +
                "parallax layers.", null);

            GameObject[] targets = GetValidTargets();

            TimeWarpEditorUI.BeginSection("Set up a 2D object");

            if (targets.Length == 0)
            {
                EditorGUILayout.HelpBox("Select a 2D object in the scene.", MessageType.Info);
            }
            else
            {
                GameObject go = targets[0];
                bool has2D = go.GetComponent<Rigidbody2D>() != null;
                bool has3D = go.GetComponent<Rigidbody>() != null;

                if (has3D && !has2D)
                    EditorGUILayout.HelpBox(
                        $"\"{go.name}\" has a 3D Rigidbody. Use the Objects page instead - this page " +
                        $"is for Rigidbody2D objects.", MessageType.Warning);

                TimeWarpEditorUI.Note(has2D
                    ? "Rigidbody2D found - physics will be recorded."
                    : "No Rigidbody2D - only the Transform will be recorded, which is fine for " +
                      "kinematic platforms and decoration.");

                if (TimeWarpEditorUI.ActionButton("Set up as 2D character", TimeWarpEditorUI.AccentGreen))
                    foreach (var g in targets) ApplyRole(g, ObjectRole.Character2D, false);

                if (GUILayout.Button("Set up as 2D physics object"))
                    foreach (var g in targets) ApplyRole(g, ObjectRole.PhysicsProp, false);
            }

            TimeWarpEditorUI.EndSection();

            TimeWarpEditorUI.BeginSection("Create a 2D Time Zone");
            TimeWarpEditorUI.Note("Opens the Time Zones page with the 2D option preselected.");

            if (GUILayout.Button("Go to Time Zones (2D)"))
            {
                _zone2D = true;
                _page = Page.Zones;
            }

            TimeWarpEditorUI.EndSection();
        }

        // ==========================================
        // PAGE: VFX & PARTICLES
        // ==========================================
        private void DrawParticles()
        {
            TimeWarpEditorUI.PageTitle("VFX & Particles",
                "The most expensive thing to rewind, and the one with real trade-offs.",
                TimeWarpEditorUI.AccentAmber);

            TimeWarpEditorUI.Manual("fx.modes", "Two ways to rewind particles",
                "CACHED - the full state of every live particle is stored for every frame of history. " +
                "Exact, reversible, and by far the most memory-hungry thing in the engine.\n\n" +
                "PROCEDURAL - nothing is stored; the system is re-simulated backwards from its own " +
                "parameters. Almost free, but approximate - anything driven by collision or randomness " +
                "will not retrace its exact path.\n\n" +
                "You do not choose per system. The manager holds a global RAM BUDGET, and systems are " +
                "granted caching until it runs out; the rest fall back to procedural automatically. " +
                "That is why raising the budget makes some effects suddenly look right.",
                "A refused system does not warn. If smoke looks wrong during a rewind, raise the budget first.");

            TimeWarpEditorUI.Manual("fx.cost", "Keeping the cost sane",
                "The cost is (particles x bytes per particle x frames of history). The three levers:\n\n" +
                "1. Turn the Particles channel OFF on systems nobody watches during a rewind. Cheapest " +
                "win available and it costs nothing visually.\n\n" +
                "2. Lower Max Particles on the system itself - it caps the worst case directly.\n\n" +
                "3. Set a short Record Time Override on the Identity. A spark burst does not need ten " +
                "seconds of history.", null);

            TimeWarpEditorUI.Manual("fx.vfxgraph", "VFX Graph is different",
                "Shuriken can be rewound particle by particle. VFX Graph runs on the GPU and its state " +
                "is not readable that way, so Time VFX re-simulates it instead.\n\n" +
                "MAX VFX REWIND STEPS on the manager caps how far that re-simulation goes in a single " +
                "frame, so a long rewind cannot stall your frame while the effect catches up.\n\n" +
                "Practical consequence: VFX Graph effects will not retrace an identical path during a " +
                "rewind. For hero effects that must reverse exactly, use Shuriken.", null);

            GameObject[] targets = GetValidTargets();

            TimeWarpEditorUI.BeginSection("Set up an effect");

            if (targets.Length == 0)
            {
                EditorGUILayout.HelpBox("Select an object with a Particle System or Visual Effect.", MessageType.Info);
            }
            else
            {
                GameObject go = targets[0];
                bool shuriken = go.GetComponentInChildren<ParticleSystem>(true) != null;

                TimeWarpEditorUI.Bullet(shuriken ? "Particle System found" : "No Particle System found");

                if (TimeWarpEditorUI.ActionButton("Set up as VFX / decoration", TimeWarpEditorUI.AccentAmber))
                    foreach (var g in targets) ApplyRole(g, ObjectRole.Decoration, false);
            }

            TimeWarpEditorUI.EndSection();

            TimeWarpEditorUI.BeginSection("Budget");
            var mgr = Object.FindAnyObjectByType<TimeWarpManager>();

            if (mgr != null)
            {
                TimeWarpEditorUI.Note($"Current particle RAM budget: {mgr.MaxParticleRamBudgetMB} MB.");
                if (GUILayout.Button("Select manager to change it")) Selection.activeGameObject = mgr.gameObject;
            }
            else TimeWarpEditorUI.Note("No manager in the scene.");

            TimeWarpEditorUI.EndSection();
        }

        // ==========================================
        // PAGE: NAVMESH
        // ==========================================
        private void DrawNavMesh()
        {
            TimeWarpEditorUI.PageTitle("NavMesh / AI",
                "Agents fight playback harder than almost anything else.",
                TimeWarpEditorUI.AccentGreen);

            TimeWarpEditorUI.Manual("nav.problem", "Why an agent needs special handling",
                "A NavMeshAgent does not merely read the transform - it OWNS it. Every frame it moves " +
                "the object toward its destination and overwrites whatever else wrote there.\n\n" +
                "During a rewind that produces a very specific bug: the agent drags the enemy forward " +
                "while the engine drags it backward, and the enemy vibrates in place or slides off the " +
                "recorded path entirely.\n\n" +
                "Time NavMesh Agent solves it by suspending the agent for the duration of the anomaly " +
                "and restoring it - with its destination intact - afterwards.",
                "This is why the Enemy role adds it automatically when it finds an agent.");

            TimeWarpEditorUI.Manual("nav.setup", "Correct setup",
                "1. Identity with the Navigation channel enabled.\n" +
                "2. Time NavMesh Agent on the same object.\n" +
                "3. Your AI/behaviour script in the Component Manager's list - otherwise it will keep " +
                "issuing SetDestination calls during the rewind.\n\n" +
                "Do NOT put the NavMeshAgent itself in the kill switch list. Disabling the agent makes " +
                "it lose its position on the navmesh, and re-enabling it can warp the enemy.", null);

            TimeWarpEditorUI.Manual("nav.offmesh", "Edge cases worth knowing",
                "OFF-MESH LINKS: an agent rewound while mid-jump can land off the navmesh. Call " +
                "Warp() on the agent in your OnTimeWarpResume() if you use links heavily.\n\n" +
                "SPAWNED AGENTS: an enemy spawned during the rewound window will be removed by the " +
                "rewind. Use Time Warp Lifecycle if you want spawn and despawn to reverse cleanly.\n\n" +
                "AGENT VELOCITY: the agent recalculates its own velocity on resume, so you do not need " +
                "to record it through the Bridge.", null);

            GameObject[] targets = GetValidTargets();

            TimeWarpEditorUI.BeginSection("Set up an AI object");

            if (targets.Length == 0)
            {
                EditorGUILayout.HelpBox("Select an enemy or AI object.", MessageType.Info);
            }
            else
            {
                GameObject go = targets[0];
                bool hasAgent = go.GetComponent<UnityEngine.AI.NavMeshAgent>() != null;

                TimeWarpEditorUI.Bullet(hasAgent
                    ? "NavMeshAgent found - Time NavMesh Agent will be added."
                    : "No NavMeshAgent on this object. The Enemy setup still works, it just skips the agent module.");

                if (TimeWarpEditorUI.ActionButton("Set up as Enemy / AI", TimeWarpEditorUI.AccentGreen))
                    foreach (var g in targets) ApplyRole(g, ObjectRole.EnemyAI, false);
            }

            TimeWarpEditorUI.EndSection();
        }

        // ==========================================
        // PAGE: SHADERS
        // ==========================================
        private void DrawShaders()
        {
            TimeWarpEditorUI.PageTitle("Shaders",
                "Making your own shaders obey the engine.",
                TimeWarpEditorUI.AccentViolet);

            TimeWarpEditorUI.Manual("sh.rule", "The single rule",
                "NEVER animate on _Time.\n\n" +
                "That is the whole problem in one line. Unity's _Time is the real wall clock. A shader " +
                "using it keeps scrolling at full speed while the world hangs frozen in mid-air, and " +
                "the illusion dies instantly.\n\n" +
                "Instead, expose your own float - call it _LocalTime - and let a script feed it. The " +
                "script advances it by the object's effective time scale, and backwards during a " +
                "rewind. Everything the shader animates then follows the engine for free.",
                "Anything derived from _Time is affected: scrolling UVs, sin() pulses, dissolve, flipbooks.");

            TimeWarpEditorUI.Manual("sh.convert", "Converting an existing shader - three steps",
                "STEP 1. Add the property.\n" +
                "    _LocalTime (\"Local Time\", Float) = 0\n" +
                "and declare it in your CBUFFER alongside the rest.\n\n" +
                "STEP 2. Replace every use of time.\n" +
                "    _Time.y   ->  _LocalTime\n" +
                "    _Time.x   ->  _LocalTime * 0.05\n" +
                "    _SinTime  ->  sin(_LocalTime)\n\n" +
                "STEP 3. Feed it. Either add Time Shader to the object and bind _LocalTime as a " +
                "recorded property, or copy the pattern from TimeWarpForceField.cs, which computes it " +
                "from the Identity's rewind depth.", null);

            TimeWarpEditorUI.Manual("sh.dont", "What NOT to use",
                "_Time, _SinTime, _CosTime, _DeltaTime - all wall clock, all ignore the engine.\n\n" +
                "Time.timeScale in a script feeding a shader. The engine never sets it, so it will " +
                "always read 1 and tell you nothing.\n\n" +
                "Material.SetFloat on a SHARED material when many objects use it - every object would " +
                "share one clock. Use MaterialPropertyBlock instead, which is per-renderer.\n\n" +
                "Baked-in animation such as a flipbook driven purely by _Time inside a Sub Graph - it " +
                "cannot be intercepted from outside.", null);

            TimeWarpEditorUI.Manual("sh.graph", "Shader Graph",
                "Same rule, different UI.\n\n" +
                "1. Delete the Time node.\n" +
                "2. Add a Float property named exactly _LocalTime and expose it.\n" +
                "3. Feed it wherever Time.Time used to go.\n\n" +
                "Then drive it from a script with a MaterialPropertyBlock. Note that Shader Graph " +
                "declares exposed properties in UnityPerMaterial automatically, which is what you want.",
                "There is a Force Field Graph in Demo/ShaderGraphs you can compare against.");

            TimeWarpEditorUI.Manual("sh.forcefield", "The Force Field shader",
                "TimeWarp/Force Field ships as a worked example of all of the above, and as a usable " +
                "effect in its own right.\n\n" +
                "It reacts to the engine: the pattern crawls in slow motion, freezes in a time stop and " +
                "un-scrolls during a rewind, and the whole field shifts colour while time reverses.\n\n" +
                "It reacts to the world: impacts send expanding rings from the point of contact, " +
                "objects standing inside it get a pulsing halo that follows them, and repeated hits " +
                "push it toward overload - it swells, brightens, and fires an event you can hook.\n\n" +
                "Because impacts are stored with the time they happened at, rewinding makes them " +
                "un-happen in reverse order. That needed no extra code - a hit timestamped in the " +
                "future simply is not drawn.",
                "Give the object a trigger collider or nothing can be detected entering it.");

            TimeWarpEditorUI.BeginSection("Force field setup");
            TimeWarpEditorUI.Bullet("Material using TimeWarp/Force Field on a sphere or dome");
            TimeWarpEditorUI.Bullet("Add Time Warp Force Field to the same object");
            TimeWarpEditorUI.Bullet("Add a Time Warp Identity so it obeys the engine");
            TimeWarpEditorUI.Bullet("Add a trigger Collider so it can detect impacts and intruders");
            TimeWarpEditorUI.Bullet("Set Impact Layers / Impact Tags to what should affect it");
            TimeWarpEditorUI.EndSection();
        }

        // ==========================================
        // PAGE: LOGIC / BRIDGE
        // ==========================================
        private void DrawLogic()
        {
            TimeWarpEditorUI.PageTitle("Logic, UI & Custom Code",
                "Rewinding the numbers inside your own scripts.",
                TimeWarpEditorUI.AccentBlue);

            TimeWarpEditorUI.Manual("bridge.what", "What the Bridge is for",
                "Rewinding an object's POSITION is only half the job. If the player rewinds past the " +
                "moment they fired, the bullet comes back - but the ammo counter does not, unless you " +
                "tell it to.\n\n" +
                "Time Warp Bridge binds float fields INSIDE YOUR OWN SCRIPTS by name: ammo, health, " +
                "charge, cooldown, light intensity, anything. No adapter, no interface, no changes to " +
                "your script at all.\n\n" +
                "It resolves members using compiled expression trees cached per type - so it is not " +
                "doing reflection every frame.",
                "It reads private fields too, so you do not have to make anything public.");

            TimeWarpEditorUI.Manual("bridge.modes", "The three modes",
                "RECORD ONLY - the value is stored every frame and replayed during a rewind. Use for " +
                "state: ammo, health, score, charge.\n\n" +
                "MATH ONLY - the value is multiplied by the time scale, not recorded. Use for rates: a " +
                "cooldown that should tick slower in slow motion, a fuel drain, a spin speed.\n\n" +
                "MATH AND RECORD - both. Use for a value that both ticks over time AND must rewind " +
                "correctly, which is most timers.", null);

            TimeWarpEditorUI.Manual("bridge.velocity", "The classic bug it fixes",
                "A controller that tracks velocity in a private field will lurch on the first frame " +
                "after a rewind: the character is in the past, but the velocity variable still holds " +
                "the value from the future.\n\n" +
                "Bind that field with Record and the problem disappears.", null);

            TimeWarpEditorUI.Manual("bridge.events", "Reacting instead of recording",
                "For things that are not floats - inventories, state machines, spawned objects - use " +
                "events rather than the Bridge.\n\n" +
                "Identity events: OnLocalRewindStart/Stop, OnLocalTimeStopStart/Stop, OnLocalSlowMoStart/Stop.\n\n" +
                "Or implement ITimeWarpStateListener for start / update / resume callbacks with the " +
                "current time scale and the rewind delta.\n\n" +
                "Everything hooked there runs on unscaled time, so it still works during a time stop - " +
                "which is exactly what you want for UI.", null);

            TimeWarpEditorUI.Manual("bridge.il2cpp", "IL2CPP warning - read before shipping",
                "Because the Bridge finds members by NAME, Managed Code Stripping cannot see that they " +
                "are used and may delete them from the build.\n\n" +
                "The result appears ONLY in the final build, never in the editor: the Bridge silently " +
                "finds nothing and your value never changes.\n\n" +
                "Fix: mark the field [Preserve] (using UnityEngine.Scripting), or list its type in a " +
                "link.xml. Verify with a development IL2CPP build - the editor uses Mono and never strips.",
                "This applies to YOUR scripts, so the engine cannot annotate them for you.");

            GameObject[] targets = GetValidTargets();

            TimeWarpEditorUI.BeginSection("Add the Bridge");

            if (targets.Length == 0)
            {
                EditorGUILayout.HelpBox("Select the object whose script variables should rewind.", MessageType.Info);
            }
            else if (TimeWarpEditorUI.ActionButton("Add Time Warp Bridge", TimeWarpEditorUI.AccentBlue))
            {
                foreach (var go in targets)
                {
                    var identity = Ensure<TimeWarpIdentity>(go);
                    Ensure<TimeWarpBridge>(go);

                    Undo.RecordObject(identity, "Add Bridge");
                    identity.Channels |= TimeChannels.Custom;
                    EditorUtility.SetDirty(identity);
                    identity.RefreshModules();
                }

                EditorUtility.DisplayDialog("TimeWarp",
                    "Bridge added and the Custom channel enabled.\n\n" +
                    "Now open the Identity's Variables tab and add a binding: pick the component, type " +
                    "the field name exactly as written in your script, and choose a mode.", "OK");
            }

            TimeWarpEditorUI.EndSection();
        }

        // ==========================================
        // PAGE: EXTRAS
        // ==========================================
        private void DrawExtras()
        {
            TimeWarpEditorUI.PageTitle("Extras & Edge Cases",
                "Things the engine can do that are not obvious from the component list.",
                TimeWarpEditorUI.AccentAmber);

            TimeWarpEditorUI.Manual("x.haptics", "Haptics and controller rumble",
                "Drive rumble from the engine's state rather than from your gameplay code, so it " +
                "automatically matches whatever time is doing.\n\n" +
                "Hook OnGlobalRewindStart / OnGlobalRewindStop on the manager for a simple on/off " +
                "rumble. For something that breathes with the effect, read " +
                "TimeWarpManager.Instance.ActionProgress01 each frame and use it as the motor " +
                "amplitude - the rumble then ramps exactly with your entry and exit curves.\n\n" +
                "For per-object feedback - the player being hit while inside a slow-motion zone - use " +
                "the Identity's own events instead, since those fire per object.\n\n" +
                "With the Input System: Gamepad.current.SetMotorSpeeds(low, high). Remember to call " +
                "ResetHaptics() on resume, or a rumble started during a time stop will never stop.",
                "Drive amplitude from 1 - EffectiveTimeScale and slow motion rumbles harder the slower it gets.");

            TimeWarpEditorUI.Manual("x.multipart", "One hit affecting a whole multi-part object",
                "The situation: a machine or a statue built from twenty pieces, each with its own " +
                "Identity. You shoot ONE piece with a rewind bullet and want the WHOLE thing to rewind.\n\n" +
                "There are three ways, in increasing order of control.\n\n" +
                "1. GROUP SYNC - add Time Warp Group Sync to the parent. Anything you do to one member " +
                "propagates to the rest. This is the intended solution and needs no code.\n\n" +
                "2. PERMISSION ZONE - put the object inside one, and set the members' Action Filters to " +
                "require that zone. The zone then acts as the switch for the whole group.\n\n" +
                "3. BY HAND - on the hit piece, walk the parent and trigger each Identity yourself. " +
                "Most control, most code:\n\n" +
                "    foreach (var id in transform.root.GetComponentsInChildren<TimeWarpIdentity>())\n" +
                "        id.LocalTimeScale = -1f;\n\n" +
                "A negative local scale IS a local rewind - that is how rewind zones work internally.",
                "Group Sync also keeps the pieces from drifting apart when they rewind at slightly different rates.");

            TimeWarpEditorUI.Manual("x.bullets", "Rewind bullets and time weapons",
                "The projectile carries the effect instead of the player triggering it globally.\n\n" +
                "Simplest version: on impact, set LocalTimeScale to a negative value on whatever it " +
                "hit, then back to 1 after a delay. The target rewinds; nothing else does.\n\n" +
                "Stasis rounds: set LocalTimeScale to 0 instead.\n\n" +
                "Area effect: spawn a short-lived prefab carrying a Time Warp Zone with a Rewind " +
                "profile and a Timed exit rule. Everything caught in the blast rewinds and the zone " +
                "deletes itself. This is usually the best-looking option because the zone gizmo, the " +
                "filtering and the exit blending all come for free.", null);

            TimeWarpEditorUI.Manual("x.selective", "Making things immune to time",
                "Action Filters on the Identity restrict which anomalies affect an object - so a boss " +
                "can be immune to rewind but not to slow motion.\n\n" +
                "An object with NO Identity at all is completely untouched by everything, which is the " +
                "cheapest kind of immunity and costs zero memory.\n\n" +
                "For a temporary shield, toggle the Identity component off - Free Memory On Disable " +
                "controls whether the history is dropped or kept while it is disabled.", null);

            TimeWarpEditorUI.Manual("x.audio", "Audio during anomalies",
                "Time Audio pitches an AudioSource with the time scale, which is the effect people " +
                "expect - sounds drop in pitch as time slows.\n\n" +
                "What it deliberately does NOT do is reverse audio during a rewind, because reversing " +
                "a live stream is not something Unity supports cleanly. The usual trick is to hook " +
                "OnGlobalRewindStart, mute the normal mix and play a dedicated reversed loop.\n\n" +
                "Because the engine never touches Time.timeScale, your music and UI sounds keep playing " +
                "normally for free - only the sources you have given a Time Audio component change.", null);

            TimeWarpEditorUI.Manual("x.cutscene", "Slowing time without an ability",
                "Nothing forces anomalies to be player abilities. TriggerAction from a cutscene, a " +
                "trigger volume, a death, a boss phase change - a brief TimeStop on a kill is a very " +
                "cheap way to add impact.\n\n" +
                "For a permanently slowed object with no anomaly at all, just set its LocalTimeScale " +
                "and leave it. Manager Mode does not even need to be Global for that.", null);
        }

        // ==========================================
        // SETUP LOGIC
        // ==========================================
        private static List<string> BuildPlanFor(GameObject go, ObjectRole role, bool addEchoes)
        {
            var lines = new List<string>();

            lines.Add(go.GetComponent<TimeWarpIdentity>() != null
                ? "Time Warp Identity - already present, settings updated"
                : "Add Time Warp Identity (the core recorder)");

            bool has3D = go.GetComponent<Rigidbody>() != null;
            bool has2D = go.GetComponent<Rigidbody2D>() != null;
            bool hasAnimator = go.GetComponentInChildren<Animator>(true) != null;
            bool hasAudio = go.GetComponentInChildren<AudioSource>(true) != null;
            bool hasParticles = go.GetComponentInChildren<ParticleSystem>(true) != null;
            bool hasRenderer = go.GetComponentInChildren<Renderer>(true) != null;
            bool hasAgent = go.GetComponent<UnityEngine.AI.NavMeshAgent>() != null;

            switch (role)
            {
                case ObjectRole.PlayerCharacter:
                case ObjectRole.EnemyAI:
                    lines.Add(has3D ? "Add Time Body (physics recording)"
                                    : "No Rigidbody - skipping Time Body, movement recorded from the Transform");
                    if (hasAnimator) lines.Add("Add Time Animator (rewinds animation)");
                    if (role == ObjectRole.EnemyAI && hasAgent) lines.Add("Add Time NavMesh Agent");
                    lines.Add("Add Component Manager and auto-fill it with your scripts");
                    break;

                case ObjectRole.Character2D:
                    lines.Add(has2D ? "Add Time Body 2D" : "No Rigidbody2D - skipping Time Body 2D");
                    if (hasAnimator) lines.Add("Add Time Animator");
                    lines.Add("Add Component Manager and auto-fill it with your scripts");
                    break;

                case ObjectRole.PhysicsProp:
                case ObjectRole.Projectile:
                    if (has3D) lines.Add("Add Time Body (physics recording)");
                    else if (has2D) lines.Add("Add Time Body 2D");
                    else lines.Add("No Rigidbody - only the Transform will be recorded");
                    break;

                case ObjectRole.Decoration:
                    if (hasParticles) lines.Add("Add Time Particles");
                    if (hasRenderer) lines.Add("Add Time Shader");
                    if (!hasParticles && !hasRenderer) lines.Add("Nothing visual found - only the Transform will be recorded");
                    break;
            }

            bool isCharacter = role == ObjectRole.PlayerCharacter || role == ObjectRole.EnemyAI || role == ObjectRole.Character2D;
            if (hasAudio && isCharacter) lines.Add("Add Time Audio (pitch follows time scale)");

            if (addEchoes || role == ObjectRole.Projectile) lines.Add("Add Time Paradox Module (leaves ghosts)");

            return lines;
        }

        private List<string> BuildPlan(GameObject go) => BuildPlanFor(go, _role, _addEchoes);

        private static void ApplyRole(GameObject go, ObjectRole role, bool addEchoes)
        {
            Undo.SetCurrentGroupName("TimeWarp Setup: " + go.name);
            int group = Undo.GetCurrentGroup();

            var identity = Ensure<TimeWarpIdentity>(go);
            TimeChannels channels = TimeChannels.Transform;

            bool has3D = go.GetComponent<Rigidbody>() != null;
            bool has2D = go.GetComponent<Rigidbody2D>() != null;
            bool hasAnimator = go.GetComponentInChildren<Animator>(true) != null;
            bool hasAudio = go.GetComponentInChildren<AudioSource>(true) != null;
            bool hasParticles = go.GetComponentInChildren<ParticleSystem>(true) != null;
            bool hasRenderer = go.GetComponentInChildren<Renderer>(true) != null;
            bool hasAgent = go.GetComponent<UnityEngine.AI.NavMeshAgent>() != null;

            switch (role)
            {
                case ObjectRole.PlayerCharacter:
                case ObjectRole.EnemyAI:
                    if (has3D) { Ensure<TimeBody>(go); channels |= TimeChannels.Velocity; }
                    if (hasAnimator) { Ensure<TimeAnimator>(go); channels |= TimeChannels.Animation; }
                    if (role == ObjectRole.EnemyAI && hasAgent) { Ensure<TimeNavMeshAgent>(go); channels |= TimeChannels.Navigation; }
                    ConfigureKillSwitch(go);
                    break;

                case ObjectRole.Character2D:
                    if (has2D) { Ensure<TimeBody2D>(go); channels |= TimeChannels.Velocity2D; }
                    if (hasAnimator) { Ensure<TimeAnimator>(go); channels |= TimeChannels.Animation; }
                    ConfigureKillSwitch(go);
                    break;

                case ObjectRole.PhysicsProp:
                case ObjectRole.Projectile:
                    if (has3D) { Ensure<TimeBody>(go); channels |= TimeChannels.Velocity; }
                    else if (has2D) { Ensure<TimeBody2D>(go); channels |= TimeChannels.Velocity2D; }
                    break;

                case ObjectRole.Decoration:
                    if (hasParticles) { Ensure<TimeParticles>(go); channels |= TimeChannels.Particles; }
                    if (hasRenderer) { Ensure<TimeShader>(go); channels |= TimeChannels.Shaders; }
                    break;
            }

            bool isCharacter = role == ObjectRole.PlayerCharacter || role == ObjectRole.EnemyAI || role == ObjectRole.Character2D;
            if (hasAudio && isCharacter) { Ensure<TimeAudio>(go); channels |= TimeChannels.Audio; }

            if (addEchoes || role == ObjectRole.Projectile) { Ensure<TimeParadoxModule>(go); channels |= TimeChannels.Echoes; }

            Undo.RecordObject(identity, "TimeWarp Setup");
            identity.Channels = channels;
            EditorUtility.SetDirty(identity);
            identity.RefreshModules();

            Undo.CollapseUndoOperations(group);
        }

        private static void ConfigureKillSwitch(GameObject go)
        {
            var manager = Ensure<TimeWarpComponentManager>(go);

            if (manager.TargetComponents.Count == 0)
            {
                Undo.RecordObject(manager, "TimeWarp Setup");
                manager.AutoFillComponents();
                EditorUtility.SetDirty(manager);
            }
        }

        private static T Ensure<T>(GameObject go) where T : Component
        {
            var existing = go.GetComponent<T>();
            return existing != null ? existing : Undo.AddComponent<T>(go);
        }

        private static GameObject[] GetValidTargets()
        {
            var list = new List<GameObject>();

            foreach (var obj in Selection.gameObjects)
            {
                if (obj == null) continue;
                if (obj.GetComponent<TimeWarpManager>() != null) continue;
                list.Add(obj);
            }

            return list.ToArray();
        }
    }
}
