using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;

public class FixCinemachineBinding
{
    [MenuItem("Tools/Poison/Fix Cinemachine Binding")]
    public static void FixBinding()
    {
        const string scenePath = "Assets/Scenes/MainScene.unity";
        var scene = SceneManager.GetSceneByName("MainScene");
        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(scenePath);
        }

        Debug.Log("=== CINEMACHINE BINDING FIX START ===");

        var player = GameObject.Find("Player");
        var playerModel = player?.transform.Find("PlayerModel")?.gameObject;
        var mainCam = GameObject.Find("Main Camera");
        var vcamGo = mainCam?.transform.Find("Player Camera")?.gameObject;

        if (player == null || playerModel == null || mainCam == null || vcamGo == null)
        {
            Debug.LogError("[CinemachineFix] Missing required objects");
            return;
        }

        // CinemachineCameraBinder 컴포넌트 추가/갱신
        var binder = vcamGo.GetComponent<CinemachineCameraBinder>();
        if (binder == null) binder = vcamGo.AddComponent<CinemachineCameraBinder>();

        // 타겟 설정
        binder.followTarget = player.transform;
        binder.lookAtTarget = playerModel.transform;
        binder.cameraDistance = 25f;
        binder.minDistance = 15f;
        binder.maxDistance = 40f;
        binder.verticalOffset = 1.5f;
        binder.horizontalOffset = 0f;
        binder.shoulderOffset = new Vector3(0.5f, 0f, 0f);
        binder.horizontalAxis = "Mouse X";
        binder.verticalAxis = "Mouse Y";
        binder.maxSpeed = 300f;
        binder.accelTime = 0.1f;
        binder.decelTime = 0.1f;
        binder.minDistanceFromTarget = 0.5f;
        binder.maxDistanceFromTarget = 40f;
        binder.colliderRadius = 0.3f;
        binder.collideAgainstLayers = ~LayerMask.GetMask("Player", "Ignore Raycast");

        // CinemachineBrain 기본 블렌드 설정
        var brain = mainCam.GetComponent<CinemachineBrain>();
        if (brain == null) brain = mainCam.AddComponent<CinemachineBrain>();
        
        var blendDefType = typeof(CinemachineBlendDefinition);
        var styleEnum = blendDefType.GetNestedType("Style", System.Reflection.BindingFlags.Public);
        if (styleEnum != null)
        {
            var easeInOut = System.Enum.Parse(styleEnum, "EaseInOut");
            var blendCtor = blendDefType.GetConstructor(new[] { styleEnum, typeof(float) });
            if (blendCtor != null)
            {
                var blend = blendCtor.Invoke(new object[] { easeInOut, 1.5f });
                var blendProp = brain.GetType().GetProperty("DefaultBlend", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (blendProp != null) blendProp.SetValue(brain, blend);
            }
        }

        // 우선순위 설정
        var vcam = vcamGo.GetComponent<CinemachineCamera>();
        if (vcam == null) vcam = vcamGo.AddComponent<CinemachineCamera>();
        vcam.Priority = 100;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, scenePath, true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[CinemachineFix] CinemachineCameraBinder added to Player Camera");
        Debug.Log($"[CinemachineFix] Follow: {binder.followTarget.name}, LookAt: {binder.lookAtTarget.name}");
        Debug.Log("=== CINEMACHINE BINDING FIX COMPLETE ===");
    }
}