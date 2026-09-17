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
        [SerializeField] private Vector3 _myTerritoryPos = new Vector3(0, 0, 32);
        [SerializeField] private Vector3 _enemyTerritoryPos = new Vector3(0, 0, -32);

        private void Awake()
        {
            // Test_10_TerritoryCombat 전용: HighSpec 모드 강제 (메인 씬은 Balanced 기본값 유지)
            ActionFeel.SetMode(ActionFeelMode.HighSpec);

            // Test 씬 분열 비활성(HP바/드랍 미정합) — 슬라임이 HP 30% 이하에서 초록 구체 2개로 늘어나
            // "몬스터가 안 죽고 늘어난다" 체감을 유발하는 것을 차단. 분열체는 AnimalAI만 부착된 풀HP 구체.
            MonsterSkillSystem.SlimeSplitEnabled = false;

            // [2026-09-11] 테스트 씬 몬스터 레벨 스케일 HP 게이트 — 몬스터 몇 타에 사망해야 공격 검증 가능.
            // 영상 실측(테스트씬 영상 4): 레벨 스케일링(hpPerLevel×level)으로 MaxHP가 커져 HP바 비율이
            // 거의 0처럼 보여도 실제 HP가 남아 계속 두드려도 안 죽는 증상 → 스케일링 비활성.
            MonsterLevelManager.LevelScalingEnabled = false;

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
            SetupCamera();      // 2026-09-10: SetupPlayer보다 먼저 — PlayerMovement.Awake의 "카메라 없음! 생성" 에러 방지
            SetupPlayer();
            SetupGround();
            SetupLight();
            EnsureEventSystem();
            SpawnLord();
            SpawnGuard();
            SpawnMonster("slime");
            AttachAttackSystem();
            SetupTerritoriesAndGuards();   // 2026-09-10: 내 영지(PlayerOwned) + 적 영지(EnemyOwned 표기) + 병사 3+3 배치
            SetupHerbs();                  // 2026-09-10: 채집 가능 약초 3종(Red/Purple/Green) 배치 — E키 채집 흐름 점검용
            SetupFarm();                   // 2026-09-10: 농경 시스템 — 내 영지(East_01) 부지 농장 2x2 (E키 파종 → 게임 2일 성장 → HerbPickup 재사용 수확)
            EnsurePlayerHUD();             // 2026-09-10: 하트 HUD 부착(하트 아이콘+숫자HP) — Test_09 선례 이식
            SetupUITestArena();            // 2026-09-10: UI 전수(미니맵/인벤/스탯/창고·크래프트 박스/전 아이템 시딩)

            // 2026-09-10: Test_10에 몬스터 없음 — Aggro 등록 없으므로 시스템 인스턴스만 정리 대상.
            // (기존: EnsureGameManager가 MonsterAggroSystem을 GM에 부착 — DontDestroyOnLoad가 아니라 씬 정리 경고는
            //  AnimalAI 등이 런타임에 Instance 프로퍼티로 자동 생성한 별도 GO. 부팅 후 존재 시 씬 전환 유실 방지 차원에서 유지)

            // Phase 1 훅: Test_10 전용 애니 부트 — 레거시 Procedural/Neural 제거 + Player_AC/HumanoidClipDriver 부착
            var playerAnimBoot = GameObject.FindGameObjectWithTag("Player");
            if (playerAnimBoot != null) playerAnimBoot.AddComponent<TestPlayerAnimatorBoot>();
        }

        // ================================================================
        // 매니저
        // ================================================================
        private void EnsureGameManager()
        {
            // [2026-09-15(59차 후속)] early-return 제거 — GameManager가 이미 존재해도
            // EquipmentManager/ArmorVisualAttachSystem은 항상 보장해야 한다.
            // 기존: if (GameManager.Instance != null) return; → 장비 시스템 생성이 스킵되어
            // 재실행/재스폰 상황에서 '드래그 장착 실패(사유: EquipmentManager 없음)'가 지속됐다.
            GameObject gm = GameManager.Instance != null ? GameManager.Instance.gameObject : new GameObject("GameManager");

            if (GameManager.Instance == null)
            {
                gm.AddComponent<GameManager>();
                gm.AddComponent<BuffManager>();
                gm.AddComponent<MonsterLevelManager>();
                gm.AddComponent<MonsterAggroSystem>();
                gm.AddComponent<MonsterSkillSystem>();
            }
            if (Object.FindAnyObjectByType<GuardHostilitySystem>(FindObjectsInactive.Include) == null)
            {
                gm.AddComponent<GuardHostilitySystem>();   // [57차 후속] 타 영지 병사 적대화 — Instance 보장(중복 가드)
            }

            // [T4 2026-09-15] Test_10(영역 전투 씬)은 CoreSystemsBootstrap 미실행이므로
            // 장비 슬롯(방어구 장착)과 방어구 비주얼 부착 시스템을 이 씬에서 직접 생성 보장.
            // 54차 '장비 착용' 수정은 메인 씬(CoreSystemsBootstrap)만 커버했고, 테스트 씬엔
            // EquipmentManager가 없어 '우클릭 장착 wood_armor → 실패(사유: EquipmentManager 없음)'가 났다.
            // [59차 후속] GameManager 존재 여부와 무관하게 항상 보장(중복 가드 포함) — gm이 새로 생성된
            // 경우엔 이미 위에 없으므로, 여기서 일괄 보장. 중복 AddComponent는 Unity가 방지하나 가드 유지.
            if (Object.FindAnyObjectByType<EquipmentManager>(FindObjectsInactive.Include) == null)
            {
                gm.AddComponent<EquipmentManager>();
                Debug.Log("[TestTerritoryCombat] ✅ EquipmentManager 생성 — 방어구 장착 활성화");
            }
            if (Object.FindAnyObjectByType<ArmorVisualAttachSystem>(FindObjectsInactive.Include) == null)
            {
                gm.AddComponent<ArmorVisualAttachSystem>();
                Debug.Log("[TestTerritoryCombat] ✅ ArmorVisualAttachSystem 생성 — 방어구 비주얼 부착 활성화");
            }
            // [2026-09-17] 병사 방어구 비주얼 부착 (분리/ADDITIVE — 플레이어 시스템과 무관)
            if (Object.FindAnyObjectByType<GuardVisualAttachSystem>(FindObjectsInactive.Include) == null)
            {
                gm.AddComponent<GuardVisualAttachSystem>();
                Debug.Log("[TestTerritoryCombat] ✅ GuardVisualAttachSystem 생성 — 병사 방어구 비주얼 부착 활성화");
            }
            // [2026-09-17] RTS 인터랙션 커서/호버/명령 + 농사/채집 (ADDITIVE)
            ContextCommandRouter.Ensure(gm);       // 커서 시스템(OS커서 Ctrl표시·컸텍스트 아이콘)+좌클릭 병사명령 자가확보
            FarmingSystem.Ensure(gm);              // 경지(농사) — 약초 파종→성장→수확
            GatheringSystem.Ensure(gm);            // 채집(풀) — 약초 리스폰
            // [59차 후속] 드래그 단체 지정 — Test_10은 CoreSystemsBootstrap 미실행이라
            // GuardSelectionManager(좌클릭 드래그 병사 선택)가 생성되지 않아 '내 병사 드래그 지정 모션'이 안 됐다.
            if (Object.FindAnyObjectByType<GuardSelectionManager>(FindObjectsInactive.Include) == null)
            {
                // [TEST27-68차] 전용 GO + DontDestroyOnLoad — 공유 GameManager GO가 중간에 파괴되면
                // GSM Update가 같이 죽어(67차 실측: [RTS] 로그 0건) 드래그가 완전히 무반응이 됐다.
                var ggo = new GameObject("GuardSelectionManager");
                DontDestroyOnLoad(ggo);
                ggo.AddComponent<GuardSelectionManager>();
                Debug.Log("[TestTerritoryCombat] ✅ GuardSelectionManager 생성(전용 GO — 자가치유 대상)");
            }
            if (Object.FindAnyObjectByType<RTSCommandSystem>(FindObjectsInactive.Include) == null)
            {
                var rgo = new GameObject("RTSCommandSystem");
                DontDestroyOnLoad(rgo);
                rgo.AddComponent<RTSCommandSystem>();
                Debug.Log("[TestTerritoryCombat] ✅ RTSCommandSystem 생성(전용 GO)");
            }
            // [TEST21-FOLLOWUP] 화살 발사 시스템 — Test_10은 CoreSystemsBootstrap 미실행이라 ArrowManager가
            // 생성되지 않아(Instance null) 활 공격이 스킵됐다. 여기서 보장.
            if (Object.FindAnyObjectByType<ArrowManager>(FindObjectsInactive.Include) == null)
            {
                gm.AddComponent<ArrowManager>();
                Debug.Log("[TestTerritoryCombat] ✅ ArrowManager 생성 — 화살 발사 활성화");
            }

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

            // 2026-09-11: 공격 범위 표시기 부착 — 무기 타입별 사거리 링(지면). 맨손 때는 자동 숨김.
            WeaponRangeIndicator.EnsureOn(player.transform);

            // [TEST25-66차] 데모 자동장착 — 시작 즉시 방어구 풀셋(wood) + 창이 보이도록.
            // 뿌리(실측): Awake 중 즉시 장착하면 ArmorVisualAttachSystem.AttachRoutine 코루틴이 부팅 프레임에서
            //   첫 yield 후 재개되지 않고 침묵 사망("부착 시작" 로그 후 성공/실패 로그 전무 — Editor.log 실측).
            //   → 장착 이벤트를 부트 완료 후(0.25s)로 지연해 모든 시스템 기동 뒤에 발화. ArmorVisualAttachSystem
            //   쪽에도 동기 즉시 부착 경로를 추가해 이중 방어(패치 66-#3).
            StartCoroutine(EquipDemoStarterGearDelayed());
            Debug.Log($"[TestTerritoryCombat] ✅ Player 설정 완료 (pos={player.transform.position}, 표면 y={SurfaceY(0f, 0f):F2})");
        }

        // [TEST25-66차] 부팅 레이스 흡수 — Awake 완료 후 0.25s 뒤 장착(위 주석 참조).
        System.Collections.IEnumerator EquipDemoStarterGearDelayed()
        {
            yield return new WaitForSeconds(0.25f);
            Debug.Log("[TestTerritoryCombat] 🛡️ 데모 자동장착 시작(부트 지연 해제 — 시스템 기동 완료 후)");
            EquipDemoStarterGear();
        }

        // [2026-09-16] 테스트씬 부팅용 시작 장비 데모 — wood 방어구 풀셋 + 창을 인벤 보장 후 즉시 장착.
        // 모든 매니저는 lazy/Get() 확보, 각 단계 예외 격리(실패해도 크래시 금지 — 데모 목적).
        private void EquipDemoStarterGear()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                Debug.LogWarning("[TestTerritoryCombat] ⚠️ 데모 장착 스킵 — Player 없음");
                return;
            }

            // ① 방어구 아이템 인벤 보장(멱등) + 슬롯별 장착
            var em = ProjectName.Systems.EquipmentManager.Get();
            if (PlayerInventory.Instance != null && em != null)
            {
                // (id, 대상 슬롯) — ArmorVisualAttachSystem.ResolveBones 슬롯 규칙과 정합.
                var gear = new (string id, ProjectName.Systems.EquipmentManager.EquipmentSlot slot)[]
                {
                    ("wood_helmet", ProjectName.Systems.EquipmentManager.EquipmentSlot.Helmet),
                    ("wood_armor",  ProjectName.Systems.EquipmentManager.EquipmentSlot.Armor),
                    ("wood_boot",  ProjectName.Systems.EquipmentManager.EquipmentSlot.Shoes),      // [TEST28-69차] 좌우 분리 → 통합(양발 자동)
                    ("wood_glove", ProjectName.Systems.EquipmentManager.EquipmentSlot.Gloves),     // [TEST28-69차] 통합(양손 자동)
                    ("wood_shield", ProjectName.Systems.EquipmentManager.EquipmentSlot.Back),
                };

                foreach (var (id, slot) in gear)
                {
                    try
                    {
                        if (!PlayerInventory.Instance.HasItem(id))
                        {
                            var itemData = PlayerInventory.GetItemById(id);
                            if (itemData == null)
                            {
                                Debug.LogWarning($"[TestTerritoryCombat] ⚠️ 데모 장착 스킵 — ItemData 미정의: {id}");
                                continue;
                            }
                            PlayerInventory.Instance.AddItem(itemData, 1);
                        }
                        // 해당 id 슬롯을 찾아 EquipmentManager.EquipItem(인벤 소모 + OnEquipmentChanged 발화).
                        PlayerInventory.ItemSlot invSlot = null;
                        var all = PlayerInventory.Instance.GetAllSlots();
                        if (all != null)
                        {
                            foreach (var s in all)
                            {
                                if (s != null && s.item != null && s.item.id == id)
                                { invSlot = s; break; }
                            }
                        }
                        if (invSlot == null)
                        {
                            Debug.LogWarning($"[TestTerritoryCombat] ⚠️ 데모 장착 스킵 — 인벤 슬롯 미발견: {id}");
                            continue;
                        }
                        if (em.EquipItem(invSlot, slot))
                            Debug.Log($"[TestTerritoryCombat] 🛡️ 자동장착: {id} → {slot}");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[TestTerritoryCombat] ⚠️ 데모 장착 예외({id}) — 계속: {e.Message}");
                    }
                }
            }
            else
            {
                Debug.LogWarning("[TestTerritoryCombat] ⚠️ 데모 방어구 장착 스킵 — PlayerInventory/EquipmentManager 없음");
            }

            // ② 창 시작 무기 — 시작 즉시 그립(전방 수리) 표시.
            try
            {
                ProjectName.Systems.WeaponEquipManager.Equip("weapon_spear_wood", player.transform, ProjectName.Core.WeaponType.Spear);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[TestTerritoryCombat] ⚠️ 데모 창 장착 예외 — 계속: {e.Message}");
            }

            Debug.Log("[TestTerritoryCombat] ✅ 데모 무기/방어구 자동장착 완료");
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
                rb.useGravity = false;   // 2026-09-15 접지: 병사는 명령 기반 이동 — 중력 off로 지면 고정(TestTerritory CreateGuard와 동일)
                rb.isKinematic = true;
                rb.mass = 1f;
            }
            if (guardGO.GetComponent<HitReaction>() == null)
                guardGO.AddComponent<HitReaction>();

            // [TEST27-68차] 병사 헤드 UI — 이름 + Lv + HP바 (몬스터 MonsterHeadUI 대응)
            if (guardGO.GetComponent<GuardHeadUI>() == null)
                guardGO.AddComponent<GuardHeadUI>();

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
                {
                    go = Instantiate(modelPrefab, _monsterPos, Quaternion.identity);
                    // [2026-09-11] bounds 기반 접지 — GLB 피벗 오프셋과 무관하게 발끝이 _monsterPos.y
                    // (기존 SurfaceY 계약)에 정렬. 기존 접지 보정 코드 없음 → 신규 적용(중복 아님).
                    GroundModelToY(go, _monsterPos.y);
                }
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
                // 2026-09-11: glTFast GLB 프리팹은 메시 콜라이더를 전혀 생성하지 않는다 →
                // 이 BoxCollider가 몬스터의 '유일한' 레이캐스트 히트 볼륨. GLB 시각 몸체(토끼/늑대)는
                // 1.5박스(중심 0,0,0)보다 크고 위로 솟아 있어 커서가 몸통 위에 있어도 레이가 완전히 빗나갔다.
                // 2×2×2 + center y 0.75로 확대해 시각 몸체 대부분을 커버 (기존 1→1.5 확대 선례 연장).
                col.size = new Vector3(2.0f, 2.0f, 2.0f);
                col.center = new Vector3(0f, 0.75f, 0f);
                Debug.Log($"[TestTerritoryCombat] 📦 {go.name} 히트용 BoxCollider 추가 (2x2x2, center.y=0.75) — GLB 메시 콜라이더 부재 대비");
            }
            if (go.GetComponent<Rigidbody>() == null)
            {
                var rb = go.AddComponent<Rigidbody>();
                rb.useGravity = true;
                rb.mass = 1f;
            }
            // 2026-09-10: 슬라임 비행 차단 — GLB 프리팹에 자동부착된 절차애니 컴포넌트(ModelAnimatorAssigner 계열)가
            // RequireComponent(Rigidbody)로 비관성 rb를 남기고, HitReaction 넉백(AddForce Impulse)+HighSpec 절트(홉 y+0.4, 2배)가
            // 중첩되면 몬스터가 공중으로 날아가 파란 스카이박스 위로 이동하는 증상이 발생했다.
            // Test_10은 공격 판정 점검용이므로 몬스터 rb를 완전 관성화(중력 유지, 외력 무시)하고 절트도 차단한다.
            {
                var mrb = go.GetComponent<Rigidbody>();
                if (mrb != null)
                {
                    mrb.isKinematic = false;
                    mrb.useGravity = true;
                    mrb.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ
                                    | RigidbodyConstraints.FreezeRotation;   // y만 물리(접지), 수평은 AnimalAI Transform 제어
                    mrb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                    mrb.linearDamping = 0f; mrb.angularDamping = 0.05f;
                }
                // 넉백/절트 차단은 HitReaction 확정 '이후'에 적용 — GLB 프리팹은 HitReaction 미부착이므로
                // 이전 순서(GetComponent→null→스킵 후 AddComponent)로는 신규 추가분에 스위치가
                // 빠져 비행 증상이 재발한다 (2026-09-10 QA 순서 버그 수정)
            }
            if (go.GetComponent<HitReaction>() == null)
                go.AddComponent<HitReaction>();
            go.GetComponent<HitReaction>()?.DisableKnockback();   // 넉백/절트 완전 오프 — 절차애니 몬스터는 HitReaction 넉백이 Transform을 유령처럼 이동시킴

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
        // OnGUI 안내 — [TEST28-69차 후속6] "좌클릭=공격" 점검 안내창 사용자 요청으로 제거
        // ================================================================

        // ================================================================
        // 내/적 소속 영지 배치 (2026-09-10 신규 — 기존 셋업 무변경, 신규 배치만 추가)
        // ================================================================
        private void SetupTerritoriesAndGuards()
        {
            SetupMyTerritory();
            SetupEnemyTerritory();
        }

        /// <summary>
        /// 내 소속 영지(PlayerOwned): 파란 성 1채 + 내 병사 3명(레벨 10, East, 파랑, 포섭됨).
        /// 뒤쪽(+z)에 배치해 SpawnLord(0,0,15)/SpawnGuard(3,0,12)와 겹치지 않는다.
        /// </summary>
        private void SetupMyTerritory()
        {
            TerritoryDatabase.Instance.SetOwnership(NationType.East, 1, TerritoryOwnership.PlayerOwned);

            // 성 (순수 시각 — Collider 제거, 큐브 피벗이 중심이므로 y = 표면 + 높이/2 로 바닥 정렬)
            float cx = _myTerritoryPos.x, cz = _myTerritoryPos.z;
            float baseY = SurfaceY(cx, cz);
            var castle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            castle.name = "Territory_My_PlayerOwned";
            castle.transform.position = new Vector3(cx, baseY + 4f, cz); // 높이 8 → 중심 +4, 바닥 = 수식 표면
            castle.transform.localScale = new Vector3(14f, 12f, 14f);   // 2026-09-10: 대형화 — 카메라(원점 추적)에서도 확실히 보이게
            var castleRenderer = castle.GetComponent<MeshRenderer>();
            if (castleRenderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(0.25f, 0.4f, 0.85f, 1f); // 내 진영 파랑
                castleRenderer.material = mat;
            }
            var castleCol = castle.GetComponent<Collider>();
            if (castleCol != null) DestroyImmediate(castleCol); // Raycast 히트 대상은 병사뿐

            // 내 병사 3명 — 성 전면(-z)에 3m 간격
            float frontZ = cz - 6f;
            for (int i = 0; i < 3; i++)
            {
                float gx = cx - 3f + i * 3f;
                Vector3 pos = new Vector3(gx, SurfaceY(gx, frontZ) + 1.0f, frontZ);
                CreateGuard($"MyGuard_{i}", pos, $"내병사{i + 1}", 10, NationType.East,
                    true, new Color(0.2f, 0.4f, 0.9f, 1f));
            }

            Debug.Log($"[MyTerritory] ✅ 내 소속 영지(PlayerOwned) 배치 — 성 'Territory_My_PlayerOwned' @({cx:F1}, {baseY:F1}, {cz:F1}), 내병사 3명(레벨 10, East, 파랑)");
        }

        /// <summary>
        /// 적 소속 영지(EnemyOwned 표기): 빨간 성 1채 + 적 문지기 3명(레벨 15, North, 빨강, 미포섭).
        /// 참고: TerritoryOwnership 열거형에는 EnemyOwned 값이 없어(Unoccupied/PlayerOwned/LordOwned/Contested)
        /// 소유권은 '적 AI 영주 소유'에 해당하는 LordOwned로 등록하고, GO 이름은 요구대로 EnemyOwned 표기를 유지한다.
        /// </summary>
        private void SetupEnemyTerritory()
        {
            TerritoryDatabase.Instance.SetOwnership(NationType.North, 1, TerritoryOwnership.LordOwned);

            // 성 (순수 시각 — Collider 제거, 바닥 정렬 동일 계약)
            float cx = _enemyTerritoryPos.x, cz = _enemyTerritoryPos.z;
            float baseY = SurfaceY(cx, cz);
            var castle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            castle.name = "Territory_Enemy_EnemyOwned";
            castle.transform.position = new Vector3(cx, baseY + 4f, cz);
            castle.transform.localScale = new Vector3(14f, 12f, 14f);   // 2026-09-10: 대형화 — 원점 카메라에서도 가시성 확보
            var castleRenderer = castle.GetComponent<MeshRenderer>();
            if (castleRenderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(0.85f, 0.3f, 0.3f, 1f); // 적 진영 빨강
                castleRenderer.material = mat;
            }
            var castleCol = castle.GetComponent<Collider>();
            if (castleCol != null) DestroyImmediate(castleCol);

            // 적 문지기 3명 — 성문(-z) 방향, 성에서 4~6m 앞
            float gateZ = cz - 10f;
            for (int i = 0; i < 3; i++)
            {
                float gx = cx - 3f + i * 3f;
                Vector3 pos = new Vector3(gx, SurfaceY(gx, gateZ) + 1.0f, gateZ);
                CreateGuard($"EnemyGateGuard_{i}", pos, $"적문지기{i + 1}", 15, NationType.North,
                    false, new Color(0.9f, 0.3f, 0.3f, 1f));
            }

            Debug.Log($"[EnemyTerritory] ✅ 적 소속 영지(EnemyOwned 표기) 배치 — 성 'Territory_Enemy_EnemyOwned' @({cx:F1}, {baseY:F1}, {cz:F1}), 적문지기 3명(레벨 15, North, 빨강)");
        }

        // ================================================================
        // 2026-09-10: 하트 HUD 부착 (Test_09 EnsurePlayerHUD 이식 — 리플렉션, 중복 방지)
        // ================================================================
        private System.Type _hudType;

        private void EnsurePlayerHUD()
        {
            try
            {
                if (_hudType == null)
                {
                    var uiAssembly = System.Reflection.Assembly.Load("ProjectName.UI");
                    _hudType = uiAssembly != null ? uiAssembly.GetType("ProjectName.UI.HUD") : null;
                }
                if (_hudType == null)
                {
                    Debug.LogWarning("[TestTerritoryCombat] ⚠️ ProjectName.UI.HUD 타입 미발견 — HUD 건너뜀");
                    return;
                }

                var existing = UnityEngine.Object.FindObjectsByType(_hudType, FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                if (existing != null && existing.Length > 0)
                {
                    Debug.Log("[TestTerritoryCombat] ℹ️ 이미 HUD 존재 — 건너뜀");
                    return;
                }

                var hudGO = new GameObject("HUD");
                hudGO.AddComponent(_hudType);
                Debug.Log("[TestTerritoryCombat] ✅ 하트 HUD 부착 (하트 아이콘 + 숫자HP)");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[TestTerritoryCombat] HUD 부착 실패(무시): {ex.Message}");
            }
        }

        // ================================================================
        // 병사 공용 생성 (SpawnGuard 패턴 그대로 — 이름/위치/레벨/국가/포섭/색만 파라미터화)
        // ================================================================
        private GameObject CreateGuard(string goName, Vector3 pos, string guardName, int level,
            NationType nation, bool recruited, Color color)
        {
            // [59차 후속] 병사 접지 — 루트를 지면(SurfaceY)에 직접 놓는다.
            // 기존: pos.y = SurfaceY+1.0(레이케스트용 박스 오프셋)을 루트로 쓰고, 모델만 GroundModelToY로
            // 지면에 내려 발이 땅에 닿게 했다. 이 구조는 StepToward(루트.y를 TryGetGroundY=지면으로 보정)가
            // 루트를 지면으로 내리면 모델은 상대 -1.0 이 되어 지면 아래로 파묻히는 모순을 낳았다('병사 땅으로 사라짐').
            // → 루트 자체를 지면에 두고, BoxCollider 히트 볼륨은 루트 기준 위쪽(+0.9)으로 조정한다.
            pos.y = SurfaceY(pos.x, pos.z);
            GameObject guardGO = new GameObject(goName);
            guardGO.transform.position = pos;
            guardGO.tag = "Guard";

            var guard = guardGO.AddComponent<GuardPlaceholder>();
            guard.SetGuardInfo(guardName, level, nation);
            guard.SetRecruited(recruited);

            // Raycast 히트용 Collider — [59차 후속] 루트가 지면으로 내려왔으므로 히트 볼륨을 위쪽(+0.9)으로 올려 몸통 커버
            if (guardGO.GetComponent<Collider>() == null)
            {
                var col = guardGO.AddComponent<BoxCollider>();
                col.size = new Vector3(0.6f, 1.8f, 0.6f);
                col.center = new Vector3(0f, 0.9f, 0f);
            }
            if (guardGO.GetComponent<Rigidbody>() == null)
            {
                var rb = guardGO.AddComponent<Rigidbody>();
                // 2026-09-15 접지 수정: useGravity=true면 모델 부착 후에도 중력이 발끝을 지면 밑으로
                // 가라앉히거나(GroundModelToY로 맞춘 pos.y가 매 프레임 깨짐) 떠 있는 상태를 유지한다.
                // 내/적 병사 이동은 GuardPlaceholder.StepToward가 명령 기반 transform(MovePosition 또는
                // transform.position)으로 수행하므로 중력이 불필요 → 중력 off + isKinematic으로 지면 고정.
                rb.useGravity = false;    // 중력 off — 명령 외력으로 y를 흔들지 않음(접지 안정화)
                rb.isKinematic = true;   // kinematic → StepToward가 transform.position 직접 이동 채택
                rb.mass = 1f;
            }
            if (guardGO.GetComponent<HitReaction>() == null)
                guardGO.AddComponent<HitReaction>();

            // [TEST27-68차] 병사 헤드 UI — 이름 + Lv + HP바 (몬스터 MonsterHeadUI 대응)
            if (guardGO.GetComponent<GuardHeadUI>() == null)
                guardGO.AddComponent<GuardHeadUI>();

            // 시각 바디 (Collider 제거 — 물리 간섭 방지)
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = $"{goName}_Visual";
            visual.transform.SetParent(guardGO.transform, false);
            visual.transform.localPosition = new Vector3(0, 1f, 0);
            visual.transform.localScale = new Vector3(0.5f, 1f, 0.5f);

            var renderer = visual.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = color;
                renderer.material = mat;
            }
            var visCol = visual.GetComponent<Collider>();
            if (visCol != null)
                DestroyImmediate(visCol);

            // 2026-09-10/09-15: 병사 비주얼 — [TEST25-66차] FBX 우선 + GLB 재질 이식.
            // 뿌리(실측): Soldier_*.glb는 임베디드 애니메이션 0개 + 뼈대 중첩(metarig/Root/spine/spine.001/...),
            //   반면 Soldier_*.anim 클립 바인딩은 flat 단일 경로(foot.L, shin.L, shoulder.L, Root 등)라
            //   Unity 제너릭 바인딩이 GLB 중첩 경로와 불일치 → GLB 몸통은 무애니(T포즈).
            //   FBX(fbx/soldier_lv*_rigged)는 flat 계층이라 클립 경로가 일치 → 애니 재생(59차 Editor.log 실측).
            //   → 몸통은 FBX(애니 보장), GLB는 재질 소스(CopyMaterialsFromGlb)로만 사용 — 59차 검증 조합 복원.
            {
                // ① FBX 우선 — SoldierShield_AC + HumanoidClipDriver(Soldier) + GLB 재질 이식.
                string fbxKey = level >= 40
                    ? "Models/UserProvided/fbx/soldier_lv40-50_rigged"
                    : level >= 20 ? "Models/UserProvided/fbx/soldier_lv20-40_rigged"
                    : "Models/UserProvided/fbx/soldier_lv1-20_rigged";
                string matGlbKey = level >= 40
                    ? "Models/UserProvided/Soldier_Lv40-50_Rigged"
                    : level >= 20 ? "Models/UserProvided/Soldier_Lv20-40_Rigged"
                    : "Models/UserProvided/Soldier_Lv1-20_Rigged";
                var fbxPrefab = Resources.Load<GameObject>(fbxKey);
                GameObject attachedBody = null;

                if (fbxPrefab != null)
                {
                    var soldier = Instantiate(fbxPrefab, guardGO.transform);
                    soldier.name = $"{goName}_Body";
                    soldier.transform.localPosition = Vector3.zero;
                    soldier.transform.localRotation = Quaternion.identity;
                    soldier.transform.localScale = Vector3.one;
                    // [69차 후속17] 병사 크기 = 플레이어 실측 높이와 동일화 — 스케일 후 접지
                    // [후속17-2] lv20+ 덩치 병사는 일반(lv1-20) 대비 1.8배 확대 + 히트박스 동기화
                    float soldierScale = GuardManager.NormalizeSoldierScaleToPlayer(soldier);
                    soldierScale *= GuardManager.GetSoldierSizeMultiplier(level);
                    soldier.transform.localScale = Vector3.one * soldierScale;
                    ScaleGuardHitbox(guardGO, level);
                    // 접지 — FBX도 발끝을 실제 지면에 정렬(pos.y는 박스 오프셋+1.0 포함, 발이 뜸).
                    GroundModelToY(soldier, SurfaceY(pos.x, pos.z));

                    // 캡슐 시각 제거 + FBX 자식 콜라이더 제거(루트 BoxCollider만 유지)
                    DestroyImmediate(visual);
                    foreach (var c in soldier.GetComponentsInChildren<Collider>(true))
                        DestroyImmediate(c);

                    // Animator + SoldierShield_AC(방패병사 컨트롤러 — 프로덕션 동일 계열)
                    var anim = soldier.GetComponent<Animator>();
                    if (anim == null) anim = soldier.gameObject.AddComponent<Animator>();
                    var ctrl = Resources.Load<RuntimeAnimatorController>("Animation/Controllers/SoldierShield_AC");
                    if (ctrl != null) anim.runtimeAnimatorController = ctrl;
                    anim.applyRootMotion = false;
                    anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                    bool avatarOk = anim.avatar != null && anim.avatar.isValid && anim.avatar.isHuman;
                    Debug.Log($"[TestTerritoryCombat] 🧍 {goName} avatar={(anim.avatar != null ? anim.avatar.name : "NULL")}"
                        + $" isValid={(anim.avatar != null ? anim.avatar.isValid.ToString() : "-")}"
                        + $" isHuman={(anim.avatar != null ? anim.avatar.isHuman.ToString() : "-")}"
                        + $" controller={(anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "NULL")}");
                    if (!avatarOk)
                        Debug.LogWarning($"[TestTerritoryCombat] ⚠️ {goName} Humanoid avatar 무효 — Soldier_AC 재생 불가(T포즈) 가능성");

                    // 병사 모드 드라이버 — Speed=transform 델타, 공격은 GuardCombatAI→TriggerAttack
                    var driver = guardGO.AddComponent<HumanoidClipDriver>();
                    driver.mode = HumanoidClipDriver.DriveMode.Soldier;
                    HumanoidClipDriver.CopyMaterialsFromGlb(soldier, matGlbKey);

                    Debug.Log($"[TestTerritoryCombat] ✅ 병사 FBX 부착(애니 보장): {goName} ← {fbxKey} + GLB 재질 이식({matGlbKey}) (SoldierShield_AC+드라이버)");
                    attachedBody = soldier;
                }
                else
                {
                    // ② FBX 실패 폴백 → GLB 부착(애니는 클립 경로 불일치로 재생 안 될 수 있음 — 렌더 보장용).
                    string glbName = level >= 40 ? "Soldier_Lv40-50_Rigged"
                                  : level >= 20 ? "Soldier_Lv20-40_Rigged"
                                  : "Soldier_Lv1-20_Rigged";
                    var glbPrefab = Resources.Load<GameObject>($"Models/UserProvided/{glbName}");
                    if (glbPrefab == null)
                        glbPrefab = Resources.Load<GameObject>($"Models/UserProvided/{glbName}.glb");
                    if (glbPrefab != null)
                    {
                        var soldier = Instantiate(glbPrefab, guardGO.transform);
                        soldier.name = $"{goName}_Body";
                        soldier.transform.localPosition = Vector3.zero;
                        soldier.transform.localRotation = Quaternion.identity;
                        soldier.transform.localScale = Vector3.one;
                        // [69차 후속17] 병사 크기 = 플레이어 실측 높이와 동일화 — 스케일 후 접지
                        // [후속17-2] lv20+ 덩치 병사는 일반(lv1-20) 대비 1.8배 확대 + 히트박스 동기화
                        float soldierScaleGlb = GuardManager.NormalizeSoldierScaleToPlayer(soldier);
                        soldierScaleGlb *= GuardManager.GetSoldierSizeMultiplier(level);
                        soldier.transform.localScale = Vector3.one * soldierScaleGlb;
                        ScaleGuardHitbox(guardGO, level);

                        GroundModelToY(soldier, SurfaceY(pos.x, pos.z));

                        DestroyImmediate(visual);
                        foreach (var c in soldier.GetComponentsInChildren<Collider>(true))
                            DestroyImmediate(c);

                        var anim = soldier.GetComponent<Animator>();
                        if (anim == null) anim = soldier.gameObject.AddComponent<Animator>();
                        var ctrl = Resources.Load<RuntimeAnimatorController>("Animation/Controllers/SoldierShield_AC");
                        if (ctrl != null) anim.runtimeAnimatorController = ctrl;
                        anim.applyRootMotion = false;
                        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                        var driver = guardGO.AddComponent<HumanoidClipDriver>();
                        driver.mode = HumanoidClipDriver.DriveMode.Soldier;

                        Debug.LogWarning($"[TestTerritoryCombat] ⚠️ 병사 GLB 폴백 부착(FBX 실패 — 애니 바인딩 불일치 가능): {goName} ← Models/UserProvided/{glbName}.glb");
                        attachedBody = soldier;
                    }
                }

                if (attachedBody == null)
                {
                    // ③ FBX·GLB 모두 미로드 — 캡슐(플레이스홀더) 유지 경고(가급적 발생 안 함).
                    Debug.LogWarning($"[TestTerritoryCombat] ⚠️ 병사 GLB/FBX 모두 미로드 — 캡슐 유지: {goName}");

                    // [2026-09-15] 공격 배치 보장: 병사GO에 Rigidbody 중력 off(위 접지 수정) + 모델 없어도
                    // GuardCombatAI 추종 기반 동작은 유지되도록 태그/약어 기록.
                    guardGO.tag = recruited ? "RecruitedSoldier" : "Guard";
                }
            }

            // [2026-09-16] 포섭 여부 태그 — 성공 부착(GLB/FBX) 경로에서도 RecruitedSoldier/Guard 일관 부여.
            //   64차: 프로덕션(GuardManager)은 SetRecruited(true) 시 RecruitedSoldier 태그 부여로 통일했지만,
            //   이 테스트 CreateGuard는 '폐백(GLB/FBX 실패)' 분기에서만 태그를 바꿔 성공 부착 시 "Guard"로 남아
            //   내병사의 RecruitedSoldier 태그(화살 적중/선택·내병사 GLB 회피 방지)가 누락됐다. 전 케이스 일관 부여.
            guardGO.tag = recruited ? "RecruitedSoldier" : "Guard";

            return guardGO;
        }

        /// <summary>
        /// [2026-09-11] GLB/FBX 모델 bounds 기반 접지 정렬 — 전체 렌더러 bounds의 최저점을
        /// targetY(스폰 pos.y, 기존 SurfaceY 계약)에 맞춘다. 모델 피벗이 발끝과 다른 FBX/Glb에서
        /// localPosition.zero 배치만으로는 뜨거나 부유하는 문제를 피벗 오프셋과 무관하게 수리.
        /// Instantiate 직후(부착 직후) 1회 호출. 기존 색/레벨/포섭 로직 무변경.
        /// </summary>
        private static void GroundModelToY(GameObject model, float targetY)
        {
            if (model == null) return;
            var rends = model.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0) return;
            var b = rends[0].bounds;
            foreach (var r in rends)
                b.Encapsulate(r.bounds);
            float bottom = b.min.y;
            model.transform.position += Vector3.up * (targetY - bottom);
        }

        // ================================================================
        // 채집 가능 약초 3종 배치 (2026-09-10 신규 — 기존 셋업 무변경, 신규 배치만 추가)
        // ================================================================
        /// <summary>
        /// Test_10 채집 흐름 점검용 약초 3종(Red/Purple/Green).
        /// E키 채집 → HerbPickup.Harvest() → LootBasket.Create + PlayerStats EXP 3 흐름 검증.
        /// 내 영지 앞(z 18~20)에 배치 — 기존 배치(내병사 z=19, 성 z=20~30)와 이격된 위치.
        /// </summary>
        private void SetupHerbs()
        {
            SetupHerb("Herb_Red", "Models/UserProvided/herb_red", new Vector3(3f, 0f, 20f),
                HerbPickup.HerbType.Red, new Color(0.85f, 0.2f, 0.2f, 1f));
            SetupHerb("Herb_Purple", "Models/UserProvided/herb_purple", new Vector3(-3f, 0f, 20f),
                HerbPickup.HerbType.Purple, new Color(0.7f, 0.2f, 0.8f, 1f));
            SetupHerb("Herb_Green", "Models/UserProvided/herb_green", new Vector3(0f, 0f, 18f),
                HerbPickup.HerbType.Green, new Color(0.2f, 0.7f, 0.2f, 1f));
        }

        private void SetupHerb(string goName, string modelPath, Vector3 xzPos,
            HerbPickup.HerbType herbType, Color fallbackColor)
        {
            // 지표면 계약(SurfaceY = 1 + GetHeightAt) 준수: 약초는 작은 지형 오브젝트 → 표면 + 0.3
            Vector3 pos = new Vector3(xzPos.x, SurfaceY(xzPos.x, xzPos.z) + 0.3f, xzPos.z);

            // 시각 바디: GLB 프리팹 우선, 실패 시 색상 구체 폴백
            // (HerbPickup.Start가 GetComponent<Renderer>()를 사용하므로 시각 바디 필수)
            GameObject go = null;
            GameObject modelPrefab = Resources.Load<GameObject>(modelPath);
            if (modelPrefab != null)
                go = Instantiate(modelPrefab, pos, Quaternion.identity);

            if (go == null)
            {
                Debug.LogWarning($"[TestHerb] ⚠️ '{modelPath}' 로드 실패 — 프리미티브 폴백");
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.position = pos;
                go.transform.localScale = Vector3.one * 0.5f;
                var r = go.GetComponent<Renderer>();
                if (r != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.color = fallbackColor;
                    r.material = mat;
                }
            }

            go.name = goName;

            var herbPickup = go.AddComponent<HerbPickup>();

            // _herbType은 private SerializeField — 프로젝트 관례(TestPlayerSetup 등)대로 리플렉션으로 설정
            var hf = typeof(HerbPickup).GetField("_herbType",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            hf?.SetValue(herbPickup, herbType);

            // Harvest()/Respawn()이 GetComponent<Collider>().enabled를 토글하므로 Collider 보장
            if (go.GetComponent<Collider>() == null)
            {
                var col = go.AddComponent<BoxCollider>();
                col.size = new Vector3(0.5f, 0.5f, 0.5f);
            }

            Debug.Log($"[TestHerb] ✅ {goName} 배치 (type={herbType}, pos={pos})");
        }

        // ================================================================
        // 농장 배치 (2026-09-10 신규 — 기존 셋업 무변경, 신규 배치만 추가)
        // ================================================================
        /// <summary>
        /// 내 소속 영지(East_01 — SetupMyTerritory가 SetOwnership(East,1,PlayerOwned) 등록) 근처 농장 2x2 배치.
        /// FarmPlot: 빈 밭에서 [E] 파종 → TimeManager 게임 2일 경과 → Ready → 부착된 HerbPickup이
        /// 기존 E키 채집 흐름(LootBasket.Create + Random yield + EXP)을 그대로 수행.
        /// y 좌표는 이 파일의 SurfaceY(x,z)+0.1 계약 준수(밭이 흙타일로 지면에 박히게 — FarmPlot.Awake에서도 자체 보정).
        /// </summary>
        private void SetupFarm()
        {
            if (FarmingManager.Instance == null)
            {
                var managerGO = new GameObject("FarmingManager");
                managerGO.AddComponent<FarmingManager>();
            }

            // 내 영지 성(0,0,25) 전면 부지 — 약초(x ±3/0, z 18~20)와 내병사(z=19) 사이 여백에 2x2 격자
            const float farmX = 0f, farmZ = 18f;
            Vector3 center = new Vector3(farmX, SurfaceY(farmX, farmZ) + 0.1f, farmZ);

            var plots = FarmingManager.Instance.SpawnPlots(NationType.East, 1, center,
                rows: 2, cols: 2, spacing: 2.5f, HerbPickup.HerbType.Red, 2);

            Debug.Log($"[Farm] ✅ 농장 {plots.Count}칸 배치 (내 영지 East_01, center={center})");
        }

        // ================================================================
        // UI 전수 부착 + 창고/크래프트 박스 + 전 아이템 시딩 (2026-09-10 신규)
        // ================================================================
        /// <summary>Test_10 UI 전수 테스트: 미니맵/인벤/스탯/EXP바/창고·크래프트 박스/전 아이템 시딩.
        /// Systems asmdef은 UI 참조 불가(순환) → 리플렉션으로 부착(AttachUiComponent 선례).</summary>
        private void SetupUITestArena()
        {
            var uiAsm = System.Reflection.Assembly.Load("ProjectName.UI");
            if (uiAsm == null) { Debug.LogWarning("[UITest] ⚠️ ProjectName.UI 어셈블리 미발견 — UI 부착 생략"); return; }

            // ① 미니맵 부착(셀프부트 없음 — 명시 생성)
            var mmType = uiAsm.GetType("ProjectName.UI.MinimapUI");
            if (mmType != null && UnityEngine.Object.FindAnyObjectByType(mmType) == null)
            {
                var mmGO = new GameObject("MinimapUI");
                mmGO.AddComponent(mmType);
                Debug.Log("[UITest] ✅ 미니맵 부착 (상시 표시)");
            }

            // ② 인벤토리 창 (I키) + 핫키 2단 바인딩 (2026-09-12 P4: 열림 신뢰화)
            // 핫키를 창과 별도 GameObject에 선부착 — ① 창 닫힘(CloseAnimation → _windowRoot 비활성)과
            // 무관하게 I키 수신 유지(38차 '열림 0회'의 근본 원인: 자가등록 핫키가 창 GO에 동거해
            // 창 닫힘 시 함께 죽음), ② 창 Awake의 자가등록 폴백이 선점 스킵(중복 등록 방지).
            var hotType = uiAsm.GetType("ProjectName.UI.UIInventoryHotkey");
            UnityEngine.Object hotComp = null;
            if (hotType != null && UnityEngine.Object.FindAnyObjectByType(hotType) == null)
            {
                var hotGO = new GameObject("UIInventoryHotkey");
                hotComp = hotGO.AddComponent(hotType);
            }
            var invType = uiAsm.GetType("ProjectName.UI.InventoryWindow");
            if (invType != null && UnityEngine.Object.FindAnyObjectByType(invType) == null)
            {
                var invGO = new GameObject("InventoryUI");
                invGO.AddComponent(invType);
                Debug.Log("[UITest] ✅ 인벤토리 창 부착 (I키 토글)");
                // 2단(Attach→Bind) 명시 — 셋업이 핫키-창 연결을 보장 (자가등록 폴백 의존 제거)
                if (hotComp != null)
                {
                    var bind = hotType.GetMethod("Bind", new[] { invType });
                    bind?.Invoke(hotComp, new object[] { UnityEngine.Object.FindAnyObjectByType(invType) });
                    Debug.Log("[UITest] ✅ UIInventoryHotkey 부착+Bind 2단 완료 (별도 GO — 창 비활성과 무관 I키 수신)");
                }
            }

            // ③ 스탯 창(P키) — 셀프부트가 있으나 겹침 방지로 존재 확인 후 부착
            var stType = uiAsm.GetType("ProjectName.UI.StatusWindowUI");
            if (stType != null && UnityEngine.Object.FindAnyObjectByType(stType) == null)
            {
                var stGO = new GameObject("StatusWindowUI");
                stGO.AddComponent(stType);
                Debug.Log("[UITest] ✅ 스탯 창 부착 (P키 토글)");
            }

            // ④ 창고 박스 2개 + 크래프트 박스 1개 — 창고(territoryId별)에 시딩(wood 장비+무기 재료+Gold)
            // 2026-09-11: 두 박스 모두 시딩 ID("wh_test")와 일치 — 이전 "wh_test_1"/"wh_test2"는
            // 시딩 창고와 다른 빈 창고를 가리켜 박스 UI가 텅 비어 보였다. 64슬롯은 Configure로 반영.
            SetupWarehouseBox("Warehouse_1", new Vector3(6f, 0f, 14f), new Color(0.5f, 0.45f, 0.35f, 1f), "wh_test");
            SetupWarehouseBox("Warehouse_2", new Vector3(-6f, 0f, 14f), new Color(0.45f, 0.5f, 0.35f, 1f), "wh_test");
            SetupCraftBox(new Vector3(0f, 0f, 16f), new Color(0.55f, 0.4f, 0.2f, 1f));
            SeedAllItemsToWarehouse("wh_test");

            // ⑤ 플레이어 인벤토리에도 대표 아이템 시딩(인벤 창 표시 검증용)
            SeedPlayerInventory();
        }

        private void SetupWarehouseBox(string goName, Vector3 pos, Color color, string territoryId)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = goName;
            box.transform.position = new Vector3(pos.x, SurfaceY(pos.x, pos.z) + 0.55f, pos.z);
            box.transform.localScale = new Vector3(1.4f, 1.1f, 1.4f);
            var mr = box.GetComponent<Renderer>();
            if (mr != null) mr.sharedMaterial.color = color;
            // TerritoryWarehouse(ProjectName.UI) — 리플렉션 부착(asmdef 순환 회피)
            var uiAsm = System.Reflection.Assembly.Load("ProjectName.UI");
            var whType = uiAsm != null ? uiAsm.GetType("ProjectName.UI.TerritoryWarehouse") : null;
            if (whType != null)
            {
                var whComp = box.AddComponent(whType);
                // 2026-09-11: Test_09 선례와 동일 계약 Configure("wh_test_09",64,3f) → Test_10 박스도
                // 시딩 ID 일치 + 64슬롯 상향. AddComponent 직후 Awake가 20슬롯 캐시를 만들므로
                // Configure(런타임 AddComponent 후 외부 초기화용)가 재조정한다.
                var cfg = whType.GetMethod("Configure");
                cfg?.Invoke(whComp, new object[] { territoryId, 64, 3f });
            }
        }

        private void SetupCraftBox(Vector3 pos, Color color)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "CraftStation_Box";
            box.transform.position = new Vector3(pos.x, SurfaceY(pos.x, pos.z) + 0.55f, pos.z);
            box.transform.localScale = new Vector3(1.4f, 1.1f, 1.4f);
            var mr = box.GetComponent<Renderer>();
            if (mr != null) mr.sharedMaterial.color = color;
            var uiAsm = System.Reflection.Assembly.Load("ProjectName.UI");
            var csType = uiAsm != null ? uiAsm.GetType("ProjectName.UI.CraftingStation") : null;
            if (csType != null && box.GetComponent(csType) == null) box.AddComponent(csType);
            var col = box.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);   // Raycast 히트 대상에서 제외
        }

        /// <summary>WarehouseSystem 시딩(territoryId="wh_test" 단일 창고) — wood 장비 전종+무기 재료+Gold+화살 3종으로 축소.
        /// 2026-09-13(44차 P6): 화살 3종 시딩 — 활 발사 테스트용.</summary>
        private void SeedAllItemsToWarehouse(string territoryId)
        {
            // 2026-09-11: Test_10 창고 64슬롯 — TestAllInOneSetup 선례 패턴 이식.
            // ① WarehouseSystem은 자동 생성 싱글톤이 아님(Instance get; private set;) → 없으면 생성.
            //    (이전에는 Instance 부재 시 시딩 전체가 무음 스킵됐다)
            // ② _maxSlotsPerTerritory 기본 20은 시딩을 잘라 사용자 실측 증상 →
            //    private [SerializeField]라 리플렉션으로 상향(시딩 전 1회).
            //    2026-09-11: 기존 시딩 ~43슬롯 + 신규 4티어 장비 47종×2(94슬롯) ≈ 137슬롯 → 64→160 상향.
            //    2026-09-11(2): 시딩 축소(wood 장비 13종×2 + 무기 재료/Gold) — 상한 160은 여유로 유지.
            if (WarehouseSystem.Instance == null)
            {
                var wsGO = new GameObject("WarehouseSystem");
                wsGO.AddComponent<WarehouseSystem>();
                Debug.Log("[UITest] ✅ WarehouseSystem 생성");
            }
            var slotsField = typeof(WarehouseSystem).GetField("_maxSlotsPerTerritory",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            slotsField?.SetValue(WarehouseSystem.Instance, 160);

            int total = 0;
            void Add(PlayerInventory.ItemData item, int count)
            {
                if (item == null) return;
                if (WarehouseSystem.Instance != null && WarehouseSystem.Instance.AddItem(territoryId, item, count))
                    total += count;
            }

            // 2026-09-11(2): 시딩 축소 — wood 등급 장비 전종 + 무기 재료 2종 + Gold만 유지.
            //  제거: 허브·씨앗·고기·모피/가죽·도구·어류·잠입장비·물약·토큰/증서·비wood(steel/stone/crystal) 장비
            Add(PlayerInventory.BoarTusk, 5);   // 무기 재료 (멧돼지 엄니)
            Add(PlayerInventory.WolfTooth, 5);  // 무기 재료 (늑대 이빨)
            Add(PlayerInventory.Gold, 999);

            // wood 등급 GLB 장비 전종(id에 "wood" 포함) ×2 — steel/stone/crystal은 시딩 제외.
            //  유지 13종: weapon_sword/spear/bow/dagger_wood + wood_armor/helmet/boot_left/boot_right/
            //  glove_left/glove_right + wood_shield + wood_gas_mask + wood_chemical_pack
            foreach (var gear in PlayerInventory.AllTieredGear)
            {
                if (gear != null && !string.IsNullOrEmpty(gear.id) && gear.id.Contains("wood"))
                    Add(gear, 2);
            }

            // 2026-09-13(44차 P6): 화살 3종 시딩 — 활 발사 테스트용.
            //  PlayerInventory에 화살 정적 정의가 없어 ArrowManager.AddArrows(ArrowData 매핑)와
            //  동일한 id/displayName/description으로 인라인 ItemData 생성(category=ItemCategory.Arrow,
            //  maxStack=50). ArrowManager.TryConsumeArrow(RemoveItem by id) 소모 판정 호환.
            Add(new PlayerInventory.ItemData
            {
                id = "arrow_regular",
                displayName = "일반 화살",
                description = "기본 화살. 특별한 효과 없음.",
                category = PlayerInventory.ItemCategory.Arrow,
                rarity = ItemRarity.Common,
                maxStack = 50,
                maxDurability = 0
            }, 20);
            Add(new PlayerInventory.ItemData
            {
                id = "arrow_reinforced",
                displayName = "강화 화살",
                description = "철촉이 달린 강화 화살. +5 데미지.",
                category = PlayerInventory.ItemCategory.Arrow,
                rarity = ItemRarity.Uncommon,
                maxStack = 50,
                maxDurability = 0
            }, 20);
            Add(new PlayerInventory.ItemData
            {
                id = "arrow_magic",
                displayName = "마법 화살",
                description = "마력이 깃든 화살. +15 데미지.",
                category = PlayerInventory.ItemCategory.Arrow,
                rarity = ItemRarity.Rare,
                maxStack = 50,
                maxDurability = 0
            }, 20);

            Debug.Log($"[UITest] ✅ 창고 '{territoryId}'에 시딩 완료 (wood 장비+무기 재료+Gold+화살 3종, {total}개)");
        }

        /// <summary>플레이어 인벤토리 대표 시딩 — 인벤 창(I)/핫바 표시 검증용.</summary>
        private void SeedPlayerInventory()
        {
            var inv = PlayerInventory.Instance;
            if (inv == null) { Debug.LogWarning("[UITest] ⚠️ PlayerInventory 미발견 — 시딩 생략"); return; }

            inv.AddItem(PlayerInventory.Herb_Red, 5);
            inv.AddItem(PlayerInventory.RabbitMeat, 5);
            inv.AddItem(PlayerInventory.SwordWood, 1);
            inv.AddItem(PlayerInventory.LeatherArmor, 1);
            inv.AddItem(PlayerInventory.Pickaxe, 1);
            inv.AddItem(PlayerInventory.Fish_Common, 2);
            inv.AddItem(PlayerInventory.Gold, 100);
            // 2026-09-12(41차 P7): 우클릭 복용 테스트용 물약 시딩 — 실존 Potion 정의만 사용
            // (PlayerInventory.StealthPotion=potion_stealth / Sedative=potion_sedative, maxStack 10).
            // PotionUseSystem.Use 매핑 확인: stealth→은신 활성화, sedative→진정(투약 기록) 둘 다 true 반환.
            inv.AddItem(PlayerInventory.StealthPotion, 3);
            inv.AddItem(PlayerInventory.Sedative, 3);

            // [TEST23-FIX] 화살 인벤 시딩 — ArrowManager는 PlayerInventory에서 화살을 소모한다(창고 아님).
            // 창고(wh_test)에도 화살을 시딩했으나 인벤엔 없어 '화살 부족' → 발사 불가. 활 좌클릭 테스트용으로
            // ArrowManager가 소모하는 id(arrow_regular/reinforced/magic)와 동일 ItemData를 인벤에 추가.
            // PlayerInventory에 화살 정적 정의가 없으므로 인라인 ItemData 생성(창고 시딩 선례와 동일).
            inv.AddItem(new PlayerInventory.ItemData { id = "arrow_regular",    displayName = "일반 화살", description = "기본 화살. 특별한 효과 없음.", category = PlayerInventory.ItemCategory.Arrow, rarity = ItemRarity.Common,   maxStack = 50, maxDurability = 0 }, 20);
            inv.AddItem(new PlayerInventory.ItemData { id = "arrow_reinforced", displayName = "강화 화살", description = "철촉이 달린 강화 화살. +5 데미지.", category = PlayerInventory.ItemCategory.Arrow, rarity = ItemRarity.Uncommon, maxStack = 50, maxDurability = 0 }, 20);
            inv.AddItem(new PlayerInventory.ItemData { id = "arrow_magic",      displayName = "마법 화살", description = "마력이 깃든 화살. +15 데미지.", category = PlayerInventory.ItemCategory.Arrow, rarity = ItemRarity.Rare,      maxStack = 50, maxDurability = 0 }, 20);

            Debug.Log("[UITest] ✅ 플레이어 인벤 대표 아이템 시딩 완료 (물약 포함: 은신 물약×3, 진정제×3 — 우클릭 복용 테스트용)");
        }

        /// <summary>[후속17-2] 덩치 병사(level>=20)의 히트용 BoxCollider를 배율만큼 확대(윗몸이 안 맞는 버그 방지).
        ///   보통 병사(배율 1.0)는 기존 상태를 그대로 유지한다.</summary>
        private void ScaleGuardHitbox(GameObject guardGO, int level)
        {
            float mult = GuardManager.GetSoldierSizeMultiplier(level);
            if (mult == 1f) return;
            var hc = guardGO.GetComponent<BoxCollider>();
            if (hc == null) return;
            float w = 0.6f * mult, h = 1.8f * mult, d = 0.6f * mult;
            hc.size = new Vector3(w, h, d);
            hc.center = new Vector3(hc.center.x, h * 0.5f, hc.center.z); // 바닥(0) 기준으로 스팬 — 발이 지면에 닿게
        }
    }
}