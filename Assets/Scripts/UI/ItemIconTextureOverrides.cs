using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.UI
{
    /// <summary>
    /// [198차] 아이콘 전용 per-item 텍스처 오버라이드(static).
    ///
    /// 배경(P0 전수 감사 377종): 아이콘 대상 GLB의 임베드 알베도는 720~4096으로 고해상 —
    /// 시각 이득 교체 대상은 현재 0종(dish_* = UV붕괴 팔레트형, 정점컬러 149종 = 텍스처 무).
    /// 이 훅은 향후 고해상 아트 수용 시 "테이블 1줄 + 리소스 1장"으로 즉시 적용되는 인프라.
    ///
    /// 계약:
    ///  - 아이콘 베이크 경로 전용(GblItemIconRenderer TryMount/SyncBakeFromPrefab 호출) —
    ///    월드 비주얼 미개입(GLB 원본 무수정 — 141차 원본해상도 계약 유지).
    ///  - MPB 텍스처 교체만 — rend.material 인스턴스 생성 금지(DishGlbCatalog.ApplyTint 동일 규약,
    ///    아이콘 베이크 임시노드 머티리얼 누수 방지). GetPropertyBlock 선행으로 기존 MPB(_BaseColor 틴트) 보존.
    ///  - 미등록 id는 false + 무변경(등록 없으면 전 종 기존 임베드 텍스처 그대로).
    /// </summary>
    public static class ItemIconTextureOverrides
    {
        /// <summary>item id → Resources 텍스처 경로(확장자 제외, 예: "Textures/foo_albedo").</summary>
        public static readonly Dictionary<string, string> Table = new Dictionary<string, string>
        {
            // [198차 P0 감사] 운영 등록 0건 — 교체 이득 대상 부재. 수용 예시:
            // { "wood_bow", "Textures/wood_bow_albedo" },
        };

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");   // URP/glTFast 알베도
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");   // 빌트인 계열 폴백(무해)

        /// <summary>등록분만 적용. 미등록/리소스 부재/렌더러 부재 → false(무변경).</summary>
        public static bool TryApply(string itemId, GameObject node)
        {
            if (string.IsNullOrEmpty(itemId) || node == null)
                return false;
            if (!Table.TryGetValue(itemId, out string resPath))
                return false;
            var tex = Resources.Load<Texture2D>(resPath);
            if (tex == null)
            {
                Debug.LogWarning($"[ItemIconTextureOverrides] 오버라이드 텍스처 로드 실패 — id={itemId} path={resPath}");
                return false;
            }
            var rends = node.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0)
                return false;
            foreach (var r in rends)
            {
                var mpb = new MaterialPropertyBlock();
                r.GetPropertyBlock(mpb);          // 기존 MPB(_BaseColor 틴트 등) 보존 — 틴트와 공존
                mpb.SetTexture(BaseMapId, tex);
                mpb.SetTexture(MainTexId, tex);
                r.SetPropertyBlock(mpb);
            }
            return true;
        }

        /// <summary>테이블 등록 여부(진단/테스트용).</summary>
        public static bool HasOverride(string itemId)
            => !string.IsNullOrEmpty(itemId) && Table.ContainsKey(itemId);
    }
}
