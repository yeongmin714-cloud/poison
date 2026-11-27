// [124차-ImplB] 월드맵 우측 QuestDetailPanel 실데이터 포맷터 계약 테스트 — 순수 함수(위조 금지 계약) 검증.
// 대상: WorldMapTerritoryIntelContent (배지/영주 이름/병사 수/평균레벨/전투력/브리핑 본문).
// 런타임 렌더는 Play 게이트 — 본 파일은 순수 포맷터 계약만 단언한다.
// 패턴 소스: WorldMapTerritoryTooltipContentTests.cs ([123차] 선례).
using System.Collections.Generic;
using NUnit.Framework;
using ProjectName.Core.Data;
using ProjectName.UI.Toolkit;

namespace ProjectName.Tests.EditMode
{
    public class WorldMapTerritoryIntelContentTests
    {
        private static TerritoryDefinition MakeDef(string name)
        {
            var def = new TerritoryDefinition();
            def.territoryName = name;
            return def;
        }

        private static TerritoryState MakeState(TerritoryOwnership ownership, string trackedLord)
        {
            var state = new TerritoryState(new TerritoryId(NationType.East, 1));
            state.ownership = ownership;
            state.ownerLordName = trackedLord;
            return state;
        }

        // ===== 배지 — 123차 OwnershipStateLine 계약 재사용 =====
        [Test]
        public void OwnershipBadge_PlayerOwned_ReturnsPlayerOwnedText()
        {
            StringAssert.AreEqualIgnoringCase("플레이어 소유", WorldMapTerritoryIntelContent.OwnershipBadgeText(TerritoryOwnership.PlayerOwned));
        }

        [Test]
        public void OwnershipBadge_LordOwned_ReturnsLordOwnedText()
        {
            StringAssert.AreEqualIgnoringCase("영주 소유", WorldMapTerritoryIntelContent.OwnershipBadgeText(TerritoryOwnership.LordOwned));
        }

        // ===== 영주 이름 — 123차 ownerLordName 매핑 계약 =====
        [Test]
        public void LordName_PlayerOwned_ReturnsPlayer()
        {
            var def = MakeDef("동쪽 테스트 초원");
            var state = MakeState(TerritoryOwnership.PlayerOwned, "세드릭 경");
            StringAssert.AreEqualIgnoringCase("플레이어", WorldMapTerritoryIntelContent.LordNameValue(def, state));
        }

        [Test]
        public void LordName_Unoccupied_ReturnsEmpty()
        {
            var def = MakeDef("동쪽 테스트 초원");
            var state = MakeState(TerritoryOwnership.Unoccupied, "세드릭 경");
            StringAssert.AreEqualIgnoringCase("무주", WorldMapTerritoryIntelContent.LordNameValue(def, state));
        }

        [Test]
        public void LordName_LordOwned_ReturnsTrackedOwnerLord()
        {
            var def = MakeDef("동쪽 테스트 초원");
            var state = MakeState(TerritoryOwnership.LordOwned, "세드릭 경");
            StringAssert.AreEqualIgnoringCase("세드릭 경", WorldMapTerritoryIntelContent.LordNameValue(def, state));
        }

        [Test]
        public void LordName_EmptyTracked_FallsBackToDefinitionLord()
        {
            var def = MakeDef("동쪽 테스트 초원");
            def.lord = new LordInfo { lordName = "홍 경" };
            var state = MakeState(TerritoryOwnership.LordOwned, "");
            StringAssert.AreEqualIgnoringCase("홍 경", WorldMapTerritoryIntelContent.LordNameValue(def, state));
        }

        [Test]
        public void LordName_NoFallbackValue_ReturnsDash()
        {
            var def = MakeDef("동쪽 테스트 초원");
            var state = MakeState(TerritoryOwnership.Contested, "");
            StringAssert.AreEqualIgnoringCase("—", WorldMapTerritoryIntelContent.LordNameValue(def, state));
        }

        // ===== 병사 수 — 실배치 집계 우선, def.guardCount 폴백 =====
        [Test]
        public void SoldierCount_DeployedFirst()
        {
            StringAssert.AreEqualIgnoringCase("5명", WorldMapTerritoryIntelContent.SoldierCountValue(5, 12));
        }

        [Test]
        public void SoldierCount_FallsBackToGuardCount()
        {
            StringAssert.AreEqualIgnoringCase("12명", WorldMapTerritoryIntelContent.SoldierCountValue(0, 12));
        }

        [Test]
        public void SoldierCount_Zero_ReturnsDash()
        {
            StringAssert.AreEqualIgnoringCase("—", WorldMapTerritoryIntelContent.SoldierCountValue(0, 0));
        }

        // ===== 평균레벨 =====
        [Test]
        public void AvgLevel_ComputesMean()
        {
            StringAssert.AreEqualIgnoringCase("Lv. 15.0",
                WorldMapTerritoryIntelContent.SoldierAvgLevelValue(new List<int> { 10, 20 }));
        }

        [Test]
        public void AvgLevel_Empty_ReturnsDash()
        {
            StringAssert.AreEqualIgnoringCase("—", WorldMapTerritoryIntelContent.SoldierAvgLevelValue(new List<int>()));
            StringAssert.AreEqualIgnoringCase("—", WorldMapTerritoryIntelContent.SoldierAvgLevelValue(null));
        }

        // ===== 전투력 — 기존 산식 합산 값 그대로 표기 =====
        [Test]
        public void CombatPower_Positive_FormatsF1()
        {
            StringAssert.AreEqualIgnoringCase("1234.5", WorldMapTerritoryIntelContent.CombatPowerValue(1234.5f));
        }

        [Test]
        public void CombatPower_ZeroOrNegative_ReturnsDash()
        {
            StringAssert.AreEqualIgnoringCase("—", WorldMapTerritoryIntelContent.CombatPowerValue(0f));
            StringAssert.AreEqualIgnoringCase("—", WorldMapTerritoryIntelContent.CombatPowerValue(-1f));
        }

        // ===== 브리핑 본문 — 기존 데이터 조합 4행 =====
        [Test]
        public void Briefing_Peacetime_ContainsFourRealDataLines()
        {
            var def = MakeDef("동쪽 테스트 초원");
            var state = MakeState(TerritoryOwnership.LordOwned, "세드릭 경");
            string body = WorldMapTerritoryIntelContent.BriefingBody(def, state, 3);
            StringAssert.Contains("소유 상태: 영주 소유", body);
            StringAssert.Contains("난이도:", body);
            StringAssert.Contains("수비대: 3명", body);
            StringAssert.Contains("전쟁 여부: 평시", body);
        }

        [Test]
        public void Briefing_Contested_ReportsWar()
        {
            var def = MakeDef("동쪽 테스트 초원");
            var state = MakeState(TerritoryOwnership.Contested, "");
            string body = WorldMapTerritoryIntelContent.BriefingBody(def, state, 0);
            StringAssert.Contains("전쟁 여부: 전쟁 중", body);
        }
    }
}