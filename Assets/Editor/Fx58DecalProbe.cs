using UnityEditor;
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>[무번호 58-P2 진단] 데칼 셰이더 실측 — URP 내장 Decal이 Shader.Find에서 null인지,
    /// 매뉴얼 카피 "Shader Graphs/Decal"이 파손(백색 렌더) 상태인지 에디터 진실원으로 판별.</summary>
    public static class Fx58DecalProbe
    {
        public static void RunAll()
        {
            var urp = Shader.Find("Universal Render Pipeline/Decal");
            Debug.LogWarning($"[58-PROBE] URP 내장 'Universal Render Pipeline/Decal' = {(urp == null ? "NULL(미로드/스트리핑)" : "FOUND")}" +
                (urp != null ? $" hasError={UnityEditor.ShaderUtil.ShaderHasError(urp)} supported={urp.isSupported} maxQueue={urp.renderQueue}" : ""));

            var manual = Shader.Find("Shader Graphs/Decal");
            Debug.LogWarning($"[58-PROBE] 매뉴얼 카피 'Shader Graphs/Decal' = {(manual == null ? "NULL" : "FOUND")}" +
                (manual != null ? $" hasError={UnityEditor.ShaderUtil.ShaderHasError(manual)} supported={manual.isSupported} maxQueue={manual.renderQueue}" : ""));

            if (urp != null)
            {
                var m = new Material(urp) { name = "Probe_URPDecal" };
                Debug.LogWarning($"[58-PROBE] URP Decal 머티리얼: renderQueue={m.renderQueue} color={m.color} mainTex={(m.mainTexture == null ? "null" : m.mainTexture.name)}");
                Object.DestroyImmediate(m);
            }
            if (manual != null)
            {
                var m = new Material(manual) { name = "Probe_ManualDecal" };
                Debug.LogWarning($"[58-PROBE] 매뉴얼 카피 머티리얼: renderQueue={m.renderQueue} color={m.color} mainTex={(m.mainTexture == null ? "null" : m.mainTexture.name)}");
                Object.DestroyImmediate(m);
            }

            // Graphics 설정 확인 생략(네임스페이스 버전 차이) — Shader.Find 실측이 핵심 근거.
            Debug.LogWarning("[58-PROBE] DONE");
        }
    }
}
