using UnityEditor;
using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;

namespace ProjectName.Systems.Animation.Procedural.Debug
{
    /// <summary>
    /// Scene view debugger for procedural animation system.
    /// Shows phases, IK targets, foot placements, spine curves, etc.
    /// </summary>
    [InitializeOnLoad]
    public static class ProceduralAnimDebugger
    {
        static ProceduralAnimDebugger()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        static readonly Dictionary<string, DebugData> _debugData = new();
        static bool _showDebug = true;
        static bool _showPhases = true;
        static bool _showIKTargets = true;
        static bool _showFootPlacements = true;
        static bool _showSpineCurve = true;
        static bool _showVelocities = true;
        static bool _showGroundContacts = true;

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

        static void OnSceneGUI(SceneView sceneView)
        {
            if (!_showDebug) return;
            if (!Application.isPlaying) return;

            Handles.BeginGUI();
            DrawToolbar();
            Handles.EndGUI();

            foreach (var kvp in _debugData)
            {
                var data = kvp.Value;
                if (data.Controller == null) continue;

                var ctrl = data.Controller;
                var t = ctrl.transform;

                if (_showPhases) DrawPhaseDiagram(ctrl, t);
                if (_showIKTargets) DrawIKTargets(ctrl, t, data);
                if (_showFootPlacements) DrawFootPlacements(ctrl, t, data);
                if (_showSpineCurve) DrawSpineCurve(ctrl, t, data);
                if (_showVelocities) DrawVelocities(ctrl, t);
                if (_showGroundContacts) DrawGroundContacts(ctrl, t);
            }

            // Cleanup old entries
            var keysToRemove = new List<string>();
            foreach (var kvp in _debugData)
            {
                if (Time.realtimeSinceStartup - kvp.Value.LastUpdate > 5f)
                    keysToRemove.Add(kvp.Key);
            }
            foreach (var k in keysToRemove) _debugData.Remove(k);
        }

        static void DrawToolbar()
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 200), "Procedural Anim Debug", "window");
            _showDebug = GUILayout.Toggle(_showDebug, "Enable Debug");
            if (!_showDebug) { GUILayout.EndArea(); return; }

            _showPhases = GUILayout.Toggle(_showPhases, "Leg Phases");
            _showIKTargets = GUILayout.Toggle(_showIKTargets, "IK Targets");
            _showFootPlacements = GUILayout.Toggle(_showFootPlacements, "Foot Placements");
            _showSpineCurve = GUILayout.Toggle(_showSpineCurve, "Spine Curve");
            _showVelocities = GUILayout.Toggle(_showVelocities, "Velocities");
            _showGroundContacts = GUILayout.Toggle(_showGroundContacts, "Ground Contacts");
            GUILayout.EndArea();
        }

        static void DrawPhaseDiagram(ProceduralAnimationController ctrl, Transform t)
        {
            // Get leg phases via reflection or public fields
            var leftPhaseField = typeof(ProceduralAnimationController).GetField("_leftLegPhase", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var rightPhaseField = typeof(ProceduralAnimationController).GetField("_rightLegPhase",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (leftPhaseField == null || rightPhaseField == null) return;

            float leftPhase = (float)leftPhaseField.GetValue(ctrl);
            float rightPhase = (float)rightPhaseField.GetValue(ctrl);

            // Draw phase circles at feet
            Vector3 leftFoot = t.position + t.forward * 0.5f + t.right * -0.3f;
            Vector3 rightFoot = t.position + t.forward * 0.5f + t.right * 0.3f;

            DrawPhaseCircle(leftFoot, leftPhase, Color.cyan, "L");
            DrawPhaseCircle(rightFoot, rightPhase, Color.magenta, "R");
        }

        static void DrawPhaseCircle(Vector3 pos, float phase, Color color, string label)
        {
            Handles.color = color;
            float radius = 0.3f;

            // Background circle
            Handles.DrawWireDisc(pos, Vector3.up, radius);

            // Phase arc
            float angle = phase * 360f;
            Handles.DrawWireArc(pos, Vector3.up, Vector3.forward, angle, radius * 1.1f);

            // Stance/swing indicator
            bool stance = phase < 0.6f; // duty cycle
            Handles.color = stance ? Color.green : Color.yellow;
            Handles.DrawSolidDisc(pos + Vector3.up * 0.05f, Vector3.up, 0.05f);

            // Label
            Handles.Label(pos + Vector3.up * 0.5f, $"{label}: {phase:F2} ({ (stance ? "Stance" : "Swing") })");
        }

        static void DrawIKTargets(ProceduralAnimationController ctrl, Transform t, DebugData data)
        {
            // Get private fields
            var lTargetField = typeof(ProceduralAnimationController).GetField("_leftFootTarget",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var rTargetField = typeof(ProceduralAnimationController).GetField("_rightFootTarget",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var lHintField = typeof(ProceduralAnimationController).GetField("_leftFootHint",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var rHintField = typeof(ProceduralAnimationController).GetField("_rightFootHint",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var lHandTargetField = typeof(ProceduralAnimationController).GetField("_leftHandTarget",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var rHandTargetField = typeof(ProceduralAnimationController).GetField("_rightHandTarget",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var headLookField = typeof(ProceduralAnimationController).GetField("_headLookTarget",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (lTargetField == null) return;

            var lTarget = (Vector3)lTargetField.GetValue(ctrl);
            var rTarget = (Vector3)rTargetField.GetValue(ctrl);
            var lHint = (Vector3)lHintField.GetValue(ctrl);
            var rHint = (Vector3)rHintField.GetValue(ctrl);
            var lHandTarget = (Vector3)lHandTargetField.GetValue(ctrl);
            var rHandTarget = (Vector3)rHandTargetField.GetValue(ctrl);
            var headLook = (Vector3)headLookField.GetValue(ctrl);

            // Foot targets
            Handles.color = Color.green;
            Handles.DrawWireCube(lTarget, Vector3.one * 0.1f);
            Handles.DrawLine(t.position, lTarget);
            Handles.Label(lTarget + Vector3.up * 0.1f, "L Target");

            Handles.color = Color.red;
            Handles.DrawWireCube(rTarget, Vector3.one * 0.1f);
            Handles.DrawLine(t.position, rTarget);
            Handles.Label(rTarget + Vector3.up * 0.1f, "R Target");

            // Hints (knee positions)
            Handles.color = new Color(0, 1, 0, 0.5f);
            Handles.DrawWireCube(lHint, Vector3.one * 0.08f);
            Handles.DrawLine(lTarget, lHint);

            Handles.color = new Color(1, 0, 0, 0.5f);
            Handles.DrawWireCube(rHint, Vector3.one * 0.08f);
            Handles.DrawLine(rTarget, rHint);

            // Hand targets
            Handles.color = Color.blue;
            Handles.DrawWireCube(lHandTarget, Vector3.one * 0.08f);
            Handles.DrawLine(t.position + Vector3.up * 1.5f, lHandTarget);

            Handles.color = Color.magenta;
            Handles.DrawWireCube(rHandTarget, Vector3.one * 0.08f);
            Handles.DrawLine(t.position + Vector3.up * 1.5f, rHandTarget);

            // Head look target
            Handles.color = Color.yellow;
            Handles.DrawWireSphere(headLook, 0.15f);
            Handles.DrawLine(t.position + Vector3.up * 1.7f, headLook);
        }

        static void DrawFootPlacements(ProceduralAnimationController ctrl, Transform t, DebugData data)
        {
            // Show predicted next foot positions
            var lTargetField = typeof(ProceduralAnimationController).GetField("_leftFootTarget",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var rTargetField = typeof(ProceduralAnimationController).GetField("_rightFootTarget",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (lTargetField == null) return;

            var lTarget = (Vector3)lTargetField.GetValue(ctrl);
            var rTarget = (Vector3)rTargetField.GetValue(ctrl);

            // Current foot positions (from bone map)
            var boneMap = ctrl.GetComponent<ProceduralBoneMap>();
            if (boneMap == null) return;

            var lFoot = boneMap.Get(BoneRole.L_Foot);
            var rFoot = boneMap.Get(BoneRole.R_Foot);

            if (lFoot != null)
            {
                Handles.color = Color.cyan;
                Handles.DrawLine(lFoot.position, lTarget);
                Handles.DrawDottedLine(lFoot.position, lTarget, 5f);
            }
            if (rFoot != null)
            {
                Handles.color = Color.magenta;
                Handles.DrawLine(rFoot.position, rTarget);
                Handles.DrawDottedLine(rFoot.position, rTarget, 5f);
            }
        }

        static void DrawSpineCurve(ProceduralAnimationController ctrl, Transform t, DebugData data)
        {
            var boneMap = ctrl.GetComponent<ProceduralBoneMap>();
            if (boneMap == null) return;

            var spine0 = boneMap.Get(BoneRole.Spine0);
            var spine1 = boneMap.Get(BoneRole.Spine1);
            var spine2 = boneMap.Get(BoneRole.Spine2);
            var spine3 = boneMap.Get(BoneRole.Spine3);
            var neck = boneMap.Get(BoneRole.Neck);
            var head = boneMap.Get(BoneRole.Head);

            var spines = new List<Transform> { spine0, spine1, spine2, spine3, neck, head };
            spines.RemoveAll(x => x == null);

            if (spines.Count < 2) return;

            Handles.color = Color.white;
            for (int i = 0; i < spines.Count - 1; i++)
            {
                Handles.DrawLine(spines[i].position, spines[i + 1].position, 3f);
                Handles.DrawWireSphere(spines[i].position, 0.05f);
            }
            Handles.DrawWireSphere(spines[spines.Count - 1].position, 0.05f);

            // Draw control handles
            Handles.color = new Color(1, 1, 1, 0.3f);
            for (int i = 1; i < spines.Count - 1; i++)
            {
                Vector3 prev = spines[i - 1].position;
                Vector3 curr = spines[i].position;
                Vector3 next = spines[i + 1].position;

                Vector3 inTangent = (curr - prev).normalized * 0.1f;
                Vector3 outTangent = (next - curr).normalized * 0.1f;

                Handles.DrawLine(curr - inTangent, curr + outTangent);
            }
        }

        static void DrawVelocities(ProceduralAnimationController ctrl, Transform t)
        {
            Vector3 vel = ctrl.CurrentVelocity;
            if (vel.sqrMagnitude < 0.01f) return;

            Handles.color = Color.cyan;
            Handles.ArrowHandleCap(0, t.position + Vector3.up, Quaternion.LookRotation(vel), 
                vel.magnitude * 0.5f, EventType.Repaint);
            Handles.Label(t.position + Vector3.up + vel.normalized * 0.5f, $"Speed: {vel.magnitude:F1} m/s");
        }

        static void DrawGroundContacts(ProceduralAnimationController ctrl, Transform t)
        {
            var lGroundedField = typeof(ProceduralAnimationController).GetField("_leftFootGrounded",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var rGroundedField = typeof(ProceduralAnimationController).GetField("_rightFootGrounded",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var lHitField = typeof(ProceduralAnimationController).GetField("_leftFootHit",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var rHitField = typeof(ProceduralAnimationController).GetField("_rightFootHit",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (lGroundedField == null) return;

            bool lGrounded = (bool)lGroundedField.GetValue(ctrl);
            bool rGrounded = (bool)rGroundedField.GetValue(ctrl);
            RaycastHit lHit = (RaycastHit)lHitField.GetValue(ctrl);
            RaycastHit rHit = (RaycastHit)rHitField.GetValue(ctrl);

            if (lGrounded && lHit.point != Vector3.zero)
            {
                Handles.color = Color.green;
                Handles.DrawSolidDisc(lHit.point, lHit.normal, 0.15f);
                Handles.ArrowHandleCap(0, lHit.point, Quaternion.LookRotation(lHit.normal), 0.2f, EventType.Repaint);
            }
            if (rGrounded && rHit.point != Vector3.zero)
            {
                Handles.color = Color.red;
                Handles.DrawSolidDisc(rHit.point, rHit.normal, 0.15f);
                Handles.ArrowHandleCap(0, rHit.point, Quaternion.LookRotation(rHit.normal), 0.2f, EventType.Repaint);
            }
        }

        public struct DebugData
        {
            public ProceduralAnimationController Controller;
            public float LastUpdate;
            // Add more fields as needed for visualization
            public float LeftPhase;
            public float RightPhase;
            public float DutyCycle;
            public Vector3 LeftFootTarget;
            public Vector3 RightFootTarget;
            public Vector3 LeftHandTarget;
            public Vector3 RightHandTarget;
            public Vector3 HeadLookTarget;
            public Vector3 CurrentVelocity;
            public bool IsGrounded;
        }
    }

    /// <summary>
    /// Component to auto-register with debugger.
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
                // Update debug data
                var data = new ProceduralAnimDebugger.DebugData
                {
                    Controller = _ctrl,
                    LastUpdate = Time.realtimeSinceStartup,
                    // Populate via reflection
                };
                ProceduralAnimDebugger.UpdateData(_key, data);
            }
        }
    }
}