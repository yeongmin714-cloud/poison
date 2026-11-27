using UnityEngine;
using UnityEditor;
using System.Text;
using System.IO;

/// <summary>
/// [194차 P3 검증] 오버라이드+Z-보정 프레이밍 적용 후 실제 베이크 경로(SyncBakeItem)로
/// 수리 대상 키 + 정상 대조군을 고립 재베이크해 PNG 저장. 오염 감시(원격 구역 루트 센서스) 포함.
/// 사용: Unity.exe -batchmode -projectPath C:\Unity\code -executeMethod IconBakeFix194.Run -quit
/// </summary>
public static class IconBakeFix194
{
    // (itemId, modelKey) — dish는 실제 레시피 id(실틴트 적용 검증), bucket key로 오버라이드 조회.
    private static readonly (string id, string key)[] Items =
    {
        ("dish_001", "dish_stirfry"), ("dish_002", "dish_braise"), ("dish_003", "dish_steam"),
        ("dish_006", "dish_salad"),   ("dish_008", "dish_stew"),   ("dish_023", "dish_jeon"),
        ("dish_046", "dish_sashimi"), ("dish_075", "dish_ricebowl"), ("dish_081", "dish_steak"),
        ("dish_031", "dish_pie"),     ("dish_012", "grilled_skin"), ("dish_013", "tempura"),
        ("dish_047", "yakitori_skewer"),
        ("dish_028", "dish_pickle"),  ("dish_007", "dish_tea"),     // 대조군(무오버라이드)
        ("potion_sedative", "potion_sedative"), ("potion_stealth", "potion_stealth"),
        ("tool_shovel", "tool_shovel"), ("tool_pickaxe", "tool_pickaxe"),
        ("crystal_shard", "crystal_shard"),
        ("mat_boar_tusk", "boar_tusk"), ("mat_wolf_tooth", "wolf_tooth"),
        ("armor_wood", "wood_armor"), ("mat_stone", "mat_stone"),   // 대조군 포함
    };

    public static void Run()
    {
        string dir = "TestOutput/icon_fix_194";
        Directory.CreateDirectory(dir);
        var sb = new StringBuilder();

        var mi = typeof(ProjectName.UI.GblItemIconRenderer).GetMethod("SyncBakeItem",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        if (mi == null)
        {
            File.WriteAllText(Path.Combine(dir, "facts.log"), "FAIL: SyncBakeItem reflection null\n");
            return;
        }

        int leakTotal = Census(sb, "START");
        foreach (var (id, key) in Items)
        {
            Texture2D tex = null;
            try { tex = (Texture2D)mi.Invoke(null, new object[] { id, key, null }); }
            catch (System.Exception e) { sb.AppendLine($"EXC: {id} {e.GetType().Name}: {e.Message}"); }
            if (tex == null) { sb.AppendLine($"MISS: {id} key={key}"); continue; }

            File.WriteAllBytes(Path.Combine(dir, key + ".png"), tex.EncodeToPNG());
            Color sum = Color.clear; int n = 0;
            for (int y = tex.height / 3; y < tex.height * 2 / 3; y++)
                for (int x = tex.width / 3; x < tex.width * 2 / 3; x++)
                { sum += tex.GetPixel(x, y); n++; }
            var avg = sum / n;
            sb.AppendLine($"OK: {id} key={key} avgRGB=({avg.r:F2},{avg.g:F2},{avg.b:F2})");
            Object.DestroyImmediate(tex);
            leakTotal += Census(sb, key);
        }
        sb.AppendLine($"LEAK_TOTAL={leakTotal}");
        sb.AppendLine("DONE");
        File.WriteAllText(Path.Combine(dir, "facts.log"), sb.ToString());
        Debug.Log("[IconBakeFix194] 완료 — " + dir);
    }

    /// <summary>오염 감시 — 원격 베이크 구역(x/y/z>9000급) 루트 오브젝트 나열(0이면 청결).</summary>
    private static int Census(StringBuilder sb, string tag)
    {
        int c = 0;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach (var go in scene.GetRootGameObjects())
        {
            var t = go.transform;
            if (t.position.x > 9000f || t.position.y > 900f || t.position.z > 9000f)
            {
                c++;
                sb.AppendLine($"  LEAK[{tag}]: {go.name} @ {t.position}");
            }
        }
        return c;
    }
}
