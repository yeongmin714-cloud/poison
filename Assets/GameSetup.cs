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

    private void Start()
    {
        // CRITICAL: Purge any leftover DontDestroyOnLoad singletons from previous Play sessions
        // Other systems (SoundManagerEnhanced, TerritoryManager, TimeManager) use RuntimeInitializeOnLoadMethod
        // which creates persistent objects that survive across Play mode sessions
        PurgeRuntimeSingletons();

        if (!_autoSetup) return;

        // ── 메인 씬 모드 ────────────────────────
        SetupPlayerComponents();
        SetupWorldComponents();

        _autoSetup = false; // 한 번만 실행
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
        }

        // ── BuffManager ───────────────────────────────────────────────
        if (player.GetComponent<BuffManager>() == null)
        {
            player.AddComponent<BuffManager>();
            Debug.Log("[GameSetup] ✅ BuffManager → Player에 추가");
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
        var actions = new InputActionAsset();
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