using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [2026-10-10 J1/J2 — 영지 실내 통일] 계약 테스트.
    /// - J1: 성 진입(소유 여부 무관)이 전부 PlayerCastleInteriorBuilder로 통일 —
    ///   구 영주 성 셸(CastleInteriorBuilder.BuildCastleInterior) 호출부 폐기.
    /// - J2: 영주 성 잠긴 문 4개(집무실 VeryHard/금고 Legendary/무기고 Hard/문서고 Easy)가
    ///   통일 레이아웃의 로비 포탈 중심에 재정착 — 포탈 전체 봉쇄 콜라이더.
    /// </summary>
    public sealed class LordCastleUnificationTests
    {
        private const string TransitionPath = "Assets/Scripts/UI/IndoorSceneTransition.cs";
        private const string BuilderPath = "Assets/Scripts/Systems/CastleInteriorBuilder.cs";

        // ======================================================================
        // J1 — 진입 분기 통일
        // ======================================================================

        [Test]
        public void J1_Transition_UsesUnifiedBuilder_ForBothOwnershipStates()
        {
            string src = System.IO.File.ReadAllText(TransitionPath);
            Assert.That(src, Does.Contain("PlayerCastleInteriorBuilder.BuildPlayerCastleInterior(nation, layoutVariant)"),
                "성 진입은 소유 여부 무관 통일 빌더.");
            Assert.That(src, Does.Not.Contain("CastleInteriorBuilder.BuildCastleInterior"),
                "구 영주 성 셸 호출부 폐기(재추가 금지 — 영지 실내 통일 계약).");
            Assert.That(src, Does.Contain("AddLordLockedDoors(hqRoom)"),
                "잠긴 문 콘텐츠는 통일 레이아웃에 재정착되어 유지.");
        }

        [Test]
        public void J1_OldLordShell_NotInvokedAnywhere()
        {
            // 구 빌더의 공개 진입점은 완전 폐기됐어야 한다(잠금 콘텐츠만 생존).
            string src = System.IO.File.ReadAllText(BuilderPath);
            Assert.That(src, Does.Not.Contain("public static GameObject BuildCastleInterior"),
                "구 48×36 영주 성 셸 빌드 메서드 폐기.");
            Assert.That(src, Does.Contain("AddLordLockedDoors"), "잠금 재정착 진입점은 존재.");
        }

        // ======================================================================
        // J2 — 잠긴 문 재정착
        // ======================================================================

        [Test]
        public void J2_LordCastle_FourLockedDoors_AtPortalCenters()
        {
            GameObject room = PlayerCastleInteriorBuilder.BuildPlayerCastleInterior("Empire", 0);
            Assert.That(room, Is.Not.Null);
            try
            {
                CastleInteriorBuilder.AddLordLockedDoors(room);

                var expected = new (string wall, string lockId, LockpickingSystem.LockDifficulty diff)[]
                {
                    ("Lobby_North", "castle_lord_office", LockpickingSystem.LockDifficulty.VeryHard),
                    ("Lobby_East", "castle_vault", LockpickingSystem.LockDifficulty.Legendary),
                    ("Lobby_SouthWest", "castle_armory", LockpickingSystem.LockDifficulty.Hard),
                    ("Lobby_NorthWest", "castle_archive", LockpickingSystem.LockDifficulty.Easy),
                };

                foreach (var (wallName, lockId, difficulty) in expected)
                {
                    Transform door = room.transform.Find(wallName + "_LordLock");
                    Assert.That(door, Is.Not.Null, wallName + " 포탈에 잠긴 문이 존재해야 한다.");

                    var locked = door.GetComponent<LockedDoor>();
                    Assert.That(locked, Is.Not.Null, "LockedDoor 컴포넌트 필수.");
                    Assert.That(locked.LocationId, Is.EqualTo(lockId), "잠금 ID 계약 불변.");
                    Assert.That(locked.Difficulty, Is.EqualTo(difficulty), "잠금 난이도 계약 불변.");
                    Assert.That(door.GetComponent<LockedDoorCollision>(), Is.Not.Null,
                        "잠금 상태 동기화 콜라이더 필수.");

                    // 포탈 중심 배치 — 토폴로지 벽 스팬 중앙, 포탈 높이 중간(y=1.4).
                    Vector2 delta = Vector2.zero;
                    float offset = 0f;
                    bool found = false;
                    foreach (var w in PlayerCastleInteriorBuilder.GetTopologyWalls())
                    {
                        if (w.Name != wallName) continue;
                        delta = w.End - w.Start;
                        offset = w.DoorwayOffset;
                        found = true;
                    }
                    Assert.That(found, Is.True, wallName + " 벽은 토폴로지에 존재.");
                    Vector2 center = delta * 0.5f; // DoorwayOffset=0 → 스팬 중앙
                    Assert.That(door.localPosition.x,
                        Is.EqualTo(wallCenterX(wallName, center)).Within(0.05f), "문 x = 포탈 중심.");
                    Assert.That(door.localPosition.z,
                        Is.EqualTo(wallCenterZ(wallName, center)).Within(0.05f), "문 z = 포탈 중심.");
                    Assert.That(door.localPosition.y,
                        Is.EqualTo(PlayerCastleInteriorBuilder.PortalHeight * 0.5f).Within(0.02f),
                        "문 y = 포탈 높이 중간(바닥 0 ~ 2.8 스팬).");

                    // 포탈 전체 봉쇄 — 폭 3.4(포탈 3.2+틈), 높이 2.8 콜라이더.
                    var col = door.GetComponent<BoxCollider>();
                    Assert.That(col, Is.Not.Null);
                    Vector3 worldSize = Vector3.Scale(col.size, door.localScale);
                    Assert.That(worldSize.x, Is.GreaterThanOrEqualTo(PlayerCastleInteriorBuilder.PortalWidth),
                        "콜라이더가 포탈 폭을 전부 막아야 한다(측면 틈 금지).");
                    Assert.That(worldSize.y, Is.GreaterThanOrEqualTo(PlayerCastleInteriorBuilder.PortalHeight),
                        "콜라이더가 포탈 높이를 전부 막아야 한다.");
                }

                // 남쪽 입구 포탈은 개방(잠금 없음).
                Assert.That(room.transform.Find("Lobby_South_LordLock"), Is.Null,
                    "남쪽 입구는 개방 계약.");
                Assert.That(room.transform.Find("Lobby_SouthEast_LordLock"), Is.Null,
                    "연금술실 포탈은 개방(잠금 4곳만).");
            }
            finally
            {
                Object.DestroyImmediate(room);
            }
        }

        private static float wallCenterX(string wallName, Vector2 center)
        {
            foreach (var w in PlayerCastleInteriorBuilder.GetTopologyWalls())
                if (w.Name == wallName)
                    return w.Start.x + center.x;
            return 0f;
        }

        private static float wallCenterZ(string wallName, Vector2 center)
        {
            foreach (var w in PlayerCastleInteriorBuilder.GetTopologyWalls())
                if (w.Name == wallName)
                    return w.Start.y + center.y;
            return 0f;
        }
    }
}
