using System.Collections.Generic;
using UnityEngine;

namespace TimeWarp.Core
{
    /// <summary>
    /// Shared, allocation-free tag matching used by every optional tag filter in the engine.
    ///
    /// WHY THIS EXISTS: GameObject.CompareTag is the non-allocating way to test a tag, but
    /// it throws UnityException when the tag is not defined in the Tag Manager. A single
    /// typo in an inspector field would otherwise throw on every OnTriggerEnter, every
    /// frame, for the lifetime of the scene.
    ///
    /// This helper catches that once per bad tag, logs a single actionable warning, and
    /// then permanently skips that tag - so a typo degrades into "this filter entry does
    /// nothing" instead of an exception storm.
    /// </summary>
    public static class TimeWarpTagUtility
    {
        // Tags that were found to be undefined. Skipped on every later call.
        private static readonly HashSet<string> _invalidTags = new HashSet<string>();

        /// <summary>
        /// Domain-reload safety, consistent with the rest of the engine: a tag added to
        /// the Tag Manager mid-session must be picked up on the next Play.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _invalidTags.Clear();
        }

        /// <summary>
        /// True when the list is non-empty and the object carries at least one of the tags.
        ///
        /// An empty or null list returns FALSE, never true. Callers combine this with their
        /// layer test using OR, so "no tags configured" must contribute nothing rather than
        /// matching everything.
        /// </summary>
        public static bool HasAnyTag(GameObject go, List<string> tags)
        {
            if (go == null || tags == null || tags.Count == 0) return false;

            for (int i = 0; i < tags.Count; i++)
            {
                string tag = tags[i];

                if (string.IsNullOrEmpty(tag)) continue;
                if (_invalidTags.Contains(tag)) continue;

                try
                {
                    if (go.CompareTag(tag)) return true;
                }
                catch (UnityException)
                {
                    _invalidTags.Add(tag);
                    Debug.LogWarning(
                        $"[TimeWarp] Tag \"{tag}\" is not defined in the Tag Manager, so this filter entry " +
                        $"will be ignored. Add it under Project Settings > Tags and Layers, or remove it " +
                        $"from the filter.");
                }
            }

            return false;
        }

        /// <summary>
        /// Convenience overload for a plain layer mask test.
        /// </summary>
        public static bool IsInLayerMask(GameObject go, LayerMask mask)
        {
            if (go == null) return false;
            return (mask.value & (1 << go.layer)) != 0;
        }

        /// <summary>
        /// The standard combined test used across the engine: layer OR tag.
        ///
        /// OR (not AND) keeps every existing setup working unchanged - the layer mask
        /// alone still decides everything until someone actually fills in a tag list.
        /// To filter purely by tag, set the layer mask to Nothing.
        /// </summary>
        public static bool MatchesLayerOrTag(GameObject go, LayerMask mask, List<string> tags)
        {
            return IsInLayerMask(go, mask) || HasAnyTag(go, tags);
        }
    }
}
