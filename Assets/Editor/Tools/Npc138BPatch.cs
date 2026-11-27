using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [138차 Impl-B] 빈블록(clipAnimations: []) 케이스 2건 수리 — defaultClipAnimations 직렬화 후 orientationOffsetY 재패치.
///   · Npc_OldWoman_1_talk.fbx → 195.8  (감사 원평균 15.8 @ offset 0 → 목표 ±180: 15.8 − 195.8 = −180)
///   · Npc_Man_5_walk.fbx      → 274.1  (walk 표준 Δ=+85.9 균일 적용: 94.1 − 274.1 ≡ +180)
/// 측정 gain = −1(측정 hips−root = raw − offset, 135차 P2-v2 191.2 @0 / 136차 0.2 @191 / 138차 감사 −1.0 @191 교차 확정).
/// 나머지 46개 메타(clipAnimations 블록 존재)는 YAML 직접 편집 — 본 스크립트 불개입.
/// 실행: Unity.exe -batchmode -projectPath C:/Unity/code -executeMethod Npc138BPatch.RunPatch -quit -logFile TestOutput/qa_138B_compile.log
/// 산출: TestOutput/npc138_empty_block_patch.txt
/// </summary>
public static class Npc138BPatch
{
    private static readonly string[] TargetsPath =
    {
        "Assets/Resources/Models/UserProvided/Npc2026/Npc_OldWoman_1/Npc_OldWoman_1_talk.fbx",
        "Assets/Resources/Models/UserProvided/Npc2026/Npc_Man_5/Npc_Man_5_walk.fbx",
    };
    private static readonly float[] TargetsOffset = { 195.8f, 274.1f };

    public static void RunPatch()
    {
        var log = new StringBuilder();
        log.AppendLine("[138차-B] 빈블록 defaultClipAnimations 직렬화+패치 시작 — " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        Directory.CreateDirectory("TestOutput");
        AssetDatabase.Refresh();

        for (int i = 0; i < TargetsPath.Length; i++)
        {
            string path = TargetsPath[i];
            float offset = TargetsOffset[i];
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null)
            {
                log.AppendLine("[138차-B] FAIL importer null: " + path);
                continue;
            }
            var clips = imp.defaultClipAnimations;   // 빈블록 → 기본 클립 설정 직렬화
            if (clips == null || clips.Length == 0)
            {
                log.AppendLine("[138차-B] FAIL defaultClipAnimations 비어있음: " + path);
                continue;
            }
            // [138차-B 수리] Unity 6000.x ModelImporterClipAnimation에 orientationOffsetY 프로퍼티 부재(CS1061 실측 —
            //   135차 결론과 동일: 해당 프롭은 API 미노출 → YAML 직접 편집 대상). 본 배치는 블록 물질화까지만 수행하고,
            //   orientationOffsetY 값은 직후 후속 패스에서 meta YAML 직접 치환한다(목표 로그 기록).
            imp.clipAnimations = clips;
            imp.SaveAndReimport();
            log.AppendLine("[138차-B] OK " + path + ": clips=" + clips.Length + " 블록 물질화(오프셋 YAML 후속 패치 목표=" + offset + ")");
        }

        log.AppendLine("[138차-B] DONE");
        Debug.Log(log.ToString());
        File.WriteAllText("TestOutput/npc138_empty_block_patch.txt", log.ToString());
    }
}
