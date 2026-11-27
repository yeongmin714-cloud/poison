using UnityEditor;
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>[무번호 55-P1 진단] MagicHit 4종 머티리얼의 mainTexture 실측 — 미해결 GUID가
    /// null로 오는지 기본 텍스처로 오는지 판별(55차 수리 게이트 교정 근거).</summary>
    public static class Fx55TextureProbe
    {
        public static void RunAll()
        {
            string[] paths = new[]
            {
                "Assets/Matthew Guz/Hits Effects FREE/Materials/Magic Hit/Magic Hit.mat",
                "Assets/Matthew Guz/Hits Effects FREE/Materials/Magic Hit/Magic Simbol 2.mat",
                "Assets/Matthew Guz/Hits Effects FREE/Materials/Magic Hit/Magic Shockwave.mat",
                "Assets/Matthew Guz/Hits Effects FREE/Materials/Magic Hit/Magic Flash.mat",
            };
            foreach (var p in paths)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (mat == null) { Debug.LogWarning($"[55-PROBE] 로드 실패: {p}"); continue; }
                var tex = mat.mainTexture;
                if (tex == null)
                    Debug.LogWarning($"[55-PROBE] {mat.name}: mainTexture=NULL (Shader={mat.shader.name})");
                else
                    Debug.LogWarning($"[55-PROBE] {mat.name}: mainTexture='{tex.name}' {tex.width}x{tex.height} " +
                        $"iid={tex.GetInstanceID()} path='{AssetDatabase.GetAssetPath(tex)}' Shader={mat.shader.name}");
            }
            Debug.LogWarning("[55-PROBE] DONE");
        }
    }
}
