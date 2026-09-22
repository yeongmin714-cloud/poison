// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
// [2026-09-22 뉴럴 애니 제거] NeuralAnimationController/HybridAnimationController/ProgressiveRolloutManager
//   전면 퇴역 — ONNX 정책 모델이 미배치 상태로 부착만 하고 출력이 0인 경로(스폰 렉+경고 소음의 뿌리)였다.
//   애니 경로는 이제 단일 계약: 4족=QuadrupedProcedural(Locomotion+Animation) / 2족=ProceduralAnimationController
//   / 특수형=SpecialCreatureAnimator / 휴머노이드(병사·NPC·플레이어)=HumanoidClipDriver+*_AC 클립.
#pragma warning disable 618
using UnityEngine;
using UnityEditor;
using ProjectName.Systems.Animation.Procedural;
using ProjectName.Systems.Animation.Procedural.Locomotion.Quadruped;
using ProjectName.Systems.Animation.Procedural.Bones;

namespace ProjectName.Systems.Animation
{
    /// <summary>
    /// GLB 모델 타입(2족/4족/비인간형) 감지 → 적절한 애니메이션 컨트롤러 자동 부착
    /// Player, Monster, Guard, NPC 등 모든 캐릭터에 부착되어야 함
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(ProceduralBoneMap))]
    public class ModelAnimatorAssigner : MonoBehaviour
    {
        [Header("Auto-Detection")]
        [SerializeField] bool _autoDetectOnAwake = true;
        [SerializeField] bool _forceBiped = false;
        [SerializeField] bool _forceQuadruped = false;
        [SerializeField] bool _isSpecialCreature = false;

        Animator _animator;
        ProceduralBoneMap _boneMap;
        Rigidbody _rigidbody;
        ProceduralAnimationController _proceduralAnim;
        QuadrupedProceduralLocomotion _quadrupedLocomotion;
        SpecialCreatureAnimator _specialCreatureAnim;

        void Awake()
        {
            if (!_autoDetectOnAwake) return;
            SetupAnimationSystem();
        }

        public void SetupAnimationSystem()
        {
            _animator = GetComponent<Animator>();
            _boneMap = GetComponent<ProceduralBoneMap>();
            _rigidbody = GetComponent<Rigidbody>();

            if (_animator == null)
            {
                _animator = gameObject.AddComponent<Animator>();
            }
            if (_boneMap == null)
            {
                _boneMap = gameObject.AddComponent<ProceduralBoneMap>();
            }
            if (_rigidbody == null)
            {
                _rigidbody = gameObject.AddComponent<Rigidbody>();
            }

            _animator.applyRootMotion = false;
            _animator.updateMode = AnimatorUpdateMode.Fixed;
            _animator.animatePhysics = true;
            _boneMap.Initialize(_animator);

            // 타입 감지 및 분기
            bool isBiped = _forceBiped || (!_forceQuadruped && !_isSpecialCreature && _animator.isHuman);
            bool isQuadruped = _forceQuadruped || (!_forceBiped && !_isSpecialCreature && !_animator.isHuman);
            bool isSpecial = _isSpecialCreature || (!isBiped && !isQuadruped);

            if (isBiped)
            {
                SetupBiped();
            }
            else if (isQuadruped)
            {
                SetupQuadruped();
            }
            else if (isSpecial)
            {
                SetupSpecialCreature();
            }
        }

        void SetupBiped()
        {
            // ProceduralAnimationController (Locomotion/Jump/Roll/Gather 등)
            _proceduralAnim = GetComponent<ProceduralAnimationController>();
            if (_proceduralAnim == null)
            {
                _proceduralAnim = gameObject.AddComponent<ProceduralAnimationController>();
            }
            _proceduralAnim.SetBoneMap(_boneMap);
        }

        void SetupQuadruped()
        {
            // QuadrupedProceduralLocomotion (Walk/Trot/Pace/Gallop 자동 전이)
            // RequireComponent로 QuadrupedProceduralAnimation이 함께 부착된다.
            _quadrupedLocomotion = GetComponent<QuadrupedProceduralLocomotion>();
            if (_quadrupedLocomotion == null)
            {
                _quadrupedLocomotion = gameObject.AddComponent<QuadrupedProceduralLocomotion>();
            }
        }

        void SetupSpecialCreature()
        {
            _specialCreatureAnim = GetComponent<SpecialCreatureAnimator>();
            if (_specialCreatureAnim == null)
            {
                _specialCreatureAnim = gameObject.AddComponent<SpecialCreatureAnimator>();
            }
        }

        // ================================================================
        // Public API
        // ================================================================

        /// <summary>
        /// 외부에서 강제 타입 지정 후 재설정
        /// </summary>
        public void ForceBiped(bool biped = true)
        {
            _forceBiped = biped;
            _forceQuadruped = !biped;
            _isSpecialCreature = false;
            RemoveAllAnimationComponents();
            SetupAnimationSystem();
        }

        public void ForceQuadruped(bool quadruped = true)
        {
            _forceQuadruped = quadruped;
            _forceBiped = !quadruped;
            _isSpecialCreature = false;
            RemoveAllAnimationComponents();
            SetupAnimationSystem();
        }

        public void ForceSpecialCreature(SpecialCreatureAnimator.CreatureType type)
        {
            _isSpecialCreature = true;
            _forceBiped = false;
            _forceQuadruped = false;
            if (_specialCreatureAnim != null)
                _specialCreatureAnim.creatureType = type;
            RemoveAllAnimationComponents();
            SetupAnimationSystem();
        }

        void RemoveAllAnimationComponents()
        {
            if (_proceduralAnim != null) DestroyImmediate(_proceduralAnim);
            if (_quadrupedLocomotion != null) DestroyImmediate(_quadrupedLocomotion);
            if (_specialCreatureAnim != null) DestroyImmediate(_specialCreatureAnim);
            // 자동감지가 먼저 부착했다가 ForceBiped 재정렬 시 잔존하는 컴포넌트 정리.
            // (위 필드로 추적되지 않는 타입 — RequireComponent(Rigidbody) 의존 때문에 rb 제거도 막는다)
            // 네임스페이스가 다르므로(ProjectName.Systems) 정규화된 이름으로 참조.
            foreach (var quadrupedAnim in GetComponents<ProjectName.Systems.QuadrupedProceduralAnimation>())
                DestroyImmediate(quadrupedAnim);
        }

        public ProceduralAnimationController ProceduralController => _proceduralAnim;
        public QuadrupedProceduralLocomotion QuadrupedController => _quadrupedLocomotion;
        public SpecialCreatureAnimator SpecialCreatureController => _specialCreatureAnim;
    }

    // ================================================================
    // Editor Menu for manual setup
    // ================================================================
#if UNITY_EDITOR
    public static class ModelAnimatorAssignerEditor
    {
        [MenuItem("Tools/Animation/Setup ModelAnimatorAssigner on Selection")]
        public static void SetupOnSelection()
        {
            foreach (var go in Selection.gameObjects)
            {
                var assigner = go.GetComponent<ModelAnimatorAssigner>();
                if (assigner == null)
                {
                    assigner = go.AddComponent<ModelAnimatorAssigner>();
                }
                assigner.SetupAnimationSystem();
                EditorUtility.SetDirty(go);
            }
            Debug.Log($"[ModelAnimatorAssigner] Setup complete on {Selection.gameObjects.Length} object(s)");
        }

        [MenuItem("Tools/Animation/Force Biped on Selection")]
        public static void ForceBipedOnSelection()
        {
            foreach (var go in Selection.gameObjects)
            {
                var assigner = go.GetComponent<ModelAnimatorAssigner>();
                if (assigner == null) assigner = go.AddComponent<ModelAnimatorAssigner>();
                assigner.ForceBiped(true);
                EditorUtility.SetDirty(go);
            }
        }

        [MenuItem("Tools/Animation/Force Quadruped on Selection")]
        public static void ForceQuadrupedOnSelection()
        {
            foreach (var go in Selection.gameObjects)
            {
                var assigner = go.GetComponent<ModelAnimatorAssigner>();
                if (assigner == null) assigner = go.AddComponent<ModelAnimatorAssigner>();
                assigner.ForceQuadruped(true);
                EditorUtility.SetDirty(go);
            }
        }
    }
#endif
}
