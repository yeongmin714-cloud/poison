using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectName.Systems;

/// <summary>
/// Editor setup for PNG texture + GLB terrain application system.
/// Tools/Terrain/ 메뉴를 통해 지형 텍스처 및 오브젝트 배치를 실행한다.
/// 실행 시 NationTerrainController를 비활성화 처리한다.
/// </summary>
public static class TerrainEditorSetup
{
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";

    // ================================================================
    //  Apply Terrain Textures
    // ================================================================

    /// <summary>
    /// PNG 텍스처를 Ground에 적용한다.
    /// NationTerrainController를 비활성화하고 TerrainTextureApplier로 대체한다.
    /// </summary>
    [MenuItem("Tools/Terrain/Apply Terrain Textures")]
    public static void ApplyTerrainTextures()
    {
        EnsureMainScene();

        GameObject ground = GameObject.Find("Ground");
        if (ground == null)
        {
            Debug.LogError("[TerrainEditorSetup] Ground GameObject not found in scene.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int groupIndex = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Apply Terrain Textures");

        // 1. Disable NationTerrainController
        var nationCtrl = ground.GetComponent<NationTerrainController>();
        if (nationCtrl != null)
        {
            Undo.RecordObject(nationCtrl, "Disable NationTerrainController");
            nationCtrl.enabled = false;
            Debug.Log("[TerrainEditorSetup] NationTerrainController disabled.");
        }
        else
        {
            Debug.Log("[TerrainEditorSetup] No NationTerrainController found on Ground.");
        }

        // 2. Add or enable TerrainTextureApplier
        var texApplier = ground.GetComponent<TerrainTextureApplier>();
        if (texApplier == null)
        {
            texApplier = ground.AddComponent<TerrainTextureApplier>();
            Undo.RegisterCreatedObjectUndo(texApplier, "Add TerrainTextureApplier");
            Debug.Log("[TerrainEditorSetup] TerrainTextureApplier added to Ground.");
        }
        else
        {
            Undo.RecordObject(texApplier, "Enable TerrainTextureApplier");
            texApplier.enabled = true;
            Debug.Log("[TerrainEditorSetup] TerrainTextureApplier already present, enabled.");
        }

        // 3. Trigger texture application
        texApplier.LoadTextures();
        texApplier.CreateMaterials();
        texApplier.ApplyMaterialForNation(ProjectName.Core.Data.NationType.East);

        Undo.CollapseUndoOperations(groupIndex);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[TerrainEditorSetup] ✅ Terrain textures applied via TerrainTextureApplier.");
    }

    [MenuItem("Tools/Terrain/Apply Terrain Textures", true)]
    private static bool ValidateApplyTerrainTextures() => true;

    // ================================================================
    //  Place Props
    // ================================================================

    /// <summary>
    /// 나무/바위/풀 GLB를 지형에 랜덤 배치한다.
    /// TerrainPropPlacer를 Ground에 추가/사용하여 실행한다.
    /// </summary>
    [MenuItem("Tools/Terrain/Place Props")]
    public static void PlaceProps()
    {
        EnsureMainScene();

        GameObject ground = GameObject.Find("Ground");
        if (ground == null)
        {
            Debug.LogError("[TerrainEditorSetup] Ground GameObject not found in scene.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int groupIndex = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Place Props");

        // Find or add TerrainPropPlacer
        var propPlacer = ground.GetComponent<TerrainPropPlacer>();
        if (propPlacer == null)
        {
            propPlacer = ground.AddComponent<TerrainPropPlacer>();
            Undo.RegisterCreatedObjectUndo(propPlacer, "Add TerrainPropPlacer");
            Debug.Log("[TerrainEditorSetup] TerrainPropPlacer added to Ground.");
        }
        else
        {
            Undo.RecordObject(propPlacer, "Re-place Props");
        }

        // Trigger prop placement
        propPlacer.LoadGLBs();
        propPlacer.CreateFallbacks();
        propPlacer.PlaceProps();

        Undo.CollapseUndoOperations(groupIndex);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[TerrainEditorSetup] ✅ Props placed on terrain.");
    }

    [MenuItem("Tools/Terrain/Place Props", true)]
    private static bool ValidatePlaceProps() => true;

    // ================================================================
    //  Disable NationTerrainController (standalone tool)
    // ================================================================

    [MenuItem("Tools/Terrain/Disable NationTerrainController")]
    public static void DisableNationTerrainController()
    {
        EnsureMainScene();

        GameObject ground = GameObject.Find("Ground");
        if (ground == null)
        {
            Debug.LogError("[TerrainEditorSetup] Ground not found.");
            return;
        }

        var nationCtrl = ground.GetComponent<NationTerrainController>();
        if (nationCtrl != null)
        {
            Undo.RecordObject(nationCtrl, "Disable NationTerrainController");
            nationCtrl.enabled = false;
            Debug.Log("[TerrainEditorSetup] NationTerrainController disabled.");
        }
        else
        {
            Debug.Log("[TerrainEditorSetup] No NationTerrainController found.");
        }
    }

    [MenuItem("Tools/Terrain/Disable NationTerrainController", true)]
    private static bool ValidateDisableNationTerrainController() => true;

    // ================================================================
    //  Helpers
    // ================================================================

    private static void EnsureMainScene()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene == null || string.IsNullOrEmpty(scene.path))
        {
            string[] guids = AssetDatabase.FindAssets("t:Scene MainScene");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                Debug.Log($"[TerrainEditorSetup] Opened MainScene: {path}");
            }
            else
            {
                Debug.LogWarning("[TerrainEditorSetup] No MainScene found. Using current scene.");
            }
        }
    }
}