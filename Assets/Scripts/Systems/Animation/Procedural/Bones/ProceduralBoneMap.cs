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
        BoneFamilyHint _familyHint = BoneFamilyHint.None; // 마지막 Initialize의 계열 힌트(재빌드 시 유지)

        Dictionary<BoneRole, Transform> _boneDict = new Dictionary<BoneRole, Transform>();

        public void Initialize(Animator animator = null)
        {
            Initialize(animator, _familyHint);
        }

        /// <summary>
        /// 계열 힌트 지정 초기화 — [2026-09-22] 익명 리그(bone_N) 토폴로지 매핑이 힌트에 따라
        /// 4족(앞/뒤 4다리)/2족(2다리+2팔)/특수형(Root만)으로 배치를 다르게 한다.
        /// ModelAnimatorAssigner/QuadrupedProceduralAnimation/ProceduralAnimationController가 각자 호출해도
        /// 동일 계열 힌트로 재빌드되므로 결과가 일관된다.
        /// </summary>
        public void Initialize(Animator animator, BoneFamilyHint familyHint)
        {
            _familyHint = familyHint;
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
            var utilityMap = ProceduralBoneUtility.BuildMap(_animator, _familyHint);
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