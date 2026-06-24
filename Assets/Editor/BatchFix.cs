using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Batchmode-safe Poly Haven 제거 + Player 수정.
/// Usage: Unity -batchmode -quit -projectPath ... -executeMethod BatchFix.RunAll
/// </summary>
public static class BatchFix
{
    public static void RunAll()
    {
        Debug.Log("[BatchFix] Starting...");

        // Load MainScene
        string[] guids = AssetDatabase.FindAssets("MainScene t:Scene");
        string scenePath = null;
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("MainScene") && !path.Contains("Backup"))
            {
                scenePath = path;
                break;
            }
        }

        if (string.IsNullOrEmpty(scenePath))
        {
            Debug.LogError("[BatchFix] MainScene not found!");
            return;
        }

        EditorSceneManager.OpenScene(scenePath);
        Debug.Log($"[BatchFix] Opened scene: {scenePath}");

        // Step 1: Remove Poly Haven objects
        RemovePolyHavenObjects();

        // Step 2: Fix Player
        FixPlayer();

        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[BatchFix] Complete!");
    }

    private static void RemovePolyHavenObjects()
    {
        string[] keywords = new string[] {
            "fir_tree", "jacaranda", "tree_small",
            "boulder", "namaqualand", "cliff",
            "periwinkle", "searsia",
            "poly", "heven", "terrainring"
        };

        int count = 0;
        GameObject[] roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var root in roots)
        {
            bool matched = false;
            string nameLower = root.name.ToLower();
            foreach (var kw in keywords)
            {
                if (nameLower.Contains(kw)) { matched = true; break; }
            }
            if (matched)
            {
                GameObject.DestroyImmediate(root);
                count++;
                continue;
            }

            // Check children
            var children = root.GetComponentsInChildren<Transform>(true);
            foreach (var child in children)
            {
                if (child == null || child == root.transform) continue;
                string cn = child.name.ToLower();
                foreach (var kw in keywords)
                {
                    if (cn.Contains(kw))
                    {
                        GameObject.DestroyImmediate(child.gameObject);
                        count++;
                        break;
                    }
                }
            }
        }

        Debug.Log($"[BatchFix] Removed {count} Poly Haven objects");
    }

    private static void FixPlayer()
    {
        var player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            Debug.LogError("[BatchFix] Player (tag) not found!");
            // Try finding by name
            player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("[BatchFix] Player GameObject not found at all!");
                return;
            }
            Debug.Log("[BatchFix] Found Player by name instead of tag");
        }

        var t = player.transform;

        // Fix position
        if (t.position.z < -900f || t.position.z > 900f)
        {
            t.position = Vector3.zero;
            Debug.Log("[BatchFix] Player position → (0,0,0)");
        }

        // Fix scale
        t.localScale = new Vector3(2, 2, 2);
        Debug.Log("[BatchFix] Player scale → 2");

        // Fix CharacterController
        var cc = player.GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.height = 4f;
            cc.radius = 0.8f;
            cc.center = Vector3.zero;
            Debug.Log("[BatchFix] CharacterController fixed (h=4, r=0.8)");
        }
        else
        {
            player.AddComponent<CharacterController>();
            Debug.Log("[BatchFix] CharacterController added");
        }

        // Add Animator if missing
        if (player.GetComponent<Animator>() == null)
            player.AddComponent<Animator>();

        // Add RigAnimationController via reflection
        var rigType = System.Type.GetType("ProjectName.Systems.RigAnimationController, ProjectName.Systems, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
        if (rigType == null)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                rigType = asm.GetType("ProjectName.Systems.RigAnimationController");
                if (rigType != null) break;
            }
        }
        if (rigType != null && player.GetComponent(rigType) == null)
            player.AddComponent(rigType);

        // Add PlayerPlaceholder
        var phType = System.Type.GetType("ProjectName.Systems.PlayerPlaceholder, ProjectName.Systems");
        if (phType == null)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                phType = asm.GetType("ProjectName.Systems.PlayerPlaceholder");
                if (phType != null) break;
            }
        }
        if (phType != null && player.GetComponent(phType) == null)
            player.AddComponent(phType);

        Debug.Log("[BatchFix] Player fixed successfully");
    }
}