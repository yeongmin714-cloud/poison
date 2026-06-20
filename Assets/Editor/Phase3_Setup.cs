using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Cinemachine;
using ProjectName.Core;
using ProjectName.Systems;

public static class Phase3_Setup
{
    [MenuItem("Tools/Phase 3 - Setup Player Scene")]
    public static void SetupPlayerScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ===== 1. 기본 지형 (Ground) =====
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(50, 1, 50);
        ground.transform.position = Vector3.zero;

        var groundMat = MaterialHelper.CreateLitMaterial(
            new Color(0.3f, 0.6f, 0.2f), "Ground_Grass"
        );
        ground.GetComponent<MeshRenderer>().material = groundMat;

        // ===== 2. 환경 오브젝트 (나무 기둥 10개) =====
        for (int i = 0; i < 10; i++)
        {
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = $"Pillar_{i}";
            pillar.transform.position = new Vector3(
                Random.Range(-40f, 40f),
                1f,
                Random.Range(-40f, 40f)
            );
            pillar.transform.localScale = new Vector3(0.5f, 2f, 0.5f);
            var mat = MaterialHelper.CreateLitMaterial(
                new Color(0.4f, 0.3f, 0.2f), $"Pillar_{i}_Mat"
            );
            pillar.GetComponent<MeshRenderer>().material = mat;
        }

        // ===== 3. Directional Light =====
        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.shadowStrength = 0.8f;
        lightGO.transform.rotation = Quaternion.Euler(50, -30, 0);

        // ===== 4. 플레이어 캐릭터 =====
        var player = new GameObject("Player");
        player.transform.position = new Vector3(0, 2, 0);
        player.tag = "Player";

        var cc = player.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.4f;
        cc.center = new Vector3(0, 1, 0);

        player.AddComponent<PlayerMovement>();
        // PlayerPlaceholder: 파란색 사람 모양 추가
        player.AddComponent<PlayerPlaceholder>();

        // ===== 5. Cinemachine 3인칭 카메라 =====
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 500f;
        camGO.AddComponent<CinemachineBrain>();

        var vcamGO = new GameObject("Player Camera");
        vcamGO.transform.SetParent(camGO.transform);
        var vcam = vcamGO.AddComponent<CinemachineCamera>();
        vcam.Follow = player.transform;
        vcam.LookAt = player.transform;
        vcam.Priority = 100;

        var follow = vcamGO.AddComponent<CinemachineThirdPersonFollow>();
        follow.ShoulderOffset = new Vector3(0.5f, 0f, 0f);
        follow.VerticalArmLength = 2f;
        follow.CameraSide = 1;
        follow.Damping = new Vector3(0.2f, 0.5f, 0.3f);

        var inputCtrl = vcamGO.AddComponent<CinemachineInputAxisController>();

        // ===== 6. GameManager =====
        var gmGO = new GameObject("GameManager");
        gmGO.AddComponent<GameManager>();

        // ===== 씬 저장 =====
        string path = "Assets/Scenes/MainScene.unity";
        EditorSceneManager.SaveScene(scene, path);
        Debug.Log($"[Phase3] Player scene setup complete → {path}");
    }

    [MenuItem("Tools/Phase 3 - Setup Player Scene", true)]
    private static bool Validate() => true;
}