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

            // GetHeightAt 계약 정합: 배치 좌표의 XZ는 존중하고 y만 수식 표면으로 재설정.
            // PlayerMovement.ClampToGroundByHeight / BlobShadow가 기대하는 표면(1+GetHeightAt) 위에
            // 엔티티가 정확히 떨어지도록 하여 텔레포트 진동을 근본 차단.
            float lordY = SurfaceY(_lordPos.x, _lordPos.z) + 1.6f;   // 영주 box(3m)
            float guardY = SurfaceY(_guardPos.x, _guardPos.z) + 1.0f; // 병사 box(1.8m)
            float monsterY = SurfaceY(_monsterPos.x, _monsterPos.z) + 0.9f; // 몬스터 box(1m, GO scale 1.5)
            _lordPos = new Vector3(_lordPos.x, lordY, _lordPos.z);
            _guardPos = new Vector3(_guardPos.x, guardY, _guardPos.z);
            _monsterPos = new Vector3(_monsterPos.x, monsterY, _monsterPos.z);

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

            // Phase 1 훅: Test_10 전용 애니 부트 — 레거시 Procedural/Neural 제거 + Player_AC/HumanoidClipDriver 부착
            var playerAnimBoot = GameObject.FindGameObjectWithTag("Player");
            if (playerAnimBoot != null) playerAnimBoot.AddComponent<TestPlayerAnimatorBoot>();
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
        // 지형 수식 계약 (PlayerMovement.ClampToGroundByHeight와 동일 기준)
        // ================================================================
        /// <summary>
        /// GetHeightAt 수식 표면 y (1 + 높이). PlayerMovement가 플레이어/그림자에 대해
        /// 기대하는 바로 그 값 — 이 표면 위로 미니지형/스폰을 정렬해야 진동이 없다.
        /// </summary>
        private static float SurfaceY(float x, float z)
        {
            try
            {
                return 1f + ProjectName.Systems.TerrainGenerator.GetHeightAt(
                    x, z, ProjectName.Core.Data.BiomeType.Plains, 42);
            }
            catch
            {
                return 1f;
            }
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

            // PlayerMovement.Awake의 (728, …) 스폰 오버라이드를 여기서 이긴다(실행 순서 의존 유지).
            // 캡슐 중심 = 표면 + 1 + 여유 → ClampToGroundByHeight 텔레포트 트리거 없음.
            player.transform.position = new Vector3(0f, SurfaceY(0f, 0f) + 1.02f, 0f);
            Debug.Log($"[TestTerritoryCombat] ✅ Player 설정 완료 (pos={player.transform.position}, 표면 y={SurfaceY(0f, 0f):F2})");
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

            // Plane 프리미티브(표면 y=-0.5) 대신 GetHeightAt 계약 준수 미니지형.
            // 정점 y = SurfaceY(x, z) = 1 + GetHeightAt(x, z, Plains, 42) →
            // 스폰·ClampToGroundByHeight·BlobShadow가 기대하는 표면과 정확히 일치.
            const int halfSize = 60;   // 반경 ±60m
            const int step = 1;        // 정점 간격 1m
            int vertCount = halfSize * 2 / step + 1; // 121 → 14641 정점

            var vertices = new Vector3[vertCount * vertCount];
            var triangles = new int[(vertCount - 1) * (vertCount - 1) * 6];

            int vi = 0;
            for (int zi = 0; zi < vertCount; zi++)
            {
                for (int xi = 0; xi < vertCount; xi++)
                {
                    float x = -halfSize + xi * step;
                    float z = -halfSize + zi * step;
                    vertices[vi++] = new Vector3(x, SurfaceY(x, z), z);
                }
            }

            // 쿼드당 삼각형 2개 (아래→위 인덱스, 위쪽 면 시계방향 유지)
            int ti = 0;
            for (int zi = 0; zi < vertCount - 1; zi++)
            {
                for (int xi = 0; xi < vertCount - 1; xi++)
                {
                    int tl = zi * vertCount + xi;      // 좌하
                    int tr = tl + 1;                   // 우하
                    int bl = tl + vertCount;           // 좌상
                    int br = bl + 1;                   // 우상
                    triangles[ti++] = tl;
                    triangles[ti++] = bl;
                    triangles[ti++] = tr;
                    triangles[ti++] = tr;
                    triangles[ti++] = bl;
                    triangles[ti++] = br;
                }
            }

            var mesh = new Mesh
            {
                vertices = vertices,
                triangles = triangles
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var ground = new GameObject("Ground");
            ground.transform.position = Vector3.zero; // 메시는 월드 좌표 기준

            var meshFilter = ground.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            var renderer = ground.AddComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.2f, 0.5f, 0.2f, 1f);
            renderer.material = mat;

            var meshCollider = ground.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = mesh;
            meshCollider.convex = false;

            Debug.Log($"[TestTerritoryCombat] ✅ 미니지형 생성 (계약: GetHeightAt+1, 정점 {vertCount}x{vertCount}={vertices.Length}, 원점 표면 y={SurfaceY(0f, 0f):F2})");
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

            // Test_10 Phase 2 훅: 몬스터 헤드 UI(이름/Lv/HP바) 부착 — 그 외 로직 무변경
            if (ai != null)
            {
                var head = go.AddComponent<MonsterHeadUI>();
                head.Setup(ai);
            }
            else
            {
                Debug.LogWarning("[TestTerritoryCombat] ⚠️ AnimalAI 없음 — MonsterHeadUI 부착 생략");
            }

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