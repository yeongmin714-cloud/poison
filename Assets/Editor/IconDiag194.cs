using UnityEngine;
using UnityEditor;
using System.Text;
using System.IO;

/// <summary>
/// [194차 P3 진단] 아이콘 방향/절단 결함 키의 원본 자세 로그 — 수리 각도 산출 전용(코드 수정 없음).
/// 대상: dish 15버킷(정상 대조 dish_tea 포함) + potion 2 + tool 3(정상 대조 tool_axe 포함)
///       + crystal_shard + boar_tusk + wolf_tooth + mat_stone + wood_armor.
/// 로그: TestOutput/icon_diag_194/diag.log — key, originalEuler, extents, renderers, skinned.
/// 사용: Unity.exe -batchmode -projectPath C:\Unity\code -executeMethod IconDiag194.Run -quit
/// </summary>
public static class IconDiag194
{
    private static readonly string[] Keys =
    {
        "dish_stirfry", "dish_braise", "dish_steam", "dish_salad", "dish_stew",
        "grilled_skin", "tempura", "dish_jeon", "dish_pickle", "dish_pie",
        "dish_sashimi", "yakitori_skewer", "dish_ricebowl", "dish_steak", "dish_tea",
        "potion_sedative", "potion_stealth", "potion_health",
        "tool_pickaxe", "tool_shovel", "tool_watering_can", "tool_axe",
        "crystal_shard", "boar_tusk", "wolf_tooth", "mat_stone", "wood_armor",
    };

    public static void Run()
    {
        string dir = "TestOutput/icon_diag_194";
        Directory.CreateDirectory(dir);
        var sb = new StringBuilder();
        sb.AppendLine("key | origEuler | ext(x,y,z) | renderers | skinned");

        foreach (var key in Keys)
        {
            var prefab = Resources.Load<GameObject>("Models/UserProvided/" + key);
            if (prefab == null)
            {
                sb.AppendLine($"{key} | MISS");
                continue;
            }
            var node = Object.Instantiate(prefab);
            node.name = "IconDiag_" + key;
            var rends = node.GetComponentsInChildren<Renderer>();
            bool skinned = node.GetComponentInChildren<SkinnedMeshRenderer>() != null;
            var e = node.transform.rotation.eulerAngles;
            if (rends.Length == 0)
            {
                sb.AppendLine($"{key} | rot=({e.x:F1},{e.y:F1},{e.z:F1}) | NO-RENDERER");
                Object.DestroyImmediate(node);
                continue;
            }
            var b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            sb.AppendLine($"{key} | rot=({e.x:F1},{e.y:F1},{e.z:F1}) | " +
                          $"ext=({b.extents.x:F3},{b.extents.y:F3},{b.extents.z:F3}) | " +
                          $"{rends.Length} | skinned={skinned}");
            Object.DestroyImmediate(node);
        }

        sb.AppendLine("DONE");
        File.WriteAllText(Path.Combine(dir, "diag.log"), sb.ToString());
        Debug.Log("[IconDiag194] 완료 — " + dir + "/diag.log");
    }
}
