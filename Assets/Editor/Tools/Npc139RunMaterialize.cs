using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [139차 P1] Npc2026 per-character run FBX 23종 — 빈 clipAnimations 블록 물질화(오프셋 0 기본 블록 생성).
/// 근거: run 메타 전원 clipAnimations: [](오프셋 0 — 135차 표 Run=−6.0 실측과 정합). 목표 오프셋 +174는
/// 배치 후 YAML 직접 치환(부모 후속 패스 — ModelImporterClipAnimation에 orientationOffsetY API 부재, CS1061 실측).
/// 138차 실측 교훈 반영: defaultClipAnimations가 비는 케이스 = 테이크 없는 FBX(OW_1_talk/Man_5_walk) —
/// run FBX는 테이크 존재(135차 전수 실측)라 정상 반환 기대. 비면 SKIP 로그(예외 케이스 보고용).
/// 실행: Unity.exe -batchmode -projectPath C:/Unity/code -executeMethod Npc139RunMaterialize.RunPatch -quit -logFile TestOutput/npc139_materialize_unity.log
/// 산출: TestOutput/npc139_run_materialize.txt
/// </summary>
public static class Npc139RunMaterialize
{
    public static void RunPatch()
    {
        var log = new StringBuilder();
        log.AppendLine("[139차 P1] run 블록 물질화 시작 — " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        Directory.CreateDirectory("TestOutput");
        AssetDatabase.Refresh();

        string[] dirs = Directory.GetDirectories("Assets/Resources/Models/UserProvided/Npc2026", "Npc_*");
        int ok = 0, skip = 0;
        foreach (string dir in dirs)
        {
            string[] fbxs = Directory.GetFiles(dir, "*_run.fbx");
            foreach (string path in fbxs)
            {
                var imp = AssetImporter.GetAtPath(path.Replace('\\', '/')) as ModelImporter;
                if (imp == null) { log.AppendLine("[139차 P1] FAIL importer null: " + path); continue; }
                var clips = imp.defaultClipAnimations;
                if (clips == null || clips.Length == 0)
                {
                    log.AppendLine("[139차 P1] SKIP 테이크 없음(자산 공백): " + path);
                    skip++;
                    continue;
                }
                imp.clipAnimations = clips;   // 빈블록 → 기본 클립 블록 물질화(오프셋 0)
                imp.SaveAndReimport();
                log.AppendLine("[139차 P1] OK " + path + ": clips=" + clips.Length + " 블록 물질화(오프셋은 YAML 후속 +174)");
                ok++;
            }
        }
        log.AppendLine($"[139차 P1] DONE — materialized={ok} skip={skip}");
        Debug.Log(log.ToString());
        File.WriteAllText("TestOutput/npc139_run_materialize.txt", log.ToString());
    }
}
