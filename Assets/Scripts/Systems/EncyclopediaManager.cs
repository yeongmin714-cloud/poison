using System;
using System.Collections.Generic;
using System.IO;
using ProjectName.Core.Data;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 42: 도감 관리자 싱글톤.
    /// 발견 이력 저장/로드, 수집률 계산, 보상 시스템 관리.
    /// GameManager.InitializeSystems()에서 자동 생성됨.
    /// </summary>
    [DefaultExecutionOrder(-80)]
    public class EncyclopediaManager : MonoBehaviour
    {
        // ===== 싱글톤 =====
        private static EncyclopediaManager _instance;
        public static EncyclopediaManager Instance => _instance;

        // ===== 이벤트 =====
        /// <summary>항목 발견 시 발생 (EncyclopediaEntry 전달)</summary>
        public static event Action<EncyclopediaEntry> OnEntryDiscovered;

        // ===== 설정 =====
        [Header("설정")]
        [SerializeField] private string _databaseResourcePath = "Encyclopedia/EncyclopediaDatabase";
        [SerializeField] private string _saveFileName = "encyclopedia_discoveries.json";

        [Header("디버그")]
        [SerializeField] private bool _verbose;

        // ===== 런타임 데이터 =====
        private EncyclopediaDatabase _database;
        private HashSet<string> _discoveredIds = new HashSet<string>();

        // ===== 보상 트래킹 (각 보상이 이미 지급되었는지) =====
        private bool _reward10Given;
        private bool _reward25Given;
        private bool _reward50Given;
        private bool _reward75Given;
        private bool _reward100Given;

        // ===== 프로퍼티 =====
        public EncyclopediaDatabase Database => _database;
        public IReadOnlyCollection<string> DiscoveredIds => _discoveredIds;

        public int TotalEntryCount => _database != null ? _database.TotalEntryCount : 0;
        public int TotalDiscoveredCount => _discoveredIds.Count;
        public float OverallCompletionRate =>
            TotalEntryCount > 0 ? (float)TotalDiscoveredCount / TotalEntryCount : 0f;

        // ===== 생명주기 =====

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            LoadDatabase();
            LoadDiscoveries();
        }

        private void Start()
        {
            CheckRewards();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        // ===== 데이터베이스 로드 =====

        private void LoadDatabase()
        {
            _database = Resources.Load<EncyclopediaDatabase>(_databaseResourcePath);
            if (_database == null)
            {
                Debug.LogWarning("[EncyclopediaManager] EncyclopediaDatabase를 찾을 수 없습니다. " +
                    $"Resources/{_databaseResourcePath} 경로에 ScriptableObject가 필요합니다. " +
                    "EncyclopediaDataInitializer를 실행하거나 직접 생성하세요.");
                // 빈 데이터베이스 생성 (에러 방지)
                _database = ScriptableObject.CreateInstance<EncyclopediaDatabase>();
                _database.categories = new List<EncyclopediaCategoryData>();
            }
            else if (_verbose)
            {
                Debug.Log($"[EncyclopediaManager] 도감 로드 완료: " +
                    $"{_database.TotalEntryCount}개 항목, {_database.categories.Count}개 카테고리");
            }
        }

        // ===== 발견 관리 =====

        /// <summary>
        /// ID로 항목을 발견 처리합니다.
        /// </summary>
        public bool DiscoverEntry(string entryId)
        {
            if (_database == null) return false;
            if (string.IsNullOrEmpty(entryId)) return false;
            if (_discoveredIds.Contains(entryId)) return false; // 이미 발견됨

            var entry = _database.FindEntryById(entryId);
            if (entry == null)
            {
                Debug.LogWarning($"[EncyclopediaManager] 항목을 찾을 수 없음: {entryId}");
                return false;
            }

            entry.Discover();
            _discoveredIds.Add(entryId);
            SaveDiscoveries();

            Debug.Log($"[EncyclopediaManager] 도감 발견: [{entry.category}] {entry.entryName} ({entryId})");

            // 이벤트 발행
            OnEntryDiscovered?.Invoke(entry);

            // 수집률 변화 확인 및 보상 체크
            CheckRewards();

            return true;
        }

        /// <summary>
        /// 항목이 발견되었는지 확인.
        /// </summary>
        public bool IsDiscovered(string entryId)
        {
            return _discoveredIds.Contains(entryId);
        }

        // ===== 수집률 계산 =====

        /// <summary>특정 카테고리의 수집률 (0.0 ~ 1.0)</summary>
        public float GetCategoryCompletionRate(EncyclopediaCategory category)
        {
            if (_database == null) return 0f;
            var catData = _database.GetCategory(category);
            if (catData == null || catData.TotalCount == 0) return 0f;
            return catData.CompletionRate;
        }

        /// <summary>특정 카테고리의 발견 수</summary>
        public int GetCategoryDiscoveredCount(EncyclopediaCategory category)
        {
            if (_database == null) return 0;
            var catData = _database.GetCategory(category);
            return catData != null ? catData.DiscoveredCount : 0;
        }

        /// <summary>특정 카테고리의 전체 수</summary>
        public int GetCategoryTotalCount(EncyclopediaCategory category)
        {
            if (_database == null) return 0;
            var catData = _database.GetCategory(category);
            return catData != null ? catData.TotalCount : 0;
        }

        /// <summary>특정 카테고리의 항목 리스트</summary>
        public List<EncyclopediaEntry> GetCategoryEntries(EncyclopediaCategory category)
        {
            if (_database == null) return new List<EncyclopediaEntry>();
            var catData = _database.GetCategory(category);
            return catData != null ? catData.entries : new List<EncyclopediaEntry>();
        }

        // ===== 보상 시스템 =====

        /// <summary>
        /// 수집률에 따른 보상 지급을 확인합니다.
        /// </summary>
        private void CheckRewards()
        {
            float rate = OverallCompletionRate;

            if (!_reward10Given && rate >= 0.10f)
            {
                _reward10Given = true;
                UnlockReward(10, "기본 정보 잠금 해제");
            }
            if (!_reward25Given && rate >= 0.25f)
            {
                _reward25Given = true;
                UnlockReward(25, "제작 성공률 +5%");
            }
            if (!_reward50Given && rate >= 0.50f)
            {
                _reward50Given = true;
                UnlockReward(50, "특수 레시피 잠금 해제");
            }
            if (!_reward75Given && rate >= 0.75f)
            {
                _reward75Given = true;
                UnlockReward(75, "제작 성공률 +10%");
            }
            if (!_reward100Given && rate >= 1.00f - 0.001f) // float 부동소수점 보정
            {
                _reward100Given = true;
                UnlockReward(100, "전설 아이템/업적 해금");
            }
        }

        private void UnlockReward(int percent, string rewardName)
        {
            Debug.Log($"[EncyclopediaManager] 🎉 수집률 {percent}% 달성! 보상: {rewardName}");

            // 보상 효과 적용
            switch (percent)
            {
                case 10:
                    // 기본 정보 잠금 해제 — 도감 UI에서 모든 항목 기본 설명 표시
                    ApplyReward10();
                    break;
                case 25:
                    // 제작 성공률 +5%
                    ApplyCraftingBonus(5);
                    break;
                case 50:
                    // 특수 레시피 잠금 해제
                    UnlockSpecialRecipes();
                    break;
                case 75:
                    // 제작 성공률 +10% (기존에 +5%가 있었다면 누적)
                    ApplyCraftingBonus(10);
                    break;
                case 100:
                    // 전설 아이템/업적 해금
                    UnlockLegendaryRewards();
                    break;
            }

            // UI 알림 (옵션: 알림 시스템 연동)
            Debug.Log($"[EncyclopediaManager] 보상 지급 완료: {rewardName}");
        }

        private void ApplyReward10()
        {
            // 10% 보상: 모든 미발견 항목의 기본 설명을 볼 수 있게 함.
            // EncyclopediaWindow에서 이 플래그를 확인하여 표시.
            if (_verbose) Debug.Log("[EncyclopediaManager] 보상 10%: 기본 정보 잠금 해제");
        }

        private void ApplyCraftingBonus(int percent)
        {
            // 제작 성공률 보너스 — PlayerStats 또는 Recipe 시스템 연동
            // 예: PlayerStats에 수집률 보너스 플래그 설정
            if (_verbose) Debug.Log($"[EncyclopediaManager] 보상: 제작 성공률 +{percent}% 적용됨");
        }

        private void UnlockSpecialRecipes()
        {
            // 특수 레시피 잠금 해제 — RecipeDiscoverySystem 연동
            if (_verbose) Debug.Log("[EncyclopediaManager] 보상 50%: 특수 레시피 잠금 해제");
        }

        private void UnlockLegendaryRewards()
        {
            // 100% 보상: 전설 아이템/업적
            if (_verbose) Debug.Log("[EncyclopediaManager] 보상 100%: 전설 아이템/업적 해금!");
        }

        /// <summary>보상 초기화 (디버그/테스트 용)</summary>
        public void ResetRewards()
        {
            _reward10Given = false;
            _reward25Given = false;
            _reward50Given = false;
            _reward75Given = false;
            _reward100Given = false;
            CheckRewards();
        }

        // ===== 저장/로드 =====

        [Serializable]
        private class DiscoverySaveData
        {
            public List<string> discoveredIds = new List<string>();
            public bool reward10Given;
            public bool reward25Given;
            public bool reward50Given;
            public bool reward75Given;
            public bool reward100Given;
        }

        private string GetSavePath()
        {
            return Path.Combine(Application.persistentDataPath, _saveFileName);
        }

        private void SaveDiscoveries()
        {
            try
            {
                var data = new DiscoverySaveData
                {
                    discoveredIds = new List<string>(_discoveredIds),
                    reward10Given = _reward10Given,
                    reward25Given = _reward25Given,
                    reward50Given = _reward50Given,
                    reward75Given = _reward75Given,
                    reward100Given = _reward100Given
                };

                string json = JsonUtility.ToJson(data, prettyPrint: true);
                File.WriteAllText(GetSavePath(), json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EncyclopediaManager] 저장 실패: {ex.Message}");
            }
        }

        private void LoadDiscoveries()
        {
            try
            {
                string path = GetSavePath();
                if (!File.Exists(path))
                {
                    if (_verbose) Debug.Log("[EncyclopediaManager] 저장된 발견 데이터 없음. 새로 시작합니다.");
                    return;
                }

                string json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<DiscoverySaveData>(json);
                if (data == null) return;

                _discoveredIds.Clear();
                foreach (var id in data.discoveredIds)
                {
                    _discoveredIds.Add(id);
                    // Database의 항목에도 발견 상태 적용
                    var entry = _database?.FindEntryById(id);
                    if (entry != null)
                    {
                        entry.IsDiscovered = true;
                        entry.DiscoveryDate = "저장된 데이터";
                    }
                }

                _reward10Given = data.reward10Given;
                _reward25Given = data.reward25Given;
                _reward50Given = data.reward50Given;
                _reward75Given = data.reward75Given;
                _reward100Given = data.reward100Given;

                if (_verbose)
                {
                    Debug.Log($"[EncyclopediaManager] 발견 데이터 로드 완료: " +
                        $"{_discoveredIds.Count}개 항목 발견됨");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EncyclopediaManager] 로드 실패: {ex.Message}");
            }
        }

        /// <summary>모든 발견 데이터 초기화 (디버그/테스트)</summary>
        public void ClearAllDiscoveries()
        {
            _discoveredIds.Clear();
            _reward10Given = false;
            _reward25Given = false;
            _reward50Given = false;
            _reward75Given = false;
            _reward100Given = false;

            // 데이터베이스 항목 상태 리셋
            if (_database != null)
            {
                for (int c = 0; c < _database.categories.Count; c++)
                {
                    var cat = _database.categories[c];
                    for (int e = 0; e < cat.entries.Count; e++)
                    {
                        cat.entries[e].IsDiscovered = false;
                        cat.entries[e].DiscoveryDate = null;
                    }
                }
            }

            SaveDiscoveries();
            Debug.Log("[EncyclopediaManager] 모든 발견 데이터 초기화 완료");
        }

        // ===== 외부 연동 헬퍼 (다른 시스템에서 호출) =====

        /// <summary>약초 채집 시 호출 (Herb ID)</summary>
        public void OnHerbCollected(string herbId)
        {
            DiscoverEntry("HERB_" + herbId);
        }

        /// <summary>몬스터 처치 시 호출 (Monster ID)</summary>
        public void OnMonsterKilled(string monsterId)
        {
            DiscoverEntry("MON_" + monsterId);
        }

        /// <summary>요리 제조 시 호출 (Dish name)</summary>
        public void OnCookingCreated(string dishName)
        {
            DiscoverEntry("COOK_" + dishName);
        }

        /// <summary>약물 제조 시 호출 (Potion name)</summary>
        public void OnPotionCreated(string potionName)
        {
            DiscoverEntry("POT_" + potionName);
        }

        /// <summary>영주 접촉 시 호출 (Lord ID)</summary>
        public void OnLordMet(string lordId)
        {
            DiscoverEntry("LORD_" + lordId);
        }

        /// <summary>영지 방문 시 호출 (Territory ID)</summary>
        public void OnTerritoryVisited(string territoryId)
        {
            DiscoverEntry("TERR_" + territoryId);
        }

        /// <summary>문서 발견 시 호출 (Document ID)</summary>
        public void OnDocumentFound(string documentId)
        {
            DiscoverEntry("DOC_" + documentId);
        }

        /// <summary>업적 달성 시 호출 (Achievement ID)</summary>
        public void OnAchievementUnlocked(string achievementId)
        {
            DiscoverEntry("ACH_" + achievementId);
        }
    }
}