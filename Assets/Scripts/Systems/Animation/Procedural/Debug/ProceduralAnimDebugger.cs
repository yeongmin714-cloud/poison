using UnityEditor;
using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;
using System.Reflection;
using ProjectName.Systems.Animation.Procedural.Bones;
using ProjectName.Systems.Animation.Procedural;

namespace ProjectName.Systems.Animation.Procedural.Debug
{
    /// <summary>
    /// Scene view debugger for procedural animation system.
    /// Shows phases, IK targets, foot placements, spine curves, gait diagram,
    /// and provides runtime parameter tweaking via IMGUI.
    /// </summary>
    [InitializeOnLoad]
    public static class ProceduralAnimDebugger
    {
        static ProceduralAnimDebugger()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        // ──────────────────────────────────────────────
        // Registry
        // ──────────────────────────────────────────────

        static readonly Dictionary<string, DebugData> _debugData = new();

        // ──────────────────────────────────────────────
        // Toggle state
        // ──────────────────────────────────────────────

        static bool _showDebug = true;
        static bool _showPhases = true;
        static bool _showIKTargets = true;
        static bool _showFootPlacements = true;
        static bool _showSpineWave = true;
        static bool _showGaitDiagram = true;
        static bool _showVelocities = true;
        static bool _showGroundContacts = true;
        static bool _showParameterWindow = false;

        // ──────────────────────────────────────────────
        // Parameter tweaking state (runtime)
        // ──────────────────────────────────────────────

        static float _paramWalkSpeed = 5f;
        static float _paramRunSpeed = 10f;
        static float _paramAcceleration = 20f;
        static float _paramTurnSpeed = 540f;
        static float _paramFootIKWeight = 0.9f;
        static float _paramHandIKWeight = 1f;
        static float _paramSpineIKWeight = 0.6f;
        static float _paramHeadLookWeight = 0.7f;
        static float _paramBodyLean = 0.4f;
        static float _paramDutyCycle = 0.6f;
        static float _paramStepLength = 0.6f;
        static float _paramStepHeight = 0.25f;
        static bool _paramDirty = false;

        // ──────────────────────────────────────────────
        // Public API
        // ──────────────────────────────────────────────

        public static void Register(ProceduralAnimationController controller, string key = null)
        {
            if (string.IsNullOrEmpty(key)) key = controller.gameObject.name;
            _debugData[key] = new DebugData
            {
                Controller = controller,
                LastUpdate = Time.realtimeSinceStartup
            };
        }

        public static void Unregister(string key)
        {
            _debugData.Remove(key);
        }

        public static void UpdateData(string key, DebugData data)
        {
            if (_debugData.ContainsKey(key))
            {
                data.LastUpdate = Time.realtimeSinceStartup;
                _debugData[key] = data;
            }
        }

        // ──────────────────────────────────────────────
        // Scene GUI
        // ──────────────────────────────────────────────

        static void OnSceneGUI(SceneView sceneView)
        {
            if (!_showDebug) return;
            if (!Application.isPlaying) return;

            // Toolbar overlay
            Handles.BeginGUI();
            DrawToolbar();
            if (_showParameterWindow) DrawParameterWindow();
            Handles.EndGUI();

            // Apply parameter changes
            if (_paramDirty)
            {
                ApplyParameterChanges();
                _paramDirty = false;
            }

            // Draw gizmos for each registered controller
            foreach (var kvp in _debugData)
            {
                var data = kvp.Value;
                if (data.Controller == null) continue;

                var ctrl = data.Controller;
                var t = ctrl.transform;

                if (_showPhases) DrawPhaseDiagram(ctrl, t);
                if (_showIKTargets) DrawIKTargets(ctrl, t, data);
                if (_showFootPlacements) DrawFootPlacements(ctrl, t, data);
                if (_showSpineWave) DrawSpineWave(ctrl, t, data);
                if (_showGaitDiagram) DrawGaitDiagram(ctrl, t, data);
                if (_showVelocities) DrawVelocities(ctrl, t);
                if (_showGroundContacts) DrawGroundContacts(ctrl, t);
            }

            // Cleanup stale entries
            var keysToRemove = new List<string>();
            foreach (var kvp in _debugData)
            {
                if (Time.realtimeSinceStartup - kvp.Value.LastUpdate > 5f)
                    keysToRemove.Add(kvp.Key);
            }
            foreach (var k in keysToRemove) _debugData.Remove(k);
        }

        // ──────────────────────────────────────────────
        // Toolbar
        // ──────────────────────────────────────────────

        static void DrawToolbar()
        {
            GUILayout.BeginArea(new Rect(10, 10, 280, 260), "Procedural Anim Debug", GUI.skin.window);

            _showDebug = GUILayout.Toggle(_showDebug, "Enable Debug");
            if (!_showDebug) { GUILayout.EndArea(); return; }

            GUILayout.Space(4);

            _showPhases = GUILayout.Toggle(_showPhases, "Leg Phases");
            _showIKTargets = GUILayout.Toggle(_showIKTargets, "IK Targets");
            _showFootPlacements = GUILayout.Toggle(_showFootPlacements, "Foot Placements");
            _showSpineWave = GUILayout.Toggle(_showSpineWave, "Spine Wave");
            _showGaitDiagram = GUILayout.Toggle(_showGaitDiagram, "Gait Diagram");
            _showVelocities = GUILayout.Toggle(_showVelocities, "Velocities");
            _showGroundContacts = GUILayout.Toggle(_showGroundContacts, "Ground Contacts");

            GUILayout.Space(6);
            if (GUILayout.Button(_showParameterWindow ? "Close Params" : "Open Params", GUILayout.Height(22)))
                _showParameterWindow = !_showParameterWindow;

            GUILayout.EndArea();
        }

        // ──────────────────────────────────────────────
        // Parameter tweaking window
        // ──────────────────────────────────────────────

        static void DrawParameterWindow()
        {
            // Position the parameter window to the right of the toolbar
            Rect toolbarRect = new Rect(10, 10, 280, 260);
            Rect paramRect = new Rect(toolbarRect.xMax + 10, 10, 280, 480);

            GUILayout.BeginArea(paramRect, "Runtime Parameters", GUI.skin.window);

            GUILayout.Label("Locomotion", EditorStyles.boldLabel);
            _paramWalkSpeed = EditorGUILayout.Slider("Walk Speed", _paramWalkSpeed, 0f, 15f);
            _paramRunSpeed = EditorGUILayout.Slider("Run Speed", _paramRunSpeed, 0f, 20f);
            _paramAcceleration = EditorGUILayout.Slider("Acceleration", _paramAcceleration, 1f, 50f);
            _paramTurnSpeed = EditorGUILayout.Slider("Turn Speed", _paramTurnSpeed, 90f, 1080f);
            _paramDutyCycle = EditorGUILayout.Slider("Duty Cycle", _paramDutyCycle, 0.3f, 0.8f);
            _paramStepLength = EditorGUILayout.Slider("Step Length", _paramStepLength, 0.2f, 1.5f);
            _paramStepHeight = EditorGUILayout.Slider("Step Height", _paramStepHeight, 0.05f, 0.8f);

            GUILayout.Space(6);
            GUILayout.Label("IK Weights", EditorStyles.boldLabel);
            _paramFootIKWeight = EditorGUILayout.Slider("Foot IK", _paramFootIKWeight, 0f, 1f);
            _paramHandIKWeight = EditorGUILayout.Slider("Hand IK", _paramHandIKWeight, 0f, 1f);
            _paramSpineIKWeight = EditorGUILayout.Slider("Spine IK", _paramSpineIKWeight, 0f, 1f);
            _paramHeadLookWeight = EditorGUILayout.Slider("Head Look", _paramHeadLookWeight, 0f, 1f);

            GUILayout.Space(6);
            GUILayout.Label("Procedural Modifiers", EditorStyles.boldLabel);
            _paramBodyLean = EditorGUILayout.Slider("Body Lean", _paramBodyLean, 0f, 1f);

            GUILayout.Space(8);
            if (GUILayout.Button("Apply to All Controllers", GUILayout.Height(24)))
            {
                _paramDirty = true;
            }

            if (GUILayout.Button("Reset to Defaults", GUILayout.Height(20)))
            {
                _paramWalkSpeed = 5f;
                _paramRunSpeed = 10f;
                _paramAcceleration = 20f;
                _paramTurnSpeed = 540f;
                _paramFootIKWeight = 0.9f;
                _paramHandIKWeight = 1f;
                _paramSpineIKWeight = 0.6f;
                _paramHeadLookWeight = 0.7f;
                _paramBodyLean = 0.4f;
                _paramDutyCycle = 0.6f;
                _paramStepLength = 0.6f;
                _paramStepHeight = 0.25f;
                _paramDirty = true;
            }

            GUILayout.EndArea();
        }

        static void ApplyParameterChanges()
        {
            foreach (var kvp in _debugData)
            {
                var ctrl = kvp.Value.Controller;
                if (ctrl == null) continue;

                // Use reflection to set private fields
                SetPrivateField(ctrl, "walkSpeed", _paramWalkSpeed);
                SetPrivateField(ctrl, "runSpeed", _paramRunSpeed);
                SetPrivateField(ctrl, "acceleration", _paramAcceleration);
                SetPrivateField(ctrl, "turnSpeed", _paramTurnSpeed);
                SetPrivateField(ctrl, "footIKWeight", _paramFootIKWeight);
                SetPrivateField(ctrl, "handIKWeight", _paramHandIKWeight);
                SetPrivateField(ctrl, "spineIKWeight", _paramSpineIKWeight);
                SetPrivateField(ctrl, "headLookWeight", _paramHeadLookWeight);
                SetPrivateField(ctrl, "bodyLeanAmount", _paramBodyLean);
                SetPrivateField(ctrl, "_dutyCycle", _paramDutyCycle);

                // Set step parameters via FootPlannerJob fields in ScheduleLocomotionJobs
                // These are local variables in the method, so we store them on the DebugData
                kvp.Value.DutyCycle = _paramDutyCycle;
                kvp.Value.StepLength = _paramStepLength;
                kvp.Value.StepHeight = _paramStepHeight;
            }
        }

        static void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            if (field != null)
                field.SetValue(obj, value);
        }

        // ──────────────────────────────────────────────
        // Reflection helpers
        // ──────────────────────────────────────────────

        static object GetPrivateField(object obj, string fieldName)
        {
            var field = obj.GetType().GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            if (field == null)
            {
                // Try with underscore prefix variants
                field = obj.GetType().GetField("_" + fieldName,
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            }
            return field?.GetValue(obj);
        }

        static T GetPrivateField<T>(object obj, string fieldName, T defaultValue = default)
        {
            var val = GetPrivateField(obj, fieldName);
            if (val == null) return defaultValue;
            try { return (T)val; }
            catch { return defaultValue; }
        }

        // ──────────────────────────────────────────────
        // 1. Leg Phase Visualization
        // ──────────────────────────────────────────────

        static void DrawPhaseDiagram(ProceduralAnimationController ctrl, Transform t)
        {
            bool isQuadruped = DetectQuadruped(ctrl);
            int legCount = isQuadruped ? 4 : 2;

            float[] phases = new float[legCount];
            string[] labels = isQuadruped
                ? new[] { "LF", "RF", "LH", "RH" }
                : new[] { "L", "R" };

            Color[] colors = isQuadruped
                ? new[] { Color.cyan, Color.magenta, new Color(0.3f, 0.8f, 1f), new Color(1f, 0.3f, 0.8f) }
                : new[] { Color.cyan, Color.magenta };

            if (isQuadruped)
            {
                // Get 4-leg phases from reflected fields or NativeArrays
                var lfPhase = GetPrivateField<float>(ctrl, "_leftLegPhase", 0f);
                var rfPhase = GetPrivateField<float>(ctrl, "_rightLegPhase", 0.5f);
                // For hind legs, use offset from front legs
                float lhPhase = math.fmod(lfPhase + 0.5f, 1f);
                float rhPhase = math.fmod(rfPhase + 0.5f, 1f);

                // Try to read from NativeArray if available
                var lfArr = GetPrivateField(ctrl, "_leftLegPhaseArr");
                var rfArr = GetPrivateField(ctrl, "_rightLegPhaseArr");
                if (lfArr != null)
                {
                    var arr = (Unity.Collections.NativeArray<float>)lfArr;
                    if (arr.IsCreated) lfPhase = arr[0];
                }
                if (rfArr != null)
                {
                    var arr = (Unity.Collections.NativeArray<float>)rfArr;
                    if (arr.IsCreated) rfPhase = arr[0];
                }

                // Try reading LegPhaseOffsetJob results if available
                var legPhaseField = ctrl.GetType().GetField("_legPhases",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (legPhaseField != null)
                {
                    var legPhases = (Unity.Collections.NativeArray<float>)legPhaseField.GetValue(ctrl);
                    if (legPhases.IsCreated && legPhases.Length >= 4)
                    {
                        lfPhase = legPhases[0];
                        rfPhase = legPhases[1];
                        lhPhase = legPhases[2];
                        rhPhase = legPhases[3];
                    }
                }

                phases[0] = lfPhase;
                phases[1] = rfPhase;
                phases[2] = lhPhase;
                phases[3] = rhPhase;
            }
            else
            {
                phases[0] = GetPrivateField<float>(ctrl, "_leftLegPhase", 0f);
                phases[1] = GetPrivateField<float>(ctrl, "_rightLegPhase", 0.5f);
            }

            // Get duty cycle
            float dutyCycle = GetPrivateField<float>(ctrl, "_dutyCycle", 0.6f);

            // Draw phase circles at each foot position
            Vector3 bodyCenter = t.position + Vector3.up * 0.3f;
            float spread = isQuadruped ? 0.6f : 0.4f;
            float forwardOffset = 0.5f;

            for (int i = 0; i < legCount; i++)
            {
                Vector3 pos;
                if (isQuadruped)
                {
                    // Quadruped layout: [LF, RF] front, [LH, RH] hind
                    bool isFront = i < 2;
                    bool isLeft = i % 2 == 0;
                    float fwd = isFront ? forwardOffset : -forwardOffset * 0.5f;
                    float lat = isLeft ? -spread : spread;
                    pos = bodyCenter + t.forward * fwd + t.right * lat;
                }
                else
                {
                    // Biped layout: L on left, R on right
                    bool isLeft = i == 0;
                    pos = bodyCenter + t.forward * forwardOffset + t.right * (isLeft ? -spread : spread);
                }

                DrawPhaseCircle(pos, phases[i], dutyCycle, colors[i], labels[i]);
            }
        }

        static void DrawPhaseCircle(Vector3 pos, float phase, float dutyCycle, Color color, string label)
        {
            float radius = 0.3f;
            float phaseAngle = phase * 360f;
            bool stance = phase < dutyCycle;

            // Background wire disc
            Handles.color = new Color(color.r, color.g, color.b, 0.2f);
            Handles.DrawWireDisc(pos, Vector3.up, radius);

            // Stance arc (solid color)
            Handles.color = new Color(0.2f, 0.8f, 0.2f, 0.25f);
            float stanceAngle = dutyCycle * 360f;
            if (dutyCycle > 0.01f)
                Handles.DrawSolidArc(pos, Vector3.up, Vector3.forward, stanceAngle, radius * 0.85f);

            // Phase indicator (radial line at current phase angle)
            Handles.color = color;
            Vector3 dir = Quaternion.Euler(0, phaseAngle, 0) * Vector3.forward;
            Vector3 end = pos + dir * radius;
            Handles.DrawLine(pos, end, 2f);

            // Stance/swing status dot
            Vector3 dotPos = pos + Vector3.up * 0.08f;
            Handles.color = stance ? new Color(0.2f, 0.9f, 0.2f) : new Color(0.9f, 0.9f, 0.2f);
            Handles.DrawSolidDisc(dotPos, Vector3.up, 0.06f);

            // Phase progress arc (thick arc showing current phase progression)
            Handles.color = new Color(color.r, color.g, color.b, 0.6f);
            Handles.DrawWireArc(pos, Vector3.up, Vector3.forward, phaseAngle, radius * 1.12f);

            // Label
            string status = stance ? "S" : "W";
            Handles.color = Color.white;
            Handles.Label(pos + Vector3.up * 0.45f + dir * 0.15f,
                $"{label}: {phase:F2} [{status}]");
        }

        // ──────────────────────────────────────────────
        // 2. IK Target Visualization
        // ──────────────────────────────────────────────

        static void DrawIKTargets(ProceduralAnimationController ctrl, Transform t, DebugData data)
        {
            // Read IK targets from NativeArrays or fallback fields
            Vector3 lTarget = ReadVector3Arr(ctrl, "_leftFootTarget", t.position + t.right * -0.3f);
            Vector3 rTarget = ReadVector3Arr(ctrl, "_rightFootTarget", t.position + t.right * 0.3f);
            Vector3 lHint = ReadVector3Arr(ctrl, "_leftFootHint", lTarget + Vector3.up * 0.3f);
            Vector3 rHint = ReadVector3Arr(ctrl, "_rightFootHint", rTarget + Vector3.up * 0.3f);
            Vector3 lHandTarget = ReadVector3Arr(ctrl, "_leftHandTarget", t.position + t.forward * 0.5f + Vector3.up * 1.0f);
            Vector3 rHandTarget = ReadVector3Arr(ctrl, "_rightHandTarget", t.position + t.forward * 0.5f + Vector3.up * 1.0f);
            Vector3 headLook = ReadVector3Field(ctrl, "_headLookTarget", t.position + t.forward * 5f + Vector3.up * 1.5f);

            // Also try quadruped leg targets
            Vector3 lhTarget = ReadVector3Arr(ctrl, "_leftHindTarget", t.position + t.forward * -0.4f + t.right * -0.3f);
            Vector3 rhTarget = ReadVector3Arr(ctrl, "_rightHindTarget", t.position + t.forward * -0.4f + t.right * 0.3f);
            Vector3 lhHint = ReadVector3Arr(ctrl, "_leftHindHint", lhTarget + Vector3.up * 0.3f);
            Vector3 rhHint = ReadVector3Arr(ctrl, "_rightHindHint", rhTarget + Vector3.up * 0.3f);

            // Check if quadruped by looking for hind fields
            bool isQuadruped = ctrl.GetType().GetField("_leftHindTarget",
                BindingFlags.NonPublic | BindingFlags.Instance) != null;

            // ── Foot targets (spheres with handles) ──
            DrawIKTargetSphere(lTarget, Color.green, "L Foot", t.position);
            DrawIKTargetSphere(rTarget, Color.red, "R Foot", t.position);

            if (isQuadruped)
            {
                DrawIKTargetSphere(lhTarget, new Color(0.3f, 0.8f, 1f), "LH Foot", t.position);
                DrawIKTargetSphere(rhTarget, new Color(1f, 0.3f, 0.8f), "RH Foot", t.position);
            }

            // ── Hint targets (knee/elbow positions) ──
            DrawIKHintSphere(lTarget, lHint, new Color(0, 1, 0, 0.5f));
            DrawIKHintSphere(rTarget, rHint, new Color(1, 0, 0, 0.5f));

            if (isQuadruped)
            {
                DrawIKHintSphere(lhTarget, lhHint, new Color(0.3f, 0.8f, 1f, 0.5f));
                DrawIKHintSphere(rhTarget, rhHint, new Color(1f, 0.3f, 0.8f, 0.5f));
            }

            // ── Hand targets ──
            Vector3 handOrigin = t.position + Vector3.up * 1.5f;
            DrawIKTargetSphere(lHandTarget, Color.blue, "L Hand", handOrigin);
            DrawIKTargetSphere(rHandTarget, Color.magenta, "R Hand", handOrigin);

            // ── Head look target ──
            Handles.color = Color.yellow;
            Vector3 headOrigin = t.position + Vector3.up * 1.7f;
            Handles.DrawWireDisc(headLook, Vector3.up, 0.15f);
            Handles.DrawDottedLine(headOrigin, headLook, 2f);
            Handles.Label(headLook + Vector3.up * 0.15f, "Look", EditorStyles.whiteLabel);
        }

        static void DrawIKTargetSphere(Vector3 pos, Color color, string label, Vector3 origin)
        {
            Handles.color = color;
            Handles.SphereHandleCap(0, pos, Quaternion.identity, 0.12f, EventType.Repaint);
            Handles.DrawDottedLine(origin, pos, 2f);
            Handles.Label(pos + Vector3.up * 0.12f, label, EditorStyles.whiteLabel);
        }

        static void DrawIKHintSphere(Vector3 target, Vector3 hint, Color color)
        {
            Handles.color = color;
            Handles.SphereHandleCap(0, hint, Quaternion.identity, 0.08f, EventType.Repaint);
            Handles.DrawDottedLine(target, hint, 2f);
        }

        // ──────────────────────────────────────────────
        // 3. Foot Placement Visualization
        // ──────────────────────────────────────────────

        static void DrawFootPlacements(ProceduralAnimationController ctrl, Transform t, DebugData data)
        {
            var boneMap = ctrl.GetComponent<ProceduralBoneMap>();
            if (boneMap == null) return;

            // Read IK targets and phases
            Vector3 lTarget = ReadVector3Arr(ctrl, "_leftFootTarget", t.position);
            Vector3 rTarget = ReadVector3Arr(ctrl, "_rightFootTarget", t.position);
            float lPhase = GetPrivateField<float>(ctrl, "_leftLegPhase", 0f);
            float rPhase = GetPrivateField<float>(ctrl, "_rightLegPhase", 0.5f);
            float dutyCycle = data.DutyCycle > 0 ? data.DutyCycle : GetPrivateField<float>(ctrl, "_dutyCycle", 0.6f);
            float stepHeight = data.StepHeight > 0 ? data.StepHeight : 0.25f;

            // Left foot
            var lFoot = boneMap.Get(BoneRole.L_Foot);
            if (lFoot != null)
            {
                DrawFootArc(lFoot.position, lTarget, lPhase, dutyCycle, stepHeight, Color.cyan, "L");
            }

            // Right foot
            var rFoot = boneMap.Get(BoneRole.R_Foot);
            if (rFoot != null)
            {
                DrawFootArc(rFoot.position, rTarget, rPhase, dutyCycle, stepHeight, Color.magenta, "R");
            }

            // Quadruped hind legs
            var lhFoot = boneMap.Get(BoneRole.L_Ankle); // or use L_Foot for hind if available
            // For hind legs, we look for a different bone role if available
            // Try to find hind-specific bones via naming convention
            var lHind = FindBoneByRole(boneMap, "L_HindFoot");
            if (lHind != null)
            {
                float lhPhase = math.fmod(lPhase + 0.5f, 1f);
                Vector3 lhTarget = ReadVector3Arr(ctrl, "_leftHindTarget", lHind.position);
                DrawFootArc(lHind.position, lhTarget, lhPhase, dutyCycle, stepHeight, new Color(0.3f, 0.8f, 1f), "LH");
            }

            var rHind = FindBoneByRole(boneMap, "R_HindFoot");
            if (rHind != null)
            {
                float rhPhase = math.fmod(rPhase + 0.5f, 1f);
                Vector3 rhTarget = ReadVector3Arr(ctrl, "_rightHindTarget", rHind.position);
                DrawFootArc(rHind.position, rhTarget, rhPhase, dutyCycle, stepHeight, new Color(1f, 0.3f, 0.8f), "RH");
            }
        }

        static void DrawFootArc(Vector3 currentPos, Vector3 targetPos, float phase, float dutyCycle,
            float stepHeight, Color color, string label)
        {
            bool stance = phase < dutyCycle;

            // Line from current foot position to target
            Handles.color = color;
            Handles.DrawLine(currentPos, targetPos, 2f);

            // Draw stance/swing arc
            if (stance)
            {
                // Stance: foot planted, draw ground contact circle
                Handles.color = new Color(0.2f, 0.9f, 0.2f, 0.4f);
                Handles.DrawSolidDisc(currentPos, Vector3.up, 0.08f);
                Handles.color = color;
                Handles.DrawWireDisc(currentPos, Vector3.up, 0.12f);
            }
            else
            {
                // Swing: draw arc trajectory
                float swingProgress = (phase - dutyCycle) / (1f - dutyCycle);
                float height = Mathf.Sin(swingProgress * Mathf.PI) * stepHeight;

                // Draw arc from current to target with height
                Handles.color = new Color(color.r, color.g, color.b, 0.5f);
                Vector3 mid = (currentPos + targetPos) * 0.5f + Vector3.up * height;
                Handles.DrawBezier(currentPos, targetPos, mid, mid, color, null, 2f);

                // Draw height indicator
                Handles.color = new Color(1, 1, 0, 0.3f);
                Vector3 groundMid = (currentPos + targetPos) * 0.5f;
                Handles.DrawDottedLine(groundMid, mid, 2f);

                // Step height label
                Handles.Label(mid + Vector3.up * 0.05f, $"h:{height:F2}", EditorStyles.whiteLabel);
            }

            // Label
            Handles.Label(currentPos + Vector3.right * 0.1f, label, EditorStyles.whiteLabel);
        }

        // ──────────────────────────────────────────────
        // 4. Spine Wave Visualization
        // ──────────────────────────────────────────────

        static void DrawSpineWave(ProceduralAnimationController ctrl, Transform t, DebugData data)
        {
            var boneMap = ctrl.GetComponent<ProceduralBoneMap>();
            if (boneMap == null) return;

            // Collect spine bones
            var spineBones = new List<Transform>();
            var spineRoles = new[] { BoneRole.Spine0, BoneRole.Spine1, BoneRole.Spine2, BoneRole.Spine3, BoneRole.Neck, BoneRole.Head };
            foreach (var role in spineRoles)
            {
                var b = boneMap.Get(role);
                if (b != null) spineBones.Add(b);
            }

            if (spineBones.Count < 2) return;

            // Draw spine chain
            Handles.color = Color.white;
            for (int i = 0; i < spineBones.Count - 1; i++)
            {
                Handles.DrawLine(spineBones[i].position, spineBones[i + 1].position, 3f);
                Handles.DrawWireDisc(spineBones[i].position, Vector3.up, 0.05f);
            }
            Handles.DrawWireDisc(spineBones[spineBones.Count - 1].position, Vector3.up, 0.05f);

            // Draw S-curve wave offset
            float speed = ctrl.CurrentSpeed;
            float time = Time.time;
            float waveFreq = 2.0f;
            float waveAmp = 0.08f * Mathf.Clamp01(speed / 5f);

            if (waveAmp > 0.01f)
            {
                Handles.color = new Color(0.5f, 0.8f, 1f, 0.6f);
                Vector3 prevWavePos = spineBones[0].position;

                for (int i = 0; i < spineBones.Count; i++)
                {
                    float segmentPhase = time * waveFreq + i * 0.5f;
                    float lateralOffset = Mathf.Sin(segmentPhase) * waveAmp;
                    float verticalOffset = Mathf.Sin(segmentPhase * 0.7f + 1.0f) * waveAmp * 0.5f;

                    Vector3 bonePos = spineBones[i].position;
                    Vector3 wavePos = bonePos + t.right * lateralOffset + Vector3.up * verticalOffset;

                    // Draw wave offset line
                    Handles.DrawDottedLine(bonePos, wavePos, 2f);

                    // Draw small sphere at wave position
                    Handles.DrawSolidDisc(wavePos, Vector3.up, 0.02f);

                    if (i > 0)
                    {
                        // Connect wave positions
                        Handles.color = new Color(0.5f, 0.8f, 1f, 0.4f);
                        Handles.DrawLine(prevWavePos, wavePos, 2f);
                    }

                    prevWavePos = wavePos;
                    Handles.color = new Color(0.5f, 0.8f, 1f, 0.6f);
                }
            }

            // Draw control handles for each spine bone (as small moveable spheres)
            Handles.color = new Color(1, 1, 1, 0.3f);
            for (int i = 1; i < spineBones.Count - 1; i++)
            {
                Vector3 prev = spineBones[i - 1].position;
                Vector3 curr = spineBones[i].position;
                Vector3 next = spineBones[i + 1].position;

                // Tangent visualization
                Vector3 inTangent = (curr - prev).normalized * 0.15f;
                Vector3 outTangent = (next - curr).normalized * 0.15f;

                Handles.color = new Color(0.8f, 0.8f, 0.2f, 0.5f);
                Handles.DrawLine(curr, curr + inTangent);
                Handles.DrawLine(curr, curr + outTangent);
                Handles.DrawSolidDisc(curr + inTangent, Vector3.up, 0.02f);
                Handles.DrawSolidDisc(curr + outTangent, Vector3.up, 0.02f);
            }

            // Label spine count
            Handles.color = Color.white;
            Handles.Label(spineBones[0].position + Vector3.up * 0.2f,
                $"Spine: {spineBones.Count} segments", EditorStyles.whiteLabel);
        }

        // ──────────────────────────────────────────────
        // 5. Gait Phase Diagram
        // ──────────────────────────────────────────────

        static void DrawGaitDiagram(ProceduralAnimationController ctrl, Transform t, DebugData data)
        {
            bool isQuadruped = DetectQuadruped(ctrl);
            int legCount = isQuadruped ? 4 : 2;

            float[] phases = new float[legCount];
            string[] labels = isQuadruped
                ? new[] { "LF", "RF", "LH", "RH" }
                : new[] { "Left", "Right" };

            Color[] legColors = isQuadruped
                ? new[] { Color.cyan, Color.magenta, new Color(0.3f, 0.8f, 1f), new Color(1f, 0.3f, 0.8f) }
                : new[] { Color.cyan, Color.magenta };

            if (isQuadruped)
            {
                phases[0] = GetPrivateField<float>(ctrl, "_leftLegPhase", 0f);
                phases[1] = GetPrivateField<float>(ctrl, "_rightLegPhase", 0.5f);
                phases[2] = math.fmod(phases[0] + 0.5f, 1f);
                phases[3] = math.fmod(phases[1] + 0.5f, 1f);
            }
            else
            {
                phases[0] = GetPrivateField<float>(ctrl, "_leftLegPhase", 0f);
                phases[1] = GetPrivateField<float>(ctrl, "_rightLegPhase", 0.5f);
            }

            float dutyCycle = data.DutyCycle > 0 ? data.DutyCycle : GetPrivateField<float>(ctrl, "_dutyCycle", 0.6f);

            // Draw gait diagram as a set of horizontal bars in world space
            // Position the diagram above the character
            Vector3 diagramOrigin = t.position + t.forward * -0.5f + t.right * -0.8f + Vector3.up * 1.5f;
            float barWidth = 1.5f;
            float barHeight = 0.15f;
            float barSpacing = 0.25f;

            // Background
            Vector3 bgCenter = diagramOrigin + new Vector3(barWidth * 0.5f, (legCount - 1) * barSpacing * 0.5f, 0);
            Handles.color = new Color(0, 0, 0, 0.3f);
            Handles.DrawSolidRectangleWithOutline(
                new Rect(diagramOrigin.x - 0.05f, diagramOrigin.z - 0.05f, barWidth + 0.1f,
                         (legCount - 1) * barSpacing + barHeight + 0.1f),
                new Color(0, 0, 0, 0.15f), new Color(1, 1, 1, 0.2f));

            // Draw gait label
            Handles.color = Color.white;
            Handles.Label(diagramOrigin + Vector3.up * 0.15f + Vector3.right * 0.3f,
                $"Gait @ {Time.time:F1}s", EditorStyles.whiteLabel);

            // Draw each leg bar
            for (int i = 0; i < legCount; i++)
            {
                Vector3 barPos = diagramOrigin + Vector3.right * 0 + Vector3.up * (i * barSpacing);
                float phase = phases[i];
                bool stance = phase < dutyCycle;

                // Stance bar (green)
                float stanceWidth = barWidth * dutyCycle;
                Vector3 stanceStart = barPos + Vector3.right * 0;
                Vector3 stanceEnd = stanceStart + Vector3.right * stanceWidth;
                Handles.color = new Color(0.2f, 0.8f, 0.2f, 0.6f);
                DrawBar(stanceStart, stanceEnd, barHeight);

                // Swing bar (yellow)
                float swingWidth = barWidth * (1f - dutyCycle);
                Vector3 swingStart = barPos + Vector3.right * stanceWidth;
                Vector3 swingEnd = swingStart + Vector3.right * swingWidth;
                Handles.color = new Color(0.8f, 0.8f, 0.2f, 0.6f);
                DrawBar(swingStart, swingEnd, barHeight);

                // Phase indicator (current position marker)
                float phasePos = phase * barWidth;
                Vector3 markerPos = barPos + Vector3.right * phasePos + Vector3.up * barHeight * 0.5f;
                Handles.color = legColors[i];
                Handles.DrawSolidDisc(markerPos, Vector3.up, 0.04f);

                // Label
                Handles.color = Color.white;
                Handles.Label(barPos + Vector3.right * (barWidth + 0.05f) + Vector3.up * barHeight * 0.2f,
                    labels[i], EditorStyles.whiteLabel);
            }

            // Draw phase offset lines between legs
            if (legCount > 1)
            {
                Handles.color = new Color(1, 1, 1, 0.15f);
                for (int i = 0; i < legCount; i++)
                {
                    float phaseX = phases[i] * barWidth;
                    Vector3 top = diagramOrigin + Vector3.right * phaseX + Vector3.up * 0;
                    Vector3 bottom = top + Vector3.up * ((legCount - 1) * barSpacing + barHeight);
                    Handles.DrawDottedLine(top, bottom, 3f);
                }
            }
        }

        static void DrawBar(Vector3 start, Vector3 end, float height)
        {
            // Draw a 2D-ish bar in world space (XZ plane, using Y as up)
            Vector3 p1 = start;
            Vector3 p2 = end;
            Vector3 p3 = end + Vector3.up * height;
            Vector3 p4 = start + Vector3.up * height;

            Handles.DrawSolidRectangleWithOutline(
                new Vector3[] { p1, p2, p3, p4 },
                Handles.color, new Color(1, 1, 1, 0.1f));
        }

        // ──────────────────────────────────────────────
        // 6. Velocity Visualization
        // ──────────────────────────────────────────────

        static void DrawVelocities(ProceduralAnimationController ctrl, Transform t)
        {
            Vector3 vel = ctrl.CurrentVelocity;
            if (vel.sqrMagnitude < 0.01f) return;

            Handles.color = Color.cyan;
            Vector3 origin = t.position + Vector3.up * 0.1f;
            Handles.ArrowHandleCap(0, origin, Quaternion.LookRotation(vel.normalized),
                vel.magnitude * 0.3f, EventType.Repaint);
            Handles.Label(origin + vel.normalized * (vel.magnitude * 0.3f + 0.2f),
                $"Speed: {vel.magnitude:F1} m/s\nDir: ({vel.normalized.x:F2}, {vel.normalized.z:F2})",
                EditorStyles.whiteLabel);
        }

        // ──────────────────────────────────────────────
        // 7. Ground Contact Visualization
        // ──────────────────────────────────────────────

        static void DrawGroundContacts(ProceduralAnimationController ctrl, Transform t)
        {
            bool lGrounded = GetPrivateField<bool>(ctrl, "_leftFootGrounded", false);
            bool rGrounded = GetPrivateField<bool>(ctrl, "_rightFootGrounded", false);
            RaycastHit lHit = GetPrivateField<RaycastHit>(ctrl, "_leftFootHit");
            RaycastHit rHit = GetPrivateField<RaycastHit>(ctrl, "_rightFootHit");

            if (lGrounded && lHit.point != Vector3.zero)
            {
                // Contact disc
                Handles.color = new Color(0.2f, 0.9f, 0.2f, 0.5f);
                Handles.DrawSolidDisc(lHit.point, lHit.normal, 0.15f);
                // Normal arrow
                Handles.color = Color.green;
                Handles.ArrowHandleCap(0, lHit.point, Quaternion.LookRotation(lHit.normal), 0.25f, EventType.Repaint);
                // Pressure label
                Handles.Label(lHit.point + Vector3.up * 0.2f, "L Contact", EditorStyles.whiteLabel);
            }

            if (rGrounded && rHit.point != Vector3.zero)
            {
                Handles.color = new Color(0.9f, 0.2f, 0.2f, 0.5f);
                Handles.DrawSolidDisc(rHit.point, rHit.normal, 0.15f);
                Handles.color = Color.red;
                Handles.ArrowHandleCap(0, rHit.point, Quaternion.LookRotation(rHit.normal), 0.25f, EventType.Repaint);
                Handles.Label(rHit.point + Vector3.up * 0.2f, "R Contact", EditorStyles.whiteLabel);
            }

            // Quadruped hind contacts
            RaycastHit lhHit = GetPrivateField<RaycastHit>(ctrl, "_leftHindHit");
            RaycastHit rhHit = GetPrivateField<RaycastHit>(ctrl, "_rightHindHit");
            bool lhGrounded = lhHit.point != Vector3.zero;
            bool rhGrounded = rhHit.point != Vector3.zero;

            if (lhGrounded)
            {
                Handles.color = new Color(0.3f, 0.8f, 1f, 0.5f);
                Handles.DrawSolidDisc(lhHit.point, lhHit.normal, 0.15f);
                Handles.ArrowHandleCap(0, lhHit.point, Quaternion.LookRotation(lhHit.normal), 0.25f, EventType.Repaint);
                Handles.Label(lhHit.point + Vector3.up * 0.2f, "LH Contact", EditorStyles.whiteLabel);
            }
            if (rhGrounded)
            {
                Handles.color = new Color(1f, 0.3f, 0.8f, 0.5f);
                Handles.DrawSolidDisc(rhHit.point, rhHit.normal, 0.15f);
                Handles.ArrowHandleCap(0, rhHit.point, Quaternion.LookRotation(rhHit.normal), 0.25f, EventType.Repaint);
                Handles.Label(rhHit.point + Vector3.up * 0.2f, "RH Contact", EditorStyles.whiteLabel);
            }
        }

        // ──────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────

        static bool DetectQuadruped(ProceduralAnimationController ctrl)
        {
            // Check if the controller has quadruped-specific fields
            return ctrl.GetType().GetField("_leftHindTarget",
                BindingFlags.NonPublic | BindingFlags.Instance) != null;
        }

        static Vector3 ReadVector3Arr(ProceduralAnimationController ctrl, string fieldName, Vector3 fallback)
        {
            var field = ctrl.GetType().GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            if (field == null) return fallback;

            var val = field.GetValue(ctrl);
            if (val is Unity.Collections.NativeArray<float3> arr && arr.IsCreated && arr.Length > 0)
                return arr[0];
            if (val is Vector3 v)
                return v;

            return fallback;
        }

        static Vector3 ReadVector3Field(ProceduralAnimationController ctrl, string fieldName, Vector3 fallback)
        {
            var field = ctrl.GetType().GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            if (field == null) return fallback;
            var val = field.GetValue(ctrl);
            if (val is Vector3 v) return v;
            return fallback;
        }

        static Transform FindBoneByRole(ProceduralBoneMap boneMap, string roleName)
        {
            // Try to find a bone by parsing the role name
            if (System.Enum.TryParse<BoneRole>(roleName, out var role))
                return boneMap.Get(role);
            return null;
        }

        // ──────────────────────────────────────────────
        // DebugData
        // ──────────────────────────────────────────────

        public struct DebugData
        {
            public ProceduralAnimationController Controller;
            public float LastUpdate;
            public float LeftPhase;
            public float RightPhase;
            public float DutyCycle;
            public float StepLength;
            public float StepHeight;
            public Vector3 LeftFootTarget;
            public Vector3 RightFootTarget;
            public Vector3 LeftHandTarget;
            public Vector3 RightHandTarget;
            public Vector3 HeadLookTarget;
            public Vector3 CurrentVelocity;
            public bool IsGrounded;
        }
    }

    // ──────────────────────────────────────────────
    // Auto-registration component
    // ──────────────────────────────────────────────

    /// <summary>
    /// Component to auto-register with debugger.
    /// Attach to any GameObject with ProceduralAnimationController.
    /// </summary>
    [ExecuteAlways]
    public class ProceduralAnimDebugRegistrar : MonoBehaviour
    {
        ProceduralAnimationController _ctrl;
        string _key;

        void OnEnable()
        {
            _ctrl = GetComponent<ProceduralAnimationController>();
            if (_ctrl != null)
            {
                _key = gameObject.name;
                ProceduralAnimDebugger.Register(_ctrl, _key);
            }
        }

        void OnDisable()
        {
            if (!string.IsNullOrEmpty(_key))
                ProceduralAnimDebugger.Unregister(_key);
        }

        void LateUpdate()
        {
            if (_ctrl != null && Application.isPlaying)
            {
                var data = new ProceduralAnimDebugger.DebugData
                {
                    Controller = _ctrl,
                    LastUpdate = Time.realtimeSinceStartup,
                    LeftPhase = ReadPrivateField<float>("_leftLegPhase"),
                    RightPhase = ReadPrivateField<float>("_rightLegPhase"),
                    DutyCycle = ReadPrivateField<float>("_dutyCycle"),
                    CurrentVelocity = _ctrl.CurrentVelocity,
                    IsGrounded = _ctrl.IsGrounded,
                };

                ProceduralAnimDebugger.UpdateData(_key, data);
            }
        }

        T ReadPrivateField<T>(string fieldName, T defaultValue = default)
        {
            if (_ctrl == null) return defaultValue;
            var field = typeof(ProceduralAnimationController).GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            if (field == null) return defaultValue;
            var val = field.GetValue(_ctrl);
            if (val == null) return defaultValue;
            try { return (T)val; }
            catch { return defaultValue; }
        }
    }
}