using UnityEngine;
using UnityEngine.InputSystem;
using ProjectName.Systems.Animation.Procedural;
using ProjectName.Systems.Animation.Procedural.Bones;
using ProjectName.Systems.Animation.Neural;
using ProjectName.Systems; // for PlayerMovement, IVelocityProvider

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

    private NeuralAnimationController _neuralAnim;
    private HybridAnimationController _hybridAnim;

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

        // Animator (ProceduralAnimationController가 필요)
                if (player.GetComponent<Animator>() == null)
                {
                    player.AddComponent<Animator>();
                }

                // ProceduralAnimationController — 완전 프로시저럴 애니메이션 (Hybrid보다 먼저 추가)
                if (player.GetComponent<ProceduralAnimationController>() == null)
                {
                    player.AddComponent<ProceduralAnimationController>();
                }

                // ProceduralAnimStateMachine — 상태 머신
                if (player.GetComponent<ProceduralAnimStateMachine>() == null)
                {
                    player.AddComponent<ProceduralAnimStateMachine>();
                }

                // ProceduralBoneMap — 본 자동 매핑
                if (player.GetComponent<ProceduralBoneMap>() == null)
                {
                    player.AddComponent<ProceduralBoneMap>();
                }

                // NeuralAnimationController — ONNX 정책 추론 (같은 GameObject)
                _neuralAnim = player.GetComponent<NeuralAnimationController>();
                if (_neuralAnim == null)
                    _neuralAnim = player.AddComponent<NeuralAnimationController>();

                // HybridAnimationController — Procedural + Neural 브리지 (같은 GameObject, 마지막에 추가)
                _hybridAnim = player.GetComponent<HybridAnimationController>();
                if (_hybridAnim == null)
                    _hybridAnim = player.AddComponent<HybridAnimationController>();

                // ProgressiveRolloutManager에 등록
                if (ProgressiveRolloutManager.Instance != null)
                    ProgressiveRolloutManager.Instance.ConfigureHybridController(_hybridAnim);

                // PlayerPlaceholder: RuntimeModelLoader → Player_Rigged GLB 로드
                // ProceduralAnimationController가 본 구조를 자동 감지하므로 PlayerPlaceholder는 유지
                if (player.GetComponent<PlayerPlaceholder>() == null)
                {
                    player.AddComponent<PlayerPlaceholder>();
                    Debug.Log("[TestPlayerSetup] ✅ PlayerPlaceholder 부착됨 (RuntimeModelLoader가 GLB 모델 로드)");
                }

                // PlayerMovement — 이동 제어 + IVelocityProvider (NeuralAnimationController 연동용)
                var playerMovement = player.GetComponent<PlayerMovement>();
                if (playerMovement == null)
                {
                    playerMovement = player.AddComponent<PlayerMovement>();
                    // TestPlayerSetup용 설정 (public property 사용)
                    var pmType = typeof(PlayerMovement);
                    pmType.GetProperty("WalkSpeed")?.SetValue(playerMovement, _walkSpeed);
                    pmType.GetProperty("RunSpeed")?.SetValue(playerMovement, _runSpeed);
                    pmType.GetProperty("JumpHeight")?.SetValue(playerMovement, _jumpHeight);
                }

                // NeuralAnimationController에 velocity provider 설정
                if (_neuralAnim != null)
                {
                    _neuralAnim.SetVelocityProvider(playerMovement);
                }

                // ProceduralAnimationController에도 velocity provider 설정
                var procAnim = player.GetComponent<ProceduralAnimationController>();
                if (procAnim != null)
                {
                    procAnim.SetVelocityProvider(playerMovement);
                }

                // HybridAnimationController에도 velocity provider 설정
                if (_hybridAnim != null)
                {
                    _hybridAnim.SetVelocityProvider(playerMovement);
                }

                // PlayerPlaceholder: RuntimeModelLoader → Player_Rigged GLB 로드
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
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.name = "Ground";
                ground.transform.position = new Vector3(0, -1f, 0);
                ground.transform.localScale = new Vector3(100f, 1f, 100f);
            
                // Remove the default BoxCollider and add a proper one
                var collider = ground.GetComponent<BoxCollider>();
                if (collider == null)
                {
                    collider = ground.AddComponent<BoxCollider>();
                }
                collider.isTrigger = false;
            
                // URP Lit 머티리얼 적용 (초록색 잔디)
                var renderer = ground.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.color = new Color(0.2f, 0.5f, 0.2f, 1f);
                    mat.SetFloat("_Smoothness", 0f);
                    renderer.material = mat;
                }
            
                // Remove the default Cube mesh filter/renderer if we want a plane look
                // But keep it as a thick floor for reliable collision
            }
        
            // Ensure player starts above ground
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                player.transform.position = new Vector3(0, 1f, 0);
            }
        
            Debug.Log("[TestPlayerSetup] ✅ Ground 생성 (두꺼운 바닥으로 확실한 충돌)");
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