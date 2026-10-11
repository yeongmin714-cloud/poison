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
                if (player.GetComponent<PlayerCastleInteriorTestMovement>() == null)
                    player.AddComponent<PlayerCastleInteriorTestMovement>();
                if (PlayerInventory.Instance == null && player.GetComponent<PlayerInventory>() == null)
                    player.AddComponent<PlayerInventory>();
                return player;
            }

            player = new GameObject("PlayerCastleTestPlayer") { tag = "Player" };
            player.transform.position = new Vector3(0f, 0f, -3f);
            ConfigureController(player.AddComponent<CharacterController>());

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "PlayerTestCapsule";
            visual.transform.SetParent(player.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            visual.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            Collider visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
                Destroy(visualCollider);

            player.AddComponent<PlayerCastleInteriorTestMovement>();
            if (PlayerInventory.Instance == null)
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
            camera.orthographic = true;
            camera.orthographicSize = 12f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;

            if (cameraObject.GetComponent<IndoorCameraFollow>() == null)
                cameraObject.AddComponent<IndoorCameraFollow>();
            if (cameraObject.GetComponent<AudioListener>() == null && Object.FindAnyObjectByType<AudioListener>() == null)
                cameraObject.AddComponent<AudioListener>();
        }
    }
}
