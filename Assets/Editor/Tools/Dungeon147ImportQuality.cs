using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [147차 P-A3] 던전 팩 임포트 품질 — 141차 텍스처 계약(원본해상도·BC7·q100) 적용/검증.
    /// 실측 근거: 던전 GLB 320종은 텍스처를 GLB에 임베드([141차] GLB임베드=포프로 계약 — 추출 금지).
    /// ModelImporter는 임베드 텍스처의 개별 압축 설정 API가 없으므로 본 스크립트는:
    ///   ①스탠드얼론 아틀라스(New Atlas.png 등) → TextureImporter로 BC7+quality100 적용(141차 패턴 준용)
    ///   ②임베드 텍스처는 서브에셋 실측 리포트(해상도·포맷) — 원본 이상 축소 여부만 판정(수정 없음)
    /// 멱등 — 재실행 안전. Random 0건.
    /// </summary>
    public static class Dungeon147ImportQuality
    {
        private const string Tag = "[147차 P-A3]";
        private const string Root = "Assets/Resources/Models/Dungeon";

        [MenuItem("Tools/Dungeon/147 임포트 품질 적용·검증")]
        public static void RunFromMenu() => RunAll();

        public static void RunAll()
        {
            int texChanged = 0, texOk = 0, glbChecked = 0, shrunk = 0;
            var lines = new List<string> { $"{Tag} === 던전 임포트 품질 시작 ===" };

            // ── ① 스탠드얼론 텍스처 — TextureImporter 계약 적용(Npc141TextureQuality 패턴 준용) ──
            foreach (string path in Directory.GetFiles(Root, "*.png", SearchOption.AllDirectories))
            {
                string p = path.Replace('\\', '/');
                var imp = AssetImporter.GetAtPath(p) as TextureImporter;
                if (imp == null) continue;
                bool changed = false;
                if (imp.maxTextureSize < 4096) { imp.maxTextureSize = 4096; changed = true; }   // 원본해상도 보존 상한
                if (imp.textureCompression != TextureImporterCompression.Compressed) { imp.textureCompression = TextureImporterCompression.Compressed; changed = true; }
                var ps = imp.GetPlatformTextureSettings("Standalone");
                if (ps.format != TextureImporterFormat.BC7) { ps.format = TextureImporterFormat.BC7; ps.compressionQuality = 100; imp.SetPlatformTextureSettings(ps); changed = true; }
                if (changed) { imp.SaveAndReimport(); texChanged++; Debug.Log($"{Tag} 텍스처 계약 적용 — {p}"); }
                else texOk++;
            }

            // ── ② GLB 임베드 텍스처 검증 리포트 — 포프로 계약(수정 없음, 축소 판정만) ──
            foreach (string path in Directory.GetFiles(Root, "*.glb", SearchOption.AllDirectories))
            {
                string p = path.Replace('\\', '/');
                glbChecked++;
                foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(p))
                {
                    var tex = obj as Texture2D;
                    if (tex == null) continue;
                    int w = tex.width, h = tex.height;
                    if (w > 2048 || h > 2048) shrunk++;
                    lines.Add($"embed {Path.GetFileName(p)} :: {tex.name} {w}x{h}");
                }
            }

            lines.Add($"{Tag} DONE — 텍스처 변경 {texChanged}/기적용 {texOk}, GLB 검증 {glbChecked}, 2048 초과(축소 의심) {shrunk}");
            Debug.Log(string.Join("\n", lines));
            File.WriteAllLines(Path.Combine(Application.dataPath, "..", "TestOutput", "147_dungeon_import_quality.txt"), lines);
        }
    }
}
