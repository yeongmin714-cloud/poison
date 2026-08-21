using UnityEditor;
using UnityEngine;
using Unity.InferenceEngine;
using ProjectName.Systems.Animation.Neural;
using ProjectName.Systems;
using ProjectName.Systems.Animation.Procedural.Bones;

/// <summary>
/// Auto-setup NeuralAnimationController with ONNX models for testing.
/// Run via: Tools > Neural > Auto Setup Player with Neural Animation
/// </summary>
public static class NeuralAnimationAutoSetup
{
    private const string PlayerTag = "Player";

    [MenuItem("Tools/Neural/Auto Setup Player with Neural Animation")]
    public static void AutoSetupPlayer()
    {
        // Find or create Player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            player = new GameObject("Player");
            player.tag = "Player";
        }

        Undo.RecordObject(player, "Setup Neural Animation");

        // Required components
        EnsureComponent<Rigidbody>(player);
        EnsureComponent<CharacterController>(player);
        EnsureComponent<Animator>(player);
        EnsureComponent<UnityEngine.InputSystem.PlayerInput>(player);
        EnsureComponent<ProceduralBoneMap>(player);

        // Neural Animation Controller
        var neuralAnim = EnsureComponent<NeuralAnimationController>(player);

        // Assign ONNX models via SerializedObject
        AssignONNXModels(neuralAnim);

        // Velocity Provider
        var playerMovement = EnsureComponent<PlayerMovement>(player);
        neuralAnim.SetVelocityProvider(playerMovement);

        // Hybrid Animation Controller (optional but recommended)
        var hybridAnim = EnsureComponent<HybridAnimationController>(player);
        hybridAnim.SetVelocityProvider(playerMovement);

        // Configure PlayerMovement
        ConfigurePlayerMovement(playerMovement);

        // Progressive Rollout Manager registration
        var rolloutManager = ProgressiveRolloutManager.Instance;
        if (rolloutManager != null)
        {
            rolloutManager.ConfigureHybridController(hybridAnim);
        }

        EditorUtility.SetDirty(player);
        AssetDatabase.SaveAssets();

        Debug.Log("[NeuralAnimationAutoSetup] ✅ Player setup complete with Neural Animation!");
    }

    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
        var comp = go.GetComponent<T>();
        if (comp == null)
        {
            comp = Undo.AddComponent<T>(go);
            Debug.Log($"[AutoSetup] Added {typeof(T).Name}");
        }
        return comp;
    }

    private static void AssignONNXModels(NeuralAnimationController neuralAnim)
    {
        var serialized = new SerializedObject(neuralAnim);
        
        var models = new (string fieldName, string assetName)[]
        {
            ("_locomotionPolicy", "locomotion_biped_base_fp32.onnx"),
            ("_combatPolicy", "combat_biped_base_fp32.onnx"),
            ("_reactPolicy", "react_biped_base_fp32.onnx"),
            ("_interactPolicy", "interact_biped_base_fp32.onnx"),
            ("_flyPolicy", "fly_biped_base_fp32.onnx"),
            ("_swimPolicy", "swim_biped_base_fp32.onnx"),
        };

        foreach (var (fieldName, assetName) in models)
        {
            var property = serialized.FindProperty(fieldName);
            if (property != null)
            {
                var assetPath = $"Assets/Resources/NeuralModels/{assetName}";
                var modelAsset = AssetDatabase.LoadAssetAtPath<ModelAsset>(assetPath);
                if (modelAsset != null)
                {
                    property.objectReferenceValue = modelAsset;
                    Debug.Log($"[AutoSetup] Assigned {assetName}");
                }
                else
                {
                    Debug.LogWarning($"[AutoSetup] ONNX not found: {assetPath}");
                }
            }
            else
            {
                Debug.LogWarning($"[AutoSetup] Field not found: {fieldName}");
            }
        }

        serialized.ApplyModifiedProperties();
    }

    private static void ConfigurePlayerMovement(PlayerMovement pm)
    {
        var pmType = typeof(PlayerMovement);
        pmType.GetField("_walkSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(pm, 5f);
        pmType.GetField("_runSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(pm, 10f);
        pmType.GetField("_jumpHeight", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(pm, 2f);
    }
}