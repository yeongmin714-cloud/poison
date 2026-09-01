using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Auto-fix Ground_Grass_Mat._BaseMap when it is empty (was lost/reset).
/// URP/Lit shows black albedo when _BaseMap is empty, making terrain invisible.
/// Reassigns Terrain_Grass texture whenever the scene opens or on first editor load.
/// </summary>
[InitializeOnLoad]
public static class TerrainBaseMapFixer
{
    static TerrainBaseMapFixer()
    {
        // (b) Run check whenever a scene opens.
        EditorSceneManager.sceneOpened += OnSceneOpened;

        // (a) Also run once on editor startup / first load (AssetDatabase.Refresh needed).
        EditorApplication.delayCall += OnDelayCall;

        // (c) Batch-safe safety net: -quit / headless 모드에서는 delayCall이 보장되지 않으므로
        //     EditorApplication.update를 통해 매 도메인 로드 후 즉시 FixIfNeeded를 태운다.
        //     복구가 완료되면(더 이상 _BaseMap이 비어있지 않으면) 스스로 구독을 해제해 오버헤드를 막는다.
        EditorApplication.update += OnUpdate;
    }

    private static void OnUpdate()
    {
        FixIfNeeded(); // 호출마다 항상 검사 → 복구 완료되면 update는 안쪽에서 자동 해제
    }

    private static void OnDelayCall()
    {
        AssetDatabase.Refresh();
        FixIfNeeded();
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        // Only fix for MainScene.
        if (scene.name != "MainScene") return;

        FixIfNeeded();
    }

    // 도메인당 1회 제약(_fixed)은 제거. 매 호출마다 항상 두 .mat의 _BaseMap을 검사하고
    // 비어있으면 복구한다. 이로써 자동 배치 재실행으로 _BaseMap이 다시 지워져도 즉시 살아난다.
    private static void FixIfNeeded()
    {
        bool allOk = true;
        allOk &= FixMaterial("Assets/URP/Ground_Grass_Mat.mat", "Assets/URP/Terrain_Grass.asset");
        allOk &= FixMaterial("Assets/Resources/URP/Ground_Grass_Mat.mat", "Assets/URP/Terrain_Grass.asset");

        // 복구가 모두 완료되어 더 이상 할 일이 없으면 update 훅 해제 (배치/에디터 오버헤드 방지)
        if (allOk)
            EditorApplication.update -= OnUpdate;
    }

    // _BaseMap이 null/비면 복구. true = 복구 후 _BaseMap이 정상(더 이상 작업 불필요), false = 아직 텍스처 부족.
    private static bool FixMaterial(string materialPath, string texturePath)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (mat == null) return true; // 아직 생성되지 않은 경로면 복구 대상 아님 → 정상 취급

        // _BaseMap is empty -> recover it.
        if (mat.GetTexture("_BaseMap") == null)
        {
            // 실제 초록 PNG east_grass1(guid caaecd65a5efab84a8ec7eacc2b077a6)를 최우선으로,
            // 없으면 절차 생성된 Terrain_Grass로 대체한다.
            var tex = ResolveBaseMapTexture(texturePath);
            if (tex == null)
            {
                Debug.LogWarning("[TerrainBaseMapFixer] 텍스처를 찾을 수 없음: " + texturePath +
                                 " (GUID caaecd65a5efab84a8ec7eacc2b077a6도 비어 있음)");
                return false;
            }

            mat.SetTexture("_BaseMap", tex);
            mat.SetTexture("_MainTex", tex);
            mat.SetTextureScale("_BaseMap", new Vector2(200f, 200f));

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();

            Debug.LogWarning("[TerrainBaseMapFixer] " + materialPath + " 의 _BaseMap이 비어 있어 복구함 (재발 방지) -> " + tex.name);
        }
        return true;
    }

    // east_grass1(guid) 우선, 실패 시 전달받은 절차 텍스처 경로로 대체.
    private static Texture2D ResolveBaseMapTexture(string fallbackPath)
    {
        string grassGuid = AssetDatabase.GUIDToAssetPath("caaecd65a5efab84a8ec7eacc2b077a6");
        if (!string.IsNullOrEmpty(grassGuid))
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(grassGuid);
            if (tex != null) return tex;
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(fallbackPath);
    }
}