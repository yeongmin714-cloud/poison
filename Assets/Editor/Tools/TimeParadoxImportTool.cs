using UnityEditor;
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [무번호5/P3] Time Paradox Engine unitypackage 배치 임포트 — 계획 docs/plans/2026-11-07_attack-parry-premium-timeparadox-plan.md §P3-E.
    /// 경로 실측: Assets/새로운 glb/쓸만한/Time Paradox.zip = Time+Paradox+Engine...6000.5.7f1.unitypackage(101.9MB).
    /// 프로젝트 6000.4.10f1 — 마이너 차이. 의존성(Burst/Collections/Mathematics/Cinemachine/VFG/InputSystem) 전부 기설치 실측.
    /// 실행: batchmode -executeMethod ProjectName.EditorTools.TimeParadoxImportTool.ImportNow
    /// </summary>
    public static class TimeParadoxImportTool
    {
        private const string Tag = "[TimeParadoxImport]";
        private const string PackagePath = "Assets/새로운 glb/쓸만한/Time Paradox.zip";

        public static void ImportNow()
        {
            Debug.Log($"{Tag} === unitypackage 임포트 시작 ===");
            // zip 내부의 unitypackage를 먼저 풀어야 함 — zip은 에셋으로 임포트 불가.
            // 사전 조건: WSL 측에서 /tmp/timeparadox/pkg까지 해제 완료(부모가 검증) →
            //   여기서는 해제된 unitypackage를 프로젝트 임시 경로에 복사해 사용.
            string srcPkg = System.IO.Path.GetFullPath(System.IO.Path.Combine("..", "_importpkg", "unitypackage.unitypackage"));
            if (!System.IO.File.Exists(srcPkg))
            {
                Debug.LogError($"{Tag} FAIL — unitypackage 사본 부재: {srcPkg} (부모가 사전 복사해야 함)");
                EditorApplication.Exit(1);
                return;
            }
            try
            {
                AssetDatabase.ImportPackage(srcPkg, interactive: false);
                AssetDatabase.Refresh();
                bool coreExists = AssetDatabase.LoadAllAssetsAtPath("Assets/Time Paradox Engine/Core/Managers/TimeWarpManager.cs") != null
                                  && System.IO.File.Exists("Assets/Time Paradox Engine/Core/Managers/TimeWarpManager.cs");
                Debug.Log($"{Tag} DONE coreExists={coreExists}");
                EditorApplication.Exit(coreExists ? 0 : 1);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                Debug.LogError($"{Tag} FAIL — {e.Message}");
                EditorApplication.Exit(1);
            }
        }
    }
}
