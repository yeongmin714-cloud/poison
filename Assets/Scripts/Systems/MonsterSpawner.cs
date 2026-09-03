using UnityEngine;
using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems.Animation;
using ProjectName.Systems.Animation.Neural;
using ProjectName.Systems.Animation.Procedural;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// MonsterSpawner — 영지 난이도(RingDifficultyData) 기반으로 몬스터 22종을 자동 배치.
    /// 
    /// 배치 규칙 (ROADMAP 3.6):
    ///   Ring 1 (최외곽, 쉬움)     : 🟢 초반 몬스터 (Beginner)           3~4마리
    ///   Ring 2 (중간 바깥, 보통)   : 🟢 초반 + 일부 🟡 중반              4~5마리
    ///   Ring 3 (중간 안쪽, 어려움)  : 🟡 중반 몬스터 (Intermediate)       3~5마리
    ///   Ring 4 (황제국 인접, 매우어려움): 🟡 중반 + 일부 🔴 후반          4~6마리
    ///   황제국 (최종)             : 🔴 후반 최상위                       8~12마리
    ///   
    /// 각 종류당 마리수, Fixed seed로 재현 가능.
    /// 
    /// C18-02: 시간대별 스폰 (Day/Evening/Night)
    /// C18-03: 밤 리스폰 속도 증가
    /// C18-04: 밤눈 이펙트 (emissive glow)
    /// Animation: ModelAnimatorAssigner로 4족/비인간형/비행/수영 분기 자동 처리
    /// </summary>
    public class MonsterSpawner : MonoBehaviour
    {
        [System.Serializable]
        public class SpawnConfig
        {
            [Header("Spawn Ring Zones (meters from center)")]
            public float safeRadius = 200f;
            public float beginnerInner = 200f;
            public float beginnerOuter = 600f;
            public float intermediateInner = 600f;
            public float intermediateOuter = 1200f;
            public float advancedInner = 1200f;
            public float advancedOuter = 1800f;  // 맵 반경 1800m
        }

        // ===== C18-02: 시간대 열거형 =====
        public enum TimePeriod
        {
            Day,      // 6:00 ~ 18:00
            Evening,  // 18:00 ~ 20:00, 4:00 ~ 6:00
            Night     // 20:00 ~ 4:00
        }

        // ===== C18-02: 스폰 확률표 =====
        [System.Serializable]
        public class SpawnProbabilities
        {
            public float common = 0.8f;   // basic monsters
            public float elite = 0.15f;   // strong monsters
            public float boss = 0.05f;    // boss-grade

            public SpawnProbabilities() { }
            public SpawnProbabilities(float common, float elite, float boss)
            {
                this.common = common; this.elite = elite; this.boss = boss;
            }
        }

        // ===== C18-03: 총기반 밤 리스폰 =====
        [System.Serializable]
        public class RespawnThreshold
        {
            public float checkInterval = 10f;
            public int minMonstersPerTier = 3;
        }

        // ===== Serialized Fields =====
        [Header("Spawn Configuration")]
        [SerializeField] private SpawnConfig _config = new SpawnConfig();
        [SerializeField] private int _randomSeed = 42;

        [Header("Visual Prefab")]
        [SerializeField] private GameObject _monsterPrefab; // null이면 GLB 로드

        [Header("Spawned Monsters")]
        [SerializeField] private List<GameObject> _spawnedMonsters = new List<GameObject>();
        private Transform _playerT; // 플레이어 주변 스폰용 캐시

        // ===== C18-02: 시간대별 확률 =====
        [Header("Time-Aware Spawning (C18)")]
        // P-3: 모든 시간대 초보(common) 위주 유지 + 밤에도 초보 과반 (강한 종은 소량)
        [SerializeField] private SpawnProbabilities _dayProb = new SpawnProbabilities(0.75f, 0.22f, 0.03f);
        [SerializeField] private SpawnProbabilities _eveningProb = new SpawnProbabilities(0.65f, 0.30f, 0.05f);
        [SerializeField] private SpawnProbabilities _nightProb = new SpawnProbabilities(0.55f, 0.35f, 0.10f);

        // ===== C18-03: 밤 리스폰 =====
        [Header("Night Respawn (C18-03)")]
        [SerializeField] private float _nightRespawnRateMultiplier = 2.0f;
        [SerializeField] private RespawnThreshold _respawnThreshold = new RespawnThreshold();
        private float _lastRespawnCheck;

        // ===== C18-04: 밤눈 이펙트 =====
        [Header("Night Eye Effect (C18-04)")]
        [SerializeField] private bool _addNightEyeEffect = true;
        [SerializeField] private Color _nightEyeColor = new Color(1f, 0.8f, 0.2f);
        [SerializeField] private float _nightEyeIntensity = 1.5f;
        private float _lastEyeUpdate;

        /// <summary>생성된 몬스터 수</summary>
        public int TotalSpawned => _spawnedMonsters.Count;

        /// <summary>C18-02: 현재 시간대</summary>
        public TimePeriod CurrentPeriod { get; private set; }

        // 캐시된 몬스터 모델 프리팹 (Resources.Load 중복 방지)
        private static Dictionary<string, GameObject> _loadedMonsterModels = new Dictionary<string, GameObject>();

        // ===== 생명주기 =====
        private void Start()
        {
            SpawnAll();
        }

        private void OnEnable()
        {
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnTimeChanged += OnTimeChanged;
                TimeManager.Instance.OnDayNightChanged += OnDayNightChanged;
            }
        }

        private void OnDisable()
        {
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnTimeChanged -= OnTimeChanged;
                TimeManager.Instance.OnDayNightChanged -= OnDayNightChanged;
            }
        }

        private void Update()
        {
            if (TimeManager.Instance != null)
            {
                TimePeriod actual = GetTimePeriod(TimeManager.Instance.Hour);
                if (actual != CurrentPeriod)
                {
                    CurrentPeriod = actual;
                    RefreshSpawn();
                    return;
                }
            }

            if (Time.time - _lastRespawnCheck >= _respawnThreshold.checkInterval)
            {
                _lastRespawnCheck = Time.time;
                CheckAndRespawn();
            }

            if (_addNightEyeEffect && Time.time - _lastEyeUpdate >= 30f)
            {
                _lastEyeUpdate = Time.time;
                UpdateNightEyeEffect();
            }
        }

        // ===== C18-02: 시간대 계산 =====
        public TimePeriod GetTimePeriod(int hour)
        {
            if (hour >= 6 && hour < 18) return TimePeriod.Day;
            if ((hour >= 18 && hour < 20) || (hour >= 4 && hour < 6)) return TimePeriod.Evening;
            return TimePeriod.Night;
        }

        private void OnDayNightChanged(bool isDay) => RefreshSpawn();

        private void OnTimeChanged(int hour, int minute)
        {
            TimePeriod newPeriod = GetTimePeriod(hour);
            if (newPeriod != CurrentPeriod)
            {
                CurrentPeriod = newPeriod;
                RefreshSpawn();
            }
        }

        /// <summary>
        /// 현재 영지 난이도에 맞게 몬스터 재배치
        /// </summary>
        public void RefreshSpawn()
        {
            ClearAll();
            SpawnAll();
        }

        // ===== 영지 난이도 기반 스폰 (핵심 수정) =====
        public void SpawnAll()
        {
            ClearAll();
            Random.InitState(_randomSeed);

            TimeManager tm = TimeManager.Instance;
            int currentHour = tm != null ? tm.Hour : 12;
            CurrentPeriod = GetTimePeriod(currentHour);
            SpawnProbabilities prob = GetCurrentProbabilities();

            // 현재 영지 난이도 조회
            TerritoryDifficulty difficulty = GetCurrentTerritoryDifficulty();
            MonsterTier[] tiers = RingDifficultyData.GetMonsterTiersForDifficulty(difficulty);
            Vector2Int countRange = RingDifficultyData.GetMonsterCountRange(difficulty);

            Debug.Log($"[MonsterSpawner] Territory Difficulty: {difficulty}, Tiers: {string.Join(",", tiers)}, Count Range: {countRange.x}-{countRange.y}");

            // 각 티어별 스폰
            foreach (var tier in tiers)
            {
                SpawnTierByDifficulty(tier, prob, countRange, difficulty);
            }

            Debug.Log($"[MonsterSpawner] ✅ 총 {_spawnedMonsters.Count}마리 배치 완료! (초반={CountByTier(MonsterTier.Beginner)}, 중반={CountByTier(MonsterTier.Intermediate)}, 후반={CountByTier(MonsterTier.Advanced)}) [기간={CurrentPeriod}]");

            _lastEyeUpdate = Time.time;
            UpdateNightEyeEffect();
        }

        private SpawnProbabilities GetCurrentProbabilities()
        {
            return CurrentPeriod switch
            {
                TimePeriod.Evening => _eveningProb,
                TimePeriod.Night => _nightProb,
                _ => _dayProb
            };
        }

        /// <summary>
        /// 영지 난이도별 티어 스폰 (기존 SpawnTier 대체)
        /// </summary>
        private void SpawnTierByDifficulty(MonsterTier tier, SpawnProbabilities prob, Vector2Int countRange, TerritoryDifficulty difficulty)
        {
            // 시간대 필터 제거: 모든 시간대에 티어 전체 종을 풀로 사용 (시간대는 수 배수에만 반영)
            var tierPool = MonsterDatabase.GetByTier(tier);

            if (tierPool.Count == 0) return;

            float countMultiplier = (CurrentPeriod == TimePeriod.Night) ? _nightRespawnRateMultiplier : 1f;

            Vector2Int countRangeAdjusted = RingDifficultyData.GetMonsterCountRange(GetCurrentTerritoryDifficulty());
            int baseCount = Mathf.RoundToInt(Random.Range(countRangeAdjusted.x, countRangeAdjusted.y + 1) * countMultiplier);

            foreach (var def in tierPool)
            {
                float weight = GetSpawnWeight(def.tier, prob);
                int weightedCount = Mathf.Max(1, Mathf.RoundToInt(baseCount * weight));

                for (int i = 0; i < weightedCount; i++)
                {
                    Vector3 pos = RandomPositionInTerritory(def);
                    GameObject go = CreateMonster(def, pos);
                    if (go != null) _spawnedMonsters.Add(go);
                }
            }
        }

        private float GetSpawnWeight(MonsterTier tier, SpawnProbabilities prob)
        {
            return tier switch
            {
                MonsterTier.Beginner => prob.common,
                MonsterTier.Intermediate => prob.elite,
                MonsterTier.Advanced => prob.boss,
                _ => 0.5f
            };
        }

        /// <summary>
        /// 몬스터 스폰 위치: 플레이어 주변 30~70m 링 (어디를 탐험하든 조우 가능).
        /// (기존: TerritoryManager 중심=원점 기반이라 스폰 지점에서 900m 밖에 생성돼 안 보였음)
        /// y는 지형 콜라이더 raycast(와인딩 픽스로 정상 동작)로 표면에 배치.
        /// </summary>
        private Vector3 RandomPositionInTerritory(MonsterDef def)
        {
            // 플레이어 참조 캐시
            if (_playerT == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) _playerT = p.transform;
            }

            TerritoryManager tm = TerritoryManager.Instance;
            Vector3 center = _playerT != null ? _playerT.position
                           : (tm != null ? tm.GetTerritoryCenter() : transform.position);

            TerritoryDifficulty diff = GetCurrentTerritoryDifficulty();
            float radius = GetTerritoryRadius(diff);

            // 플레이어 중심 스폰: 화면 밖 30m ~ 조우 거리 70m 링
            float minR = _playerT != null ? 30f : 0f;
            float maxR = _playerT != null ? Mathf.Clamp(radius * 0.9f, 40f, 70f) : radius * 0.9f;

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float offset = Random.Range(minR, maxR);
            float x = Mathf.Cos(angle) * offset;
            float z = Mathf.Sin(angle) * offset;

            float y = 0f;
            var ray = new Ray(new Vector3(center.x + x, 100f, center.z + z), Vector3.down);
            if (Physics.Raycast(ray, out var hit, 200f, LayerMask.GetMask("Ground", "Terrain")))
            {
                y = hit.point.y;
            }

            return new Vector3(center.x + x, y, center.z + z);
        }

        private float GetTerritoryRadius(TerritoryDifficulty diff)
        {
            return diff switch
            {
                TerritoryDifficulty.Ring1 => 200f,
                TerritoryDifficulty.Ring2 => 400f,
                TerritoryDifficulty.Ring3 => 600f,
                TerritoryDifficulty.Ring4 => 800f,
                TerritoryDifficulty.Empire => 1000f,
                _ => 200f
            };
        }

        /// <summary>
        /// 현재 영지 난이도 조회 (TerritoryManager 연동)
        /// </summary>
        private TerritoryDifficulty GetCurrentTerritoryDifficulty()
        {
            TerritoryManager tm = TerritoryManager.Instance;
            if (tm != null)
            {
                var def = tm.CurrentDefinition;
                if (def.id.nation != NationType.None) return def.difficulty;
            }
            // P-4: 스포너 원점이 아닌 플레이어 위치 기준으로 판정
            if (_playerT == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) _playerT = player.transform;
            }
            return DetermineTerritoryDifficulty(_playerT != null ? _playerT.position : transform.position);
        }

        // ===== 기존 메서드들 (호환성 유지) =====
        private TerritoryDifficulty DetermineTerritoryDifficulty(Vector3 pos)
        {
            // P-4: 맵 중심(원점) 기준 거리로 Ring 판정 (멀수록 강한 링)
            float dist = Vector3.Distance(Vector3.zero, pos);
            if (dist < 600f) return TerritoryDifficulty.Ring1;
            if (dist < 1200f) return TerritoryDifficulty.Ring2;
            if (dist < 1800f) return TerritoryDifficulty.Ring3;
            return TerritoryDifficulty.Ring4;
        }

        private Vector3 RandomPositionInRing(float innerR, float outerR)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float radius = Random.Range(innerR, outerR);
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            return new Vector3(x, 0f, z);
        }

        /// <summary>
        /// 몬스터 게임오브젝트 생성 (핵심: ModelAnimatorAssigner 부착으로 애니메이션 분기)
        /// </summary>
        private GameObject CreateMonster(MonsterDef def, Vector3 position)
        {
            if (def == null)
            {
                Debug.LogError("[MonsterSpawner] CreateMonster: def가 null입니다!");
                return null;
            }

            GameObject go;

            if (_monsterPrefab != null)
            {
                go = Instantiate(_monsterPrefab, position, Quaternion.identity, transform);
            }
            else
            {
                string modelPath = GetMonsterModelPath(def.id);
                if (!string.IsNullOrEmpty(modelPath))
                {
                    GameObject modelPrefab;
                    if (!_loadedMonsterModels.TryGetValue(modelPath, out modelPrefab))
                    {
                        modelPrefab = Resources.Load<GameObject>($"Models/UserProvided/{modelPath}");
                        _loadedMonsterModels[modelPath] = modelPrefab;
                    }

                    if (modelPrefab != null)
                    {
                        go = Instantiate(modelPrefab, position, Quaternion.identity, transform);
                    }
                    else
                    {
                        go = CreatePrimitiveMonster(def, position);
                    }
                }
                else
                {
                    go = CreatePrimitiveMonster(def, position);
                }
            }

            go.name = $"Monster_{def.id}_{Random.Range(10000, 99999)}";
            go.tag = "Monster";

            // [핵심] ModelAnimatorAssigner 부착 → GLB 타입 자동 감지 → Biped/Quadruped/Special 분기
            var assigner = go.GetComponent<ProjectName.Systems.Animation.ModelAnimatorAssigner>();
            if (assigner == null) assigner = go.AddComponent<ProjectName.Systems.Animation.ModelAnimatorAssigner>();

            // AnimalAI 컴포넌트
            AnimalAI ai = go.GetComponent<AnimalAI>();
            if (ai == null) ai = go.AddComponent<AnimalAI>();
            ai.SetMonsterId(def.id);

            // [핵심] NeuralAnimationController IsQuadruped 설정 (ModelAnimatorAssigner가 처리하므로 참고용)
            NeuralAnimationController nac = go.GetComponent<NeuralAnimationController>();
            if (nac != null) nac.IsQuadruped = def.isQuadruped;

            // [핵심] MonsterSkillSystem 부착 (몬스터별 고유 스킬 패턴)
            var skillSys = go.GetComponent<MonsterSkillSystem>();
            if (skillSys == null) skillSys = go.AddComponent<MonsterSkillSystem>();

            // [핵심] SpecialCreatureAnimator for non-biped/non-quadruped (Spider, Clam, Slime 등)
            if (!def.isQuadruped && !IsBiped(def.id))
            {
                var special = go.GetComponent<SpecialCreatureAnimator>();
                if (special == null) special = go.AddComponent<SpecialCreatureAnimator>();
                special.creatureType = GetSpecialCreatureType(def.id);
            }

            // [5.3.5] 몬스터 레벨 적용
            ApplyMonsterLevel(ai);

            // C18-04: 밤눈 이펙트 적용
            if (_addNightEyeEffect && IsNightTime()) ApplyNightEyeEffect(go);

            return go;
        }

        /// <summary>
        /// [5.3.5] 몬스터 레벨 시스템 적용
        /// MonsterLevelManager를 통해 영지 난이도 기반 레벨 생성 및 적용
        /// MonsterLevelLabel 컴포넌트 추가
        /// </summary>
        private void ApplyMonsterLevel(AnimalAI ai)
        {
            if (ai == null) return;

            MonsterLevelManager lvlMgr = MonsterLevelManager.Instance;
            if (lvlMgr == null)
            {
                Debug.LogWarning("[MonsterSpawner] MonsterLevelManager 인스턴스가 없습니다. 기본 레벨 1 사용.");
                return;
            }

            // 영지 난이도 결정 (맵 중심 원점 기준 — P-4)
            TerritoryDifficulty difficulty = DetermineTerritoryDifficulty(ai.transform.position);

            // 레벨 생성 및 적용
            int level = lvlMgr.GetMonsterLevel(difficulty, ai.Tier);
            ai.SetLevel(level);

            // MonsterLevelLabel 추가 — LabelFactory 통해 생성 (Systems→UI 의존성 제거)
            if (LabelFactory.CreateLabel != null)
            {
                LabelFactory.CreateLabel(ai.gameObject, level);
            }
            else
            {
                // 이미 붙어있으면 ILevelLabel로 접근
                ILevelLabel label = ai.GetComponent<ILevelLabel>();
                if (label != null)
                    label.SetLevel(level);
            }

            Debug.Log($"[MonsterSpawner] {ai.MonsterId} Lv.{level} ({difficulty})");
        }

        /// <summary>
        /// 몬스터 ID가 2족(biped)인지 확인 (isQuadruped=false이면서 SpecialCreature가 아닌 경우)
        /// </summary>
        private bool IsBiped(string monsterId)
        {
            string[] quadrupedIds = { "wolf", "boar", "deer", "fox", "bear", "slime", "golem", "fire_lizard",
                "salamander", "swamp_croc", "snake", "hedgehog", "wild_troll", "ogre", "minotaur",
                "griffin", "banshee", "manticore", "shadow_assassin" };

            string[] specialIds = { "spider", "clam", "spirit", "deep_clam", "ice_spider" };

            if (System.Array.Exists(quadrupedIds, id => id == monsterId)) return false;
            if (System.Array.Exists(specialIds, id => id == monsterId)) return false;

            var def = MonsterDatabase.Get(monsterId);
            if (def != null && !def.isQuadruped && !System.Array.Exists(specialIds, id => id == monsterId)) return true;

            return false;
        }

        private SpecialCreatureAnimator.CreatureType GetSpecialCreatureType(string monsterId)
        {
            return monsterId switch
            {
                "spider" or "ice_spider" => SpecialCreatureAnimator.CreatureType.Spider,
                "clam" or "deep_clam" => SpecialCreatureAnimator.CreatureType.Clam,
                "slime" => SpecialCreatureAnimator.CreatureType.Slime,
                "forest_spirit" => SpecialCreatureAnimator.CreatureType.Spirit,
                "giant_clam" or "deep_clam" => SpecialCreatureAnimator.CreatureType.LargeMonster,
                _ => SpecialCreatureAnimator.CreatureType.Spider
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
                _ => ""
            };
        }

        /// <summary>
        /// 프리팹이 없을 때 Primitive 도형으로 몬스터 생성
        /// </summary>
        private GameObject CreatePrimitiveMonster(MonsterDef def, Vector3 position)
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

                if (_addNightEyeEffect && IsNightTime())
                {
                    r.material.EnableKeyword("_EMISSION");
                    r.material.SetColor("_EmissionColor", _nightEyeColor * _nightEyeIntensity);
                }
            }

            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.isKinematic = true;

            return go;
        }

        private bool IsNightTime()
        {
            if (TimeManager.Instance != null) return TimeManager.Instance.IsNight;
            return CurrentPeriod == TimePeriod.Night;
        }

        private void ApplyNightEyeEffect(GameObject monster)
        {
            if (monster == null) return;

            Transform[] children = monster.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children)
            {
                if (child.name.Contains("Eye") || child.name.Contains("eye"))
                {
                    Renderer eyeRenderer = child.GetComponent<Renderer>();
                    if (eyeRenderer != null && eyeRenderer.material != null)
                    {
                        eyeRenderer.material.EnableKeyword("_EMISSION");
                        eyeRenderer.material.SetColor("_EmissionColor", _nightEyeColor * _nightEyeIntensity);
                    }
                }
            }
        }

        private void UpdateNightEyeEffect()
        {
            if (!_addNightEyeEffect) return;
            bool isNight = IsNightTime();

            foreach (var go in _spawnedMonsters)
            {
                if (go == null) continue;

                Renderer r = go.GetComponent<Renderer>();
                if (r != null && r.material != null)
                {
                    if (isNight)
                    {
                        r.material.EnableKeyword("_EMISSION");
                        r.material.SetColor("_EmissionColor", _nightEyeColor * _nightEyeIntensity);
                    }
                    else
                    {
                        r.material.DisableKeyword("_EMISSION");
                        r.material.SetColor("_EmissionColor", Color.black);
                    }
                }

                Transform[] children = go.GetComponentsInChildren<Transform>(true);
                foreach (Transform child in children)
                {
                    if (child.name.Contains("Eye") || child.name.Contains("eye"))
                    {
                        Renderer eyeRenderer = child.GetComponent<Renderer>();
                        if (eyeRenderer != null && eyeRenderer.material != null)
                        {
                            if (isNight)
                            {
                                eyeRenderer.material.EnableKeyword("_EMISSION");
                                eyeRenderer.material.SetColor("_EmissionColor", _nightEyeColor * _nightEyeIntensity);
                            }
                            else
                            {
                                eyeRenderer.material.DisableKeyword("_EMISSION");
                                eyeRenderer.material.SetColor("_EmissionColor", Color.black);
                            }
                        }
                    }
                }
            }
        }

        public void ClearAll()
        {
            foreach (var go in _spawnedMonsters)
            {
                if (go != null) Destroy(go);
            }
            _spawnedMonsters.Clear();
        }

        private void CheckAndRespawn()
        {
            if (TimeManager.Instance == null) return;

            _spawnedMonsters.RemoveAll(go => go == null);

            bool isNight = TimeManager.Instance.IsNight;
            int minPerTier = _respawnThreshold.minMonstersPerTier;
            if (isNight) minPerTier = Mathf.RoundToInt(minPerTier * _nightRespawnRateMultiplier);

            CheckAndRespawnTier(MonsterTier.Beginner, minPerTier);
            CheckAndRespawnTier(MonsterTier.Intermediate, minPerTier);
            CheckAndRespawnTier(MonsterTier.Advanced, minPerTier);
        }

        // ===== Updated CheckAndRespawnTier for Territory Difficulty =====
        private void CheckAndRespawnTier(MonsterTier tier, int minCount)
        {
            int currentCount = CountByTier(tier);
            if (currentCount >= minCount) return;

            int deficit = minCount - currentCount;

            // 시간대 필터 제거: 모든 시간대에 티어 전체 종을 풀로 사용 (시간대는 수 배수에만 반영)
            var tierPool = MonsterDatabase.GetByTier(tier);

            if (tierPool.Count == 0) return;

            SpawnProbabilities prob = GetCurrentProbabilities();
            float weight = GetSpawnWeight(tier, prob);
            int toSpawn = Mathf.Max(1, Mathf.RoundToInt(deficit * weight));

            for (int i = 0; i < toSpawn; i++)
            {
                var def = tierPool[Random.Range(0, tierPool.Count)];
                Vector3 pos = RandomPositionInTerritory(def);
                GameObject go = CreateMonster(def, pos);
                if (go != null) _spawnedMonsters.Add(go);
            }
        }

        private int CountByTier(MonsterTier tier)
        {
            int count = 0;
            foreach (var go in _spawnedMonsters)
            {
                if (go == null) continue;
                var ai = go.GetComponent<AnimalAI>();
                if (ai != null && ai.Tier == tier) count++;
            }
            return count;
        }

        public SpawnProbabilities DayProb => _dayProb;
        public SpawnProbabilities EveningProb => _eveningProb;
        public SpawnProbabilities NightProb => _nightProb;
        public float NightRespawnRateMultiplier => _nightRespawnRateMultiplier;

        private void OnDrawGizmosSelected()
        {
            if (_config == null) return;

            Gizmos.color = new Color(0f, 0.5f, 1f, 0.05f);
            Gizmos.DrawWireSphere(transform.position, _config.safeRadius);

            Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
            DrawRing(transform.position, _config.beginnerInner, _config.beginnerOuter);

            Gizmos.color = new Color(1f, 1f, 0f, 0.15f);
            DrawRing(transform.position, _config.intermediateInner, _config.intermediateOuter);

            Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
            DrawRing(transform.position, _config.advancedInner, _config.advancedOuter);
        }

        private void DrawRing(Vector3 center, float innerR, float outerR)
        {
            DrawCircle(center, innerR);
            DrawCircle(center, outerR);
        }

        private void DrawCircle(Vector3 center, float radius)
        {
            int segments = 36;
            float angleStep = 360f / segments;
            Vector3 prev = center + new Vector3(radius, 0, 0);
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        /// <summary>
        /// 에디터에서 배치 다시 하기 (public)
        /// </summary>
        public void RespawnAll()
        {
            ClearAll();
            SpawnAll();
        }
    }
}