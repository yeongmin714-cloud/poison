using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase FIX: 로드된 3D 모델에 Animator Controller를 할당합니다.
    /// RuntimeModelLoader가 모델을 로드한 후, 모델 타입에 따라 적절한 컨트롤러를 연결합니다.
    /// 
    /// 사용법:
    ///   ModelAnimatorAssigner.AssignController(modelGameObject, "player");
    ///   ModelAnimatorAssigner.AssignController(modelGameObject, "soldier");
    ///   ModelAnimatorAssigner.AssignController(modelGameObject, "wolf");
    /// </summary>
    public static class ModelAnimatorAssigner
    {
        private static RuntimeAnimatorController _playerController;
        private static RuntimeAnimatorController _soldierController;
        private static RuntimeAnimatorController _monsterController;

        // Animator 파라미터 상수
        private const string ParamState = "State";
        private const string ParamAttackTrigger = "AttackTrigger";
        private const string ParamJumpTrigger = "JumpTrigger";
        private const string ParamGatherTrigger = "GatherTrigger";

        private const int StateIdle = 0;

        /// <summary>
        /// Animator Controller들을 로드합니다. (최초 1회, 스레드 안전하지 않음 — 메인 스레드 전용)
        /// </summary>
        private static void EnsureControllers()
        {
            if (_playerController != null) return;

            _playerController = Resources.Load<RuntimeAnimatorController>("Animations/Player_Animator");
            _soldierController = Resources.Load<RuntimeAnimatorController>("Animations/Soldier_Animator");
            _monsterController = Resources.Load<RuntimeAnimatorController>("Animations/Monster_Animator");

            if (_playerController == null)
                Debug.LogWarning("[ModelAnimatorAssigner] Player_Animator.controller를 찾을 수 없습니다. (Resources/Animations/ 경로 확인)");
            if (_soldierController == null)
                Debug.LogWarning("[ModelAnimatorAssigner] Soldier_Animator.controller를 찾을 수 없습니다. (Resources/Animations/ 경로 확인)");
            if (_monsterController == null)
                Debug.LogWarning("[ModelAnimatorAssigner] Monster_Animator.controller를 찾을 수 없습니다. (Resources/Animations/ 경로 확인)");
        }

        /// <summary>
        /// 모델의 이름에 따라 적절한 Animator Controller를 할당합니다.
        /// </summary>
        /// <param name="model">애니메이터를 할당할 모델 GameObject</param>
        /// <param name="modelName">모델 이름 (소문자, 예: "player", "soldier_lv1-20", "wolf", "golem")</param>
        public static void AssignController(GameObject model, string modelName)
        {
            if (model == null) return;
            EnsureControllers();

            Animator animator = model.GetComponentInChildren<Animator>(includeInactive: false);
            if (animator == null)
            {
                // Animator가 없으면 SkinnedMeshRenderer 존재 시 새로 추가
                if (model.GetComponentInChildren<SkinnedMeshRenderer>() != null)
                    animator = model.AddComponent<Animator>();
                else
                    return; // Skinned mesh가 없으면 스킵
            }

            string lowerName = modelName.ToLowerInvariant();
            RuntimeAnimatorController controller = DetermineController(lowerName);

            if (controller != null)
            {
                animator.runtimeAnimatorController = controller;
                animator.updateMode = AnimatorUpdateMode.Normal;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                // 시작 시 Idle 상태
                animator.SetInteger(ParamState, StateIdle);
            }
        }

        /// <summary>
        /// 모델 이름으로 적절한 컨트롤러를 결정합니다.
        /// </summary>
        private static RuntimeAnimatorController DetermineController(string modelName)
        {
            // Player
            if (modelName.Contains("player"))
                return _playerController;

            // Soldiers
            if (modelName.Contains("soldier") || modelName.Contains("병사"))
                return _soldierController;

            // Mercenary
            if (modelName.Contains("mercenary") || modelName.Contains("용병"))
                return _soldierController;

            // NPCs — soldier controller (basic walk/idle)
            if (modelName.Contains("npc") || modelName.Contains("lord") || modelName.Contains("king")
                || modelName.Contains("shop") || modelName.EndsWith("man") || modelName.Contains("girl")
                || modelName.Contains("oldman") || modelName.Contains("dracula") || modelName.Contains("bard"))
                return _soldierController;

            // Monsters
            if (modelName.Contains("wolf") || modelName.Contains("boar") || modelName.Contains("deer")
                || modelName.Contains("crow") || modelName.Contains("bat") || modelName.Contains("rabbit")
                || modelName.Contains("snake") || modelName.Contains("slime") || modelName.Contains("golem")
                || modelName.Contains("minotaur") || modelName.Contains("griffon") || modelName.Contains("manticore")
                || modelName.Contains("salamander") || modelName.Contains("alligator") || modelName.Contains("ogre")
                || modelName.Contains("troll") || modelName.Contains("lizard") || modelName.Contains("hedgehog")
                || modelName.Contains("assassin") || modelName.Contains("banshee") || modelName.Contains("mouse")
                || modelName.Contains("spider") || modelName.Contains("clam") || modelName.Contains("spirit")
                || modelName.Contains("monster"))
                return _monsterController;

            // Default: player controller
            return _playerController;
        }

        /// <summary>
        /// Animator가 있는 GameObject에서 Animator 컴포넌트를 찾습니다.
        /// </summary>
        private static Animator FindAnimator(GameObject model)
        {
            return model != null ? model.GetComponentInChildren<Animator>(includeInactive: false) : null;
        }

        /// <summary>
        /// Animator의 상태를 변경합니다.
        /// State: 0=Idle, 1=Walk, 2=Run
        /// Triggers: AttackTrigger, JumpTrigger, GatherTrigger
        /// </summary>
        public static void SetState(GameObject model, int state)
        {
            Animator animator = FindAnimator(model);
            if (animator != null)
                animator.SetInteger(ParamState, state);
        }

        /// <summary>
        /// Attack 트리거를 발동합니다. 애니메이션 종료 후 Idle로 복귀합니다.
        /// </summary>
        public static void TriggerAttack(GameObject model)
        {
            Animator animator = FindAnimator(model);
            if (animator != null)
            {
                animator.SetInteger(ParamState, StateIdle);
                animator.SetTrigger(ParamAttackTrigger);
            }
        }

        /// <summary>
        /// Jump 트리거를 발동합니다. 애니메이션 종료 후 Idle로 복귀합니다.
        /// </summary>
        public static void TriggerJump(GameObject model)
        {
            Animator animator = FindAnimator(model);
            if (animator != null)
            {
                animator.SetInteger(ParamState, StateIdle);
                animator.SetTrigger(ParamJumpTrigger);
            }
        }

        /// <summary>
        /// Gather(채집) 트리거를 발동합니다. 애니메이션 종료 후 Idle로 복귀합니다.
        /// </summary>
        public static void TriggerGather(GameObject model)
        {
            Animator animator = FindAnimator(model);
            if (animator != null)
            {
                animator.SetInteger(ParamState, StateIdle);
                animator.SetTrigger(ParamGatherTrigger);
            }
        }
    }
}