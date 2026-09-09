using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ProjectName.Systems;

/// <summary>
/// IndoorScene에 중세 기본 셸 + 실내 카메라를 영구 상주화 (2026-09-09 5차).
/// Tools/Indoor/... 메뉴 또는 배치(-executeMethod IndoorShellMenu.RunForBatch)로 실행.
/// </summary>
public static class IndoorShellMenu
{
    [MenuItem("Tools/Indoor/중세 기본 셸 생성+저장")]
    public static void BuildAndSaveFromMenu()
    {
        BuildAndSave();
    }

    /// <summary>배치 진입점: -executeMethod IndoorShellMenu.RunForBatch</summary>
    public static void RunForBatch()
    {
        BuildAndSave();
    }

    private static void BuildAndSave()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/IndoorScene.unity", OpenSceneMode.Single);

        // 기존 셸/카메라 정리 — 비활성 포함 전수 제거(누적 버그 수정)
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (go.name == "MedievalShell" || go.name == "IndoorCamera") Object.DestroyImmediate(go);
        }

        MedievalShellBuilder.CreateShell();

        // 실내 상주 Directional Light — 따뜻톤(어둠 근본 해소)
        var sun = new GameObject("InteriorSun", typeof(Light));
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        var sl = sun.GetComponent<Light>();
        sl.type = LightType.Directional;
        sl.color = new Color(1f, 0.9f, 0.75f);
        sl.intensity = 0.8f;

        // 실내 카메라 (메인 씬과 동일 시점 3/4뷰, 활성 상태로 저장 — IndoorScene 로드 시에만 존재)
        var camGO = new GameObject("IndoorCamera", typeof(Camera));
        camGO.transform.position = new Vector3(0f, 9.5f, -10.5f);
        camGO.transform.rotation = Quaternion.Euler(40f, 0f, 0f);
        var cam = camGO.GetComponent<Camera>();
        cam.fieldOfView = 50f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 80f;
        cam.clearFlags = CameraClearFlags.Skybox;
        camGO.AddComponent<ProjectName.Systems.IndoorCameraFollow>(); // 플레이어 추적(메인씬 동일 시점)

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[IndoorShellMenu] 중세 셸 + IndoorCamera 상주화 + 저장 완료 (셸 1루트, 카메라 1)");
    }
}
