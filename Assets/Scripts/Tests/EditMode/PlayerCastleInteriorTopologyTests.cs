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
            GameObject room = PlayerCastleInteriorBuilder.BuildPlayerCastleInterior("Empire", 0);
            Assert.That(room, Is.Not.Null);
            try
            {
                Assert.That(room.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(room.transform.Find("Floor"), Is.Not.Null);
                Assert.That(room.transform.Find("Ceiling"), Is.Not.Null);
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
                        Is.EqualTo("PlayerCastle_CalmAshlar_2Course"));
                    Assert.That(wallRenderer.sharedMaterial.mainTextureScale, Is.EqualTo(Vector2.one),
                        "Topology UVs encode physical wall tiling; the material must not tile a second time.");
                    Assert.That(wallRenderer.sharedMaterial.IsKeywordEnabled("_NORMALMAP"), Is.False,
                        "Noisy supplied wall normal must not be used on the player-castle topology shell.");
                }
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
    }
}
