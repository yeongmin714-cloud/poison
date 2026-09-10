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
            StartCoroutine(SeedTestContentWhenReady());   // 2026-09-10: 테스트 아이템 시딩+몬스터(HP바 검증) 지연 구성
            Debug.Log("[InteriorSystemsTest] 구성 완료 — I인벤/E장비·스테이션/P스탯/M지도/X크래프트/K복수");
        }

        // =====================================================================
        // 2026-09-10: UI 전수 테스트 지원 — 아이템 시딩/몬스터/가이드
        // =====================================================================

        /// <summary>테스트 아이템 자동 지급(희귀도 분포+전설 글로우 검증용)+몬스터 1기(HP/레벨 머리표시)+핫바 할당. 플레이어/인벤 준비 대기 후 1회.</summary>
        private System.Collections.IEnumerator SeedTestContentWhenReady()
        {
            // 플레이어 인벤토리 준비 대기 (최대 5초)
            PlayerInventory inv = null;
            for (int i = 0; i < 20; i++)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) { inv = player.GetComponent<PlayerInventory>(); if (inv != null) break; }
                yield return new WaitForSecondsRealtime(0.25f);
            }
            if (inv == null) { Debug.LogWarning("[InteriorSystemsTest] ⚠️ 인벤토리 미발견 — 아이템 시딩 생략"); yield break; }

            // 카테고리 대표 아이템 지급 (약초/고기/재료/무기/방어구 — 전부 기존 정적 데이터 재사용)
            inv.AddItem(PlayerInventory.Herb_Red, 5);
            inv.AddItem(PlayerInventory.Herb_Purple, 3);
            inv.AddItem(PlayerInventory.RabbitMeat, 5);
            inv.AddItem(PlayerInventory.WolfMeat, 2);
            inv.AddItem(PlayerInventory.SwordWood, 1);
            inv.AddItem(PlayerInventory.LeatherArmor, 1);
            inv.AddItem(PlayerInventory.StealthBoots, 1);
            inv.AddItem(PlayerInventory.ClothArmor, 1);
            inv.AddItem(PlayerInventory.SpearWood, 1);
            inv.AddItem(PlayerInventory.BowWood, 1);

            // 희귀도 분포 — 전설 1개(글로우 검증용) + 희귀/영웅 수동 지정 사본
            var legendary = new PlayerInventory.ItemData
            {
                id = "test_sword_legendary", displayName = "★ 전설의 검", description = "AAA 글로우 검증용 전설 무기.",
                category = PlayerInventory.ItemCategory.Weapon, maxStack = 1, maxDurability = 60,
                rarity = ProjectName.Core.ItemRarity.Legendary
            };
            inv.AddItem(legendary, 1);
            var rare = new PlayerInventory.ItemData
            {
                id = "test_herb_rare", displayName = "희귀 치유초", description = "희귀 등급 테스트용 약초.",
                category = PlayerInventory.ItemCategory.Herb, maxStack = 20,
                rarity = ProjectName.Core.ItemRarity.Rare
            };
            inv.AddItem(rare, 4);
            var epic = new PlayerInventory.ItemData
            {
                id = "test_armor_epic", displayName = "영웅의 사슬갑옷", description = "영웅 등급 테스트용 방어구.",
                category = PlayerInventory.ItemCategory.Armor, maxStack = 1, maxDurability = 60,
                rarity = ProjectName.Core.ItemRarity.Epic
            };
            inv.AddItem(epic, 1);

            // 핫바 1~3슬롯 자동 할당 (우클릭장착/드래그 대체 수동 검증용)
            HotbarUI.AssignItem(0, PlayerInventory.SwordWood.id, PlayerInventory.SwordWood.displayName);
            HotbarUI.AssignItem(1, PlayerInventory.Herb_Red.id, PlayerInventory.Herb_Red.displayName);
            HotbarUI.AssignItem(2, PlayerInventory.BoarMeat.id, PlayerInventory.BoarMeat.displayName);
            Debug.Log("[InteriorSystemsTest] ✅ 테스트 아이템 10종 시딩 + 핫바 1~3 할당 완료 (전설 글로우: ★ 전설의 검)");

            // 몬스터 1기 — 머리 위 HP/레벨 표시(MonsterHeadUI) 검증용
            SpawnTestMonster();
        }

        private void SpawnTestMonster()
        {
            var monster = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            monster.name = "Monster_slime_test";
            monster.tag = "Monster";
            monster.transform.position = new Vector3(0f, 0.8f, 6f);
            monster.transform.localScale = Vector3.one * 1.2f;
            var mr = monster.GetComponent<Renderer>();
            if (mr != null) mr.sharedMaterial.color = new Color(0.45f, 0.85f, 0.35f, 1f);

            var ai = monster.GetComponent<AnimalAI>();
            if (ai == null) ai = monster.AddComponent<AnimalAI>();
            ai.SetMonsterId("slime");

            if (monster.GetComponent<Collider>() == null) { var col = monster.AddComponent<BoxCollider>(); col.size = Vector3.one; }
            if (monster.GetComponent<Rigidbody>() == null) { var rb = monster.AddComponent<Rigidbody>(); rb.useGravity = true; rb.mass = 1f; }
            if (monster.GetComponent<HitReaction>() == null) monster.AddComponent<HitReaction>();

            var head = MonsterHeadUI.AttachTo(monster);
            if (head != null) head.Setup(ai);
            Debug.Log("[InteriorSystemsTest] ✅ 테스트 몬스터 생성(slime) — 머리 위 이름/Lv/HP바 표시, 좌클릭 타격 시 실시간 감소");
        }

        /// <summary>UI 테스트 가이드 — 전체 창 열기 키 안내 OnGUI(하단 좌측).</summary>
        private void OnGUI()
        {
            float w = 560f, h = 30f;
            var r = new Rect(8f, Screen.height - h - 8f, w, h);
            GUI.Box(r, " UI 테스트 — I 인벤 · E 장비/스테이션 · P 스탯 · M 지맵 · X 크래프트 · K 복수 · 슬라임 타격→HP바");
        }

        /// <summary>UIManager + 각 창 인스턴스 생성 (스테이션들이 OpenWindow(Type)으로 탐색)</summary>
        private void SetupUIManager()
        {
            if (Game.UI.Core.UIManager.Instance != null) return;
            var go = new GameObject("UIManager");
            var uim = go.AddComponent<Game.UI.Core.UIManager>();

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
