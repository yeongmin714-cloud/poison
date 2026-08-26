using UnityEngine;
using UnityEditor;
using ProjectName.Systems.Animation.Neural;
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

        [Header("Neural Animation")]
        [SerializeField] bool _enableNeural = true;
        [SerializeField] NeuralAnimationController.PolicyType _defaultPolicy = NeuralAnimationController.PolicyType.Locomotion;

        Animator _animator;
        ProceduralBoneMap _boneMap;
        Rigidbody _rigidbody;
        NeuralAnimationController _neuralAnim;
        ProceduralAnimationController _proceduralAnim;
        HybridAnimationController _hybridAnim;
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

            // HybridAnimationController는 항상 부착 (블렌딩용)
            SetupHybrid();

            // ProgressiveRolloutManager로 설정
            if (ProgressiveRolloutManager.Instance != null)
            {
                ProgressiveRolloutManager.Instance.ConfigureHybridController(_hybridAnim);
            }

            // NeuralAnimationController 모델 로드
            if (_enableNeural && _neuralAnim != null)
            {
                LoadNeuralModels();
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

            // NeuralAnimationController (Combat/React/Interact/Fly/Swim/Mount/Climb 등)
            if (_enableNeural)
            {
                _neuralAnim = GetComponent<NeuralAnimationController>();
                if (_neuralAnim == null)
                {
                    _neuralAnim = gameObject.AddComponent<NeuralAnimationController>();
                }
                _neuralAnim.SetBoneMap(_boneMap);
            }
        }

        void SetupQuadruped()
        {
            // QuadrupedProceduralLocomotion (Walk/Trot/Pace/Gallop 자동 전이)
            _quadrupedLocomotion = GetComponent<QuadrupedProceduralLocomotion>();
            if (_quadrupedLocomotion == null)
            {
                _quadrupedLocomotion = gameObject.AddComponent<QuadrupedProceduralLocomotion>();
            }

            // NeuralAnimationController for quadruped policies
            if (_enableNeural)
            {
                _neuralAnim = GetComponent<NeuralAnimationController>();
                if (_neuralAnim == null)
                {
                    _neuralAnim = gameObject.AddComponent<NeuralAnimationController>();
                }
                _neuralAnim.IsQuadruped = true;
                _neuralAnim.SetBoneMap(_boneMap);
            }
        }

        void SetupSpecialCreature()
        {
            _specialCreatureAnim = GetComponent<SpecialCreatureAnimator>();
            if (_specialCreatureAnim == null)
            {
                _specialCreatureAnim = gameObject.AddComponent<SpecialCreatureAnimator>();
            }

            // Neural for special creature policies (if any)
            if (_enableNeural)
            {
                _neuralAnim = GetComponent<NeuralAnimationController>();
                if (_neuralAnim == null)
                {
                    _neuralAnim = gameObject.AddComponent<NeuralAnimationController>();
                }
                _neuralAnim.SetBoneMap(_boneMap);
            }
        }

        void SetupHybrid()
        {
            _hybridAnim = GetComponent<HybridAnimationController>();
            if (_hybridAnim == null)
            {
                _hybridAnim = gameObject.AddComponent<HybridAnimationController>();
            }
        }

        void LoadNeuralModels()
        {
            if (_neuralAnim == null) return;

            var db = Resources.Load<NeuralModelDatabase>("NeuralModelDatabase");
            if (db == null)
            {
                Debug.LogWarning("[ModelAnimatorAssigner] NeuralModelDatabase not found in Resources. Run Tools/Neural/Auto-Setup Model Database");
                return;
            }

            // PolicyType별로 모델 경로 조회 후 MLRuntimeManager로 로드
            var policyTypes = System.Enum.GetValues(typeof(NeuralAnimationController.PolicyType));
            foreach (NeuralAnimationController.PolicyType policy in policyTypes)
            {
                if (db.HasPolicy(policy))
                {
                    string modelPath = db.GetModelPath(policy);
                    if (!string.IsNullOrEmpty(modelPath))
                    {
                        // NeuralAnimationController 내부에서 MLRuntimeManager.LoadModel 호출
                        // SwitchPolicy 시 자동으로 로드됨
                        Debug.Log($"[ModelAnimatorAssigner] Policy {policy} mapped to {modelPath}");
                    }
                }
            }

            // 기본 정책 설정
            _neuralAnim.SwitchPolicy(_defaultPolicy);
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
            if (_neuralAnim != null) DestroyImmediate(_neuralAnim);
            if (_hybridAnim != null) DestroyImmediate(_hybridAnim);
            if (_quadrupedLocomotion != null) DestroyImmediate(_quadrupedLocomotion);
            if (_specialCreatureAnim != null) DestroyImmediate(_specialCreatureAnim);
        }

        public NeuralAnimationController NeuralController => _neuralAnim;
        public ProceduralAnimationController ProceduralController => _proceduralAnim;
        public HybridAnimationController HybridController => _hybridAnim;
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