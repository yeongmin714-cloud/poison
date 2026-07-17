using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

/// <summary>
/// Player_Animator.controller에 Speed 파라미터 추가 및 Blend Tree 설정
/// Tools/ProjectName/Setup Player Animator Controller 메뉴에서 실행
/// </summary>
public static class SetupPlayerAnimatorController
{
    [MenuItem("Tools/ProjectName/Setup Player Animator Controller")]
    public static void Setup()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Resources/Animations/Player_Animator.controller");
        if (controller == null)
        {
            Debug.LogError("[SetupPlayerAnimatorController] Player_Animator.controller를 찾을 수 없습니다.");
            return;
        }

        // Speed 파라미터 추가
        bool hasSpeedParam = false;
        foreach (var param in controller.parameters)
        {
            if (param.name == "Speed")
            {
                hasSpeedParam = true;
                break;
            }
        }

        if (!hasSpeedParam)
        {
            var parameters = new AnimatorControllerParameter[controller.parameters.Length + 1];
            controller.parameters.CopyTo(parameters, 0);
            parameters[parameters.Length - 1] = new AnimatorControllerParameter
            {
                name = "Speed",
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 0f
            };
            controller.parameters = parameters;
            Debug.Log("[SetupPlayerAnimatorController] Speed 파라미터 추가됨");
        }
        else
        {
            Debug.Log("[SetupPlayerAnimatorController] Speed 파라미터 이미 존재");
        }

        // Base Layer 가져오기
        var baseLayer = controller.layers[0];
        var stateMachine = baseLayer.stateMachine;

        // 기존 Idle/Walk/Run/Attack/Jump/Gather 상태들
        AnimatorState idleState = null, walkState = null, runState = null;
        foreach (var childState in stateMachine.states)
        {
            switch (childState.state.name)
            {
                case "Idle": idleState = childState.state; break;
                case "Walk": walkState = childState.state; break;
                case "Run": runState = childState.state; break;
            }
        }

        // Blend Tree 생성 (Idle/Walk/Run)
        var blendTree = new BlendTree();
        blendTree.blendParameter = "Speed";
        blendTree.blendType = BlendTreeType.Simple1D;
        blendTree.useAutomaticThresholds = false;

        // 애니메이션 클립들 (기존 Motion 참조 유지)
        if (idleState != null && idleState.motion is AnimationClip idleClip)
        {
            blendTree.AddChild(idleClip, 0f);
        }
        if (walkState != null && walkState.motion is AnimationClip walkClip)
        {
            blendTree.AddChild(walkClip, 0.5f);
        }
        if (runState != null && runState.motion is AnimationClip runClip)
        {
            blendTree.AddChild(runClip, 1f);
        }

        // 기존 상태들 제거하고 Blend Tree 상태 추가
        // Idle 상태를 Blend Tree로 교체
        if (idleState != null)
        {
            stateMachine.RemoveState(idleState);
        }
        if (walkState != null)
        {
            stateMachine.RemoveState(walkState);
        }
        if (runState != null)
        {
            stateMachine.RemoveState(runState);
        }

        // Blend Tree 상태 생성
        var locomotionState = stateMachine.AddState("Locomotion", new Vector3(200, 0, 0));
        locomotionState.motion = blendTree;
        locomotionState.speed = 1f;
        stateMachine.defaultState = locomotionState;

        // Attack, Jump, Gather는 그대로 유지 (Any State Transition으로 연결됨)

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[SetupPlayerAnimatorController] 완료 - Speed 파라미터 추가, Locomotion Blend Tree 생성");
    }
}