using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// Test_10_TerritoryCombat 씬 전용: 저사양 공격 시스템 집중 점검.
    /// 최소 구성 — Player(AttackSystem) + 타영주 영지(DraculaLord) 1 + 병사(GuardPlaceholder) 1 + 몬스터 1.
    /// 좌클릭 → AttackSystem(Raycast) → IDamageable 데미지/사망 드랍을 단독 검증한다.
    /// 메인씬 확산 전 공격 시스템 통과 여부를 빠르게 판정하는 목적.
    /// </summary>
    public class TestTerritoryCombatSetup : MonoBehaviour
    {
        [Header("배치 좌표")]
        [SerializeField] private Vector3 _lordPos = new Vector3(0, 0, 15);
        [SerializeField] private Vector3 _guardPos = new Vector3(3, 0, 12);
        [SerializeField] private Vector3 _monsterPos = new Vector3(-5, 0, 8);

        private void Awake()
        {
            // Test_10_TerritoryCombat 전용: HighSpec 모드 강제 (메인 씬은 Balanced 기본값 유지)
            ActionFeel.SetMode(ActionFeelMode.HighSpec);

            EnsureGameManager();
            SetupPlayer();
            SetupCamera();
            SetupGround();
            SetupLight();
            EnsureEventSystem();
            SpawnLord();
            SpawnGuard();
            SpawnMonster("slime");
            AttachAttackSystem();
        }

        // ================================================================
        // 매니저
        // ================================================================
        private void EnsureGameManager()
        {
            if (GameManager.Instance != null) return;

            var gm = new GameObject("GameManager");
            gm.AddComponent<GameManager>();
            gm.AddComponent<BuffManager>();
            gm.AddComponent<MonsterLevelManager>();
            gm.AddComponent<MonsterAggroSystem>();
            gm.AddComponent<MonsterSkillSystem>();
            Debug.Log("[TestTerritoryCombat] ✅ GameManager + 시스템 생성");
        }

        // ================================================================
        // 플레이어 (TestCombatSetup와 동일 구성)
        // ================================================================
        private void SetupPlayer()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                player = new GameObject("Player");
                player.tag = "Player";
            }

            if (player.GetComponent<CharacterController>() == null)
            {
                var cc = player.AddComponent<CharacterController>();
                cc.height = 2f;
                cc.radius = 0.5f;
            }

            var pmType = typeof(ProjectName.Systems.PlayerMovement);
            if (player.GetComponent(pmType) == null)
                player.AddComponent(pmType);

            if (player.GetComponent<UnityEngine.InputSystem.PlayerInput>() == null)
            {
                var pi = player.AddComponent<UnityEngine.InputSystem.PlayerInput>();
                pi.defaultActionMap = "Player";
                pi.notificationBehavior = UnityEngine.InputSystem.PlayerNotifications.InvokeUnityEvents;
            }

            if (player.GetComponent<PlayerCombat>() == null)
                player.AddComponent<PlayerCombat>();
            if (player.GetComponent<PlayerHealth>() == null)
                player.AddComponent<PlayerHealth>();
            if (player.GetComponent<PlayerStats>() == null)
                player.AddComponent<PlayerStats>();
            if (player.GetComponent<PlayerPlaceholder>() == null)
                player.AddComponent<PlayerPlaceholder>();
            if (player.GetComponent<PlayerInventory>() == null)
                player.AddComponent<PlayerInventory>();

            player.transform.position = Vector3.zero;
            Debug.Log("[TestTerritoryCombat] ✅ Player 설정 완료");
        }

        // ================================================================
        // 카메라 / 지면 / 조명 / 이벤트
        // ================================================================
        private void SetupCamera()
        {
            GameObject camGO = GameObject.FindGameObjectWithTag("MainCamera");
            if (camGO == null)
            {
                camGO = new GameObject("Main Camera");
                camGO.tag = "MainCamera";
            }

            Camera cam = camGO.GetComponent<Camera>();
            if (cam == null)
                cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 500f;

            var tdcType = typeof(ProjectName.Systems.TopDownCameraController);
            if (camGO.GetComponent(tdcType) == null)
                camGO.AddComponent(tdcType);
            if (camGO.GetComponent<AudioListener>() == null)
                camGO.AddComponent<AudioListener>();

            Debug.Log("[TestTerritoryCombat] ✅ 카메라 설정 완료");
        }

        private void SetupGround()
        {
            if (GameObject.Find("Ground") != null) return;

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0, -0.5f, 0);
            ground.transform.localScale = Vector3.one * 40f;

            var renderer = ground.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(0.2f, 0.5f, 0.2f, 1f);
                renderer.material = mat;
            }
            Debug.Log("[TestTerritoryCombat] ✅ Ground 생성");
        }

        private void SetupLight()
        {
            if (FindAnyObjectByType<Light>() != null) return;

            var lightGO = new GameObject("Directional Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            Debug.Log("[TestTerritoryCombat] ✅ Directional Light 생성");
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;

            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            Debug.Log("[TestTerritoryCombat] ✅ EventSystem 생성");
        }

        // ================================================================
        // 타영주 영지 (DraculaLord — IDamageable)
        // ================================================================
        private void SpawnLord()
        {
            GameObject lordGO = new GameObject("TerritoryLord");
            lordGO.transform.position = _lordPos;
            lordGO.tag = "DraculaLord";

            var lord = lordGO.AddComponent<DraculaLord>();
            lord.SetTerritoryId(new TerritoryId(NationType.Dracula, 1));

            // Raycast 히트용 Collider (IDamageable 대상)
            if (lordGO.GetComponent<Collider>() == null)
            {
                var col = lordGO.AddComponent<BoxCollider>();
                col.size = new Vector3(2, 3, 2);
            }
            if (lordGO.GetComponent<Rigidbody>() == null)
            {
                var rb = lordGO.AddComponent<Rigidbody>();
                rb.useGravity = true;
                rb.mass = 4f;
            }
            if (lordGO.GetComponent<HitReaction>() == null)
                lordGO.AddComponent<HitReaction>();

            Debug.Log($"[TestTerritoryCombat] ✅ 타영주 영지 생성 ({_lordPos}, HP:{lord.MaxHP}, ATK:{lord.AttackDamage})");
        }

        // ================================================================
        // 병사 (GuardPlaceholder — IDamageable)
        // ================================================================
        private void SpawnGuard()
        {
            GameObject guardGO = new GameObject("Guard_0");
            guardGO.transform.position = _guardPos;
            guardGO.tag = "Guard";

            var guard = guardGO.AddComponent<GuardPlaceholder>();

            // Raycast 히트용 Collider
            if (guardGO.GetComponent<Collider>() == null)
            {
                var col = guardGO.AddComponent<BoxCollider>();
                col.size = new Vector3(0.6f, 1.8f, 0.6f);
            }
            if (guardGO.GetComponent<Rigidbody>() == null)
            {
                var rb = guardGO.AddComponent<Rigidbody>();
                rb.useGravity = true;
                rb.mass = 1f;
            }
            if (guardGO.GetComponent<HitReaction>() == null)
                guardGO.AddComponent<HitReaction>();

            // 시각 바디 (Collider 제거 — 물리 간섭 방지)
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Guard_Visual_0";
            visual.transform.SetParent(guardGO.transform, false);
            visual.transform.localPosition = new Vector3(0, 1f, 0);
            visual.transform.localScale = new Vector3(0.5f, 1f, 0.5f);

            var renderer = visual.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(0.2f, 0.6f, 0.2f, 1f);
                renderer.material = mat;
            }
            var visCol = visual.GetComponent<Collider>();
            if (visCol != null)
                DestroyImmediate(visCol);

            Debug.Log("[TestTerritoryCombat] ✅ 병사 생성");
        }

        // ================================================================
        // 몬스터 1 (TestCombatSetup 패턴)
        // ================================================================
        private void SpawnMonster(string monsterId)
        {
            GameObject go = null;

            MonsterDef def = MonsterDatabase.Get(monsterId);
            if (def == null)
            {
                Debug.LogWarning($"[TestTerritoryCombat] MonsterDef '{monsterId}' 없음. 프리미티브 생성");
                def = new MonsterDef(monsterId, monsterId, MonsterTier.Intermediate, 20f, 5, 4f, Color.white);
            }

            string modelPath = GetMonsterModelPath(monsterId);
            if (!string.IsNullOrEmpty(modelPath))
            {
                GameObject modelPrefab = Resources.Load<GameObject>(modelPath);
                if (modelPrefab != null)
                    go = Instantiate(modelPrefab, _monsterPos, Quaternion.identity);
            }

            if (go == null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.transform.position = _monsterPos;
                go.transform.localScale = Vector3.one * 1.5f;
                var r = go.GetComponent<Renderer>();
                if (r != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.color = Color.yellow;
                    r.material = mat;
                }
            }

            go.name = $"Monster_{monsterId}";
            go.tag = "Monster";

            AnimalAI ai = go.GetComponent<AnimalAI>();
            if (ai == null)
                ai = go.AddComponent<AnimalAI>();
            ai.SetMonsterId(monsterId);

            if (go.GetComponent<Collider>() == null)
            {
                var col = go.AddComponent<BoxCollider>();
                col.size = new Vector3(1, 1, 1);
            }
            if (go.GetComponent<Rigidbody>() == null)
            {
                var rb = go.AddComponent<Rigidbody>();
                rb.useGravity = true;
                rb.mass = 1f;
            }
            if (go.GetComponent<HitReaction>() == null)
                go.AddComponent<HitReaction>();

            Debug.Log($"[TestTerritoryCombat] ✅ 몬스터 생성 ({monsterId}, {_monsterPos})");
        }

        private string GetMonsterModelPath(string monsterId)
        {
            return monsterId switch
            {
                "rabbit" => "Models/UserProvided/Rabbit_Rigged",
                "wolf" => "Models/UserProvided/Wolf_Rigged",
                "slime" => "Models/UserProvided/Slime_Rigged",
                _ => null
            };
        }

        // ================================================================
        // 공격 시스템 (점검 대상 중심)
        // ================================================================
        private void AttachAttackSystem()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            if (player.GetComponent<AttackSystem>() == null)
                player.AddComponent<AttackSystem>();

            Debug.Log("[TestTerritoryCombat] ✅ AttackSystem 부착 완료 — 좌클릭으로 공격 점검");
        }

        // ================================================================
        // OnGUI 안내
        // ================================================================
        private void OnGUI()
        {
            float labelWidth = 420;
            float labelHeight = 50;
            float x = (Screen.width - labelWidth) / 2f;
            float y = Screen.height - 110;

            GUI.Box(new Rect(x, y, labelWidth, labelHeight),
                "🎯 공격시스템 점검 — 좌클릭=공격\n영지(타영주)·병사·몬스터 1기씩 → IDamageable 데미지 확인");
        }
    }
}