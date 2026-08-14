using UnityEngine;
using UnityEngine.InputSystem;
using ProjectName.Systems.Animation.Procedural;
using ProjectName.Systems.Animation.Procedural.Bones;
using ProjectName.Systems.Animation.Neural;
using ProjectName.Systems; // for PlayerMovement, IVelocityProvider
using ProjectName.Core;

namespace ProjectName.Systems
{
/// <summary>
/// 테스트 씬 전용: Player 이동에 필요한 최소 구성 요소만 설정.
/// GameManager/UIManager 등 모든 시스템을 사용하지 않음.
/// ProceduralAnimationController 사용 (완전 프로시저럴, 애니메이션 클립 0개).
/// GLB 모델 로드 활성화 — PlayerPlaceholder가 자동으로 GLB 로드 및 본 매핑 처리.
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
        // 카메라를 가장 먼저 생성 (PlayerMovement.Awake가 카메라 참조함)
        SetupCamera();
        SetupGround();
        SetupLight();
        EnsureEventSystem();
        SetupPlayer(); // 마지막에 플레이어 생성 (카메라/바닥 준비 후)
    }

    private void SetupPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            player = new GameObject("Player");
            player.tag = "Player";
        }

        // 플레이어를 바닥 위에 배치 (CharacterController height=2, center=0,1,0 고려)
        player.transform.position = new Vector3(PlayerSpawnConfig.SpawnPosition.x, 1.1f, PlayerSpawnConfig.SpawnPosition.z);

        // Rigidbody (ProceduralAnimationController가 필요) — Kinematic으로 설정해 CharacterController와 충돌 방지
        if (player.GetComponent<Rigidbody>() == null)
        {
            var rb = player.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.useGravity = false; // CharacterController가 중력/이동 처리
            rb.isKinematic = true; // 물리 시뮬레이션 비활성화
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezePositionY;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        // CharacterController (충돌 감지용) - center.y = 1로 바닥 밀착 방지
        if (player.GetComponent<CharacterController>() == null)
        {
            var cc = player.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.radius = 0.5f;
            cc.center = new Vector3(0, 1f, 0);
            cc.slopeLimit = 45f;
            cc.stepOffset = 0.3f;
            cc.skinWidth = 0.08f; // 기본값보다 약간 크게 (충돌 안정성)
            cc.minMoveDistance = 0f;
        }

        // Animator (ProceduralAnimationController가 필요)
        if (player.GetComponent<Animator>() == null)
        {
            player.AddComponent<Animator>();
        }

        // 1순위: ProceduralAnimationController — 완전 프로시저럴 애니메이션 (가장 먼저 추가)
        var procAnim = player.GetComponent<ProceduralAnimationController>();
        if (procAnim == null)
        {
            procAnim = player.AddComponent<ProceduralAnimationController>();
        }

        // 2순위: ProceduralAnimStateMachine — 상태 머신
        if (player.GetComponent<ProceduralAnimStateMachine>() == null)
        {
            player.AddComponent<ProceduralAnimStateMachine>();
        }

        // 3순위: ProceduralBoneMap — 본 자동 매핑
        if (player.GetComponent<ProceduralBoneMap>() == null)
        {
            player.AddComponent<ProceduralBoneMap>();
        }

        // 4순위: NeuralAnimationController — ONNX 정책 추론
        _neuralAnim = player.GetComponent<NeuralAnimationController>();
        if (_neuralAnim == null)
            _neuralAnim = player.AddComponent<NeuralAnimationController>();

        // 5순위: HybridAnimationController — Procedural + Neural 브리지 (마지막에 추가, ProcAnim 참조 가능)
        _hybridAnim = player.GetComponent<HybridAnimationController>();
        if (_hybridAnim == null)
            _hybridAnim = player.AddComponent<HybridAnimationController>();

        // ProgressiveRolloutManager에 등록
        if (ProgressiveRolloutManager.Instance != null)
            ProgressiveRolloutManager.Instance.ConfigureHybridController(_hybridAnim);

        // PlayerPlaceholder 활성화 — GLB 모델 로드 및 본 매핑 자동 처리
        var placeholder = player.GetComponent<PlayerPlaceholder>();
        if (placeholder == null)
        {
            placeholder = player.AddComponent<PlayerPlaceholder>();
        }
        // GLB 모델이 없으면 캡슐 폴백 생성 (PlayerPlaceholder.Start에서 처리)

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

        // VelocityProvider 설정 (모든 컨트롤러에)
        if (_neuralAnim != null)
            _neuralAnim.SetVelocityProvider(playerMovement);
        if (procAnim != null)
            procAnim.SetVelocityProvider(playerMovement);
        if (_hybridAnim != null)
            _hybridAnim.SetVelocityProvider(playerMovement);

        Debug.Log("[TestPlayerSetup] ✅ Player 설정 완료 (ProceduralAnimationController + Neural + Hybrid)");

        // NeuralModelDatabase에서 ONNX 모델 자동 로드
        LoadNeuralModelsFromDatabase();

        // GLB 로드 후 플레이어 위치 보정 및 GLB 콜라이더 제거
        StartCoroutine(EnsurePlayerOnGroundAfterGLBLoad(player));
    }

    /// <summary>
            /// NeuralModelDatabase.asset에서 정책별 모델 경로를 읽어 NeuralAnimationController에 비동기 로드.
            /// 테스트 씬에서 별도 메뉴 실행 없이 바로 신경망 애니메이션 확인 가능.
            /// </summary>
            private void LoadNeuralModelsFromDatabase()
            {
                var db = Resources.Load<NeuralModelDatabase>("NeuralModelDatabase");
                if (db == null)
                {
                    Debug.LogWarning("[TestPlayerSetup] NeuralModelDatabase not found. Run Tools/Neural/Auto-Setup Model Database first.");
                    return;
                }

                if (_neuralAnim == null)
                {
                    Debug.LogWarning("[TestPlayerSetup] NeuralAnimationController not found.");
                    return;
                }

                int loaded = 0;
                foreach (var entry in db.Policies)
                {
                    if (!string.IsNullOrEmpty(entry.modelPath))
                    {
                        Debug.Log($"[TestPlayerSetup] Loading neural model: {entry.policyType} -> {entry.modelPath}");
                        _neuralAnim.LoadModelAsync(entry.policyType, entry.modelPath);
                        loaded++;
                    }
                }

                if (loaded > 0)
                {
                    Debug.Log($"[TestPlayerSetup] 🧠 Neural models loading started: {loaded} policies from database.");
                }
                else
                {
                    Debug.LogWarning("[TestPlayerSetup] No valid model entries in NeuralModelDatabase.");
                }
            }

    /// <summary>
            /// GLB 모델 로드 완료 후 플레이어를 바닥에 정확히 위치시키고 GLB 콜라이더 제거
            /// </summary>
            private System.Collections.IEnumerator EnsurePlayerOnGroundAfterGLBLoad(GameObject player)
            {
                // PlayerPlaceholder.Start()에서 GLB 로드하므로 한 프레임 대기
                yield return null;
                yield return null;

                var model = player.transform.Find("PlayerModel");
                if (model != null)
                {
                    // GLB 모델의 모든 콜라이더 비활성화/제거 (CharacterController가 충돌 처리)
                    var colliders = model.GetComponentsInChildren<Collider>(true);
                    foreach (var col in colliders)
                    {
                        if (col != null && col != player.GetComponent<CharacterController>())
                        {
                            DestroyImmediate(col);
                        }
                    }

                    // GLB 모델의 Rigidbody도 제거 (kinematic이어도 간섭 가능)
                    var rb = model.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        DestroyImmediate(rb);
                    }

                    // GLB 모델을 별도 레이어로 이동 (Player와 충돌 안 함)
                    model.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast"); // 또는 별도 레이어
                    foreach (Transform child in model.GetComponentsInChildren<Transform>(true))
                    {
                        child.gameObject.layer = model.gameObject.layer;
                    }

                    // 모델 로컬 위치를 원점으로 리셋 (CharacterController가 이동 주체)
                    model.localPosition = Vector3.zero;
                    model.localRotation = Quaternion.identity;
                }

                // CharacterController가 완전히 초기화될 때까지 한 프레임 더 대기
                yield return null;

                // 플레이어를 바닥에 정확히 위치 (Ground top y=0, CC height=2, center=0,1,0 → bottom at player.y - 1)
                // skinWidth(0.08) 고려해서 약간 위에 배치
                var cc = player.GetComponent<CharacterController>();
                float targetY = cc != null ? 1f + cc.radius + cc.skinWidth + 0.05f : 1.05f;
                player.transform.position = new Vector3(PlayerSpawnConfig.SpawnPosition.x, targetY, PlayerSpawnConfig.SpawnPosition.z);

                // Ground check raycast로 실제 바닥 높이 검증
                if (Physics.Raycast(player.transform.position + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit, 2f, ~0, QueryTriggerInteraction.Ignore))
                {
                    float groundY = hit.point.y;
                    float ccBottom = player.transform.position.y - (cc != null ? cc.height * 0.5f - cc.center.y : 1f);
                    if (ccBottom < groundY + 0.01f)
                    {
                        player.transform.position = new Vector3(PlayerSpawnConfig.SpawnPosition.x, groundY + (cc != null ? cc.height * 0.5f - cc.center.y : 1f) + 0.02f, PlayerSpawnConfig.SpawnPosition.z);
                        Debug.Log($"[TestPlayerSetup] Ground raycast corrected player Y: {player.transform.position.y:F3} (ground at {groundY:F3})");
                    }
                }

                Debug.Log($"[TestPlayerSetup] ✅ GLB 로드 후 플레이어 위치 보정 완료 (Y={player.transform.position.y:F3})");

                // 추가: 몇 프레임 동안 위치 강제 유지 (다른 스크립트가 밀지 못하게)
                for (int i = 0; i < 5; i++)
                {
                    yield return null;
                    if (player != null && cc != null)
                    {
                        float currentBottom = player.transform.position.y - (cc.height * 0.5f - cc.center.y);
                        if (currentBottom < 0.01f)
                        {
                            player.transform.position = new Vector3(player.transform.position.x, targetY, player.transform.position.z);
                        }
                    }
                }
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
            ground.transform.position = new Vector3(0, -0.5f, 0);
            ground.transform.localScale = new Vector3(100f, 1f, 100f);
    
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
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                var mat = new Material(shader);
                mat.color = new Color(0.2f, 0.5f, 0.2f, 1f);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0f);
                renderer.material = mat;
            }
        }

        Debug.Log("[TestPlayerSetup] ✅ Ground 생성 (Cube + BoxCollider, y=0 상면)");
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

    /// <summary>
    /// 에디터에서 CharacterController와 Ground 충돌 시각화
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        var cc = GetComponent<CharacterController>();
        if (cc != null)
        {
            // CharacterController bounds
            Gizmos.color = new Color(0, 1, 0, 0.3f);
            Vector3 center = transform.position + cc.center;
            Gizmos.DrawWireCube(center, new Vector3(cc.radius * 2, cc.height, cc.radius * 2));
        
            // Ground check ray
            Gizmos.color = Color.red;
            float rayDist = cc.height * 0.5f + cc.skinWidth + 0.1f;
            Gizmos.DrawRay(transform.position + Vector3.up * cc.skinWidth, Vector3.down * rayDist);
        
            // Bottom sphere
            Gizmos.color = new Color(1, 0, 0, 0.3f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * cc.skinWidth, cc.radius * 0.9f);
        }
    }
}
}