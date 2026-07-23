using UnityEngine;
using UnityEngine.InputSystem;
using ProjectName.Core;
using ProjectName.Systems.Animation.Procedural;
using ProjectName.Systems.Animation.Neural;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// 말/탈것 시스템 (Phase 4)
    /// - 말 NPC 태그/레이어: "Mount" 또는 "Horse"
    /// - 말 근처(3m)에서 E키 → 탑승/하차 토글
    /// - 탑승 중: 이동속도 2.5배, 점프 불가, 시야 Top-down 유지
    /// - 말 소환: mount_token 아이템 사용 시 가장 가까운 길바닥에 말 스폰
    /// - 말 체력: 100 max, 질주 시 초당 5 감소, 정지 시 초당 10 회복
    /// - Shift 질주 (속도 4배, HP 소모)
    /// - 플레이어 웅크리기(Ctrl) 탑승 불가
    /// </summary>
    [DefaultExecutionOrder(-80)] // PlayerMovement(-100)보다 늦게, AutoMoveManager(-50)보다 먼저
    public class MountSystem : MonoBehaviour
    {
        public static MountSystem Instance { get; private set; }

        [Header("말 설정")]
        [SerializeField] private GameObject _horsePrefab;
        [SerializeField] private float _mountRange = 3f;
        [SerializeField] private float _walkSpeedMultiplier = 2.5f;
        [SerializeField] private float _sprintSpeedMultiplier = 4f;

        [Header("말 체력")]
        [SerializeField] private float _maxMountHP = 100f;
        [SerializeField] private float _sprintHPCostPerSec = 5f;
        [SerializeField] private float _idleHPRegenPerSec = 10f;

        [Header("쿨다운")]
        [SerializeField] private float _hpZeroDismountCooldown = 30f;
        [SerializeField] private float _deathRespawnCooldown = 60f;

        // 참조
        private PlayerMovement _playerMovement;
        private CharacterController _characterController;
        private TopDownCameraController _cameraController;
        private Keyboard _keyboard;

        // 상태
        private bool _isMounted = false;
        private GameObject _currentHorse;
        private MountSpawner _currentHorseSpawner;
        private float _mountHP;
        private bool _isSprinting = false;
        private float _dismountCooldownTimer = 0f;
        private bool _isOnCooldown = false;

        // 입력 상태 트래킹
        // private bool _sprintKeyWasPressed = false;

        // 애니메이션
        private RigAnimationController _rigAnim;
        private NeuralAnimationController _neuralAnim;

        // ===== Public Properties =====

        /// <summary>현재 탑승 중인가?</summary>
        public bool IsMounted => _isMounted;

        /// <summary>현재 질주 중인가?</summary>
        public bool IsSprinting => _isSprinting && _isMounted;

        /// <summary>현재 말 체력</summary>
        public float MountHP => _mountHP;

        /// <summary>최대 말 체력</summary>
        public float MaxMountHP => _maxMountHP;

        /// <summary>말 체력 비율 (0~1)</summary>
        public float MountHPRatio => _maxMountHP > 0f ? Mathf.Clamp01(_mountHP / _maxMountHP) : 0f;

        /// <summary>현재 탑승 중인 말 오브젝트</summary>
        public GameObject CurrentHorse => _currentHorse;

        /// <summary>현재 말의 MountSpawner</summary>
        public MountSpawner CurrentHorseSpawner => _currentHorseSpawner;

        /// <summary>쿨다운 상태인가? (HP 0으로 강제 하차 후)</summary>
        public bool IsOnCooldown => _isOnCooldown;

        /// <summary>현재 이동 속도 배수 (일반 2.5, 질주 4)</summary>
        public float CurrentSpeedMultiplier => _isSprinting ? _sprintSpeedMultiplier : _walkSpeedMultiplier;

        /// <summary>속도 상태 문자열 (걷기/달리기/질주)</summary>
        public string SpeedStateText
        {
            get
            {
                if (!_isMounted) return "땅";
                if (_isSprinting) return "질주";
                return "달리기";
            }
        }

        // ===== Events =====

        /// <summary>탑승/하차 상태 변경 시 (true=탑승)</summary>
        public event System.Action<bool> OnMountStateChanged;

        /// <summary>말 HP 변경 시 (currentHP, maxHP)</summary>
        public event System.Action<float, float> OnMountHPChanged;

        /// <summary>말 사망 시</summary>
        public event System.Action OnMountDied;

        // ===== Unity Lifecycle =====

        private void Awake()
        {
            // 싱글톤 설정
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[MountSystem] 중복 인스턴스 감지 — 제거합니다.");
                Destroy(gameObject);
                return;
            }
            _instance = this;

            _keyboard = Keyboard.current;
            _mountHP = _maxMountHP;
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
            if (_keyboard == null) return;

            // 참조가 없으면 재탐색
            if (_playerMovement == null || _characterController == null)
            {
                if (!FindPlayerReferences())
                    return;
            }

            // 쿨다운 타이머
            if (_isOnCooldown)
            {
                _dismountCooldownTimer -= Time.deltaTime;
                if (_dismountCooldownTimer <= 0f)
                {
                    _isOnCooldown = false;
                    _dismountCooldownTimer = 0f;
                }
            }

            if (_isMounted)
            {
                // 탑승 중: 말 이동 처리 + HP 관리
                HandleMountedMovement();
                HandleMountHP();
                HandleDismountInput();

                // 질주 중 오토무브 취소
                if (_isSprinting && AutoMoveManager.Instance != null && AutoMoveManager.Instance.IsMoving)
                {
                    AutoMoveManager.Instance.CancelAutoMove("말 질주");
                }
            }
            else
            {
                // 비탑승 중: E키 상호작용 (말 근처 탑승)
                HandleMountInput();

                // 말 소환 (mount_token)
                HandleSummonInput();
            }
        }

        /// <summary>
        /// 플레이어 Transform, CharacterController, PlayerMovement, 카메라 참조를 찾습니다.
        /// </summary>
        private bool FindPlayerReferences()
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj == null)
            {
                var pm = FindAnyObjectByType<PlayerMovement>();
                if (pm != null)
                    playerObj = pm.gameObject;
            }

            if (playerObj == null)
                return false;

            _playerMovement = playerObj.GetComponent<PlayerMovement>();
            _characterController = playerObj.GetComponent<CharacterController>();
            _rigAnim = playerObj.GetComponent<RigAnimationController>();
            _neuralAnim = playerObj.GetComponent<NeuralAnimationController>();

            // 카메라 참조
            if (_cameraController == null)
            {
                _cameraController = FindAnyObjectByType<TopDownCameraController>();
            }

            if (_playerMovement == null)
            {
                Debug.LogWarning("[MountSystem] PlayerMovement를 찾을 수 없습니다.");
            }

            return true;
        }

        // ===== 말 찾기 및 탑승 =====

        /// <summary>
        /// E키 입력 → 근처 말 찾기 → 탑승
        /// </summary>
        private void HandleMountInput()
        {
            if (_keyboard == null) return;

            // Ctrl (웅크리기) 상태면 탑승 불가
            if (StealthSystem.Instance != null && StealthSystem.Instance.IsStealthed)
                return;

            if (!_keyboard.eKey.wasPressedThisFrame)
                return;

            // 쿨다운 중이면 무시
            if (_isOnCooldown)
            {
                Debug.Log($"[MountSystem] ⏳ 말 소환 쿨다운 중... ({_dismountCooldownTimer:F1}초 남음)");
                return;
            }

            // 플레이어 위치 기준 반경 검색
            Collider[] hits = Physics.OverlapSphere(_playerMovement.transform.position, _mountRange);
            foreach (var hit in hits)
            {
                if (hit == null) continue;

                // 태그 또는 레이어 확인: "Mount" 또는 "Horse"
                if (hit.CompareTag("Mount") || hit.CompareTag("Horse") ||
                    hit.gameObject.layer == LayerMask.NameToLayer("Mount") ||
                    hit.gameObject.layer == LayerMask.NameToLayer("Horse"))
                {
                    TryMount(hit.gameObject);
                    return;
                }
            }
        }

        /// <summary>
        /// 말 소환 입력 (mount_token 사용)
        /// </summary>
        private void HandleSummonInput()
        {
            if (_keyboard == null || _keyboard.eKey == null) return;

            // E키 + 특수 조건: mount_token 사용은 PlayerInventory.UseItem 기반
            // 여기서는 InventoryWindow 등에서 mount_token 사용 시 호출될 SummonHorse() 메서드를 제공
            // 직접 E키로 소환하지 않고, 아이템 사용 시스템을 통해 호출됨
        }

        /// <summary>
        /// 말 소환을 시도합니다. mount_token 아이템 사용 시 호출됩니다.
        /// 가장 가까운 길바닥에 말을 스폰합니다.
        /// </summary>
        public bool SummonHorse()
        {
            // 쿨다운 체크
            if (_isOnCooldown)
            {
                Debug.Log($"[MountSystem] ⏳ 말 소환 쿨다운 중... ({_dismountCooldownTimer:F1}초 남음)");
                return false;
            }

            // 이미 말이 존재하는지 확인
            if (_currentHorse != null && _currentHorse.activeInHierarchy)
            {
                Debug.Log("[MountSystem] 이미 소환된 말이 있습니다.");
                return false;
            }

            if (_horsePrefab == null)
            {
                Debug.LogError("[MountSystem] HorsePrefab이 할당되지 않았습니다! Inspector에서 설정해주세요.");
                return false;
            }

            // mount_token 소모
            if (PlayerInventory.Instance != null)
            {
                if (!PlayerInventory.Instance.HasItem("mount_token"))
                {
                    Debug.Log("[MountSystem] mount_token 아이템이 없습니다.");
                    return false;
                }
                PlayerInventory.Instance.RemoveItem("mount_token", 1);
            }

            // 플레이어 위치
            Transform playerTransform = _playerMovement != null ? _playerMovement.transform : null;
            if (playerTransform == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                    playerTransform = playerObj.transform;
            }

            if (playerTransform == null)
            {
                Debug.LogError("[MountSystem] 플레이어를 찾을 수 없어 말을 소환할 수 없습니다.");
                return false;
            }

            // 가장 가까운 길바닥 찾기 (Raycast)
            Vector3 spawnPos = playerTransform.position + playerTransform.forward * 2f;
            spawnPos.y += 2f;

            RaycastHit groundHit;
            if (Physics.Raycast(spawnPos, Vector3.down, out groundHit, 10f))
            {
                spawnPos = groundHit.point;
            }
            else
            {
                // Raycast 실패 시 플레이어 위치 그대로 사용
                spawnPos = new Vector3(playerTransform.position.x, playerTransform.position.y, playerTransform.position.z);
            }

            // 말 스폰
            GameObject horse = Instantiate(_horsePrefab, spawnPos, Quaternion.identity);
            if (horse == null)
            {
                Debug.LogError("[MountSystem] 말 프리팹 인스턴스 생성 실패!");
                return false;
            }

            // MountSpawner 참조 캐싱
            _currentHorseSpawner = horse.GetComponent<MountSpawner>();
            if (_currentHorseSpawner == null)
            {
                _currentHorseSpawner = horse.AddComponent<MountSpawner>();
            }

            _currentHorse = horse;
            _mountHP = _maxMountHP;

            // 말 소환 이벤트 (Spawner에 알림)
            if (_currentHorseSpawner != null)
            {
                _currentHorseSpawner.OnHorseSummoned();
            }

            Debug.Log($"[MountSystem] 🐴 말 소환 완료! 위치: {spawnPos}");
            return true;
        }

        // ===== 탑승/하차 =====

        /// <summary>
        /// 말 탑승을 시도합니다. UI 호출용: 플레이어 반경 내 말 탑승.
        /// </summary>
        public void MountHorse()
        {
            if (_isMounted) return;
            if (_playerMovement == null) return;

            Collider[] hits = Physics.OverlapSphere(_playerMovement.transform.position, _mountRange);
            foreach (var hit in hits)
            {
                if (hit == null) continue;
                if (hit.CompareTag("Mount") || hit.CompareTag("Horse") ||
                    hit.gameObject.layer == LayerMask.NameToLayer("Mount") ||
                    hit.gameObject.layer == LayerMask.NameToLayer("Horse"))
                {
                    TryMount(hit.gameObject);
                    return;
                }
            }

            Debug.Log("[MountSystem] 탑승 가능한 말을 찾을 수 없습니다.");
        }

        /// <summary>
        /// UI 호출용: 말에서 내리기.
        /// </summary>
        public void DismountHorse()
        {
            Dismount();
        }


        private void TryMount(GameObject horse)
        {
            if (horse == null) return;
            if (_isMounted) return;

            // Ctrl (웅크리기) 상태면 탑승 불가
            if (StealthSystem.Instance != null && StealthSystem.Instance.IsStealthed)
            {
                Debug.Log("[MountSystem] 은신 상태에서는 탑승할 수 없습니다.");
                return;
            }

            // 말 오브젝트에 MountSpawner가 있으면 참조
            _currentHorseSpawner = horse.GetComponent<MountSpawner>();

            // 말 오브젝트에 RigAnimationController가 있으면 Idle → Run 전환 (선택사항)
            var horseRig = horse.GetComponent<RigAnimationController>();
            if (horseRig != null)
            {
                horseRig.SetState(AnimationState.Run);
            }

            _currentHorse = horse;
            _isMounted = true;
            _mountHP = _maxMountHP;
            _isSprinting = false;

            // Neural Animation: Mount 정책으로 전환
            _neuralAnim?.SwitchPolicy(NeuralAnimationController.PolicyType.Mount);

            // 플레이어를 말 위치로 이동
            Transform playerTransform = _playerMovement != null ? _playerMovement.transform : null;
            if (playerTransform != null)
            {
                playerTransform.position = horse.transform.position + Vector3.up * 1.5f;
                playerTransform.SetParent(horse.transform);

                // 플레이어 ProceduralAnimationController에 탑승 자세 알림
                var playerProceduralAnim = playerTransform.GetComponentInChildren<ProceduralAnimationController>();
                playerProceduralAnim?.TriggerAction("mount");
            }

            // 플레이어 애니메이션
            if (_rigAnim != null)
            {
                _rigAnim.SetState(AnimationState.Run);
            }

            // 오토무브 취소
            if (AutoMoveManager.Instance != null && AutoMoveManager.Instance.IsMoving)
            {
                AutoMoveManager.Instance.CancelAutoMove("말 탑승");
            }

            // 이벤트 발생
            OnMountStateChanged?.Invoke(true);
            OnMountHPChanged?.Invoke(_mountHP, _maxMountHP);

            Debug.Log("[MountSystem] 🐴 말 탑승!");
        }

        /// <summary>
        /// E키로 하차
        /// </summary>
        private void HandleDismountInput()
        {
            if (_keyboard == null) return;

            if (_keyboard.eKey.wasPressedThisFrame)
            {
                Dismount();
            }
        }

        /// <summary>
        /// 하차 처리
        /// </summary>
        public void Dismount()
        {
            if (!_isMounted) return;

            Transform playerTransform = _playerMovement != null ? _playerMovement.transform : null;

            // 플레이어를 말 옆으로 이동
            if (playerTransform != null && _currentHorse != null)
            {
                Vector3 dismountPos = _currentHorse.transform.position + _currentHorse.transform.right * 1.5f;
                dismountPos.y = _currentHorse.transform.position.y;
                playerTransform.position = dismountPos;
                playerTransform.SetParent(null);

                // 플레이어 ProceduralAnimationController에 하차 알림
                var playerProceduralAnim = playerTransform.GetComponentInChildren<ProceduralAnimationController>();
                playerProceduralAnim?.TriggerAction("dismount");
            }
            else if (playerTransform != null)
            {
                playerTransform.SetParent(null);

                // 플레이어 ProceduralAnimationController에 하차 알림
                var playerProceduralAnim = playerTransform.GetComponentInChildren<ProceduralAnimationController>();
                playerProceduralAnim?.TriggerAction("dismount");
            }

            // 말 애니메이션
            if (_currentHorse != null)
            {
                var horseRig = _currentHorse.GetComponent<RigAnimationController>();
                if (horseRig != null)
                {
                    horseRig.SetState(AnimationState.Idle);
                }
            }

            // 플레이어 애니메이션
            if (_rigAnim != null)
            {
                _rigAnim.SetState(AnimationState.Idle);
            }

            _isMounted = false;
            _isSprinting = false;
            _currentHorse = null;
            _currentHorseSpawner = null;

            // Neural Animation: Locomotion 정책으로 복귀
            _neuralAnim?.SwitchPolicy(NeuralAnimationController.PolicyType.Locomotion);

            // 이벤트 발생
            OnMountStateChanged?.Invoke(false);

            Debug.Log("[MountSystem] 🐴 말 하차!");
        }

        /// <summary>
        /// 강제 하차 (HP 0 등)
        /// </summary>
        private void ForceDismount(string reason)
        {
            if (!_isMounted) return;

            Debug.Log($"[MountSystem] ⚠️ 강제 하차: {reason}");

            // 말 사망 처리
            if (_currentHorseSpawner != null)
            {
                _currentHorseSpawner.OnHorseDeath();
            }

            Transform playerTransform = _playerMovement != null ? _playerMovement.transform : null;
            if (playerTransform != null && _currentHorse != null)
            {
                Vector3 dismountPos = _currentHorse.transform.position + _currentHorse.transform.right * 2f;
                dismountPos.y = _currentHorse.transform.position.y;
                playerTransform.position = dismountPos;
                playerTransform.SetParent(null);

                // 플레이어 ProceduralAnimationController에 하차 알림
                var playerProceduralAnim = playerTransform.GetComponentInChildren<ProceduralAnimationController>();
                playerProceduralAnim?.TriggerAction("dismount");
            }
            else if (playerTransform != null)
            {
                playerTransform.SetParent(null);

                // 플레이어 ProceduralAnimationController에 하차 알림
                var playerProceduralAnim = playerTransform.GetComponentInChildren<ProceduralAnimationController>();
                playerProceduralAnim?.TriggerAction("dismount");
            }

            if (_rigAnim != null)
            {
                _rigAnim.SetState(AnimationState.Idle);
            }

            _isMounted = false;
            _isSprinting = false;

            // 쿨다운 설정
            _isOnCooldown = true;
            _dismountCooldownTimer = _hpZeroDismountCooldown;

            // 이벤트
            OnMountStateChanged?.Invoke(false);
            OnMountDied?.Invoke();

            _currentHorse = null;
            _currentHorseSpawner = null;
        }

        /// <summary>
        /// 현재 타고 있는 말을 제거합니다 (despawn).
        /// MountSpawner에서 거리 초과 시 호출.
        /// </summary>
        public void DespawnCurrentHorse()
        {
            if (_isMounted)
            {
                ForceDismount("말 제거");
                return;
            }

            if (_currentHorse != null)
            {
                Destroy(_currentHorse);
                _currentHorse = null;
                _currentHorseSpawner = null;
            }

            Debug.Log("[MountSystem] 말 제거됨 (Despawn)");
        }

        // ===== 탑승 중 이동 =====

        /// <summary>
        /// 탑승 중 이동 처리 (CharacterController.Move)
        /// </summary>
        private void HandleMountedMovement()
        {
            if (_playerMovement == null || _characterController == null) return;

            // WASD 입력
            float horizontal = 0;
            float vertical = 0;

            if (_keyboard.wKey.isPressed || _keyboard.upArrowKey.isPressed) vertical += 1;
            if (_keyboard.sKey.isPressed || _keyboard.downArrowKey.isPressed) vertical -= 1;
            if (_keyboard.aKey.isPressed || _keyboard.leftArrowKey.isPressed) horizontal -= 1;
            if (_keyboard.dKey.isPressed || _keyboard.rightArrowKey.isPressed) horizontal += 1;

            Vector3 inputDirection = new Vector3(horizontal, 0, vertical).normalized;

            // 카메라 기준 이동 방향
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                Vector3 forward = mainCam.transform.forward;
                Vector3 right = mainCam.transform.right;

                // Top-down 카메라 처리
                if (Mathf.Approximately(Mathf.Abs(forward.y), 1f))
                {
                    forward = mainCam.transform.up;
                    right = mainCam.transform.right;
                }

                forward.y = 0;
                right.y = 0;
                forward.Normalize();
                right.Normalize();

                Vector3 moveDirection = (forward * vertical + right * horizontal).normalized;

                // Shift 키: 질주
                _isSprinting = _keyboard.leftShiftKey.isPressed && inputDirection.magnitude > 0.1f && _mountHP > 0f;

                // 속도 계산
                float baseSpeed = _playerMovement.WalkSpeed;
                float speed = baseSpeed * (_isSprinting ? _sprintSpeedMultiplier : _walkSpeedMultiplier);

                // 이동 방향으로 회전
                if (moveDirection != Vector3.zero)
                {
                    if (_currentHorse != null)
                    {
                        _currentHorse.transform.rotation = Quaternion.LookRotation(moveDirection);
                    }
                }

                // 중력 적용
                float verticalVelocity = 0f;
                if (_characterController.isGrounded)
                {
                    verticalVelocity = -2f;
                }
                else
                {
                    verticalVelocity += -9.81f * Time.deltaTime;
                }

                // SimpleMove 대신 CharacterController.Move 사용 (속도 제어)
                Vector3 motion = moveDirection * speed * Time.deltaTime;
                motion.y = verticalVelocity * Time.deltaTime;

                // CharacterController.Move 호출
                _characterController.Move(motion);
            }

            // 점프 차단 (Space 키 무시)
            // HandleJump()는 PlayerMovement에서 호출되지만, MountSystem이 _isMounted 상태를 
            // PlayerMovement에 알려주는 방식으로는 여기서 _playerMovement.IsJumping 같은 속성을 
            // 강제로 관리할 수 없음.
            // 대신 CharacterController.Move에서 점프 velocity를 적용하지 않음으로써 간접 차단.
            // PlayerMovement 측에서도 MountSystem.Instance.IsMounted 체크 후 점프를 막는 것이 이상적.
        }

        // ===== 말 체력 =====

        /// <summary>
        /// 말 체력 관리 (질주 소모, 정지 회복)
        /// </summary>
        private void HandleMountHP()
        {
            if (_isSprinting)
            {
                // 질주 중 HP 소모
                _mountHP -= _sprintHPCostPerSec * Time.deltaTime;
                if (_mountHP <= 0f)
                {
                    _mountHP = 0f;
                    ForceDismount("말 체력 소진");

                    // HP 변경 이벤트
                    OnMountHPChanged?.Invoke(_mountHP, _maxMountHP);
                    return;
                }
            }
            else
            {
                // 정지/일반 이동 중 HP 회복
                if (_mountHP < _maxMountHP)
                {
                    _mountHP += _idleHPRegenPerSec * Time.deltaTime;
                    _mountHP = Mathf.Min(_mountHP, _maxMountHP);
                }
            }

            // HP 변경 이벤트
            OnMountHPChanged?.Invoke(_mountHP, _maxMountHP);
        }

        // ===== PlayerMovement 연동 =====

        /// <summary>
        /// 탑승 중 점프가 가능한지 확인. PlayerMovement.HandleJump()에서 호출.
        /// </summary>
        public bool CanJump()
        {
            return !_isMounted;
        }

        // ===== 싱글톤 =====

        private static MountSystem _instance;

        // ===== Gizmos =====

        private void OnDrawGizmosSelected()
        {
            if (_playerMovement == null) return;
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(_playerMovement.transform.position, _mountRange);
        }
    }
}