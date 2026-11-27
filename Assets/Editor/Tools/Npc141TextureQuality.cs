using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [141차 P1/P2] NPC 텍스처 품질 계약 도구 — **무조건 고품질**(사용자 확정 계약).
/// 배경: 현 데스크톱 = 테스트씬 전용 확인 환경, 메인씬 판정 = 고사양 데스크톱 →
/// VRAM 절약 다운스케일/압축 절충을 하지 않고 원본 품질을 그대로 사용한다(사용자 지시 141차).
/// 기존 상태(실측): base_color 원본 4096² → maxSize 2048 다운스케일 + compressionQuality 50 +
/// 자동 압축(BC1) = 어두운 그라데이션 4×4 블록 아티팩트(인게임 "피부 깨짐"의 유력 원인).
/// normal.png는 textureType=Default(색상 취급)로 임포트돼 _BumpMap 샘플링 오염(부가 결함).
///
/// 적용(base_color 24장): Standalone 플랫폼 설정 maxSize 4096(원본 유지) + BC7 + compressionQuality 100 + mipmap.
/// 적용(normal 24장): textureType NormalMap + sRGB off + maxSize 4096 + compressionQuality 100(포맷 자동 BC5).
/// 실행: Unity.exe -batchmode -projectPath C:/Unity/code -executeMethod Npc141TextureQuality.RunPatch -quit -logFile C:/Unity/code/TestOutput/npc141_tq_unity.log
/// 산출: TestOutput/npc141_texture_quality.txt
/// </summary>
public static class Npc141TextureQuality
{
    public static void RunPatch()
    {
        var log = new StringBuilder();
        log.AppendLine("[141차] 무조건 고품질 텍스처 계약 적용 시작 — " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        Directory.CreateDirectory("TestOutput");
        AssetDatabase.Refresh();

        string root = "Assets/Resources/Models/UserProvided/Npc2026/Textures";
        string[] pngs = Directory.GetFiles(root, "*.png", SearchOption.AllDirectories);
        int baseOk = 0, normalOk = 0, skip = 0;
        foreach (string rawPath in pngs)
        {
            string path = rawPath.Replace('\\', '/');
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) { log.AppendLine("[141차] FAIL importer null: " + path); skip++; continue; }

            bool isNormal = path.ToLowerInvariant().EndsWith("normal.png");
            bool changed = false;
            if (isNormal)
            {
                if (imp.textureType != TextureImporterType.NormalMap) { imp.textureType = TextureImporterType.NormalMap; changed = true; }
                if (imp.sRGBTexture) { imp.sRGBTexture = false; changed = true; }   // 노멀맵은 선형 데이터
                if (imp.maxTextureSize != 4096) { imp.maxTextureSize = 4096; changed = true; }
                if (imp.compressionQuality != 100) { imp.compressionQuality = 100; changed = true; }
                if (changed) { imp.SaveAndReimport(); normalOk++; }
                else skip++;
                log.AppendLine($"[141차] normal {Path.GetFileName(Path.GetDirectoryName(path))}: NormalMap/4096/q100 {(changed ? "적용" : "이미 준수")}");
            }
            else
            {
                // [141차] BC7은 플랫폼 설정 API로 지정(textureFormat 직접 세터는 폐기됨 — CS0618).
                var ps = imp.GetPlatformTextureSettings("Standalone");
                if (!ps.overridden)
                    ps = imp.GetDefaultPlatformTextureSettings();
                bool psChanged = false;
                if (ps.format != TextureImporterFormat.BC7) { ps.format = TextureImporterFormat.BC7; psChanged = true; }
                if (ps.maxTextureSize != 4096) { ps.maxTextureSize = 4096; psChanged = true; }
                if (ps.compressionQuality != 100) { ps.compressionQuality = 100; psChanged = true; }
                if (psChanged)
                {
                    imp.SetPlatformTextureSettings(ps);
                    changed = true;
                }
                if (!imp.mipmapEnabled) { imp.mipmapEnabled = true; changed = true; }
                if (changed) { imp.SaveAndReimport(); baseOk++; }
                else skip++;
                log.AppendLine($"[141차] base {Path.GetFileName(Path.GetDirectoryName(path))}: BC7/4096/q100/mip {(changed ? "적용" : "이미 준수")}");
            }
        }
        log.AppendLine($"[141차] DONE — base 재임포트={baseOk} normal 재임포트={normalOk} 준수-스킵={skip}");
        Debug.Log(log.ToString());
        File.WriteAllText("TestOutput/npc141_texture_quality.txt", log.ToString());
    }
}
