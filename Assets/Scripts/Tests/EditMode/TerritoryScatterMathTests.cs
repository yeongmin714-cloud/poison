using System.Collections.Generic;
using NUnit.Framework;
using ProjectName.Core.Utils;
using static ProjectName.Core.Utils.TerritoryScatterMath;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [2026-10-15 79차 T1-1 — 영지 산포 순수 수학] 계약 테스트.
    /// - 밴드 클램프: 결과 거리 ∈ [bandMin, bandMax] (부동소수 오차 ±0.01 허용).
    /// - 쐐기 마진: 결과 각도 ∈ wedgeBaseDeg ± (45−margin) — East/North/West/South 기저각 전부 검증,
    ///   West(180)/South(270) 포함 시 랩어라웃(±180° 경계) 없음 확인.
    /// - 최소 간격: taken 점 주변 minSpacing 배제 — 밴드가 taken 점과 겹치는 실제 리젝션 케이스 사용.
    /// - 회피점: 스폰 지점(728,−529) 100m 배제 규약 + 밴드 내부 회피점 실제 리젝션 케이스.
    /// - 결정론: 동일 시드 재시작 → 동일 시퀀스.
    /// - 폴백: maxTries 소진 시 밴드 중간 거리 + 쐐기 기저각 좌표, false 반환 (예외 없음).
    /// - null 처리: taken=null은 간격 조건 통과, rng=null은 ArgumentException.
    /// 실행 판정은 QA 서브에이전트 증거로 확정한다(구현 서브에이전트는 실행하지 않음).
    /// </summary>
    public sealed class TerritoryScatterMathTests
    {
        private const float BandTolerance = 0.01f;      // 밴드 경계 부동소수 허용 오차
        private const float SpacingTolerance = 0.001f;  // 간격 판정 허용 오차 (119.999 ≒ 120)

        // ════════════════════════════════════════════════════════════════════
        // 1. 밴드 클램프 — 결과 거리는 항상 밴드 안
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void TryScatter_BandDistanceStaysInsideBand_AcrossSeededSamples()
        {
            ScatterInput input = MakeInput(300f, 650f, 0f); // Ring3 밴드, East 쐐기
            System.Random rng = new System.Random(1234);

            for (int i = 0; i < 200; i++)
            {
                bool ok = TerritoryScatterMath.TryScatter(
                    input, rng, null, 0f, Vector2.zero, 0f, 200, out Vector2 result);

                Assert.That(ok, Is.True, $"taken=null·회피 0이면 첫 후보 즉시 확정이어야 한다 (i={i}).");
                float magnitude = result.magnitude;
                Assert.That(magnitude, Is.InRange(300f - BandTolerance, 650f + BandTolerance),
                    $"결과 거리는 밴드 [300, 650] 안이어야 한다 (i={i}, r={magnitude}).");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // 2. 쐐기 마진 — 결과 각도는 쐐기 마진 안 (국가 기저각 전부, 랩어라웃 없음)
        // ════════════════════════════════════════════════════════════════════

        [TestCase(0f)]    // East (−45~45)
        [TestCase(90f)]   // North (45~135)
        [TestCase(180f)]  // West (135~225) — ±180° 경계 랩어라웃 없음 확인
        [TestCase(270f)]  // South (225~315) — ±180° 경계 랩어라웃 없음 확인
        public void TryScatter_NationWedgeBase_KeepsAngleInsideMarginSpan(float wedgeBaseDeg)
        {
            const float margin = 6f;
            ScatterInput input = MakeInput(100f, 400f, wedgeBaseDeg);
            System.Random rng = new System.Random(5678);

            for (int i = 0; i < 120; i++)
            {
                bool ok = TerritoryScatterMath.TryScatter(
                    input, rng, null, 0f, Vector2.zero, 0f, 50, out Vector2 result);

                Assert.That(ok, Is.True, $"taken=null이면 확정 성공이어야 한다 (base={wedgeBaseDeg}, i={i}).");

                float angleDeg = AngleOfDeg(result); // Atan2(y, x)·Rad2Deg — −180~180
                float diff = AngleDiffFromBaseDeg(wedgeBaseDeg, angleDeg); // 기저각 기준 각차

                Assert.That(Mathf.Abs(diff), Is.LessThanOrEqualTo(45f - margin + BandTolerance),
                    $"각차는 ±(45−{margin})={45f - margin}° 안이어야 한다 (base={wedgeBaseDeg}, i={i}, diff={diff}).");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // 3. 최소 간격 — taken 점 주변 minSpacing 배제
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void TryScatter_TakenPoint_ExcludesMinSpacingNeighborhood()
        {
            // 밴드 [50, 300]이 taken 점 (60, 0)과 겹치므로 실제 리젝션이 발생하는 케이스다.
            ScatterInput input = MakeInput(50f, 300f, 0f);
            System.Random rng = new System.Random(2024);
            List<Vector2> taken = new List<Vector2> { new Vector2(60f, 0f) };

            for (int i = 0; i < 200; i++)
            {
                bool ok = TerritoryScatterMath.TryScatter(
                    input, rng, taken, 120f, Vector2.zero, 0f, 200, out Vector2 result);

                Assert.That(ok, Is.True, $"200회 내 확정 실패가 없어야 한다 (i={i}).");
                float dist = Vector2.Distance(result, taken[0]);
                Assert.That(dist, Is.GreaterThanOrEqualTo(120f - SpacingTolerance),
                    $"taken 점 (60,0)에서 최소 120m 떨어져야 한다 (i={i}, dist={dist}).");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // 4. 회피점 — 스폰 지점 배제 규약 + 밴드 내부 회피점 실제 리젝션
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void TryScatter_SpawnAvoidPoint_ExcludesAvoidRadius()
        {
            ScatterInput input = MakeInput(50f, 300f, 0f);
            System.Random rng = new System.Random(42);
            Vector2 avoidPoint = new Vector2(728f, -529f); // 스폰 지점 규약

            for (int i = 0; i < 200; i++)
            {
                bool ok = TerritoryScatterMath.TryScatter(
                    input, rng, null, 0f, avoidPoint, 100f, 200, out Vector2 result);

                Assert.That(ok, Is.True, $"밴드 밖 회피점이면 확정 성공이어야 한다 (i={i}).");
                float dist = Vector2.Distance(result, avoidPoint);
                Assert.That(dist, Is.GreaterThanOrEqualTo(100f - SpacingTolerance),
                    $"스폰 지점에서 최소 100m 떨어져야 한다 (i={i}, dist={dist}).");
            }
        }

        [Test]
        public void TryScatter_AvoidPointInsideBand_CausesRealRejection()
        {
            // 회피점을 밴드 내부에 두면 리젝션이 실제로 작동함을 검증한다.
            ScatterInput input = MakeInput(50f, 300f, 0f);
            System.Random rng = new System.Random(7);
            Vector2 avoidPoint = new Vector2(100f, 0f);

            for (int i = 0; i < 60; i++)
            {
                bool ok = TerritoryScatterMath.TryScatter(
                    input, rng, null, 0f, avoidPoint, 100f, 200, out Vector2 result);

                Assert.That(ok, Is.True, $"200회 내 확정 실패가 없어야 한다 (i={i}).");
                float dist = Vector2.Distance(result, avoidPoint);
                Assert.That(dist, Is.GreaterThanOrEqualTo(100f - SpacingTolerance),
                    $"밴드 내부 회피점에서도 최소 반경을 지켜야 한다 (i={i}, dist={dist}).");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // 5. 결정론 — 동일 시드 재시작 = 동일 시퀀스
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void TryScatter_SameSeedRestart_ProducesIdenticalSequence()
        {
            ScatterInput input = MakeInput(50f, 300f, 0f);
            System.Random first = new System.Random(987);
            System.Random second = new System.Random(987);

            for (int i = 0; i < 20; i++)
            {
                TerritoryScatterMath.TryScatter(input, first, null, 0f, Vector2.zero, 0f, 64, out Vector2 a);
                TerritoryScatterMath.TryScatter(input, second, null, 0f, Vector2.zero, 0f, 64, out Vector2 b);

                Assert.That(Vector2.Distance(a, b), Is.LessThan(1e-6f),
                    $"동일 시드 2회 시퀀스는 원소 단위로 동일해야 한다 (i={i}).");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // 6. 폴백 — maxTries 소진 시 밴드 중간 + 쐐기 기저각, false 반환
        // ════════════════════════════════════════════════════════════════════

        [TestCase(0f)]    // East 폴백: (475, 0)
        [TestCase(270f)]  // South 폴백: (0, −475) — 각도 계산 랩어라웃 없음 확인
        public void TryScatter_MaxTriesExhausted_FallsBackToBandMidpointAtWedgeBase(float wedgeBaseDeg)
        {
            const float bandMin = 300f;
            const float bandMax = 650f;
            ScatterInput input = MakeInput(bandMin, bandMax, wedgeBaseDeg);
            System.Random rng = new System.Random(11);

            // 회피 반경이 밴드 최대 거리보다 크면 어떤 후보도 채택 불가 → maxTries 소진 보장
            bool ok = TerritoryScatterMath.TryScatter(
                input, rng, null, 0f, Vector2.zero, bandMax + 1f, 1, out Vector2 result);

            Assert.That(ok, Is.False, "모든 후보가 회피 반경 안이면 폴백(false)이어야 한다.");

            float midRadius = (bandMin + bandMax) * 0.5f;
            float baseRad = wedgeBaseDeg * Mathf.Deg2Rad;
            Vector2 expected = new Vector2(midRadius * Mathf.Cos(baseRad), midRadius * Mathf.Sin(baseRad));

            Assert.That(Vector2.Distance(result, expected), Is.LessThan(0.001f),
                $"폴백 좌표는 밴드 중간 거리 {midRadius} + 기저각 {wedgeBaseDeg}° 방향이어야 한다 (result={result}).");
        }

        // ════════════════════════════════════════════════════════════════════
        // 7. null 처리 — taken=null은 간격 통과, rng=null은 예외
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void TryScatter_NullTakenList_SucceedsAsSpacingPass()
        {
            ScatterInput input = MakeInput(300f, 650f, 90f); // North 쐐기
            System.Random rng = new System.Random(31337);

            bool ok = TerritoryScatterMath.TryScatter(
                input, rng, null, 120f, Vector2.zero, 0f, 8, out Vector2 result);

            Assert.That(ok, Is.True, "taken=null은 간격 조건 통과로 간주되어 확정되어야 한다.");
            Assert.That(result.magnitude, Is.InRange(300f - BandTolerance, 650f + BandTolerance),
                $"정상 동작 시 결과 거리는 밴드 안이어야 한다 (r={result.magnitude}).");
        }

        [Test]
        public void TryScatter_NullRng_ThrowsArgumentException()
        {
            ScatterInput input = MakeInput(300f, 650f, 0f);

            Assert.Throws<System.ArgumentException>(() =>
                TerritoryScatterMath.TryScatter(input, null, null, 0f, Vector2.zero, 0f, 8, out _),
                "rng가 null이면 한국어 메시지의 ArgumentException을 던져야 한다.");
        }

        // ════════════════════════════════════════════════════════════════════
        // 헬퍼
        // ════════════════════════════════════════════════════════════════════

        /// <summary>계획 규약 기본 마진(6°)을 쓰는 입력 묶음 생성 헬퍼.</summary>
        private static ScatterInput MakeInput(float bandMin, float bandMax, float wedgeBaseDeg)
        {
            return new ScatterInput
            {
                key = "test_scatter",
                bandMin = bandMin,
                bandMax = bandMax,
                wedgeBaseDeg = wedgeBaseDeg,
                wedgeMarginDeg = 6f,
            };
        }

        /// <summary>좌표의 각도(도, −180~180). 규약: Atan2(y, x) — 세계 (x, z)를 Vector2(x, z)로 전달.</summary>
        private static float AngleOfDeg(Vector2 p)
        {
            return Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
        }

        /// <summary>기저각 base에서 본 각차(도, −180~180 정규화) — 쐐기 랩어라웃 판정용.</summary>
        private static float AngleDiffFromBaseDeg(float baseDeg, float angleDeg)
        {
            return Mathf.Repeat(angleDeg - baseDeg + 180f, 360f) - 180f;
        }
    }
}
