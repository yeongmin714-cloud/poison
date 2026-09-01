using UnityEngine;
using UnityEngine.InputSystem;
using ProjectName.Systems.Animation.Procedural;
using ProjectName.Systems.Animation.Neural;
using System.Linq;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// 플레이어 이동을 담당하는 스크립트.
    /// Input System Package 기반으로 동작 (Input.GetKey 대신 Keyboard.current 사용)
    /// WASD 이동, Shift 달리기/대쉬, Space 점프, Q 구르기를 지원합니다.
    /// 
    /// C16-02: E 키 상호작용 추가 — 근처 Bed 발견 시 Bed.OnInteract() 호출.
    /// C21-01: 대쉬 시스템 — 스태미나, HUD, 카메라 효과
    /// C21-02: 구르기 시스템 — Q 키, 무적, 쿨다운, 더블탭
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : MonoBehaviour, IVelocityProvider
    {
        [Header("Movement Settings")]
        [SerializeField] private float _walkSpeed = 5f;
        [SerializeField] private float _runSpeed = 10f;
        [SerializeField] private float _dashSpeed = 15f;
        [SerializeField] private float _jumpHeight = 2f;
        [SerializeField] private float _gravity = -9.81f;

        [Header("Interaction Settings")]
        [SerializeField] private float _interactionRadius = 2.5f;
        [SerializeField] private LayerMask _interactableLayers = -1; // Default: Everything

        [Header("Stamina Settings")]
        [SerializeField] private float _maxStamina = 100f;
        [SerializeField] private float _dashStaminaCost = 20f;     // 초당 소모
        [SerializeField] private float _staminaRegenRate = 15f;    // 초당 회복
        [SerializeField] private float _staminaRegenDelay = 2f;    // 고갈 후 대기

        [Header("Roll Settings")]
        [SerializeField] private float _rollDuration = 0.5f;
        [SerializeField] private float _rollSpeedMultiplier = 3f;  // walkSpeed × 3
        [SerializeField] private float _rollCooldown = 1.5f;
        [SerializeField] private float _doubleTapTimeWindow = 0.3f; // 더블탭 인식 시간

        private CharacterController _controller;
        private Transform _cameraTransform;
        private Camera _camera;

        private Vector3 _moveDirection;
        private float _verticalVelocity;
        private float _currentSpeed;
        private bool _isGrounded;

        // Input System 캐싱
        private Keyboard _keyboard;

        // --- 스태미나 관련 ---
        private float _stamina;
        private float _staminaEmptyTime = -10f;

        // --- 대쉬 관련 ---
        private bool _isDashing = false;

        // --- 속도 수정자 (BiomeEffectController 등에서 설정) ---
        private float _speedModifier = 1f;

        // --- 이동 잠금 (MountSystem 등에서 설정 — 탑승 중 PlayerMovement 이동 방지) ---
        // private bool _movementLocked = false;

        // --- 발소리 타이머 ---
        private float _footstepTimer = 0f;

        // --- 구르기 관련 ---
        private bool _isRolling = false;
        private Vector3 _rollDirection;
        private float _rollTimer = 0f;
        private float _lastRollTime = -10f;

        // --- 점프 관련 (로컬 추적 — RigAnimationController의 CurrentState 타이밍 이슈 해결) ---
        private bool _isJumping = false;

        // --- 더블탭 구르기 관련 ---
        private enum KeyDirection { Up, Down, Left, Right }
        // 게임 시작 직후 첫 키 입력이 더블탭으로 오인되지 않도록 음수로 초기화
        private float[] _lastKeyTime = new float[] { -10f, -10f, -10f, -10f };

        // --- 은신 관련 (Phase 34) ---
        private bool _stealthToggleHeld = false; // Ctrl 키 홀드 상태 추적

        // --- 카메라 효과 관련 ---
        private float _defaultFOV;
        private float _dashFOVMultiplier = 1.1f; // 10% 줌아웃
        private float _cameraShakeTimer = 0f;
        private float _cameraShakeDuration = 0f;
        private float _cameraShakeIntensity = 0f;
        private Vector3 _cameraOriginalLocalPosition; // 카메라 흔들림 원위치 복원용

        // Rig animation
        private RigAnimationController _rigAnim;

        // Procedural animation (PlayerModel 자식에 있음)
        private ProceduralAnimationController _proceduralAnim;

        // Neural animation (같은 GameObject에 있음)
        private NeuralAnimationController _neuralAnim;
        private HybridAnimationController _hybridAnim;

        // 저장된 CharacterController 초기 높이 (구르기 복원용)
        private float _originalControllerHeight = 2f;

        private void Awake()
                {
                    _controller = GetComponent<CharacterController>();
                    if (_controller == null)
                    {
                        Debug.LogError("[PlayerMovement] CharacterController가 필요합니다!");
                        return; // CharacterController 없이 진행 불가
                    }
            
                    // CRITICAL: Disable CC first to prevent falling before position is locked
                    _controller.enabled = false;
            
                    _originalControllerHeight = _controller.height;

                    // RigAnimationController 찾기 (PlayerPlaceholder에서 Awake로 이미 추가됨)
                    _rigAnim = GetComponent<RigAnimationController>();
                    if (_rigAnim == null)
                    {
                        Animator anim = GetComponent<Animator>();
                        if (anim != null && anim.runtimeAnimatorController != null)
                            _rigAnim = gameObject.AddComponent<RigAnimationController>();
                    }

                    // 메인 카메라 찾기
                    if (Camera.main != null)
                    {
                        _cameraTransform = Camera.main.transform;
                        _camera = Camera.main;
                        _defaultFOV = _camera.fieldOfView;
                        _cameraOriginalLocalPosition = _cameraTransform.localPosition;
                    }
                    else
                    {
                        // Try to find any camera
                        var anyCamera = FindFirstObjectByType<Camera>();
                        if (anyCamera != null)
                        {
                            _cameraTransform = anyCamera.transform;
                            _camera = anyCamera;
                            _defaultFOV = _camera.fieldOfView;
                            _cameraOriginalLocalPosition = _cameraTransform.localPosition;
                            anyCamera.tag = "MainCamera"; // Tag it for future use
                            Debug.LogWarning("[PlayerMovement] No MainCamera tagged camera found, using first available camera and tagging it.");
                        }
                        else
                        {
                            Debug.LogError("[PlayerMovement] 씬에 카메라가 없습니다! 카메라를 생성합니다.");
                            // Create a default camera
                            var camGO = new GameObject("Main Camera");
                            camGO.tag = "MainCamera";
                            _camera = camGO.AddComponent<Camera>();
                            _cameraTransform = _camera.transform;
                            _defaultFOV = _camera.fieldOfView;
                            _cameraOriginalLocalPosition = _cameraTransform.localPosition;
                            camGO.AddComponent<AudioListener>();
                            Debug.Log("[PlayerMovement] Created default Main Camera.");
                        }
                    }

                    _keyboard = Keyboard.current;
                    _stamina = _maxStamina;

                    // PlayerModel 자식에서 ProceduralAnimationController 찾기
                    _proceduralAnim = GetComponentInChildren<ProceduralAnimationController>();
                    if (_proceduralAnim == null)
                    {
                        Transform model = transform.Find("PlayerModel");
                        if (model != null)
                            _proceduralAnim = model.GetComponent<ProceduralAnimationController>();
                    }

                    // NeuralAnimationController 설정 (같은 GameObject)
                    _neuralAnim = GetComponent<NeuralAnimationController>();
                    if (_neuralAnim == null)
                        _neuralAnim = gameObject.AddComponent<NeuralAnimationController>();
                    _neuralAnim.SetVelocityProvider(this);

                    // HybridAnimationController 설정 (같은 GameObject)
                    _hybridAnim = GetComponent<HybridAnimationController>();
                    if (_hybridAnim == null)
                        _hybridAnim = gameObject.AddComponent<HybridAnimationController>();

                    // ProgressiveRolloutManager에 등록 (Phase 4.6.2)
                    if (ProgressiveRolloutManager.Instance != null)
                        ProgressiveRolloutManager.Instance.ConfigureHybridController(_hybridAnim);

                    // 스폰 위치 적용 (PlayerSpawnConfig에서 읽어옴 — 테스트씬과 MainScene 동기화)
                    Vector3 spawnPos = PlayerSpawnConfig.SpawnPosition;
                    // 지형(Ground_Inner) 표면 위에 스폰. TerrainGenerator로 실제 지표면 높이 계산.
                    // CollisionFloor 없이 지형 MeshCollider(+ClampToGround)가 플레이어를 고정한다.
                    float spawnGroundY = spawnPos.y;
                    try
                    {
                        // 지형 높이(세계 y). Ground y=1 + 지형 굴곡(GetHeightAt은 0~0.5)
                        spawnGroundY = ProjectName.Systems.TerrainGenerator.GetHeightAt(spawnPos.x, spawnPos.z, ProjectName.Core.Data.BiomeType.Plains, 42) + 1f;
                    }
                    catch (System.Exception) { /* 기본값 유지 */ }
                    // 지형 표면 위 0.4m에 스폰 → 잠깐 아래로 내려가 지형 MeshCollider에 안착 (추락 아님)
                    transform.position = new Vector3(spawnPos.x, spawnGroundY + 0.4f, spawnPos.z);
            
                    // CRITICAL: Re-enable CC after position is finalized
                    _controller.enabled = true;
                    // NOTE: _controller.Move(down*0.2f)는 스폰 직후 플레이어를 CollisionFloor 표면보다
                    //       아래(2.80 < 3.0)로 밀어 CharacterController가 바닥을 통과해 SafetyFloor로
                    //       추락하게 만드는 범인이었음. 제거한다. ClampToGround가 표면을 유지한다.

                    // 스폰 직후 지면 콜라이더 존재 + Raycast 감지 여부를 로그 (추락 원인 파악)
                    bool cfGrab = Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down,
                        out RaycastHit _cHit, 5f, ~0, QueryTriggerInteraction.Ignore);
                    Debug.Log($"[PlayerMovement] 스폰 후 지면 Raycast 존재={cfGrab} 대상={(cfGrab ? _cHit.collider?.gameObject.name : "없음")} 플레이어y={transform.position.y:F2}");
        }

        private void Update()
        {
            // 카메라 보정: 메인 카메라가 항상 플레이어를 내려다보게 강제 (3인칭 시점).
            // Cinemachine vcam의 Follow/LookAt이 배치/런타임에 제대로 안 먹혀 카메라가 수평(0,0,0)으로
            // 떠 있어 발밑 지형이 안 보이던 원인 해결. 매 프레임 플레이어를 lookAt한다.
            FixCameraToPlayer();
            CamForwardProbe();

            // ApplyGravity()를 먼저 호출하여 _isGrounded를 최신 상태로 유지
            ApplyGravity();

            HandleMovement();
            HandleRoll();
            HandleJump();
            HandleStamina();
            MovePlayer();
            HandleInteraction(); // C16-02: E 키 상호작용
            HandleCameraShake();
            HandleDashCameraEffect();

            // Phase 8.3: 발소리 (땅에 닿고 이동 중)
            HandleFootstepSound();

            // Phase 34: 은신 입력 처리
            HandleStealthInput();

            // Phase 34: 은신 중 암살 가능 체크 (StealthSystem으로 위임)
            // Phase 34: 은신 상태에서 속도 제한은 HandleMovement()에서 직접 적용 (_walkSpeed * 0.5f)
        }

        /// <summary>
        /// Phase 34: Ctrl 키 입력 → StealthSystem.ToggleStealth() 호출
        /// </summary>
        private void HandleStealthInput()
        {
            if (_keyboard == null) return;

            // Ctrl 키 누름/뗌 토글
            bool ctrlPressed = _keyboard.ctrlKey.isPressed;

            if (ctrlPressed && !_stealthToggleHeld)
            {
                _stealthToggleHeld = true;
                if (StealthSystem.Instance != null)
                    StealthSystem.Instance.ToggleStealth();
            }
            else if (!ctrlPressed && _stealthToggleHeld)
            {
                _stealthToggleHeld = false;
            }
        }

        /// <summary>
        /// C16-02: E 키 입력 감지 → 근처 Bed 찾기 → 상호작용
        /// </summary>
        private void HandleInteraction()
        {
            if (_keyboard == null) return;

            // E 키 (wasPressedThisFrame: 눌린 순간만 반응)
            if (_keyboard.eKey.wasPressedThisFrame)
            {
                // 탑승 중에는 상호작용 무시 (MountSystem에서 하차 처리)
                if (MountSystem.Instance != null && MountSystem.Instance.IsMounted)
                    return;

                // Physics.OverlapSphere로 주변 Bed 검색
                Collider[] hits = Physics.OverlapSphere(transform.position, _interactionRadius, _interactableLayers);

                foreach (var hit in hits)
                {
                    Bed bed = hit.GetComponent<Bed>();
                    if (bed != null)
                    {
                        bed.OnInteract();
                        return; // 첫 번째 Bed만 상호작용
                    }
                }
            }
        }

        private void HandleMovement()
        {
            if (_keyboard == null) _keyboard = Keyboard.current;
            if (_keyboard == null) return;

            // 구르기 중에는 이동 입력을 새로운 방향으로 변경하지 않음
            if (_isRolling) return;

            float horizontal = 0;
            float vertical = 0;

            bool wPressed = _keyboard.wKey.isPressed || _keyboard.upArrowKey.isPressed;
            bool sPressed = _keyboard.sKey.isPressed || _keyboard.downArrowKey.isPressed;
            bool aPressed = _keyboard.aKey.isPressed || _keyboard.leftArrowKey.isPressed;
            bool dPressed = _keyboard.dKey.isPressed || _keyboard.rightArrowKey.isPressed;

            if (wPressed) vertical += 1;
            if (sPressed) vertical -= 1;
            if (aPressed) horizontal -= 1;
            if (dPressed) horizontal += 1;

            // 더블탭 감지 (구르기용)
            DetectDoubleTap();

            Vector3 inputDirection = new Vector3(horizontal, 0, vertical).normalized;

            if (inputDirection.magnitude > 0.1f && _cameraTransform != null)
            {
                Vector3 forward;
                Vector3 right;

                // 카메라가 위/아래를 보고 있으면(top-down), forward 대신 up 사용
                if (Mathf.Approximately(Mathf.Abs(_cameraTransform.forward.y), 1f))
                {
                    forward = _cameraTransform.up;
                    right = _cameraTransform.right;
                }
                else
                {
                    forward = _cameraTransform.forward;
                    right = _cameraTransform.right;
                }
                forward.y = 0;
                right.y = 0;
                forward.Normalize();
                right.Normalize();

                _moveDirection = (forward * vertical + right * horizontal).normalized;

                // 캐릭터가 이동 방향을 바라보게 회전
                if (_moveDirection != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(_moveDirection);
                }
            }
            else
            {
                _moveDirection = Vector3.zero;
            }

            // 걷기/달리기/대쉬
            bool sprintKey = _keyboard != null && _keyboard.leftShiftKey.isPressed;
            bool hasStamina = _stamina > 0f;
            bool isMoving = _moveDirection.magnitude > 0.1f;

            if (sprintKey && hasStamina && isMoving)
            {
                _currentSpeed = _dashSpeed;
                _isDashing = true;
            }
            else if (sprintKey && isMoving)
            {
                _currentSpeed = _runSpeed;
                _isDashing = false;
            }
            else
            {
                _currentSpeed = _walkSpeed;
                _isDashing = false;
            }

            // Phase 34: 은신 중 속도 50% 제한
            if (StealthSystem.Instance != null && StealthSystem.Instance.IsStealthed)
            {
                _currentSpeed = _walkSpeed * 0.5f;
                _isDashing = false; // 은신 중 대쉬 불가
            }

            // 애니메이션 상태 업데이트
            if (_rigAnim != null)
            {
                // Jump 등 트리거 기반 상태는 로컬 _isJumping 추적으로 덮어쓰지 않음
                // (RigAnimationController.CurrentState는 코루틴 전환 중 지연되므로 로컬 추적 사용)
                if (_isJumping)
                    return;

                if (!isMoving)
                {
                    if (_rigAnim.CurrentState != AnimationState.Idle)
                        _rigAnim.SetState(AnimationState.Idle);
                    _rigAnim.CurrentSpeed = 0f;
                }
                else if (_isDashing)
                {
                    _rigAnim.CurrentSpeed = 1f; // Speed = 1 for Run blend
                    _rigAnim.SetState(AnimationState.Run);
                }
                else if (sprintKey)
                {
                    _rigAnim.CurrentSpeed = 1f; // Speed = 1 for Run blend
                    _rigAnim.SetState(AnimationState.Run);
                }
                else
                {
                    _rigAnim.CurrentSpeed = 0.5f; // Speed = 0.5 for Walk blend
                    _rigAnim.SetState(AnimationState.Walk);
                }
            }
        }

        /// <summary>
        /// 더블탭 방향키 감지 — 같은 방향키를 _doubleTapTimeWindow 내에 두 번 누르면 구르기
        /// </summary>
        private void DetectDoubleTap()
        {
            if (_keyboard == null) return;

            // 각 키의 wasPressedThisFrame 확인
            if (_keyboard.wKey.wasPressedThisFrame || _keyboard.upArrowKey.wasPressedThisFrame)
                CheckDoubleTap(KeyDirection.Up);
            if (_keyboard.sKey.wasPressedThisFrame || _keyboard.downArrowKey.wasPressedThisFrame)
                CheckDoubleTap(KeyDirection.Down);
            if (_keyboard.aKey.wasPressedThisFrame || _keyboard.leftArrowKey.wasPressedThisFrame)
                CheckDoubleTap(KeyDirection.Left);
            if (_keyboard.dKey.wasPressedThisFrame || _keyboard.rightArrowKey.wasPressedThisFrame)
                CheckDoubleTap(KeyDirection.Right);
        }

        private void CheckDoubleTap(KeyDirection dir)
        {
            int idx = (int)dir;
            float now = Time.time;

            if (now - _lastKeyTime[idx] < _doubleTapTimeWindow)
            {
                // 더블탭 감지 → 구르기 실행
                if (!_isRolling && _isGrounded && Time.time - _lastRollTime > _rollCooldown)
                {
                    StartRoll(GetDirectionVector(dir));
                }
                _lastKeyTime[idx] = 0f; // 더블탭 중복 방지
            }
            else
            {
                _lastKeyTime[idx] = now;
            }
        }

        private Vector3 GetDirectionVector(KeyDirection dir)
        {
            if (_cameraTransform == null) return transform.forward;

            Vector3 forward = _cameraTransform.forward;
            Vector3 right = _cameraTransform.right;
            forward.y = 0;
            right.y = 0;
            forward.Normalize();
            right.Normalize();

            switch (dir)
            {
                case KeyDirection.Up:    return forward;
                case KeyDirection.Down:  return -forward;
                case KeyDirection.Left:  return -right;
                case KeyDirection.Right: return right;
                default: return transform.forward;
            }
        }

        private void HandleRoll()
        {
            if (_keyboard == null || _controller == null) return;

            // Q 키 구르기 + 더블탭 구르기 조건
            if (_keyboard.qKey.wasPressedThisFrame && !_isRolling && 
                Time.time - _lastRollTime > _rollCooldown && _isGrounded)
            {
                // 방향: 현재 이동 방향 또는 캐릭터 정면
                Vector3 rollDir = _moveDirection.magnitude > 0.1f ? _moveDirection : transform.forward;
                StartRoll(rollDir);
            }

            // 구르기 진행
            if (_isRolling)
            {
                _rollTimer += Time.deltaTime;

                // 구르기 모션: walkSpeed * ROLL_SPEED_MULTIPLIER
                Vector3 rollMotion = _rollDirection * (_walkSpeed * _rollSpeedMultiplier);
                rollMotion.y = _verticalVelocity; // 중력 유지
                _controller.Move(rollMotion * Time.deltaTime);

                // 구르기 중 플레이어 높이 약간 낮춤 (스케일을 일시적으로 줄임)
                // 간단히 CharacterController의 height를 조정 (대신 transform scale 사용)
                if (_controller.height > _originalControllerHeight * 0.5f)
                {
                    _controller.height = Mathf.Lerp(_controller.height, _originalControllerHeight * 0.5f, Time.deltaTime * 10f);
                }

                // 구르기 종료
                if (_rollTimer >= _rollDuration)
                {
                    _isRolling = false;
                    _rollTimer = 0f;
                    _controller.height = _originalControllerHeight; // 저장된 원래 높이로 복구

                    // 카메라 흔들림 효과 (구르기 종료 시 약간)
                    TriggerCameraShake(0.05f, 0.05f);
                }
                else
                {
                    // 구르기 시작 시 카메라 흔들림
                    if (_rollTimer < 0.1f)
                    {
                        TriggerCameraShake(0.1f, 0.1f);
                    }
                }
            }
        }

        private void StartRoll(Vector3 direction)
        {
            _isRolling = true;
            _rollTimer = 0f;
            _lastRollTime = Time.time;
            _rollDirection = direction.normalized;
            _rollDirection.y = 0;

            // 구르기 시작 시 카메라 흔들림
            TriggerCameraShake(0.1f, 0.1f);
            _proceduralAnim?.TriggerAction("roll");

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[PlayerMovement] 🌀 구르기 시작! 방향: {_rollDirection}");
#endif
        }

        private void HandleJump()
        {
            if (_keyboard == null) return;

            // 구르기 중 점프 불가
            if (_isRolling) return;

            // 탑승 중 점프 불가 (MountSystem)
            if (MountSystem.Instance != null && MountSystem.Instance.IsMounted)
                return;

            if (_keyboard.spaceKey.wasPressedThisFrame && _isGrounded)
            {
                _verticalVelocity = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
                _isJumping = true;
                if (_rigAnim != null) _rigAnim.SetState(AnimationState.Jump);
                _proceduralAnim?.TriggerAction("jump");
            }

            // 땅에 닿으면 점프 상태 해제
            if (_isJumping && _isGrounded && _verticalVelocity <= 0f)
            {
                _isJumping = false;
            }
        }

        private void HandleStamina()
        {
            // 대쉬 중 스태미나 소모
            if (_isDashing && _stamina > 0f)
            {
                _stamina -= _dashStaminaCost * Time.deltaTime;
                if (_stamina <= 0f)
                {
                    _stamina = 0f;
                    _staminaEmptyTime = Time.time;
                    _isDashing = false;
                }
            }
            else
            {
                // 스태미나 회복 (고갈 후 딜레이 확인)
                if (_stamina < _maxStamina)
                {
                    if (Time.time - _staminaEmptyTime > _staminaRegenDelay)
                    {
                        _stamina += _staminaRegenRate * Time.deltaTime;
                        _stamina = Mathf.Min(_stamina, _maxStamina);
                    }
                }
            }
        }

        private void ApplyGravity()
        {
            if (_controller == null) return;

            // Use CharacterController's isGrounded as primary
            _isGrounded = _controller.isGrounded;

            // Fallback: Manual raycast ground check if CC isn't grounded (handles edge cases)
            if (!_isGrounded)
            {
                float rayDistance = _controller.height * 0.5f + _controller.skinWidth + 0.1f;
                _isGrounded = Physics.SphereCast(
                    transform.position + Vector3.up * _controller.skinWidth,
                    _controller.radius * 0.9f,
                    Vector3.down,
                    out _,
                    rayDistance,
                    ~0, // All layers
                    QueryTriggerInteraction.Ignore
                );
            }

            if (_isGrounded && _verticalVelocity < 0)
            {
                _verticalVelocity = -2f; // Small downward force to keep grounded
            }

            _verticalVelocity += _gravity * Time.deltaTime;

            if (!_isRolling)
            {
                _moveDirection.y = _verticalVelocity;
            }
        }

        private void MovePlayer()
        {
            if (_controller == null) return;

            if (_isRolling)
            {
                // 구르기 중에는 HandleRoll에서 이미 Move 처리
                return;
            }

            Vector3 motion = _moveDirection * _currentSpeed * _speedModifier;
            motion.y = _verticalVelocity;
            _controller.Move(motion * Time.deltaTime);

            // === 지면 고정 (추락 영구 방지 / 지형 위 안착) ===
            // 물리 충돌·Raycast에 의존하지 않고, 지형을 만든 TerrainGenerator.GetHeightAt으로
            // 현재 x,z의 지표면 높이를 수학적으로 도출해 그 위에 붙인다.
            ClampToGroundByHeight();
        }

        /// <summary>메인 카메라를 플레이어 뒤-위에서 플레이어를 내려다보게 강제 (발밑 지형이 보이도록).</summary>
        private void FixCameraToPlayer()
        {
            if (_cameraTransform == null)
            {
                if (Camera.main != null) _cameraTransform = Camera.main.transform;
                else return;
            }

            Transform playerT = transform;
            // 플레이어 뒤쪽 위(살짝 뒤+위)에 카메라 배치 → 플레이어(발밑 지형 포한)를 lookAt
            Vector3 desiredPos = playerT.position + new Vector3(3f, 4f, -6f);
            _cameraTransform.position = desiredPos;

            // 플레이어 발밑(지면 위 0.5m)을 lookAt — 아래 초록 지형이 화면에 잡힌다
            Vector3 lookTarget = playerT.position + new Vector3(0f, 0.5f, 0f);
            _cameraTransform.rotation = Quaternion.LookRotation(lookTarget - _cameraTransform.position);
        }

        // 캐디거 지형 로그: 카메라 전방 raycast로 화면이 실제 뭘 보는지 수회 확정
        private int _camProbeCount = 0;
        private float _camProbeTimer = 0f;
        private void CamForwardProbe()
        {
            if (_cameraTransform == null) return;
            // 처음 5회만, 0.3초 간격으로 로그
            if (_camProbeCount >= 5) return;
            _camProbeTimer += Time.deltaTime;
            if (_camProbeTimer < 0.3f) return;
            _camProbeTimer = 0f;

            Vector3 o = _cameraTransform.position + _cameraTransform.forward * 1f;
            bool hit = Physics.Raycast(o, _cameraTransform.forward, out RaycastHit h, 40f, ~0, QueryTriggerInteraction.Ignore);
            _camProbeCount++;

            string matName = "-";
            if (hit && h.collider != null)
            {
                var mrTmp = h.collider.GetComponent<MeshRenderer>();
                if (mrTmp != null && mrTmp.sharedMaterial != null)
                    matName = mrTmp.sharedMaterial.name;
                else
                    matName = "(재질없음:" + h.collider.gameObject.name + ")";
            }

            Debug.Log($"[CamProbe#{_camProbeCount}] 카메라방향(정면40m)={hit} 대상={(hit && h.collider != null ? h.collider?.gameObject.name + " y=" + h.point.y.ToString("F2") : "없음(허공)")} 재질={matName}");
            Debug.Log($"[CamProbe#{_camProbeCount}] camPos=({_cameraTransform.position.x:F1},{_cameraTransform.position.y:F1},{_cameraTransform.position.z:F1}) fwd=({_cameraTransform.forward.x:F2},{_cameraTransform.forward.y:F2},{_cameraTransform.forward.z:F2})");
        }


        /// <summary>
        /// 지형 높이 함수(GetHeightAt)로 현재 x,z의 지표면 세계 y를 계산해,
        /// 점프 중이 아닐 때 플레이어를 지표면(세계 y=1+높이) 바로 위에 강제로 붙인다.
        /// Raycast/물리 시뮬레이션과 무관 — 구조적으로 추락·통과가 불가능하다.
        /// </summary>
        private void ClampToGroundByHeight()
        {
            if (_controller == null) return;

            // 점프 중이면 지표면 고정하지 않음 (점프 상승)
            if (_isJumping) return;

            float groundWorldY;
            try
            {
                groundWorldY = 1f + ProjectName.Systems.TerrainGenerator.GetHeightAt(
                    transform.position.x, transform.position.z,
                    ProjectName.Core.Data.BiomeType.Plains, 42);
            }
            catch (System.Exception) { return; }

            // 발이 지표면보다 살짝 아래거나 가까우면(0.3m 이내) 지표면 위 0.05로 고정
            float playerFootY = transform.position.y;
            if (playerFootY < groundWorldY + 0.3f)
            {
                float targetY = groundWorldY + 0.05f;
                if (Mathf.Abs(transform.position.y - targetY) > 0.001f)
                {
                    transform.position = new Vector3(transform.position.x, targetY, transform.position.z);
                    _verticalVelocity = 0f;
                    _isGrounded = true;
                }
            }
        }

        /// <summary>
        /// 아래로 Raycast/SphereCast 해 지면 표면을 찾고,
        /// 플레이어 발이 지면보다 아래로 내려가면 그 표면 바로 위(0.05m)로 y를 강제로 교정한다.
        /// 물리 시뮬레이션 충돌(shrl)에 의존하지 않아 CharacterController가 뚫어도 항상 작동한다.
        /// 아래 지면이 없으면(낭떠러지) 하늘로 안 올리고 그대로 둔다.
        /// </summary>
        private void ClampToGround()
        {
            if (_controller == null) return;

            // 오리진을 발 아래가 아니라 살짝 위에서 시작해, 그 아래 지면(RaycastAll)을 찾는다.
            // 플레이어 자신(CharacterController/Collider)의 콜라이더는 지면이 아니므로 반드시 무시한다.
            const float searchDistance = 4f;
            Vector3 origin = new Vector3(transform.position.x, transform.position.y + 0.3f, transform.position.z);

            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, searchDistance, ~0, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0) return;

            // 자기 자신이 아닌 가장 가까운 지면 hit를 찾는다.
            // (RaycastAll 결과는 거리순이 아니므로, 자기 자신을 제외한 최근접을 선택)
            RaycastHit groundHit = default;
            bool found = false;
            float best = float.MaxValue;
            foreach (var h in hits)
            {
                if (h.collider == null) continue;
                // 자기 자신(이 GameObject 또는 자식)은 건너뜀
                if (h.collider.transform.IsChildOf(transform) || h.collider.transform == transform)
                    continue;
                if (h.distance < best)
                {
                    best = h.distance;
                    groundHit = h;
                    found = true;
                }
            }
            if (!found) return;

            float surfaceY = groundHit.point.y + 0.05f;   // 발이 지면 위 0.05m
            float playerFootY = transform.position.y;

            // 점프/낙하가 아닐 때, 발이 지면과 가까우면(아래거나 바로 위 0.15m) 표면에 고정.
            bool belowOrNear = playerFootY < surfaceY + 0.15f;
            bool airborne = playerFootY > surfaceY + 0.5f; // 지면 위 0.5m 이상은 강제 안함
            if (belowOrNear && !airborne)
            {
                transform.position = new Vector3(transform.position.x, surfaceY, transform.position.z);
                _verticalVelocity = 0f;
                _isGrounded = true;
            }
        }

        /// <summary>
        /// 카메라 흔들림 트리거
        /// </summary>
        private void TriggerCameraShake(float duration, float intensity)
        {
            // 진행 중인 흔들림보다 강한 효과만 덮어쓰기 (약한 효과는 무시)
            if (_cameraShakeTimer > 0f && intensity <= _cameraShakeIntensity)
                return;

            _cameraShakeTimer = duration;
            _cameraShakeDuration = duration;
            _cameraShakeIntensity = intensity;
        }

        /// <summary>
        /// 카메라 흔들림 효과 처리 — 누적 버그 수정: 원래 위치로 복원
        /// </summary>
        private void HandleCameraShake()
        {
            if (_cameraShakeTimer > 0f && _cameraTransform != null)
            {
                _cameraShakeTimer -= Time.deltaTime;

                // 카메라를 원래 위치로 복원한 후 흔들림 오프셋 적용 (누적 방지)
                _cameraTransform.localPosition = _cameraOriginalLocalPosition;

                float progress = 1f - (_cameraShakeTimer / Mathf.Max(_cameraShakeDuration, 0.001f));
                float decay = 1f - Mathf.Clamp01(progress);
                float shakeAmount = _cameraShakeIntensity * decay;

                Vector3 shakeOffset = new Vector3(
                    Random.Range(-1f, 1f) * shakeAmount,
                    Random.Range(-1f, 1f) * shakeAmount,
                    Random.Range(-1f, 1f) * shakeAmount
                );

                _cameraTransform.localPosition += shakeOffset;
            }
            else if (_cameraTransform != null && _cameraTransform.localPosition != _cameraOriginalLocalPosition)
            {
                // 흔들림 종료 후 원래 위치로 복원
                _cameraTransform.localPosition = _cameraOriginalLocalPosition;
            }
        }

        /// <summary>
        /// 대쉬 중 카메라 효과 — FOV 증가, 비네트 효과 (화면 어두워짐)
        /// </summary>
        private void HandleDashCameraEffect()
        {
            if (_camera == null) return;

            if (_isDashing && _stamina > 0f)
            {
                // FOV 10% 증가
                float targetFOV = _defaultFOV * _dashFOVMultiplier;
                _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, targetFOV, Time.deltaTime * 5f);
            }
            else
            {
                // FOV 복구
                _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, _defaultFOV, Time.deltaTime * 5f);
            }
        }

        /// <summary>
        /// HUD: 스태미나 바 표시 (화면 왼쪽 하단, HP 바 아래)
        /// </summary>
        private void OnGUI()
        {
            DrawStaminaBar();
        }

        private void DrawStaminaBar()
        {
            float barWidth = 200f;
            float barHeight = 16f;
            float barX = 10f;
            float barY = Screen.height - 50f; // HP 바 아래 (HP 바가 y=30 가정, 50으로 배치)

            float ratio = _maxStamina > 0f ? Mathf.Clamp01(_stamina / _maxStamina) : 0f;

            // 배경
            GUI.Box(new Rect(barX, barY, barWidth, barHeight), "");

            // 채워진 부분
            Color barColor;
            if (ratio > 0.5f)
                barColor = Color.Lerp(Color.yellow, Color.green, (ratio - 0.5f) * 2f); // 연두색 (100-50%)
            else if (ratio > 0.25f)
                barColor = Color.Lerp(Color.red, Color.yellow, (ratio - 0.25f) * 4f);  // 노랑 (50-25%)
            else
                barColor = Color.red; // 빨강 (25-0%)

            GUI.color = barColor;
            GUI.DrawTexture(new Rect(barX, barY, barWidth * ratio, barHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 텍스트
            GUI.Label(new Rect(barX + 5, barY, barWidth - 10, barHeight), $"⚡ 스태미나");
        }

        /// <summary>
        /// Phase 8.3: 발소리 처리 — 땅에 닿고 이동 중일 때 0.5초 간격
        /// FootstepSoundController가 존재하면 해당 컴포넌트에 위임 (표면 인지, 속도별 간격)
        /// </summary>
        private void HandleFootstepSound()
        {
            // FootstepSoundController가 존재하면 자체 발소리 처리 생략
            // (표면 인지 및 속도별 간격을 지원하는 더 정교한 버전이 처리함)
            if (TryGetComponent<FootstepSoundController>(out _))
                return;

            if (!_isGrounded) return;

            // CharacterController.velocity로 실제 이동 속도 확인
            Vector3 velocity = _controller != null ? _controller.velocity : Vector3.zero;
            velocity.y = 0f; // 수직 속도 제외

            if (velocity.magnitude > 0.5f)
            {
                _footstepTimer += Time.deltaTime;
                if (_footstepTimer >= 0.5f)
                {
                    _footstepTimer = 0f;
                    SoundEffectManager.Instance?.PlaySFX(SoundEffectManager.SFXType.Footstep);
                }
            }
            else
            {
                // 정지 시 타이머 리셋 (다음 이동 시 바로 첫 발소리)
                _footstepTimer = 0f;
            }
        }

        // --- public 속성 (테스트용, UI 표시용) ---
        public float WalkSpeed => _walkSpeed;
        public float RunSpeed => _runSpeed;
        public float DashSpeed => _dashSpeed;
        public float JumpHeight => _jumpHeight;
        public bool IsSprinting => _keyboard != null && _keyboard.leftShiftKey.isPressed && _moveDirection.magnitude > 0.1f;
        public bool IsDashing => _isDashing;
        public bool IsJumping => _isJumping;
        public Vector3 Velocity => _controller != null ? _controller.velocity : Vector3.zero;

        public float InteractionRadius => _interactionRadius;

        // --- 스태미나 속성 ---
        public float Stamina => _stamina;
        public float MaxStamina => _maxStamina;
        public float StaminaRatio => _maxStamina > 0f ? Mathf.Clamp01(_stamina / _maxStamina) : 0f;
        public float StaminaEmptyTime => _staminaEmptyTime;

        // --- 속도 수정자 ---
        public float SpeedModifier { get => _speedModifier; set => _speedModifier = Mathf.Max(0.1f, value); }

        // --- 구르기 속성 ---
        public bool IsRolling => _isRolling;
        public float RollTimer => _rollTimer;
        public float RollDuration => _rollDuration;
        public float RollCooldown => _rollCooldown;
        public float LastRollTime => _lastRollTime;

        // --- 대쉬 속성 ---
        public float DashStaminaCost => _dashStaminaCost;
        public float StaminaRegenRate => _staminaRegenRate;
        public float StaminaRegenDelay => _staminaRegenDelay;

        public float RollSpeedMultiplier => _rollSpeedMultiplier;

        // ──────────────────────────────────────────────
        // IVelocityProvider 구현 (ProceduralAnimationController 연동)
        // ──────────────────────────────────────────────

        public Vector3 CurrentVelocity => _controller != null ? _controller.velocity : Vector3.zero;
        public float CurrentSpeed => _currentSpeed;
        public bool IsGrounded => _isGrounded;
    }
}