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

            // GLB 모델 로드
            var treeModels = Resources.LoadAll<GameObject>("Models/UserProvided/terrain/trees");
            var rockModels = Resources.LoadAll<GameObject>("Models/UserProvided/terrain/rocks");
            if (treeModels.Length == 0 || rockModels.Length == 0)
            {
                Debug.LogError("[TerrainModelPlacer] GLB terrain models not found in Resources/Models/UserProvided/terrain/ — skip placement.");
                return;
            }

            var envParent = new GameObject("EnvironmentModels");
            envParent.transform.SetParent(parent, false);
            envParent.layer = 0; // Default — 몬스터 스폰 raycast(Ground|Terrain 마스크)에서 자동 무시

            var rng = new System.Random(PROP_SEED);

            int treePlaced = PlaceType(envParent.transform, treeModels, TREE_ATTEMPTS, TreeAcceptance, 0.8f, 1.8f, false, rng);
            int rockPlaced = PlaceType(envParent.transform, rockModels, ROCK_ATTEMPTS, RockAcceptance, 0.8f, 2.0f, true, rng);

            // GPU Instancing 활성화 (기존 구조 유지 — 공유 머티리얼 enableInstancing)
            EnableGPUInstancing(envParent);

            // 배치 마커 (중복 실행 방지)
            var marker = new GameObject(MARKER_NAME);
            marker.transform.SetParent(parent, false);
            marker.SetActive(false);

            Debug.Log($"[TerrainModelPlacer] Placed trees: {treePlaced}, rocks: {rockPlaced}. Environment children: {envParent.transform.childCount}");
        }

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