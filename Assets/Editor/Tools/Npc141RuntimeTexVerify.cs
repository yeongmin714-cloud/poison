using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [141차 검증] 런타임 NPC 머티리얼이 실제로 샘플링할 텍스처 신원/포맷 실측 배치 도구.
/// 목적: "여전히 깨짐" 판정의 1차 검증 — 임포트 수리(BC7/4096)가 런타임 바인딩에 도달했는가.
/// 각 rigged FBX의 Material 서브어셈블리 전수:
///  · 셰이더명, _MainTex/_BaseMap/_BumpMap/_MetallicGlossMap/_OcclusionMap 바인딩 텍스처의
///    name/width/height/TextureFormat(정수+이름)/mipCount/anisoLevel
///  · 바인딩 텍스처의 TextureImporter 상태(textureType/maxTextureSize/플랫폼 포맷/품질)
/// 실행: batchmode -executeMethod Npc141RuntimeTexVerify.RunVerify
/// 산출: TestOutput/npc141_runtime_tex_verify.txt
/// </summary>
public static class Npc141RuntimeTexVerify
{
    public static void RunVerify()
    {
        var log = new StringBuilder();
        log.AppendLine("[141차 검증] 런타임 바인딩 텍스처 실측 — " + System.DateTime.Now.ToString("HH:mm:ss"));
        string root = "Assets/Resources/Models/UserProvided/Npc2026";
        var fbxs = AssetDatabase.FindAssets("t:Model", new[] { root })
            .Select(g => AssetDatabase.GUIDToAssetPath(g))
            .Where(p => p.EndsWith("_rigged.fbx")).OrderBy(p => p).ToArray();

        string[] props = { "_MainTex", "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" };
        foreach (string fbx in fbxs)
        {
            var mats = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Material>().ToArray();
            if (mats.Length == 0) { log.AppendLine($"[141차] {fbx} — 머티리얼 0건"); continue; }
            foreach (var mat in mats)
            {
                log.AppendLine($"── {System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(fbx))} mat='{mat.name}' shader={mat.shader.name}");
                foreach (string prop in props)
                {
                    if (!mat.HasProperty(prop)) continue;
                    var tex = mat.GetTexture(prop) as Texture2D;
                    if (tex == null) { log.AppendLine($"   {prop}: (미바인딩)"); continue; }
                    string ipath = AssetDatabase.GetAssetPath(tex);
                    string impState = "";
                    if (AssetImporter.GetAtPath(ipath) is TextureImporter ti)
                        impState = $" importer(type={ti.textureType} maxSize={ti.maxTextureSize} sRGB={ti.sRGBTexture} q={ti.compressionQuality})";
                    log.AppendLine($"   {prop}: '{tex.name}' {tex.width}x{tex.height} fmt={tex.format}({(int)tex.format}) mips={tex.mipmapCount} aniso={tex.anisoLevel}{impState} path={ipath}");
                }
            }
        }
        Debug.Log(log.ToString());
        System.IO.File.WriteAllText("TestOutput/npc141_runtime_tex_verify.txt", log.ToString());
    }
}
