using UnityEditor;
using UnityEngine;
using ProjectName.Systems;

/// <summary>
/// [5.1] Phase 5 — TownBuilder Editor Menu
/// 
/// Tools/Phase 5/Build Town 메뉴를 통해 에디터에서 영지 건물/병사 배치를 테스트합니다.
/// 영지 선택 드롭다운으로 여러 영지 레이아웃 중 선택 가능.
/// </summary>
public class Phase5_TownBuilderMenu : EditorWindow
{
    // 테스트용 영지 목록
    private static readonly string[] TerritoryOptions =
    {
        "East_01 — 리카드 영지",
        "East_02 — 동부 개척지",
        "West_01 — 서부 변경",
        "South_01 — 남부 항구",
        "North_01 — 북부 산악"
    };

    private static readonly string[] TerritoryIds =
    {
        "East_01",
        "East_02",
        "West_01",
        "South_01",
        "North_01"
    };

    private int _selectedIndex = 0;
    private Vector2 _scrollPos;

    [MenuItem("Tools/Phase 5/Build Town")]
    private static void ShowWindow()
    {
        // 기존 EditorWindow 인스턴스가 있으면 포커스, 없으면 새로 생성
        var window = GetWindow<Phase5_TownBuilderMenu>("Town Builder");
        window.minSize = new Vector2(320, 250);
        window.Show();
    }

    private void OnGUI()
    {
        GUILayout.Space(8);
        GUILayout.Label("🏗️ Town Builder — Phase 5", EditorStyles.boldLabel);
        GUILayout.Space(4);

        EditorGUILayout.HelpBox(
            "영지 건물/병사 Procedural 배치 도구입니다.\n" +
            "영지를 선택하고 'Build Town' 버튼을 클릭하면\n" +
            "해당 영지가 씬에 생성됩니다.",
            MessageType.Info
        );
        GUILayout.Space(8);

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        // 영지 선택 드롭다운
        EditorGUILayout.LabelField("영지 선택", EditorStyles.miniLabel);
        _selectedIndex = EditorGUILayout.Popup(
            _selectedIndex,
            TerritoryOptions
        );
        GUILayout.Space(4);

        // 선택된 영지 ID 표시
        string selectedId = TerritoryIds[_selectedIndex];
        EditorGUILayout.LabelField($"선택: {selectedId}", EditorStyles.miniBoldLabel);
        GUILayout.Space(12);

        // === 버튼 영역 ===
        EditorGUILayout.BeginHorizontal();

        GUI.enabled = !EditorApplication.isPlaying;
        if (GUILayout.Button("🏗️ Build Town", GUILayout.Height(36)))
        {
            BuildSelectedTown(selectedId);
        }
        GUI.enabled = true;

        if (GUILayout.Button("🗑️ Clear All", GUILayout.Height(36)))
        {
            ClearAllTowns();
        }

        EditorGUILayout.EndHorizontal();
        GUILayout.Space(6);

        // 빠른 액션 버튼
        EditorGUILayout.LabelField("빠른 액션", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Create Layout SO", GUILayout.Height(28)))
        {
            CreateLayoutAsset(selectedId);
        }

        if (GUILayout.Button("📋 Log Layout", GUILayout.Height(28)))
        {
            LogLayoutInfo(selectedId);
        }

        EditorGUILayout.EndHorizontal();
        GUILayout.Space(8);

        // 상태 메시지 영역
        EditorGUILayout.LabelField("상태", EditorStyles.miniLabel);
        EditorGUILayout.SelectableLabel(
            $"Ready. Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}\n" +
            $"Active: {TerritoryOptions[_selectedIndex]}",
            EditorStyles.helpBox,
            GUILayout.MinHeight(40)
        );

        EditorGUILayout.EndScrollView();
    }

    /// <summary>
    /// 선택된 영지 ID로 BuildTown 실행
    /// </summary>
    private static void BuildSelectedTown(string territoryId)
    {
        Debug.Log($"[Phase5Menu] 🏗️ Build Town: {territoryId}");

        if (EditorApplication.isPlaying)
        {
            // 런타임: TownBuilder.BuildTown(string) 사용
            TownBuilder.BuildTown(territoryId);
        }
        else
        {
            // 에디터 모드: 직접 호출 (정적 메서드)
            TownBuilder.BuildTown(territoryId);
        }

        // 씬 변경 저장 알림
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene()
        );

        Debug.Log($"[Phase5Menu] ✅ Build Town 완료: {territoryId}");
    }

    /// <summary>
    /// 모든 Town_* 오브젝트 제거
    /// </summary>
    private static void ClearAllTowns()
    {
        if (!EditorUtility.DisplayDialog(
            "Clear All Towns",
            "모든 영지 건물/병사를 제거하시겠습니까?",
            "확인", "취소"))
        {
            return;
        }

        TownBuilder.ClearAll();
        Debug.Log("[Phase5Menu] 🗑️ All towns cleared.");
    }

    /// <summary>
    /// 선택된 영지의 TownLayoutData ScriptableObject Asset 생성
    /// </summary>
    private static void CreateLayoutAsset(string territoryId)
    {
        var layout = ScriptableObject.CreateInstance<TownLayoutData>();
        layout.territoryId = territoryId;
        layout.displayName = territoryId.Replace("_", " ");
        layout.GenerateDefaultLayout();

        string path = EditorUtility.SaveFilePanelInProject(
            "Save Town Layout Data",
            $"TownLayout_{territoryId}",
            "asset",
            "영지 레이아웃 데이터 저장"
        );

        if (string.IsNullOrEmpty(path))
        {
            Debug.Log("[Phase5Menu] ❌ 저장 취소됨.");
            return;
        }

        AssetDatabase.CreateAsset(layout, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 에디터에서 선택
        Selection.activeObject = layout;

        Debug.Log($"[Phase5Menu] ✅ Layout SO 생성 완료: {path}");
    }

    /// <summary>
    /// 선택된 영지의 레이아웃 정보를 콘솔에 출력
    /// </summary>
    private static void LogLayoutInfo(string territoryId)
    {
        var layout = ScriptableObject.CreateInstance<TownLayoutData>();
        layout.territoryId = territoryId;
        layout.displayName = territoryId.Replace("_", " ");
        layout.GenerateDefaultLayout();

        Debug.Log("===========================================");
        Debug.Log($"📋 Layout Info: {layout.displayName} ({layout.territoryId})");
        Debug.Log($"중심 좌표: {layout.centerPosition}");
        Debug.Log($"입구 좌표: {layout.entrancePosition}");
        Debug.Log($"병사 수: {layout.guardCount}명 (Lv.{layout.guardMinLevel}~{layout.guardMaxLevel})");
        Debug.Log($"건물 수: {layout.buildings.Count}개");
        foreach (var b in layout.buildings)
        {
            Debug.Log($"  [{b.componentType}] {b.label} @ {b.position} (scale: {b.scale})");
        }
        Debug.Log("===========================================");
    }
}