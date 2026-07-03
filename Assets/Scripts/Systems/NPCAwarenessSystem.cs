using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 34: NPC 경계/수색 AI.
    /// 5단계 상태 머신: 평화 → 의심 → 수색 → 발각 → 경계
    /// </summary>
    public class NPCAwarenessSystem : MonoBehaviour
    {
        /// <summary>
        /// NPC 인식 상태 5단계.
        /// </summary>
        public enum AwarenessState
        {
            Peace,      // 평화 — 기본 상태
            Suspicious, // 의심 — 소리/움직임 감지
            Searching,  // 수색 — 주변 탐색
            Detected,   // 발각 — 전투 상태
            Alert       // 경계 — 수색 후 일시적 경계 강화
        }

        [Header("Awareness Settings")]
        [SerializeField] private AwarenessState _currentState = AwarenessState.Peace;
        [SerializeField] private float _suspiciousDuration = 5f;
        [SerializeField] private float _searchingDuration = 15f;
        [SerializeField] private float _searchingRadius = 10f;
        [SerializeField] private float _alertDuration = 30f;
        [SerializeField] private float _alertDetectionRangeMultiplier = 1.5f;
        [SerializeField] private float _alertFOVMultiplier = 1.3f;
        [SerializeField] private int _npcLevel = 1;

        [Header("Detection Settings")]
        [SerializeField] private float _baseSightRange = 12f;
        [SerializeField] private float _baseFOV = 60f;
        [SerializeField] private float _hearRange = 8f; // 소리 감지 범위

        [Header("Noise Propagation")]
        [SerializeField] private float _noiseHearRange = 12f; // 달리기/공격 소음 전파 범위
        [SerializeField] private float _explosionHearRange = 25f; // 폭탄 소음 전파 범위

        [Header("References")]
        [SerializeField] private Transform _headTransform; // 시선 방향

        // 내부 상태
        private float _stateTimer = 0f;
        private Vector3 _suspicionOrigin; // 의심 발생 위치
        private GameObject _detectedTarget;
        private Coroutine _stateCoroutine;

        // 시야/감지 거리 캐시 (경계 상태 보정)
        private float _currentSightRange;
        private float _currentFOV;

        // 주변 NPC 알림용
        private static readonly HashSet<NPCAwarenessSystem> _allNPCs = new HashSet<NPCAwarenessSystem>();

        // ===== Public Properties =====
        public AwarenessState CurrentAwarenessState => _currentState;
        public int NPCLevel => _npcLevel;
        public bool IsActive => gameObject.activeInHierarchy && enabled;
        public float CurrentSightRange => _currentSightRange;
        public float CurrentFOV => _currentFOV;
        public Vector3 SuspicionOrigin => _suspicionOrigin;
        public GameObject DetectedTarget => _detectedTarget;

        // ===== Events =====
        public event System.Action<AwarenessState> OnStateChanged;
        public event System.Action<Vector3> OnAlertTriggered; // SetAlert 트리거 시

        private void OnEnable()
        {
            _allNPCs.Add(this);
            InitializeState();
        }

        private void OnDisable()
        {
            _allNPCs.Remove(this);
        }

        private void Start()
        {
            if (_headTransform == null)
                _headTransform = transform;

            InitializeState();
        }

        private void InitializeState()
        {
            _currentState = AwarenessState.Peace;
            _stateTimer = 0f;
            _detectedTarget = null;
            _currentSightRange = _baseSightRange;
            _currentFOV = _baseFOV;

            if (_stateCoroutine != null)
                StopCoroutine(_stateCoroutine);
        }

        private void Update()
        {
            UpdateStateMachine(Time.deltaTime);
        }

        /// <summary>
        /// 상태 머신 업데이트.
        /// </summary>
        private void UpdateStateMachine(float deltaTime)
        {
            switch (_currentState)
            {
                case AwarenessState.Peace:
                    // 평화 상태: 특별한 처리 없음
                    break;

                case AwarenessState.Suspicious:
                    _stateTimer -= deltaTime;
                    // 의심 위치 응시
                    LookAtPosition(_suspicionOrigin);

                    if (_stateTimer <= 0f)
                    {
                        // 5초 후 평화 복귀
                        SetState(AwarenessState.Peace);
                    }
                    break;

                case AwarenessState.Searching:
                    _stateTimer -= deltaTime;

                    if (_stateTimer <= 0f)
                    {
                        // 15초 후 경계 상태로 전환
                        SetState(AwarenessState.Alert);
                    }
                    break;

                case AwarenessState.Detected:
                    // 발각: 전투 상태 — 외부에서 해제될 때까지 유지
                    // 전투 종료 시 Alert 상태로 전환
                    break;

                case AwarenessState.Alert:
                    _stateTimer -= deltaTime;

                    if (_stateTimer <= 0f)
                    {
                        // 30초 후 평화 복귀
                        SetState(AwarenessState.Peace);
                    }
                    break;
            }
        }

        /// <summary>
        /// 상태 전환.
        /// </summary>
        private void SetState(AwarenessState newState)
        {
            if (_currentState == newState) return;

            AwarenessState prevState = _currentState;
            _currentState = newState;

            // 상태 진입 처리
            switch (newState)
            {
                case AwarenessState.Peace:
                    _stateTimer = 0f;
                    _detectedTarget = null;
                    _currentSightRange = _baseSightRange;
                    _currentFOV = _baseFOV;
                    if (_stateCoroutine != null)
                        StopCoroutine(_stateCoroutine);
                    break;

                case AwarenessState.Suspicious:
                    _stateTimer = _suspiciousDuration;
                    break;

                case AwarenessState.Searching:
                    _stateTimer = _searchingDuration;
                    // 수색 코루틴 시작
                    if (_stateCoroutine != null)
                        StopCoroutine(_stateCoroutine);
                    _stateCoroutine = StartCoroutine(SearchCoroutine());
                    break;

                case AwarenessState.Detected:
                    _currentSightRange = _baseSightRange;
                    _currentFOV = _baseFOV;
                    // 주변 NPC 호출
                    AlertNearbyNPCs();
                    break;

                case AwarenessState.Alert:
                    _stateTimer = _alertDuration;
                    _currentSightRange = _baseSightRange * _alertDetectionRangeMultiplier;
                    _currentFOV = _baseFOV * _alertFOVMultiplier;
                    break;
            }

            OnStateChanged?.Invoke(newState);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[NPCAwarenessSystem] {gameObject.name}: {prevState} → {newState}");
#endif
        }

        /// <summary>
        /// 의심 상태 시작 (소리/움직임 감지).
        /// </summary>
        public void SetSuspicious(Vector3 origin)
        {
            _suspicionOrigin = origin;

            if (_currentState == AwarenessState.Peace || _currentState == AwarenessState.Suspicious)
            {
                SetState(AwarenessState.Suspicious);
            }
            else if (_currentState == AwarenessState.Searching)
            {
                // 수색 중 추가 의심 → 수색 시간 연장
                _stateTimer = Mathf.Max(_stateTimer, _searchingDuration);
                _suspicionOrigin = origin;
            }
        }

        /// <summary>
        /// 수색 상태 시작.
        /// </summary>
        public void SetSearching()
        {
            if (_currentState == AwarenessState.Peace || _currentState == AwarenessState.Suspicious)
            {
                SetState(AwarenessState.Searching);
            }
        }

        /// <summary>
        /// 발각 상태 시작 (플레이어 발견).
        /// </summary>
        public void SetDetected(GameObject target)
        {
            _detectedTarget = target;

            if (_currentState != AwarenessState.Detected)
            {
                SetState(AwarenessState.Detected);
            }

            // 이미 발각 상태여도 타겟 갱신
            _detectedTarget = target;
        }

        /// <summary>
        /// 전투 종료 시 호출 (발각 → 경계).
        /// </summary>
        public void OnCombatEnd()
        {
            if (_currentState == AwarenessState.Detected)
            {
                SetState(AwarenessState.Alert);
            }
        }

        /// <summary>
        /// 강제로 평화 상태 복귀 (진정제 등).
        /// </summary>
        public void ForcePeace()
        {
            SetState(AwarenessState.Peace);
        }

        /// <summary>
        /// SetAlert — 특정 위치에서 경보를 울려 주변 NPC를 Detected 상태로 전환.
        /// MonsterAggroSystem.SetAlertAllNearby와 연동 가능.
        /// </summary>
        /// <param name="position">경보 발생 위치</param>
        /// <param name="radius">영향 반경</param>
        public static void SetAlert(Vector3 position, float radius)
        {
            foreach (var npc in _allNPCs)
            {
                if (!npc.IsActive) continue;
                float dist = Vector3.Distance(npc.transform.position, position);
                if (dist <= radius)
                {
                    // 플레이어 탐색
                    GameObject player = GameObject.FindGameObjectWithTag("Player");
                    if (player != null)
                    {
                        npc.SetDetected(player);
                    }
                    npc.OnAlertTriggered?.Invoke(position);
                }
            }

            Debug.Log($"[NPCAwarenessSystem] SetAlert at {position}, radius={radius}");
        }

        /// <summary>
        /// 소음 전파: 달리기/공격/폭탄 등 소음 발생 시 주변 NPC를 Suspicious 상태로 전환.
        /// 외부 시스템(PlayerMovement, AttackSystem, 폭탄)에서 호출.
        /// </summary>
        /// <param name="noisePosition">소음 발생 위치</param>
        /// <param name="noiseType">소음 종류 ("footstep" / "attack" / "explosion")</param>
        public static void PropagateNoise(Vector3 noisePosition, string noiseType)
        {
            float range;
            switch (noiseType)
            {
                case "explosion":
                    range = 25f;
                    break;
                case "attack":
                    range = 12f;
                    break;
                case "footstep":
                default:
                    range = 8f;
                    break;
            }

            foreach (var npc in _allNPCs)
            {
                if (!npc.IsActive) continue;
                float dist = Vector3.Distance(npc.transform.position, noisePosition);
                if (dist <= range)
                {
                    npc.SetSuspicious(noisePosition);
                }
            }
        }

        /// <summary>
        /// 방독면/안개 효과 적용: NPC 시야를 반으로 감소.
        /// GasSprayer 등에서 호출.
        /// </summary>
        /// <param name="active">true=시야 감소 적용, false=복원</param>
        public void ApplyFogEffect(bool active)
        {
            if (active)
            {
                _currentSightRange = _baseSightRange * 0.5f;
                _currentFOV = _baseFOV * 0.5f;
            }
            else
            {
                _currentSightRange = _baseSightRange;
                _currentFOV = _baseFOV;
            }

            Debug.Log($"[NPCAwarenessSystem] {gameObject.name} FogEffect={(active ? "ON" : "OFF")} SightRange={_currentSightRange} FOV={_currentFOV}");
        }

        /// <summary>
        /// 모든 NPC에 방독면/안개 효과 일괄 적용.
        /// GasSprayer 등에서 호출.
        /// </summary>
        public static void ApplyFogEffectToAll(bool active)
        {
            foreach (var npc in _allNPCs)
            {
                if (npc.IsActive)
                    npc.ApplyFogEffect(active);
            }
        }

        /// <summary>
        /// 수색 코루틴 — 주변 10m 탐색.
        /// </summary>
        private IEnumerator SearchCoroutine()
        {
            float searchTimer = _searchingDuration;
            float patrolInterval = 1.5f;

            while (searchTimer > 0f && _currentState == AwarenessState.Searching)
            {
                // 주변 플레이어 감지 시도
                DetectNearbyPlayer();

                // 랜덤 방향 응시 (수색 행동)
                LookAtRandomDirection();

                yield return new WaitForSeconds(patrolInterval);
                searchTimer -= patrolInterval;
            }

            // 수색 종료 → 경계
            if (_currentState == AwarenessState.Searching)
            {
                SetState(AwarenessState.Alert);
            }
        }

        /// <summary>
        /// 주변 플레이어 감지.
        /// </summary>
        private void DetectNearbyPlayer()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            float dist = Vector3.Distance(transform.position, player.transform.position);

            // 감지 범위 내 확인
            if (dist <= _currentSightRange)
            {
                Vector3 dirToPlayer = (player.transform.position - transform.position).normalized;
                float angle = Vector3.Angle(GetLookDirection(), dirToPlayer);

                // 시야각 내 확인
                if (angle < _currentFOV * 0.5f)
                {
                    // Raycast 차단 확인
                    if (!Physics.Raycast(_headTransform.position, dirToPlayer, out RaycastHit hit, dist))
                    {
                        // 플레이어 발견!
                        SetDetected(player);
                        return;
                    }

                    // 차단된 오브젝트가 플레이어 자신인지 확인
                    if (hit.collider.gameObject == player || hit.collider.GetComponentInParent<StealthSystem>() != null)
                    {
                        SetDetected(player);
                    }
                }
            }
        }

        /// <summary>
        /// 주변 NPC에게 발각 알림.
        /// </summary>
        private void AlertNearbyNPCs()
        {
            if (_detectedTarget == null) return;

            foreach (var npc in _allNPCs)
            {
                if (npc == this || !npc.IsActive) continue;
                float dist = Vector3.Distance(npc.transform.position, transform.position);
                if (dist <= _searchingRadius)
                {
                    npc.SetDetected(_detectedTarget);
                }
            }
        }

        /// <summary>
        /// 특정 위치 응시.
        /// </summary>
        private void LookAtPosition(Vector3 position)
        {
            Vector3 dir = (position - transform.position).normalized;
            dir.y = 0f;
            if (dir.magnitude > 0.1f)
            {
                transform.rotation = Quaternion.LookRotation(dir);
            }
        }

        /// <summary>
        /// 랜덤 방향 응시 (수색 행동).
        /// </summary>
        private void LookAtRandomDirection()
        {
            float randomAngle = Random.Range(-90f, 90f);
            transform.Rotate(Vector3.up, randomAngle);
        }

        /// <summary>
        /// 현재 시선 방향 벡터.
        /// </summary>
        private Vector3 GetLookDirection()
        {
            if (_headTransform != null)
                return _headTransform.forward;
            return transform.forward;
        }

        /// <summary>
        /// 플레이어가 NPC 뒤에 있는지 확인 (암살 조건).
        /// </summary>
        public bool IsPlayerBehind(Vector3 playerPosition)
        {
            Vector3 dirToPlayer = (playerPosition - transform.position).normalized;
            float dot = Vector3.Dot(transform.forward, dirToPlayer);
            return dot < -0.3f; // 뒤쪽 120도 범위
        }

        /// <summary>
        /// 레벨 설정 (외부에서 호출).
        /// </summary>
        public void SetLevel(int level)
        {
            _npcLevel = Mathf.Max(1, level);
        }

        private void OnDestroy()
        {
            if (_stateCoroutine != null)
                StopCoroutine(_stateCoroutine);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // 시야 범위
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _currentSightRange);

            // 시야 원뿔 (대략적)
            Vector3 forward = GetLookDirection();
            float halfFOV = _currentFOV * 0.5f;
            Vector3 leftDir = Quaternion.Euler(0, -halfFOV, 0) * forward;
            Vector3 rightDir = Quaternion.Euler(0, halfFOV, 0) * forward;

            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawLine(transform.position, transform.position + leftDir * _currentSightRange);
            Gizmos.DrawLine(transform.position, transform.position + rightDir * _currentSightRange);

            // 수색 범위
            if (_currentState == AwarenessState.Searching)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
                Gizmos.DrawWireSphere(transform.position, _searchingRadius);
            }

            // 상태 텍스트
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2.5f, _currentState.ToString());
        }
#endif
    }
}