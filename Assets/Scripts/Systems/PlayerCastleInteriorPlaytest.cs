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
            EnsureWarehouseSystem();
            EnsureTestPlayer();
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
            }

            EnsureCamera();
            Debug.Log("[PlayerCastleInteriorPlaytest] Empire player-castle interior ready. WASD/arrow keys move.");
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
