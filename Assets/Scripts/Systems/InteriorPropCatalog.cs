using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [실내 고품질화 P0] 인테리어 소품 카탈로그 — Resources 기반 결정론 로더.
    ///
    /// 소스(실측 2026-11-07):
    ///   - Resources/Models/Restaurant/ 플랫 325 GLB(이미 임포트됨 — 임포트 비용 0):
    ///     Furnit×168 / Table×49 / Stool×29 / Chair×22 / StationTable×12 / Sofa×12 /
    ///     Sink×7 / Owen×7 / Armchair×7 / HangShelf×6 / StandShelf×4 / RangeHood×2
    ///   - Resources/Models/Interior/{Decor,Kitchen,Props}/ — 신규 선별 복사 43종
    ///     (조명기구·기둥·커튼·문 / 주방 / 음식·물약·자루 소품)
    ///
    /// 계약: ①결정론 — 정수 해시로만 변형 선택(엔진 난수 미사용) ②부재 시 무음 폴백
    ///   (다음 후보 순차 시도 — 전부 부재면 null 반환+1회 경고 캐시) ③크기 정규화 —
    ///   렌더러 bounds 기반 목표 높이 스케일(155차 실측 스케일 테이블 선례 — 팩 원본 스케일 불신).
    /// </summary>
    public static class InteriorPropCatalog
    {
        private const string RestaurantPrefix = "Models/Restaurant/";
        private const string InteriorPrefix = "Models/Interior/";

        private static readonly HashSet<string> _warnedMissing = new HashSet<string>();

        // ── 시리즈 접미사 테이블(실측 파일 목록 기반 — 결정론 순회 순서) ──
        internal static readonly string[] Table2Suffixes = { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "R", "S" };
        internal static readonly string[] Table3Suffixes = { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J" };
        internal static readonly string[] Table4Suffixes = { "A", "B", "C", "D", "E", "F", "G", "H" };
        internal static readonly string[] Chair1Suffixes = { "A", "B", "C", "D", "E", "F", "G" };
        internal static readonly string[] Chair2Suffixes = { "A", "B", "C", "D", "E", "F", "G" };
        internal static readonly string[] Chair3Suffixes = { "A", "B", "C", "D", "E", "F", "G", "H", "I" };
        internal static readonly string[] Stool1Suffixes = { "A", "B", "C", "D" };
        internal static readonly string[] ArmchairSuffixes = { "A", "B", "C", "D", "E", "F", "G" };
        internal static readonly string[] SofaSuffixes = { "A", "B", "C", "D", "E", "F", "G" };
        internal static readonly string[] FurnitSeries = { "A", "B", "C", "D", "E", "F", "G", "H" };
        internal const int FurnitVariants = 18;   // Furnit_{S}_1..18(+_{S} 변형 — 폴백 순회)

        /// <summary>시리즈 접미사 결정 — 순수 함수(테스트 대상). 폴백 순회는 Place 경로가 담당.</summary>
        public static string PickSuffix(string[] suffixes, int index)
        {
            if (suffixes == null || suffixes.Length == 0) return "A";
            int i = ((index % suffixes.Length) + suffixes.Length) % suffixes.Length;
            return suffixes[i];
        }

        /// <summary>Furnit 계열 결정 — 순수 함수(테스트 대상). "Furnit_{S}_{n}" 형태 반환.</summary>
        public static string PickFurnit(int index)
        {
            string series = PickSuffix(FurnitSeries, index);
            int variant = ((index % FurnitVariants) + FurnitVariants) % FurnitVariants + 1;
            return $"Furnit_{series}_{variant}";
        }

        /// <summary>리소스 로드+월드 배치 — 부재 시 후보 경로 순차 폴백, 전부 부재면 null(무음 1회 경고).</summary>
        public static GameObject Place(Transform parent, string[] candidatePaths, string name,
            Vector3 localPos, float yawDeg, float targetHeightM)
        {
            GameObject prefab = LoadFirst(candidatePaths);
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab);
            go.name = name;
            // [렌더러 전용] 장식 가구 콜라이더 제거 — 기능 앵커 블로킹 금지(CreateRendererOnlyStoneBox 선례)
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
                Object.Destroy(col);
            if (targetHeightM > 0f) NormalizeHeight(go, targetHeightM);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            return go;
        }

        /// <summary>[157차] Polyfork GLB 게이트 — Resources/Models/Polyfork/{id}. CC BY 4.0(CREDITS.md 유지). 기존 Place 재사용(콜라이더 제거·높이 정규화 내장).</summary>
        public static GameObject PlacePolyfork(Transform parent, string polyforkId, string name,
            Vector3 localPos, float yawDeg, float targetHeightM)
            => Place(parent, new[] { $"Models/Polyfork/{polyforkId}" }, name, localPos, yawDeg, targetHeightM);

        /// <summary>[161차 P1] 야외 건축물용 — 콜라이더 유지형(렌더러 bounds 기반 BoxCollider 1개 부여). 실내 렌더러 전용 계약과 별개 경로.</summary>
        public static GameObject PlacePolyforkSolid(Transform parent, string polyforkId, string name,
            Vector3 localPos, float yawDeg, float targetHeightM)
        {
            // Place는 콜라이더를 제거하므로 복제하지 않고 Solid 전용 경로: 로드→인스턴스→(스트립 없음)→정규화→배치→BoxCollider
            var go = LoadFirst(new[] { $"Models/Polyfork/{polyforkId}" });
            if (go == null) return null;
            var inst = Object.Instantiate(go);
            inst.name = name;
            if (targetHeightM > 0f) NormalizeHeight(inst, targetHeightM);
            if (parent != null) inst.transform.SetParent(parent, false);
            inst.transform.localPosition = localPos;
            inst.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            var renderers = inst.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                var col = inst.AddComponent<BoxCollider>();
                col.center = inst.transform.InverseTransformPoint(b.center);
                col.size = new Vector3(
                    b.size.x / Mathf.Max(0.0001f, inst.transform.lossyScale.x),
                    b.size.y / Mathf.Max(0.0001f, inst.transform.lossyScale.y),
                    b.size.z / Mathf.Max(0.0001f, inst.transform.lossyScale.z));
            }
            return inst;
        }

        /// <summary>후보 경로 순차 로드 — 첫 성공 반환(결정론 순서).</summary>
        public static GameObject LoadFirst(params string[] candidatePaths)
        {
            if (candidatePaths == null) return null;
            foreach (string path in candidatePaths)
            {
                if (string.IsNullOrEmpty(path)) continue;
                var prefab = Resources.Load<GameObject>(path);
                if (prefab != null) return prefab;
            }
            string key = string.Join("|", candidatePaths);
            if (_warnedMissing.Add(key))
                Debug.LogWarning($"[InteriorPropCatalog] 리소스 전부 부재 — {key}");
            return null;
        }

        /// <summary>렌더러 bounds 기반 균일 스케일 — 목표 높이 정규화(155차 선례).</summary>
        public static void NormalizeHeight(GameObject go, float targetHeightM)
        {
            if (go == null || targetHeightM <= 0f) return;
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            if (b.size.y <= 0.0001f) return;
            float scale = targetHeightM / b.size.y;
            go.transform.localScale = go.transform.localScale * scale;
        }

        // ── 카테고리 편의 배치(후보 경로 구성 내장 — 호출부는 해시 인덱스만 전달) ──

        public static GameObject PlaceTable(Transform parent, string name, Vector3 pos, float yaw, int idx) =>
            Place(parent, new[]
            {
                RestaurantPrefix + "Table_2_" + PickSuffix(Table2Suffixes, idx),
                RestaurantPrefix + "Table_3_" + PickSuffix(Table3Suffixes, idx),
                RestaurantPrefix + "Table_4_" + PickSuffix(Table4Suffixes, idx),
            }, name, pos, yaw, 0.78f);

        public static GameObject PlaceChair(Transform parent, string name, Vector3 pos, float yaw, int idx) =>
            Place(parent, new[]
            {
                RestaurantPrefix + "Chair_1_" + PickSuffix(Chair1Suffixes, idx),
                RestaurantPrefix + "Chair_2_" + PickSuffix(Chair2Suffixes, idx),
                RestaurantPrefix + "Chair_3_" + PickSuffix(Chair3Suffixes, idx),
            }, name, pos, yaw, 0.95f);

        public static GameObject PlaceStool(Transform parent, string name, Vector3 pos, float yaw, int idx) =>
            Place(parent, new[]
            {
                RestaurantPrefix + "Stool_1_" + PickSuffix(Stool1Suffixes, idx),
                RestaurantPrefix + "Stool_2_A",
            }, name, pos, yaw, 0.62f);

        public static GameObject PlaceArmchair(Transform parent, string name, Vector3 pos, float yaw, int idx) =>
            Place(parent, new[] { RestaurantPrefix + "Armchair_1_" + PickSuffix(ArmchairSuffixes, idx) },
                name, pos, yaw, 1.0f);

        public static GameObject PlaceSofa(Transform parent, string name, Vector3 pos, float yaw, int idx) =>
            Place(parent, new[] { RestaurantPrefix + "Sofa_1_" + PickSuffix(SofaSuffixes, idx) },
                name, pos, yaw, 0.9f);

        /// <summary>수납/장식 캐비닛 계열(Furnit_*) — PickFurnit 결정론 변형.</summary>
        public static GameObject PlaceFurnit(Transform parent, string name, Vector3 pos, float yaw, int idx, float targetHeightM = 1.8f)
        {
            string baseName = PickFurnit(idx);
            string series = baseName.Substring("Furnit_".Length, 1);
            string num = baseName.Substring(baseName.LastIndexOf('_') + 1);
            return Place(parent, new[]
            {
                RestaurantPrefix + baseName,
                RestaurantPrefix + $"Furnit_{series}_{num}_{series}",   // 자기접미 변형 폴백
                RestaurantPrefix + "Furnit_A_1",
            }, name, pos, yaw, targetHeightM);
        }

        public static GameObject PlaceStationTable(Transform parent, string name, Vector3 pos, float yaw, int idx) =>
            Place(parent, new[]
            {
                RestaurantPrefix + "StationTable_1_" + PickSuffix(new[] { "A", "B", "C", "F" }, idx),
                RestaurantPrefix + "StationTable_2_" + PickSuffix(new[] { "A", "B", "C", "F" }, idx),
            }, name, pos, yaw, 0.9f);

        public static GameObject PlaceStandShelf(Transform parent, string name, Vector3 pos, float yaw, int idx) =>
            Place(parent, new[]
            {
                RestaurantPrefix + "StandShelf_1_" + PickSuffix(new[] { "A", "B" }, idx),
                RestaurantPrefix + "StandShelf_2_" + PickSuffix(new[] { "A", "B" }, idx),
            }, name, pos, yaw, 1.85f);

        public static GameObject PlaceHangShelf(Transform parent, string name, Vector3 pos, float yaw, int idx) =>
            Place(parent, new[]
            {
                RestaurantPrefix + "HangShelf_1_" + PickSuffix(new[] { "A", "B" }, idx),
                RestaurantPrefix + "HangShelf_2_" + PickSuffix(new[] { "A", "B" }, idx),
            }, name, pos, yaw, 0f);   // 벽부착 — 높이 정규화 생략(원본 스케일 유지)

        public static GameObject PlaceOwen(Transform parent, string name, Vector3 pos, float yaw) =>
            Place(parent, new[]
            {
                RestaurantPrefix + "Owen_A", RestaurantPrefix + "Owen_B",
                RestaurantPrefix + "Owen_C", RestaurantPrefix + "Owen_E",
            }, name, pos, yaw, 1.25f);

        public static GameObject PlaceKitchen(Transform parent, string name, string prop, Vector3 pos, float yaw, float height) =>
            Place(parent, new[]
            {
                InteriorPrefix + "Kitchen/" + prop,
                RestaurantPrefix + prop,   // 플랫 Restaurant에도 있는 키친 후보 폴백
            }, name, pos, yaw, height);

        public static GameObject PlaceDecor(Transform parent, string name, string prop, Vector3 pos, float yaw, float height) =>
            Place(parent, new[] { InteriorPrefix + "Decor/" + prop }, name, pos, yaw, height);

        public static GameObject PlaceProp(Transform parent, string name, string prop, Vector3 pos, float yaw, float height) =>
            Place(parent, new[] { InteriorPrefix + "Props/" + prop }, name, pos, yaw, height);

        /// <summary>천장 행ラン 프 — 천장에서 내려오는 조명기구(Interior/Decor/Lights_*).</summary>
        public static GameObject PlaceCeilingLamp(Transform parent, string name, Vector3 pos, int idx)
        {
            string[] candidates =
            {
                InteriorPrefix + "Decor/Lights_1_A",
                InteriorPrefix + "Decor/Lights_2_A",
                InteriorPrefix + "Decor/Lights_1_A_001",
                InteriorPrefix + "Decor/Lights_2_A_001",
            };
            GameObject go = Place(parent, candidates, name, pos, 0f, 0f);
            return go;
        }
    }
}
