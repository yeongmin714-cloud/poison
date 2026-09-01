using UnityEngine;
using UnityEngine.InputSystem;
using ProjectName.Core;
using ProjectName.Systems;
using ProjectName.UI;
using Unity.Cinemachine;
using ProjectName.Core; // PlayerInputHelper namespace

/// <summary>
/// 게임 시작 시 MonsterSpawner, PlayerHealth, HUD를 자동 설정.
/// Assembly-CSharp (기본 어셈블리) — 모든 asmdf 참조 가능.
/// </summary>
public class GameSetup : MonoBehaviour
{
    [Header("Auto Setup")]
    [SerializeField] private bool _autoSetup = true;

    private void Awake()
    {
        // CRITICAL: Set up Physics layer collision matrix BEFORE first physics step
        // Disable autoSimulation, configure collision matrix, then re-enable
        Physics.autoSimulation = false;
        EnsureLayerCollisionMatrix();
        Physics.autoSimulation = true;
        
        // CRITICAL: Purge any leftover DontDestroyOnLoad singletons from previous Play sessions
        PurgeRuntimeSingletons();
    }

    private void Start()
    {
        if (!_autoSetup) return;

        // ── 메인 씬 모드 ────────────────────────
        SetupPlayerComponents();
        SetupWorldComponents();

        _autoSetup = false; // 한 번만 실행
    }

    /// <summary>
    /// Ensures Player and Ground layers are set to collide in Physics settings.
    /// This is critical for CharacterController collision detection.
    /// Also creates Ground layer if it doesn't exist.
    /// </summary>
    private void EnsureLayerCollisionMatrix()
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        int groundLayer = LayerMask.NameToLayer("Ground");
        
        // If Ground layer doesn't exist, we can't create it at runtime (Editor only)
        // But we can ensure collision matrix for existing layers
        if (playerLayer >= 0 && groundLayer >= 0)
        {
            // Ensure Player collides with Ground (and vice versa)
            Physics.IgnoreLayerCollision(playerLayer, groundLayer, false);
            Physics.IgnoreLayerCollision(groundLayer, playerLayer, false);
            
            // Also ensure Player collides with Default (0) and other common layers
            Physics.IgnoreLayerCollision(playerLayer, 0, false); // Default layer
            
            Debug.Log($"[GameSetup] Physics layer collision matrix verified: Player({playerLayer}) <-> Ground({groundLayer}) = collide");
        }
        else
        {
            Debug.LogWarning($"[GameSetup] Could not find Player or Ground layer. Player={playerLayer}, Ground={groundLayer}. Ground layer must be created in Edit > Project Settings > Tags and Layers.");
            
            // Fallback: If Ground layer is missing, ensure Player collides with Default layer
            if (playerLayer >= 0)
            {
                Physics.IgnoreLayerCollision(playerLayer, 0, false);
                Debug.Log($"[GameSetup] Fallback: Player({playerLayer}) <-> Default(0) = collide");
            }
        }
    }

    /// <summary>
    /// Removes stale DontDestroyOnLoad singleton instances that persist across Play mode sessions.
    /// These are created by RuntimeInitializeOnLoadMethod in systems like SoundManagerEnhanced, TerritoryManager, TimeManager.
    /// NOTE: PlayerHealth, PlayerStats, PlayerInventory, PlayerCombat, BuffManager are scene-based (attached to Player)
    /// and should NOT be purged - they move with the Player GameObject.
    /// </summary>
    private void PurgeRuntimeSingletons()
    {
        // Core singletons that might conflict with scene-loaded instances
        // ONLY purge systems that use RuntimeInitializeOnLoadMethod and create their own DontDestroyOnLoad objects
        var typesToPurge = new System.Type[]
        {
            typeof(ProjectName.Systems.SoundManagerEnhanced),
            typeof(ProjectName.Systems.TerritoryManager),
            typeof(ProjectName.Systems.TimeManager),
        };

        foreach (var type in typesToPurge)
        {
            // Use reflection to access static Instance property and DontDestroyOnLoad objects
            var instanceProp = type.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (instanceProp != null)
            {
                var instance = instanceProp.GetValue(null);
                if (instance != null)
                {
                    var monoBehaviour = instance as MonoBehaviour;
                    if (monoBehaviour != null && monoBehaviour.gameObject != null)
                    {
                        // Only destroy if it's a DontDestroyOnLoad object from a different scene
                        if (monoBehaviour.gameObject.scene.name == "DontDestroyOnLoad" || monoBehaviour.gameObject.scene.name == null)
                        {
                            Debug.Log($"[GameSetup] Purging stale singleton: {type.Name}");
                            DestroyImmediate(monoBehaviour.gameObject);
                        }
                    }
                }
            }
        }

        // Also purge any DontDestroyOnLoad objects with known names (systems only, NOT Player components)
        var staleNames = new string[] { "SoundManagerEnhanced", "TerritoryManager", "TimeManager", "GameManager" };
        foreach (var name in staleNames)
        {
            var obj = GameObject.Find(name);
            if (obj != null && (obj.scene.name == "DontDestroyOnLoad" || obj.scene.name == null))
            {
                Debug.Log($"[GameSetup] Purging stale DontDestroyOnLoad object: {name}");
                DestroyImmediate(obj);
            }
        }
    }

    /// <summary>
    /// Player 태그 오브젝트에 PlayerHealth, BombThrower 등을 설정.
    /// PlayerHealth의 [RuntimeInitializeOnLoadMethod] auto-create와 충돌하지 않도록
    /// 이미 존재하는 Instance가 있으면 재사용합니다.
    /// </summary>
    private void SetupPlayerComponents()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("[GameSetup] ⚠️ 'Player' 태그 오브젝트를 찾을 수 없습니다. Player 관련 컴포넌트를 건너뜁니다.");
            return;
        }

        // ── Player Camera (Cinemachine Virtual Camera) 검증 ──────────
        // Player Camera는 Cinemachine VC이므로 Camera 컴포넌트 불필요
        // Main Camera(CinemachineBrain)가 유일한 렌더링 카메라
        var playerCamGO = GameObject.Find("Player Camera");
        if (playerCamGO != null)
        {
            if (!playerCamGO.activeSelf)
            {
                playerCamGO.SetActive(true);
                Debug.Log("[GameSetup] ✅ Player Camera(Cinemachine VC) 활성화");
            }
            // Camera 컴포넌트가 있으면 제거 (Cinemachine VC는 Camera가 없어야 함)
            var cam = playerCamGO.GetComponent<Camera>();
            if (cam != null)
            {
                DestroyImmediate(cam);
                Debug.Log("[GameSetup] ✅ Player Camera에서 불필요한 Camera 컴포넌트 제거");
            }
        }

        // ── Main Camera (CinemachineBrain) 검증 ─────────────────────
        var mainCam = GameObject.FindGameObjectWithTag("MainCamera");
        if (mainCam != null)
        {
            var cam = mainCam.GetComponent<Camera>();
            if (cam == null) cam = mainCam.AddComponent<Camera>();
            var brain = mainCam.GetComponent<CinemachineBrain>();
            if (brain == null) brain = mainCam.AddComponent<CinemachineBrain>();
            Debug.Log("[GameSetup] ✅ Main Camera(CinemachineBrain) 검증 완료");
        }

        // ── CinemachineInputAxisController 런타임 구성 (마우스 오비트, Input System) ──
        // Unity Input System(신형)이 활성화된 환경에서는 Cinemachine 기본 레거시
        // 'Mouse X'/'Mouse Y' 축이 동작하지 않는다. 따라서:
        //   A) PlayerInput의 'Look' 액션을 찾아 CinemachineInputAxisController.Controllers에 bind.
        //   B) 폴백: 'Look'(마우스 델타)로 CinemachineOrbitalFollow 축을 직접
        //      회전시키는 RuntimeCinemachineOrbitInput 드라이버를 추가(PlayerInput 기반).
        var vcam = GameObject.Find("Player Camera");
        var playerInputForLook = player.GetComponent<UnityEngine.InputSystem.PlayerInput>();
        if (vcam != null)
        {
            // A) PlayerInput/입력 에셋에서 'Look' 액션을 찾는다.
            UnityEngine.InputSystem.InputAction lookAction = playerInputForLook?.actions != null
                ? playerInputForLook.actions.FindAction("Look", throwIfNotFound: false) : null;
            if (lookAction == null && playerInputForLook?.actions != null)
            {
                // 맵 순회로 대소문자 구분 없이 재탐색
                foreach (var am in playerInputForLook.actions.actionMaps)
                {
                    foreach (var act in am.actions)
                    {
                        if (act.name.Equals("Look", System.StringComparison.OrdinalIgnoreCase))
                        {
                            lookAction = act;
                            break;
                        }
                    }
                    if (lookAction != null) break;
                }
            }

            var inputAxis = vcam.GetComponent<CinemachineInputAxisController>();
            if (inputAxis == null) inputAxis = vcam.AddComponent<CinemachineInputAxisController>();

            // A-2) Cinemachine 3.x: Controllers를 동기화(빈 배열이 아니라 IInputAxisOwner 축별로 채움),
            //      Look 액션을 Input System으로 바인딩한다. (레거시 'Mouse X/Y' 아님)
            int axesConfigured = PopulateCinemachineAxisControllers(inputAxis, lookAction);

            // B) 폴백 드라이버: 내장 축 컨트롤러가 IInputAxisOwner 축을 발견하지 못한 경우에만
            //    (비표준/레거시 Body 등) PlayerInput의 Look 델타로 직접 회전시킨다.
            if (axesConfigured == 0)
            {
                var driver = vcam.GetComponent<RuntimeCinemachineOrbitInput>();
                if (driver == null) driver = vcam.AddComponent<RuntimeCinemachineOrbitInput>();
                driver.player = player;
                driver.lookActionName = (lookAction != null) ? lookAction.name : "Look";
                Debug.Log($"[GameSetup] ⚠️ 내장 축 컨트롤러 백업용 RuntimeCinemachineOrbitInput 드라이버 사용 (Look='{driver.lookActionName}')");
            }
            Debug.Log($"[GameSetup] ✅ CinemachineInputAxisController configured (Look axes bound = {axesConfigured})");
        }

        // ── PlayerHealth ───────────────────────────────────────────────
        // PlayerHealth는 [RuntimeInitializeOnLoadMethod]로 자동 생성될 수 있음.
        // Instance가 이미 있으면 AddComponent하지 않고 Instance를 설정.
        if (PlayerHealth.Instance != null)
        {
            PlayerHealth.Instance.SetInvincibleTime(0.5f);
            Debug.Log("[GameSetup] ✅ PlayerHealth.Instance.SetInvincibleTime(0.5f) 설정 (기존 Instance 재사용)");
        }
        else if (player.GetComponent<PlayerHealth>() == null)
        {
            var health = player.AddComponent<PlayerHealth>();
            health.SetInvincibleTime(0.5f);
            Debug.Log("[GameSetup] ✅ PlayerHealth → Player에 추가");
        }
        else
        {
            Debug.Log("[GameSetup] ✅ PlayerHealth는 이미 Player에 존재");
        }

        // ── BombThrower ────────────────────────────────────────────────
        if (player.GetComponent<BombThrower>() == null)
        {
            player.AddComponent<BombThrower>();
            Debug.Log("[GameSetup] ✅ BombThrower → Player에 추가");
        }

        // ── PlayerStats ────────────────────────────────────────────────
        if (player.GetComponent<PlayerStats>() == null)
        {
            player.AddComponent<PlayerStats>();
            Debug.Log("[GameSetup] ✅ PlayerStats → Player에 추가");
        }

        // ── PlayerInventory ───────────────────────────────────────────
        if (player.GetComponent<PlayerInventory>() == null)
        {
            player.AddComponent<PlayerInventory>();
            Debug.Log("[GameSetup] ✅ PlayerInventory → Player에 추가");
        }

        // ── PlayerCombat ──────────────────────────────────────────────
        if (player.GetComponent<PlayerCombat>() == null)
        {
            player.AddComponent<PlayerCombat>();
            Debug.Log("[GameSetup] ✅ PlayerCombat → Player에 추가");
        }

        // ── PlayerInput (Input System) ────────────────────────────────
        if (player.GetComponent<UnityEngine.InputSystem.PlayerInput>() == null)
        {
            var pi = PlayerInputHelper.SetupPlayerInputFromResources(player);
            
            // FALLBACK: If PlayerInputHelper fails (e.g., empty actionMaps), create basic actions programmatically
            if (pi == null)
            {
                Debug.LogWarning("[GameSetup] PlayerInputHelper failed, creating fallback InputActionAsset programmatically");
                pi = CreateFallbackPlayerInput(player);
            }

            // ── CollisionDebugger (진단용) ──────────────────────────────────
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (player.GetComponent<ProjectName.Diagnostics.CollisionDebugger>() == null)
            {
                player.AddComponent<ProjectName.Diagnostics.CollisionDebugger>();
                Debug.Log("[GameSetup] ✅ CollisionDebugger → Player에 추가");
            }
            #endif

            // ── Disable GLB Renderers (런타임에 GLB 렌더러 비활성화) ──────────────
            // GLB 모델은 PlayerModel의 자식으로, 렌더러를 비활성화해야 함
            var glbModel = player.transform.Find("PlayerModel/PlayerModel_GLB");
            if (glbModel != null)
            {
                var disableType = System.Type.GetType("ProjectName.Core.DisableGLBRenderers, Assembly-CSharp");
                if (disableType != null)
                {
                    var disabler = glbModel.gameObject.GetComponent(disableType) ?? glbModel.gameObject.AddComponent(disableType);
                    // GLB 및 자식들의 모든 렌더러 수집
                    var renderers = glbModel.GetComponentsInChildren<Renderer>(true);
                    disableType.GetField("glbRenderers").SetValue(disabler, renderers);
                    Debug.Log($"[GameSetup] ✅ DisableGLBRenderers → GLB 모델에 추가 ({renderers.Length}개 렌더러)");
                }
                else
                {
                    Debug.LogWarning("[GameSetup] DisableGLBRenderers type not found");
                }
            }
            else
            {
                Debug.LogWarning("[GameSetup] GLB 모델(PlayerModel_GLB)을 찾을 수 없음");
            }

            // ── GLB 물리/애니메이션 컴포넌트 즉시 무력화 후 제거 (런타임) ─────────────────
            // 에디터 타임에 제거했지만, 누락된 Rigidbody/Animator/Joint 등이 GLB를 독립적으로 움직이게 할 수 있음.
            // ⚠️ UnityEngine.Object.Destroy()는 프레임 끝까지 지연되므로 그 사이 물리 시뮬레이션이
            //    돌며 GLB가 아래로 낙하할 수 있다. 따라서 제거 직전에 물리/애니메이션을 즉시 무력화한다.
            var glbModelCleanup = player.transform.Find("PlayerModel/PlayerModel_GLB");
            if (glbModelCleanup != null)
            {
                // 0) 모든 자식 Rigidbody를 즉시 정지(kinematic + 중력 off) -> Destroy 지연 구간의 낙하 방지
                foreach (var childRb in glbModelCleanup.GetComponentsInChildren<Rigidbody>(true))
                {
                    childRb.isKinematic = true;
                    childRb.useGravity = false;
                    childRb.interpolation = RigidbodyInterpolation.None;
                }
                var rootRb = glbModelCleanup.GetComponent<Rigidbody>();
                if (rootRb != null)
                {
                    rootRb.isKinematic = true;
                    rootRb.useGravity = false;
                    rootRb.interpolation = RigidbodyInterpolation.None;
                }

                var componentsToRemove = glbModelCleanup.GetComponentsInChildren<Component>(true);
                int removedCount = 0;
                foreach (var comp in componentsToRemove)
                {
                    if (comp is Transform) continue;
                    if (comp is Renderer) continue; // 렌더러는 DisableGLBRenderers가 관리
                    if (comp is DisableGLBRenderers) continue; // 우리 디세이블러는 유지

                    // Animator는 제거되기 전까지 계속 실행되므로 먼저 비활성화
                    if (comp is Animator anim) { anim.enabled = false; anim.applyRootMotion = false; }
                    // Rigidbody는 Destroy가 지연되는 동안 물리/낙하가 걸리지 않도록 즉시 정지
                    else if (comp is Rigidbody rigidBody)
                    {
                        rigidBody.isKinematic = true;
                        rigidBody.useGravity = false;
                    }
                    // Collider가 남아 물리가 관여하지 않도록 즉시 비활성화
                    else if (comp is Collider coll)
                    {
                        coll.enabled = false;
                    }

                    UnityEngine.Object.Destroy(comp);
                    removedCount++;
                }
                if (removedCount > 0)
                    Debug.Log($"[GameSetup] ✅ GLB 잔존 컴포넌트 {removedCount}개 즉시 무력화 후 제거 (Rigidbody/Animator/Joint/Collider 등)");
            }

            // ── GLB를 visualCapsule(=PlayerModel) 자식으로 강제 부착 + 월드 위치 일치 ──
            // 씬에서는 붙어있지만 런타임에 분리될 수 있음.
            // SetParent 직후 월드 위치를 플레이어와 일치시켜 Destroy 지연 구간 및
            // CollisionFloor(바닥)가 설정되기 전에 GLB가 낙하/이탈하지 않도록 고정한다.
            var visualCapsule = player.transform.Find("PlayerModel");
            // glbModel 변수는 이미 위에서 선언됨 (line 319)
            if (visualCapsule != null && glbModel != null)
            {
                if (glbModel.parent != visualCapsule)
                {
                    glbModel.SetParent(visualCapsule);
                    glbModel.localScale = Vector3.one;
                    Debug.Log("[GameSetup] ✅ GLB 모델을 PlayerModel(visualCapsule) 자식으로 재부착");
                }
                // SetParent 직후 월드 위치를 플레이어와 일치시켜 물리/오프셋 이탈 방지
                glbModel.position = player.transform.position;
                glbModel.rotation = player.transform.rotation;
                Debug.Log("[GameSetup] ✅ GLB 모델 월드 위치/회전을 플레이어와 일치 (낙하 방지)");
            }
            else if (visualCapsule == null)
            {
                Debug.LogWarning("[GameSetup] PlayerModel(visualCapsule)을 찾을 수 없음");
            }
        }
    }

    /// <summary>
    /// 씬에 MonsterSpawner, HUD, BuffManager가 없으면 생성합니다.
    /// </summary>
    private void SetupWorldComponents()
    {
        // ── 지형 머티리얼 적용 (런타임) ──────────────────────────────
        // FixMainScene에서 생성한 머티리얼 에셋을 로드하여 적용
        var groundObj = GameObject.Find("Ground_Inner");
        if (groundObj != null)
        {
            var mr = groundObj.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                var groundMat = Resources.Load<Material>("URP/Ground_Grass_Mat");
                if (groundMat != null)
                {
                    // 자기치유: 직렬화 누락 등으로 _BaseMap이 비어 있으면
                    // Resources 복사본(UAssets/Resources/URP)의 Terrain_Grass로 복구.
                    // 에셋 상태와 무관하게 Play 모드에서 초록 지형이 보이도록 보장한다.
                    if (groundMat.GetTexture("_BaseMap") == null)
                    {
                        var terrainGrass = Resources.Load<Texture2D>("URP/Terrain_Grass");
                        if (terrainGrass != null)
                        {
                            groundMat.SetTexture("_BaseMap", terrainGrass);
                            groundMat.SetTextureScale("_BaseMap", new Vector2(200f, 200f));
                            Debug.Log("[GameSetup] ⚠️ _BaseMap이 비어 있어 Resources의 Terrain_Grass로 자기치유 완료");
                        }
                        else
                        {
                            Debug.LogWarning("[GameSetup] _BaseMap이 비어 있고 Resources/URP/Terrain_Grass도 없어 치유 생략");
                        }
                    }

                    // 렌더러에 실제 반영: sharedMaterial을 재지정하여 dirty 상태로 만들어
                    // 다음 프레임에 재렌더되도록 한다.
                    mr.sharedMaterial = groundMat;
                    Debug.Log("[GameSetup] ✅ 지형 머티리얼 적용 완료 (Ground_Grass_Mat)");
                }
                else
                {
                    Debug.LogWarning("[GameSetup] Ground_Grass_Mat 머티리얼을 찾을 수 없음");
                }
            }
        }

        // MonsterSpawner (원점)
        if (FindAnyObjectByType<MonsterSpawner>() == null)
        {
            var spawnerGO = new GameObject("MonsterSpawner");
            spawnerGO.AddComponent<MonsterSpawner>();
            Debug.Log("[GameSetup] ✅ MonsterSpawner 생성");
        }

        // HUD
        if (FindAnyObjectByType<HUD>() == null)
        {
            var hudGO = new GameObject("HUD");
            hudGO.AddComponent<HUD>();
            Debug.Log("[GameSetup] ✅ HUD 생성");
        }

        // BuffManager
        if (FindAnyObjectByType<BuffManager>() == null)
        {
            var buffGO = new GameObject("BuffManager");
            buffGO.AddComponent<BuffManager>();
            Debug.Log("[GameSetup] ✅ BuffManager 생성");
        }

        // EventSystem (Input System 필수)
        if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            // Input System 패키지용 UI Input Module 사용 (StandaloneInputModule은 구 Input System용)
            esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            Debug.Log("[GameSetup] ✅ EventSystem 생성 (InputSystemUIInputModule)");
        }

        // MinimapUI
        if (FindAnyObjectByType<MinimapUI>() == null)
        {
            var mmGO = new GameObject("MinimapUI");
            mmGO.AddComponent<MinimapUI>();
            Debug.Log("[GameSetup] ✅ MinimapUI 생성");
        }

        // TerrainTextureApplier (Ground에 자동 부착)
        if (FindAnyObjectByType<TerrainTextureApplier>() == null)
        {
            var ground = GameObject.Find("Ground_Inner");
            if (ground != null && ground.GetComponent<TerrainTextureApplier>() == null)
            {
                ground.AddComponent<TerrainTextureApplier>();
                Debug.Log("[GameSetup] ✅ TerrainTextureApplier → Ground_Inner에 추가");
            }
        }

        // NationTerrainController
        if (FindAnyObjectByType<NationTerrainController>() == null)
        {
            var ground = GameObject.Find("Ground_Inner");
            if (ground != null && ground.GetComponent<NationTerrainController>() == null)
            {
                ground.AddComponent<NationTerrainController>();
                Debug.Log("[GameSetup] ✅ NationTerrainController → Ground_Inner에 추가");
            }
            else
            {
                var ntcGO = new GameObject("NationTerrainController");
                ntcGO.AddComponent<NationTerrainController>();
                Debug.Log("[GameSetup] ✅ NationTerrainController 생성 (Ground 없음, 별도 오브젝트)");
            }
        }

        // LoadingManager
        if (FindAnyObjectByType<LoadingManager>() == null)
        {
            var loadGO = new GameObject("LoadingManager");
            loadGO.AddComponent<LoadingManager>();
            Debug.Log("[GameSetup] ✅ LoadingManager 생성");
        }
    }

    #region Cinemachine 3.x axis helpers (Input System binding)

    /// <summary>
    /// Cinemachine 3.x의 CinemachineInputAxisController.Controllers를 Instrument System 축 액션이
    /// (IInputAxisOwner, 예: CinemachineOrbitalFollow의 'Look Orbit X/Y')에 맞춰 동기화해서 채우고,
    /// 각 로테이션 컨트롤러의 Reader.InputAction에 PlayerControls.inputactions의 'Look' 액션
    /// (InputActionReference)을 바인딩한다.
    /// Cinemachine의 Reader는 Vector2 'Look'을 힌트(X/Y)에 따라 자동으로 분리해 준다.
    /// </summary>
    private int PopulateCinemachineAxisControllers(CinemachineInputAxisController inputAxis, UnityEngine.InputSystem.InputAction lookAction)
    {
        try
        {
            // 빈 Controllers 리스트를 vcam이 가진 IInputAxisOwner 축 목록으로 실제로 채운다.
            inputAxis.SynchronizeControllers();

            var lookRef = (lookAction != null)
                ? UnityEngine.InputSystem.InputActionReference.Create(lookAction)
                : null;

            int bound = 0;
            foreach (var c in inputAxis.Controllers)
            {
                if (c == null || c.Input == null)
                    continue;

                c.Enabled = true;
                c.Input.Gain = 1f;
                // 마우스 델타는 프레임 시간 의존이므로 Cinemachine이 deltaTime으로 다시 스케일하지 않게 함
                c.Input.CancelDeltaTime = true;

                // 회전 축('Look Orbit X'/'Look Orbit Y')에만 Look 바인딩.
                // 'Orbit Scale' 등 다른 축은 바인딩하지 않아 마우스 Y로 줌이 되지 않게 한다.
                if (c.Name != null && c.Name.StartsWith("Look Orbit") && lookRef != null)
                {
                    c.Input.InputAction = lookRef;
                    bound++;
                }
            }
            return bound;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[GameSetup] Cinemachine 입력 축 구성 실패: {ex.Message}");
            return 0;
        }
    }

    #endregion

    /// <summary>
    /// Fallback: Create basic InputActionAsset programmatically when Resources.Load fails to deserialize actionMaps
    /// </summary>
    private PlayerInput CreateFallbackPlayerInput(GameObject player)
    {
        // FIX: InputActionAsset must be created via ScriptableObject.CreateInstance, not 'new'
        var actions = ScriptableObject.CreateInstance<InputActionAsset>();
        actions.name = "PlayerControls_Fallback";

        // Create Player action map
        var playerMap = actions.AddActionMap("Player");

        // Move action (Vector2)
        var moveAction = playerMap.AddAction("Move", InputActionType.Value);
        moveAction.AddBinding("<Keyboard>/w").WithGroup("Keyboard");
        moveAction.AddBinding("<Keyboard>/s").WithGroup("Keyboard");
        moveAction.AddBinding("<Keyboard>/a").WithGroup("Keyboard");
        moveAction.AddBinding("<Keyboard>/d").WithGroup("Keyboard");
        moveAction.AddBinding("<Gamepad>/leftStick").WithGroup("Gamepad");
        moveAction.expectedControlType = "Vector2";

        // Jump action (Button)
        var jumpAction = playerMap.AddAction("Jump", InputActionType.Button);
        jumpAction.AddBinding("<Keyboard>/space");
        jumpAction.AddBinding("<Gamepad>/buttonSouth");

        // Attack action (Button)
        var attackAction = playerMap.AddAction("Attack", InputActionType.Button);
        attackAction.AddBinding("<Mouse>/leftButton");
        attackAction.AddBinding("<Gamepad>/rightTrigger");

        // Dash action (Button)
        var dashAction = playerMap.AddAction("Dash", InputActionType.Button);
        dashAction.AddBinding("<Keyboard>/leftShift");
        dashAction.AddBinding("<Gamepad>/leftShoulder");

        // Interact action (Button)
        var interactAction = playerMap.AddAction("Interact", InputActionType.Button);
        interactAction.AddBinding("<Keyboard>/e");
        interactAction.AddBinding("<Gamepad>/buttonWest");

        // Roll action (Button) - for C21-02
        var rollAction = playerMap.AddAction("Roll", InputActionType.Button);
        rollAction.AddBinding("<Keyboard>/q");
        rollAction.AddBinding("<Gamepad>/buttonEast");

        // Control schemes are optional - skip for fallback to avoid API compatibility issues
        // The Input System will auto-detect based on bindings

        // Now add PlayerInput component with proper initialization order
        bool wasActive = player.activeInHierarchy;
        if (wasActive) player.SetActive(false);

        var pi = player.AddComponent<PlayerInput>();
        pi.actions = actions;
        pi.defaultActionMap = "Player";
        pi.notificationBehavior = PlayerNotifications.InvokeUnityEvents;

        if (wasActive) player.SetActive(true);

        Debug.Log($"[GameSetup] ✅ Fallback PlayerInput created with action map 'Player' ({playerMap.actions.Count} actions)");
        return pi;
    }
}

/// <summary>
/// Cinemachine 3.x 오비트 카메라(CinemachineOrbitalFollow)를 Unity Input System의
/// 'Look'(마우스 델타) 액션으로 직접 회전시키는 폴백 드라이버.
/// (GameSetup가 내장 CinemachineInputAxisController가 IInputAxisOwner 축을 못 찾은 경우에만 추가)
/// 신형 Input System이 활성화되면 Cinemachine 기본 축 공급이 동작하지 않을 수 있으므로,
/// PlayerInput의 Look 액션을 1프레임씩 읽어 HorizontalAxis/VerticalAxis 값에 직접 반영한다.
/// </summary>
public class RuntimeCinemachineOrbitInput : MonoBehaviour
{
    [Tooltip("PlayerInput을 보유한 Player 오브젝트 (보통 태그 'Player')")]
    public GameObject player;
    [Tooltip("찾을 Look 액션 이름 (PlayerControls.inputactions)")]
    public string lookActionName = "Look";
    [Tooltip("회전 감도")]
    public float lookSensitivity = 1f;
    [Tooltip("수직 회전 방향 반전")]
    public bool invertY = false;

    private CinemachineOrbitalFollow m_Orbital;
    private UnityEngine.InputSystem.InputAction m_LookAction;

    private void OnEnable() => BindReferences();

    private void Start() => BindReferences();

    private void BindReferences()
    {
        if (m_Orbital == null)
        {
            m_Orbital = GetComponent<CinemachineOrbitalFollow>();
            if (m_Orbital == null) m_Orbital = GetComponentInChildren<CinemachineOrbitalFollow>(true);
        }
        if (m_LookAction == null && player != null)
        {
            var pi = player.GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (pi != null && pi.actions != null)
            {
                m_LookAction = pi.actions.FindAction(lookActionName, throwIfNotFound: false)
                            ?? pi.actions.FindAction("Look", throwIfNotFound: false);
            }
        }
    }

    private void Update()
    {
        if (m_Orbital == null) { BindReferences(); return; }
        if (m_LookAction == null) { BindReferences(); if (m_LookAction == null) return; }

        var delta = m_LookAction.ReadValue<Vector2>();
        if (delta.sqrMagnitude < 0.0001f) return;

        // 가로 회전 (마우스 X) — InputAxis.ClampValue가 범위/래핑을 처리
        m_Orbital.HorizontalAxis.Value =
            m_Orbital.HorizontalAxis.ClampValue(m_Orbital.HorizontalAxis.Value + delta.x * lookSensitivity * Time.deltaTime);

        // 세로 회전 (마우스 Y) + 범위 제한
        float vs = (invertY ? -delta.y : delta.y) * lookSensitivity * Time.deltaTime;
        m_Orbital.VerticalAxis.Value =
            m_Orbital.VerticalAxis.ClampValue(m_Orbital.VerticalAxis.Value + vs);
    }
}