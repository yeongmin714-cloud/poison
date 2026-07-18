using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// Test_03_Combat 씬 전용: 전투 + 몬스터 시스템 통합 검증.
    /// Player(이동/공격) + 여러 몬스터(4족/2족/보스) 생성 + 전투 로그 검증.
    /// </summary>
    public class TestCombatSetup : MonoBehaviour
    {
        [Header("Monster Spawning")]
        [SerializeField] private bool _spawnTestMonsters = true;
        [SerializeField] private int _monsterCount = 5;

        [Header("Player Settings")]
        [SerializeField] private float _walkSpeed = 5f;
        [SerializeField] private float _runSpeed = 10f;

        private void Awake()
        {
            EnsureGameManager();
            SetupPlayer();
            SetupCamera();
            SetupGround();
            SetupLight();
            EnsureEventSystem();

            if (_spawnTestMonsters)
                SpawnTestMonstersDelayed();
        }

        private void EnsureGameManager()
        {
            if (GameManager.Instance == null)
            {
                var gm = new GameObject("GameManager");
                gm.AddComponent<GameManager>();
                gm.AddComponent<BuffManager>();
                gm.AddComponent<MonsterLevelManager>();
                gm.AddComponent<MonsterAggroSystem>();
                gm.AddComponent<MonsterSkillSystem>();
                Debug.Log("[TestCombatSetup] ✅ GameManager + 시스템 생성");
            }
        }

        private void SetupPlayer()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                player = new GameObject("Player");
                player.tag = "Player";
            }

            // CharacterController
            if (player.GetComponent<CharacterController>() == null)
            {
                var cc = player.AddComponent<CharacterController>();
                cc.height = 2f;
                cc.radius = 0.5f;
            }

            // PlayerMovement
            var pmType = typeof(ProjectName.Systems.PlayerMovement);
            if (player.GetComponent(pmType) == null)
                player.AddComponent(pmType);

            // PlayerInput
            if (player.GetComponent<UnityEngine.InputSystem.PlayerInput>() == null)
            {
                var pi = player.AddComponent<UnityEngine.InputSystem.PlayerInput>();
                pi.defaultActionMap = "Player";
                pi.notificationBehavior = UnityEngine.InputSystem.PlayerNotifications.InvokeUnityEvents;
            }

            // PlayerCombat
            if (player.GetComponent<PlayerCombat>() == null)
                player.AddComponent<PlayerCombat>();

            // PlayerHealth
            if (player.GetComponent<PlayerHealth>() == null)
                player.AddComponent<PlayerHealth>();

            // PlayerStats
            if (player.GetComponent<PlayerStats>() == null)
                player.AddComponent<PlayerStats>();

            // PlayerPlaceholder (시각적 바디)
            if (player.GetComponent<PlayerPlaceholder>() == null)
                player.AddComponent<PlayerPlaceholder>();

            // PlayerInventory
            if (player.GetComponent<PlayerInventory>() == null)
                player.AddComponent<PlayerInventory>();

            player.transform.position = Vector3.zero;
            Debug.Log("[TestCombatSetup] ✅ Player + Combat 시스템 설정 완료");
        }

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

            Debug.Log("[TestCombatSetup] ✅ 카메라 설정 완료");
        }

        private void SetupGround()
        {
            if (GameObject.Find("Ground") == null)
            {
                GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Ground";
                ground.transform.position = new Vector3(0, -0.5f, 0);
                ground.transform.localScale = Vector3.one * 50f;

                var renderer = ground.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.color = new Color(0.2f, 0.5f, 0.2f, 1f);
                    mat.SetFloat("_Smoothness", 0f);
                    renderer.material = mat;
                }
                Debug.Log("[TestCombatSetup] ✅ Ground 생성");
            }
        }

        private void SetupLight()
        {
            if (FindAnyObjectByType<Light>() == null)
            {
                var lightGO = new GameObject("Directional Light");
                var light = lightGO.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                light.transform.rotation = Quaternion.Euler(50, -30, 0);
                Debug.Log("[TestCombatSetup] ✅ Directional Light 생성");
            }
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                Debug.Log("[TestCombatSetup] ✅ EventSystem 생성");
            }
        }

        private void SpawnTestMonstersDelayed()
        {
            // 각 티어별 몬스터 생성
            SpawnMonsterOfType("rabbit", new Vector3(5, 0, 3), MonsterTier.Beginner);
            SpawnMonsterOfType("wolf", new Vector3(8, 0, 0), MonsterTier.Beginner);
            SpawnMonsterOfType("boar", new Vector3(5, 0, -3), MonsterTier.Beginner);
            SpawnMonsterOfType("deer", new Vector3(-5, 0, 3), MonsterTier.Beginner);
            SpawnMonsterOfType("slime", new Vector3(-8, 0, 0), MonsterTier.Intermediate);
            SpawnMonsterOfType("stone_golem", new Vector3(-5, 0, -3), MonsterTier.Intermediate);
            SpawnMonsterOfType("ogre", new Vector3(10, 0, 5), MonsterTier.Advanced);
            SpawnMonsterOfType("shadow_assassin", new Vector3(10, 0, -5), MonsterTier.Advanced);

            Debug.Log("[TestCombatSetup] ✅ 테스트 몬스터 8마리 생성 완료 (Beginner/Intermediate/Advanced)");
        }

        private GameObject SpawnMonsterOfType(string monsterId, Vector3 position, MonsterTier tier)
        {
            MonsterDef def = MonsterDatabase.Get(monsterId);
            if (def == null)
            {
                Debug.LogWarning($"[TestCombatSetup] MonsterDef '{monsterId}' 없음. 프리미티브 생성");
                def = new MonsterDef(monsterId, monsterId, tier, 20f, 5, 4f, Color.white);
            }

            // GLB 모델 로드 시도
            GameObject go = null;
            string modelPath = GetMonsterModelPath(monsterId);
            if (!string.IsNullOrEmpty(modelPath))
            {
                GameObject modelPrefab = Resources.Load<GameObject>($"Models/UserProvided/{modelPath}");
                if (modelPrefab != null)
                    go = Instantiate(modelPrefab, position, Quaternion.identity);
            }

            // 프리미티브 폴백
            if (go == null)
                go = CreatePrimitiveMonster(monsterId, position, tier);

            go.name = $"Monster_{monsterId}";
            go.tag = "Monster";

            // AnimalAI
            AnimalAI ai = go.GetComponent<AnimalAI>();
            if (ai == null)
                ai = go.AddComponent<AnimalAI>();
            ai.SetMonsterId(monsterId);

            // Collider 확인
            if (go.GetComponent<Collider>() == null)
            {
                var col = go.AddComponent<BoxCollider>();
                col.size = new Vector3(1, 1, 1);
            }

            // Rigidbody 확인 (IDamageable 충돌/넉백용)
            if (go.GetComponent<Rigidbody>() == null)
            {
                var rb = go.AddComponent<Rigidbody>();
                rb.useGravity = true;
                rb.mass = 1f;
            }

            // HitReaction (IDamageable에서 참조)
            if (go.GetComponent<HitReaction>() == null)
                go.AddComponent<HitReaction>();

            // Quadruped monster 체크: Wolf, Boar, Deer는 4족
            if (IsQuadruped(monsterId))
            {
                // QuadrupedProceduralAnimation이 없으면 추가
                if (go.GetComponent<QuadrupedProceduralAnimation>() == null)
                {
                    Debug.Log($"[TestCombatSetup] ⚠️ {monsterId}: QuadrupedProceduralAnimation 미탑재 — GLB 모델이 Animator를 제공해야 함");
                }

                // QuadrupedProceduralAnimation이 Animator + Rigidbody를 RequireComponent로 가지므로 확인
                if (go.GetComponent<Animator>() == null)
                {
                    Debug.Log($"[TestCombatSetup] ⚠️ {monsterId}: Animator 없음 — QuadrupedProceduralAnimation 동작 불가");
                }
                if (go.GetComponent<Rigidbody>() == null)
                {
                    Debug.Log($"[TestCombatSetup] ⚠️ {monsterId}: Rigidbody 없음 — QuadrupedProceduralAnimation 동작 불가");
                }
            }

            Debug.Log($"[TestCombatSetup] ✅ {monsterId}({tier}) at {position}");
            return go;
        }

        private bool IsQuadruped(string monsterId)
        {
            return monsterId switch
            {
                "wolf" => true,
                "boar" => true,
                "deer" => true,
                _ => false
            };
        }

        private string GetMonsterModelPath(string monsterId)
        {
            return monsterId switch
            {
                "rabbit" => "Rabbit_Rigged",
                "wolf" => "Wolf_Rigged",
                "boar" => "Boar_Rigged",
                "deer" => "Deer_Rigged",
                "poison_snake" => "Snake_Rigged",
                "bat" => "Bat_Rigged",
                "giant_rat" => "Big_Mouse_Rigged",
                "crow" => "Crow_Rigged",
                "slime" => "Slime_Rigged",
                "stone_golem" => "Golem_Rigged",
                "fire_lizard" => "Fire_Lizard_Rigged",
                "electric_porcupine" => "Electric_Spine_Hedgehog_Rigged",
                "swamp_croc" => "Swamp_Alligator_Rigged",
                "forest_spirit" => "Wooden Forest Spirit",
                "wild_troll" => "Wild_Troll_Rigged",
                "ogre" => "Swamp_Ogre_Rigged",
                "banshee" => "Banshee_Rigged",
                "griffin" => "Griffon_Rigged",
                "minotaur" => "Minotaur_Rigged",
                "manticore" => "Manticore_Rigged",
                "salamander" => "Salamander_Rigged",
                "shadow_assassin" => "Shadow_Assassin_Rigged",
                _ => null
            };
        }

        private GameObject CreatePrimitiveMonster(string monsterId, Vector3 position, MonsterTier tier)
        {
            Color color = tier switch
            {
                MonsterTier.Beginner => Color.green,
                MonsterTier.Intermediate => Color.yellow,
                MonsterTier.Advanced => Color.red,
                _ => Color.white
            };

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.position = position;
            go.transform.localScale = tier switch
            {
                MonsterTier.Beginner => Vector3.one * 1f,
                MonsterTier.Intermediate => Vector3.one * 1.5f,
                MonsterTier.Advanced => Vector3.one * 2f,
                _ => Vector3.one
            };

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = color;
                renderer.material = mat;
            }

            return go;
        }
    }
}