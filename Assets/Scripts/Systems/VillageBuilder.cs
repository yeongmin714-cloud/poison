using System.Collections.Generic;
using ProjectName.Core.Data;
using ProjectName.Core.Utils;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// P31-C: 24개 마을(4국가 × 6 = 24) 절차 건물 배치기 + 대표 마을 실외 상점.
    ///
    /// 설계:
    ///  · 좌표는 VillagePlacementSystem.GetAllVillages()의 결정론 좌표를 그대로 사용한다.
    ///  · 마을마다 광장(원판) 1 + 집 4~6채 + 창고 1 + 우물 1을 배치한다.
    ///  · 배치는 마을 center 기준 결정론(System.Random 고정 시드) — 프로세스 재시작 간 동일.
    ///    UnityEngine.Random 사용 금지.
    ///  · 집은 center 반지름 8~28m 링에 균등 각도(+결정론 지터)로 분산, 최소 간격 6m 강제.
    ///  · 대표 마을(index 0, isRepresentative)에는 실외 상점 건물을 추가 배치하고
    ///    ShopPlaceholder(ProjectName.UI)를 부착해 실외에서 E키로 상점 창을 연다.
    ///    ※ 실내 진입용 BuildingTrigger는 상점에 붙이지 않는다(실외 상점 전용).
    ///    ※ 기존 성 내부 상점(TerritoryBuilder.SpawnInteriorFixtures)은 유지(롤백 안전).
    ///  · 지형 높이: TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42) + GROUND_BASE(1f)
    ///    — TerritoryBuilder.TrySpawnModelOrPlaceholder와 동일 기저 보정.
    ///  · 건물 표현: 'hut' GLB 우선(RuntimeModelLoader), 없으면 프리미티브 큐브 조합(몸통+지붕).
    ///    BuildingPlaceholder(마을 집 = NPCHouse / 창고·우물 = Other / 상점 = Shop) 부착.
    ///    건물 몸통 색은 국가 틴트 기준(동=녹갈, 서=황토, 남=적갈, 북=회백).
    ///  · 로그: [VillageBuilder] Nation=East Village=0 center=(x,z) buildings=6 layoutHash=...
    ///    layoutHash는 부트 간 배치 비교용 결정론 해시.
    ///
    /// 진입점: CoreSystemsBootstrap.BuildAllTerritories() 마지막에서 VillageBuilder.BuildAllVillages() 호출.
    /// </summary>
    public static class VillageBuilder
    {
        /// <summary>지형 기저 보정 (Ground_Inner 월드 y=1) — TerritoryBuilder와 동일.</summary>
        private const float GROUND_BASE = 1f;

        /// <summary>지형 높이 조회 시드 (TerritoryBuilder와 동일).</summary>
        private const int TerrainSeed = 42;

        /// <summary>건물(집/창고/상점/우물) 최소 간격 (m).</summary>
        private const float MinSpacing = 6f;

        /// <summary>마을 광장 반경 (m).</summary>
        private const float PlazaRadius = 6f;

        /// <summary>집/상점 폴백 대신 우선 사용하는 GLB 모델 키.</summary>
        private const string HutModelKey = "hut";

        /// <summary>결정론 시드 베이스 (VillagePlacementSystem.GetHash와 동일 알고리즘).</summary>
        private const int SeedBase = 9130;

        /// <summary>
        /// 전체 24개 마을(4국가 × 6) 절차 건물 일괄 배치 + 대표 마을 4곳에 실외 상점.
        /// 중복 호출 시 스킵(씬에 Villages_Root 존재 검사 — TerritoryBuilder와 동일 패턴).
        /// </summary>
        public static void BuildAllVillages()
        {
            // [ExecuteInEditMode] Awake 오염 방지 — 마을은 런타임(Play)에서만 절차 생성
            if (!Application.isPlaying)
                return;

            if (GameObject.Find("Villages_Root") != null)
            {
                Debug.Log("[VillageBuilder] 마을 이미 생성됨 — 스킵 (중복 방지)");
                return;
            }

            var root = new GameObject("Villages_Root");

            var villages = VillagePlacementSystem.GetAllVillages();
            int builtCount = 0;
            int shopCount = 0;
            int combinedHash = 17;

            foreach (var village in villages)
            {
                int layoutHash = BuildVillage(root.transform, village, out bool shopPlaced);
                combinedHash = combinedHash * 31 + layoutHash;
                builtCount++;
                if (shopPlaced) shopCount++;
            }

            Debug.Log($"[VillageBuilder] 전체 마을 빌드 완료: villages={builtCount} outdoorShops={shopCount} layoutHash={combinedHash:X8}");
        }

        /// <summary>단일 마을 건물 배치. 마을 단위 결정론 layoutHash 반환, 실외 상점 배치 여부 출력.</summary>
        private static int BuildVillage(Transform root, VillagePlacementSystem.VillageInfo village, out bool shopPlaced)
        {
            shopPlaced = false;
            string parentName = $"Village_{village.nation}_{village.index:D2}";

            var rng = new System.Random(SeedBase + GetHash($"{village.nation}_{village.index}_villagebuild"));

            var parentGo = new GameObject(parentName);
            parentGo.transform.SetParent(root, false);
            parentGo.transform.position = village.center;

            Color nationTint = GetNationBuildingTint(village.nation);
            var placed = new List<Vector3>();   // 간격 강제용 XZ 좌표 목록 (월드)
            int buildings = 0;
            int layoutHash = 17;

            // 1) 광장 — 마을 center 원판 (국가 톤 회색빛)
            CreatePlaza(parentGo.transform, village.center, nationTint);

            // 2) 우물 — 광장 바로 밖(반지름 7~9m), 간격 목록에 선반영
            Vector3 wellPos = CreateWell(parentGo.transform, village.center, rng, placed);
            layoutHash = FoldHash(layoutHash, wellPos);
            buildings++;

            // 3) 대표 마을 → 실외 상점 (집 배치 전에 간격 목록에 포함시켜 겹침 방지)
            if (village.isRepresentative)
            {
                Vector3 shopPos = CreateOutdoorShop(parentGo.transform, village.center, rng, nationTint, placed, layoutHash);
                layoutHash = FoldHash(layoutHash, shopPos);
                shopPlaced = true;
                buildings++;
            }

            // 4) 집 4~6채 — 반지름 8~28m 링, 균등 각도 + 결정론 지터, 최소 간격 6m 강제
            int houseCount = 4 + rng.Next(3); // 4~6
            for (int i = 0; i < houseCount; i++)
            {
                float slotAngle = (360f / houseCount) * i;
                float angleDeg = slotAngle - 10f + (float)rng.NextDouble() * 20f; // ±10° 결정론 지터
                // 안팎 교대 링: 짝수=안쪽(8~16m), 홀수=바깥쪽(19~28m) → 간격 확보에 유리
                float radius = (i % 2 == 0)
                    ? 8f + (float)rng.NextDouble() * 8f
                    : 19f + (float)rng.NextDouble() * 9f;
                float angleRad = angleDeg * Mathf.Deg2Rad;
                Vector3 spot = village.center + new Vector3(Mathf.Cos(angleRad) * radius, 0f, Mathf.Sin(angleRad) * radius);

                spot = EnforceSpacing(spot, placed);
                placed.Add(spot);

                var house = CreateVillageHouse(parentGo.transform, spot, village.center, nationTint, $"House_{i + 1}", layoutHash, i);
                layoutHash = FoldHash(layoutHash, house != null ? house.transform.position : spot);
                buildings++;
            }

            // 5) 창고 1채 — 바깥 링(22~27m), 결정론 각도
            {
                float angleDeg = 15f + (float)rng.NextDouble() * 40f;
                float radius = 22f + (float)rng.NextDouble() * 5f;
                float angleRad = angleDeg * Mathf.Deg2Rad;
                Vector3 spot = village.center + new Vector3(Mathf.Cos(angleRad) * radius, 0f, Mathf.Sin(angleRad) * radius);

                spot = EnforceSpacing(spot, placed);
                placed.Add(spot);

                var warehouse = CreateWarehouse(parentGo.transform, spot, village.center, nationTint, layoutHash);
                layoutHash = FoldHash(layoutHash, warehouse != null ? warehouse.transform.position : spot);
                buildings++;
            }

            Debug.Log($"[VillageBuilder] Nation={village.nation} Village={village.index} center=({village.center.x:F1},{village.center.z:F1}) buildings={buildings} layoutHash={layoutHash:X8}");
            return layoutHash;
        }

        // ────────────────────────────── 건물 팩토리 ──────────────────────────────

        /// <summary>광장: 마을 center 원판(실린더 지름 12m × 두께 0.12m), 회색+국가 톤.</summary>
        private static void CreatePlaza(Transform parent, Vector3 center, Color nationTint)
        {
            float groundY = TerrainGenerator.GetHeightAt(center.x, center.z, BiomeType.Plains, TerrainSeed) + GROUND_BASE;

            var plaza = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            plaza.name = "Plaza";
            plaza.transform.position = new Vector3(center.x, groundY + 0.06f, center.z); // 두께 0.12m → 절반만 위로
            plaza.transform.localScale = new Vector3(PlazaRadius * 2f, 0.06f, PlazaRadius * 2f); // 실린더 기본: 지름1 × 높이2
            plaza.tag = "Untagged";
            var renderer = plaza.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Color plazaColor = Color.Lerp(new Color(0.52f, 0.50f, 0.47f), nationTint, 0.35f); // 회색바탕 + 국가 톤
                renderer.material = MaterialHelper.CreateLitMaterial(plazaColor, "VillagePlaza_Mat");
            }
            plaza.transform.SetParent(parent, true);
        }

        /// <summary>우물: 광장 바로 밖(7~9m) 돌 원통 + 지붕판. BuildingPlaceholder(Other/"우물") 부착.</summary>
        private static Vector3 CreateWell(Transform parent, Vector3 center, System.Random rng, List<Vector3> placed)
        {
            float angleDeg = 30f + (float)rng.NextDouble() * 40f;
            float radius = 7f + (float)rng.NextDouble() * 2f;
            float angleRad = angleDeg * Mathf.Deg2Rad;
            Vector3 pos = center + new Vector3(Mathf.Cos(angleRad) * radius, 0f, Mathf.Sin(angleRad) * radius);
            pos = EnforceSpacing(pos, placed);
            placed.Add(pos);

            float groundY = TerrainGenerator.GetHeightAt(pos.x, pos.z, BiomeType.Plains, TerrainSeed) + GROUND_BASE;

            var well = new GameObject("Well");
            well.transform.position = new Vector3(pos.x, groundY, pos.z);
            well.transform.rotation = Quaternion.LookRotation(new Vector3(-pos.x + center.x, 0f, -pos.z + center.z), Vector3.up);

            // 돌 몸통
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Body";
            body.transform.SetParent(well.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            body.transform.localScale = new Vector3(1.2f, 0.45f, 1.2f); // 지름 1.2m × 높이 0.9m
            body.tag = "Untagged";
            var br = body.GetComponent<MeshRenderer>();
            if (br != null)
                br.material = MaterialHelper.CreateLitMaterial(new Color(0.55f, 0.55f, 0.53f), "VillageWell_Mat");

            // 지붕판
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Roof";
            roof.transform.SetParent(well.transform, false);
            roof.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            roof.transform.localScale = new Vector3(1.6f, 0.12f, 1.6f);
            roof.tag = "Untagged";
            var rr = roof.GetComponent<MeshRenderer>();
            if (rr != null)
                rr.material = MaterialHelper.CreateLitMaterial(new Color(0.35f, 0.25f, 0.16f), "VillageWellRoof_Mat");

            var ph = well.AddComponent<BuildingPlaceholder>();
            ph.buildingType = BuildingPlaceholder.BuildingType.Other;
            ph.buildingName = "우물";

            well.transform.SetParent(parent, true);
            return well.transform.position;
        }

        /// <summary>
        /// 실외 상점 건물 (대표 마을 전용): center 근처 반지름 10~15m, 결정론 각도.
        /// 상점 건물 GLB 카탈로그(주점/음식점/약초방) 우선, 없으면 'hut', 아니면 큐브 조합.
        /// BuildingPlaceholder(Shop/"상점", 노란빛 몸통) + ShopPlaceholder(ProjectName.UI) 부착
        /// → 실외에서 E키(3m 내)로 ShopWindowUTK 열림. BuildingTrigger는 붙이지 않는다(실내 진입 방지).
        /// </summary>
        private static Vector3 CreateOutdoorShop(Transform parent, Vector3 center, System.Random rng, Color nationTint, List<Vector3> placed, int layoutHash)
        {
            float angleDeg = (float)rng.NextDouble() * 360f;
            float dist = 10f + (float)rng.NextDouble() * 5f;
            float angleRad = angleDeg * Mathf.Deg2Rad;
            Vector3 pos = center + new Vector3(Mathf.Cos(angleRad) * dist, 0f, Mathf.Sin(angleRad) * dist);
            pos = EnforceSpacing(pos, placed);
            placed.Add(pos);

            float groundY = TerrainGenerator.GetHeightAt(pos.x, pos.z, BiomeType.Plains, TerrainSeed) + GROUND_BASE;
            Color shopColor = new Color(0.85f, 0.66f, 0.25f); // 노란빛 (상점 규약)

            string shopGlb = VillageBuildingCatalog.ShopPath(layoutHash);
            GameObject shop = VillageBuildingCatalog.InstantiateAtGround(
                shopGlb, pos, groundY, VillageBuildingCatalog.ShopTargetHeight, "OutdoorShop");
            if (shop == null && RuntimeModelLoader.TryGetModel(HutModelKey, out var hutPrefab))
            {
                shop = Object.Instantiate(hutPrefab);
                shop.name = "OutdoorShop";
                shop.transform.position = new Vector3(pos.x, groundY + 0.05f, pos.z);
                shop.transform.localScale = new Vector3(1.8f, 1.8f, 1.8f);
                Debug.Log("[VillageBuilder] GLB 모델 'hut'로 'OutdoorShop' 생성");
            }
            if (shop == null)
            {
                shop = new GameObject("OutdoorShop");
                shop.transform.position = new Vector3(pos.x, groundY, pos.z);

                var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = "Body";
                body.transform.SetParent(shop.transform, false);
                body.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                body.transform.localScale = new Vector3(3.4f, 2.4f, 3.0f);
                body.tag = "Untagged";
                var br = body.GetComponent<MeshRenderer>();
                if (br != null)
                    br.material = MaterialHelper.CreateLitMaterial(shopColor, "VillageShop_Mat");

                var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
                roof.name = "Roof";
                roof.transform.SetParent(shop.transform, false);
                roof.transform.localPosition = new Vector3(0f, 2.65f, 0f);
                roof.transform.localScale = new Vector3(4.0f, 0.5f, 3.6f);
                roof.tag = "Untagged";
                var rr = roof.GetComponent<MeshRenderer>();
                if (rr != null)
                    rr.material = MaterialHelper.CreateLitMaterial(Color.Lerp(shopColor, Color.black, 0.35f), "VillageShopRoof_Mat");
            }

            // 마을 광장(center)을 향하도록
            Vector3 face = new Vector3(pos.x - center.x, 0f, pos.z - center.z);
            if (face.sqrMagnitude > 0.0001f)
                shop.transform.rotation = Quaternion.LookRotation(-face, Vector3.up);

            var placeholder = shop.AddComponent<BuildingPlaceholder>();
            placeholder.buildingType = BuildingPlaceholder.BuildingType.Shop;
            placeholder.buildingName = "상점";

            // [핵심] 실외 E키 상점 — ShopPlaceholder(ProjectName.UI) 부착.
            // Systems 어셈블리는 UI asmdef를 참조하지 않으므로(순환 참조 방지) 기존 규약대로
            // 리플렉션 AddComponent — ArenaSystem/GameEndingManager와 동일 패턴.
            // UTKWireUp이 ToggleShopRequestedUTK를 구독 → E키 3m 내에서 ShopWindowUTK 열림.
            // ※ 실내 진입용 IndoorTransitionSetup.CreateBuildingTrigger / BuildingTrigger는 붙이지 않는다.
            var shopPlaceholderType = System.Type.GetType("ProjectName.UI.ShopPlaceholder, ProjectName.UI");
            if (shopPlaceholderType != null)
            {
                shop.AddComponent(shopPlaceholderType);
            }
            else
            {
                Debug.LogWarning("[VillageBuilder] ProjectName.UI.ShopPlaceholder 타입 미발견 — E키 상호작용 없이 상점 건물만 배치됨");
            }

            shop.transform.SetParent(parent, true);
            Debug.Log($"[VillageBuilder] 대표 마을 실외 상점 배치: {parentNameOf(parent)} pos=({pos.x:F1},{pos.z:F1}) — E키 상호작용 (ShopPlaceholder, BuildingTrigger 없음)");
            return shop.transform.position;
        }

        /// <summary>마을 집: 건물 GLB 카탈로그(집/둥글/부자집 풀) 우선, 없으면 'hut' GLB, 마지막 큐브 조합.
        /// 건물 인덱스는 결정론(마을 layoutHash + 집i)으로 풀에서 선택. BuildingPlaceholder(NPCHouse) 부착.</summary>
        private static GameObject CreateVillageHouse(Transform parent, Vector3 pos, Vector3 center, Color nationTint, string name, int layoutHash, int houseIndex)
        {
            float groundY = TerrainGenerator.GetHeightAt(pos.x, pos.z, BiomeType.Plains, TerrainSeed) + GROUND_BASE;

            // 결정론 선택: 부자집은 4집당 1회, 나머지는 일반 집/둥글 풀
            bool rich = ((layoutHash + houseIndex) % 4) == 0;
            string glbPath = rich
                ? VillageBuildingCatalog.RichPath(layoutHash + houseIndex * 2)
                : VillageBuildingCatalog.HousePath(layoutHash + houseIndex);
            float targetH = rich ? VillageBuildingCatalog.RichTargetHeight : VillageBuildingCatalog.HouseTargetHeight;

            GameObject house = VillageBuildingCatalog.InstantiateAtGround(
                glbPath, pos, groundY, targetH, name);
            if (house != null)
            {
                house.transform.rotation = Quaternion.LookRotation(new Vector3(pos.x - center.x, 0f, pos.z - center.z), Vector3.up);
                var ph = house.AddComponent<BuildingPlaceholder>();
                ph.buildingType = BuildingPlaceholder.BuildingType.NPCHouse;
                ph.buildingName = "마을 집";
                house.transform.SetParent(parent, true);
                return house;
            }

            // 폴백: hut GLB
            if (RuntimeModelLoader.TryGetModel(HutModelKey, out var hutPrefab))
            {
                house = Object.Instantiate(hutPrefab);
                house.name = name;
                house.transform.position = new Vector3(pos.x, groundY + 0.05f, pos.z);
                house.transform.localScale = new Vector3(1.4f, 1.4f, 1.4f);
                house.transform.rotation = Quaternion.LookRotation(new Vector3(pos.x - center.x, 0f, pos.z - center.z), Vector3.up);
                var ph = house.AddComponent<BuildingPlaceholder>();
                ph.buildingType = BuildingPlaceholder.BuildingType.NPCHouse;
                ph.buildingName = "마을 집";
                house.transform.SetParent(parent, true);
                return house;
            }

            // 최종 폴백: 큐브 조합 (기존 로직)
            house = new GameObject(name);
            house.transform.position = new Vector3(pos.x, groundY, pos.z);
            Color wallColor = Color.Lerp(nationTint, Color.white, 0.18f);
            Color roofColor = Color.Lerp(nationTint, Color.black, 0.35f);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(house.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            body.transform.localScale = new Vector3(2.2f, 1.6f, 2.2f);
            body.tag = "Untagged";
            var br = body.GetComponent<MeshRenderer>();
            if (br != null)
                br.material = MaterialHelper.CreateLitMaterial(wallColor, $"{name}_Mat");

            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Roof";
            roof.transform.SetParent(house.transform, false);
            roof.transform.localPosition = new Vector3(0f, 1.85f, 0f);
            roof.transform.localScale = new Vector3(2.7f, 0.5f, 2.7f);
            roof.tag = "Untagged";
            var rr = roof.GetComponent<MeshRenderer>();
            if (rr != null)
                rr.material = MaterialHelper.CreateLitMaterial(roofColor, $"{name}_Roof_Mat");

            Vector3 face = new Vector3(pos.x - center.x, 0f, pos.z - center.z);
            if (face.sqrMagnitude > 0.0001f)
                house.transform.rotation = Quaternion.LookRotation(-face, Vector3.up);

            var ph2 = house.AddComponent<BuildingPlaceholder>();
            ph2.buildingType = BuildingPlaceholder.BuildingType.NPCHouse;
            ph2.buildingName = "마을 집";

            house.transform.SetParent(parent, true);
            return house;
        }

        /// <summary>창고: 건물 GLB 카탈로그(쉼터/리테일) 우선, 없으면 큐브 조합. BuildingPlaceholder(Other/"창고") 부착.</summary>
        private static GameObject CreateWarehouse(Transform parent, Vector3 pos, Vector3 center, Color nationTint, int layoutHash)
        {
            float groundY = TerrainGenerator.GetHeightAt(pos.x, pos.z, BiomeType.Plains, TerrainSeed) + GROUND_BASE;

            string whGlb = VillageBuildingCatalog.ShelterPath(layoutHash);
            var warehouse = VillageBuildingCatalog.InstantiateAtGround(
                whGlb, pos, groundY, VillageBuildingCatalog.ShelterTargetHeight, "Warehouse");
            if (warehouse != null)
            {
                warehouse.transform.rotation = Quaternion.LookRotation(new Vector3(pos.x - center.x, 0f, pos.z - center.z), Vector3.up);
                var ph = warehouse.AddComponent<BuildingPlaceholder>();
                ph.buildingType = BuildingPlaceholder.BuildingType.Other;
                ph.buildingName = "창고";
                warehouse.transform.SetParent(parent, true);
                return warehouse;
            }

            // 폴백: 큐브 조합
            warehouse = new GameObject("Warehouse");
            warehouse.transform.position = new Vector3(pos.x, groundY, pos.z);
            Color wallColor = Color.Lerp(nationTint, Color.black, 0.15f);
            Color roofColor = Color.Lerp(nationTint, Color.black, 0.45f);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(warehouse.transform, false);
            body.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            body.transform.localScale = new Vector3(3.5f, 2.2f, 2.8f);
            body.tag = "Untagged";
            var br = body.GetComponent<MeshRenderer>();
            if (br != null)
                br.material = MaterialHelper.CreateLitMaterial(wallColor, "VillageWarehouse_Mat");

            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Roof";
            roof.transform.SetParent(warehouse.transform, false);
            roof.transform.localPosition = new Vector3(0f, 2.45f, 0f);
            roof.transform.localScale = new Vector3(4.1f, 0.5f, 3.4f);
            roof.tag = "Untagged";
            var rr = roof.GetComponent<MeshRenderer>();
            if (rr != null)
                rr.material = MaterialHelper.CreateLitMaterial(roofColor, "VillageWarehouseRoof_Mat");

            Vector3 face = new Vector3(pos.x - center.x, 0f, pos.z - center.z);
            if (face.sqrMagnitude > 0.0001f)
                warehouse.transform.rotation = Quaternion.LookRotation(-face, Vector3.up);

            var ph2 = warehouse.AddComponent<BuildingPlaceholder>();
            ph2.buildingType = BuildingPlaceholder.BuildingType.Other;
            ph2.buildingName = "창고";

            warehouse.transform.SetParent(parent, true);
            return warehouse;
        }

        // ────────────────────────────── 헬퍼 ──────────────────────────────

        /// <summary>후보 위치가 기배치 건물과 6m 미만이면 바깥쪽으로 결정론적으로 밀어낸다.</summary>
        private static Vector3 EnforceSpacing(Vector3 candidate, List<Vector3> placed)
        {
            for (int iter = 0; iter < 8; iter++)
            {
                float minDist = float.MaxValue;
                for (int i = 0; i < placed.Count; i++)
                {
                    float dx = candidate.x - placed[i].x;
                    float dz = candidate.z - placed[i].z;
                    float d = Mathf.Sqrt(dx * dx + dz * dz);
                    if (d < minDist) minDist = d;
                }
                if (minDist >= MinSpacing) break;

                // 마을 center(원점 방향)에서 바깥쪽으로 밀어내기 (결정론 — 랜덤 없음)
                Vector3 flat = new Vector3(candidate.x, 0f, candidate.z);
                float r = flat.magnitude;
                Vector3 dir = r > 0.01f ? flat / r : new Vector3(1f, 0f, 0f);
                candidate = candidate + dir * (MinSpacing - minDist + 0.5f);
            }
            return candidate;
        }

        /// <summary>건물 국가 틴트 — NationTerrainController 흙길 색 규약과 동일 (동=녹갈, 서=황토, 남=적갈, 북=회백).</summary>
        private static Color GetNationBuildingTint(NationType nation) => nation switch
        {
            NationType.East  => new Color(0.42f, 0.34f, 0.22f), // 동 — 녹갈 토
            NationType.West  => new Color(0.50f, 0.38f, 0.22f), // 서 — 황토
            NationType.South => new Color(0.48f, 0.31f, 0.20f), // 남 — 적갈
            NationType.North => new Color(0.40f, 0.40f, 0.38f), // 북 — 회백 설토
            _                => new Color(0.50f, 0.48f, 0.45f)  // 기타 — 회색
        };

        /// <summary>layoutHash 폴딩 — XZ 좌표(cm 단위 절삭)를 결정론 해시에 합산.</summary>
        private static int FoldHash(int hash, Vector3 pos)
        {
            unchecked
            {
                hash = hash * 31 + (int)(pos.x * 100f);
                hash = hash * 31 + (int)(pos.z * 100f);
                return hash;
            }
        }

        /// <summary>결정론 문자열 해시 — VillagePlacementSystem과 동일 알고리즘.</summary>
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

        /// <summary>로그용 부모 컨테이너 이름 추출.</summary>
        private static string parentNameOf(Transform parent) => parent != null ? parent.name : "?";
    }
}
