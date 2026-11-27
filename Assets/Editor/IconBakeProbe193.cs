using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.IO;

/// <summary>
/// [193차 P4①] UI 실제 베이크 경로(GblItemIconRenderer.SyncBakeFromPrefab) 그대로 호출해
/// 목검/창/폭탄 3종 아이콘을 PNG로 저장하는 진단 프로브. 근원 특정 전용 — 코드 수정 없음.
/// 사용: Unity.exe -batchmode -projectPath C:\Unity\code -executeMethod IconBakeProbe193.Run -quit
/// </summary>
public static class IconBakeProbe193
{
    private static readonly (string id, string modelKey)[] Items =
    {
        ("weapon_sword_wood", "wood_sword"),
        ("weapon_spear_wood", "wood_spear"),
        ("bomb_explosive",    "bomb_explosive"),
    };

    public static void Run()
    {
        string outDir = "TestOutput/icon_bake_probe_193";
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "facts.log"), "");

        var mi = typeof(ProjectName.UI.GblItemIconRenderer).GetMethod(
            "SyncBakeFromPrefab", BindingFlags.NonPublic | BindingFlags.Static);
        if (mi == null)
        {
            File.AppendAllText(Path.Combine(outDir, "facts.log"),
                "FAIL: SyncBakeFromPrefab reflection null\n");
            return;
        }

        foreach (var (id, modelKey) in Items)
        {
            GameObject prefab = Resources.Load<GameObject>("Models/UserProvided/" + modelKey);
            if (prefab == null)
            {
                File.AppendAllText(Path.Combine(outDir, "facts.log"),
                    $"MISS: {id} model={modelKey} prefab=null\n");
                continue;
            }
            Texture2D tex = null;
            try
            {
                tex = (Texture2D)mi.Invoke(null, new object[] { id, modelKey, prefab });
            }
            catch (System.Exception e)
            {
                File.AppendAllText(Path.Combine(outDir, "facts.log"),
                    $"EXC: {id} {e.Message}\n");
            }
            if (tex == null)
            {
                File.AppendAllText(Path.Combine(outDir, "facts.log"),
                    $"FAIL: {id} bake=null\n");
                continue;
            }
            File.WriteAllBytes(Path.Combine(outDir, id + ".png"), tex.EncodeToPNG());
            // 평균색 실측(중앙 1/3 영역)
            Color sum = Color.clear; int n = 0;
            for (int y = tex.height / 3; y < tex.height * 2 / 3; y++)
                for (int x = tex.width / 3; x < tex.width * 2 / 3; x++)
                { sum += tex.GetPixel(x, y); n++; }
            Color avg = sum / n;
            File.AppendAllText(Path.Combine(outDir, "facts.log"),
                $"OK: {id} model={modelKey} size={tex.width}x{tex.height} avgRGB=({avg.r:F2},{avg.g:F2},{avg.b:F2})\n");
            Object.DestroyImmediate(tex);
        }
        File.AppendAllText(Path.Combine(outDir, "facts.log"), "DONE\n");
    }
}
