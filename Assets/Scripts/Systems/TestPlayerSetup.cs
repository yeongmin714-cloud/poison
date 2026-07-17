using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectName.Systems
{
/// <summary>
/// 테스트 씬 전용: Player 이동에 필요한 최소 구성 요소만 설정.
/// GameManager/UIManager 등 모든 시스템을 사용하지 않음.
/// 풀네임을 사용하여 네임스페이스/어셈블리 참조 문제 회피.
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

        // PlayerMovement (풀네임)
        var pmType = typeof(ProjectName.Systems.PlayerMovement);
        if (player.GetComponent(pmType) == null)
        {
            player.AddComponent(pmType);
        }

        // PlayerInput (Input System 활성화용)
        if (player.GetComponent<PlayerInput>() == null)
        {
            var pi = player.AddComponent<PlayerInput>();
            pi.defaultActionMap = "Player";
            pi.notificationBehavior = PlayerNotifications.InvokeUnityEvents;
        }

        // PlayerPlaceholder: RuntimeModelLoader → Player_Rigged GLB 로드 + RigAnimationController 연결 (MainScene과 동일한 경로)
        if (player.GetComponent<PlayerPlaceholder>() == null)
        {
            player.AddComponent<PlayerPlaceholder>();
            Debug.Log("[TestPlayerSetup] ✅ PlayerPlaceholder 부착됨 (RuntimeModelLoader가 GLB 모델 로드)");
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

        // TopDownCameraController (풀네임)
        var tdcType = typeof(ProjectName.Systems.TopDownCameraController);
        if (camGO.GetComponent(tdcType) == null)
        {
            camGO.AddComponent(tdcType);
        }

        if (camGO.GetComponent<AudioListener>() == null)
            camGO.AddComponent<AudioListener>();

        Debug.Log("[TestPlayerSetup] ✅ 카메라 설정 완료");
    }

    private void SetupGround()
            {
                // TODO: 추후 GLB 지형 모델 로드 가능 (RuntimeModelLoader.TryGetModel("terrain", ...) 사용)
                // 현재는 Plane 프리미티브 유지
                if (GameObject.Find("Ground") == null)
                {
                    var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                    ground.name = "Ground";
                    ground.transform.position = new Vector3(0, -0.5f, 0);
                    ground.transform.localScale = Vector3.one * 50f;
                
                    // URP Lit 머티리얼 적용 (초록색 잔디)
                    var renderer = ground.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        mat.color = new Color(0.2f, 0.5f, 0.2f, 1f); // 잔디색
                        mat.SetFloat("_Smoothness", 0f);
                        renderer.material = mat;
                    }
                
                    Debug.Log("[TestPlayerSetup] ✅ Ground 생성 (URP Lit 머티리얼 적용)");
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
}