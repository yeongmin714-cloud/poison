using System;
using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 24: 영지 전투 상태 관리자.
    /// - 플레이어 이탈 감지 (50m) → Retreat 전환
    /// - 10초 타이머 후 Reinforcing
    /// - 30초 간격 병사 리스폰
    /// - 영주 사망 시 Conquered (리스폰 중단)
    /// </summary>
    public class TerritoryBattleManager : MonoBehaviour
    {
        public static TerritoryBattleManager Instance { get; private set; }

        [Header("설정")]
        [SerializeField] private float _territoryRadius = 50f;
        [SerializeField] private float _retreatDelay = 10f;
        [SerializeField] private float _reinforceInterval = 30f;
        [SerializeField] private float _lordRespawnDelay = 30f; // 병사 전원 복원 후 +30초

        // 사망한 가드 리스폰 큐 (territoryId → dead guards list)
        private readonly Dictionary<string, Queue<GuardRespawnEntry>> _respawnQueues
            = new Dictionary<string, Queue<GuardRespawnEntry>>();

        // 플레이어 참조
        private Transform _playerTransform;

        // ===== 이벤트 =====
        public event Action<TerritoryId, TerritoryBattleState> OnBattleStateChanged;

        // ================================================================
        // 리스폰 엔트리 데이터
        // ================================================================

        public class GuardRespawnEntry
        {
            public GuardPlaceholder guard;
        }

        // ================================================================
        // 생명주기
        // ================================================================

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
            _playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (_playerTransform == null) return;

            // 각 영지의 상태 업데이트
            TerritoryManager tm = TerritoryManager.Instance;
            if (tm == null) return;

            TerritoryDatabase db = tm.TerritoryData;
            Vector3 playerPos = _playerTransform.position;

            // 현재 영지 중심에서 플레이어 거리 계산
            Vector3 center = tm.GetTerritoryCenter();
            float dist = Vector3.Distance(playerPos, center);
            TerritoryId currentId = tm.CurrentTerritoryId;

            TerritoryState state = db.GetState(currentId);
            if (state == null) return;

            if (state.battleState == TerritoryBattleState.UnderAttack)
            {
                // 플레이어가 영지 범위를 벗어남?
                if (dist > _territoryRadius)
                {
                    state.retreatTimer += Time.deltaTime;
                    if (state.retreatTimer >= _retreatDelay)
                    {
                        // 10초 이상 떨어져 있음 → Retreat
                        state.retreatTimer = 0f; // Retreated 타이머 리셋 (Bug fix: 누적된 타이머로 인해 5초 대기 스킵 방지)
                        TransitionTo(currentId, TerritoryBattleState.Retreated);
                    }
                }
                else
                {
                    // 재진입 → 타이머 리셋
                    state.retreatTimer = 0f;
                }
            }

            if (state.battleState == TerritoryBattleState.Retreated)
            {
                // Retreat 진입 후 5초 대기 → Reinforcing
                state.retreatTimer += Time.deltaTime;
                if (state.retreatTimer >= 5f)
                {
                    TransitionTo(currentId, TerritoryBattleState.Reinforcing);
                    // 병사 리스폰 시작
                    TryReinforce(currentId, state);
                }
            }

            if (state.battleState == TerritoryBattleState.Reinforcing)
            {
                // 30초마다 병사 리스폰
                state.reinforceTimer += Time.deltaTime;
                if (state.reinforceTimer >= _reinforceInterval)
                {
                    state.reinforceTimer = 0f;
                    TryReinforce(currentId, state);
                }

                // 플레이어 재진입 감지 (UnderAttack으로 복귀)
                if (dist <= _territoryRadius)
                {
                    TransitionTo(currentId, TerritoryBattleState.UnderAttack);
                }
            }
        }

        // ================================================================
        // 공개 API
        // ================================================================

        /// <summary>전투 시작 (AlarmSystem 연동)</summary>
        public void StartBattle(TerritoryId territoryId)
        {
            TerritoryState state = TerritoryDatabase.Instance.GetState(territoryId);
            if (state == null || state.battleState != TerritoryBattleState.Peaceful) return;

            // 원래 병사 수 기록
            int guardCount = TerritoryManager.Instance?.GuardNames?.Count ?? 0;
            state.totalGuardCount = guardCount;
            state.deadGuardCount = 0;
            state.reinforcedCount = 0;
            state.retreatTimer = 0f;
            state.reinforceTimer = 0f;

            TransitionTo(territoryId, TerritoryBattleState.UnderAttack);
        }

        /// <summary>가드 사망 통보 (GuardPlaceholder 연동)</summary>
        public void OnGuardDied(TerritoryId territoryId)
        {
            TerritoryState state = TerritoryDatabase.Instance.GetState(territoryId);
            if (state == null) return;
            if (state.battleState == TerritoryBattleState.Conquered) return;

            state.deadGuardCount++;
        }

        /// <summary>가드 리스폰 큐에 추가</summary>
        public void EnqueueGuardRespawn(TerritoryId territoryId, GuardPlaceholder guard)
        {
            string key = territoryId.ToString();
            if (!_respawnQueues.ContainsKey(key))
                _respawnQueues[key] = new Queue<GuardRespawnEntry>();

            _respawnQueues[key].Enqueue(new GuardRespawnEntry
            {
                guard = guard
            });
        }

        /// <summary>영주 사망 → Conquered 전환</summary>
        public void OnLordDefeated(TerritoryId territoryId)
        {
            TransitionTo(territoryId, TerritoryBattleState.Conquered);

            // 리스폰 큐 비우기
            string key = territoryId.ToString();
            if (_respawnQueues.ContainsKey(key))
                _respawnQueues[key].Clear();
        }

        /// <summary>현재 영지의 전투 상태 반환</summary>
        public TerritoryBattleState GetBattleState(TerritoryId territoryId)
        {
            TerritoryState state = TerritoryDatabase.Instance.GetState(territoryId);
            return state?.battleState ?? TerritoryBattleState.Peaceful;
        }

        /// <summary>평화 상태 복원 (AlarmSystem 연동)</summary>
        public void RestorePeace(TerritoryId territoryId)
        {
            TerritoryState state = TerritoryDatabase.Instance.GetState(territoryId);
            if (state == null) return;

            // Conquered가 아닐 때만 Peaceful로 복원 가능
            if (state.battleState != TerritoryBattleState.Conquered)
            {
                TransitionTo(territoryId, TerritoryBattleState.Peaceful);
                state.retreatTimer = 0f;
                state.reinforceTimer = 0f;
            }
        }

        // ================================================================
        // 내부 로직
        // ================================================================

        private void TransitionTo(TerritoryId id, TerritoryBattleState newState)
        {
            TerritoryState state = TerritoryDatabase.Instance.GetState(id);
            if (state == null) return;

            TerritoryBattleState oldState = state.battleState;
            state.battleState = newState;

            Debug.Log($"[TerritoryBattleManager] 🔄 {id} 상태 전환: {oldState} → {newState}");

            OnBattleStateChanged?.Invoke(id, newState);
        }

        private void TryReinforce(TerritoryId territoryId, TerritoryState state)
        {
            if (state.battleState != TerritoryBattleState.Reinforcing) return;
            if (state.reinforcedCount >= state.deadGuardCount) return; // 전부 복원됨

            // 리스폰 큐에서 가드 꺼내서 복원
            string key = territoryId.ToString();
            if (_respawnQueues.ContainsKey(key) && _respawnQueues[key].Count > 0)
            {
                var entry = _respawnQueues[key].Dequeue();
                if (entry.guard != null)
                {
                    entry.guard.Respawn();
                    state.reinforcedCount++;
                    Debug.Log($"[TerritoryBattleManager] 🏴 {entry.guard.name} 리스폰 완료 ({state.reinforcedCount}/{state.deadGuardCount})");
                }
            }

            // 전부 복원됐으면 영주 리스폰 (아직 살아있을 경우)
            if (state.reinforcedCount >= state.deadGuardCount)
            {
                if (!state.lordDefeated && !state.lordExecuted)
                {
                    Debug.Log($"[TerritoryBattleManager] 👑 {territoryId} 모든 병사 복원 완료!");
                    // 영주는 아직 안 죽었음 → Peaceful (영주 리스폰은 직접 하지 않음, 영주는 이미 있음)
                    TransitionTo(territoryId, TerritoryBattleState.Peaceful);
                }
                else
                {
                    // 영주 사망 → Conquered
                    TransitionTo(territoryId, TerritoryBattleState.Conquered);
                }
            }
        }

        // ===== 테스트 헬퍼 =====

        /// <summary>테스트용: 전투 상태 강제 설정</summary>
        public void SetBattleStateForTest(TerritoryId id, TerritoryBattleState state)
        {
            var s = TerritoryDatabase.Instance.GetState(id);
            if (s != null) s.battleState = state;
        }

        /// <summary>테스트용: 플레이어 위치 시뮬레이션</summary>
        public void SetTestPlayerPosition(Vector3 pos)
        {
            // Create a temporary player if none exists
            if (_playerTransform == null)
            {
                var go = new GameObject("TestPlayer");
                go.tag = "Player";
                _playerTransform = go.transform;
            }
            _playerTransform.position = pos;
        }
    }
}