using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [157차 P-C2] 동굴 레이아웃 풀 검증기 — 신규 20종 셀맵 + 선택 계약:
    /// ①선택 결정론(동일 territoryId 동일 결과·클래식 영지 false·범위 밖 폴백) ②Random API 0건(소스계약)
    /// ③클래식 상수 존재 ④해시 단일소스. 맵 지오메트리 검증(연결성/면적/횟불/앵커)은 생성기 실측으로 승계
    /// (docs/plans/2026-11-06_157_cave_layout_pool_20.txt — 생성기가 전종목 검증 통과 후 산출).
    /// </summary>
    public sealed class Cave157LayoutPoolTests
    {
        [Test]
        public void Pool_Select_IsDeterministic_And_ClassicTerritories_Passthrough()
        {
            bool first = CaveLayoutPool.TrySelect("empire_1_cave", 2, out var a);
            bool second = CaveLayoutPool.TrySelect("empire_1_cave", 2, out var b);
            Assert.That(first, Is.True, "[157차] 일반 영지는 풀 선택(true)");
            Assert.That(second, Is.True);
            Assert.That(b, Is.EqualTo(a), "[157차] 동일 territoryId 재선택 → 동일 셀맵(세이브 일치 결정론)");

            Assert.That(CaveLayoutPool.TrySelect("test", 1, out _), Is.False,
                "[157차] 클래식 영지(test)는 기본 시퀀스 경로(false — 계약테스트 핀 보존)");
            Assert.That(CaveLayoutPool.TrySelect("cave_test", 3, out _), Is.False,
                "[157차] 클래식 영지(cave_test)는 기본 시퀀스 경로(false)");
            Assert.That(CaveLayoutPool.TrySelect("empire_1_cave", 0, out _), Is.False,
                "[157차] 범위 밖 층 → 클래식 폴백(false)");
        }

        [Test]
        public void Pool_Cellmap_Dimensions_And_SpawnAnchor()
        {
            // 풀 선택 맵 — 48×36 전층 + S 앵커 존재(스폰 경로의 전제 계약).
            Assert.That(CaveLayoutPool.TrySelect("empire_2_cave", 3, out var cells), Is.True);
            Assert.That(cells.Length, Is.EqualTo(36), "[157차] 셀맵 36행");
            foreach (var row in cells)
                Assert.That(row.Length, Is.EqualTo(48), "[157차] 셀맵 48열 — 셀 2m 경계 96×72m");
            bool hasSpawn = false;
            foreach (var row in cells)
                if (row.Contains("S")) hasSpawn = true;
            Assert.That(hasSpawn, Is.True, "[157차] 풀 맵 S 스폰 앵커 필수(GetFloorSpawnPosition 경로 전제)");
        }

        [Test]
        public void Pool_SourceContract_NoRandomApi_And_ClassicConst()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/CaveLayoutPool.cs");
            Assert.That(src.Contains("Random.Range"), Is.False, "[157차] Random API 금지 계약");
            Assert.That(src.Contains("Random.value"), Is.False, "[157차] Random API 금지 계약(value)");
            Assert.That(src, Does.Contain("ClassicTerritory1 = \"test\""),
                "[157차] 클래식 영지 상수 — 기본 시퀀스 보존 계약");
            Assert.That(src, Does.Contain("Hash32"),
                "[157차] 해시 결정론 단일소스(FNV-1a) 존재");
        }
    }
}
