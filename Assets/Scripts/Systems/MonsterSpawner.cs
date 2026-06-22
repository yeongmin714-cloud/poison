using UnityEngine;
using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// MonsterSpawner — TopDownScene에서 몬스터 24종을 거리 기반으로 자동 배치.
    /// 
    /// 배치 규칙:
    ///   0~200m  : 안전 지대 (스폰 위치, 몬스터 없음)
    ///   200~600m: 🟢 초반 몬스터 (Beginner)
    ///   600~1200m: 🟡 중반 몬스터 (Intermediate)
    ///   1200~1000m: 🔴 후반 몬스터 (Advanced)
    ///   
    /// 각 종류당 3~5마리씩, 총 약 72~120마리 배치.
    /// Fixed seed로 재현 가능.
    ///
    /// C18-02: 시간대별 스폰 (Day/Evening/Night)
    /// C18-03: 밤 리스폰 속도 증가
    /// C18-04: 밤눈 이펙트 (emissive glow)
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
            public float advancedOuter = 1000f;  // 맵 반경 1000m
        }

        // ===== C18-02: 시간대 열거형 =====

        /// <summary>
        /// C18-02: 하루를 세 가지 시간대(주간/황혼/야간)로 구분
        /// </summary>
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
                this.common = common;
                this.elite = elite;
                this.boss = boss;
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
        [SerializeField] private int _monstersPerType = 4; // 각 종류당 마리수

        [Header("Visual Prefab")]
        [SerializeField] private GameObject _monsterPrefab; // null이면 primitive 생성

        [Header("Spawned Monsters")]
        [SerializeField] private List<GameObject> _spawnedMonsters = new List<GameObject>();

        // ===== C18-02: 시간대별 확률 =====
        [Header("Time-Aware Spawning (C18)")]
        [SerializeField] private SpawnProbabilities _dayProb = new SpawnProbabilities(0.80f, 0.15f, 0.05f);
        [SerializeField] private SpawnProbabilities _eveningProb = new SpawnProbabilities(0.50f, 0.40f, 0.10f);
        [SerializeField] private SpawnProbabilities _nightProb = new SpawnProbabilities(0.20f, 0.60f, 0.20f);

        // ===== C18-03: 밤 리스폰 =====
        [Header("Night Respawn (C18-03)")]
        [SerializeField] private float _nightRespawnRateMultiplier = 1.5f;
        [SerializeField] private RespawnThreshold _respawnThreshold = new RespawnThreshold();
        private float _lastRespawnCheck;

        // ===== C18-04: 밤눈 이펙트 =====
        [Header("Night Eye Effect (C18-04)")]
        [SerializeField] private bool _addNightEyeEffect = true;
        [SerializeField] private Color _nightEyeColor = new Color(1f, 0.8f, 0.2f); // 황금빛
        [SerializeField] private float _nightEyeIntensity = 1.5f;
        private float _lastEyeUpdate;

        /// <summary>생성된 몬스터 수</summary>
        public int TotalSpawned => _spawnedMonsters.Count;

        /// <summary>C18-02: 현재 시간대</summary>
        public TimePeriod CurrentPeriod { get; private set; }

        // ===== 생명주기 =====

        private void Start()
        {
            SpawnAll();
        }

        private void OnEnable()
        {
            // C18-02: 시간 변경 구독
            if (TimeManager.Instance != null)
                TimeManager.Instance.OnDayNightChanged += OnDayNightChanged;
        }

        private void OnDisable()
        {
            // C18-02: 구독 해제
            if (TimeManager.Instance != null)
                TimeManager.Instance.OnDayNightChanged -= OnDayNightChanged;
        }

        private void Update()
        {
            // C18-03: 주기적 리스폰 체크
            if (Time.time - _lastRespawnCheck >= _respawnThreshold.checkInterval)
            {
                _lastRespawnCheck = Time.time;
                CheckAndRespawn();
            }

            // C18-04: 주기적 밤눈 이펙트 업데이트 (30초)
            if (_addNightEyeEffect && Time.time - _lastEyeUpdate >= 30f)
            {
                _lastEyeUpdate = Time.time;
                UpdateNightEyeEffect();
            }
        }

        // ===== C18-02: 시간대 계산 =====

        /// <summary>
        /// C18-02: TimeManager의 Hour를 기반으로 현재 시간대 결정
        /// Evening = 18-20, 4-6 | Day = 6-18 | Night = 20-4
        /// </summary>
        public TimePeriod GetTimePeriod(int hour)
        {
            if (hour >= 6 && hour < 18)
                return TimePeriod.Day;
            if ((hour >= 18 && hour < 20) || (hour >= 4 && hour < 6))
                return TimePeriod.Evening;
            return TimePeriod.Night; // 20-4
        }

        // ===== C18-02: 시간 변경 콜백 =====

        private void OnDayNightChanged(bool isDay)
        {
            RefreshSpawn();
        }

        /// <summary>
        /// C18-02: 현재 시간대에 맞게 몬스터 재배치
        /// </summary>
        public void RefreshSpawn()
        {
            ClearAll();
            SpawnAll();
        }

        // ===== C18-02: 시간대별 스폰 =====

        /// <summary>
        /// 모든 몬스터를 생성한다. 에디터에서 호출 가능 (public)
        /// C18-02: 시간대별 확률 기반 스폰
        /// </summary>
        public void SpawnAll()
        {
            // 기존 몬스터 제거
            ClearAll();

            // 시드 고정
            Random.InitState(_randomSeed);

            // C18-02: 현재 시간대 확인
            TimeManager tm = TimeManager.Instance;
            int currentHour = tm != null ? tm.Hour : 12; // 기본값 정오
            CurrentPeriod = GetTimePeriod(currentHour);

            // C18-02: 시간대별 확률표 선택
            SpawnProbabilities prob = GetCurrentProbabilities();

            // 각 티어별 몬스터 배치 (시간대 고려)
            SpawnTier(MonsterTier.Beginner, _config.beginnerInner, _config.beginnerOuter, prob);
            SpawnTier(MonsterTier.Intermediate, _config.intermediateInner, _config.intermediateOuter, prob);
            SpawnTier(MonsterTier.Advanced, _config.advancedInner, _config.advancedOuter, prob);

            Debug.Log($"[MonsterSpawner] ✅ 총 {_spawnedMonsters.Count}마리 배치 완료! " +
                $"(초반={CountByTier(MonsterTier.Beginner)}, " +
                $"중반={CountByTier(MonsterTier.Intermediate)}, " +
                $"후반={CountByTier(MonsterTier.Advanced)}) " +
                $"[기간={CurrentPeriod}]");

            // C18-04: 최초 밤눈 이펙트 적용
            _lastEyeUpdate = Time.time;
            UpdateNightEyeEffect();
        }

        /// <summary>
        /// C18-02: 현재 시간대에 맞는 확률표 반환
        /// </summary>
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
        /// C18-02: 특정 티어 몬스터를 시간대별 ActiveTime 필터링 + 확률 기반 배치
        /// </summary>
        private void SpawnTier(MonsterTier tier, float innerRadius, float outerRadius, SpawnProbabilities prob)
        {
            // C18-02: 현재 시간대에 맞는 ActiveTime 계산
            ActiveTime filterTime = CurrentPeriod == TimePeriod.Night ? ActiveTime.Night : ActiveTime.Day;

            // C18-02: GetByActiveTime 필터
            var activePool = MonsterDatabase.GetByActiveTime(filterTime);
            var tierPool = new List<MonsterDef>();

            foreach (var def in activePool)
            {
                if (def.tier == tier)
                    tierPool.Add(def);
            }

            if (tierPool.Count == 0) return;

            // C18-03: 밤이면 _nightRespawnRateMultiplier 적용
            float countMultiplier = (CurrentPeriod == TimePeriod.Night && TimeManager.Instance != null && TimeManager.Instance.IsNight)
                ? _nightRespawnRateMultiplier
                : 1f;

            // 확률 가중치 기반 배치
            foreach (var def in tierPool)
            {
                // 기본 마리수
                int baseCount = Mathf.RoundToInt(_monstersPerType * countMultiplier);

                // 확률 가중치 적용
                float weight = GetSpawnWeight(def.tier, prob);
                int weightedCount = Mathf.Max(1, Mathf.RoundToInt(baseCount * weight));

                for (int i = 0; i < weightedCount; i++)
                {
                    Vector3 pos = RandomPositionInRing(innerRadius, outerRadius);
                    GameObject go = CreateMonster(def, pos);
                    _spawnedMonsters.Add(go);
                }
            }
        }

        /// <summary>
        /// C18-02: 티어별 가중치 반환 (확률 기반)
        /// </summary>
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
        /// 원형 링 구역 내 랜덤 위치 생성 (Y=0, 평지)
        /// </summary>
        private Vector3 RandomPositionInRing(float innerR, float outerR)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float radius = Random.Range(innerR, outerR);
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            return new Vector3(x, 0f, z);
        }

        /// <summary>
        /// 몬스터 게임오브젝트 생성
        /// </summary>
        private GameObject CreateMonster(MonsterDef def, Vector3 position)
        {
            GameObject go;

            if (_monsterPrefab != null)
            {
                go = Instantiate(_monsterPrefab, position, Quaternion.identity, transform);
            }
            else
            {
                // Resources/Models/UserProvided/ 에서 GLB 모델 로드 시도
                string modelPath = GetMonsterModelPath(def.id);
                if (!string.IsNullOrEmpty(modelPath))
                {
                    GameObject modelPrefab = Resources.Load<GameObject>($"Models/UserProvided/{modelPath}");
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

            go.name = $"Monster_{def.id}_{Random.Range(1000, 9999)}";
            go.tag = "Monster";

            // AnimalAI 컴포넌트
            AnimalAI ai = go.GetComponent<AnimalAI>();
            if (ai == null)
                ai = go.AddComponent<AnimalAI>();

            // 몬스터 ID 설정 (Start()에서 MonsterDatabase 조회)
            ai.SetMonsterId(def.id);

            // [5.3.5] 몬스터 레벨 적용
            ApplyMonsterLevel(ai);

            // C18-04: 밤눈 이펙트 적용
            if (_addNightEyeEffect && IsNightTime())
            {
                ApplyNightEyeEffect(go);
            }

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

            // 영지 난이도 결정 (스포너 위치 기준)
            TerritoryDifficulty difficulty = DetermineTerritoryDifficulty(ai.transform.position);

            // 레벨 생성 및 적용
            int level = lvlMgr.GetMonsterLevel(difficulty, ai.Tier);
            ai.SetLevel(level);

            // MonsterLevelLabel 추가
            UI.MonsterLevelLabel label = ai.GetComponent<UI.MonsterLevelLabel>();
            if (label == null)
                label = ai.gameObject.AddComponent<UI.MonsterLevelLabel>();

            Debug.Log($"[MonsterSpawner] {ai.MonsterId} Lv.{level} ({difficulty})");
        }

        /// <summary>
        /// [5.3.5] 몬스터 위치 기준 영지 난이도 결정
        /// 거리 기반 링 시스템 사용
        /// </summary>
        private TerritoryDifficulty DetermineTerritoryDifficulty(Vector3 monsterPos)
        {
            float dist = Vector3.Distance(transform.position, monsterPos);

            if (dist < _config.beginnerOuter)
                return TerritoryDifficulty.Ring1;
            if (dist < _config.intermediateOuter)
                return TerritoryDifficulty.Ring2;
            if (dist < _config.advancedOuter)
                return TerritoryDifficulty.Ring3;

            return TerritoryDifficulty.Ring4;
        }

        /// <summary>
        /// [몬스터 GLB] MonsterDef.id → Resources/Models/UserProvided/ 의 GLB 파일명 (확장자 제외) 매핑
        /// </summary>
        private string GetMonsterModelPath(string monsterId)
        {
            return monsterId switch
            {
                "rabbit"           => "Rabbit_Rigged",
                "wolf"             => "Wolf_Rigged",
                "boar"             => "Boar_Rigged",
                "deer"             => "Deer_Rigged",
                "poison_snake"     => "Snake_Rigged",
                "bat"              => "Bat_Rigged",
                "giant_rat"        => "Big_Mouse_Rigged",
                "crow"             => "Crow_Rigged",
                "slime"            => "Slime_Rigged",
                "stone_golem"      => "Golem_Rigged",
                "fire_lizard"      => "Fire_Lizard_Rigged",
                "electric_porcupine" => "Electric_Spine_Hedgehog_Rigged",
                "swamp_croc"       => "Swamp_Alligator_Rigged",
                "forest_spirit"    => "Wooden Forest Spirit",
                "wild_troll"       => "Wild_Troll_Rigged",
                "ogre"             => "Swamp_Ogre_Rigged",
                "banshee"          => "Banshee_Rigged",
                "griffin"          => "Griffon_Rigged",
                "minotaur"         => "Minotaur_Rigged",
                "manticore"        => "Manticore_Rigged",
                "salamander"       => "Salamander_Rigged",
                "shadow_assassin"  => "Shadow_Assassin_Rigged",
                _                  => ""
            };
        }

        /// <summary>
        /// 프리팹이 없을 때 Primitive 도형으로 몬스터 생성
        /// </summary>
        private GameObject CreatePrimitiveMonster(MonsterDef def, Vector3 position)
        {
            // 티어별 형태 차별화
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

            // 색상 적용
            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                r.material.color = def.gizmoColor;

                // C18-04: 밤눈 이펙트 (emissive)
                if (_addNightEyeEffect && IsNightTime())
                {
                    r.material.EnableKeyword("_EMISSION");
                    r.material.SetColor("_EmissionColor", _nightEyeColor * _nightEyeIntensity);
                }
            }

            // Collider가 이미 Primitive에 있음
            // Rigidbody (물리 처리)
            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.isKinematic = true; // AI가 직접 위치 제어

            return go;
        }

        /// <summary>
        /// C18-04: 현재 야간인지 확인
        /// </summary>
        private bool IsNightTime()
        {
            if (TimeManager.Instance != null)
                return TimeManager.Instance.IsNight;
            return CurrentPeriod == TimePeriod.Night;
        }

        /// <summary>
        /// C18-04: GLB 몬스터에 밤눈 이펙트 적용 (Eye sub-object 찾기)
        /// </summary>
        private void ApplyNightEyeEffect(GameObject monster)
        {
            if (monster == null) return;

            // 모든 하위 오브젝트에서 "Eye" 또는 "eye" 포함하는 것 찾기
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

        /// <summary>
        /// C18-04: 모든 몬스터의 밤눈 이펙트 갱신 (30초 주기)
        /// </summary>
        private void UpdateNightEyeEffect()
        {
            if (!_addNightEyeEffect) return;
            bool isNight = IsNightTime();

            foreach (var go in _spawnedMonsters)
            {
                if (go == null) continue;

                // Primitive 몬스터 처리
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

                // GLB Eye sub-object 처리
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

        /// <summary>
        /// 모든 생성 몬스터 제거
        /// </summary>
        public void ClearAll()
        {
            foreach (var go in _spawnedMonsters)
            {
                if (go != null) Destroy(go);
            }
            _spawnedMonsters.Clear();
        }

        /// <summary>
        /// C18-03: 주기적 리스폰 체크 — 몬스터 수가 임계값 이하면 추가 스폰
        /// </summary>
        private void CheckAndRespawn()
        {
            if (TimeManager.Instance == null) return;

            bool isNight = TimeManager.Instance.IsNight;
            int minPerTier = _respawnThreshold.minMonstersPerTier;
            if (isNight)
                minPerTier = Mathf.RoundToInt(minPerTier * _nightRespawnRateMultiplier);

            // 각 티어별로 체크
            CheckAndRespawnTier(MonsterTier.Beginner, _config.beginnerInner, _config.beginnerOuter, minPerTier);
            CheckAndRespawnTier(MonsterTier.Intermediate, _config.intermediateInner, _config.intermediateOuter, minPerTier);
            CheckAndRespawnTier(MonsterTier.Advanced, _config.advancedInner, _config.advancedOuter, minPerTier);
        }

        /// <summary>
        /// C18-03: 특정 티어 리스폰 체크
        /// </summary>
        private void CheckAndRespawnTier(MonsterTier tier, float innerRadius, float outerRadius, int minCount)
        {
            int currentCount = CountByTier(tier);
            if (currentCount >= minCount) return;

            int deficit = minCount - currentCount;

            ActiveTime filterTime = IsNightTime() ? ActiveTime.Night : ActiveTime.Day;
            var activePool = MonsterDatabase.GetByActiveTime(filterTime);
            var tierPool = new List<MonsterDef>();

            foreach (var def in activePool)
            {
                if (def.tier == tier)
                    tierPool.Add(def);
            }

            if (tierPool.Count == 0) return;

            SpawnProbabilities prob = GetCurrentProbabilities();
            float weight = GetSpawnWeight(tier, prob);

            int toSpawn = Mathf.Max(1, Mathf.RoundToInt(deficit * weight));

            for (int i = 0; i < toSpawn; i++)
            {
                var def = tierPool[Random.Range(0, tierPool.Count)];
                Vector3 pos = RandomPositionInRing(innerRadius, outerRadius);
                GameObject go = CreateMonster(def, pos);
                _spawnedMonsters.Add(go);
            }
        }

        /// <summary>
        /// 특정 티어 개수
        /// </summary>
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

        // ===== C18-02: 공개 접근자 (테스트/UI 용) =====

        public SpawnProbabilities DayProb => _dayProb;
        public SpawnProbabilities EveningProb => _eveningProb;
        public SpawnProbabilities NightProb => _nightProb;
        public float NightRespawnRateMultiplier => _nightRespawnRateMultiplier;

        // 에디터에서 시각화
        private void OnDrawGizmosSelected()
        {
            if (_config == null) return;

            // 안전 지대
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.05f);
            Gizmos.DrawWireSphere(transform.position, _config.safeRadius);

            // 초반 링
            Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
            DrawRing(transform.position, _config.beginnerInner, _config.beginnerOuter);

            // 중반 링
            Gizmos.color = new Color(1f, 1f, 0f, 0.15f);
            DrawRing(transform.position, _config.intermediateInner, _config.intermediateOuter);

            // 후반 링
            Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
            DrawRing(transform.position, _config.advancedInner, _config.advancedOuter);
        }

        private void DrawRing(Vector3 center, float innerR, float outerR)
        {
            // 내부 원
            DrawCircle(center, innerR);
            // 외부 원
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
