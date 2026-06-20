using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [5.3.6] 병사 부활 시스템.
    /// GuardPlaceholder.Die() → 사망 등록 → 일정 시간 후 부활.
    /// 부활 시 HP = maxHP × 10%.
    /// 부활 위치: 소속 영지 입구 또는 사망 위치.
    /// </summary>
    public class GuardResurrectionSystem : MonoBehaviour
    {
        [Header("설정")]
        [SerializeField] private float _respawnDelay = 30f; // 부활 대기 시간 (초)
        [SerializeField] private float _respawnHPPercent = 0.1f; // 부활 HP 비율

        // 사망 대기열
        private readonly List<DeadGuardEntry> _deadGuards = new List<DeadGuardEntry>();

        public static GuardResurrectionSystem Instance { get; private set; }

        private struct DeadGuardEntry
        {
            public GuardPlaceholder guard;       // 비활성화된 GuardPlaceholder
            public TerritoryId? territoryId;     // 소속 영지 (null 가능)
            public Vector3 deathPosition;        // 사망 위치
            public float deathTime;              // 사망 시간 (Time.time)
            public bool resurrected;             // 부활 완료 여부
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            // GuardPlaceholder 사망 이벤트 구독
            GuardPlaceholder.OnAnyGuardDied += OnGuardDied;
        }

        private void OnDisable()
        {
            GuardPlaceholder.OnAnyGuardDied -= OnGuardDied;
        }

        private void Update()
        {
            // 부활 타이머 체크
            ProcessResurrection();
        }

        /// <summary>
        /// GuardPlaceholder.Die()에서 호출 — 사망 병사 등록
        /// </summary>
        private void OnGuardDied(GuardPlaceholder guard)
        {
            if (guard == null) return;

            // 이미 등록된 경우 중복 방지
            foreach (var entry in _deadGuards)
            {
                if (entry.guard == guard && !entry.resurrected)
                    return;
            }

            // 소속 영지 찾기
            TerritoryId? territoryId = FindGuardTerritory(guard);

            var newEntry = new DeadGuardEntry
            {
                guard = guard,
                territoryId = territoryId,
                deathPosition = guard.transform.position,
                deathTime = Time.time,
                resurrected = false
            };

            _deadGuards.Add(newEntry);

            Debug.Log($"[GuardResurrection] 💀 {guard.GuardName} 사망 등록. {_respawnDelay}초 후 부활 예정. 영지: {territoryId}");
        }

        /// <summary>
        /// 매 프레임 부활 대기열 확인 — 일정 시간 경과 시 부활
        /// </summary>
        private void ProcessResurrection()
        {
            if (_deadGuards.Count == 0) return;

            float now = Time.time;

            for (int i = _deadGuards.Count - 1; i >= 0; i--)
            {
                var entry = _deadGuards[i];

                if (entry.resurrected)
                {
                    _deadGuards.RemoveAt(i);
                    continue;
                }

                // 비활성화된 오브젝트가 Destroy되었거나 null이면 제거
                if (entry.guard == null || entry.guard.gameObject == null)
                {
                    _deadGuards.RemoveAt(i);
                    Debug.LogWarning($"[GuardResurrection] ⚠️ 사망 병사 오브젝트 사라짐, 부활 불가");
                    continue;
                }

                // 부활 시간 체크
                if (now - entry.deathTime >= _respawnDelay)
                {
                    PerformResurrection(entry, i);
                }
            }
        }

        /// <summary>
        /// 실제 부활 처리
        /// </summary>
        private void PerformResurrection(DeadGuardEntry entry, int index)
        {
            GuardPlaceholder guard = entry.guard;
            if (guard == null) return;

            // 부활 위치 결정
            Vector3 respawnPosition = DetermineRespawnPosition(entry);

            // 위치 복원
            guard.transform.position = respawnPosition;

            // 부활 처리 (HP 10%)
            guard.Resurrect(_respawnHPPercent);

            // GuardManager에 재등록
            if (GuardManager.Instance != null && entry.territoryId.HasValue)
            {
                GuardManager.Instance.RegisterGuard(entry.territoryId.Value, guard);
            }

            // 상태 갱신
            var updatedEntry = entry;
            updatedEntry.resurrected = true;
            _deadGuards[index] = updatedEntry;

            Debug.Log($"[GuardResurrection] 🔄 {guard.GuardName} 부활! 위치:{respawnPosition} HP:{guard.HP:F1}/{guard.MaxHP:F1}");
        }

        /// <summary>
        /// 부활 위치 결정: 소속 영지 입구 우선, 실패 시 사망 위치
        /// </summary>
        private Vector3 DetermineRespawnPosition(DeadGuardEntry entry)
        {
            // 소속 영지의 입구(중심점) 위치 시도
            if (entry.territoryId.HasValue)
            {
                Vector3 territoryPos = GetTerritoryEntrance(entry.territoryId.Value);
                if (territoryPos != Vector3.zero)
                {
                    // 영지 중심 근처 랜덤 오프셋
                    Vector2 offset = Random.insideUnitCircle * 3f;
                    return territoryPos + new Vector3(offset.x, 0, offset.y);
                }
            }

            // 영지 정보 없으면 사망 위치 사용
            return entry.deathPosition;
        }

        /// <summary>
        /// 영지 입구(중심점) 위치 반환
        /// </summary>
        private Vector3 GetTerritoryEntrance(TerritoryId territoryId)
        {
            // TerritoryManager에서 영지 중심점 가져오기
            if (TerritoryManager.Instance != null)
            {
                return TerritoryManager.Instance.GetTerritoryCenter();
            }

            // Fallback: TerritoryDatabase에서 GateGuardPlaceholder 찾기
            var gateGuards = Object.FindObjectsOfType<GateGuardPlaceholder>();
            foreach (var gate in gateGuards)
            {
                if (gate.Nation == territoryId.nation && gate.TerritoryIndex == territoryId.index)
                {
                    return gate.transform.position;
                }
            }

            return Vector3.zero;
        }

        /// <summary>
        /// 병사의 소속 영지 찾기
        /// </summary>
        private TerritoryId? FindGuardTerritory(GuardPlaceholder guard)
        {
            if (GuardManager.Instance == null) return null;

            var db = TerritoryDatabase.Instance;
            if (db == null) return null;

            foreach (var def in db.GetAllDefinitions())
            {
                var guardsInTerritory = GuardManager.Instance.GetGuardsInTerritory(def.id);
                if (guardsInTerritory.Contains(guard))
                {
                    return def.id;
                }
            }

            return null;
        }

        // ===== 퍼블릭 API =====

        /// <summary>
        /// 현재 사망 대기열에 있는 병사 수
        /// </summary>
        public int DeadGuardCount => _deadGuards.Count;

        /// <summary>
        /// 특정 병사가 사망 대기열에 있는지 확인
        /// </summary>
        public bool IsDeadAndPending(GuardPlaceholder guard)
        {
            foreach (var entry in _deadGuards)
            {
                if (entry.guard == guard && !entry.resurrected)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 부활 대기 시간 설정 (테스트/런타임)
        /// </summary>
        public float RespawnDelay { get => _respawnDelay; set => _respawnDelay = value; }

        /// <summary>
        /// 부활 HP 비율 설정 (테스트/런타임)
        /// </summary>
        public float RespawnHPPercent { get => _respawnHPPercent; set => _respawnHPPercent = Mathf.Clamp01(value); }

        /// <summary>
        /// 강제 부활 처리 (테스트용)
        /// </summary>
        public void ForceResurrectAll()
        {
            for (int i = _deadGuards.Count - 1; i >= 0; i--)
            {
                var entry = _deadGuards[i];
                if (!entry.resurrected && entry.guard != null)
                {
                    PerformResurrection(entry, i);
                }
            }
        }

        /// <summary>
        /// 특정 병사 강제 부활 (테스트용)
        /// </summary>
        public void ForceResurrect(GuardPlaceholder guard)
        {
            for (int i = 0; i < _deadGuards.Count; i++)
            {
                var entry = _deadGuards[i];
                if (entry.guard == guard && !entry.resurrected)
                {
                    PerformResurrection(entry, i);
                    return;
                }
            }
        }

        /// <summary>
        /// 모든 사망 대기열 초기화 (테스트용)
        /// </summary>
        public void ClearDeadGuards()
        {
            _deadGuards.Clear();
        }
    }
}