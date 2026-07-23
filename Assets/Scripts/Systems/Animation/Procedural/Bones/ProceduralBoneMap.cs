using System.Collections.Generic;
using UnityEngine;
using ProjectName.Systems.Animation.Procedural.Bones;

namespace ProjectName.Systems.Animation.Procedural.Bones
{
    /// <summary>
    /// Simple bone map container for runtime access.
    /// </summary>
    [System.Serializable]
    public struct BoneEntry
    {
        public BoneRole Role;
        public Transform Transform;
    }

    /// <summary>
    /// MonoBehaviour wrapper for procedural bone mapping.
    /// Uses ProceduralBoneUtility to auto-map, exposes lookups.
    /// </summary>
    public class ProceduralBoneMap : MonoBehaviour
    {
        [SerializeField] Animator _animator;
        [SerializeField] BoneEntry[] _bones = new BoneEntry[0];

        Dictionary<BoneRole, Transform> _boneDict = new Dictionary<BoneRole, Transform>();

        public void Initialize(Animator animator = null)
        {
            if (animator != null) _animator = animator;
            if (_animator == null) _animator = GetComponentInChildren<Animator>();

            if (_animator == null)
            {
                UnityEngine.Debug.LogWarning("[ProceduralBoneMap] No Animator found");
                return;
            }

            BuildMap();
        }

        void BuildMap()
        {
            _boneDict.Clear();

            // Auto-map using utility
            var utilityMap = ProceduralBoneUtility.BuildMap(_animator);
            foreach (var kvp in utilityMap)
            {
                if (kvp.Value != null)
                    _boneDict[kvp.Key] = kvp.Value;
            }

            // Also add serialized entries (for manual overrides)
            foreach (var entry in _bones)
            {
                if (entry.Transform != null)
                    _boneDict[entry.Role] = entry.Transform;
            }

            UnityEngine.Debug.Log($"[ProceduralBoneMap] Mapped {_boneDict.Count} bones");
        }

        public Transform Get(BoneRole role)
        {
            _boneDict.TryGetValue(role, out Transform t);
            return t;
        }

        public bool Has(BoneRole role) => _boneDict.ContainsKey(role);

        public IReadOnlyDictionary<BoneRole, Transform> AllBones => _boneDict;

        /// <summary>
        /// Returns all serialized bone entries. Used by neural/hybrid controllers for iteration.
        /// </summary>
        public BoneEntry[] GetAllBones() => _bones;

        void OnValidate()
        {
            if (Application.isPlaying) return;
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
        }
    }
}