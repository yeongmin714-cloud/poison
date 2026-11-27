using System.Collections.Generic;
using System.IO;
using ProjectName.Systems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [155차 P2] Test_22_AfterlifeCemetery 씬 빌더 — 에디터 전용(씬 액셋 생성).
    ///
    /// 구성(155차 P2 스펙):
    ///   - EditorSceneManager.NewScene(EmptyScene, Single)
    ///   - GameObject "Boot" + AddComponent&lt;CoreSystemsBootstrap&gt;() + AddComponent&lt;AfterlifeCemeteryTestSetup&gt;()
    ///   - Assets/Scenes/TestScenes/Test_22_AfterlifeCemetery.unity 저장(기존 파일 있으면 덮어씀)
    ///
    /// 선례: PlayerCastleInteriorPlaytestSceneBuilder(빈 씬 → 부트 GO → SaveScene — YAML 수편집 회피).
    ///   Test_21 씬 실측(1컴포넌트 최소 구성) 준용: CoreSystemsBootstrap이 편집 모드 Awake(ExecuteInEditMode)에서
    ///   생성하는 싱글턴 오브젝트(TerritoryManager 등)는 저장 전 제거 — Play 시 동일 Awake이 재생성(멱등).
    ///   _buildAllTerritories=false(직렬화 필드 — 월드 전수 빌드 생략, 부트 BuildPaused 이중 가드와 동일 취지).
    ///
    /// 실행(부모 몫 — 이 스크립트는 작성까지만):
    ///   Unity.exe -batchmode -projectPath C:/Unity/code -executeMethod
    ///     ProjectName.EditorTools.Afterlife155TestSceneBuilder.RunAll -quit
    ///   또는 에디터 메뉴: Tools/Afterlife/155차 P2 — Test_22_AfterlifeCemetery 씬 생성
    /// </summary>
    public static class Afterlife155TestSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/TestScenes/Test_22_AfterlifeCemetery.unity";
        private const string BootName = "Boot";

        [MenuItem("Tools/Afterlife/155차 P2 — Test_22_AfterlifeCemetery 씬 생성")]
        public static void RunAll()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 저장 전 루트 스냅샷 — 부트 편집 모드 Awake이 즉석 생성한 싱글턴은 저장 전 정리(재생성 몫).
            var before = new HashSet<string>();
            foreach (var rootGo in scene.GetRootGameObjects())
                before.Add(rootGo.name);

            var boot = new GameObject(BootName);
            SceneManager.MoveGameObjectToScene(boot, scene);

            var bootstrap = boot.AddComponent<CoreSystemsBootstrap>();   // 편집 모드에서도 Awake 즉시 실행(ExecuteInEditMode)
            var so = new SerializedObject(bootstrap);
            var prop = so.FindProperty("_buildAllTerritories");
            if (prop != null)
            {
                // [155차 P2] 월드 전수 빌드 생략 — 테스트씬 경량화(24개 영지/마을 배치 불요).
                prop.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            boot.AddComponent<AfterlifeCemeteryTestSetup>();   // 사망 체인 부트(Play 시 Awake — 씬 이름 게이트 통과)

            // 부트가 아닌 신규 루트(싱글턴 오브젝트) 제거 — Test_21 최소 구성 실측 준용.
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root == null) continue;   // 부트 Awake 정리로 이미 파괴된 루트 스킵
                if (root == boot || before.Contains(root.name)) continue;
                string doomed = root.name;    // [수정] 파괴 전 캡처 — 파괴 후 name 접근은 MissingReferenceException
                Object.DestroyImmediate(root);
                Debug.Log($"[Afterlife155TestSceneBuilder] 편집 모드 싱글턴 정리 — {doomed} (Play 시 재생성)");
            }

            string dir = Path.GetDirectoryName(ScenePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))   // 기존 파일 있으면 덮어씀
                throw new IOException($"[Afterlife155TestSceneBuilder] 저장 실패 — {ScenePath}");
            AssetDatabase.Refresh();

            Debug.Log($"[Afterlife155TestSceneBuilder] DONE — {ScenePath} 생성(Boot: CoreSystemsBootstrap + " +
                      "AfterlifeCemeteryTestSetup). Play 시 1.5s 후 즉사 → 이계 자동 진입 체인 발화.");
        }
    }
}
