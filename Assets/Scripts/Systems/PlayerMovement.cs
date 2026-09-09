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

        // 이동 가속/감속 평활 속도 — 0↔5m/s 즉발 전환 제거(Idle↔Walk↔Run 애니 사이클 급전환·클립 재시작 방지).
        // 가속 12m/s², 감속 18m/s²로 평면 속도만 램프 (방향은 즉시 반영 — 조작감 유지).
        private float _smoothedPlanarSpeed;

        // ── Input System 키보드 참조 (W키 간헐 드랍 수정: 캐시 금지, 매 접근 즉시 조회) ──
        // 과거 `_keyboard = Keyboard.current`를 1회 캐시하면 Input System이 디바이스를
        // 재열거(장치 재연결/포커스 복귀 등)할 때 캐시 참조가 stale이 되어 isPressed가
        // 계속 false로 읽히는 클래식 이슈(걷다가 W가 안 들림 → 이동 정지 → Idle 전환)가 발생했다.
        // 캐시 없이 매번 Keyboard.current를 새로 읽는다. AddDevice 등 무리한 재생성은 하지 않는다.
        private static Keyboard CurrentKeyboard
        {
            get
            {
                var kb = Keyboard.current;
                if (kb != null) return kb;
                // 1회 재시도: 디바이스 목록을 직접 재열거해 Keyboard를 다시 찾는다 (재연결 직후 대비)
                var devices = InputSystem.devices;
                for (int i = 0; i < devices.Count; i++)
                {
                    if (devices[i] is Keyboard enumerated) return enumerated;
                }
                return null;
            }
        }

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

        // --- T2B-3 착지 흡수 (공중→접지 전환 체감 완화 — 하드 텔레포트/위치 스냅 없이) ---
        private bool _wasAirborne = false;        // 직전 프레임 공중 여부 (_isJumping || !_isGrounded)
        private float _airPeakFallSpeed = 0f;     // 공중 중 최대 낙하속도(m/s) — 강착지 판정용
        private float _landingDampTimer = 0f;     // 착지 직후 가속 램프 완화 지속시간

        // --- 더블탭 구르기 관련 ---
        private enum KeyDirection { Up, Down, Left, Right }
        // 게임 시작 직후 첫 키 입력이 더블탭으로 오인되지 않도록 음수로 초기화
        private float[] _lastKeyTime = new float[] { -10f, -10f, -10f, -10f };

        // ── DD5: WASD 입력 상태 에지 프로브 (Play 시작 120초 진단 창, 에지에서만 로그) ──
        // W키 간헐 드랍 검증용. isPressed를 순수 폴링해 상승/하강 에지에서만 1줄 출력
        // (홀드 중엔 무출력 → 스팸 방지). try-catch 없음, 동작 변경 없음.
        private const float InputProbeDuration = 120f;
        private float _inputProbeStartTime = -1f;
        private bool _inputProbeActive = false;
        private readonly bool[] _probePrevWASD = new bool[4];          // 이전 프레임 W/A/S/D 상태
        private readonly int[] _probeDownCount = new int[4];           // 상승 에지(down) 횟수
        private readonly int[] _probeUpCount = new int[4];             // 하강 에지(up) 횟수
        private readonly float[] _probePressStartTime = new float[4];  // 현재 홀드 시작 시각
        private readonly float[] _probeMaxHold = new float[4];         // 최대 홀드 시간(초)
        private static readonly string[] ProbeKeyNames = { "W", "A", "S", "D" };

        // --- 은신 관련 (Phase 34) ---
        private bool _stealthToggleHeld = false; // C 키 홀드 상태 추적

        // 웅크림(LeftCtrl 토글) / 수영(수면 근처 부유) 상태
        private bool _isCrouching = false;
        private bool _crouchToggleHeld = false;  // LeftCtrl 상승엣지 추적
        private bool _isSwimming = false;        // 수면 근처 부유 상태

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

        // AA3: 접지감용 동적 블롭 섀도우 (Start에서 1회 GetOrAdd 부착)
        private BlobShadow _blobShadow;

        // 저장된 CharacterController 초기 높이 (구르기 복원용)
        private float _originalControllerHeight = 2f;

        // --- 월드 경계 클램프 (지형 ±1600m 확장, 안쪽 10m 마진) ---
        private const float WorldBound = 1590f;

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
                    // 자동 부착 금지(2026-09-05 정책) — 씬에 명시 배치된 경우만 사용
                    _rigAnim = GetComponent<RigAnimationController>();

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

                    // W키 드랍 수정: 키보드 참조 캐시 제거 — 모든 접근은 CurrentKeyboard 즉시 조회로 대체
                    _stamina = _maxStamina;

                    // PlayerModel 자식에서 ProceduralAnimationController 찾기
                    _proceduralAnim = GetComponentInChildren<ProceduralAnimationController>();
                    if (_proceduralAnim == null)
                    {
                        Transform model = transform.Find("PlayerModel");
                        if (model != null)
                            _proceduralAnim = model.GetComponent<ProceduralAnimationController>();
                    }

                    // 애니 정책(2026-09-05): Neural/Hybrid 보류 — Player_AC(HumanoidClipDriver) 단일 경로.
                    // Phase 67 유산 자동부착 제거. _neuralAnim/_hybridAnim은 씬에 명시 배치된 경우에만 GetComponent로 획득.
                    _neuralAnim = GetComponent<NeuralAnimationController>();
                    _hybridAnim = GetComponent<HybridAnimationController>();

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

        /// <summary>
        /// AA3: 플레이어 동적 접촉 그림자(BlobShadow) 1회 부착(GetOrAdd).
        /// BlobShadow(같은 폴더 Assets/Scripts/Systems/BlobShadow.cs)는 자체 Start에서
        /// 런타임 생성되고 LateUpdate에서 GetHeightAt + GROUND_BASE(1f)로 지면을 추적하므로
        /// 플레이어 GameObject에만 부착하면 발밑에 고정 접지 그림자가 따라다닌다.
        /// 크기/알파는 컴포넌트 내장 상수(반경 0.8m, 알파 0.35)를 사용한다 — 외부 API 없음.
        /// 부착 실패 시 경고 1회만 남기고 Play를 중단하지 않는다(회귀 방지).
        /// </summary>
        private void Start()
        {
            try
            {
                _blobShadow = GetComponent<BlobShadow>();          // GetOrAdd — 중복 부착 방지
                if (_blobShadow == null)
                    _blobShadow = gameObject.AddComponent<BlobShadow>();
                if (_blobShadow != null)
                {
                    _blobShadow.enabled = true;
                    Debug.Log("[PlayerMovement] ✅ BlobShadow 동적 부착 완료 (발밑 접지 그림자, 내장 r=0.8/α=0.35)");
                }
            }
            catch (System.Exception e)
            {
                // 그림자 부재는 치명적이지 않다 — 경고 후 그림자 없이 계속 진행
                Debug.LogWarning($"[PlayerMovement] BlobShadow 부착 실패 — 그림자 없이 계속: {e.Message}");
                _blobShadow = null;
            }
        }

        private void Update()
        {
            // DD5: WASD 입력 에지 프로브 (Play 시작 120초 동안만 — 진단 전용, 동작 변경 없음)
            UpdateInputProbe();

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
            HandleSwimming(); // 수영 판정 + 부유 보정 (중력/이동/접지 적용 후)
            HandleInteraction(); // C16-02: E 키 상호작용
            HandleCameraShake();
            HandleDashCameraEffect();

            // Phase 8.3: 발소리 (땅에 닿고 이동 중)
            HandleFootstepSound();

            // Phase 34: 은신 입력 처리
            HandleStealthInput();

            // 웅크림 입력 처리 (LeftCtrl 상승엣지 토글)
            HandleCrouchInput();

            // Phase 34: 은신 중 암살 가능 체크 (StealthSystem으로 위임)
            // Phase 34: 은신 상태에서 속도 제한은 HandleMovement()에서 직접 적용 (_walkSpeed * 0.5f)
        }

        /// <summary>
        /// DD5: WASD 입력 상태 에지 프로브 — Play 시작 120초 동안만 활성.
        /// 매 프레임 isPressed를 폴링해 상승/하강 에지에서만 1줄 로그 (홀드 중엔 무출력).
        /// 형식: [InputProbe] t=12.3s W=down (전체상태 W=1 A=0 S=0 D=0)
        /// 120초 경과 후 키별 down/up 횟수와 최대 홀드 시간 요약 1줄 출력 후 종료.
        /// </summary>
        private void UpdateInputProbe()
        {
            // 최초 호출에서 진단 창 시작 시각 기록
            if (_inputProbeStartTime < 0f)
            {
                _inputProbeStartTime = Time.time;
                _inputProbeActive = true;
                Debug.Log("[InputProbe] 시작 — 120초간 WASD 에지 로깅 (down/up 순간에만 출력)");
            }

            float t = Time.time - _inputProbeStartTime;

            // 진단 창 종료 → 요약 1줄 출력 후 영구 비활성
            if (t > InputProbeDuration)
            {
                if (_inputProbeActive)
                {
                    _inputProbeActive = false;
                    // 120초 시점에 아직 홀드 중인 키의 홀드 시간도 최대값에 반영
                    for (int i = 0; i < 4; i++)
                    {
                        if (_probePrevWASD[i])
                        {
                            float hold = Time.time - _probePressStartTime[i];
                            if (hold > _probeMaxHold[i]) _probeMaxHold[i] = hold;
                        }
                    }
                    Debug.Log($"[InputProbe] 종료 — W down={_probeDownCount[0]}/up={_probeUpCount[0]} 최대홀드={_probeMaxHold[0]:F2}s, " +
                              $"A down={_probeDownCount[1]}/up={_probeUpCount[1]} 최대홀드={_probeMaxHold[1]:F2}s, " +
                              $"S down={_probeDownCount[2]}/up={_probeUpCount[2]} 최대홀드={_probeMaxHold[2]:F2}s, " +
                              $"D down={_probeDownCount[3]}/up={_probeUpCount[3]} 최대홀드={_probeMaxHold[3]:F2}s");
                }
                return;
            }

            var kb = CurrentKeyboard;
            if (kb == null) return;

            bool wNow = kb.wKey.isPressed;
            bool aNow = kb.aKey.isPressed;
            bool sNow = kb.sKey.isPressed;
            bool dNow = kb.dKey.isPressed;
            bool[] now = { wNow, aNow, sNow, dNow };

            for (int i = 0; i < 4; i++)
            {
                if (now[i] && !_probePrevWASD[i])
                {
                    // 상승 에지 (down)
                    _probeDownCount[i]++;
                    _probePressStartTime[i] = Time.time;
                    Debug.Log($"[InputProbe] t={t:F1}s {ProbeKeyNames[i]}=down (전체상태 W={(wNow ? 1 : 0)} A={(aNow ? 1 : 0)} S={(sNow ? 1 : 0)} D={(dNow ? 1 : 0)})");
                }
                else if (!now[i] && _probePrevWASD[i])
                {
                    // 하강 에지 (up)
                    _probeUpCount[i]++;
                    float hold = Time.time - _probePressStartTime[i];
                    if (hold > _probeMaxHold[i]) _probeMaxHold[i] = hold;
                    Debug.Log($"[InputProbe] t={t:F1}s {ProbeKeyNames[i]}=up (전체상태 W={(wNow ? 1 : 0)} A={(aNow ? 1 : 0)} S={(sNow ? 1 : 0)} D={(dNow ? 1 : 0)})");
                }
                _probePrevWASD[i] = now[i];
            }
        }

        /// <summary>LateUpdate: 모든 스크립트/Cinemachine 이후에 카메라를 최종 적용 — 플레이어 추적 보장.</summary>
        private void LateUpdate()
        {
            ApplyFollowCamera();
        }

        /// <summary>
        /// Phase 34: C 키 입력 → StealthSystem.ToggleStealth() 호출
        /// </summary>
        private void HandleStealthInput()
        {
            var kb = CurrentKeyboard;
            if (kb == null) return;

            // C 키 누름/뗌 토글 (상승엣지)
            bool cPressed = kb.cKey.isPressed;

            if (cPressed && !_stealthToggleHeld)
            {
                _stealthToggleHeld = true;
                if (StealthSystem.Instance != null)
                    StealthSystem.Instance.ToggleStealth();
            }
            else if (!cPressed && _stealthToggleHeld)
            {
                _stealthToggleHeld = false;
            }
        }

        /// <summary>
        /// 웅크림 입력: LeftCtrl 상승엣지(누를 때) → 웅크림 토글.
        /// 은신(HandleStealthInput)이 C 키 상승엣지를 사용하므로 키 충돌 없음.
        /// </summary>
        private void HandleCrouchInput()
        {
            var kb = CurrentKeyboard;
            if (kb == null) return;

            bool leftCtrlPressed = kb.leftCtrlKey.isPressed;
            if (leftCtrlPressed && !_crouchToggleHeld)
            {
                _crouchToggleHeld = true;
                _isCrouching = !_isCrouching;
            }
            else if (!leftCtrlPressed && _crouchToggleHeld)
            {
                _crouchToggleHeld = false;
            }
        }

        /// <summary>
        /// 수영 판정 + 부유 보정. Update 후반(중력/이동/접지 적용 후)에 호출.
        /// 수면 근처(pos.y+0.4 < 수면 && pos.y+1.2 > 수면-1.5)에 몸이 잠기면 수영 상태로
        /// 판정하고, 수면 아래 0.7m 높이에 떠 있도록 y를 보정한다(바닥이 더 높으면 바닥 우선).
        /// </summary>
        private void HandleSwimming()
        {
            if (_controller == null) return;

            Vector3 pos = transform.position;
            float surfaceY = NearestWaterSurfaceY(pos);

            // 수영 판정: 수면 근처 밴드(수면 위로 튀지 않고, 수면 아래 1.5m+1.2m 이내)
            _isSwimming = surfaceY != float.MinValue
                && pos.y + 0.4f < surfaceY
                && (pos.y + 1.2f) > surfaceY - 1.5f;

            if (!_isSwimming) return;

            // 부유 보정: y = Max(바닥, 수면 - 0.7) — 수면 위로 튀지 않게 하고 바닥 파묻힘도 방지
            float bottomCenterY;
            try
            {
                bottomCenterY = 1f + ProjectName.Systems.TerrainGenerator.GetHeightAt(
                    pos.x, pos.z, ProjectName.Core.Data.BiomeType.Plains, 42)
                    + _controller.height * 0.5f;
            }
            catch (System.Exception) { bottomCenterY = pos.y; }

            float swimY = Mathf.Max(bottomCenterY, surfaceY - 0.7f);
            if (!Mathf.Approximately(pos.y, swimY))
            {
                transform.position = new Vector3(pos.x, swimY, pos.z);
                _verticalVelocity = -2f;         // 하강속도 누적 방지(출수 시 급강하 방지)
                _moveDirection.y = _verticalVelocity;
                _isGrounded = false;             // 부유 중 — 접지 상태 아님
            }
        }

        /// <summary>
        /// 주어진 위치에서 가장 가까운 물 표면 y 반환. 호수(중심 거리 < radius*1.05)와
        /// 강(마스크 > 0.5)을 모두 검사하며, 물이 없으면 float.MinValue.
        /// NOTE: TerrainGenerator.LakesOrNull은 재귀 가드 프로퍼티로 런타임(생성 완료 후) 읽기 안전.
        /// </summary>
        private float NearestWaterSurfaceY(Vector3 pos)
        {
            float best = float.MinValue;

            var lakes = ProjectName.Systems.TerrainGenerator.LakesOrNull;
            if (lakes != null)
            {
                for (int i = 0; i < lakes.Count; i++)
                {
                    var lake = lakes[i];
                    float dx = pos.x - lake.center.x;
                    float dz = pos.z - lake.center.z;
                    float r = lake.radius * 1.05f;
                    if (dx * dx + dz * dz < r * r && lake.waterLevel > best)
                        best = lake.waterLevel;
                }
            }

            if (ProjectName.Systems.TerrainGenerator.GetRiverMask(pos.x, pos.z) > 0.5f)
            {
                float riverY = ProjectName.Systems.TerrainGenerator.GetRiverSurfaceY(pos.x, pos.z);
                if (riverY > best) best = riverY;
            }

            return best;
        }

        /// <summary>
        /// C16-02: E 키 입력 감지 → 근처 Bed 찾기 → 상호작용
        /// </summary>
        private void HandleInteraction()
        {
            var kb = CurrentKeyboard;
            if (kb == null) return;

            // E 키 (wasPressedThisFrame: 눌린 순간만 반응)
            if (kb.eKey.wasPressedThisFrame)
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
            // W키 드랍 수정: 캐시된 _keyboard 대신 매 프레임 즉시 조회 (stale 참조 차단)
            var kb = CurrentKeyboard;
            if (kb == null)
            {
                // 디바이스 부재 → 이동 입력 0 처리 (기존 null 가드 유지 + 관성 이동 방지)
                _moveDirection = Vector3.zero;
                return;
            }

            // 구르기 중에는 이동 입력을 새로운 방향으로 변경하지 않음
            if (_isRolling) return;

            float horizontal = 0;
            float vertical = 0;

            bool wPressed = kb.wKey.isPressed || kb.upArrowKey.isPressed;
            bool sPressed = kb.sKey.isPressed || kb.downArrowKey.isPressed;
            bool aPressed = kb.aKey.isPressed || kb.leftArrowKey.isPressed;
            bool dPressed = kb.dKey.isPressed || kb.rightArrowKey.isPressed;

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
            // T2B-1: 조준 회전이 실제로 개입한 프레임만 표시 — 개입 시 아래 이동 방향 회전은 건너뜀(충돌 방지)
            bool aimRotationApplied = false;
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
                                aimRotationApplied = true; // 조준 회전 개입 — 이동 방향 회전 생략 (기존 조준 동작 보존)
                            }
                        }
                    }
                }
            }

            // 걷기/달리기/대쉬
            bool sprintKey = kb.leftShiftKey.isPressed; // kb는 위 가드에서 null 아님 보장
            bool hasStamina = _stamina > 0f;
            bool isMoving = _moveDirection.magnitude > 0.1f;

            // T2B-1: 방향 전환 스무딩 — 조준 회전이 개입하지 않은 프레임에서만,
            // 이동 중 루트가 _moveDirection 방향으로 급격히 돌지 않고 부드럽게 회전하도록 Slerp.
            // (조준 중이면 위 커서 조준 Slerp가 우선 — 중복 회전/진동 방지. 구르기 중은 HandleRoll 소관.)
            if (!aimRotationApplied && isMoving && !_isRolling)
            {
                Vector3 moveDir = new Vector3(_moveDirection.x, 0f, _moveDirection.z); // y는 ApplyGravity의 vv — 제외
                if (moveDir.sqrMagnitude > 0.01f)
                {
                    var moveRot = Quaternion.LookRotation(moveDir.normalized, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, moveRot, MoveTurnSpeed * Time.deltaTime);
                }
            }

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

            // 웅크림 중 속도 50% (은신과 동일 패턴)
            if (_isCrouching)
            {
                _currentSpeed = _walkSpeed * 0.5f;
                _isDashing = false; // 웅크림 중 대쉬 불가
            }

            // 수영 중 속도 60% (웅크림과 중첩 시 곱연산)
            if (_isSwimming)
            {
                _currentSpeed *= 0.6f;
                _isDashing = false; // 수영 중 대쉬 불가
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
            var kb = CurrentKeyboard;
            if (kb == null) return;

            // 각 키의 wasPressedThisFrame 확인
            if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame)
                CheckDoubleTap(KeyDirection.Up);
            if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame)
                CheckDoubleTap(KeyDirection.Down);
            if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame)
                CheckDoubleTap(KeyDirection.Left);
            if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame)
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
            var kb = CurrentKeyboard;
            if (kb == null || _controller == null) return;

            // Q 키 구르기 + 더블탭 구르기 조건
            if (kb.qKey.wasPressedThisFrame && !_isRolling && 
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
                ClampToWorldBounds(); // 구르기 이동도 월드 경계 안으로 클램프

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
            if (_isSwimming) return; // 수영 중 구르기 무시

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
            var kb = CurrentKeyboard;
            if (kb == null) return;

            // 구르기 중 점프 불가
            if (_isRolling) return;

            // 수영 중 점프 무시 (부유 상태에서 수면 위로 튀어오름 방지)
            if (_isSwimming) return;

            // 탑승 중 점프 불가 (MountSystem)
            if (MountSystem.Instance != null && MountSystem.Instance.IsMounted)
                return;

            // [JumpProbe] 점프 무반응 원인 분리 — Space 입력 순간 판정 변수 스냅샷(진단 로그 전용, 동작 변경 없음)
            if (kb.spaceKey.wasPressedThisFrame)
            {
                Debug.Log($"[JumpProbe] Space입력: grounded={_isGrounded} rolling={_isRolling} mountBlocked={(MountSystem.Instance != null && MountSystem.Instance.IsMounted)} vv={_verticalVelocity:F2}");
            }

            if (kb.spaceKey.wasPressedThisFrame && _isGrounded)
            {
                _verticalVelocity = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
                _isJumping = true;
                _isCrouching = false; // 점프 시 웅크림 해제
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

            // AA3: 물리 접촉 복구 — CC.isGrounded=false인데 아래 raycast(SphereCast)로
            // 지면이 확인되면 소량 하강 Move를 1회 실행해 CC가 실제로 지면과 충돌하게 만든다.
            // (ClampToGroundByHeight의 위치 고정이 CC 충돌 판정을 무력화해 isGrounded가
            //  false로 지속되던 문제의 보완. 점프 상승(vv>0)·구르기 중에는 개입하지 않는다.)
            if (!_controller.isGrounded && _isGrounded && _verticalVelocity < 0f && !_isRolling)
            {
                _controller.Move(Vector3.down * 0.02f);
                if (_controller.isGrounded)
                {
                    _verticalVelocity = -2f;   // 접지 규약 유지 (적은 하강속도로 접지 유지)
                    _isGrounded = true;
                }
            }

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

            // ── 이동 가속/감속 스무딩: 목표 속도 = 입력 있음 ? 기존 속도값(_currentSpeed × _speedModifier) : 0 ──
            // 방향은 즉시 반영(조작감 유지), 평면 속도만 램프: 가속 12m/s², 감속 18m/s².
            // 대시는 _currentSpeed=15로 target이 자동 상향되고, 구르기는 별도 경로(HandleRoll)라 무영향.
            // 주의: ApplyGravity()가 _moveDirection.y에 _verticalVelocity를 기록하므로 y 오염을 제거한
            //       평면 성분(x,z)만 판정/정규화에 사용한다 — 그대로 쓰면 정지 시 sqrMagnitude≈4로
            //       "입력 있음" 오판, 이동 시 크기 sqrt(1+vv²)로 나뉘어 실제 평면 속도가 절반 이하로 감소.
            Vector3 planarDir = new Vector3(_moveDirection.x, 0f, _moveDirection.z);
            bool hasInput = planarDir.sqrMagnitude > 0.01f;
            float targetSpeed = hasInput ? _currentSpeed * _speedModifier : 0f;

            // T2B-3: 착지 흡수 — 공중(점프/추락)→접지 전환 프레임에 가속 램프를 잠시 완화해
            // 착지 후 급출발/클립 팝 체감을 줄인다. 위치는 건드리지 않는다(하드 텔레포트 금지 —
            // ClampToGroundByHeight의 기존 접지 수리는 그대로 유지).
            bool airborneNow = !_isGrounded || _isJumping;
            if (airborneNow)
            {
                _airPeakFallSpeed = Mathf.Max(_airPeakFallSpeed, -_verticalVelocity); // 낙하 강도 추적
            }
            else if (_wasAirborne)
            {
                // 착지 프레임 — 강착지(낙하속도 > HardLandingSpeed)일 때만 짧고 약한 카메라 흔들림
                if (_airPeakFallSpeed > HardLandingSpeed)
                    TriggerCameraShake(0.1f, 0.08f);
                _landingDampTimer = LandingDampTime;
                _airPeakFallSpeed = 0f;
            }
            _wasAirborne = airborneNow;
            if (_landingDampTimer > 0f) _landingDampTimer -= Time.deltaTime;

            // 가속/감속 램프 — 착지 직후(_landingDampTimer > 0)만 가속 계수를 40%로 완화
            float accel = targetSpeed > _smoothedPlanarSpeed ? 12f : 18f;
            if (_landingDampTimer > 0f) accel *= 0.4f;
            _smoothedPlanarSpeed = Mathf.MoveTowards(
                _smoothedPlanarSpeed, targetSpeed,
                accel * Time.deltaTime);
            Vector3 motion = planarDir.normalized * _smoothedPlanarSpeed;
            motion.y = _verticalVelocity;
            _controller.Move(motion * Time.deltaTime);
            ClampToWorldBounds(); // 월드 경계 클램프 — 지형 밖(±1600m 초과) 이동 차단

            // === 지면 고정 (추락 영구 방지 / 지형 위 안착) ===
            // 물리 충돌·Raycast에 의존하지 않고, 지형을 만든 TerrainGenerator.GetHeightAt으로
            // 현재 x,z의 지표면 높이를 수학적으로 도출해 그 위에 붙인다.
            ClampToGroundByHeight();
            ApplySlopeAlignment(); // T2B-2: 경사 정렬(가벼운 개선) — 접지 수리 이후에만 미세 기울임
        }

        /// <summary>
        /// 월드 경계 클램프: XZ 위치를 ±WorldBound(1590m)로 강제 — 지형(±1600m) 밖 이동 차단.
        /// 위치만 보정하고 속도/상태(_verticalVelocity, _moveDirection 등)는 건드리지 않는다.
        /// </summary>
        private void ClampToWorldBounds()
        {
            var p = transform.position;
            float nx = Mathf.Clamp(p.x, -WorldBound, WorldBound);
            float nz = Mathf.Clamp(p.z, -WorldBound, WorldBound);
            if (nx != p.x || nz != p.z)
            {
                p.x = nx;
                p.z = nz;
                transform.position = p;
            }
        }

        /// <summary>탑다운(3/4 뷰) 카메라: 마우스 회전 + 휠 줌 + 플레이어 추적.
        /// Cinemachine vcam의 Follow가 배치에서 직렬화 안 돼 카메라가 고정되던 문제는
        /// vcam/Brain을 완전 비활성하고 LateUpdate에서 강제 적용하는 것으로 차단한다.</summary>
        private float _camYaw = 0f;
        // 2026-09-09(3): 에지 팬 비활성 — 커서 조준+카메라 팬 이중 반응 루프 차단(고정 오빗). true로 재활용 가능.
        private const bool CameraEdgePan = false;
        private Vector3 _camSmoothPos;                    // 카메라 평활 위치(끊김 제거)
        private float _camPitch = 65f;
        private float _camDistance = 11f;
        private const float CamDistanceMin = 4f;
        private const float CamDistanceMax = 30f;
        private const float ZoomStepPerNotch = 1.5f;
        private const float MouseSensitivity = 0.12f;
        // 몸 회전 속도 (Slerp 계수/초) — 이동 방향/커서 조준 공용
        private const float TurnSpeed = 12f;
        private const float CursorTurnSpeed = 10f;
        // T2B-1: 이동 방향 회전 (조준 회전이 개입하지 않는 프레임에만 사용 — CursorTurnSpeed와 독립)
        private const float MoveTurnSpeed = 9f;      // Slerp 계수/초 — 8~10 rad/s 대역, 급회전 방지
        // T2B-2: 경사 정렬 (가벼운 개선 — 과한 기울기 방지 상한)
        private const float SlopeTiltMaxDeg = 6f;    // 기울임 각도 상한
        private const float SlopeAlignSpeed = 6f;    // 정렬 Slerp 계수/초 — 서서히 들어가고 나옴
        // T2B-3: 착지 흡수
        private const float LandingDampTime = 0.2f;  // 착지 직후 가속 완화 지속시간
        private const float HardLandingSpeed = 8f;   // 낙하속도 이상이면 강착지 판정(카메라 흔들림)
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
            // 2026-09-09(3) FIX: 커서 조준(플레이어 회전)과 카메라 팬의 이중 반응 루프가
            // "카메라가 계속 도는/시선이 끊기는" 현상의 원인 — 에지 팬 비활성(고정 오빗, 휠 줌만 유지).
            // 재활용하려면 아래 CameraEdgePan을 true로.
            if (CameraEdgePan && Mathf.Abs(nx) > 0.35f)
            {
                float edge = (Mathf.Abs(nx) - 0.35f) / 0.65f;          // 0(데드존 끝) ~ 1(가장자리)
                _camYaw += Mathf.Sign(nx) * edge * 90f * Time.deltaTime;
            }

            // ── 커서 상하 위치 → 카메라 피치 소프트 팬 ──
            // 위쪽 = 시야 앞쪽(40°, 카메라 낮아짐) / 아래쪽 = 수직 탑다운(80°, 카메라 높아짐)
            float ny = camPixel.y > 1f ? (cursorPos.y / camPixel.y) * 2f - 1f : 0f; // -1(위) ~ +1(아래)
            if (CameraEdgePan && Mathf.Abs(ny) > 0.35f)
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
            // 2026-09-09(3) FIX: 실내 씬(IndoorScene) 활성 시 클램프 스킵 — 실내는 본 지형(y≈42) 아래 y=0에 있고,
            // 클램프가 카메라를 지표 위로 밀어올려 지형 표면만 보이던 것이 "실내 렌더링 실패"의 원인.
            // 지형 메시는 아래에서 보면 컬링되어 실내가 정상 렌더된다.
            try
            {
                bool indoorActive = UnityEngine.SceneManagement.SceneManager.GetSceneByName("IndoorScene").isLoaded;
                if (!indoorActive)
                {
                    float gY = 1f + ProjectName.Systems.TerrainGenerator.GetHeightAt(
                        camPos.x, camPos.z, ProjectName.Core.Data.BiomeType.Plains, 42);
                    if (camPos.y < gY + 0.6f) camPos.y = gY + 0.6f;
                }
            }
            catch (System.Exception) { }

            _cameraTransform.position = camPos;
            // 2026-09-09(3): 카메라 위치/시선 평활 — 직접 대입 스냅 제거(부드러운 추적)
            if (_camSmoothPos.sqrMagnitude < 0.001f) _camSmoothPos = camPos;
            _camSmoothPos = Vector3.Lerp(_camSmoothPos, camPos, 1f - Mathf.Exp(-14f * Time.deltaTime));
            _cameraTransform.position = _camSmoothPos;
            Vector3 lookTarget = playerT.position + new Vector3(0f, 1.2f, 0f);
            _cameraTransform.rotation = Quaternion.LookRotation(lookTarget - _camSmoothPos);

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
        // 감시 대상은 Ground_Inner 전용(GameObject.Find). 주의: 청크 완료 시 renderer 비활성은
        // RuntimeTerrainChunkManager의 정상 동작이므로 복구하지 않는다(오판 시 구/신 지형 이중 렌더).
        // 활성(activeInHierarchy)·메시 파손만 감시/복구한다.
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

            // 자동 복구: 비활성/메시없음이면 즉시 복구.
            // renderer 비활성(mrW.enabled==false)은 청크 매니저의 정상 완료 동작이므로 broken에 넣지 않음.
            bool broken = !g.activeInHierarchy || (mfW == null || mfW.sharedMesh == null);
            if (broken)
            {
                if (!g.activeSelf) g.SetActive(true);
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

            // 점프 중에는 지표면 고정하지 않음 (점프 상승)
            if (_isJumping) return;

            // ── 1) 물리 접촉 우선 ──
            // CC가 아직 접지 안 됐고 중력 하강 중이면 소량 하강 Move로 지형/데코/건물 콜라이더와
            // 실제 충돌하도록 유도. 데코 위에 서 있으면 여기서 자연 접지된다.
            if (!_controller.isGrounded && _verticalVelocity <= 0f && !_isRolling)
            {
                _controller.Move(Vector3.down * 0.05f);
                if (_controller.isGrounded)
                {
                    _verticalVelocity = -2f;   // 접지 규약 (적은 하강속도로 접지 유지)
                    _isGrounded = true;
                    return;                    // 물리 접지 성공 — 수식 개입 없음
                }
            }

            // ── 2) 이탈/추락 안전망 (수식, GetHeightAt = 지형 표면 함수) ──
            // 지형 메시는 TerrainTextureApplier가 GetHeightAt으로 재표본되므로 GetHeightAt은
            // 지형 표면과 정확히 일치. 물리 접지가 실패했을 때만 수식으로 구제한다.
            // (데코/건물 콜라이더 위에 서 있으면 feetY > formulaY라 여기 본문에 안 걸림 → 자연 유지)
            float formulaY;
            try
            {
                formulaY = 1f + ProjectName.Systems.TerrainGenerator.GetHeightAt(
                    transform.position.x, transform.position.z,
                    ProjectName.Core.Data.BiomeType.Plains, 42);
            }
            catch (System.Exception) { formulaY = 1.24f; }

            // 캡슐 바닥(feet) = center - height/2
            float feetY = transform.position.y - _controller.height * 0.5f;

            if (feetY < formulaY - 0.5f)
            {
                // 지표면보다 0.5m+ 아래 = 지형 콜라이더 유실 등 낙하 위험 → 수식으로 복귀
                transform.position = new Vector3(
                    transform.position.x,
                    formulaY + _controller.height * 0.5f + 0.02f,
                    transform.position.z);
                _verticalVelocity = 0f;
                _isGrounded = true;
            }
            else if (feetY < formulaY + 0.02f)
            {
                // 소량 파묻힘(캡슐 바닥이 지표면 살짝 아래) → 표면 위로 정렬
                transform.position = new Vector3(
                    transform.position.x,
                    formulaY + _controller.height * 0.5f + 0.02f,
                    transform.position.z);
                _verticalVelocity = -2f;
                _isGrounded = true;
            }
            // feetY ≥ formulaY+0.02 (데코 위, 구릉 정상 등) → 개입 안 함 — CC가 물리 접지 유지
        }

        /// <summary>
        /// T2B-2 경사 정렬(가벼운 개선): 접지 + 이동 중일 때 지형 경사 방향으로 몸을 최대 ±6°까지만 기울인다.
        /// 법선은 ClampToGroundByHeight와 동일한 TerrainGenerator.GetHeightAt 인접 표본(±0.5m)으로
        /// 수학적으로 추정 — 물리 쿼리 없음, 결정론적, 추가 콜라이더 비용 없음.
        /// 데코/건물 위(feetY가 지형 표면과 1m 이상 괴리)에서는 개입하지 않고, Slerp로 서서히
        /// 들어가고 나오므로 조준/이동의 요(yaw) 회전과 충돌하지 않는다(피치/롤만 미세 보정).
        /// </summary>
        private void ApplySlopeAlignment()
        {
            if (_controller == null || _isRolling) return;

            // 개입 조건(접지+이동 중) 벗어남 → 기울임을 서서히 해제(요 회전은 조준/이동 로직 소관)
            if (_isJumping || !_isGrounded || _smoothedPlanarSpeed < 0.1f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.Euler(0f, transform.eulerAngles.y, 0f),
                    SlopeAlignSpeed * Time.deltaTime);
                return;
            }

            float hC, hX, hZ;
            try
            {
                hC = ProjectName.Systems.TerrainGenerator.GetHeightAt(
                    transform.position.x, transform.position.z,
                    ProjectName.Core.Data.BiomeType.Plains, 42);
                hX = ProjectName.Systems.TerrainGenerator.GetHeightAt(
                    transform.position.x + 0.5f, transform.position.z,
                    ProjectName.Core.Data.BiomeType.Plains, 42);
                hZ = ProjectName.Systems.TerrainGenerator.GetHeightAt(
                    transform.position.x, transform.position.z + 0.5f,
                    ProjectName.Core.Data.BiomeType.Plains, 42);
            }
            catch (System.Exception) { return; }

            // 지형이 아닌 곳(데코/건물 위)은 높이 함수 기울기가 무의미 → 건너뜀
            float formulaY = 1f + hC;
            float feetY = transform.position.y - _controller.height * 0.5f;
            if (Mathf.Abs(feetY - formulaY) > 1f) return;

            // 표면 법선 추정: n = (-dh/dx, 1, -dh/dz)
            const float sample = 0.5f;
            Vector3 normal = new Vector3(-(hX - hC) / sample, 1f, -(hZ - hC) / sample).normalized;

            // 요(yaw)는 현재값 유지, 경사 성분만 피치/롤로 미세 반영 (각 ±6° 클램프 — 과한 기울기 방지)
            float yaw = transform.eulerAngles.y;
            Quaternion yawOnly = Quaternion.Euler(0f, yaw, 0f);
            Vector3 fwd = yawOnly * Vector3.forward;
            Vector3 rgt = yawOnly * Vector3.right;
            float pitchDeg = Mathf.Atan2(Vector3.Dot(fwd, normal), normal.y) * Mathf.Rad2Deg; // + = 전방 내리막
            float rollDeg = Mathf.Atan2(Vector3.Dot(rgt, normal), normal.y) * Mathf.Rad2Deg;  // + = 우측 내리막
            if (Mathf.Abs(pitchDeg) < 1.5f && Mathf.Abs(rollDeg) < 1.5f)
            {
                // 준평지 — 기울임 없이 서서히 직립 복귀
                transform.rotation = Quaternion.Slerp(transform.rotation, yawOnly, SlopeAlignSpeed * Time.deltaTime);
                return;
            }
            pitchDeg = Mathf.Clamp(pitchDeg, -SlopeTiltMaxDeg, SlopeTiltMaxDeg);
            rollDeg = Mathf.Clamp(rollDeg, -SlopeTiltMaxDeg, SlopeTiltMaxDeg);

            Quaternion tiltTarget = Quaternion.Euler(pitchDeg, yaw, -rollDeg);
            transform.rotation = Quaternion.Slerp(transform.rotation, tiltTarget, SlopeAlignSpeed * Time.deltaTime);
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
        public bool IsSprinting
        {
            get
            {
                var kb = CurrentKeyboard; // 캐시 없이 즉시 조회 (W키 드랍 수정)
                return kb != null && kb.leftShiftKey.isPressed && _moveDirection.magnitude > 0.1f;
            }
        }
        public bool IsDashing => _isDashing;
        public bool IsJumping => _isJumping;

        /// <summary>T-D3 이동 파라미터 확장: 로컬(캐릭터 기준) 이동 벡터(X=측면, Z=전후). 입력 없으면 zero.</summary>
        public Vector3 LocalMoveDirection
        {
            get
            {
                if (_moveDirection.sqrMagnitude < 0.0001f) return Vector3.zero;
                Vector3 local = transform.InverseTransformDirection(new Vector3(_moveDirection.x, 0f, _moveDirection.z));
                return new Vector3(Mathf.Clamp(local.x, -1f, 1f), 0f, Mathf.Clamp(local.z, -1f, 1f));
            }
        }
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

        // --- 웅크림/수영 속성 ---
        public bool IsCrouching => _isCrouching;
        public bool IsSwimming => _isSwimming;
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