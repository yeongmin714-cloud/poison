using System.Collections.Generic;
using UnityEngine;

namespace TimeWarp.Modules.Paradox
{
    /// <summary>
    /// Reuse pool for Time Echo Ghosts.
    ///
    /// WHY THIS IS WORTH IT: spawning a ghost is not just an Instantiate. The spawner
    /// then runs four separate GetComponentsInChildren(true) sweeps and destroys every
    /// TimeWarpIdentity, TimeParadoxModule, Camera and Cinemachine component on the
    /// clone, and TimeEchoGhost.Initialize runs a fifth sweep that strips every
    /// remaining foreign MonoBehaviour. On a rigged character that is by far the most
    /// expensive part of the operation - far more than the Instantiate itself.
    ///
    /// A released ghost has already been through all of that. Its hierarchy is exactly
    /// the shape the next ghost needs, so reusing it skips the whole stripping pass.
    ///
    /// Pooled instances are keyed by the source prefab: a ghost built from prefab A
    /// must never be handed out for prefab B.
    ///
    /// Follows the same request/release shape as the engine's particle RAM budget.
    /// </summary>
    public static class TimeGhostPool
    {
        // Key: the prefab (or source GameObject) the ghost was built from.
        //
        // Keyed by the object REFERENCE rather than an instance id. UnityEngine.Object
        // implements GetHashCode/Equals by identity, so this is equivalent - and it avoids
        // GetInstanceID(), which Unity 6.5 deprecated in favour of GetEntityId(). Using the
        // reference keeps this file compiling on 6.5 and on older versions alike, since
        // GetEntityId does not exist before 6.5.
        private static readonly Dictionary<GameObject, Stack<TimeEchoGhost>> _pools =
            new Dictionary<GameObject, Stack<TimeEchoGhost>>();

        private static Transform _poolRoot;

        /// <summary>Hard cap per prefab, so an unusual burst cannot pin memory forever.</summary>
        public const int MaxPooledPerPrefab = 16;

        /// <summary>
        /// Domain-reload safety: static state must not survive a Play session
        /// (see the same pattern in TimeWarpManager / TimeParadoxModule).
        /// The pooled GameObjects themselves are destroyed with the scene, so keeping
        /// the dictionary would leave us holding destroyed references.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _pools.Clear();
            _poolRoot = null;
        }

        private static Transform GetPoolRoot()
        {
            if (_poolRoot != null) return _poolRoot;

            var go = new GameObject("[TimeWarp Ghost Pool]");
            go.SetActive(false); // keeps parked ghosts fully inert
            _poolRoot = go.transform;
            return _poolRoot;
        }

        /// <summary>
        /// Try to take a reusable ghost built from this prefab.
        /// Returns null when the pool is empty - the caller must then Instantiate normally.
        /// </summary>
        public static TimeEchoGhost TryRent(GameObject prefabKey, Vector3 position, Quaternion rotation)
        {
            if (prefabKey == null) return null;

            if (!_pools.TryGetValue(prefabKey, out var stack)) return null;

            // Entries can be null if the scene was unloaded while they were parked.
            while (stack.Count > 0)
            {
                TimeEchoGhost ghost = stack.Pop();
                if (ghost == null) continue;

                Transform t = ghost.transform;
                t.SetParent(null, false);
                t.SetPositionAndRotation(position, rotation);

                ghost.ResetForReuse();
                return ghost;
            }

            return null;
        }

        /// <summary>
        /// Park a finished ghost for reuse. Returns false if the pool is full or the
        /// ghost cannot be pooled, in which case the caller should destroy it.
        /// </summary>
        public static bool TryReturn(TimeEchoGhost ghost)
        {
            if (ghost == null) return false;

            GameObject prefabKey = ghost.PoolPrefabKey;
            if (prefabKey == null) return false; // not pool-managed

            if (!_pools.TryGetValue(prefabKey, out var stack))
            {
                stack = new Stack<TimeEchoGhost>();
                _pools[prefabKey] = stack;
            }

            if (stack.Count >= MaxPooledPerPrefab) return false;

            ghost.transform.SetParent(GetPoolRoot(), false);
            stack.Push(ghost);
            return true;
        }

        /// <summary>
        /// Destroys every parked ghost. Useful between levels when the pooled prefabs
        /// are no longer relevant.
        /// </summary>
        public static void Clear()
        {
            foreach (var stack in _pools.Values)
            {
                while (stack.Count > 0)
                {
                    TimeEchoGhost ghost = stack.Pop();
                    if (ghost != null) Object.Destroy(ghost.gameObject);
                }
            }
            _pools.Clear();
        }

        /// <summary>Number of ghosts currently parked for a given prefab (diagnostics).</summary>
        public static int CountFor(GameObject prefabKey)
        {
            if (prefabKey == null) return 0;
            return _pools.TryGetValue(prefabKey, out var stack) ? stack.Count : 0;
        }
    }
}
