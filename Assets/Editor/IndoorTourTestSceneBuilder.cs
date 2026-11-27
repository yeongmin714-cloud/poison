using System.IO;
using ProjectName.Systems;
using ProjectName.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [실내 관람 모드] Test_23_IndoorTour 씬 빌더 — 에디터 전용(씬 액셋 생성).
    ///
    /// 선례: Afterlife155TestSceneBuilder(빈 씬 → 부트 GO → SaveScene — YAML 수편집 회피).
    ///
    /// 구성(실내 관람 계획서 §3):
    ///   - GameObject "IndoorTourTestSetup"에 CaveInteriorTestSetup(동굴 부트/플레이어/카메라 경로 재사용 —
    ///     씬 게이트 2종 허용 수정본) + IndoorTourMode(순회/키/라벨) 부착
    ///   - Directional Light 1개(Unity 신규 씬 템플릿 값 — 런타임 보정은 CaveInteriorTestSetup이 담당)
    ///   - Assets/Scenes/TestScenes/Test_23_IndoorTour.unity 저장(기존 파일 있으면 덮어씀)
    ///
    /// 실행(부모 몫 — 이 스크립트는 작성까지만):
    ///   Unity.exe -batchmode -projectPath C:/Unity/code -executeMethod
    ///     ProjectName.EditorTools.IndoorTourTestSceneBuilder.CreateScene -quit
    ///   또는 에디터 메뉴: Tools/IndoorTour/Test_23_IndoorTour 씬 생성
    /// </summary>
    public static class IndoorTourTestSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/TestScenes/Test_23_IndoorTour.unity";
        private const string BootName = "IndoorTourTestSetup";

        [MenuItem("Tools/IndoorTour/Test_23_IndoorTour 씬 생성")]
        public static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var boot = new GameObject(BootName);
            SceneManager.MoveGameObjectToScene(boot, scene);
            boot.AddComponent<CaveInteriorTestSetup>();   // 동굴 부트 — Awake 씬 게이트 2종(Test_21/Test_23) 통과
            boot.AddComponent<IndoorTourMode>();          // 관람 순회 — Start에서 DontDestroyOnLoad 드라이버로 이관

            // Directional Light 1개 — 템플릿 값(회전 50/-30/0, 강도 1.0, Soft 그림자).
            var lightGO = new GameObject("Directional Light");
            SceneManager.MoveGameObjectToScene(lightGO, scene);
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.0f;
            light.shadows = LightShadows.Soft;
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            string dir = Path.GetDirectoryName(ScenePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))   // 기존 파일 있으면 덮어씀
                throw new IOException($"[IndoorTourTestSceneBuilder] 저장 실패 — {ScenePath}");
            AssetDatabase.Refresh();

            Debug.Log($"[IndoorTourTestSceneBuilder] DONE — {ScenePath} 생성" +
                      "(IndoorTourTestSetup: CaveInteriorTestSetup + IndoorTourMode + Directional Light 1개).");
        }
    }
}
