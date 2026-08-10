using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class QuickFix
{
    public static void Apply()
    {
        Debug.Log("[QuickFix] 시작");
        
        // Load MainScene
        var guids = AssetDatabase.FindAssets("MainScene t:Scene");
        string path = null;
        foreach (var g in guids)
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (p.Contains("MainScene") && !p.Contains("Backup")) { path = p; break; }
        }
        if (path == null) { Debug.LogError("씬 없음"); return; }
        
        EditorSceneManager.OpenScene(path);
        Debug.Log("씬 로드됨: " + path);
        
        // 1. 폴리해븐 제거
        int removed = 0;
        var all = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var go in all)
        {
            if (!go.scene.IsValid() || go.scene.name == "DontDestroyOnLoad") continue;
            if ((go.hideFlags & HideFlags.NotEditable) != 0) continue;
            
            var n = go.name.ToLower();
            if (n.Contains("fir_tree") || n.Contains("jacaranda") || n.Contains("tree_small") ||
                n.Contains("boulder_01") || n.Contains("namaqualand") || n.Contains("cliff") ||
                n.Contains("periwinkle") || n.Contains("searsia") || n.Contains("polyheven"))
            {
                Object.DestroyImmediate(go);
                removed++;
            }
        }
        Debug.Log("폴리해븐 제거: " + removed + "개");
        
        // 2. Player 확인/수정
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) player = GameObject.Find("Player");
        
        if (player != null)
        {
            player.transform.localScale = Vector3.one * 2;
            
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) { cc.height = 4; cc.radius = 0.8f; }
            
            // PlayerPlaceholder가 없으면 추가
            var phType = System.Type.GetType("ProjectName.Systems.PlayerPlaceholder");
            if (phType == null)
            {
                foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    phType = a.GetType("ProjectName.Systems.PlayerPlaceholder");
                    if (phType != null) break;
                }
            }
            if (phType != null && player.GetComponent(phType) == null)
                player.AddComponent(phType);
            
            // PlayerMovement 확인
            var pm = player.GetComponent<ProjectName.Systems.PlayerMovement>();
            if (pm != null) pm.enabled = true;
        }
        
        // 3. Scene 바닥 확인 - Ground가 없으면 추가
        var ground = GameObject.Find("Ground");
        if (ground == null)
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = Vector3.one * 100;
            ground.transform.position = new Vector3(0, -0.5f, 0);
            Debug.Log("Ground Plane 생성됨");
        }
        
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[QuickFix] 완료!");
    }
}