using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Poly Haven 3D 모델 및 텍스처를 TopDownScene에 자동 배치.
/// glTFast으로 임포트된 모델을 분류하여 기존 도형(Sphere/Cylinder) 대체.
/// 
/// 분류 기준 (파일명 기반):
///   tree_*, fir_*, jacaranda_*      → 나무
///   boulder_*, cliff_*               → 바위/절벽
///   periwinkle_*, searsia_*          → 식물/덤불
///   ground_*, dirt_*, grass_*        → 지형 텍스처
/// </summary>
public static class Phase3_PolyHavenSetup
{
    private const string POLYHAVEN_PATH = "Assets/Resources/Models/PolyHeven";

    [MenuItem("Tools/Phase 3.9 - Poly Haven 모델 배치")]
    public static void SetupPolyHavenModels()
    {
        // TopDownScene 열기
        string scenePath = "Assets/Scenes/TopDownScene.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // 1. 임포트된 모델 검색 및 분류
        var importResults = ScanImportedModels();
        Debug.Log($"[PolyHaven] 발견: 나무 {importResults.trees.Count}종, 바위 {importResults.rocks.Count}종, 식물 {importResults.plants.Count}종");

        // 2. 기존 오브젝트 제거 (나무/바위/풀 등 Phase 3.6 프리미티브)
        int removedCount = CleanupOldPrimitives();
        Debug.Log($"[PolyHaven] 기존 도형 {removedCount}개 제거");

        // 3. 새 모델 배치
        int placedCount = 0;
        placedCount += PlaceTrees(importResults.trees);
        placedCount += PlaceRocks(importResults.rocks);
        placedCount += PlacePlants(importResults.plants);
        Debug.Log($"[PolyHaven] ✅ 새 모델 {placedCount}개 배치 완료");

        // 4. 씬 저장
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log($"[PolyHaven] ✅ TopDownScene 저장 완료");

        EditorApplication.Exit(0);
    }

    /// <summary>임포트된 모델 분류 결과</summary>
    private class ImportResults
    {
        public List<string> trees = new List<string>();
        public List<string> rocks = new List<string>();
        public List<string> plants = new List<string>();
    }

    /// <summary>
    /// PolyHeven 폴더에서 glTFast로 임포트된 모델 검색 + 분류
    /// </summary>
    private static ImportResults ScanImportedModels()
    {
        var results = new ImportResults();

        if (!AssetDatabase.IsValidFolder(POLYHAVEN_PATH))
        {
            Debug.LogWarning($"[PolyHaven] 폴더 없음: {POLYHAVEN_PATH}");
            return results;
        }

        string[] subdirs = Directory.GetDirectories(POLYHAVEN_PATH);
        foreach (string dir in subdirs)
        {
            string dirName = Path.GetFileName(dir).ToLower();

            // .blend 폴더는 텍스처 소스 — 모델 아님
            if (dirName.EndsWith(".blend")) continue;
            // glTF 폴더
            if (!dirName.EndsWith(".gltf")) continue;

            // .gltf 파일 찾기
            string[] gltfFiles = Directory.GetFiles(dir, "*.gltf");
            if (gltfFiles.Length == 0) continue;

            string relativePath = gltfFiles[0].Replace("\\", "/");
            // Assets 경로로 변환
            int assetsIdx = relativePath.IndexOf("Assets/");
            if (assetsIdx < 0) continue;
            relativePath = relativePath.Substring(assetsIdx);

            // 파일명으로 분류
            string baseName = Path.GetFileNameWithoutExtension(dirName);

            if (baseName.Contains("tree") || baseName.Contains("fir") || baseName.Contains("jacaranda"))
            {
                results.trees.Add(relativePath);
                Debug.Log($"[PolyHaven] 🌲 나무 발견: {baseName}");
            }
            else if (baseName.Contains("boulder") || baseName.Contains("cliff"))
            {
                results.rocks.Add(relativePath);
                Debug.Log($"[PolyHaven] 🪨 바위 발견: {baseName}");
            }
            else if (baseName.Contains("periwinkle") || baseName.Contains("searsia"))
            {
                results.plants.Add(relativePath);
                Debug.Log($"[PolyHaven] 🌿 식물 발견: {baseName}");
            }
            else
            {
                Debug.Log($"[PolyHaven] 미분류: {baseName} → 식물로 처리");
                results.plants.Add(relativePath);
            }
        }

        return results;
    }

    /// <summary>
    /// 기존 프리미티브 도형(나무/바위/풀/덤불/꽃) 제거
    /// </summary>
    private static int CleanupOldPrimitives()
    {
        int count = 0;

        // DestroyImmediate가 배열을 변경하므로, 이름을 먼저 수집 후 제거
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>();
        List<GameObject> toRemove = new List<GameObject>();

        foreach (var go in allObjects)
        {
            if (go == null) continue;
            if (go.name.StartsWith("Pillar_") ||
                go.name.StartsWith("Tree_") ||
                go.name.StartsWith("Rock_") ||
                go.name.StartsWith("Grass_") ||
                go.name.StartsWith("Bush_") ||
                go.name.StartsWith("Flower_") ||
                go.name == "Ground")
            {
                // Ground는 제거하지 않음
                if (go.name == "Ground") continue;
                toRemove.Add(go);
            }
        }

        // EnvironmentalDetails 오브젝트
        GameObject envParent = GameObject.Find("EnvironmentalDetails");
        if (envParent != null) toRemove.Add(envParent);

        // 안전하게 제거
        foreach (var go in toRemove)
        {
            if (go != null)
            {
                Object.DestroyImmediate(go);
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// 나무 모델 배치 (2000×2000 맵에 분산)
    /// </summary>
    private static int PlaceTrees(List<string> treePaths)
    {
        if (treePaths.Count == 0)
        {
            Debug.LogWarning("[PolyHaven] 배치할 나무 모델 없음");
            return 0;
        }

        int count = 0;
        int treesPerType = 25;
        System.Random rng = new System.Random(42);

        foreach (string path in treePaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            for (int i = 0; i < treesPerType; i++)
            {
                float angle = (float)(rng.NextDouble() * 360f) * Mathf.Deg2Rad;
                float radius = (float)(rng.NextDouble() * 850f + 50f); // 50~900m
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;

                Vector3 pos = new Vector3(x, 0f, z);
                // 플레이어 스폰 지역 피하기
                if (Vector3.Distance(pos, new Vector3(0, 0, -950)) < 30f) continue;

                GameObject instance = (GameObject)Object.Instantiate(prefab, pos, Quaternion.identity);
                instance.name = $"Tree_{prefab.name}_{i}";

                // 랜덤 회전 + 크기 변형
                instance.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                float scale = (float)(rng.NextDouble() * 0.4f + 0.8f); // 0.8~1.2
                instance.transform.localScale = Vector3.one * scale;

                // Y축 보정 (glTF 모델의 바닥면이 지면에 닿도록)
                instance.transform.position = new Vector3(pos.x, 0f, pos.z);

                count++;
            }
        }

        Debug.Log($"[PolyHaven] 🌲 나무 {count}개 배치");
        return count;
    }

    /// <summary>
    /// 바위 모델 배치
    /// </summary>
    private static int PlaceRocks(List<string> rockPaths)
    {
        if (rockPaths.Count == 0) return 0;

        int count = 0;
        int rocksPerType = 30;
        System.Random rng = new System.Random(43);

        foreach (string path in rockPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            for (int i = 0; i < rocksPerType; i++)
            {
                float angle = (float)(rng.NextDouble() * 360f) * Mathf.Deg2Rad;
                float radius = (float)(rng.NextDouble() * 900f + 30f);
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;

                Vector3 pos = new Vector3(x, 0f, z);
                if (Vector3.Distance(pos, new Vector3(0, 0, -950)) < 30f) continue;

                GameObject instance = (GameObject)Object.Instantiate(prefab, pos, Quaternion.identity);
                instance.name = $"Rock_{prefab.name}_{i}";
                instance.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                float scale = (float)(rng.NextDouble() * 0.6f + 0.7f);
                instance.transform.localScale = Vector3.one * scale;
                instance.transform.position = new Vector3(pos.x, 0f, pos.z);

                count++;
            }
        }

        Debug.Log($"[PolyHaven] 🪨 바위 {count}개 배치");
        return count;
    }

    /// <summary>
    /// 식물/덤불 모델 배치
    /// </summary>
    private static int PlacePlants(List<string> plantPaths)
    {
        if (plantPaths.Count == 0) return 0;

        int count = 0;
        int plantsPerType = 40;
        System.Random rng = new System.Random(44);

        foreach (string path in plantPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            for (int i = 0; i < plantsPerType; i++)
            {
                float angle = (float)(rng.NextDouble() * 360f) * Mathf.Deg2Rad;
                float radius = (float)(rng.NextDouble() * 880f + 20f);
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;

                Vector3 pos = new Vector3(x, 0f, z);
                if (Vector3.Distance(pos, new Vector3(0, 0, -950)) < 20f) continue;

                GameObject instance = (GameObject)Object.Instantiate(prefab, pos, Quaternion.identity);
                instance.name = $"Plant_{prefab.name}_{i}";
                instance.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                float scale = (float)(rng.NextDouble() * 0.5f + 0.6f);
                instance.transform.localScale = Vector3.one * scale;
                instance.transform.position = new Vector3(pos.x, 0f, pos.z);

                count++;
            }
        }

        Debug.Log($"[PolyHaven] 🌿 식물 {count}개 배치");
        return count;
    }

    [MenuItem("Tools/Phase 3.9 - Poly Haven 모델 배치", true)]
    private static bool Validate() => true;
}