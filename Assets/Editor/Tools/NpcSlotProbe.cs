using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [134차 P1-4 선행] NPC 머티리얼 슬롯 매핑 실측 프로브(배치모드, 읽기전용).
    /// 목적: NPC 피부 "깨짐" 원인 판정 — ①알베도 자리에 normal 텍스처가 들어갔는지(슬롯 오배치/교차 바인딩)
    /// ②머티리얼 부재/텍스처 미바인딩 ③tiling/scale 이상 ④셰이더 폴백(InternalErrorShader) 유무.
    /// 대상: Assets/Resources/Models/UserProvided/Npc2026 각 rigged FBX 전부.
    /// 측정: ①FBX 루트 하위 MeshRenderer/SkinnedMeshRenderer 전수 ②FBX Material 서브어셈 전수
    /// ③FBX 내장 Texture2D 전수. 각 머티리얼: 이름/셰이더/mainTexture(이름+크기)/normal 슬롯 텍스처/
    /// 색상/tiling·offset. 판정은 실측값만 기록(수정 없음, SetDirty/SaveAssets 호출 없음).
    /// 실행: batchmode -executeMethod ProjectName.EditorTools.NpcSlotProbe.DumpAll
    /// 출력: TestOutput/npc_slot_probe.txt + 콘솔 "[134차 슬롯프로브] DONE".
    /// </summary>
    public static class NpcSlotProbe
    {
        private const string Root = "Assets/Resources/Models/UserProvided/Npc2026";

        [MenuItem("Tools/Anim/Npc2026 Slot Probe (Read-only Dump)")]
        public static void DumpAll()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== [134차 P1-4] NPC 머티리얼 슬롯 매핑 프로브 결과 ===");
            sb.AppendLine($"생성(UTC): {System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Unity: {Application.unityVersion}");
            sb.AppendLine();

            var guids = AssetDatabase.FindAssets("t:Model", new[] { Root })
                .Select(g => AssetDatabase.GUIDToAssetPath(g))
                .Where(p => p.EndsWith("_rigged.fbx"))
                .OrderBy(p => p, System.StringComparer.OrdinalIgnoreCase)
                .ToList();

            sb.AppendLine($"rigged FBX 총 {guids.Count}개");
            sb.AppendLine();

            int totalSwapSuspect = 0, totalMissingMat = 0, totalUnboundAlbedo = 0, totalShaderError = 0, totalTilingOdd = 0;

            foreach (string fbxPath in guids)
            {
                sb.AppendLine("###############################################");
                sb.AppendLine($"# FBX: {fbxPath}");
                sb.AppendLine("###############################################");

                var root = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (root == null)
                {
                    sb.AppendLine("  !! FBX 루트 로드 실패");
                    sb.AppendLine();
                    continue;
                }

                // ---- ① 렌더러 전수(루트 GameObject 하위, 비활성 포함) ----
                var renderers = root.GetComponentsInChildren<Renderer>(true);
                sb.AppendLine($"[렌더러] 총 {renderers.Length}개");
                foreach (var r in renderers)
                {
                    string path = GetPath(r.transform);
                    string type = r.GetType().Name;
                    var mats = r.sharedMaterials;
                    int missing = mats.Count(m => m == null);
                    if (missing > 0) totalMissingMat += missing;

                    sb.AppendLine($"  - R {type} \"{path}\"  머티리얼 {mats.Length}개 (null/missing {missing}개)");
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var m = mats[i];
                        if (m == null)
                        {
                            sb.AppendLine($"      [{i}] = NULL (missing material 슬롯!)");
                            continue;
                        }
                        sb.AppendLine(DumpMaterial(m, "      ", ref totalSwapSuspect, ref totalUnboundAlbedo, ref totalShaderError, ref totalTilingOdd));
                    }
                }

                // ---- ② Material 서브어셈 전수 ----
                var subMats = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Material>().ToList();
                sb.AppendLine($"[머티리얼 서브어셈] 총 {subMats.Count}개");
                foreach (var m in subMats)
                    sb.AppendLine(DumpMaterial(m, "  - ", ref totalSwapSuspect, ref totalUnboundAlbedo, ref totalShaderError, ref totalTilingOdd));

                // ---- ③ FBX 내장 텍스처 전수 ----
                var texs = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Texture2D>().ToList();
                sb.AppendLine($"[내장 텍스처] 총 {texs.Count}개");
                foreach (var t in texs)
                    sb.AppendLine($"  - T \"{t.name}\" {t.width}x{t.height} format={t.format}");

                sb.AppendLine();
            }

            sb.AppendLine("=== 요약 ===");
            sb.AppendLine($"FBX {guids.Count}개 / 알베도에 normal 의심(mainTexture 이름 'normal') {totalSwapSuspect}건");
            sb.AppendLine($"missing(null) 머티리얼 슬롯 {totalMissingMat}건");
            sb.AppendLine($"알베도 미바인딩(mainTexture NULL) {totalUnboundAlbedo}건");
            sb.AppendLine($"InternalErrorShader(셰이더 폴백) {totalShaderError}건");
            sb.AppendLine($"tiling != (1,1) {totalTilingOdd}건");
            sb.AppendLine("[134차 슬롯프로브] DONE");

            // 출력 파일
            string projRoot = Application.dataPath.Substring(0, Application.dataPath.Length - "/Assets".Length);
            string outDir = Path.Combine(projRoot, "TestOutput");
            Directory.CreateDirectory(outDir);
            string outFile = Path.Combine(outDir, "npc_slot_probe.txt");
            File.WriteAllText(outFile, sb.ToString(), new UTF8Encoding(false));

            Debug.Log($"[134차 슬롯프로브] DONE — 리포트: {outFile} / FBX {guids.Count}개, " +
                      $"normal의심 {totalSwapSuspect}, missing {totalMissingMat}, 미바인딩 {totalUnboundAlbedo}, " +
                      $"셰이더에러 {totalShaderError}, tiling이상 {totalTilingOdd}");
        }

        private static string DumpMaterial(Material m, string indent,
            ref int swapSuspect, ref int unbound, ref int shaderErr, ref int tilingOdd)
        {
            var b = new StringBuilder();
            string shaderName = m.shader != null ? m.shader.name : "(null shader)";
            bool isErrShader = shaderName.Contains("InternalErrorShader");
            if (isErrShader) shaderErr++;

            var colMain = m.HasProperty("_Color") ? m.GetColor("_Color") : (Color?)null;
            var colBase = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : (Color?)null;

            Texture2D mainTex = m.mainTexture as Texture2D;
            string mainTexInfo = mainTex != null
                ? $"\"{mainTex.name}\" {mainTex.width}x{mainTex.height} fmt={mainTex.format}"
                : "NULL(미바인딩)";
            if (mainTex == null) unbound++;

            bool normalInAlbedo = mainTex != null && mainTex.name.ToLowerInvariant().Contains("normal");
            if (normalInAlbedo) swapSuspect++;

            var sbT = new StringBuilder();
            var scale = m.mainTexture != null ? m.mainTextureScale : Vector2.zero;
            var offset = m.mainTexture != null ? m.mainTextureOffset : Vector2.zero;
            if (m.mainTexture != null && (scale != Vector2.one || offset != Vector2.zero)) tilingOdd++;

            // normal/bump 계열 슬롯 전수 조사
            var bumpSlots = new List<string>();
            if (m.shader != null)
            {
                int n = ShaderUtil.GetPropertyCount(m.shader);
                for (int i = 0; i < n; i++)
                {
                    if (ShaderUtil.GetPropertyType(m.shader, i) != ShaderUtil.ShaderPropertyType.TexEnv) continue;
                    string pn = ShaderUtil.GetPropertyName(m.shader, i);
                    if (!m.HasProperty(pn)) continue;
                    var t = m.GetTexture(pn) as Texture2D;
                    if (t == null) continue;
                    bool isBumpSlot = pn.ToLowerInvariant().Contains("bump") || pn.ToLowerInvariant().Contains("normal");
                    string line = $"\"{pn}\"←\"{t.name}\" {t.width}x{t.height}{(isBumpSlot ? "" : " (기타텍슬롯)")}";
                    if (isBumpSlot)
                    {
                        bumpSlots.Add(line);
                        // 역방향 오배치: normal 슬롯에 알베도 후보 텍스처가 들어간 경우
                        string tn = t.name.ToLowerInvariant();
                        if (tn.Contains("base_color") || tn.Contains("albedo") || tn.Contains("diffuse"))
                            bumpSlots.Add($"      !! 역오배치의심: normal 슬롯({pn})에 알베도 후보 \"{t.name}\" 바인딩됨");
                    }
                    else
                    {
                        sbT.Append(line).Append("; ");
                    }
                }
            }

            string verdict = normalInAlbedo ? "  <-- !! 알베도 슬롯에 normal 텍스처 의심(슬롯오배치)" : "";
            b.AppendLine($"{indent}[M] \"{m.name}\" shader=\"{shaderName}\"{(isErrShader ? "  <-- !! 셰이더폴백(InternalErrorShader)" : "")}");
            b.AppendLine($"{indent}    mainTexture: {mainTexInfo}{verdict}");
            b.AppendLine($"{indent}    main tiling/scale=({scale.x},{scale.y}) offset=({offset.x},{offset.y})");
            b.AppendLine($"{indent}    color _Color={(colMain.HasValue ? Col(colMain.Value) : "-")} _BaseColor={(colBase.HasValue ? Col(colBase.Value) : "-")}");
            b.AppendLine($"{indent}    normal/bump 슬롯: {(bumpSlots.Count > 0 ? string.Join(" | ", bumpSlots) : "(없음)")}");
            if (sbT.Length > 0) b.AppendLine($"{indent}    기타 텍스처 슬롯: {sbT}");
            return b.ToString();
        }

        private static string Col(Color c) => $"RGBA({c.r:F3},{c.g:F3},{c.b:F3},{c.a:F3})";

        private static string GetPath(Transform t)
        {
            var parts = new List<string>();
            while (t != null) { parts.Insert(0, t.name); t = t.parent; }
            return string.Join("/", parts);
        }
    }
}
