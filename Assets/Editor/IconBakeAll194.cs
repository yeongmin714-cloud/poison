using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.IO;
using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Systems;
using ProjectName.UI;

/// <summary>
/// [194차 P3] 전 아이템 정면 아이콘 전수 베이크 스냅샷 — 실제 UI 베이크 경로
/// (GblItemIconRenderer.SyncBakeItem → SyncBakeFromPrefab) 그대로 호출해 PNG 저장.
/// 대상: ①PlayerInventory 공개/비공개 정적 ItemData 필드 + ItemData[] 배열(AllBombs/AllTieredGear 포함)
///       ②어종 지명종(NamedKeySet — 사용자가 실명으로 보는 38종, fish_glb_ 규약)
///       ③요리 GLB 버킷(RecipeCatalog dish_ id → DishGlbCatalog.ModelKey 버킷별 대표 1종 — 실틴트 적용 검증)
/// 근원 특정/프레이밍 판독 전용 — 코드 수정 없음. GREENISH 마커 = 초록 틴트 의심 자동 플래그.
/// 사용: Unity.exe -batchmode -projectPath C:\Unity\code -executeMethod IconBakeAll194.Run -quit
/// </summary>
public static class IconBakeAll194
{
    private const string OutDir = "TestOutput/icon_bake_all_194";

    public static void Run()
    {
        Directory.CreateDirectory(OutDir);
        var sb = new System.Text.StringBuilder();

        var miSync = typeof(GblItemIconRenderer).GetMethod("SyncBakeItem",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (miSync == null)
        {
            File.WriteAllText(Path.Combine(OutDir, "facts.log"), "FAIL: SyncBakeItem reflection null\n");
            return;
        }

        var jobs = new List<(string id, string key, string glb)>();
        var seen = new HashSet<string>();

        // 1) 코어 아이템 — PlayerInventory 정적 ItemData / ItemData[]
        foreach (var f in typeof(PlayerInventory).GetFields(
                     BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
        {
            if (f.FieldType == typeof(PlayerInventory.ItemData))
            {
                if (f.GetValue(null) is PlayerInventory.ItemData it
                    && !string.IsNullOrEmpty(it.id) && seen.Add(it.id))
                {
                    string key = GblItemIconRenderer.ResolveModelKey(it);
                    jobs.Add((it.id, key, null));
                }
            }
            else if (f.FieldType == typeof(PlayerInventory.ItemData[]))
            {
                if (f.GetValue(null) is PlayerInventory.ItemData[] arr)
                {
                    foreach (var it in arr)
                    {
                        if (it != null && !string.IsNullOrEmpty(it.id) && seen.Add(it.id))
                        {
                            string key = GblItemIconRenderer.ResolveModelKey(it);
                            jobs.Add((it.id, key, null));
                        }
                    }
                }
            }
        }

        // 2) 어종 — 지명종(NamedKeySet)만, id는 fish_glb_ 규약, GLB 직결 경로
        var nks = typeof(FishCatalog).GetField("NamedKeySet",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (nks != null && nks.GetValue(null) is string[] named)
        {
            foreach (var k in named)
            {
                string id = "fish_glb_" + k;
                if (seen.Add(id))
                    jobs.Add((id, null, FishCatalog.GlbPathByKey(k)));
            }
        }

        // 3) 요리 — RecipeCatalog dish_ id → ModelKey 버킷별 대표 1종(실제 레시피 id라 틴트까지 실측)
        var recipesProp = typeof(RecipeCatalog).GetProperty("Recipes",
            BindingFlags.Public | BindingFlags.Static)
            ?? (MemberInfo)typeof(RecipeCatalog).GetField("Recipes",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (recipesProp != null)
        {
            object recipesVal = recipesProp is PropertyInfo pi
                ? pi.GetValue(null)
                : ((FieldInfo)recipesProp).GetValue(null);
            if (recipesVal is System.Collections.IEnumerable recipes)
            {
                var bucketSeen = new HashSet<string>();
                foreach (var r in recipes)
                {
                    var t = r.GetType();
                    string rid = t.GetField("id")?.GetValue(r) as string;
                    if (string.IsNullOrEmpty(rid) || !rid.StartsWith("dish_", System.StringComparison.Ordinal))
                        continue;
                    string bucket = DishGlbCatalog.ModelKey(rid);
                    if (string.IsNullOrEmpty(bucket) || !bucketSeen.Add(bucket))
                        continue;
                    jobs.Add((rid, bucket, null));
                }
            }
        }

        jobs.Sort((a, b) => string.CompareOrdinal(a.id, b.id));

        int ok = 0, miss = 0, fb = 0;
        foreach (var (id, key, glb) in jobs)
        {
            if (key == null && string.IsNullOrEmpty(glb))
            {
                sb.AppendLine($"FALLBACK: {id} — GLB 없음(절차 아이콘 경로)");
                fb++;
                continue;
            }
            Texture2D tex = null;
            try { tex = (Texture2D)miSync.Invoke(null, new object[] { id, key, glb }); }
            catch (System.Exception e) { sb.AppendLine($"EXC: {id} {e.GetType().Name}: {e.Message}"); }
            if (tex == null)
            {
                sb.AppendLine($"MISS: {id} key={key} glb={glb}");
                miss++;
                continue;
            }

            string safeId = id.Replace('/', '_');
            File.WriteAllBytes(Path.Combine(OutDir, safeId + ".png"), tex.EncodeToPNG());
            Color sum = Color.clear; int n = 0;
            for (int y = tex.height / 3; y < tex.height * 2 / 3; y++)
                for (int x = tex.width / 3; x < tex.width * 2 / 3; x++)
                { sum += tex.GetPixel(x, y); n++; }
            Color avg = sum / n;
            bool greenish = avg.g > avg.r + 0.04f && avg.g > avg.b + 0.04f;
            sb.AppendLine($"OK: {id} key={key} size={tex.width}x{tex.height} " +
                          $"avgRGB=({avg.r:F2},{avg.g:F2},{avg.b:F2}){(greenish ? " GREENISH" : "")}");
            ok++;
            Object.DestroyImmediate(tex);
        }

        sb.AppendLine($"SUMMARY: jobs={jobs.Count} ok={ok} miss={miss} fallback={fb}");
        sb.AppendLine("DONE");
        File.WriteAllText(Path.Combine(OutDir, "facts.log"), sb.ToString());
        Debug.Log($"[IconBakeAll194] 완료 — jobs={jobs.Count} ok={ok} miss={miss} fallback={fb}");
    }
}
