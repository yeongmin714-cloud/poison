using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor 시작 시 SwayController를 자동 설치.
/// 실행 조건: Library/ 첫 빌드 완료 후 최초 1회 실행.
/// </summary>
[InitializeOnLoad]
public static class AutoSwayInstaller
{
    private const string FlagKey = "AutoSwayInstaller_Installed";

    static AutoSwayInstaller()
    {
        // Library가 아직 없거나 최초 임포트 중이면 스킵
        if (!SessionState.GetBool(FlagKey, false))
        {
            SessionState.SetBool(FlagKey, true);
            EditorApplication.delayCall += RunInstall;
        }
    }

    private static void RunInstall()
    {
        Debug.Log("[AutoSwayInstaller] SwayController 자동 설치를 시작합니다...");
        SwayInstaller.InstallSwayControllers();
        Debug.Log("[AutoSwayInstaller] 자동 설치 완료!");
    }
}