using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// Heightmap 지형에 UserProvided GLB 환경 모델(나무/바위)을 GPU Instancing으로 대량 배치.
    /// 바이옴(방위)별 분포 + 제외존(엠파이어 중앙, 호수, 스폰, 지도 경계) 적용.
    /// 월드 지표면 y = 1f + TerrainGenerator.GetHeightAt(...) (Ground_Inner 기저 1f 포함).
    /// 정적 엔트리포인트: PlaceAllIfNeeded(Transform parent) — 상위(FixMainScene)가 통합 페이즈에서 호출.
    /// </summary>
    public static class TerrainModelPlacer
    {
        // === 배치 상수 ===
        const string MARKER_NAME = "TerrainModelPlacer_Marker";
        const int TREE_ATTEMPTS = 1050;  // 나무 배치 시도 수 (바이옴 수락 확률 반영 시 최종 ≈500)
        const int ROCK_ATTEMPTS = 595;   // 바위 배치 시도 수 (바이옴 수락 확률 반영 시 최종 ≈400)
        const float BOUND_MAX = 950f;    // 지도 경계 (±950m 밖 제외)
        const float EMPIRE_EXCLUDE = 120f;   // 엠파이어 중앙(0,0,0) 제외 반경
        const float SPAWN_X = 728f;
        const float SPAWN_Z = -529f;
        const float SPAWN_EXCLUDE = 5f;      // 스폰지 제외 반경
        const float LAKE_MARGIN_FACTOR = 1.15f; // 호수 해안 여백 (radius*이값 밖)
        const int PROP_SEED = 20260901;    // 고정 시드 (UnityEngine.Random 언시드 금지)
        const float GROUND_BASE = 1f;        // Ground_Inner 월드 y 기저

        // === 산/바위 절벽·군락 소수 배치 (09-24 재활성 — 국가별 6~10개) ===
        // [09-04 제거된 대량 배치(나무~500/바위~400) 대체] 대형 GLB를 국가별 소수만 배치해
        // 절벽·군락 실루엣을 확보한다. 콜라이더 필수 부착(접지 안전) + 제외존 확장:
        // 영지 성 60m / 마을+10m / 자연자원 노드 8m / 흙길 10m / 호수 / 엠파이어 / 스폰지.
        const int MOUNTAIN_ATTEMPTS = 1200;     // 소수 배치 시도 수 (제외존·간격 통과분만 배치)
        const float MOUNTAIN_GAP = 90f;         // 산/군락 간 최소 간격 (m)
        const float RIDGE_MIN_SLOPE = 14f;      // 절벽형(능선·방위경계) 판정 최소 경사 (도)
        const int RIDGE_PER_NATION = 3;         // 국가별 절벽형(대형) 목표 — 나머지는 바위군락
        const float RIDGE_SCALE_MIN = 3.0f, RIDGE_SCALE_MAX = 5.0f;       // 절벽/산 대형 스케일
        const float CLUSTER_SCALE_MIN = 1.5f, CLUSTER_SCALE_MAX = 2.8f;   // 바위군락 스케일
        const float CASTLE_EXCLUDE = 60f;       // 영지 성 앵커 제외 반경 (m)
        const float VILLAGE_EXTRA = 10f;        // 마을 반경(40m) 추가 여유 (m)
        const float RESOURCE_EXCLUDE = 8f;      // 자연자원 노드(돌/나무 채집) 제외 반경 (m)
        const float DIRT_PATH_EXCLUDE = 10f;    // 흙길(반폭 3.5m + 스커트) 제외 반경 (m)
        const float BASE_SINK = 0.8f;           // 모델 기부 지형 침하 (절벽 결합, 뜸 방지)
        const string RESOURCE_CONTAINER = "NaturalResources";   // 자원노드 컨테이너 (NaturalResourceSpawner)

        /// <summary>'NaturalResources' 컨테이너 자식 위치 캐시 (배치 진입 시 1회).</summary>
        static List<Vector2> s_resourceXZ;

        // === 바이옴별 수락 확률 ===
        // East(초원)=나무 다수+바위 소량 / North(설산)=나무 중간+큰 바위
        // West(화산)=바위 다수+나무 아주 적음 / South(사막)=바위 위주+나무 거의 없음
        // Empire(중앙 120m)=제외 (0)
        static float TreeAcceptance(NationType n)
        {
            switch (n)
            {
                case NationType.East:  return 1.0f;   // 다수
                case NationType.North: return 0.7f;   // 중간
                case NationType.West:  return 0.15f;  // 아주 적음
                case NationType.South: return 0.05f;  // 거의 없음
                default:               return 0f;     // Empire 등 → 미배치
            }
        }

        static float RockAcceptance(NationType n)
        {
            switch (n)
            {
                case NationType.East:  return 0.2f;   // 소량
                case NationType.North: return 0.5f;   // 바위 활용, 큰 바위 강조
                case NationType.West:  return 1.0f;   // 다수
                case NationType.South: return 1.0f;   // 위주
                default:               return 0f;     // Empire 등 → 미배치
            }
        }

        /// <summary>
        /// 진입점. parent 하위에 이미 배치 마커가 있으면 스킵(중복 실행 가드).
        /// 나무 ~500 + 바위 ~400 (바이옴 분포 반영, 제외존 적용).
        /// </summary>
        public static void PlaceAllIfNeeded(Transform parent)
        {
            if (parent == null) return;

            // 중복 실행 가드
            if (FindChild(parent, MARKER_NAME) != null)
            {
                Debug.Log("[TerrainModelPlacer] Already placed — skipping.");
                return;
            }

            // GLB 모델 로드 — 산/절벽·바위군락은 rocks GLB만 사용 (trees는 IdyllicDecoPlacer 담당)
            var rockModels = Resources.LoadAll<GameObject>("Models/UserProvided/terrain/rocks");
            if (rockModels.Length == 0)
            {
                Debug.LogError("[TerrainModelPlacer] GLB rock models not found in Resources/Models/UserProvided/terrain/rocks/ — skip placement.");
                return;
            }

            var envParent = new GameObject("EnvironmentModels");
            envParent.transform.SetParent(parent, false);
            envParent.layer = 0; // Default — 몬스터 스폰 raycast(Ground|Terrain 마스크)에서 자동 무시

            var rng = new System.Random(PROP_SEED);

            // [보존] 기존 대량 배치(나무~500/바위~400, TREE_ATTEMPTS=1050/ROCK_ATTEMPTS=595) —
            // 사용자 지시로 09-04 비활성 → 09-24 국가별 6~10개 소수 배치로 재활성. 롤백 시 아래 주석 해제:
            // var treeModels = Resources.LoadAll<GameObject>("Models/UserProvided/terrain/trees");
            // int treePlaced = PlaceType(envParent.transform, treeModels, TREE_ATTEMPTS, TreeAcceptance, 0.8f, 1.8f, false, rng);
            // int rockPlaced = PlaceType(envParent.transform, rockModels, ROCK_ATTEMPTS, RockAcceptance, 0.8f, 2.0f, true, rng);

            // 산/절벽·바위군락 소수 배치 (국가별 6~10개, 콜라이더 부착, 제외존 확장)
            int mountainPlaced = PlaceMountainOutcrops(envParent.transform, rockModels, rng);

            // GPU Instancing 활성화 (기존 구조 유지 — 공유 머티리얼 enableInstancing)
            EnableGPUInstancing(envParent);

            // 배치 마커 (중복 실행 방지)
            var marker = new GameObject(MARKER_NAME);
            marker.transform.SetParent(parent, false);
            marker.SetActive(false);

            Debug.Log(string.Format("[TerrainModelPlacer] 산/절벽·바위군락 소수 배치: {0}개 (국가별 6~10 목표). Environment children: {1}", mountainPlaced, envParent.transform.childCount));
        }

        // ================================================================
        // 산/바위 절벽·군락 소수 배치 (09-24 재활성)
        // ================================================================
        /// <summary>
        /// 산/절벽·바위군락 GLB를 국가별 6~10개만 배치 (대량 배치 대체 — 성능 보수).
        ///  · 절벽형(능선): 경사 ≥ 14도 지점 우선 — 능선/방위경계/하천 절벽 근처에 대형 스케일 배치.
        ///  · 제외존: 엠파이어 120m / 호수 ×1.15 / 스폰지 / 흙길 10m / 영지 성 60m / 마을+10m /
        ///    자연자원 노드 8m ('NaturalResources' 컨테이너 실존 시) / 산 간 최소 간격 90m.
        ///  · 접지 계약: 밑면 지표 정렬 + 기부 침하 후 MeshCollider 전수 부착 (폴백 BoxCollider) —
        ///    콜라이더 없는 모델 미배치 원칙.
        /// </summary>
        static int PlaceMountainOutcrops(Transform parent, GameObject[] models, System.Random rng)
        {
            // 마을/성 앵커 (결정론 좌표 조회 — 실패 시 성/마을 제외만 생략, 게임 계속)
            List<VillagePlacementSystem.VillageInfo> villages = null;
            try { villages = VillagePlacementSystem.GetAllVillages(); }
            catch (System.Exception) { villages = null; }

            CacheResourceNodes();   // 자원노드 위치 캐시 (컨테이너 미존재 시 무시)

            var placedXZ = new List<Vector2>(40);                 // 산 간 최소 간격 체크
            var totalCount = new Dictionary<NationType, int>();   // 국가별 총 배치 수
            var ridgeCount = new Dictionary<NationType, int>();   // 국가별 절벽형 수
            int placed = 0;

            for (int i = 0; i < MOUNTAIN_ATTEMPTS; i++)
            {
                float x = RandomRange(rng, -BOUND_MAX, BOUND_MAX);
                float z = RandomRange(rng, -BOUND_MAX, BOUND_MAX);

                // === 제외존 (기존 + 확장) ===
                if (Mathf.Sqrt(x * x + z * z) < EMPIRE_EXCLUDE) continue;          // 엠파이어 중앙
                if (IsInLakeExclusion(x, z)) continue;                             // 호수 (해안 여백 포함)
                if (IsInSpawnExclusion(x, z)) continue;                            // 스폰지
                if (IsNearDirtPath(x, z, DIRT_PATH_EXCLUDE)) continue;             // 흙길
                if (IsNearCastleOrVillage(x, z, villages)) continue;               // 영지 성/마을
                if (IsNearCachedResourceNodes(x, z)) continue;                     // 자원노드

                // === 바이옴 판정 + 국가별 목표 수 ===
                NationType nation = NationTerrainController.GetNationFromPosition(new Vector3(x, 0f, z));
                if (nation == NationType.None || nation == NationType.Dracula) continue;
                int target = MountainTarget(nation);
                if (target == 0) continue;
                if (GetCount(totalCount, nation) >= target) continue;

                // === 산 간 최소 간격 ===
                bool tooClose = false;
                for (int j = 0; j < placedXZ.Count; j++)
                {
                    float gdx = x - placedXZ[j].x;
                    float gdz = z - placedXZ[j].y;
                    if (gdx * gdx + gdz * gdz < MOUNTAIN_GAP * MOUNTAIN_GAP) { tooClose = true; break; }
                }
                if (tooClose) continue;

                // === 절벽형(능선·방위경계) vs 바위군락 ===
                float slope = TerrainSplatBaker.EstimateSlopeDegrees(x, z);
                bool asRidge = GetCount(ridgeCount, nation) < RIDGE_PER_NATION && slope >= RIDGE_MIN_SLOPE;

                // === y = 기저 1f + 지형 높이 (지표면 계약, 기존 수식 동일) ===
                float y = GROUND_BASE + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42);

                var model = models[rng.Next(models.Length)];
                var go = Object.Instantiate(model, parent);
                go.layer = 0; // Default — 몬스터 스폰 raycast(Ground|Terrain 마스크) 자동 무시
                go.name = string.Format("Mountain_{0}_{1:D2}", nation, placed + 1);
                go.transform.position = new Vector3(x, y, z);
                go.transform.rotation = Quaternion.Euler(0f, RandomRange(rng, 0f, 360f), 0f);
                go.transform.localScale = Vector3.one * (asRidge
                    ? RandomRange(rng, RIDGE_SCALE_MIN, RIDGE_SCALE_MAX)
                    : RandomRange(rng, CLUSTER_SCALE_MIN, CLUSTER_SCALE_MAX));

                AlignBottomToGround(go, BASE_SINK);   // 밑면 지표 정렬 + 기부 침하 (뜸/박힘 방지)
                AttachGroundColliders(go);            // 접지 계약 — 콜라이더 필수

                placedXZ.Add(new Vector2(x, z));
                totalCount[nation] = GetCount(totalCount, nation) + 1;
                if (asRidge) ridgeCount[nation] = GetCount(ridgeCount, nation) + 1;
                placed++;
            }
            return placed;
        }

        /// <summary>국가별 산/군락 목표 수 (국가별 6~10개 — East 6 / North 10 / West 10 / South 8).</summary>
        static int MountainTarget(NationType n)
        {
            switch (n)
            {
                case NationType.East:  return 6;
                case NationType.North: return 10;
                case NationType.West:  return 10;
                case NationType.South: return 8;
                default:               return 0;   // Empire/None 등 → 미배치
            }
        }

        static int GetCount(Dictionary<NationType, int> dict, NationType key)
        {
            int v;
            return dict.TryGetValue(key, out v) ? v : 0;
        }

        /// <summary>
        /// 흙길 위 배치 금지 판정 — DirtPaths 세그먼트 AABB 사전 필터 후 점-선분 최단거리.
        /// NationTerrainController.DirtPaths (결정론 캐시 53 세그먼트, IdyllicDecoPlacer 선례).
        /// </summary>
        static bool IsNearDirtPath(float x, float z, float radius)
        {
            var paths = NationTerrainController.DirtPaths;
            if (paths == null || paths.Count == 0) return false;
            int count = paths.Count;
            float r2 = radius * radius;
            for (int i = 0; i < count; i++)
            {
                var s = paths[i];
                // 1차 필터: 세그먼트 AABB(+반경 여유) — 벗어나면 즉시 스킵
                if (x < s.MinX - radius || x > s.MaxX + radius
                    || z < s.MinZ - radius || z > s.MaxZ + radius)
                    continue;
                // 2차: 점-선분 최단거리 제곱 < 반경 제곱이면 길 위
                float vx = s.X1 - s.X0, vz = s.Z1 - s.Z0;
                float wx = x - s.X0, wz = z - s.Z0;
                float len2 = vx * vx + vz * vz;
                float t = len2 > 0f ? Mathf.Clamp01((wx * vx + wz * vz) / len2) : 0f;
                float dx = s.X0 + vx * t - x;
                float dz = s.Z0 + vz * t - z;
                if (dx * dx + dz * dz < r2) return true;
            }
            return false;
        }

        /// <summary>영지 성 앵커(반경 60m) + 마을(반경+10m) 제외 — VillagePlacementSystem 결정론 좌표.</summary>
        static bool IsNearCastleOrVillage(float x, float z, List<VillagePlacementSystem.VillageInfo> villages)
        {
            if (villages == null || villages.Count == 0) return false;
            for (int i = 0; i < villages.Count; i++)
            {
                var v = villages[i];
                float dcx = x - v.castleCenter.x;
                float dcz = z - v.castleCenter.z;
                if (dcx * dcx + dcz * dcz < CASTLE_EXCLUDE * CASTLE_EXCLUDE) return true;   // 성 앵커 60m
                float vr = v.radius + VILLAGE_EXTRA;
                float dvx = x - v.center.x;
                float dvz = z - v.center.z;
                if (dvx * dvx + dvz * dvz < vr * vr) return true;                           // 마을 +10m 여유
            }
            return false;
        }

        /// <summary>'NaturalResources' 컨테이너 자식(자원노드) 위치 1회 캐시 — 컨테이너 미존재 시 무시.</summary>
        static void CacheResourceNodes()
        {
            s_resourceXZ = null;
            var cont = GameObject.Find(RESOURCE_CONTAINER);
            if (cont == null) return;
            var t = cont.transform;
            var list = new List<Vector2>(t.childCount);
            for (int i = 0; i < t.childCount; i++)
            {
                var c = t.GetChild(i);
                list.Add(new Vector2(c.position.x, c.position.z));
            }
            if (list.Count > 0) s_resourceXZ = list;
        }

        /// <summary>캐시된 자원노드 위치 근처(8m) 판정 — 컨테이너 미존재 시 항상 false.</summary>
        static bool IsNearCachedResourceNodes(float x, float z)
        {
            if (s_resourceXZ == null) return false;
            for (int i = 0; i < s_resourceXZ.Count; i++)
            {
                float dx = x - s_resourceXZ[i].x;
                float dz = z - s_resourceXZ[i].y;
                if (dx * dx + dz * dz < RESOURCE_EXCLUDE * RESOURCE_EXCLUDE) return true;
            }
            return false;
        }

        /// <summary>렌더러 bounds 밑면을 지표에 정렬 + 기부 침하 (절벽이 지형과 결합, 공중 뜸 방지).</summary>
        static void AlignBottomToGround(GameObject go, float sink)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) return;
            Bounds bounds = CollectBounds(renderers);
            if (bounds.size == Vector3.zero) return;
            float shift = (go.transform.position.y - sink) - bounds.min.y;
            if (Mathf.Abs(shift) > 0.001f)
                go.transform.position += new Vector3(0f, shift, 0f);
        }

        /// <summary>
        /// 접지 계약 — 배치 모델에 콜라이더 필수 부착(캐릭터/몬스터 겉돎·통과 방지).
        /// 자식 MeshFilter 전수에 정적 MeshCollider(convex 아님 — 절벽 표면 보행 판정 정확) 부착,
        /// 메시 부재 시 루트 bounds BoxCollider 폴백(자연자원 AddBoundsBoxCollider 선례).
        /// </summary>
        static void AttachGroundColliders(GameObject go)
        {
            var filters = go.GetComponentsInChildren<MeshFilter>();
            bool anyMeshCollider = false;
            if (filters != null)
            {
                for (int i = 0; i < filters.Length; i++)
                {
                    var mf = filters[i];
                    if (mf == null || mf.sharedMesh == null) continue;
                    if (mf.GetComponent<MeshCollider>() != null) { anyMeshCollider = true; continue; }
                    var mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    mc.convex = false;   // 정적 스태틱 — 절벽 표면 걷기 판정 정확도 우선
                    anyMeshCollider = true;
                }
            }
            if (anyMeshCollider) return;

            // 폴백: 메시 없는 모델 — 렌더러 bounds를 루트 BoxCollider로 변환
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) return;
            Bounds b = CollectBounds(renderers);
            if (b.size == Vector3.zero) return;
            var col = go.AddComponent<BoxCollider>();
            col.center = go.transform.InverseTransformPoint(b.center);
            Vector3 localSize = go.transform.InverseTransformDirection(b.size);
            col.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
        }

        static Bounds CollectBounds(Renderer[] renderers)
        {
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var rb = r.bounds;
                if (rb.size == Vector3.zero) continue;
                if (first) { bounds = rb; first = false; }
                else bounds.Encapsulate(rb);
            }
            return bounds;
        }

        // (기존 대량 배치 경로 — [보존] 롤백용. 호출부는 주석 처리됨.)
        /// <summary>
        /// 특정 유형(나무/바위)을 attempts만큼 시도해 제외존·바이옴 수락 확률을 통과한 경우 배치.
        /// 제외존: 엠파이어(0,0,0 반경120m), 호수(radius*1.15), 스폰(728,-529 반경5m), 지도 경계(±950 샘플링).
        /// </summary>
        static int PlaceType(Transform parent, GameObject[] models, int attempts,
            System.Func<NationType, float> acceptance, float scaleMin, float scaleMax,
            bool boostScaleForColdVolcanic, System.Random rng)
        {
            int placed = 0;
            for (int i = 0; i < attempts; i++)
            {
                // ±950 경계 내 랜덤 위치
                float x = RandomRange(rng, -BOUND_MAX, BOUND_MAX);
                float z = RandomRange(rng, -BOUND_MAX, BOUND_MAX);

                // === 제외존 ===
                if (Mathf.Sqrt(x * x + z * z) < EMPIRE_EXCLUDE) continue;          // 엠파이어 중앙
                if (IsInLakeExclusion(x, z)) continue;                             // 호수 (해안 여백 포함)
                if (IsInSpawnExclusion(x, z)) continue;                            // 스폰지

                // === 바이옴 수락 ===
                NationType nation = NationTerrainController.GetNationFromPosition(new Vector3(x, 0f, z));
                if (nation == NationType.None || nation == NationType.Dracula) continue;
                if (RandomRange(rng, 0f, 1f) >= acceptance(nation)) continue;

                // === y = 기저 1f + 지형 높이 (Mesh 로컬) ===
                float y = GROUND_BASE + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42);

                var model = models[rng.Next(models.Length)];
                var go = Object.Instantiate(model, parent);
                go.layer = 0; // Default
                go.transform.position = new Vector3(x, y, z);
                go.transform.rotation = Quaternion.Euler(0f, RandomRange(rng, 0f, 360f), 0f);

                // 스케일 — North/West 큰 바위 강조(설산 침엽/화산 느낌)
                float sMin = scaleMin;
                float sMax = scaleMax;
                if (boostScaleForColdVolcanic && (nation == NationType.North || nation == NationType.West))
                {
                    sMin += 0.4f;
                    sMax += 0.3f;
                }
                go.transform.localScale = Vector3.one * RandomRange(rng, sMin, sMax);

                placed++;
            }
            return placed;
        }

        static bool IsInLakeExclusion(float x, float z)
        {
            var lakes = TerrainGenerator.Lakes;
            for (int i = 0; i < lakes.Count; i++)
            {
                var lake = lakes[i];
                float dx = x - lake.center.x;
                float dz = z - lake.center.z;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);
                if (dist < lake.radius * LAKE_MARGIN_FACTOR)
                    return true;
            }
            return false;
        }

        static bool IsInSpawnExclusion(float x, float z)
        {
            float dx = x - SPAWN_X;
            float dz = z - SPAWN_Z;
            return (dx * dx + dz * dz) < (SPAWN_EXCLUDE * SPAWN_EXCLUDE);
        }

        static GameObject FindChild(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c != null && c.gameObject.name == name) return c.gameObject;
            }
            return null;
        }

        static float RandomRange(System.Random rng, float min, float max)
        {
            return (float)(rng.NextDouble() * (max - min) + min);
        }

        static void EnableGPUInstancing(GameObject parent)
        {
            var renderers = parent.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (r.sharedMaterial != null)
                {
                    r.sharedMaterial.enableInstancing = true;
                }
            }
        }

        // ================================================================
        // Editor: 수동 실행 메뉴
        // ================================================================
#if UNITY_EDITOR
        [UnityEditor.MenuItem("Tools/Poison/Place Environment Models")]
        public static void PlaceEnvironmentModelsEditor()
        {
            var ground = GameObject.Find("Ground_Inner");
            if (ground != null)
            {
                PlaceAllIfNeeded(ground.transform);
                UnityEditor.EditorUtility.SetDirty(ground);
            }
        }
#endif
    }
}