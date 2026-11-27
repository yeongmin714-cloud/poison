using UnityEngine;
using System.Reflection;
using System.IO;
using ProjectName.Core;
using ProjectName.UI;

/// <summary>
/// [209차-A] 코드 수정 전 현재 파이프라인 원본 아이콘 베이크 — wood_sword 1종 before 스냅샷.
/// UI 실제 베이크 경로(GblItemIconRenderer.SyncBakeItem → SyncBakeFromPrefab) 그대로 호출.
/// 키 산출은 IconBakeAll194 방식(PlayerInventory 정적 → item.id 기준 ResolveModelKey) 재사용.
/// 근원 특정 전용 — 런타임/게임 코드 수정 없음(에디터 프로브 신설만).
/// 사용: Unity.exe -batchmode -projectPath C:\Unity\code -executeMethod IconCompare209.Run -logFile ... -quit
/// </summary>
public static class IconCompare209
{
    private const string OutDir = "TestOutput/icon_compare209";
    private const string TargetId = "weapon_sword_wood";   // wood_sword GLB 아이콘의 item.id
    private const string FallbackKey = "wood_sword";
    private const string FallbackGlb = "Assets/Resources/Models/UserProvided/wood_sword.glb";

    public static void Run()
    {
        Directory.CreateDirectory(OutDir);
        var log = new System.Text.StringBuilder();

        var miSync = typeof(GblItemIconRenderer).GetMethod("SyncBakeItem",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (miSync == null)
        {
            File.WriteAllText(Path.Combine(OutDir, "facts.log"), "FAIL: SyncBakeItem reflection null\n");
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
            File.WriteAllText(Path.Combine(OutDir, "facts.log"), log.ToString());
            return;
        }

        string png = Path.Combine(OutDir, "wood_sword_before.png");
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
        File.WriteAllText(Path.Combine(OutDir, "facts.log"), log.ToString());
        Debug.Log($"[IconCompare209] 완료 — {png}");
    }
}
