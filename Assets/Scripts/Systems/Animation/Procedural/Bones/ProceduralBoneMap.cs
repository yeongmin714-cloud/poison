using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems.Animation.Procedural.Bones
{
    /// <summary>
    /// Standardized bone roles for procedural animation.
    /// Matches ProceduralBoneUtility.BoneRole but without the static utility dependency.
    /// </summary>
    public enum BoneRole
    {
        // Root
        Root,           // Hips / Pelvis / Root
        Hip,            // Hips (Root or child)

        // Spine chain (root → head)
        Spine0,         // Lower spine
        Spine1,         // Mid spine
        Spine2,         // Upper spine / Chest
        Spine3,         // Neck base
        Neck,           // Neck
        Head,           // Head

        // Left Arm
        L_Clavicle,     // Left clavicle / collar
        L_Shoulder,     // Left shoulder / upper arm
        L_Elbow,        // Left elbow / lower arm
        L_Wrist,        // Left wrist / hand
        L_Hand,         // Left hand (end effector)
        L_Fingers,      // Optional: finger root

        // Right Arm
        R_Clavicle,
        R_Shoulder,
        R_Elbow,
        R_Wrist,
        R_Hand,
        R_Fingers,

        // Left Leg
        L_Hip,          // Left hip / thigh root
        L_Knee,         // Left knee / shin
        L_Ankle,        // Left ankle / foot
        L_Foot,         // Left foot (end effector)
        L_Toes,         // Optional: toes

        // Right Leg
        R_Hip,
        R_Knee,
        R_Ankle,
        R_Foot,
        R_Toes,
    }

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
                Debug.LogWarning("[ProceduralBoneMap] No Animator found");
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

            Debug.Log($"[ProceduralBoneMap] Mapped {_boneDict.Count} bones");
        }

        public Transform Get(BoneRole role)
        {
            _boneDict.TryGetValue(role, out Transform t);
            return t;
        }

        public bool Has(BoneRole role) => _boneDict.ContainsKey(role);

        public IReadOnlyDictionary<BoneRole, Transform> AllBones => _boneDict;

        void OnValidate()
        {
            if (Application.isPlaying) return;
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
        }
    }
}