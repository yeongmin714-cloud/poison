using UnityEngine;
using UnityEngine.InputSystem;
using ProjectName.Systems.Animation.Procedural;
using ProjectName.Systems.Animation.Procedural.Bones;

namespace ProjectName.Systems
{
/// <summary>
/// 테스트 씬 전용: Player 이동에 필요한 최소 구성 요소만 설정.
/// GameManager/UIManager 등 모든 시스템을 사용하지 않음.
/// ProceduralAnimationController 사용 (완전 프로시저럴, 애니메이션 클립 0개).
/// </summary>
public class TestPlayerSetup : MonoBehaviour
{
    [Header("Player Settings")]
    [SerializeField] private float _walkSpeed = 5f;
    [SerializeField] private float _runSpeed = 10f;
    [SerializeField] private float _jumpHeight = 2f;

    [Header("Camera Settings")]
    [SerializeField] private float _orbitRadius = 15f;
    [SerializeField] private float _defaultPitch = 45f;

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

        // Rigidbody (ProceduralAnimationController가 필요)
        if (player.GetComponent<Rigidbody>() == null)
        {
            var rb = player.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        // CharacterController (충돌 감지용)
        if (player.GetComponent<CharacterController>() == null)
        {
            var cc = player.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.radius = 0.5f;
        }

        // PlayerInput (Input System 활성화용)
        if (player.GetComponent<PlayerInput>() == null)
        {
            var pi = player.AddComponent<PlayerInput>();
            pi.defaultActionMap = "Player";
            pi.notificationBehavior = PlayerNotifications.InvokeUnityEvents;
        }

        // Animator (ProceduralAnimationController가 필요)
        if (player.GetComponent<Animator>() == null)
        {
            player.AddComponent<Animator>();
        }

        // ProceduralBoneMap — 본 자동 매핑
        if (player.GetComponent<ProceduralBoneMap>() == null)
        {
            player.AddComponent<ProceduralBoneMap>();
        }

        // ProceduralAnimStateMachine — 상태 머신
        if (player.GetComponent<ProceduralAnimStateMachine>() == null)
        {
            player.AddComponent<ProceduralAnimStateMachine>();
        }

        // ProceduralAnimationController — 완전 프로시저럴 애니메이션
        if (player.GetComponent<ProceduralAnimationController>() == null)
        {
            player.AddComponent<ProceduralAnimationController>();
        }

        // PlayerPlaceholder: RuntimeModelLoader → Player_Rigged GLB 로드
        // ProceduralAnimationController가 본 구조를 자동 감지하므로 PlayerPlaceholder는 유지
        if (player.GetComponent<PlayerPlaceholder>() == null)
        {
            player.AddComponent<PlayerPlaceholder>();
            Debug.Log("[TestPlayerSetup] ✅ PlayerPlaceholder 부착됨 (RuntimeModelLoader가 GLB 모델 로드)");
        }

        // PlayerMovement 제거 (ProceduralAnimationController로 대체)
        var pm = player.GetComponent<PlayerMovement>();
        if (pm != null)
            DestroyImmediate(pm);

        // RigAnimationController 제거 (ProceduralAnimStateMachine으로 대체)
        var rac = player.GetComponent<RigAnimationController>();
        if (rac != null)
            DestroyImmediate(rac);

        player.transform.position = Vector3.zero;
        Debug.Log("[TestPlayerSetup] ✅ Player 설정 완료 (ProceduralAnimationController)");
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
                mat.color = new Color(0.2f, 0.5f, 0.2f, 1f);
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