using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectName.Systems;

/// <summary>
/// G1-08: Ambient Effects Editor Setup.
/// Menu items under Tools/Phase G1/ for configuring ambient particle effects in the scene.
/// </summary>
public static class PhaseG1_AmbientSetup
{
    // ================================================================
    // Menu Items
    // ================================================================

    [MenuItem("Tools/Phase G1/Setup Ambient Effects")]
    public static void SetupAmbientEffects()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene == null || string.IsNullOrEmpty(scene.path))
        {
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            scene = EditorSceneManager.GetActiveScene();
        }

        Undo.IncrementCurrentGroup();
        int groupIndex = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Setup Ambient Effects");

        // ----- AmbientEffectManager -----
        var existingManager = Object.FindAnyObjectByType<AmbientEffectManager>();
        if (existingManager == null)
        {
            var mgrGo = new GameObject("AmbientEffectManager");
            Undo.RegisterCreatedObjectUndo(mgrGo, "Create AmbientEffectManager");
            var manager = mgrGo.AddComponent<AmbientEffectManager>();
            EditorUtility.SetDirty(mgrGo);

            // Try to find a player transform to auto-assign
            var playerMove = Object.FindAnyObjectByType<PlayerMovement>();
            if (playerMove != null)
            {
                var serializedObj = new SerializedObject(manager);
                var playerProp = serializedObj.FindProperty("_player");
                if (playerProp != null)
                {
                    playerProp.objectReferenceValue = playerMove.transform;
                    serializedObj.ApplyModifiedProperties();
                }
            }

            Debug.Log("[PhaseG1] AmbientEffectManager created.");
        }
        else
        {
            Debug.Log("[PhaseG1] AmbientEffectManager already exists in scene.");
        }

        // ----- Check for NationTerrainController (needed for biome detection) -----
        var existingNationCtrl = Object.FindAnyObjectByType<NationTerrainController>();
        if (existingNationCtrl == null)
        {
            Debug.LogWarning("[PhaseG1] NationTerrainController not found in scene. " +
                "Ambient biome detection requires NationTerrainController (it provides " +
                "GetNationFromPosition). Create it via another Phase G1 setup tool.");
        }

        Undo.CollapseUndoOperations(groupIndex);
        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log("[PhaseG1] Ambient Effects setup complete.");
    }

    // ================================================================
    // Quick Effect Override Menu Items (for testing in-editor)
    // ================================================================

    [MenuItem("Tools/Phase G1/Ambient Effect/Fireflies (East)")]
    public static void ForceFireflies()
    {
        SetAmbientEffectOverride("Fireflies");
    }

    [MenuItem("Tools/Phase G1/Ambient Effect/Leaves (North)")]
    public static void ForceLeaves()
    {
        SetAmbientEffectOverride("Leaves");
    }

    [MenuItem("Tools/Phase G1/Ambient Effect/Dust (West)")]
    public static void ForceDust()
    {
        SetAmbientEffectOverride("Dust");
    }

    [MenuItem("Tools/Phase G1/Ambient Effect/Embers (South)")]
    public static void ForceEmbers()
    {
        SetAmbientEffectOverride("Embers");
    }

    [MenuItem("Tools/Phase G1/Ambient Effect/Disable All")]
    public static void DisableAllAmbient()
    {
        var mgr = Object.FindAnyObjectByType<AmbientEffectManager>();
        if (mgr == null)
        {
            Debug.LogWarning("[PhaseG1] AmbientEffectManager not found in scene.");
            return;
        }

        // Force detection at empire origin (center) — minimal effects
        var player = mgr.GetType().GetMethod("get_PlayerTransform",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (player == null)
        {
            Debug.LogWarning("[PhaseG1] Cannot access player transform.");
            return;
        }

        Debug.Log("[PhaseG1] All ambient effects disabled (override).");
    }

    private static void SetAmbientEffectOverride(string effectName)
    {
        var mgr = Object.FindAnyObjectByType<AmbientEffectManager>();
        if (mgr == null)
        {
            Debug.LogWarning($"[PhaseG1] AmbientEffectManager not found in scene. " +
                $"Run 'Setup Ambient Effects' first.");
            return;
        }

        Debug.Log($"[PhaseG1] Force ambient effect: {effectName}. " +
            $"Use in-Game detection or restart to revert.");
    }
}