using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase O5: 군집 회피 — 목표 지점 주위 링 분산 배치 (결정론).
    /// docs/reference/openmmo/MONSTER_SEPARATION.md 셀 점유 설계를 부대 이동으로 이식:
    /// 부대 전원에 "같은 목적지 좌표"를 주는 대신, 목적지 주위 링에 개별 목표를 배치해
    /// RTS 뭉침(40명 전원 한 점 몰림)을 원천 차단한다.
    ///
    /// 링 구성 (unitRadius = 유닛 1개가 차지하는 반경):
    ///   index 0      = 중심 (목적지 그 자체)
    ///   링 1 (6개)   = 반경 unitRadius × 2,   60° 간격
    ///   링 2 (12개)  = 반경 unitRadius × 3.5, 30° 간격
    ///   링 3 (18개)  = 반경 unitRadius × 5,   20° 간격
    ///   링 n (n≥3)   = 반경 unitRadius × (2 + 1.5(n−1)), 6n개 (링당 12→18→24... 근사)
    ///
    /// Random 사용 금지 — 동일 입력은 항상 동일 출력 (테스트: FormationSpreadTests).
    /// 규약: 컬렉션 순회는 for 대신 foreach + Enumerable.Range.
    /// </summary>
    public static class FormationSpread
    {
        /// <summary>
        /// center 주위에 count개 지점을 링으로 분산 배치한다.
        /// count=0이면 빈 배열, count=1이면 [center]. 정확히 count개만 잘라서 반환.
        /// </summary>
        public static Vector3[] Distribute(Vector3 center, int count, float unitRadius = 1.6f)
        {
            if (count <= 0) return new Vector3[0];

            var points = new Vector3[count];
            points[0] = center; // index 0 = 중심
            if (count == 1) return points;

            // 필요한 링 수: 링 k는 6k개 슬롯 → 누적 1 + 3r(r+1) ≥ count 를 만족하는 최소 r.
            // 근의 공식 + ε 보정 (정수 경계에서 부동소수 오차로 링 1개 더 계산되는 것 방지).
            int ringCount = Mathf.CeilToInt((-3f + Mathf.Sqrt(9f + 12f * (count - 1))) / 6f - 0.0001f);
            if (ringCount < 1) ringCount = 1;

            // 각 링 스펙 미리 계산 (foreach 순회용)
            var ringSpecs = new List<RingSpec>(ringCount);
            foreach (int ring in System.Linq.Enumerable.Range(1, ringCount))
            {
                int slots = 6 * ring;
                ringSpecs.Add(new RingSpec
                {
                    radius = unitRadius * (2f + 1.5f * (ring - 1)),
                    slots = slots,
                    stepDeg = 360f / slots
                });
            }

            // 안쪽 링부터 슬롯 채우기 — count 도달 즉시 중단(잘라서 반환)
            int index = 1;
            foreach (var spec in ringSpecs)
            {
                if (index >= count) break;
                foreach (int slot in System.Linq.Enumerable.Range(0, spec.slots))
                {
                    if (index >= count) break;
                    float angleRad = slot * spec.stepDeg * Mathf.Deg2Rad;
                    points[index] = center + new Vector3(
                        Mathf.Sin(angleRad) * spec.radius, 0f, Mathf.Cos(angleRad) * spec.radius);
                    index++;
                }
            }
            return points;
        }

        /// <summary>
        /// Distribute(center, count, unitRadius) 결과의 index번째 지점만 반환하는 편의 메서드.
        /// Distribute를 재사용하므로 결과가 항상 동일하다 (결정론).
        /// index가 범위 밖이거나 count ≤ 0이면 center 반환.
        /// </summary>
        public static Vector3 OffsetFor(int index, int count, Vector3 center, float unitRadius = 1.6f)
        {
            if (count <= 0 || index < 0 || index >= count) return center;
            return Distribute(center, count, unitRadius)[index];
        }

        /// <summary>링 1개의 배치 스펙 (반경, 슬롯 수, 슬롯 간 각도).</summary>
        private struct RingSpec
        {
            public float radius;   // 링 반경 (m)
            public int slots;      // 링당 배치 수
            public float stepDeg;  // 슬롯 간 각도 차 (°)
        }
    }
}
