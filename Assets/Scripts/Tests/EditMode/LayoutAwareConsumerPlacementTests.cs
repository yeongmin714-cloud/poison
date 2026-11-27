using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Utils;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [2026-10-14 통합 — 레이아웃별 소비 정착] 계약 테스트.
    /// 대상: CastleInteriorBuilder.AddLordLockedDoors / InvadedLordEncounter / InteriorNationFlagSystem.
    /// - 잠긴 문 4개(잠금 ID·난이도 계약 불변)가 활성 레이아웃 정의의 실제 포탈 개구부 중심에 정렬.
    /// - 침략 영주 앵커가 정의의 LordBed(침실)에 정착 — 정의 존 판정으로 Bedroom 검증.
    /// - 국가 깃발이 정의의 로비 내부에 정착 — 지휘책상과의 충돌 회피까지 검증.
    /// - 컨텍스트가 없는 방은 기준형 KeepOctagon 폴백(구 호출자·레거시 테스트 호환).
    /// 최소 GO+컨텍스트 방만 사용 — 고비용 성 내부 씬 빌드 없음(헬퍼 검증만).
    /// 실행 판정은 QA 서브에이전트 증거로 확정한다(구현 서브에이전트는 실행하지 않음).
    /// </summary>
    public sealed class LayoutAwareConsumerPlacementTests
    {
        /// <summary>컨텍스트만 부착된 최소 방 — 고비용 성 내부 씬 빌드 없이 정의 연결만 검증.</summary>
        private static GameObject MakeContextRoom(InteriorLayoutDefinition definition)
        {
            var room = new GameObject("TestRoom_" + definition.LayoutId);
            room.AddComponent<InteriorLayoutContext>().Configure(definition);
            return room;
        }

        // ════════════════════════════════════════════════════════════════════
        // 1. 잠긴 문 — 활성 레이아웃 정의의 실제 포탈 개구부 중심 정렬
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void LockedDoors_AllLayouts_AlignToActualPortalOpenings_WithContractIds()
        {
            InteriorLayoutDefinition[] layouts =
            {
                InteriorLayoutDefinition.KeepOctagon,
                InteriorLayoutDefinition.RoundKeep,
                InteriorLayoutDefinition.AsymmetricManor,
            };

            (string lockId, string label, LockpickingSystem.LockDifficulty difficulty)[] contract =
            {
                ("castle_lord_office", null, LockpickingSystem.LockDifficulty.VeryHard),
                ("castle_vault", null, LockpickingSystem.LockDifficulty.Legendary),
                ("castle_armory", null, LockpickingSystem.LockDifficulty.Hard),
                ("castle_archive", null, LockpickingSystem.LockDifficulty.Easy),
            };

            foreach (InteriorLayoutDefinition layout in layouts)
            {
                GameObject room = MakeContextRoom(layout);
                try
                {
                    CastleInteriorBuilder.AddLordLockedDoors(room);

                    var placed = room.GetComponentsInChildren<LockedDoor>(true);
                    Assert.That(placed.Length, Is.EqualTo(4),
                        $"{layout.LayoutId}: 잠긴 문은 정확히 4개(남측 입구/연금술 포탈 개방 계약).");

                    foreach (LockedDoor door in placed)
                    {
                        string wallName = door.gameObject.name.Replace("_LordLock", "");
                        PlayerCastleInteriorBuilder.WallLayout wall = FindWall(layout, wallName);

                        Assert.That(wall.Name, Is.Not.Null,
                            $"{layout.LayoutId}: 문 '{wallName}'은 활성 레이아웃 정의의 실제 포탈에 대응.");
                        Assert.That(wall.DoorwayWidth, Is.GreaterThan(0f),
                            $"{layout.LayoutId}: 문이 붙은 벽은 실제 개구부(포탈)여야 한다.");
                        Assert.That(wall.ConnectedFrom, Is.EqualTo("Lobby"),
                            $"{layout.LayoutId}: 잠금 포탈은 로비에서 열린다.");

                        // 포탈 개구부 중심 — DoorwayOffset을 반영한 실제 절단 중심.
                        Vector3 expected = PortalCenter(wall);
                        Assert.That(door.transform.localPosition.x, Is.EqualTo(expected.x).Within(0.05f),
                            $"{layout.LayoutId}/{wallName}: 문 x = 활성 정의 포탈 개구부 중심.");
                        Assert.That(door.transform.localPosition.z, Is.EqualTo(expected.z).Within(0.05f),
                            $"{layout.LayoutId}/{wallName}: 문 z = 활성 정의 포탈 개구부 중심.");
                        Assert.That(door.transform.localPosition.y,
                            Is.EqualTo(PlayerCastleInteriorBuilder.PortalHeight * 0.5f).Within(0.02f),
                            $"{layout.LayoutId}/{wallName}: 문 y = 포탈 높이 중간.");

                        Assert.That(door.GetComponent<LockedDoorCollision>(), Is.Not.Null,
                            "잠금 상태 동기화 콜라이더 필수.");
                        Assert.That(door.GetComponent<NameplateDisplay>(), Is.Not.Null,
                            "이름표 컴포넌트 필수.");

                        // 포탈 전체 봉쇄 — 폭 3.4(포탈 3.2+틈), 높이 2.8 콜라이더.
                        var collider = door.GetComponent<BoxCollider>();
                        Assert.That(collider, Is.Not.Null);
                        Vector3 worldSize = Vector3.Scale(collider.size, door.transform.localScale);
                        Assert.That(worldSize.x,
                            Is.GreaterThanOrEqualTo(PlayerCastleInteriorBuilder.PortalWidth),
                            $"{layout.LayoutId}/{wallName}: 콜라이더가 포탈 폭을 전부 막아야 한다.");
                        Assert.That(worldSize.y,
                            Is.GreaterThanOrEqualTo(PlayerCastleInteriorBuilder.PortalHeight),
                            $"{layout.LayoutId}/{wallName}: 콜라이더가 포탈 높이를 전부 막아야 한다.");
                    }

                    // 잠금 계약 불변 — 집무실 VeryHard / 금고 Legendary / 무기고 Hard / 문서고 Easy.
                    bool sawOffice = false, sawVault = false, sawArmory = false, sawArchive = false;
                    foreach (LockedDoor door in placed)
                    {
                        switch (door.LocationId)
                        {
                            case "castle_lord_office":
                                sawOffice = true;
                                Assert.That(door.Difficulty, Is.EqualTo(LockpickingSystem.LockDifficulty.VeryHard));
                                break;
                            case "castle_vault":
                                sawVault = true;
                                Assert.That(door.Difficulty, Is.EqualTo(LockpickingSystem.LockDifficulty.Legendary));
                                break;
                            case "castle_armory":
                                sawArmory = true;
                                Assert.That(door.Difficulty, Is.EqualTo(LockpickingSystem.LockDifficulty.Hard));
                                break;
                            case "castle_archive":
                                sawArchive = true;
                                Assert.That(door.Difficulty, Is.EqualTo(LockpickingSystem.LockDifficulty.Easy));
                                break;
                            default:
                                Assert.Fail($"알 수 없는 잠금 ID: {door.LocationId}");
                                break;
                        }
                    }
                    Assert.That(sawOffice && sawVault && sawArmory && sawArchive, Is.True,
                        $"{layout.LayoutId}: 4개 잠금 ID가 모두 존재해야 한다.");

                    // 개방 포탈 계약 — 남측 입구/연금술실 포탈에는 문이 없다.
                    Assert.That(room.transform.Find("Lobby_South_LordLock"), Is.Null,
                        $"{layout.LayoutId}: 남쪽 입구는 개방 계약.");
                    Assert.That(room.transform.Find("Lobby_SouthEast_LordLock"), Is.Null,
                        $"{layout.LayoutId}: 연금술실 포탈은 개방(잠금 4곳만).");
                }
                finally
                {
                    Object.DestroyImmediate(room);
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // 2. 침략 영주 — 정의의 LordBed(침실) 앵커 정착 + 존 판정 오버로드
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void LordSpawnAnchor_AllLayouts_ResolvesToLayoutLordBedInBedroomZone()
        {
            InteriorLayoutDefinition[] layouts =
            {
                InteriorLayoutDefinition.KeepOctagon,
                InteriorLayoutDefinition.RoundKeep,
                InteriorLayoutDefinition.AsymmetricManor,
            };

            foreach (InteriorLayoutDefinition layout in layouts)
            {
                GameObject room = MakeContextRoom(layout);
                try
                {
                    Vector2 anchor = InvadedLordEncounter.GetNorthernBedroomAnchor(room);

                    // 정의의 LordBed 앵커와 일치 — 레이아웃별 소비 정착 계약.
                    Assert.That(anchor, Is.EqualTo(layout.GetAnchorPosition("LordBed")).Within(0.01f),
                        $"{layout.LayoutId}: 영주 앵커는 활성 정의의 LordBed 앵커를 따른다.");
                    Assert.That(layout.GetZoneAt(anchor), Is.EqualTo("Bedroom"),
                        $"{layout.LayoutId}: LordBed 앵커는 정의 판정상 Bedroom 존 내부.");

                    Assert.That(InvadedLordEncounter.IsNorthernBedroomPosition(room, anchor), Is.True,
                        $"{layout.LayoutId}: 앵커가 컨텍스트 오버로드 판정상 Bedroom.");
                    Assert.That(
                        InvadedLordEncounter.IsNorthernBedroomPosition(room, new Vector2(0f, 0f)), Is.False,
                        $"{layout.LayoutId}: 로비 중앙(0,0)은 Bedroom이 아니다.");
                }
                finally
                {
                    Object.DestroyImmediate(room);
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // 3. 국가 깃발 — 정의 로비 내부 정착 + 지휘책상 비충돌
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void FlagPosition_KeepOctagon_PreservesLegacyAnchor()
        {
            GameObject room = MakeContextRoom(InteriorLayoutDefinition.KeepOctagon);
            try
            {
                Vector3 local = InteriorNationFlagSystem.GetFlagLocalPositionForTest(room);

                Assert.That(local.x, Is.EqualTo(9f).Within(0.01f), "기준형 깃발 x = 구판 (9, 36.75) 유지.");
                Assert.That(local.z, Is.EqualTo(36.75f).Within(0.01f), "기준형 깃발 z = 구판 (9, 36.75) 유지.");
                Assert.That(local.y, Is.EqualTo(0f).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(room);
            }
        }

        [Test]
        public void FlagPosition_NonLegacyLayouts_ResolvesInsideLobbyWithoutDeskOverlap()
        {
            InteriorLayoutDefinition[] layouts =
            {
                InteriorLayoutDefinition.RoundKeep,
                InteriorLayoutDefinition.AsymmetricManor,
            };

            foreach (InteriorLayoutDefinition layout in layouts)
            {
                GameObject room = MakeContextRoom(layout);
                try
                {
                    Vector3 local = InteriorNationFlagSystem.GetFlagLocalPositionForTest(room);
                    var point = new Vector2(local.x, local.z);

                    Assert.That(layout.GetZoneAt(point), Is.EqualTo("Lobby"),
                        $"{layout.LayoutId}: 깃발 위치는 활성 정의 판정상 Lobby 내부.");
                    Assert.That(layout.IsInsideLobby(point), Is.True,
                        $"{layout.LayoutId}: 깃발은 로비 다각형 내부.");

                    // 지휘책상(정의 로비 앵커)과 충돌하지 않는다 — 비침입 반경 2m.
                    Vector2 desk = layout.GetAnchorPosition("CommandDesk");
                    float deskDistance = Vector2.Distance(point, desk);
                    Assert.That(deskDistance, Is.GreaterThanOrEqualTo(2f),
                        $"{layout.LayoutId}: 깃발-책상 거리 {deskDistance:0.00}m ≥ 2m (충돌 회피).");
                }
                finally
                {
                    Object.DestroyImmediate(room);
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // 4. 깃발 헬퍼 계약 — 별칭 동일성·null 폴백·책상 실측 우선순위
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void FlagHelpers_ResolveFlagLocalPosition_MatchesAlias_NullFallback_AndActualDeskPriority()
        {
            InteriorLayoutDefinition[] layouts =
            {
                InteriorLayoutDefinition.KeepOctagon,
                InteriorLayoutDefinition.RoundKeep,
                InteriorLayoutDefinition.AsymmetricManor,
            };

            foreach (InteriorLayoutDefinition layout in layouts)
            {
                GameObject room = MakeContextRoom(layout);
                try
                {
                    Assert.That(InteriorNationFlagSystem.ResolveFlagLocalPosition(room),
                        Is.EqualTo(InteriorNationFlagSystem.GetFlagLocalPositionForTest(room)),
                        $"{layout.LayoutId}: 두 헬퍼는 동일 해석(별칭 계약).");
                    Assert.That(InteriorNationFlagSystem.GetCommandDeskPositionForTest(room),
                        Is.EqualTo(layout.GetAnchorPosition("CommandDesk")),
                        $"{layout.LayoutId}: 책상 트랜스폼이 없는 방은 정의 앵커가 책상 기준.");
                }
                finally
                {
                    Object.DestroyImmediate(room);
                }
            }

            // null 룸 — 컨텍스트 부재 → 기준형 KeepOctagon 레거시 폴백(무예외 계약).
            Assert.That(InteriorNationFlagSystem.ResolveFlagLocalPosition(null),
                Is.EqualTo(new Vector3(9f, 0f, 36.75f)), "룸 null → 레거시 (9, 0, 36.75).");
            Assert.That(InteriorNationFlagSystem.GetCommandDeskPositionForTest(null),
                Is.EqualTo(new Vector2(0f, 36.75f)), "룸 null → 레거시 책상 (0, 36.75).");

            // 실제 CommandDesk 트랜스폼 우선 — 실측 위치 + (9, 0) 후보가 유효하면 그대로 채택.
            GameObject deskRoom = MakeContextRoom(InteriorLayoutDefinition.RoundKeep);
            try
            {
                GameObject desk = new GameObject("CommandDesk");
                desk.transform.SetParent(deskRoom.transform, false);
                desk.transform.localPosition = new Vector3(2f, 0f, 5f);

                Assert.That(InteriorNationFlagSystem.GetCommandDeskPositionForTest(deskRoom),
                    Is.EqualTo(new Vector2(2f, 5f)), "책상 실측 트랜스폼이 정의 앵커보다 우선.");

                Vector3 flag = InteriorNationFlagSystem.ResolveFlagLocalPosition(deskRoom);
                Assert.That(flag.x, Is.EqualTo(11f).Within(0.01f), "책상 실측 (2,5) + (9, 0) → (11, 5).");
                Assert.That(flag.z, Is.EqualTo(5f).Within(0.01f));
                Assert.That(flag.y, Is.EqualTo(0f).Within(0.01f));
                Assert.That(InteriorLayoutDefinition.RoundKeep.GetZoneAt(new Vector2(flag.x, flag.z)),
                    Is.EqualTo("Lobby"), "실측 채택 위치도 정의 판정상 Lobby.");
            }
            finally
            {
                Object.DestroyImmediate(deskRoom);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // 헬퍼
        // ════════════════════════════════════════════════════════════════════

        private static PlayerCastleInteriorBuilder.WallLayout FindWall(
            InteriorLayoutDefinition layout, string wallName)
        {
            foreach (PlayerCastleInteriorBuilder.WallLayout wall in layout.GetWalls())
                if (wall.Name == wallName)
                    return wall;
            return default;
        }

        private static Vector3 PortalCenter(PlayerCastleInteriorBuilder.WallLayout wall)
        {
            Vector2 delta = wall.End - wall.Start;
            Vector2 center = (wall.Start + wall.End) * 0.5f + delta.normalized * wall.DoorwayOffset;
            return new Vector3(center.x, PlayerCastleInteriorBuilder.PortalHeight * 0.5f, center.y);
        }
    }
}
