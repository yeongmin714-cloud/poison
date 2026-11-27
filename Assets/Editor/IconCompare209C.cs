using UnityEngine;
using System.Reflection;
using System.IO;
using ProjectName.Core;
using ProjectName.UI;

/// <summary>
/// [209차-C] 3패널 비교 — 중간/최종 패널 베이크 전용 프로브 (before는 209차-A 산출물 재사용, 본 프로브 미재생성).
/// - RunAfter13 : 신규 파이프라인([209차 B] 조명 0.60/0.10/0.45 + 후처리 비네트0.15·콘트라스트/채도1.05) × 원본 GLB → wood_sword_after13.png
/// - RunFull    : 동일 파이프라인 × AO 수술 GLB(COLOR_0 주입 후) → wood_sword_full.png
/// UI 실제 베이크 경로(GblItemIconRenderer.SyncBakeItem) 그대로 호출 — IconCompare209(A단계) 동일 패턴.
/// 런타임/게임 코드 수정 없음(에디터 프로브 신설만). 커밋 금지 세션 산출물.
/// 사용: Unity.exe -batchmode -projectPath C:\Unity\code -executeMethod IconCompare209C.RunAfter13 -logFile ... -quit
/// </summary>
public static class IconCompare209C
{
    private const string OutDir = "TestOutput/icon_compare209";
    private const string TargetId = "weapon_sword_wood";   // wood_sword GLB 아이콘의 item.id
    private const string FallbackKey = "wood_sword";
    private const string FallbackGlb = "Assets/Resources/Models/UserProvided/wood_sword.glb";

    public static void RunAfter13() { Run("wood_sword_after13.png", "after13"); }
    public static void RunFull() { Run("wood_sword_full.png", "full"); }

    // [209차-E] Polyfork anvil — GLB 직결 마운트 패턴(AssetDatabase 로드, 물고기 28차 선례).
    // before = AO 수술 전 원본 GLB, after = vertex_ao_bake.bake_file COLOR_0 곱셈 후 GLB.
    // 배치 시작 시 GLB 자동 재임포트 + 아이콘 캐시 세션 전용 → 신선 베이크 보장.
    public static void RunAnvilBefore() { RunAnvil("polyfork_anvil_before.png", "anvil_before"); }
    public static void RunAnvilAfter() { RunAnvil("polyfork_anvil_full.png", "anvil_after"); }

    private static void RunAnvil(string outName, string tag)
    {
        Directory.CreateDirectory(OutDir);
        var log = new System.Text.StringBuilder();

        var miSync = typeof(GblItemIconRenderer).GetMethod("SyncBakeItem",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (miSync == null)
        {
            File.WriteAllText(Path.Combine(OutDir, $"facts_209e_{tag}.log"), "FAIL: SyncBakeItem reflection null\n");
            return;
        }

        const string id = "polyfork_anvil";    // 캐시/노드 명명용 — 아이템 테이블 비의존
        const string key = "anvil-1286cb";     // modelKey — 방향 오버라이드/은닉노드 미등록(무동작)
        const string glb = "Assets/Resources/Models/Polyfork/anvil-1286cb.glb";
        log.AppendLine($"RESOLVED: id={id} key={key} glb={glb}");

        // GLB 직결 1회 호출 — SyncBakeItem은 glbPath 우선(AssetDatabase.LoadAssetAtPath)
        Texture2D tex = null;
        try { tex = (Texture2D)miSync.Invoke(null, new object[] { id, key, glb }); }
        catch (System.Exception e) { log.AppendLine($"EXC(glb): {e.GetType().Name}: {e.Message}"); }

        if (tex == null)
        {
            log.AppendLine("MISS: anvil bake=null");
            log.AppendLine("DONE");
            File.WriteAllText(Path.Combine(OutDir, $"facts_209e_{tag}.log"), log.ToString());
            return;
        }

        string png = Path.Combine(OutDir, outName);
        File.WriteAllBytes(png, tex.EncodeToPNG());

        // 평균색 실측(중앙 1/3 영역) — wood_sword 프로브 동일 패턴
        Color sum = Color.clear; int n = 0;
        for (int y = tex.height / 3; y < tex.height * 2 / 3; y++)
            for (int x = tex.width / 3; x < tex.width * 2 / 3; x++)
            { sum += tex.GetPixel(x, y); n++; }
        Color avg = sum / n;
        log.AppendLine($"OK: {id} key={key} size={tex.width}x{tex.height} avgRGB=({avg.r:F2},{avg.g:F2},{avg.b:F2}) png={png}");
        Object.DestroyImmediate(tex);

        log.AppendLine("DONE");
        File.WriteAllText(Path.Combine(OutDir, $"facts_209e_{tag}.log"), log.ToString());
        Debug.Log($"[IconCompare209C:{tag}] 완료 — {png}");
    }

    private static void Run(string outName, string tag)
    {
        Directory.CreateDirectory(OutDir);
        var log = new System.Text.StringBuilder();

        var miSync = typeof(GblItemIconRenderer).GetMethod("SyncBakeItem",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (miSync == null)
        {
            File.WriteAllText(Path.Combine(OutDir, $"facts_209c_{tag}.log"), "FAIL: SyncBakeItem reflection null\n");
            return;
        }

        // [IconBakeAll194 방식] PlayerInventory 정적에서 item.id 기준으로 실제 ItemData를 찾아 키 산출
        string id = null, key = null;
        foreach (var f in typeof(PlayerInventory).GetFields(
                     BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
        {
            if (id != null) break;
            if (f.FieldType == typeof(PlayerInventory.ItemData))
            {
                if (f.GetValue(null) is PlayerInventory.ItemData it && it.id == TargetId)
                { id = it.id; key = GblItemIconRenderer.ResolveModelKey(it); }
            }
            else if (f.FieldType == typeof(PlayerInventory.ItemData[]))
            {
                if (f.GetValue(null) is PlayerInventory.ItemData[] arr)
                    foreach (var it in arr)
                    {
                        if (it != null && it.id == TargetId)
                        { id = it.id; key = GblItemIconRenderer.ResolveModelKey(it); break; }
                    }
            }
        }
        if (id == null)
        {
            id = TargetId; key = FallbackKey;
            log.AppendLine("FALLBACK: PlayerInventory 정적에서 미발견 — 하드코딩 키 사용");
        }
        log.AppendLine($"RESOLVED: id={id} key={key}");

        // UI 경로와 동일: 무기는 glbPath=null → modelKey로 Resources.Load("Models/UserProvided/<key>")
        Texture2D tex = null;
        try { tex = (Texture2D)miSync.Invoke(null, new object[] { id, key, null }); }
        catch (System.Exception e) { log.AppendLine($"EXC(null-glb): {e.GetType().Name}: {e.Message}"); }
        if (tex == null)
        {
            // 프로브 견고성 폴백 — GLB 직결(AssetDatabase) 1회 재시도(로그 명시)
            log.AppendLine("RETRY: glbPath 직결");
            try { tex = (Texture2D)miSync.Invoke(null, new object[] { id, key, FallbackGlb }); }
            catch (System.Exception e) { log.AppendLine($"EXC(glb): {e.GetType().Name}: {e.Message}"); }
        }

        if (tex == null)
        {
            log.AppendLine("MISS: wood_sword bake=null");
            log.AppendLine("DONE");
            File.WriteAllText(Path.Combine(OutDir, $"facts_209c_{tag}.log"), log.ToString());
            return;
        }

        string png = Path.Combine(OutDir, outName);
        File.WriteAllBytes(png, tex.EncodeToPNG());

        // 평균색 실측(중앙 1/3 영역) — 193/194 프로브 동일 패턴
        Color sum = Color.clear; int n = 0;
        for (int y = tex.height / 3; y < tex.height * 2 / 3; y++)
            for (int x = tex.width / 3; x < tex.width * 2 / 3; x++)
            { sum += tex.GetPixel(x, y); n++; }
        Color avg = sum / n;
        log.AppendLine($"OK: {id} key={key} size={tex.width}x{tex.height} avgRGB=({avg.r:F2},{avg.g:F2},{avg.b:F2}) png={png}");
        Object.DestroyImmediate(tex);

        log.AppendLine("DONE");
        File.WriteAllText(Path.Combine(OutDir, $"facts_209c_{tag}.log"), log.ToString());
        Debug.Log($"[IconCompare209C:{tag}] 완료 — {png}");
    }
}
