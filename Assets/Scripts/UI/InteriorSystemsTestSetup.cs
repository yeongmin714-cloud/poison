using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectName.Core;
using ProjectName.Systems;

namespace ProjectName.UI
{
    /// <summary>
    /// 2026-09-09: 실내 시스템 전용 테스트 씬 부트스트랩 (저사양 검증용).
    /// 빈 씬 이름을 "InteriorSystemsTest"로 만들고 Play하면 자동 구성:
    /// 바닥 + 크래프트 작업대/요리/연금 + 창고 + 상점 NPC + 플레이어 + 카메라.
    /// Test_10(TestTerritoryCombatSetup) 선례와 동일 패턴.
    /// </summary>
    public static class InteriorSystemsTestSetup
    {
        private const string TestSceneName = "InteriorSystemsTest";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (SceneManager.GetActiveScene().name != TestSceneName) return;
            if (Object.FindAnyObjectByType<InteriorSystemsTestRunner>() != null) return;

            var go = new GameObject("InteriorSystemsTestRunner");
            go.AddComponent<InteriorSystemsTestRunner>();
        }
    }

    public class InteriorSystemsTestRunner : MonoBehaviour
    {
        private void Awake()
        {
            SetupGround();
            SetupUIManager();   // 2026-09-09: 스테이션/창고/상점의 선행 요건
            SetupCamera();      // PlayerMovement.Awake 전에 카메라 확보
            SetupPlayer();
            SetupStations();
            Debug.Log("[InteriorSystemsTest] 구성 완료 — E키로 각 스테이션/창고/상점 상호작용");
        }

        /// <summary>UIManager + 각 창 인스턴스 생성 (스테이션들이 OpenWindow(Type)으로 탐색)</summary>
        private void SetupUIManager()
        {
            if (UIManager.Instance != null) return;
            var go = new GameObject("UIManager");
            var uim = go.AddComponent<UIManager>();

            uim.warehouseWindow = NewWindow<WarehouseUI>("WarehouseUI");
            uim.craftingWindow = NewWindow<CraftingUI>("CraftingUI");
            NewWindow<CookingUI>("CookingUI");
            NewWindow<AlchemyUI>("AlchemyUI");
            NewWindow<InventoryWindow>("InventoryUI");
        }

        private static T NewWindow<T>(string name) where T : UIWindow
        {
            var go = new GameObject(name);
            return go.AddComponent<T>();
        }

        private void SetupGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(6f, 1f, 6f); // 60x60m
            ground.GetComponent<Renderer>().sharedMaterial.color = new Color(0.35f, 0.32f, 0.28f);
        }

        private void SetupPlayer()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                player = new GameObject("Player");
                player.tag = "Player";
            }
            if (player.GetComponent<PlayerMovement>() == null) player.AddComponent<PlayerMovement>();
            if (player.GetComponent<UnityEngine.InputSystem.PlayerInput>() == null)
            {
                var pi = player.AddComponent<UnityEngine.InputSystem.PlayerInput>();
                pi.defaultActionMap = "Player";
                pi.notificationBehavior = UnityEngine.InputSystem.PlayerNotifications.InvokeUnityEvents;
            }
            if (player.GetComponent<PlayerCombat>() == null) player.AddComponent<PlayerCombat>();
            if (player.GetComponent<PlayerHealth>() == null) player.AddComponent<PlayerHealth>();
            if (player.GetComponent<PlayerInventory>() == null) player.AddComponent<PlayerInventory>();
            player.transform.position = new Vector3(0f, 1.5f, -6f);
        }

        private void SetupCamera()
        {
            var camGO = GameObject.FindGameObjectWithTag("MainCamera");
            if (camGO == null)
            {
                camGO = new GameObject("Main Camera");
                camGO.tag = "MainCamera";
                camGO.AddComponent<Camera>();
                camGO.AddComponent<AudioListener>();
            }
            camGO.transform.position = new Vector3(0f, 9f, -8f);
            camGO.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
        }

        private void Label(Vector3 pos, string text)
        {
            var go = new GameObject("Label_" + text);
            go.transform.position = pos;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 48;
            tm.characterSize = 0.25f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = Color.yellow;
        }

        private GameObject MakeBox(Vector3 pos, Color c, string name)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = pos;
            box.transform.localScale = new Vector3(1.4f, 1.1f, 1.4f);
            box.GetComponent<Renderer>().sharedMaterial.color = c;
            return box;
        }

        private void SetupStations()
        {
            // ① 장비 크래프트 작업대
            var craft = MakeBox(new Vector3(3f, 0.55f, 0f), new Color(0.55f, 0.4f, 0.2f), "CraftingStation_Box");
            if (craft.GetComponent<CraftingStation>() == null) craft.AddComponent<CraftingStation>();
            Label(new Vector3(3f, 1.6f, 0f), "⚒️ 크래프트(E)");

            // ② 요리 스테이션
            var cook = MakeBox(new Vector3(-3f, 0.55f, 0f), new Color(0.4f, 0.55f, 0.3f), "CookingStation_Box");
            if (cook.GetComponent<CookingStation>() == null) cook.AddComponent<CookingStation>();
            Label(new Vector3(-3f, 1.6f, 0f), "🍳 요리(E)");

            // ③ 연금 스테이션
            var alc = MakeBox(new Vector3(3f, 0.55f, 3f), new Color(0.35f, 0.4f, 0.6f), "AlchemyStation_Box");
            if (alc.GetComponent<AlchemyStation>() == null) alc.AddComponent<AlchemyStation>();
            Label(new Vector3(3f, 1.6f, 3f), "🧪 연금(E)");

            // ④ 창고
            var wh = MakeBox(new Vector3(-3f, 0.55f, 3f), new Color(0.5f, 0.45f, 0.35f), "Warehouse_Box");
            var warehouse = wh.GetComponent<TerritoryWarehouse>();
            if (warehouse == null) warehouse = wh.AddComponent<TerritoryWarehouse>();
            Label(new Vector3(-3f, 1.6f, 3f), "📦 창고(E)");

            // ⑤ 상점 NPC
            var shopNpc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            shopNpc.name = "ShopNPC";
            shopNpc.transform.position = new Vector3(0f, 1f, 5f);
            shopNpc.GetComponent<Renderer>().sharedMaterial.color = new Color(0.8f, 0.7f, 0.3f);
            var shop = shopNpc.AddComponent<ShopPlaceholder>();
            Label(new Vector3(0f, 2.4f, 5f), "🏪 상점NPC(E)");
        }
    }
}
