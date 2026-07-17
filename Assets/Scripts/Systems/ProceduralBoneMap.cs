using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 프로시저럴 애니메이션용 본 매핑 런타임 컨테이너 (MonoBehaviour 래퍼).
    /// Animator에서 본 Transform을 찾아 역할별로 캐싱.
    /// </summary>
    public class ProceduralBoneMap : MonoBehaviour
    {
        [System.Serializable]
        public struct BoneEntry
        {
            public ProceduralBoneUtility.BoneRole role;
            public Transform transform;
        }

        [SerializeField] private List<BoneEntry> _bones = new List<BoneEntry>();
        private Dictionary<ProceduralBoneUtility.BoneRole, Transform> _boneMap = new Dictionary<ProceduralBoneUtility.BoneRole, Transform>();

        public void Initialize(Animator animator)
        {
            _boneMap.Clear();

            if (animator == null) return;

            var builtMap = ProceduralBoneUtility.BuildMap(animator);
            foreach (var kvp in builtMap)
            {
                if (kvp.Value != null)
                    _boneMap[kvp.Key] = kvp.Value;
            }

            Debug.Log($"[ProceduralBoneMap] Initialized {_boneMap.Count} bones");
        }

        public Transform Get(ProceduralBoneUtility.BoneRole role)
        {
            _boneMap.TryGetValue(role, out Transform t);
            return t;
        }

        public bool Has(ProceduralBoneUtility.BoneRole role) => _boneMap.ContainsKey(role);

        public IReadOnlyDictionary<ProceduralBoneUtility.BoneRole, Transform> AllBones => _boneMap;

        private void OnValidate()
        {
            // 에디터에서 수동 매핑도 가능하도록
        }
    }
}