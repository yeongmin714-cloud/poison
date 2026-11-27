using System.Collections.Generic;
using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>Pure topology assertions: no scene build, Unity compiler launch, or Play-mode dependency.</summary>
    public sealed class PlayerCastleInteriorTopologyTests
    {
        [Test]
        public void Footprint_IsSixTimesTheExistingArea_AndUsesUnitScale()
        {
            float area = PlayerCastleInteriorBuilder.RoomWidth * PlayerCastleInteriorBuilder.RoomDepth;
            float baselineArea = PlayerCastleInteriorBuilder.ExistingRoomWidth * PlayerCastleInteriorBuilder.ExistingRoomDepth;
            // The footprint is intended to be 6x the existing area; the unchanged dimensions (117.6 x 88.2 vs. 48 x 36) yield 2.45^2 = 6.0025.
            Assert.That(area / baselineArea, Is.EqualTo(6.0025f).Within(0.0001f));
            Assert.That(PlayerCastleInteriorBuilder.RoomWidth / PlayerCastleInteriorBuilder.RoomDepth,
                Is.EqualTo(4f / 3f).Within(0.0001f));
            Assert.That(PlayerCastleInteriorBuilder.RoomHeight, Is.EqualTo(6f));
        }

        [Test]
        public void RuntimeZoneMapping_PlacesAllRequiredQuadrantsAndEntryHall()
        {
            Assert.That(PlayerCastleInteriorBuilder.GetTopologyZoneAt(new Vector2(0f, 24f)), Is.EqualTo("Bedroom"));
            Assert.That(PlayerCastleInteriorBuilder.GetTopologyZoneAt(new Vector2(-42f, 24f)), Is.EqualTo("Craft"));
            Assert.That(PlayerCastleInteriorBuilder.GetTopologyZoneAt(new Vector2(42f, 24f)), Is.EqualTo("Storage"));
            Assert.That(PlayerCastleInteriorBuilder.GetTopologyZoneAt(new Vector2(-42f, -28f)), Is.EqualTo("Barracks"));
            Assert.That(PlayerCastleInteriorBuilder.GetTopologyZoneAt(new Vector2(42f, -28f)), Is.EqualTo("Alchemy"));
            Assert.That(PlayerCastleInteriorBuilder.GetTopologyZoneAt(Vector2.zero), Is.EqualTo("Lobby"));
            Assert.That(PlayerCastleInteriorBuilder.GetTopologyZoneAt(new Vector2(0f, -30f)), Is.EqualTo("EntryHall"));
        }

        [Test]
        public void RequiredAnchors_ArePlacedInTheirCorrectRoomZones_AtWorldRoomScale()
        {
            Assert.That(PlayerCastleInteriorBuilder.GetAnchorPosition("Workbench"), Is.EqualTo(new Vector2(-40f, 25f)));
            Assert.That(PlayerCastleInteriorBuilder.GetAnchorPosition("StorageShelf_2"), Is.EqualTo(new Vector2(40f, 25f)));
            Assert.That(PlayerCastleInteriorBuilder.GetAnchorPosition("WeaponStand_0"), Is.EqualTo(new Vector2(-40f, -25f)));
            Assert.That(PlayerCastleInteriorBuilder.GetAnchorPosition("AlchemyTable"), Is.EqualTo(new Vector2(40f, -25f)));
            Assert.That(PlayerCastleInteriorBuilder.GetAnchorPosition("LordBed"), Is.EqualTo(new Vector2(0f, 29f)));
            Assert.That(PlayerCastleInteriorBuilder.GetTopologyZoneAt(PlayerCastleInteriorBuilder.GetAnchorPosition("Workbench")), Is.EqualTo("Craft"));
            Assert.That(PlayerCastleInteriorBuilder.GetTopologyZoneAt(PlayerCastleInteriorBuilder.GetAnchorPosition("StorageShelf_2")), Is.EqualTo("Storage"));
            Assert.That(PlayerCastleInteriorBuilder.GetTopologyZoneAt(PlayerCastleInteriorBuilder.GetAnchorPosition("WeaponStand_0")), Is.EqualTo("Barracks"));
            Assert.That(PlayerCastleInteriorBuilder.GetTopologyZoneAt(PlayerCastleInteriorBuilder.GetAnchorPosition("AlchemyTable")), Is.EqualTo("Alchemy"));
            Assert.That(PlayerCastleInteriorBuilder.GetTopologyZoneAt(PlayerCastleInteriorBuilder.GetAnchorPosition("LordBed")), Is.EqualTo("Bedroom"));
        }

        [Test]
        public void PortalGraph_IsDerivedFromActualWallOpeningsAndMatchesRequiredWallFaces()
        {
            PlayerCastleInteriorBuilder.WallLayout[] walls = PlayerCastleInteriorBuilder.GetTopologyWalls();
            PlayerCastleInteriorBuilder.PortalConnection[] portals = PlayerCastleInteriorBuilder.GetPortalConnections();
            var portalWalls = new HashSet<string>();
            foreach (var portal in portals)
            {
                PlayerCastleInteriorBuilder.WallLayout matching = FindWall(walls, portal.WallName);
                Assert.That(matching.DoorwayWidth, Is.GreaterThan(0f), portal.WallName + " must have a real opening");
                Assert.That(matching.DoorwayHeight, Is.GreaterThanOrEqualTo(2f), portal.WallName + " headroom");
                Assert.That(matching.ConnectedFrom, Is.EqualTo(portal.From));
                Assert.That(matching.ConnectedTo, Is.EqualTo(portal.To));
                Assert.That(portalWalls.Add(portal.WallName), Is.True, "One portal graph edge per actual wall opening");
                Assert.That(portal.From == "Lobby" && portal.To == "Lobby", Is.False);
                bool touchesExterior = portal.From == "Exterior" || portal.To == "Exterior";
                Assert.That(touchesExterior, Is.EqualTo(portal.WallName == "Entrance_South"),
                    portal.WallName + " must be the sole exterior connection");
            }

            AssertPortal(portals, "Entrance_South", "EntryHall", "Exterior");
            AssertPortal(portals, "Lobby_North", "Lobby", "Bedroom");
            AssertPortal(portals, "Lobby_NorthWest", "Lobby", "Craft");
            AssertPortal(portals, "Lobby_West", "Lobby", "Craft");
            AssertPortal(portals, "Lobby_NorthEast", "Lobby", "Storage");
            AssertPortal(portals, "Lobby_East", "Lobby", "Storage");
            AssertPortal(portals, "Lobby_SouthWest", "Lobby", "Barracks");
            AssertPortal(portals, "Lobby_SouthEast", "Lobby", "Alchemy");
            AssertPortal(portals, "Lobby_South", "Lobby", "EntryHall");

            var nodes = new HashSet<string>(PlayerCastleInteriorBuilder.GetTopologyZoneNames());
            nodes.Add("Lobby"); nodes.Add("EntryHall"); nodes.Add("Exterior");
            Assert.That(portals.Length, Is.EqualTo(portalWalls.Count));
            Assert.That(portals.Length, Is.EqualTo(9), "No portal graph edges may be invented without a wall opening.");
            foreach (var portal in portals)
            {
                Assert.That(nodes.Contains(portal.From), Is.True, "Unknown portal source " + portal.From);
                Assert.That(nodes.Contains(portal.To), Is.True, "Unknown portal target " + portal.To);
            }
            var adjacency = new Dictionary<string, List<string>>();
            foreach (string node in nodes) adjacency[node] = new List<string>();
            foreach (var portal in portals)
            {
                adjacency[portal.From].Add(portal.To);
                adjacency[portal.To].Add(portal.From);
            }
            var visited = new HashSet<string> { "Exterior" };
            var pending = new Queue<string>(); pending.Enqueue("Exterior");
            while (pending.Count > 0)
                foreach (string next in adjacency[pending.Dequeue()])
                    if (visited.Add(next)) pending.Enqueue(next);
            CollectionAssert.AreEquivalent(nodes, visited, "All zones must be physically reachable through represented openings.");
        }

        [Test]
        public void Walls_AreNonDegenerateAndPortalsHaveRealWallRemainders()
        {
            PlayerCastleInteriorBuilder.WallLayout[] walls = PlayerCastleInteriorBuilder.GetTopologyWalls();
            Assert.That(walls, Is.Not.Empty);
            var names = new HashSet<string>();
            foreach (var wall in walls)
            {
                Assert.That(names.Add(wall.Name), Is.True, "Wall names must be unique: " + wall.Name);
                float length = Vector2.Distance(wall.Start, wall.End);
                Assert.That(length, Is.GreaterThan(0f), wall.Name);
                if (wall.DoorwayWidth <= 0f)
                {
                    Assert.That(wall.DoorwayHeight, Is.EqualTo(0f), wall.Name);
                    Assert.That(wall.ConnectedFrom, Is.Null, wall.Name + " cannot claim a portal connection");
                    Assert.That(wall.ConnectedTo, Is.Null, wall.Name + " cannot claim a portal connection");
                    continue;
                }
                Assert.That(length, Is.GreaterThan(wall.DoorwayWidth), wall.Name);
                Assert.That(wall.DoorwayWidth, Is.GreaterThanOrEqualTo(1.8f), wall.Name + " controller clearance");
                Assert.That(wall.DoorwayHeight, Is.GreaterThanOrEqualTo(2f), wall.Name + " capsule headroom");
                Assert.That(wall.DoorwayHeight, Is.LessThan(PlayerCastleInteriorBuilder.RoomHeight), wall.Name);
                Assert.That(Mathf.Abs(wall.DoorwayOffset) + wall.DoorwayWidth * 0.5f,
                    Is.LessThanOrEqualTo(length * 0.5f), wall.Name + " opening must lie within wall");
                Assert.That(string.IsNullOrEmpty(wall.ConnectedFrom), Is.False, wall.Name);
                Assert.That(string.IsNullOrEmpty(wall.ConnectedTo), Is.False, wall.Name);
            }

            // Non-portal boundaries block direct room-room movement, while each ring connection is a wall cut.
            Assert.That(FindWall(walls, "Bedroom_West").DoorwayWidth, Is.EqualTo(0f));
            Assert.That(FindWall(walls, "Craft_NorthBoundary").DoorwayWidth, Is.EqualTo(0f));
            Assert.That(FindWall(walls, "Storage_SouthBoundary").DoorwayWidth, Is.EqualTo(0f));
            Assert.That(FindWall(walls, "EntryHall_West").DoorwayWidth, Is.EqualTo(0f));
        }

        [Test]
        public void BuiltInterior_UsesFinalScale_HasCollidableWalls_AndKeepsAnchorsInsideZones()
        {
            // [122차] 빈 실내 규칙 기본값(FurnishStations=false)에서 시설 앵커 검사는 임시 구성으로 수행.
            PlayerCastleInteriorBuilder.FurnishStations = true;
            GameObject room = PlayerCastleInteriorBuilder.BuildPlayerCastleInterior("Empire", 0);
            Assert.That(room, Is.Not.Null);
            try
            {
                Assert.That(room.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(room.transform.Find("Floor"), Is.Not.Null);
                // [수조 통일 2026-11-06] 천장 부재 계약으로 진화(구: Is.Not.Null) —
                //   IndoorDioramaKit.Apply가 빌더 return 직전 Ceiling을 제거(9/20 실내씬 예시 기준).
                Assert.That(room.transform.Find("Ceiling"), Is.Null, "[수조 통일] Ceiling 부재 — IndoorDioramaKit 천장 제거 계약");
                foreach (var wall in PlayerCastleInteriorBuilder.GetTopologyWalls())
                {
                    Transform wallTransform = room.transform.Find(wall.Name);
                    Assert.That(wallTransform, Is.Not.Null, "Missing runtime wall " + wall.Name);
                    MeshFilter filter = wallTransform.GetComponent<MeshFilter>();
                    MeshCollider collider = wallTransform.GetComponent<MeshCollider>();
                    Assert.That(filter, Is.Not.Null, wall.Name);
                    Assert.That(filter.sharedMesh, Is.Not.Null, wall.Name);
                    Assert.That(collider, Is.Not.Null, wall.Name);
                    Assert.That(collider.sharedMesh, Is.SameAs(filter.sharedMesh), wall.Name);
                }

                AssertAnchor(room, "Workbench", "Craft");
                AssertAnchor(room, "StorageShelf_2", "Storage");
                AssertAnchor(room, "WeaponStand_0", "Barracks");
                AssertAnchor(room, "AlchemyTable", "Alchemy");
                AssertAnchor(room, "LordBed", "Bedroom");
                AssertAnchor(room, "CookingTable", "Storage");

                Transform workbench = room.transform.Find("Workbench");
                Assert.That(workbench.Find("WorkbenchAnvil"), Is.Not.Null,
                    "Nested workbench contents must remain parented to its interaction anchor.");
                Assert.That(workbench.Find("WorkbenchTool_1"), Is.Not.Null);

                MeshRenderer floorRenderer = room.transform.Find("Floor").GetComponent<MeshRenderer>();
                Assert.That(floorRenderer.sharedMaterial, Is.Not.Null);
                if (ProjectName.Core.IndoorTextureLoader.Floor != null)
                    Assert.That(floorRenderer.sharedMaterial.mainTexture,
                        Is.SameAs(ProjectName.Core.IndoorTextureLoader.Floor), "Provided floor texture must be applied.");
                if (ProjectName.Core.IndoorTextureLoader.WallStone != null)
                {
                    MeshRenderer wallRenderer = room.transform.Find("Lobby_North").GetComponent<MeshRenderer>();
                    Assert.That(wallRenderer.sharedMaterial.mainTexture,
                        Is.Not.SameAs(ProjectName.Core.IndoorTextureLoader.WallStone),
                        "Topology masonry must use the dedicated calm, seam-free ashlar tile.");
                    Assert.That(wallRenderer.sharedMaterial.mainTexture.name,
                        Is.EqualTo("wall_plaster_upper"));   // [2026-10-09 Phase D] 벽 전면 회반죽(사용자 제안 확정) — 절차 ashlar에서 제공 텍스처로 전환
                    Assert.That(wallRenderer.sharedMaterial.mainTextureScale, Is.EqualTo(Vector2.one),
                        "Topology UVs encode physical wall tiling; the material must not tile a second time.");
                    Assert.That(wallRenderer.sharedMaterial.IsKeywordEnabled("_NORMALMAP"), Is.False,
                        "Noisy supplied wall normal must not be used on the player-castle topology shell.");
                }
            }
            finally
            {
                Object.DestroyImmediate(room);
                PlayerCastleInteriorBuilder.FurnishStations = false;   // [122차] 기본값 복원
            }
        }

        [Test]
        public void EmptyInterior_Rule_FurnishStationsDefaultFalse_KeepsBedsOnly()
        {
            // [122차] 빈 실내 규칙: 신규 점령 영지는 시설(저장고/무기고/작업대/요리/연금) 없이 시작.
            //   침대·병사관리 상호판넬 등 생존/운영 최소 시설은 항상 구성. 플레이어가 직접 배치.
            Assert.That(PlayerCastleInteriorBuilder.FurnishStations, Is.False,
                "Default must follow the [122차] empty-interior rule (FurnishStations=false).");
            GameObject room = PlayerCastleInteriorBuilder.BuildPlayerCastleInterior("Empire", 0);
            Assert.That(room, Is.Not.Null);
            try
            {
                string[] absentStations =
                {
                    "Workbench", "StorageShelf_2", "WeaponStand_0", "AlchemyTable",
                    "CookingTable", "CookingIngredientWarehouse"
                };
                foreach (string name in absentStations)
                    Assert.That(room.transform.Find(name), Is.Null,
                        $"[122차] 빈 실내 규칙 위반: '{name}'는 FurnishStations=false에서 구성되어선 안 됩니다.");

                string[] keptEssentials = { "LordBed" };
                foreach (string name in keptEssentials)
                    Assert.That(room.transform.Find(name), Is.Not.Null,
                        $"[122차] 최소 시설 '{name}'는 빈 실내 규칙에서도 항상 구성되어야 합니다.");
            }
            finally
            {
                Object.DestroyImmediate(room);
            }
        }

        [Test]
        public void RuntimePortalOpenings_AreNotBlockedByTheirOwnWallColliders()
        {
            GameObject room = PlayerCastleInteriorBuilder.BuildPlayerCastleInterior("Empire", 0);
            Assert.That(room, Is.Not.Null);
            try
            {
                var walls = PlayerCastleInteriorBuilder.GetTopologyWalls();
                foreach (var portal in PlayerCastleInteriorBuilder.GetPortalConnections())
                {
                    var wall = FindWall(walls, portal.WallName);
                    Vector2 center = (wall.Start + wall.End) * 0.5f;
                    Transform wallTransform = room.transform.Find(portal.WallName);
                    MeshCollider collider = wallTransform.GetComponent<MeshCollider>();
                    Vector3 point = new Vector3(center.x, 1.2f, center.y);
                    var ray = new Ray(point - wallTransform.forward, wallTransform.forward);
                    Assert.That(collider.Raycast(ray, out _, 2f), Is.False,
                        portal.WallName + " opening must be a physical gap at the advertised center.");
                }
            }
            finally
            {
                Object.DestroyImmediate(room);
            }
        }

        // ===================================================================
        // [2026-10-14 Phase 1 — 레이아웃 정의 추출] 현행 팔각 기준형(KeepOctagon)을
        //   InteriorLayoutDefinition 단일 소스로 추출한 회귀 테스트.
        //   - 정의 id는 "KeepOctagon"으로 고정
        //   - 노출 배열(로비 정점/벽/포탈/존 이름)은 모두 방어 복사본
        //   - 기존 8 정점·벽/포탈 수·앵커 좌표·존 판정은 절대 불변
        // ===================================================================
        [Test]
        public void KeepOctagonDefinition_HasStableId_AndMatchesBuilderTopology()
        {
            InteriorLayoutDefinition definition = InteriorLayoutDefinition.KeepOctagon;
            Assert.That(definition.LayoutId, Is.EqualTo("KeepOctagon"));

            // 현행 팔각 로비: 정확히 이 8개 정점(방 로컬 XZ 미터).
            Vector2[] expectedLobby =
            {
                new Vector2(-18f, 14f), new Vector2(18f, 14f), new Vector2(30f, 8f), new Vector2(30f, -8f),
                new Vector2(18f, -14f), new Vector2(-18f, -14f), new Vector2(-30f, -8f), new Vector2(-30f, 8f)
            };
            Assert.That(definition.GetLobbyVertices(), Is.EqualTo(expectedLobby));
            Assert.That(PlayerCastleInteriorBuilder.GetLobbyVertices(), Is.EqualTo(expectedLobby));

            // 벽/포탈 수는 현행 기준선 유지(외곽 4 + 로비 8 + 침실 2 + 날개 경계 4 + 입구홀 4 = 22).
            Assert.That(definition.GetWalls().Length, Is.EqualTo(22));
            Assert.That(PlayerCastleInteriorBuilder.GetTopologyWalls().Length, Is.EqualTo(22));
            Assert.That(definition.GetPortalConnections().Length, Is.EqualTo(9));
            Assert.That(PlayerCastleInteriorBuilder.GetPortalConnections().Length, Is.EqualTo(9));

            // 빌더 호환 래퍼는 정의의 데이터를 그대로 반환해야 한다.
            Assert.That(PlayerCastleInteriorBuilder.GetTopologyWalls(), Is.EqualTo(definition.GetWalls()));
            Assert.That(PlayerCastleInteriorBuilder.GetPortalConnections(), Is.EqualTo(definition.GetPortalConnections()));
            Assert.That(PlayerCastleInteriorBuilder.GetTopologyZoneNames(), Is.EqualTo(definition.GetZoneNames()));

            // 외곽 치수는 공개 상수 기준선과 동일해야 한다.
            Assert.That(definition.RoomWidth, Is.EqualTo(PlayerCastleInteriorBuilder.RoomWidth));
            Assert.That(definition.RoomDepth, Is.EqualTo(PlayerCastleInteriorBuilder.RoomDepth));
            Assert.That(definition.RoomHeight, Is.EqualTo(PlayerCastleInteriorBuilder.RoomHeight));
            Assert.That(definition.WallThickness, Is.EqualTo(PlayerCastleInteriorBuilder.WallThickness));
            Assert.That(definition.PortalWidth, Is.EqualTo(PlayerCastleInteriorBuilder.PortalWidth));
            Assert.That(definition.PortalHeight, Is.EqualTo(PlayerCastleInteriorBuilder.PortalHeight));

            // 앵커 좌표 불변(정의/래퍼 두 표면 모두).
            string[] anchorNames =
            {
                "Workbench", "StorageShelf_2", "WeaponStand_0", "AlchemyTable",
                "LordBed", "CookingTable", "CookingIngredientWarehouse"
            };
            foreach (string anchorName in anchorNames)
                Assert.That(definition.GetAnchorPosition(anchorName),
                    Is.EqualTo(PlayerCastleInteriorBuilder.GetAnchorPosition(anchorName)), anchorName);

            // 존 판정 불변(정의/래퍼 두 표면이 모든 탐침점에서 동일 결과).
            Vector2[] probes =
            {
                new Vector2(0f, 24f), new Vector2(-42f, 24f), new Vector2(42f, 24f),
                new Vector2(-42f, -28f), new Vector2(42f, -28f), Vector2.zero,
                new Vector2(0f, -30f), new Vector2(0f, -18f), new Vector2(0f, 15f),
                new Vector2(-25f, 10f), new Vector2(25f, -10f), new Vector2(0f, -40f),
                new Vector2(-1.5f, -25f), new Vector2(1.5f, -25f), new Vector2(200f, 0f)
            };
            foreach (Vector2 probe in probes)
                Assert.That(definition.GetZoneAt(probe),
                    Is.EqualTo(PlayerCastleInteriorBuilder.GetTopologyZoneAt(probe)), $"probe {probe}");
        }

        [Test]
        public void LayoutDefinition_ExposedArrays_AreDefensiveCopies()
        {
            InteriorLayoutDefinition definition = InteriorLayoutDefinition.KeepOctagon;

            Vector2[] lobby = definition.GetLobbyVertices();
            Assert.That(lobby.Length, Is.EqualTo(8));
            lobby[0] = new Vector2(999f, 999f);
            Assert.That(definition.GetLobbyVertices()[0], Is.EqualTo(new Vector2(-18f, 14f)), "lobby vertices");

            PlayerCastleInteriorBuilder.WallLayout[] walls = definition.GetWalls();
            walls[0] = default;
            Assert.That(definition.GetWalls()[0].Name, Is.EqualTo("Exterior_North"), "walls");

            PlayerCastleInteriorBuilder.PortalConnection[] portals = definition.GetPortalConnections();
            portals[0] = default;
            Assert.That(definition.GetPortalConnections()[0].WallName, Is.EqualTo("Entrance_South"), "portals");

            string[] zoneNames = definition.GetZoneNames();
            zoneNames[0] = "Tampered";
            Assert.That(definition.GetZoneNames()[0], Is.EqualTo("Bedroom"), "zone names");

            // 고정 빌더 래퍼(GetTopologyWalls 등)도 방어 복사본을 유지해야 한다.
            PlayerCastleInteriorBuilder.WallLayout[] builderWalls = PlayerCastleInteriorBuilder.GetTopologyWalls();
            builderWalls[0] = default;
            Assert.That(PlayerCastleInteriorBuilder.GetTopologyWalls()[0].Name, Is.EqualTo("Exterior_North"));

            Vector2[] builderLobby = PlayerCastleInteriorBuilder.GetLobbyVertices();
            builderLobby[0] = new Vector2(999f, 999f);
            Assert.That(PlayerCastleInteriorBuilder.GetLobbyVertices()[0], Is.EqualTo(new Vector2(-18f, 14f)));

            string[] builderZones = PlayerCastleInteriorBuilder.GetTopologyZoneNames();
            builderZones[0] = "Tampered";
            Assert.That(PlayerCastleInteriorBuilder.GetTopologyZoneNames()[0], Is.EqualTo("Bedroom"));

            PlayerCastleInteriorBuilder.PortalConnection[] builderPortals =
                PlayerCastleInteriorBuilder.GetPortalConnections();
            builderPortals[0] = default;
            Assert.That(PlayerCastleInteriorBuilder.GetPortalConnections()[0].WallName, Is.EqualTo("Entrance_South"));
        }

        // ===================================================================
        // [2026-10-14 Phase 2 — 원형 중앙홀형(RoundKeep)] 정의 추가 TDD.
        //   - 런타임 선택/배정(Phase 4)에는 연결하지 않는다: 정의·명시 API·토폴로지 계약만 검증.
        //   - 로비는 16세그먼트 원형(반지름 20 m, 정점각 11.25°+22.5°k)이고 변 중심각이
        //     22.5°k 이므로 8개 의미 포탈(45° 간격)이 모두 링 변 중심에 놓인다.
        //   - 외곽 치수·포탈 규격·필수 앵커 이름·의미 방 이름은 KeepOctagon 계약을 공유한다.
        //   - 방은 링 밖을 6개 방사 스포크로 나눈 부채꼴이며 각도 존 규칙이 내부 전체를 덮는다.
        // ===================================================================
        [Test]
        public void RoundKeepDefinition_HasStableId_AndIsResolvableViaTryGetLayout()
        {
            InteriorLayoutDefinition roundKeep = InteriorLayoutDefinition.RoundKeep;
            Assert.That(roundKeep.LayoutId, Is.EqualTo("RoundKeep"));

            Assert.That(InteriorLayoutDefinition.TryGetLayout("RoundKeep", out InteriorLayoutDefinition resolved), Is.True);
            Assert.That(resolved, Is.SameAs(roundKeep));

            // 기준형 조회 계약은 그대로 유지된다(기존 호출자 보호).
            Assert.That(InteriorLayoutDefinition.TryGetLayout("KeepOctagon", out InteriorLayoutDefinition octagon), Is.True);
            Assert.That(octagon, Is.SameAs(InteriorLayoutDefinition.KeepOctagon));
            Assert.That(InteriorLayoutDefinition.TryGetLayout("NoSuchLayout", out InteriorLayoutDefinition none), Is.False);
            Assert.That(none, Is.Null);
        }

        [Test]
        public void RoundKeep_Lobby_IsSixteenSegmentCircle_DistinctFromKeepOctagon()
        {
            InteriorLayoutDefinition roundKeep = InteriorLayoutDefinition.RoundKeep;
            Vector2[] lobby = roundKeep.GetLobbyVertices();

            // 원형 로비는 12~16개 벽 구간 요구를 16변으로 충족한다.
            Assert.That(lobby.Length, Is.InRange(12, 16));

            // 모든 정점이 반지름 20 m 원 위에 있고 각도 간격이 균등하다(닫힌 균등 링).
            foreach (Vector2 vertex in lobby)
                Assert.That(vertex.magnitude, Is.EqualTo(20f).Within(0.01f), vertex.ToString());
            for (int i = 0; i < lobby.Length; i++)
            {
                float delta = Mathf.DeltaAngle(NormalizedAngleDegrees(lobby[i]),
                    NormalizedAngleDegrees(lobby[(i + 1) % lobby.Length]));
                Assert.That(delta, Is.EqualTo(360f / lobby.Length).Within(0.01f), $"vertex {i}");
            }

            // 원형 실루엣: 정점 반경과 변 중심 반경(아포팀)의 편차가 작다 — 팔각형의 큰 요철과 구별.
            Assert.That(LobbyRadialSpread(lobby), Is.LessThan(1f), "원형 로비 실루엣");
            Assert.That(LobbyRadialSpread(InteriorLayoutDefinition.KeepOctagon.GetLobbyVertices()),
                Is.GreaterThan(5f), "팔각 기준형은 의도적으로 큰 요철을 가진다");

            // 실루엣 구별: 정점 수와 로비 외접 사각형 크기가 모두 다르다.
            Assert.That(InteriorLayoutDefinition.KeepOctagon.GetLobbyVertices().Length,
                Is.Not.EqualTo(lobby.Length));
            Assert.That(LobbyExtents(InteriorLayoutDefinition.KeepOctagon.GetLobbyVertices()),
                Is.Not.EqualTo(LobbyExtents(lobby)));

            // 노출 배열은 방어 복사본(정의 오염 금지)이다.
            lobby[0] = new Vector2(999f, 999f);
            Assert.That(roundKeep.GetLobbyVertices()[0].magnitude, Is.EqualTo(20f).Within(0.01f), "방어 복사본");
        }

        [Test]
        public void RoundKeep_Walls_AreUniqueNonDegenerate_AndPortalsAreConsistent()
        {
            InteriorLayoutDefinition roundKeep = InteriorLayoutDefinition.RoundKeep;
            PlayerCastleInteriorBuilder.WallLayout[] walls = roundKeep.GetWalls();
            Assert.That(walls.Length, Is.EqualTo(26), "외곽 4 + 링 16 + 방사 스포크 6");

            var names = new HashSet<string>();
            foreach (PlayerCastleInteriorBuilder.WallLayout wall in walls)
            {
                Assert.That(names.Add(wall.Name), Is.True, "Wall names must be unique: " + wall.Name);
                float length = Vector2.Distance(wall.Start, wall.End);
                Assert.That(length, Is.GreaterThan(0f), wall.Name);
                if (wall.DoorwayWidth <= 0f)
                {
                    Assert.That(wall.DoorwayHeight, Is.EqualTo(0f), wall.Name);
                    Assert.That(wall.ConnectedFrom, Is.Null, wall.Name + " cannot claim a portal connection");
                    Assert.That(wall.ConnectedTo, Is.Null, wall.Name + " cannot claim a portal connection");
                    continue;
                }
                Assert.That(length, Is.GreaterThan(wall.DoorwayWidth), wall.Name);
                Assert.That(wall.DoorwayWidth, Is.GreaterThanOrEqualTo(1.8f), wall.Name + " controller clearance");
                Assert.That(wall.DoorwayHeight, Is.GreaterThanOrEqualTo(2f), wall.Name + " capsule headroom");
                Assert.That(wall.DoorwayHeight, Is.LessThan(roundKeep.RoomHeight), wall.Name);
                Assert.That(Mathf.Abs(wall.DoorwayOffset) + wall.DoorwayWidth * 0.5f,
                    Is.LessThanOrEqualTo(length * 0.5f), wall.Name + " opening must lie within wall");
                Assert.That(string.IsNullOrEmpty(wall.ConnectedFrom), Is.False, wall.Name);
                Assert.That(string.IsNullOrEmpty(wall.ConnectedTo), Is.False, wall.Name);
            }

            // 포탈 그래프는 실제 벽 개구부에서 1:1 유도되며 외부 연결은 남측 입구 하나뿐이다.
            PlayerCastleInteriorBuilder.PortalConnection[] portals = roundKeep.GetPortalConnections();
            Assert.That(portals.Length, Is.EqualTo(9), "남측 입구 1 + 링 의미 포탈 8");
            var portalWalls = new HashSet<string>();
            foreach (PlayerCastleInteriorBuilder.PortalConnection portal in portals)
            {
                PlayerCastleInteriorBuilder.WallLayout matching = FindWall(walls, portal.WallName);
                Assert.That(matching.DoorwayWidth, Is.GreaterThan(0f), portal.WallName + " must have a real opening");
                Assert.That(matching.ConnectedFrom, Is.EqualTo(portal.From));
                Assert.That(matching.ConnectedTo, Is.EqualTo(portal.To));
                Assert.That(portalWalls.Add(portal.WallName), Is.True, "One portal graph edge per actual wall opening");
                Assert.That(portal.From == "Lobby" && portal.To == "Lobby", Is.False);
                bool touchesExterior = portal.From == "Exterior" || portal.To == "Exterior";
                Assert.That(touchesExterior, Is.EqualTo(portal.WallName == "Entrance_South"),
                    portal.WallName + " must be the sole exterior connection");
            }
        }

        [Test]
        public void RoundKeep_PortalGraph_ReachesEveryRoomFromExterior()
        {
            InteriorLayoutDefinition roundKeep = InteriorLayoutDefinition.RoundKeep;
            PlayerCastleInteriorBuilder.PortalConnection[] portals = roundKeep.GetPortalConnections();

            // 팔각 기준형과 동일한 의미 연결(8방 링 포탈 + 남측 입구)을 유지한다.
            AssertPortal(portals, "Entrance_South", "EntryHall", "Exterior");
            AssertPortal(portals, "Lobby_North", "Lobby", "Bedroom");
            AssertPortal(portals, "Lobby_NorthEast", "Lobby", "Storage");
            AssertPortal(portals, "Lobby_East", "Lobby", "Storage");
            AssertPortal(portals, "Lobby_SouthEast", "Lobby", "Alchemy");
            AssertPortal(portals, "Lobby_South", "Lobby", "EntryHall");
            AssertPortal(portals, "Lobby_SouthWest", "Lobby", "Barracks");
            AssertPortal(portals, "Lobby_West", "Lobby", "Craft");
            AssertPortal(portals, "Lobby_NorthWest", "Lobby", "Craft");

            var nodes = new HashSet<string>(roundKeep.GetZoneNames());
            nodes.Add("Lobby"); nodes.Add("EntryHall"); nodes.Add("Exterior");
            var adjacency = new Dictionary<string, List<string>>();
            foreach (string node in nodes) adjacency[node] = new List<string>();
            foreach (PlayerCastleInteriorBuilder.PortalConnection portal in portals)
            {
                Assert.That(nodes.Contains(portal.From), Is.True, "Unknown portal source " + portal.From);
                Assert.That(nodes.Contains(portal.To), Is.True, "Unknown portal target " + portal.To);
                adjacency[portal.From].Add(portal.To);
                adjacency[portal.To].Add(portal.From);
            }
            var visited = new HashSet<string> { "Exterior" };
            var pending = new Queue<string>(); pending.Enqueue("Exterior");
            while (pending.Count > 0)
                foreach (string next in adjacency[pending.Dequeue()])
                    if (visited.Add(next)) pending.Enqueue(next);
            CollectionAssert.AreEquivalent(nodes, visited,
                "모든 방이 실제 개구부 그래프로 외부에서 도달 가능해야 한다.");
        }

        [Test]
        public void RoundKeep_ZonesTileInterior_AndRequiredAnchorsSitInCorrectRooms()
        {
            InteriorLayoutDefinition roundKeep = InteriorLayoutDefinition.RoundKeep;

            Assert.That(roundKeep.GetZoneAt(Vector2.zero), Is.EqualTo("Lobby"));
            Assert.That(roundKeep.GetZoneAt(new Vector2(0f, 19f)), Is.EqualTo("Lobby"));
            Assert.That(roundKeep.GetZoneAt(new Vector2(0f, 21f)), Is.EqualTo("Bedroom"));
            Assert.That(roundKeep.GetZoneAt(new Vector2(-40f, 25f)), Is.EqualTo("Craft"));
            Assert.That(roundKeep.GetZoneAt(new Vector2(40f, 25f)), Is.EqualTo("Storage"));
            Assert.That(roundKeep.GetZoneAt(new Vector2(-28f, -30f)), Is.EqualTo("Barracks"));
            Assert.That(roundKeep.GetZoneAt(new Vector2(28f, -30f)), Is.EqualTo("Alchemy"));
            Assert.That(roundKeep.GetZoneAt(new Vector2(0f, -30f)), Is.EqualTo("EntryHall"));
            Assert.That(roundKeep.GetZoneAt(new Vector2(200f, 0f)), Is.EqualTo("Exterior"));

            // 남측 진입 동선: 로비 남 포탈 바깥에서 외곽 입구까지 EntryHall 이다.
            for (float y = -21f; y >= -43f; y -= 1f)
                Assert.That(roundKeep.GetZoneAt(new Vector2(0f, y)), Is.EqualTo("EntryHall"), $"y={y}");

            // 부채꼴 존 규칙이 내부 전체를 던진다 — 사각(널) 구역 없음.
            for (float px = -55f; px <= 55f; px += 5.5f)
                for (float py = -41f; py <= 41f; py += 4.1f)
                    Assert.That(roundKeep.GetZoneAt(new Vector2(px, py)), Is.Not.Null,
                        $"probe ({px},{py})");

            // 필수 앵커는 정의에서 공급되며 올바른 방 존 안(로비 밖)에 놓인다.
            string[] anchorNames =
            {
                "Workbench", "StorageShelf_2", "WeaponStand_0", "AlchemyTable",
                "LordBed", "CookingTable", "CookingIngredientWarehouse",
            };
            string[] anchorZones = { "Craft", "Storage", "Barracks", "Alchemy", "Bedroom", "Storage", "Storage" };
            for (int i = 0; i < anchorNames.Length; i++)
            {
                Assert.That(roundKeep.TryGetAnchor(anchorNames[i], out Vector2 position), Is.True, anchorNames[i]);
                Assert.That(roundKeep.GetZoneAt(position), Is.EqualTo(anchorZones[i]), anchorNames[i]);
                Assert.That(roundKeep.IsInsideLobby(position), Is.False,
                    anchorNames[i] + " must sit in its room, not the lobby");
                Assert.That(Mathf.Abs(position.x), Is.LessThan(roundKeep.RoomWidth * 0.5f), anchorNames[i]);
                Assert.That(Mathf.Abs(position.y), Is.LessThan(roundKeep.RoomDepth * 0.5f), anchorNames[i]);
            }
            Assert.That(roundKeep.TryGetAnchor("NoSuchAnchor", out _), Is.False);
        }

        [Test]
        public void RoundKeep_RingAndSpokes_FormSixteenSegmentCircleWithSpacedPortals()
        {
            InteriorLayoutDefinition roundKeep = InteriorLayoutDefinition.RoundKeep;
            PlayerCastleInteriorBuilder.WallLayout[] walls = roundKeep.GetWalls();
            float apothem = 20f * Mathf.Cos(Mathf.PI / 16f);   // 링 변 중심 반경 ≈ 19.6157

            var lobbyPortalNames = new HashSet<string>
            {
                "Lobby_East", "Lobby_NorthEast", "Lobby_North", "Lobby_NorthWest",
                "Lobby_West", "Lobby_SouthWest", "Lobby_South", "Lobby_SouthEast",
            };

            int ringWallCount = 0, solidRingCount = 0, spokeCount = 0;
            var portalMidAngles = new List<float>();
            foreach (PlayerCastleInteriorBuilder.WallLayout wall in walls)
            {
                bool isLobbyPortal = lobbyPortalNames.Contains(wall.Name);
                if (isLobbyPortal || wall.Name.StartsWith("Lobby_Ring_"))
                {
                    ringWallCount++;
                    // 링 벽은 반지름 20 m 원 위의 두 정점을 잇는 코드(chord)다.
                    Assert.That(wall.Start.magnitude, Is.EqualTo(20f).Within(0.01f), wall.Name);
                    Assert.That(wall.End.magnitude, Is.EqualTo(20f).Within(0.01f), wall.Name);
                    if (isLobbyPortal)
                    {
                        Assert.That(wall.DoorwayOffset, Is.EqualTo(0f).Within(0.001f),
                            wall.Name + " 개구부는 링 변 중심에 있다");
                        Vector2 openingCenter = (wall.Start + wall.End) * 0.5f;
                        Assert.That(openingCenter.magnitude, Is.EqualTo(apothem).Within(0.01f),
                            wall.Name + " 개구부 중심은 링 변 위에 있다");
                        portalMidAngles.Add(NormalizedAngleDegrees(openingCenter));
                    }
                    else
                    {
                        solidRingCount++;
                        Assert.That(wall.DoorwayWidth, Is.EqualTo(0f), wall.Name);
                    }
                    continue;
                }
                if (wall.Name.StartsWith("Spoke_"))
                {
                    spokeCount++;
                    Assert.That(wall.DoorwayWidth, Is.EqualTo(0f), wall.Name + " 스포크는 무개구 방사 벽");
                    Assert.That(wall.Start.magnitude, Is.EqualTo(apothem).Within(0.05f),
                        wall.Name + " 스포크는 링 변 중심에서 시작한다");
                    // 스포크는 원점에서 나온 방사선 위에 있다(시작·끝 방위각 동일).
                    Assert.That(NormalizedAngleDegrees(wall.Start),
                        Is.EqualTo(NormalizedAngleDegrees(wall.End)).Within(0.01f), wall.Name);
                    // 스포크 끝은 외곽 셸(±58.8 / ±44.1)에 닿는다.
                    bool touchesShell =
                        Mathf.Abs(Mathf.Abs(wall.End.x) - roundKeep.RoomWidth * 0.5f) < 0.01f ||
                        Mathf.Abs(Mathf.Abs(wall.End.y) - roundKeep.RoomDepth * 0.5f) < 0.01f;
                    Assert.That(touchesShell, Is.True, wall.Name + " 스포크는 외곽 셸까지 이른다");
                }
            }

            Assert.That(ringWallCount, Is.EqualTo(16), "링은 12~16 구간 요구를 16으로 충족");
            Assert.That(solidRingCount, Is.EqualTo(8), "포탈 8변 외의 링 변은 무개구");
            Assert.That(spokeCount, Is.EqualTo(6), "6방 부채꼴 분할 스포크");

            // 포탈 개구부 중심은 45° 간격으로 서로 겹치지 않는 세그먼트에 놓인다.
            Assert.That(portalMidAngles.Count, Is.EqualTo(8));
            portalMidAngles.Sort();
            for (int i = 0; i < portalMidAngles.Count; i++)
            {
                float gap = i + 1 < portalMidAngles.Count
                    ? portalMidAngles[i + 1] - portalMidAngles[i]
                    : 360f - portalMidAngles[portalMidAngles.Count - 1] + portalMidAngles[0];
                Assert.That(gap, Is.EqualTo(45f).Within(0.01f), $"portal gap {i}");
            }
        }

        [Test]
        public void RoundKeep_SharesExteriorEnvelopeAndRoomContracts_WithKeepOctagon()
        {
            InteriorLayoutDefinition roundKeep = InteriorLayoutDefinition.RoundKeep;
            InteriorLayoutDefinition octagon = InteriorLayoutDefinition.KeepOctagon;

            // 외곽 치수와 포탈 규격은 현행 규모를 유지한다(Phase 4 빌더 소비 호환).
            Assert.That(roundKeep.RoomWidth, Is.EqualTo(octagon.RoomWidth));
            Assert.That(roundKeep.RoomDepth, Is.EqualTo(octagon.RoomDepth));
            Assert.That(roundKeep.RoomHeight, Is.EqualTo(octagon.RoomHeight));
            Assert.That(roundKeep.WallThickness, Is.EqualTo(octagon.WallThickness));
            Assert.That(roundKeep.PortalWidth, Is.EqualTo(octagon.PortalWidth));
            Assert.That(roundKeep.PortalHeight, Is.EqualTo(octagon.PortalHeight));

            // 의미 방 이름은 기준형과 동일한 5개를 유지한다.
            Assert.That(roundKeep.GetZoneNames(), Is.EquivalentTo(octagon.GetZoneNames()));
        }

        // ===================================================================
        // [2026-10-14 Phase 3 — 비대칭 저택형(AsymmetricManor)] 정의 추가 TDD.
        //   - 런타임 선택/배정(Phase 4)에는 연결하지 않는다: 정의·명시 API·토폴로지 계약만 검증.
        //   - 로비는 동측으로 밀린 8정점 비대칭 팔각형(정점 평균 중심 약 (3.75,1.25))이고,
        //     남측 입구홀 축도 x=3으로 동편 이동해 남측 외곽 출입구 개구부가 벽 중심에서 +3 오프셋된다.
        //   - 서측 공방 날개(깊이 36.8 m)와 동측 창고 날개(깊이 28.8 m)의 경계 벽 길이가 다르고,
        //     로비 정점 집합은 중심 좌우 반사 집합과 달라야 한다(실루엣 비대칭 좌표 단정).
        //   - 외곽 치수·포탈 규격·필수 앵커 이름·의미 방 이름은 KeepOctagon 계약을 공유한다.
        //   - 존 규칙은 로비 다각형 밖 내부 전체를 빈틈없이 던진다(격자 전수 탐침).
        // ===================================================================
        [Test]
        public void AsymmetricManorDefinition_HasStableId_AndIsResolvableViaTryGetLayout()
        {
            InteriorLayoutDefinition manor = InteriorLayoutDefinition.AsymmetricManor;
            Assert.That(manor.LayoutId, Is.EqualTo("AsymmetricManor"));

            Assert.That(InteriorLayoutDefinition.TryGetLayout("AsymmetricManor", out InteriorLayoutDefinition resolved), Is.True);
            Assert.That(resolved, Is.SameAs(manor));

            // 기존 두 정의와 미지 id 조회 계약은 그대로 유지된다(기존 호출자 보호).
            Assert.That(InteriorLayoutDefinition.TryGetLayout("KeepOctagon", out InteriorLayoutDefinition octagon), Is.True);
            Assert.That(octagon, Is.SameAs(InteriorLayoutDefinition.KeepOctagon));
            Assert.That(InteriorLayoutDefinition.TryGetLayout("RoundKeep", out InteriorLayoutDefinition round), Is.True);
            Assert.That(round, Is.SameAs(InteriorLayoutDefinition.RoundKeep));
            Assert.That(InteriorLayoutDefinition.TryGetLayout("NoSuchLayout", out InteriorLayoutDefinition none), Is.False);
            Assert.That(none, Is.Null);
        }

        [Test]
        public void AsymmetricManor_Lobby_IsOffCenterAsymmetricOctagon_DistinctFromBaselines()
        {
            InteriorLayoutDefinition manor = InteriorLayoutDefinition.AsymmetricManor;
            Vector2[] lobby = manor.GetLobbyVertices();

            // 8정점 비대칭 팔각 로비 — 정확히 이 정점들(방 로컬 XZ 미터).
            Vector2[] expectedLobby =
            {
                new Vector2(-10f, 19f), new Vector2(18f, 19f), new Vector2(30f, 10f), new Vector2(30f, -8f),
                new Vector2(16f, -16f), new Vector2(-10f, -16f), new Vector2(-22f, -8f), new Vector2(-22f, 10f)
            };
            Assert.That(lobby, Is.EqualTo(expectedLobby));

            // 로비 중심(정점 평균)은 방 중심에서 동·북으로 밀려 있다(비대칭 배치의 핵심).
            Vector2 centroid = Vector2.zero;
            foreach (Vector2 vertex in lobby) centroid += vertex;
            centroid /= lobby.Length;
            Assert.That(centroid, Is.Not.EqualTo(Vector2.zero).Within(0.01f), "로비 중심 오프셋");
            Assert.That(centroid.x, Is.GreaterThan(0f), "로비는 동측으로 이동해야 한다");
            Assert.That(centroid.y, Is.GreaterThan(0f), "로비는 북측으로 살짝 이동해야 한다");

            // 방 중심 기준 서단(-22)과 동단(30)이 다르고, 로비 정점 집합을 로비 중심 기준으로
            // 좌우 반사하면 원 집합과 달라진다(하한 변 x -10..16 vs 상한 변 x -10..18).
            float minX = float.MaxValue, maxX = float.MinValue;
            foreach (Vector2 vertex in lobby)
            {
                minX = Mathf.Min(minX, vertex.x);
                maxX = Mathf.Max(maxX, vertex.x);
            }
            Assert.That(Mathf.Abs(minX), Is.Not.EqualTo(Mathf.Abs(maxX)).Within(0.01f), "방 중심 기준 좌우 외단 비대칭");
            var mirrored = new HashSet<Vector2>();
            foreach (Vector2 vertex in lobby)
                mirrored.Add(new Vector2(2f * centroid.x - vertex.x, vertex.y));
            int mirroredOutOfSet = 0;
            foreach (Vector2 vertex in lobby)
                if (!mirrored.Contains(vertex)) mirroredOutOfSet++;
            Assert.That(mirroredOutOfSet, Is.GreaterThanOrEqualTo(1),
                "로비 실루엣이 중심 좌우 대칭이면 비대칭형이 아니다");

            // 실루엣 구별: 로비 외접 사각형이 두 기준형과 모두 다르다.
            Assert.That(LobbyExtents(InteriorLayoutDefinition.KeepOctagon.GetLobbyVertices()),
                Is.Not.EqualTo(LobbyExtents(lobby)), "팔각 기준형과 로비 외접 사각형 구별");
            Assert.That(LobbyExtents(InteriorLayoutDefinition.RoundKeep.GetLobbyVertices()),
                Is.Not.EqualTo(LobbyExtents(lobby)), "원형 기준형과 로비 외접 사각형 구별");

            // 노출 배열은 방어 복사본(정의 오염 금지)이다.
            lobby[0] = new Vector2(999f, 999f);
            Assert.That(manor.GetLobbyVertices()[0], Is.EqualTo(new Vector2(-10f, 19f)), "방어 복사본");
        }

        [Test]
        public void AsymmetricManor_Walls_AreUniqueNonDegenerate_AndPortalsAreConsistent()
        {
            InteriorLayoutDefinition manor = InteriorLayoutDefinition.AsymmetricManor;
            PlayerCastleInteriorBuilder.WallLayout[] walls = manor.GetWalls();
            Assert.That(walls.Length, Is.EqualTo(20), "외곽 4 + 로비 8 + 침실 2 + 날개 경계 4 + 입구홀 2");

            var names = new HashSet<string>();
            foreach (PlayerCastleInteriorBuilder.WallLayout wall in walls)
            {
                Assert.That(names.Add(wall.Name), Is.True, "Wall names must be unique: " + wall.Name);
                float length = Vector2.Distance(wall.Start, wall.End);
                Assert.That(length, Is.GreaterThan(0f), wall.Name);
                if (wall.DoorwayWidth <= 0f)
                {
                    Assert.That(wall.DoorwayHeight, Is.EqualTo(0f), wall.Name);
                    Assert.That(wall.ConnectedFrom, Is.Null, wall.Name + " cannot claim a portal connection");
                    Assert.That(wall.ConnectedTo, Is.Null, wall.Name + " cannot claim a portal connection");
                    continue;
                }
                Assert.That(length, Is.GreaterThan(wall.DoorwayWidth), wall.Name);
                Assert.That(wall.DoorwayWidth, Is.GreaterThanOrEqualTo(1.8f), wall.Name + " controller clearance");
                Assert.That(wall.DoorwayHeight, Is.GreaterThanOrEqualTo(2f), wall.Name + " capsule headroom");
                Assert.That(wall.DoorwayHeight, Is.LessThan(manor.RoomHeight), wall.Name);
                Assert.That(Mathf.Abs(wall.DoorwayOffset) + wall.DoorwayWidth * 0.5f,
                    Is.LessThanOrEqualTo(length * 0.5f), wall.Name + " opening must lie within wall");
                Assert.That(string.IsNullOrEmpty(wall.ConnectedFrom), Is.False, wall.Name);
                Assert.That(string.IsNullOrEmpty(wall.ConnectedTo), Is.False, wall.Name);
            }

            // 로비 벽 8개는 로비 다각형의 변과 정확히 일치한다(정의 단일 소스 정합).
            var lobbyEdges = new HashSet<string>();
            Vector2[] lobby = manor.GetLobbyVertices();
            for (int i = 0; i < lobby.Length; i++)
                lobbyEdges.Add(EdgeKey(lobby[i], lobby[(i + 1) % lobby.Length]));
            var lobbyWallNames = new HashSet<string>
            {
                "Lobby_North", "Lobby_NorthEast", "Lobby_East", "Lobby_SouthEast",
                "Lobby_South", "Lobby_SouthWest", "Lobby_West", "Lobby_NorthWest",
            };
            int lobbyWallCount = 0;
            foreach (PlayerCastleInteriorBuilder.WallLayout wall in walls)
            {
                if (!lobbyWallNames.Contains(wall.Name)) continue;
                lobbyWallCount++;
                Assert.That(lobbyEdges.Contains(EdgeKey(wall.Start, wall.End)), Is.True,
                    wall.Name + " must coincide with a lobby polygon edge");
            }
            Assert.That(lobbyWallCount, Is.EqualTo(8));

            // 비대칭 단서 1: 남측 외곽 입구 개구부가 벽 중심에서 +3으로 동편 이동하며,
            // 개구부 중심은 입구홀 축 x=3(로비 남 포탈 개구부 중심과 같은 축)에 정렬된다.
            PlayerCastleInteriorBuilder.WallLayout entrance = FindWall(walls, "Entrance_South");
            Assert.That(entrance.DoorwayWidth, Is.EqualTo(4f));
            Assert.That(entrance.DoorwayOffset, Is.EqualTo(3f).Within(0.001f), "남측 출입구 동편 오프셋");
            float entranceLength = Vector2.Distance(entrance.Start, entrance.End);
            Vector2 doorwayCenter = Vector2.Lerp(entrance.Start, entrance.End,
                (entranceLength * 0.5f + entrance.DoorwayOffset) / entranceLength);
            // Vector2 비교는 비트 단위 등가라 좌표별 float 근사로 단정한다.
            Assert.That(doorwayCenter.x, Is.EqualTo(3f).Within(0.01f),
                "출입구 개구부 중심 x는 입구홀 축 x=3과 정렬된다");
            Assert.That(doorwayCenter.y, Is.EqualTo(-manor.RoomDepth * 0.5f).Within(0.01f),
                "출입구 개구부 중심 y는 남측 셸 위에 있다");

            // 비대칭 단서 2: 서측 공방 날개 경계가 동측 창고 날개 경계보다 길다(서측이 더 깊은 날개).
            float westBoundary = Vector2.Distance(FindWall(walls, "Craft_NorthBoundary").Start,
                FindWall(walls, "Craft_NorthBoundary").End);
            float eastBoundary = Vector2.Distance(FindWall(walls, "Storage_NorthBoundary").Start,
                FindWall(walls, "Storage_NorthBoundary").End);
            Assert.That(westBoundary, Is.GreaterThan(eastBoundary), "서측 날개가 더 깊은 비대칭 날개");

            // 포탈 그래프는 실제 벽 개구부에서 1:1 유도되며 외부 연결은 남측 입구 하나뿐이다.
            PlayerCastleInteriorBuilder.PortalConnection[] portals = manor.GetPortalConnections();
            Assert.That(portals.Length, Is.EqualTo(9), "남측 입구 1 + 로비 의미 포탈 8");
            var portalWalls = new HashSet<string>();
            foreach (PlayerCastleInteriorBuilder.PortalConnection portal in portals)
            {
                PlayerCastleInteriorBuilder.WallLayout matching = FindWall(walls, portal.WallName);
                Assert.That(matching.DoorwayWidth, Is.GreaterThan(0f), portal.WallName + " must have a real opening");
                Assert.That(matching.ConnectedFrom, Is.EqualTo(portal.From));
                Assert.That(matching.ConnectedTo, Is.EqualTo(portal.To));
                Assert.That(portalWalls.Add(portal.WallName), Is.True, "One portal graph edge per actual wall opening");
                Assert.That(portal.From == "Lobby" && portal.To == "Lobby", Is.False);
                bool touchesExterior = portal.From == "Exterior" || portal.To == "Exterior";
                Assert.That(touchesExterior, Is.EqualTo(portal.WallName == "Entrance_South"),
                    portal.WallName + " must be the sole exterior connection");
            }

            // 벽 배열도 방어 복사본이다.
            walls[0] = default;
            Assert.That(manor.GetWalls()[0].Name, Is.EqualTo("Exterior_North"), "walls 방어 복사본");
        }

        [Test]
        public void AsymmetricManor_PortalGraph_ReachesEveryRoomFromExterior()
        {
            InteriorLayoutDefinition manor = InteriorLayoutDefinition.AsymmetricManor;
            PlayerCastleInteriorBuilder.PortalConnection[] portals = manor.GetPortalConnections();

            // 팔각 기준형과 동일한 의미 연결(8방 로비 포탈 + 남측 입구)을 유지한다.
            AssertPortal(portals, "Entrance_South", "EntryHall", "Exterior");
            AssertPortal(portals, "Lobby_North", "Lobby", "Bedroom");
            AssertPortal(portals, "Lobby_NorthEast", "Lobby", "Storage");
            AssertPortal(portals, "Lobby_East", "Lobby", "Storage");
            AssertPortal(portals, "Lobby_SouthEast", "Lobby", "Alchemy");
            AssertPortal(portals, "Lobby_South", "Lobby", "EntryHall");
            AssertPortal(portals, "Lobby_SouthWest", "Lobby", "Barracks");
            AssertPortal(portals, "Lobby_West", "Lobby", "Craft");
            AssertPortal(portals, "Lobby_NorthWest", "Lobby", "Craft");

            var nodes = new HashSet<string>(manor.GetZoneNames());
            nodes.Add("Lobby"); nodes.Add("EntryHall"); nodes.Add("Exterior");
            var adjacency = new Dictionary<string, List<string>>();
            foreach (string node in nodes) adjacency[node] = new List<string>();
            foreach (PlayerCastleInteriorBuilder.PortalConnection portal in portals)
            {
                Assert.That(nodes.Contains(portal.From), Is.True, "Unknown portal source " + portal.From);
                Assert.That(nodes.Contains(portal.To), Is.True, "Unknown portal target " + portal.To);
                adjacency[portal.From].Add(portal.To);
                adjacency[portal.To].Add(portal.From);
            }
            var visited = new HashSet<string> { "Exterior" };
            var pending = new Queue<string>(); pending.Enqueue("Exterior");
            while (pending.Count > 0)
                foreach (string next in adjacency[pending.Dequeue()])
                    if (visited.Add(next)) pending.Enqueue(next);
            CollectionAssert.AreEquivalent(nodes, visited,
                "모든 방이 실제 개구부 그래프로 외부에서 도달 가능해야 한다.");
        }

        [Test]
        public void AsymmetricManor_ZonesTileInterior_AndRequiredAnchorsSitInCorrectRooms()
        {
            InteriorLayoutDefinition manor = InteriorLayoutDefinition.AsymmetricManor;

            Assert.That(manor.GetZoneAt(Vector2.zero), Is.EqualTo("Lobby"));
            Assert.That(manor.GetZoneAt(new Vector2(4f, 15f)), Is.EqualTo("Lobby"));
            Assert.That(manor.GetZoneAt(new Vector2(4f, 21f)), Is.EqualTo("Bedroom"));
            Assert.That(manor.GetZoneAt(new Vector2(-36f, 24f)), Is.EqualTo("Craft"));
            Assert.That(manor.GetZoneAt(new Vector2(44f, 26f)), Is.EqualTo("Storage"));
            Assert.That(manor.GetZoneAt(new Vector2(-34f, -26f)), Is.EqualTo("Barracks"));
            Assert.That(manor.GetZoneAt(new Vector2(40f, -28f)), Is.EqualTo("Alchemy"));
            Assert.That(manor.GetZoneAt(new Vector2(3f, -30f)), Is.EqualTo("EntryHall"));
            Assert.That(manor.GetZoneAt(new Vector2(-40f, 0f)), Is.EqualTo("Craft"));
            Assert.That(manor.GetZoneAt(new Vector2(40f, 0f)), Is.EqualTo("Storage"));
            Assert.That(manor.GetZoneAt(new Vector2(-40f, -12f)), Is.EqualTo("Barracks"));
            Assert.That(manor.GetZoneAt(new Vector2(40f, -12f)), Is.EqualTo("Alchemy"));
            Assert.That(manor.GetZoneAt(new Vector2(200f, 0f)), Is.EqualTo("Exterior"));

            // 동편 이동 진입 동선: 로비 남 포탈(x=3) 바깥에서 오프셋된 외곽 입구까지 EntryHall 이다.
            for (float y = -17f; y >= -43f; y -= 1f)
                Assert.That(manor.GetZoneAt(new Vector2(3f, y)), Is.EqualTo("EntryHall"), $"y={y}");

            // 존 규칙이 로비 밖 내부 전체를 던진다 — 사각(널) 구역 없음.
            for (float px = -55f; px <= 55f; px += 5.5f)
                for (float py = -41f; py <= 41f; py += 4.1f)
                    Assert.That(manor.GetZoneAt(new Vector2(px, py)), Is.Not.Null,
                        $"probe ({px},{py})");

            // 필수 앵커는 정의에서 공급되며 올바른 방 존 안(로비 밖)에 놓인다.
            string[] anchorNames =
            {
                "Workbench", "StorageShelf_2", "WeaponStand_0", "AlchemyTable",
                "LordBed", "CookingTable", "CookingIngredientWarehouse",
            };
            string[] anchorZones = { "Craft", "Storage", "Barracks", "Alchemy", "Bedroom", "Storage", "Storage" };
            for (int i = 0; i < anchorNames.Length; i++)
            {
                Assert.That(manor.TryGetAnchor(anchorNames[i], out Vector2 position), Is.True, anchorNames[i]);
                Assert.That(manor.GetZoneAt(position), Is.EqualTo(anchorZones[i]), anchorNames[i]);
                Assert.That(manor.IsInsideLobby(position), Is.False,
                    anchorNames[i] + " must sit in its room, not the lobby");
                Assert.That(Mathf.Abs(position.x), Is.LessThan(manor.RoomWidth * 0.5f), anchorNames[i]);
                Assert.That(Mathf.Abs(position.y), Is.LessThan(manor.RoomDepth * 0.5f), anchorNames[i]);
            }
            Assert.That(manor.TryGetAnchor("NoSuchAnchor", out _), Is.False);

            // 비대칭 앵커 배치: 좌우 날개 앵커의 x 크기가 의도적으로 다르다(기준형의 대칭 ±40 과 구별).
            Assert.That(Mathf.Abs(manor.GetAnchorPosition("Workbench").x),
                Is.Not.EqualTo(Mathf.Abs(manor.GetAnchorPosition("StorageShelf_2").x)).Within(0.01f),
                "좌우 공방/창고 앵커 비대칭");
            Assert.That(Mathf.Abs(manor.GetAnchorPosition("WeaponStand_0").x),
                Is.Not.EqualTo(Mathf.Abs(manor.GetAnchorPosition("AlchemyTable").x)).Within(0.01f),
                "좌우 병영/연금술 앵커 비대칭");
        }

        [Test]
        public void AsymmetricManor_SharesExteriorEnvelopeAndRoomContracts_WithKeepOctagon()
        {
            InteriorLayoutDefinition manor = InteriorLayoutDefinition.AsymmetricManor;
            InteriorLayoutDefinition octagon = InteriorLayoutDefinition.KeepOctagon;

            // 외곽 치수와 포탈 규격은 현행 규모를 유지한다(Phase 4 빌더 소비 호환).
            Assert.That(manor.RoomWidth, Is.EqualTo(octagon.RoomWidth));
            Assert.That(manor.RoomDepth, Is.EqualTo(octagon.RoomDepth));
            Assert.That(manor.RoomHeight, Is.EqualTo(octagon.RoomHeight));
            Assert.That(manor.WallThickness, Is.EqualTo(octagon.WallThickness));
            Assert.That(manor.PortalWidth, Is.EqualTo(octagon.PortalWidth));
            Assert.That(manor.PortalHeight, Is.EqualTo(octagon.PortalHeight));

            // 의미 방 이름은 기준형과 동일한 5개를 유지한다.
            Assert.That(manor.GetZoneNames(), Is.EquivalentTo(octagon.GetZoneNames()));
        }

        // ===================================================================
        // [2026-10-14 Phase 4 — 결정론 영지 키 레이아웃 배정 + 레이아웃 인식 실내 빌드]
        //   - InteriorLayoutDefinition.SelectForTerritory: FNV-1a 32비트 안정 해시 3분할.
        //     string.GetHashCode(실행마다 달라짐)/Random/시간 미사용 — 같은 키는 항상 같은 정의.
        //     null/빈 키는 KeepOctagon 기준형 폴백.
        //   - PlayerCastleInteriorBuilder.BuildPlayerCastleInterior(nationStyle, layoutVariant,
        //     layout) 명시 정의 오버로드: shell/벽/포탈 석공/팀버 골조/밴드/캡/존 판정/앵커가
        //     전달된 정의를 따른다. 구(舊) 1/2인자 오버로드는 KeepOctagon 계약 100% 보존.
        //   - InteriorLayoutContext: 방에 부착되어 소비 통합(Phase 4b)이 레이아웃을 조회하게
        //     한다(정적 전역/싱글턴 없음).
        // ===================================================================
        [Test]
        public void SelectForTerritory_IsDeterministic_AndFallsBackToKeepOctagon()
        {
            string[] keys =
            {
                "East_01", "West_02", "South_03", "North_04", "Empire_05",
                "East_01", "Territory_A", "Territory_42",
            };
            foreach (string key in keys)
            {
                InteriorLayoutDefinition first = InteriorLayoutDefinition.SelectForTerritory(key);
                InteriorLayoutDefinition second = InteriorLayoutDefinition.SelectForTerritory(key);
                Assert.That(first, Is.Not.Null, key);
                Assert.That(second, Is.SameAs(first), key + " must be deterministic");
                Assert.That(InteriorLayoutDefinition.TryGetLayout(first.LayoutId,
                    out InteriorLayoutDefinition resolved) && ReferenceEquals(resolved, first), Is.True,
                    key + " must resolve to a registered definition");
            }

            // null/빈 키 폴백 — 기준형 KeepOctagon 고정(반복 호출 포함).
            Assert.That(InteriorLayoutDefinition.SelectForTerritory(null),
                Is.SameAs(InteriorLayoutDefinition.KeepOctagon));
            Assert.That(InteriorLayoutDefinition.SelectForTerritory(""),
                Is.SameAs(InteriorLayoutDefinition.KeepOctagon));
            Assert.That(InteriorLayoutDefinition.SelectForTerritory(null),
                Is.SameAs(InteriorLayoutDefinition.SelectForTerritory("")),
                "null/빈 키 폴백은 항상 같은 정의여야 한다");
        }

        [Test]
        public void SelectForTerritory_AlwaysKeepOctagon_UnifiedAcrossAllTerritories()
        {
            // [무번호 15 — 실내 구조 통일] 영지 키 무관 단일 레이아웃 — 모든 영지 성이 기준형 KeepOctagon.
            //   기존 FNV-1a 3분할 배정 테스트는 폐기(사용자 지시 "모든 실내 영지가 나의 소속 영지와 동일한 구조").
            //   5개 국가 스타일 × 12개 인덱스 + 임의 키 + null/빈 키 전부 동일 정의(결정론 유지).
            string[] nations = { "East", "West", "South", "North", "Empire" };
            var keys = new List<string>();
            foreach (string nation in nations)
                for (int i = 1; i <= 12; i++)
                    keys.Add(nation + "_" + i.ToString("00", System.Globalization.CultureInfo.InvariantCulture));
            keys.Add("Territory_A");
            keys.Add("Territory_42");
            keys.Add("indoor_tour_lord");

            foreach (string key in keys)
            {
                Assert.That(InteriorLayoutDefinition.SelectForTerritory(key),
                    Is.SameAs(InteriorLayoutDefinition.KeepOctagon),
                    key + " — 모든 영지 키는 기준형 KeepOctagon으로 통일되어야 한다");
            }

            // null/빈 키도 동일 정의(폴백 경로 포함 전면 통일).
            Assert.That(InteriorLayoutDefinition.SelectForTerritory(null),
                Is.SameAs(InteriorLayoutDefinition.KeepOctagon));
            Assert.That(InteriorLayoutDefinition.SelectForTerritory(""),
                Is.SameAs(InteriorLayoutDefinition.KeepOctagon));
        }

        [Test]
        public void BuilderLegacyOverloads_StillBuildKeepOctagon_AndAttachLayoutContext()
        {
            GameObject singleArg = PlayerCastleInteriorBuilder.BuildPlayerCastleInterior("Empire");
            GameObject twoArg = PlayerCastleInteriorBuilder.BuildPlayerCastleInterior("Empire", 5);
            GameObject nullLayout = PlayerCastleInteriorBuilder.BuildPlayerCastleInterior("Empire", 0, null);
            try
            {
                foreach (GameObject room in new[] { singleArg, twoArg, nullLayout })
                {
                    Assert.That(room, Is.Not.Null);
                    InteriorLayoutContext context = room.GetComponent<InteriorLayoutContext>();
                    Assert.That(context, Is.Not.Null, "room must carry an InteriorLayoutContext");
                    Assert.That(context.LayoutId, Is.EqualTo(InteriorLayoutDefinition.KeepOctagonLayoutId));
                    Assert.That(InteriorLayoutContext.TryGetLayout(room,
                        out InteriorLayoutDefinition resolved), Is.True);
                    Assert.That(resolved, Is.SameAs(InteriorLayoutDefinition.KeepOctagon));
                    // 기준형 전용 벽(입구홀 북서 구획)이 여전히 존재 — 토폴로지 교체 없음.
                    Assert.That(room.transform.Find("EntryHall_NorthWest"), Is.Not.Null,
                        "legacy overloads must keep the KeepOctagon shell");
                }
            }
            finally
            {
                Object.DestroyImmediate(singleArg);
                Object.DestroyImmediate(twoArg);
                Object.DestroyImmediate(nullLayout);
            }
        }

        [Test]
        public void BuilderExplicitLayoutOverload_BuildsSelectedTopology_AnchorsAndContext()
        {
            // [122차] 빈 실내 규칙: 시설 앵커 검사를 위해 임시 구성으로 수행.
            PlayerCastleInteriorBuilder.FurnishStations = true;
            try
            {
            InteriorLayoutDefinition[] definitions =
            {
                InteriorLayoutDefinition.KeepOctagon,
                InteriorLayoutDefinition.RoundKeep,
                InteriorLayoutDefinition.AsymmetricManor,
            };
            foreach (InteriorLayoutDefinition definition in definitions)
            {
                GameObject room =
                    PlayerCastleInteriorBuilder.BuildPlayerCastleInterior("Empire", 0, definition);
                Assert.That(room, Is.Not.Null, definition.LayoutId);
                try
                {
                    // 컨텍스트가 선택 정의를 그대로 노출한다(정적 전역 없음).
                    InteriorLayoutContext context = room.GetComponent<InteriorLayoutContext>();
                    Assert.That(context, Is.Not.Null, definition.LayoutId);
                    Assert.That(context.LayoutId, Is.EqualTo(definition.LayoutId));
                    Assert.That(context.Definition, Is.SameAs(definition));
                    Assert.That(InteriorLayoutContext.TryGetLayout(room,
                        out InteriorLayoutDefinition resolved), Is.True, definition.LayoutId);
                    Assert.That(resolved, Is.SameAs(definition));

                    // 정의의 모든 벽이 실제 콜라이더 메시 셸로 존재한다(정의 GetWalls 소비).
                    PlayerCastleInteriorBuilder.WallLayout[] walls = definition.GetWalls();
                    foreach (PlayerCastleInteriorBuilder.WallLayout wall in walls)
                    {
                        Transform wallTransform = room.transform.Find(wall.Name);
                        Assert.That(wallTransform, Is.Not.Null,
                            definition.LayoutId + " missing wall " + wall.Name);
                        Assert.That(wallTransform.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null,
                            definition.LayoutId + " wall mesh " + wall.Name);
                        Assert.That(wallTransform.GetComponent<MeshCollider>(), Is.Not.Null,
                            definition.LayoutId + " wall collider " + wall.Name);
                    }

                    // 토폴로지 의존 장식(포탈 석공/팀버 골조/톱 캡)도 정의 벽에서 파생된다.
                    PlayerCastleInteriorBuilder.WallLayout portalWall = FindWall(walls, "Lobby_North");
                    Assert.That(room.transform.Find(portalWall.Name + "_NonCollidingStoneFrame"),
                        Is.Not.Null, definition.LayoutId + " portal stonework");
                    Assert.That(room.transform.Find(portalWall.Name + "_TimberFrame"),
                        Is.Not.Null, definition.LayoutId + " timber frame");
                    Assert.That(room.transform.Find(portalWall.Name + "_TopCap"),
                        Is.Not.Null, definition.LayoutId + " top cap");

                    // 기능 앵커는 선택 정의 좌표로 배치된다(기준형 하드코딩 아님).
                    Vector2 anchor = definition.GetAnchorPosition("Workbench");
                    Transform workbench = room.transform.Find("Workbench");
                    Assert.That(workbench.localPosition.x, Is.EqualTo(anchor.x).Within(0.001f), definition.LayoutId);
                    Assert.That(workbench.localPosition.z, Is.EqualTo(anchor.y).Within(0.001f), definition.LayoutId);

                    // RoundKeep: 방사 스포크 벽은 가구 스케일 패스를 통과하지 않는다(정의 벽 보호).
                    foreach (PlayerCastleInteriorBuilder.WallLayout wall in walls)
                    {
                        if (!wall.Name.StartsWith("Spoke_")) continue;
                        Transform spoke = room.transform.Find(wall.Name);
                        Vector3 expected = new Vector3((wall.Start.x + wall.End.x) * 0.5f, 0f,
                            (wall.Start.y + wall.End.y) * 0.5f);
                        Assert.That(spoke.localPosition.x, Is.EqualTo(expected.x).Within(0.001f),
                            definition.LayoutId + " " + wall.Name + " must not be scaled");
                        Assert.That(spoke.localPosition.z, Is.EqualTo(expected.z).Within(0.001f),
                            definition.LayoutId + " " + wall.Name + " must not be scaled");
                    }
                }
                finally
                {
                    Object.DestroyImmediate(room);
                }
            }
            }
            finally
            {
                PlayerCastleInteriorBuilder.FurnishStations = false;   // [122차] 기본값 복원
            }
        }

        private static void AssertAnchor(GameObject room, string name, string expectedZone)
        {
            Transform anchor = room.transform.Find(name);
            Assert.That(anchor, Is.Not.Null, "Missing runtime interaction anchor " + name);
            Vector2 position = new Vector2(anchor.localPosition.x, anchor.localPosition.z);
            Assert.That(position.x, Is.GreaterThan(-PlayerCastleInteriorBuilder.RoomWidth * 0.5f));
            Assert.That(position.x, Is.LessThan(PlayerCastleInteriorBuilder.RoomWidth * 0.5f));
            Assert.That(position.y, Is.GreaterThan(-PlayerCastleInteriorBuilder.RoomDepth * 0.5f));
            Assert.That(position.y, Is.LessThan(PlayerCastleInteriorBuilder.RoomDepth * 0.5f));
            Assert.That(PlayerCastleInteriorBuilder.GetTopologyZoneAt(position), Is.EqualTo(expectedZone), name);
        }

        private static PlayerCastleInteriorBuilder.WallLayout FindWall(
            PlayerCastleInteriorBuilder.WallLayout[] walls, string name)
        {
            foreach (var wall in walls) if (wall.Name == name) return wall;
            Assert.Fail("Topology is missing wall " + name);
            return default;
        }

        private static void AssertPortal(PlayerCastleInteriorBuilder.PortalConnection[] portals,
            string wallName, string from, string to)
        {
            foreach (var portal in portals)
                if (portal.WallName == wallName)
                {
                    Assert.That(portal.From, Is.EqualTo(from));
                    Assert.That(portal.To, Is.EqualTo(to));
                    return;
                }
            Assert.Fail("Missing portal opening at wall " + wallName);
        }

        // [2026-10-14 Phase 2] RoundKeep 원형 로비 검증 보조.
        private static float NormalizedAngleDegrees(Vector2 point)
        {
            float angle = Mathf.Atan2(point.y, point.x) * Mathf.Rad2Deg;
            return angle < 0f ? angle + 360f : angle;
        }

        private static float LobbyRadialSpread(Vector2[] lobbyVertices)
        {
            float maxRadius = 0f, minRadius = float.MaxValue;
            for (int i = 0; i < lobbyVertices.Length; i++)
            {
                Vector2 edgeMid = (lobbyVertices[i] + lobbyVertices[(i + 1) % lobbyVertices.Length]) * 0.5f;
                maxRadius = Mathf.Max(Mathf.Max(maxRadius, lobbyVertices[i].magnitude), edgeMid.magnitude);
                minRadius = Mathf.Min(Mathf.Min(minRadius, lobbyVertices[i].magnitude), edgeMid.magnitude);
            }
            return maxRadius - minRadius;
        }

        private static Vector2 LobbyExtents(Vector2[] lobbyVertices)
        {
            float maxX = 0f, maxY = 0f;
            foreach (Vector2 vertex in lobbyVertices)
            {
                maxX = Mathf.Max(maxX, Mathf.Abs(vertex.x));
                maxY = Mathf.Max(maxY, Mathf.Abs(vertex.y));
            }
            return new Vector2(maxX * 2f, maxY * 2f);
        }

        // [2026-10-14 Phase 3] AsymmetricManor 로비 벽↔다각형 변 일치 판정 보조(무방향 변 키).
        private static string EdgeKey(Vector2 a, Vector2 b)
        {
            if (a.x > b.x || (a.x == b.x && a.y > b.y))
            {
                Vector2 swap = a;
                a = b;
                b = swap;
            }
            return a.x.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "," +
                a.y.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "|" +
                b.x.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "," +
                b.y.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
