using UnityEngine;
using System.Collections.Generic;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// 스폰지 인근에 콜라이더 포함 개별 프롭(나무/바위) 배치.
    /// 몬스터 스폰 raycast는 Ground|Terrain 마스크만 쏘므로 Default 레이어 프롭 콜라이더는 자동 무시된다.
    /// GLB 로드 실패 시 Primitive 폴백 사용.
    /// 정적 엔트리포인트: PlaceAllIfNeeded(Transform parent) — 상위(FixMainScene)가 통합 페이즈에서 호출.
    /// </summary>
    public static class TerrainPropPlacer
    {
        // === 배치 상수 ===
        const string MARKER_NAME = "TerrainPropPlacer_Marker";
        const float SPAWN_X = 728f;
        const float SPAWN_Z = -529f;
        const float MIN_RADIUS = 30f;   // 스폰지에서 최소 거리
        const float MAX_RADIUS = 150f;  // 스폰지에서 최대 거리
        const int TREE_COUNT = 12;      // 나무 개수
        const int ROCK_COUNT = 10;      // 바위 개수
        const int PROP_SEED = 20260901; // 고정 시드
        const float GROUND_BASE = 1f;   // Ground_Inner 월드 y 기저

        /// <summary>
        /// 진입점. parent 하위에 이미 배치 마커가 있으면 스킵(중복 실행 가드).
        /// (728,-529) 반경 30~150m에 나무 12 + 바위 10, 콜라이더 포함(Default 레이어).
        /// </summary>
        public static void PlaceAllIfNeeded(Transform parent)
        {
            if (parent == null) return;

            // 중복 실행 가드
            if (FindChild(parent, MARKER_NAME) != null)
            {
                Debug.Log("[TerrainPropPlacer] Already placed — skipping.");
                return;
            }

            // GLB 모델 로드
            var treeModels = Resources.LoadAll<GameObject>("Models/UserProvided/terrain/trees");
            var rockModels = Resources.LoadAll<GameObject>("Models/UserProvided/terrain/rocks");

            var propsParent = new GameObject("SpawnProps");
            propsParent.transform.SetParent(parent, false);
            propsParent.layer = 0; // Default

            var rng = new System.Random(PROP_SEED);

            // 나무 배치 (GLB 실패 시 Primitive 폴백)
            if (treeModels.Length > 0)
            {
                for (int i = 0; i < TREE_COUNT; i++)
                {
                    GameObject instance = PlaceInstance(treeModels[rng.Next(treeModels.Length)], propsParent.transform, rng, 0.8f, 1.5f);
                    AddTreeCollider(instance);
                }
            }
            else
            {
                GameObject fallback = CreateTreeFallback(propsParent.transform);
                for (int i = 0; i < TREE_COUNT; i++)
                {
                    GameObject instance = PlaceInstance(fallback, propsParent.transform, rng, 0.8f, 1.5f);
                    AddTreeCollider(instance);
                }
                Debug.LogWarning("[TerrainPropPlacer] tree GLB empty — primitive fallback used.");
            }

            // 바위 배치 (GLB 실패 시 Primitive 폴백)
            if (rockModels.Length > 0)
            {
                for (int i = 0; i < ROCK_COUNT; i++)
                {
                    GameObject instance = PlaceInstance(rockModels[rng.Next(rockModels.Length)], propsParent.transform, rng, 0.8f, 2.0f);
                    AddRockCollider(instance);
                }
            }
            else
            {
                GameObject fallback = CreateRockFallback(propsParent.transform);
                for (int i = 0; i < ROCK_COUNT; i++)
                {
                    GameObject instance = PlaceInstance(fallback, propsParent.transform, rng, 0.8f, 2.0f);
                    AddRockCollider(instance);
                }
                Debug.LogWarning("[TerrainPropPlacer] rock GLB empty — primitive fallback used.");
            }

            // 배치 마커 (중복 실행 방지)
            var marker = new GameObject(MARKER_NAME);
            marker.transform.SetParent(parent, false);
            marker.SetActive(false);

            Debug.Log($"[TerrainPropPlacer] Placed trees: {TREE_COUNT}, rocks: {ROCK_COUNT}. SpawnProps children: {propsParent.transform.childCount}");
        }

        /// <summary>
        /// 스폰 반경 30~150m 내 랜덤 위치에 프롭 배치. y = 기저 1f + 지형 높이.
        /// </summary>
        static GameObject PlaceInstance(GameObject prefab, Transform parent, System.Random rng, float scaleMin, float scaleMax)
        {
            float angle = RandomRange(rng, 0f, 360f) * Mathf.Deg2Rad;
            float radius = RandomRange(rng, MIN_RADIUS, MAX_RADIUS);
            float x = SPAWN_X + Mathf.Cos(angle) * radius;
            float z = SPAWN_Z + Mathf.Sin(angle) * radius;

            // y = 기저 1f + 지형 높이 (Mesh 로컬)
            float y = GROUND_BASE + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42);

            var go = Object.Instantiate(prefab, parent);
            go.layer = 0; // Default
            go.transform.position = new Vector3(x, y, z);
            go.transform.rotation = Quaternion.Euler(0f, RandomRange(rng, 0f, 360f), 0f);
            go.transform.localScale = Vector3.one * RandomRange(rng, scaleMin, scaleMax);
            return go;
        }

        // === 콜라이더 근사 (Default 레이어 → 몬스터 스폰 raycast 무시) ===
        static void AddTreeCollider(GameObject go)
        {
            if (go.GetComponent<CapsuleCollider>() != null) return;
            float s = go.transform.localScale.x;
            var cc = go.AddComponent<CapsuleCollider>();
            cc.radius = 0.4f * s;
            cc.height = 2.4f * s; // CapsuleCollider는 height 사용 (halfHeight 아님)
        }

        static void AddRockCollider(GameObject go)
        {
            if (go.GetComponent<BoxCollider>() != null) return;
            float s = go.transform.localScale.x;
            var bc = go.AddComponent<BoxCollider>();
            bc.size = new Vector3(1.2f * s, 0.8f * s, 1.2f * s);
        }

        // === Primitive 폴백 ===
        static GameObject CreateTreeFallback(Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Tree_Fallback";
            go.transform.SetParent(parent, false);
            go.GetComponent<Renderer>().material.color = new Color(0.2f, 0.6f, 0.1f);
            return go;
        }

        static GameObject CreateRockFallback(Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Rock_Fallback";
            go.transform.SetParent(parent, false);
            go.GetComponent<Renderer>().material.color = new Color(0.4f, 0.4f, 0.4f);
            return go;
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
    }
}