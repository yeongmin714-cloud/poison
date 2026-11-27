// No78MaterialRemapBatch.cs — [무번호 78] Meteorite FBX 결손 머티리얼 리맵 배치 (v5 — 임포트 콜백)
// 배경: P78 임포트 3개 FBX(GroundImpact/Meteorite/MeteoriteBlastMesh2)의 meta externalObjects가
//       소스 레포(vfx-samples) guid 참조(프로젝트 .mat 부재) → 렌더러 null material →
//       FbxModelShaderProbeTests [43-PROBE] 신규 실패 1건(baseline 10건 외).
// v2~v4 실측 실패: ①SerializedObject(m_ExternalObjects) 편집 applied=True에도 네이티브 리맵 상태 무반영,
//       meta 디스크 불변 ②GetExternalObjectMap 사전 변이 무효(복사본 반환) ③SetExternalObjectMap —
//       Unity 6 미존재(CS1061).
// v5 전략: AssetPostprocessor.OnAssignMaterialModel — Unity 공식 임포트 시점 머티리얼 할당 콜백.
//       dangling remap(lambert1→null)은 유효 리맵이 아니므로 콜백이 발화하고, 43-PROBE는 렌더러
//       sharedMaterials만 검사하므로 임포트 시 할당만 되면 통과. VFX Graph 메시 출력은 그래프 자체
//       셰이더로 렌더 — 이 머티리얼은 런타임 미사용(프로브 무결성만 충족).
// 실행: Unity.exe -quit -batchmode -projectPath C:/Unity/code -executeMethod No78MaterialRemapBatch.Run
//       -logFile TestOutput/no78_remap.log
// 결과: TestOutput/no78_material_remap.txt (기계판독용) + Unity 로그 [NO78-REMAP] 라인.

using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// 임포트 시점 lambert1 → Basic Hit_URP 할당 (Meteorite 메시 전용 스코프 — 프로젝트 상설 가드)
public class No78MeteoriteMaterialAssigner : AssetPostprocessor
{
    const string Scope = "Assets/Resources/FX/Meteorite/Meshes/";
    const string SourceMatName = "lambert1";
    const string TargetMatPath = "Assets/Resources/FX/Impact/Mats/Basic Hit_URP.mat";

    Material OnAssignMaterialModel(Material currentMaterial, Renderer renderer)
    {
        if (!assetPath.StartsWith(Scope, StringComparison.OrdinalIgnoreCase)) return null;
        // GroundImpact.fbx의 FBX 내부 머티리얼은 lambert2(유일) — lambert* 접두 전체 수용(78-P1 실측)
        if (currentMaterial == null || !currentMaterial.name.StartsWith("lambert")) return null;
        var mat = AssetDatabase.LoadAssetAtPath<Material>(TargetMatPath);
        if (mat == null) return null; // 대상 부재 — 기본 동작
        Debug.Log("[NO78-REMAP] OnAssignMaterialModel " + assetPath + " :: " + renderer.name +
                  " lambert1 → " + mat.name);
        return mat;
    }
}

public static class No78MaterialRemapBatch
{
    const string TargetMatPath = "Assets/Resources/FX/Impact/Mats/Basic Hit_URP.mat";

    static readonly string[] FbxPaths =
    {
        "Assets/Resources/FX/Meteorite/Meshes/GroundImpact.fbx",
        "Assets/Resources/FX/Meteorite/Meshes/Meteorite.fbx",
        "Assets/Resources/FX/Meteorite/Meshes/MeteoriteBlastMesh2.fbx",
    };

    public static void Run()
    {
        var sb = new StringBuilder();
        Action<string> emit = line =>
        {
            sb.AppendLine(line);
            Debug.Log("[NO78-REMAP] " + line);
        };

        emit("=== No78 Meteorite FBX material remap v5 (OnAssignMaterialModel) ===");
        var targetMat = AssetDatabase.LoadAssetAtPath<Material>(TargetMatPath);
        if (targetMat == null)
        {
            emit("FATAL: target material missing: " + TargetMatPath);
            Write(sb, false);
            EditorApplication.Exit(2);
            return;
        }
        string targetGuid = AssetDatabase.AssetPathToGUID(TargetMatPath);
        emit("target=" + TargetMatPath + " guid=" + targetGuid + " shader=" + targetMat.shader.name);

        int reimported = 0, failed = 0;
        foreach (string path in FbxPaths)
        {
            try
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                {
                    emit("FAIL importer_null " + path);
                    failed++;
                    continue;
                }
                importer.SaveAndReimport(); // OnAssignMaterialModel이 lambert1 할당
                emit("REIMPORTED " + path);
                reimported++;
            }
            catch (Exception e)
            {
                emit("EXCEPTION " + path + " :: " + e.GetType().Name + ": " + e.Message);
                failed++;
            }
        }

        // 검증 — 리임포트 후 렌더러 전수 실측(43-PROBE 동일 기준)
        int broken = 0;
        foreach (string path in FbxPaths)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null)
            {
                emit("VERIFY model_null " + path);
                broken++;
                continue;
            }
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                if (mats == null) continue;
                foreach (var m in mats)
                {
                    if (m == null)
                    {
                        emit("VERIFY_BROKEN null_material " + path + " :: " + r.name);
                        broken++;
                    }
                    else if (m.shader == null)
                    {
                        emit("VERIFY_BROKEN null_shader " + path + " :: " + r.name);
                        broken++;
                    }
                    else if (m.shader.name == "Hidden/InternalErrorShader")
                    {
                        emit("VERIFY_BROKEN error_shader " + path + " :: " + r.name);
                        broken++;
                    }
                    else
                    {
                        emit("VERIFY_OK " + path + " :: " + r.name + " :: " + m.name + " :: " + m.shader.name);
                    }
                }
            }
        }

        emit("SUMMARY reimported=" + reimported + " failed=" + failed + " verify_broken=" + broken);
        bool ok = failed == 0 && broken == 0;
        emit("NO78_REMAP_VERDICT=" + (ok ? "PASS" : "FAIL"));
        Write(sb, ok);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    static void Write(StringBuilder sb, bool ok)
    {
        try
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string outDir = Path.Combine(projectRoot, "TestOutput");
            Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, "no78_material_remap.txt"), sb.ToString());
        }
        catch (Exception we)
        {
            Debug.LogError("[NO78-REMAP] result_write_failed=" + we.Message);
        }
    }
}
