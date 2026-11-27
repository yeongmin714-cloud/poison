using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [141차 B2] NPC 머티리얼 교차바인딩 에셋 레벨 영구 수리(배치모드).
/// 원인(141차 실측): 임베드 텍스처가 서브어셈블리로 노출되지 않아(134차 A-2 실측) 임포터가
/// 머티리얼 텍스처 참조명("base_color"/"normal" — 전 캐릭터 동일명)을 프로젝트 전역 이름매칭으로
/// 해결 → 알파벳 선두 Npc_Boy_1의 텍스처에 전 캐릭터가 몰림(_BumpMap 포함 전원 교차바인딩).
/// Unity 임포터 텍스처 해석 우선순위 1위 = 모델과 같은 폴더 → **캐릭터별 텍스처를 rigged FBX 옆에
/// 배치**하면 임포터가 자기 텍스처를 영구 바인딩한다(재임포트 내성 — 서브어셈블리 직접 수정과 달리 지속).
/// 절차: ① Textures/&lt;stem&gt;/의 base_color/normal을 Npc2026/&lt;stem&gt;/로 복제(부재 시)
///       ② 복제본 임포터 설정 — base: Standalone BC7/4096/q100+mip, normal: NormalMap+sRGB off+4096/q100
///          (무조건 고품질 계약 — [141차 P1]과 동일 규격)
///       ③ rigged FBX SaveAndReimport → 이름매칭 자기 바인딩
///       ④ 검증: 서브어셈블리 머티리얼 _MainTex/_BaseMap/_BumpMap path 덤프
/// 실행: batchmode -executeMethod Npc141BindingFix.FixAll -logFile C:/Unity/code/TestOutput/npc141_binding_unity.log
/// 산출: TestOutput/npc141_binding_fix.txt
/// 멱등: 복제본 존재 시 설정만 재확인, FBX 재임포트는 반복 무해.
/// </summary>
public static class Npc141BindingFix
{
    private const string Root = "Assets/Resources/Models/UserProvided/Npc2026";

    public static void FixAll()
    {
        var log = new StringBuilder();
        log.AppendLine("[141차 B2] 교차바인딩 에셋 수리 시작 — " + System.DateTime.Now.ToString("HH:mm:ss"));
        AssetDatabase.Refresh();

        var stems = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Root }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (!p.EndsWith("_rigged.fbx")) continue;
            string stem = Path.GetFileName(Path.GetDirectoryName(p));
            if (!string.IsNullOrEmpty(stem) && !stems.Contains(stem)) stems.Add(stem);
        }
        stems.Sort();
        log.AppendLine($"[141차 B2] 대상 캐릭터 {stems.Count}종");

        int copied = 0, configured = 0;
        foreach (string stem in stems)
        {
            string srcBase = $"{Root}/Textures/{stem}/base_color.png";
            string srcNorm = $"{Root}/Textures/{stem}/normal.png";
            string dstBase = $"{Root}/{stem}/base_color.png";
            string dstNorm = $"{Root}/{stem}/normal.png";

            if (!File.Exists(srcBase) || !File.Exists(srcNorm))
            {
                log.AppendLine($"[141차 B2] FAIL 추출 텍스처 부재 — {stem}");
                continue;
            }
            if (!File.Exists(dstBase))
            {
                File.Copy(srcBase, dstBase);
                copied++;
                log.AppendLine($"[141차 B2] 복제 — {dstBase}");
            }
            if (!File.Exists(dstNorm))
            {
                File.Copy(srcNorm, dstNorm);
                copied++;
                log.AppendLine($"[141차 B2] 복제 — {dstNorm}");
            }
        }
        AssetDatabase.Refresh();   // 복제본 인식

        foreach (string stem in stems)
        {
            string dstBase = $"{Root}/{stem}/base_color.png";
            string dstNorm = $"{Root}/{stem}/normal.png";
            ConfigureAlbedo(dstBase, log, ref configured);
            ConfigureNormal(dstNorm, log, ref configured);
        }

        // ③ rigged FBX 재임포트 — 이름매칭이 같은 폴더 자기 텍스처로 해결되도록
        int reimported = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Root }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (!p.EndsWith("_rigged.fbx")) continue;
            if (AssetImporter.GetAtPath(p) is ModelImporter mi)
            {
                mi.SaveAndReimport();
                reimported++;
            }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        log.AppendLine($"[141차 B2] 복제={copied} 설정={configured} FBX재임포트={reimported}");

        // ④ 검증 덤프
        int selfBound = 0, crossBound = 0;
        foreach (string stem in stems)
        {
            string fbxPath = $"{Root}/{stem}/{stem}_rigged.fbx";
            var mats = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Material>().ToArray();
            foreach (var mat in mats)
            {
                string mainPath = mat.mainTexture != null ? AssetDatabase.GetAssetPath(mat.mainTexture) : "(null)";
                string bumpPath = (mat.HasProperty("_BumpMap") && mat.GetTexture("_BumpMap") != null)
                    ? AssetDatabase.GetAssetPath(mat.GetTexture("_BumpMap")) : "(null)";
                bool self = mainPath.Contains($"/{stem}/");
                bool selfBump = bumpPath.Contains($"/{stem}/") || bumpPath == "(null)";
                if (self && selfBump) selfBound++; else crossBound++;
                log.AppendLine($"[141차 B2] {stem} '{mat.name}' main={mainPath} bump={bumpPath} {(self && selfBump ? "SELF-OK" : "CROSS!")}");
            }
        }
        log.AppendLine($"[141차 B2] DONE — 자기바인딩={selfBound} 교차잔존={crossBound}");
        Debug.Log(log.ToString());
        File.WriteAllText("TestOutput/npc141_binding_fix.txt", log.ToString());
    }

    /// <summary>[141차 P1 규격] 알베도 — Standalone BC7/4096/q100 + mip(textureFormat 직접 세터 폐기 회피).</summary>
    private static void ConfigureAlbedo(string path, StringBuilder log, ref int configured)
    {
        if (!File.Exists(path)) return;
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        var ps = imp.GetPlatformTextureSettings("Standalone");
        if (!ps.overridden) ps = imp.GetDefaultPlatformTextureSettings();
        bool changed = false;
        if (ps.format != TextureImporterFormat.BC7) { ps.format = TextureImporterFormat.BC7; changed = true; }
        if (ps.maxTextureSize != 4096) { ps.maxTextureSize = 4096; changed = true; }
        if (ps.compressionQuality != 100) { ps.compressionQuality = 100; changed = true; }
        if (changed) { imp.SetPlatformTextureSettings(ps); imp.SaveAndReimport(); configured++; }
    }

    /// <summary>[141차 P1 규격] 노멀 — NormalMap 타입 + sRGB off + 4096/q100(포맷 자동 BC5).</summary>
    private static void ConfigureNormal(string path, StringBuilder log, ref int configured)
    {
        if (!File.Exists(path)) return;
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        bool changed = false;
        if (imp.textureType != TextureImporterType.NormalMap) { imp.textureType = TextureImporterType.NormalMap; changed = true; }
        if (imp.sRGBTexture) { imp.sRGBTexture = false; changed = true; }
        if (imp.maxTextureSize != 4096) { imp.maxTextureSize = 4096; changed = true; }
        if (imp.compressionQuality != 100) { imp.compressionQuality = 100; changed = true; }
        if (changed) { imp.SaveAndReimport(); configured++; }
    }
}
