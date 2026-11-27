// [123차] Phase 2 — 월드맵 영지 마커 호버 영주 툴팁 계약 테스트.
// 목적: WorldMapTerritoryTooltipContent 포맷터 계약(4줄 구조/영주 라인/PlayerOwned="플레이어"/
//       Unoccupied="무주"/ownerLordName 빈 값 폴백) 동결 + WorldMapWindowUTK 호버 배선 소스 계약.
// 패턴 선례: WorldMapSceneWiringTests(소스-텍스트 계약) / TerritoryTooltipContentTests(포맷터 직접 호출).
using System.IO;
using NUnit.Framework;
using UnityEngine;
using ProjectName.Core.Data;
using ProjectName.UI.Toolkit;

namespace ProjectName.Tests.EditMode
{
    public class WorldMapTerritoryTooltipContentTests
    {
        private const string WorldMapSourcePath = "Assets/Scripts/UI/Toolkit/WorldMapWindowUTK.cs";

        private static TerritoryDefinition MakeDef()
        {
            return new TerritoryDefinition
            {
                id = new TerritoryId(NationType.East, 1),
                territoryName = "동쪽 평화로운 초원",
                nation = NationType.East,
                difficulty = TerritoryDifficulty.Ring1,
                guardCount = 10,
                lord = new LordInfo { lordName = "홍 경" },
            };
        }

        private static string[] BuildLines(TerritoryDefinition def, TerritoryState state)
        {
            return WorldMapTerritoryTooltipContent.Build(def, state).Split('\n');
        }

        [Test]
        public void Build_HasFourLineStructure()
        {
            // 라인 구조 계약: ①영지명 ②국가 ③영주: ④소유 상태
            string[] lines = BuildLines(MakeDef(), new TerritoryState());
            Assert.AreEqual(4, lines.Length, "툴팁은 4줄이어야 함");
            Assert.AreEqual("동쪽 평화로운 초원", lines[0], "라인 1 = 영지명");
            StringAssert.Contains("동국", lines[1], "라인 2 = 국가 한글명");
            StringAssert.StartsWith("영주: ", lines[2], "라인 3 = 영주 라인");
            Assert.IsNotEmpty(lines[3], "라인 4 = 소유 상태");
        }

        [Test]
        public void Build_PlayerOwned_ShowsPlayer()
        {
            var state = new TerritoryState { ownership = TerritoryOwnership.PlayerOwned };
            string[] lines = BuildLines(MakeDef(), state);
            Assert.AreEqual("영주: 플레이어", lines[2], "PlayerOwned는 플레이어 표기");
            Assert.AreEqual("플레이어 소유", lines[3], "라인 4 소유 상태");
        }

        [Test]
        public void Build_Unoccupied_ShowsUnowned()
        {
            var state = new TerritoryState { ownership = TerritoryOwnership.Unoccupied };
            string[] lines = BuildLines(MakeDef(), state);
            Assert.AreEqual("영주: 무주", lines[2], "Unoccupied는 무주 표기");
            Assert.AreEqual("무주", lines[3]);
        }

        [Test]
        public void Build_LordOwned_ShowsTrackedOwnerLord()
        {
            // [123차] Phase 1 추적 필드 소비 — AI 점령 시 승리 영주명 반영
            var state = new TerritoryState
            {
                ownership = TerritoryOwnership.LordOwned,
                ownerLordName = "이 방",
            };
            string[] lines = BuildLines(MakeDef(), state);
            Assert.AreEqual("영주: 이 방", lines[2], "ownerLordName 추적값 표기");
            Assert.AreEqual("영주 소유", lines[3]);
        }

        [Test]
        public void Build_EmptyTrackedName_FallsBackToDefinitionLord()
        {
            // 구세이브/초기화 전 폴백 — 원래 영주(def.lord.lordName) 표기
            var state = new TerritoryState
            {
                ownership = TerritoryOwnership.LordOwned,
                ownerLordName = string.Empty,
            };
            string[] lines = BuildLines(MakeDef(), state);
            Assert.AreEqual("영주: 홍 경", lines[2], "ownerLordName 빈 값은 def.lord.lordName 폴백");
        }

        [Test]
        public void WorldMapWindowUTK_ContainsHoverTooltipWiring()
        {
            // 소스 계약 — TerritoryMarker에 PointerEnter/Move/Leave 배선 + UTKTooltip/포맷터 연결 존재
            string src = File.ReadAllText(Path.Combine(Application.dataPath, "..", WorldMapSourcePath));
            StringAssert.Contains("RegisterCallback<PointerEnterEvent>(_ => ShowTerritoryTooltip())", src);
            StringAssert.Contains("RegisterCallback<PointerMoveEvent>(_ => ShowTerritoryTooltip())", src);
            StringAssert.Contains("RegisterCallback<PointerLeaveEvent>(_ => UTKTooltip.Hide())", src);
            StringAssert.Contains("UTKTooltip.Show(WorldMapTerritoryTooltipContent.Build(Def, state)", src,
                "호버 시점 실시간 조회 배선 없음");
        }
    }
}
