using System;
using System.Collections.Generic;
using NUnit.Framework;
using ProjectName.Core.Data;
using ProjectName.Core.Utils; // [T1-2] 산포 API 계약 인접성 — TerritoryScatterMath 교체 대상 문맥 명시
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [2026-10-15 T1-2 — 영지 밴드 산포 배치] 계약 테스트.
    /// TerritoryDatabase.GenerateAllDefinitions의 좌표 산출이 링 고정 반경×지터(원 배치)에서
    /// 밴드 산포(TerritoryScatterMath.TryScatter)로 교체된 것을 검증한다.
    ///
    /// 검증 축:
    ///  - BandMembership:  산포 영지 80개(82 − Empire − Dracula)가 자기 난이도 밴드 ±0.5m 이내
    ///    (밴드 테이블: Ring4 50–380 / Ring3 380–750 / Ring2 750–1150 / Ring1 1150–1550m —
    ///     프로덕션 GetRingBand와 동일 상수를 테스트에 독립 복제해 교차 검증)
    ///  - BandMonotonic:   Ring4 최대 ≤ Ring3 최소 ≤ Ring2 최소 ≤ Ring1 최소 (±0.5 허용)
    ///  - GlobalMinSpacing: 82개 전 쌍 O(n²) 간격 ≥ 119.999m (부동소수 오차 허용 하한)
    ///  - SpawnAvoid:      스폰(728,−529)으로부터 전 영지 ≥ 100m
    ///  - WedgeMembership: 각도→국가 판정(중앙 50m=Empire, 사분면 경계 45/135/225/315°)을 테스트에
    ///    독립 재구현해 def.worldPosition 판정 == def.nation 교차 검증.
    ///    NationTerrainController.GetNationFromPosition은 static으로 확인되어(파일 1006행) 2차 대조로도 사용.
    ///    (InteriorLayoutAssignmentTests의 독립 재구현 컨벤션 따름)
    ///  - Determinism:     reflection으로 private 생성자 호출 → 신규 인스턴스 좌표가 싱글턴과 전부 동일.
    ///    (코드 확인: private 생성자가 InitializeDefinitions() → GenerateAllDefinitions()를 자동 실행하므로
    ///     인스턴스 생성만으로 정의가 채워진다. 프로덕션에 테스트용 API 추가 없음.)
    ///  - FixedAnchors:    Empire=(0,0), Dracula=1350m ± 0.5 & 150° ± 0.5°
    ///  - Count:           전체 82개 (4국 × 4링 × 5 + 2)
    ///
    /// 실행 판정은 QA 서브에이전트 증거로 확정한다(구현 서브에이전트는 실행하지 않음).
    /// </summary>
    public sealed class TerritoryScatterPlacementTests
    {
        // ════════════════════════════════════════════════════════════════════
        // 스펙 상수 — 프로덕션(GetRingBand / GenerateAllDefinitions 산포 상수)과
        // 동일 값을 테스트에 독립 복제한다(교차 검증 방식 — 구현 의존 아님).
        // ════════════════════════════════════════════════════════════════════

        private const float Ring4BandMin = 50f;    // Ring4 = (50, 380) — 황제국 인접 최난
        private const float Ring4BandMax = 380f;
        private const float Ring3BandMin = 380f;   // Ring3 = (380, 750)
        private const float Ring3BandMax = 750f;
        private const float Ring2BandMin = 750f;   // Ring2 = (750, 1150)
        private const float Ring2BandMax = 1150f;
        private const float Ring1BandMin = 1150f;  // Ring1 = (1150, 1550) — 최외곽 최약
        private const float Ring1BandMax = 1550f;

        private const float BandTolerance = 0.5f; // 밴드 경계 부동소수 허용 오차(m)

        private const float GlobalMinSpacing = 120f;   // 프로덕션 산포 최소 간격과 동일
        private const float SpacingFloor = 119.999f;   // 간격 하한(부동소수 오차 허용)
        private static readonly Vector2 SpawnPoint = new Vector2(728f, -529f); // 플레이어 스폰(XZ 규약)
        private const float SpawnAvoidRadius = 100f;   // 스폰 회피 반경

        private const float DraculaDistance = 1350f;   // 드라큘라 성 고정 앵커
        private const float DraculaAngleDeg = 150f;

        // ════════════════════════════════════════════════════════════════════
        // 1. Count — 전체 82개 (4국 × 4링 × 5 + 황제국 + 드라큘라)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void Count_AllTerritories_82TotalWith20PerNation()
        {
            var defs = AllDefs();

            Assert.That(defs.Count, Is.EqualTo(82), "4국 × 4링 × 5 + 황제국 1 + 드라큘라 1 = 82개.");
            Assert.That(defs.FindAll(d => d.nation == NationType.East).Count, Is.EqualTo(20), "동국 20개.");
            Assert.That(defs.FindAll(d => d.nation == NationType.West).Count, Is.EqualTo(20), "서국 20개.");
            Assert.That(defs.FindAll(d => d.nation == NationType.South).Count, Is.EqualTo(20), "남국 20개.");
            Assert.That(defs.FindAll(d => d.nation == NationType.North).Count, Is.EqualTo(20), "북국 20개.");
            Assert.That(defs.FindAll(d => d.nation == NationType.Empire).Count, Is.EqualTo(1), "황제국 1개.");
            Assert.That(defs.FindAll(d => d.nation == NationType.Dracula).Count, Is.EqualTo(1), "드라큘라 1개.");
        }

        // ════════════════════════════════════════════════════════════════════
        // 2. BandMembership — 산포 영지 80개가 자기 난이도 밴드 ±0.5m 이내
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void BandMembership_AllScatteredTerritories_WithinOwnBand()
        {
            var scattered = ScatteredDefs();
            Assert.That(scattered.Count, Is.EqualTo(80), "산포 대상은 80개(82 − Empire − Dracula).");

            foreach (TerritoryDefinition def in scattered)
            {
                (float min, float max) = BandOf(def.difficulty);
                float dist = XZDistance(def.worldPosition, Vector2.zero);
                Assert.That(dist, Is.InRange(min - BandTolerance, max + BandTolerance),
                    $"{def.id} 거리 {dist:F1}m ∉ {def.difficulty} 밴드 [{min:F0},{max:F0}] ±0.5m.");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // 3. BandMonotonic — 안쪽 링 최대거리 ≤ 바깥 링 최소거리 (±0.5 허용)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void BandMonotonic_InnerRingMax_AtOrBelowOuterRingMins()
        {
            var scattered = ScatteredDefs();

            float ring4Max = MaxXZDistance(scattered, TerritoryDifficulty.Ring4);
            float ring3Min = MinXZDistance(scattered, TerritoryDifficulty.Ring3);
            float ring2Min = MinXZDistance(scattered, TerritoryDifficulty.Ring2);
            float ring1Min = MinXZDistance(scattered, TerritoryDifficulty.Ring1);

            Assert.That(ring4Max, Is.LessThanOrEqualTo(ring3Min + BandTolerance),
                $"Ring4 최대 {ring4Max:F1}m ≤ Ring3 최소 {ring3Min:F1}m(±0.5 허용) — 안쪽 링이 더 가깝다.");
            Assert.That(ring3Min, Is.LessThanOrEqualTo(ring2Min + BandTolerance),
                $"Ring3 최소 {ring3Min:F1}m ≤ Ring2 최소 {ring2Min:F1}m(±0.5 허용).");
            Assert.That(ring2Min, Is.LessThanOrEqualTo(ring1Min + BandTolerance),
                $"Ring2 최소 {ring2Min:F1}m ≤ Ring1 최소 {ring1Min:F1}m(±0.5 허용).");
        }

        // ════════════════════════════════════════════════════════════════════
        // 4. GlobalMinSpacing — 82개 전 쌍 O(n²) 간격 ≥ 119.999m
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void GlobalMinSpacing_All82TerritoryPairs_AtLeast120m()
        {
            var defs = AllDefs();
            Assert.That(defs.Count, Is.EqualTo(82), "전 쌍 검증 대상은 82개 영지(Empire/Dracula 포함).");

            int violations = 0;
            string firstViolation = null;
            for (int i = 0; i < defs.Count; i++)
            {
                for (int j = i + 1; j < defs.Count; j++)
                {
                    float dist = XZDistance(defs[i].worldPosition, defs[j].worldPosition);
                    if (dist < SpacingFloor)
                    {
                        violations++;
                        firstViolation ??= $"{defs[i].id} ↔ {defs[j].id} 간격 {dist:F3}m";
                    }
                }
            }

            Assert.That(violations, Is.EqualTo(0),
                $"120m 미만 쌍 {violations}건 발견(최초 위반: {firstViolation ?? "없음"}) — 산포 최소 간격 120m 위반.");
        }

        // ════════════════════════════════════════════════════════════════════
        // 5. SpawnAvoid — 전 영지가 스폰(728,−529)으로부터 ≥ 100m
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void SpawnAvoid_AllTerritories_AtLeast100mFromSpawnPoint()
        {
            foreach (TerritoryDefinition def in AllDefs())
            {
                float dist = XZDistance(def.worldPosition, SpawnPoint);
                Assert.That(dist, Is.GreaterThanOrEqualTo(SpawnAvoidRadius),
                    $"{def.id}가 스폰(728,−529)에서 {dist:F1}m — 회피 반경 100m 위반.");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // 6. WedgeMembership — 위치의 각도 판정 국가 == 영지 소속 국가
        //    (독립 재구현 1차 + NationTerrainController static 2차 교차 검증)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void WedgeMembership_ScatteredPositions_JudgeOwnNation()
        {
            foreach (TerritoryDefinition def in ScatteredDefs())
            {
                NationType judged = IndependentJudgeNation(def.worldPosition);
                Assert.That(judged, Is.EqualTo(def.nation),
                    $"{def.id} 위치({def.worldPosition.x:F1},{def.worldPosition.z:F1}) — 독립 각도 판정({judged})이 " +
                    $"소속 국가({def.nation})와 불일치(쐐기 이탈).");

                // 2차 교차 검증 — GetNationFromPosition은 static으로 확인(코드 1006행)되어 직접 대조 가능.
                // 산포 쐐기(기저각 ±39°)는 사분면 경계(±45°) 안쪽이므로 지형 판정과도 일치해야 한다.
                Assert.That(NationTerrainController.GetNationFromPosition(def.worldPosition), Is.EqualTo(def.nation),
                    $"{def.id} 위치가 NationTerrainController 지형 국가 판정과 불일치.");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // 7. Determinism — reflection 신규 인스턴스 좌표 == 싱글턴 좌표 (dist < 1e-6)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void Determinism_ReflectedFreshInstance_MatchesSingletonCoordinates()
        {
            // private 생성자가 InitializeDefinitions() → GenerateAllDefinitions()를 자동 실행하므로
            // 신규 인스턴스 생성만으로 정의가 채워진다(프로덕션 테스트용 API 추가 없이 reflection 사용).
            var fresh = (TerritoryDatabase)Activator.CreateInstance(typeof(TerritoryDatabase), nonPublic: true);

            var singletonPositions = KeyedPositions(TerritoryDatabase.Instance);
            var freshPositions = KeyedPositions(fresh);

            Assert.That(freshPositions.Keys, Is.EquivalentTo(singletonPositions.Keys),
                "reflection 신규 인스턴스와 싱글턴의 영지 키 집합이 동일해야 한다.");

            foreach (var kvp in singletonPositions)
            {
                float dist = Vector3.Distance(kvp.Value, freshPositions[kvp.Key]);
                Assert.That(dist, Is.LessThan(1e-6),
                    $"{kvp.Key} 좌표 불일치(결정론 위반): 싱글턴 {kvp.Value} vs 신규 {freshPositions[kvp.Key]} (거리 {dist}m).");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // 8. FixedAnchors — Empire=(0,0), Dracula=1350m ± 0.5 & 150° ± 0.5°
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void FixedAnchors_EmpireAtOrigin_DraculaAt1350mAngle150()
        {
            TerritoryDefinition empire = TerritoryDatabase.Instance.GetDefinition(NationType.Empire, 1);
            Assert.That(XZDistance(empire.worldPosition, Vector2.zero), Is.LessThanOrEqualTo(1e-4),
                "황제국은 중심 (0,0) 고정 앵커(밴드 산포 대상 아님).");

            TerritoryDefinition dracula = TerritoryDatabase.Instance.GetDefinition(NationType.Dracula, 1);
            float dist = XZDistance(dracula.worldPosition, Vector2.zero);
            Assert.That(dist, Is.EqualTo(DraculaDistance).Within(BandTolerance),
                $"드라큘라 성 거리 {dist:F1}m — 고정 앵커 1350m ± 0.5.");

            float angleDeg = Mathf.Atan2(dracula.worldPosition.z, dracula.worldPosition.x) * Mathf.Rad2Deg;
            if (angleDeg < 0f) angleDeg += 360f;
            Assert.That(angleDeg, Is.EqualTo(DraculaAngleDeg).Within(0.5),
                $"드라큘라 성 각도 {angleDeg:F2}° — 고정 앵커 150° ± 0.5°(동쪽 기준 반시계/Atan2(z,x) 규약).");
        }

        // ════════════════════════════════════════════════════════════════════
        // 내부 헬퍼 — 스펙의 독립 재구현(교차 검증용)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Instance의 전체 정의 스냅샷(Dictionary 순서 비의존).</summary>
        private static List<TerritoryDefinition> AllDefs()
        {
            return new List<TerritoryDefinition>(TerritoryDatabase.Instance.GetAllDefinitions());
        }

        /// <summary>산포 대상 정의 — 고정 앵커(Empire/Dracula) 제외한 4국 80개.</summary>
        private static List<TerritoryDefinition> ScatteredDefs()
        {
            return AllDefs().FindAll(d =>
                d.nation != NationType.Empire && d.nation != NationType.Dracula);
        }

        /// <summary>
        /// 밴드 테이블 — 프로덕션 GetRingBand와 동일 상수의 테스트 독립 복제(교차 검증 방식).
        /// Ring4=(50,380) / Ring3=(380,750) / Ring2=(750,1150) / Ring1=(1150,1550), Empire=(0,0) 폴백.
        /// </summary>
        private static (float min, float max) BandOf(TerritoryDifficulty difficulty)
        {
            switch (difficulty)
            {
                case TerritoryDifficulty.Ring4: return (Ring4BandMin, Ring4BandMax);
                case TerritoryDifficulty.Ring3: return (Ring3BandMin, Ring3BandMax);
                case TerritoryDifficulty.Ring2: return (Ring2BandMin, Ring2BandMax);
                case TerritoryDifficulty.Ring1: return (Ring1BandMin, Ring1BandMax);
                default: return (0f, 0f);
            }
        }

        /// <summary>
        /// 각도→국가 판정의 독립 재구현 — NationTerrainController.GetNationFromPosition 문서 규칙
        /// (중앙 50m=Empire, 사분면 경계 45/135/225/315°, Atan2(z,x) 도 단위)의 테스트 로컬 구현.
        /// </summary>
        private static NationType IndependentJudgeNation(Vector3 pos)
        {
            float dist = Mathf.Sqrt(pos.x * pos.x + pos.z * pos.z);
            if (dist < 50f)
                return NationType.Empire;

            float angle = Mathf.Atan2(pos.z, pos.x) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;

            if (angle < 45f || angle >= 315f)
                return NationType.East;
            if (angle < 135f)
                return NationType.North;
            if (angle < 225f)
                return NationType.West;
            return NationType.South;
        }

        /// <summary>XZ 평면 거리 — Vector2(x, z) 좌표 규약(Vector2의 y = 세계 Z).</summary>
        private static float XZDistance(Vector3 from, Vector2 to)
        {
            float dx = from.x - to.x;
            float dz = from.z - to.y;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>XZ 평면 거리 — Vector3끼리(y 성분 무시, 항상 0).</summary>
        private static float XZDistance(Vector3 from, Vector3 to)
        {
            float dx = from.x - to.x;
            float dz = from.z - to.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>해당 난이도 산포 영지들의 최소 원점 거리(집합 크기 20 보장 — 빈 집합 방어).</summary>
        private static float MinXZDistance(List<TerritoryDefinition> defs, TerritoryDifficulty difficulty)
        {
            float min = float.MaxValue;
            int count = 0;
            foreach (TerritoryDefinition def in defs)
            {
                if (def.difficulty != difficulty) continue;
                count++;
                min = Mathf.Min(min, XZDistance(def.worldPosition, Vector2.zero));
            }
            Assert.That(count, Is.EqualTo(20), $"{difficulty} 산포 영지는 20개(4국 × 5)여야 한다.");
            return min;
        }

        /// <summary>해당 난이도 산포 영지들의 최대 원점 거리(집합 크기 20 보장 — 빈 집합 방어).</summary>
        private static float MaxXZDistance(List<TerritoryDefinition> defs, TerritoryDifficulty difficulty)
        {
            float max = float.MinValue;
            int count = 0;
            foreach (TerritoryDefinition def in defs)
            {
                if (def.difficulty != difficulty) continue;
                count++;
                max = Mathf.Max(max, XZDistance(def.worldPosition, Vector2.zero));
            }
            Assert.That(count, Is.EqualTo(20), $"{difficulty} 산포 영지는 20개(4국 × 5)여야 한다.");
            return max;
        }

        /// <summary>영지 키 → 월드 좌표 사전(결정론 비교용 — Dictionary 순서 비의존).</summary>
        private static Dictionary<string, Vector3> KeyedPositions(TerritoryDatabase db)
        {
            var map = new Dictionary<string, Vector3>();
            foreach (TerritoryDefinition def in db.GetAllDefinitions())
                map[def.id.ToString()] = def.worldPosition;
            return map;
        }
    }
}
