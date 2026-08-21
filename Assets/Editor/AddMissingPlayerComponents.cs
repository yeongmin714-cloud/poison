using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Reflection;

/// <summary>
/// Player에 누락된 핵심 컴포넌트 추가 - Tools > Scene Fix > Add Missing Player Components
/// </summary>
public class AddMissingPlayerComponents
{
    private const string ScenePath = "Assets/Scenes/MainScene.unity";

    [MenuItem("Tools/Scene Fix/Add Missing Player Components")]
    public static void AddComponents()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        
        int count = 0;
        var player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            Debug.LogError("[AddMissingPlayerComponents] Player not found!");
            return;
        }

        Debug.Log($"[AddMissingPlayerComponents] Player found: {player.name}");

        // 컴포넌트 타입들
        var componentsToAdd = new (string typeName, string displayName)[]
        {
            ("ProjectName.Systems.PlayerMovement, Assembly-CSharp", "PlayerMovement"),
            ("ProjectName.Systems.PlayerCombat, Assembly-CSharp", "PlayerCombat"),
            ("ProjectName.Systems.Animation.Procedural.RigAnimationController, Assembly-CSharp", "RigAnimationController"),
            ("ProjectName.Systems.Animation.Neural.NeuralAnimationController, Assembly-CSharp", "NeuralAnimationController"),
            ("ProjectName.Systems.Animation.Neural.HybridAnimationController, Assembly-CSharp", "HybridAnimationController"),
            ("UnityEngine.InputSystem.PlayerInput, Unity.InputSystem", "PlayerInput"),
        };

        foreach (var (typeName, displayName) in componentsToAdd)
        {
            var type = System.Type.GetType(typeName);
            if (type != null)
            {
                if (player.GetComponent(type) == null)
                {
                    player.AddComponent(type);
                    count++;
                    Debug.Log($"[AddMissingPlayerComponents] {displayName} 추가됨");
                }
                else
                {
                    Debug.Log($"[AddMissingPlayerComponents] {displayName} 이미 존재함");
                }
            }
            else
            {
                Debug.LogWarning($"[AddMissingPlayerComponents] {displayName} 타입을 찾을 수 없음: {typeName}");
            }
        }

        // CharacterController
        if (player.GetComponent<CharacterController>() == null)
        {
            var cc = player.AddComponent<CharacterController>();
            cc.center = new Vector3(0, 0.9f, 0);
            cc.radius = 0.3f;
            cc.height = 1.8f;
            cc.stepOffset = 0.3f;
            cc.slopeLimit = 45f;
            cc.skinWidth = 0.08f;
            cc.minMoveDistance = 0.001f;
            count++;
            Debug.Log("[AddMissingPlayerComponents] CharacterController 추가됨");
        }

        // Animator
        if (player.GetComponent<Animator>() == null)
        {
            player.AddComponent<Animator>();
            count++;
            Debug.Log("[AddMissingPlayerComponents] Animator 추가됨");
        }

        // PlayerInput 설정
        var inputType = System.Type.GetType("UnityEngine.InputSystem.PlayerInput, Unity.InputSystem");
        if (inputType != null)
        {
            var pi = player.GetComponent(inputType);
            if (pi != null)
            {
                var defaultActionMapProp = inputType.GetProperty("defaultActionMap");
                if (defaultActionMapProp != null) defaultActionMapProp.SetValue(pi, "Player");
                var notificationBehaviorProp = inputType.GetProperty("notificationBehavior");
                if (notificationBehaviorProp != null)
                {
                    var enumType = System.Type.GetType("UnityEngine.InputSystem.PlayerNotifications, Unity.InputSystem");
                    if (enumType != null)
                    {
                        var invokeUnityEvents = System.Enum.Parse(enumType, "InvokeUnityEvents");
                        notificationBehaviorProp.SetValue(pi, invokeUnityEvents);
                    }
                }
                Debug.Log("[AddMissingPlayerComponents] PlayerInput 설정 완료");
            }
        }

        // NeuralAnimationController, HybridAnimationController에 VelocityProvider 연결
        var pmType = System.Type.GetType("ProjectName.Systems.PlayerMovement, Assembly-CSharp");
        if (pmType != null)
        {
            var pm = player.GetComponent(pmType);
            if (pm != null)
            {
                var neuralType = System.Type.GetType("ProjectName.Systems.Animation.Neural.NeuralAnimationController, Assembly-CSharp");
                if (neuralType != null)
                {
                    var na = player.GetComponent(neuralType);
                    if (na != null)
                    {
                        var method = neuralType.GetMethod("SetVelocityProvider");
                        if (method != null) method.Invoke(na, new object[] { pm });
                    }
                }

                var hybridType = System.Type.GetType("ProjectName.Systems.Animation.Neural.HybridAnimationController, Assembly-CSharp");
                if (hybridType != null)
                {
                    var ha = player.GetComponent(hybridType);
                    if (ha != null)
                    {
                        var method = hybridType.GetMethod("SetVelocityProvider");
                        if (method != null) method.Invoke(ha, new object[] { pm });
                    }
                }
            }
        }

        // ProgressiveRolloutManager 등록
        var prmType = System.Type.GetType("ProjectName.Systems.Animation.Neural.ProgressiveRolloutManager, Assembly-CSharp");
        if (prmType != null)
        {
            var instanceProp = prmType.GetProperty("Instance");
            if (instanceProp != null)
            {
                var instance = instanceProp.GetValue(null);
                if (instance != null)
                {
                    var hybridType = System.Type.GetType("ProjectName.Systems.Animation.Neural.HybridAnimationController, Assembly-CSharp");
                    if (hybridType != null)
                    {
                        var method = prmType.GetMethod("ConfigureHybridController");
                        if (method != null)
                        {
                            var hybrid = player.GetComponent(hybridType);
                            if (hybrid != null) method.Invoke(instance, new object[] { hybrid });
                        }
                    }
                }
            }
        }

        // 씬 저장
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log($"[AddMissingPlayerComponents] 완료! {count}개 컴포넌트 추가됨");
        EditorUtility.DisplayDialog("Complete", $"Player 컴포넌트 추가 완료!\n\n추가된 컴포넌트: {count}개", "OK");
    }
}