using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using TimeWarp.Core;
using TimeWarp.Core.Managers;
using TimeWarp.Modules.Zones.Components;
using TimeWarp.Modules.Zones.Profiles;
using TimeWarp.Modules.Paradox;

namespace TimeWarp.EditorTools
{
    /// <summary>
    /// Menu entries for setting up and sanity-checking a TimeWarp scene.
    ///
    /// The engine previously shipped with no menu items at all, so the only path in was
    /// the 96-page PDF. The scene validator matters more than the creation shortcut: it
    /// collects, in one place, every silent misconfiguration this engine can have - the
    /// failure modes that produce no error and simply make things quietly not work.
    /// </summary>
    public static class TimeWarpMenuItems
    {
        // ==========================================
        // CREATION
        // ==========================================

        [MenuItem("GameObject/TimeWarp/Time Warp Manager", false, 10)]
        public static void CreateManager(MenuCommand command)
        {
            var existing = Object.FindAnyObjectByType<TimeWarpManager>();
            if (existing != null)
            {
                EditorUtility.DisplayDialog(
                    "Manager already exists",
                    "This scene already has a Time Warp Manager. Only one may be active - " +
                    "the duplicate would destroy itself on Awake.",
                    "OK");

                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            var go = new GameObject("Time Warp Manager");
            go.AddComponent<TimeWarpManager>();

            GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(go, "Create Time Warp Manager");

            Selection.activeGameObject = go;
        }

        [MenuItem("GameObject/TimeWarp/Time Warp Zone (3D)", false, 11)]
        public static void CreateZone3D(MenuCommand command)
        {
            var go = new GameObject("Time Warp Zone");

            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;          // the single most common setup mistake
            box.size = Vector3.one * 5f;

            go.AddComponent<TimeWarpZone>();

            GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(go, "Create Time Warp Zone");

            Selection.activeGameObject = go;

            Debug.Log("[TimeWarp] Zone created. Assign a Time Profile to it - without one the zone does nothing.", go);
        }

        // ==========================================
        // VALIDATION
        // ==========================================

        [MenuItem("Tools/TimeWarp/Validate Scene", false, 1)]
        public static void ValidateScene()
        {
            int errors = 0;
            int warnings = 0;
            var summary = new StringBuilder();

            void Error(string message, Object context)
            {
                errors++;
                summary.AppendLine("ERROR   " + message);
                Debug.LogError("[TimeWarp] " + message, context);
            }

            void Warn(string message, Object context)
            {
                warnings++;
                summary.AppendLine("WARNING " + message);
                Debug.LogWarning("[TimeWarp] " + message, context);
            }

            // --- Manager ---
            var managers = Object.FindObjectsByType<TimeWarpManager>(FindObjectsInactive.Include);

            if (managers.Length == 0)
            {
                Error("No Time Warp Manager in the scene. Nothing will record or rewind. " +
                      "Create one via GameObject > TimeWarp > Time Warp Manager.", null);
            }
            else if (managers.Length > 1)
            {
                Error($"{managers.Length} Time Warp Managers found. All but the first destroy " +
                      $"themselves on Awake, which is rarely what you want.", managers[0]);
            }
            else if (managers[0].MaxRecordTime <= 0f)
            {
                Error($"Manager Max Record Time is {managers[0].MaxRecordTime}s - no history " +
                      $"will be recorded.", managers[0]);
            }

            // --- Identities ---
            var identities = Object.FindObjectsByType<TimeWarpIdentity>(FindObjectsInactive.Include);

            if (identities.Length == 0)
            {
                Warn("No Time Warp Identity in the scene, so no object can be rewound.", null);
            }

            // --- 3D zones ---
            foreach (var zone in Object.FindObjectsByType<TimeWarpZone>(FindObjectsInactive.Include))
            {
                var col = zone.GetComponent<Collider>();

                if (col == null)
                    Error($"Zone \"{zone.name}\" has no Collider, so it can never trigger.", zone);
                else if (!col.isTrigger)
                    Error($"Zone \"{zone.name}\" has a Collider that is not Is Trigger.", zone);

                if (zone.Profile == null)
                {
                    Error($"Zone \"{zone.name}\" has no Time Profile assigned - it is inactive.", zone);
                }
                else
                {
                    ValidateProfileFilter(zone.name, zone.Profile.AffectedLayers,
                        zone.Profile.AffectedTags, zone, Warn);
                }
            }

            // --- 2D zones ---
            foreach (var zone in Object.FindObjectsByType<TimeWarpZone2D>(FindObjectsInactive.Include))
            {
                var col = zone.GetComponent<Collider2D>();

                if (col == null)
                    Error($"Zone 2D \"{zone.name}\" has no Collider2D.", zone);
                else if (!col.isTrigger)
                    Error($"Zone 2D \"{zone.name}\" has a Collider2D that is not Is Trigger.", zone);

                if (zone.Profile == null)
                {
                    Error($"Zone 2D \"{zone.name}\" has no Time Profile assigned - it is inactive.", zone);
                }
                else
                {
                    ValidateProfileFilter(zone.name, zone.Profile.AffectedLayers,
                        zone.Profile.AffectedTags, zone, Warn);
                }
            }

            // --- Ghost setup ---
            foreach (var paradox in Object.FindObjectsByType<TimeParadoxModule>(FindObjectsInactive.Include))
            {
                var identity = paradox.GetComponent<TimeWarpIdentity>();
                if (identity == null)
                {
                    Error($"\"{paradox.name}\" has a Time Paradox Module but no Time Warp Identity.", paradox);
                    continue;
                }

                var settings = identity.EchoSettings;
                if (settings == null) continue;

                if (!string.IsNullOrEmpty(settings.GhostLayerName) &&
                    LayerMask.NameToLayer(settings.GhostLayerName) == -1)
                {
                    Error($"\"{paradox.name}\": ghost layer \"{settings.GhostLayerName}\" does not exist. " +
                          $"Ghosts will fall back to Default and collide with everything, including " +
                          $"the player.", paradox);
                }
            }

            // --- Report ---
            string headline = errors == 0 && warnings == 0
                ? "Scene looks good - no problems found."
                : $"Found {errors} error(s) and {warnings} warning(s). Details are in the Console; " +
                  $"click an entry to select the object.";

            Debug.Log("[TimeWarp] Scene validation complete. " + headline);

            EditorUtility.DisplayDialog("TimeWarp - Validate Scene",
                headline + (summary.Length > 0 ? "\n\n" + Truncate(summary.ToString(), 1200) : ""),
                "OK");
        }

        private static void ValidateProfileFilter(
            string zoneName, LayerMask layers, List<string> tags, Object context,
            System.Action<string, Object> warn)
        {
            bool hasTags = tags != null && tags.Count > 0;

            if (layers.value == 0 && !hasTags)
            {
                warn($"Zone \"{zoneName}\" matches nothing: Affected Layers is Nothing and there " +
                     $"are no Affected Tags.", context);
                return;
            }

            if (!hasTags) return;

            // An undefined tag would throw at runtime; the engine survives it, but the
            // filter entry silently does nothing.
            foreach (string tag in tags)
            {
                if (string.IsNullOrEmpty(tag)) continue;
                if (IsTagDefined(tag)) continue;

                warn($"Zone \"{zoneName}\" filters on tag \"{tag}\", which is not defined in the " +
                     $"Tag Manager. That filter entry will be ignored.", context);
            }

            if (layers.value == 0)
            {
                warn($"Zone \"{zoneName}\" filters by tag only (Affected Layers is Nothing). " +
                     $"Objects that ENTER the zone are caught, but objects already inside it at " +
                     $"scene start are not: the initial overlap scan is a physics query and can " +
                     $"only filter by layer.", context);
            }
        }

        private static bool IsTagDefined(string tag)
        {
            foreach (string defined in UnityEditorInternal.InternalEditorUtility.tags)
            {
                if (defined == tag) return true;
            }
            return false;
        }

        private static string Truncate(string text, int max)
        {
            return text.Length <= max ? text : text.Substring(0, max) + "\n...";
        }
    }
}
