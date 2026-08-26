using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;

public class FixPhase4_Camera
{
    [MenuItem("Tools/Poison/Fix Phase 4 - Camera")]
    public static void FixCamera()
    {
        const string scenePath = "Assets/Scenes/MainScene.unity";
        var scene = SceneManager.GetSceneByName("MainScene");
        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(scenePath);
        }

        Debug.Log("=== PHASE 4: CAMERA BINDING START ===");

        // 1. Player 찾기
        var player = GameObject.Find("Player");
        if (player == null)
        {
            Debug.LogError("[Phase4] Player NOT FOUND");
            return;
        }

        var playerModel = player.transform.Find("PlayerModel")?.gameObject;
        if (playerModel == null)
        {
            Debug.LogError("[Phase4] PlayerModel NOT FOUND");
            return;
        }

        // 2. Main Camera 찾기
        var mainCam = GameObject.Find("Main Camera");
        if (mainCam == null)
        {
            Debug.LogError("[Phase4] Main Camera NOT FOUND");
            return;
        }

        var brain = mainCam.GetComponent<CinemachineBrain>();
        if (brain == null) brain = mainCam.AddComponent<CinemachineBrain>();
        
        // DefaultBlend 설정 (리플렉션) - Style enum 처리
        var brainType = brain.GetType();
        var blendProp = brainType.GetProperty("DefaultBlend", BindingFlags.Public | BindingFlags.Instance);
        if (blendProp != null)
        {
            var blendDefType = typeof(CinemachineBlendDefinition);
            var styleEnum = blendDefType.GetNestedType("Style", BindingFlags.Public);
            if (styleEnum != null)
            {
                var easeInOut = Enum.Parse(styleEnum, "EaseInOut");
                var blendCtor = blendDefType.GetConstructor(new[] { styleEnum, typeof(float) });
                if (blendCtor != null)
                {
                    var blend = blendCtor.Invoke(new object[] { easeInOut, 1.5f });
                    blendProp.SetValue(brain, blend);
                }
            }
        }

        // 3. Player Camera (Virtual Camera) 찾기
        var vcamGo = mainCam.transform.Find("Player Camera")?.gameObject;
        if (vcamGo == null)
        {
            vcamGo = new GameObject("Player Camera");
            vcamGo.transform.SetParent(mainCam.transform);
            Debug.Log("[Phase4] Created Player Camera");
        }

        // CinemachineCamera
        var vcam = vcamGo.GetComponent<CinemachineCamera>();
        if (vcam == null) vcam = vcamGo.AddComponent<CinemachineCamera>();
        vcam.Priority = 100;

        // CinemachineThirdPersonFollow - 리플렉션으로 모든 속성 설정
        var tpf = vcamGo.GetComponent<CinemachineThirdPersonFollow>();
        if (tpf == null) tpf = vcamGo.AddComponent<CinemachineThirdPersonFollow>();
        
        SetProp(tpf, "Follow", player.transform);
        SetProp(tpf, "LookAt", playerModel.transform);
        SetProp(tpf, "VerticalOffset", 1.5f);
        SetProp(tpf, "HorizontalOffset", 0f);
        SetProp(tpf, "CameraDistance", 25f);
        SetProp(tpf, "MinCameraDistance", 15f);
        SetProp(tpf, "MaxCameraDistance", 40f);
        SetProp(tpf, "ShoulderOffset", new Vector3(0.5f, 0f, 0f));

        // CinemachineInputAxisController
        var inputAxis = vcamGo.GetComponent<CinemachineInputAxisController>();
        if (inputAxis == null) inputAxis = vcamGo.AddComponent<CinemachineInputAxisController>();
        
        SetProp(inputAxis, "HorizontalAxis", "Mouse X");
        SetProp(inputAxis, "VerticalAxis", "Mouse Y");
        SetProp(inputAxis, "MaxSpeed", 300f);
        SetProp(inputAxis, "AccelTime", 0.1f);
        SetProp(inputAxis, "DecelTime", 0.1f);

        // CinemachineCollider (deprecated but still works)
        var collider = vcamGo.GetComponent<CinemachineCollider>();
        if (collider == null) collider = vcamGo.AddComponent<CinemachineCollider>();
        SetProp(collider, "MinimumDistanceFromTarget", 0.5f);
        SetProp(collider, "MaximumDistanceFromTarget", 40f);
        SetProp(collider, "Radius", 0.3f);
        SetProp(collider, "CollideAgainst", ~LayerMask.GetMask("Player", "Ignore Raycast"));
        
        var stratEnum = typeof(CinemachineCollider).GetNestedType("ResolutionStrategy", BindingFlags.Public);
        if (stratEnum != null)
        {
            var preserveDist = Enum.Parse(stratEnum, "PreserveCameraDistance");
            SetProp(collider, "Strategy", preserveDist);
        }

        Debug.Log($"[Phase4] Camera bound via reflection");

        // 4. 씬 저장
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, scenePath, true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[Phase4] Scene saved to: {scenePath}");
        Debug.Log("=== PHASE 4: CAMERA BINDING COMPLETE ===");
    }

    static void SetProp(object obj, string name, object value)
    {
        if (obj == null) return;
        var type = obj.GetType();
        var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(obj, value);
        }
        else if (field != null)
        {
            field.SetValue(obj, value);
        }
        else
        {
            Debug.LogWarning($"[Phase4] Property/Field '{name}' not found on {type.Name}");
        }
    }
}