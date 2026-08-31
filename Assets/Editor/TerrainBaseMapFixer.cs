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
    // Guard so the fix runs only once per domain reload (safe, static resets on script reload).
    private static bool _fixed;

    static TerrainBaseMapFixer()
    {
        // (b) Run check whenever a scene opens.
        EditorSceneManager.sceneOpened += OnSceneOpened;

        // (a) Also run once on editor startup / first load (AssetDatabase.Refresh needed).
        EditorApplication.delayCall += OnDelayCall;
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

    private static void FixIfNeeded()
    {
        // Run once per domain reload only; static resets on script reload so the editor
        // will re-run the guard automatically after a restart.
        if (_fixed) return;
        _fixed = true;

        FixMaterial("Assets/URP/Ground_Grass_Mat.mat", "Assets/URP/Terrain_Grass.asset");
        FixMaterial("Assets/Resources/URP/Ground_Grass_Mat.mat", "Assets/URP/Terrain_Grass.asset");
    }

    private static void FixMaterial(string materialPath, string texturePath)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (mat == null) return;

        // _BaseMap is empty -> recover it.
        if (mat.GetTexture("_BaseMap") == null)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (tex == null)
            {
                Debug.LogWarning("[TerrainBaseMapFixer] 텍스처를 찾을 수 없음: " + texturePath);
                return;
            }

            mat.SetTexture("_BaseMap", tex);
            mat.SetTexture("_MainTex", tex);
            mat.SetTextureScale("_BaseMap", new Vector2(200f, 200f));

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();

            Debug.LogWarning("[TerrainBaseMapFixer] " + materialPath + " 의 _BaseMap이 비어 있어 복구함 (재발 방지) -> " + texturePath);
        }
    }
}