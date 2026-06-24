#nullable disable
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

        /// <summary>
        /// Animator Controller들을 로드합니다. (최초 1회)
        /// </summary>
        private static void EnsureControllers()
        {
            if (_playerController != null) return;

            _playerController = Resources.Load<RuntimeAnimatorController>("Animations/Player_Animator");
            _soldierController = Resources.Load<RuntimeAnimatorController>("Animations/Soldier_Animator");
            _monsterController = Resources.Load<RuntimeAnimatorController>("Animations/Monster_Animator");

            if (_playerController == null)
                Debug.LogWarning("[ModelAnimatorAssigner] Player_Animator.controller를 찾을 수 없습니다. (Resources/Animations/ 경로 확인)");
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

            Animator animator = model.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                animator = model.GetComponent<Animator>();
                if (animator == null)
                {
                    // Animator가 없으면 새로 추가 (SkinnedMeshRenderer 있는 경우)
                    if (model.GetComponentInChildren<SkinnedMeshRenderer>() != null)
                        animator = model.AddComponent<Animator>();
                    else
                        return; // Skinned mesh가 없으면 스킵
                }
            }

            string lowerName = modelName.ToLowerInvariant();
            RuntimeAnimatorController controller = DetermineController(lowerName);

            if (controller != null)
            {
                animator.runtimeAnimatorController = controller;
                animator.updateMode = AnimatorUpdateMode.Normal;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                // 시작 시 Idle 상태
                animator.SetInteger("State", 0); // 0 = Idle
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
                || modelName.Contains("shop") || modelName.Contains("man") || modelName.Contains("girl")
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
        /// Animator의 상태를 변경합니다.
        /// State: 0=Idle, 1=Walk, 2=Run
        /// Triggers: AttackTrigger, JumpTrigger, GatherTrigger
        /// </summary>
        public static void SetState(GameObject model, int state)
        {
            Animator animator = model.GetComponentInChildren<Animator>();
            if (animator != null)
                animator.SetInteger("State", state);
        }

        public static void TriggerAttack(GameObject model)
        {
            Animator animator = model.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.SetInteger("State", 0); // Return to idle after attack
                animator.SetTrigger("AttackTrigger");
            }
        }

        public static void TriggerJump(GameObject model)
        {
            Animator animator = model.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.SetInteger("State", 0);
                animator.SetTrigger("JumpTrigger");
            }
        }

        public static void TriggerGather(GameObject model)
        {
            Animator animator = model.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.SetInteger("State", 0);
                animator.SetTrigger("GatherTrigger");
            }
        }
    }
}