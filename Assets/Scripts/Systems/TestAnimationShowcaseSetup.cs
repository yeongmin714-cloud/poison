// Test_11_AnimationShowcase (2026-09-20): 몬스터 22종 / 병사 3모델 / NPC 11종 애니메이션 쇼케이스 테스트 씬 셋업.
// 기존 패턴 재사용 (파일 직접 수정 없음 — 로직만 복사):
//  - 지형/라이트/카메라/스카이박스: TestAllInOneSetup.SetupGround/SetupLight/SetupCamera/SetupSkybox 패턴
//  - 몬스터 생성: MonsterSpawner.CreateMonster(470~546행) + CreatePrimitiveMonster(674~710행)
//    + IsBiped(599~623행) + GetSpecialCreatureType(625~639행) + GetMonsterModelPath(641~669행) 로직 재현
//    (MonsterSpawner 컴포넌트 자체는 부착하지 않음 — SpawningPaused 이슈 회피, 생성 로직만 재현)
//  - 병사 생성: TestTerritoryCombatSetup.CreateGuard(805~991행) 패턴 — 루트+FBX 바디+SoldierShield_AC+HumanoidClipDriver
//  - NPC 생성: TerritoryNPCSpawner.SpawnNPC/TryAttachSoldierHumanoidBody(93~250행) 패턴 — GLB + 병사 FBX 골격 교체
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
    ///  1) 몬스터 존(Z=-15): MonsterDatabase 22종 각 1마리 (간격 2.2m, X 중앙 정렬)
    ///  2) 병사 존(Z=0): 병사 FBX 3종(lv1-20 / lv20-40 / lv40-50) 각 1명 (간격 3m)
    ///  3) NPC 존(Z=15): NPC GLB 11종 각 1명 (간격 2.4m)
    /// 각 유닛 머리 위에 TextMesh 이름 라벨을 부착해 구분한다.
    /// </summary>
    public class TestAnimationShowcaseSetup : MonoBehaviour
    {
        [Header("Verbose")]
        [SerializeField] private bool _verbose = true;

        [Header("Layout (X spacing / Z rows)")]
        [SerializeField] private float _monsterSpacing = 2.2f;
        [SerializeField] private float _monsterRowZ = -15f;
        [SerializeField] private float _guardSpacing = 3f;
        [SerializeField] private float _guardRowZ = 0f;
        [SerializeField] private float _npcSpacing = 2.4f;
        [SerializeField] private float _npcRowZ = 15f;

        // ---- 몬스터 22종 (MonsterSpawner.GetMonsterModelPath 641행 맵 복사) ----
        private static readonly string[] MonsterIds =
        {
            "rabbit", "wolf", "boar", "deer", "poison_snake", "bat", "giant_rat", "crow",
            "slime", "stone_golem", "fire_lizard", "electric_porcupine", "swamp_croc",
            "forest_spirit", "wild_troll", "ogre", "banshee", "griffin", "minotaur",
            "manticore", "salamander", "shadow_assassin"
        };

        // ---- NPC 11종 (RuntimeModelLoader alias 89~99행 키 + 한글 표시명) ----
        private static readonly string[] NpcKeys =
        {
            "lord", "king", "shop_npc", "man1", "man2",
            "girl1", "girl2", "girl3", "oldman1", "oldman2", "dracula"
        };
        private static readonly string[] NpcNames =
        {
            "영주", "왕", "상인", "남자1", "남자2",
            "소녀1", "소녀2", "소녀3", "노인1", "노인2", "드라큘라"
        };
        // RuntimeModelLoader alias 실제키(소문자) — CopyMaterialsFromGlb GLB 재질 이식용
        private static readonly string[] NpcGlbKeys =
        {
            "npc_lord_rigged", "npc_king_rigged", "npc_shop_rigged", "npc_man1_rigged",
            "npc_man2_rigged", "npc_girl1_rigged", "npc_girl2_rigged", "npc_girl3_rigged",
            "npc_oldman1_rigged", "npc_oldman2_rigged", "npc_dracula_rigged"
        };

        private void Awake()
        {
            Debug.Log("[TestAnimShowcase] 🚀 애니메이션 쇼케이스 셋업 시작...");

            SetupGround();
            SetupLight();
            SetupSkybox();
            SetupCamera();

            SpawnMonsterShowcase();
            SpawnGuardShowcase();
            SpawnNpcShowcase();

            Debug.Log("[TestAnimShowcase] ✅ 몬스터 22종 / 병사 3모델 / NPC 11종 쇼케이스 설정 완료!");
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

            // 고정 탑다운: (0,45,-30)에서 60° 하향 — 몬스터행(-15)/병사행(0)/NPC행(+15) 전부 화면에
            camGO.transform.position = new Vector3(0f, 45f, -30f);
            camGO.transform.rotation = Quaternion.Euler(60f, 0f, 0f);

            if (camGO.GetComponent<AudioListener>() == null)
                camGO.AddComponent<AudioListener>();

            Log("[TestAnimShowcase] ✅ 고정 탑다운 카메라 설정 (0,45,-30 / 60°)");
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
        // 1) 몬스터 존 (Z=-15, 22종 각 1마리) — MonsterSpawner.CreateMonster 재현
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

            // ② ModelAnimatorAssigner (GLB 타입 자동 감지 → Biped/Quadruped/Special 분기)
            var assigner = go.GetComponent<ProjectName.Systems.Animation.ModelAnimatorAssigner>();
            if (assigner == null)
                assigner = go.AddComponent<ProjectName.Systems.Animation.ModelAnimatorAssigner>();

            // ③ AnimalAI + SetMonsterId
            var ai = go.GetComponent<AnimalAI>();
            if (ai == null) ai = go.AddComponent<AnimalAI>();
            ai.SetMonsterId(def.id);

            // ④ SpecialCreatureAnimator — non-biped/non-quadruped 전용 (Spider/Clam/Slime/Spirit)
            if (!def.isQuadruped && !IsBiped(def.id))
            {
                var special = go.GetComponent<ProjectName.Systems.Animation.Procedural.SpecialCreatureAnimator>();
                if (special == null)
                    special = go.AddComponent<ProjectName.Systems.Animation.Procedural.SpecialCreatureAnimator>();
                special.creatureType = GetSpecialCreatureType(def.id);
            }

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

            AttachLabel(guardGO, $"병사 Lv{level}", labelColor);
            return guardGO;
        }

        // ================================================================
        // 3) NPC 존 (Z=15, 11종 각 1명) — TerritoryNPCSpawner.SpawnNPC 93~250행 재현
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
                    return npcGO;
                }

                // 기존 GLB 경로 폴백 — ForceBiped 프로시저럴 (TerritoryNPCSpawner 139~146행)
                var instance = Instantiate(npcModel, npcGO.transform);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                var assigner = instance.AddComponent<ProjectName.Systems.Animation.ModelAnimatorAssigner>();
                assigner.ForceBiped(true);
                GroundModelToY(npcGO, position.y);
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
            return npcGO;
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
