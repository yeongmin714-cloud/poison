using System.Reflection;
using NUnit.Framework;
using ProjectName.Systems;
using ProjectName.UI;
using ProjectName.UI.Toolkit;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    public sealed class PlayerCastleInteriorCollisionAndKitchenTests
    {
        [Test]
        public void BuiltInterior_HasNoPhysicalBrazierOrSouthEntranceBlockingProps()
        {
            GameObject room = PlayerCastleInteriorBuilder.BuildPlayerCastleInterior("Empire", 0);
            Assert.That(room, Is.Not.Null);
            try
            {
                Transform entranceWall = room.transform.Find("Entrance_South");
                Assert.That(entranceWall, Is.Not.Null, "The south entrance wall must retain its real doorway cut.");

                foreach (Transform child in room.GetComponentsInChildren<Transform>(true))
                {
                    Assert.That(child.name, Is.Not.EqualTo("BrazierBowl"), "Physical brazier bowls must not be built.");
                    Assert.That(child.name, Is.Not.EqualTo("BrazierCoals"), "Physical brazier coals must not be built.");
                    Assert.That(child.name, Is.Not.EqualTo("Hearth_Left"), "Physical left hearth props must not be built.");
                    Assert.That(child.name, Is.Not.EqualTo("Hearth_Right"), "Physical right hearth props must not be built.");
                }

                // [2026-10-09 교정] 10-08 승인 개편("실내 어둡게 + 은은한 조명만")으로 입구측
                // 화광이 (1,0.55,0.25)@|x|20~30 강광에서 저강도 웜 풀로 재편됐다.
                // 물리 브레이저 금지 원칙 유지 — 입구측(최종 로컬 z<-35)에 따뜻한 광원이
                // 은은한 세기(intensity<=0.5)·근거리(range<=7)로 남아 있는지만 단정.
                bool retainedWarmEntranceLight = false;
                foreach (Light light in room.GetComponentsInChildren<Light>(true))
                {
                    if (light.transform.localPosition.z < -35f &&
                        light.color.r >= 0.9f && light.color.g >= 0.4f && light.color.g <= 0.9f &&
                        light.color.b >= 0.1f && light.color.b <= 0.7f &&
                        light.intensity <= 0.5f && light.range <= 7f)
                        retainedWarmEntranceLight = true;
                }
                Assert.That(retainedWarmEntranceLight, Is.True,
                    "입구측에 따뜻한 은은 광원(저강도 포인트라이트)은 남아 있어야 한다 — 물리 브레이저는 금지.");

                // Confirm the real south-wall mesh has an opening at player height. Its broad-phase
                // bounds span the doorway, so test its triangles directly rather than its AABB.
                MeshCollider entranceCollider = entranceWall.GetComponent<MeshCollider>();
                Assert.That(entranceCollider, Is.Not.Null, "The south entrance must retain its wall mesh collider.");
                var portalRay = new Ray(
                    new Vector3(0f, 1.2f, -PlayerCastleInteriorBuilder.RoomDepth * 0.5f - 1f),
                    Vector3.forward);
                Assert.That(entranceCollider.Raycast(portalRay, out _, 2f), Is.False,
                    "The south portal itself must remain open at its center.");

                // Sweep a player-sized capsule from just outside to just inside the south entrance.
                // Unlike Bounds.Intersects, these narrow-phase physics probes do not count the floor
                // or ceiling support surfaces (and do not treat the portal mesh's AABB as solid).
                Transform floor = room.transform.Find("Floor");
                Transform ceiling = room.transform.Find("Ceiling");
                const float capsuleRadius = 0.35f;
                const float capsuleLowerTip = 0.55f;
                const float capsuleUpperTip = 1.45f;
                float startZ = -PlayerCastleInteriorBuilder.RoomDepth * 0.5f -
                    PlayerCastleInteriorBuilder.WallThickness * 0.5f - 0.35f;
                float sweepDistance = PlayerCastleInteriorBuilder.WallThickness + 0.7f;
                // Keep the full player capsule inside the 4m portal, clear of the side-wall inner faces.
                float[] passageLanes = { -1.4f, -0.7f, 0f, 0.7f, 1.4f };
                Physics.SyncTransforms();
                foreach (float laneX in passageLanes)
                {
                    Vector3 lowerTip = new Vector3(laneX, capsuleLowerTip, startZ);
                    Vector3 upperTip = new Vector3(laneX, capsuleUpperTip, startZ);
                    foreach (Collider collider in Physics.OverlapCapsule(
                        lowerTip, upperTip, capsuleRadius, Physics.DefaultRaycastLayers,
                        QueryTriggerInteraction.Ignore))
                    {
                        if (collider == null || !collider.transform.IsChildOf(room.transform) ||
                            collider.transform == entranceWall || collider.transform == floor ||
                            collider.transform == ceiling) continue;
                        Assert.Fail(collider.name + " must not physically obstruct the south entrance passage.");
                    }

                    foreach (RaycastHit hit in Physics.CapsuleCastAll(
                        lowerTip, upperTip, capsuleRadius, Vector3.forward, sweepDistance,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    {
                        Collider collider = hit.collider;
                        if (collider == null || !collider.transform.IsChildOf(room.transform) ||
                            collider.transform == entranceWall || collider.transform == floor ||
                            collider.transform == ceiling) continue;
                        Assert.Fail(collider.name + " must not physically obstruct the south entrance passage.");
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(room);
            }
        }

        [Test]
        public void Workbench_UsesEInputAndRoutesEquipmentCraftingToWeaponForge()
        {
            GameObject room = PlayerCastleInteriorBuilder.BuildPlayerCastleInterior("Empire", 0);
            Assert.That(room, Is.Not.Null);
            try
            {
                Transform workbench = room.transform.Find("Workbench");
                Assert.That(workbench, Is.Not.Null);
                var station = workbench.GetComponent<TerritoryCraftingStation>();
                Assert.That(station, Is.Not.Null, "The built Workbench must carry its E-interaction station.");
                Assert.That(station.StationName, Is.EqualTo("영지 작업대"));
                Assert.That(station.TerritoryId, Is.EqualTo("Empire_01"));

                MethodInfo openStation = typeof(TerritoryCraftingStation).GetMethod(
                    "OpenCraftingUI", BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo openForge = typeof(WeaponForgeUTK).GetMethod(
                    "Open", BindingFlags.Public | BindingFlags.Static, null, System.Type.EmptyTypes, null);
                Assert.That(openStation, Is.Not.Null);
                Assert.That(openForge, Is.Not.Null);
                Assert.That(HasCallTo(openStation, openForge), Is.True,
                    "The Toolkit-ready Workbench route must open the equipment WeaponForgeUTK.");

                MethodInfo readInput = typeof(TerritoryCraftingStation).GetMethod(
                    "WasInteractPressed", BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo findPlayer = typeof(TerritoryCraftingStation).GetMethod(
                    "FindPlayer", BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo findStats = typeof(TerritoryCraftingStation).GetMethod(
                    "FindPlayerStats", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(readInput, Is.Not.Null);
                Assert.That(findPlayer, Is.Not.Null);
                Assert.That(findStats, Is.Not.Null);
                Assert.That(readInput.ToString(), Does.Contain("WasInteractPressed"));
                Assert.That(findPlayer.ToString(), Does.Contain("FindPlayer"));
                Assert.That(findStats.ToString(), Does.Contain("FindPlayerStats"));
            }
            finally
            {
                Object.DestroyImmediate(room);
            }
        }

        [Test]
        public void PlayerCastleBedAndAlchemyStation_AreAttachedAndRouteToTheirToolkitWindows()
        {
            GameObject room = PlayerCastleInteriorBuilder.BuildPlayerCastleInterior("Empire", 0);
            Assert.That(room, Is.Not.Null);
            try
            {
                Transform bed = room.transform.Find("LordBed");
                Assert.That(bed, Is.Not.Null);
                Assert.That(bed.GetComponent<Bed>(), Is.Not.Null,
                    "Player castle bed must retain its sleep/save interaction component.");

                Transform table = room.transform.Find("AlchemyTable");
                Assert.That(table, Is.Not.Null);
                Assert.That(table.GetComponent<AlchemyStation>(), Is.Not.Null,
                    "Player castle alchemy table must carry its E-interaction station.");
                MethodInfo openStation = typeof(AlchemyStation).GetMethod(
                    "OpenAlchemyUI", BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo openBench = typeof(AlchemyBenchUTK).GetMethod(
                    "Open", BindingFlags.Public | BindingFlags.Static, null, System.Type.EmptyTypes, null);
                MethodInfo readInput = typeof(AlchemyStation).GetMethod(
                    "WasInteractPressed", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(openStation, Is.Not.Null);
                Assert.That(openBench, Is.Not.Null);
                Assert.That(readInput, Is.Not.Null);
                Assert.That(HasCallTo(openStation, openBench), Is.True,
                    "Alchemy station must route the Toolkit path to AlchemyBenchUTK.");
                MethodInfo ensureBootstrap = typeof(UIToolkitBootstrap).GetMethod(
                    "Ensure", BindingFlags.Public | BindingFlags.Static, null, System.Type.EmptyTypes, null);
                Assert.That(ensureBootstrap, Is.Not.Null);
                Assert.That(HasCallTo(openStation, ensureBootstrap), Is.True,
                    "Alchemy station must initialize the Toolkit root before opening its bench.");

                MethodInfo saveAtBed = typeof(SleepUTK).GetMethod("SaveAtBed", BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo setSpawn = typeof(Bed).GetMethod("SetSpawnPoint", BindingFlags.Public | BindingFlags.Static);
                MethodInfo autoSave = typeof(SaveManager).GetMethod("AutoSave", BindingFlags.Public | BindingFlags.Instance);
                Assert.That(saveAtBed, Is.Not.Null);
                Assert.That(setSpawn, Is.Not.Null);
                Assert.That(autoSave, Is.Not.Null);
                Assert.That(HasCallTo(saveAtBed, setSpawn), Is.True, "Bed save action must set the bed respawn point.");
                Assert.That(HasCallTo(saveAtBed, autoSave), Is.True, "Bed save action must invoke the existing save manager.");
            }
            finally
            {
                Object.DestroyImmediate(room);
            }
        }

        [Test]
        public void BarrackBeds_TwentyStayInsideBarracksZoneClearOfTableAndEntrance()
        {
            GameObject room = PlayerCastleInteriorBuilder.BuildPlayerCastleInterior("Empire", 0);
            Assert.That(room, Is.Not.Null);
            try
            {
                Transform table = room.transform.Find("BarracksMapTable");
                Assert.That(table, Is.Not.Null, "병사 관리 탁자(BarracksMapTable)가 존재해야 한다.");
                Physics.SyncTransforms();
                Bounds tableBounds = MergeRendererBounds(table);

                int bedCount = 0;
                float halfWidth = PlayerCastleInteriorBuilder.RoomWidth * 0.5f;
                float halfDepth = PlayerCastleInteriorBuilder.RoomDepth * 0.5f;
                foreach (Transform child in room.transform)
                {
                    if (!child.name.StartsWith("BarrackBed_")) continue;
                    bedCount++;
                    BoxCollider bedCollider = child.GetComponent<BoxCollider>();
                    Assert.That(bedCollider, Is.Not.Null,
                        child.name + " 이 충돌 풋프린트(BoxCollider)를 유지해야 한다.");
                    Bounds bedBounds = bedCollider.bounds;

                    Assert.That(bedBounds.min.x, Is.GreaterThan(-halfWidth),
                        child.name + " 이 방 서쪽 밖으로 나갔다.");
                    Assert.That(bedBounds.max.x, Is.LessThan(halfWidth),
                        child.name + " 이 방 동쪽 밖으로 나갔다.");
                    Assert.That(bedBounds.min.z, Is.GreaterThan(-halfDepth),
                        child.name + " 이 남벽 밖으로 나갔다 — ×2.45 가구 스케일 패스가 침대 좌표를 다시 민 회귀.");
                    Assert.That(bedBounds.max.z, Is.LessThan(halfDepth),
                        child.name + " 이 방 북쪽 밖으로 나갔다.");

                    string zone = PlayerCastleInteriorBuilder.GetTopologyZoneAt(
                        new Vector2(bedBounds.center.x, bedBounds.center.z));
                    Assert.That(zone, Is.EqualTo("Barracks"),
                        child.name + " 중심이 배럭 구역(SW 사분면) 밖에 있다 — 병사 관리 탁자 옆 배럭에 세워져야 한다.");

                    Assert.That(bedBounds.Intersects(tableBounds), Is.False,
                        child.name + " 이 병사 관리 탁자와 겹친다.");
                    Assert.That(bedBounds.Contains(new Vector3(-30f, 0.5f, -33f)), Is.False,
                        child.name + " 이 아군 병사 스폰 지점(-30,-33)을 침범한다.");
                    Assert.That(bedBounds.max.x, Is.LessThan(-4f),
                        child.name + " 이 남측 입구 통로(x=±2, 여유 2m)를 침범한다.");
                }
                Assert.That(bedCount, Is.EqualTo(20), "배럭 침대는 정확히 20개여야 한다.");
            }
            finally
            {
                Object.DestroyImmediate(room);
            }
        }

        /// <summary>루트 하위 Renderer들의 월드 AABB 합본 — GLB 탁자류(콜라이더 부재) 풋프린트용.</summary>
        private static Bounds MergeRendererBounds(Transform root)
        {
            bool mergedAny = false;
            Bounds merged = new Bounds(root.position, Vector3.zero);
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (!mergedAny)
                {
                    merged = renderer.bounds;
                    mergedAny = true;
                }
                else
                {
                    merged.Encapsulate(renderer.bounds);
                }
            }
            Assert.That(mergedAny, Is.True, root.name + " 하위에 Renderer가 하나도 없다.");
            return merged;
        }

        private static bool HasCallTo(MethodInfo caller, MethodInfo target)
        {
            byte[] il = caller?.GetMethodBody()?.GetILAsByteArray();
            if (il == null || target == null) return false;
            for (int i = 0; i + 4 < il.Length; i++)
            {
                if (il[i] != 0x28 && il[i] != 0x6f) continue;
                int token = il[i + 1] | (il[i + 2] << 8) | (il[i + 3] << 16) | (il[i + 4] << 24);
                try
                {
                    MethodBase called = caller.Module.ResolveMethod(token);
                    if (called == target ||
                        (called != null && called.Name == target.Name && called.DeclaringType == target.DeclaringType))
                        return true;
                }
                catch (System.ArgumentException)
                {
                    // Ignore coincidental metadata tokens that do not resolve to methods.
                }
                i += 4;
            }
            return false;
        }
    }
}
