using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.IO;

/// <summary>
/// 자동 게임플레이 테스트 - PlayMode에서 실행되어 스크린샷 캡처
/// 실행: xvfb-run Unity -executeMethod AutoGameplayTest.RunAndCapture
/// </summary>
public class AutoGameplayTest
{
    private const string ScreenshotDir = "Screenshots";
    private static string _screenshotPath;

    /// <summary>
    /// 메인 진입점 - 배치모드에서 호출됨
    /// </summary>
    public static void RunAndCapture()
    {
        Debug.Log("========================================");
        Debug.Log("[AutoGameplayTest] === Starting Automated Gameplay Test ===");
        Debug.Log("========================================");

        // 스크린샷 디렉토리 생성
        string fullDir = Path.Combine(Application.dataPath, "..", ScreenshotDir);
        Directory.CreateDirectory(fullDir);

        _screenshotPath = Path.Combine(fullDir, $"gameplay_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");

        // 테스트 시작 코루틴 실행
        var runner = new GameObject("AutoTestRunner");
        runner.AddComponent<AutoTestRunner>().StartTest(_screenshotPath);
    }
}

/// <summary>
    /// 실제 테스트 로직을 수행하는 MonoBehaviour
    /// </summary>
    public class AutoTestRunner : MonoBehaviour
    {
        private string _targetScreenshotPath;
        private float _startTime;

        public void StartTest(string screenshotPath)
        {
            _targetScreenshotPath = screenshotPath;
            _startTime = Time.realtimeSinceStartup;
            
            Debug.Log("[AutoTestRunner] Test started");
            Debug.Log($"[AutoTestRunner] Current scene: {SceneManager.GetActiveScene().name}");
            
            // 동기 실행 (Invoke 없이)
            RunTestSteps();
        }

        private void RunTestSteps()
        {
            Debug.Log("[AutoTestRunner] Step 0: Waiting for initialization...");
            
            // 3초 대기 (동기)
            float endTime = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < endTime)
            {
                // 대기
            }
            
            Debug.Log("[AutoTestRunner] Step 1: Checking systems...");
            CheckSystems();
            
            // 1초 대기
            endTime = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < endTime)
            {
                // 대기
            }
            
            Debug.Log("[AutoTestRunner] Step 2: Capturing screenshot...");
            CaptureScreenshot();
            
            // 1초 대기
            endTime = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < endTime)
            {
                // 대기
            }
            
            Debug.Log("[AutoTestRunner] Step 3: Logging results...");
            LogResults();
            
            Debug.Log("[AutoTestRunner] Test completed, quitting...");
            UnityEditor.EditorApplication.Exit(0);
        }

    private void CheckSystems()
    {
        Debug.Log("[AutoTestRunner] === System Check ===");

        // Player 확인
        var player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            Debug.Log($"[AutoTestRunner] ✅ Player found: {player.name} at {player.transform.position}");
            Debug.Log($"  Components: {player.GetComponents<Component>().Length}");
            
            var cam = GameObject.Find("Player Camera");
            if (cam != null)
            {
                var camera = cam.GetComponent<Camera>();
                Debug.Log($"[AutoTestRunner] ✅ Player Camera: {cam.name}, active: {cam.activeInHierarchy}, Camera.enabled: {camera?.enabled}");
            }
            else
            {
                Debug.LogWarning("[AutoTestRunner] ⚠️ Player Camera not found!");
            }
        }
        else
        {
            Debug.LogError("[AutoTestRunner] ❌ Player NOT FOUND!");
        }

        // 주요 시스템들
        var systems = new (string, System.Type)[]
        {
            ("GameSetup", typeof(GameSetup)),
            ("MonsterSpawner", System.Type.GetType("MonsterSpawner, Assembly-CSharp")),
            ("HUD", System.Type.GetType("HUD, Assembly-CSharp")),
            ("MinimapUI", System.Type.GetType("MinimapUI, Assembly-CSharp")),
            ("EventSystem", typeof(UnityEngine.EventSystems.EventSystem)),
            ("BuffManager", System.Type.GetType("BuffManager, Assembly-CSharp")),
            ("NationTerrainController", System.Type.GetType("NationTerrainController, Assembly-CSharp")),
        };

        foreach (var (name, type) in systems)
        {
            if (type != null)
            {
                var obj = FindAnyObjectByType(type);
                Debug.Log($"[AutoTestRunner] {(obj != null ? "✅" : "❌")} {name}: {(obj != null ? "FOUND" : "MISSING")}");
            }
            else
            {
                Debug.Log($"[AutoTestRunner] ❓ {name}: Type not loaded");
            }
        }

        // 콘솔 에러 확인 (로그에서 에러 패턴 검색)
        Debug.Log("[AutoTestRunner] Check Unity Console for errors above ⬆️");
    }

    private void CaptureScreenshot()
    {
        Debug.Log($"[AutoTestRunner] Capturing screenshot to: {_targetScreenshotPath}");
        
        try
        {
            // ScreenCapture.CaptureScreenshot expects relative path from project root
            string relativePath = _targetScreenshotPath;
            string projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (relativePath.StartsWith(projectPath))
            {
                relativePath = relativePath.Substring(projectPath.Length + 1).Replace('\\', '/');
            }
            
            ScreenCapture.CaptureScreenshot(relativePath);
            Debug.Log($"[AutoTestRunner] ✅ Screenshot capture requested: {relativePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AutoTestRunner] ❌ Screenshot failed: {e.Message}");
        }
    }

    private void LogResults()
    {
        float elapsed = Time.realtimeSinceStartup - _startTime;
        Debug.Log("========================================");
        Debug.Log($"[AutoTestRunner] === Test Results (elapsed: {elapsed:F1}s) ===");
        Debug.Log("========================================");
        Debug.Log($"Screenshot: {_targetScreenshotPath}");
        Debug.Log($"File exists: {File.Exists(_targetScreenshotPath)}");
        
        if (File.Exists(_targetScreenshotPath))
        {
            var info = new FileInfo(_targetScreenshotPath);
            Debug.Log($"File size: {info.Length / 1024f:F1} KB");
        }

        Debug.Log("[AutoTestRunner] === Ready for Vision Analysis ===");
    }
}