using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 실내 가구 GLB 카탈로그 (2026-09-24).
    ///
    /// Assets/새로운 glb/furniture/ 의 가구 GLB를 유형(table/chair/shelf/counter/bed/crate/mirror)별
    /// 풀로 카탈로그화. VillageBuildingCatalog와 동일 정신: 에디터 AssetDatabase 직접 로드(#if UNITY_EDITOR)
    /// + 스케일 정규화(렌더러 bounds 높이 → 목표높이) + 접지(실내 floorY=0, 밑면 y=0).
    ///
    /// 기존 프리미티브 IndoorFurniturePlacer.CreateXxx 와 시그니처 1:1 호환 — 빌더 코드의
    /// SetParent/localPosition/name/rotation 호출은 그대로 두고 프리미티브 생성만 교체.
    /// GLB 로드 실패 시 프리미티브 폴백 → 어떤 경우에도 null 반환 금지(호출부가 null 체크 없이 SetParent).
    /// Collider 등 부가 컴포넌트는 붙이지 않는다(순수 시각). 단, Bed는 세이브 상호작용 유지를 위해
    /// BoxCollider(isTrigger) + Bed 컴포넌트를 root에 부착(프리미티브 CreateBed 로직 모방).
    /// </summary>
    public static class IndoorFurnitureCatalog
    {
        private const string FurnitureDir = "Assets/새로운 glb/furniture/";

        // 유형별 목표 높이(m) — GLB 정규화 기준 (가구답게).
        private const float TableTargetHeight = 0.9f;
        private const float ChairTargetHeight = 0.5f;
        private const float ShelfTargetHeight = 1.8f;
        private const float CounterTargetHeight = 1.0f;
        private const float BedTargetHeight = 0.5f;
        private const float CrateTargetHeight = 0.7f;
        private const float MirrorTargetHeight = 1.6f;

        private static readonly string[] TablePool =
        {
            "angular-common-furniture-round-cafe-table-cottage-normal.glb",
            "angular-common-furniture-square-card-table-streamline-normal.glb",
            "angular-common-furniture-triangular-corner-table-cottage-normal.glb",
            "angular-common-furniture-wall-mounted-drop-leaf-table-heritage-normal.glb",
            "angular-common-furniture-wall-mounted-drop-leaf-table-heritage-destroyed.glb",
            "자기 소속 영주 사각테이블.glb", "자기 소속 영주 테이블.glb", "타 영주실 테이블.glb",
        };
        private static readonly string[] ChairPool =
        {
            "자기 소속 영주 의자.glb",
            "angular-onsen-bathing-stool-rack-empty.glb",
            "angular-onsen-garden-recliner.glb",
        };
        private static readonly string[] ShelfPool =
        {
            "자기 소속 영주 책장 2.glb", "자기 소속 영지 책장.glb",
            "angular-onsen-tea-sideboard-clear.glb",
            "angular-onsen-tea-sideboard-open-empty.glb",
            "angular-onsen-guest-tea-pantry-cabinet-heritage-destroyed.glb",
        };
        private static readonly string[] CounterPool =
        {
            "angular-onsen-tea-sideboard-clear.glb",
            "angular-onsen-tea-sideboard-open-empty.glb",
            "angular-onsen-three-seat-foot-drying-station-heritage-normal.glb",
            "angular-onsen-guest-tea-pantry-cabinet-heritage-destroyed.glb",
        };
        private static readonly string[] BedPool = { "침대.glb" };
        private static readonly string[] CratePool = { "전리품 상자.glb", "전리품 상자2.glb" };
        private static readonly string[] MirrorPool = { "angular-onsen-yukata-fitting-mirror-heritage-normal.glb" };

        /// <summary>호출 시마다 순환 선택(시각 다양성) — 시드 없는 시그니처 대체.</summary>
        private static int _counter;

        // ===================================================================
        // 공개 API — 프리미티브 IndoorFurniturePlacer와 시그니처 1:1
        // ===================================================================

        /// <summary>테이블 (targetHeight 0.9). GLB 실패 시 프리미티브 폴백.</summary>
        public static GameObject CreateTable(float width, float depth, float height, Material mat)
        {
            var go = Spawn(TablePool, TableTargetHeight, "Table");
            return go != null ? go : IndoorFurniturePlacer.CreateTable(width, depth, height, mat);
        }

        /// <summary>의자 (targetHeight 0.5). GLB 실패 시 프리미티브 폴백.</summary>
        public static GameObject CreateChair(float height, Material mat)
        {
            var go = Spawn(ChairPool, ChairTargetHeight, "Chair");
            return go != null ? go : IndoorFurniturePlacer.CreateChair(height, mat);
        }

        /// <summary>선반/사이드보드 (targetHeight 1.8). GLB 실패 시 프리미티브 폴백.</summary>
        public static GameObject CreateShelf(float width, float height, float depth, Material mat, int shelves = 3)
        {
            var go = Spawn(ShelfPool, ShelfTargetHeight, "Shelf");
            return go != null ? go : IndoorFurniturePlacer.CreateShelf(width, height, depth, mat, shelves);
        }

        /// <summary>카운터 (targetHeight 1.0). GLB 실패 시 프리미티브 폴백.</summary>
        public static GameObject CreateCounter(float width, float height, float depth, Material mat)
        {
            var go = Spawn(CounterPool, CounterTargetHeight, "Counter");
            return go != null ? go : IndoorFurniturePlacer.CreateCounter(width, height, depth, mat);
        }

        /// <summary>
        /// 침대 (targetHeight 0.5). ⚠ 세이브 상호작용 유지: root에 BoxCollider(isTrigger,
        /// center=(0,0.3,0) size=(width,0.6,depth)) + Bed 컴포넌트 부착.
        /// GLB 실패 시 프리미티브 CreateBed 폴백(Bed+Collider 이미 포함).
        /// </summary>
        public static GameObject CreateBed(float width, float depth, Material mat)
        {
            var go = Spawn(BedPool, BedTargetHeight, "Bed");
            if (go == null)
                return IndoorFurniturePlacer.CreateBed(width, depth, mat);

            BoxCollider bedCollider = go.GetComponent<BoxCollider>();
            if (bedCollider == null)
                bedCollider = go.AddComponent<BoxCollider>();
            bedCollider.isTrigger = true;
            bedCollider.center = new Vector3(0f, 0.3f, 0f);
            bedCollider.size = new Vector3(width, 0.6f, depth);

            if (go.GetComponent<Bed>() == null)
                go.AddComponent<Bed>();
            return go;
        }

        /// <summary>상자 (targetHeight 0.7). GLB 실패 시 프리미티브 CreateCounter 폴백.</summary>
        public static GameObject CreateCrate(float width, float height, float depth, Material mat)
        {
            var go = Spawn(CratePool, CrateTargetHeight, "Crate");
            return go != null ? go : IndoorFurniturePlacer.CreateCounter(width, height, depth, mat);
        }

        /// <summary>거울(벽장식) (targetHeight 1.6). GLB 실패 시 프리미티브 CreateShelf 폴백.</summary>
        public static GameObject CreateMirror(float width, float height, float depth, Material mat)
        {
            var go = Spawn(MirrorPool, MirrorTargetHeight, "Mirror");
            return go != null ? go : IndoorFurniturePlacer.CreateShelf(width, height, depth, mat);
        }

        // ===================================================================
        // 내부 — GLB 로드/인스턴스화/정규화/접지
        // ===================================================================

        /// <summary>GLB 로드 — 에디터 AssetDatabase 직접 로드, 실패 시 null.</summary>
        private static GameObject LoadGlb(string path)
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) return prefab;
#endif
            return null;
        }

        /// <summary>
        /// 풀에서 순환 선택 → Instantiate + 높이 정규화 + 접지(floorY=0). 로드 실패 시 null(호출부 폴백).
        /// Collider 등 부가 컴포넌트 미부착(순수 시각). Bed는 예외(호출부에서 부착).
        /// </summary>
        private static GameObject Spawn(string[] pool, float targetHeight, string name)
        {
            if (pool == null || pool.Length == 0) return null;
            var src = LoadGlb(FurnitureDir + pool[Mod(_counter++, pool.Length)]);
            if (src == null) return null;

            var go = Object.Instantiate(src);
            go.name = name;

            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers != null && renderers.Length > 0)
            {
                // 스케일 정규화: world bounds 높이 → targetHeight 균등 스케일.
                Bounds b = BoundsOf(renderers);
                float h = b.size.y;
                if (h > 0.0001f && Mathf.Abs(h - targetHeight) > 0.01f)
                {
                    float s = targetHeight / h;
                    go.transform.localScale = new Vector3(s, s, s);
                }
                // 접지: 밑면(bounds.min.y)이 바닥 y=0에 닿도록 y 보정.
                Bounds nb = BoundsOf(renderers);
                Vector3 p = go.transform.position;
                go.transform.position = new Vector3(p.x, p.y - nb.min.y, p.z);
            }
            return go;
        }

        private static Bounds BoundsOf(Renderer[] rs)
        {
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        private static int Mod(int a, int b) { int r = a % b; return r < 0 ? r + b : r; }
    }
}
