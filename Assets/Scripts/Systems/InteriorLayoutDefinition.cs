using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 플레이어 성 내부 단일 레이아웃의 불변(immutable) 정의 — 도면의 단일 소스.
    /// [2026-10-14 Phase 1 — 영지별 실내 레이아웃 다양화 계획]
    /// 외곽 치수, 로비 다각형, 벽 목록(포탈 개구부 포함), 의미 기반 방 이름(존),
    /// 포탈 연결 그래프, 기능 앵커 배치, 존 판정을 이 정의 하나에서 제공한다.
    /// PlayerCastleInteriorBuilder의 고정 토폴로지 API(GetLobbyVertices/GetTopologyWalls/
    /// GetTopologyZoneAt/GetPortalConnections/GetAnchorPosition/GetTopologyZoneNames)는
    /// 기준형인 KeepOctagon을 반환하는 호환 래퍼다. 모든 배열 접근자는 방어 복사본을
    /// 반환하므로 호출자가 정의를 오염시킬 수 없다. Phase 2(원형)/Phase 3(비대칭)은
    /// 이 클래스에 정의를 추가하는 방식으로만 확장하며, 좌표를 이중 관리하지 않는다.
    /// </summary>
    public sealed class InteriorLayoutDefinition
    {
        /// <summary>현행 팔각 기준형의 안정적 식별자. 저장/테스트/롤백 기준과 호환된다.</summary>
        public const string KeepOctagonLayoutId = "KeepOctagon";

        /// <summary>원형 중앙홀형(Phase 2 신규)의 안정적 식별자.</summary>
        public const string RoundKeepLayoutId = "RoundKeep";

        /// <summary>비대칭 저택형(Phase 3 신규)의 안정적 식별자.</summary>
        public const string AsymmetricManorLayoutId = "AsymmetricManor";

        private readonly Vector2[] _lobbyVertices;
        private readonly PlayerCastleInteriorBuilder.WallLayout[] _walls;
        private readonly string[] _zoneNames;
        private readonly Dictionary<string, Vector2> _anchors;
        private readonly ZoneRule[] _zoneRules;

        /// <summary>
        /// 기준형 — 현행 팔각 중앙 로비 성 내부. Phase 1 이전의 모든 좌표·벽·존·앵커
        /// 값을 그대로 보존한다(레이아웃 variant 0~7 가구 배치에는 영향 없음).
        /// </summary>
        public static InteriorLayoutDefinition KeepOctagon { get; } = CreateKeepOctagon();

        /// <summary>
        /// 원형 중앙홀형(Phase 2 신규). 런타임 배정(Phase 4) 전까지는 런타임 선택에 연결하지
        /// 않고, 정의/명시 API와 테스트에서만 소비한다. 팔각 기준형의 정적 래퍼는 그대로 유지.
        /// </summary>
        public static InteriorLayoutDefinition RoundKeep { get; } = CreateRoundKeep();

        /// <summary>
        /// 비대칭 저택형(Phase 3 신규). 런타임 배정(Phase 4) 전까지는 런타임 선택에 연결하지
        /// 않고, 정의/명시 API와 테스트에서만 소비한다. 기존 두 정의의 정적 래퍼는 그대로 유지.
        /// </summary>
        public static InteriorLayoutDefinition AsymmetricManor { get; } = CreateAsymmetricManor();

        /// <summary>레이아웃의 안정적 식별자(결정론 배정·세이브 호환용).</summary>
        public string LayoutId { get; }

        /// <summary>외곽 방 치수(방 로컬 XZ 미터, 단위 스케일 기준).</summary>
        public float RoomWidth { get; }
        public float RoomDepth { get; }
        public float RoomHeight { get; }
        public float WallThickness { get; }
        public float PortalWidth { get; }
        public float PortalHeight { get; }

        private InteriorLayoutDefinition(string layoutId,
            float roomWidth, float roomDepth, float roomHeight,
            float wallThickness, float portalWidth, float portalHeight,
            Vector2[] lobbyVertices, PlayerCastleInteriorBuilder.WallLayout[] walls,
            string[] zoneNames, KeyValuePair<string, Vector2>[] anchors,
            ZoneRule[] zoneRules)
        {
            LayoutId = layoutId;
            RoomWidth = roomWidth;
            RoomDepth = roomDepth;
            RoomHeight = roomHeight;
            WallThickness = wallThickness;
            PortalWidth = portalWidth;
            PortalHeight = portalHeight;
            // 생성 시점에도 방어 복사 — 이후 정의가 변하지 않는다.
            _lobbyVertices = (Vector2[])lobbyVertices.Clone();
            _walls = (PlayerCastleInteriorBuilder.WallLayout[])walls.Clone();
            _zoneNames = (string[])zoneNames.Clone();
            _anchors = new Dictionary<string, Vector2>(anchors.Length);
            foreach (KeyValuePair<string, Vector2> anchor in anchors)
                _anchors[anchor.Key] = anchor.Value;
            _zoneRules = (ZoneRule[])zoneRules.Clone();
        }

        /// <summary>등록된 레이아웃 정의를 안정적 id로 조회한다(Phase 4 배정 시 확장 지점).</summary>
        public static bool TryGetLayout(string layoutId, out InteriorLayoutDefinition definition)
        {
            switch (layoutId)
            {
                case KeepOctagonLayoutId:
                    definition = KeepOctagon;
                    return true;
                case RoundKeepLayoutId:
                    definition = RoundKeep;
                    return true;
                case AsymmetricManorLayoutId:
                    definition = AsymmetricManor;
                    return true;
                default:
                    definition = null;
                    return false;
            }
        }

        // [2026-10-14 Phase 4 — 런타임 결정론 배정] FNV-1a 32비트 안정 해시 상수.
        // string.GetHashCode는 실행(도메인)마다 값이 달라져 배정 키로 쓸 수 없고,
        // UnityEngine.Random/시간은 재방문 재현을 깨므로 전부 배제한다.
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

        /// <summary>
        /// [무번호 15 — 실내 구조 통일] 영지 키 무관 단일 레이아웃 — 모든 영지 성이 기준형 KeepOctagon
        /// (플레이어 소속 영지와 동일한 크기/벽/바닥/방 배치). 기존 FNV-1a 3분할 배정(KeepOctagon/
        /// RoundKeep/AsymmetricManor)은 폐기 — 사용자 지시 "모든 실내 영지가 나의 소속 영지와 동일한 구조".
        /// 결정론 계약은 유지(같은 키 → 항상 같은 정의 — 이제 전부 KeepOctagon). null/빈 키도 동일.
        /// RoundKeep/AsymmetricManor 정의와 TryGetLayout은 명시 API/테스트용으로 보존(런타임 미선택).
        /// </summary>
        public static InteriorLayoutDefinition SelectForTerritory(string territoryKey)
        {
            return KeepOctagon;
        }

        /// <summary>로비 다각형 정점(방 로컬 XZ 미터). 방어 복사본.</summary>
        public Vector2[] GetLobbyVertices() => (Vector2[])_lobbyVertices.Clone();

        /// <summary>이름 있는 벽 목록(외곽/로비/방 경계, 포탈 개구부 포함). 방어 복사본.</summary>
        public PlayerCastleInteriorBuilder.WallLayout[] GetWalls() =>
            (PlayerCastleInteriorBuilder.WallLayout[])_walls.Clone();

        /// <summary>의미 기반 방 이름(외곽/로비/입구홀 제외 기능실 존). 방어 복사본.</summary>
        public string[] GetZoneNames() => (string[])_zoneNames.Clone();

        /// <summary>
        /// 실제 벽 개구부에서 유도한 포탈 연결 그래프. 벽 목록과 1:1 대응하며,
        /// 두 공간(From/To)을 연결한다. 호출마다 새 배열을 돌려준다(방어 복사).
        /// </summary>
        public PlayerCastleInteriorBuilder.PortalConnection[] GetPortalConnections()
        {
            var portals = new List<PlayerCastleInteriorBuilder.PortalConnection>();
            foreach (PlayerCastleInteriorBuilder.WallLayout wall in _walls)
                if (wall.DoorwayWidth > 0f && !string.IsNullOrEmpty(wall.ConnectedFrom) &&
                    !string.IsNullOrEmpty(wall.ConnectedTo))
                    portals.Add(new PlayerCastleInteriorBuilder.PortalConnection(
                        wall.Name, wall.ConnectedFrom, wall.ConnectedTo));
            return portals.ToArray();
        }

        /// <summary>기능 앵커의 방 로컬 XZ 배치. 정의에 없는 이름은 Vector2.zero(기존 계약).</summary>
        public Vector2 GetAnchorPosition(string anchorName) =>
            _anchors.TryGetValue(anchorName, out Vector2 position) ? position : Vector2.zero;

        /// <summary>정의에 앵커가 있는지 구분해서 확인한다(Phase 2+ 새 레이아웃용).</summary>
        public bool TryGetAnchor(string anchorName, out Vector2 position) =>
            _anchors.TryGetValue(anchorName, out position);

        /// <summary>
        /// 최종 방 로컬 XZ 좌표가 놓인 방 이름. 외곽 밖이면 "Exterior",
        /// 어느 방에도 속하지 않으면 null(기존 GetTopologyZoneAt 계약).
        /// </summary>
        public string GetZoneAt(Vector2 point)
        {
            if (Mathf.Abs(point.x) > RoomWidth * 0.5f || Mathf.Abs(point.y) > RoomDepth * 0.5f)
                return "Exterior";
            if (IsInsideLobby(point)) return "Lobby";
            foreach (ZoneRule rule in _zoneRules)
                if (rule.Match(point)) return rule.Zone;
            return null;
        }

        /// <summary>로비 다각형 내부 판정(홀짝 사선 교차 알고리즘, 기존 IsInsideLobby와 동일).</summary>
        public bool IsInsideLobby(Vector2 point)
        {
            bool inside = false;
            for (int i = 0, j = _lobbyVertices.Length - 1; i < _lobbyVertices.Length; j = i++)
            {
                Vector2 a = _lobbyVertices[i], b = _lobbyVertices[j];
                bool crosses = (a.y > point.y) != (b.y > point.y) &&
                    point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x;
                if (crosses) inside = !inside;
            }
            return inside;
        }

        /// <summary>순서가 중요한 존 판정 규칙(선언 순서대로 평가되며, 첫 일치가 결과).</summary>
        private readonly struct ZoneRule
        {
            public readonly string Zone;
            public readonly Func<Vector2, bool> Match;

            public ZoneRule(string zone, Func<Vector2, bool> match)
            {
                Zone = zone;
                Match = match;
            }
        }

        /// <summary>
        /// 현행 팔각 기준형을 구성한다. 모든 좌표·포탈 파라미터·존 규칙은 Phase 1 이전
        /// PlayerCastleInteriorBuilder의 고정값과 동일하다(출력 불변 회귀 기준).
        /// </summary>
        private static InteriorLayoutDefinition CreateKeepOctagon()
        {
            Vector2[] lobbyVertices =
            {
                new Vector2(-18f, 14f), new Vector2(18f, 14f), new Vector2(30f, 8f), new Vector2(30f, -8f),
                new Vector2(18f, -14f), new Vector2(-18f, -14f), new Vector2(-30f, -8f), new Vector2(-30f, 8f)
            };

            // 외곽 치수/포탈 규격 — 기존 PlayerCastleInteriorBuilder 공개 상수와 동일 값.
            // (테스트가 두 표면의 일치를 단정하므로 어긋나면 즉시 적발된다.)
            const float roomWidth = 117.6f, roomDepth = 88.2f, roomHeight = 6f;
            const float portalWidth = 3.2f, portalHeight = 2.8f;

            float x = roomWidth * 0.5f, z = roomDepth * 0.5f;
            Vector2[] lobby = lobbyVertices;
            PlayerCastleInteriorBuilder.WallLayout[] walls =
            {
                // Solid outer shell; the south entry opens into the dedicated central entry hall.
                Wall("Exterior_North", new Vector2(-x, z), new Vector2(x, z)),
                Wall("Exterior_West", new Vector2(-x, -z), new Vector2(-x, z)),
                Wall("Exterior_East", new Vector2(x, -z), new Vector2(x, z)),
                Wall("Entrance_South", new Vector2(-x, -z), new Vector2(x, -z), 4f, portalHeight, 0f, "EntryHall", "Exterior"),

                // Actual octagon openings: N Bedroom; NW/W Craft; NE/E Storage; SW Barracks; SE Alchemy;
                // S is the entry hall. No portal directly joins two named rooms.
                Wall("Lobby_North", lobby[0], lobby[1], portalWidth, portalHeight, 0f, "Lobby", "Bedroom"),
                Wall("Lobby_NorthEast", lobby[1], lobby[2], portalWidth, portalHeight, 0f, "Lobby", "Storage"),
                Wall("Lobby_East", lobby[2], lobby[3], portalWidth, portalHeight, 0f, "Lobby", "Storage"),
                Wall("Lobby_SouthEast", lobby[3], lobby[4], portalWidth, portalHeight, 0f, "Lobby", "Alchemy"),
                Wall("Lobby_South", lobby[4], lobby[5], 4f, portalHeight, 0f, "Lobby", "EntryHall"),
                Wall("Lobby_SouthWest", lobby[5], lobby[6], portalWidth, portalHeight, 0f, "Lobby", "Barracks"),
                Wall("Lobby_West", lobby[6], lobby[7], portalWidth, portalHeight, 0f, "Lobby", "Craft"),
                Wall("Lobby_NorthWest", lobby[7], lobby[0], portalWidth, portalHeight, 0f, "Lobby", "Craft"),

                // N bedroom and uninterrupted NW/W and NE/E wings (each pair is one connected room).
                Wall("Bedroom_West", new Vector2(-18f, 14f), new Vector2(-18f, z)),
                Wall("Bedroom_East", new Vector2(18f, 14f), new Vector2(18f, z)),
                Wall("Craft_NorthBoundary", new Vector2(-x, 8f), new Vector2(-30f, 8f)),
                Wall("Craft_SouthBoundary", new Vector2(-x, -8f), new Vector2(-30f, -8f)),
                Wall("Storage_NorthBoundary", new Vector2(30f, 8f), new Vector2(x, 8f)),
                Wall("Storage_SouthBoundary", new Vector2(30f, -8f), new Vector2(x, -8f)),

                // South rooms are separated by a walkable 4 m entry corridor from lobby to exterior.
                Wall("EntryHall_West", new Vector2(-2f, -14f), new Vector2(-2f, -z)),
                Wall("EntryHall_East", new Vector2(2f, -14f), new Vector2(2f, -z)),
                Wall("EntryHall_NorthWest", new Vector2(-18f, -14f), new Vector2(-2f, -14f)),
                Wall("EntryHall_NorthEast", new Vector2(2f, -14f), new Vector2(18f, -14f)),
                // The lobby's SW/SE portal already defines the south room entrances. Extra vertical
                // dividers here would sit directly behind those portals and seal the quadrant anchors.
                // South rooms remain separated by the continuous entry-hall walls at x=±2.
            };

            string[] zoneNames = { "Bedroom", "Craft", "Storage", "Barracks", "Alchemy" };

            KeyValuePair<string, Vector2>[] anchors =
            {
                new KeyValuePair<string, Vector2>("Workbench", new Vector2(-40f, 25f)),
                new KeyValuePair<string, Vector2>("StorageShelf_2", new Vector2(40f, 25f)),
                new KeyValuePair<string, Vector2>("WeaponStand_0", new Vector2(-40f, -25f)),
                new KeyValuePair<string, Vector2>("AlchemyTable", new Vector2(40f, -25f)),
                new KeyValuePair<string, Vector2>("LordBed", new Vector2(0f, 29f)),
                new KeyValuePair<string, Vector2>("CookingTable", new Vector2(40f, 34f)),
                new KeyValuePair<string, Vector2>("CookingIngredientWarehouse", new Vector2(45.5f, 34f)),
            };

            // 존 판정 경계는 로비 다각형 자체에서 유도한다(정점 인덱스 순서 고정:
            // [0]=(-18,14) [1]=(18,14) [2]=(30,8) — 팔각형 정의와 존 규칙이 한 소스에서 일치).
            float northHalfWidth = Mathf.Abs(lobby[1].x);   // 18 — 로비 북변 절반 폭
            float lobbyTopY = lobby[0].y;                   // 14 — 로비 북단 y
            float sideHalfWidth = lobby[2].x;               // 30 — 로비 측면(동/서) x
            float wingMidY = lobby[2].y;                    // 8  — 동/서 날개 경계 y
            const float entryCorridorHalfWidth = 2.1f;      // 진입 회랑 x=±2 벽 + 0.1 여유

            ZoneRule[] zoneRules =
            {
                // 기존 GetTopologyZoneAt의 평가 순서를 그대로 보존한다(첫 일치가 결과).
                new ZoneRule("Bedroom", p => Mathf.Abs(p.x) < northHalfWidth && p.y > lobbyTopY),
                new ZoneRule("Craft", p => p.x < -northHalfWidth && p.y > wingMidY),
                new ZoneRule("Craft", p => p.x < -sideHalfWidth && p.y >= -wingMidY && p.y <= wingMidY),
                new ZoneRule("Storage", p => p.x > northHalfWidth && p.y > wingMidY),
                new ZoneRule("Storage", p => p.x > sideHalfWidth && p.y >= -wingMidY && p.y <= wingMidY),
                new ZoneRule("Barracks", p => p.x < -entryCorridorHalfWidth && p.y < -lobbyTopY),
                new ZoneRule("Alchemy", p => p.x > entryCorridorHalfWidth && p.y < -lobbyTopY),
                new ZoneRule("EntryHall", p => Mathf.Abs(p.x) <= entryCorridorHalfWidth && p.y < -lobbyTopY),
            };

            return new InteriorLayoutDefinition(KeepOctagonLayoutId,
                roomWidth: roomWidth, roomDepth: roomDepth, roomHeight: roomHeight,
                wallThickness: 0.45f, portalWidth: portalWidth, portalHeight: portalHeight,
                lobbyVertices, walls, zoneNames, anchors, zoneRules);
        }

        /// <summary>
        /// 원형 중앙홀형(Phase 2). 외곽 셸·포탈 규격·존 이름은 KeepOctagon 계약을 공유하고,
        /// 로비만 반지름 20 m의 16세그먼트 원형 링으로 교체한다. 링 변 중심각은 22.5°k
        /// 이므로 45° 간격 의미 포탈 8개가 모두 링 변 중심에 놓인다. 링 밖은 6개 방사
        /// 스포크(67.5°,112.5°,202.5°,247.5°,292.5°,337.5°)로 나눈 부채꼴 존이며,
        /// 각도 존 규칙이 내부 전체를 던진다.
        /// </summary>
        private static InteriorLayoutDefinition CreateRoundKeep()
        {
            // 외곽 치수/포탈 규격 — KeepOctagon과 동일 값(테스트가 두 정의의 일치를 단정).
            const float roomWidth = 117.6f, roomDepth = 88.2f, roomHeight = 6f;
            const float portalWidth = 3.2f, portalHeight = 2.8f;
            const float ringRadius = 20f;
            const float vertexStepDegrees = 22.5f;   // 16세그먼트 링의 정점 각 간격
            const float firstVertexDegrees = -11.25f; // 변 중심이 22.5°k에 오도록 하는 오프셋

            float x = roomWidth * 0.5f, z = roomDepth * 0.5f;
            float apothem = ringRadius * Mathf.Cos(Mathf.PI / 16f);

            // 16개 링 정점: 각 -11.25° + 22.5°k (원점 중심, 반지름 20).
            Vector2[] lobbyVertices = new Vector2[16];
            for (int i = 0; i < 16; i++)
            {
                float angleDegrees = firstVertexDegrees + vertexStepDegrees * i;
                float radians = angleDegrees * Mathf.Deg2Rad;
                lobbyVertices[i] = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * ringRadius;
            }

            var wallList = new List<PlayerCastleInteriorBuilder.WallLayout>
            {
                // Solid outer shell; the south entry opens into the dedicated central entry hall.
                Wall("Exterior_North", new Vector2(-x, z), new Vector2(x, z)),
                Wall("Exterior_West", new Vector2(-x, -z), new Vector2(-x, z)),
                Wall("Exterior_East", new Vector2(x, -z), new Vector2(x, z)),
                Wall("Entrance_South", new Vector2(-x, -z), new Vector2(x, -z), 4f, portalHeight, 0f, "EntryHall", "Exterior"),
            };

            // 링 변 중심각이 45° 배수(±0.01°)에 맞는 변만 의미 포탈로 선택한다.
            for (int i = 0; i < 16; i++)
            {
                Vector2 start = lobbyVertices[i];
                Vector2 end = lobbyVertices[(i + 1) % 16];
                Vector2 midpoint = (start + end) * 0.5f;
                float midAngle = NormalizeAngleDegrees(
                    Mathf.Atan2(midpoint.y, midpoint.x) * Mathf.Rad2Deg);
                float nearest45 = Mathf.Round(midAngle / 45f) * 45f;
                bool isPortal = Mathf.Abs(Mathf.DeltaAngle(midAngle, nearest45)) < 0.01f;

                if (!isPortal)
                {
                    wallList.Add(Wall($"Lobby_Ring_{i}", start, end));
                    continue;
                }

                float key = NormalizeAngleDegrees(nearest45);
                string portalName;
                string connectedTo;
                float doorwayWidth = portalWidth;
                switch (key)
                {
                    case 0f:
                        portalName = "Lobby_East"; connectedTo = "Storage"; break;
                    case 45f:
                        portalName = "Lobby_NorthEast"; connectedTo = "Storage"; break;
                    case 90f:
                        portalName = "Lobby_North"; connectedTo = "Bedroom"; break;
                    case 135f:
                        portalName = "Lobby_NorthWest"; connectedTo = "Craft"; break;
                    case 180f:
                        portalName = "Lobby_West"; connectedTo = "Craft"; break;
                    case 225f:
                        portalName = "Lobby_SouthWest"; connectedTo = "Barracks"; break;
                    case 270f:
                        portalName = "Lobby_South"; connectedTo = "EntryHall";
                        doorwayWidth = 4f; break;
                    default: // 315f
                        portalName = "Lobby_SouthEast"; connectedTo = "Alchemy"; break;
                }
                wallList.Add(Wall(portalName, start, end, doorwayWidth, portalHeight, 0f, "Lobby", connectedTo));
            }

            // 6개 방사 스포크: 링 변 중심(아포팀)에서 같은 방사선을 따라 외곽 셸까지.
            float[] spokeAngles = { 67.5f, 112.5f, 202.5f, 247.5f, 292.5f, 337.5f };
            foreach (float spokeAngle in spokeAngles)
            {
                float radians = spokeAngle * Mathf.Deg2Rad;
                float cos = Mathf.Cos(radians), sin = Mathf.Sin(radians);
                Vector2 start = new Vector2(cos, sin) * apothem;
                // 근사분모 가드 후 사각형 경계까지의 방사 거리 t를 구한다.
                const float epsilon = 1e-5f;
                float tx = Mathf.Abs(cos) < epsilon ? float.MaxValue : x / Mathf.Abs(cos);
                float tz = Mathf.Abs(sin) < epsilon ? float.MaxValue : z / Mathf.Abs(sin);
                float t = Mathf.Min(tx, tz);
                Vector2 end = new Vector2(cos, sin) * t;
                wallList.Add(Wall("Spoke_" + spokeAngle.ToString(
                    System.Globalization.CultureInfo.InvariantCulture), start, end));
            }

            string[] zoneNames = { "Bedroom", "Craft", "Storage", "Barracks", "Alchemy" };

            KeyValuePair<string, Vector2>[] anchors =
            {
                new KeyValuePair<string, Vector2>("Workbench", new Vector2(-40f, 25f)),
                new KeyValuePair<string, Vector2>("StorageShelf_2", new Vector2(40f, 25f)),
                new KeyValuePair<string, Vector2>("WeaponStand_0", new Vector2(-40f, -25f)),
                new KeyValuePair<string, Vector2>("AlchemyTable", new Vector2(40f, -25f)),
                new KeyValuePair<string, Vector2>("LordBed", new Vector2(0f, 29f)),
                new KeyValuePair<string, Vector2>("CookingTable", new Vector2(40f, 34f)),
                new KeyValuePair<string, Vector2>("CookingIngredientWarehouse", new Vector2(45.5f, 34f)),
            };

            // 링 밖 부채꼴 존: 정규화 방위각 [0,360)을 6개 경계 스포크 각으로 나눈다.
            // (IsInsideLobby가 먼저 평가되므로 이 규칙은 링 밖 영역만 담당한다.)
            ZoneRule[] zoneRules =
            {
                new ZoneRule("Storage", p =>
                {
                    float a = NormalizeAngleDegrees(Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg);
                    return a >= 337.5f || a < 67.5f;
                }),
                new ZoneRule("Bedroom", p =>
                {
                    float a = NormalizeAngleDegrees(Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg);
                    return a >= 67.5f && a < 112.5f;
                }),
                new ZoneRule("Craft", p =>
                {
                    float a = NormalizeAngleDegrees(Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg);
                    return a >= 112.5f && a < 202.5f;
                }),
                new ZoneRule("Barracks", p =>
                {
                    float a = NormalizeAngleDegrees(Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg);
                    return a >= 202.5f && a < 247.5f;
                }),
                new ZoneRule("EntryHall", p =>
                {
                    float a = NormalizeAngleDegrees(Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg);
                    return a >= 247.5f && a < 292.5f;
                }),
                new ZoneRule("Alchemy", p =>
                {
                    float a = NormalizeAngleDegrees(Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg);
                    return a >= 292.5f && a < 337.5f;
                }),
            };

            return new InteriorLayoutDefinition(RoundKeepLayoutId,
                roomWidth: roomWidth, roomDepth: roomDepth, roomHeight: roomHeight,
                wallThickness: 0.45f, portalWidth: portalWidth, portalHeight: portalHeight,
                lobbyVertices, wallList.ToArray(), zoneNames, anchors, zoneRules);
        }

        /// <summary>
        /// 비대칭 저택형(Phase 3). 외곽 셸·포탈 규격·존 이름은 KeepOctagon 계약을 공유하고,
        /// 로비는 동측으로 밀린 8정점 비대칭 팔각형(정점 평균 중심 약 (3.75,1.25))이다. 남측
        /// 입구홀 축도 x=3으로 동편 이동해 로비 남 포탈 개구부(x 1..5)와 외곽 출입구 개구부
        /// (벽 중심 +3 오프셋, x 1..5)가 같은 축에서 만난다. 서측 공방 날개(깊이 36.8 m)가
        /// 동측 창고 날개(28.8 m)보다 깊고, 로비 하단 변(x -10..16)이 상단 변(x -10..18)보다
        /// 서쪽에 치우쳐 실루엣 자체가 비대칭이다. 로비 밖 존 규칙은 날개 경계·침실 벽·회랑
        /// 벽에 정렬되며 로비 밖 내부 전체를 던진다.
        /// </summary>
        private static InteriorLayoutDefinition CreateAsymmetricManor()
        {
            // 외곽 치수/포탈 규격 — KeepOctagon과 동일 값(테스트가 두 정의의 일치를 단정).
            const float roomWidth = 117.6f, roomDepth = 88.2f, roomHeight = 6f;
            const float portalWidth = 3.2f, portalHeight = 2.8f;
            const float entryHallCenterX = 3f;          // 동편으로 밀린 남측 입구홀 축
            const float entryCorridorHalfWidth = 2.1f;  // 진입 회랑 벽 x=0.9/5.1 + 판정 여유

            float x = roomWidth * 0.5f, z = roomDepth * 0.5f;
            Vector2[] lobby =
            {
                new Vector2(-10f, 19f), new Vector2(18f, 19f), new Vector2(30f, 10f), new Vector2(30f, -8f),
                new Vector2(16f, -16f), new Vector2(-10f, -16f), new Vector2(-22f, -8f), new Vector2(-22f, 10f)
            };

            // 존 판정 경계는 로비 다각형 자체에서 유도한다(정점 인덱스 순서 고정:
            // [0]=(-10,19) [1]=(18,19) [2]=(30,10) [3]=(30,-8) [4]=(16,-16) [5]=(-10,-16)
            // [6]=(-22,-8) [7]=(-22,10) — 비대칭 팔각형 정의와 존 규칙이 한 소스에서 일치).
            float lobbyTopY = lobby[0].y;        // 19 — 로비 북단 y
            float bedroomWestX = lobby[0].x;     // -10 — 북측 침실 서벽 x
            float bedroomEastX = lobby[1].x;     // 18 — 북측 침실 동벽 x
            float lobbyEastX = lobby[2].x;       // 30 — 로비 동벽 x
            float wingMidY = lobby[2].y;         // 10 — 동/서 날개 상단 경계 y
            float wingSouthY = lobby[3].y;       // -8 — 동/서 날개 하단 경계 y
            float lobbyBottomY = lobby[4].y;     // -16 — 로비 남단 y
            float lobbyWestX = lobby[6].x;       // -22 — 로비 서벽 x
            float entryHallWestBoundary = entryHallCenterX - entryCorridorHalfWidth;  // 0.9
            float entryHallEastBoundary = entryHallCenterX + entryCorridorHalfWidth;  // 5.1

            PlayerCastleInteriorBuilder.WallLayout[] walls =
            {
                // Solid outer shell; the south entry opens east of centre, into the shifted entry hall.
                Wall("Exterior_North", new Vector2(-x, z), new Vector2(x, z)),
                Wall("Exterior_West", new Vector2(-x, -z), new Vector2(-x, z)),
                Wall("Exterior_East", new Vector2(x, -z), new Vector2(x, z)),
                Wall("Entrance_South", new Vector2(-x, -z), new Vector2(x, -z), 4f, portalHeight,
                    entryHallCenterX, "EntryHall", "Exterior"),

                // Asymmetric octagon openings: N Bedroom; NW/W Craft; NE/E Storage; SW Barracks;
                // SE is Alchemy; S is the east-shifted entry hall. No portal joins two named rooms.
                Wall("Lobby_North", lobby[0], lobby[1], portalWidth, portalHeight, 0f, "Lobby", "Bedroom"),
                Wall("Lobby_NorthEast", lobby[1], lobby[2], portalWidth, portalHeight, 0f, "Lobby", "Storage"),
                Wall("Lobby_East", lobby[2], lobby[3], portalWidth, portalHeight, 0f, "Lobby", "Storage"),
                Wall("Lobby_SouthEast", lobby[3], lobby[4], portalWidth, portalHeight, 0f, "Lobby", "Alchemy"),
                Wall("Lobby_South", lobby[4], lobby[5], 4f, portalHeight, 0f, "Lobby", "EntryHall"),
                Wall("Lobby_SouthWest", lobby[5], lobby[6], portalWidth, portalHeight, 0f, "Lobby", "Barracks"),
                Wall("Lobby_West", lobby[6], lobby[7], portalWidth, portalHeight, 0f, "Lobby", "Craft"),
                Wall("Lobby_NorthWest", lobby[7], lobby[0], portalWidth, portalHeight, 0f, "Lobby", "Craft"),

                // North bedroom chamber between x=-10 and x=18 above the lobby's north edge.
                Wall("Bedroom_West", new Vector2(bedroomWestX, lobbyTopY), new Vector2(bedroomWestX, z)),
                Wall("Bedroom_East", new Vector2(bedroomEastX, lobbyTopY), new Vector2(bedroomEastX, z)),

                // Wing boundaries meet the lobby's side-wall corners. The west craft wing runs
                // 36.8 m to the shell versus 28.8 m east — a deliberate depth asymmetry.
                Wall("Craft_NorthBoundary", new Vector2(-x, wingMidY), new Vector2(lobbyWestX, wingMidY)),
                Wall("Craft_SouthBoundary", new Vector2(-x, wingSouthY), new Vector2(lobbyWestX, wingSouthY)),
                Wall("Storage_NorthBoundary", new Vector2(lobbyEastX, wingMidY), new Vector2(x, wingMidY)),
                Wall("Storage_SouthBoundary", new Vector2(lobbyEastX, wingSouthY), new Vector2(x, wingSouthY)),

                // South rooms are separated by the 4.2 m entry corridor on axis x=3. The lobby's
                // south doorway (x 1..5) and the offset exterior entrance (x 1..5) both align with it.
                Wall("EntryHall_West", new Vector2(entryHallCenterX - entryCorridorHalfWidth, lobbyBottomY),
                    new Vector2(entryHallCenterX - entryCorridorHalfWidth, -z)),
                Wall("EntryHall_East", new Vector2(entryHallCenterX + entryCorridorHalfWidth, lobbyBottomY),
                    new Vector2(entryHallCenterX + entryCorridorHalfWidth, -z)),
            };

            string[] zoneNames = { "Bedroom", "Craft", "Storage", "Barracks", "Alchemy" };

            KeyValuePair<string, Vector2>[] anchors =
            {
                // 좌우 날개 앵커의 x 크기를 일부러 다르게 둔다(기준형의 대칭 ±40 배치와 구별).
                new KeyValuePair<string, Vector2>("Workbench", new Vector2(-36f, 24f)),
                new KeyValuePair<string, Vector2>("StorageShelf_2", new Vector2(44f, 26f)),
                new KeyValuePair<string, Vector2>("WeaponStand_0", new Vector2(-34f, -26f)),
                new KeyValuePair<string, Vector2>("AlchemyTable", new Vector2(40f, -28f)),
                new KeyValuePair<string, Vector2>("LordBed", new Vector2(4f, 30f)),
                new KeyValuePair<string, Vector2>("CookingTable", new Vector2(44f, 4f)),
                new KeyValuePair<string, Vector2>("CookingIngredientWarehouse", new Vector2(52f, 4f)),
            };

            // 로비 밖 존 규칙(선언 순서대로 평가, 첫 일치가 결과): 날개 경계와 침실 벽·회랑
            // 벽에 정렬되며, IsInsideLobby가 먼저 평가되므로 이 규칙은 로비 밖 영역만 담당한다.
            ZoneRule[] zoneRules =
            {
                // 북측 침실 챔버: 로비 북변 위, 침실 벽 x=-10/18 사이.
                new ZoneRule("Bedroom", p => p.y > lobbyTopY && p.x >= bedroomWestX && p.x <= bedroomEastX),
                // 북서/서측 공방: 침실 서벽 서쪽 + 로비 서벽 서쪽 날개.
                new ZoneRule("Craft", p => p.x < bedroomWestX && p.y > wingMidY),
                new ZoneRule("Craft", p => p.x < lobbyWestX && p.y >= wingSouthY && p.y <= wingMidY),
                // 북동/동측 창고: 침실 동벽 동쪽 + 로비 동벽 동쪽 날개.
                new ZoneRule("Storage", p => p.x > bedroomEastX && p.y > wingMidY),
                new ZoneRule("Storage", p => p.x > lobbyEastX && p.y >= wingSouthY && p.y <= wingMidY),
                // 남서 병영 / 남동 연금술: 남측은 회랑 축 x=3을 기준으로 갈라진다.
                new ZoneRule("Barracks", p => p.x < entryHallWestBoundary && p.y < wingSouthY),
                new ZoneRule("Alchemy", p => p.x > entryHallEastBoundary && p.y < wingSouthY),
                // 남중 입구홀: 회랑 벽 x=0.9/5.1 사이, 로비 남단에서 남측 셸까지.
                new ZoneRule("EntryHall", p => Mathf.Abs(p.x - entryHallCenterX) <= entryCorridorHalfWidth
                    && p.y < lobbyBottomY),
            };

            return new InteriorLayoutDefinition(AsymmetricManorLayoutId,
                roomWidth: roomWidth, roomDepth: roomDepth, roomHeight: roomHeight,
                wallThickness: 0.45f, portalWidth: portalWidth, portalHeight: portalHeight,
                lobby, walls, zoneNames, anchors, zoneRules);
        }

        /// <summary>방위각을 [0,360)으로 정규화한다(음각/360 초과 처리).</summary>
        private static float NormalizeAngleDegrees(float angleDegrees)
        {
            float normalized = angleDegrees % 360f;
            if (normalized < 0f) normalized += 360f;
            return normalized;
        }

        /// <summary>팩토리 내 벽 리터럴 보조(기존 로컬 함수 S와 동일 시그니처).</summary>
        private static PlayerCastleInteriorBuilder.WallLayout Wall(string name, Vector2 start, Vector2 end,
            float doorwayWidth = 0f, float doorwayHeight = 0f, float doorwayOffset = 0f,
            string connectedFrom = null, string connectedTo = null)
        {
            return new PlayerCastleInteriorBuilder.WallLayout(name, start, end, doorwayWidth,
                doorwayHeight, doorwayOffset, connectedFrom, connectedTo);
        }
    }
}
