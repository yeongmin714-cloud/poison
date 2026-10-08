using System.Collections.Generic;
using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 플레이어 소유 영지의 성 내부 절차 생성 (C11 계열 확장).
    /// 기존 CastleInteriorBuilder(타 영주용, 잠긴 문 다수)와 달리
    /// 플레이어가 점령한 "내 영지"이므로 중세 판타지 성 대전당으로 만든다:
    ///   - 지휘 책상 + 관리용 책상/문서 (집무 공간)
    ///   - 저장고 (선반/상자), 무기고 (무기 스탠드), 작업대
    ///   - 중세 석재 기둥 2열 + 따뜻한 주황빛 조명 (문 진입로에 물리 화로 없음)
    ///   - 문장 방패/붉은 러그 장식, 플레이어 환영 배너 (유지)
    ///   - 잠금문 없음 (이미 내 것이므로 접근 가능)
    ///   - NameplateDisplay 기능 안내 라벨
    /// 국가별 텍스처/방 생성 틀은 CastleInteriorBuilder와 동일한 API 사용.
    /// </summary>
    public static class PlayerCastleInteriorBuilder
    {
        // Preserve the former 4:3 footprint ratio while providing six times its area.
        // All dimensions below are final room-local/world meters; the room root stays at unit scale.
        public const float ExistingRoomWidth = 48f;
        public const float ExistingRoomDepth = 36f;
        public const float RoomWidth = 117.6f;
        public const float RoomDepth = 88.2f;
        public const float RoomHeight = 6f;
        public const float WallThickness = 0.45f;
        public const float PortalWidth = 3.2f;
        public const float PortalHeight = 2.8f;
        private const float TopologyFurnitureScale = 2.45f;

        public readonly struct WallLayout
        {
            public readonly string Name;
            public readonly Vector2 Start;
            public readonly Vector2 End;
            public readonly float DoorwayOffset;
            public readonly float DoorwayWidth;
            public readonly float DoorwayHeight;
            public readonly string ConnectedFrom;
            public readonly string ConnectedTo;

            public WallLayout(string name, Vector2 start, Vector2 end, float doorwayWidth = 0f,
                float doorwayHeight = 0f, float doorwayOffset = 0f, string connectedFrom = null,
                string connectedTo = null)
            {
                Name = name; Start = start; End = end; DoorwayOffset = doorwayOffset;
                DoorwayWidth = doorwayWidth; DoorwayHeight = doorwayHeight;
                ConnectedFrom = connectedFrom; ConnectedTo = connectedTo;
            }
        }

        public readonly struct PortalConnection
        {
            public readonly string WallName;
            public readonly string From;
            public readonly string To;
            public PortalConnection(string wallName, string from, string to)
            { WallName = wallName; From = from; To = to; }
        }

        private static readonly string[] ZoneNames = { "Bedroom", "Craft", "Storage", "Barracks", "Alchemy" };
        private static readonly Dictionary<string, WarehouseSystem> SeededCookingPantries =
            new Dictionary<string, WarehouseSystem>();

        /// <summary>North is +Z. Vertices are final room-local XZ coordinates in meters.</summary>
        // The entry opening is on the south-east chamfer, aimed into a real walkable central corridor.
        public static Vector2[] GetLobbyVertices() => new[]
        {
            new Vector2(-18f, 14f), new Vector2(18f, 14f), new Vector2(30f, 8f), new Vector2(30f, -8f),
            new Vector2(18f, -14f), new Vector2(-18f, -14f), new Vector2(-30f, -8f), new Vector2(-30f, 8f)
        };

        private static bool IsInsideLobby(Vector2 point)
        {
            Vector2[] vertices = GetLobbyVertices();
            bool inside = false;
            for (int i = 0, j = vertices.Length - 1; i < vertices.Length; j = i++)
            {
                Vector2 a = vertices[i], b = vertices[j];
                bool crosses = (a.y > point.y) != (b.y > point.y) &&
                    point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x;
                if (crosses) inside = !inside;
            }
            return inside;
        }

        public static string[] GetTopologyZoneNames() => (string[])ZoneNames.Clone();
        public static Vector2 GetAnchorPosition(string anchorName)
        {
            switch (anchorName)
            {
                case "Workbench": return new Vector2(-40f, 25f);
                case "StorageShelf_2": return new Vector2(40f, 25f);
                case "WeaponStand_0": return new Vector2(-40f, -25f);
                case "AlchemyTable": return new Vector2(40f, -25f);
                case "LordBed": return new Vector2(0f, 29f);
                case "CookingTable": return new Vector2(40f, 34f);
                case "CookingIngredientWarehouse": return new Vector2(45.5f, 34f);
                default: return Vector2.zero;
            }
        }

        private static void SetAnchorPosition(GameObject room, string name, Vector2 position)
        {
            Transform anchor = room != null ? room.transform.Find(name) : null;
            if (anchor == null) return;
            Vector3 local = anchor.localPosition;
            local.x = position.x;
            local.z = position.y;
            anchor.localPosition = local;
        }

        public static PortalConnection[] GetPortalConnections()
        {
            var portals = new List<PortalConnection>();
            foreach (WallLayout wall in GetTopologyWalls())
                if (wall.DoorwayWidth > 0f && !string.IsNullOrEmpty(wall.ConnectedFrom) &&
                    !string.IsNullOrEmpty(wall.ConnectedTo))
                    portals.Add(new PortalConnection(wall.Name, wall.ConnectedFrom, wall.ConnectedTo));
            return portals.ToArray();
        }

        /// <summary>Returns the named room occupied by a final room-local XZ point, or null in the exterior.</summary>
        public static string GetTopologyZoneAt(Vector2 point)
        {
            if (Mathf.Abs(point.x) > RoomWidth * 0.5f || Mathf.Abs(point.y) > RoomDepth * 0.5f)
                return "Exterior";
            if (IsInsideLobby(point)) return "Lobby";
            if (Mathf.Abs(point.x) < 18f && point.y > 14f) return "Bedroom";
            if (point.x < -18f && point.y > 8f) return "Craft";
            if (point.x < -30f && point.y >= -8f && point.y <= 8f) return "Craft";
            if (point.x > 18f && point.y > 8f) return "Storage";
            if (point.x > 30f && point.y >= -8f && point.y <= 8f) return "Storage";
            if (point.x < -2.1f && point.y < -14f) return "Barracks";
            if (point.x > 2.1f && point.y < -14f) return "Alchemy";
            if (Mathf.Abs(point.x) <= 2.1f && point.y < -14f) return "EntryHall";
            return null;
        }

        /// <summary>
        /// Named, floor-level XZ wall spans for the connected room ring around the central octagonal lobby.
        /// A portal is a real cut in its named wall, and carries the two spaces it joins.
        /// </summary>
        public static WallLayout[] GetTopologyWalls()
        {
            float x = RoomWidth * 0.5f, z = RoomDepth * 0.5f;
            Vector2[] lobby = GetLobbyVertices();
            WallLayout S(string name, Vector2 a, Vector2 b, float doorwayWidth = 0f,
                float doorwayHeight = 0f, float offset = 0f, string from = null, string to = null) =>
                new WallLayout(name, a, b, doorwayWidth, doorwayHeight, offset, from, to);

            return new[]
            {
                // Solid outer shell; the south entry opens into the dedicated central entry hall.
                S("Exterior_North", new Vector2(-x, z), new Vector2(x, z)),
                S("Exterior_West", new Vector2(-x, -z), new Vector2(-x, z)),
                S("Exterior_East", new Vector2(x, -z), new Vector2(x, z)),
                S("Entrance_South", new Vector2(-x, -z), new Vector2(x, -z), 4f, PortalHeight, 0f, "EntryHall", "Exterior"),

                // Actual octagon openings: N Bedroom; NW/W Craft; NE/E Storage; SW Barracks; SE Alchemy;
                // S is the entry hall. No portal directly joins two named rooms.
                S("Lobby_North", lobby[0], lobby[1], PortalWidth, PortalHeight, 0f, "Lobby", "Bedroom"),
                S("Lobby_NorthEast", lobby[1], lobby[2], PortalWidth, PortalHeight, 0f, "Lobby", "Storage"),
                S("Lobby_East", lobby[2], lobby[3], PortalWidth, PortalHeight, 0f, "Lobby", "Storage"),
                S("Lobby_SouthEast", lobby[3], lobby[4], PortalWidth, PortalHeight, 0f, "Lobby", "Alchemy"),
                S("Lobby_South", lobby[4], lobby[5], 4f, PortalHeight, 0f, "Lobby", "EntryHall"),
                S("Lobby_SouthWest", lobby[5], lobby[6], PortalWidth, PortalHeight, 0f, "Lobby", "Barracks"),
                S("Lobby_West", lobby[6], lobby[7], PortalWidth, PortalHeight, 0f, "Lobby", "Craft"),
                S("Lobby_NorthWest", lobby[7], lobby[0], PortalWidth, PortalHeight, 0f, "Lobby", "Craft"),

                // N bedroom and uninterrupted NW/W and NE/E wings (each pair is one connected room).
                S("Bedroom_West", new Vector2(-18f, 14f), new Vector2(-18f, z)),
                S("Bedroom_East", new Vector2(18f, 14f), new Vector2(18f, z)),
                S("Craft_NorthBoundary", new Vector2(-x, 8f), new Vector2(-30f, 8f)),
                S("Craft_SouthBoundary", new Vector2(-x, -8f), new Vector2(-30f, -8f)),
                S("Storage_NorthBoundary", new Vector2(30f, 8f), new Vector2(x, 8f)),
                S("Storage_SouthBoundary", new Vector2(30f, -8f), new Vector2(x, -8f)),

                // South rooms are separated by a walkable 4 m entry corridor from lobby to exterior.
                S("EntryHall_West", new Vector2(-2f, -14f), new Vector2(-2f, -z)),
                S("EntryHall_East", new Vector2(2f, -14f), new Vector2(2f, -z)),
                S("EntryHall_NorthWest", new Vector2(-18f, -14f), new Vector2(-2f, -14f)),
                S("EntryHall_NorthEast", new Vector2(2f, -14f), new Vector2(18f, -14f)),
                // The lobby's SW/SE portal already defines the south room entrances. Extra vertical
                // dividers here would sit directly behind those portals and seal the quadrant anchors.
                // South rooms remain separated by the continuous entry-hall walls at x=±2.

            };
        }

        /// <summary>
        /// 국가 스타일에 맞는 플레이어 소유 중세 판타지 성 내부 생성.
        /// </summary>
        /// <param name="nationStyle">
        /// "Eastern" (동부), "Western" (서부), "Southern" (남부),
        /// "Northern" (북부), "Empire" (황제국)
        /// </param>
        public static GameObject BuildPlayerCastleInterior(string nationStyle)
        {
            // 기존 1인자 호출 100% 보존: variant 0 = 기존 배치
            return BuildPlayerCastleInterior(nationStyle, 0);
        }

        /// <summary>
        /// 국가 스타일 + 레이아웃 변형(0~7)에 맞는 플레이어 소유 중세 판타지 성 내부 생성.
        /// 결정론: layoutVariant 정수만으로 모든 배치가 고정됨 (Random/전역 상태 없음).
        /// 모든 variant에서 작업대/저장고/무기고 상호작용 앵커가 존재하며
        /// (이름 기반 조회되는 WeaponStand_0, AttachUiComponent로 부착되는 workbench/
        ///  storageShelf2 — 위치만 variant에 따라 변경) Nameplate 라벨도 유지됨.
        /// </summary>
        /// <param name="nationStyle">"Eastern"/"Western"/"Southern"/"Northern"/"Empire"</param>
        /// <param name="layoutVariant">0~7 (범위 밖 값은 8로 나눈 나머지로 정규화)</param>
        public static GameObject BuildPlayerCastleInterior(string nationStyle, int layoutVariant)
        {
            const int variantCount = 8;
            layoutVariant = ((layoutVariant % variantCount) + variantCount) % variantCount;

            // ===== 레이아웃 변형 파라미터 (결정론 테이블) =====
            // mirrorX: 가구 구역 좌우 대칭 (+,-x 스왑 — 단 상호작용 앵커 이름/참조는 유지)
            // pillarPerSide: 기둥 2열 개수(각 3~5), pillarXFac: 기둥 x 위치 계수(±3.5~±4.5)
            // hearthZ: 입구 양쪽 따뜻한 조명 z 시프트, commandZX: 지휘책상 x 시프트(±)
            // meetingZ: 작전회의테이블 z 시프트, extraDecor: 추가 장식가구 유무
            GetLayoutVariantParams(layoutVariant,
                out bool mirrorX, out int pillarPerSide, out float pillarXFac,
                out float hearthZ, out float commandZX, out float meetingZ, out bool extraDecor);

            // 좌우 대칭 부호 (variant 0 = -1 기존: 작업대/저장고 왼쪽, 무기고 오른쪽)
            float mx = mirrorX ? 1f : -1f;

            // Geometry and anchors are authored directly in final room-local meters.
            const float roomWidth = RoomWidth;
            const float roomHeight = RoomHeight;
            const float roomDepth = RoomDepth;

            // 영지 고유 키 (작업대/저장고 상호작용 컴포넌트 설정용 — 국가 스타일 기반 매핑)
            string territoryKey = GetTerritoryKeyForNationStyle(nationStyle);

            // ===== 국가별 텍스처 생성 (CastleInteriorBuilder와 동일) =====
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
                    Debug.LogWarning($"[PlayerCastleInteriorBuilder] 알 수 없는 국가 스타일: '{nationStyle}'. 기본(Empire) 사용.");
                    floorTex = IndoorTextureGenerator.GenerateCastleFloorEmpire();
                    wallTex = IndoorTextureGenerator.GenerateCastleWallEmpire();
                    break;
            }

            Texture2D ceilingTex = IndoorTextureGenerator.GeneratePlasterWall(256, 256,
                new Color(0.65f, 0.62f, 0.58f));

            // ===== 재질 생성 (URP Lit, fallback Standard) =====
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogWarning("[PlayerCastleInteriorBuilder] URP Lit shader not found. Falling back to Standard.");
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                Debug.LogError("[PlayerCastleInteriorBuilder] No shader found (URP Lit nor Standard)! Using default material.");
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            Material floorMat = new Material(shader) { name = $"PlayerCastle_FloorMat_{nationStyle}" };
            floorMat.mainTexture = floorTex;
            floorMat.color = Color.white;

            Material wallMat = new Material(shader) { name = $"PlayerCastle_WallMat_{nationStyle}" };
            wallMat.mainTexture = wallTex;
            wallMat.color = Color.white;

            Material ceilingMat = new Material(shader) { name = "PlayerCastle_CeilingMat" };
            ceilingMat.mainTexture = ceilingTex;
            ceilingMat.color = Color.white;

            // 기능적 가구 재질들 (실용적 톤)
            Material deskMat = new Material(shader) { name = "PlayerCastle_DeskMat" };
            deskMat.color = new Color(0.45f, 0.30f, 0.16f); // 집무 나무

            Material shelfMat = new Material(shader) { name = "PlayerCastle_ShelfMat" };
            shelfMat.color = new Color(0.50f, 0.36f, 0.22f); // 선반 나무

            Material crateMat = new Material(shader) { name = "PlayerCastle_CrateMat" };
            crateMat.color = new Color(0.55f, 0.42f, 0.26f); // 창고 상자

            Material workbenchMat = new Material(shader) { name = "PlayerCastle_WorkbenchMat" };
            workbenchMat.color = new Color(0.35f, 0.24f, 0.14f); // 작업대 짙은 나무

            Material standMat = new Material(shader) { name = "PlayerCastle_StandMat" };
            standMat.color = new Color(0.25f, 0.25f, 0.28f); // 무기 스탠드 철제

            Material bladeMat = new Material(shader) { name = "PlayerCastle_BladeMat" };
            bladeMat.color = new Color(0.65f, 0.66f, 0.70f); // 무기 강철

            Material bannerMat = new Material(shader) { name = "PlayerCastle_BannerMat" };
            bannerMat.color = new Color(0.20f, 0.45f, 0.75f); // 플레이어 환영 (청색)

            Material trimMat = new Material(shader) { name = "PlayerCastle_TrimMat" };
            trimMat.color = new Color(0.85f, 0.70f, 0.25f); // 금장 트림

            Material paperMat = new Material(shader) { name = "PlayerCastle_PaperMat" };
            paperMat.color = new Color(0.92f, 0.90f, 0.82f); // 문서 용지

            // 요리/연금 스테이션 재질 (Phase 1 — 기존 가구와 톤 구분)
            Material cookingMat = new Material(shader) { name = "PlayerCastle_CookingMat" };
            cookingMat.color = new Color(0.50f, 0.32f, 0.18f); // 요리 카운터 따뜻한 나무

            Material alchemyMat = new Material(shader) { name = "PlayerCastle_AlchemyMat" };
            alchemyMat.color = new Color(0.36f, 0.26f, 0.44f); // 연금 테이블 보라빛 나무

            // 침실 재질 (성주 침대 — 세이브/수면 상호작용)
            Material bedMat = new Material(shader) { name = "PlayerCastle_BedMat" };
            bedMat.color = new Color(0.62f, 0.46f, 0.30f); // 따뜻한 나무 침대 프레임

            // 중세 판타지 장식 재질들 (석재 기둥/러그)
            Material pillarMat = new Material(shader) { name = "PlayerCastle_PillarMat" };
            pillarMat.color = new Color(0.40f, 0.38f, 0.35f); // 회색 석재 기둥

            Material rugMat = new Material(shader) { name = "PlayerCastle_RugMat" };
            rugMat.color = new Color(0.55f, 0.18f, 0.15f); // 붉은 카펫/문장 자수

            // ===== 방 생성 =====
            GameObject room = IndoorBuilder.CreateRoom(roomWidth, roomHeight, roomDepth,
                floorMat, wallMat, ceilingMat);

            if (room == null)
            {
                Debug.LogError("[PlayerCastleInteriorBuilder] 방 생성 실패: IndoorBuilder.CreateRoom returned null.");
                var fallback = new GameObject("PlayerCastleRoom_Fallback");
                return fallback;
            }

            room.name = "PlayerCastleRoom";

            // Replace the old rectangular shell; retain the walkable floor and ceiling at final dimensions.
            room.transform.localScale = Vector3.one;
            foreach (string legacyWall in new[] { "Wall_Front", "Wall_Back", "Wall_Left", "Wall_Right" })
            {
                Transform oldWall = room.transform.Find(legacyWall);
                if (oldWall != null) UnityEngine.Object.DestroyImmediate(oldWall.gameObject);
            }
            foreach (WallLayout wall in GetTopologyWalls())
            {
                IndoorBuilder.CreateSolidInteriorWall(room, wall.Name, wall.Start, wall.End,
                    RoomHeight, WallThickness, wall.DoorwayOffset, wall.DoorwayWidth,
                    wall.DoorwayHeight, wallMat);
            }
            // The old procedural country wall texture is not appropriate for exposed masonry topology.
            // Keep its floor override, and apply the provider's stone+normal material to topology meshes.
            IndoorMaterialFactory.ApplyFloorToRoom(room, RoomWidth, RoomDepth);
            IndoorMaterialFactory.ApplyToTopologyWalls(room, RoomWidth, RoomHeight);

            // ===================================================================
            // 1. 지휘 책상 + 관리용 책상/문서 (왕좌 대체 — 뒷벽 중앙, +z 방향)
            // ===================================================================
            GameObject commandDesk = IndoorFurnitureCatalog.CreateTable(3.2f, 1.2f, 1.1f, deskMat);
            commandDesk.name = "CommandDesk";
            commandDesk.transform.SetParent(room.transform);
            commandDesk.transform.localPosition = new Vector3(0f, 0f, 15f);
            AddNameplate(commandDesk, "🪑 지휘 책상");

            // 지휘관 의자 (책상 뒤에서 방 중앙을 향함)
            GameObject commandChair = IndoorFurnitureCatalog.CreateChair(1.1f, deskMat);
            commandChair.name = "CommandChair";
            commandChair.transform.SetParent(room.transform);
            commandChair.transform.localPosition = new Vector3(0f, 0f, 13.5f);

            // 책상 위 문서들 (책상 x 시프트 추종)
            CreateBoxPrimitive(room, "DeskDocument_1", new Vector3(0.35f, 0.02f, 0.25f),
                new Vector3(-19.1f, 1.12f, 0.7f), paperMat);
            CreateBoxPrimitive(room, "DeskDocument_2", new Vector3(0.35f, 0.02f, 0.25f),
                new Vector3(-18.7f, 1.12f, 0.9f), paperMat);
            CreateBoxPrimitive(room, "DeskInkwell", new Vector3(0.08f, 0.12f, 0.08f),
                new Vector3(-17.9f, 1.17f, 0.6f), standMat);

            // 관리용 사이드 책상 (집무실 보조) + 문서 더미 — 좌우 대칭(mx) 적용
            GameObject adminDesk = IndoorFurnitureCatalog.CreateTable(1.8f, 0.9f, 1.0f, deskMat);
            adminDesk.name = "AdminDesk";
            adminDesk.transform.SetParent(room.transform);
            adminDesk.transform.localPosition = new Vector3(-15.5f, 0f, -0.5f);
            AddNameplate(adminDesk, "📜 관리 사무소");

            CreateBoxPrimitive(room, "AdminPaperStack_1", new Vector3(0.30f, 0.06f, 0.22f),
                new Vector3(-15.8f, 1.04f, -0.3f), paperMat);
            CreateBoxPrimitive(room, "AdminPaperStack_2", new Vector3(0.30f, 0.06f, 0.22f),
                new Vector3(-15.2f, 1.04f, -0.7f), paperMat);

            // 중앙 작전 회의 테이블 + 의자 2개
            GameObject planningTable = IndoorFurnitureCatalog.CreateTable(3.0f, 1.6f, 1.0f, deskMat);
            planningTable.name = "PlanningTable";
            planningTable.transform.SetParent(room.transform);
            planningTable.transform.localPosition = new Vector3(2f, 0f, 0f);

            CreateBoxPrimitive(room, "PlanningMap", new Vector3(0.8f, 0.02f, 0.6f),
                new Vector3(2f, 1.02f, 0f), bannerMat); // 작전 지도

            for (int side = -1; side <= 1; side += 2)
            {
                GameObject planChair = IndoorFurnitureCatalog.CreateChair(1.0f, deskMat);
                planChair.name = $"PlanningChair_{side}";
                planChair.transform.SetParent(room.transform);
                planChair.transform.localPosition = new Vector3(side * 1.0f, 0f, meetingZ);
                // 테이블 중앙을 향하도록 회전
                planChair.transform.localRotation = Quaternion.Euler(0f, side > 0 ? -90f : 90f, 0f);
            }

            // ===================================================================
            // 1b. 침대 (성주 침실 — E키 상호작용: 수면/💾세이브, 사망 시 스폰핀 부활)
            //     앞벽(z=-roomDepth*0.5)에 머리(베개, 로컬 -z)를 향해 배치.
            //     작업대(5번, x=mx*-5)와 요리 테이블(5b, x=mx*6.2) 사이 여백이며,
            //     석재 기둥열(6번, z=-5..5)과는 z 간격으로 분리 — 8개 variant 전부 무충돌.
            //     CreateBed가 root에 Bed 컴포넌트를 부착하므로 E키 상호작용 즉시 동작.
            // ===================================================================
            GameObject lordBed = IndoorFurnitureCatalog.CreateBed(1.2f, 2.0f, bedMat);
            lordBed.name = "LordBed";
            lordBed.transform.SetParent(room.transform);
            lordBed.transform.localPosition = new Vector3(-16f, 0f, 12f);
            AddNameplate(lordBed, "🛏️ 성주의 침대 (세이브)");

            // ===================================================================
            // 2. 플레이어 환영 배너 (뒷벽 위 — 점령지 표시)
            // ===================================================================
            GameObject bannerRoot = new GameObject("PlayerBanner");
            bannerRoot.transform.SetParent(room.transform);
            bannerRoot.transform.localPosition = new Vector3(2f, 0f, 5.5f);

            // 깃대 2개
            CreateCylinderPrimitive(bannerRoot, "BannerPole_Left", 0.05f, 4.6f,
                new Vector3(-1.9f, 2.3f, 0f), standMat);
            CreateCylinderPrimitive(bannerRoot, "BannerPole_Right", 0.05f, 4.6f,
                new Vector3(1.9f, 2.3f, 0f), standMat);

            // 가로 바
            CreateBoxPrimitive(bannerRoot, "BannerBar", new Vector3(3.8f, 0.06f, 0.06f),
                new Vector3(0f, 4.5f, 0f), standMat);

            // 깃천 (플레이어 청색) + 금장 트림 + 문장
            CreateBoxPrimitive(bannerRoot, "BannerCloth", new Vector3(2.6f, 1.7f, 0.04f),
                new Vector3(0f, 3.4f, 0f), bannerMat);
            CreateBoxPrimitive(bannerRoot, "BannerTrim_Top", new Vector3(2.6f, 0.08f, 0.05f),
                new Vector3(0f, 4.2f, -0.03f), trimMat);
            CreateBoxPrimitive(bannerRoot, "BannerTrim_Bottom", new Vector3(2.6f, 0.08f, 0.05f),
                new Vector3(0f, 2.6f, -0.03f), trimMat);
            CreateBoxPrimitive(bannerRoot, "BannerEmblem", new Vector3(0.5f, 0.5f, 0.05f),
                new Vector3(0f, 3.4f, -0.06f), trimMat);
            AddNameplate(bannerRoot, "🚩 플레이어 영지");

            // ===================================================================
            // 3. 저장고 (왼쪽 벽 — 선반 + 상자)
            // ===================================================================
            GameObject storageShelf1 = IndoorFurnitureCatalog.CreateShelf(2.2f, 2.2f, 0.6f, shelfMat, 3);
            storageShelf1.name = "StorageShelf_1";
            storageShelf1.transform.SetParent(room.transform);
            storageShelf1.transform.localPosition = new Vector3(16f, 0f, 1.5f);
            storageShelf1.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            GameObject storageShelf2 = IndoorFurnitureCatalog.CreateShelf(2.2f, 2.2f, 0.6f, shelfMat, 3);
            storageShelf2.name = "StorageShelf_2";
            storageShelf2.transform.SetParent(room.transform);
            storageShelf2.transform.localPosition = new Vector3(16f, 0f, 4.0f);
            storageShelf2.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            // Phase B: 저장고 상호작용 — StorageShelf_2 기준점에 TerritoryWarehouse 부착.
            // E키 근접 상호작용으로 창고 UI를 열며, 실제 데이터는 WarehouseSystem(영지 키)에 위임.
            // Systems asmdef는 UI asmdef를 참조할 수 없으므로(순환 참조) 리플렉션으로 부착.
            AttachUiComponent(storageShelf2, TerritoryWarehouseTypeName, territoryKey);

            // 창고 상자 더미 (큐브 프리미티브) — 좌우 대칭(mx) 적용
            CreateBoxPrimitive(room, "StorageCrate_1", new Vector3(0.7f, 0.7f, 0.7f),
                new Vector3(13f, 0.35f, 1.5f), crateMat);
            CreateBoxPrimitive(room, "StorageCrate_2", new Vector3(0.7f, 0.7f, 0.7f),
                new Vector3(13f, 0.35f, 2.5f), crateMat);
            CreateBoxPrimitive(room, "StorageCrate_3", new Vector3(0.7f, 0.7f, 0.7f),
                new Vector3(13f, 1.05f, 2.0f), crateMat);
            CreateBoxPrimitive(room, "StorageCrate_4", new Vector3(0.9f, 0.9f, 0.9f),
                new Vector3(12f, 0.45f, 3.3f), crateMat);
            CreateBoxPrimitive(room, "StorageCrate_5", new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(13.5f, 0.25f, 3.5f), crateMat);

            // 저장고 통 (Cylinder)
            CreateCylinderPrimitive(room, "StorageBarrel", 0.4f, 1.0f,
                new Vector3(12f, 0.5f, 1.2f), crateMat);

            // 저장고 안내 팻말 (벽)
            GameObject storageSign = CreateBoxPrimitive(room, "StorageSign", new Vector3(0.05f, 0.7f, 2.4f),
                new Vector3(8.5f, 2.5f, 3f), bannerMat);
            AddNameplate(storageSign, "🎒 저장고");

            // ===================================================================
            // 4. 무기고 (오른쪽 벽 — 무기 스탠드 3개 + 벽걸이 무기고) — 좌우 대칭(mx) 적용
            // ===================================================================
            for (int i = 0; i < 3; i++)
            {
                float zPos = 3.0f - i * 3.0f;
                CreateWeaponStand(room, $"WeaponStand_{i}",
                    new Vector3(21f, 0f, 15f - i * 3f), mirrorX ? 90f : -90f, standMat, bladeMat);
            }

            // Phase C: 무기고 상호작용 기준점 — WeaponStand_0 (첫 번째 무기 스탠드, z=3.0,
            // 오른쪽 벽에서 0.6m 안쪽)이 가장 접근성이 좋으므로 부착 앵커로 사용.
            // CreateWeaponStand는 void 반환이라 생성 시점에 참조를 받을 수 없어,
            // room의 직계 자식 이름으로 조회한다 (시각 배치 코드는 변경 없음).
            GameObject armoryAnchor = room.transform.Find("WeaponStand_0")?.gameObject;
            if (armoryAnchor == null)
            {
                Debug.LogWarning("[PlayerCastleInteriorBuilder] WeaponStand_0 을 찾지 못해 무기고 상호작용 부착을 건너뜁니다.");
            }

            // 벽걸이 무기고 (벽 앞쪽) — 좌우 대칭(mx) 적용
            GameObject wallRack = CreateBoxPrimitive(room, "WeaponWallRack", new Vector3(0.08f, 1.8f, 2.0f),
                new Vector3(21.5f, 1.6f, 8f), standMat);
            if (wallRack != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    float bladeZ = -6.1f + i * 0.4f;
                    CreateBoxPrimitive(room, $"WallWeapon_{i}", new Vector3(0.05f, 1.2f, 0.14f),
                        new Vector3(21.5f, 1.5f, 7.4f + i * 0.4f), bladeMat);
                }
            }

            // 무기고 안내 팻말 (벽) — 좌우 대칭(mx) 적용
            GameObject armorySign = CreateBoxPrimitive(room, "ArmorySign", new Vector3(0.05f, 0.7f, 2.4f),
                new Vector3(21f, 2.6f, 17f), bannerMat);
            AddNameplate(armorySign, "⚔️ 무기고");

            // Phase C: 무기고 상호작용 — WeaponStand_0에 TerritoryWarehouse 부착 (무기고 전용 창고).
            // 영지 키에 "_armory" 접미사(예: "East_01_armory")를 붙여 저장고(territoryKey)와
            // 슬롯이 분리된 독립 창고로 동작. E키 근접 상호작용으로 창고 UI를 열며,
            // 실제 데이터는 WarehouseSystem(무기고 전용 영지 키)에 위임.
            // Systems asmdef는 UI asmdef를 참조할 수 없으므로(순환 참조) 리플렉션으로 부착.
            AttachUiComponent(armoryAnchor, TerritoryWarehouseTypeName, territoryKey + "_armory");

            // ===================================================================
            // 5. 작업대 (앞벽 왼쪽 — 제작/수리 공간)
            // ===================================================================
            GameObject workbench = IndoorFurnitureCatalog.CreateCounter(2.6f, 1.0f, 1.0f, workbenchMat);
            workbench.name = "Workbench";
            workbench.transform.SetParent(room.transform);
            workbench.transform.localPosition = new Vector3(-16f, 0f, -16f);
            AddNameplate(workbench, "🛠️ 작업대");

            // Equipment-crafting station: E opens WeaponForgeUTK when the Toolkit root is ready,
            // with the legacy CraftingUI retained as a fallback for unmigrated scenes.
            // The station supports both Input System and legacy E-key input. Keep UI attached
            // through reflection because the Systems assembly cannot reference UI directly.
            AttachUiComponent(workbench, TerritoryCraftingStationTypeName, territoryKey, "영지 작업대");

            // The doorway and its central walkable aisle stay free of physical props.
            // Keep point-light ambience only; wall decor remains high and outside the opening.
            // 작업대 위 도구들 (모루 + 공구) — 좌우 대칭(mx) 적용
            CreateBoxPrimitive(workbench, "WorkbenchAnvil", new Vector3(0.5f, 0.25f, 0.35f),
                new Vector3(-0.5f, 1.13f, 0f), bladeMat);
            CreateBoxPrimitive(workbench, "WorkbenchTool_1", new Vector3(0.12f, 0.12f, 0.30f),
                new Vector3(0.4f, 1.07f, 0.1f), standMat);
            CreateBoxPrimitive(workbench, "WorkbenchTool_2", new Vector3(0.12f, 0.12f, 0.30f),
                new Vector3(0.6f, 1.07f, -0.2f), standMat);

            // Barracks map table: position it inside the expanded Barracks after the uniform
            // 2.45 furniture scale, with enough clearance from the WeaponStand_0 armory station.
            // E-key soldier-management interaction remains on the table without a Systems -> UI reference.
            GameObject barracksMapTable = IndoorFurnitureCatalog.CreateTable(2.0f, 1.4f, 1.0f, deskMat);
            barracksMapTable.name = "BarracksMapTable";
            barracksMapTable.transform.SetParent(room.transform);
            barracksMapTable.transform.localPosition = new Vector3(-12.25f, 0f, -14.3f);
            AddNameplate(barracksMapTable, "⚔️ 병사 관리");
            AttachUiComponent(barracksMapTable, CastleSoldierManagementStationTypeName,
                "병사 관리", 1.8f);

            // [2026-10-09 수리] 배럭 침대 20개 — 병사 관리 탁자(BarracksMapTable) 옆 배럭 구역
            // (SW 사분면, GetTopologyZoneAt=="Barracks") 내부 4열×5행 그리드.
            // ⚠ 좌표는 authored(소형 공간) 값이고, 빌드 말미의 ×2.45 가구 스케일 패스를 통과한
            // 값이 최종 local이다: 열 x -4/-5.4/-6.8/-8.2 → 최종 -9.8/-13.2/-16.7/-20.1,
            // 행 z -7.2/-9.4/-11.6/-13.8/-16.0 → 최종 -17.6/-23.0/-28.3/-33.8/-39.2.
            // (이전 z=-18~-26.8은 스케일 후 -44.1~-65.7로 남벽 밖에 세워지는 버그였음 —
            //  병사 관리창이 있는 배럭이 아니라 성 남벽 밖에 20개가 배치돼 있었다.)
            // 최종 그리드 x -21.6..-8.3 / z -15.2..-41.7 — 탁자(최종 -30,-35)·아군 병사
            // 스폰(-30,-33)·무기고(x -39..-51)·입구 통로(x=±2)·남벽(z=-44.1)과 전부 무충돌.
            // 회귀 게이트: PlayerCastleInteriorCollisionAndKitchenTests.BarrackBeds_*
            for (int row = 0; row < 5; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    GameObject barrackBed = IndoorFurnitureCatalog.CreateBed(1.2f, 2.0f, bedMat);
                    barrackBed.name = $"BarrackBed_{row * 4 + col + 1}";
                    barrackBed.transform.SetParent(room.transform);
                    barrackBed.transform.localPosition = new Vector3(-4f - col * 1.4f, 0f, -7.2f - row * 2.2f);
                }
            }

            // 작업대 안내 팻말 (앞벽) — 좌우 대칭(mx) 적용
            GameObject workbenchSign = CreateBoxPrimitive(room, "WorkbenchSign", new Vector3(1.6f, 0.6f, 0.05f),
                new Vector3(-16f, 2.4f, -17.5f), bannerMat);
            AddNameplate(workbenchSign, "🛠️ 작업대");

            // ===================================================================
            // 5b. 요리/연금 스테이션 (CRAFTING_WAREHOUSE_PLAN Phase 1)
            //     기존 장비 작업대(5번)에 더해 요리·연금 상호작용 앵커를 추가.
            //     요리 카운터/연금대와 pantry는 Storage 구역에 배치되어 서로 간섭하지 않는다.
            //     작업대(앞벽 x=mx*-5)·장식탁자(extraDecor, x=mx*5, z=-7.4)는 반대편
            //     또는 z 간격 0.3m 이상이라 8개 variant 전부에서 겹치지 않음.
            //     참고: CookingStation/AlchemyStation에는 Configure 메서드가 없어
            //     AttachUiComponent가 AddComponent 후 경고 1회를 남기고 정상 진행
            //     (컴포넌트는 부착됨 — 기본 직렬화 값으로 E키 상호작용 동작).
            // ===================================================================
            GameObject cookingTable = IndoorFurnitureCatalog.CreateCounter(1.4f, 0.95f, 0.9f, cookingMat);
            cookingTable.name = "CookingTable";
            cookingTable.transform.SetParent(room.transform);
            cookingTable.transform.localPosition = new Vector3(1f, 0f, 14f);
            AddNameplate(cookingTable, "🍳 요리 테이블");

            // 요리 냄비 소품 (카운터 상판 위 — 시각 구분용)
            CreateCylinderPrimitive(cookingTable, "CookingPot", 0.22f, 0.28f,
                new Vector3(0f, 1.09f, 0f), standMat);

            // Systems asmdef는 UI asmdef를 참조할 수 없으므로(순환 참조) 리플렉션으로 부착.
            AttachUiComponent(cookingTable, CookingStationTypeName);

            // Dedicated pantry beside the cooking station. Keep its storage key independent from
            // the general store and armory, while storing real items usable by both cooking paths.
            GameObject cookingIngredientWarehouse = IndoorFurnitureCatalog.CreateShelf(1.8f, 1.8f, 0.6f, shelfMat, 3);
            cookingIngredientWarehouse.name = "CookingIngredientWarehouse";
            cookingIngredientWarehouse.transform.SetParent(room.transform);
            cookingIngredientWarehouse.transform.localPosition = new Vector3(3.2f, 0f, 14f);
            AddNameplate(cookingIngredientWarehouse, "🥩 요리 재료 창고");
            string kitchenWarehouseKey = territoryKey + "_kitchen";
            AttachUiComponent(cookingIngredientWarehouse, TerritoryWarehouseTypeName, kitchenWarehouseKey, 20, 2f);
            SeedCookingIngredients(kitchenWarehouseKey);

            GameObject alchemyTable = IndoorFurnitureCatalog.CreateTable(1.4f, 1.4f, 0.95f, alchemyMat);
            alchemyTable.name = "AlchemyTable";
            alchemyTable.transform.SetParent(room.transform);
            alchemyTable.transform.localPosition = new Vector3(6f, 0f, 9f);
            AddNameplate(alchemyTable, "🧪 연금술 테이블");

            // 연금 플라스크 소품 (테이블 상판 위 — 시각 구분용)
            CreateCylinderPrimitive(alchemyTable, "AlchemyFlask", 0.14f, 0.32f,
                new Vector3(0.35f, 1.11f, 0.30f), trimMat);

            // Systems asmdef는 UI asmdef를 참조할 수 없으므로(순환 참조) 리플렉션으로 부착.
            AttachUiComponent(alchemyTable, AlchemyStationTypeName);

            // ===================================================================
            // 6. 중세 석재 기둥 2열 (대전당 느낌 — 중앙 복도 양쪽)
            //    x=±pillarX(3.5~4.5), z=-5..5에 pillarPerSide개씩 — 가구와 겹치지 않게 배치
            // ===================================================================
            float pillarX = 3f + pillarXFac;   // 3.5~4.5 (variant 0: 4f)
            // [P17] 기둥 옵션 — 기본 제거(예시 이미지 스타일). IncludePillars=true 시 복원.
            //   return 금지: 뒤에 7(조명)/8(장식) 섹션이 이어짐 → 루프 바운드만 0 처리.
            for (int side = -1; side <= 1; side += 2)
            {
                for (int pi = 0; pi < (IndoorTextureLoader.IncludePillars ? pillarPerSide : 0); pi++)
                {
                    float pillarZ = -5f + pi * (10f / Mathf.Max(1, pillarPerSide - 1)); // -5..5 균등
                    GameObject pillarRoot = new GameObject($"StonePillar_{(side > 0 ? "R" : "L")}{pi}");
                    pillarRoot.transform.SetParent(room.transform);
                    pillarRoot.transform.localPosition = new Vector3(side * pillarX, 0f, pillarZ);

                    // 기둥 받침 (석재 베이스)
                    CreateBoxPrimitive(pillarRoot, "Base", new Vector3(1.0f, 0.3f, 1.0f),
                        new Vector3(0f, 0.15f, 0f), pillarMat);
                    // 몸통 (Cylinder)
                    CreateCylinderPrimitive(pillarRoot, "Shaft", 0.35f, 5.4f,
                        new Vector3(0f, 3.0f, 0f), pillarMat);
                    // 머리 받침 (캐피털 — 천장 바로 아래)
                    CreateBoxPrimitive(pillarRoot, "Capital", new Vector3(1.0f, 0.3f, 1.0f),
                        new Vector3(0f, 5.75f, 0f), pillarMat);
                }
            }

            // ===================================================================
            // 7. Warm entrance-side point lights only; no physical braziers or coals.
            // ===================================================================
            for (int side = -1; side <= 1; side += 2)
            {
                IndoorLighting.AddPointLight(room,
                    new Vector3(side * 10.0f, 1.5f, hearthZ),
                    new Color(1f, 0.48f, 0.20f), 5f, 0.3f);
            }

            // Subtle warm bounce near the front pillars; keep the central volume subdued.
            IndoorLighting.AddPointLight(room, new Vector3(-pillarX - 0.55f, 4.5f, -5f), new Color(1f, 0.48f, 0.24f), 4.5f, 0.22f);
            IndoorLighting.AddPointLight(room, new Vector3(pillarX + 0.55f, 4.5f, -5f), new Color(1f, 0.48f, 0.24f), 4.5f, 0.22f);

            // ===================================================================
            // 8. 중세 장식 — 왕실 문장 방패 + 붉은 러그 (기존 청색 배너는 유지)
            // ===================================================================
            // 뒷벽 문장 방패 2개 (배너 양옆 — 왕실/영지 상징)
            for (int sx = -1; sx <= 1; sx += 2)
            {
                string shieldSide = sx > 0 ? "R" : "L";
                CreateBoxPrimitive(room, $"HeraldicShield_Back{shieldSide}",
                    new Vector3(0.7f, 0.85f, 0.06f),
                    new Vector3(sx * 4.5f, 3.0f, roomDepth * 0.5f - 0.04f), trimMat);
                // 방패 안쪽 붉은 자수 문장
                CreateBoxPrimitive(room, $"HeraldicEmblem_Back{shieldSide}",
                    new Vector3(0.35f, 0.45f, 0.04f),
                    new Vector3(sx * 4.5f, 3.0f, roomDepth * 0.5f - 0.10f), rugMat);
            }

            // Entry-side left/right wall heraldic shields, mounted above the walking volume.
            for (int sx = -1; sx <= 1; sx += 2)
            {
                string shieldSide = sx > 0 ? "R" : "L";
                CreateBoxPrimitive(room, $"HeraldicShield_Wall{shieldSide}",
                    new Vector3(0.06f, 0.85f, 0.7f),
                    new Vector3(sx * (roomWidth * 0.5f - 0.04f), 2.9f, -4.5f), trimMat);
                CreateBoxPrimitive(room, $"HeraldicEmblem_Wall{shieldSide}",
                    new Vector3(0.04f, 0.45f, 0.35f),
                    new Vector3(sx * (roomWidth * 0.5f - 0.10f), 2.9f, -4.5f), rugMat);
            }

            // 바닥 러그 (붉은 카펫 — 바닥에서 0.005 위 부착, z-fighting 방지)
            CreateBoxPrimitive(room, "Rug_CentralAisle", new Vector3(2.8f, 0.03f, 6.0f),
                new Vector3(0f, 0.02f, -3.5f), rugMat);
            CreateBoxPrimitive(room, "Rug_Throne", new Vector3(4.0f, 0.03f, 2.4f),
                new Vector3(0f, 0.02f, 4.2f), rugMat);

            // ===================================================================
            // 8b. 추가 장식 가구 (variant별 — 측벽 공백/복도 모서리 고정 좌표)
            // ===================================================================
            if (extraDecor)
            {
                // 꽃병 받침대 (Cylinder 화분) — 뒷벽 코너 공백
                CreateCylinderPrimitive(room, "PottedPlant_BackL", 0.45f, 0.9f,
                    new Vector3(10f, 0.45f, 5.2f), crateMat);
                CreateCylinderPrimitive(room, "PottedPlant_BackR", 0.45f, 0.9f,
                    new Vector3(-6f, 0.45f, 5.2f), crateMat);
                // 장식 탁자 (전면벽 중앙 공백 — 작업대 반대편)
                GameObject decorTable = IndoorFurnitureCatalog.CreateCounter(1.6f, 0.9f, 0.7f, deskMat);
                decorTable.name = "DecorativeTable";
                decorTable.transform.SetParent(room.transform);
                decorTable.transform.localPosition = new Vector3(-4f, 0f, 5f);
                // 표주 잔 (데코)
                CreateCylinderPrimitive(decorTable, "Goblet", 0.12f, 0.25f,
                    new Vector3(0f, 0.55f, 0f), trimMat);
            }

            // ===================================================================
            // 9. 조명 — 따뜻한 중세 촛불빛 톤의 주황빛, 은은한 점멸
            // ===================================================================
            // Force a color ambient mode: ambientLight has no visible effect while the active mode is Skybox.
            // The additive indoor scene unload restores the world scene's lighting environment.
            Color ambient = new Color(0.10f, 0.075f, 0.055f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            // 주변광 대폭 축소 — 횃불/불빛(포인트라이트)만 남는 은은한 실내 무드.
            IndoorLighting.SetupIndoorLighting(room, ambient, 0.26f, true, 0.22f, 6f);
            RenderSettings.ambientLight = new Color(ambient.r * 0.26f, ambient.g * 0.26f, ambient.b * 0.26f);

            // A subdued ceiling lantern leaves the room legible without flattening the candlelit mood.
            IndoorLighting.AddPointLight(room,
                new Vector3(0f, roomHeight - 0.6f, 0f),
                new Color(1f, 0.72f, 0.46f), 4.5f, 0.35f);

            // Small warm pools keep work areas readable while the aisles remain dimmer.
            IndoorLighting.AddPointLight(room,
                new Vector3(commandZX, 4.2f, roomDepth * 0.5f - 1.8f),
                new Color(1f, 0.78f, 0.5f), 5.5f, 0.3f);
            IndoorLighting.AddPointLight(room,
                new Vector3(mx * -5f, 3.8f, -roomDepth * 0.5f + 0.8f),
                new Color(1f, 0.82f, 0.58f), 5.5f, 0.3f);
            IndoorLighting.AddPointLight(room,
                new Vector3(mx * -9.5f, 3.8f, 0.5f),
                new Color(1f, 0.78f, 0.5f), 6f, 0.35f);
            IndoorLighting.AddPointLight(room,
                new Vector3(mx * 9.5f, 3.8f, 0.5f),
                new Color(1f, 0.78f, 0.5f), 6f, 0.35f);

            Debug.Log($"[PlayerCastleInteriorBuilder] 플레이어 소유 중세 판타지 성 내부 생성 완료! (스타일: {nationStyle}, 레이아웃 변형: {layoutVariant}) — 석재 기둥 2열·따뜻한 조명·문장 방패·러그 장식 포함");

            // ===================================================================
            // 9b. 방 표지판 (각 방 입구 근처 스탠드 — 자연스러운 실내 안내) + 방별 보조 조명
            // ===================================================================
            AddRoomSign(room, "Sign_Office", 1.6f, 0.5f, deskMat, -13.4f, -0.5f, 0f, "🖥️ 집무실");
            AddRoomSign(room, "Sign_Craft", 1.6f, 0.5f, workbenchMat, -13.4f, -6.5f, 0f, "🛠️ 크래프트");
            AddRoomSign(room, "Sign_Bedroom", 1.6f, 0.5f, bedMat, -13.4f, 12.0f, 0f, "🛏️ 침실");
            AddRoomSign(room, "Sign_Kitchen", 1.6f, 0.5f, cookingMat, 3.0f, 12.8f, 0f, "🍳 부엌·연금");
            AddRoomSign(room, "Sign_Armory", 1.6f, 0.5f, standMat, 18.5f, 11.5f, 0f, "⚔️ 무기고");
            AddRoomSign(room, "Sign_Storage", 1.6f, 0.5f, crateMat, 12.5f, 5.2f, 0f, "🎒 저장고");

            // 은은한 방별 보조 조명 — 실내 어둠을 유지하고 각 기능 구역만 살짝 비춘다.
            IndoorLighting.AddPointLight(room, new Vector3(16f, 4f, -9f), new Color(1f, 0.82f, 0.58f), 5.5f, 0.3f);
            IndoorLighting.AddPointLight(room, new Vector3(-16f, 4f, 12f), new Color(1f, 0.78f, 0.5f), 5.5f, 0.28f);

            // Scale furniture/decor uniformly into the expanded walkable footprint. The shell, floor,
            // ceiling and topology walls are already authored in final meters and remain untouched.
            foreach (Transform child in room.transform)
            {
                if (child.name == "Floor" || child.name == "Ceiling" ||
                    child.name.StartsWith("Exterior_") || child.name.StartsWith("Entrance_") ||
                    child.name.StartsWith("Lobby_") || child.name.Contains("Boundary") ||
                    child.name.Contains("Divider") || child.name.StartsWith("Bedroom_") ||
                    child.name.StartsWith("EntryHall_") || child.name.StartsWith("Barracks_") ||
                    child.name.StartsWith("Alchemy_")) continue;
                Vector3 position = child.localPosition;
                position.x *= TopologyFurnitureScale;
                position.z *= TopologyFurnitureScale;
                child.localPosition = position;
                child.localScale *= TopologyFurnitureScale;
            }

            // Add after legacy furniture scaling so portal trim remains in wall-local meters.
            AddNonCollidingPortalStonework(room, GetTopologyWalls(), RoomHeight, WallThickness, shader);

            // Interaction anchors are explicitly assigned to their intended player-owned zones.
            SetAnchorPosition(room, "Workbench", GetAnchorPosition("Workbench"));
            SetAnchorPosition(room, "StorageShelf_2", GetAnchorPosition("StorageShelf_2"));
            SetAnchorPosition(room, "WeaponStand_0", GetAnchorPosition("WeaponStand_0"));
            SetAnchorPosition(room, "AlchemyTable", GetAnchorPosition("AlchemyTable"));
            SetAnchorPosition(room, "LordBed", GetAnchorPosition("LordBed"));
            SetAnchorPosition(room, "StorageShelf_1", new Vector2(40f, 18f));
            SetAnchorPosition(room, "CookingTable", GetAnchorPosition("CookingTable"));
            SetAnchorPosition(room, "CookingIngredientWarehouse", GetAnchorPosition("CookingIngredientWarehouse"));

            // Keep legacy scenery beside the topology zone and stations it identifies.
            SetAnchorPosition(room, "WeaponStand_1", new Vector2(-45f, -25f));
            SetAnchorPosition(room, "WeaponStand_2", new Vector2(-50f, -25f));
            SetAnchorPosition(room, "WorkbenchSign", new Vector2(-40f, 21.5f));
            SetAnchorPosition(room, "StorageCrate_1", new Vector2(35f, 20f));
            SetAnchorPosition(room, "StorageCrate_2", new Vector2(37f, 20f));
            SetAnchorPosition(room, "StorageCrate_3", new Vector2(36f, 21f));
            SetAnchorPosition(room, "StorageCrate_4", new Vector2(34f, 22f));
            SetAnchorPosition(room, "StorageCrate_5", new Vector2(38f, 22f));
            SetAnchorPosition(room, "StorageBarrel", new Vector2(44f, 20f));
            SetAnchorPosition(room, "StorageSign", new Vector2(40f, 15f));
            SetAnchorPosition(room, "WeaponWallRack", new Vector2(-40f, -20f));
            SetAnchorPosition(room, "ArmorySign", new Vector2(-45f, -18f));
            for (int i = 0; i < 4; i++)
                SetAnchorPosition(room, $"WallWeapon_{i}", new Vector2(-40f, -20f + i * 0.5f));
            SetAnchorPosition(room, "CommandDesk", new Vector2(0f, 36.75f));
            SetAnchorPosition(room, "CommandChair", new Vector2(0f, 33.075f));
            SetAnchorPosition(room, "Sign_Craft", new Vector2(-43f, 24f));
            SetAnchorPosition(room, "Sign_Storage", new Vector2(45f, 22f));
            SetAnchorPosition(room, "Sign_Armory", new Vector2(-45f, -22f));
            SetAnchorPosition(room, "Sign_Bedroom", new Vector2(0f, 25f));
            SetAnchorPosition(room, "Sign_Kitchen", new Vector2(45f, 34f));
            room.transform.localScale = Vector3.one;
            return room;
        }

        /// <summary>바닥 스탠드 방 표지판 (기둥 + 상판 설치, Nameplate 부착).</summary>
        private static void AddRoomSign(GameObject parent, string name, float width, float height,
            Material mat, float x, float z, float yRot, string label)
        {
            GameObject sign = new GameObject(name);
            sign.transform.SetParent(parent.transform);
            sign.transform.localPosition = new Vector3(x, 0f, z);
            sign.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);
            CreateCylinderPrimitive(sign, "Pole", 0.035f, 1.3f, new Vector3(0f, 0.65f, 0f), mat);
            CreateBoxPrimitive(sign, "Board", new Vector3(width, height, 0.06f),
                new Vector3(0f, 1.45f, 0f), mat);
            AddNameplate(sign, label);
        }

        /// <summary>
        /// 레이아웃 변형(0~7)별 결정론 파라미터 테이블.
        /// Random/전역 상태 없음 — layoutVariant 정수만으로 배치가 완전히 결정됨.
        /// variant 0 = 기존 배치와 정확히 동일 (회귀 방지).
        /// 모든 variant에서 작업대/저장고/무기고 앵커가 존재 (이름/참조 유지).
        /// </summary>
        private static void GetLayoutVariantParams(int variant,
            out bool mirrorX, out int pillarPerSide, out float pillarXFac,
            out float hearthZ, out float commandZX, out float meetingZ, out bool extraDecor)
        {
            //           mirror pillar xFac  hearthZ cmdZX  meetZ  extra
            // 0(기본):  no    4     1.0    -4.5   0     0.5    no     (모두 기존값)
            // 1(플립):  yes   4     1.0    -4.5   0     0.5    no
            // 2(소형):  no    3     0.0    -6.0   0.8   1.0    yes
            // 3(촘촘):  yes   5     1.5    -3.0  -0.8  -0.5   yes
            // 4(넓은):  no    3     0.5    -5.0   0.6   1.5    no
            // 5(외곽):  yes   4     1.5    -4.0  -0.6   0.0    yes
            // 6(대형):  no    5     0.5    -4.5   0     0.5    yes
            // 7(소플립):yes   3     1.0    -6.5   0.8  -1.0   no
            switch (variant)
            {
                case 1:
                    mirrorX = true;  pillarPerSide = 4; pillarXFac = 1.0f;
                    hearthZ = -4.5f; commandZX = 0f; meetingZ = 0.5f; extraDecor = false;
                    break;
                case 2:
                    mirrorX = false; pillarPerSide = 3; pillarXFac = 0.0f;
                    hearthZ = -6.0f; commandZX = 0.8f; meetingZ = 1.0f; extraDecor = true;
                    break;
                case 3:
                    mirrorX = true;  pillarPerSide = 5; pillarXFac = 1.5f;
                    hearthZ = -3.0f; commandZX = -0.8f; meetingZ = -0.5f; extraDecor = true;
                    break;
                case 4:
                    mirrorX = false; pillarPerSide = 3; pillarXFac = 0.5f;
                    hearthZ = -5.0f; commandZX = 0.6f; meetingZ = 1.5f; extraDecor = false;
                    break;
                case 5:
                    mirrorX = true;  pillarPerSide = 4; pillarXFac = 1.5f;
                    hearthZ = -4.0f; commandZX = -0.6f; meetingZ = 0.0f; extraDecor = true;
                    break;
                case 6:
                    mirrorX = false; pillarPerSide = 5; pillarXFac = 0.5f;
                    hearthZ = -4.5f; commandZX = 0f; meetingZ = 0.5f; extraDecor = true;
                    break;
                case 7:
                    mirrorX = true;  pillarPerSide = 3; pillarXFac = 1.0f;
                    hearthZ = -6.5f; commandZX = 0.8f; meetingZ = -1.0f; extraDecor = false;
                    break;
                case 0:
                default:
                    // 기존 배치와 정확히 동일 (기둥 ±4·4개, 화로 z=-4.5, 지휘책상 x=0, 작전테이블 z=0.5, 장식 없음)
                    mirrorX = false; pillarPerSide = 4; pillarXFac = 1.0f;
                    hearthZ = -4.5f; commandZX = 0f; meetingZ = 0.5f; extraDecor = false;
                    break;
            }
        }

        // ===================================================================
        // 프라이빗 헬퍼 (기존 빌더 클래스에는 손대지 않음 — 이 클래스 내부 전용)
        // ===================================================================

        /// <summary>ProjectName.UI 어셈블리 상호작용 컴포넌트의 어셈블리 정식 타입 이름 (리플렉션 전용).</summary>
        private const string TerritoryCraftingStationTypeName =
            "ProjectName.UI.TerritoryCraftingStation, ProjectName.UI";
        private const string TerritoryWarehouseTypeName =
            "ProjectName.UI.TerritoryWarehouse, ProjectName.UI";
        private const string CookingStationTypeName =
            "ProjectName.UI.CookingStation, ProjectName.UI";
        private const string AlchemyStationTypeName =
            "ProjectName.UI.AlchemyStation, ProjectName.UI";
        private const string CastleSoldierManagementStationTypeName =
            "ProjectName.UI.CastleSoldierManagementStation, ProjectName.UI";

        /// <summary>
        /// 어셈블리 경계(ProjectName.UI)를 넘어 상호작용 컴포넌트를 리플렉션으로 부착한다.
        /// Systems asmdef가 UI asmdef를 참조하면 순환 참조(UI → Systems)가 되어 불가능하므로,
        /// 컴파일 타임 참조 없이 어셈블리 정식 타입 이름으로 타입을 찾아 AddComponent(Type) 후
        /// Configure를 호출한다. 타입/메서드를 못 찾으면 경고만 남기고 컴포넌트 없이 진행.
        /// </summary>
        /// <param name="target">컴포넌트를 부착할 GameObject</param>
        /// <param name="assemblyQualifiedTypeName">
        /// 예: "ProjectName.UI.TerritoryWarehouse, ProjectName.UI" (asmdef name과 정확히 일치)
        /// </param>
        /// <param name="configureArgs">Configure에 전달할 앞쪽 인자들 (나머지는 선언된 기본값 사용)</param>
        private static void AttachUiComponent(GameObject target, string assemblyQualifiedTypeName,
            params object[] configureArgs)
        {
            if (target == null) return;

            System.Type uiType = System.Type.GetType(assemblyQualifiedTypeName);
            if (uiType == null)
            {
                Debug.LogWarning($"[PlayerCastleInteriorBuilder] '{assemblyQualifiedTypeName}' 타입을 찾지 못함 (ProjectName.UI 어셈블리 미로드?). 상호작용 컴포넌트 없이 진행.");
                return;
            }

            Component component = target.AddComponent(uiType);
            if (component == null)
            {
                Debug.LogWarning($"[PlayerCastleInteriorBuilder] '{uiType.Name}' AddComponent 실패. 상호작용 컴포넌트 없이 진행.");
                return;
            }

            System.Reflection.MethodInfo configure = uiType.GetMethod("Configure");
            if (configure == null)
            {
                Debug.LogWarning($"[PlayerCastleInteriorBuilder] '{uiType.Name}'.Configure 메서드를 찾지 못함 — 기본값 상태로 부착만 진행.");
                return;
            }

            // MethodInfo.Invoke는 C# 선택적 매개변수 기본값을 채워주지 않으므로
            // 전달되지 않은 뒤쪽 인자를 선언된 기본값(예: maxSlots=20, interactRange=null)으로 채운다.
            System.Reflection.ParameterInfo[] parameters = configure.GetParameters();
            var invokeArgs = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i < configureArgs.Length && configureArgs[i] != null)
                {
                    invokeArgs[i] = configureArgs[i];
                }
                else if (parameters[i].HasDefaultValue)
                {
                    invokeArgs[i] = parameters[i].DefaultValue;
                }
                else
                {
                    invokeArgs[i] = parameters[i].ParameterType.IsValueType
                        ? System.Activator.CreateInstance(parameters[i].ParameterType)
                        : null;
                }
            }

            try
            {
                configure.Invoke(component, invokeArgs);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[PlayerCastleInteriorBuilder] '{uiType.Name}'.Configure 호출 실패: {ex.Message}");
            }
        }

        private static void SeedCookingIngredients(string kitchenWarehouseKey)
        {
            if (string.IsNullOrEmpty(kitchenWarehouseKey)) return;

            // WarehouseSystem is a persistent singleton and must be bootstrapped before the
            // interior builder runs. Never create it here: AddComponent invokes Awake and can
            // claim the production singleton even when the preview object is later disabled.
            WarehouseSystem warehouse = WarehouseSystem.Instance;
            if (warehouse == null)
            {
                Debug.LogWarning("[PlayerCastleInteriorBuilder] WarehouseSystem is not initialized; kitchen pantry seeding skipped.");
                return;
            }

            if (SeededCookingPantries.TryGetValue(kitchenWarehouseKey, out WarehouseSystem seededBy) &&
                seededBy == warehouse)
                return;

            var existing = warehouse.GetItems(kitchenWarehouseKey);

            // Seed one real modern recipe pair (dish_116) only into an empty pantry. Any
            // existing stock may be restored or partially consumed; never top it back up.
            // Herb_Yakcho is not resolved by CookingWindowUTK's RecipeCatalog category resolver.
            if (existing.Count == 0)
            {
                warehouse.AddItem(kitchenWarehouseKey, PlayerInventory.Fruit_Apple, 3);
                warehouse.AddItem(kitchenWarehouseKey,
                    RecipeCatalog.MonsterMeatItem("보통 육류", "토끼고기"), 3);
            }

            SeededCookingPantries[kitchenWarehouseKey] = warehouse;
        }

        /// <summary>
        /// 국가 스타일 → 영지 고유 키 매핑 (작업대/저장고 상호작용 설정용).
        /// 알 수 없는 스타일은 빌더 본문 switch에서 이미 경고하므로 여기서는 조용히 기본값 사용.
        /// </summary>
        private static string GetTerritoryKeyForNationStyle(string nationStyle)
        {
            switch (nationStyle?.ToLower())
            {
                case "eastern": return "East_01";
                case "western": return "West_01";
                case "southern": return "South_01";
                case "northern": return "North_01";
                case "empire": return "Empire_01";
                default: return "Empire_01";
            }
        }

        /// <summary>
        /// Adds shallow stone jambs and lintels to actual portals. Renderer-only meshes sit outside
        /// the clear opening and never participate in collision or movement.
        /// </summary>
        private static void AddNonCollidingPortalStonework(GameObject room, WallLayout[] walls,
            float wallHeight, float wallThickness, Shader shader)
        {
            if (room == null || walls == null || shader == null) return;
            var trimMaterial = new Material(shader) { name = "PlayerCastle_PortalStoneTrim" };
            trimMaterial.color = new Color(0.72f, 0.69f, 0.63f);
            if (trimMaterial.HasProperty("_Smoothness")) trimMaterial.SetFloat("_Smoothness", 0.12f);
            Mesh cubeMesh = CreateUnitCubeMesh();

            foreach (WallLayout wall in walls)
            {
                if (wall.DoorwayWidth <= 0f || wall.DoorwayHeight <= 0f) continue;
                Vector2 delta = wall.End - wall.Start;
                if (delta.sqrMagnitude <= 0.0001f) continue;
                float rotationY = Mathf.Atan2(-delta.y, delta.x) * Mathf.Rad2Deg;
                var frame = new GameObject(wall.Name + "_NonCollidingStoneFrame");
                frame.transform.SetParent(room.transform, false);
                frame.transform.localPosition = new Vector3((wall.Start.x + wall.End.x) * 0.5f,
                    0f, (wall.Start.y + wall.End.y) * 0.5f);
                frame.transform.localRotation = Quaternion.Euler(0f, rotationY, 0f);

                float faceOffset = wallThickness * 0.5f + 0.035f;
                for (int side = -1; side <= 1; side += 2)
                {
                    float z = side * faceOffset;
                    float left = wall.DoorwayOffset - wall.DoorwayWidth * 0.5f;
                    float right = wall.DoorwayOffset + wall.DoorwayWidth * 0.5f;
                    CreateRendererOnlyStoneBox(frame.transform, wall.Name + "_JambL_" + side,
                        new Vector3(left - 0.07f, wall.DoorwayHeight * 0.5f, z),
                        new Vector3(0.14f, wall.DoorwayHeight, 0.12f), cubeMesh, trimMaterial);
                    CreateRendererOnlyStoneBox(frame.transform, wall.Name + "_JambR_" + side,
                        new Vector3(right + 0.07f, wall.DoorwayHeight * 0.5f, z),
                        new Vector3(0.14f, wall.DoorwayHeight, 0.12f), cubeMesh, trimMaterial);
                    CreateRendererOnlyStoneBox(frame.transform, wall.Name + "_Lintel_" + side,
                        new Vector3(wall.DoorwayOffset, wall.DoorwayHeight + 0.075f, z),
                        new Vector3(wall.DoorwayWidth + 0.28f, 0.15f, 0.12f), cubeMesh, trimMaterial);
                }
            }
        }

        private static void CreateRendererOnlyStoneBox(Transform parent, string name, Vector3 position,
            Vector3 size, Mesh mesh, Material material)
        {
            var detail = new GameObject(name);
            detail.transform.SetParent(parent, false);
            detail.transform.localPosition = position;
            detail.transform.localScale = size;
            detail.AddComponent<MeshFilter>().sharedMesh = mesh;
            detail.AddComponent<MeshRenderer>().sharedMaterial = material;
            // No Collider by design: decorative portal stonework must not block movement.
        }

        private static Mesh CreateUnitCubeMesh()
        {
            var vertices = new List<Vector3>(24);
            var triangles = new List<int>(36);
            AddCubeFace(vertices, triangles, new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f), new Vector3(0.5f, 0.5f, -0.5f));
            AddCubeFace(vertices, triangles, new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f));
            AddCubeFace(vertices, triangles, new Vector3(-0.5f, -0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, 0.5f), new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f));
            AddCubeFace(vertices, triangles, new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f), new Vector3(0.5f, -0.5f, 0.5f), new Vector3(0.5f, 0.5f, 0.5f));
            AddCubeFace(vertices, triangles, new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, 0.5f, 0.5f), new Vector3(0.5f, 0.5f, -0.5f), new Vector3(0.5f, 0.5f, 0.5f));
            AddCubeFace(vertices, triangles, new Vector3(-0.5f, -0.5f, 0.5f),
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, -0.5f));
            var mesh = new Mesh { name = "PlayerCastle_RendererOnlyStoneTrimMesh" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddCubeFace(List<Vector3> vertices, List<int> triangles,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int first = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 1);
            triangles.Add(first + 2); triangles.Add(first + 3); triangles.Add(first + 1);
        }

        /// <summary>Cube 프리미티브 생성 + 재질 적용.</summary>
        private static GameObject CreateBoxPrimitive(GameObject parent, string name, Vector3 size,
            Vector3 localPosition, Material mat)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent.transform);
            box.transform.localPosition = localPosition;
            box.transform.localScale = size;

            var renderer = box.GetComponent<MeshRenderer>();
            if (renderer != null && mat != null)
            {
                renderer.sharedMaterial = mat;
            }
            return box;
        }

        /// <summary>Cylinder 프리미티브 생성 + 재질 적용 (기둥/깃대/통).</summary>
        private static GameObject CreateCylinderPrimitive(GameObject parent, string name, float radius,
            float height, Vector3 localPosition, Material mat)
        {
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            cylinder.transform.SetParent(parent.transform);
            cylinder.transform.localPosition = localPosition;
            // Unity Cylinder: scale.y = height * 0.5, scale.xz = radius * 2
            cylinder.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);

            var renderer = cylinder.GetComponent<MeshRenderer>();
            if (renderer != null && mat != null)
            {
                renderer.sharedMaterial = mat;
            }
            return cylinder;
        }

        /// <summary>
        /// 무기 스탠드 생성: 받침대 + 세움기둥 + 가로 바 + 걸린 무기(blade) 4개.
        /// rotationY로 벽을 따라 회전 배치.
        /// </summary>
        private static void CreateWeaponStand(GameObject parent, string name, Vector3 position,
            float rotationY, Material standMat, Material bladeMat)
        {
            GameObject stand = new GameObject(name);
            stand.transform.SetParent(parent.transform);
            stand.transform.localPosition = position;
            stand.transform.localRotation = Quaternion.Euler(0f, rotationY, 0f);

            // 받침대
            CreateBoxPrimitive(stand, "Base", new Vector3(0.9f, 0.1f, 0.5f),
                new Vector3(0f, 0.05f, 0f), standMat);

            // 세움 기둥 (Cylinder)
            CreateCylinderPrimitive(stand, "Pole", 0.04f, 1.6f,
                new Vector3(0f, 0.85f, 0f), standMat);

            // 가로 바 (무기 걸이)
            CreateBoxPrimitive(stand, "Crossbar", new Vector3(0.8f, 0.06f, 0.06f),
                new Vector3(0f, 1.55f, 0f), standMat);

            // 걸린 무기 4개 (얇은 세로 박스 — 검/창 느낌)
            for (int i = 0; i < 4; i++)
            {
                float xOffset = -0.3f + i * 0.2f;
                CreateBoxPrimitive(stand, $"Weapon_{i}", new Vector3(0.05f, 0.9f, 0.12f),
                    new Vector3(xOffset, 1.05f, 0f), bladeMat);

                // 칼등 가드
                CreateBoxPrimitive(stand, $"WeaponGuard_{i}", new Vector3(0.12f, 0.05f, 0.16f),
                    new Vector3(xOffset, 1.45f, 0f), standMat);
            }
        }

        /// <summary>NameplateDisplay 부착 (잠금문 없음 — 기능 안내 전용 라벨).</summary>
        private static void AddNameplate(GameObject target, string label)
        {
            if (target == null) return;

            var nameplate = target.AddComponent<NameplateDisplay>();
            nameplate.DisplayName = label;
        }
    }
}
