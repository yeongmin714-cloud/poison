using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Systems;
using ProjectName.UI;

/// <summary>
/// Phase 2: 🏠 튜토리얼 — 추방지 허름한 집 셋업
/// 
/// 실행: Tools > Phase 2 - Setup Tutorial Scene
/// 또는 배치모드 자동 실행
/// 
/// 생성할 것들:
/// 1. Tutorial Ground (100x100, 초원)
/// 2. Hut (외부) — Placeholder_Hut  
/// 3. Hut Interior Trigger 영역
/// 4. CraftingTable (집 안) — Placeholder_CraftingTable
/// 5. Herbs 5종 x 3개씩 지형 배치
/// 6. Rabbit x2, Boar x1, Wolf (후반 지역)
/// 7. Lord NPC — Placeholder_Lord
/// 8. E 키 안내 UI 프리팹
/// 9. PlayerInventory 싱글톤
/// </summary>
public static class Phase2_Setup
{
    [MenuItem("Tools/Phase 2 - Setup Tutorial Scene")]
    public static void SetupTutorialScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        scene.name = "TutorialScene";

        // ===== 1. 지형 (Ground) 100x100 =====
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground_Tutorial";
        ground.transform.localScale = new Vector3(10, 1, 10); // 100x100
        ground.transform.position = Vector3.zero;

        var groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        groundMat.color = new Color(0.35f, 0.65f, 0.25f);
        groundMat.name = "Ground_Tutorial_Grass";
        ground.GetComponent<MeshRenderer>().material = groundMat;

        // ===== 2. 환경 — 나무/돌/풀 (방향감각 + 분위기) =====
        var treeMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        treeMat.color = new Color(0.25f, 0.5f, 0.15f);

        var rockMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        rockMat.color = new Color(0.4f, 0.35f, 0.3f);

        System.Random rng = new System.Random(42); // fixed seed for consistent placement
        for (int i = 0; i < 20; i++)
        {
            float angle = (float)(rng.NextDouble() * Mathf.PI * 2);
            float radius = 10f + (float)(rng.NextDouble() * 30f);
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;

            // 나무 12개, 돌 8개
            bool isTree = (i % 5) < 3;
            if (isTree)
            {
                var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                trunk.name = $"Tree_{i}";
                trunk.transform.position = new Vector3(x, 0.5f, z);
                trunk.transform.localScale = new Vector3(0.3f, 1f, 0.3f);
                trunk.GetComponent<MeshRenderer>().material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.4f, 0.25f, 0.1f) };

                var leaves = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                leaves.name = $"Leaves_{i}";
                leaves.transform.position = new Vector3(x, 2f, z);
                leaves.transform.localScale = new Vector3(0.8f, 0.6f, 0.8f);
                leaves.GetComponent<MeshRenderer>().material = treeMat;
            }
            else
            {
                var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                rock.name = $"Rock_{i}";
                rock.transform.position = new Vector3(x, 0.3f, z);
                rock.transform.localScale = new Vector3((float)(0.3f + rng.NextDouble() * 0.5f), 0.3f, (float)(0.3f + rng.NextDouble() * 0.5f));
                rock.GetComponent<MeshRenderer>().material = rockMat;
            }
        }

        // ===== 3. 허름한 집 (Hut) — Placeholder =====
        var hut = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hut.name = "Placeholder_Hut";
        hut.transform.position = new Vector3(0, 0, 5);
        hut.transform.localScale = new Vector3(6, 3, 4);
        var hutMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        hutMat.color = new Color(0.55f, 0.35f, 0.15f);
        hut.GetComponent<MeshRenderer>().material = hutMat;

        // 지붕 (삼각 프리즘 — Cube로 대체)
        var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roof.name = "Placeholder_Hut_Roof";
        roof.transform.position = new Vector3(0, 2.8f, 5);
        roof.transform.localScale = new Vector3(6.5f, 0.4f, 4.5f);
        var roofMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        roofMat.color = new Color(0.6f, 0.3f, 0.05f);
        roof.GetComponent<MeshRenderer>().material = roofMat;
        roof.transform.SetParent(hut.transform);

        // 굴뚝
        var chimney = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        chimney.name = "Placeholder_Hut_Chimney";
        chimney.transform.position = new Vector3(2f, 2.5f, 6f);
        chimney.transform.localScale = new Vector3(0.3f, 1f, 0.3f);
        var chimneyMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        chimneyMat.color = new Color(0.45f, 0.3f, 0.2f);
        chimney.GetComponent<MeshRenderer>().material = chimneyMat;
        chimney.transform.SetParent(hut.transform);

        // 집 앞 마당 (작은 평면)
        var yard = GameObject.CreatePrimitive(PrimitiveType.Plane);
        yard.name = "Placeholder_Yard";
        yard.transform.position = new Vector3(-3f, 0.01f, 5f);
        yard.transform.localScale = new Vector3(1f, 1, 1.5f);
        var yardMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        yardMat.color = new Color(0.6f, 0.5f, 0.3f);
        yard.GetComponent<MeshRenderer>().material = yardMat;

        // ===== 4. 집 내부 공간 =====
        var hutInterior = new GameObject("Placeholder_Hut_Interior");
        hutInterior.transform.position = new Vector3(0, 0.5f, 5);

        // 내부 바닥 (Hut 안쪽)
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Interior_Floor";
        floor.transform.position = new Vector3(0, 0.01f, 5);
        floor.transform.localScale = new Vector3(0.55f, 1, 0.45f);
        var floorMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        floorMat.color = new Color(0.5f, 0.35f, 0.2f);
        floor.GetComponent<MeshRenderer>().material = floorMat;
        floor.transform.SetParent(hutInterior.transform);

        // ===== 5. 크래프트 테이블 (집 안) =====
        var ct = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ct.name = "Placeholder_CraftingTable";
        ct.transform.position = new Vector3(-0.8f, 0.3f, 5.8f);
        ct.transform.localScale = new Vector3(1.2f, 0.6f, 0.8f);
        var ctMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        ctMat.color = new Color(0.5f, 0.3f, 0.15f);
        ct.GetComponent<MeshRenderer>().material = ctMat;

        // CraftingStation 스크립트 추가
        var craftingComp = ct.AddComponent<CraftingStation>();
        ct.transform.SetParent(hutInterior.transform);

        // ===== 6. 약초 5종 × 3개씩 =====
        HerbType[] herbTypes = new[] { HerbType.Red, HerbType.Purple, HerbType.Yellow, HerbType.Silver, HerbType.Green };
        string[] herbNames = new[] { "Placeholder_Herb_Red", "Placeholder_Herb_Purple", 
                                     "Placeholder_Herb_Yellow", "Placeholder_Herb_Silver", "Placeholder_Herb_Green" };
        Color[] herbColors = new[] { Color.red, new Color(0.7f, 0.1f, 0.7f), Color.yellow, 
                                     new Color(0.75f, 0.75f, 0.85f), Color.green };

        for (int h = 0; h < herbTypes.Length; h++)
        {
            string herbName = herbNames[h];
            Color herbColor = herbColors[h];

            for (int copy = 0; copy < 3; copy++)
            {
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2);
                float radius = 3f + (float)(rng.NextDouble() * 8f);
                float hx = Mathf.Cos(angle + h * 1.2f) * radius + (copy - 1) * 0.5f;
                float hz = Mathf.Sin(angle + h * 1.2f) * radius + 3f;

                var herb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                herb.name = $"{herbName}_{copy}";
                herb.transform.position = new Vector3(hx, 0.05f, hz);
                herb.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);
                var herbMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                herbMat.color = herbColor;
                herb.GetComponent<MeshRenderer>().material = herbMat;
                herb.tag = "Interactable";

                // HerbPickup 컴포넌트
                var pickup = herb.AddComponent<HerbPickup>();
                // 리플렉션으로 HerbType 설정 불가 → label로 설정
                // 대신 FieldInfo로 설정

                // Sphere Collider (기본 있음)
                var col = herb.GetComponent<Collider>();
                if (col != null) col.isTrigger = false;
            }
        }

        // ===== 7. 토끼 × 2 =====
        for (int r = 0; r < 2; r++)
        {
            float rx = 5f + r * 4f;
            float rz = -3f;

            var rabbit = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rabbit.name = $"Placeholder_Rabbit_{r}";
            rabbit.transform.position = new Vector3(rx, 0.2f, rz);
            rabbit.transform.localScale = new Vector3(0.2f, 0.15f, 0.25f);
            var rabbitMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            rabbitMat.color = new Color(0.7f, 0.7f, 0.75f);
            rabbit.GetComponent<MeshRenderer>().material = rabbitMat;

            var ai = rabbit.AddComponent<AnimalAI>();
            // AnimalAI의 _type 필드는 SerializeField → reflection or default
            // 기본값 Rabbit이므로 그대로
            rabbit.AddComponent<SphereCollider>().radius = 0.15f;
        }

        // ===== 8. 멧돼지 × 1 =====
        var boar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        boar.name = "Placeholder_Boar";
        boar.transform.position = new Vector3(-6f, 0.3f, 10f);
        boar.transform.localScale = new Vector3(0.5f, 0.4f, 0.8f);
        var boarMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        boarMat.color = new Color(0.45f, 0.3f, 0.15f);
        boar.GetComponent<MeshRenderer>().material = boarMat;

        var boarAI = boar.AddComponent<AnimalAI>();
        // Boar type 설정 불가 → 직접 수정 필요 (Editor에서)
        // 여기서는 UnityEditor.SerializedObject로 설정
        var so = new SerializedObject(boarAI);
        var typeProp = so.FindProperty("_type");
        if (typeProp != null)
        {
            typeProp.enumValueIndex = 1; // 1 = Boar
            so.ApplyModifiedProperties();
        }
        boar.AddComponent<BoxCollider>().size = new Vector3(0.5f, 0.4f, 0.8f);

        // ===== 9. 늑대 × 2 (후반 지역) =====
        for (int w = 0; w < 2; w++)
        {
            float wx = -8f + w * 3f;
            float wz = 18f;

            var wolf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wolf.name = $"Placeholder_Wolf_{w}";
            wolf.transform.position = new Vector3(wx, 0.3f, wz);
            wolf.transform.localScale = new Vector3(0.4f, 0.35f, 0.7f);
            var wolfMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            wolfMat.color = new Color(0.3f, 0.3f, 0.35f);
            wolf.GetComponent<MeshRenderer>().material = wolfMat;

            var wolfAI = wolf.AddComponent<AnimalAI>();
            var wolfSO = new SerializedObject(wolfAI);
            var wolfTypeProp = wolfSO.FindProperty("_type");
            if (wolfTypeProp != null)
            {
                wolfTypeProp.enumValueIndex = 2; // 2 = Wolf
                wolfSO.ApplyModifiedProperties();
            }
            wolf.AddComponent<BoxCollider>().size = new Vector3(0.4f, 0.35f, 0.7f);
        }

        // ===== 10. 영주 NPC =====
        var lord = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        lord.name = "Placeholder_Lord";
        lord.transform.position = new Vector3(3.5f, 0.5f, 7f);
        lord.transform.localScale = new Vector3(0.4f, 0.8f, 0.4f);
        var lordMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        lordMat.color = new Color(0.7f, 0.15f, 0.15f);
        lord.GetComponent<MeshRenderer>().material = lordMat;
        lord.tag = "Interactable";

        // 머리
        var lordHead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        lordHead.name = "Placeholder_Lord_Head";
        lordHead.transform.position = new Vector3(3.5f, 1.1f, 7f);
        lordHead.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
        var headMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        headMat.color = new Color(0.95f, 0.8f, 0.7f);
        lordHead.GetComponent<MeshRenderer>().material = headMat;
        lordHead.transform.SetParent(lord.transform);

        // TutorialQuestNPC 스크립트
        lord.AddComponent<TutorialQuestNPC>();
        lord.AddComponent<CapsuleCollider>().height = 1.2f;

        // ===== 11. Player 스폰 위치 =====
        var playerSpawn = new GameObject("Player_Spawn");
        playerSpawn.transform.position = new Vector3(0, 0, -3);

        // ===== 12. PlayerInventory 싱글톤 =====
        var invGO = new GameObject("PlayerInventory");
        invGO.AddComponent<PlayerInventory>();

        // ===== 13. Directional Light =====
        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.shadowStrength = 0.8f;
        lightGO.transform.rotation = Quaternion.Euler(50, -30, 0);

        // ===== 씬 저장 =====
        string path = "Assets/Scenes/TutorialScene.unity";
        EditorSceneManager.SaveScene(scene, path);
        Debug.Log($"[Phase2] 튜토리얼 씬 생성 완료 → {path}");
    }

    [MenuItem("Tools/Phase 2 - Setup Tutorial Scene", true)]
    private static bool Validate()
    {
        return true;
    }

    // Helper enum for herb types in Setup
    private enum HerbType { Red, Purple, Yellow, Silver, Green }
}