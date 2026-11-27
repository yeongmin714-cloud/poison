using System.Collections.Generic;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [88차 A1] 마을 음식점 배치 지점 시스템 — 결정론 후보 산출 + 런타임 유효 판정.
    ///
    /// 역할 (docs/88_RESTAURANT_PLACEMENT_FIGMA_PLAN.md Part A / A1):
    ///  · ComputeCandidateSpots — 마을당 결정론 3개 후보(각도 = SeedBase(472)+GetHash 해시 시드,
    ///    반경 = 일반 village.radius-7 / 촌락 village.radius-5). 순수 결정론 — EditMode 테스트 가능.
    ///  · TryGetValidSpots — 런타임 유효 판정(씬 질의):
    ///      ① 점유 판정 — VillageDrugState.restaurantBuilt 또는 ConstructionManager bp_restaurant
    ///         구조물(완료/진행 무관) 매칭 시 스팟 없음(마커 표시 안 함).
    ///      ② 마을 루트 Village_{nation}_{index:D2} 하위 렌더러 transforms XZ를 기준으로
    ///         VillageBuilder.EnforceSpacing 동일 방식(충돌 시 바깥쪽 밀기)으로 간격 정리.
    ///      ③ 광장 회피(PlazaRadius 일반 6/촌락 4 + footprint), 마을 경계(radius+마진) 이내.
    ///      ④ 지형 접지(y = TerrainGenerator.GetHeightAt(x, z, Plains, 42) + GROUND_BASE).
    ///
    /// 결정론: 해시/시드/산식은 VillageBuilder/RestaurantSystem과 동일 알고리즘 복제(17·31 문자열 해시,
    /// UnityEngine.Random 미사용). 씬 질의부는 런타임 전용 경로로 분리 — EditMode에서는 안전 false 폴백.
    /// </summary>
    public static class RestaurantPlacementSpotSystem
    {
        /// <summary>결정론 시드 베이스 — [88차] 스펙 고정치(VillageBuilder.SeedBase와 별개 축).</summary>
        public const int SeedBase = 472;

        /// <summary>음식점 footprint 반경 (m) — [실측] BlueprintData.bp_restaurant.footprintRadius = 2.5.</summary>
        public const float RestaurantFootprintRadius = 2.5f;

        /// <summary>건물 간 최소 여유 마진 (m) — footprint + 이 값 이상 간격(스펙: 마진 ≥ 2.0).</summary>
        public const float BuildingSpacingMargin = 2.0f;

        /// <summary>일반 마을 광장 반경 (m) — [실측] VillageBuilder.PlazaRadius = 6(정적 클래스 상수 공유 불가 — 복제).</summary>
        public const float NormalPlazaRadius = 6f;

        /// <summary>촌락(isHamlet) 광장 반경 (m) — [실측] VillageBuilder.HamletPlazaRadius = 4(복제).</summary>
        public const float HamletPlazaRadius = 4f;

        /// <summary>일반 마을 스팟 반경 인셋 (m) — village.radius - 7(=33m). 펜스(radius+3)·외곽 링(최대 28m) 회피.</summary>
        public const float NormalRadiusInset = 7f;

        /// <summary>촌락 스팟 반경 인셋 (m) — village.radius - 5(=17m).</summary>
        public const float HamletRadiusInset = 5f;

        /// <summary>지형 기저 보정 (Ground_Inner 월드 y=1) — VillageBuilder/RestaurantPropBuilder 동일.</summary>
        private const float GROUND_BASE = 1f;

        /// <summary>지형 높이 조회 시드 — VillageBuilder와 동일.</summary>
        private const int TerrainSeed = 42;

        /// <summary>간격 밀기 반복 상한 — VillageBuilder.EnforceSpacing(535행) 동일(8회).</summary>
        private const int MaxPushIterations = 8;

        /// <summary>밀기 여유 패딩 (m) — VillageBuilder.EnforceSpacing 동일(+0.5).</summary>
        private const float PushPadding = 0.5f;

        /// <summary>
        /// 결정론 문자열 해시 — VillageBuilder.GetHash와 동일 알고리즘 복제(17·31, string.GetHashCode 미사용).
        /// 프로세스 재시작 간 동일값(결정론 계약).
        /// </summary>
        public static int GetHash(string input)
        {
            unchecked
            {
                int hash = 17;
                foreach (char c in input)
                    hash = hash * 31 + c;
                return hash;
            }
        }

        /// <summary>
        /// 스팟 링 반경 산출 — 일반 village.radius - 7 / 촌락 village.radius - 5.
        /// 비정상적으로 작은 반경 입력에는 footprint 하한을 적용한다(방어 — 결정론).
        /// </summary>
        public static float ResolveSpotRingRadius(VillagePlacementSystem.VillageInfo village)
        {
            float ringRadius = village.isHamlet
                ? village.radius - HamletRadiusInset
                : village.radius - NormalRadiusInset;
            return Mathf.Max(RestaurantFootprintRadius, ringRadius);
        }

        /// <summary>
        /// [88차 A1] 결정론 후보 지점 산출 — 마을당 기본 3개(시드 키 "{nation}_{index}_restspot_{i}").
        /// 각도 = (SeedBase + GetHash(키)) 시드 System.Random의 NextDouble × 360°.
        /// 반환 좌표는 XZ 결정론(y=0) — 접지(y)는 런타임 경로(TryGetValidSpots)에서 적용한다.
        /// 씬 의존 없음 — EditMode 테스트 가능(순수 결정론 분리 규약).
        /// </summary>
        public static List<Vector3> ComputeCandidateSpots(VillagePlacementSystem.VillageInfo village, int count = 3)
        {
            var spots = new List<Vector3>();
            if (village.nation == NationType.None || count <= 0)
                return spots; // 방어 — 무효 마을/개수 0

            float ringRadius = ResolveSpotRingRadius(village);
            for (int i = 0; i < count; i++)
            {
                var rng = new System.Random(SeedBase + GetHash($"{village.nation}_{village.index}_restspot_{i}"));
                float angleDeg = (float)rng.NextDouble() * 360f; // 0~360° 결정론 각도
                float angleRad = angleDeg * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angleRad), 0f, Mathf.Sin(angleRad)) * ringRadius;
                spots.Add(new Vector3(village.center.x + offset.x, 0f, village.center.z + offset.z));
            }
            return spots;
        }

        /// <summary>
        /// [88차 A1] 런타임 유효 스팟 판정 — results에 유효 지점(접지 완료)을 추가 기입하고
        /// 1개 이상 확보 시 true. 점유 마을/씬 미로드/EditMode는 false(빈 목록) — 안전 폴백.
        /// 판정 순서: 점유(플래그 → 구조물) → 마을 루트 질의 → 간격 정리 → 광장/경계 → 접지.
        /// </summary>
        public static bool TryGetValidSpots(VillagePlacementSystem.VillageInfo village, List<Vector3> results)
        {
            if (results == null)
                return false;
            results.Clear();

            if (village.nation == NationType.None)
                return false;
            if (!Application.isPlaying)
                return false; // 씬 질의 경로는 런타임 전용 — EditMode는 후보 산출(순수)만 테스트(스펙 분리 규약)

            // ① 점유 판정 — 마을 상태 스냅샷(읽기 전용 — 상태 엔트리 생성 없음 규약)
            if (VillageDrugState.GetSnapshot(village.nation, village.index).restaurantBuilt)
                return false; // 이미 음식점 있는 마을 — 스팟 없음(마커 표시 안 함)

            // ①' 점유 판정 — ConstructionManager bp_restaurant 구조물(완료/진행 무관) 매칭
            ConstructionManager mgr = ConstructionManager.Instance;
            if (mgr != null)
            {
                foreach (ConstructionManager.StructureEntry entry in mgr.Structures)
                {
                    if (entry.blueprintId != RestaurantPropBuilder.RestaurantBlueprintId)
                        continue;
                    if (!ConstructionManager.TryFindRestaurantVillage(entry.pos, out NationType entryNation, out int entryVillage))
                        continue;
                    if (entryNation == village.nation && entryVillage == village.index)
                        return false; // 시공 중/완료 구조물 존재 — 점유 취급
                }
            }

            // ② 마을 루트 질의 — VillageBuilder.BuildVillage 112행 이름 규약("Village_{nation}_{index:D2}")
            GameObject villageRoot = GameObject.Find($"Village_{village.nation}_{village.index:D2}");
            if (villageRoot == null)
                return false; // 씬 미로드 — 안전 폴백(마커 비표시)

            // 기배치 빌딩 중심(XZ) 수집 — 루트 하위 렌더러 transform 위치(빌딩 근사)
            var buildingCenters = new List<Vector3>();
            foreach (Renderer buildingRenderer in villageRoot.GetComponentsInChildren<Renderer>())
            {
                if (buildingRenderer == null)
                    continue;
                Vector3 p = buildingRenderer.transform.position;
                buildingCenters.Add(new Vector3(p.x, 0f, p.z));
            }

            float plazaKeepout = (village.isHamlet ? HamletPlazaRadius : NormalPlazaRadius)
                + RestaurantFootprintRadius;                          // 광장 회피 반경
            float minSpacing = RestaurantFootprintRadius + BuildingSpacingMargin; // 건물 간격(4.5m)
            float maxCenterDist = village.radius + ConstructionManager.VillagePlacementMargin; // 경계(radius+5m)

            List<Vector3> candidates = ComputeCandidateSpots(village);
            // 간격 대상 = 기배치 빌딩 + 이번 판정에서 수용한 스팟(스팟 간 간격도 확보 — placed 누적 규약)
            var spacingList = new List<Vector3>(buildingCenters);

            foreach (Vector3 candidate in candidates)
            {
                Vector3 spot = EnforceSpacing(candidate, spacingList, minSpacing);

                float centerDist = Vector2.Distance(
                    new Vector2(spot.x, spot.z),
                    new Vector2(village.center.x, village.center.z));
                if (centerDist < plazaKeepout)
                    continue; // 광장 회피 실패 — 후보 기각
                if (centerDist > maxCenterDist)
                    continue; // 마을 경계(radius + VillagePlacementMargin) 이탈 — 후보 기각

                spacingList.Add(spot);
                // ④ 지형 접지 — VillageBuilder/RestaurantPropBuilder 동일 기저(GetHeightAt + GROUND_BASE)
                float groundY = TerrainGenerator.GetHeightAt(spot.x, spot.z, BiomeType.Plains, TerrainSeed) + GROUND_BASE;
                results.Add(new Vector3(spot.x, groundY, spot.z));
            }

            return results.Count > 0;
        }

        /// <summary>
        /// 간격 강제 — VillageBuilder.EnforceSpacing(535행) 동일 방식 복제:
        /// 최근접 기배치 대상과의 거리가 minSpacing 미만이면 마을 center 방향 바깥쪽으로
        /// (minSpacing - minDist + 0.5) 밀어내기를 최대 8회 반복한다(결정론 — 랜덤 없음).
        /// </summary>
        private static Vector3 EnforceSpacing(Vector3 candidate, List<Vector3> placed, float minSpacing)
        {
            for (int iter = 0; iter < MaxPushIterations; iter++)
            {
                float minDist = float.MaxValue;
                for (int i = 0; i < placed.Count; i++)
                {
                    float dx = candidate.x - placed[i].x;
                    float dz = candidate.z - placed[i].z;
                    float d = Mathf.Sqrt(dx * dx + dz * dz);
                    if (d < minDist) minDist = d;
                }
                if (minDist >= minSpacing) break;

                // 바깥쪽으로 밀어내기 (결정론 — VillageBuilder 동일: 후보 XZ 방향 단위벡터)
                Vector3 flat = new Vector3(candidate.x, 0f, candidate.z);
                float r = flat.magnitude;
                Vector3 dir = r > 0.01f ? flat / r : new Vector3(1f, 0f, 0f);
                candidate = candidate + dir * (minSpacing - minDist + PushPadding);
            }
            return new Vector3(candidate.x, 0f, candidate.z);
        }
    }
}
