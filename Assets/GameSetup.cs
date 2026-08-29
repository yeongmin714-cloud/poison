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

        // ── CinemachineInputAxisController 런타임 구성 (마우스 오비트) ──────────────
        var vcam = GameObject.Find("Player Camera");
        if (vcam != null)
        {
            var inputAxis = vcam.GetComponent<CinemachineInputAxisController>();
            if (inputAxis == null)
            {
                inputAxis = vcam.AddComponent<CinemachineInputAxisController>();
            }
            
            // Configure axes using reflection (SerializedObject not available at runtime)
            var inputAxisType = inputAxis.GetType();
            
            // Create X axis (Mouse X -> horizontal rotation)
            var axisXType = System.Type.GetType("Unity.Cinemachine.CinemachineInputAxisController+AxisState, Unity.Cinemachine");
            if (axisXType != null)
            {
                var axisX = System.Activator.CreateInstance(axisXType);
                axisXType.GetProperty("ValueRange").SetValue(axisX, new Vector2(-180, 180));
                axisXType.GetProperty("Wrap").SetValue(axisX, true);
                axisXType.GetProperty("Speed").SetValue(axisX, 180f);
                axisXType.GetProperty("AccelTime").SetValue(axisX, 0.1f);
                axisXType.GetProperty("DecelTime").SetValue(axisX, 0.1f);
                axisXType.GetProperty("InputAxisName").SetValue(axisX, "Mouse X");
                axisXType.GetProperty("InputAxisValue").SetValue(axisX, 0f);
                
                var xAxisField = inputAxisType.GetField("XAxis", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (xAxisField != null) xAxisField.SetValue(inputAxis, axisX);
            }
            
            // Create Y axis (Mouse Y -> vertical rotation)
            var axisYType = System.Type.GetType("Unity.Cinemachine.CinemachineInputAxisController+AxisState, Unity.Cinemachine");
            if (axisYType != null)
            {
                var axisY = System.Activator.CreateInstance(axisYType);
                axisYType.GetProperty("ValueRange").SetValue(axisY, new Vector2(-80, 80));
                axisYType.GetProperty("Wrap").SetValue(axisY, false);
                axisYType.GetProperty("Speed").SetValue(axisY, 180f);
                axisYType.GetProperty("AccelTime").SetValue(axisY, 0.1f);
                axisYType.GetProperty("DecelTime").SetValue(axisY, 0.1f);
                axisYType.GetProperty("InputAxisName").SetValue(axisY, "Mouse Y");
                axisYType.GetProperty("InputAxisValue").SetValue(axisY, 0f);
                
                var yAxisField = inputAxisType.GetField("YAxis", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (yAxisField != null) yAxisField.SetValue(inputAxis, axisY);
            }
            
            // Also set the controller manager's axes
            var controllerManager = inputAxisType.GetProperty("ControllerManager");
            if (controllerManager != null)
            {
                var cm = controllerManager.GetValue(inputAxis);
                if (cm != null)
                {
                    var cmType = cm.GetType();
                    var controllersField = cmType.GetField("Controllers", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (controllersField != null)
                    {
                        var axisStateType = System.Type.GetType("Unity.Cinemachine.CinemachineInputAxisController+AxisState, Unity.Cinemachine");
                        var controllerArray = System.Array.CreateInstance(axisStateType, 2);
                        
                        var xAxisField = inputAxisType.GetField("XAxis", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        var yAxisField = inputAxisType.GetField("YAxis", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (xAxisField != null) controllerArray.SetValue(xAxisField.GetValue(inputAxis), 0);
                        if (yAxisField != null) controllerArray.SetValue(yAxisField.GetValue(inputAxis), 1);
                        
                        controllersField.SetValue(cm, controllerArray);
                    }
                }
            }
            
            Debug.Log("[GameSetup] ✅ CinemachineInputAxisController configured for mouse orbit at runtime");
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

            // ── GLB 물리/애니메이션 컴포넌트 완전 제거 (런타임) ─────────────────
            // 에디터 타임에 제거했지만, 누락된 Rigidbody/Animator/Joint 등이 GLB를 독립적으로 움직이게 할 수 있음
            var glbModelCleanup = player.transform.Find("PlayerModel/PlayerModel_GLB");
            if (glbModelCleanup != null)
            {
                var componentsToRemove = glbModelCleanup.GetComponentsInChildren<Component>(true);
                int removedCount = 0;
                foreach (var comp in componentsToRemove)
                {
                    if (comp is Transform) continue;
                    if (comp is Renderer) continue; // 렌더러는 DisableGLBRenderers가 관리
                    if (comp is DisableGLBRenderers) continue; // 우리 디세이블러는 유지
                    UnityEngine.Object.Destroy(comp);
                    removedCount++;
                }
                if (removedCount > 0)
                    Debug.Log($"[GameSetup] ✅ GLB 잔존 컴포넌트 {removedCount}개 제거 (Rigidbody/Animator/Joint 등)");
            }

            // ── GLB를 visualCapsule(=PlayerModel) 자식으로 강제 부착 ──────────────────
            // 씬에서는 붙어있지만 런타임에 분리될 수 있음
            var visualCapsule = player.transform.Find("PlayerModel");
            // glbModel 변수는 이미 위에서 선언됨 (line 319)
            if (visualCapsule != null && glbModel != null && glbModel.parent != visualCapsule)
            {
                glbModel.SetParent(visualCapsule);
                glbModel.localPosition = Vector3.zero;
                glbModel.localScale = Vector3.one;
                Debug.Log("[GameSetup] ✅ GLB 모델을 PlayerModel(visualCapsule) 자식으로 재부착");
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