// One-shot (2026-11-27): Vefects "Trails VFX - URP" Dark 트레일 프리팹 → Resources 리소스 인스톨 (208차 P1).
//
// 배치 실행 (compile_test.sh와 동일한 Unity 경로 사용):
//   Unity.exe -batchmode -projectPath C:\Unity\code \
//     -executeMethod ProjectName.EditorTools.CopyTrailsVfxResource208.Run \
//     -logFile C:\Unity\code\copy_trails_vfx_208.log
//
// 계약: 후속 코드가 Resources.Load("FX/Afterlife/TrailsVfx/VFX_Trail_Dark") 키로 로드한다.
//  - 원본 Assets/Vefects/Trails VFX URP/VFX/Particles/VFX_Trail_Dark.prefab 은 무손상 유지.
//  - 반드시 AssetDatabase.CopyAsset 사용 (수동 파일+meta 복사는 guid 충돌 — 금지).
//  - 의존성(머티리얼/텍스처/셰이더)은 원본 위치에 그대로 두고 참조로 해결 — 전수 복사 불필요(최소 복사).
// CopyAsset은 동기 API이므로 207차의 폴링/SessionState 불필요 — 모든 경로에서 즉시 Exit.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectName.EditorTools
{
    public static class CopyTrailsVfxResource208
    {
        private const string SrcPath =
            "Assets/Vefects/Trails VFX URP/VFX/Particles/VFX_Trail_Dark.prefab";
        private const string DestPath =
            "Assets/Resources/FX/Afterlife/TrailsVfx/VFX_Trail_Dark.prefab";

        public static void Run()
        {
            try
            {
                // 1) 원본 존재 확인
                bool srcFileExists = File.Exists(Path.Combine(Directory.GetParent(Application.dataPath).FullName, SrcPath.Replace('/', Path.DirectorySeparatorChar)));
                UnityEngine.Object srcAsset = AssetDatabase.LoadMainAssetAtPath(SrcPath);
                Debug.Log($"[CopyTrailsVfx208] START src=\"{SrcPath}\" srcFileExists={srcFileExists} srcAssetLoaded={(srcAsset != null)}");

                if (!srcFileExists || srcAsset == null)
                {
                    Debug.LogError($"[CopyTrailsVfx208] SOURCE_NOT_FOUND: {SrcPath}");
                    EditorApplication.Exit(2);
                    return;
                }

                // 2) 목적지 폴더 체인 생성 (Assets/Resources 는 존재, FX 도 존재)
                if (!EnsureFolder("Assets/Resources", "FX")
                    || !EnsureFolder("Assets/Resources/FX", "Afterlife")
                    || !EnsureFolder("Assets/Resources/FX/Afterlife", "TrailsVfx"))
                {
                    Debug.LogError("[CopyTrailsVfx208] FOLDER_CREATE_FAILED");
                    EditorApplication.Exit(4);
                    return;
                }

                // 3) 목적지 선점 시 제거 후 복사 (재실행 멱등성; 원본은 건드리지 않음)
                if (AssetDatabase.LoadMainAssetAtPath(DestPath) != null)
                {
                    Debug.Log($"[CopyTrailsVfx208] DEST_EXISTS — 재복사 위해 삭제: {DestPath}");
                    if (!AssetDatabase.DeleteAsset(DestPath))
                    {
                        Debug.LogError($"[CopyTrailsVfx208] DEST_DELETE_FAILED: {DestPath}");
                        EditorApplication.Exit(4);
                        return;
                    }
                }

                // 4) 복사 (guid 충돌 없는 에디터 정식 복제)
                bool copied = AssetDatabase.CopyAsset(SrcPath, DestPath);
                Debug.Log($"[CopyTrailsVfx208] COPY result={copied} src=\"{SrcPath}\" dest=\"{DestPath}\"");
                if (!copied)
                {
                    Debug.LogError($"[CopyTrailsVfx208] COPY_FAILED: {SrcPath} -> {DestPath}");
                    EditorApplication.Exit(3);
                    return;
                }

                // 5) 착지 검증: 목적지 로드 + .prefab/.meta 디스크 존재 + 원본 무손상
                UnityEngine.Object destAsset = AssetDatabase.LoadMainAssetAtPath(DestPath);
                string destAbs = Path.Combine(Directory.GetParent(Application.dataPath).FullName, DestPath.Replace('/', Path.DirectorySeparatorChar));
                bool destFile = File.Exists(destAbs);
                bool destMeta = File.Exists(destAbs + ".meta");
                UnityEngine.Object srcAfter = AssetDatabase.LoadMainAssetAtPath(SrcPath);

                Debug.Log($"[CopyTrailsVfx208] VERIFY destLoaded={(destAsset != null)} destFile={destFile} destMeta={destMeta} srcStillLoaded={(srcAfter != null)}");

                if (destAsset == null || !destFile || !destMeta || srcAfter == null)
                {
                    Debug.LogError("[CopyTrailsVfx208] LANDING_VERIFY_FAILED");
                    EditorApplication.Exit(5);
                    return;
                }

                AssetDatabase.SaveAssets();

                Debug.Log("[CopyTrailsVfx208] DONE");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CopyTrailsVfx208] EXCEPTION: {e}");
                EditorApplication.Exit(6);
            }
        }

        private static bool EnsureFolder(string parent, string folder)
        {
            string combined = parent + "/" + folder;
            if (AssetDatabase.IsValidFolder(combined))
                return true;
            string created = AssetDatabase.CreateFolder(parent, folder);
            bool ok = !string.IsNullOrEmpty(created) && AssetDatabase.IsValidFolder(combined);
            Debug.Log($"[CopyTrailsVfx208] FOLDER {(ok ? "CREATED" : "FAILED")}: {combined}");
            return ok;
        }
    }
}
