using System.Collections.Generic;
using ProjectName.Core.Data;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 1 / P31-A: 국가별 마을 6개 (4국가 × 6 = 24개) 결정론적 배치 시스템.
    ///
    /// 설계:
    ///  · 각 국가의 영지(TerritoryDatabase) 좌표를 기준으로 마을 중심을 산출한다.
    ///  · 마을은 영지 성(castle)과 겹치지 않도록 성 중심에서 반경 38~46m 떨어진
    ///    지점(결정론 각도)에 배치한다.
    ///  · Ring 선택: Ring1~Ring3(1450/1000/550m)의 영지 인덱스를 고르게 선정해
    ///    국가 각도 슬라이스 전체에 분산시킨다. (Ring4=150m는 황제국 근접이라 제외)
    ///  · 결정론: 국가/인덱스 조합마다 고정 시드(System.Random) — 프로세스 재시작
    ///    간에도 동일 좌표. UnityEngine.Random 사용 금지.
    ///  · 마을 radius 기본 40m.
    ///  · 대표 마을(index 0) = 상점 실외 배치 후보.
    ///
    /// 사용법:
    ///   List<VillagePlacementSystem.VillageInfo> villages =
    ///       VillagePlacementSystem.GetVillages(NationType.East);
    /// </summary>
    public static class VillagePlacementSystem
    {
        /// <summary>국가당 마을 수.</summary>
        public const int VillagesPerNation = 6;

        /// <summary>마을 기본 반경 (m).</summary>
        public const float VillageRadius = 40f;

        /// <summary>성 중심에서 마을 중심까지 거리 범위 (m) — 성과 겹침 방지.</summary>
        private const float OffsetMin = 38f;
        private const float OffsetMax = 46f;

        /// <summary>결정론 시드 오프셋 (국가별 *1000 + 고정 base).</summary>
        private const int SeedBase = 20260921;

        /// <summary>국가별로 선정할 영지 인덱스 (Ring1~Ring3 분산). 인덱스는 1~20 중 선택.</summary>
        private static readonly int[] _territoryIndices = { 1, 4, 6, 9, 11, 14 };

        /// <summary>
        /// 마을 배치 정보.
        /// </summary>
        public struct VillageInfo
        {
            public NationType nation;
            public int index;             // 0~5
            public Vector3 center;        // 마을 중심 (월드)
            public float radius;          // 마을 반경
            public Vector3 castleCenter;  // 기준이 된 영지 성 좌표
            public bool isRepresentative; // index==0 — 상점 실외 배치 후보
        }

        /// <summary>특정 국가의 마을 6개 결정론적 조회. 결과는 캐시된다.</summary>
        public static List<VillageInfo> GetVillages(NationType nation)
        {
            var list = new List<VillageInfo>();
            for (int i = 0; i < VillagesPerNation; i++)
            {
                list.Add(ComputeVillage(nation, i));
            }
            return list;
        }

        /// <summary>모든 4국가의 마을 24개 조회.</summary>
        public static List<VillageInfo> GetAllVillages()
        {
            var all = new List<VillageInfo>();
            foreach (var n in new[] { NationType.East, NationType.West, NationType.South, NationType.North })
                all.AddRange(GetVillages(n));
            return all;
        }

        /// <summary>해당 좌표가 어느 국가 마을에 속하는지 판별 (반경 내). 어디에도 없으면 -1.</summary>
        public static int ResolveVillageIndexAt(NationType nation, Vector3 pos)
        {
            var list = GetVillages(nation);
            float best = float.MaxValue;
            int bestIdx = -1;
            for (int i = 0; i < list.Count; i++)
            {
                float d = Vector3.Distance(list[i].center, pos);
                if (d <= list[i].radius && d < best) { best = d; bestIdx = i; }
            }
            return bestIdx;
        }

        /// <summary>결정론적 세계 좌표로 국가 판정 확인용 헬퍼.</summary>
        private static NationType NationAt(Vector3 pos) =>
            NationTerrainController.GetNationFromPosition(pos);

        /// <summary>단일 마을 좌표 계산 (결정론).</summary>
        private static VillageInfo ComputeVillage(NationType nation, int villageIndex)
        {
            int baseTerritoryIndex = _territoryIndices[villageIndex % _territoryIndices.Length];
            Vector3 castle = ResolveTerritoryCenter(nation, baseTerritoryIndex);

            // 결정론 각도: 국가 기본 각도(slice 중심) 기준 ±30° 내 오프셋, 시드 고정.
            float centerAngle = GetNationBaseAngle(nation);
            var rng = new System.Random(GetHash($"{nation}_{villageIndex}_village"));
            float angleDeltaDeg = -30f + (float)rng.NextDouble() * 60f;
            float offsetDist = OffsetMin + (float)rng.NextDouble() * (OffsetMax - OffsetMin);
            float angleRad = (centerAngle + angleDeltaDeg) * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angleRad) * offsetDist, 0f, Mathf.Sin(angleRad) * offsetDist);

            Vector3 center = castle + offset;

            // 국가 각도 슬라이스 유지 후보 — 판정 확인은 호출부에서 수행(성 근처이므로 대개 유지).
            return new VillageInfo
            {
                nation = nation,
                index = villageIndex,
                center = center,
                radius = VillageRadius,
                castleCenter = castle,
                isRepresentative = villageIndex == 0
            };
        }

        /// <summary>영지 중심 좌표 해석 (TerritoryDatabase 우선, 실패 시 각도 기반 재계산).</summary>
        private static Vector3 ResolveTerritoryCenter(NationType nation, int index)
        {
            if (TerritoryDatabase.Instance != null)
            {
                foreach (var def in TerritoryDatabase.Instance.GetDefinitionsByNation(nation))
                {
                    if (def.id.index == index && def.worldPosition != Vector3.zero)
                        return def.worldPosition;
                }
            }
            // 폴백: 결정론 각도/링 재계산 (Ring1 => 1450m 기준)
            float angleDeg = GetNationBaseAngle(nation) - 45f + ((index - 1) * 18f) + 9f;
            float angleRad = angleDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(angleRad) * 550f, 0f, Mathf.Sin(angleRad) * 550f);
        }

        private static float GetNationBaseAngle(NationType nation) => nation switch
        {
            NationType.East  => 0f,
            NationType.North => 90f,
            NationType.West  => 180f,
            NationType.South => 270f,
            _                => 0f
        };

        /// <summary>결정론 문자열 해시 — 프로세스 간 동일 (TerritoryDatabase와 동일 알고리즘).</summary>
        private static int GetHash(string input)
        {
            unchecked
            {
                int hash = 17;
                foreach (char c in input)
                    hash = hash * 31 + c;
                return hash;
            }
        }
    }
}
