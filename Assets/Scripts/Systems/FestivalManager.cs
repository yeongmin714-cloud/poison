using System;
using System.Collections.Generic;
using System.Linq;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 38.3: 영지별 축제 관리자 싱글톤.
    /// 게임 시작 시 FestivalData 로드, Day 체크 → 활성화된 축제 판단,
    /// 축제 시작/종료 이벤트 발행, UI 알림 연동.
    /// </summary>
    [DefaultExecutionOrder(100)] // GameManager 이후 실행
    public class FestivalManager : MonoBehaviour
    {
        // ===== 싱글톤 =====
        public static FestivalManager Instance { get; private set; }

        [Header("설정")]
        [SerializeField] private bool _verbose;

        // ===== 상태 =====
        private List<FestivalData> _allFestivals = new List<FestivalData>();
        private readonly Dictionary<string, FestivalData> _festivalLookup = new Dictionary<string, FestivalData>();

        /// <summary>현재 Day에 활성화된 축제 목록 (캐시)</summary>
        private List<FestivalData> _activeFestivals = new List<FestivalData>();

        /// <summary>이전 프레임의 활성 축제 ID 집합 (변경 감지용)</summary>
        private HashSet<string> _previouslyActiveIds = new HashSet<string>();

        // 마지막 체크 Day (중복 검사 방지)
        private int _lastCheckedDay = -1;
        private int _lastCheckedHour = -1;

        // ===== 이벤트 =====

        /// <summary>축제 시작 시 호출</summary>
        public static event Action<FestivalData> OnFestivalStarted;

        /// <summary>축제 종료 시 호출</summary>
        public static event Action<FestivalData> OnFestivalEnded;

        /// <summary>활성 축제 목록 변경 시 호출 (시작/종료 모두)</summary>
        public static event Action<IReadOnlyList<FestivalData>> OnActiveFestivalsChanged;

        // ===== 프로퍼티 =====

        /// <summary>모든 등록된 축제 (읽기 전용)</summary>
        public IReadOnlyList<FestivalData> AllFestivals => _allFestivals.AsReadOnly();

        /// <summary>현재 활성화된 축제 목록 (읽기 전용)</summary>
        public IReadOnlyList<FestivalData> ActiveFestivals => _activeFestivals.AsReadOnly();

        /// <summary>특정 영지에서 현재 활성화된 축제 (없으면 null)</summary>
        public FestivalData GetActiveFestivalAtTerritory(TerritoryId territoryId)
        {
            string key = territoryId.ToString();
            return _activeFestivals.FirstOrDefault(f => f.territoryId.ToString() == key);
        }

        /// <summary>특정 영지에서 현재 활성화된 축제 (문자열 키)</summary>
        public FestivalData GetActiveFestivalAtTerritory(string territoryIdStr)
        {
            return _activeFestivals.FirstOrDefault(f => f.territoryId.ToString() == territoryIdStr);
        }

        /// <summary>특정 ID의 축제 데이터 조회</summary>
        public FestivalData GetFestivalById(string festivalId)
        {
            _festivalLookup.TryGetValue(festivalId, out var data);
            return data;
        }

        /// <summary>현재 축제 기간 여부 (어떤 축제든)</summary>
        public bool HasAnyActiveFestival => _activeFestivals.Count > 0;

        // ===== 싱글톤 생명주기 =====

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadDefinitions();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            CheckFestivals();
        }

        // ===== 축제 정의 로드 =====

        /// <summary>
        /// FestivalDefinitions.CreateAll()에서 6개 축제 정의를 로드합니다.
        /// Resources 폴더에서 Asset 기반 FestivalData도 함께 로드할 수 있습니다.
        /// </summary>
        private void LoadDefinitions()
        {
            // 코드 기반 정의 로드
            var codeDefs = FestivalDefinitions.CreateAll();
            foreach (var def in codeDefs)
            {
                RegisterFestival(def);
            }

            // Resources 폴더에서 ScriptableObject 에셋 로드 (선택적)
            var assetDefs = Resources.LoadAll<FestivalData>("Festivals");
            foreach (var def in assetDefs)
            {
                if (!_festivalLookup.ContainsKey(def.festivalId))
                {
                    RegisterFestival(def);
                }
                else if (_verbose)
                {
                    Debug.LogWarning($"[FestivalManager] 중복 축제 ID 무시: {def.festivalId} (Asset이 Code 정의보다 우선하지 않음)");
                }
            }

            if (_verbose)
            {
                Debug.Log($"[FestivalManager] {_allFestivals.Count}개 축제 정의 로드 완료");
                foreach (var f in _allFestivals)
                {
                    Debug.Log($"  - {f}");
                }
            }
        }

        /// <summary>축제를 레지스트리에 등록</summary>
        private void RegisterFestival(FestivalData data)
        {
            if (data == null) return;
            _allFestivals.Add(data);
            _festivalLookup[data.festivalId] = data;
        }

        /// <summary>외부에서 축제 정의 추가 (Runtime)</summary>
        public void AddFestival(FestivalData data)
        {
            if (data == null) return;
            if (_festivalLookup.ContainsKey(data.festivalId))
            {
                Debug.LogWarning($"[FestivalManager] 이미 존재하는 축제 ID: {data.festivalId}, 덮어씁니다.");
                _allFestivals.RemoveAll(f => f.festivalId == data.festivalId);
            }
            RegisterFestival(data);
        }

        // ===== 축제 상태 체크 =====

        /// <summary>
        /// 매 프레임 TimeManager의 CurrentDay/Hour를 확인하여
        /// 축제 시작/종료를 감지하고 이벤트를 발행합니다.
        /// </summary>
        private void CheckFestivals()
        {
            var timeManager = TimeManager.Instance;
            if (timeManager == null) return;

            int currentDay = timeManager.CurrentDay;
            int currentHour = timeManager.Hour;

            // 같은 시간 내에서는 중복 체크하지 않음 (성능 최적화)
            if (currentDay == _lastCheckedDay && currentHour == _lastCheckedHour)
                return;

            _lastCheckedDay = currentDay;
            _lastCheckedHour = currentHour;

            // 활성 축전 재계산
            var newlyActive = _allFestivals
                .Where(f => f.IsActive(currentDay, currentHour))
                .ToList();

            // 변경 감지
            var newlyActiveIds = new HashSet<string>(newlyActive.Select(f => f.festivalId));

            // 종료된 축제 감지
            foreach (var prevId in _previouslyActiveIds)
            {
                if (!newlyActiveIds.Contains(prevId) && _festivalLookup.TryGetValue(prevId, out var ended))
                {
                    if (_verbose)
                        Debug.Log($"[FestivalManager] 축제 종료: {ended.festivalName} (Day {currentDay}, {currentHour}:00)");
                    OnFestivalEnded?.Invoke(ended);
                }
            }

            // 시작된 축제 감지
            foreach (var newlyId in newlyActiveIds)
            {
                if (!_previouslyActiveIds.Contains(newlyId) && _festivalLookup.TryGetValue(newlyId, out var started))
                {
                    if (_verbose)
                        Debug.Log($"[FestivalManager] 🎉 축제 시작: {started.festivalName} (Day {currentDay}, {currentHour}:00)");
                    OnFestivalStarted?.Invoke(started);
                }
            }

            // 상태 업데이트
            _activeFestivals = newlyActive;
            _previouslyActiveIds = newlyActiveIds;

            // 변경 시 통합 이벤트 발행
            if (newlyActiveIds.Count != _previouslyActiveIds.Count ||
                !newlyActiveIds.SetEquals(_previouslyActiveIds))
            {
                OnActiveFestivalsChanged?.Invoke(_activeFestivals.AsReadOnly());
            }
        }

        /// <summary>강제로 축제 상태를 다시 체크합니다 (예: 수면 후)</summary>
        public void ForceRefresh()
        {
            _lastCheckedDay = -1;
            _lastCheckedHour = -1;
            CheckFestivals();
        }

        /// <summary>특정 영지의 축제 정보 문자열 (UI 표시용)</summary>
        public string GetTerritoryFestivalInfo(TerritoryId territoryId)
        {
            var festival = GetActiveFestivalAtTerritory(territoryId);
            if (festival == null) return string.Empty;

            return $"{festival.emoji} {festival.festivalName}\n{festival.description}\n효과: {festival.GetEffect().GetSummary()}";
        }
    }
}