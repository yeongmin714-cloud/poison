using System.Collections.Generic;
using ProjectName.Core.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectName.Systems
{
    /// <summary>
    /// 호수 수변 물고기 장식 스포너 (09-23 신규) — FishCatalog 결정론 50종 GLB를 호수 가장자리에 배치.
    ///
    /// NaturalResourceSpawner의 "호수(반경×1.15) 제외존"을 뒤집은 배치: 호수 밖을 거부하는 대신
    /// radius×(0.98~1.15) 수변 밴드(물 바로 옆 육지)에만 배치한다. SHORE_OUT 1.15는
    /// NaturalResourceSpawner.LAKE_MARGIN_FACTOR와 동일값 — 채집/채굴 노드와 공간적으로 분리.
    ///
    /// 순수 시각 장식: ResourceNode/HerbPickup/BoxCollider 미부착(호버·채집 대상 아님).
    /// 지표면 계약: 월드 y = 1f + TerrainGenerator.GetHeightAt(x, z, Plains, 42) + 밑면 정렬.
    /// 수락 조건: waterLevel+0.15 이상(육지) ~ +2.2 이하(가장자리) — 물속/내륙 깊숙이 배제.
    ///
    /// 결정론: 자체 xorshift64* PRNG(DetRng 동형, 고정 시드 20260925)만 사용 —
    /// System.Random 금지 준수. 종 선택은 배치 순번의 결정론 순환(placed % 50) —
    /// 전체 50종이 순서대로 한 번씩 커버된 뒤 반복된다.
    ///
    /// 모델: 에디터에선 AssetDatabase로 fish GLB 직접 로드, 실패 시 스폰 시점 프리미티브 폴백
    /// (빌드엔 fish GLB가 Resources에 없으므로 전량 폴백 — 시각 장식이라 게임플레이 영향 없음).
    ///
    /// 배선: GameSetup.BootstrapTerrainDeco() 끝에서 EnsureLakeshoreFish(decoGO.transform) 멱등 호출.
    /// </summary>
    public static class FishLakeshoreDecorator
    {
        // === 배치 상수 (NaturalResourceSpawner 계약과 정합) ===
        const string CONTAINER_NAME = "LakeshoreFishDecor";
        const string MARKER_NAME = "LakeshoreFishDecor_Marker";
        const ulong SPAWN_SEED = 20260925;         // 고정 시드 — 실행마다 동일 배치(결정론)
        const float GROUND_BASE = 1f;              // Ground_Inner 월드 y 기저 (높이 계약)
        const int HEIGHT_SEED = 42;                // 지형 높이 계약 시드 (Plains, 42)
        const float SHORE_IN = 0.98f;              // 호수 반경 배수 — 물 바로 옆
        const float SHORE_OUT = 1.15f;             // = NaturalResourceSpawner.LAKE_MARGIN_FACTOR
        const int FISH_PER_LAKE = 8;               // 호수당 목표 (16개 호수 → 최대 ~128기)
        const float MIN_DIST = 2.5f;               // 물고기 간 최소 간격
        const float SPAWN_X = 728f, SPAWN_Z = -529f, SPAWN_EXCLUDE = 25f;   // 스폰지 제외
        const float SCALE_MIN = 0.8f, SCALE_MAX = 1.3f;

        static GameObject[] _visualPrefabs;   // 인덱스 = FishCatalog 순서 (에디터 GLB 로드, null 허용)

        /// <summary>진입점(멱등). 컨테이너 중복 가드 + TerrainOnly/DisableAllUi 게이트.</summary>
        public static void EnsureLakeshoreFish(Transform parent)
        {
            if (parent == null) return;

            // [GNB] TerrainOnly(지형 관찰) 씬 — 장식 스폰 생략 (NaturalResourceSpawner 동일 게이트)
            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid() && !string.IsNullOrEmpty(scene.name) && scene.name.Contains("TerrainOnly"))
            {
                Debug.Log("[FishLakeshoreDecorator] TerrainOnly 씬 — 수변 장식 스폰 생략.");
                return;
            }
            if (ProjectName.Core.UITransitionState.DisableAllUi)
            {
                Debug.Log("[FishLakeshoreDecorator] DisableAllUi=true — 수변 장식 스폰 생략.");
                return;
            }

            // 중복 실행 가드 (컨테이너 직속 자식 마커)
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c != null && c.gameObject.name == CONTAINER_NAME)
                {
                    Debug.Log("[FishLakeshoreDecorator] Already placed — skipping.");
                    return;
                }
            }

            var container = new GameObject(CONTAINER_NAME);
            container.transform.SetParent(parent, false);
            container.layer = 0;

            LoadVisualPrefabs();

            var lakes = TerrainGenerator.Lakes;
            if (lakes == null || lakes.Count == 0)
            {
                Debug.LogWarning("[FishLakeshoreDecorator] ⚠️ 호수 목록 부재 — 수변 장식 스폰 생략.");
                return;
            }

            var rng = new DetRng(SPAWN_SEED);
            var placedPoints = new List<Vector2>();   // 최소 간격 검사 (배치 수 백 단위 — 선형 탐색 충분)
            int placed = 0;

            foreach (var lake in lakes)
            {
                int lakePlaced = 0;
                int attempts = FISH_PER_LAKE * 10;
                for (int attempt = 0; attempt < attempts && lakePlaced < FISH_PER_LAKE; attempt++)
                {
                    float ang = rng.Range(0f, 360f) * Mathf.Deg2Rad;
                    float d = lake.radius * rng.Range(SHORE_IN, SHORE_OUT);
                    float x = lake.center.x + Mathf.Cos(ang) * d;
                    float z = lake.center.z + Mathf.Sin(ang) * d;

                    // 스폰지 제외 (프롭/건물과 겹침 방지)
                    float sdx = x - SPAWN_X, sdz = z - SPAWN_Z;
                    if (sdx * sdx + sdz * sdz < SPAWN_EXCLUDE * SPAWN_EXCLUDE) continue;

                    // 지표면 계약 + 수변 밴드 수락 (육지이면서 가장자리)
                    float y = GROUND_BASE + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, HEIGHT_SEED);
                    if (y < lake.waterLevel + 0.15f) continue;   // 물속 배제
                    if (y > lake.waterLevel + 2.2f) continue;    // 내륙 깊숙이 배제 — 가장자리만

                    // 최소 간격 (결정론 선형 검사)
                    bool tooClose = false;
                    foreach (var p in placedPoints)
                    {
                        float dx = p.x - x, dz = p.y - z;
                        if (dx * dx + dz * dz < MIN_DIST * MIN_DIST) { tooClose = true; break; }
                    }
                    if (tooClose) continue;

                    // 종 선택: 배치 순번의 결정론 순환 — 50종 전종이 순서대로 커버됨
                    int speciesIndex = placed % FishCatalog.TotalCount;
                    float scale = rng.Range(SCALE_MIN, SCALE_MAX);
                    float yaw = rng.Range(0f, 360f);

                    SpawnFishVisual(container.transform, $"LakeshoreFish_{placed + 1:D3}_{FishCatalog.SpeciesKey(speciesIndex)}",
                        speciesIndex, x, y, z, scale, yaw);

                    placedPoints.Add(new Vector2(x, z));
                    placed++;
                    lakePlaced++;
                }
            }

            // 배치 마커 (중복 실행 방지)
            var marker = new GameObject(MARKER_NAME);
            marker.transform.SetParent(container.transform, false);
            marker.SetActive(false);

            Debug.Log($"[FishLakeshoreDecorator] ✅ 수변 물고기 장식 배치 완료 — {placed}기 / 호수 {lakes.Count}개 (FishCatalog 50종 결정론 순환)");
        }

        // ================================================================
        // 시각 오브젝트 구성 — 순수 장식 (컴포넌트/콜라이더 미부착)
        // ================================================================

        /// <summary>물고기 1기 배치: 지표면에 루트를 세우고 GLB 시각 모델을 밑면 정렬. 콜라이더 없음.</summary>
        static void SpawnFishVisual(Transform parent, string goName, int speciesIndex,
            float x, float y, float z, float scale, float yawDeg)
        {
            var go = new GameObject(goName);
            go.layer = 0;   // Default — 인터랙티브 아님(호버/채집 대상 배제)
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, y, z);
            go.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);

            var prefab = _visualPrefabs != null && speciesIndex < _visualPrefabs.Length
                ? _visualPrefabs[speciesIndex] : null;

            if (prefab != null)
            {
                var visual = Object.Instantiate(prefab, go.transform);
                visual.name = "Visual";
                visual.layer = 0;
                visual.transform.localScale = Vector3.one * scale;
                AlignBottomToGround(go.transform, visual);   // 밑면 == 지표면
            }
            else
            {
                // 폴백: 프리미티브 캡슐(물고기 형태 축소) — 콜라이더 즉시 제거(순수 시각)
                var prim = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                prim.name = "Visual_Fallback";
                Object.Destroy(prim.GetComponent<Collider>());
                prim.layer = 0;
                prim.transform.SetParent(go.transform, false);
                prim.transform.localScale = new Vector3(0.35f, 0.22f, 0.7f) * scale;
                prim.transform.localPosition = new Vector3(0f, 0.12f, 0f);
                var r = prim.GetComponent<Renderer>();
                if (r != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.color = new Color(0.36f, 0.55f, 0.68f, 1f);   // 은청색 물고기톤
                    r.material = mat;
                }
            }
        }

        /// <summary>시각 모델 렌더러 bounds 밑면을 노드 루트(지표면) 높이로 정렬 — NaturalResourceSpawner 선례.</summary>
        static void AlignBottomToGround(Transform node, GameObject visual)
        {
            var renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) return;

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
            if (!first && bounds.size != Vector3.zero)
            {
                float dy = node.position.y - bounds.min.y;
                if (Mathf.Abs(dy) > 0.001f)
                    visual.transform.position += new Vector3(0f, dy, 0f);
            }
        }

        // ================================================================
        // 모델 로드 — 에디터 GLB 직접 로드 (NaturalResourceSpawner 선례)
        // ================================================================
        static void LoadVisualPrefabs()
        {
            _visualPrefabs = new GameObject[FishCatalog.TotalCount];
#if UNITY_EDITOR
            for (int i = 0; i < FishCatalog.TotalCount; i++)
            {
                _visualPrefabs[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(FishCatalog.GlbPath(i));
            }
            int loaded = 0;
            foreach (var p in _visualPrefabs) if (p != null) loaded++;
            Debug.Log($"[FishLakeshoreDecorator] fish GLB 로드: {loaded}/{FishCatalog.TotalCount} (미로드분은 프리미티브 폴백)");
#endif
            // 빌드: fish GLB가 Resources에 없으므로 전량 null → 스폰 시점 프리미티브 폴백
        }

        // ================================================================
        // 결정론 PRNG — NaturalResourceSpawner.DetRng 동형 xorshift64*
        // (원본은 private nested라 재사용 불가 → 동일 구현 복제. System.Random 미사용.)
        // ================================================================
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
