using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.IO;

/// <summary>
/// [193차 P4②] 전 아이템 GLB 실베이크 접촉시트 원료 수집 프로브 — 게임 본경로
/// (GblItemIconRenderer.SyncBakeFromPrefab) 그대로 호출해 UserProvided 전 GLB(rigged 제외)를
/// PNG로 저장. 근원 판독 전용 — 게임 코드 수정 없음.
/// 사용: Unity.exe -batchmode -projectPath C:\Unity\code -executeMethod IconFrontageProbe193.Run -quit
/// </summary>
public static class IconFrontageProbe193
{
    public static void Run()
    {
        string outDir = "TestOutput/icon_frontage_193";
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

        string dir = "Assets/Resources/Models/UserProvided";
        var files = Directory.GetFiles(dir, "*.glb");
        int ok = 0, skip = 0, fail = 0;
        foreach (var f in files)
        {
            string stem = Path.GetFileNameWithoutExtension(f);
            if (stem.EndsWith("_Rigged")) { skip++; continue; }

            GameObject prefab = null;
            try { prefab = Resources.Load<GameObject>("Models/UserProvided/" + stem); }
            catch (System.Exception e)
            {
                File.AppendAllText(Path.Combine(outDir, "facts.log"), $"MISS: {stem} {e.Message}\n");
                fail++;
                continue;
            }
            if (prefab == null)
            {
                File.AppendAllText(Path.Combine(outDir, "facts.log"), $"MISS: {stem} prefab=null\n");
                fail++;
                continue;
            }

            Texture2D tex = null;
            try
            {
                tex = (Texture2D)mi.Invoke(null, new object[] { stem, stem, prefab });
            }
            catch (System.Exception e)
            {
                File.AppendAllText(Path.Combine(outDir, "facts.log"), $"EXC: {stem} {e.Message}\n");
                fail++;
                continue;
            }
            if (tex == null)
            {
                File.AppendAllText(Path.Combine(outDir, "facts.log"), $"FAIL: {stem} bake=null\n");
                fail++;
                continue;
            }

            File.WriteAllBytes(Path.Combine(outDir, stem + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            ok++;
        }
        File.AppendAllText(Path.Combine(outDir, "facts.log"), $"DONE ok={ok} skip={skip} fail={fail}\n");
    }
}
