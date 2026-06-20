using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class AlchemyTestSceneSetup
{
    [MenuItem("Tools/Create Alchemy Test Scene")]
    public static void CreateAlchemyTestScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // Rename and set up Main Camera
        var camera = GameObject.Find("Main Camera");
        if (camera != null)
        {
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 2, -5);
            camera.transform.rotation = Quaternion.Euler(15, 0, 0);
            var camComp = camera.GetComponent<Camera>();
            camComp.backgroundColor = new Color(0.1f, 0.1f, 0.15f);
            camComp.clearFlags = CameraClearFlags.SolidColor;
        }

        // Set up Directional Light
        var light = GameObject.Find("Directional Light");
        if (light != null)
        {
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            var lightComp = light.GetComponent<Light>();
            lightComp.shadowStrength = 0.8f;
        }

        // Create a simple ground plane for reference
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(10, 1, 10);
        
        // Add a simple UI instruction panel
        CreateInstructionPanel();

        // Add GameManager (to initialize core systems)
        var gmGO = new GameObject("GameManager");
        gmGO.AddComponent<ProjectName.Core.GameManager>();
        
        // Add AlchemyUI for testing
        var alchemyGO = new GameObject("AlchemyUITest");
        alchemyGO.AddComponent<ProjectName.Core.UI.AlchemyUI>();

        // Add a simple test script to give player some starting herbs
        var testGO = new GameObject("AlchemyTestInitializer");
        testGO.AddComponent<AlchemyTestInitializer>();

        // Save the scene
        var path = "Assets/Scenes/AlchemyTestScene.unity";
        EditorSceneManager.SaveScene(scene, path);
        EditorSceneManager.OpenScene(path);

        Debug.Log($"[AlchemyTestSceneSetup] Alchemy test scene created at {path}");
    }

    [MenuItem("Tools/Create Alchemy Test Scene", true)]
    private static bool ValidateCreateAlchemyTestScene()
    {
        return !AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/AlchemyTestScene.unity");
    }

    private static void CreateInstructionPanel()
    {
        // Create a canvas for instructions if not already present
        var canvasObj = GameObject.Find("InstructionCanvas");
        if (canvasObj == null)
        {
            canvasObj = new GameObject("InstructionCanvas");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // Create background panel
        var panelGO = new GameObject("InstructionPanel");
        panelGO.transform.SetParent(canvasObj.transform, false);
        var panelRect = panelGO.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(400, 200);
        panelRect.anchoredPosition = new Vector2(0, 150);

        var panelImg = panelGO.AddComponent<UnityEngine.UI.Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.7f);

        // Create instruction text
        var textGO = new GameObject("InstructionText");
        textGO.transform.SetParent(panelGO.transform, false);
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(20, 20);
        textRect.offsetMax = new Vector2(-20, -20);

        var textComp = textGO.AddComponent<UnityEngine.UI.Text>();
        textComp.text = "<b>Alchemy Test Scene</b>\n\n" +
                       "1. Use the dropdowns to select two herbs\n" +
                       "2. Click '제조하기' (Craft) to attempt combination\n" +
                       "3. Check the result text for success/failure\n" +
                       "4. View debug console for detailed logs\n" +
                       "5. Your inventory will update with successful crafts\n\n" +
                       "Tip: Start with common herbs like 쓴풀 (A1) + 가시덤불 (A2)";
        textComp.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        textComp.fontSize = 14;
        textComp.color = Color.white;
        textComp.alignment = TextAnchor.UpperLeft;
        textComp.supportRichText = true;
    }
}

// Simple test initializer to give player some starting herbs for testing
public class AlchemyTestInitializer : MonoBehaviour
{
    private void Awake()
    {
        // Give player some basic herbs for testing
        var inventory = ProjectName.Core.PlayerInventory.Instance;
        if (inventory != null)
        {
            // Add some test herbs
            inventory.AddItem(ProjectName.Core.PlayerInventory.Herb_Red, 5);    // 치유초
            inventory.AddItem(ProjectName.Core.PlayerInventory.Herb_Yellow, 5); // 황혼초
            inventory.AddItem(ProjectName.Core.PlayerInventory.Herb_Purple, 5); // 독나물
            inventory.AddItem(ProjectName.Core.PlayerInventory.Herb_Green, 5);  // 피어리
            inventory.AddItem(ProjectName.Core.PlayerInventory.Herb_Silver, 5); // 은빛 이끼
            
            Debug.Log("[AlchemyTestInitializer] Added test herbs to inventory");
        }
        
        // Remove this component after initialization
        Destroy(this);
    }
}