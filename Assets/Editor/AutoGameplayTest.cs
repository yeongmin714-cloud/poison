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
            
            // 마을 뷰 검증 — 대표 마을 위로 카메라를 이동해 건물/NPC/상점을 촬영
                        Debug.Log("[AutoTestRunner] Step 2.5: Framing representative village...");
                        FrameRepresentativeVillage();

                        // 주민 NPC 존재 확인
                        var npcRoot = GameObject.Find("VillageNpcs_Root");
                        string npcLog = npcRoot != null ? "✅ VillageNpcs_Root FOUND (children=" + npcRoot.transform.childCount + ")" : "❌ VillageNpcs_Root MISSING";
                        Debug.Log("[AutoTestRunner] " + npcLog);

                        // 건물 존재 확인
                        var villagesRoot = GameObject.Find("Villages_Root");
                        string vilLog = villagesRoot != null ? "✅ Villages_Root FOUND (children=" + villagesRoot.transform.childCount + ")" : "❌ Villages_Root MISSING";
                        Debug.Log("[AutoTestRunner] " + vilLog);

                        // 1초 대기 (씬 안정화 후 촬영)
                        endTime = Time.realtimeSinceStartup + 1f;
                        while (Time.realtimeSinceStartup < endTime)
                        {
                            // 대기
                        }

                        Debug.Log("[AutoTestRunner] Step 2: Capturing screenshot...");
                        CaptureScreenshot();

                        // 상점 GLB 렌더 확인을 위한 추가 촬영 (카메라 전환)
                        endTime = Time.realtimeSinceStartup + 1f;
                        while (Time.realtimeSinceStartup < endTime)
                        {
                            // 대기
                        }
                        CaptureScreenshot2();

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

    /// <summary>상점/건물 근접 클로즈업 추가 촬영 경로 (마을 뷰 2차 프레임).</summary>
    private void CaptureScreenshot2()
    {
        string p2 = _targetScreenshotPath.Replace(".png", "_village2.png");
        try
        {
            string projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string rel = p2;
            if (p2.StartsWith(projectPath))
                rel = p2.Substring(projectPath.Length + 1).Replace('\\', '/');
            ScreenCapture.CaptureScreenshot(rel);
            Debug.Log($"[AutoTestRunner] ✅ Village screenshot captured: {rel}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AutoTestRunner] ❌ Screenshot2 failed: {e.Message}");
        }
    }

    /// <summary>첫 대표 마을(Village_{nation}_00) 위로 카메라를 이동·프레임해 건물/NPC/상점이 보이게 한다.</summary>
    private void FrameRepresentativeVillage()
    {
        // 대표 마을 오브젝트 4국가 시도 (East_00부터)
        string[] tries = { "Village_East_00", "Village_North_00", "Village_West_00", "Village_South_00" };
        Transform target = null;
        foreach (var t in tries)
        {
            var go = GameObject.Find(t);
            if (go != null) { target = go.transform; break; }
        }
        if (target == null)
        {
            Debug.LogWarning("[AutoTestRunner] 대표 마을 오브젝트 없음 — 마을 프레임 생략");
            return;
        }

        // 카메라 찾기
        var cam = Camera.main;
        if (cam == null)
        {
            var camObj = GameObject.Find("Player Camera") ?? GameObject.Find("Main Camera");
            if (camObj != null) cam = camObj.GetComponent<Camera>();
        }

        Vector3 center = target.position;
        float height = TerrainFindHeight(center);

        if (cam != null)
        {
            // 마을 남쪽/동쪽 상공에서 중앙을 내려다보는 3/4 뷰
            Vector3 camPos = new Vector3(center.x + 42f, height + 34f, center.z + 30f);
            cam.transform.position = camPos;
            cam.transform.LookAt(new Vector3(center.x, height + 2f, center.z));
            Debug.Log($"[AutoTestRunner] 카메라 → 마을 {target.name} @ ({center.x:F1},{center.z:F1})");
        }
        else
        {
            Debug.LogWarning("[AutoTestRunner] 카메라 없음 — 마을 프레임 부정확");
        }
    }

    /// <summary>지표 높이 근사 — 마을 평탄화(GetHeightAt 단일소스)라 대표 마을 주변은 안정.</summary>
    private float TerrainFindHeight(Vector3 center)
    {
        try
        {
            // TerrainHeightApplier 메시 콜라이더 레이로 지표 추정
            if (Physics.Raycast(new Vector3(center.x, 200f, center.z), Vector3.down, out var hit, 400f))
                return hit.point.y;
        }
        catch (System.Exception) { }
        return center.y;
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