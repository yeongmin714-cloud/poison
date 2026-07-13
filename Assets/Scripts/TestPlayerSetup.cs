using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 테스트 씬 전용: Player 이동에 필요한 최소 구성 요소만 설정.
/// GameManager/UIManager 등 모든 시스템을 사용하지 않음.
/// </summary>
public class TestPlayerSetup : MonoBehaviour
{
    [Header("Player Settings")]
    [SerializeField] private float _walkSpeed = 5f;
    [SerializeField] private float _runSpeed = 10f;
    [SerializeField] private float _jumpHeight = 2f;

    [Header("Camera Settings")]
    [SerializeField] private float _orbitRadius = 30f;
    [SerializeField] private float _defaultPitch = 50f;

    private void Awake()
    {
        SetupPlayer();
        SetupCamera();
        SetupGround();
        SetupLight();
        EnsureEventSystem();
    }

    private void SetupPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            player = new GameObject("Player");
            player.tag = "Player";
        }

        // CharacterController
        if (player.GetComponent<CharacterController>() == null)
        {
            var cc = player.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.radius = 0.5f;
        }

        // PlayerMovement
        if (player.GetComponent<PlayerMovement>() == null)
        {
            player.AddComponent<PlayerMovement>();
        }

        // PlayerInput (Input System 활성화용)
        if (player.GetComponent<PlayerInput>() == null)
        {
            var pi = player.AddComponent<PlayerInput>();
            pi.defaultActionMap = "Player";
            pi.notificationBehavior = PlayerNotifications.InvokeUnityEvents;
        }

        // Player visual (capsule)
        if (player.GetComponent<MeshRenderer>() == null)
        {
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "PlayerVisual";
            capsule.transform.SetParent(player.transform);
            capsule.transform.localPosition = Vector3.zero;
            capsule.transform.localScale = Vector3.one;
            // Remove the collider from the visual (CharacterController handles it)
            DestroyImmediate(capsule.GetComponent<CapsuleCollider>());
        }

        player.transform.position = Vector3.zero;
        Debug.Log("[TestPlayerSetup] ✅ Player 설정 완료");
    }

    private void SetupCamera()
    {
        // 메인 카메라 찾기 또는 생성
        GameObject camGO = GameObject.FindGameObjectWithTag("MainCamera");
        if (camGO == null)
        {
            camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
        }

        Camera cam = camGO.GetComponent<Camera>();
        if (cam == null)
            cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 500f;

        // TopDownCameraController
        if (camGO.GetComponent<TopDownCameraController>() == null)
        {
            camGO.AddComponent<TopDownCameraController>();
        }

        // AudioListener
        if (camGO.GetComponent<AudioListener>() == null)
            camGO.AddComponent<AudioListener>();

        Debug.Log("[TestPlayerSetup] ✅ 카메라 설정 완료");
    }

    private void SetupGround()
    {
        if (GameObject.Find("Ground") == null)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0, -0.5f, 0);
            ground.transform.localScale = Vector3.one * 50f;
            Debug.Log("[TestPlayerSetup] ✅ Ground 생성");
        }
    }

    private void SetupLight()
    {
        if (FindAnyObjectByType<Light>() == null)
        {
            var lightGO = new GameObject("Directional Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            Debug.Log("[TestPlayerSetup] ✅ Directional Light 생성");
        }
    }

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            Debug.Log("[TestPlayerSetup] ✅ EventSystem 생성");
        }
    }
}