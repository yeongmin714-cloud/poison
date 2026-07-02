using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [5.3.9] 자동 임무 관리자 — 광부/사냥꾼/약초꾼 임무를 주기적으로 실행
    /// 
    /// 설정된 간격(기본 5초)마다 ExecuteMining(), ExecuteHunting(), ExecuteGathering()을 호출하고
    /// 결과를 수집하여 PlayerInventory에 자동 추가하고 이벤트를 발생시킵니다.
    /// </summary>
    public class AutoMissionManager : MonoBehaviour
    {
        public static AutoMissionManager Instance { get; private set; }

        [Header("설정")]
        [SerializeField] private float _missionInterval = 5f;

        // ===== 타이머 =====
        private float _timer;

        // ===== 결과 큐 =====
        private readonly Queue<MissionResultsBatch> _resultQueue = new Queue<MissionResultsBatch>(20);
        private const int MAX_QUEUED_BATCHES = 20;

        // ===== 최신 결과 =====
        private MissionResultsBatch _latestResults;

        // ===== 이벤트 =====
        /// <summary>최신 임무 결과 배치가 준비되었을 때 발생</summary>
        public static event System.Action<MissionResultsBatch> OnMissionResultsReady;

        // ===== 퍼블릭 프로퍼티 =====
        public float MissionInterval { get => _missionInterval; set => _missionInterval = Mathf.Max(0.5f, value); }
        public MissionResultsBatch LatestResults => _latestResults;
        public Queue<MissionResultsBatch> ResultQueue => _resultQueue;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _timer = _missionInterval;
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                _timer = _missionInterval;
                ExecuteAllMissions();
            }
        }

        private void ExecuteAllMissions()
        {
            var batch = new MissionResultsBatch
            {
                timestamp = Time.time,
                mineResults = MiningMission.ExecuteMining(),
                huntResults = HuntingMission.ExecuteHunting(),
                gatherResults = HerbGatheringMission.ExecuteGathering()
            };

            _latestResults = batch;

            // 큐에 추가 (최대 20개 유지)
            _resultQueue.Enqueue(batch);
            while (_resultQueue.Count > MAX_QUEUED_BATCHES)
                _resultQueue.Dequeue();

            // 이벤트 발생
            OnMissionResultsReady?.Invoke(batch);
        }
    }

    /// <summary>
    /// 한 번의 임무 실행 주기에서 수집된 모든 결과
    /// </summary>
    public struct MissionResultsBatch
    {
        public float timestamp;
        public List<MiningMission.MineResult> mineResults;
        public List<HuntingMission.HuntResult> huntResults;
        public List<HerbGatheringMission.GatherResult> gatherResults;
    }
}
