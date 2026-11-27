using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [88차 A6 — 마을 음식점 배치 지점 시스템] 계약 테스트 (RestaurantPlacementSpotSystem.cs /
    /// RestaurantPlacementMarker.cs) — EditMode 가능 부.
    ///
    /// ① 후보 각도/반경 결정론 재현 — 같은 시드(SeedBase 472 + 17·31 해시) → 같은 좌표.
    /// ② 반경 클램프 — 일반 radius-7(=33m) / 촌락 radius-5(=17m), 마을 경계(radius+마진 5m) 이내.
    /// ③ 씬 질의 경로 소스 어서션 — 마커 프롬프트 문구 "E: 음식점 배치", territoryId 조립
    ///    new TerritoryId(nation, villageIndex + 1), TryPlace 호출, PlacementAttempted 발화,
    ///    SelectionRingController/CreateRingMaterial 사용, 골드 #E3B341 근사.
    /// ④ 점유 마을 스킵 소스 어서션 — restaurantBuilt / bp_restaurant 구조물 매칭.
    /// ⑤ EditMode 안전 — TryGetValidSpots(씬 질의 경로)는 편집 모드에서 예외 없이 false 폴백.
    ///
    /// 실행 판정은 QA 서브에이전트 증거로 확정(구현 서브에이전트는 실행하지 않음).
    /// </summary>
    public sealed class RestaurantPlacementSpotTests
    {
        private const float Tol = 0.0001f;

        private static string SpotSystemSrc() =>
            File.ReadAllText("Assets/Scripts/Systems/RestaurantPlacementSpotSystem.cs");
        private static string MarkerSrc() =>
            File.ReadAllText("Assets/Scripts/Systems/RestaurantPlacementMarker.cs");

        /// <summary>일반 마을 가상 정보 — radius 40(실측 기본), center는 원점에서 충분히 먼 좌표.</summary>
        private static VillagePlacementSystem.VillageInfo NormalVillage()
        {
            return new VillagePlacementSystem.VillageInfo
            {
                nation = NationType.East,
                index = 3,
                center = new Vector3(1200f, 0f, 900f),
                radius = 40f,
                castleCenter = new Vector3(1240f, 0f, 940f),
                isHamlet = false,
                isRepresentative = false,
            };
        }

        /// <summary>촌락 가상 정보 — radius 22(실측 촌락 반경), Ring4 매핑 축.</summary>
        private static VillagePlacementSystem.VillageInfo HamletVillage()
        {
            return new VillagePlacementSystem.VillageInfo
            {
                nation = NationType.West,
                index = 17, // 기준 영지 18 — Ring4(16~20) 촌락 축
                center = new Vector3(-800f, 0f, 600f),
                radius = 22f,
                castleCenter = new Vector3(-760f, 0f, 640f),
                isHamlet = true,
                isRepresentative = false,
            };
        }

        /// <summary>후보 → 마을 center XZ 거리 집계 헬퍼.</summary>
        private static List<float> CenterDistances(List<Vector3> spots, VillagePlacementSystem.VillageInfo village)
        {
            var distances = new List<float>();
            foreach (Vector3 spot in spots)
            {
                float dx = spot.x - village.center.x;
                float dz = spot.z - village.center.z;
                distances.Add(Mathf.Sqrt(dx * dx + dz * dz));
            }
            return distances;
        }

        // ════════════════════════════════════════════════════════════════════
        // 1. ComputeCandidateSpots — 결정론 후보 산출 (순수, EditMode 안전)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void Candidates_Deterministic_SameSeedReproducesSameSpots()
        {
            VillagePlacementSystem.VillageInfo village = NormalVillage();

            List<Vector3> first = RestaurantPlacementSpotSystem.ComputeCandidateSpots(village);
            List<Vector3> second = RestaurantPlacementSpotSystem.ComputeCandidateSpots(village);

            Assert.That(first, Has.Count.EqualTo(3), "기본 후보 수 = 3(스펙 고정).");
            Assert.That(second, Has.Count.EqualTo(first.Count), "두 번째 호출도 동일 개수.");
            for (int i = 0; i < first.Count; i++)
            {
                Assert.That(second[i], Is.EqualTo(first[i]).Within(Tol),
                    $"[{i}] 동일 시드 → 동일 좌표(결정론 재현 계약).");
            }
        }

        [Test]
        public void Candidates_NormalVillage_RingRadiusIsRadiusMinus7()
        {
            VillagePlacementSystem.VillageInfo village = NormalVillage(); // radius 40

            List<Vector3> spots = RestaurantPlacementSpotSystem.ComputeCandidateSpots(village);
            List<float> distances = CenterDistances(spots, village);

            Assert.That(distances, Has.Count.EqualTo(3));
            foreach (float distance in distances)
            {
                Assert.That(distance, Is.EqualTo(village.radius - 7f).Within(Tol),
                    "일반 마을 스팟 반경 = village.radius - 7(=33m) — 펜스/외곽 링 회피 스펙.");
            }
        }

        [Test]
        public void Candidates_HamletVillage_RingRadiusIsRadiusMinus5()
        {
            VillagePlacementSystem.VillageInfo village = HamletVillage(); // radius 22

            List<Vector3> spots = RestaurantPlacementSpotSystem.ComputeCandidateSpots(village);
            List<float> distances = CenterDistances(spots, village);

            Assert.That(distances, Has.Count.EqualTo(3));
            foreach (float distance in distances)
            {
                Assert.That(distance, Is.EqualTo(village.radius - 5f).Within(Tol),
                    "촌락 스팟 반경 = village.radius - 5(=17m) — 스펙 고정.");
            }
        }

        [Test]
        public void Candidates_StayWithinVillagePlacementMargin()
        {
            foreach (VillagePlacementSystem.VillageInfo village in
                     new[] { NormalVillage(), HamletVillage() })
            {
                List<float> distances = CenterDistances(
                    RestaurantPlacementSpotSystem.ComputeCandidateSpots(village), village);
                foreach (float distance in distances)
                {
                    Assert.That(distance, Is.LessThanOrEqualTo(
                            village.radius + ConstructionManager.VillagePlacementMargin + Tol),
                        "모든 후보는 마을 경계(radius + VillagePlacementMargin 5m) 이내다.");
                }
            }
        }

        [Test]
        public void Candidates_ExplicitCount_IsHonored()
        {
            List<Vector3> spots = RestaurantPlacementSpotSystem.ComputeCandidateSpots(NormalVillage(), 5);
            Assert.That(spots, Has.Count.EqualTo(5), "count 파라미터 준수(확장 여유).");
        }

        [Test]
        public void GetHash_MatchesVillageBuilderAlgorithm()
        {
            // 17·31 문자열 해시 수기 재계산 — VillageBuilder.GetHash 동일 알고리즘 복제 계약.
            // "ab" = ((17*31 + 'a') * 31 + 'b') = (17*31=527; 527+97=624; 624*31=19344; 19344+98=19442)
            Assert.That(RestaurantPlacementSpotSystem.GetHash("ab"), Is.EqualTo(19442),
                "17·31 문자열 해시 — VillageBuilder/RestaurantSystem DeriveVillageSeed와 동일 알고리즘.");
        }

        // ════════════════════════════════════════════════════════════════════
        // 2. TryGetValidSpots — EditMode 안전 폴백 (씬 질의는 런타임 전용)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void TryGetValidSpots_EditMode_ReturnsFalseWithoutThrow()
        {
            var results = new List<Vector3>();

            Assert.DoesNotThrow(
                () => RestaurantPlacementSpotSystem.TryGetValidSpots(NormalVillage(), results),
                "편집 모드(씬 질의 불가)에서도 예외 없이 폴백해야 한다(스펙 분리 규약).");
            Assert.That(results, Is.Empty,
                "EditMode 폴백 — 씬 질의 경로는 유효 스팟을 산출하지 않는다(런타임 전용).");
        }

        // ════════════════════════════════════════════════════════════════════
        // 3. 소스 어서션 — 결정론 상수/점유 스킵/마커 배선 (스펙 A1/A2 진실치)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void SpotSystem_Source_DeterminismAndOccupancyMarkers()
        {
            string src = SpotSystemSrc();

            Assert.That(src, Does.Contain("[88차]"), "[88차] 진실치 마커 주석.");
            Assert.That(src, Does.Contain("SeedBase = 472"), "결정론 시드 베이스 472(스펙 고정).");
            Assert.That(src, Does.Contain("_restspot_"), "시드 키 규약 \"{nation}_{index}_restspot_{i}\".");
            Assert.That(src, Does.Contain("Village_{village.nation}_{village.index:D2}"),
                "마을 루트 질의 이름 규약 — VillageBuilder 112행 실측과 동일.");
            Assert.That(src, Does.Contain("restaurantBuilt"),
                "점유 판정 ① — VillageDrugState 스냅샷 restaurantBuilt 스킵.");
            Assert.That(src, Does.Contain("RestaurantPropBuilder.RestaurantBlueprintId"),
                "점유 판정 ② — bp_restaurant 구조물 매칭(완료/진행 무관).");
            Assert.That(src, Does.Contain("TryFindRestaurantVillage"),
                "구조물→마을 매칭 — ConstructionManager 실측 API 재사용.");
            Assert.That(src, Does.Contain("VillagePlacementMargin"),
                "마을 경계 판정 — radius + VillagePlacementMargin(5m) 이내.");
            Assert.That(src, Does.Contain("GetHeightAt"),
                "지형 접지 — TerrainGenerator.GetHeightAt(Plains, 42) + GROUND_BASE.");
            Assert.That(src, Does.Contain("EnforceSpacing"),
                "간격 정리 — VillageBuilder.EnforceSpacing 동일 방식 복제.");
            Assert.That(src, Does.Contain("Application.isPlaying"),
                "씬 질의 경로 런타임 전용 게이트 — EditMode 후보 산출 분리 규약.");
        }

        [Test]
        public void Marker_Source_PromptAndPlacementWiring()
        {
            string src = MarkerSrc();

            Assert.That(src, Does.Contain("[88차]"), "[88차] 진실치 마커 주석.");
            Assert.That(src, Does.Contain("E: 음식점 배치"),
                "E 프롬프트 문구 — [88차] 스펙 고정 진실치.");
            Assert.That(src, Does.Contain("RaiseRegister"), "프롬프트 등록 배선(EInteractionPromptBridge).");
            Assert.That(src, Does.Contain("RaiseUnregister"), "프롬프트 해제 배선(이탈/숨김).");
            Assert.That(src, Does.Contain("new TerritoryId("), "territoryId 조립 — TerritoryId 생성.");
            Assert.That(src, Does.Contain("index + 1").Or.Contain("villageIndex + 1"),
                "마을→영지 1:1 조립 — villageIndex 0~19 ↔ 영지 1~20(TestTerritoryCombatSetup 실측).");
            Assert.That(src, Does.Contain("TryPlace"), "ConstructionManager.TryPlace 호출.");
            Assert.That(src, Does.Contain("PlacementAttempted"),
                "PlacementAttempted(bool, string) 정적 이벤트 발화(Phase B UI 구독 계약).");
            Assert.That(src, Does.Contain("건설 시작!"), "성공 판정 — TryPlace 성공 메시지 접두 실측.");
            Assert.That(src, Does.Contain("Keyboard.current.eKey.wasPressedThisFrame"),
                "E키 폴링 — Input System 실측 패턴(ShopPlaceholder 동일).");
            Assert.That(src, Does.Contain("SelectionRingController"),
                "링 VFX — SelectionRingController(84/85차 수제 링) 사용.");
            Assert.That(src, Does.Contain("CreateRingMaterial"),
                "링 머티리얼 — SelectionRingController.CreateRingMaterial 정적 헬퍼 사용.");
            Assert.That(src, Does.Contain("E3B341"),
                "골드 앰버 #E3B341 — RTS 파랑 선택링과 구분(스펙 고정색).");
            Assert.That(src, Does.Contain("TryGetValidSpots"),
                "유효 스팟 조회 — RestaurantPlacementSpotSystem 연동.");
            Assert.That(src, Does.Contain("RuntimeInitializeOnLoadMethod"),
                "부트 — RuntimeInitializeOnLoadMethod(AfterSceneLoad) 규약.");
            Assert.That(src, Does.Contain("DontDestroyOnLoad"), "싱글턴 씬 전환 생존(Update 폴링 오브젝트).");
        }

        [Test]
        public void Marker_Source_ShowRangeAndPlayerCaching()
        {
            string src = MarkerSrc();

            Assert.That(src, Does.Contain("25f"), "표시 범위 25m — 스팟 25m 이내 접근 시 링 표시.");
            Assert.That(src, Does.Contain("GameObjectWithTag(\"Player\")"),
                "플레이어 tag \"Player\" 캐싱 — ShopPlaceholder 47~52행 선례(FindGameObjectWithTag).");
            Assert.That(src, Does.Contain("sqrMagnitude").Or.Contain("dx * dx"),
                "sqrMagnitude 거리 비교 — 선례 성능 규약.");
            Assert.That(src, Does.Contain("SetActive"), "링 숨김은 비활성 전환 — 파괴 아님(재사용 풀 계약).");
            Assert.That(src, Does.Contain("wasPressedThisFrame"), "E키 프레임 엣지 판정 — 재호출 방지.");
        }
    }
}
