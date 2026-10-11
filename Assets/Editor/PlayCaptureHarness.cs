using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// [21차 게이지 재판정] 배치 Play 캡처 하니스 (진입부).
/// 실행: Unity.exe -batchmode -projectPath C:/Unity/code -executeMethod PlayCaptureHarness.Run
///        -logFile C:/tmp/r21_playcap.log   (주의: -nographics 금지 — UI 렌더에 그래픽스 필요)
/// 실제 캡처/덤프/종료는 런타임 드라이버 Assets/Scripts/UI/PlayCaptureDriver.cs —
/// Play 진입 도메인 리로드가 EditorApplication.update 구독과 정적 필드를 초기화하기 때문에
/// SessionState(리로드 생존) + 런타임 MonoBehaviour로 카운팅한다(2026-10-11 실측 교훈).
/// </summary>
public static class PlayCaptureHarness
{
    public static void Run()
    {
        UnityEditor.SessionState.SetBool("pcap_active", true);
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        EditorApplication.isPlaying = true;
        Debug.Log("[PlayCapture] play 진입 요청 — MainScene (드라이버가 캡처 담당)");
    }
}