using System.IO;
using ProjectName.Systems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectName.EditorTools
{
    /// <summary>Creates a dedicated, isolated Play scene without hand-editing Unity scene YAML.</summary>
    public static class PlayerCastleInteriorPlaytestSceneBuilder
    {
        private const string TestScenePath = "Assets/Scenes/TestScenes/Test_PlayerCastleInterior.unity";

        [MenuItem("Tools/Indoor/테스트 씬 생성/플레이어 성 내부 플레이 테스트")]
        public static void CreateOrOpenTestScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("[PlayerCastleInteriorPlaytest] Current scene save was cancelled; no test scene changes made.");
                return;
            }

            EnsureTestSceneAsset();
            EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
            Debug.Log("[PlayerCastleInteriorPlaytest] Dedicated play scene opened. Press Play; use WASD/arrow keys to move.");
        }

        public static void EnsureTestSceneAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScenePath) == null)
            {
                CreateTestSceneAsset();
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScenePath) == null)
                    throw new IOException("Unity did not import " + TestScenePath);
            }
        }

        private static void CreateTestSceneAsset()
        {
            string directory = Path.GetDirectoryName(TestScenePath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject bootstrap = new GameObject("PlayerCastleInteriorPlaytest");
            SceneManager.MoveGameObjectToScene(bootstrap, scene);
            bootstrap.AddComponent<PlayerCastleInteriorPlaytest>();

            GameObject keyLight = new GameObject("PlaytestDirectionalLight");
            SceneManager.MoveGameObjectToScene(keyLight, scene);
            keyLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light light = keyLight.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, TestScenePath))
                throw new IOException("Failed to save " + TestScenePath);

            AssetDatabase.Refresh();
        }

    }
}
