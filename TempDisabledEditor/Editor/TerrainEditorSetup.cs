using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectName.Systems;
using ProjectName.Core.Data;

/// <summary>
/// Editor setup for PNG texture + GLB terrain application system.
/// Tools/Terrain/ 메뉴를 통해 지형 텍스처 및 오브젝트 배치를 실행한다.
/// Ground_Inner/Ground_Mid/Ground_Outer 3링 구조를 지원한다.
/// </summary>
public static class TerrainEditorSetup
{
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";

    // Ground name patterns
    private static readonly string[] GroundNames = { "Ground_Inner", "Ground_Mid", "Ground_Outer", "Ground" };

    // ================================================================
    //  Full Terrain Setup (원클릭)
    // ================================================================

    /// <summary>
    /// 전체 지형 설정을 한 번에 실행한다:
    /// 1. NationTerrainController 비활성화
    /// 2. Ground 3링에 PNG 텍스처 적용
    /// 3. 나무/바위/풀 GLB 배치
    /// </summary>
    [MenuItem("Tools/Terrain/Full Terrain Setup")]
    public static void FullTerrainSetup()
    {
        EnsureMainScene();

        Undo.IncrementCurrentGroup();
        int groupIndex = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Full Terrain Setup");

        // 1. Disable NationTerrainController on all Ground objects
        DisableNationControllers();

        // 2. Apply textures to all Ground objects
        ApplyTexturesInternal();

        // 3. Place props
        PlacePropsInternal();

        Undo.CollapseUndoOperations(groupIndex);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[TerrainEditorSetup] ✅ Full Terrain Setup 완료!");
        EditorUtility.DisplayDialog("Full Terrain Setup",
            "✅ 지형 설정 완료!\n\n" +
            "- 3링 Ground에 PNG 텍스처 적용\n" +
            "- 나무/바위/풀 GLB 배치\n" +
            "- NationTerrainController 비활성화",
            "OK");
    }

    [MenuItem("Tools/Terrain/Full Terrain Setup", true)]
    private static bool ValidateFullTerrainSetup() => true;

    // ================================================================
    //  Apply Terrain Textures
    // ================================================================

    /// <summary>
    /// PNG 텍스처를 Ground_Inner/Mid/Outer에 적용한다.
    /// 각 Ground에 동일한 Nation별 텍스처 Material을 생성/할당한다.
    /// </summary>
    [MenuItem("Tools/Terrain/Apply Terrain Textures")]
    public static void ApplyTerrainTextures()
    {
        EnsureMainScene();
        ApplyTexturesInternal();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    private static void ApplyTexturesInternal()
    {
        var grounds = FindGroundObjects();
        if (grounds.Count == 0)
        {
            Debug.LogError("[TerrainEditorSetup] Ground_Inner/Mid/Outer를 찾을 수 없습니다.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int groupIndex = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Apply Terrain Textures");

        // NationTerrainController 비활성화
        DisableNationControllers();

        // 타겟 Ground 이름 로그
        Debug.Log($"[TerrainEditorSetup] 대상 Ground: {string.Join(", ", grounds.Select(g => g.name))}");

        // 첫 번째 Ground에 TerrainTextureApplier 설정 (Material 생성 담당)
        GameObject primaryGround = grounds[0];
        var texApplier = primaryGround.GetComponent<TerrainTextureApplier>();
        if (texApplier == null)
        {
            texApplier = primaryGround.AddComponent<TerrainTextureApplier>();
            Undo.RegisterCreatedObjectUndo(texApplier, "Add TerrainTextureApplier");
            Debug.Log("[TerrainEditorSetup] TerrainTextureApplier added to " + primaryGround.name);
        }
        else
        {
            Undo.RecordObject(texApplier, "Enable TerrainTextureApplier");
            texApplier.enabled = true;
        }

        // 텍스처 로드 및 Material 생성
        texApplier.LoadTextures();
        texApplier.CreateMaterials();

        // 모든 Ground에 동일한 Material 할당
        var materials = texApplier.NationMaterials;
        if (materials == null || materials.Count == 0)
        {
            Debug.LogError("[TerrainEditorSetup] 생성된 Material이 없습니다.");
            return;
        }

        foreach (var ground in grounds)
        {
            var renderer = ground.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                Debug.LogWarning($"[TerrainEditorSetup] {ground.name}에 MeshRenderer 없음.");
                continue;
            }

            Undo.RecordObject(renderer, "Assign terrain material");

            // NationType.East Material을 기본으로 할당
            if (materials.TryGetValue(NationType.East, out Material eastMat))
            {
                renderer.sharedMaterial = eastMat;
                Debug.Log($"[TerrainEditorSetup] {ground.name} ← East(초원) Material 할당");
            }
            else if (materials.Count > 0)
            {
                renderer.sharedMaterial = materials.Values.First();
                Debug.Log($"[TerrainEditorSetup] {ground.name} ← {materials.Keys.First()} Material 할당");
            }
        }

        Undo.CollapseUndoOperations(groupIndex);
        Debug.Log($"[TerrainEditorSetup] ✅ {grounds.Count}개 Ground에 텍스처 적용 완료.");
    }

    [MenuItem("Tools/Terrain/Apply Terrain Textures", true)]
    private static bool ValidateApplyTerrainTextures() => true;

    // ================================================================
    //  Place Props (나무/바위/풀 GLB 배치)
    // ================================================================

    /// <summary>
    /// 나무/바위/풀 GLB를 지형 전역에 랜덤 배치한다.
    /// </summary>
    [MenuItem("Tools/Terrain/Place Props")]
    public static void PlaceProps()
    {
        EnsureMainScene();
        PlacePropsInternal();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    private static void PlacePropsInternal()
    {
        var grounds = FindGroundObjects();
        if (grounds.Count == 0)
        {
            Debug.LogError("[TerrainEditorSetup] Ground를 찾을 수 없습니다.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int groupIndex = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Place Props");

        // Props 부모 오브젝트 생성/확인
        string parentName = "TerrainProps";
        Transform propsParent = GameObject.Find(parentName)?.transform;
        if (propsParent == null)
        {
            var parentObj = new GameObject(parentName);
            Undo.RegisterCreatedObjectUndo(parentObj, "Create TerrainProps parent");
            propsParent = parentObj.transform;
            Debug.Log("[TerrainEditorSetup] TerrainProps 부모 오브젝트 생성.");
        }

        // 기존 Props 제거 (중복 배치 방지)
        var existingProps = propsParent.GetComponentsInChildren<Transform>();
        foreach (var child in existingProps)
        {
            if (child != propsParent)
            {
                Undo.DestroyObjectImmediate(child.gameObject);
            }
        }

        // 첫 번째 Ground에 TerrainPropPlacer 설정
        GameObject primaryGround = grounds[0];
        var propPlacer = primaryGround.GetComponent<TerrainPropPlacer>();
        if (propPlacer == null)
        {
            propPlacer = primaryGround.AddComponent<TerrainPropPlacer>();
            Undo.RegisterCreatedObjectUndo(propPlacer, "Add TerrainPropPlacer");
        }
        else
        {
            Undo.RecordObject(propPlacer, "Re-place Props");
        }

        // Props 부모 할당 및 실행
        propPlacer.SetPropsParent(propsParent);
        propPlacer.LoadGLBs();
        propPlacer.CreateFallbacks();
        propPlacer.PlaceProps();

        Undo.CollapseUndoOperations(groupIndex);
        Debug.Log("[TerrainEditorSetup] ✅ Props 배치 완료.");
    }

    [MenuItem("Tools/Terrain/Place Props", true)]
    private static bool ValidatePlaceProps() => true;

    // ================================================================
    //  Disable NationTerrainController
    // ================================================================

    [MenuItem("Tools/Terrain/Disable NationTerrainController")]
    public static void DisableNationTerrainController()
    {
        EnsureMainScene();
        DisableNationControllers();
    }

    private static void DisableNationControllers()
    {
        var grounds = FindGroundObjects();
        int count = 0;
        foreach (var ground in grounds)
        {
            var nationCtrl = ground.GetComponent<NationTerrainController>();
            if (nationCtrl != null && nationCtrl.enabled)
            {
                Undo.RecordObject(nationCtrl, "Disable NationTerrainController");
                nationCtrl.enabled = false;
                count++;
                Debug.Log($"[TerrainEditorSetup] {ground.name}: NationTerrainController 비활성화.");
            }
        }
        if (count == 0)
            Debug.Log("[TerrainEditorSetup] 비활성화할 NationTerrainController가 없습니다.");
        else
            Debug.Log($"[TerrainEditorSetup] {count}개 NationTerrainController 비활성화 완료.");
    }

    [MenuItem("Tools/Terrain/Disable NationTerrainController", true)]
    private static bool ValidateDisableNationTerrainController() => true;

    // ================================================================
    //  Helpers
    // ================================================================

    /// <summary>
    /// Ground_Inner, Ground_Mid, Ground_Outer를 모두 찾는다.
    /// 없으면 "Ground" 단일 이름으로 찾는다.
    /// </summary>
    private static List<GameObject> FindGroundObjects()
    {
        var results = new List<GameObject>();
        foreach (string name in GroundNames)
        {
            var go = GameObject.Find(name);
            if (go != null && !results.Contains(go))
                results.Add(go);
        }
        return results;
    }

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
