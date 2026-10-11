using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [15차 B1] 선택 병사 실루엣 하이라이트 — URP Lit 머티리얼의 _EMISSION 키워드(공유 1회 활성,
    /// 기본 검정이므로 미선택 무변화) + 렌더러별 MaterialPropertyBlock으로 액센트 이미시션 주입.
    /// 머티리얼 색 변이 금지 계약 준수(공유 머티리얼 색/텍스처는 불변).
    /// </summary>
    public static class GuardHighlightUtility
    {
        private static readonly Color SelectedEmission = new Color(0.10f, 0.28f, 0.60f, 1f);
        private static MaterialPropertyBlock _mpb;

        public static void SetHighlight(GuardPlaceholder guard, bool on)
        {
            if (guard == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            var renderers = guard.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var mat = r.sharedMaterial;
                if (mat == null || !mat.HasProperty("_EmissionColor")) continue;
                if (on && !mat.IsKeywordEnabled("_EMISSION")) mat.EnableKeyword("_EMISSION");
                _mpb.SetColor("_EmissionColor", on ? SelectedEmission : Color.black);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
