// One-shot (2026-11-27): Vefects "Trails VFX - URP" unitypackage 무인 임포트 + 착지 검증.
//
// 배치 실행 (compile_test.sh와 동일한 Unity 경로 사용):
//   Unity.exe -batchmode -projectPath C:\Unity\code \
//     -executeMethod ProjectName.EditorTools.ImportTrailsVFX207.Run \
//     -logFile C:\Unity\code\import_trails_vfx_207.log
//
// 주의: -quit 플래그를 쓰지 않는다. ImportPackage 완료(프리팹 10종 착지)를
// 에디터 update 루프 폴링으로 확인한 뒤 스스로 EditorApplication.Exit 한다.
// 패키지에 .cs가 포함되어 임포트 중 도메인 리로드가 일어나면 정적 상태가 초기화되므로,
// SessionState 플래그 + [InitializeOnLoad]로 폴링을 재등록한다 (세션 간에는 유지되지 않음 —
// 이후 일반 에디터 세션이나 다른 배치 작업에 부작용 없음).
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProjectName.EditorTools
{
    public static class ImportTrailsVFX207
    {
        private const string PackagePath =
            @"C:\Users\korea\AppData\Roaming\Unity\Asset Store-5.x\Vefects\VFX\Trails VFX - URP.unitypackage";
        private const string DestRoot = "Assets/Vefects/Trails VFX URP";
        private const string VefectsRoot = "Assets/Vefects";
        private const string FlagKey = "ImportTrailsVFX207_Polling";
        private const double VerifyTimeoutSeconds = 600.0;
        private const int MinPrefabs = 10;

        private static double _deadline;
        private static int _ticks;

        static ImportTrailsVFX207()
        {
            // 도메인 리로드 후 폴링 재등록 (세션 내에서만 유효한 플래그)
            if (SessionState.GetBool(FlagKey, false))
            {
                Debug.Log("[ImportTrailsVFX207] 도메인 리로드 감지 — 폴링 재등록");
                EditorApplication.update += Poll;
            }
        }

        public static void Run()
        {
            try
            {
                bool packageExists = System.IO.File.Exists(PackagePath);
                Debug.Log($"[ImportTrailsVFX207] START package=\"{PackagePath}\" exists={packageExists}");

                if (!packageExists)
                {
                    Debug.LogError($"[ImportTrailsVFX207] PACKAGE_NOT_FOUND: {PackagePath}");
                    EditorApplication.Exit(2);
                    return;
                }

                SessionState.SetBool(FlagKey, true);

                // 비대화형 임포트 (전체 에셋, 대화상자 없음 → 기본 경로 Assets/Vefects/Trails VFX URP에 착지)
                AssetDatabase.ImportPackage(PackagePath, false);
                Debug.Log("[ImportTrailsVFX207] ImportPackage 호출 완료 — 착지 폴링 시작");

                _ticks = 0;
                _deadline = EditorApplication.timeSinceStartup + VerifyTimeoutSeconds;
                EditorApplication.update -= Poll; // 중복 등록 방지
                EditorApplication.update += Poll;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ImportTrailsVFX207] IMPORT_EXCEPTION: {e}");
                EditorApplication.Exit(3);
            }
        }

        private static void Poll()
        {
            _ticks++;

            bool rootValid = AssetDatabase.IsValidFolder(DestRoot);
            string[] rootPrefabs = rootValid ? AssetDatabase.FindAssets("t:Prefab", new[] { DestRoot }) : new string[0];
            bool vefectsValid = AssetDatabase.IsValidFolder(VefectsRoot);
            string[] vefectsPrefabs = vefectsValid ? AssetDatabase.FindAssets("t:Prefab", new[] { VefectsRoot }) : new string[0];

            if (vefectsValid && vefectsPrefabs.Length >= MinPrefabs)
            {
                SessionState.SetBool(FlagKey, false);

                Debug.Log($"[ImportTrailsVFX207] IMPORT_COMPLETE destRootValid={rootValid} rootPrefabs={rootPrefabs.Length} vefectsPrefabs={vefectsPrefabs.Length} ticks={_ticks}");

                var sorted = new List<string>(rootPrefabs);
                sorted.Sort();
                foreach (string guid in sorted)
                    Debug.Log($"[ImportTrailsVFX207] PREFAB {AssetDatabase.GUIDToAssetPath(guid)}");

                foreach (string folder in AssetDatabase.GetSubFolders(DestRoot))
                    Debug.Log($"[ImportTrailsVFX207] SUBFOLDER {folder}");

                EditorApplication.Exit(0);
                return;
            }

            if (EditorApplication.timeSinceStartup > _deadline)
            {
                SessionState.SetBool(FlagKey, false);
                Debug.LogError($"[ImportTrailsVFX207] IMPORT_TIMEOUT destRootValid={rootValid} rootPrefabs={rootPrefabs.Length} vefectsPrefabs={vefectsPrefabs.Length} ticks={_ticks}");
                EditorApplication.Exit(4);
            }
        }
    }
}
