using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Cinemachine;
using ProjectName.Systems;
using ProjectName.Systems.Motions;

/// <summary>
/// Batchmode-safe fix for MainScene integration issues.
/// Run: Unity -batchmode -projectPath ... -executeMethod SceneIntegrationFix.BatchmodeFix -quit
/// </summary>
public static class SceneIntegrationFix
{
    [MenuItem("Tools/Scene Integration/Fix MainScene")]
    public static void FixMainScene()
    {
        BatchmodeFix();
    }

    public static void BatchmodeFix()
    {
        Debug.Log("[Fix] === Starting MainScene Integration Fix ===");

        // Open MainScene
        var scenePath = "Assets/Scenes/MainScene.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        Debug.Log($"[Fix] Opened scene: {scene.name}");

        // =========================================================
        // 1. Fix Player
        // =========================================================
        var player = GameObject.Find("Player");
        if (player == null)
        {
            Debug.LogError("[Fix] Player not found! Cannot continue.");
            return;
        }
        Debug.Log("[Fix] Player found ✓");

        Undo.RegisterFullObjectHierarchyUndo(player, "Scene Integration Fix");

        // 1a. Remove SnakeSlitherMotion (wrong component on human player)
        var slither = player.GetComponent<ProjectName.Systems.Motions.SnakeSlitherMotion>();
        if (slither != null)
        {
            Object.DestroyImmediate(slither);
            Debug.Log("[Fix] Removed SnakeSlitherMotion ✓");
        }

        // 1b. Fix MotionDetector — disable auto-setup to prevent re-adding wrong components
        var motionDetector = player.GetComponent<MotionDetector>();
        if (motionDetector != null)
        {
            // Already configured in SceneIntegrationFix
            // Do not repeat _autoSetupOnStart disable here — already handled in GameSetup
            Debug.Log("[Fix] Skipped MotionDetector autoSetup modification (already handled by GameSetup)");
        }

        // 1c. Add PlayerPlaceholder if missing
        var placeholder = player.GetComponent<PlayerPlaceholder>();
        if (placeholder == null)
        {
            player.AddComponent<PlayerPlaceholder>();
            Debug.Log("[Fix] Added PlayerPlaceholder ✓");
        }
        else
        {
            Debug.Log("[Fix] PlayerPlaceholder already exists ✓");
        }

        // 1d. Fix AnimationRiggingSetup
        var rigging = player.GetComponent<AnimationRiggingSetup>();
        if (rigging != null)
        {
            SerializedObject so = new SerializedObject(rigging);
            so.FindProperty("_autoFindBonesOnStart").boolValue = true;
            so.FindProperty("_autoSetupRiggingOnStart").boolValue = true;
            // Clear wrong serialized bone references so auto-find works correctly
            so.FindProperty("_headBone").objectReferenceValue = null;
            so.FindProperty("_spineBone").objectReferenceValue = null;
            so.FindProperty("_leftArmBone").objectReferenceValue = null;
            so.FindProperty("_rightArmBone").objectReferenceValue = null;
            so.FindProperty("_leftLegBone").objectReferenceValue = null;
            so.FindProperty("_rightLegBone").objectReferenceValue = null;
            so.FindProperty("_neckBone").objectReferenceValue = null;
            so.FindProperty("_chestBone").objectReferenceValue = null;
            so.FindProperty("_hipsBone").objectReferenceValue = null;
            so.ApplyModifiedProperties();
            Debug.Log("[Fix] Fixed AnimationRiggingSetup (clear bone refs, auto-find on) ✓");
        }
        else
        {
            var newRigging = player.AddComponent<AnimationRiggingSetup>();
            SerializedObject so = new SerializedObject(newRigging);
            so.FindProperty("_autoFindBonesOnStart").boolValue = true;
            so.FindProperty("_autoSetupRiggingOnStart").boolValue = true;
            so.ApplyModifiedProperties();
            Debug.Log("[Fix] Added AnimationRiggingSetup ✓");
        }

        // =========================================================
        // 2. Fix Camera System — Cinemachine
        // =========================================================
        SetupCinemachineCamera(player);

        // =========================================================
        // 3. Save
        // =========================================================
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.Refresh();
        Debug.Log("[Fix] ✅ MainScene saved!");
        Debug.Log("[Fix] === Scene Integration Fix Complete! ===");
    }

    private static void SetupCinemachineCamera(GameObject player)
    {
        var mainCamera = Camera.main;
        if (mainCamera == null)
        {
            // Create Main Camera if missing
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            mainCamera = camGO.AddComponent<Camera>();
            mainCamera.clearFlags = CameraClearFlags.Skybox;
            mainCamera.nearClipPlane = 0.1f;
            mainCamera.farClipPlane = 1000f;
            Debug.Log("[Fix] Created Main Camera ✓");
        }

        // Add CinemachineBrain
        if (mainCamera.GetComponent<CinemachineBrain>() == null)
        {
            mainCamera.gameObject.AddComponent<CinemachineBrain>();
            Debug.Log("[Fix] Added CinemachineBrain ✓");
        }

        // Find or create Player Camera (CinemachineCamera)
        var playerCam = GameObject.Find("Player Camera");
        if (playerCam == null)
        {
            playerCam = new GameObject("Player Camera");
            Debug.Log("[Fix] Created Player Camera GameObject ✓");
        }

        var cmCam = playerCam.GetComponent<CinemachineCamera>();
        if (cmCam == null)
        {
            cmCam = playerCam.AddComponent<CinemachineCamera>();
        }
        cmCam.Follow = player.transform;
        cmCam.LookAt = player.transform;
        cmCam.Priority = 100;

        // CinemachineThirdPersonFollow
        var follow = playerCam.GetComponent<CinemachineThirdPersonFollow>();
        if (follow == null)
        {
            follow = playerCam.AddComponent<CinemachineThirdPersonFollow>();
        }
        follow.ShoulderOffset = new Vector3(0.5f, 0f, 0f);
        follow.VerticalArmLength = 2f;
        follow.CameraSide = 1;
        follow.Damping = new Vector3(0.2f, 0.5f, 0.3f);
        follow.CameraDistance = 5f;

        // CinemachineInputAxisController
        if (playerCam.GetComponent<CinemachineInputAxisController>() == null)
        {
            playerCam.AddComponent<CinemachineInputAxisController>();
        }

        Debug.Log("[Fix] Cinemachine Camera System configured ✓");
    }
}