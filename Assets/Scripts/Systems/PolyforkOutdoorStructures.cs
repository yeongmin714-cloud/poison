using UnityEngine;
using ProjectName.Core.Data;   // BiomeType (IdyllicDecoPlacer 선례 동일)

namespace ProjectName.Systems
{
    /// <summary>
    /// [161차 P2] Polyfork 야외 건축물 패스 — 마을(Idyllic) 지형 스폰 중심 주변 고정 링 배치.
    /// [162차 P2] 호수 선박 패스 가산 — TerrainGenerator.Lakes(LCG 앵커) 앞 3개 호수에 선박 5종 순환 배치.
    /// - 결정론: 고정 오프셋 상수(엔진 난수 0 — IdyllicDecoPlacer SEED 규약 동일)
    /// - 콜라이더: PlacePolyforkSolid(렌더러 bounds BoxCollider 1개 — 플레이어 통과 방지)
    /// - y: GROUND_BASE + TerrainGenerator.GetHeightAt (IdyllicDecoPlacer 선례 재사용 — Plains/seed 42)
    /// - SPAWN_EXCLUDE 6m 존중 — 최소 24m 이격, 구조물 간 이격으로 과밀 배제
    /// 좌표 규약: +x=동, +z=북(ArenaInteriorBuilder 선례). 오프셋 ≤70m — BOUND_MAX 1550 내.
    /// 배치 표: docs/161_POLYFORK_OUTDOOR_STRUCTURES_PLAN.md §2.
    /// </summary>
    public static class PolyforkOutdoorStructures
    {
        const string ROOT_NAME = "OutdoorStructures";
        const float GROUND_BASE = 1f;   // IdyllicDecoPlacer.GROUND_BASE 규약 동일
        const int TERRAIN_SEED = 42;    // IdyllicDecoPlacer GetHeightAt 시드 규약 동일
        // [162차 P2] 호수 선박 패스 상수 — 호수 중심 0.8r 수면 위(연잎 선례), 2개 시 8m 간격, 최대 3개 호수
        const float LAKE_BOAT_RADIUS = 0.8f;   // 호수 중심 기준 선박 배치 반경(0.8r — 수면 안쪽, 북측 에지 방향)
        const float LAKE_BOAT_SPACING = 8f;    // 호수당 2개 시 8m 간격(중심 기준 좌우 4m)
        const float LAKE_BOAT_Y_LIFT = 0.02f;  // 수면 위 리프트 — IdyllicDecoPlacer 연잎 y=waterLevel+0.02 선례

        /// <summary>스폰 기준 고정 오프셋 엔트리(id — Polyfork GLB, name — 오브젝트 접미, dx/dz — +x동 +z북).</summary>
        struct Entry
        {
            public string id, name;
            public float dx, dz;
            public bool solid;   // [163차 P1] true=콜라이더 부여(Solid) / false=렌더러 전용(아치 통로)
            public Entry(string id, string name, float dx, float dz, bool solid = true)
            {
                this.id = id; this.name = name; this.dx = dx; this.dz = dz; this.solid = solid;
            }
        }

        // 61종 배치 표(161 §2 + 162차 6 + [163차] 21 + [164차] 24 — 스폰 기준 오프셋). 구조물 간 이격 지향 — 짐수레/수레(6.3m)는 동선 인접 소품으로 의도적 인접.
        static readonly Entry[] Entries =
        {
            new Entry("four-wheel-wagon-adb946",  "Wagon",            24f,  24f),  // 스폰 북동 24m — 짐수레
            new Entry("wheelbarrow-13f422",       "Wheelbarrow",      30f,  26f),  // 스폰 북동 30m — 수레
            new Entry("blacksmith-forge-09fa85",  "BlacksmithForge", -28f,  28f),  // 스폰 북서 28m — 대장간
            new Entry("water-mill-b31972",        "WaterMill",       -40f, -40f),  // 스폰 남서 40m — 물레방아(호수/하천 방향)
            new Entry("windmill-3aad49",          "Windmill",         60f, -60f),  // 스폰 남동 60m — 풍차(스카이라인)
            new Entry("livery-stable-c01ee0",     "LiveryStable",      0f,  36f),  // 스폰 북 36m — 마구간
            new Entry("open-stable-dbf2d5",       "OpenStable",        8f,  44f),  // 스폰 북 44m — 개방 축사
            new Entry("mine-headframe-735cf4",    "MineHeadframe",   -70f,   0f),  // 스폰 서 70m — 광산 양새(던전 입구 연상)
            new Entry("ancestral-shrine-de7ed2",  "AncestralShrine",  32f,   0f),  // 스폰 동 32m — 사당
            new Entry("temple-bell-tower-745387", "BellTower",         0f, -55f),  // 스폰 남 55m — 종탑
            // ── [163차 P1] 가산 21종 — 건물 7 + 시설 5 + 소품 9 (기존 16종과 이격 검증) ──
            new Entry("stone-chapel-03e774",              "StoneChapel",     -12f,  52f),  // 북서 소교회
            new Entry("parish-church-543cf8",             "ParishChurch",    -30f,  60f),  // 북서 대형 교회
            new Entry("porch-log-cabin-d2f3ef",           "PorchCabin",       44f,  18f),  // 동측 주택
            new Entry("log-cabin-4fac3b",                 "LogCabin",         48f,  12f),  // 동측 소형
            new Entry("adobe-colonial-farmhouse-cf13d8",  "AdobeHouse",       36f, -28f),  // 남동 농가
            new Entry("orongo-house-run-e35d16",          "HouseRun",        -44f,  24f),  // 북서 연립
            new Entry("water-tower-dd6200",               "WaterTower",       24f, -34f),  // 남측 물탑
            new Entry("water-pump-house-121270",          "PumpHouse",        28f, -22f),  // 남동 양수장
            new Entry("country-well-8feec8",              "Well",              -4f,  -8f),  // 스폰 남서 근교 9m — 우물
            new Entry("chicken-coop-371f97",              "ChickenCoop",      16f, -30f),  // 남동 닭장
            new Entry("barrel-organ-cart-30799d",         "OrganCart",         6f,  14f),  // 스폰 북 인접 소품
            new Entry("trailhead-sign-aa29e1",            "TrailSign",        -8f, -14f),  // 남서 길목 표지판
            new Entry("hide-rain-catchment-funnel-e41d94", "RainCatchment",    34f, -34f),  // 농가 옆 빗물 받이
            new Entry("upgradeable-assembly-workbench-a18ff3", "AsmWorkbench", -20f,  10f),  // 서측 야외 작업대
            new Entry("chicha-barrel-77037f",             "ChichaBarrel",      10f, -12f),  // 남동 술통
            new Entry("town-welcome-arch-d2af7d",         "WelcomeArch",        0f,  20f, false), // 스폰 북측 입구 아치 — 통과 가능(렌더러 전용)
            new Entry("arched-passage-56393e",            "ArchPassage",       -8f,  24f, false), // 골목 아치 — 통과 가능
            new Entry("tupa-stone-tower-801c13",          "StoneTower",       -52f, -20f),  // 남서 감시탑
            new Entry("azalea-bush-7bb14d",               "AzaleaBush",        -6f,  22f),  // 북서 정원 소형
            new Entry("halloween-yard-scarecrow-cb0ffb",  "Scarecrow",         26f, -38f),  // 농경지 허수아비(밤 톤 소재)
            new Entry("modular-doorstep-bridge-df54e1",   "DoorstepBridge",   -16f,  30f),  // 문턱 다리(북서 — campfire와 15m)
            // ── [164차 P1] 가산 24종 — 건물 14 + 관문 3 + 탑 4 + 농가 2 + 기타 1 (기존 61종 전체 이격 검증) ──
            new Entry("tenshu-keep-2ff636",                 "TenshuKeep",        72f, -72f),  // 남동 외곽 천수각
            new Entry("brick-church-322885",                "BrickChurch",       52f,  44f),  // 북동 벽돌 교회
            new Entry("governor-s-audience-hall-a4d777",    "GovernorHall",      18f,  58f),  // 북 영주 알현실
            new Entry("village-hall-c6d3c8",                "VillageHall",      -56f, -16f),  // 서 마을 회관(11.2k 대형)
            new Entry("village-tavern-4e94e3",              "VillageTavern",     38f, -12f),  // 남동 선술집
            new Entry("cove-tavern-5861a6",                 "CoveTavern",       -24f, -34f),  // 남서 만 선술집
            new Entry("bathhouse-4ac41a",                   "Bathhouse",         44f,  -2f),  // 동 목욕탕
            new Entry("shopfront-house-43d94d",             "Shopfront",         30f,  38f),  // 북동 상점가
            new Entry("thatched-cottage-0e63f5",            "ThatchedCottage",   -2f, -44f),  // 남 초가
            new Entry("timber-wash-house-c7e7d5",           "WashHouse",        -38f, -10f),  // 서 세탁물 집
            new Entry("kura-storehouse-5c9631",             "KuraStore",         38f,  30f),  // 북동 쿠라 창고
            new Entry("stilted-granary-3e7d7d",             "StiltGranary",      48f, -48f),  // 남동 고상 창고
            new Entry("dock-warehouse-bf0967",              "DockWarehouse",    -56f, -48f),  // 남서 항구 창고
            new Entry("village-gatehouse-181a9e",           "VillageGate",        6f, -28f, false), // 남 관문 — 통과 가능(렌더러 전용)
            new Entry("arch-gatehouse-f4391b",              "ArchGate",          22f,  -2f, false), // 동 길목 아치 관문 — 통과 가능
            new Entry("camp-gate-2de13f",                   "CampGate",         -36f, -26f, false), // 서 야영지 관문 — 통과 가능
            new Entry("palisade-wall-621e29",               "PalisadeWall",      60f, -30f),  // 남동 외곽 목책벽 — Solid(막는 게 목적)
            new Entry("splay-leg-lookout-tower-6fae7e",     "LookoutTower",     -64f,  36f),  // 북서 외곽 망루
            new Entry("watchtower-140f78",                  "WatchTower",        64f,  10f),  // 동 외곽 망루
            new Entry("signal-beacon-tower-9c0d1a",         "BeaconTower",      -10f, -64f),  // 남 외곽 봉화대
            new Entry("minka-farmhouse-680475",             "MinkaHouse",        54f, -14f),  // 동 민가
            new Entry("machiya-townhouse-8ef64e",           "MachiyaRow",        18f,  44f),  // 북 상가 연립
            new Entry("gabled-farmhouse-c7534f",            "GabledFarm",        42f, -40f),  // 남동 박공 농가
            new Entry("stone-farmhouse-billet-2dbbf2",      "StoneFarm",        -50f, -32f),  // 서남 석조 농가
            // ── [162차 P1] 가산 6종 — 마을 수레/축산/야영 (기존 10종과 이격 검증 완료) ──
            new Entry("mine-cart-21d7f0",         "MineCart",         -74f,   2f),  // 광산 양새 옆 4.5m — 던전 입구 연출 세트
            new Entry("hayride-wagon-1641de",     "HayWagon",         -22f, -16f),  // 스폰 남서 27m — 건초 수레
            new Entry("flower-seller-s-hand-d617f9", "FlowerCart",     18f,  10f),  // 스폰 북동 20m — 꽃장수 수레(시장)
            new Entry("salvage-hand-cart-222304", "HandCart",          19f,   7f),  // 꽃수레 옆 3.2m — 인접 소품 조합(짐수레/수레 선례)
            new Entry("alpine-hay-barn-3965e5",   "HayBarn",           14f,  34f),  // 개방 축사와 11.7m — 건초 헛간
            new Entry("campfire-parent-b1c0d5",   "Campfire",         -12f,  18f),  // 스폰 북서 21.6m — 야영 모닥불(night)
        };

        /// <summary>
        /// 야외 건축물 61종 일괄 배치(37+24 — [164차 P1]) — GameSetup IdyllicDecoPlacer.PlaceAll 직후 훅.
        /// center는 호출부 규약 유지용 인자(배치 기준은 스폰 좌표 고정 — IdyllicDecoPlacer SPAWN_POS 선례).
        /// 멱등: ROOT_NAME 직속 자식 존재 시 스킵(IdyllicDecoPlacer.PlaceAll 가드 선례 동일).
        /// </summary>
        public static void PlaceAll(Transform center, Transform parent)
        {
            if (parent == null) return;
            if (parent.Find(ROOT_NAME) != null)
            {
                Debug.Log("[PolyforkOutdoorStructures] Already placed - skipping.");
                return;
            }

            var root = new GameObject(ROOT_NAME);
            root.transform.SetParent(parent, false);

            // 스폰 좌표 — IdyllicDecoPlacer L140-141 선례 동일(static 필드 직접 참조).
            float sx = ProjectName.Core.PlayerSpawnConfig.SpawnPosition.x;
            float sz = ProjectName.Core.PlayerSpawnConfig.SpawnPosition.z;

            int placed = 0;
            for (int i = 0; i < Entries.Length; i++)
            {
                var e = Entries[i];
                float x = sx + e.dx;
                float z = sz + e.dz;
                // y = GROUND_BASE + 지형 높이 — IdyllicDecoPlacer 선례(Plains/seed 42) 재사용.
                float y = GROUND_BASE + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, TERRAIN_SEED);
                // 구조물이 마을 중심(스폰)을 향하는 yaw — 결정론 Atan2(+z 전방 기준).
                float yaw = Mathf.Atan2(-e.dx, -e.dz) * Mathf.Rad2Deg;

                // [163차 P1] solid=false(아치류)는 렌더러 전용 — 통로를 막지 않음
                var go = e.solid
                    ? InteriorPropCatalog.PlacePolyforkSolid(
                        root.transform, e.id, "Outdoor_" + e.name, new Vector3(x, y, z), yaw, 0f)
                    : InteriorPropCatalog.PlacePolyfork(
                        root.transform, e.id, "Outdoor_" + e.name, new Vector3(x, y, z), yaw, 0f);
                if (go == null)
                    Debug.LogWarning($"[PolyforkOutdoorStructures] ⚠️ {e.id} 로드 실패 — 스킵");
                else
                    placed++;
            }
            Debug.Log($"[PolyforkOutdoorStructures] ✅ 야외 건축물 {placed}/{Entries.Length} 배치 완료 (스폰 기준 링)");
            PlaceLakeBoats(parent); // [162차 P2]
        }

        /// <summary>
        /// [162차 P2] 호수 선박 패스 — TerrainGenerator.Lakes(LCG 앵커, 결정론) 앞 3개 호수에 선박 5종 순환 배치.
        /// 결정론: 호수 리스트 순서 고정 + 선박 순환 할당(엔진 난수 0 — IdyllicDecoPlacer SEED 규약 동일).
        /// 위치: 호수 중심 북측 에지 방향 0.8r — 수면 위(+z=북 규약, IdyllicDecoPlacer shoreT 패턴 참고).
        /// 호수당 2개 시 8m 간격(중심 기준 좌우 4m). y = waterLevel + 0.02 — 수면 위 배치
        /// (연잎 y=waterLevel+0.02 선례. 바지선/롱보트/카누 모두 수면).
        /// yaw: 호수 중심 방향 고정(결정론 Atan2 — +z 전방 기준). 콜라이더: PlacePolyforkSolid(BoxCollider 1개).
        /// 호수가 0개면(데이터 없음) 전체 스킵+로그 — 예외 금지. 멱등: PlaceAll 가드(ROOT_NAME)가 커버 —
        /// OutdoorStructures 루트 아래 LakeBoats 계층에 배치하므로 가드 스킵 시 본 메서드도 자동 스킵.
        /// </summary>
        static void PlaceLakeBoats(Transform parent)
        {
            if (parent == null) return;
            Transform root = parent.Find(ROOT_NAME);
            if (root == null) return;   // PlaceAll 가드가 스킵한 경우 — 동일 멱등
            var lakes = TerrainGenerator.Lakes;
            if (lakes == null || lakes.Count == 0)
            {
                Debug.Log("[PolyforkOutdoorStructures] 호수 데이터 없음 — 호수 선박 배치 스킵.");
                return;
            }
            var boatsT = new GameObject("LakeBoats");
            boatsT.transform.SetParent(root, false);

            // 선박 5종 — 호수별 순환 할당(결정론: 호수 순서 고정 + 인덱스 순환)
            string[] boatIds =
            {
                "small-fishing-boat-4ac0e3",
                "hanga-roa-fishing-boat-f09534",
                "ship-s-longboat-f159fd",
                "camp-canoe-c65d2a",
                "dock-cargo-barge-69dc4b",
            };
            string[] boatNames =
            {
                "FishingBoat", "HangaBoat", "Longboat", "Canoe", "CargoBarge",
            };
            int placed = 0;
            int maxLakes = Mathf.Min(3, lakes.Count);
            for (int i = 0; i < maxLakes; i++)
            {
                var lake = lakes[i];
                // 호수당 1~2개 — 5종을 3개 호수에 2+2+1 순환 배분(5종 1:1 전체 커버)
                int boatCount = (i < boatIds.Length - maxLakes) ? 2 : 1;
                for (int j = 0; j < boatCount; j++)
                {
                    int idx = i * 2 + j;
                    if (idx >= boatIds.Length) break;
                    // 호수 중심 북측 에지 방향 0.8r — 수면 위(+z=북 규약). 2개 시 8m 간격(중심 기준 좌우 4m)
                    float dx = (j == 1) ? LAKE_BOAT_SPACING * 0.5f : 0f;
                    float x = lake.center.x + dx;
                    float z = lake.center.z + lake.radius * LAKE_BOAT_RADIUS;
                    // y = waterLevel + 0.02 — 수면 위 배치(IdyllicDecoPlacer 연잎 선례)
                    float y = lake.waterLevel + LAKE_BOAT_Y_LIFT;
                    // yaw: 호수 중심 방향 고정(결정론 Atan2 — +z 전방 기준, +x=동 +z=북)
                    float ddx = x - lake.center.x;
                    float ddz = z - lake.center.z;
                    float yaw = Mathf.Atan2(-ddx, -ddz) * Mathf.Rad2Deg;
                    var go = InteriorPropCatalog.PlacePolyforkSolid(
                        boatsT.transform, boatIds[idx], "LakeBoat_" + boatNames[idx], new Vector3(x, y, z), yaw, 0f);
                    if (go == null)
                        Debug.LogWarning($"[PolyforkOutdoorStructures] ⚠️ {boatIds[idx]} 로드 실패 — 스킵");
                    else
                        placed++;
                }
            }
            // [163차 P1] 등대 — 첫 호수 북측 에지 밖 10m 육지(수면 구조물 아님 — Solid 콜라이더)
            var l0 = lakes[0];
            float lx = l0.center.x;
            float lz = l0.center.z + l0.radius + 10f;
            float ly = GROUND_BASE + TerrainGenerator.GetHeightAt(lx, lz, BiomeType.Plains, TERRAIN_SEED);
            var lgo = InteriorPropCatalog.PlacePolyforkSolid(
                root.transform, "old-lighthouse-48ca6c", "Lake_Lighthouse", new Vector3(lx, ly, lz), 180f, 0f);
            if (lgo == null)
                Debug.LogWarning("[PolyforkOutdoorStructures] ⚠️ lighthouse 로드 실패 — 스킵");
            Debug.Log($"[PolyforkOutdoorStructures] ✅ 호수 선박 {placed}/{maxLakes} 호수 배치 완료 (수면 위 5종 순환)");
        }
    }
}
