using ProjectName.Core;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>48x36 castle blueprint with a central octagonal lobby and five accessible rooms.</summary>
    public static class CastleInteriorBuilder
    {
        public static GameObject BuildCastleInterior(string nationStyle)
        {
            return BuildCastleInterior(nationStyle, 0);
        }

        public static GameObject BuildCastleInterior(string nationStyle, int layoutVariant)
        {
            layoutVariant = ((layoutVariant % 8) + 8) % 8;
            const float width = 48f, height = 6f, depth = 36f;

            Texture2D floorTex;
            Texture2D wallTex;
            switch (nationStyle?.ToLower())
            {
                case "eastern":
                    floorTex = IndoorTextureGenerator.GenerateCastleFloorEastern();
                    wallTex = IndoorTextureGenerator.GenerateCastleWallEastern();
                    break;
                case "western":
                    floorTex = IndoorTextureGenerator.GenerateCastleFloorWestern();
                    wallTex = IndoorTextureGenerator.GenerateCastleWallWestern();
                    break;
                case "southern":
                    floorTex = IndoorTextureGenerator.GenerateCastleFloorSouthern();
                    wallTex = IndoorTextureGenerator.GenerateCastleWallSouthern();
                    break;
                case "northern":
                    floorTex = IndoorTextureGenerator.GenerateCastleFloorNorthern();
                    wallTex = IndoorTextureGenerator.GenerateCastleWallNorthern();
                    break;
                case "empire":
                    floorTex = IndoorTextureGenerator.GenerateCastleFloorEmpire();
                    wallTex = IndoorTextureGenerator.GenerateCastleWallEmpire();
                    break;
                default:
                    Debug.LogWarning($"[CastleInteriorBuilder] Unknown nation style '{nationStyle}'; using Empire.");
                    floorTex = IndoorTextureGenerator.GenerateCastleFloorEmpire();
                    wallTex = IndoorTextureGenerator.GenerateCastleWallEmpire();
                    break;
            }

            Texture2D ceilingTex = IndoorTextureGenerator.GeneratePlasterWall(256, 256,
                new Color(0.60f, 0.55f, 0.50f));
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogError("[CastleInteriorBuilder] No supported shader found.");
                return new GameObject("Room_Fallback");
            }

            Material floorMat = new Material(shader) { name = $"Castle_FloorMat_{nationStyle}", mainTexture = floorTex };
            Material wallMat = new Material(shader) { name = $"Castle_WallMat_{nationStyle}", mainTexture = wallTex };
            Material ceilingMat = new Material(shader) { name = "Castle_CeilingMat", mainTexture = ceilingTex };
            Material woodMat = new Material(shader) { name = "Castle_WoodMat", color = new Color(0.42f, 0.29f, 0.17f) };
            Material stoneMat = new Material(shader) { name = "Castle_StoneMat", color = new Color(0.38f, 0.36f, 0.33f) };

            GameObject room = IndoorBuilder.CreateRoom(width, height, depth, floorMat, wallMat, ceilingMat);
            if (room == null)
            {
                Debug.LogError("[CastleInteriorBuilder] IndoorBuilder.CreateRoom returned null.");
                return new GameObject("Room_Fallback");
            }

            // The room keeps its original name. Replace the south wall with two spans and a 6m entry.
            Transform backWall = room.transform.Find("Wall_Back");
            if (backWall != null) Object.Destroy(backWall.gameObject);
            CreatePartition(room, "SouthEntry_Left", 21f, height, 0.4f,
                new Vector3(-13.5f, 0f, -18f), 0f, 0f, 0f, wallMat);
            CreatePartition(room, "SouthEntry_Right", 21f, height, 0.4f,
                new Vector3(13.5f, 0f, -18f), 0f, 0f, 0f, wallMat);

            // The south Lobby_South opening is the entrance vestibule, not a sixth room.
            // Bound its 6m-wide route from the lobby to the matching exterior opening.
            CreateLineWall(room, "SouthVestibule_Left", new Vector3(-3f, 0f, -7f),
                new Vector3(-3f, 0f, -18f), height, wallMat, 0f);
            CreateLineWall(room, "SouthVestibule_Right", new Vector3(3f, 0f, -7f),
                new Vector3(3f, 0f, -18f), height, wallMat, 0f);

            // Octagonal lobby perimeter: vertices at (+/-3,+/-7) and (+/-7,+/-3).
            // Five intentional openings face bedroom, craft, storage, barracks and alchemy.
            CreateLineWall(room, "Lobby_North", new Vector3(-3f, 0f, 7f), new Vector3(3f, 0f, 7f), height, wallMat, 2.4f);
            CreateLineWall(room, "Lobby_NW", new Vector3(-7f, 0f, 3f), new Vector3(-3f, 0f, 7f), height, wallMat, 2.0f);
            CreateLineWall(room, "Lobby_NE", new Vector3(3f, 0f, 7f), new Vector3(7f, 0f, 3f), height, wallMat, 2.0f);
            CreateLineWall(room, "Lobby_SW", new Vector3(-7f, 0f, -3f), new Vector3(-3f, 0f, -7f), height, wallMat, 2.0f);
            CreateLineWall(room, "Lobby_SE", new Vector3(3f, 0f, -7f), new Vector3(7f, 0f, -3f), height, wallMat, 2.0f);
            CreateLineWall(room, "Lobby_West_Upper", new Vector3(-7f, 0f, 3f), new Vector3(-7f, 0f, -3f), height, wallMat, 0f);
            CreateLineWall(room, "Lobby_East_Upper", new Vector3(7f, 0f, 3f), new Vector3(7f, 0f, -3f), height, wallMat, 0f);

            // Room dividers extend from the lobby to the outer boundary. The north bedroom
            // occupies the narrow central bay; its straight walls replace the crossing diagonals.
            CreateLineWall(room, "Bedroom_WestBoundary", new Vector3(-3f, 0f, 7f), new Vector3(-3f, 0f, 18f), height, wallMat, 0f);
            CreateLineWall(room, "Bedroom_EastBoundary", new Vector3(3f, 0f, 7f), new Vector3(3f, 0f, 18f), height, wallMat, 0f);
            CreateLineWall(room, "WestRoomSeparator", new Vector3(-7f, 0f, 0f), new Vector3(-24f, 0f, 0f), height, wallMat, 0f);
            CreateLineWall(room, "EastRoomSeparator", new Vector3(7f, 0f, 0f), new Vector3(24f, 0f, 0f), height, wallMat, 0f);
            // South vestibule sidewalls also form the inner boundaries of the south rooms.

            // Ancillary locked pockets. Each partition and door is wholly inside its room, away
            // from the open lobby links. The four historical lock contracts are unchanged.
            CreatePartition(room, "OfficePocketWall", 4f, height, 0.3f,
                new Vector3(-5f, 0f, 13f), 0f, 0f, 1.2f, wallMat);
            CreateDoor(room, "LordOfficeDoor_Locked", "🚪 영주 집무실 (잠김)", "castle_lord_office",
                LockpickingSystem.LockDifficulty.VeryHard, new Vector3(-5f, 1.25f, 13f), 0f, woodMat);
            CreateLineWall(room, "OfficePocketReturn_West", new Vector3(-7f, 0f, 13f),
                new Vector3(-7f, 0f, 17f), height, wallMat, 0f);
            CreateLineWall(room, "OfficePocketBack", new Vector3(-7f, 0f, 17f),
                new Vector3(-3f, 0f, 17f), height, wallMat, 0f);
            CreatePartition(room, "VaultPocketWall", 4f, height, 0.3f,
                new Vector3(5f, 0f, 13f), 0f, 0f, 1.2f, wallMat);
            CreateDoor(room, "VaultDoor_Locked", "💰 금고실 (전설 잠김)", "castle_vault",
                LockpickingSystem.LockDifficulty.Legendary, new Vector3(5f, 1.25f, 13f), 0f, stoneMat);
            CreateLineWall(room, "VaultPocketReturn_East", new Vector3(7f, 0f, 13f),
                new Vector3(7f, 0f, 17f), height, wallMat, 0f);
            CreateLineWall(room, "VaultPocketBack", new Vector3(3f, 0f, 17f),
                new Vector3(7f, 0f, 17f), height, wallMat, 0f);
            CreatePartition(room, "ArmoryPocketWall", 4f, height, 0.3f,
                new Vector3(-19f, 0f, -13f), 0f, 0f, 1.2f, wallMat);
            CreateDoor(room, "ArmoryDoor_Locked", "⚔️ 무기고 (잠김)", "castle_armory",
                LockpickingSystem.LockDifficulty.Hard, new Vector3(-19f, 1.25f, -13f), 0f, stoneMat);
            CreateLineWall(room, "ArmoryPocketReturn_West", new Vector3(-21f, 0f, -13f),
                new Vector3(-21f, 0f, -17f), height, wallMat, 0f);
            CreateLineWall(room, "ArmoryPocketReturn_East", new Vector3(-17f, 0f, -13f),
                new Vector3(-17f, 0f, -17f), height, wallMat, 0f);
            CreateLineWall(room, "ArmoryPocketBack", new Vector3(-21f, 0f, -17f),
                new Vector3(-17f, 0f, -17f), height, wallMat, 0f);
            CreatePartition(room, "ArchivePocketWall", 4f, height, 0.3f,
                new Vector3(19f, 0f, -13f), 0f, 0f, 1.2f, wallMat);
            CreateDoor(room, "ArchiveDoor_Locked", "📜 문서고 (잠김)", "castle_archive",
                LockpickingSystem.LockDifficulty.Easy, new Vector3(19f, 1.25f, -13f), 0f, woodMat);
            CreateLineWall(room, "ArchivePocketReturn_West", new Vector3(17f, 0f, -13f),
                new Vector3(17f, 0f, -17f), height, wallMat, 0f);
            CreateLineWall(room, "ArchivePocketReturn_East", new Vector3(21f, 0f, -13f),
                new Vector3(21f, 0f, -17f), height, wallMat, 0f);
            CreateLineWall(room, "ArchivePocketBack", new Vector3(17f, 0f, -17f),
                new Vector3(21f, 0f, -17f), height, wallMat, 0f);

            // Sparse catalog furnishings stay within their named rooms and away from the lobby.
            PlaceFurniture(IndoorFurnitureCatalog.CreateBed(1.2f, 2.0f, woodMat), room, "BedroomBed", new Vector3(0f, 0f, 12f));
            PlaceFurniture(IndoorFurnitureCatalog.CreateTable(1.4f, 1.0f, 0.8f, woodMat), room, "BedroomTable", new Vector3(-1.8f, 0f, 10f));
            PlaceFurniture(IndoorFurnitureCatalog.CreateChair(0.9f, woodMat), room, "BedroomChair", new Vector3(1.8f, 0f, 10f));
            PlaceFurniture(IndoorFurnitureCatalog.CreateShelf(1.5f, 2.0f, 0.5f, woodMat, 3), room, "BedroomShelf", new Vector3(0f, 0f, 16f));

            PlaceFurniture(IndoorFurnitureCatalog.CreateTable(1.8f, 1.2f, 0.9f, woodMat), room, "CraftTable", new Vector3(-21.5f, 0f, 8f));
            PlaceFurniture(IndoorFurnitureCatalog.CreateCounter(1.6f, 1.0f, 0.7f, woodMat), room, "CraftCounter", new Vector3(-21.5f, 0f, 12f));
            PlaceFurniture(IndoorFurnitureCatalog.CreateShelf(1.4f, 1.8f, 0.5f, woodMat, 3), room, "CraftShelf", new Vector3(-23f, 0f, 5f));

            PlaceFurniture(IndoorFurnitureCatalog.CreateShelf(1.6f, 2.0f, 0.6f, stoneMat, 4), room, "StorageShelf", new Vector3(22f, 0f, 8f));
            PlaceFurniture(IndoorFurnitureCatalog.CreateShelf(1.6f, 2.0f, 0.6f, stoneMat, 4), room, "StorageShelf_2", new Vector3(22f, 0f, 13f));
            PlaceFurniture(IndoorFurnitureCatalog.CreateCrate(0.9f, 0.7f, 0.9f, woodMat), room, "StorageCrate", new Vector3(22f, 0f, 4f));
            PlaceFurniture(IndoorFurnitureCatalog.CreateCrate(0.9f, 0.7f, 0.9f, woodMat), room, "StorageCrate_2", new Vector3(22f, 0f, 6f));
            PlaceFurniture(IndoorFurnitureCatalog.CreateCrate(0.9f, 0.7f, 0.9f, woodMat), room, "StorageCrate_3", new Vector3(18f, 0f, 15f));
            PlaceFurniture(IndoorFurnitureCatalog.CreateCrate(0.9f, 0.7f, 0.9f, woodMat), room, "StorageCrate_4", new Vector3(18f, 0f, 17f));

            // Barracks: two neat rows of four catalog beds plus a map table and seating.
            for (int row = 0; row < 2; row++)
            {
                float bedX = -10f - row * 2.5f;
                for (int index = 0; index < 4; index++)
                {
                    float bedZ = -5f - index * 1.8f;
                    PlaceFurniture(IndoorFurnitureCatalog.CreateBed(0.9f, 1.7f, woodMat), room,
                        $"BarracksBed_{row}_{index}", new Vector3(bedX, 0f, bedZ));
                }
            }
            PlaceFurniture(IndoorFurnitureCatalog.CreateTable(2.0f, 1.4f, 1.0f, woodMat), room,
                "BarracksMapTable", new Vector3(-19f, 0f, -5f));
            PlaceFurniture(IndoorFurnitureCatalog.CreateChair(0.9f, woodMat), room,
                "BarracksMapChair_1", new Vector3(-19f, 0f, -7f));
            PlaceFurniture(IndoorFurnitureCatalog.CreateChair(0.9f, woodMat), room,
                "BarracksMapChair_2", new Vector3(-17f, 0f, -5f));
            PlaceFurniture(IndoorFurnitureCatalog.CreateShelf(1.6f, 1.8f, 0.5f, stoneMat, 3), room,
                "BarracksWeaponRack", new Vector3(-22f, 0f, -5f));

            PlaceFurniture(IndoorFurnitureCatalog.CreateCounter(1.6f, 1.0f, 0.8f, woodMat), room,
                "AlchemyCounter", new Vector3(20f, 0f, -11f));
            PlaceFurniture(IndoorFurnitureCatalog.CreateTable(1.8f, 1.2f, 0.9f, woodMat), room,
                "AlchemyTable", new Vector3(18f, 0f, -5f));
            PlaceFurniture(IndoorFurnitureCatalog.CreateShelf(1.5f, 1.8f, 0.5f, woodMat, 3), room,
                "AlchemyShelf", new Vector3(21f, 0f, -5f));
            PlaceFurniture(IndoorFurnitureCatalog.CreateChair(0.9f, woodMat), room,
                "AlchemyChair", new Vector3(18f, 0f, -7f));
            PlaceFurniture(IndoorFurnitureCatalog.CreateChair(0.9f, woodMat), room,
                "CraftChair", new Vector3(-20f, 0f, 7f));
            PlaceFurniture(IndoorFurnitureCatalog.CreateChair(0.9f, woodMat), room,
                "StorageChair", new Vector3(17.5f, 0f, 10f));


            // Layout variants deterministically rotate a small subset of furniture without
            // changing room connectivity, lock locations, or the five-room floor plan.
            if ((layoutVariant & 1) != 0)
                room.transform.Find("BedroomTable")?.Rotate(0f, 90f, 0f, Space.Self);

            Color ambient = new Color(0.08f, 0.07f, 0.06f);
            // 주변광 대폭 축소 — 불빛(포인트라이트) 중심의 은은한 실내 무드.
            IndoorLighting.SetupIndoorLighting(room, ambient, 0.22f, false);
            IndoorLighting.AddPointLight(room, new Vector3((layoutVariant - 3.5f) * 0.2f, height - 0.5f, 0f),
                new Color(1f, 0.9f, 0.7f), 12f, 1.0f);
            IndoorLighting.AddPointLight(room, new Vector3(0f, height - 0.5f, 11f),
                new Color(1f, 0.85f, 0.6f), 8f, 0.8f);

            Debug.Log($"[CastleInteriorBuilder] Castle interior built (style: {nationStyle}, variant: {layoutVariant}).");
            return room;
        }

        private static void CreateLineWall(GameObject parent, string name, Vector3 a, Vector3 b,
            float height, Material material, float doorwayWidth)
        {
            float dx = b.x - a.x;
            float dz = b.z - a.z;
            float length = Mathf.Sqrt(dx * dx + dz * dz);
            float rotation = Mathf.Atan2(-dz, dx) * Mathf.Rad2Deg;
            Vector3 center = (a + b) * 0.5f;
            CreatePartition(parent, name, length, height, 0.35f, center,
                rotation, 0f, doorwayWidth, material);
        }

        private static void CreatePartition(GameObject parent, string name, float length, float height,
            float thickness, Vector3 center, float rotationY, float doorwayCenter, float doorwayWidth,
            Material material)
        {
            IndoorBuilder.CreateInteriorWall(parent, name, length, height, thickness, center,
                rotationY, doorwayCenter, doorwayWidth, material);
            // IndoorBuilder uses a floor-centered convention for position but offsets its wall
            // root upward too. Reset that root so its child quads span y=0..height.
            Transform wall = parent.transform.Find(name);
            if (wall != null)
            {
                Vector3 position = wall.localPosition;
                position.y = 0f;
                wall.localPosition = position;
            }
        }

        private static void CreateDoor(GameObject parent, string objectName, string label, string locationId,
            LockpickingSystem.LockDifficulty difficulty, Vector3 position, float rotationY, Material material)
        {
            GameObject door = new GameObject(objectName);
            door.transform.SetParent(parent.transform);
            door.transform.localPosition = position;
            door.transform.localRotation = Quaternion.Euler(0f, rotationY, 0f);
            door.transform.localScale = new Vector3(1.2f, 2.5f, 0.2f);
            MeshFilter filter = door.AddComponent<MeshFilter>();
            filter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            MeshRenderer renderer = door.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            LockedDoor lockedDoor = door.AddComponent<LockedDoor>();
            lockedDoor.LocationId = locationId;
            lockedDoor.Difficulty = difficulty;
            // LockedDoor handles the lock interaction/state only; it has no collider or
            // physical open behavior. Block passage while locked, then clear it on unlock.
            BoxCollider doorCollider = door.AddComponent<BoxCollider>();
            doorCollider.size = new Vector3(1f, 2.4f, 1f);
            doorCollider.center = new Vector3(0f, 0.7f, 0f);
            door.AddComponent<LockedDoorCollision>();
            NameplateDisplay nameplate = door.AddComponent<NameplateDisplay>();
            nameplate.DisplayName = label;
        }

        private static void PlaceFurniture(GameObject furniture, GameObject parent, string name, Vector3 position)
        {
            if (furniture == null) return;
            furniture.name = name;
            furniture.transform.SetParent(parent.transform);
            furniture.transform.localPosition = position;
        }
    }

    /// <summary>Synchronizes a generated lock door's solid collider with its lock state.</summary>
    public sealed class LockedDoorCollision : MonoBehaviour
    {
        private LockedDoor _lockedDoor;
        private BoxCollider _collider;

        private void Awake()
        {
            _lockedDoor = GetComponent<LockedDoor>();
            _collider = GetComponent<BoxCollider>();
            UpdateCollider();
        }

        private void Update()
        {
            UpdateCollider();
        }

        private void UpdateCollider()
        {
            if (_lockedDoor != null && _collider != null)
                _collider.enabled = _lockedDoor.IsLocked;
        }
    }
}
