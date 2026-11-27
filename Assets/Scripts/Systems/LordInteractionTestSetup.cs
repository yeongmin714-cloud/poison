using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// [2026-10-14] Test_17_LordInteraction 전용 — 침략 영주 NPC(영주실) ↔ 내 소속 병사 상호작용 검증 테스트씬.
    ///
    /// 목적: **내 소속 병사 ↔ 영주 NPC(침략 영주) 상호작용이 가시적으로 성립하는지** 실측 검증.
    ///   내가 다른 영지(적 영주 소유)의 성 내부에 들어온 시나리오를 재현:
    ///   - 영주 NPC 1명 = 영주실(북쪽 침실 앵커 0,0,28)에 배치 (InvadedLordEncounter.SpawnForInvadedTerritory).
    ///   - 내 소속 병사 3명 = 남쪽 로비에서 대기 — 자동 공격 없음, 수동 명령 검증용.
    ///
    /// 검증 포인트 (Play 게이트):
    ///   ① 좌클릭 영주 → 상호작용 패널(대화하기/공격하기 2버튼)
    ///   ② 대화하기 → InvadedLordDialogueUTK(처형/추궁/풀어주기)
    ///   ③ 공격하기 → 영주 반격은 플레이어만 대상(TickCombat — 플레이어 HP 감소)
    ///   ④ 병사 선택 → 영주 우클릭 → 병사가 영주를 타격(영주 HP 감소) → 제압 → 처형/풀어주기
    ///
    /// 소유권 선행세팅 필수: InvadedLordEncounter.Initialize의 RegisterInvadedLord는
    /// TerritoryDatabase.GetState(key).ownership == LordOwned 게이트 — 미세팅 시 등록 거부로
    /// BeginCombat/Resolve(처형·풀어주기) 전부 불능. Awake에서 EnsureLordScenarioOwnership으로 먼저 세팅한다.
    /// </summary>
    public sealed class LordInteractionTestSetup : MonoBehaviour
    {
        public const string TestSceneName = "Test_17_LordInteraction";

        // 침공 대상 적 영지 — 서부(West). 내 병사는 동부(East) → 국가 상이 적대 구조 유지(Test_16 선례).
        private const NationType EnemyNation = NationType.West;
        private const NationType MyNation = NationType.East;
        private const string EnemyNationStyle = "Western";
        private const string InvadedTerritoryKey = "West_01";

        // ---- Test_17 전용 상태 (Awake에서 기록 → OnDestroy에서 원복) ----
        // 씬 전용 게이트: Awake에서 Test_17일 때만 true. 다른 씬에서는 아무것도 건드리지 않는다.
        private bool _owned;
        private bool _previousBuildPaused;
        private bool _builderPreexisting;          // Awake 시점에 TerritoryBuilder가 이미 있었는지
        private Color _ambientBeforeTest;           // 테스트 진입 전 RenderSettings 값(원복용)
        private Color _baseAmbient;                 // 빌더가 설정한 실내 앰비언트 (보정 기준)
        private DayNightCycle _dnc;                 // 봉쇄한 DayNightCycle 참조
        private bool _dncWasEnabled;
        private readonly List<(Light light, float intensity, LightShadows shadows)> _lightSnapshots = new List<(Light, float, LightShadows)>();
        private bool _lightingApplied;
        private bool _previousSquadModeActive;     // GuardSelectionManager.squadModeActive 진입 전 값(원복용)

        // Test_17 전용 조명 보정 (+25%). 공유 PlayerCastleInteriorBuilder/IndoorLighting 계약은 건드리지 않음.
        private const float TestOnlyAmbientBoost = 1.25f;
        private const int LightingMaxWaitFrames = 300;

        private void Awake()
        {
            if (gameObject.scene.name != TestSceneName)
                return;
            _owned = true;

            // [44차 계약] 실내 진입 상태 — 실내 이동/전투 게이트가 실제 실내처럼 작동.
            UITransitionState.IndoorActive = true;

            // [Test_17 전용] 월드 영지 성 GLB 생성 차단.
            // GameManager.Start → EnsureTerritoryManager가 TerritoryBuilder를 추가하고, 그 Start가
            // 82개 영지 성 GLB를 배치한다. BuildPaused=true는 그 Start(이 씬 로드 프레임 내)에서 읽히므로
            // Awake에서 세우고, OnDestroy에서 이전 값으로 복원한다.
            _previousBuildPaused = TerritoryBuilder.BuildPaused;
            TerritoryBuilder.BuildPaused = true;
            _builderPreexisting = Object.FindAnyObjectByType<TerritoryBuilder>(FindObjectsInactive.Include) != null;

            // 테스트 진입 전 조명 상태 기록 (OnDestroy에서 원복).
            _ambientBeforeTest = RenderSettings.ambientLight;

            NeutralizeSceneSunlights();
            EnsureGuardSystems();
            EnsureGameManager();

            // [Test_17] 소유권 선행세팅 — RegisterInvadedLord의 LordOwned 게이트 통과 조건.
            // SpawnForInvadedTerritory(Initialize→RegisterInvadedLord)보다 먼저 세팅해야 등록이 성립한다.
            EnsureLordScenarioOwnership();

            // [Test_17] 기존 RTS 마우스 조작 활성화: 드래그 선택(부대 모드) + 우클릭 명령(RTSCommandSystem).
            // 진입 전 squadModeActive 값은 OnDestroy에서 원복.
            _previousSquadModeActive = GuardSelectionManager.squadModeActive;
            GuardSelectionManager.squadModeActive = true;
            EnsureRtsControls();
            // 좌클릭 NPC 패널 라우팅 보장 — 영주 좌클릭 → InteractionPanelUTK(대화/공격) 경로.
            ContextCommandRouter.Ensure();

            GameObject room = PlayerCastleInteriorBuilder.BuildPlayerCastleInterior(EnemyNationStyle, 0);
            if (room == null)
            {
                Debug.LogError("[LordInteractionTest] PlayerCastleInteriorBuilder returned null.");
                return;
            }

            IndoorMaterialFactory.ApplyToRoom(room,
                PlayerCastleInteriorBuilder.RoomWidth,
                PlayerCastleInteriorBuilder.RoomHeight,
                PlayerCastleInteriorBuilder.RoomDepth);
            HideCeiling(room);

            // 빌더가 설정한 실내 앰비언트를 보정 기준으로 캡처 (DayNightCycle 봉쇄는 코루틴에서 1회).
            _baseAmbient = RenderSettings.ambientLight;
            StartCoroutine(ApplyTestLightingWhenReady());

            // ③ 영주 NPC — 영주실(북쪽 침실 앵커 0,0,28) 배치.
            var lord = InvadedLordEncounter.SpawnForInvadedTerritory(room, InvadedTerritoryKey);
            if (lord == null)
                Debug.LogError("[LordInteractionTest] 영주 스폰 실패 — TerritoryDatabase/키/소유권 확인");
            else
            {
                Debug.Log($"[LordInteractionTest] 👑 영주 배치 — {lord.LordName} @ 영주실(0,0,28) HP={lord.MaxHP}");
                // [69차 P3] 영주 근접 F 프롬프트 — 63차 근접 프롬프트 라벨 패턴 통일.
                EInteractionPromptBridge.RaiseRegister(lord.transform, "F키를 눌러 영주와 대화", 3f);
            }

            // ④ 내 소속 병사 3명 (자동 공격 없음 — 수동 명령 검증용).
            SpawnMyInteractionSquad(room);

            EnsurePlayer();
            EnsureCamera();
            Debug.Log("[LordInteractionTest] 🧪 영주 상호작용 테스트 준비 — ①좌클릭 영주→패널(대화/공격) ②대화→처형/추궁/풀어주기 ③공격→영주 반격(플레이어 HP) ④병사 선택→영주 우클릭→병사 타격(영주 HP)→제압→처형/풀어주기");
        }

        private void OnDestroy()
        {
            if (!_owned) return;
            _owned = false;

            UITransitionState.IndoorActive = false;
            TerritoryBuilder.BuildPaused = _previousBuildPaused;
            // 부대 모드 임시 설정 원복 (RTS 매니저 자체는 공유 서비스라 파괴하지 않는다).
            GuardSelectionManager.squadModeActive = _previousSquadModeActive;

            // GameManager(DontDestroyOnLoad)가 이 씬 중 만든 TerritoryBuilder는 BuildPaused=true 상태로
            // Start가 끝나 한 번도 빌드하지 않았다. 이 상태로 남기면 다음 씬(메인 등)에서 영지가 생성되지
            // 않으므로, 이 씬이 만든 빌더 컴포넌트만 제거한다. 다음 EnsureTerritoryManager가 다시 추가한다.
            if (!_builderPreexisting)
            {
                var builder = Object.FindAnyObjectByType<TerritoryBuilder>(FindObjectsInactive.Include);
                if (builder != null) Destroy(builder);
            }

            // 조명 원복: 봉쇄했던 DayNightCycle 복귀, 방향광 원래 값, 앰비언트 원래 값.
            if (_dnc != null) _dnc.enabled = _dncWasEnabled;
            foreach (var snap in _lightSnapshots)
            {
                if (snap.light == null) continue;
                snap.light.intensity = snap.intensity;
                snap.light.shadows = snap.shadows;
            }
            _lightSnapshots.Clear();
            _dnc = null;
            if (_lightingApplied)
            {
                RenderSettings.ambientLight = _ambientBeforeTest;
                _lightingApplied = false;
            }
        }

        /// <summary>
        /// [Test_17 전용] 침략 영주 시나리오 소유권 선행세팅 — TestTerritoryCombatSetup.EnsureLordsWarDbOwnership 선례.
        /// 서부(West_01)를 LordOwned로 세팅해 RegisterInvadedLord의 소유권 게이트를 통과시킨다.
        /// </summary>
        private static void EnsureLordScenarioOwnership()
        {
            try
            {
                var db = TerritoryDatabase.Instance;
                if (db == null)
                {
                    Debug.LogError("[LordInteractionTest] TerritoryDatabase.Instance가 null — 소유권 세팅 불가");
                    return;
                }
                db.SetOwnership(new TerritoryId(NationType.West, 1), TerritoryOwnership.LordOwned);
                Debug.Log("[LordInteractionTest] ✅ 영지 소유 선행세팅 — West_01 → LordOwned (RegisterInvadedLord 게이트 통과용)");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LordInteractionTest] 영지 DB 소유권 세팅 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// [Test_17 전용] GameManager.Start가 만드는 DayNightCycle은 매 프레임 RenderSettings.ambientLight를
        /// 덮어쓰고 MoonLight(방향광)를 켠다. DayNightCycle이 나타나면 1회 봉쇄(enabled=false)하고,
        /// 방향광을 끈 뒤 기준 앰비언트 ×boost를 1회 적용한다. OnDestroy에서 모두 원복한다.
        /// 별도 컴포넌트 없이 이 씬 오브젝트의 코루틴으로 수행 (씬 언로드 시 자동 중단).
        /// </summary>
        private IEnumerator ApplyTestLightingWhenReady()
        {
            for (int frame = 0; frame < LightingMaxWaitFrames; frame++)
            {
                var dnc = Object.FindAnyObjectByType<DayNightCycle>(FindObjectsInactive.Include);
                if (dnc != null)
                {
                    // 1) 주기 조명 봉쇄. 이전 상태를 기록해 OnDestroy에서 복원.
                    _dnc = dnc;
                    _dncWasEnabled = dnc.enabled;
                    dnc.enabled = false;

                    // 2) 방향광(MoonLight 등) 끄기 — 원래 값 스냅샷 후 제거.
                    foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    {
                        if (light == null || light.type != LightType.Directional) continue;
                        _lightSnapshots.Add((light, light.intensity, light.shadows));
                        light.intensity = 0f;
                        light.shadows = LightShadows.None;
                    }

                    // 3) 기준 앰비언트 ×boost 1회 적용.
                    RenderSettings.ambientLight = new Color(
                        _baseAmbient.r * TestOnlyAmbientBoost,
                        _baseAmbient.g * TestOnlyAmbientBoost,
                        _baseAmbient.b * TestOnlyAmbientBoost);
                    _lightingApplied = true;

                    Debug.Log($"[LordInteractionTest] 조명 보정 적용 — DayNightCycle 봉쇄(원복 예정), 방향광 {_lightSnapshots.Count}개 끔, 앰비언트 ×{TestOnlyAmbientBoost:F2} → {RenderSettings.ambientLight}");
                    yield break;
                }
                yield return null;
            }
            Debug.LogWarning("[LordInteractionTest] DayNightCycle을 찾지 못해 테스트 조명 보정을 건너뜀.");
        }

        private static void NeutralizeSceneSunlights()
        {
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var light in lights)
            {
                if (light == null || light.type != LightType.Directional) continue;
                light.intensity = 0f;
                light.shadows = LightShadows.None;
            }
        }

        /// <summary>전투 판정/오버랩스피어에 필요한 콜라이더+kinematic rb를 보장한다.
        /// TerritoryBuilder.CreateGuard(FBX 경로)는 콜라이더를 추가하지 않으므로
        /// 병사↔영주 상호작용 판정이 쌍방에서 작동하도록 여기서 보강.</summary>
        private static void EnsureCombatCollider(GameObject go)
        {
            if (go.GetComponent<Collider>() == null)
            {
                var col = go.AddComponent<BoxCollider>();
                col.size = new Vector3(0.6f, 1.8f, 0.6f);
                col.center = new Vector3(0f, 0.9f, 0f);
            }
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.mass = 1f;
            if (go.GetComponent<GuardHeadUI>() == null)
                go.AddComponent<GuardHeadUI>();
        }

        private static void HideCeiling(GameObject room)
        {
            Transform ceiling = room.transform.Find("Ceiling");
            var renderer = ceiling != null ? ceiling.GetComponent<MeshRenderer>() : null;
            if (renderer != null) renderer.enabled = false;
        }

        /// <summary>내 소속 병사 3명 — 남쪽 로비에 대기(자동 공격 명령 없음). 플레이어가 드래그 선택 후
        /// 영주에게 우클릭 명령(RTSCommandSystem → SetExplicitAttackTarget, IDamageable)으로 지시한다.
        /// 실내 이동(IndoorMoveExempt)을 허용해 명령 이동이 실내에서 작동.</summary>
        private void SpawnMyInteractionSquad(GameObject room)
        {
            Vector3 basePos = room.transform.position;
            float floorY = basePos.y;
            // 남쪽 로비(EntryHall 중앙) 대기 위치.
            Vector3 startCenter = new Vector3(basePos.x, floorY, basePos.z - 8f);
            Color myColor = new Color(0.25f, 0.5f, 0.9f);

            for (int i = 0; i < 3; i++)
            {
                Vector3 pos = new Vector3(startCenter.x - 2.4f + i * 2.4f, floorY, startCenter.z);
                SpawnMySoldier($"LordTestMySoldier_{i + 1}", pos, $"내 상호작용 병사 {i + 1}", 8, myColor);
            }
            Debug.Log($"[LordInteractionTest] ⚔️ 내 병사 스폰 — 대기 (영주 상호작용 수동 명령용) @ {startCenter}");
        }

        private void SpawnMySoldier(string goName, Vector3 pos, string guardName, int level, Color color)
        {
            try
            {
                var go = TerritoryBuilder.SpawnDeployedGuard(goName, level, MyNation, new Vector3(pos.x, pos.z));
                if (go == null) return;
                go.transform.position = new Vector3(pos.x, 0f, pos.z);

                EnsureCombatCollider(go);
                GuardPlaceholder ph = go.GetComponent<GuardPlaceholder>();
                if (ph == null) ph = go.AddComponent<GuardPlaceholder>();
                ph.SetGuardInfo(guardName, level, MyNation);
                ph.SetRecruited(true);
                ph.HostileToPlayerFaction = false;  // 내 병사 — 플레이어 진영
                ph.IndoorMoveExempt = true;         // 실내 이동 게이트 예외
                ColorBody(go, color);
                go.tag = "RecruitedSoldier";
                Debug.Log($"[LordInteractionTest] 내 병사 스폰 — {goName} (Lv.{level}, 대기 — 영주 상호작용 수동 명령용)");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LordInteractionTest] 내 병사 배치 실패({goName}): {ex.Message}");
            }
        }

        private static void ColorBody(GameObject go, Color color)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                if (r != null && (r is SkinnedMeshRenderer || r is MeshRenderer))
                    r.material.color = color;
        }

        private static void EnsureGuardSystems()
        {
            if (GuardManager.Instance == null)
            {
                var gmGO = new GameObject("GuardManager");
                gmGO.AddComponent<GuardManager>();
            }
            GuardTaskSystem.Ensure();
        }

        /// <summary>[Test_17] 기존 RTS 마우스 조작 시스템 보장 — 있으면 재사용, 없을 때만 생성.
        /// 생성 시 전용 GO + DontDestroyOnLoad (TestTerritoryCombatSetup 선례). 파괴/교체는 하지 않는다.</summary>
        private static void EnsureRtsControls()
        {
            if (Object.FindAnyObjectByType<GuardSelectionManager>(FindObjectsInactive.Include) == null)
            {
                var gsmGO = new GameObject("GuardSelectionManager");
                DontDestroyOnLoad(gsmGO);
                gsmGO.AddComponent<GuardSelectionManager>();
            }
            if (Object.FindAnyObjectByType<RTSCommandSystem>(FindObjectsInactive.Include) == null)
            {
                var rtsGO = new GameObject("RTSCommandSystem");
                DontDestroyOnLoad(rtsGO);
                rtsGO.AddComponent<RTSCommandSystem>();
            }
        }

        private static void EnsureGameManager()
        {
            GameObject gm = GameManager.Instance != null ? GameManager.Instance.gameObject : new GameObject("GameManager");
            if (GameManager.Instance == null)
                gm.AddComponent<GameManager>();
            if (Object.FindAnyObjectByType<GuardHostilitySystem>(FindObjectsInactive.Include) == null)
                gm.AddComponent<GuardHostilitySystem>();
            if (Object.FindAnyObjectByType<GuardVisualAttachSystem>(FindObjectsInactive.Include) == null)
                gm.AddComponent<GuardVisualAttachSystem>();
        }

        private static void EnsurePlayer()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                player = new GameObject("Player") { tag = "Player" };
                player.transform.position = new Vector3(0f, 0f, -12f);
            }
            if (player.GetComponent<CharacterController>() == null)
            {
                var cc = player.AddComponent<CharacterController>();
                cc.height = 1.8f; cc.radius = 0.35f; cc.center = new Vector3(0f, 0.9f, 0f); cc.stepOffset = 0.3f;
            }
            // [이동통일 B1] 단순 WASD 전용 컨트롤러 폐기 — Test_10 계약 PlayerMovement로 통일
            //   (위 CharacterController 계수 유지 + PM RequireComponent 자동 부착).
            if (player.GetComponent<PlayerMovement>() == null)
                player.AddComponent<PlayerMovement>();
            // [이동통일 B3] 구 컨트롤러 발지 클램프 승계 — 성 외부 이탈 방지(벽 콜라이더 부재 씬).
            if (player.GetComponent<IndoorPlayBoundsClamp>() == null)
                player.AddComponent<IndoorPlayBoundsClamp>();
            if (PlayerInventory.Instance == null && player.GetComponent<PlayerInventory>() == null)
                player.AddComponent<PlayerInventory>();
            EnsurePlayerVisual(player);
        }

        // [Test_17] 플레이어 가시화 — Test_16과 동일 비주얼(Player_Rigged_Heat, 1.8m 정규화),
        // FBX 부재 시 캡슐 폴백. 멱등: 보이는 Renderer가 이미 있으면 아무것도 하지 않아 중복 생성하지 않는다.
        private static void EnsurePlayerVisual(GameObject player)
        {
            var renderers = player.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r != null && r.enabled && r.gameObject.activeInHierarchy)
                    return;   // 이미 가시 렌더러 존재
            }

            // 이전에 만들어 두었지만 비활성인 비주얼이 있으면 재사용(중복 생성 방지).
            Transform existing = player.transform.Find("PlayerModel") ?? player.transform.Find("PlayerTestCapsule");
            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                foreach (var r in existing.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
                return;
            }

            GameObject visual;
            var playerFbx = Resources.Load<GameObject>("Models/UserProvided/fbx/Player_Rigged_Heat");
            if (playerFbx != null)
            {
                visual = Object.Instantiate(playerFbx);
                visual.name = "PlayerModel";
                visual.transform.SetParent(player.transform, false);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;
                TerritoryBuilder.NormalizeSoldierHeight(visual, TerritoryBuilder.SoldierTargetHeight, skinnedLocalBounds: false);
                if (visual.GetComponent<Animator>() == null)
                    visual.AddComponent<Animator>();
            }
            else
            {
                visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visual.name = "PlayerTestCapsule";
                visual.transform.SetParent(player.transform, false);
                visual.transform.localPosition = new Vector3(0f, 0.45f, 0f);
                visual.transform.localScale = new Vector3(0.35f, 0.5f, 0.35f);
                Collider visualCollider = visual.GetComponent<Collider>();
                if (visualCollider != null)
                    Object.Destroy(visualCollider);
            }
        }

        private static void EnsureCamera()
        {
            GameObject camGO = GameObject.FindGameObjectWithTag("MainCamera");
            if (camGO == null)
            {
                camGO = new GameObject("Main Camera");
                camGO.tag = "MainCamera";
            }
            Camera cam = camGO.GetComponent<Camera>();
            if (cam == null) cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            // [카메라통일] 직교 폐기 → Test_10식 원근(PM 오빗 57° 단일 기록자 계약).
            cam.orthographic = false;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 500f;   // farClip 500 계약 통일(Test_10 선례 — 기존 120 상향)
            // [카메라통일] 잔존 IndoorCameraFollow 제거 — PM 원근 단일 기록자(투어 DDOL 카메라 재사용 대비).
            var staleFollow = camGO.GetComponent<IndoorCameraFollow>();
            if (staleFollow != null) Object.Destroy(staleFollow);
            if (camGO.GetComponent<AudioListener>() == null && Object.FindAnyObjectByType<AudioListener>() == null)
                camGO.AddComponent<AudioListener>();
        }
    }
}
