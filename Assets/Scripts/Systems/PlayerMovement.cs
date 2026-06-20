using UnityEngine;
using UnityEngine.InputSystem;
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
    public class PlayerMovement : MonoBehaviour
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

        // --- 발소리 타이머 ---
        private float _footstepTimer = 0f;

        // --- 구르기 관련 ---
        private bool _isRolling = false;
        private Vector3 _rollDirection;
        private float _rollTimer = 0f;
        private float _lastRollTime = -10f;

        // --- 더블탭 구르기 관련 ---
        private enum KeyDirection { Up, Down, Left, Right }
        private float[] _lastKeyTime = new float[4]; // 각 방향키 마지막 누른 시간
        private KeyDirection[] _keyToDirection = new KeyDirection[4]; // key index to direction

        // --- 카메라 효과 관련 ---
        private float _defaultFOV;
        private float _dashFOVMultiplier = 1.1f; // 10% 줌아웃
        private float _cameraShakeTimer = 0f;
        private float _cameraShakeDuration = 0f;
        private float _cameraShakeIntensity = 0f;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (_controller == null)
                Debug.LogError("[PlayerMovement] CharacterController가 필요합니다!");

            // 메인 카메라 찾기
            if (Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
                _camera = Camera.main;
                _defaultFOV = _camera.fieldOfView;
            }
            else
            {
                Debug.LogError("[PlayerMovement] 씬에 MainCamera 태그가 있는 카메라가 없습니다!");
            }

            _keyboard = Keyboard.current;
            _stamina = _maxStamina;
        }

        private void Update()
        {
            HandleMovement();
            HandleRoll();
            HandleJump();
            ApplyGravity();
            HandleStamina();
            MovePlayer();
            HandleInteraction(); // C16-02: E 키 상호작용
            HandleCameraShake();
            HandleDashCameraEffect();

            // Phase 8.3: 발소리 (땅에 닿고 이동 중)
            HandleFootstepSound();
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
            DetectDoubleTap(wPressed, sPressed, aPressed, dPressed);

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
        }

        /// <summary>
        /// 더블탭 방향키 감지 — 같은 방향키를 _doubleTapTimeWindow 내에 두 번 누르면 구르기
        /// </summary>
        private void DetectDoubleTap(bool wPressed, bool sPressed, bool aPressed, bool dPressed)
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
            if (_keyboard == null) return;

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
                if (_controller.height > 1.0f)
                {
                    _controller.height = Mathf.Lerp(_controller.height, 1.0f, Time.deltaTime * 10f);
                }

                // 구르기 종료
                if (_rollTimer >= _rollDuration)
                {
                    _isRolling = false;
                    _rollTimer = 0f;
                    _controller.height = 2.0f; // 원래 높이로 복구 (기본값)

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

            Debug.Log($"[PlayerMovement] 🌀 구르기 시작! 방향: {_rollDirection}");
        }

        private void HandleJump()
        {
            if (_keyboard == null) return;

            // 구르기 중 점프 불가
            if (_isRolling) return;

            if (_keyboard.spaceKey.wasPressedThisFrame && _isGrounded)
            {
                _verticalVelocity = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
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
            _isGrounded = _controller.isGrounded;

            if (_isGrounded && _verticalVelocity < 0)
            {
                _verticalVelocity = -2f;
            }

            _verticalVelocity += _gravity * Time.deltaTime;

            if (!_isRolling)
            {
                _moveDirection.y = _verticalVelocity;
            }
        }

        private void MovePlayer()
        {
            if (_isRolling)
            {
                // 구르기 중에는 HandleRoll에서 이미 Move 처리
                return;
            }

            Vector3 motion = _moveDirection * _currentSpeed * _speedModifier;
            motion.y = _verticalVelocity;
            _controller.Move(motion * Time.deltaTime);
        }

        /// <summary>
        /// 카메라 흔들림 트리거
        /// </summary>
        private void TriggerCameraShake(float duration, float intensity)
        {
            _cameraShakeTimer = duration;
            _cameraShakeDuration = duration;
            _cameraShakeIntensity = intensity;
        }

        /// <summary>
        /// 카메라 흔들림 효과 처리
        /// </summary>
        private void HandleCameraShake()
        {
            if (_cameraShakeTimer > 0f && _cameraTransform != null)
            {
                _cameraShakeTimer -= Time.deltaTime;

                float progress = 1f - (_cameraShakeTimer / _cameraShakeDuration);
                float decay = 1f - Mathf.Clamp01(progress);
                float shakeAmount = _cameraShakeIntensity * decay;

                Vector3 shakeOffset = new Vector3(
                    Random.Range(-1f, 1f) * shakeAmount,
                    Random.Range(-1f, 1f) * shakeAmount,
                    Random.Range(-1f, 1f) * shakeAmount
                );

                _cameraTransform.localPosition += shakeOffset;
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
        /// </summary>
        private void HandleFootstepSound()
        {
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
        public bool IsGrounded => _isGrounded;
        public bool IsSprinting => _keyboard != null && _keyboard.leftShiftKey.isPressed && _moveDirection.magnitude > 0.1f;
        public bool IsDashing => _isDashing;
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
    }
}