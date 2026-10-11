// 2026-10-09: 4방위국 아레나 — 베팅 로비(셸·GLB 규약·베팅NPC·투기장 문) + 투기장(ArenaPit) 검증.
using UnityEngine;
using ProjectName.Systems;
using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    public class ArenaInteriorTests
    {
        [Test]
        public void ArenaGlbKey_MapsFourNations()
        {
            Assert.That(ArenaInteriorBuilder.ArenaGlbKey("Eastern"), Is.EqualTo("arena_east"), "동방 아레나 GLB 규약: arena_east.glb");
            Assert.That(ArenaInteriorBuilder.ArenaGlbKey("Western"), Is.EqualTo("arena_west"), "서방 아레나 GLB 규약: arena_west.glb");
            Assert.That(ArenaInteriorBuilder.ArenaGlbKey("Southern"), Is.EqualTo("arena_south"), "남방 아레나 GLB 규약: arena_south.glb");
            Assert.That(ArenaInteriorBuilder.ArenaGlbKey("Northern"), Is.EqualTo("arena_north"), "북방 아레나 GLB 규약: arena_north.glb");
            Assert.That(ArenaInteriorBuilder.ArenaGlbKey("Empire"), Is.EqualTo("arena_empire"), "아우레우스 아레나 GLB 규약: arena_empire.glb");
        }

        [Test]
        public void ArenaPitGlbKey_MapsFourNations()
        {
            Assert.That(ArenaPitInteriorBuilder.ArenaPitGlbKey("Eastern"), Is.EqualTo("arena_pit_east"), "동방 투기장 GLB 규약");
            Assert.That(ArenaPitInteriorBuilder.ArenaPitGlbKey("Northern"), Is.EqualTo("arena_pit_north"), "북방 투기장 GLB 규약");
        }

        [Test]
        public void ArenaTypes_RegisteredForEnterBuildingSwitch()
        {
            Assert.That(IndoorTransitionSetup.TYPE_ARENA, Is.EqualTo("Arena"));
            Assert.That(IndoorTransitionSetup.TYPE_ARENA_PIT, Is.EqualTo("ArenaPit"));
        }

        [Test]
        public void BuildArenaInterior_CreatesBettingLobbyWithNpcAndPitDoor()
        {
            var room = ArenaInteriorBuilder.BuildArenaInterior("Eastern");
            Assert.That(room, Is.Not.Null, "아레나 베팅 로비가 생성되어야 한다.");
            Assert.That(room.name, Is.EqualTo("ArenaRoom"));
            Assert.That(room.transform.Find("ArenaSouthEntry_Left"), Is.Not.Null, "진입로 왼쪽 경간 존재");
            Assert.That(room.transform.Find("ArenaSouthEntry_Right"), Is.Not.Null, "진입로 오른쪽 경간 존재");
            Assert.That(room.transform.Find("ArenaBettingDesk"), Is.Not.Null, "베팅 데스크 존재");
            Assert.That(room.transform.Find("ArenaNpc"), Is.Not.Null, "아레나 관리자 NPC 존재");
            Assert.That(room.transform.Find("ArenaPit_Trigger"), Is.Not.Null, "투기장 출입문 트리거 존재");
        }

        [Test]
        public void BuildArenaPitInterior_CreatesPitWithStageAndExit()
        {
            var room = ArenaPitInteriorBuilder.BuildArenaPitInterior("Western");
            Assert.That(room, Is.Not.Null, "투기장 실내가 생성되어야 한다.");
            Assert.That(room.name, Is.EqualTo("ArenaPitRoom"));
            Assert.That(room.transform.Find("ArenaPit_Stage"), Is.Not.Null, "중앙 무대 존재");
            Assert.That(room.transform.Find("ArenaPit_ExitDoor"), Is.Not.Null, "출구 문 존재");
            var exitTrigger = room.transform.Find("ArenaPit_ExitDoor");
            if (exitTrigger != null)
            {
                var tr = exitTrigger.gameObject.GetComponent<BuildingTrigger>();
                Assert.That(tr, Is.Not.Null, "출구 문에 BuildingTrigger 존재");
            }
        }
    }
}