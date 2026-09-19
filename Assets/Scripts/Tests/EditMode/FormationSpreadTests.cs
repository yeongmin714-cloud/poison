using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// Phase O5: 군집 회피 — FormationSpread 링 분산 + SeparationSystem 순수 push 로직 테스트.
    /// MONSTER_SEPARATION.md 셀 점유 설계 이식 검증: 링 배치(중심/반경/각도), 결정론, count 절단,
    /// ComputePush 방향·크기 클램프·0거리 방어.
    /// </summary>
    public class FormationSpreadTests
    {
        private const float DefaultUnitRadius = 1.6f;
        private static readonly Vector3 Origin = Vector3.zero;

        // ── Distribute: 중심/개수 ──────────────────────────────────────

        [Test]
        public void Distribute_Count1_ReturnsCenterOnly()
        {
            var center = new Vector3(10f, 0f, 20f);
            var result = FormationSpread.Distribute(center, 1);

            Assert.AreEqual(1, result.Length, "count=1 → 지점 1개");
            Assert.AreEqual(center, result[0], "index 0 = 중심");
        }

        [Test]
        public void Distribute_Count7_CenterPlusRing1Of6()
        {
            var result = FormationSpread.Distribute(Origin, 7);

            Assert.AreEqual(7, result.Length, "count=7 → 중심 1 + 링1 6개");
            Assert.AreEqual(Origin, result[0], "index 0 = 중심");
            // 링1 반경 = unitRadius × 2
            foreach (int i in System.Linq.Enumerable.Range(1, 6))
            {
                float dist = Vector3.Distance(result[i], Origin);
                Assert.AreEqual(DefaultUnitRadius * 2f, dist, 0.1f, $"index {i} 링1 반경");
            }
        }

        [Test]
        public void Distribute_Count19_CenterPlusRing1AndRing2()
        {
            var result = FormationSpread.Distribute(Origin, 19);

            Assert.AreEqual(19, result.Length, "count=19 → 중심 1 + 링1 6 + 링2 12");
            Assert.AreEqual(Origin, result[0], "index 0 = 중심");
            // 링1: index 1~6
            foreach (int i in System.Linq.Enumerable.Range(1, 6))
            {
                float dist = Vector3.Distance(result[i], Origin);
                Assert.AreEqual(DefaultUnitRadius * 2f, dist, 0.1f, $"index {i} 링1 반경");
            }
            // 링2: index 7~18
            foreach (int i in System.Linq.Enumerable.Range(7, 12))
            {
                float dist = Vector3.Distance(result[i], Origin);
                Assert.AreEqual(DefaultUnitRadius * 3.5f, dist, 0.1f, $"index {i} 링2 반경");
            }
        }

        [Test]
        public void Distribute_Count0_ReturnsEmptyArray()
        {
            var result = FormationSpread.Distribute(Origin, 0);
            Assert.AreEqual(0, result.Length, "count=0 → 빈 배열");
        }

        [Test]
        public void Distribute_LengthAlwaysEqualsCount()
        {
            foreach (int count in new[] { 2, 7, 8, 19, 20, 25, 40 })
            {
                var result = FormationSpread.Distribute(Origin, count);
                Assert.AreEqual(count, result.Length, $"count={count} → 배열 길이 == count");
            }
        }

        // ── Distribute: 링 반경 (오차 0.1) ─────────────────────────────

        [Test]
        public void Distribute_Ring1_RadiusIsUnitRadiusTimes2()
        {
            var result = FormationSpread.Distribute(Origin, 40, DefaultUnitRadius);

            foreach (int i in System.Linq.Enumerable.Range(1, 6))
            {
                float dist = Vector3.Distance(result[i], Origin);
                Assert.AreEqual(DefaultUnitRadius * 2f, dist, 0.1f, $"index {i} (링1) 거리 ≈ unitRadius×2");
            }
        }

        [Test]
        public void Distribute_Ring2_RadiusIsUnitRadiusTimes3_5()
        {
            var result = FormationSpread.Distribute(Origin, 40, DefaultUnitRadius);

            foreach (int i in System.Linq.Enumerable.Range(7, 12))
            {
                float dist = Vector3.Distance(result[i], Origin);
                Assert.AreEqual(DefaultUnitRadius * 3.5f, dist, 0.1f, $"index {i} (링2) 거리 ≈ unitRadius×3.5");
            }
        }

        // ── Distribute: 링 내 각도 분산 (인접 각도차 일정) ─────────────

        [Test]
        public void Distribute_RingAngularStepConstant_60And30Degrees()
        {
            var result = FormationSpread.Distribute(Origin, 19, DefaultUnitRadius);

            // 링1 (index 1~6): 인접 각도차 = 60°
            AssertAngleStepConstant(result, 1, 6, 60f, "링1");
            // 링2 (index 7~18): 인접 각도차 = 30°
            AssertAngleStepConstant(result, 7, 18, 30f, "링2");
        }

        /// <summary>인덱스 범위의 XZ 각도(atan2)를 순회하며 인접 각도차가 step으로 일정한지 검증.</summary>
        private static void AssertAngleStepConstant(Vector3[] points, int from, int to, float stepDeg, string label)
        {
            float prevAngle = Mathf.Atan2(points[from].x, points[from].z) * Mathf.Rad2Deg;
            foreach (int i in System.Linq.Enumerable.Range(from + 1, to - from))
            {
                float angle = Mathf.Atan2(points[i].x, points[i].z) * Mathf.Rad2Deg;
                float diff = Mathf.Repeat(angle - prevAngle, 360f); // 360° 랩 처리
                Assert.AreEqual(stepDeg, diff, 0.5f, $"{label} index {i} 인접 각도차");
                prevAngle = angle;
            }
        }

        // ── 결정론 / 편의 메서드 ───────────────────────────────────────

        [Test]
        public void Distribute_IsDeterministic_SameInputSameOutput()
        {
            var center = new Vector3(5f, 1f, -3f);
            var a = FormationSpread.Distribute(center, 25);
            var b = FormationSpread.Distribute(center, 25);

            Assert.AreEqual(a.Length, b.Length);
            foreach (int i in System.Linq.Enumerable.Range(0, a.Length))
            {
                Assert.AreEqual(a[i], b[i], $"index {i} 동일 입력 → 동일 출력 (Random 없음)");
            }
        }

        [Test]
        public void OffsetFor_MatchesDistributeIndex()
        {
            var center = new Vector3(2f, 0f, 8f);
            int count = 7;
            var distributed = FormationSpread.Distribute(center, count);

            foreach (int i in System.Linq.Enumerable.Range(0, count))
            {
                var viaOffset = FormationSpread.OffsetFor(i, count, center);
                Assert.AreEqual(distributed[i], viaOffset, $"OffsetFor({i}) == Distribute[{i}]");
            }

            // 범위 백 방어: 잘못된 index/count는 center 반환
            Assert.AreEqual(center, FormationSpread.OffsetFor(-1, count, center), "음수 index → center");
            Assert.AreEqual(center, FormationSpread.OffsetFor(count, count, center), "index == count → center");
            Assert.AreEqual(center, FormationSpread.OffsetFor(0, 0, center), "count=0 → center");
        }

        // ── SeparationSystem 순수 로직 (ComputePush) ───────────────────

        [Test]
        public void ComputePush_DirectionIsAwayFromOther()
        {
            // push 방향 = (자신 − 상대) 정규화 — 서로 반대 방향으로 밀려남
            var self = new Vector3(1f, 0f, 1f);
            var other = new Vector3(3f, 0f, 1f);
            var push = SeparationSystem.ComputePush(self, other, 0.02f);

            var expectedDir = new Vector3(-1f, 0f, 0f); // self − other = (-2,0,0) → 정규화
            Assert.AreEqual(expectedDir.x * 0.02f, push.x, 0.0001f, "X 방향 × push 크기");
            Assert.AreEqual(0f, push.y, 0.0001f, "Y 성분 없음 (XZ 평면)");
            Assert.AreEqual(0f, push.z, 0.0001f, "Z 성분 없음");

            // 반대 쌍 대칭 확인 — other 쪽 push는 정반대
            var pushOther = SeparationSystem.ComputePush(other, self, 0.02f);
            Assert.AreEqual(-push.x, pushOther.x, 0.0001f, "서로 반대 방향 (대칭)");
        }

        [Test]
        public void ComputePush_MagnitudeAtMostPushAmount()
        {
            var self = new Vector3(0f, 5f, 0f);
            var other = new Vector3(0.01f, 5.3f, 0.01f); // 아주 가까움 + y 차이

            // 일반 케이스: 크기 == pushAmount
            var push = SeparationSystem.ComputePush(self, other, 0.02f);
            Assert.AreEqual(0.02f, push.magnitude, 0.0001f, "크기 == pushAmount (≤ 보장)");

            // 음수 pushAmount → 0 클램프
            var negative = SeparationSystem.ComputePush(self, other, -1f);
            Assert.AreEqual(Vector3.zero, negative, "음수 pushAmount → 0");
            Assert.LessOrEqual(negative.magnitude, 0.02f, "크기 ≤ pushAmount");
        }

        [Test]
        public void ComputePush_ZeroDistance_ReturnsZero()
        {
            var same = new Vector3(4f, 2f, 6f);
            var push = SeparationSystem.ComputePush(same, same, 0.02f);
            Assert.AreEqual(Vector3.zero, push, "동일 좌표(0거리) → 0 반환 (NaN 방어)");
        }
    }
}
