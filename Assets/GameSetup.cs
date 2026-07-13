using UnityEngine;
using ProjectName.Core;
using ProjectName.Systems;
using ProjectName.UI;

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
        if (!_autoSetup) return;

        // ── 테스트 씬 모드 확인 ─────────────────────────────────────
        var testConfig = FindAnyObjectByType<TestSceneConfig>();
        if (testConfig != null && testConfig.isTestScene)
        {
            Debug.Log($"[GameSetup] 🧪 테스트 씬 모드: {testConfig.testFocus}");

            // 필수 시스템 보강
            EnsureEventSystem();
            EnsureLoadingManager();

            // Player에 Input System 컴포넌트만 추가 (카메라/Health는 건드리지 않음)
            AddPlayerInputOnly();

            _autoSetup = false;
            return;
        }

        // ── 메인 씬 모드 (기존 전체 초기화) ────────────────────────
        SetupPlayerComponents();
        SetupWorldComponents();

        _autoSetup = false; // 한 번만 실행
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

        // ── Player Camera 활성화 및 Camera 컴포넌트 추가 ─────────────
        var playerCamGO = GameObject.Find("Player Camera");
        if (playerCamGO != null)
        {
            if (!playerCamGO.activeSelf)
            {
                // Main Camera 비활성화 (충돌 방지)
                var mainCam = GameObject.FindGameObjectWithTag("MainCamera");
                if (mainCam != null && mainCam != playerCamGO)
                    mainCam.SetActive(false);

                playerCamGO.SetActive(true);
                Debug.Log("[GameSetup] ✅ Player Camera 활성화 (Main Camera 비활성화)");
            }
            if (playerCamGO.GetComponent<Camera>() == null)
            {
                var cam = playerCamGO.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.Depth;
                cam.cullingMask = -1;
                cam.depth = 0;
                Debug.Log("[GameSetup] ✅ Player Camera에 Camera 컴포넌트 추가");
            }
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
            var pi = player.AddComponent<UnityEngine.InputSystem.PlayerInput>();
            pi.defaultActionMap = "Player";
            pi.notificationBehavior = UnityEngine.InputSystem.PlayerNotifications.InvokeUnityEvents;
            Debug.Log("[GameSetup] ✅ PlayerInput → Player에 추가");
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
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            Debug.Log("[GameSetup] ✅ EventSystem 생성");
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
            var ntcGO = new GameObject("NationTerrainController");
            ntcGO.AddComponent<NationTerrainController>();
            Debug.Log("[GameSetup] ✅ NationTerrainController 생성");
        }

        // LoadingManager
        if (FindAnyObjectByType<LoadingManager>() == null)
        {
            var loadGO = new GameObject("LoadingManager");
            loadGO.AddComponent<LoadingManager>();
            Debug.Log("[GameSetup] ✅ LoadingManager 생성");
        }
    }

    // ── 테스트 씬 전용: Input System만 추가 (카메라/Health 건드리지 않음) ──
    private void AddPlayerInputOnly()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("[GameSetup] ⚠️ 테스트 씬: 'Player' 태그 없음");
            return;
        }

        // PlayerInput만 추가 (메인씬에서 복제되지 않음)
        if (player.GetComponent<UnityEngine.InputSystem.PlayerInput>() == null)
        {
            var pi = player.AddComponent<UnityEngine.InputSystem.PlayerInput>();
            pi.defaultActionMap = "Player";
            pi.notificationBehavior = UnityEngine.InputSystem.PlayerNotifications.InvokeUnityEvents;
            Debug.Log("[GameSetup] ✅ PlayerInput → Player에 추가 (테스트 씬)");
        }
    }

    // ── 테스트 씬 전용: 필수 시스템 보강 ─────────────────────

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            Debug.Log("[GameSetup] ✅ EventSystem 생성 (테스트 씬)");
        }
    }

    private void EnsureLoadingManager()
    {
        if (FindAnyObjectByType<LoadingManager>() == null)
        {
            var loadGO = new GameObject("LoadingManager");
            loadGO.AddComponent<LoadingManager>();
        }
    }
}