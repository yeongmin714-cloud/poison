using UnityEngine;
using ProjectName.Systems.Animation.Procedural.Bones;
using ProjectName.Systems.Animation.Procedural.IK;
using static ProjectName.Systems.Animation.Procedural.IK.LimbIKSolver;

namespace ProjectName.Systems.Animation.Procedural
{
    /// <summary>
    /// 특수 생물(거미, 조개 등) 전용 프로시저럴 애니메이터.
    /// ModelAnimatorAssigner에서 비인간형 감지 시 부착됨.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(ProceduralBoneMap))]
    public class SpecialCreatureAnimator : MonoBehaviour
    {
        public enum CreatureType { Spider, Clam, Slime, Spirit, LargeMonster }

        [Header("Creature Type")]
        public CreatureType creatureType = CreatureType.Spider;

        [Header("Locomotion")]
        [SerializeField] float _moveSpeed = 3f;
        [SerializeField] float _turnSpeed = 360f;

        Animator _animator;
        ProceduralBoneMap _boneMap;
        Rigidbody _rigidbody;

        void Awake()
        {
            _animator = GetComponent<Animator>();
            _boneMap = GetComponent<ProceduralBoneMap>();
            _rigidbody = GetComponent<Rigidbody>();
            _animator.applyRootMotion = false;
            _animator.updateMode = AnimatorUpdateMode.Fixed;
            _animator.animatePhysics = true;
            _boneMap.Initialize(_animator);
        }

        void Update()
        {
            // 기본 이동 로직 (자식 클래스에서 오버라이드)
        }
    }
}