using System.Collections.Generic;
using System.Linq;
using ProjectName.Core.Data;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Systems.Animation;
using ProjectName.Systems.Animation.Neural;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 27: 영지별 병사 목록 관리자.
    /// - 병사 사망 시 목록에서 제거
    /// - 영지 재충원: 시간 경과 시 신규 병사 생성 (레벨별 GLB + 장비 파츠 + ModelAnimatorAssigner)
    /// - 플레이어 사망 시 병사 퇴각/귀환
    /// - 병사 레벨: RingDifficultyData.GetGuardLevelRange() 기반 (Lv1-50)
    /// - 병사 GLB: Soldier_Lv1-20 / Lv20-40 / Lv40-50_Rigged.glb
    /// - 장비 파츠: wood→steel→crystal→stone (레벨별)
    /// </summary>
    public class GuardManager : MonoBehaviour
    {
        public static GuardManager Instance { get; private set; }

        [Header("설정")]
        [SerializeField] private float _refillInterval = 60f; // 재충원 간격 (초)
        [SerializeField] private float _refillChance = 0.5f;  // 재충원 확률 (0~1)
        [SerializeField] private int _maxGuardsPerTerritory = 5;

        // 영지별 병사 목록: territoryId_string → List<GuardPlaceholder>
        private readonly Dictionary<string, List<GuardPlaceholder>> _territoryGuards
            = new Dictionary<string, List<GuardPlaceholder>>();

        // 재충원 타이머
        private readonly Dictionary<string, float> _refillTimers = new Dictionary<string, float>();

        // 병사 퇴각 모드 플래그 (플레이어 사망 시 true)
        public bool IsRetreatMode { get; private set; } = false;

        // 자동 회복
        private float _autoHealTimer = 0f;
        private bool _isAutoHealing = false;

        // ===== 이벤트 =====
        /// <summary>병사 사망 시 발생 (territoryId, guard)</summary>
        public event System.Action<TerritoryId, GuardPlaceholder> OnGuardKilled;
        /// <summary>신규 병사 생성 시 발생 (territoryId, guard)</summary>
        public event System.Action<TerritoryId, GuardPlaceholder> OnGuardSpawned;
        /// <summary>퇴각 모드 진입/해제</summary>
        public event System.Action<bool> OnRetreatModeChanged;

        // ===== 상수 =====
        private static readonly string[] _guardFirstNames = {
            "김", "이", "박", "최", "정", "강", "조", "윤", "장", "임",
            "오", "한", "신", "서", "권", "황", "안", "송", "전", "홍"
        };
        private static readonly string[] _guardLastNames = {
            "용사", "전사", "수호자", "파수꾼", "검투사", "창병", "궁수",
            "기병", "보병", "정예병", "순찰자", "경비병", "호위병"
        };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // PlayerHealth 사망 이벤트 구독
            PlayerHealth.OnPlayerDied += OnPlayerDied;
            PlayerHealth.OnPlayerRespawned += OnPlayerRespawned;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            PlayerHealth.OnPlayerDied -= OnPlayerDied;
            PlayerHealth.OnPlayerRespawned -= OnPlayerRespawned;
        }

        private void Update()
        {
            // 재충원 타이머 업데이트
            var keys = _refillTimers.Keys.ToArray();
            foreach (string key in keys)
            {
                _refillTimers[key] += Time.deltaTime;
                if (_refillTimers[key] >= _refillInterval)
                {
                    _refillTimers[key] = 0f;
                    TryRefillTerritory(key);
                }
            }

            // 퇴각 모드: 병사 회복 처리
            if (IsRetreatMode)
            {
                ProcessRetreatHealing();
            }

            // 자동 회복 처리
            if (_isAutoHealing)
            {
                ProcessAutoHealing();
            }
        }

        // ================================================================
        // 공개 API
        // ================================================================

        /// <summary>영지에 병사 등록</summary>
        public void RegisterGuard(TerritoryId territoryId, GuardPlaceholder guard)
        {
            string key = territoryId.ToString();
            if (!_territoryGuards.ContainsKey(key))
                _territoryGuards[key] = new List<GuardPlaceholder>();

            if (!_territoryGuards[key].Contains(guard))
            {
                _territoryGuards[key].Add(guard);
                Debug.Log($"[GuardManager] ✅ {guard.GuardName} 등록됨 → {key}");
            }
        }

        /// <summary>영지에서 병사 제거 (사망 시)</summary>
        public void UnregisterGuard(TerritoryId territoryId, GuardPlaceholder guard)
        {
            string key = territoryId.ToString();
            if (_territoryGuards.TryGetValue(key, out var guards))
            {
                if (guards.Remove(guard))
                {
                    Debug.Log($"[GuardManager] ❌ {guard.GuardName} 제거됨 ← {key}");
                    OnGuardKilled?.Invoke(territoryId, guard);
                }
            }
        }

        /// <summary>영지의 병사 목록 반환</summary>
        public List<GuardPlaceholder> GetGuardsInTerritory(TerritoryId territoryId)
        {
            string key = territoryId.ToString();
            if (_territoryGuards.TryGetValue(key, out var guards))
                return new List<GuardPlaceholder>(guards);
            return new List<GuardPlaceholder>();
        }

        /// <summary>플레이어 소유 모든 영지의 병사 목록 반환</summary>
        public List<GuardPlaceholder> GetAllPlayerGuards()
        {
            var result = new List<GuardPlaceholder>();
            var db = TerritoryDatabase.Instance;
            foreach (var kvp in _territoryGuards)
            {
                TerritoryId id = ParseTerritoryKey(kvp.Key);
                TerritoryState state = db.GetState(id);
                if (state != null && state.ownership == TerritoryOwnership.PlayerOwned)
                {
                    result.AddRange(kvp.Value);
                }
            }
            return result;
        }

        /// <summary>모든 병사의 전투 중단</summary>
        public void StopAllCombat()
        {
            foreach (var kvp in _territoryGuards)
            {
                foreach (var guard in kvp.Value)
                {
                    if (guard != null)
                    {
                        guard.SetInCombat(false);
                    }
                }
            }
        }

        /// <summary>
        /// 퇴각 모드 설정 (플레이어 사망 시)
        /// </summary>
        public void SetRetreatMode(bool retreat)
        {
            if (IsRetreatMode == retreat) return;
            IsRetreatMode = retreat;
            OnRetreatModeChanged?.Invoke(retreat);

            if (retreat)
            {
                // 모든 병사 전투 중단
                StopAllCombat();
                Debug.Log("[GuardManager] 🏴 퇴각 모드 활성화!");
            }
            else
            {
                Debug.Log("[GuardManager] ⚔️ 퇴각 모드 해제!");
            }
        }

        /// <summary>
        /// 가장 가까운 플레이어 소유 영지 찾기
        /// </summary>
        public TerritoryId? FindNearestPlayerTerritory(Vector3 position)
        {
            TerritoryId? nearest = null;
            float nearestDist = float.MaxValue;
            var db = TerritoryDatabase.Instance;

            foreach (var kvp in _territoryGuards)
            {
                TerritoryId id = ParseTerritoryKey(kvp.Key);
                TerritoryState state = db.GetState(id);
                if (state == null || state.ownership != TerritoryOwnership.PlayerOwned)
                    continue;

                if (kvp.Value.Count == 0) continue;

                // 첫 번째 병사의 위치를 영지 중심으로 사용
                float dist = Vector3.Distance(position, kvp.Value[0].transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = id;
                }
            }

            // TerritoryManager의 중심점도 확인
            if (TerritoryManager.Instance != null)
            {
                Vector3 center = TerritoryManager.Instance.GetTerritoryCenter();
                TerritoryId currentId = TerritoryManager.Instance.CurrentTerritoryId;
                TerritoryState currentState = db.GetState(currentId);
                if (currentState != null && currentState.ownership == TerritoryOwnership.PlayerOwned)
                {
                    float dist = Vector3.Distance(position, center);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearest = currentId;
                    }
                }
            }

            return nearest;
        }

        // ================================================================
        // 재충원 시스템
        // ================================================================

        /// <summary>영지 재충원 타이머 시작</summary>
        public void StartRefillTimer(TerritoryId territoryId)
        {
            string key = territoryId.ToString();
            if (!_refillTimers.ContainsKey(key))
                _refillTimers[key] = 0f;
        }

        /// <summary>재충원 타이머 중지</summary>
        public void StopRefillTimer(TerritoryId territoryId)
        {
            string key = territoryId.ToString();
            _refillTimers.Remove(key);
        }

        private void TryRefillTerritory(string key)
        {
            if (!_territoryGuards.TryGetValue(key, out var guards)) return;

            // 최대 병사 수 체크
            if (guards.Count >= _maxGuardsPerTerritory) return;

            // 확률 체크
            if (Random.value > _refillChance) return;
            TerritoryId id = ParseTerritoryKey(key);

            // 새 병사 생성 (GLB 레벨별 + 장비 파츠 + ModelAnimatorAssigner)
            string newName = GenerateGuardName();
            GameObject guardGO = new GameObject(newName);
            guardGO.transform.position = GetRandomSpawnPosition(id);

            // 레벨 결정: RingDifficultyData.GetGuardLevelRange()
            var territoryDef = TerritoryDatabase.Instance?.GetDefinition(id);
            TerritoryDifficulty diff = territoryDef?.difficulty ?? TerritoryDifficulty.Ring1;
            var levelRange = RingDifficultyData.GetGuardLevelRange(diff);
            int newLevel = Random.Range(levelRange.x, levelRange.y + 1);

            // GLB 모델 로드 (레벨별)
            string modelPath = GetSoldierModelPath(newLevel);
            GameObject modelPrefab = null;
            if (!string.IsNullOrEmpty(modelPath))
            {
                modelPrefab = Resources.Load<GameObject>($"Models/UserProvided/{modelPath}");
            }

            GameObject guardModel;
            if (modelPrefab != null)
            {
                guardModel = Object.Instantiate(modelPrefab, guardGO.transform);
                guardModel.name = "GuardModel";
                guardModel.transform.localPosition = new Vector3(0, 0.9f, 0);
                guardModel.transform.localScale = Vector3.one;
                // Soldier 모델도 Player 레이어로
                SetLayerRecursive(guardModel, LayerMask.NameToLayer("Player"));
            }
            else
            {
                // 폴백: Cube 프록시
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "GuardModel";
                cube.transform.SetParent(guardGO.transform);
                cube.transform.localPosition = new Vector3(0, 0.9f, 0);
                cube.transform.localScale = new Vector3(0.6f, 1.8f, 0.6f);
                Object.DestroyImmediate(cube.GetComponent<BoxCollider>());
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = Color.cyan;
                cube.GetComponent<MeshRenderer>().sharedMaterial = mat;
                cube.layer = LayerMask.NameToLayer("Player");
                guardModel = cube;
            }

            var guard = guardGO.AddComponent<GuardPlaceholder>();
            guard.SetGuardInfo(newName, newLevel, id.nation);
            guard.SetRecruited(true);

            // ModelAnimatorAssigner 부착 (ForceBiped)
            var assigner = guardModel.AddComponent<ModelAnimatorAssigner>();
            assigner.ForceBiped(true);

            // 장비 파츠 장착 (레벨별 wood→steel→crystal→stone)
            EquipSoldierParts(guardModel, newLevel);

            // 생성된 병사를 현재 영지에 등록
            guards.Add(guard);

            Debug.Log($"[GuardManager] 🆕 신규 병사 생성: {newName} (Lv.{newLevel}) → {key} | Model: {modelPath}");
            OnGuardSpawned?.Invoke(id, guard);
        }

        // ================================================================
        // 플레이어 사망 → 병사 퇴각/부활 처리
        // ================================================================

        private void OnPlayerDied()
        {
            Debug.Log("[GuardManager] 💀 플레이어 사망 감지! 병사 퇴각 모드 진입");
            Debug.Log("[GuardManager] 📢 \"플레이어가 쓰러졌다! 병사들, 퇴각하라!\"");

            // 퇴각 모드 활성화
            SetRetreatMode(true);
        }

        private void OnPlayerRespawned()
        {
            Debug.Log("[GuardManager] 🔄 플레이어 부활 감지! 병사 상태 복구");

            // 퇴각 모드 해제
            SetRetreatMode(false);

            // 모든 플레이어 소속 병사: 체력 10%만 보유
            SetGuardsToLowHP();

            // 30초 자동 회복 시작
            StartAutoHealing();
        }

        /// <summary>
        /// 모든 플레이어 병사 체력을 최대의 10%로 설정
        /// </summary>
        private void SetGuardsToLowHP()
        {
            var playerGuards = GetAllPlayerGuards();
            foreach (var guard in playerGuards)
            {
                if (guard != null && guard.IsAlive)
                {
                    float lowHP = guard.MaxHP * 0.1f;
                    guard.SetHP(lowHP);
                }
            }
            Debug.Log($"[GuardManager] 💔 모든 병사 체력을 최대의 10%로 설정 완료");
        }

        private void StartAutoHealing()
        {
            _autoHealTimer = 0f;
            _isAutoHealing = true;
        }

        private void ProcessRetreatHealing()
        {
            // 체력 회복은 퇴각 모드가 끝난 후 30초간 진행
            // ProcessAutoHealing에서 처리
        }

        private void ProcessAutoHealing()
        {
            if (!_isAutoHealing) return;

            _autoHealTimer += Time.deltaTime;
            if (_autoHealTimer > 30f)
            {
                _isAutoHealing = false;
                Debug.Log("[GuardManager] ✅ 자동 회복 완료");
                return;
            }

            // 체력 1%/초 회복
            var playerGuards = GetAllPlayerGuards();
            foreach (var guard in playerGuards)
            {
                if (guard != null && guard.IsAlive)
                {
                    float healAmount = guard.MaxHP * 0.01f * Time.deltaTime;
                    guard.SetHP(guard.HP + healAmount);
                }
            }
        }

        // ================================================================
        // 헬퍼
        // ================================================================

        private static string GenerateGuardName()
        {
            string first = _guardFirstNames[Random.Range(0, _guardFirstNames.Length)];
            string last = _guardLastNames[Random.Range(0, _guardLastNames.Length)];
            return $"{first}{last}";
        }

        private static Vector3 GetRandomSpawnPosition(TerritoryId id)
        {
            // TerritoryManager 중심점 기준 랜덤 위치
            if (TerritoryManager.Instance != null)
            {
                Vector3 center = TerritoryManager.Instance.GetTerritoryCenter();
                float radius = 10f;
                Vector2 randomCircle = Random.insideUnitCircle * radius;
                return center + new Vector3(randomCircle.x, 0, randomCircle.y);
            }
            return Vector3.zero;
        }

        private static TerritoryId ParseTerritoryKey(string key)
        {
            // Format: "East_01"
            var parts = key.Split('_');
            if (parts.Length == 2 && System.Enum.TryParse<NationType>(parts[0], out var nation)
                && int.TryParse(parts[1], out int index))
            {
                return new TerritoryId(nation, index);
            }
            return new TerritoryId(NationType.East, 1);
        }

        // ===== 테스트 헬퍼 =====

        /// <summary>테스트용: 영지 병사 목록 직접 설정</summary>
        public void SetGuardsForTest(TerritoryId id, List<GuardPlaceholder> guards)
        {
            string key = id.ToString();
            _territoryGuards[key] = new List<GuardPlaceholder>(guards);
        }

        /// <summary>
        /// GuardPlaceholder.Die()에서 호출 — 사망한 병사를 GuardManager 목록에서 제거
        /// </summary>
        public void OnGuardDiedInGame(GuardPlaceholder guard)
        {
            // 모든 영지 목록에서 이 병사 제거
            foreach (var kvp in _territoryGuards)
            {
                if (kvp.Value.Remove(guard))
                {
                    TerritoryId id = ParseTerritoryKey(kvp.Key);
                    OnGuardKilled?.Invoke(id, guard);
                    Debug.Log($"[GuardManager] 💀 {guard.GuardName} 사망 → {kvp.Key}에서 제거됨");
                    return;
                }
            }
        }

        /// <summary>테스트용: 퇴각 모드 강제 설정</summary>
        public void SetRetreatModeForTest(bool retreat)
        {
            SetRetreatMode(retreat);
        }

        /// <summary>테스트용: 모든 플레이어 병사 체력을 10%로 설정</summary>
        public void SetGuardsToLowHPForTest()
        {
            SetGuardsToLowHP();
        }

        /// <summary>테스트용: 자동 회복 강제 실행 (1틱)</summary>
        public void ProcessAutoHealingForTest()
        {
            ProcessAutoHealing();
        }

        /// <summary>테스트용: 자동 회복 활성화</summary>
        public void StartAutoHealingForTest()
        {
            StartAutoHealing();
        }

        /// <summary>테스트용: 재충원 실행</summary>
        public void TryRefillForTest(TerritoryId id)
        {
            TryRefillTerritory(id.ToString());
        }

        /// <summary>테스트용: 영지 병사 수 반환</summary>
        public int GetGuardCount(TerritoryId id)
        {
            string key = id.ToString();
            if (_territoryGuards.TryGetValue(key, out var guards))
                return guards.Count;
            return 0;
        }

        // ================================================================
        // 헬퍼: 병사 GLB 모델 경로 (레벨별)
        // ================================================================
        private string GetSoldierModelPath(int level)
        {
            if (level <= 20) return "Soldier_Lv1-20_Rigged.glb";
            if (level <= 40) return "Soldier_Lv20-40_Rigged.glb";
            return "Soldier_Lv40-50_Rigged.glb";
        }

        // ================================================================
        // 헬퍼: 병사 장비 파츠 장착 (레벨별 wood→steel→crystal→stone)
        // ================================================================
        private void EquipSoldierParts(GameObject model, int level)
        {
            // 파츠 타입 결정
            string tier;
            if (level <= 10) tier = "wood";
            else if (level <= 25) tier = "steel";
            else if (level <= 40) tier = "crystal";
            else tier = "stone";

            string[] partNames = { "armor", "helmet", "boots_left", "boots_right", "gloves_left", "gloves_right", "sword", "shield" };

            foreach (string part in partNames)
            {
                string partPath = $"{tier}_{part}.glb";
                var partPrefab = Resources.Load<GameObject>($"Models/UserProvided/{partPath}");
                if (partPrefab != null)
                {
                    var partInstance = Object.Instantiate(partPrefab, model.transform);
                    partInstance.name = part;
                    partInstance.transform.localPosition = Vector3.zero;
                    partInstance.transform.localScale = Vector3.one;
                    partInstance.layer = LayerMask.NameToLayer("Player");
                }
            }
        }

        // ================================================================
        // 헬퍼: 재귀적으로 레이어 설정
        // ================================================================
        private static void SetLayerRecursive(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursive(child.gameObject, layer);
            }
        }
    }
}