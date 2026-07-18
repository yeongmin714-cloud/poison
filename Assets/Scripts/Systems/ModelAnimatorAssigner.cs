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

            // Generic 아바타가 없으면 런타임 생성 (GLB는 에디터 아바타를 안 만듦)
            if (animator.avatar == null)
            {
                Transform rootBone = FindRootBone(model);
                if (rootBone != null)
                {
                    try
                    {
                        Avatar genericAvatar = AvatarBuilder.BuildGenericAvatar(model, rootBone.name);
                        genericAvatar.name = model.name + "_GenericAvatar";
                        animator.avatar = genericAvatar;
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[ModelAnimatorAssigner] Generic Avatar 생성 실패 ({model.name}): {e.Message}");
                    }
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
                animator.SetInteger(ParamState, StateIdle);
            }

            // 프로시저럴 포즈 보정 — 모델 타입에 따라 2족/4족 컨트롤러 분기 부착 (중복 방지)
            bool isQuadruped = false;
            if (RuntimeModelLoader.TryGetModelMetadata(modelName, out var meta))
                isQuadruped = meta.ModelType == ModelType.RiggedQuadruped;

            if (isQuadruped)
            {
                if (model.GetComponent<QuadrupedPoseController>() == null)
                    model.AddComponent<QuadrupedPoseController>();
            }
            else
            {
                if (model.GetComponent<ProceduralAnimationController>() == null)
                    model.AddComponent<ProceduralAnimationController>();
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
        /// GLB 모델 계층에서 루트 본(루트 뼈대) Transform을 찾습니다.
        /// 우선순위:
        /// 1. SkinnedMeshRenderer.rootBone
        /// 2. 이름이 "Root"/"root"/"Armature"/"Hips"/"hips"인 Transform
        /// 3. SkinnedMeshRenderer.bones[0]의 부모 체인 중 최상위 본
        /// 4. model.transform
        /// </summary>
        private static Transform FindRootBone(GameObject model)
        {
            if (model == null) return null;

            // 1. SkinnedMeshRenderer.rootBone 우선 사용
            SkinnedMeshRenderer smr = model.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null && smr.rootBone != null)
                return smr.rootBone;

            // 2. 이름 기준 탐색
            Transform[] allTransforms = model.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in allTransforms)
            {
                string name = t.name;
                if (name == "Root" || name == "root" || name == "Armature" || name == "Hips" || name == "hips")
                    return t;
            }

            // 3. SkinnedMeshRenderer.bones[0]의 부모 체인 중 최상위 본
            if (smr != null && smr.bones != null && smr.bones.Length > 0)
            {
                Transform bone = smr.bones[0];
                while (bone.parent != null && bone.parent != model.transform)
                    bone = bone.parent;
                if (bone != null && bone != model.transform)
                    return bone;
            }

            // 4. 기본값: model.transform
            return model.transform;
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