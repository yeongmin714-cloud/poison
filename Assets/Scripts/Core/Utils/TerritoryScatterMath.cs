using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Core.Utils
{
    /// <summary>
    /// 영지 산포(Territory Scatter) 결정론 순수 수학 유틸리티.
    /// MonoBehaviour 아님 — UnityEngine API는 Vector2/Mathf만 사용하므로 EditMode에서 검증 가능하다.
    ///
    /// 좌표 규약: 세계 XZ 평면을 Vector2(x, z)로 표현한다 (result.y가 세계 z를 의미).
    /// 각도 규약: 각도 = Atan2(z, x), 도(deg) 단위.
    ///   East 기저각 0°(−45~45) / North 90°(45~135) / West 180°(135~225) / South 270°(225~315).
    ///   후보 좌표는 result.x = r*cos(rad), result.y = r*sin(rad)로 산출한다.
    ///
    /// 밴드 규약: 중심 거리 r은 [bandMin, bandMax] 구간에서 균등 추첨한다.
    ///   링 밴드(Ring4 50~380 / Ring3 380~750 / Ring2 750~1150 / Ring1 1150~1550)는 서로 중첩 없이
    ///   단조 배치되므로, 산포 후에도 "중앙에 가까울수록 난이도 증가" 규약이 좌표만으로 유지된다.
    ///
    /// 쐐기 규약: 국가 쐐기는 90°(±45°)이며, 국가색 경계 혼선 방지를 위해 마진을 뺀
    ///   wedgeBaseDeg ± (45 − wedgeMarginDeg) 안에서만 각도를 추첨한다 (쐐기 내부 — 랩어라웃 없음).
    ///
    /// 리젝션 규약: 후보가 taken의 모든 점과 minSpacing 이상, avoidPoint와 avoidRadius 이상
    ///   떨어져 있을 때만 채택한다. 조건을 만족하지 못하면 재추첨한다.
    ///
    /// 그리드 스캔 규약: 리젝션이 maxTries 내에 성공하지 못하면(포화), 결정론 격자 스캔으로
    ///   간격·회피 조건을 만족하는 점을 찾아 "최대분산" 배치한다 (아래 TryScatter 2단계 주석 참조).
    ///
    /// 폴백 규약: 리젝션 소진 + 그리드 스캔에서도 유효점이 없으면(진짜 포화) 밴드 중간 거리 +
    ///   쐐기 기저각(쐐기 중심) 방향 좌표를 result로 내고 false를 반환한다 —
    ///   절대 예외를 던지지 않는다(호출부는 배치를 계속 진행 가능).
    ///
    /// 결정론: 후보는 rng 시퀀스만으로 결정되므로, 동일 시드 재시작 시 동일 배치가 재현된다.
    /// </summary>
    public static class TerritoryScatterMath
    {
        /// <summary>국가 쐐기 반경 각도 — 4국가 360° 등분(90° 쐐기)의 절반.</summary>
        private const float NationWedgeHalfDeg = 45f;

        /// <summary>
        /// 산포 1회 호출에 필요한 입력 묶음.
        /// key는 결정론 시드 파생용 식별 키다 (예: "East_Ring4_3_scatter") —
        /// 실제 시드 생성은 호출부 책임이며(TerritoryDatabase.GetDeterministicHash 규약), 본 클래스는 주어진 rng만 소비한다.
        /// </summary>
        public struct ScatterInput
        {
            public string key;            // 결정론 시드용 키 (예: "East_Ring4_3_scatter")
            public float bandMin;         // 중심 거리 하한
            public float bandMax;         // 중심 거리 상한
            public float wedgeBaseDeg;    // 국가 쐐기 중심각
            public float wedgeMarginDeg;  // 쐐기 경계 마진 (기본 6 — 실제 사용 각폭 = ±(45−margin))
        }

        /// <summary>
        /// 밴드×쐐기 내부에서 간격·회피 조건을 만족하는 산포 후보를 결정론 추첨한다.
        /// maxTries 소진 시(리젝션 포화) 결정론 그리드 스캔으로 유효점을 찾고(2단계),
        /// 그래도 유효점이 없으면(진짜 포화) 폴백 좌표(밴드 중간 거리 + 쐐기 기저각)를 반환한다(false).
        /// </summary>
        /// <param name="input">밴드/쐐기/키 입력 묶음 (bandMin ≤ bandMax, wedgeMarginDeg ≤ 45 가정).</param>
        /// <param name="rng">결정론 난수기 (필수 — null이면 ArgumentException). 호출부가 시드 키에서 파생한다.</param>
        /// <param name="taken">기확정 좌표 목록 — 모든 점과 minSpacing 이상 떨어져야 채택. null이면 간격 조건 통과로 간주.</param>
        /// <param name="minSpacing">taken 점들과의 최소 간격 (예: 영지 중심 간 120m 규약).</param>
        /// <param name="avoidPoint">회피점 (예: 스폰 지점 (728,−529)).</param>
        /// <param name="avoidRadius">회피 반경 (예: 스폰 100m 규약). 0 이하면 회피 조건은 항상 통과.</param>
        /// <param name="maxTries">리젝션 재추첨 상한 (계획 기본 200). 0 이하면 그리드 스캔 없이 즉시 폴백.</param>
        /// <param name="result">확정 좌표(성공) 또는 폴백 좌표(실패) — 실패 시에도 반드시 세팅된다.</param>
        /// <returns>조건 만족 후보 확정 시 true, maxTries 소진 후 폴백이면 false. 예외는 rng null뿐이다.</returns>
        public static bool TryScatter(
            ScatterInput input,
            System.Random rng,
            List<Vector2> taken,
            float minSpacing,
            Vector2 avoidPoint,
            float avoidRadius,
            int maxTries,
            out Vector2 result)
        {
            if (rng == null)
                throw new System.ArgumentException(
                    "TerritoryScatterMath.TryScatter: rng가 null이면 결정론 재현이 성립하지 않는다. " +
                    "시드 키(input.key)에서 파생된 System.Random을 전달할 것 (예: new System.Random(hash)).");

            float halfSpan = NationWedgeHalfDeg - input.wedgeMarginDeg;

            // 1단계 — 리젝션 루프: 밴드 거리 + 쐐기 각도 후보 → 간격/회피 판정 → 만족 시 채택
            for (int attempt = 0; attempt < maxTries; attempt++)
            {
                float radius = input.bandMin + (float)rng.NextDouble() * (input.bandMax - input.bandMin);
                float angleDeg = input.wedgeBaseDeg + ((float)rng.NextDouble() * 2f - 1f) * halfSpan;
                float angleRad = angleDeg * Mathf.Deg2Rad;
                Vector2 candidate = new Vector2(radius * Mathf.Cos(angleRad), radius * Mathf.Sin(angleRad));

                bool spacingOk = taken == null || IsFarEnoughFromTaken(candidate, taken, minSpacing);
                bool avoidOk = Vector2.Distance(candidate, avoidPoint) >= avoidRadius;

                if (spacingOk && avoidOk)
                {
                    result = candidate;
                    return true;
                }
            }

            // 2단계 — 결정론 그리드 스캔: 리젝션 포화 시 의도적 최대분산 배치.
            //   리젝션이 maxTries 내에 끝나지 않았다는 것은 밴드가 이미 촘촘하다는 뜻이므로,
            //   무작위 재추첨 대신 격자점들 중 간격 여유(최근접 점까지 거리)가 가장 큰 점을 택해
            //   남은 여유를 최대한 활용한다 — 폴백(단일 고정점 재사용 → 0m 충돌)을 최소화하는 목적이다.
            //   격자: 거리 32분할(r_i = bandMin + (i+0.5)·(bandMax−bandMin)/32, i=0..31) ×
            //         각도 52분할(a_j = wedgeBaseDeg + (−(45−margin) + (j+0.5)·2(45−margin)/52), j=0..51).
            //   해상도 근거: 최외곽 밴드(Ring4 상한 ~380m)에서 각도 1칸 간격은 호폭
            //     380·(78°/52)·(π/180) ≈ 5.5m, 거리 1칸 간격은 ~10m 수준 — 120m 간격 규약 대비
            //     충분히 촘촘하여 유효 영역이 존재하면 격자점도 존재한다고 간주할 수 있다.
            //     [2026-10-15 시뮬 실측] 16×26에서는 3/20 시드(전부 South_r4_t4)에서 좁은 유효
            //     포켓을 놓쳐 폴백이 남았으나, 32×52에서는 20/20 시드 폴백 0건 — 실측으로 선정.
            //   선택: 유효점(taken 전체와 dist ≥ minSpacing, avoidPoint와 dist ≥ avoidRadius)들 중
            //     "taken(및 avoidPoint)까지의 최소거리"가 최대인 점. 동률 시 스캔 순서 첫 점
            //     (바깥 거리 우선 i 내림차순, 그다음 각도 오름차순 j 오름차순) — 완전 결정론.
            //     rng를 소비하지 않으므로 1단계 난수 시퀀스의 재현성도 영향받지 않는다.
            //   유효점이 하나도 없으면 진짜 포화로 보고 기존 폴백(3단계)으로 진행한다.
            //   maxTries ≤ 0은 리젝션 자체를 생략한 호출로 보아 기존 규약("즉시 폴백")을 유지한다.
            if (maxTries > 0)
            {
                const int GridRadialCount = 32;   // 거리 분할 수
                const int GridAngularCount = 52;  // 각도 분할 수

                Vector2 bestCandidate = Vector2.zero;
                float bestScore = float.NegativeInfinity;
                bool found = false;

                for (int i = GridRadialCount - 1; i >= 0; i--) // 바깥 거리 우선 스캔(동률 결정용)
                {
                    float gridRadius = input.bandMin
                        + (i + 0.5f) * (input.bandMax - input.bandMin) / GridRadialCount;

                    for (int j = 0; j < GridAngularCount; j++)
                    {
                        float gridAngleDeg = input.wedgeBaseDeg
                            + (-halfSpan + (j + 0.5f) * (2f * halfSpan) / GridAngularCount);
                        float gridAngleRad = gridAngleDeg * Mathf.Deg2Rad;
                        Vector2 candidate = new Vector2(
                            gridRadius * Mathf.Cos(gridAngleRad), gridRadius * Mathf.Sin(gridAngleRad));

                        bool spacingOk = taken == null || IsFarEnoughFromTaken(candidate, taken, minSpacing);
                        bool avoidOk = Vector2.Distance(candidate, avoidPoint) >= avoidRadius;
                        if (!spacingOk || !avoidOk)
                            continue;

                        // 유효점 점수 = taken(및 avoidPoint)까지의 최소거리 — 최대분산 선택 기준
                        float score = Vector2.Distance(candidate, avoidPoint);
                        if (taken != null)
                        {
                            for (int k = 0; k < taken.Count; k++)
                                score = Mathf.Min(score, Vector2.Distance(taken[k], candidate));
                        }

                        // 동률 시 먼저 스캔한 점을 유지(> 비교) → 스캔 순서 첫 점이 이긴다 → 결정론
                        if (!found || score > bestScore)
                        {
                            found = true;
                            bestScore = score;
                            bestCandidate = candidate;
                        }
                    }
                }

                if (found)
                {
                    result = bestCandidate;
                    return true;
                }
            }

            // 3단계 — 폴백: 밴드 중간 거리 + 쐐기 기저각(쐐기 중심) — 리젝션 판정을 무시하므로 절대 실패 없음
            float fallbackRadius = (input.bandMin + input.bandMax) * 0.5f;
            float fallbackRad = input.wedgeBaseDeg * Mathf.Deg2Rad;
            result = new Vector2(fallbackRadius * Mathf.Cos(fallbackRad), fallbackRadius * Mathf.Sin(fallbackRad));
            return false;
        }

        /// <summary>
        /// candidate가 taken의 모든 점과 minSpacing 이상 떨어져 있는지.
        /// taken == null은 이미 호출부에서 통과 처리하므로 여기서는 non-null만 다룬다.
        /// </summary>
        private static bool IsFarEnoughFromTaken(Vector2 candidate, List<Vector2> taken, float minSpacing)
        {
            for (int i = 0; i < taken.Count; i++)
            {
                if (Vector2.Distance(taken[i], candidate) < minSpacing)
                    return false;
            }
            return true;
        }
    }
}
