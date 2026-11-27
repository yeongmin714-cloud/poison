using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 15 — 실내 구조 통일] 영지 키 런타임 레이아웃 배정 계약 테스트.
    /// - SelectForTerritory: 영지 키 무관 단일 레이아웃 — 전 키가 기준형 KeepOctagon(플레이어 소속 영지와 동일 구조).
    ///   기존 FNV-1a 3분할 배정(KeepOctagon/RoundKeep/AsymmetricManor)은 폐기 — 사용자 지시.
    ///   결정론 계약은 유지(같은 키 → 항상 같은 정의 인스턴스 — 이제 전부 KeepOctagon).
    /// - IndoorSceneTransition 성 진입 분기가 선택 정의를 빌더 오버로드에 전달하는 소스 계약 유지.
    /// 실행 판정은 QA 서브에이전트 증거로 확정한다(구현 서브에이전트는 실행하지 않음).
    /// </summary>
    public sealed class InteriorLayoutAssignmentTests
    {
        private const string TransitionPath = "Assets/Scripts/UI/IndoorSceneTransition.cs";

        // ════════════════════════════════════════════════════════════════════
        // 1. 결정론 — 같은 키는 항상 같은 정의 인스턴스
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void SelectForTerritory_SameKey_ReturnsIdenticalDefinitionInstance()
        {
            InteriorLayoutDefinition first = InteriorLayoutDefinition.SelectForTerritory("East_01");
            InteriorLayoutDefinition second = InteriorLayoutDefinition.SelectForTerritory("East_01");

            Assert.That(second, Is.SameAs(first), "같은 영지 키 재호출은 동일 정의 인스턴스(결정론).");
            // [무번호 15] 전면 통일 — East_01도 기준형 KeepOctagon(기존 RoundKeep 배정 폐기).
            Assert.That(first.LayoutId, Is.EqualTo(InteriorLayoutDefinition.KeepOctagonLayoutId));
        }

        [Test]
        public void SelectForTerritory_RepeatedFullKeySet_MatchesFirstPass()
        {
            string[] keys = BuildRepresentativeKeys();

            string[] firstPass = new string[keys.Length];
            string[] secondPass = new string[keys.Length];
            for (int i = 0; i < keys.Length; i++)
                firstPass[i] = InteriorLayoutDefinition.SelectForTerritory(keys[i]).LayoutId;
            for (int i = 0; i < keys.Length; i++)
                secondPass[i] = InteriorLayoutDefinition.SelectForTerritory(keys[i]).LayoutId;

            Assert.That(secondPass, Is.EqualTo(firstPass),
                "전체 키 집합 반복 호출 — 재방문/세이브 재현 보장(배정 순열 불변).");
        }

        // ════════════════════════════════════════════════════════════════════
        // 2. 폴백 — null/빈 키는 기준형 KeepOctagon
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void SelectForTerritory_NullOrEmptyKey_FallsBackToKeepOctagon()
        {
            Assert.That(InteriorLayoutDefinition.SelectForTerritory(null),
                Is.SameAs(InteriorLayoutDefinition.KeepOctagon), "null 키는 기준형 폴백.");
            Assert.That(InteriorLayoutDefinition.SelectForTerritory(string.Empty),
                Is.SameAs(InteriorLayoutDefinition.KeepOctagon), "빈 키는 기준형 폴백.");
        }

        // ════════════════════════════════════════════════════════════════════
        // 3. 통일 — 실제 영지 키 형식 대표 집합 전부 기준형 KeepOctagon
        //    [무번호 15] 기존 3계열 균등 분포 검증 폐기 — 사용자 지시 "모든 실내 영지 동일 구조".
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void SelectForTerritory_RepresentativeTerritoryKeys_AllKeepOctagon_Unified()
        {
            string[] keys = BuildRepresentativeKeys();

            foreach (string key in keys)
            {
                InteriorLayoutDefinition assigned = InteriorLayoutDefinition.SelectForTerritory(key);
                Assert.That(assigned, Is.SameAs(InteriorLayoutDefinition.KeepOctagon),
                    $"영지 키 '{key}' — [무번호 15] 전 영지 기준형 KeepOctagon 통일.");
            }
        }

        /// <summary>
        /// 실제 영지 키 형식(TerritoryId.ToString → "{Nation}_{Index:D2}")의 대표 키 집합.
        /// 6개 국가 × 인덱스 1~3 = 18개 — 균등 분포 검증용 고정 세트.
        /// </summary>
        private static string[] BuildRepresentativeKeys()
        {
            string[] nations = { "East", "West", "South", "North", "Empire", "Dracula" };
            var keys = new string[nations.Length * 3];
            int index = 0;
            foreach (string nation in nations)
                for (int territoryIndex = 1; territoryIndex <= 3; territoryIndex++)
                    keys[index++] = $"{nation}_{territoryIndex:D2}";
            return keys;
        }

        // [무번호 15] FNV-1a 독립 구현 헬퍼(ExpectedLayoutId) 폐기 — 3분할 배정 규칙 자체가 폐기됨.

        // ════════════════════════════════════════════════════════════════════
        // 4. 소스 계약 — 성 진입 분기가 영지 키 선택 정의를 3인자 오버로드로 전달
        //    (런타임 실행 불가 EditMode 소스 검증 — LordCastleUnificationTests와 동일 기법)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void Transition_CastleBranch_PassesTerritorySelectedLayoutToBuilderOverload()
        {
            string src = System.IO.File.ReadAllText(TransitionPath);

            Assert.That(src, Does.Contain(
                    "InteriorLayoutDefinition.SelectForTerritory(_pendingTerritoryKey)"),
                "성 진입은 _pendingTerritoryKey로 결정론 레이아웃 계열을 선택해야 한다.");

            Assert.That(src, Does.Contain(
                    "PlayerCastleInteriorBuilder.BuildPlayerCastleInterior(nation, layoutVariant, interiorLayout, _pendingTerritoryKey)"),
                "선택된 정의는 3인자 명시 오버로드로 빌더에 전달되어야 한다. [F4] 4인자 확장 — 실제 영지 키도 전달.");

            Assert.That(src, Does.Contain("ComputeLayoutVariant(_pendingTerritoryKey"),
                "가구 variant(0~7) 해시는 영지 키 기반 기존 축으로 유지된다.");
        }
    }
}
