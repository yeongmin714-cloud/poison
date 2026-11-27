using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// Test bootstrap for the dedicated player-owned castle interior playtest scene.
    /// </summary>
    public sealed class PlayerCastleInteriorPlaytest : MonoBehaviour
    {
        public const string TestSceneName = "Test_PlayerCastleInterior";
        private const float RoomWidth = PlayerCastleInteriorBuilder.RoomWidth;
        private const float RoomHeight = PlayerCastleInteriorBuilder.RoomHeight;
        private const float RoomDepth = PlayerCastleInteriorBuilder.RoomDepth;

        private void Awake()
        {
            if (gameObject.scene.name != TestSceneName)
                return;

            BuildPlayableTestInterior();
        }

        /// <summary>[44차] 전용 씬 언로드 시 실내 플래그 복구 — MainScene 복귀 후 잔존 방지.</summary>
        private void OnDestroy()
        {
            if (gameObject.scene.name == TestSceneName)
                UITransitionState.IndoorActive = false;
        }

        private static void EnsureWarehouseSystem()
        {
            if (WarehouseSystem.Instance != null) return;

            var warehouseObject = new GameObject("PlayerCastlePlaytestWarehouseSystem");
            warehouseObject.AddComponent<WarehouseSystem>();
        }

        private static void BuildPlayableTestInterior()
        {
            // [2026-10-10 P1] 씬 직사광 소등 — IndoorScene 직사광 0(7차 소등)과 정합.
            //   본 씬의 PlaytestDirectionalLight(1.15+Soft)가 천장 비활성화로 실내를 직사해
            //   성내부 암전 무드/디더링 계약을 무력화했다. 런타임 소등 — 씬 복제에도 유지.
            NeutralizeSceneSunlights();
            // [44차] 전용 씬도 실내 상태로 진입 — BedRestEnforcer/자가수리 스윕/P14 이동 게이트 등
            //   IndoorActive 소비 시스템 전반이 실내 계약으로 작동한다(플래그 미세팅으로
            //   강제자/스윕이 잠자 병사가 서 있던 원인 차단). OnDestroy에서 복구.
            UITransitionState.IndoorActive = true;
            EnsureWarehouseSystem();
            EnsureTestPlayer();
            EnsureGuardSystems();   // [38차] 병사배치 UI 실증 — GuardManager/GuardTaskSystem 보장
            GameObject room = PlayerCastleInteriorBuilder.BuildPlayerCastleInterior("Empire", 0);
            if (room == null)
            {
                Debug.LogError("[PlayerCastleInteriorPlaytest] PlayerCastleInteriorBuilder returned null.");
            }
            else
            {
                // Resources/Indoor/floor_flagstone; dimensions match builder room.
                IndoorMaterialFactory.ApplyToRoom(room, RoomWidth, RoomHeight, RoomDepth);

                Transform ceiling = room.transform.Find("Ceiling");
                if (ceiling == null)
                {
                    Debug.LogWarning("[PlayerCastleInteriorPlaytest] Room has no Ceiling transform to hide.");
                }
                else
                {
                    MeshRenderer ceilingRenderer = ceiling.GetComponent<MeshRenderer>();
                    if (ceilingRenderer == null)
                        Debug.LogWarning("[PlayerCastleInteriorPlaytest] Room Ceiling has no MeshRenderer to disable.");
                    else
                        ceilingRenderer.enabled = false;
                }

                Transform floor = room.transform.Find("Floor");
                MeshRenderer floorRenderer = floor != null ? floor.GetComponent<MeshRenderer>() : null;
                Material floorMaterial = floorRenderer != null ? floorRenderer.sharedMaterial : null;
                bool applied = floorMaterial != null && floorMaterial.mainTexture == IndoorTextureLoader.Floor;
                Debug.Log($"[PlayerCastleInteriorPlaytest] HQ floor loaded={IndoorTextureLoader.Floor != null}, applied={applied}, " +
                          $"UV tiling={(floorMaterial != null ? floorMaterial.mainTextureScale.ToString() : "n/a")}");

                // [35차] 병사 배치 실증 — 배럭 지휘탁자 주변 아군 3명. 실게임 경로
                //   (IndoorSceneTransition → TerritoryBuilder.SpawnInteriorAlliedGuards)와 동일 위임이라
                //   CreateGuard/GuardPlaceholder/SoldierIdlePose 체인이 실내에서 작동하는지 그대로 검증된다.
                TerritoryBuilder.SpawnInteriorAlliedGuards(room, "Empire", 3);
            }

            EnsureCamera();
            Debug.Log("[PlayerCastleInteriorPlaytest] Empire player-castle interior ready. WASD/arrow keys move.");
        }

        /// <summary>[38차] 병사배치 UI 실증 전제 — GuardManager(씬 싱글턴)+GuardTaskSystem 보장.
        ///   TestTerritorySetup 선례(TestAllInOneSetup:272)와 동일 수동 생성.</summary>
        private static void EnsureGuardSystems()
        {
            if (GuardManager.Instance == null)
            {
                var gmGO = new GameObject("GuardManager");
                gmGO.AddComponent<GuardManager>();
                Debug.Log("[PlayerCastleInteriorPlaytest] GuardManager 생성(실내 테스트)");
            }
            GuardTaskSystem.Ensure();
        }

        /// <summary>
        /// [2026-10-10 P1] 씬 내 모든 Directional Light 소등(intensity 0 + 섀도우 None).
        /// IndoorScene의 7차 직사광 소등 계약과 동일 — 천장이 없는 플레이테스트 씬에서
        /// 태양이 실내를 직사해 암전 무드와 그림자 디더링 계약을 무력화하는 것을 방지.
        /// 런타임 코드 기반이므로 씬을 복제/재생성해도 유지된다.
        /// </summary>
        public static void NeutralizeSceneSunlights()
        {
#if UNITY_2022_2_OR_NEWER
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
#else
            var lights = Object.FindObjectsOfType<Light>();
#endif
            int count = 0;
            foreach (var light in lights)
            {
                if (light == null || light.type != LightType.Directional) continue;
                light.intensity = 0f;
                light.shadows = LightShadows.None;
                count++;
            }
            if (count > 0)
                Debug.Log($"[PlayerCastleInteriorPlaytest] 직사광 소등 완료 — Directional Light {count}개(intensity 0, 섀도우 None).");
        }

        private static GameObject EnsureTestPlayer()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                if (player.GetComponent<CharacterController>() == null)
                    ConfigureController(player.AddComponent<CharacterController>());
                // [이동통일 B1] 단순 WASD 전용 컨트롤러 폐기 — Test_10 계약 PlayerMovement로 통일
                //   (점프/구르기/더블탭 달리기/커서 조준/휠 줌/E·F 상호작용/넉백 구독 승계).
                if (player.GetComponent<PlayerMovement>() == null)
                    player.AddComponent<PlayerMovement>();
                // [이동통일 B3] 구 컨트롤러 발지 클램프 승계 — 성 외부 이탈 방지(벽 콜라이더 부재 씬).
                if (player.GetComponent<IndoorPlayBoundsClamp>() == null)
                    player.AddComponent<IndoorPlayBoundsClamp>();
                if (PlayerInventory.Instance == null && player.GetComponent<PlayerInventory>() == null)
                    player.AddComponent<PlayerInventory>();
                return player;
            }

            player = new GameObject("PlayerCastleTestPlayer") { tag = "Player" };
            player.transform.position = new Vector3(0f, 0f, -3f);
            ConfigureController(player.AddComponent<CharacterController>());

            // [51차 사용자 확정 — 키 1.8m 전 씬 통일] 이 씬 플레이어 비주얼 = 휴머노이드 FBX(Player_Rigged_Heat —
            //   Humanoid avatar 보유로 애니도 작동)를 **SoldierTargetHeight(1.8m)로 정규화** →
            //   병사-플레이어 키 완전 동일(1.8m). 캡슐은 폴백(FBX 부재 시 — 본래 ~1.8m라 무변경).
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
                TerritoryBuilder.NormalizeSoldierHeight(visual, TerritoryBuilder.SoldierTargetHeight, skinnedLocalBounds: false);   // [54차] 플레이어 모델 = 월드 bounds 측정
                // 플레이어 무기/장비 시스템과 동일하게 필요한 애니 부트 — TestPlayerAnimatorBoot가 있으면 위임, 없으면 최소 구성.
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
                    Destroy(visualCollider);
            }

            // [이동통일 B1] PlayerMovement — RequireComponent(CharacterController) 자동 부착,
            //   위 ConfigureController 계수(height 1.8/radius 0.35/center 0.9/stepOffset 0.3/skin 0.08) 유지 이식.
            player.AddComponent<PlayerMovement>();
            if (PlayerInventory.Instance == null)
            // [이동통일 B3] 구 컨트롤러 발지 클램프 승계 — 성 외부 이탈 방지(벽 콜라이더 부재 씬).
            if (player.GetComponent<IndoorPlayBoundsClamp>() == null)
                player.AddComponent<IndoorPlayBoundsClamp>();
                player.AddComponent<PlayerInventory>();
            return player;
        }

        private static void ConfigureController(CharacterController controller)
        {
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.3f;
            controller.skinWidth = 0.08f;
        }

        private static void EnsureCamera()
        {
            GameObject cameraObject = GameObject.FindGameObjectWithTag("MainCamera");
            if (cameraObject == null)
            {
                cameraObject = new GameObject("PlayerCastleTestCamera");
                cameraObject.tag = "MainCamera";
            }
            else
            {
                cameraObject.tag = "MainCamera";
            }

            Camera camera = cameraObject.GetComponent<Camera>();
            if (camera == null)
                camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            // [카메라통일] 직교 폐기 → Test_10식 원근(PM 오빗 57° 단일 기록자 계약).
            camera.orthographic = false;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 500f;   // farClip 500 계약 통일(Test_10 선례 — 기존 100 상향)

            // [카메라통일] 잔존 IndoorCameraFollow 제거 — PM 원근 단일 기록자(투어 DDOL 카메라 재사용 대비).
            var staleFollow = cameraObject.GetComponent<IndoorCameraFollow>();
            if (staleFollow != null) Destroy(staleFollow);
            if (cameraObject.GetComponent<AudioListener>() == null && Object.FindAnyObjectByType<AudioListener>() == null)
                cameraObject.AddComponent<AudioListener>();
        }
    }
}
