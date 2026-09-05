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

                    // NeuralAnimationController 설정 (같은 GameObject) — 모델 있을 때만 부착 유지.
                    // 모델 없는 Neural은 LateUpdate에서 뼈 localRotation을 덮어써 Player_AC를 얼림 → 제거.
                    _neuralAnim = GetComponent<NeuralAnimationController>();
                    if (_neuralAnim == null)
                    {
                        // AddComponent는 Awake에서 Resources에서 모델 로드 시도 → 즉시 HasAnyModel 판정 가능
                        _neuralAnim = gameObject.AddComponent<NeuralAnimationController>();
                        if (!_neuralAnim.HasAnyModel())
                        {
                            Destroy(_neuralAnim);   // 모델 없는 인스턴스는 뼈 기록 위험 → 제거
                            _neuralAnim = null;
                            Debug.Log("[PlayerMovement] Neural 모델 없음 → NeuralAnimationController 미부착 (Player_AC 재생 보호)");
                        }
                    }
                    if (_neuralAnim != null && _neuralAnim.HasAnyModel())
                    {
                        _neuralAnim.SetVelocityProvider(this);
                    }

                    // HybridAnimationController 설정 (같은 GameObject) — 신경 모델이 존재할 때만 부착.
                    // 모델 없는 Hybrid는 LateUpdate에서 뼈 localRotation을 zero-quaternion으로 덮어써
                    // Player_AC 애니메이션을 얼려버림(뼈 미작동 원인). 미부착으로 원천 차단.
                    if (_neuralAnim != null && _neuralAnim.HasAnyModel())
                    {
                        _hybridAnim = GetComponent<HybridAnimationController>();
                        if (_hybridAnim == null)
                            _hybridAnim = gameObject.AddComponent<HybridAnimationController>();

                        // ProgressiveRolloutManager에 등록 (Phase 4.6.2)
                        if (ProgressiveRolloutManager.Instance != null)
                            ProgressiveRolloutManager.Instance.ConfigureHybridController(_hybridAnim);
                    }
                    else
                    {
                        _hybridAnim = null;
                        Debug.Log("[PlayerMovement] Neural 모델 없음 → HybridAnimationController 미부착 (Player_AC 재생 보호)");
                    }

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
                    // 지형 표면 위에 캡슐 중심(height/2=1.0)을 두어 바닥이 지면에 닿게 스폰
                    transform.position = new Vector3(spawnPos.x, spawnGroundY + 1.0f, spawnPos.z);
            
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
            HandleCameraInput();
            CamForwardProbe();
            WatchAndFixGround();

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

        /// <summary>LateUpdate: 모든 스크립트/Cinemachine 이후에 카메라를 최종 적용 — 플레이어 추적 보장.</summary>
        private void LateUpdate()
        {
            ApplyFollowCamera();
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
            }
            else
            {
                _moveDirection = Vector3.zero;
            }

            // ── 마우스 커서 조준: 항상 커서가 가리키는 지면 지점을 바라봄 (탑다운 스트레이프 — Diablo/Hades 방식) ──
            // 이동 중에도 커서 방향 유지 → 걷기 애니와 독립적으로 몸이 커서를 따라 회전
            {
                var aimMouse = UnityEngine.InputSystem.Mouse.current;
                var aimCamSource = _cameraTransform != null ? _cameraTransform : (Camera.main != null ? Camera.main.transform : null);
                if (aimMouse != null && aimCamSource != null)
                {
                    var aimCam = aimCamSource.GetComponent<Camera>();
                    if (aimCam != null)
                    {
                        var aimRay = aimCam.ScreenPointToRay(aimMouse.position.ReadValue());
                        var groundPlane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
                        if (groundPlane.Raycast(aimRay, out float aimEnter))
                        {
                            Vector3 aimPoint = aimRay.GetPoint(aimEnter);
                            Vector3 aimDir = aimPoint - transform.position;
                            aimDir.y = 0f;
                            if (aimDir.sqrMagnitude > 0.25f)
                            {
                                var aimRot = Quaternion.LookRotation(aimDir.normalized, Vector3.up);
                                transform.rotation = Quaternion.Slerp(transform.rotation, aimRot, CursorTurnSpeed * Time.deltaTime);
                            }
                        }
                    }
                }
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

        /// <summary>탑다운(3/4 뷰) 카메라: 마우스 회전 + 휠 줌 + 플레이어 추적.
        /// Cinemachine vcam의 Follow가 배치에서 직렬화 안 돼 카메라가 고정되던 문제는
        /// vcam/Brain을 완전 비활성하고 LateUpdate에서 강제 적용하는 것으로 차단한다.</summary>
        private float _camYaw = 0f;
        private float _camPitch = 65f;
        private float _camDistance = 11f;
        private const float CamDistanceMin = 4f;
        private const float CamDistanceMax = 30f;
        private const float ZoomStepPerNotch = 1.5f;
        private const float MouseSensitivity = 0.12f;
        // 몸 회전 속도 (Slerp 계수/초) — 이동 방향/커서 조준 공용
        private const float TurnSpeed = 12f;
        private const float CursorTurnSpeed = 10f;
        private float _followProbeTimer = 0f;
        private int _followProbeCount = 0;
        private const float PitchMin = 30f;   // 탑다운 유지 (너무 수평 안 되게)
        private const float PitchMax = 82f;   // 거의 수직 탑다운까지
        private bool _cinemachineDisabled = false;

        /// <summary>Update: 마우스 델타/휠 입력만 처리 (카메라 적용은 LateUpdate).</summary>
        private void HandleCameraInput()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return;

            // ── 커서 좌우 위치 → 카메라 요(yaw) 소프트 패닝 ──
            // 커서가 화면 중앙 35% 데드존 안이면 유지, 좌우 가장자리로 갈수록
            // 최대 90°/s로 카메라가 해당 방향으로 회전 (RTS식 소프트 팬)
            var cursorPos = mouse.position.ReadValue();
            var camPixel = Camera.main != null
                ? new Vector2(Camera.main.pixelWidth, Camera.main.pixelHeight)
                : new Vector2(1920f, 1080f);
            float nx = camPixel.x > 1f ? (cursorPos.x / camPixel.x) * 2f - 1f : 0f; // -1(좌) ~ +1(우)
            if (Mathf.Abs(nx) > 0.35f)
            {
                float edge = (Mathf.Abs(nx) - 0.35f) / 0.65f;          // 0(데드존 끝) ~ 1(가장자리)
                _camYaw += Mathf.Sign(nx) * edge * 90f * Time.deltaTime;
            }

            // ── 커서 상하 위치 → 카메라 피치 소프트 팬 ──
            // 위쪽 = 시야 앞쪽(40°, 카메라 낮아짐) / 아래쪽 = 수직 탑다운(80°, 카메라 높아짐)
            float ny = camPixel.y > 1f ? (cursorPos.y / camPixel.y) * 2f - 1f : 0f; // -1(위) ~ +1(아래)
            if (Mathf.Abs(ny) > 0.35f)
            {
                float edgeY = (Mathf.Abs(ny) - 0.35f) / 0.65f;
                // 부호 반전: 커서 위(ny=+1) = 피치 감소(40°, 전방 시야) / 커서 아래(ny=-1) = 피치 증가(80°, 탑다운)
                // Input System은 y축 원점이 화면 하단 → ny=+1이 "위". 90°/s로 부드럽게.
                _camPitch = Mathf.Clamp(_camPitch - Mathf.Sign(ny) * edgeY * 90f * Time.deltaTime, 40f, 80f);
            }

            // 줌은 항상 휠
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.001f)
                _camDistance = Mathf.Clamp(_camDistance - Mathf.Sign(scroll) * ZoomStepPerNotch, CamDistanceMin, CamDistanceMax);
        }

        /// <summary>LateUpdate: 모든 스크립트/Cinemachine 이후에 카메라를 최종 적용 — 플레이어 추적 보장.</summary>
        private bool _cameraUnparented = false;

        private void ApplyFollowCamera()
        {
            if (_cameraTransform == null)
            {
                if (Camera.main != null) { _cameraTransform = Camera.main.transform; _camera = Camera.main; }
                else return;
            }

            // ── 카메라가 본/프롭의 자식으로 저장돼 있으면 분리 (과거 세션 잔재) ──
            // 부모가 본이면 애니메이션/이동에 카메라가 끌려가 추적이 깨진다. 루트로 분리 후 월드 추적.
            if (!_cameraUnparented && _cameraTransform.parent != null)
            {
                _cameraTransform.SetParent(null, true); // worldPositionStays
                _cameraUnparented = true;
                Debug.LogWarning($"[PlayerMovement] 카메라 부모 분리: '{_cameraTransform.parent?.name}'에서 루트로 (본 부착 잔해 제거)");
            }

            // Cinemachine 완전 차단 (1회): vcam 비활성 + Brain 비활성(문자열/순회 이중화)
            if (!_cinemachineDisabled)
            {
                _cinemachineDisabled = true;

                var vcam = GameObject.Find("Player Camera");
                if (vcam != null && vcam != gameObject)
                {
                    vcam.SetActive(false);
                    Debug.Log("[PlayerMovement] vcam('Player Camera') 비활성 — Follow null로 카메라 고정되던 원인 차단");
                }

                bool brainFound = false;
                var brain = _camera.GetComponent("CinemachineBrain") as Behaviour;
                if (brain != null) { brain.enabled = false; brainFound = true; }
                if (!brainFound)
                {
                    foreach (var comp in _camera.GetComponents<Component>())
                    {
                        if (comp != null && comp.GetType().Name == "CinemachineBrain")
                        {
                            ((Behaviour)comp).enabled = false;
                            brainFound = true;
                            break;
                        }
                    }
                }
                Debug.Log($"[PlayerMovement] CinemachineBrain 비활성={brainFound} → 탑다운 카메라가 플레이어 추적");

                // 구버전 TopDownCameraController가 LateUpdate에서 transform을 덮어써
                // 피치가 ±10°로 고정(사실상 정지)되는 원인 — 제거한다. (같은 namespace 참조)
                var tdc = _camera.GetComponent<TopDownCameraController>();
                if (tdc != null)
                {
                    Object.Destroy(tdc);
                    Debug.Log("[PlayerMovement] 구버전 TopDownCameraController 제거 — 피치가 덮어써져 정지하던 원인 차단");
                }
            }

            Transform playerT = transform;
            Quaternion orbitRot = Quaternion.Euler(_camPitch, _camYaw, 0f);
            Vector3 camPos = playerT.position + new Vector3(0f, 1.4f, 0f) + orbitRot * new Vector3(0f, 0f, -_camDistance);

            // 카메라가 지형 밑으로 못 가게 지표면 위 0.6m 유지
            try
            {
                float gY = 1f + ProjectName.Systems.TerrainGenerator.GetHeightAt(
                    camPos.x, camPos.z, ProjectName.Core.Data.BiomeType.Plains, 42);
                if (camPos.y < gY + 0.6f) camPos.y = gY + 0.6f;
            }
            catch (System.Exception) { }

            _cameraTransform.position = camPos;
            Vector3 lookTarget = playerT.position + new Vector3(0f, 1.2f, 0f);
            _cameraTransform.rotation = Quaternion.LookRotation(lookTarget - camPos);

            // 추적 진단 (처음 10회, 2초 간격): 카메라가 실제로 플레이어를 따라가는지 수치 확인
            _followProbeTimer += Time.deltaTime;
            if (_followProbeTimer >= 2f && _followProbeCount < 10)
            {
                _followProbeTimer = 0f;
                _followProbeCount++;
                Debug.Log($"[FollowProbe#{_followProbeCount}] camPos={camPos:F1} playerPos={playerT.position:F1} dist={Vector3.Distance(camPos, playerT.position):F1}m");
            }
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

        // 지형 상태 지속 감시 + 자동 복구: Ground_Inner가 언제/왜 안 보이게 되는지 포착
        private string _lastGroundState = "";
        private GameObject _groundWatchCache;
        private void WatchAndFixGround()
        {
            if (_groundWatchCache == null)
                _groundWatchCache = GameObject.Find("Ground_Inner");
            var g = _groundWatchCache;
            if (g == null)
            {
                if (_lastGroundState != "GONE")
                {
                    Debug.LogWarning("[GroundWatch] Ground_Inner가 씬에서 사라짐!");
                    _lastGroundState = "GONE";
                }
                return;
            }

            var mrW = g.GetComponent<MeshRenderer>();
            var mfW = g.GetComponent<MeshFilter>();
            string state = $"active={g.activeInHierarchy} mrEnabled={(mrW != null ? mrW.enabled.ToString() : "noMR")} mesh={(mfW != null && mfW.sharedMesh != null ? mfW.sharedMesh.name : "NULL")} vtx={(mfW != null && mfW.sharedMesh != null ? mfW.sharedMesh.vertexCount : 0)}";

            if (state != _lastGroundState)
            {
                Debug.Log($"[GroundWatch] 상태변화: {state}");
                _lastGroundState = state;
            }

            // 자동 복구: 비활성/렌더러꺼짐/메시없음이면 즉시 복구
            bool broken = !g.activeInHierarchy || (mrW != null && !mrW.enabled) || (mfW == null || mfW.sharedMesh == null);
            if (broken)
            {
                if (!g.activeSelf) g.SetActive(true);
                if (mrW != null && !mrW.enabled) mrW.enabled = true;
                Debug.LogWarning($"[GroundWatch] 지형 상태 이상 감지 → 자동 복구 시도: {state}");
            }
        }


        /// <summary>
        /// 지형 높이 함수(GetHeightAt)로 현재 x,z의 지표면 세계 y를 계산해,
        /// 점프 중이 아닐 때 플레이어를 지표면 위에 "온전히 서게" 고정한다.
        /// NOTE: CharacterController position = 캡슐 중심(height 2)이므로
        ///       position.y = 표면 + height/2 여야 캡슐 바닥이 지면에 닿는다.
        ///       (이전 표면+0.05는 캡슐 하단 1m가 지형에 파묻혀 위에서 안 보였음)
        /// </summary>
        private void ClampToGroundByHeight()
        {
            if (_controller == null) return;

            // 점프 중이면 지표면 고정하지 않음 (점프 상승)
            if (_isJumping) return;

            // 1) 실제 지형 콜라이더 raycast (와인딩 픽스로 이제 위에서 맞음) — Ground 레이어만
            float surfaceY = float.MinValue;
            RaycastHit[] gHits = Physics.RaycastAll(
                new Vector3(transform.position.x, transform.position.y + 30f, transform.position.z),
                Vector3.down, 120f, 1 << 9, QueryTriggerInteraction.Ignore);
            foreach (var gh in gHits)
            {
                if (gh.collider == null || gh.collider.transform.IsChildOf(transform)) continue;
                if (gh.point.y > surfaceY) surfaceY = gh.point.y; // 가장 높은 지면 = 실제 표면
            }

            // 2) 수식 fallback/하한 (Ground y=1 + 지형 높이) — raycast 실패 시에도 안전
            float formulaY;
            try
            {
                formulaY = 1f + ProjectName.Systems.TerrainGenerator.GetHeightAt(
                    transform.position.x, transform.position.z,
                    ProjectName.Core.Data.BiomeType.Plains, 42);
            }
            catch (System.Exception) { formulaY = 1.24f; }
            if (surfaceY < formulaY) surfaceY = formulaY;

            // 3) 캡슐 중심 = 표면 + height/2 → 바닥이 지면에 닿음 (파묻힘 해소)
            float targetY = surfaceY + _controller.height * 0.5f + 0.02f;
            if (Mathf.Abs(transform.position.y - targetY) > 0.001f)
            {
                transform.position = new Vector3(transform.position.x, targetY, transform.position.z);
                _verticalVelocity = 0f;
                _isGrounded = true;
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