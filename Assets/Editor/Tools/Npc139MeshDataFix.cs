using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [139차 P3] Npc2026 rigged FBX 24종 — 노멀/탄젠트 임포트 재계산(단일소스 수리).
/// 근거(139차 A/B 프로브 체인): ①Unlit(base_color 직결)=완전 청결 — UV/메시/알베도 정상
/// ②Lit=얼룩(그림자 on/off·SMR receiveShadows·노멀슬롯 클리어 무효과) ③AO/추가 맵 바인딩 없음
/// ④노멀 감사 — verts=4165 vs tangents=4164 **개수 불일치**(Meshy 익스포트 메시 데이터 결함)
/// → Lit 노멀맵 셰이딩이 불일치 탄젠트로 파손 = 얼룩. 임포트에서 노멀/탄젠트 Calculate로 재생성해
/// 전 인스턴스 단일소스 수리(클립 요각 패치 등 meta의 clipAnimations는 유지됨).
/// 실행: Unity.exe -batchmode -projectPath C:/Unity/code -executeMethod Npc139MeshDataFix.RunPatch -quit -logFile TestOutput/npc139_meshfix_unity.log
/// 산출: TestOutput/npc139_meshfix.txt
/// </summary>
public static class Npc139MeshDataFix
{
    public static void RunPatch()
    {
        var log = new StringBuilder();
        log.AppendLine("[139차 P3] 노멀/탄젠트 재계산 시작 — " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        Directory.CreateDirectory("TestOutput");
        AssetDatabase.Refresh();

        string[] dirs = Directory.GetDirectories("Assets/Resources/Models/UserProvided/Npc2026", "Npc_*");
        int ok = 0, skip = 0, already = 0;
        foreach (string dir in dirs)
        {
            string[] fbxs = Directory.GetFiles(dir, "*_rigged.fbx");
            foreach (string rawPath in fbxs)
            {
                string path = rawPath.Replace('\\', '/');
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp == null) { log.AppendLine("[139차 P3] FAIL importer null: " + path); continue; }
                bool needNormals = imp.importNormals != ModelImporterNormals.Calculate;
                bool needTangents = imp.importTangents != ModelImporterTangents.CalculateMikk;
                if (!needNormals && !needTangents)
                {
                    already++;
                    continue;
                }
                if (needNormals) imp.importNormals = ModelImporterNormals.Calculate;
                if (needTangents) imp.importTangents = ModelImporterTangents.CalculateMikk;
                imp.SaveAndReimport();
                log.AppendLine($"[139차 P3] OK {path}: normals=Calculate tangents=Calculate 적용");
                ok++;
            }
        }
        log.AppendLine($"[139차 P3] DONE — reimport={ok} already={already} skip={skip}");
        Debug.Log(log.ToString());
        File.WriteAllText("TestOutput/npc139_meshfix.txt", log.ToString());
    }
}
