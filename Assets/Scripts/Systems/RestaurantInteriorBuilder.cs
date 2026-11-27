using UnityEngine;
using ProjectName.Core.Utils;

namespace ProjectName.Systems
{
    /// <summary>
    /// 레스토랑 실내 생성기.
    /// TavernInteriorBuilder 패턴 — 방 셸(바닥/벽/천장/조명)은 프리미티브로 만들고,
    /// 가구(카운터/테이블/의자)는 RuntimeModelLoader.TryGetModel GLB 로드로 배치합니다.
    /// GLB 출처: Resources/Models/Restaurant/ (별칭: RestaurantCounter/RestaurantTable/RestaurantChair).
    /// 배치 계약: 카운터 (0,0,4), 테이블 4개 (±2,±2), 의자 8개(테이블당 좌우 2개) — 전부 고정 배치(결정론).
    /// </summary>
    public static class RestaurantInteriorBuilder
    {
        private const string ROOM_NAME = "RestaurantRoom";
        private const float WALL_THICKNESS = 0.1f;

        // 방 크기 — 카운터 z=4와 테이블 (±2,±2) 배치를 수용하는 정방형
        private const float ROOM_WIDTH = 12f;
        private const float ROOM_HEIGHT = 3.5f;
        private const float ROOM_DEPTH = 10f;

        // GLB 목표 높이(m) — 렌더러 바운즈 스케일 정규화 기준
        private const float COUNTER_HEIGHT = 1.0f;
        private const float TABLE_HEIGHT = 0.75f;
        private const float CHAIR_HEIGHT = 0.9f;

        /// <summary>
        /// 레스토랑 내부를 생성합니다.
        /// </summary>
        public static GameObject BuildRestaurantInterior()
        {
            float width = ROOM_WIDTH;
            float height = ROOM_HEIGHT;
            float depth = ROOM_DEPTH;

            GameObject room = new GameObject(ROOM_NAME);
            room.transform.position = Vector3.zero;

            // 바닥
            CreateFloor(room, width, depth);

            // 벽
            CreateWalls(room, width, height, depth);

            // 천장
            CreateCeiling(room, width, height, depth);

            // 카운터 (뒷벽 쪽, 영업 방향 -z)
            PlaceModel(room, "RestaurantCounter", "Counter", new Vector3(0f, 0f, 4f), 180f, COUNTER_HEIGHT);

            // 테이블 4개 — (±2,±2) 고정 배치
            var tableSpots = new Vector3[4]
            {
                new Vector3(-2f, 0f, -2f),
                new Vector3(2f, 0f, -2f),
                new Vector3(-2f, 0f, 2f),
                new Vector3(2f, 0f, 2f),
            };
            for (int i = 0; i < tableSpots.Length; i++)
            {
                PlaceModel(room, "RestaurantTable", $"Table_{i}", tableSpots[i], 0f, TABLE_HEIGHT);

                // 의자 2개 — 테이블 좌우, 테이블을 향하도록 회전 (4테이블 × 2 = 8개)
                float tx = tableSpots[i].x;
                float tz = tableSpots[i].z;
                PlaceModel(room, "RestaurantChair", $"Chair_{i}_L", new Vector3(tx - 0.9f, 0f, tz), 90f, CHAIR_HEIGHT);
                PlaceModel(room, "RestaurantChair", $"Chair_{i}_R", new Vector3(tx + 0.9f, 0f, tz), -90f, CHAIR_HEIGHT);
            }

            // 따뜻한 조명 — 선술집보다 밝은 식당 앰비언트 + 중앙 포인트 라이트
            IndoorLighting.SetupIndoorLighting(room, new Color(0.16f, 0.12f, 0.08f), 0.35f, false);
            IndoorLighting.AddPointLight(room, new Vector3(0f, 2.5f, 0f), new Color(1f, 0.9f, 0.75f), 10f, 0.9f);

            // [수조 통일] 천장 제거+남측 벽 시각 절단(콜라이더 유지) — 9/20 실내씬 예시 기준
            IndoorDioramaKit.Apply(room);

            // [실내 고품질화 P3] 가구/조명/소품 가산 패스.
            InteriorFurnishingPass.ApplyBuilding(room, "restaurant", ROOM_WIDTH, ROOM_DEPTH, null, ROOM_HEIGHT);
            return room;
        }

        /// <summary>
        /// GLB 가구 배치 공용 — RuntimeModelLoader.TryGetModel 로드 → 스케일/접지 정규화 → 배치.
        /// 로드 실패 시 경고 1행 후 스킵(ArenaInteriorBuilder.TryLoadArenaGlb 선례).
        /// </summary>
        private static void PlaceModel(GameObject room, string alias, string name, Vector3 position, float yaw, float targetHeight)
        {
            if (!RuntimeModelLoader.TryGetModel(alias, out var prefab) || prefab == null)
            {
                Debug.LogWarning($"[RestaurantInteriorBuilder] GLB '{alias}' 없음 — {name} 배치 스킵.");
                return;
            }

            var instance = Object.Instantiate(prefab, room.transform, false);
            instance.name = name;
            instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            NormalizeFurniture(instance, targetHeight);
        }

        /// <summary>
        /// 가구 GLB 정규화 — 렌더러 바운즈 합산으로 목표 높이 스케일 + 최하단 바닥(y=0) 접지
        /// (ArenaInteriorBuilder.NormalizeNpcVisual / ArenaBettingSystem.NormalizeHeight 선례).
        /// 부모 체인(room)이 원점·무회전·스케일 1이라 월드 바운즈를 로컬 오프셋에 직접 반영한다.
        /// </summary>
        private static void NormalizeFurniture(GameObject instance, float targetHeight)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0)
                return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            if (bounds.size.y <= 0.01f)
                return;

            float k = targetHeight / bounds.size.y;
            instance.transform.localScale *= k;

            // 스케일 적용 후 재측정 → 최하단을 바닥에 접지
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            instance.transform.localPosition += Vector3.down * bounds.min.y;
        }

        private static void CreateFloor(GameObject room, float width, float depth)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(room.transform);
            floor.transform.localPosition = new Vector3(0, -0.05f, 0);
            floor.transform.localScale = new Vector3(width, 0.1f, depth);

            var renderer = floor.GetComponent<MeshRenderer>();
            // [무번호 32] 영지 실내 공통 석타일 — 목재 색 바닥 폐기(씬 통일 계약). Cube UV 0..1 → 1m 타일 스케일.
            var mat = InteriorQualityBaseline.CreateCommonInteriorFloorMat(width, depth, "Restaurant");
            renderer.material = mat;
        }

        private static void CreateWalls(GameObject room, float width, float height, float depth)
        {
            CreateWallPiece(room, "Wall_Front", new Vector3(0, height / 2f, -depth / 2f), new Vector3(width, height, WALL_THICKNESS));
            CreateWallPiece(room, "Wall_Back", new Vector3(0, height / 2f, depth / 2f), new Vector3(width, height, WALL_THICKNESS));
            CreateWallPiece(room, "Wall_Left", new Vector3(-width / 2f, height / 2f, 0), new Vector3(WALL_THICKNESS, height, depth));
            CreateWallPiece(room, "Wall_Right", new Vector3(width / 2f, height / 2f, 0), new Vector3(WALL_THICKNESS, height, depth));
        }

        private static GameObject CreateWallPiece(GameObject room, string name, Vector3 position, Vector3 scale)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(room.transform);
            wall.transform.localPosition = position;
            wall.transform.localScale = scale;

            var renderer = wall.GetComponent<MeshRenderer>();
            var mat = MaterialHelper.CreateLitMaterial(new Color(0.78f, 0.70f, 0.58f), "RestaurantWallMat");
            if (mat != null)
                renderer.material = mat;

            return wall;
        }

        private static void CreateCeiling(GameObject room, float width, float height, float depth)
        {
            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.name = "Ceiling";
            ceiling.transform.SetParent(room.transform);
            ceiling.transform.localPosition = new Vector3(0, height - 0.05f, 0);
            ceiling.transform.localScale = new Vector3(width, 0.1f, depth);

            var renderer = ceiling.GetComponent<MeshRenderer>();
            var mat = MaterialHelper.CreateLitMaterial(new Color(0.85f, 0.78f, 0.65f), "RestaurantCeilingMat");
            if (mat != null)
                renderer.material = mat;
        }
    }
}
