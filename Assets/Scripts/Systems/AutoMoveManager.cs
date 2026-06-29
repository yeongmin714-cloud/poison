using UnityEngine;
using UnityEngine.InputSystem;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 40: 자동 이동 시스템 (Auto Move)
    /// 지도에서 목표를 설정하면 플레이어가 자동으로 경로를 따라 이동합니다.
    /// CharacterController 기반으로 PlayerMovement와 동일한 이동 방식을 사용합니다.
    /// </summary>
    [DefaultExecutionOrder(-50)] // PlayerMovement(-100)보다 약간 늦게, Update 순서 보장
    public class AutoMoveManager : MonoBehaviour
    {
        [Header("Auto Move Settings")]
        [SerializeField] private float _autoMoveSpeedMultiplier = 2f; // 걷기 속도 × 2
        [SerializeField] private float _arrivalDistance = 1.5f;       // 도착 판정 거리
        [SerializeField] private float _pathUpdateInterval = 0.3f;   // 경로 갱신 간격
        [SerializeField] private float _heightOffset = 0.5f;         // 목표 지점 높이 보정 (지면 레벨)

        [Header("Path Visualization")]
        [SerializeField] private Color _pathLineColor = Color.cyan;
        [SerializeField] private Color _destinationMarkerColor = Color.green;
        [SerializeField] private float _pathLineDuration = 0.5f; // Debug.DrawLine 유지 시간

        [Header("Debug")]
        [SerializeField] private bool _showDebugLogs = false;

        [Header("Combat Detection")]
        [SerializeField] private float _recentHitWindow = 5f;      // 피격 감지 시간 범위
        [SerializeField] private float _hostileDetectionRadius = 15f; // 적대 NPC 감지 반경

        [Header("Sound Effects")]
        [SerializeField] private bool _enableAutoMoveSounds = true;

        // 싱글톤
        private static AutoMoveManager _instance;
        public static AutoMoveManager Instance => _instance;

        // 상태
        private enum AutoMoveState { Idle, Moving, Paused }
        private AutoMoveState _currentState = AutoMoveState.Idle;

        // 참조
        private PlayerMovement _playerMovement;
        private CharacterController _characterController;
        private Transform _playerTransform;

        // 목표
        private Vector3 _destination;
        private Vector3 _targetPosition; // 실제 이동할 목표 위치 (높이 보정)
        private float _walkSpeed;

        // 이동
        private Vector3 _moveDirection;
        private float _verticalVelocity;
        private float _gravity = -9.81f;

        // 경로 갱신 타이머
        private float _pathTimer = 0f;

        // WASD 감지용 키보드 캐싱 (직접 감지 — PlayerMovement와 독립적)
        private Keyboard _keyboard;

        // === Public Properties ===

        /// <summary>자동 이동 중인가?</summary>
        public bool IsMoving => _currentState == AutoMoveState.Moving;

        /// <summary>자동 이동이 일시 정지되었는가?</summary>
        public bool IsPaused => _currentState == AutoMoveState.Paused;

        /// <summary>현재 목적지 (월드 좌표)</summary>
        public Vector3 Destination => _destination;

        /// <summary>자동 이동 목표 설정 여부</summary>
        public bool HasDestination => _currentState != AutoMoveState.Idle;

        /// <summary>이동 속도 (걷기 속도 × 배수)</summary>
        public float AutoMoveSpeed => _walkSpeed * _autoMoveSpeedMultiplier;

        /// <summary>플레이어에서 목적지까지 남은 거리</summary>
        public float RemainingDistance
        {
            get
            {
                if (_playerTransform == null) return float.MaxValue;
                Vector3 flatPlayer = new Vector3(_playerTransform.position.x, 0f, _playerTransform.position.z);
                Vector3 flatTarget = new Vector3(_targetPosition.x, 0f, _targetPosition.z);
                return Vector3.Distance(flatPlayer, flatTarget);
            }
        }

        // ===== Unity Lifecycle =====

        private void Awake()
        {
            // 싱글톤 설정
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[AutoMoveManager] 중복 인스턴스 감지 — 제거합니다.");
                Destroy(gameObject);
                return;
            }
            _instance = this;

            // 키보드 캐싱
            _keyboard = Keyboard.current;

            // 플레이어 참조는 런타임에 찾기 (Awake 시점에 없을 수 있음)
        }

        private void Start()
        {
            FindPlayerReferences();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void Update()
        {
            if (_currentState != AutoMoveState.Moving) return;

            // 참조가 없으면 재탐색
            if (_playerTransform == null || _characterController == null)
            {
                if (!FindPlayerReferences())
                    return;
            }

            // WASD 입력 감지 → 즉시 취소
            if (DetectWASDInput())
            {
                CancelAutoMove("WASD 입력 감지");
                return;
            }

            // 전투 상태 진입 체크 → 일시 정지
            if (IsInCombat())
            {
                PauseAutoMove();
                return;
            }

            // 경로 갱신
            _pathTimer += Time.deltaTime;
            if (_pathTimer >= _pathUpdateInterval)
            {
                _pathTimer = 0f;
                UpdatePathVisualization();
            }

            // 이동 처리
            PerformMovement();

            // 도착 체크
            if (RemainingDistance <= _arrivalDistance)
            {
                ArriveAtDestination();
            }
        }

        private void LateUpdate()
        {
            // 경로 시각화 (LateUpdate에서 카메라 이동 후에도 그려지도록)
            if (_currentState == AutoMoveState.Moving && _playerTransform != null)
            {
                DrawPathLine();
                DrawDestinationMarker();
            }
        }

        // ===== Public API =====

        /// <summary>
        /// 자동 이동 목표를 설정합니다.
        /// </summary>
        /// <param name="worldPos">목표 월드 좌표</param>
        public void SetDestination(Vector3 worldPos)
        {
            if (_playerTransform == null)
            {
                if (!FindPlayerReferences())
                {
                    Debug.LogWarning("[AutoMoveManager] 플레이어를 찾을 수 없어 목표를 설정할 수 없습니다.");
                    NotifyAutoMoveEvent("❌ 플레이어를 찾을 수 없습니다");
                    return;
                }
            }

            // 목표 지점의 높이를 지면 레벨로 보정
            _destination = worldPos;
            _targetPosition = new Vector3(worldPos.x, worldPos.y + _heightOffset, worldPos.z);

            // 지형 높이 샘플링 (Raycast로 지면 찾기)
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(worldPos.x, worldPos.y + 100f, worldPos.z), Vector3.down, out hit, 200f))
            {
                _targetPosition.y = hit.point.y + _heightOffset;
            }

            _currentState = AutoMoveState.Moving;
            _pathTimer = 0f;

            // 이동 방향 초기화
            UpdateMoveDirection();

            if (_showDebugLogs)
                Debug.Log($"[AutoMoveManager] 🚶 자동 이동 시작! 목표: {worldPos}");

            // 사운드 재생
            PlayAutoMoveSound(SoundEffectManager.SFXType.AutoMove_Start);

            NotifyAutoMoveEvent("🚶 자동 이동 시작... [WASD로 취소]");
        }

        /// <summary>
        /// 자동 이동을 취소합니다.
        /// </summary>
        public void CancelAutoMove(string reason = "")
        {
            if (_currentState == AutoMoveState.Idle) return;

            _currentState = AutoMoveState.Idle;
            _moveDirection = Vector3.zero;

            string msg = string.IsNullOrEmpty(reason)
                ? "자동 이동 취소됨"
                : $"자동 이동 취소됨 ({reason})";

            if (_showDebugLogs)
                Debug.Log($"[AutoMoveManager] {msg}");

            // 사운드 재생
            PlayAutoMoveSound(SoundEffectManager.SFXType.AutoMove_Cancel);

            NotifyAutoMoveEvent($"⏹️ {msg}");
        }

        /// <summary>
        /// 자동 이동을 일시 정지합니다. (전투 진입 등)
        /// </summary>
        public void PauseAutoMove()
        {
            if (_currentState != AutoMoveState.Moving) return;

            _currentState = AutoMoveState.Paused;
            _moveDirection = Vector3.zero;

            if (_showDebugLogs)
                Debug.Log("[AutoMoveManager] ⏸️ 자동 이동 일시 정지 (전투 상태)");

            // 사운드 재생
            PlayAutoMoveSound(SoundEffectManager.SFXType.AutoMove_Cancel);

            NotifyAutoMoveEvent("⏸️ 전투 중 - 자동 이동 일시 정지");
        }

        /// <summary>
        /// 일시 정지된 자동 이동을 재개합니다.
        /// </summary>
        public void ResumeAutoMove()
        {
            if (_currentState != AutoMoveState.Paused) return;

            _currentState = AutoMoveState.Moving;
            UpdateMoveDirection();

            if (_showDebugLogs)
                Debug.Log("[AutoMoveManager] ▶️ 자동 이동 재개");

            // 사운드 재생
            PlayAutoMoveSound(SoundEffectManager.SFXType.AutoMove_Start);

            NotifyAutoMoveEvent("▶️ 자동 이동 재개");
        }

        // ===== Internal Methods =====

        /// <summary>
        /// 플레이어 Transform, CharacterController, PlayerMovement 참조를 찾습니다.
        /// </summary>
        private bool FindPlayerReferences()
        {
            // 1. 태그로 찾기
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj == null)
            {
                // 2. PlayerMovement로 찾기
                var pm = FindAnyObjectByType<PlayerMovement>();
                if (pm != null)
                    playerObj = pm.gameObject;
            }

            if (playerObj == null)
                return false;

            _playerTransform = playerObj.transform;
            _characterController = playerObj.GetComponent<CharacterController>();
            _playerMovement = playerObj.GetComponent<PlayerMovement>();

            if (_playerMovement != null)
                _walkSpeed = _playerMovement.WalkSpeed;
            else
                _walkSpeed = 5f; // 기본값

            return _playerTransform != null && _characterController != null;
        }

        /// <summary>
        /// 이동 방향을 목표 방향으로 업데이트합니다.
        /// </summary>
        private void UpdateMoveDirection()
        {
            if (_playerTransform == null || _currentState != AutoMoveState.Moving) return;

            Vector3 direction = _targetPosition - _playerTransform.position;
            direction.y = 0f;
            _moveDirection = direction.normalized;

            // 캐릭터를 이동 방향으로 회전
            if (_moveDirection != Vector3.zero)
            {
                _playerTransform.rotation = Quaternion.LookRotation(_moveDirection);
            }
        }

        /// <summary>
        /// 실제 이동을 수행합니다. (CharacterController.Move)
        /// </summary>
        private void PerformMovement()
        {
            if (_characterController == null) return;

            // 목표 방향으로 지속 업데이트
            UpdateMoveDirection();

            // 중력 적용
            if (_characterController.isGrounded && _verticalVelocity < 0)
            {
                _verticalVelocity = -2f;
            }
            _verticalVelocity += _gravity * Time.deltaTime;

            // 이동
            Vector3 motion = _moveDirection * AutoMoveSpeed * Time.deltaTime;
            motion.y = _verticalVelocity * Time.deltaTime;
            _characterController.Move(motion);
        }

        /// <summary>
        /// 목표 지점에 도착했을 때 호출됩니다.
        /// </summary>
        private void ArriveAtDestination()
        {
            _currentState = AutoMoveState.Idle;
            _moveDirection = Vector3.zero;

            if (_showDebugLogs)
                Debug.Log($"[AutoMoveManager] ✅ 도착했습니다! 목표: {_destination}");

            // 사운드 재생
            PlayAutoMoveSound(SoundEffectManager.SFXType.AutoMove_Complete);

            NotifyAutoMoveEvent("✅ 도착했습니다!");
        }

        /// <summary>
        /// WASD 키 입력을 감지합니다.
        /// </summary>
        private bool DetectWASDInput()
        {
            if (_keyboard == null) return false;

            return _keyboard.wKey.wasPressedThisFrame ||
                   _keyboard.aKey.wasPressedThisFrame ||
                   _keyboard.sKey.wasPressedThisFrame ||
                   _keyboard.dKey.wasPressedThisFrame ||
                   _keyboard.upArrowKey.wasPressedThisFrame ||
                   _keyboard.downArrowKey.wasPressedThisFrame ||
                   _keyboard.leftArrowKey.wasPressedThisFrame ||
                   _keyboard.rightArrowKey.wasPressedThisFrame;
        }

        /// <summary>
        /// 전투 상태 여부를 확인합니다.
        /// 5초 이내 피격 or 적대적 NPC(가드/몬스터)가 근처(15m)에 있으면 true 반환.
        /// </summary>
        private bool IsInCombat()
        {
            // 1) 최근 피격 여부 확인 (PlayerHealth)
            if (PlayerHealth.Instance != null && PlayerHealth.Instance.WasRecentlyHit(_recentHitWindow))
            {
                if (_showDebugLogs)
                    Debug.Log("[AutoMoveManager] ⚔️ 전투 감지: 최근 피격됨");
                return true;
            }

            // 2) 플레이어 주변 15m 이내의 적대적 가드 확인 (GuardHostilitySystem)
            GameObject playerObj = _playerTransform != null ? _playerTransform.gameObject : null;
            if (playerObj == null)
                playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                Vector3 playerPos = playerObj.transform.position;

                // 2a) GuardPlaceholder 중 Hostile 상태인 것 확인
                var guards = FindObjectsByType<GuardPlaceholder>(FindObjectsSortMode.None);
                float sqrDetectRadius = _hostileDetectionRadius * _hostileDetectionRadius;
                foreach (var guard in guards)
                {
                    if (guard == null || !guard.IsAlive) continue;
                    float sqrDist = (guard.transform.position - playerPos).sqrMagnitude;
                    if (sqrDist > sqrDetectRadius) continue;

                    // GuardHostilitySystem을 통해 적대 상태 확인
                    bool isHostile = false;
                    if (GuardHostilitySystem.Instance != null)
                        isHostile = GuardHostilitySystem.Instance.IsHostile(guard);
                    else if (guard.IsInCombat)
                        isHostile = true;

                    if (isHostile)
                    {
                        if (_showDebugLogs)
                            Debug.Log($"[AutoMoveManager] ⚔️ 전투 감지: 적대 가드 {guard.GuardName} 근접");
                        return true;
                    }
                }

                // 2b) MonsterAggroSystem — 플레이어를 대상으로 하는 몬스터 확인
                if (MonsterAggroSystem.Instance != null)
                {
                    foreach (var monster in MonsterAggroSystem.Instance.AllMonsters)
                    {
                        if (monster == null) continue;
                        var mb = monster as MonoBehaviour;
                        if (mb == null || mb.gameObject == null) continue;
                        float sqrDist = (mb.transform.position - playerPos).sqrMagnitude;
                        if (sqrDist > sqrDetectRadius) continue;
                        if (monster.IsInCombat && monster.AggroTarget != null)
                        {
                            // AggroTarget이 플레이어인지 확인
                            GameObject aggroTarget = monster.AggroTarget;
                            if (aggroTarget == playerObj)
                            {
                                if (_showDebugLogs)
                                    Debug.Log("[AutoMoveManager] ⚔️ 전투 감지: 몬스터가 플레이어를 어그로");
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 경로 시각화를 업데이트합니다. (점선 경로)
        /// </summary>
        private void UpdatePathVisualization()
        {
            // 경로 시각화 포인트 갱신 (원하는 경우 경로 탐색 결과를 여기에 저장)
        }

        /// <summary>
        /// 플레이어에서 목적지까지 경로 선을 그립니다. (Debug.DrawLine)
        /// </summary>
        private void DrawPathLine()
        {
            if (_playerTransform == null) return;

            Vector3 start = _playerTransform.position + Vector3.up * 0.5f;
            Vector3 end = _targetPosition + Vector3.up * 0.5f;

            // 중간 지점을 약간씩 흔들어 점선 느낌
            int segments = 10;
            for (int i = 0; i < segments; i++)
            {
                float t1 = (float)i / segments;
                float t2 = (float)(i + 1) / segments;

                Vector3 p1 = Vector3.Lerp(start, end, t1);
                Vector3 p2 = Vector3.Lerp(start, end, t2);

                // 약간의 높이 변동으로 점선 느낌
                float heightOffset = Mathf.Sin(t1 * Mathf.PI * 4) * 0.3f;
                p1.y += heightOffset;
                p2.y += Mathf.Sin(t2 * Mathf.PI * 4) * 0.3f;

                Debug.DrawLine(p1, p2, _pathLineColor, _pathLineDuration);
            }
        }

        /// <summary>
        /// 목적지 마커를 그립니다.
        /// </summary>
        private void DrawDestinationMarker()
        {
            Vector3 markerPos = _targetPosition + Vector3.up * 0.5f;

            // 십자 마커
            float markerSize = 0.5f;
            Debug.DrawLine(markerPos + Vector3.left * markerSize, markerPos + Vector3.right * markerSize,
                _destinationMarkerColor, _pathLineDuration);
            Debug.DrawLine(markerPos + Vector3.forward * markerSize, markerPos + Vector3.back * markerSize,
                _destinationMarkerColor, _pathLineDuration);

            // 수직선 (하늘 방향)
            Debug.DrawLine(markerPos, markerPos + Vector3.up * 2f, _destinationMarkerColor, _pathLineDuration);
        }

        /// <summary>
        /// 자동 이동 관련 알림을 발생시킵니다. AutoMoveUI가 이 메시지를 표시합니다.
        /// </summary>
        private void NotifyAutoMoveEvent(string message)
        {
            // 정적 이벤트로 알림 — AutoMoveUI가 구독
            OnAutoMoveNotification?.Invoke(message);
        }

        /// <summary>
        /// 자동 이동 사운드를 재생합니다.
        /// _enableAutoMoveSounds가 false이면 재생하지 않습니다.
        /// </summary>
        private void PlayAutoMoveSound(SoundEffectManager.SFXType type)
        {
            if (!_enableAutoMoveSounds) return;
            if (SoundEffectManager.Instance != null)
                SoundEffectManager.Instance.PlaySFX(type);
        }

        // ===== Events =====

        /// <summary>
        /// 자동 이동 알림 이벤트 (AutoMoveUI에서 구독)
        /// </summary>
        public static event System.Action<string> OnAutoMoveNotification;
    }
}