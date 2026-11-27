using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [T3-1] VillagePlacementSystem 6→20 확장 + 촌락(isHamlet) 판정 + 대표 마을 Ring1 이동 검증.
    /// public static 직접 호출(Unity 에디터 실행 없이 순수 계산만 검증).
    /// ⚠ 실행 판정은 QA 서브에이전트 증거로 확정(구현 서브에이전트는 실행하지 않음).
    ///
    /// 검증 범위:
    ///  1) Count80 — GetAllVillages() 총 80개 (4국가 × 20, 영지당 1마을)
    ///  2) Deterministic — 두 호출 결과 좌표+순서 완전 동일(1e-4)
    ///  3) CastleOffset — 각 마을 center↔castleCenter 거리 ∈ [37.9, 46.1]
    ///  4) TerritoryClearance — 마을 center↔타 영지 worldPosition ≥ 73.9m
    ///     (자기 기준 성 제외, Empire/Dracula 포함 TerritoryDatabase 전체 대조)
    ///  5) HamletFlag — [T3-1 실측 매핑] Ring4(황제국 인접) = 영지 인덱스 16~20만 isHamlet,
    ///     촌락 총 20개 (국가당 5 × 4국가)
    ///  6) HamletSpacing — 촌락끼리 전 쌍 ≥ 43.9m (반경 22×2 = 44에서 여유 0.1m)
    ///  7) Representative — 국가당 정확히 1개, isHamlet=false,
    ///     기준 영지 ∈ Ring1(인덱스 1~5 — [T3-1] 실측 매핑, 최외곽 안전 마을)
    ///
    /// 링 매핑 실측 근거: TerritoryDatabase 산포 루프(ring 3→0, index = ring·5+t+1,
    /// difficulty = (TerritoryDifficulty)ring)에서 Ring4(밴드 50~380m)가 인덱스 16~20,
    /// Ring1(밴드 1150~1550m)이 인덱스 1~5에 배정된다(2026-10-15 시뮬 /tmp/t3_1_sim.log).
    ///
    /// EditMode 함정 대응: TerritoryDatabase는 코드 생성 DB — Instance 접근 시 정의 자동 생성
    /// (기존 컨벤션, EditMode에서 안전). VillagePlacementSystem은 public static 결정론 조회이므로
    /// 씬/MonoBehaviour 의존 없음.
    /// </summary>
    public sealed class VillagePlacementTests
    {
        /// <summary>결정론 비교 허용 오차(m) — 1e-4.</summary>
        private const float DeterminismTolerance = 1e-4f;
        /// <summary>기준 영지 성 좌표 매칭 허용 오차(m) — castleCenter는 def.worldPosition과 동일 값.</summary>
        private const float CastleMatchTolerance = 0.5f;
        /// <summary>촌락 간 최소 간격 요구 (m) — 반경 22×2 = 44에서 여유 0.1m.</summary>
        private const float HamletSpacingMin = 43.9f;
        /// <summary>마을↔타 영지 최소 간격 요구 (m) — 영지 간 120 − 오프셋 최대 46 = 74에서 여유 0.1m.</summary>
        private const float TerritoryClearanceMin = 73.9f;
        /// <summary>촌락 판정 영지 인덱스 범위 — [T3-1 실측] Ring4 = 16~20.</summary>
        private const int HamletIndexMin = 16;
        private const int HamletIndexMax = 20;
        /// <summary>대표 마을 기준 영지 인덱스 범위 — [T3-1 실측] Ring1 = 1~5.</summary>
        private const int RepresentativeIndexMin = 1;
        private const int RepresentativeIndexMax = 5;

        /// <summary>GetAllVillages 조회 헬퍼 — 테스트 대상 public static 직접 호출.</summary>
        private static List<VillagePlacementSystem.VillageInfo> AllVillages()
        {
            return VillagePlacementSystem.GetAllVillages();
        }

        /// <summary>
        /// 마을의 기준 영지 정의 조회 — castleCenter가 일치하는 같은 국가 정의를 탐색한다.
        /// TerritoryDatabase.Instance는 코드 생성 DB로 EditMode에서 자동 생성된다(기존 컨벤션).
        /// </summary>
        private static bool TryResolveBaseTerritory(VillagePlacementSystem.VillageInfo village, out TerritoryDefinition def)
        {
            var db = TerritoryDatabase.Instance;
            Assert.IsNotNull(db, "TerritoryDatabase.Instance가 EditMode에서 자동 생성되어야 한다 (코드 생성 DB 컨벤션)");
            foreach (var candidate in db.GetAllDefinitions())
            {
                if (candidate.id.nation != village.nation)
                    continue;
                if (Vector3.Distance(candidate.worldPosition, village.castleCenter) <= CastleMatchTolerance)
                {
                    def = candidate;
                    return true;
                }
            }
            def = default;
            return false;
        }

        /// <summary>1) 전체 마을 수 80 (4국가 × 20) + 국가별 VillagesPerNation 일치.</summary>
        [Test]
        public void Count80_AllNationsTwentyVillagesEach()
        {
            var all = AllVillages();
            Assert.AreEqual(80, all.Count,
                $"전체 마을 수는 4국가 × 20 = 80이어야 한다 (실제 {all.Count})");

            foreach (NationType nation in new[] { NationType.East, NationType.West, NationType.South, NationType.North })
            {
                var list = VillagePlacementSystem.GetVillages(nation);
                Assert.AreEqual(VillagePlacementSystem.VillagesPerNation, list.Count,
                    $"{nation} 국가 마을 수가 VillagesPerNation({VillagePlacementSystem.VillagesPerNation})과 다르다");
            }
        }

        /// <summary>2) 두 호출 결과 좌표+순서 완전 동일 (dist < 1e-4).</summary>
        [Test]
        public void Deterministic_TwoCallsProduceIdenticalVillages()
        {
            var first = AllVillages();
            var second = AllVillages();

            Assert.AreEqual(first.Count, second.Count, "두 호출의 마을 수가 다르다");
            for (int i = 0; i < first.Count; i++)
            {
                Assert.AreEqual(first[i].nation, second[i].nation, $"마을 #{i} 국가가 두 호출 간 다르다");
                Assert.AreEqual(first[i].index, second[i].index, $"마을 #{i} 인덱스가 두 호출 간 다르다");
                Assert.AreEqual(first[i].isHamlet, second[i].isHamlet, $"마을 #{i} isHamlet이 두 호출 간 다르다");
                Assert.AreEqual(first[i].isRepresentative, second[i].isRepresentative, $"마을 #{i} isRepresentative가 두 호출 간 다르다");

                float centerDist = Vector3.Distance(first[i].center, second[i].center);
                float castleDist = Vector3.Distance(first[i].castleCenter, second[i].castleCenter);
                Assert.GreaterOrEqual(DeterminismTolerance, centerDist,
                    $"마을 #{i} center가 두 호출 간 다르다 ({centerDist:F6}m)");
                Assert.GreaterOrEqual(DeterminismTolerance, castleDist,
                    $"마을 #{i} castleCenter가 두 호출 간 다르다 ({castleDist:F6}m)");
            }
        }

        /// <summary>3) 각 마을 center↔castleCenter 거리 ∈ [37.9, 46.1] (오프셋 규약 38~46 + 여유).</summary>
        [Test]
        public void CastleOffset_EachVillageWithinOffsetBandFromOwnCastle()
        {
            foreach (var v in AllVillages())
            {
                float d = Vector3.Distance(v.center, v.castleCenter);
                Assert.GreaterOrEqual(d, 37.9f,
                    $"마을 {v.nation}#{v.index} 성 오프셋 {d:F2}m이 하한 37.9m 미만이다");
                Assert.LessOrEqual(d, 46.1f,
                    $"마을 {v.nation}#{v.index} 성 오프셋 {d:F2}m이 상한 46.1m 초과이다");
            }
        }

        /// <summary>
        /// 4) 마을 center ↔ 타 영지 worldPosition ≥ 73.9m.
        /// 자기 기준 성(같은 국가 + 동일 좌표)은 제외하고 Empire(0,0)/드라큘라 성 포함 전체 대조.
        /// </summary>
        [Test]
        public void TerritoryClearance_VillageCentersClearOfOtherTerritories()
        {
            var db = TerritoryDatabase.Instance;
            Assert.IsNotNull(db, "TerritoryDatabase.Instance가 EditMode에서 자동 생성되어야 한다");
            var allDefs = new List<TerritoryDefinition>(db.GetAllDefinitions());

            foreach (var v in AllVillages())
            {
                foreach (var def in allDefs)
                {
                    // 자기 기준 성 제외 — 같은 국가이면서 castleCenter와 일치하는 정의
                    if (def.id.nation == v.nation &&
                        Vector3.Distance(def.worldPosition, v.castleCenter) <= CastleMatchTolerance)
                        continue;

                    float d = Vector3.Distance(v.center, def.worldPosition);
                    Assert.GreaterOrEqual(d, TerritoryClearanceMin,
                        $"마을 {v.nation}#{v.index} ↔ 영지 {def.id} 간격 {d:F2}m이 하한 {TerritoryClearanceMin}m 미만이다");
                }
            }
        }

        /// <summary>
        /// 5) 촌락 판정 — [T3-1 실측 매핑] Ring4(영지 인덱스 16~20) 마을만 isHamlet=true.
        /// villageIndex 15~19(영지 16~20) true / 0~14 false, 촌락 총 20개 (국가당 5 × 4국가).
        /// 기준 영지는 castleCenter로 TerritoryDatabase에서 역 조회해 난이도(Ring4)까지 교차 확인.
        /// </summary>
        [Test]
        public void HamletFlag_OnlyRing4TerritoryVillagesAreHamlets()
        {
            int hamletCount = 0;
            foreach (var v in AllVillages())
            {
                bool found = TryResolveBaseTerritory(v, out var def);
                Assert.IsTrue(found,
                    $"마을 {v.nation}#{v.index}의 기준 영지를 castleCenter({v.castleCenter.x:F1},{v.castleCenter.z:F1})로 조회하지 못했다");
                Assert.AreEqual(v.index + 1, def.id.index,
                    $"마을 {v.nation}#{v.index}의 기준 영지 인덱스가 1:1 대응({v.index + 1})과 다르다");

                // [T3-1 실측 매핑] 영지 인덱스 16~20 = Ring4(황제국 인접) = 촌락
                bool expected = def.id.index >= HamletIndexMin && def.id.index <= HamletIndexMax;
                Assert.AreEqual(expected, v.isHamlet,
                    $"마을 {v.nation}#{v.index}(영지 {def.id.index}) isHamlet 기대값 {expected}");

                if (v.isHamlet)
                {
                    hamletCount++;
                    Assert.AreEqual(TerritoryDifficulty.Ring4, def.difficulty,
                        $"촌락 {v.nation}#{v.index}의 기준 영지 난이도가 Ring4이어야 한다 (실제 {def.difficulty})");
                    Assert.AreEqual(VillagePlacementSystem.HamletRadius, v.radius,
                        $"촌락 {v.nation}#{v.index} 반경은 HamletRadius(22m)여야 한다");
                }
                else
                {
                    Assert.AreNotEqual(TerritoryDifficulty.Ring4, def.difficulty,
                        $"비촌락 {v.nation}#{v.index}의 기준 영지 난이도가 Ring4이면 안 된다");
                    Assert.AreEqual(VillagePlacementSystem.VillageRadius, v.radius,
                        $"일반 마을 {v.nation}#{v.index} 반경은 VillageRadius(40m)여야 한다");
                }
            }
            Assert.AreEqual(20, hamletCount,
                $"촌락 총 수는 국가당 5 × 4국가 = 20이어야 한다 (실제 {hamletCount})");

            // 국가당 촌락 5개 확인
            foreach (NationType nation in new[] { NationType.East, NationType.West, NationType.South, NationType.North })
            {
                int perNation = 0;
                foreach (var v in VillagePlacementSystem.GetVillages(nation))
                    if (v.isHamlet) perNation++;
                Assert.AreEqual(5, perNation, $"{nation} 국가 촌락 수는 5(Ring4 영지 5개)여야 한다");
            }
        }

        /// <summary>6) 촌락끼리 전 쌍 간격 ≥ 43.9m (반경 22×2 = 44 규약).</summary>
        [Test]
        public void HamletSpacing_AllHamletPairsAtLeast43_9m()
        {
            var hamlets = new List<VillagePlacementSystem.VillageInfo>();
            foreach (var v in AllVillages())
                if (v.isHamlet) hamlets.Add(v);
            Assert.AreEqual(20, hamlets.Count, "촌락 수 선행 조건(20개) 실패");

            for (int i = 0; i < hamlets.Count; i++)
            {
                for (int j = i + 1; j < hamlets.Count; j++)
                {
                    float d = Vector3.Distance(hamlets[i].center, hamlets[j].center);
                    Assert.GreaterOrEqual(d, HamletSpacingMin,
                        $"촌락 {hamlets[i].nation}#{hamlets[i].index} ↔ {hamlets[j].nation}#{hamlets[j].index} 간격 {d:F2}m이 하한 {HamletSpacingMin}m 미만이다");
                }
            }
        }

        /// <summary>
        /// 7) 대표 마을 — 국가당 정확히 1개, isHamlet=false,
        /// 기준 영지 ∈ Ring1(인덱스 1~5 — [T3-1] 실측 매핑의 최외곽 안전 마을).
        /// </summary>
        [Test]
        public void Representative_ExactlyOnePerNationOnRing1()
        {
            foreach (NationType nation in new[] { NationType.East, NationType.West, NationType.South, NationType.North })
            {
                var list = VillagePlacementSystem.GetVillages(nation);
                var reps = new List<VillagePlacementSystem.VillageInfo>();
                foreach (var v in list)
                    if (v.isRepresentative) reps.Add(v);

                Assert.AreEqual(1, reps.Count,
                    $"{nation} 국가 대표 마을은 정확히 1개여야 한다 (실제 {reps.Count})");
                Assert.IsFalse(reps[0].isHamlet, $"{nation} 대표 마을은 촌락(Ring4)이면 안 된다");

                bool found = TryResolveBaseTerritory(reps[0], out var def);
                Assert.IsTrue(found, $"{nation} 대표 마을의 기준 영지를 조회하지 못했다");
                Assert.GreaterOrEqual(def.id.index, RepresentativeIndexMin,
                    $"{nation} 대표 마을 기준 영지 인덱스 {def.id.index} — Ring1(1~5)이어야 한다");
                Assert.LessOrEqual(def.id.index, RepresentativeIndexMax,
                    $"{nation} 대표 마을 기준 영지 인덱스 {def.id.index} — Ring1(1~5)이어야 한다");
                Assert.AreEqual(TerritoryDifficulty.Ring1, def.difficulty,
                    $"{nation} 대표 마을 기준 영지 난이도는 Ring1(최외곽·최안전)이어야 한다");
            }
        }
    }
}
