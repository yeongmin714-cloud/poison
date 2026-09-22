// Test_11_AnimationShowcase (2026-09-22 수리): 몬스터 6종(외형군 대표 1마리) / 병사 3모델 / NPC 1명 애니메이션 쇼케이스 셋업.
// [2026-09-22 수리 배경] 22종 전량 스폰 시 ①렉(NeuralsModels 정책 파일 부재 상태에서 몬스터마다
//   "Unity Sentis initialized" ×22 + NeuralModelDatabase 12종 경고 ×22 + HybridAnimationController 부착)
//   ②전 종 동결(GLB 동물 리그가 익명 뼈 bone_0..N — ProceduralBoneMap 이름 매칭 3뼈뿐 → 4족 다리 IK
//   대상 미매핑) ③분류 불일치(2족/특수형도 isHuman=false → 4족 분기 + 특수형 이중 부착 충돌).
// [수리 내용] ①수량: 외형 유형군(작은털4족/파충류/조류/점액정령/거대인간형/신화) 대표 1마리씩 6종,
//   병사 3명 유지, NPC 1명(영주) ②렉: ModelAnimatorAssigner.SuppressNeuralBoot로 Sentis/Neural/Hybrid
//   부트 차단(메인 씬 불변 — static 기본 false) ③분류: Force 계열 API로 3-way 강제 정합(이중 부착 제거)
//   ④보증: ShowcaseMonitor(본 무변화 감지 → 폴백 호흡 애니) + Time.timeScale=1 방어.
// 기존 패턴 재사용 (파일 직접 수정 없음 — 로직만 복사):
//  - 지형/라이트/카메라/스카이박스: TestAllInOneSetup.SetupGround/SetupLight/SetupCamera/SetupSkybox 패턴
//  - 몬스터 생성: MonsterSpawner.CreateMonster(470~546행) + CreatePrimitiveMonster(674~710행)
//    + IsBiped(599~623행) + GetSpecialCreatureType(625~639행) + GetMonsterModelPath(641~669행) 로직 재현
//    (MonsterSpawner 컴포넌트 자체는 부착하지 않음 — SpawningPaused 이슈 회피, 생성 로직만 재현)
//  - 병사 생성: TestTerritoryCombatSetup.CreateGuard(805~991행) 패턴 — 루트+FBX 바디+SoldierShield_AC+HumanoidClipDriver
//  - NPC 생성: TerritoryNPCSpawner.SpawnNPC/TryAttachSoldierHumanoidBody(93~250행) 패턴 — GLB + 병사 FBX 골격 교체
//  - 몬스터 이동: AnimalAI 대신 ShowcaseWanderDriver 부착 — Player 없이 leash 배회+애니 피드(2026-09-20)
//  - 접지: SurfaceY(수식) 대신 요청대로 Physics.Raycast로 지면 y 계산
#pragma warning disable 0414
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// Test_11_AnimationShowcase 씬 전용 셋업.
    /// 런타임 Awake에서 지형/라이트/카메라 생성 후:
    ///  1) 몬스터 존(Z=-15): 외형 유형군 대표 6종 각 1마리 (간격 3.4m, X 중앙 정렬)
    ///  2) 병사 존(Z=0): 병사 FBX 3종(lv1-20 / lv20-40 / lv40-50) 각 1명 (간격 3m)
    ///  3) NPC 존(Z=15): NPC GLB 1명(영주) — 중앙
    /// 각 유닛 머리 위에 TextMesh 이름 라벨을 부착해 구분한다.
    /// </summary>
    public class TestAnimationShowcaseSetup : MonoBehaviour
    {
        [Header("Verbose")]
        [SerializeField] private bool _verbose = true;

        [Header("Layout (X spacing / Z rows)")]
        [SerializeField] private float _monsterSpacing = 3.4f; // 6종 대형 포함 — 기존 2.2에서 확대
        [SerializeField] private float _monsterRowZ = -15f;
        [SerializeField] private float _guardSpacing = 3f;
        [SerializeField] private float _guardRowZ = 0f;
        [SerializeField] private float _npcSpacing = 2.4f;
        [SerializeField] private float _npcRowZ = 15f;

        // ---- 몬스터 6종 (외형 유형군 대표 1마리씩 — 2026-09-22 렉 완화 + 애니 확인 목적) ----
        private static readonly string[] MonsterIds =
        {
            "rabbit",       // 작은 털 4족 동물 (도약 보행 프로필)
            "swamp_croc",   // 파충류/비늘 (척추 파동 + 저속 스트라이드)
            "griffin",      // 조류/비행 (대형 포식자 스트라이드)
            "slime",        // 점액/정령 (특수형 — 스케일 펄스 자율 애니)
            "minotaur",     // 거대 인간형 (2족 보행)
            "manticore"     // 신화 하이브리드 (대형 포식자 보행)
        };

        // ---- NPC 1명 (2026-09-22 축소 — 대표 영주) ----
        private static readonly string[] NpcKeys = { "lord" };
        private static readonly string[] NpcNames = { "영주" };
        // RuntimeModelLoader alias 실제키(소문자) — CopyMaterialsFromGlb GLB 재질 이식용
        private static readonly string[] NpcGlbKeys = { "npc_lord_rigged" };

        private void Awake()
        {
            Debug.Log("[TestAnimShowcase] 🚀 애니메이션 쇼케이스 셋업 시작...");

            // [2026-09-22 뉴럴 애니 제거] Neural/Sentis 경로 자체가 퇴역 — 별도 부트 차단 불필요.
            SetupGround();
            SetupLight();
            SetupSkybox();
            SetupCamera();

            SpawnMonsterShowcase();
            SpawnGuardShowcase();
            SpawnNpcShowcase();

            // 관찰 씬 동결 방어 — 타 시스템(ESC 메뉴 등)의 Time.timeScale=0 잔존을 초기화.
            Time.timeScale = 1f;

            Debug.Log("[TestAnimShowcase] ✅ 몬스터 6종(외형군 대표) / 병사 3모델 / NPC 1명 쇼케이스 설정 완료!");
        }

        private void Log(string msg)
        {
            if (_verbose) Debug.Log(msg);
        }

        // ================================================================
        // 지형 / 라이트 / 스카이박스 / 카메라 (TestAllInOneSetup 패턴)
        // ================================================================

        private void SetupGround()
        {
            if (GameObject.Find("Ground") != null) return;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0, -0.5f, 0);
            ground.transform.localScale = Vector3.one * 100f; // 100x100

            var renderer = ground.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(0.2f, 0.5f, 0.2f, 1f); // 초록
                mat.SetFloat("_Smoothness", 0f);
                renderer.material = mat;
            }
            Log("[TestAnimShowcase] ✅ Ground 생성 (100x100, 초록)");
        }

        private void SetupLight()
        {
            if (GameObject.Find("Sun Light") == null)
            {
                var lightGO = new GameObject("Sun Light");
                var light = lightGO.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.95f, 0.8f);
                light.intensity = 1.2f;
                light.shadowStrength = 1f;
                light.transform.rotation = Quaternion.Euler(50f, 30f, 0f);
            }
            Log("[TestAnimShowcase] ✅ Sun Light 생성 (Directional)");
        }

        private void SetupSkybox()
        {
            if (RenderSettings.skybox == null)
            {
                var skyboxMat = new Material(Shader.Find("Skybox/Procedural"));
                if (skyboxMat != null && skyboxMat.shader != null)
                {
                    skyboxMat.name = "TestSkybox_AnimShowcase";
                    skyboxMat.SetColor("_SkyTint", new Color(0.4f, 0.6f, 0.9f));
                    skyboxMat.SetColor("_GroundColor", new Color(0.5f, 0.5f, 0.5f));
                    skyboxMat.SetFloat("_Exposure", 1.0f);
                    skyboxMat.SetFloat("_AtmosphereThickness", 0.8f);
                    skyboxMat.SetFloat("_SunSize", 0.04f);
                    RenderSettings.skybox = skyboxMat;
                }
            }
            Log("[TestAnimShowcase] ✅ Procedural Skybox 설정");
        }

        /// <summary>고정 탑다운 카메라 — TopDownCameraController 대신 고정 시야(3개 존 한눈에 파악).</summary>
        private void SetupCamera()
        {
            GameObject camGO = GameObject.FindGameObjectWithTag("MainCamera");
            if (camGO == null)
            {
                camGO = new GameObject("Main Camera");
                camGO.tag = "MainCamera";
            }

            Camera cam = camGO.GetComponent<Camera>();
            if (cam == null) cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 500f;

            // 고정 탑다운: (0,28,-20)에서 60° 하향 — 2026-09-22 "캐릭터들 근처로" 요청으로 근접(기존 0,45,-30)
            camGO.transform.position = new Vector3(0f, 28f, -20f);
            camGO.transform.rotation = Quaternion.Euler(60f, 0f, 0f);

            // 줌 (2026-09-22) — 자세 유지 돌리 줌(ShowcaseCameraZoom): 휠 2경로+PageUp/Down+우클릭 드래그
            if (camGO.GetComponent<ShowcaseCameraZoom>() == null)
                camGO.AddComponent<ShowcaseCameraZoom>();

            if (camGO.GetComponent<AudioListener>() == null)
                camGO.AddComponent<AudioListener>();

            Log("[TestAnimShowcase] ✅ 탑다운 카메라 설정 (0,28,-20 / 60°, 근접) + 줌 (4~45m: 휠/PageUp·Down/우클릭 드래그)");
        }

        // ================================================================
        // 공용 유틸: 지면 y (요청대로 SurfaceY 대신 raycast)
        // ================================================================

        private static float GroundY(float x, float z)
        {
            Ray ray = new Ray(new Vector3(x, 60f, z), Vector3.down);
            if (Physics.Raycast(ray, out RaycastHit hit, 200f))
                return hit.point.y;

            // 같은 Awake 프레임 내 생성 콜라이더는 물리 쿼리에 미반영 가능 — Ground 평면 origin(y) 폴백
            GameObject ground = GameObject.Find("Ground");
            if (ground != null) return ground.transform.position.y;
            return 0f;
        }

        /// <summary>모델 bounds 최저점을 targetY로 정렬 (TestTerritoryCombatSetup.GroundModelToY 999~1009행 패턴).</summary>
        private static void GroundModelToY(GameObject model, float targetY)
        {
            if (model == null) return;
            var rends = model.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0)
            {
                model.transform.position = new Vector3(model.transform.position.x, targetY, model.transform.position.z);
                return;
            }
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++)
                b.Encapsulate(rends[i].bounds);
            float bottom = b.min.y;
            model.transform.position += Vector3.up * (targetY - bottom);
        }

        /// <summary>유닛 머리 위 이름 라벨 (TextMesh legacy — TownBuilder 라벨 패턴 + bounds 상단 정렬).</summary>
        private static void AttachLabel(GameObject target, string text, Color color)
        {
            if (target == null || string.IsNullOrEmpty(text)) return;

            var label = new GameObject(target.name + "_Label");
            label.transform.SetParent(target.transform, false);

            float topY = 2.2f;
            var rends = target.GetComponentsInChildren<Renderer>();
            if (rends != null && rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++)
                    b.Encapsulate(rends[i].bounds);
                topY = b.max.y + 0.6f;
            }
            label.transform.position = new Vector3(
                target.transform.position.x, topY, target.transform.position.z);

            var tm = label.AddComponent<TextMesh>();
            tm.text = text;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 48;
            tm.characterSize = 0.2f;
            tm.color = color;
        }

        // ================================================================
        // 1) 몬스터 존 (Z=-15, 외형군 대표 6종 각 1마리) — MonsterSpawner.CreateMonster 재현
        // ================================================================

        private void SpawnMonsterShowcase()
        {
            int count = MonsterIds.Length;
            float startX = -(count - 1) * _monsterSpacing * 0.5f;

            for (int i = 0; i < count; i++)
            {
                MonsterDef def = MonsterDatabase.Get(MonsterIds[i]);
                if (def == null)
                {
                    Debug.LogWarning($"[TestAnimShowcase] ⚠️ MonsterDef '{MonsterIds[i]}' 없음 — 스킵");
                    continue;
                }

                float x = startX + i * _monsterSpacing;
                Vector3 pos = new Vector3(x, 0f, _monsterRowZ);
                pos.y = GroundY(x, _monsterRowZ);

                GameObject go = CreateShowcaseMonster(def, pos);
                if (go == null) continue;

                AttachLabel(go, $"몬스터: {def.displayName} / {def.id}", Color.white);
                Log($"[TestAnimShowcase] 🐾 몬스터 {i + 1}/{count}: {def.displayName} ({def.id}) at {pos}");
            }
        }

        private GameObject CreateShowcaseMonster(MonsterDef def, Vector3 position)
        {
            if (def == null) return null;

            GameObject go = null;

            // ① 모델 프리팹 로드 (MonsterSpawner.CreateMonster 486~508행)
            string modelPath = GetMonsterModelPath(def.id);
            if (!string.IsNullOrEmpty(modelPath))
            {
                var modelPrefab = Resources.Load<GameObject>("Models/UserProvided/" + modelPath);
                if (modelPrefab != null)
                    go = Instantiate(modelPrefab, position, Quaternion.identity, transform);
            }

            // 프리미티브 폴백 (CreatePrimitiveMonster 674~710행 — 티어별 Sphere/Capsule/Cube)
            if (go == null)
                go = CreatePrimitiveShowcaseMonster(def, position);

            go.name = "Monster_" + def.id;
            go.tag = "Monster";

            // ② ModelAnimatorAssigner — 3-way 분류 강제 정합(2026-09-22 수리).
            //    자동감지는 익명 리그 isHuman=false → 전원 4족 분기라 2족/특수형과 충돌했다.
            //    Force 계열(RemoveAll+Setup 재실행)로 몬스터 계열당 정확히 하나의 애니 패밀리를 보장한다.
            var assigner = go.GetComponent<ProjectName.Systems.Animation.ModelAnimatorAssigner>();
            if (assigner == null)
                assigner = go.AddComponent<ProjectName.Systems.Animation.ModelAnimatorAssigner>();

            ShowcaseMonitor.MonitorFamily family;
            if (def.isQuadruped)
            {
                assigner.ForceQuadruped(true);   // QuadrupedProceduralLocomotion(+Animation) — 다리 IK 보행
                family = ShowcaseMonitor.MonitorFamily.Quadruped;
            }
            else if (IsBiped(def.id))
            {
                assigner.ForceBiped(true);       // ProceduralAnimationController — 2족 보행(IVelocityProvider 경로)
                family = ShowcaseMonitor.MonitorFamily.Biped;
            }
            else
            {
                var specialType = GetSpecialCreatureType(def.id);
                assigner.ForceSpecialCreature(specialType);
                // ForceSpecialCreature는 RemoveAll 후 신규 부착이라 creatureType를 부착 후 재지정해야 한다.
                var specialCtrl = assigner.SpecialCreatureController;
                if (specialCtrl != null) specialCtrl.creatureType = specialType;
                family = ShowcaseMonitor.MonitorFamily.Special;
            }

            // ③ ShowcaseWanderDriver + SetMonsterId — Player/AnimalAI 없이 자율 배회(걷기/대기 애니 독립 재생).
            //    AnimalAI는 Player 부재 시 Update에서 Idle+속도 0으로 되돌려 전 종 얼어붙는다
            //    (AnimalAI.cs 503~514행). 애니 피드는 드라이버가 ModelAnimatorAssigner가 부착한
            //    컨트롤러(4족 QuadrupedProceduralAnimation / 2족 ProceduralAnimationController)에 수행한다.
            var wander = go.GetComponent<ShowcaseWanderDriver>();
            if (wander == null) wander = go.AddComponent<ShowcaseWanderDriver>();
            wander.SetMonsterId(def.id);

            // ④ ShowcaseMonitor — 본 매핑 성패와 무관하게 움직임을 보증(무변화 감지 → 폴백 호흡).
            var monitor = go.GetComponent<ShowcaseMonitor>();
            if (monitor == null) monitor = go.AddComponent<ShowcaseMonitor>();
            monitor.Setup($"{def.displayName} ({def.id})", family);

            // ⑤ 접지 확정 — 물리 경합 제거(2026-09-22 접지 수리).
            //    ModelAnimatorAssigner가 루트에 비키네마틱 Rigidbody를 자동 부착해, 지면 정렬 후에도
            //    중력+콜라이더 잔류 높이가 bounds 정렬을 이기고 뜨거나 파묻히는 문제(사용자 보고).
            //    쇼케이스는 충돌/레이캐스트 요구가 없으므로: 루트 rb 키네마틱 고정(중력 off) + 자식 콜라이더 전부 제거
            //    → ShowcaseWanderDriver의 kinematic 경로(transform.position, y=_groundY 고정)가 정확히 접지 유지.
            var rootRb = go.GetComponent<Rigidbody>();
            if (rootRb != null)
            {
                rootRb.isKinematic = true;
                rootRb.useGravity = false;
            }
            var modelColliders = go.GetComponentsInChildren<Collider>(true);
            for (int c = 0; c < modelColliders.Length; c++)
                if (modelColliders[c] != null) Destroy(modelColliders[c]);

            // 접지 — 모델 bounds 최저점을 raycast 지면에 정렬
            GroundModelToY(go, position.y);

            return go;
        }

        private GameObject CreatePrimitiveShowcaseMonster(MonsterDef def, Vector3 position)
        {
            PrimitiveType primitive = def.tier switch
            {
                MonsterTier.Beginner => PrimitiveType.Sphere,
                MonsterTier.Intermediate => PrimitiveType.Capsule,
                MonsterTier.Advanced => PrimitiveType.Cube,
                _ => PrimitiveType.Sphere
            };

            GameObject go = GameObject.CreatePrimitive(primitive);
            go.transform.position = position;
            go.transform.SetParent(transform);

            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Standard")
                    ?? Shader.Find("Diffuse");
                r.material = new Material(shader);
                r.material.color = def.gizmoColor;
            }

            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.isKinematic = true;

            return go;
        }

        /// <summary>2족(사람형) 판정 — MonsterSpawner.IsBiped 599~623행 복사.</summary>
        private static bool IsBiped(string monsterId)
        {
            string[] quadrupedIds = { "wolf", "boar", "deer", "fox", "bear", "slime", "golem", "fire_lizard",
                "salamander", "swamp_croc", "snake", "hedgehog", "griffin", "manticore" };
            string[] specialIds = { "spider", "clam", "spirit", "forest_spirit", "deep_clam", "ice_spider", "bat", "crow", "poison_snake" };

            if (System.Array.Exists(quadrupedIds, id => id == monsterId)) return false;
            if (System.Array.Exists(specialIds, id => id == monsterId)) return false;

            var def = MonsterDatabase.Get(monsterId);
            if (def != null && !def.isQuadruped && !System.Array.Exists(specialIds, id => id == monsterId)) return true;

            return false;
        }

        /// <summary>특수형 크리처 타입 — MonsterSpawner.GetSpecialCreatureType 625~639행 복사.</summary>
        private static ProjectName.Systems.Animation.Procedural.SpecialCreatureAnimator.CreatureType GetSpecialCreatureType(string monsterId)
        {
            return monsterId switch
            {
                "spider" or "ice_spider" => ProjectName.Systems.Animation.Procedural.SpecialCreatureAnimator.CreatureType.Spider,
                "bat" or "crow" or "poison_snake" => ProjectName.Systems.Animation.Procedural.SpecialCreatureAnimator.CreatureType.Spider,
                "clam" or "deep_clam" => ProjectName.Systems.Animation.Procedural.SpecialCreatureAnimator.CreatureType.Clam,
                "slime" => ProjectName.Systems.Animation.Procedural.SpecialCreatureAnimator.CreatureType.Slime,
                "forest_spirit" => ProjectName.Systems.Animation.Procedural.SpecialCreatureAnimator.CreatureType.Spirit,
                "giant_clam" or "deep_clam" => ProjectName.Systems.Animation.Procedural.SpecialCreatureAnimator.CreatureType.LargeMonster,
                _ => ProjectName.Systems.Animation.Procedural.SpecialCreatureAnimator.CreatureType.Spider
            };
        }

        /// <summary>모델 경로 맵 — MonsterSpawner.GetMonsterModelPath 641~669행 복사.</summary>
        private static string GetMonsterModelPath(string monsterId)
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
                _ => ""
            };
        }

        // ================================================================
        // 2) 병사 존 (Z=0, 3모델 각 1명) — TestTerritoryCombatSetup.CreateGuard 805~991행 재현
        // ================================================================

        private void SpawnGuardShowcase()
        {
            // 레벨별 3종: lv1-20 / lv20-40 / lv40-50, 라벨 색으로 구분
            SpawnShowcaseGuard("Guard_Lv1-20", 1, new Vector3(-_guardSpacing, 0f, _guardRowZ), new Color(0.4f, 0.9f, 1f));
            SpawnShowcaseGuard("Guard_Lv20-40", 25, new Vector3(0f, 0f, _guardRowZ), new Color(1f, 0.75f, 0.2f));
            SpawnShowcaseGuard("Guard_Lv40-50", 45, new Vector3(_guardSpacing, 0f, _guardRowZ), new Color(1f, 0.35f, 0.25f));
        }

        private GameObject SpawnShowcaseGuard(string goName, int level, Vector3 pos, Color labelColor)
        {
            string guardName = $"병사 Lv{level}";
            // 접지 — SurfaceY(수식) 대신 raycast 지면 y
            pos.y = GroundY(pos.x, pos.z);

            GameObject guardGO = new GameObject(goName);
            guardGO.transform.position = pos;
            guardGO.tag = "Guard";

            var guard = guardGO.AddComponent<GuardPlaceholder>();
            guard.SetGuardInfo(guardName, level, NationType.East);
            guard.SetRecruited(false);

            // Raycast 히트용 Collider (루트 기준 위쪽 +0.9 볼륨)
            if (guardGO.GetComponent<Collider>() == null)
            {
                var col = guardGO.AddComponent<BoxCollider>();
                col.size = new Vector3(0.6f, 1.8f, 0.6f);
                col.center = new Vector3(0f, 0.9f, 0f);
            }
            if (guardGO.GetComponent<Rigidbody>() == null)
            {
                var rb = guardGO.AddComponent<Rigidbody>();
                rb.useGravity = false;   // 중력 off — 접지 안정화
                rb.isKinematic = true;   // kinematic 고정
                rb.mass = 1f;
            }
            if (guardGO.GetComponent<HitReaction>() == null)
                guardGO.AddComponent<HitReaction>();

            // 헤드 UI (이름+Lv+HP바 — 병사 표준 구성)
            if (guardGO.GetComponent<GuardHeadUI>() == null)
                guardGO.AddComponent<GuardHeadUI>();

            // 캡슐 플레이스홀더 (FBX/GLB 부착 성공 시 제거)
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = goName + "_Visual";
            visual.transform.SetParent(guardGO.transform, false);
            visual.transform.localPosition = new Vector3(0f, 1f, 0f);
            visual.transform.localScale = new Vector3(0.5f, 1f, 0.5f);
            var visRenderer = visual.GetComponent<MeshRenderer>();
            if (visRenderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = labelColor;
                visRenderer.material = mat;
            }
            var visCol = visual.GetComponent<Collider>();
            if (visCol != null) DestroyImmediate(visCol);

            // ① FBX 우선 (레벨별 모델) — 애니 보장 경로
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
                soldier.name = goName + "_Body";
                soldier.transform.localPosition = Vector3.zero;
                soldier.transform.localRotation = Quaternion.identity;
                float soldierScale = GuardManager.NormalizeSoldierScaleToPlayer(soldier);
                soldierScale *= GuardManager.GetSoldierSizeMultiplier(level);
                soldier.transform.localScale = Vector3.one * soldierScale;

                // 접지 — FBX bounds 최저점을 지면에
                GroundModelToY(soldier, pos.y);

                // 캡슐 시각 제거 + FBX 자식 콜라이더 제거(루트 BoxCollider만 유지) — 원본 패턴 DestroyImmediate
                DestroyImmediate(visual);
                var childColliders = soldier.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < childColliders.Length; i++)
                    DestroyImmediate(childColliders[i]);

                // Animator + SoldierShield_AC
                var anim = soldier.GetComponent<Animator>();
                if (anim == null) anim = soldier.gameObject.AddComponent<Animator>();
                var ctrl = Resources.Load<RuntimeAnimatorController>("Animation/Controllers/SoldierShield_AC");
                if (ctrl != null) anim.runtimeAnimatorController = ctrl;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                var driver = guardGO.AddComponent<HumanoidClipDriver>();
                driver.mode = HumanoidClipDriver.DriveMode.Soldier;
                HumanoidClipDriver.CopyMaterialsFromGlb(soldier, matGlbKey);

                Log($"[TestAnimShowcase] ✅ 병사 FBX 부착: {goName} ← {fbxKey}");
                attachedBody = soldier;
            }
            else
            {
                // ② FBX 실패 폴백 → GLB (렌더 보장)
                string glbName = level >= 40 ? "Soldier_Lv40-50_Rigged"
                              : level >= 20 ? "Soldier_Lv20-40_Rigged"
                              : "Soldier_Lv1-20_Rigged";
                var glbPrefab = Resources.Load<GameObject>("Models/UserProvided/" + glbName);
                if (glbPrefab != null)
                {
                    var soldier = Instantiate(glbPrefab, guardGO.transform);
                    soldier.name = goName + "_Body";
                    soldier.transform.localPosition = Vector3.zero;
                    soldier.transform.localRotation = Quaternion.identity;
                    float soldierScaleGlb = GuardManager.NormalizeSoldierScaleToPlayer(soldier);
                    soldierScaleGlb *= GuardManager.GetSoldierSizeMultiplier(level);
                    soldier.transform.localScale = Vector3.one * soldierScaleGlb;

                    GroundModelToY(soldier, pos.y);

                    DestroyImmediate(visual);
                    var childColliders = soldier.GetComponentsInChildren<Collider>(true);
                    for (int i = 0; i < childColliders.Length; i++)
                        DestroyImmediate(childColliders[i]);

                    var anim = soldier.GetComponent<Animator>();
                    if (anim == null) anim = soldier.gameObject.AddComponent<Animator>();
                    var ctrl = Resources.Load<RuntimeAnimatorController>("Animation/Controllers/SoldierShield_AC");
                    if (ctrl != null) anim.runtimeAnimatorController = ctrl;
                    anim.applyRootMotion = false;
                    anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                    var driver = guardGO.AddComponent<HumanoidClipDriver>();
                    driver.mode = HumanoidClipDriver.DriveMode.Soldier;

                    Debug.LogWarning($"[TestAnimShowcase] ⚠️ 병사 GLB 폴백 부착: {goName} ← {glbName}");
                    attachedBody = soldier;
                }
            }

            if (attachedBody == null)
                Debug.LogWarning($"[TestAnimShowcase] ⚠️ 병사 GLB/FBX 모두 미로드 — 캡슐 유지: {goName}");

            // [2026-09-22] 병사 배회 — HumanoidClipDriver(Soldier 모드)가 위치 델타로 Speed를 계산하므로
            // 이동시키기만 하면 걷기/대기 클립이 자동 전환된다(플레이어와 동일 클립 경로).
            var guardWander = guardGO.GetComponent<ShowcaseWanderDriver>();
            if (guardWander == null) guardWander = guardGO.AddComponent<ShowcaseWanderDriver>();
            guardWander.SetClipDriven(true);

            // 가시성 관측자 — 휴머노이드 클립 경로는 폴백 미적용(관측+경고만)
            var monitor = guardGO.GetComponent<ShowcaseMonitor>();
            if (monitor == null) monitor = guardGO.AddComponent<ShowcaseMonitor>();
            monitor.Setup(guardName, ShowcaseMonitor.MonitorFamily.HumanoidClip);

            AttachLabel(guardGO, $"병사 Lv{level}", labelColor);
            return guardGO;
        }

        // ================================================================
        // 3) NPC 존 (Z=15, 1명 — 영주) — TerritoryNPCSpawner.SpawnNPC 93~250행 재현
        // ================================================================

        private void SpawnNpcShowcase()
        {
            int count = NpcKeys.Length;
            float startX = -(count - 1) * _npcSpacing * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float x = startX + i * _npcSpacing;
                Vector3 pos = new Vector3(x, 0f, _npcRowZ);
                pos.y = GroundY(x, _npcRowZ);

                GameObject npcGO = CreateShowcaseNpc(NpcKeys[i], NpcGlbKeys[i], NpcNames[i], pos);
                if (npcGO == null) continue;

                AttachLabel(npcGO, $"NPC: {NpcNames[i]}", Color.white);
                Log($"[TestAnimShowcase] 🧍 NPC {i + 1}/{count}: {NpcNames[i]} ({NpcKeys[i]}) at {pos}");
            }
        }

        private GameObject CreateShowcaseNpc(string aliasKey, string glbKey, string npcName, Vector3 position)
        {
            GameObject npcGO = new GameObject("NPC_" + aliasKey);
            npcGO.transform.position = position;

            // GLB 모델 로드 (RuntimeModelLoader alias — 소문자 키)
            if (RuntimeModelLoader.TryGetModel(aliasKey, out var npcModel) && npcModel != null)
            {
                bool fbxOk = false;
                try
                {
                    fbxOk = TryAttachSoldierHumanoidBody(npcGO, npcName, glbKey);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[TestAnimShowcase] ⚠️ NPC '{npcName}' FBX 교체 예외 — GLB 폴백: {ex.Message}");
                    fbxOk = false;
                }

                if (fbxOk)
                {
                    // FBX 골격 교체 성공 — 접지만 보정
                    GroundModelToY(npcGO, position.y);
                    AttachShowcaseNpcMonitor(npcGO, npcName);
                    return npcGO;
                }

                // 기존 GLB 경로 폴백 — ForceBiped 프로시저럴 (TerritoryNPCSpawner 139~146행)
                var instance = Instantiate(npcModel, npcGO.transform);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                var assigner = instance.AddComponent<ProjectName.Systems.Animation.ModelAnimatorAssigner>();
                assigner.ForceBiped(true);
                GroundModelToY(npcGO, position.y);
                AttachShowcaseNpcMonitor(npcGO, npcName);
                return npcGO;
            }

            // GLB 미로드 — 프리미티브 폴백 (TerritoryNPCSpawner 149~170행 단순화)
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(npcGO.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            var bodyRend = body.GetComponent<MeshRenderer>();
            if (bodyRend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(0.85f, 0.75f, 0.6f);
                bodyRend.material = mat;
            }
            var bodyCol = body.GetComponent<Collider>();
            if (bodyCol != null) Destroy(bodyCol);

            Debug.LogWarning($"[TestAnimShowcase] ⚠️ NPC GLB 미로드 — 프리미티브 폴백: {npcName} ({aliasKey})");
            AttachShowcaseNpcMonitor(npcGO, npcName);
            return npcGO;
        }

        /// <summary>NPC 가시성 관측자 + 배회 부착 — HumanoidClip 계열(폴백 미적용, 관측+경고만).</summary>
        private static void AttachShowcaseNpcMonitor(GameObject npcGO, string npcName)
        {
            // [2026-09-22] NPC 배회 — 병사와 동일: 이동 → HumanoidClipDriver Speed → 걷기/대기 클립.
            var wander = npcGO.GetComponent<ShowcaseWanderDriver>();
            if (wander == null) wander = npcGO.AddComponent<ShowcaseWanderDriver>();
            wander.SetClipDriven(true);

            var monitor = npcGO.GetComponent<ShowcaseMonitor>();
            if (monitor == null) monitor = npcGO.AddComponent<ShowcaseMonitor>();
            monitor.Setup($"NPC: {npcName}", ShowcaseMonitor.MonitorFamily.HumanoidClip);
        }

        /// <summary>
        /// NPC 본체를 병사 Humanoid FBX 골격으로 교체 — TerritoryNPCSpawner.TryAttachSoldierHumanoidBody 195~250행 패턴 복사.
        /// FBX(soldier_lv1-20) + SoldierShield_AC + HumanoidClipDriver(Soldier) + NPC GLB 재질 이식.
        /// 성공 true / 실패 null 반환(호출부 GLB 폴백).
        /// </summary>
        private static bool TryAttachSoldierHumanoidBody(GameObject npcGO, string npcName, string glbAliasKey)
        {
            var fbxPrefab = Resources.Load<GameObject>("Models/UserProvided/fbx/soldier_lv1-20_rigged");
            if (fbxPrefab == null)
            {
                Debug.LogWarning("[TestAnimShowcase] 병사 Humanoid FBX 미로드 — GLB 폴백 대상");
                return false;
            }

            var fbxBody = Instantiate(fbxPrefab, npcGO.transform);
            fbxBody.name = npcName + "_Body";
            fbxBody.transform.localPosition = Vector3.zero;
            fbxBody.transform.localRotation = Quaternion.identity;
            fbxBody.transform.localScale = Vector3.one;

            // FBX 하위 Collider 전부 제거
            var cols = fbxBody.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null) DestroyImmediate(cols[i]);
            }

            // Animator + SoldierShield_AC
            var anim = fbxBody.GetComponent<Animator>();
            if (anim == null) anim = fbxBody.AddComponent<Animator>();
            var ctrl = Resources.Load<RuntimeAnimatorController>("Animation/Controllers/SoldierShield_AC");
            if (ctrl != null) anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // 병사 모드 드라이버 (NPC 루트에 부착 — 걷기/대기)
            var driver = npcGO.AddComponent<HumanoidClipDriver>();
            driver.mode = HumanoidClipDriver.DriveMode.Soldier;

            // NPC GLB 재질 이식(외형 유지)
            HumanoidClipDriver.CopyMaterialsFromGlb(fbxBody, "Models/UserProvided/" + glbAliasKey);

            LogStatic($"[TestAnimShowcase] ✅ NPC Humanoid FBX 부착: {npcName} (SoldierShield_AC+드라이버, 재질={glbAliasKey})");
            return true;
        }

        private static void LogStatic(string msg)
        {
            Debug.Log(msg);
        }
    }
}
