using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// P31-D: 마을 건물 GLB 카탈로그 (2026-09-24).
    ///
    /// Assets/새로운 glb/건물/ 23종 건물을 역할(집/부자집/둥글집/쉼터/상점/창고)별로 카탈로그화.
    /// 로드 = 에디터 AssetDatabase 직접 로드(#if UNITY_EDITOR) + 프리미티브 폴백 —
    ///   fish/crop(새로운 glb) 팩과 동일 정신: 대용량 GLB는 git 커밋하지 않고(local 유지),
    ///   게임이 에디터에서 경로로 직접 로드해 렌더한다. 빌드(플레이어)에선 Resources 합성 프리팹 폴백.
    ///
    /// 배치 결정론 시드는 VillageBuilder가 전달한다(건물 키 = 인덱스 % 풀 길이).
    /// 접지는 VillageBuilder의 GetHeightAt + GROUND_BASE(1f)로 처리한다(별도 접지 없음).
    /// 스케일은 렌더러 bounds 높이 기준 정규화(targetHeight)로 통일한다(배치마다 unstable 편차 방지).
    /// </summary>
    public static class VillageBuildingCatalog
    {
        private const string BuildingDir = "Assets/새로운 glb/건물/";

        /// <summary>집(일반 주택) — 지붕 있는 소형 주택 풀.</summary>
        public static readonly string[] HouseKeys =
        {
            "집.glb", "집2.glb", "집3.glb", "집4.glb", "집5.glb", "집6.glb",
            "둥글둥글 집.glb", "둥글둥글 집 2.glb", "둥글둥글 집 3.glb", "둥글둥글집 4.glb",
            "지붕 없는 집.glb",
        };

        /// <summary>부자집(대형 주택) 풀 — 마을 일부는 부자집으로.</summary>
        public static readonly string[] RichHouseKeys =
        {
            "부자집1.glb", "부자집 2.glb", "부자집2.glb", "부자집3.glb",
            "둥글둥글 부자집.glb", "지붕 없는 부자집.glb",
        };

        /// <summary>쉼터/창고 풀 — 마을 창고·헛간 자리.</summary>
        public static readonly string[] ShelterKeys =
        {
            "쉼터.glb", "쉼터2.glb",
            "angular-common-buildings-corner-retail-shell-compact-normal.glb",
        };

        /// <summary>실외 상점 풀 — 대표 마을용(주점/음식점/약초방).</summary>
        public static readonly string[] ShopKeys =
        {
            "주점.glb", "음식점.glb", "약초방.glb",
        };

        /// <summary>역할별 목표 높이(m) — 배치 스케일 정규화 기준(집/상점/창고).</summary>
        public const float HouseTargetHeight = 4.5f;
        public const float RichTargetHeight = 6.0f;
        public const float ShopTargetHeight = 5.2f;
        public const float ShelterTargetHeight = 4.0f;

        /// <summary>GLB 풀 길이.</summary>
        public static readonly int HouseCount = HouseKeys.Length;      // 11
        public static readonly int RichCount = RichHouseKeys.Length;   // 6
        public static readonly int ShelterCount = ShelterKeys.Length;  // 3
        public static readonly int ShopCount = ShopKeys.Length;        // 3

        /// <summary>결정론 인덱스 → GLB 파일명(풀 경계 래핑).</summary>
        public static string HousePath(int i) => BuildingDir + HouseKeys[Mod(i, HouseCount)];
        public static string RichPath(int i)  => BuildingDir + RichHouseKeys[Mod(i, RichCount)];
        public static string ShelterPath(int i) => BuildingDir + ShelterKeys[Mod(i, ShelterCount)];
        public static string ShopPath(int i)  => BuildingDir + ShopKeys[Mod(i, ShopCount)];

        private static int Mod(int a, int b) { int r = a % b; return r < 0 ? r + b : r; }

        /// <summary>
        /// GLB 로드 — 에디터 AssetDatabase 직접 로드, 실패 시 null(호출부가 프리미티브 폴백).
        /// </summary>
        public static GameObject LoadGlb(string path)
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) return prefab;
#endif
            return null;
        }

        /// <summary>
        /// 인스턴스화 + 스케일 정규화 + 접지 오프셋. 로드 실패 시 null 반환(호출부 폴백).
        /// 순서: ①pos에 identity 회전 배치 ②렌더러 world bounds 높이 측정 ③targetHeight로 균등 스케일
        ///       ④스케일 후 bounds.min.y 다시 측정해 밑면이 groundY에 닿도록 y 보정.
        /// </summary>
        public static GameObject InstantiateAtGround(string path, Vector3 pos, float groundY, float targetHeight, string name)
        {
            var src = LoadGlb(path);
            if (src == null) return null;

            var go = Object.Instantiate(src);
            go.name = name;
            go.transform.SetPositionAndRotation(pos, Quaternion.identity);

            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers != null && renderers.Length > 0)
            {
                // ② 현재 world bounds 높이
                Bounds b = boundsOf(renderers);
                float h = b.size.y;
                if (h > 0.0001f && Mathf.Abs(h - targetHeight) > 0.01f)
                {
                    float s = targetHeight / h;
                    go.transform.localScale = new Vector3(s, s, s);
                }
                // ④ 밑면 접지
                Bounds nb = boundsOf(renderers);
                Vector3 p = go.transform.position;
                go.transform.position = new Vector3(p.x, groundY + (p.y - nb.min.y), p.z);
            }
            else
            {
                go.transform.position = new Vector3(pos.x, groundY + 0.05f, pos.z);
            }
            return go;
        }

        private static Bounds boundsOf(Renderer[] rs)
        {
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }
    }
}