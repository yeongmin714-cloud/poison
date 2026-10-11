using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// 자연 자원 노드 스포너 (09-23 신규) — 오픈월드 지형에 채집/채굴 가능한 자원 노드를 결정론적으로 배치.
    ///
    /// 3종 카테고리:
    ///   · 돌(Rock)      → ResourceNode(ResourceType.Stone) — 광질(Mine) 대상, GuardTaskSystem.Mine 연동
    ///   · 과일나무(Fruit)→ HerbPickup(치유초) — 채집(Gather) 분류(HoverTargetClassifier.Gather).
    ///     프로젝트에 과일 아이템이 아직 없어 HerbPickup 재사용(테라리움 선례: SetupHerb) —
    ///     E키/수확 경로(LootBasket + EXP)를 그대로 타고, 아이템 지정은 PlayerInventory에 과일
    ///     ItemData가 추가되면 교체한다.
    ///   · 허브(Herb)    → HerbPickup(HerbType 5종 순환) — 약초 채집(Gather) 대상.
    ///
    /// 모델: Assets/새로운 glb/nature/ GLB(에디터 AssetDatabase 로드) → 실패 시 Resources/IdyllicPrefabs
    /// 합성 프리팹 폴백 → 최종 폴백 프리미티브(SetupMiningNode 선례). 노드 루트 GO에 시각 모델을
    /// 자식 Instantiate하고 렌더러 bounds로 밑면 지면 정렬 + BoxCollider(호버/광질 OverlapSphere 판정용).
    ///
    /// 배치 룰(결정론): 자체 xorshift PRNG(고정 시드, System.Random/UnityEngine.Random 미사용 —
    /// TerrainModelPlacer/IdyllicDecoPlacer의 "고정 시드 결정론" 계약 강화판). 카테고리별 목표 수
    /// (돌 40/과일 30/허브 50)에 도달할 때까지 시도하며, 지도 경계 ±1200m, 엠파이어 중앙 120m,
    /// 스폰지 25m, 호수(반경×1.15) 제외 + 바이옴(방위)별 수락 확률 + 최소 간격(공간 해시) 적용.
    ///
    /// 지표면 계약: 월드 y = 1f + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42)
    /// (RuntimeTerrainChunkManager/IdyllicDecoPlacer와 동일 수식).
    ///
    /// 배선: GameSetup.BootstrapTerrainDeco() 끝에서 EnsureNaturalResources(decoGO.transform) 멱등 호출.
    /// 메인 씬 전용 — TerrainOnly 씬(씬 이름 판정) 또는 UITransitionState.DisableAllUi true면 스폰 생략.
    /// </summary>
    public static class NaturalResourceSpawner
    {
        // === 배치 상수 ===
        const string CONTAINER_NAME = "NaturalResources";
        const string MARKER_NAME = "NaturalResources_Marker";
        const ulong SPAWN_SEED = 20260923;         // 고정 시드 — 동일 실행마다 동일 배치(결정론)
        const float BOUND_MAX = 1200f;             // 지도 경계 (±1200m 밖 제외)
        const float EMPIRE_EXCLUDE = 120f;         // 엠파이어 중앙(0,0,0) 제외 반경 (TerrainModelPlacer 동일)
        const float SPAWN_X = 728f;
        const float SPAWN_Z = -529f;
        const float SPAWN_EXCLUDE = 25f;           // 스폰지 제외 반경 (스폰지 프롭/건물과 겹침 방지)
        const float LAKE_MARGIN_FACTOR = 1.15f;    // 호수 해안 여백 (radius*이값 밖)
        const float GROUND_BASE = 1f;              // Ground_Inner 월드 y 기저 (높이 계약)
        const int HEIGHT_SEED = 42;                // 지형 높이 계약 시드 (Plains, 42)

        // 카테고리별 목표 수/시도 수/최소 간격 — 구조 조정: 돌을 나무 수준(200)으로 상향, 전 국가 균등
        const int ROCK_TARGET = 200, FRUIT_TARGET = 30, HERB_TARGET = 50;
        const int ROCK_ATTEMPTS = 3500, FRUIT_ATTEMPTS = 900, HERB_ATTEMPTS = 700;
        const float ROCK_MIN_DIST = 18f;           // 바위 군집 간격
        const float FRUIT_MIN_DIST = 24f;          // 수관 간섭 방지 간격
        const float HERB_MIN_DIST = 7f;            // 허브 밀집 허용(자연 군락)

        const int CROP_TARGET = 40;                // crops(과일·작물) 목표 수 — 농경지 과밀 방지
        const int CROP_ATTEMPTS = 700;
        const float CROP_MIN_DIST = 10f;           // 농작물 간격

        // 벌목 나무 (사용자 확정 200그루) — 국가당 ~50, 전 국가 균등 배치
        const int WOOD_TARGET = 200;
        const int WOOD_ATTEMPTS = 3500;
        const float WOOD_MIN_DIST = 14f;           // 나무 간 최소 간격 (수관 겹침 방지)

        // idyllic 조형 나무 기준 높이(m) — 새 GLB(작음)를 이 크기로 정규화 (스킬 GNB Trees 실측)
        const float TREES_TARGET_HEIGHT = 8.6f;

        const string GLB_DIR = "Assets/새로운 glb/nature/";
        const string GLB_DIR2 = "Assets/새로운 glb/crops-fish/";   // crops(과일·작물) GLB 폴더

        // GLB 에셋 경로(에디터 로드 우선) — 실존 확인된 신규 nature GLB만 사용
        static readonly string[] RockGlbPaths =
        {
            GLB_DIR + "angular-common-nature-rock-cluster-single-boulder.glb",
            GLB_DIR + "angular-common-nature-rock-cluster-split-boulder.glb",
            GLB_DIR + "angular-common-nature-rock-cluster-three-rocks.glb",
        };
        static readonly string[] FruitGlbPaths =
        {
            GLB_DIR + "angular-common-nature-fruit-tree-mature-fruiting.glb",
            GLB_DIR + "angular-common-nature-small-fruit-tree-streamline-normal.glb",
        };
        static readonly string[] HerbGlbPaths =
        {
            GLB_DIR + "angular-common-nature-broad-fern-clump-streamline-normal.glb",
            GLB_DIR + "angular-common-nature-bluebell-flower-cluster-streamline-normal.glb",
        };

        // 벌목 나무(일반 나무) — 파일명으로 과일나무(*fruit-tree*)와 구분. birch/broadleaf/pine 등.
        // 크기는 NormalizeTreeSize(TREES_TARGET_HEIGHT)로 idyllic과 정합.
        static readonly string[] WoodTreeGlbPaths =
        {
            GLB_DIR + "angular-common-nature-birch-tree-mature.glb",
            GLB_DIR + "angular-common-nature-broadleaf-tree-mature.glb",
            GLB_DIR + "angular-common-nature-pine-tree-mature.glb",
            GLB_DIR + "angular-common-nature-birch-tree-young.glb",
            GLB_DIR + "angular-common-nature-broadleaf-tree-young.glb",
        };

        /// <summary>
        /// crops(과일·작물) GLB 경로 — 이름 개편된 crop-*.glb 100종 전체.
        /// CropCatalog.Keys 순서(결정론 고정) 기준으로 런타임에 조립 — 죽은 경로 리터럴 제거.
        /// </summary>
        static string[] CropAllGlbPaths()
        {
            var paths = new string[CropCatalog.TotalCount];
            for (int i = 0; i < CropCatalog.TotalCount; i++)
                paths[i] = CropCatalog.GlbPath(i);
            return paths;
        }

        /// <summary>
        /// 진입점(멱등). parent 하위에 'NaturalResources' 컨테이너가 있으면 스킵(중복 실행 가드).
        /// TerrainOnly 씬 / DisableAllUi 상태에서는 스폰 생략(메인 씬 전용 기능).
        /// </summary>
        public static void EnsureNaturalResources(Transform parent)
        {
            if (parent == null) return;

            // [GNB] TerrainOnly(지형 관찰) 씬 — 자원 노드 스폰 생략 (게임플레이 요소 배제)
            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid() && !string.IsNullOrEmpty(scene.name) && scene.name.Contains("TerrainOnly"))
            {
                Debug.Log("[NaturalResourceSpawner] TerrainOnly 씬 — 자원 노드 스폰 생략.");
                return;
            }
            // [GNB] 지형 관찰 게이트 — RunTerrainOnlySetup이 세팅하는 UI 전수 비활성 상태면 생략
            if (ProjectName.Core.UITransitionState.DisableAllUi)
            {
                Debug.Log("[NaturalResourceSpawner] DisableAllUi=true — 자원 노드 스폰 생략.");
                return;
            }

            // 중복 실행 가드 (컨테이너 직속 자식 마커)
            if (FindDirectChild(parent, CONTAINER_NAME) != null)
            {
                Debug.Log("[NaturalResourceSpawner] Already placed — skipping.");
                return;
            }

            var container = new GameObject(CONTAINER_NAME);
            container.transform.SetParent(parent, false);
            container.layer = 0;

            var rng = new DetRng(SPAWN_SEED);
            var hash = new SpatialHash(8f);   // 최소 간격 검사용 공간 해시 (셀 8m)

            int rocks = PlaceCategory(container.transform, rng, hash,
                LoadVisualPrefabs(RockGlbPaths, "IdyllicPrefabs/Rocks", "Rock_"),
                ROCK_TARGET, ROCK_ATTEMPTS, ROCK_MIN_DIST, RockAcceptance,
                0.8f, 1.6f, "NaturalResource_Rock", CreateRockNode);

            int fruits = PlaceCategory(container.transform, rng, hash,
                LoadVisualPrefabs(FruitGlbPaths, "IdyllicPrefabs/Trees", "BlossomTree_"),
                FRUIT_TARGET, FRUIT_ATTEMPTS, FRUIT_MIN_DIST, FruitTreeAcceptance,
                0.9f, 1.3f, "NaturalResource_FruitTree", CreateFruitNode);

            // ── 벌목 나무(Wood) 200그루 — 일반 나무 GLB(birch/broadleaf/pine)를 전 국가 균등 배치 ──
            int woods = PlaceCategory(container.transform, rng, hash,
                LoadVisualPrefabs(WoodTreeGlbPaths, "IdyllicPrefabs/Trees", "WillowTree_"),
                WOOD_TARGET, WOOD_ATTEMPTS, WOOD_MIN_DIST, WoodAcceptance,
                0.9f, 1.15f, "NaturalResource_Wood", CreateWoodNode);

            int herbs = PlaceCategory(container.transform, rng, hash,
                LoadVisualPrefabs(HerbGlbPaths, "IdyllicPrefabs/Flowers", "Flower_"),
                HERB_TARGET, HERB_ATTEMPTS, HERB_MIN_DIST, HerbAcceptance,
                0.8f, 1.2f, "NaturalResource_Herb", CreateHerbNode);

            int crops = PlaceCategory(container.transform, rng, hash,
                LoadVisualPrefabs(CropAllGlbPaths(), "IdyllicPrefabs/Flowers", "Crop_"),
                CROP_TARGET, CROP_ATTEMPTS, CROP_MIN_DIST, CropAcceptance,
                0.7f, 1.4f, "NaturalResource_Crop", CreateCropNode);

            // 배치 마커 (중복 실행 방지)
            var marker = new GameObject(MARKER_NAME);
            marker.transform.SetParent(container.transform, false);
            marker.SetActive(false);

            Debug.Log($"[NaturalResourceSpawner] ✅ 자원 노드 배치 완료 — 돌(Stone) {rocks}/{ROCK_TARGET}, 과일나무(Gather) {fruits}/{FRUIT_TARGET}, 허브(Gather) {herbs}/{HERB_TARGET}, crops(Gather) {crops}/{CROP_TARGET}");
            Debug.Log($"[NaturalResourceSpawner] 🌳 벌목 나무(Wood) {woods}/{WOOD_TARGET} 그루 배치 — 전 국가 균등, 크기 idyllic({TREES_TARGET_HEIGHT}m) 정합.");
        }

        // ================================================================
        // 카테고리 배치 룰 — 바이옴(방위)별 수락 확률
        // ================================================================
        // 돌: 전 국가 균등(동/서/남/북) — 사용자 요구 "지역마다 부족하지 않게" (나무와 동일 원칙)
                static float RockAcceptance(NationType n)
                {
                    switch (n)
                    {
                        case NationType.East:
                        case NationType.North:
                        case NationType.West:
                        case NationType.South:
                            return 1.0f;
                        default:
                            return 0f;
                    }
                }

        // 과일나무: 동(초원) 중심, 북 소량, 서/남 거의 없음
        static float FruitTreeAcceptance(NationType n)
        {
            switch (n)
            {
                case NationType.East:  return 1.0f;
                case NationType.North: return 0.15f;
                case NationType.West:  return 0.05f;
                case NationType.South: return 0.05f;
                default:               return 0f;
            }
        }

        // crops(과일·작물): 동/남 농경지 중심, 북/서 극소량
        static float CropAcceptance(NationType n)
        {
            switch (n)
            {
                case NationType.East:  return 1.0f;
                case NationType.South: return 0.8f;
                case NationType.North: return 0.2f;
                case NationType.West:  return 0.15f;
                default:               return 0f;
            }
        }

        // 벌목 나무: 전 국가 균등(동/서/남/북 모두) — 사용자 요구 "지역마다 부족하지 않게".
        static float WoodAcceptance(NationType n)
        {
            switch (n)
            {
                case NationType.East:
                case NationType.West:
                case NationType.South:
                case NationType.North:
                    return 1.0f;
                default:
                    return 0f;
            }
        }

        /// <summary>
        /// crops 노드: HerbPickup 부착(Gather 분류) — CropCatalog 100종 결정론 순환.
        /// index % CropCatalog.TotalCount 로 _cropKey 지정 → 수확/씨앗 모두 CropCatalog 아이템.
        /// FarmPlot(영지 농경) HerbType 경로는 그대로 유지(기존 5종 작물 호환).
        /// </summary>
        static System.Action<GameObject> CreateCropNode(int index, DetRng rng, string visualName)
        {
            string cropKey = CropCatalog.Keys[index % CropCatalog.TotalCount];
            return go =>
            {
                var herb = go.AddComponent<HerbPickup>();
                SetPrivateField(herb, "_cropKey", cropKey);
            };
        }

        // 허브: 동(초원) 군락 중심, 북 숲 가장자리 소량, 서/남 드묾
        static float HerbAcceptance(NationType n)
        {
            switch (n)
            {
                case NationType.East:  return 1.0f;
                case NationType.North: return 0.3f;
                case NationType.West:  return 0.1f;
                case NationType.South: return 0.1f;
                default:               return 0f;
            }
        }

        // ================================================================
        // 배치 코어
        // ================================================================
        // [2026-10-11 28차] visualName 전달 — 벌목 나무의 종별 그루터기(birch/broadleaf/pine) 판정 소스.
        delegate System.Action<GameObject> NodeConfigurer(int index, DetRng rng, string visualName);

        static int PlaceCategory(Transform parent, DetRng rng, SpatialHash hash,
            GameObject[] visualPrefabs, int target, int attempts, float minDist,
            System.Func<NationType, float> acceptance, float scaleMin, float scaleMax,
            string goNamePrefix, NodeConfigurer configure)
        {
            if (visualPrefabs == null || visualPrefabs.Length == 0 || target <= 0)
            {
                Debug.LogWarning($"[NaturalResourceSpawner] ⚠️ {goNamePrefix} 시각 프리팹 부재/목표 0 — 배치 생략");
                return 0;
            }

            int placed = 0;
            for (int attempt = 0; attempt < attempts && placed < target; attempt++)
            {
                float x = rng.Range(-BOUND_MAX, BOUND_MAX);
                float z = rng.Range(-BOUND_MAX, BOUND_MAX);

                // === 제외존 ===
                if (Mathf.Sqrt(x * x + z * z) < EMPIRE_EXCLUDE) continue;   // 엠파이어 중앙
                if (IsInLakeExclusion(x, z)) continue;                      // 호수 (해안 여백 포함)
                if (IsInSpawnExclusion(x, z)) continue;                     // 스폰지

                // === 바이옴 수락 ===
                NationType nation = NationTerrainController.GetNationFromPosition(new Vector3(x, 0f, z));
                if (nation == NationType.None || nation == NationType.Dracula || nation == NationType.Empire) continue;
                if (rng.NextDouble() >= acceptance(nation)) continue;

                // === 오버랩 방지 (최소 간격) ===
                if (!hash.IsFree(new Vector2(x, z), minDist)) continue;

                // === 노드 생성 ===
                var prefab = visualPrefabs[placed % visualPrefabs.Length];   // 결정론 순환 선택
                float scale = rng.Range(scaleMin, scaleMax);
                float yaw = rng.Range(0f, 360f);
                var go = SpawnNode(parent, $"{goNamePrefix}_{placed + 1:D2}", prefab, x, z, scale, yaw);

                var setup = configure(placed, rng, prefab != null ? prefab.name : null);
                if (setup != null) setup(go);

                hash.Insert(new Vector2(x, z));
                placed++;
            }
            return placed;
        }

        // ================================================================
        // 노드 오브젝트 구성
        // ================================================================
        /// <summary>지표면 계약(1 + GetHeightAt)에 노드 루트를 세우고 GLB 시각 모델을 밑면 정렬 배치.</summary>
        static GameObject SpawnNode(Transform parent, string goName, GameObject visualPrefab,
            float x, float z, float scale, float yawDeg)
        {
            float y = GROUND_BASE + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, HEIGHT_SEED);
            return SpawnNodeAt(parent, goName, visualPrefab, x, y, z, scale, yawDeg);
        }

        /// <summary>노드 루트 생성 본체 — 높이를 명시 파라미터로 받는다(테스트 씬 SurfaceY 위임용, 2026-10-11).</summary>
        static GameObject SpawnNodeAt(Transform parent, string goName, GameObject visualPrefab,
            float x, float y, float z, float scale, float yawDeg)
        {
            var go = new GameObject(goName);
            go.layer = 0;   // Default — 몬스터 스폰 raycast(Ground|Terrain 마스크) 무시 대상 아님(인터랙티브 노드)
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, y, z);
            go.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);

            if (visualPrefab != null)
            {
                var visual = Object.Instantiate(visualPrefab, go.transform);
                visual.name = "Visual";
                visual.layer = 0;
                visual.transform.localScale = Vector3.one * scale;
                AlignBottomToGround(go.transform, visual);   // 밑면 == 지표면 (지형 안 박히게)
                AddBoundsBoxCollider(go, visual);            // 호버/광질(OverlapSphere) 판정용 콜라이더
            }
            else
            {
                // 폴백: 프리미티브 Cube(Renderer+BoxCollider 동봉) — SetupMiningNode 선례
                var prim = GameObject.CreatePrimitive(PrimitiveType.Cube);
                prim.name = "Visual_Fallback";
                prim.layer = 0;
                prim.transform.SetParent(go.transform, false);
                prim.transform.localPosition = new Vector3(0f, 0.3f, 0f);
                prim.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
                var r = prim.GetComponent<Renderer>();
                if (r != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.color = new Color(0.55f, 0.55f, 0.58f, 1f);
                    r.material = mat;
                }
            }
            return go;
        }

        /// <summary>시각 모델 렌더러 bounds 밑면을 노드 루트(지표면) 높이로 정렬 — 지형에 안 박히게.</summary>
        static void AlignBottomToGround(Transform node, GameObject visual)
        {
            var renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) return;

            Bounds bounds = CollectBounds(renderers);
            if (bounds.size == Vector3.zero) return;

            float dy = node.position.y - bounds.min.y;
            if (Mathf.Abs(dy) > 0.001f)
                visual.transform.position += new Vector3(0f, dy, 0f);
        }

        /// <summary>시각 모델 전체 bounds를 노드 루트 BoxCollider로 변환(호버/광질 판정).</summary>
        static void AddBoundsBoxCollider(GameObject node, GameObject visual)
        {
            var renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) return;

            Bounds bounds = CollectBounds(renderers);
            if (bounds.size == Vector3.zero) return;

            var col = node.AddComponent<BoxCollider>();
            col.center = node.transform.InverseTransformPoint(bounds.center);
            Vector3 localSize = node.transform.InverseTransformDirection(bounds.size);
            col.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));

            // 초소형 모델 대비 최소 판정 폭 보장 (호버/광질 탐색 안정화)
            col.size = new Vector3(
                Mathf.Max(col.size.x, 0.8f),
                Mathf.Max(col.size.y, 0.5f),
                Mathf.Max(col.size.z, 0.8f));
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

        // --- 노드 컴포넌트 구성 (private SerializeField는 리플렉션 — 프로젝트 관례) ---

        /// <summary>돌 노드: ResourceNode(Stone) — 광질(Mine) 대상, 호버 Mine 커서.</summary>
        static System.Action<GameObject> CreateRockNode(int index, DetRng rng, string visualName)
        {
            return go =>
            {
                var node = go.AddComponent<ResourceNode>();
                node.ConfigureResourceType(ResourceNode.ResourceType.Stone);   // [34차] Awake 프리징 없이 타입+파생상태 동시 설정
            };
        }

        /// <summary>과일나무 노드: HerbPickup(Fruit_Apple) — 실제 과일(사과) 수확.
        /// 이전 placeholder(치유초/Red) → 실제 과일 아이템 교체. 크기는 idyllic 나무와 정합.</summary>
        static System.Action<GameObject> CreateFruitNode(int index, DetRng rng, string visualName)
        {
            return go =>
            {
                var herb = go.AddComponent<HerbPickup>();
                SetPrivateField(herb, "_herbType", HerbPickup.HerbType.Fruit_Apple);
                NormalizeTreeSize(go, TREES_TARGET_HEIGHT);
            };
        }

        /// <summary>벌목 나무 노드: ResourceNode(Wood) → E 채널(파랑바)로 통나무(mat_wood) 수확. 크기 idyllic 정합.
        /// [2026-10-11 28차] 시각 GLB 파일명에서 종별(birch/broadleaf/pine) 추출 → 벌목 후 그루터기(cut-stump) 전달.</summary>
        static System.Action<GameObject> CreateWoodNode(int index, DetRng rng, string visualName)
        {
            return go =>
            {
                var node = go.AddComponent<ResourceNode>();
                node.ConfigureResourceType(ResourceNode.ResourceType.Wood);   // [34차]
                NormalizeTreeSize(go, TREES_TARGET_HEIGHT);
                SetPrivateField(node, "_stumpType", StumpTypeFromName(visualName));
            };
        }

        /// <summary>기존 구성 후 그루터기 종류만 보강(테스트 씬 경로, 28차).</summary>
        public static void ApplyStumpType(GameObject go, string visualSourceName)
        {
            if (go == null) return;
            var node = go.GetComponent<ResourceNode>();
            if (node == null) return;
            SetPrivateField(node, "_stumpType", StumpTypeFromName(visualSourceName));
        }

        /// <summary>시각 GLB 파일명 → 그루터기 종류. 미상(null)이면 그루터기 없이 숨김만.</summary>
        internal static string StumpTypeFromName(string n)
        {
            if (string.IsNullOrEmpty(n)) return null;
            if (n.Contains("birch")) return "birch";
            if (n.Contains("broadleaf")) return "broadleaf";
            if (n.Contains("pine")) return "pine";
            return null;
        }

        /// <summary>
        /// 시각 모델(Visual 자식) 전체 높이를 대상 높이로 정규화 — 새 GLB(작음)를 idyllic 나무(8.6m)와 정합.
        /// 월드 런타임 렌더러 bounds 기준 (batchmode 아님). 정규화 후 밑면 재정렬.
        /// </summary>
        static void NormalizeTreeSize(GameObject node, float targetHeight)
        {
            if (node == null) return;
            var visual = FindDirectChild(node.transform, "Visual");
            if (visual == null) return;
            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return;
            Bounds b = CollectBounds(renderers);
            if (b.size.y < 0.01f) return;   // bounds 미계산(드물게) — 스킵
            float s = targetHeight / b.size.y;
            visual.transform.localScale *= s;
            AlignBottomToGround(node.transform, visual);
        }

        /// <summary>허브 노드: HerbPickup + 5종 HerbType 결정론 순환(Red/Purple/Yellow/Silver/Green).</summary>
        static System.Action<GameObject> CreateHerbNode(int index, DetRng rng, string visualName)
        {
            IReadOnlyList<HerbInfo> herbs = HerbDatabase.AllHerbs;
            if (herbs == null || herbs.Count == 0) return null;
            string herbId = herbs[index % herbs.Count].id;
            return go =>
            {
                var herb = go.AddComponent<HerbPickup>();
                SetPrivateField(herb, "_herbDatabaseId", herbId);
            };
        }

        // ================================================================
        // 테스트 씬 배치 공개 API (Test_15_Livelihood — 2026-10-11)
        // ================================================================
        // 메인씬 ±1200m 산포 파이프라인은 국가 판정·제외존 전제라 테스트 미니지형(±60m)에 안 맞는다.
        // 로더/스폰 코어/노드 구성기만 공개 위임해, 테스트 씬에서도 메인씬과 동일 코드 경로
        // (ResourceNode/HerbPickup + E 채널 파랑 진행바 + 나무 스웨이 + 도끼 커서 + 소멸→리스폰)을 검증한다.

        /// <summary>벌목 나무(일반 나무 GLB) 시각 프리팹 로드 — 실패 시 Resources 폴백(결정론 정렬 동일).</summary>
        public static GameObject[] LoadWoodTreeVisuals() => LoadVisualPrefabs(WoodTreeGlbPaths, "IdyllicPrefabs/Trees", "WillowTree_");

        /// <summary>돌 GLB 시각 프리팹 로드.</summary>
        public static GameObject[] LoadRockVisuals() => LoadVisualPrefabs(RockGlbPaths, "IdyllicPrefabs/Rocks", "Rock_");

        /// <summary>과일나무 GLB 시각 프리팹 로드.</summary>
        public static GameObject[] LoadFruitTreeVisuals() => LoadVisualPrefabs(FruitGlbPaths, "IdyllicPrefabs/Trees", "BlossomTree_");

        /// <summary>테스트 씬용 노드 스폰 — 높이만 외부(SurfaceY 계약)에서 받는 점이 메인 SpawnNode와 다르다.</summary>
        public static GameObject SpawnTestNode(Transform parent, string goName, GameObject visualPrefab,
            float x, float y, float z, float scale, float yawDeg, System.Action<GameObject> configure)
        {
            var go = SpawnNodeAt(parent, goName, visualPrefab, x, y, z, scale, yawDeg);
            configure?.Invoke(go);
            return go;
        }

        /// <summary>벌목 나무 노드 구성(공개 위임) — ResourceNode(Wood) + idyllic 8.6m 정규화.
        /// [2026-10-11 28차] 시각 소스명에서 종별 그루터기 전달 오버로드.</summary>
        public static void ConfigureWoodNode(GameObject go, string visualSourceName)
        {
            var node = go.AddComponent<ResourceNode>();
            node.ConfigureResourceType(ResourceNode.ResourceType.Wood);   // [34차]
            NormalizeTreeSize(go, TREES_TARGET_HEIGHT);
            SetPrivateField(node, "_stumpType", StumpTypeFromName(visualSourceName));
        }

        /// <summary>벌목 나무 노드 구성(공개 위임) — ResourceNode(Wood) + idyllic 8.6m 정규화.</summary>
        public static void ConfigureWoodNode(GameObject go)
        {
            var node = go.AddComponent<ResourceNode>();
            node.ConfigureResourceType(ResourceNode.ResourceType.Wood);   // [34차]
            NormalizeTreeSize(go, TREES_TARGET_HEIGHT);
        }

        /// <summary>돌 노드 구성(공개 위임) — ResourceNode(Stone) — 광질(E 채널) 대상.</summary>
        public static void ConfigureRockNode(GameObject go)
        {
            var node = go.AddComponent<ResourceNode>();
            node.ConfigureResourceType(ResourceNode.ResourceType.Stone);   // [34차] Awake 프리징 없이 타입+파생상태 동시 설정
        }

        /// <summary>과일나무 노드 구성(공개 위임) — HerbPickup(Fruit_Apple) + idyllic 정규화.</summary>
        public static void ConfigureFruitNode(GameObject go)
        {
            var herb = go.AddComponent<HerbPickup>();
            SetPrivateField(herb, "_herbType", HerbPickup.HerbType.Fruit_Apple);
            NormalizeTreeSize(go, TREES_TARGET_HEIGHT);
        }

        /// <summary>private SerializeField 리플렉션 설정 — TestTerritoryCombatSetup.SetupMiningNode 선례.</summary>
        static void SetPrivateField(Component target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(target, value);
        }

        // ================================================================
        // 시각 모델 로드 — 에디터 GLB 직접 로드 → Resources 합성 프리팹 폴백
        // ================================================================
        /// <summary>
        /// GLB 에셋 우선 로드(에디터 AssetDatabase), 실패 시 Resources/IdyllicPrefabs 합성 프리팹에서
        /// 이름 접두어로 필터링해 결정론 정렬 반환. 플레이어 빌드에선 Resources 경로가 실질 로드 경로.
        /// </summary>
        static GameObject[] LoadVisualPrefabs(string[] glbPaths, string resourcesFolder, string fallbackNamePrefix)
        {
            var list = new List<GameObject>();

#if UNITY_EDITOR
            foreach (var path in glbPaths)
            {
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null) list.Add(prefab);
            }
            if (list.Count > 0) return list.ToArray();
            list.Clear();   // GLB 미발견 → 합성 프리팹 폴백로 진행
#endif
            // 폴백: GnbIdyllicSynth가 nature GLB에서 합성한 Resources 프리팹 풀
            var all = Resources.LoadAll<GameObject>(resourcesFolder);
            if (all != null)
            {
                foreach (var g in all)
                {
                    if (g == null) continue;
                    if (!string.IsNullOrEmpty(fallbackNamePrefix) && !g.name.StartsWith(fallbackNamePrefix)) continue;
                    list.Add(g);
                }
                // 이름순 정렬 — Resources.LoadAll 순서 비보장 → 결정론 보장
                list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            }
            return list.ToArray();
        }

        // ================================================================
        // 제외존 / 해시 / 결정론 PRNG
        // ================================================================
        static bool IsInLakeExclusion(float x, float z)
        {
            var lakes = TerrainGenerator.Lakes;
            if (lakes == null) return false;
            foreach (var lake in lakes)
            {
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

        static GameObject FindDirectChild(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c != null && c.gameObject.name == name) return c.gameObject;
            }
            return null;
        }

        /// <summary>결정론 최소 간격 검사용 균일 격자 공간 해시 (IdyllicDecoPlacer.SpatialHash 선례).</summary>
        class SpatialHash
        {
            readonly float _cell;
            readonly Dictionary<long, List<Vector2>> _map = new Dictionary<long, List<Vector2>>();

            public SpatialHash(float cell) { _cell = Mathf.Max(0.01f, cell); }

            static long Key(int cx, int cz) { return ((long)cx << 32) ^ (uint)cz; }

            public bool IsFree(Vector2 p, float minDist)
            {
                int cx = Mathf.FloorToInt(p.x / _cell);
                int cz = Mathf.FloorToInt(p.y / _cell);
                int r = Mathf.CeilToInt(minDist / _cell);
                float minSq = minDist * minDist;
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dz = -r; dz <= r; dz++)
                    {
                        List<Vector2> bucket;
                        if (!_map.TryGetValue(Key(cx + dx, cz + dz), out bucket)) continue;
                        foreach (var q in bucket)
                        {
                            float ddx = q.x - p.x;
                            float ddz = q.y - p.y;
                            if (ddx * ddx + ddz * ddz < minSq) return false;
                        }
                    }
                }
                return true;
            }

            public void Insert(Vector2 p)
            {
                int cx = Mathf.FloorToInt(p.x / _cell);
                int cz = Mathf.FloorToInt(p.y / _cell);
                long k = Key(cx, cz);
                List<Vector2> bucket;
                if (!_map.TryGetValue(k, out bucket))
                {
                    bucket = new List<Vector2>();
                    _map[k] = bucket;
                }
                bucket.Add(p);
            }
        }

        /// <summary>
        /// 고정 시드 결정론 PRNG — xorshift64* (System.Random/UnityEngine.Random 미사용).
        /// 동일 시드 → 동일 수열: 실행마다 동일한 자원 노드 배치를 보장한다.
        /// </summary>
        struct DetRng
        {
            ulong _state;

            public DetRng(ulong seed)
            {
                _state = seed != 0 ? seed : 0x9E3779B97F4A7C15UL;
                // 시드 예열 — 첫 출력 품질 안정화
                for (int i = 0; i < 4; i++) NextUInt64();
            }

            ulong NextUInt64()
            {
                _state ^= _state << 13;
                _state ^= _state >> 7;
                _state ^= _state << 17;
                return _state;
            }

            /// <summary>[0, 1) 균일 난수</summary>
            public double NextDouble()
            {
                return (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);   // 2^53
            }

            /// <summary>[min, max) 균일 난수</summary>
            public float Range(float min, float max)
            {
                if (max <= min) return min;
                return (float)(min + NextDouble() * (max - min));
            }
        }
    }
}