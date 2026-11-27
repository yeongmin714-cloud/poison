using UnityEngine;
using UnityEditor;
using System.Text;
using System.IO;

/// <summary>
/// [199차 검증] bow JPEG 감량(4096→1024 재임베드) + dish COLOR_0 정점수술(AO/에지다크닝)의
/// before/after 고립 재베이크 접촉시트 소스. 실코드 경로(SyncBakeItem 리플렉션) — IconBakeFix194 동일 기구.
/// dish는 버킷 키를 id로도 사용(틴트 미적용 — 순수 GLB 셰이딩 비교, 틴트 공존은 계약테스트 담당).
/// 사용: Unity.exe -batchmode -projectPath C:\Unity\code -executeMethod IconBakeSurgery199.RunBefore|RunAfter -quit
/// </summary>
public static class IconBakeSurgery199
{
    private static readonly string[] Keys =
    {
        "dish_stirfry", "dish_braise", "dish_steam", "dish_stew", "dish_jeon", "dish_sashimi",
        "dish_ricebowl", "dish_steak", "dish_pie", "dish_pickle", "dish_jelly", "dish_roll",
        "wood_bow", "stone_bow",
    };

    public static void RunBefore() => Run("before");
    public static void RunAfter() => Run("after");

    private static void Run(string phase)
    {
        string dir = Path.Combine("TestOutput/icon_surgery_199", phase);
        Directory.CreateDirectory(dir);
        var sb = new StringBuilder();

        var mi = typeof(ProjectName.UI.GblItemIconRenderer).GetMethod("SyncBakeItem",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        if (mi == null)
        {
            File.WriteAllText(Path.Combine(dir, "facts.log"), "FAIL: SyncBakeItem reflection null\n");
            return;
        }

        foreach (var key in Keys)
        {
            Texture2D tex = null;
            try { tex = (Texture2D)mi.Invoke(null, new object[] { key, key, null }); }
            catch (System.Exception e) { sb.AppendLine($"EXC: {key} {e.GetType().Name}: {e.Message}"); }
            if (tex == null) { sb.AppendLine($"MISS: {key}"); continue; }

            File.WriteAllBytes(Path.Combine(dir, key + ".png"), tex.EncodeToPNG());
            Color sum = Color.clear; int n = 0;
            for (int y = tex.height / 3; y < tex.height * 2 / 3; y++)
                for (int x = tex.width / 3; x < tex.width * 2 / 3; x++)
                { sum += tex.GetPixel(x, y); n++; }
            var avg = sum / n;
            sb.AppendLine($"OK: {key} size={tex.width}x{tex.height} avgRGB=({avg.r:F2},{avg.g:F2},{avg.b:F2})");
            Object.DestroyImmediate(tex);
        }
        sb.AppendLine("DONE " + phase);
        File.WriteAllText(Path.Combine(dir, "facts.log"), sb.ToString());
        Debug.Log("[IconBakeSurgery199] 완료 — " + dir);
    }
}
