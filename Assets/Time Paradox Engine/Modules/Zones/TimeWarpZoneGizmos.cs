using UnityEngine;
using TimeWarp.Modules.Zones.Profiles;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TimeWarp.Modules.Zones
{
    /// <summary>
    /// Shared Scene-view visualisation for all TimeWarp zone components.
    ///
    /// Why this exists: a zone is just a trigger collider, so in the Scene view every
    /// zone used to look identical - a plain green collider outline. In a level with
    /// several overlapping zones there was no way to tell a Slow Motion field from a
    /// Stasis trap or a Rewind pocket without clicking each one and reading the profile.
    ///
    /// Everything here is editor-only in effect: Gizmos callbacks are never invoked in a
    /// build, and the Handles label is compiled out entirely via UNITY_EDITOR.
    /// </summary>
    public static class TimeWarpZoneGizmos
    {
        // --- Palette: one colour per meaning, consistent across 2D and 3D ---
        public static readonly Color SlowMotionColor = new Color(0.30f, 0.75f, 1.00f); // cyan-blue
        public static readonly Color StasisColor     = new Color(0.75f, 0.90f, 1.00f); // pale ice
        public static readonly Color RewindColor     = new Color(0.85f, 0.40f, 1.00f); // violet
        public static readonly Color KineticColor    = new Color(1.00f, 0.60f, 0.15f); // amber
        public static readonly Color PermissionColor = new Color(0.35f, 0.95f, 0.45f); // green
        public static readonly Color ErrorColor      = new Color(1.00f, 0.25f, 0.25f); // red

        private const float FillAlpha = 0.12f;

        public static Color ColorForProfile(TimeProfile profile)
        {
            if (profile == null) return ErrorColor;

            switch (profile.EffectType)
            {
                case TimeEffectType.Stasis: return StasisColor;
                case TimeEffectType.Rewind: return RewindColor;
                default:                    return SlowMotionColor;
            }
        }

        /// <summary>
        /// Human-readable summary shown next to the zone in the Scene view.
        /// </summary>
        public static string DescribeProfile(TimeProfile profile, string zoneTypeName)
        {
            if (profile == null)
                return zoneTypeName + "\nNO PROFILE ASSIGNED - zone is inactive";

            string effect;
            switch (profile.EffectType)
            {
                case TimeEffectType.Stasis:
                    effect = "Stasis (freeze)";
                    break;
                case TimeEffectType.Rewind:
                    effect = "Rewind x" + profile.RewindSpeed.ToString("0.##");
                    break;
                default:
                    effect = profile.UseRandomScale
                        ? "Slow Motion (random)"
                        : "Slow Motion x" + profile.TargetTimeScale.ToString("0.##");
                    break;
            }

            string exit;
            switch (profile.ExitRule)
            {
                case ExitCondition.Timed:
                    exit = "timed " + profile.Duration.ToString("0.##") + "s";
                    break;
                case ExitCondition.ManualOrExternal:
                    exit = "manual exit";
                    break;
                default:
                    exit = profile.Duration > 0f
                        ? "on leave +" + profile.Duration.ToString("0.##") + "s"
                        : "on leave";
                    break;
            }

            return profile.name + "\n" + effect + "\n" + exit;
        }

        // ==========================================
        // 3D COLLIDERS
        // ==========================================
        public static void DrawCollider(Collider col, Color color)
        {
            if (col == null) return;

            Matrix4x4 previous = Gizmos.matrix;

            if (col is BoxCollider box)
            {
                Gizmos.matrix = Matrix4x4.TRS(col.transform.position, col.transform.rotation, col.transform.lossyScale);
                DrawCube(box.center, box.size, color);
            }
            else if (col is SphereCollider sphere)
            {
                // Unity scales a SphereCollider by the largest axis
                Vector3 s = col.transform.lossyScale;
                float uniform = Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));
                Gizmos.matrix = Matrix4x4.TRS(col.transform.position, col.transform.rotation, Vector3.one * uniform);
                DrawSphere(sphere.center, sphere.radius, color);
            }
            else if (col is CapsuleCollider capsule)
            {
                Gizmos.matrix = Matrix4x4.TRS(col.transform.position, col.transform.rotation, col.transform.lossyScale);
                // Approximated with its local bounds - exact capsule wireframes need Handles
                Vector3 size = capsule.direction == 0
                    ? new Vector3(capsule.height, capsule.radius * 2f, capsule.radius * 2f)
                    : capsule.direction == 1
                        ? new Vector3(capsule.radius * 2f, capsule.height, capsule.radius * 2f)
                        : new Vector3(capsule.radius * 2f, capsule.radius * 2f, capsule.height);
                DrawCube(capsule.center, size, color);
            }
            else
            {
                // MeshCollider / TerrainCollider: fall back to world bounds
                Gizmos.matrix = Matrix4x4.identity;
                DrawCube(col.bounds.center, col.bounds.size, color);
            }

            Gizmos.matrix = previous;
        }

        // ==========================================
        // 2D COLLIDERS
        // ==========================================
        public static void DrawCollider2D(Collider2D col, Color color)
        {
            if (col == null) return;

            Matrix4x4 previous = Gizmos.matrix;

            if (col is BoxCollider2D box)
            {
                Gizmos.matrix = Matrix4x4.TRS(col.transform.position, col.transform.rotation, col.transform.lossyScale);
                DrawCube(box.offset, new Vector3(box.size.x, box.size.y, 0.01f), color);
            }
            else if (col is CircleCollider2D circle)
            {
                Vector3 s = col.transform.lossyScale;
                float uniform = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y));
                Gizmos.matrix = Matrix4x4.TRS(col.transform.position, col.transform.rotation, Vector3.one * uniform);
                DrawSphere(circle.offset, circle.radius, color);
            }
            else if (col is CapsuleCollider2D capsule)
            {
                Gizmos.matrix = Matrix4x4.TRS(col.transform.position, col.transform.rotation, col.transform.lossyScale);
                DrawCube(capsule.offset, new Vector3(capsule.size.x, capsule.size.y, 0.01f), color);
            }
            else
            {
                Gizmos.matrix = Matrix4x4.identity;
                Bounds b = col.bounds;
                DrawCube(b.center, new Vector3(b.size.x, b.size.y, 0.01f), color);
            }

            Gizmos.matrix = previous;
        }

        // ==========================================
        // PRIMITIVES
        // ==========================================
        private static void DrawCube(Vector3 center, Vector3 size, Color color)
        {
            Gizmos.color = new Color(color.r, color.g, color.b, FillAlpha);
            Gizmos.DrawCube(center, size);

            Gizmos.color = color;
            Gizmos.DrawWireCube(center, size);
        }

        private static void DrawSphere(Vector3 center, float radius, Color color)
        {
            Gizmos.color = new Color(color.r, color.g, color.b, FillAlpha);
            Gizmos.DrawSphere(center, radius);

            Gizmos.color = color;
            Gizmos.DrawWireSphere(center, radius);
        }

        // ==========================================
        // LABEL
        // ==========================================
        /// <summary>
        /// Draws a text label above the zone. Compiled out completely outside the Editor.
        /// </summary>
        public static void DrawLabel(Vector3 worldPosition, string text, Color color)
        {
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(text)) return;

            var style = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter,
                richText = false
            };
            style.normal.textColor = color;

            Handles.Label(worldPosition, text, style);
#endif
        }

        /// <summary>
        /// Convenience: label anchored just above the collider's bounds.
        /// </summary>
        public static Vector3 LabelAnchor(Bounds bounds)
        {
            return new Vector3(bounds.center.x, bounds.max.y + 0.15f, bounds.center.z);
        }
    }
}
