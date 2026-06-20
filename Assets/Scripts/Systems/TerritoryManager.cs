using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// 영지 관리자 — 현재 로드된 영지의 건물, 병사, 시설 등을 추적하고 관리합니다.
    /// TerritoryDatabase와 연동하여 영지 정의/상태를 제공합니다.
    /// 
    /// 사용법:
    ///   TerritoryManager.Instance.CurrentTerritoryId  // 현재 영지 ID
    ///   TerritoryManager.Instance.TerritoryData       // TerritoryDatabase 인스턴스
    /// </summary>
    public class TerritoryManager : MonoBehaviour
    {
        public static TerritoryManager Instance { get; private set; }

        [Header("영지 설정")]
        [SerializeField] private NationType _currentNation = NationType.East;
        [SerializeField] private int _currentTerritoryIndex = 1;

        // 건물 목록
        private readonly Dictionary<string, BuildingPlaceholder> _buildings = new Dictionary<string, BuildingPlaceholder>();
        // 병사 목록
        private readonly Dictionary<string, GuardPlaceholder> _guards = new Dictionary<string, GuardPlaceholder>();

        /// <summary>현재 영지의 TerritoryId</summary>
        public TerritoryId CurrentTerritoryId => new TerritoryId(_currentNation, _currentTerritoryIndex);

        /// <summary>TerritoryDatabase 인스턴스</summary>
        public TerritoryDatabase TerritoryData => TerritoryDatabase.Instance;

        /// <summary>현재 영지 정의</summary>
        public TerritoryDefinition CurrentDefinition => TerritoryDatabase.Instance.GetDefinition(CurrentTerritoryId);

        /// <summary>현재 영지 상태</summary>
        public TerritoryState CurrentState => TerritoryDatabase.Instance.GetState(CurrentTerritoryId);

        /// <summary>로드된 모든 건물 이름 목록</summary>
        public ICollection<string> BuildingNames => _buildings.Keys;

        /// <summary>로드된 모든 병사 이름 목록</summary>
        public ICollection<string> GuardNames => _guards.Keys;

        private void Awake()
        {
            // 싱글톤 패턴
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 모든 건물과 병사 오브젝트를 찾아서 등록
            FindAllBuildings();
            FindAllGuards();

            // 영지 데이터 출력
            var def = CurrentDefinition;
            Debug.Log($"[TerritoryManager] 영지 초기화 완료: {def.territoryName} ({def.nation} Ring{(int)def.difficulty + 1}) " +
                      $"건물: {_buildings.Count}개, 병사: {_guards.Count}명");
        }

        private void FindAllBuildings()
        {
            var buildingObjects = Object.FindObjectsOfType<BuildingPlaceholder>();
            foreach (var building in buildingObjects)
            {
                if (!_buildings.ContainsKey(building.name))
                {
                    _buildings.Add(building.name, building);
                }
                else
                {
                    Debug.LogWarning($"[TerritoryManager] 중복된 건물 이름 발견: {building.name}");
                }
            }
        }

        private void FindAllGuards()
        {
            var guardObjects = Object.FindObjectsOfType<GuardPlaceholder>();
            foreach (var guard in guardObjects)
            {
                if (!_guards.ContainsKey(guard.name))
                {
                    _guards.Add(guard.name, guard);
                }
                else
                {
                    Debug.LogWarning($"[TerritoryManager] 중복된 병사 이름 발견: {guard.name}");
                }
            }
        }

        /// <summary>
        /// 건물 이름으로 건물 객체 찾기
        /// </summary>
        public BuildingPlaceholder GetBuilding(string name)
        {
            _buildings.TryGetValue(name, out var building);
            return building;
        }

        /// <summary>
        /// 병사 이름으로 병사 객체 찾기
        /// </summary>
        public GuardPlaceholder GetGuard(string name)
        {
            _guards.TryGetValue(name, out var guard);
            return guard;
        }

        /// <summary>
        /// 특정 유형의 모든 건물 반환
        /// </summary>
        public IEnumerable<BuildingPlaceholder> GetBuildingsByType(BuildingPlaceholder.BuildingType type)
        {
            foreach (var building in _buildings.Values)
            {
                if (building.buildingType == type)
                    yield return building;
            }
        }

        /// <summary>
        /// 영지 중심점 계산 (모든 건물의 평균 위치)
        /// </summary>
        public Vector3 GetTerritoryCenter()
        {
            if (_buildings.Count == 0) return Vector3.zero;
            Vector3 sum = Vector3.zero;
            foreach (var building in _buildings.Values)
            {
                sum += building.transform.position;
            }
            return sum / _buildings.Count;
        }

        /// <summary>특정 영지의 중심점 반환</summary>
        public Vector3 GetTerritoryCenter(ProjectName.Core.Data.TerritoryId territoryId)
        {
            return GetTerritoryCenter();
        }

        /// <summary>
        /// 영지 소유주 변경 (TerritoryBannerSystem 호출용 스텁)
        /// </summary>
        public void SetTerritoryOwner(string territoryId, NationType owner)
        {
            Debug.Log($"[TerritoryManager] SetTerritoryOwner: {territoryId} → {owner} (스텁)");
        }

        /// <summary>
        /// 현재 영지의 난이도 설명 문자열
        /// </summary>
        public string GetDifficultyDescription()
        {
            var def = CurrentDefinition;
            switch (def.difficulty)
            {
                case TerritoryDifficulty.Ring1: return "🟢 쉬움 (Ring 1)";
                case TerritoryDifficulty.Ring2: return "🟡 보통 (Ring 2)";
                case TerritoryDifficulty.Ring3: return "🟠 어려움 (Ring 3)";
                case TerritoryDifficulty.Ring4: return "🔴 매우 어려움 (Ring 4)";
                case TerritoryDifficulty.Empire: return "👑 황제국";
                default: return "알 수 없음";
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}