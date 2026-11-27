// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 414,618
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectName.Core;
using ProjectName.Systems.Animation.Procedural;
using Unity.Cinemachine;
using MagicPigGames.MagicTime;
using System.Collections.Generic;

namespace ProjectName.Systems
{
    /// <summary>
    /// 플레이어 공격 시스템 — 마우스 좌클릭 → 커서 방향 자동 조준 → 데미지
    /// C4-08: 마우스 커서 방향으로 가장 가까운 적 자동 탐지 및 타겟팅
    /// 2026-09-14: 공격 모션 개선 — 런지 무게감(가속→감속) / 페이스 타깃 / 공격자 리코일 / 연타 카메라 펀치 차등
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        public static PlayerCombat Instance { get; private set; }

        // ===== 2026-09-11: 마지막 적중 정보 공유 — 십자가 VFX가 실제 맞은 대상 지점에 발화되도록 =====
        // 의존 방향 유지: HumanoidClipDriver(드라이버) → PlayerCombat(컴뱃) 단방향 참조만 허용.
        /// <summary>마지막 적중 대상의 지점(대상 Collider/Renderer bounds 중심 + up*0.1 — 2026-09-13 0.2→0.1 하향, 부착감). 크로스 VFX 발화 기준점.</summary>
        public static Vector3 LastHitPoint;
        /// <summary>마지막 적중 정보 유효 여부 — 미스(빈 스윙) 경로에서 false로 무효화.</summary>
        public static bool LastHitValid;
        /// <summary>마지막 적중 시각(Time.time) — 0.5초 이내 적중만 VFX 발화 유효 판정용.</summary>
        public static float LastHitTime;

        [Header("Combat Settings")]
        [SerializeField] private LayerMask _targetLayers = -1; // 모든 레이어
        [SerializeField] private float _maxRange = 8f; // 2026-09-10: 3→8m 상향 (Test_10 근접 조준 성공률)
        [SerializeField] private float _baseDamage = 10f;
        [SerializeField] private float _attackRadius = 0.5f; // 공격 반지름
        // 2026-09-10: 근접 사거리 3→8m — Test_10 슬라임(9.4m)을 좌클릭으로 맞출 수 있게 상향.
        // (이전 _maxRange=3m라 커서 조준 실패 시 SphereCast도 전부 빗나가 '공격 실패' 315회+HP 미감소 실증)

        [Header("Auto-Aim (C4-08)")]
        [SerializeField] private float _autoAimRange = 15f;           // 자동 조준 최대 거리

        // [무번호 44-INPUT-DIAG] 입력 디바이스 상태 세션 1회 관측 플래그.
        private bool _inputDiagLogged;

        // [무번호 44-AIM-DIAG] 근접 미스 진단 로그 스로틀(3s).
        private float _nextMissDiagLog;
        [SerializeField] private float _autoAimAngle = 10f;           // 조준 원뿔 각도 (도)

        private WeaponData _currentWeapon;
        private float _lastAttackTime = -10f;
        private long _acceptedAttackSequence;
        private Camera _mainCamera;
        private CinemachineImpulseSource _impulseSource;

        // ===== 애니메이션 =====
        private RigAnimationController _rigAnim;
        private ProceduralAnimationController _proceduralAnim;
        // P6 (2026-09-11): 활(Bow) 좌클릭 발사용 — HumanoidClipDriver 공용 트리거(ArcheryShot) 접근
        private HumanoidClipDriver _clipDriver;

        // ===== 2026-09-14: 공격 모션 개선 상수 — 런지 무게감 / 페이스 타깃 / 리코일 / 연타 카메라 펀치 =====
        private const float AttackStreakWindow = 0.6f;    // 직전 공격 후 이 시간(초) 내 재공격이면 연타 스트릭 유지
        private const int AttackStreakMax = 3;            // 연타 스트릭 최대값(클램프) — 카메라 펀치 3단계
        private const float FaceTargetSpeed = 15f;        // 페이스 타깃 회전 보간 속도(12~15 상단 — 공격 진입 즉시 정렬)
        private const int FaceTargetMaxFrames = 10;       // 페이스 회전 최대 프레임 — 공격 순간에만 회전(CursorTurn 충돌 최소화)
        private const float RecoilDistanceNormal = 0.15f; // 일반 타격 시 공격자 리코일 거리(m)
        private const float RecoilDistanceCrit = 0.25f;   // 백어택/치명타 리코일 거리(m) — 반동 증폭
        private int _attackStreak;                        // 연타 카운터(1~3) — HumanoidClipDriver 콤보와 무관한 자체 카운터
        private float _nextClickProbe;                    // [TEST27-68차] 독립 클릭 프로브 쿨다운(30s)
        // #48차 후속 FIX(2026-09-14): 리코일-런지 동시 위치 덮어쓰기 충돌 게이트 플래그 — RecoilCoroutine 생존 중 true.
        // 성공 타격 시 히트스톱(timeScale 0.08) 선행으로 deltaTime이 축소되어 리코일(0.05s)과 런지(0.15s)가
        // 수십 프레임 동안 매 프레임 transform.position을 동시 기록 → 리코일이 런지에 묻혀 잘리던 버그 해소.
        private bool _recoilActive;                       // 리코일 진행 중 플래그 — 시작 true/종료 false(런지 대기 기준점)

        // ===== Phase 1-1/1-2: 차지(강공) & 패링(방어) =====
        private const float ChargeMaxHold = 0.8f;        // 차지 최대 충전 시간(초) — 오버차지 시 자동 강공
        private const float ChargeMinHold = 0.12f;       // 차지 인식 최소 홀드(초) — 그 이하는 일반 좌클릭 공격
        private const float ChargeDamageMultiplier = 1.8f; // 차지 강공 데미지 배율
        private const float ParryWindow = 0.45f;         // 패링 모션과 공격 입력을 분리하는 단일 방어 창(실시간 초)
        private const int MaxQueuedWeaponAttacks = 3;    // 패링 방어 창 중 보존할 최대 근접 클릭 수
        [SerializeField] private KeyCode _parryKey = KeyCode.V; // KeyBindings에는 전투 액션이 없어 인스펙터에서 변경 가능
        private float _chargeHeldTime;                   // 우클릭 홀드 누적 시간
        private bool _charging;                          // 차지 충전 중 여부
        private bool _parryActive;                       // 패링 방어 창 활성 여부
        private float _parryActiveUntil;                 // 패링 창 만료 시각(unscaled time)
        private int _queuedWeaponAttacks;                // Parry 모션 중 입력된 일반 무기 공격(최대 3회)
        private bool _queuedBowAction;                   // Parry 모션 중 Bow press pending
        private bool _queuedBowReleased;                 // pending Bow가 창 만료 전 해제됐는지
        // [무번호 36-PW1] 패망 창 방어 림 — 창 유지 중 무기 골드 림(잔여 시간 비례 감쇠 — "닫히는 시계").
        private GameObject _parryGuardRim;
        private const float ParryGuardRimIntensity = 1.6f;   // 29-P1-2 무기 림 선례 강도
        private bool _parryGoodTickDone;                 // [무번호 36-PW3] PERFECT→GOOD 경계 통과 1회 청색 틱
        /// <summary>[무번호 36-PW3] GOOD 구간 큐 색 — 패망D1 청색 리터럴 승계(파일 내 단일 소스).</summary>
        private float _parryGoodPulseUntil;   // [171차] GOOD 진입 림 맥동 종료 시각(unscaled) — 청색 큐 폐기(흰 단일)
        private float _queuedBowHeldTime;                // queued Bow의 캡처된 홀드 시간
        private Coroutine _parryImpactRoutine;
        private bool _parrySlowActive;
        private float _parrySlowRestoreValue = 1f;
        private Animator _parryAnimator;
        private MagicTimeUser _parryTimeUser;
        private float _animatorSpeedBeforeParry = 1f;
        private bool _parryAnimatorSpeedCaptured;
        private const float ParryActionScale = 0.10f;

        // ===== [95차 F1~F4] 패링 액션감 강화 — 퍼펙트 판정/반격 보상/히스톱/카메라 펀치/방향성 스파크 =====
        /// <summary>[95차 F1] PERFECT 판정 경계 — 창 경과(elapsed)가 이 값 이하면 PERFECT, 초과면 GOOD.</summary>
        internal const float ParryPerfectWindow = 0.15f;
        /// <summary>[95차 F1] PERFECT 반격 보상 — 큐 해방 첫 1타 데미지 배율.</summary>
        internal const float ParryCounterMultiplier = 1.5f;
        /// <summary>[95차 F2] 히스톱 로컬 타임스케일 — '완전 정지' 체감(SetParryLocalTime 클램프 하한과 동일 값).</summary>
        internal const float ParryHitStopScale = 0.05f;
        /// <summary>[95차 F2] 히스톱 hold 시간 — 50ms.</summary>
        internal const float ParryHitStopHoldSeconds = 0.05f;
        /// <summary>[95차 F3] 카메라 펀치 기준 FOV — 45.</summary>
        internal const float ParryFovBase = 45f;
        /// <summary>[95차 F3] 카메라 펀치 급줌 FOV — 38(45→38 = -7° 상대 펀치, 실측 FOV가 달라도 상대 적용).</summary>
        internal const float ParryFovPunched = 38f;
        private const float ParryFovPunchSeconds = 0.08f;    // [95차 F3] 급줌 지속(실시간 초)
        private const float ParryFovRestoreSeconds = 0.25f;  // [95차 F3] 복원 지속(실시간 초)
        private const float ParryCameraShakeImpulse = 0.25f; // [95차 F3] 미세 셰이크 — 공격 펀치(0.4~0.7) 대비 소형 강도
        private float _pendingCounterAttackMultiplier = 1f;  // [95차 F1] PERFECT 반격 1타 배율 — AttackTarget 소비형(적용 후 1 리셋)
        private Coroutine _parryCameraPunchRoutine;          // [95차 F3] FOV 펀치 코루틴
        private Coroutine _parryRimFadeRoutine;              // [무번호 61] 실패/빈 패링 림 자연 페이드 코루틴
        private GameObject _parryRimFadeTarget;              // [무번호 61] 페이드 진행 중 림 캡처 참조(재패링 시 즉시 해제용)
        private bool _parryFovPunchActive;                   // [95차 F3] 펀치 진행 중 — 재진입 시 원본 베이스 보존(HitStopManager 선례)
        private float _parryFovPunchBase = 60f;              // [95차 F3] 펀치 전 원본 FOV(복귀 기준)

        /// <summary>[95차 F1] 패링 판정 등급 — PERFECT(창 초반 0.15s 이내) / GOOD(그 외).</summary>
        internal enum ParryGrade
        {
            Perfect,
            Good
        }

        /// <summary>
        /// [95차 F1] 순수 판정 함수 — 창 경과(0~0.45s)만으로 등급 분류. 시간 주입 불요(EditMode 직접 테스트 대상).
        /// 경계 포함 규약: elapsed ≤ ParryPerfectWindow(0.15) → PERFECT.
        /// </summary>
        internal static ParryGrade EvaluateParryGrade(float elapsed)
        {
            return elapsed <= ParryPerfectWindow ? ParryGrade.Perfect : ParryGrade.Good;
        }
        public string ChargedClipName = "Charged_Upward_Slash"; // [Phase 1-1] 차지 클립 플러그인 — FBX 확보 시 클립명만 갱신
        public string ParryClipName = "Sword_Parry_Backward_1"; // [Phase 1-2] 패링 클립 플러그인 — FBX 확보 시 클립명만 갱신

        // ═════════ [95차 후속] 믹사모 패링 클립 연결 — Assets/Resources/Animations/Combat_Mixamo(Block/Slash FBX) ═════════
        // 92차 VillagerWanderer 선례 재사용: 폴더 리소스 이동 + 런타임 LoadAll + Player_AC 오버라이드 컨트롤러.
        // 새 시스템 신설 없음 — 기존 ParryClipName 플러그인 구조에 연결, 클립 부재 시 기존 동작 폴백(무해).
        /// <summary>[95차 후속] 리소스 폴더 경로 — 방어/반격 FBX 2종(런타임 Resources 로드 계약).</summary>
        private const string ParryClipResourceFolder = "Animations/Combat_Mixamo";
        /// <summary>[95차 후속] 폴더 내 소스 FBX 파일명 — 파일 단위 LoadAll 판정 소스(92차 선례: 믹사모 테이크명 "mixamo.com" 대응).</summary>
        private const string ParryBlockFileKey = "Sword And Shield Block";
        private const string ParryCounterFileKey = "Sword And Shield Slash";
        /// <summary>[95차 후속 실측] Player_AC Parry 상태 슬롯 원본 클립명 — 컨트롤러 YAML guid 역조회
        /// (Parry 상태 m_Motion → MeshyUser/Sword_Parry_Backward_1.fbx, 내부 테이크
        /// "Armature|Sword_Parry_Backward_1_withSkin"). 이 1슬롯만 교체.</summary>
        private const string PlayerAcParrySlotClipName = "Armature|Sword_Parry_Backward_1_withSkin";
        /// <summary>[95차 후속] Player_AC Parry 상태명 — 반격 모션 1회 크로스페이드 재사용 상태(트리거 'Parry'의 목적지 상태).</summary>
        private const string PlayerAcParryStateName = "Parry";
        /// <summary>[95차 후속] 와이어 감지 대상 베이스 컨트롤러명 — 부트(TestPlayerAnimatorBoot) 지연 부착 폴링용(plain Player_AC).</summary>
        private const string PlayerAcControllerName = "Player_AC";
        private static AnimationClip _parryBlockClip;      // [95차 후속] 방어 클립 정적 캐시(파일단위 판정 → 폴더 키워드 폴백)
        private static AnimationClip _parryCounterClip;    // [95차 후속] 반격 클립 정적 캐시
        private static bool _parryClipsResolved;           // [95차 후속] 클립 해석 1회 가드(연결 로그 1회)
        private static AnimatorOverrideController _parryOverride; // [95차 후속] Player_AC 베이스 오버라이드 컨트롤러(런타임 생성)
        private Coroutine _parryCounterOverrideRoutine;    // [95차 후속] 반격 모션 후 Parry 슬롯 복원 코루틴
        private Animator _parryClipsWiredAnimator;         // [95차 후속] 오버라이드 부착 완료 Animator(중복 스왑 방지)

        // ===== 활(Bow) 드로→릴리즈 (2026-09-17): 좌클릭 홀드 = 드로(파워 축적), 해제 = 릴리즈(발사) =====
        // 근접 우클릭 차지(위)와 별개의 좌클릭 경로 — 검/창/Fist에는 영향 없음.
        private bool _bowDrawing;                 // 활 드로 진행 중 여부
        /// <summary>[70차 후속19/C6] 마지막 활 발사 파워(0~1) — 명중 시 파워 풀 크리틱 연출 판정용.</summary>
        public static float LastBowPower { get; private set; } = 1f;
        private float _bowDrawHeldTime;           // 드로 홀드 누적 시간(초)
        private const float BowDrawMaxHold = 0.7f; // 드로 최대 = 0.7초 — 이때 파워 1.0 도달(0.5→0.7, 무게감 보강)
        private const float BowMinFire = 0.18f;    // 이 파워 미만 = 탭 = 캔슬(발사 안 함)

        // ===== C4-08: 자동 조준 상태 =====
        private IDamageable _currentTarget;

        /// <summary>현재 타겟 (자동 조준으로 선택된 적)</summary>
        public IDamageable CurrentTarget => _currentTarget;

        /// <summary>타겟이 있고 살아있는가?</summary>
        public bool HasTarget => _currentTarget != null && _currentTarget.IsAlive;

        /// <summary>공격이 가능한 여부 (쿨다운 기준 — [A3] 이계 증강 공속 배율로 게이트 단축)</summary>
        public bool CanAttack => _currentWeapon != null && Time.time - _lastAttackTime >= _currentWeapon.attackSpeed / AfterlifeAugmentSystem.AttackSpeedMult / AfterlifeCardSystem.CooldownFactor; // [156차 P4] 이계 카드 활성 무기 쿨다운 계수

        /// <summary>남은 쿨다운 시간 (0 이하이면 공격 가능 — [A3] 이계 증강 공속 배율 반영)</summary>
        public float RemainingCooldown => Mathf.Max(0f, (_lastAttackTime + ((_currentWeapon?.attackSpeed ?? 1f) / AfterlifeAugmentSystem.AttackSpeedMult)) - Time.time);
        // 시간 기반 쿨다운/외부 연출용. 여러 accepted 입력의 구분은 AcceptedAttackSequence를 사용한다.
        public float LastAttackTime => _lastAttackTime;
        /// <summary>Monotonic count of accepted TryAttack inputs; unlike LastAttackTime, same-time inputs remain distinct.</summary>
        public long AcceptedAttackSequence => _acceptedAttackSequence;

        /// <summary>[156차 P4] 현재 장착 무기 — 이계 카드 무기 스왑의 진입 캡처/귀환 원복용.</summary>
        public WeaponData CurrentWeapon => _currentWeapon;

        /// <summary>외부 장착 시스템(WeaponEquipManager)용 — WeaponData.Sword/Spear/Bow 정적 인스턴스를 반영.</summary>
        public void SetWeapon(WeaponData weapon)
        {
            if (weapon == null) return;
            _currentWeapon = weapon;
            // 2026-09-12(P5): 무기별 애니 판정 가시화 — 무기명/타입/공속/DMG 1줄
            Debug.Log($"[PlayerCombat] 🗡️ 무기 설정: {weapon.weaponName} (타입={weapon.weaponType}, 공속={weapon.attackSpeed}s, DMG={weapon.damage})");
        }

        /// <summary>무기와 플레이어 레벨을 기반으로 데미지를 계산합니다.</summary>
        private float CalculateDamage()
        {
            if (_currentWeapon == null) return _baseDamage;

            float damage = _currentWeapon.damage;
            if (PlayerStats.Instance != null)
                damage += PlayerStats.Instance.Level * 0.5f;
            return damage;
        }

        // [2026-09-14(51차 방어)] 컴포넌트 재활성화 시 리코일 완료 플래그 리셋 — 재활성 전 리코일이 중단(비활성화/예외)되면
        // _recoilActive가 true로 고정돼 AttackLungeCoroutine의 while (_recoilActive) 대기가 영구 양보, 런지가 영구 스킵되는 것을 방지.
        private void OnEnable()
        {
            _recoilActive = false;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _currentWeapon = WeaponData.Fist;
            _parryAnimator = GetComponentInChildren<Animator>();
            _parryTimeUser = GetComponent<MagicTimeUser>();
            if (_parryTimeUser == null) _parryTimeUser = GetComponentInChildren<MagicTimeUser>();
            if (_parryTimeUser == null) _parryTimeUser = gameObject.AddComponent<MagicTimeUser>();
            _parryTimeUser.transitionDuration = 0f;
            _animatorSpeedBeforeParry = _parryAnimator != null ? _parryAnimator.speed : 1f;
            _parryAnimatorSpeedCaptured = _parryAnimator != null;

            // ProceduralAnimationController 획득 (PlayerModel 자식)
            _proceduralAnim = GetComponentInChildren<ProceduralAnimationController>();
            if (_proceduralAnim == null)
            {
                Transform model = transform.Find("PlayerModel");
                if (model != null)
                    _proceduralAnim = model.GetComponent<ProceduralAnimationController>();
            }
        }

        private void Start()
        {
            ScreenEdgeDangerFX.EnsureSubscribed();   // [무번호 37-G3] 위험 방향 인디케이터 구독 보장
            _mainCamera = Camera.main;
            if (_mainCamera != null)
            {
                // Initialize Cinemachine Impulse Source
                _impulseSource = _mainCamera.GetComponent<CinemachineImpulseSource>();
                if (_impulseSource == null)
                    _impulseSource = _mainCamera.gameObject.AddComponent<CinemachineImpulseSource>();
            }

            // RigAnimationController 획득 — 기존 것만 사용, 자동 부착 금지
            // (RequireComponent(Animator)가 플레이어 루트에 빈 Animator를 생성해 Player_AC 재생을 깨는 원인)
            _rigAnim = GetComponent<RigAnimationController>();

            // P6: 활 발사 애니(ArcheryShot) 명시 트리거용 — 드라이버는 PlayerCombat 자식 계층에 위치
            _clipDriver = GetComponentInChildren<HumanoidClipDriver>();
        }

        // [207차 P1] 조준 카메라 이중 불합격 경고 1회 플래그 — 세션 최초 1회만 출력(발사 시도마다 스팸 금지).
        private bool _bowAimCameraGuardWarned;

        private Camera ResolveBowAimCamera()
        {
            // [P206-P5] 1순위: 플레이어 팔로우(실렌더) 카메라 — PlayerMovement가 LateUpdate에서 매 프레임
            // 이동시키는 카메라로 [FollowProbe] 실측상 진짜 렌더 카메라. Camera.main은 세계 원점 매몰 별개
            // "Main Camera"(중복 태그)를 반환해 조준이 상시 원점 방향이 되던 근원이므로 이를 우선 회피한다.
            // (태그 독립 — 실렌더 카메라는 태그 유실 여부와 무관하게 조준의 단일 진실원.)
            // [207차 P1] 최종 선택 카메라에 IsPlausibleAimCamera 가드 적용(활성+활성화+거리≤60m) —
            // FollowCamera 불합격 시 아래 Camera.main을 검사하고, 둘 다 불합격이면 기존 동작(null 반환 →
            // TryBowShot의 발사 취소 경로)으로 폴백한다 + 경고 1회.
            Camera followCamera = PlayerMovement.FollowCamera;
            if (PlayerMovement.IsPlausibleAimCamera(followCamera, transform))
                return followCamera;

            // Fallback: Camera.main is the authoritative current producer when no follow camera exists yet.
            // Refresh every release so a valid-but-old cached camera cannot solve against a different view
            // after a camera/scene handoff.
            Camera currentMainCamera = Camera.main;
            if (currentMainCamera == null || !currentMainCamera.isActiveAndEnabled
                || !currentMainCamera.gameObject.activeInHierarchy || !currentMainCamera.CompareTag("MainCamera")
                || !PlayerMovement.IsPlausibleAimCamera(currentMainCamera, transform))
            {
                // [207차 P1] 두 후보 모두 불합격 — 기존 동작(null 반환) 폴백 + 경고 1회(스팸 방지).
                if (!_bowAimCameraGuardWarned)
                {
                    _bowAimCameraGuardWarned = true;
                    Debug.LogWarning($"[PlayerCombat][207차] 조준 카메라 불합격 — " +
                        $"FollowCamera={(followCamera == null ? "<무효>" : followCamera.name)} " +
                        $"Camera.main={(currentMainCamera == null ? "<부재>" : currentMainCamera.name)} " +
                        $"모두 가드 기각(활성·활성화·거리≤{PlayerMovement.AimCameraPlausibleDistance}m). 발사를 취소합니다(이후 재발 미출력).");
                }
                return null;
            }

            if (_mainCamera != currentMainCamera)
                _mainCamera = currentMainCamera;
            return _mainCamera;
        }

        private void Update()
        {
            if (PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead) return;

            // [무번호 44-INPUT-DIAG] 입력 디바이스 상태 관측(세션 1회) — 좌클릭/WASD가 게임까지 도달했는지의
            // 최상위 게이트. 11-15 21:00 세션에서 [CombatInput] 좌클릭 관측 2건은 있었으므로 디바이스는 살아
            // 있었으나, "클릭했는데 아무 일도 없다" 보고의 세션별 재발 판별용 상시 게이트로 유지한다(수리 없음).
            if (!_inputDiagLogged)
            {
                _inputDiagLogged = true;
                bool mouseAlive = Mouse.current != null;
                bool keyboardAlive = Keyboard.current != null;
                if (!mouseAlive || !keyboardAlive)
                    Debug.LogWarning($"[44-INPUT-DIAG] ⚠️ 입력 디바이스 부재 — mouse={mouseAlive} keyboard={keyboardAlive} " +
                        "(Input System 디바이스 탈락 — 좌클릭·WASD가 무음 소비되는 근원 후보)");
                else
                    Debug.Log("[44-INPUT-DIAG] 입력 디바이스 정상 — mouse=True keyboard=True");
            }

            // [95차 후속] 패링 클립 와이어 폴링 — plain Player_AC 감지 시 1회 연결.
            // 근거: TestPlayerAnimatorBoot가 PlayerModel 발견(최대 5초 대기) 후 Player_AC를 무조건 재부착하므로
            // Start 시점 1회 할당은 부트와 순서 경쟁 → plain Player_AC(이름 일치) 재감지 폴링으로 어느 순서든 연결 유지.
            // Player_AC 부재(부착 경로 없는 씬) 시 와이어 생략 = 기존 동작 폴백(무해).
            EnsureParryClipsWired();

            // A dedicated, remappable keyboard action starts parry only; hovering UI suppresses it like world clicks.
            if (!ProjectName.Core.UITransitionState.PointerOverUI
                && Keyboard.current != null
                && _parryKey != KeyCode.None
                && Keyboard.current.FindKeyOnCurrentKeyboardLayout(_parryKey.ToString().ToLowerInvariant())?.wasPressedThisFrame == true)
                TryStartParry();

            // 타겟 상태 업데이트 (사망 또는 범위 이탈 체크)
            UpdateTargetState();

            // 패링은 unscaled-time 윈도우라 로컬/글로벌 슬로우 중에도 정확히 종료한다.
            bool parryWindowEnded = _parryActive && Time.unscaledTime >= _parryActiveUntil;
            if (parryWindowEnded)
                EndParryWindow();

            // [무번호 36-PW1/PW3] 방어 림 갱신 — 잔여 시간 비례 감쇠 + PERFECT→GOOD 경계 청색 틱(1회).
            if (_parryActive) UpdateParryGuardRim();

            // Drain at most one accepted melee click per frame, starting after the window/impact frame. TryAttack keeps its own cooldown/combo gates;
            // retain the pending click when those gates reject it so each accepted click gets a distinct timestamp.
            if (!parryWindowEnded && !_parryActive && _parryImpactRoutine == null && _queuedWeaponAttacks > 0
                && TryAttack())
                _queuedWeaponAttacks--;

            // [RTS 연동] 우클릭 차지(강공) 제거 — 부대 우클릭 명령(병사 이동/공격)과 겹침. 우클릭은 RTSCommandSystem 전용으로 해방. 패링은 좌클릭 기반 유지.
            bool isBowEquipped = _currentWeapon != null
                && _currentWeapon.weaponType == ProjectName.Core.WeaponType.Bow;
            if (Mouse.current != null)
            {
                // Parry 중에는 Bow press를 아직 드로우로 넘기지 않는다. 홀드/릴리즈를 보존해 창 종료 후 재생한다.
                if (_queuedBowAction && !_queuedBowReleased)
                {
                    if (Mouse.current.leftButton.wasReleasedThisFrame)
                    {
                        _queuedBowReleased = true;
                        _queuedBowHeldTime = Mathf.Min(BowDrawMaxHold, _queuedBowHeldTime + Time.unscaledDeltaTime);
                    }
                    else if (Mouse.current.leftButton.isPressed)
                    {
                        _queuedBowHeldTime = Mathf.Min(BowDrawMaxHold, _queuedBowHeldTime + Time.unscaledDeltaTime);
                    }
                }
                // [활 드로→릴리즈] 매 프레임 드로 누적 + 좌클릭 해제 = 릴리즈(파워 반영 발사)
                if (_bowDrawing)
                {
                    _bowDrawHeldTime += Time.deltaTime;
                    BowAimState.UpdatePower(Mathf.Clamp01(_bowDrawHeldTime / BowDrawMaxHold));
                }
                if (Mouse.current.leftButton.wasReleasedThisFrame && _bowDrawing && isBowEquipped)
                {
                    bool wasDrawing = _bowDrawing;
                    _bowDrawing = false;
                    if (wasDrawing)
                    {
                        // Freeze the current input pixel on this release frame; the UTK reticle's
                        // 16ms scheduler may not have published cursor movement since its last tick.
                        BowAimState.CaptureReleaseAimScreenPoint(Mouse.current.position.ReadValue());
                        float relPower = Mathf.Clamp01(_bowDrawHeldTime / BowDrawMaxHold);
                        BowAimState.Release(relPower >= BowMinFire, relPower);   // [P25-C1] 릴리즈(탭 캔슬/발사 페이드)
                        ReleaseBow(relPower, true);
                    }
                }
                // [189차] 폭탄 릴리즈 투척 — press(조준 시작) 후 좌클릭 해제 = 투척.
                //   활 드로→릴리즈 선례(wasReleasedThisFrame 릴리즈 패턴)와 동일 위계로 배치.
                if (BombArmController.Instance != null && BombArmController.Instance.IsAiming && Mouse.current.leftButton.wasReleasedThisFrame)
                {
                    BombArmController.Instance.ThrowTowardCursor();
                    return;
                }
            }

            // 좌클릭 감지 (InputSystem)
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                Debug.Log($"[CombatInput] physical left click observed ui={ProjectName.Core.UITransitionState.PointerOverUI} "
                    + $"squadMode={GuardSelectionManager.squadModeActive} parry={_parryActive} "
                    + $"weapon={_currentWeapon?.weaponType.ToString() ?? "none"}");
                // [U8 수리] UTK UI 위 좌클릭(인벤 드래그/슬롯 클릭)은 게임 공격으로 소비 금지
                if (ProjectName.Core.UITransitionState.PointerOverUI)
                {
                    Debug.Log("[CombatInput] click consumed by UI pointer gate");
                    return;
                }
                // [폭탄] 이번 프레임 이미 폭탄 투척 → 근접/무기 공격 금지 (AttackSystem과 순서 무관)
                if (BombArmController.BombThrowIssuedThisFrame)
                    return;
                // [폭탄][189차] 무장 상태 좌클릭 press = 조준 시작(궤적 프리뷰) — 투척은 릴리즈 경로(위 wasReleasedThisFrame 블록)
                if (BombArmController.Instance != null && BombArmController.Instance.IsArmed)
                {
                    BombArmController.Instance.BeginAim();
                    return;
                }
                // [무번호 86-P3] 승마 중 — 좌클릭은 말 발길질(HorseMountSystem)이 소비, 무기 공격(근접/활/큐) 억제
                if (HorseMountSystem.IsMounted)
                {
                    Debug.Log("[CombatInput] click consumed by mount kick gate");
                    return;
                }
                // [TEST27-68차] 독립 클릭 프로브 — GSM이 죽어도(공유 GO 파괴) 클릭 입력 자체의 증거를 남긴다.
                // [RTS] 좌클릭 감지 로그와 대조해 드래그 무반응의 위치(GSM vs 입력)를 판별한다.
                if (Time.unscaledTime >= _nextClickProbe)
                {
                    _nextClickProbe = Time.unscaledTime + 30f;
                    Debug.Log("[ClickProbe] 플레이어 좌클릭 감지(30s 쿨) — [RTS] 로그와 대조");
                }
                // [TEST24-FIX F'] 병사 드래그 단체 선택 — **Ctrl 키를 누른 채 좌클릭**이면
                // 프레임 순서와 무관하게 공격 대신 드래그로 위임(PlayerCombat이 먼저 돌아도
                // consumeLeftClickAsDrag 플래그 타이밍에 의존하지 않고 Ctrl 홀드를 직접 판정).
                // 평상 시(좌클릭만)는 공격 유지.
                // [2026-09-27] 부대 모드(squadModeActive=true)일 땐 **Ctrl 없이도** 좌클릭 = 병사 드래그 선택
                //   으로 위임 → "그냥 드래그해서 병사 지정 → 우클릭 이동" 요구 충족. 평상 시 공격 유지.
                bool ctrlDirect = Keyboard.current != null
                    && (Keyboard.current.ctrlKey.isPressed
                        || Keyboard.current.leftCtrlKey.isPressed
                        || Keyboard.current.rightCtrlKey.isPressed);
                if (ctrlDirect || GuardSelectionManager.squadModeActive)
                {
                    GuardSelectionManager.consumeLeftClickAsDrag = false;   // 드래그로 소비(중복 방지)
                    return;   // (Ctrl+좌클릭 | 부대모드 좌클릭) → 드래그(GuardSelectionManager가 선택 처리)
                }
                // [60차] GuardSelectionManager가 미리 세팅한 드래그 플래그 소비(보조 백업 — Ctrl 아닌 드래그 경로)
                if (GuardSelectionManager.consumeLeftClickAsDrag)
                {
                    GuardSelectionManager.consumeLeftClickAsDrag = false;   // 드래그로 소비
                    return;
                }

                // [Phase D-2] NPC/영주/상점NPC 좌클릭 — 공격 대상(IDamageable)이 아니므로
                //  공격(빈 스윙) 대신 상호작용으로 전환. PlayerCombat가 먼저 돌아도 ContextCommandRouter와
                //  충돌 없도록, 커서가 NPC 계열을 가리키면 이 프레임의 공격을 스킵한다.
                //  HoverTargetClassifier는 Systems 어셈블리라 Systems→UI 참조 없이 호출 가능(순수 분류기).
                if (IsPointerOverNpcInteraction())
                {
                    return;   // 상호작용 패널 처리는 ContextCommandRouter.Update가 담당
                }

                // Left click belongs only to the normal attack path. During an already-active parry
                // it may queue that attack for after the defensive window, but never starts/restarts parry.
                if (_parryActive)
                {
                    if (isBowEquipped)
                    {
                        _queuedBowAction = true;
                        _queuedBowReleased = false;
                        _queuedBowHeldTime = 0f;
                    }
                    else _queuedWeaponAttacks = Mathf.Min(_queuedWeaponAttacks + 1, MaxQueuedWeaponAttacks);
                    return;
                }

                if (isBowEquipped)
                {
                    _bowDrawing = true;
                    _bowDrawHeldTime = 0f;
                    BowAimState.Begin();
                    _clipDriver?.TriggerBowDraw();
                    AttackSoundLayerManager.PlayBowDraw();
                }
                else
                {
                    TryAttack();
                }
            }
        }

        // ── [무번호 61] 패망 창 상태 노출 — HUDUTK 패망 아크 게이지 소비(P3 창 아크 게이지 계약). ──
        /// <summary>창 활성 여부 — HUD 게이지 표시 게이트.</summary>
        public bool ParryWindowActive => _parryActive;
        /// <summary>창 잔여 비율(0~1) — 게이지 채움. 비활성=0.</summary>
        public float ParryWindowRemaining01 => _parryActive
            ? Mathf.Clamp01((_parryActiveUntil - Time.unscaledTime) / ParryWindow)
            : 0f;
        /// <summary>PERFECT 구간(경과 ≤ ParryPerfectWindow) — 게이지 흰 맥동 판정(36-PW3 림 맥동과 동일 위계).</summary>
        public bool ParryInPerfectPhase => _parryActive
            && (ParryWindow - Mathf.Max(0f, _parryActiveUntil - Time.unscaledTime)) <= ParryPerfectWindow;

        private bool TryStartParry()
        {
            if (ProjectName.Core.UITransitionState.PointerOverUI || _parryActive)
                return false;

            _parryActive = true;
            _parryActiveUntil = Time.unscaledTime + ParryWindow;
            RestoreParryCounterSlotBeforeParry();   // [95차 후속] 반격 슬롯 임시 교체 진행 중 신규 패링 → 방어 클립 슬롯 즉시 복원
            _proceduralAnim?.TriggerAction("parry");
            _clipDriver?.TriggerParry();
            // [패링D1] 창 개방 즉시 피드백 — 흰 소형 확산링(플레이어 흡수점 근방). 성공 FX(금/청 대형 링+히트스톱)와 무간섭.
            //   근거: V 입력만으로는 화면 변화가 없어 "패링 시스템이 살아있는지" 판별 불가(사용자 보고 "전혀 실행된 게 없어").
            if (Application.isPlaying)
            {
                Vector3 cueP = transform.position + transform.forward * 0.8f + Vector3.up * 0.9f;
                // [무번호 36-PW3] 개방 링 = 골드 — 창 초반(ParryPerfectWindow)이 PERFECT 구간임을 행동 전에 학습시키는 신호.
                //   GOOD 구간 전환은 UpdateParryGuardRim의 맥동(1회)이 이어받는다.
                ShockwaveRingFX.Spawn(cueP, 0.8f, new Color(FXPalette.ParryWhite.r, FXPalette.ParryWhite.g, FXPalette.ParryWhite.b, 0.5f), 0.3f);
                _parryGoodTickDone = false;   // [무번호 36-PW3] 경계 틱 1회 플래그 리셋
                // [무번호 61] 페이드 진행 중 재패링 — 구 림 페이드 중단 + 즉시 해제(이중 림 방지).
                if (_parryRimFadeRoutine != null)
                {
                    StopCoroutine(_parryRimFadeRoutine);
                    _parryRimFadeRoutine = null;
                    FxRimOverlay.Release(_parryRimFadeTarget);
                    _parryRimFadeTarget = null;
                }
                AttachParryGuardRim();   // [무번호 36-PW1] 창 유지 중 방어 림(잔여 시간 비례 감쇠)
                Debug.Log($"[PlayerCombat] 🛡️ 패링 창 개방 — {ParryWindow:0.00}s (V) [패망D1]");
            }
            return true;
        }

        /// <summary>[Phase 1-1] 차지 해제 — 충전 보너스 반영 강공 1타.</summary>
        private void ReleaseCharge(bool fire)
        {
            // [TEST24-FIX H1] 활 이중 방어 — 차지 발동 자체를 활에서 금지(우클릭 차지 검/창 전용).
            if (_currentWeapon != null && _currentWeapon.weaponType == ProjectName.Core.WeaponType.Bow)
            {
                _charging = false;
                _chargeHeldTime = 0f;
                _proceduralAnim?.TriggerAction("charge_end");
                return;
            }
            _charging = false;
            float held = _chargeHeldTime;
            _chargeHeldTime = 0f;
            _proceduralAnim?.TriggerAction("charge_end");
            if (!fire) return;

            float bonus = Mathf.Clamp01(held / ChargeMaxHold); // 0~1 충전 게이지
            TryChargeAttack(1f + bonus * (ChargeDamageMultiplier - 1f));
        }

        /// <summary>[활 드로→릴리즈] 좌클릭 해제 — 파워 기반 화살 발사. 화살 소모/발사체 생성은 ArrowManager 담당.</summary>
        /// 파워가 최소(BowMinFire) 미만이면 탭으로 간주해 드로 캔슬(발사 안 함). 근접 ReleaseCharge와 별개 경로.
        private void ReleaseBow(float power, bool currentDrawRelease = false)
        {
            if (power < BowMinFire)
            {
                Debug.Log($"[Bow] 드로 캔슬 (파워 너무 낮음) power={power:F2}");
                return;
            }
            _bowDrawHeldTime = 0f;
            TryBowShot(power, currentDrawRelease);
        }

        /// <summary>
        /// [Phase 1-1] 차지 강공 1타 — 충전 배율을 데미지/임팩트에 적용.
        /// 타겟은 기존 조준(커서 방향 → 화면 중앙 → 스윕 폴백)을 우클릭 전방 대상으로 수행.
        /// </summary>
        private void TryChargeAttack(float damageMultiplier)
        {
            if (_currentWeapon == null) return;

            // 조준 — 우클릭 방향으로 가장 가까운 살아있는 대상
            IDamageable target = FindTargetInCursorDirection();
            if (target == null) target = MeleeSweepFallback();

            if (target == null || !target.IsAlive || IsOwnSoldier(target))   // [69차 후속10] 아군 오인 차단 — 내 병사는 빈 스윙
            {
                // 미스 — 빈 차지 스윙 (카메라 펀치만)
                TriggerCameraEffects();
                return;
            }

            _currentTarget = target;
            StartFaceTarget(target);

            float baseDamage = CalculateDamage();
            float damage = baseDamage * damageMultiplier;

            MonoBehaviour targetBehaviour = target as MonoBehaviour;
            Vector3 hitDirection = Vector3.zero;
            if (targetBehaviour != null)
                hitDirection = (targetBehaviour.transform.position - transform.position).normalized;

            // 충전 강공 — 임팩트 1.5x(계획 E-2 스택 활용), 히트스톱 강화는 타격측 호출부에서
            target.TakeDamage(damage, hitDirection, _currentWeapon.weaponType.ToString());

            // [166-D] 몬스터 피격 효과음 — 강공도 데미지 실제 적용 지점(히트스톱 이전 발화).
            MonsterHitSfx.PlayMonsterHit(_currentWeapon.weaponType);

            // [무번호 37-H1] 강타격 프리즈 — 피해≥15(33-D1 Heavy 기준 공유)에만 추가 프리즈(0.05s@0.06 —
            //   36 파리 등급 0.07/0.03과 무충돌 별도 밴드). HitStopManager 계약 유지.
            if (damage >= 15f && Application.isPlaying)
                HitStopManager.RequestHitStop(0.05f, 0.06f);

            // 히트 지점 VFX — 크리티컬 룩 강화(1.5배 확대)
            if (targetBehaviour != null)
            {
                Collider hitCol = targetBehaviour.GetComponentInChildren<Collider>();
                Renderer hitRen = targetBehaviour.GetComponentInChildren<Renderer>();
                Vector3 center = hitCol != null ? hitCol.bounds.center
                               : hitRen != null ? hitRen.bounds.center
                               : targetBehaviour.transform.position + Vector3.up * 1.2f;
                LastHitPoint = center + Vector3.up * 0.1f;
                LastHitValid = true;
                LastHitTime = Time.time;
                SlashVFXRunner.PlayImpactMulti(LastHitPoint, CombatHitType.Organic, 1.5f);
            }

            string targetName = targetBehaviour != null ? targetBehaviour.gameObject.name : "Unknown";
            CombatLog.AddEntry($"{targetName}에게 강공 {damage} 데미지 (충전 {damageMultiplier:F1}x)", LogType.Damage);
            HapticFeedback.PlayPreset(HapticFeedback.RumblePreset.Heavy);
            Debug.Log($"[PlayerCombat] ⚡ 차지 강공: {targetName} 데미지 {damage} (충전 {damageMultiplier:F1}x)");
        }

        /// <summary>PlayerHealth의 명시적 타격 판정에 대한 무기 중립 단일 패링.</summary>
        public bool TryParry()
        {
            if (!_parryActive) return false;

            // [95차 F1] 창 경과 산정(0~0.45s) → 퍼펙트/굿 판정. 순수 함수(EvaluateParryGrade)로 분리 — 시간 주입 불요.
            float elapsed = Mathf.Clamp(ParryWindow - (_parryActiveUntil - Time.unscaledTime), 0f, ParryWindow);
            ParryGrade grade = EvaluateParryGrade(elapsed);

            _parryActive = false;
            _parryActiveUntil = -999f;
            ReleaseParryGuardRim();   // [무번호 36-PW1] 성공 — 방어 림 즉시 해제(28-P1 플래시 림이 이어받음 — 이중 림 방지)

            // Success presentation and timed effects are runtime-only. Keep TryParry's state
            // consumption valid for EditMode callers without starting coroutines or spawning FX.
            if (Application.isPlaying)
            {
                _proceduralAnim?.TriggerAction("parry_success");
                try { BulletTime.Get().Apply(0.35f, 0.10f, 0.38f); }
                catch (System.Exception) { }
                // A retrigger during the same local-time effect must retain its original baseline.
                // [95차 F2] 히스톱(F2)은 ParryImpactRoutine 선두에서만 — 이 baseline 보존 로직과 상호 간섭 없음.
                if (!_parrySlowActive)
                {
                    _parrySlowRestoreValue = _parryTimeUser != null ? _parryTimeUser.LocalTimeScaleValue : 1f;
                    _parrySlowActive = true;
                }
                if (_parryImpactRoutine != null) StopCoroutine(_parryImpactRoutine);
                _parryImpactRoutine = StartCoroutine(ParryImpactRoutine(grade));

                // [95차 F1/F4] 공격자 방향 실측 — kbDir = 공격자→플레이어(피격 넉백 방향, PlayerHealth.hitDirection과 동일 의미)를
                // 접촉 근접 공격자 위치로부터 재구성. 탐색은 기존 스윕 폴백(MeleeSweepFallback) 재사용 — 신규 파이프라인 없음.
                Vector3 kbDir = -transform.forward; // 폴백 — 공격자는 흡수점(전방) 방향에 있다고 가정
                MonoBehaviour attacker = MeleeSweepFallback() as MonoBehaviour;
                if (attacker != null)
                {
                    Vector3 toPlayer = transform.position - attacker.transform.position;
                    toPlayer.y = 0f;
                    if (toPlayer.sqrMagnitude > 0.0001f)
                        kbDir = toPlayer.normalized;
                }

                TryParryImpactFX(grade, kbDir);

                if (grade == ParryGrade.Perfect && attacker != null)
                {
                    // [95차 F1] PERFECT 보상 ③ — 공격자 강 넉백(kbDir×1.5 — HitReaction 기존 넉백 파이프라인 재사용).
                    ApplyPerfectParryAttackerKnockback(kbDir, attacker);
                    StartCoroutine(ParryCounterStepInRoutine(attacker));
                    // [무번호 28-P3] 공격자 반응 플래시 — 패망 성공 순간 공격자 몸 백색 반전 0.1s(EA1/M1 인프라 재사용).
                    var parryAi = attacker.GetComponent<AnimalAI>();
                    if (parryAi != null) parryAi.PlayParryFlash();
                    // [95차 F1] PERFECT 보상 ② — 큐 해방은 ParryImpactRoutine 복원 직전에서 실행(슬로우 중 반격 연출).
                }

                // [무번호5/P3] PERFECT 패링 → 타임 스톱 연출(Time Paradox 엔진, Test_10 한정 opt-in).
                //   게이트는 ParadoxTimeStop 내부(타 씬 무시) — 소유권 분리 계약(개인 슬로우=MagicTime 유지).
                if (grade == ParryGrade.Perfect)
                {
                    ParadoxTimeStop.Trigger(transform.position);
                }

                // [95차 F3] 카메라 펀치 — 성공 시(PERFECT/GOOD 공통) FOV 급줌+미세 셰이크.
                // 미스(빈 차지/빈 스윗) 경로의 TriggerCameraEffects는 그대로 — 무간섭.
                // [무번호 61/37-H2] 방향성 오프셋 — 킥을 타격 방향(공격자→플레이어 kbDir)으로 보낸다.
                TriggerParryCameraPunch(kbDir);
            }

            Debug.Log($"[PlayerCombat] 🛡️ 패링 성공({grade}) — 무기 중립 흡수 + Magic Time/BulletTime 액션 슬로우 [95차 F1]");
            return true;
        }

        /// <summary>
        /// [95차 F1] PERFECT 반격 큐 해방 — 큐된 공격 첫 1타를 소비하고 반격 배율을 반환한다.
        /// 큐가 비었으면 1(보상 없음). 실제 해방 발화는 ParryImpactRoutine 복원 직전(슬로우 중)에서 수행.
        /// </summary>
        internal float ConsumeQueuedCounterAttack()
        {
            if (_queuedWeaponAttacks <= 0) return 1f;
            _queuedWeaponAttacks--;
            return ParryCounterMultiplier;
        }

        /// <summary>[171차 C1] PERFECT 반격 스텝인 — 공격자 방향 0.4m 전진(0.1s, unscaled).
        /// 세키로식 반격 리듬: 성공→스텝인으로 접촉 유지, 반격 애니는 기존 슬롯 스왑 메커니즘(L1080 SwapParrySlotClip)이 담당.
        /// kinematic jolt 선례 — CharacterController 무시 transform 이동(네트워크/피직스 무관 싱글플레이 계약).
        /// 0.1s가 너무 짧아 히트스톱(0.07s)과 겹치면 unscaled 기준이라 무간섭.</summary>
        private System.Collections.IEnumerator ParryCounterStepInRoutine(MonoBehaviour attacker)
        {
            if (attacker == null) yield break;
            Vector3 toAttacker = attacker.transform.position - transform.position;
            toAttacker.y = 0f;
            if (toAttacker.sqrMagnitude < 0.0001f) yield break;
            toAttacker.Normalize();
            Vector3 start = transform.position;
            Vector3 target = start + toAttacker * 0.4f;
            const float StepSeconds = 0.1f;
            float elapsed = 0f;
            while (elapsed < StepSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                transform.position = Vector3.Lerp(start, target, Mathf.Clamp01(elapsed / StepSeconds));
                yield return null;
            }
        }

        /// <summary>
        /// [95차 F1] PERFECT 보상 ③ — 공격자 강 넉백(kbDir×1.5).
        /// HitReaction(공격자 피격 반응 컴포넌트)이 기존 넉백 파이프라인 — 내부에서 -kbDir(공격자 쪽)로 밀어낸다.
        /// HitReaction 부재 시 Rigidbody 임펄스 폴백(HitReaction._knockbackForce 기본 5 선례 강도).
        /// 데미지는 0 — 밸런스 무변경, 순수 넉백/경직 반응만.
        /// </summary>
        private static void ApplyPerfectParryAttackerKnockback(Vector3 kbDir, MonoBehaviour attacker)
        {
            try
            {
                var reaction = attacker.GetComponent<HitReaction>();
                if (reaction != null)
                {
                    reaction.PlayHitReaction(kbDir, ParryCounterMultiplier);
                    return;
                }
                Rigidbody rb = attacker.GetComponent<Rigidbody>();
                if (rb != null && !rb.isKinematic)
                    rb.AddForce(-kbDir.normalized * 5f * ParryCounterMultiplier, ForceMode.Impulse);
            }
            catch (System.Exception e) { Debug.LogWarning($"[PlayerCombat] PERFECT 넉백 실패: {e.Message}"); }
        }

        /// <summary>[95차 F3] 카메라 펀치 — FOV 급줌 코루틴 + 미세 셰이크(기존 Cinemachine Impulse 인프라 재사용).</summary>
        /// <summary>[95차 F3 → 무번호 61/37-H2 소화] 카메라 펀치 — FOV 급줌 + 방향성 오프셋 킥.
        ///  37-H2 소화: 임팩트 impulse를 고정 Vector3.forward(중립) 대신 타격 방향(공격자→플레이어 kbDir)으로
        ///  보낸다 — 카메라가 실제 타격 방향으로 밀려났다 복귀. CinemachineImpulseSource.GenerateImpulse는
        ///  소스(카메라) 로컬 공간 — 월드 방향을 역변환해 주입. 방향 실측 실패 시 기존 중립 폴백 유지.</summary>
        private void TriggerParryCameraPunch(Vector3 impactDir)
        {
            Camera cam = _mainCamera != null ? _mainCamera : Camera.main;
            if (cam == null) return; // 카메라 없음 — 펀치 생략(복귀 드라이버 없는 상태를 남기지 않는다)
            if (_parryCameraPunchRoutine != null) StopCoroutine(_parryCameraPunchRoutine);
            _parryCameraPunchRoutine = StartCoroutine(ParryCameraPunchRoutine(cam));
            if (_impulseSource != null)
            {
                Vector3 local = Vector3.forward;   // 폴백 — 기존 중립 펀치(방향 실측 실패 대비)
                Vector3 w = new Vector3(impactDir.x, 0f, impactDir.z);   // 수평 킥만 — 수직 뒤틀림 방지
                if (w.sqrMagnitude > 0.0001f)
                {
                    local = cam.transform.InverseTransformDirection(w.normalized);
                    local.y = 0f;
                    if (local.sqrMagnitude < 0.0001f) local = Vector3.forward;
                }
                _impulseSource.GenerateImpulse(local.normalized * ParryCameraShakeImpulse);
            }
        }

        private System.Collections.IEnumerator ParryCameraPunchRoutine(Camera cam)
        {
            // 재진입 시 원본 베이스 보존 — 중복 펀치 누적으로 FOV가 누전하는 것을 방지(HitStopManager.StartFovPunch 선례).
            if (!_parryFovPunchActive)
            {
                _parryFovPunchBase = cam.fieldOfView;
                _parryFovPunchActive = true;
            }
            float punchFov = _parryFovPunchBase - (ParryFovBase - ParryFovPunched); // 45→38 = -7° 상대 급줌
            float elapsed = 0f;
            while (elapsed < ParryFovPunchSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                if (cam == null) { _parryFovPunchActive = false; _parryCameraPunchRoutine = null; yield break; }
                cam.fieldOfView = Mathf.Lerp(_parryFovPunchBase, punchFov, Mathf.Clamp01(elapsed / ParryFovPunchSeconds));
                yield return null;
            }
            if (cam != null) cam.fieldOfView = punchFov;
            elapsed = 0f;
            while (elapsed < ParryFovRestoreSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                if (cam == null) { _parryFovPunchActive = false; _parryCameraPunchRoutine = null; yield break; }
                cam.fieldOfView = Mathf.Lerp(punchFov, _parryFovPunchBase, Mathf.Clamp01(elapsed / ParryFovRestoreSeconds));
                yield return null;
            }
            if (cam != null) cam.fieldOfView = _parryFovPunchBase;
            _parryFovPunchActive = false;
            _parryCameraPunchRoutine = null;
        }

        /// <summary>[95차 F3] OnDisable 등 중단 경로 — 진행 중 FOV 펀치 즉시 원복(복귀 보장).</summary>
        private void RestoreParryCameraFov()
        {
            if (!_parryFovPunchActive) return;
            Camera cam = _mainCamera != null ? _mainCamera : Camera.main;
            if (cam != null) cam.fieldOfView = _parryFovPunchBase;
            _parryFovPunchActive = false;
        }

        private void EndParryWindow()
        {
            _parryActive = false;
            _parryActiveUntil = -999f;
            // [무번호 36-PW2] 만료 신호 — 수렴 링 0.15s(개방 확산의 역방향 — 최소 신호). 히트스톱 없음.
            //  성공 경로는 림 선행 해제로 _parryGuardRim==null — 이 분기는 자연 만료만 발화.
            if (_parryGuardRim != null && Application.isPlaying)
            {
                Vector3 cp = transform.position + transform.forward * 0.8f + Vector3.up * 0.9f;
                ShockwaveRingFX.SpawnConvergingRing(cp, 1.2f, new Color(FXPalette.ParryWhite.r, FXPalette.ParryWhite.g, FXPalette.ParryWhite.b, 0.4f), 0.15f);
            }
            // [무번호 62] 실패/빈 패링 최소 SFX — 성공 위계(Parry_Perfect/Good)와 대비하는 낮은 짧은 신호음.
            //  클립 부재 시 SoundEffectManager 무음 폴백(133차-P2) — 에셋 미배치에도 안전. 히트스톱/시각 FX 무추가(36-PW2 최소 신호 유지).
            if (Application.isPlaying)
            {
                var failSfx = SoundEffectManager.Instance;
                if (failSfx != null)
                    failSfx.PlaySFXAtPoint(SoundEffectManager.SFXType.Parry_Fail, transform.position);
            }
            // [무번호 61] 실패/빈 패링 림 자연 페이드 — 즉시 파괴(Release) 대신 0.25s 강도 페이드 후 해제.
            //   캡처 참조 분리 — 신규 창 림 부착(TryStartParry)과 충돌 없음. EditMode는 동기 해제(테스트 안전).
            if (_parryGuardRim != null)
            {
                if (Application.isPlaying)
                {
                    _parryRimFadeTarget = _parryGuardRim;
                    _parryGuardRim = null;
                    if (_parryRimFadeRoutine != null) StopCoroutine(_parryRimFadeRoutine);
                    _parryRimFadeRoutine = StartCoroutine(ParryRimFadeRoutine(_parryRimFadeTarget));
                }
                else
                {
                    ReleaseParryGuardRim();
                }
            }
            _proceduralAnim?.TriggerAction("parry_end");
            if (_parryImpactRoutine == null)
                ResumeQueuedParryAction();
        }

        // ── [무번호 36-PW] 패망 창 방어 림 — 부착/갱신/해제(멱등) ──

        /// <summary>[무번호 36-PW1] 창 개방 시 방어 림 부착 — WeaponEquipManager.CurrentVisual(105차 F1 계약).
        ///  주먹/미장착 = 스킵(개방 링은 유지). 재입력 시 선행 해제 — 이중 부착 금지.</summary>
        private void AttachParryGuardRim()
        {
            ReleaseParryGuardRim();   // 멱등 — 재입력 이중 부착 방지
            if (!Application.isPlaying) return;   // EditMode 무동작 가드
            GameObject visual = WeaponEquipManager.CurrentVisual;
            if (visual == null) return;
            _parryGuardRim = FxRimOverlay.AttachRimStatic(visual, FXPalette.ParryWhite, 2.5f, ParryGuardRimIntensity);
        }

        /// <summary>[무번호 36-PW1/PW3] 방어 림 프레임 갱신 — 잔여 시간 비례 감쇠(0.45s "닫히는 시계") +
        ///  PERFECT→GOOD 경계(ParryPerfectWindow — 95차 F1 판정과 동일 상수 공유, 이원화 금지) 통과 시
        ///  림 맥동(강도 1.5× 0.25s)+흰 수렴링 1회 — [171차] 흰 단일, 경계=강도.</summary>
        private void UpdateParryGuardRim()
        {
            if (_parryGuardRim == null) return;
            float remaining = Time.unscaledTime < _parryActiveUntil
                ? Mathf.Clamp01((_parryActiveUntil - Time.unscaledTime) / ParryWindow) : 0f;
            float parryPulse = Time.unscaledTime < _parryGoodPulseUntil ? 1.5f : 1f;   // [171차] GOOD 진입 맥동
            FxRimOverlay.SetRimIntensity(_parryGuardRim, ParryGuardRimIntensity * remaining * parryPulse);
            float elapsed = ParryWindow - Mathf.Max(0f, _parryActiveUntil - Time.unscaledTime);
            if (!_parryGoodTickDone && elapsed > ParryPerfectWindow)
            {
                _parryGoodTickDone = true;
                // [171차] 색 통일(흰 단일) — 구 골드→청 전환 폐기. 경계 신호 = 림 맥동(강도 1.5× 0.25s)+흰 수렴링.
                _parryGoodPulseUntil = Time.unscaledTime + 0.25f;
                if (Application.isPlaying)
                {
                    Vector3 cp = transform.position + transform.forward * 0.8f + Vector3.up * 0.9f;
                    ShockwaveRingFX.SpawnConvergingRing(cp, 1.2f, new Color(FXPalette.ParryWhite.r, FXPalette.ParryWhite.g, FXPalette.ParryWhite.b, 0.4f), 0.15f);
                }
            }
        }

        /// <summary>[무번호 36-PW1] 방어 림 해제(멱등) — 성공/만료/재입력 공용.</summary>
        private void ReleaseParryGuardRim()
        {
            if (_parryGuardRim != null)
            {
                FxRimOverlay.Release(_parryGuardRim);
                _parryGuardRim = null;
            }
        }

        /// <summary>[무번호 61] 실패/빈 패링 림 자연 페이드 — 강도를 0.25s 동안 0으로 수렴 후 해제.
        ///  unscaled 시간 기준(패망 창이 unscaledTime 계약 — 95차 F1). rim이 이미 해제됐으면(외부 수명 종료) 조용히 종료.
        ///  EditMode는 호출부 가드로 미발화(테스트 안전).</summary>
        private System.Collections.IEnumerator ParryRimFadeRoutine(GameObject rim)
        {
            const float FadeSeconds = 0.25f;
            float elapsed = 0f;
            while (rim != null && elapsed < FadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                FxRimOverlay.SetRimIntensity(rim, ParryGuardRimIntensity * (1f - Mathf.Clamp01(elapsed / FadeSeconds)));
                yield return null;
            }
            FxRimOverlay.Release(rim);
            _parryRimFadeRoutine = null;
            _parryRimFadeTarget = null;
        }

        /// <summary>[무번호 36-PI3] 트리플 링 3박 — PERFECT 지면 링에 +0.12s 서드 원거리 잔광 링 추가.
        ///  unscaled 대기 — 히트스톱 프리징 중에도 리듬 유지(23-X1 WaitForSecondsRealtime 선례).
        ///  EditMode는 호출부 가드로 미발화(테스트 안전).</summary>
        private System.Collections.IEnumerator ParryThirdRingRoutine(Vector3 groundP)
        {
            yield return new WaitForSecondsRealtime(0.12f);
            ShockwaveRingFX.Spawn(groundP, 4.4f, new Color(FXPalette.ParryWhite.r, FXPalette.ParryWhite.g, FXPalette.ParryWhite.b, 0.45f), 0.75f);
        }

        private void ResumeQueuedParryAction()
        {
            if (_queuedBowAction)
            {
                _queuedWeaponAttacks = 0;
                _queuedBowAction = false;
                if (_queuedBowReleased)
                {
                    CancelBowShotWithoutFreshDraw("queued parry release", _queuedBowHeldTime / BowDrawMaxHold);
                }
                else
                {
                    _bowDrawing = true;
                    _bowDrawHeldTime = _queuedBowHeldTime;
                    _queuedBowAction = false;
                    BowAimState.Begin();
                    _clipDriver?.TriggerBowDraw();
                    AttackSoundLayerManager.PlayBowDraw();
                }
                _queuedBowReleased = false;
                _queuedBowHeldTime = 0f;
                return;
            }
        }

        private System.Collections.IEnumerator ParryImpactRoutine(ParryGrade grade)
        {
            // [95차 F2] 히스톱 — 진입 직전 완전 정지(0.05×50ms hold) 후 기존 dip 진입('물림' 임팩트).
            // 재발동 시 TryParry의 정지+재시작 경로 그대로(히스톱도 재시작) — baseline 보존 로직과 무간섭(선두에서만).
            SetParryLocalTime(ParryHitStopScale);
            float hitstop = 0f;
            while (hitstop < ParryHitStopHoldSeconds)
            {
                hitstop += Time.unscaledDeltaTime;
                yield return null;
            }

            float initial = _parrySlowRestoreValue;
            float dip = 0.10f;
            float hold = 0.38f;
            float restore = 0.28f;
            float elapsed = 0f;
            // [95차 F2] dip은 히스톱 바닥(0.05)에서 순항(0.10)으로 이완 — 기존 dip(→0.10 홀드→복원) 곡선의 목적 유지,
            // 히스톱→dip 전환 시 0.05→initial 팝(튐) 없이 자연스러운 '물림 후 이완' 연결.
            while (elapsed < dip)
            {
                elapsed += Time.unscaledDeltaTime;
                SetParryLocalTime(Mathf.Lerp(ParryHitStopScale, ParryActionScale, Mathf.Clamp01(elapsed / dip)));
                yield return null;
            }
            SetParryLocalTime(ParryActionScale);
            elapsed = 0f;
            while (elapsed < hold)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // [95차 F1] PERFECT 보상 ② — 슬로우 복원 전 큐된 공격 첫 1타 자동 해방(×ParryCounterMultiplier).
            // Update 드레인 게이트(_parryImpactRoutine == null)로 슬로우 중 이중 해방은 없다 — 이 호출이 유일한 창구.
            if (grade == ParryGrade.Perfect)
            {
                float counter = ConsumeQueuedCounterAttack();
                if (counter > 1f)
                {
                    _pendingCounterAttackMultiplier = counter;
                    if (!TryAttack())
                        _pendingCounterAttackMultiplier = 1f; // 쿨다운 등 게이트 거절 — 보상 미적용, 큐는 기존 드레인 경로로 보존
                    else
                    {
                        PlayParryCounterMotion();   // [95차 후속] 반격 모션 1회 재생 — PERFECT 반격 타격 확정 시점(큐 해방 성공 타)
                        // [패링P2-C] 반격 해방 임팩트 — 흰 지면 링 1겹+히트스톱 0.03s(159차 몬스터 명중 임팩트 선례의 반격 창구 적용).
                        Vector3 cGround = transform.position + transform.forward * 1f;
                        cGround.y = 0.05f;
                        ShockwaveRingFX.Spawn(cGround, 1.4f, new Color(FXPalette.ParryWhite.r, FXPalette.ParryWhite.g, FXPalette.ParryWhite.b, 0.7f), 0.45f);
                        if (Application.isPlaying)
                            HitStopManager.RequestHitStop(0.03f, 0.12f);
                        Debug.Log("[PlayerCombat] ⚔️ 반격 해방 임팩트 — 지면 링+히트스톱 [패링P2-C]");
                    }
                }
            }

            elapsed = 0f;
            while (elapsed < restore)
            {
                elapsed += Time.unscaledDeltaTime;
                // [패링P2-D] 복원 ease-out — 빠른 이탈 후 느린 안착(물림 체감 연장).
                float rt = Mathf.Clamp01(elapsed / restore);
                rt = 1f - (1f - rt) * (1f - rt);
                SetParryLocalTime(Mathf.Lerp(ParryActionScale, initial, rt));
                yield return null;
            }
            SetParryLocalTime(initial);
            _parryImpactRoutine = null;
            _parrySlowActive = false;
            ResumeQueuedParryAction();
        }

        // ═════════ [95차 후속] 패링 클립 연결 구현 — 92차 VillagerWanderer 선례 재사용 ═════════

        /// <summary>[95차 후속] Update 폴링 와이어 — 클립 1회 해석 후 plain Player_AC 감지 시 오버라이드 부착.
        /// 근거: TestPlayerAnimatorBoot가 PlayerModel 발견 후 Player_AC를 재부착하므로 Start 시점 1회 할당은
        /// 부트와 순서 경쟁 → plain Player_AC(이름 일치) 재감지 폴링으로 어느 순서든 연결 유지.
        /// Player_AC 부재(부착 경로 없는 씬) 또는 오버라이드 컨트롤러 구성 실패 시 와이어 생략 = 기존 동작 폴백(무해).</summary>
        private void EnsureParryClipsWired()
        {
            if (!_parryClipsResolved) ResolveParryClips();   // 1회 해석 가드(연결 로그 1회)
            if (_parryOverride == null || _parryAnimator == null) return;
            if (_parryClipsWiredAnimator == _parryAnimator) return;   // 이미 부착 — 중복 스왑 방지
            var current = _parryAnimator.runtimeAnimatorController;
            if (current == null || current.name != PlayerAcControllerName) return;   // plain Player_AC만 감지
            _parryAnimator.runtimeAnimatorController = _parryOverride;
            _parryClipsWiredAnimator = _parryAnimator;
        }

        /// <summary>[95차 후속] 방어/반격 클립 로드+Player_AC 오버라이드 컨트롤러 구성(정적 캐시 세션당 1회).
        /// 폴더 레벨 LoadAll 키워드 매칭 실패 시 소스 FBX 파일 단위 LoadAll 이차 판정(92차 선례: 테이크명 "mixamo.com").
        /// 둘 다 누락이면 오버라이드 미사용(기존 Parry 클립 유지 — 폴백 계약).</summary>
        private static void ResolveParryClips()
        {
            _parryClipsResolved = true;
            var clips = Resources.LoadAll<AnimationClip>(ParryClipResourceFolder);
            _parryBlockClip = FirstParryClipNameContaining(clips, "block");
            _parryCounterClip = FirstParryClipNameContaining(clips, "slash");

            // 믹사모 테이크명 "mixamo.com" → 키워드 매칭 불가/모호 시 파일 단위 이차 판정 — 파일명이 곧 클립 소스 계약.
            if (_parryBlockClip == null || _parryCounterClip == null || _parryBlockClip == _parryCounterClip)
            {
                var blockFileClips = Resources.LoadAll<AnimationClip>(ParryClipResourceFolder + "/" + ParryBlockFileKey);
                var counterFileClips = Resources.LoadAll<AnimationClip>(ParryClipResourceFolder + "/" + ParryCounterFileKey);
                if (_parryBlockClip == null && blockFileClips != null && blockFileClips.Length > 0) _parryBlockClip = blockFileClips[0];
                if (_parryCounterClip == null && counterFileClips != null && counterFileClips.Length > 0) _parryCounterClip = counterFileClips[0];
            }

            if (_parryBlockClip == null && _parryCounterClip == null)
            {
                Debug.LogWarning("[PlayerCombat] 패링 클립 로드 실패 — 오버라이드 미사용, 기존 Parry 클립 유지(폴백)");
                return;
            }
            var baseCtrl = Resources.Load<RuntimeAnimatorController>("Animation/Controllers/Player_AC");
            if (baseCtrl == null)
            {
                Debug.LogWarning("[PlayerCombat] Player_AC 로드 실패 — 패링 클립 오버라이드 생략(폴백)");
                return;
            }
            _parryOverride = BuildParryOverrideController(baseCtrl);
            Debug.Log($"[PlayerCombat] 패링 클립 연결 — block={(_parryBlockClip != null ? _parryBlockClip.name : "없음")} " +
                      $"counter={(_parryCounterClip != null ? _parryCounterClip.name : "없음")}");
        }

        /// <summary>[95차 후속] 클립명 키워드 매칭(lowercase contains — 첫 번째 일치 반환).</summary>
        private static AnimationClip FirstParryClipNameContaining(AnimationClip[] clips, string keyword)
        {
            if (clips == null) return null;
            foreach (var clip in clips)
            {
                if (clip != null && clip.name.ToLowerInvariant().Contains(keyword)) return clip;
            }
            return null;
        }

        /// <summary>[95차 후속] Player_AC 베이스 오버라이드 컨트롤러(런타임 API — 컨트롤러 에셋 무수정).
        /// Parry 슬롯(실측 원본 클립 "Armature|Sword_Parry_Backward_1_withSkin")만 방어 클립으로 교체 —
        /// 슬롯 미발견 시 교체 0 건 = 동작상 Player_AC와 동일(안전 폴백).</summary>
        private static AnimatorOverrideController BuildParryOverrideController(RuntimeAnimatorController baseCtrl)
        {
            var overrideCtrl = new AnimatorOverrideController(baseCtrl);
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrideCtrl.GetOverrides(pairs);
            bool swapped = false;
            if (_parryBlockClip != null)
            {
                for (int i = 0; i < pairs.Count; i++)
                {
                    var origin = pairs[i].Key;
                    if (origin != null && origin.name == PlayerAcParrySlotClipName)
                    {
                        pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(origin, _parryBlockClip);
                        swapped = true;
                    }
                }
            }
            if (swapped) overrideCtrl.ApplyOverrides(pairs);
            return overrideCtrl;
        }

        /// <summary>[95차 후속] PERFECT 반격 타격 확정 시 반격 모션 1회 재생 — Parry 슬롯을 반격 클립으로
        /// 임시 교체 후 Parry 상태 크로스페이드, 클립 길이 후 방어 클립 슬롯 복원. 클립 부재 시 no-op(기존 동작 폴백).</summary>
        private void PlayParryCounterMotion()
        {
            if (_parryAnimator == null || _parryCounterClip == null) return;   // 폴백 가드
            if (!SwapParrySlotClip(_parryCounterClip)) return;
            _parryAnimator.CrossFadeInFixedTime(PlayerAcParryStateName, 0.1f);
            if (_parryCounterOverrideRoutine != null) StopCoroutine(_parryCounterOverrideRoutine);
            _parryCounterOverrideRoutine = StartCoroutine(RestoreParryBlockSlotAfterCounter(_parryCounterClip.length));
        }

        /// <summary>[95차 후속] 신규 패링 진입 전 반격 슬롯 임시 교체 원복 — 코루틴 정리+방어 클립 슬롯 즉시 복원.</summary>
        private void RestoreParryCounterSlotBeforeParry()
        {
            if (_parryCounterOverrideRoutine != null)
            {
                StopCoroutine(_parryCounterOverrideRoutine);
                _parryCounterOverrideRoutine = null;
            }
            RestoreParryBlockSlot();
        }

        /// <summary>[95차 후속] Parry 슬롯을 방어 클립으로 복원 — 방어 클립/오버라이드 부재 시 no-op(무해).</summary>
        private void RestoreParryBlockSlot()
        {
            if (_parryOverride != null && _parryBlockClip != null)
                SwapParrySlotClip(_parryBlockClip);
        }

        /// <summary>[95차 후속] 반격 모션 재생 후 지정 시간(unscaled) 경과 시 방어 클립 슬롯 복원.</summary>
        private System.Collections.IEnumerator RestoreParryBlockSlotAfterCounter(float delay)
        {
            float end = Time.unscaledTime + delay;
            while (Time.unscaledTime < end) yield return null;
            RestoreParryBlockSlot();
            _parryCounterOverrideRoutine = null;
        }

        /// <summary>[95차 후속] Parry 슬롯(원본 클립명 일치)만 교체 — 슬롯 미발견 시 false(교체 0 건, 안전 폴백).</summary>
        private bool SwapParrySlotClip(AnimationClip replacement)
        {
            if (_parryOverride == null || replacement == null) return false;
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            _parryOverride.GetOverrides(pairs);
            bool swapped = false;
            for (int i = 0; i < pairs.Count; i++)
            {
                var origin = pairs[i].Key;
                if (origin != null && origin.name == PlayerAcParrySlotClipName)
                {
                    pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(origin, replacement);
                    swapped = true;
                }
            }
            if (swapped) _parryOverride.ApplyOverrides(pairs);
            return swapped;
        }

        private void SetParryLocalTime(float value)
        {
            if (_parryTimeUser != null)
                _parryTimeUser.LocalTimeScaleValue = Mathf.Clamp(value, 0.05f, 1f);
            if (_parryAnimator != null && _parryAnimatorSpeedCaptured)
                _parryAnimator.speed = _animatorSpeedBeforeParry * (_parryTimeUser != null ? _parryTimeUser.TimeScale : 1f);
        }

        private void UpdateParryActionSlowMotion()
        {
            if (_parryTimeUser != null && _parryAnimator != null && _parryAnimatorSpeedCaptured)
                _parryAnimator.speed = _animatorSpeedBeforeParry * _parryTimeUser.TimeScale;
        }

        private void OnDisable()
        {
            _parryActive = false;
            if (_parryRimFadeRoutine != null)   // [무번호 61] 페이드 중 비활성화 — 구 림 즉시 해제
            {
                StopCoroutine(_parryRimFadeRoutine);
                _parryRimFadeRoutine = null;
                FxRimOverlay.Release(_parryRimFadeTarget);
                _parryRimFadeTarget = null;
            }
            if (_parryImpactRoutine != null)
            {
                StopCoroutine(_parryImpactRoutine);
                _parryImpactRoutine = null;
            }
            if (_parryImpactSequence != null)   // [무번호 23-X1] 시퀀서 정리
            {
                StopCoroutine(_parryImpactSequence);
                _parryImpactSequence = null;
            }
            if (_parryCameraPunchRoutine != null)
            {
                StopCoroutine(_parryCameraPunchRoutine);
                _parryCameraPunchRoutine = null;
            }
            if (_parryCounterOverrideRoutine != null)   // [95차 후속] 반격 슬롯 복원 코루틴 정리
            {
                StopCoroutine(_parryCounterOverrideRoutine);
                _parryCounterOverrideRoutine = null;
            }
            RestoreParryBlockSlot(); // [95차 후속] 반격 슬롯 임시 교체 원복 — 방어 클립 계약 복원(기존 정리 계약 편입)
            RestoreParryCameraFov(); // [95차 F3] FOV 펀치 중단 시 원복 — 카메라 상태 누출 방지
            if (_parrySlowActive && _parryTimeUser != null)
                _parryTimeUser.LocalTimeScaleValue = Mathf.Clamp(_parrySlowRestoreValue, 0.05f, 1f);
            _parrySlowActive = false;
            _queuedWeaponAttacks = 0;
            _pendingCounterAttackMultiplier = 1f; // [95차 F1] 반격 배율 리셋 — 정리 계약에 편입
            _queuedBowAction = false;
            _queuedBowReleased = false;
            _queuedBowHeldTime = 0f;
            if (_bowDrawing)
            {
                _bowDrawing = false;
                _bowDrawHeldTime = 0f;
                BowAimState.Release(false, 0f);
            }
            if (_parryAnimator != null && _parryAnimatorSpeedCaptured)
                _parryAnimator.speed = _animatorSpeedBeforeParry;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>[테스트45 P5] 패링 성공 순간 시각 피드백 — 검증된 ShockwaveRingFX 확산링 + 별섬광 (피격 이펙트와 별개 감탄용).
        /// [95차 F1] PERFECT는 링을 금색(1,0.85,0.3)으로, GOOD은 기존 청색(0.65,0.9,1) 유지. 별섬광은 등급 무관 유지.
        /// [95차 F4] 방향성 스파크 — kbDir 역방향(공격자 방향) 원뿔 발사, 기존 링/별섬광과 병행(교체 아님).</summary>
        private void TryParryImpactFX(ParryGrade grade, Vector3 kbDir)
        {
            try
            {
                if (Application.isPlaying)
                {
                    // [무번호 23-X1 → 176차] 시퀀서 — wave 0(원형 링/화면 계층) 단일 발화(방사형 산란 wave 1~3 제거, 아래 ParryImpactSequence).
                    if (_parryImpactSequence != null) StopCoroutine(_parryImpactSequence);
                    _parryImpactSequence = StartCoroutine(ParryImpactSequence(grade, kbDir));
                }
                else
                {
                    SpawnParryImpactElements(grade, kbDir, 0);   // [176차] EditMode — wave0 원형 링 계열 단일 발화(테스트 안전)
                }
            }
            catch (System.Exception e) { Debug.LogWarning($"[PlayerCombat] 패링 FX 실패: {e.Message}"); }
        }

        private Coroutine _parryImpactSequence;   // [무번호 23-X1] 시퀀서 코루틴 핸들(UnityEngine.Coroutine)

        /// <summary>[무번호 23-X1 → 176차] 패막 임팩트 시퀀서 — [176차] 방사형 산란 제거(사용자 지시: 원형 링만 유지):
        ///   wave 1(별섬광·십자 글린트)/wave 2(곡선 위스프·리플렉 궤적)/wave 3(12방향 버스트) 철수로
        ///   wave 0 단일 발화로 단순화. 스태거 WaitForSecondsRealtime 체계는 산란 요소 소멸과 함께 폐기.
        ///   코루틴 셸·정리 로직(StopCoroutine/_parryImpactSequence/OnDisable 정리)은 무손상 유지.</summary>
        private System.Collections.IEnumerator ParryImpactSequence(ParryGrade grade, Vector3 kbDir)
        {
            SpawnParryImpactElements(grade, kbDir, 0);   // [176차] wave 0만 — 지면 링/아우터/트리플/수렴 링/미니링(원형 계열)
            _parryImpactSequence = null;
            yield break;   // [176차] 스태거 폐기 — 코루틴 셸 유지(정리 계약 무손상)
        }

        /// <summary>[무번호 23-X1 → 176차] 웨이브별 임팩트 요소 발화 — [176차] 방사형 산란 제거:
        ///   wave 0(유지): 히트스톱/화이트 프레임/비네트/라이트/블룸/SFX/지면 링 2겹/아우터/트리플/수렴-역전 링/GOOD 미니링/보유 에셋.
        ///   wave 1(별섬광·십자 글린트)/wave 2(곡선 위스프·리플렉 궤적)/wave 3(12방향 버스트) — 방사형 산란으로 철수(정의 무손상).</summary>
        private void SpawnParryImpactElements(ParryGrade grade, Vector3 kbDir, params int[] waves)
        {
            try
            {
                bool wave(int n) => System.Array.IndexOf(waves, n) >= 0;
                if (wave(0))
                {
                    // [159차 P3] 패링 히트스톱 — PERFECT 0.07s/GOOD 0.03s 프리징(scale 0.12).
                    if (Application.isPlaying)
                        HitStopManager.RequestHitStop(grade == ParryGrade.Perfect ? 0.07f : 0.03f, 0.12f);
                    // [패링P2-A] PERFECT 화이트 프레임 — 카메라 전면 페이드 쿼드 0.07s.
                    if (grade == ParryGrade.Perfect)
                        SpawnParryScreenFlash();
                    // [무번호 28-P1] 패링 무기 본체 플래시 — 장착 무기 비주얼 전체 발광 펄스(아래 WeaponFlashPulseRoutine).
                    //   PERFECT/GOOD 공통(패망 자체 피드백) — 등급 차등은 기존 위계(J2)가 담당.
                    if (Application.isPlaying) StartCoroutine(WeaponFlashPulseRoutine());
                    // [무번호 23-V2] 패링 PERFECT 흰 비네트 — 판정 피드백의 화면 언어(월드 FX와 별개 계층).
                    if (grade == ParryGrade.Perfect)
                    {
                        var vc = _mainCamera != null ? _mainCamera : Camera.main;
                        ScreenVignetteFX.Begin(vc, new Color(FXPalette.ParryWhite.r, FXPalette.ParryWhite.g, FXPalette.ParryWhite.b, 0.25f), 0.4f, 0.05f, 0.08f, 0.3f);
                        // [176차] 크로매틱 수차 스파이크 철수 — RGB 프린지(RGB 채널 분리)가 화이트 플래시를 청백으로 왜곡.
                        //   완벽 패막 화면 순백 계약: 비네트(ParryWhite)/라이트(ParryWhite)/화이트 프레임(Color.white)/블룸(무채색 강도)만 유지.
                        // [무번호 33-A2] 패망 PERFECT 흰 라이트 — 흡수점(플레이어 앞 1m) 실광 펄스
                        //   (시퀀서 wave0 연동, 색=FXPalette.ParryWhite 단일 소스, 동시 상한 3 예산 가드).
                        FxImpactLight.Spike(transform.position + transform.forward * 1f + Vector3.up * 0.9f,
                            FXPalette.ParryWhite, 2.4f, 4f, 0.3f);
                        // [무번호 33-B1] 블룸 임팩트 펄스 — 글로벌 블룸 위 스파이크(별도 볼륨 프로필 — CA 볼륨과 파라미터 충돌 없음).
                        ScreenBloomFX.Spike(vc, 2.2f, 0.12f);
                    }
                }

                // [95차 F4] 방향 계산 선행 — [무번호 36-PI1] 흡수점이 공격자 방향을 따라감(접촉 정합).
                Vector3 sparkDir = -kbDir;   // kbDir 역방향 = 공격자 방향. 폴백은 전방.
                if (sparkDir.sqrMagnitude < 0.0001f) sparkDir = transform.forward;
                sparkDir = sparkDir.normalized;
                // 패망 흡수 지점 = 공격자 방향 1m [무번호 36-PI1] — 기존 고정 전방 공식 대체(공격이 실제 닿는 쪽 정합).
                //  [176차] 잔존 요소(수렴 링/SFX/보유 에셋) 전부 p 기반 — 이 1곳 수정으로 동기 이동. 폴백(kbDir 영벡터)=기존 전방 공식 동일 결과.
                Vector3 p = transform.position + sparkDir * 1f + Vector3.up * 0.9f;
                Vector3 groundP = new Vector3(p.x, 0.05f, p.z);
                // [171차] 흰 단일 — 등급=강도(크기·지속 차등, 색 이원화 폐기).
                Color ringColor = FXPalette.ParryWhite;

                if (wave(0))
                {
                    // [159차 P3] 지면 링 파동 — PERFECT=금색 2겹(스태거)/GOOD=청색 1겹(축소 반경).
                    //   [172차 P2] PERFECT 확산링 알파 하향 0.75→0.45 — 보유 에셋 FX+레이어드 증가분 과포화 완화.
                    ShockwaveRingFX.Spawn(groundP, grade == ParryGrade.Perfect ? 2.2f : 1.2f,
                        new Color(FXPalette.ParryWhite.r, FXPalette.ParryWhite.g, FXPalette.ParryWhite.b, grade == ParryGrade.Perfect ? 0.45f : 0.6f), 0.55f);
                    if (grade == ParryGrade.Perfect)
                    {
                        // PERFECT 아우터 링 — 반경 1.5×, 반대 위상(길고 얇은 잔광) — 카운터 임팩트 확산
                        ShockwaveRingFX.Spawn(groundP, 3.3f, new Color(FXPalette.ParryWhite.r, FXPalette.ParryWhite.g, FXPalette.ParryWhite.b, 0.5f), 0.75f);
                        // [무번호 36-PI3] 트리플 링 3박 — +0.12s 서드 원거리 잔광 링(파열 리듬의 셋째 박).
                        if (Application.isPlaying)
                            StartCoroutine(ParryThirdRingRoutine(groundP));
                    }
                    // [172차 P3-L2] 수렴-역전 링 — 흡수점 단일 링(수렴 0.08s→역전 확산 0.25s ease-out).
                    //   기존 Fluent 확산링(Spawn(p,1.6f))과 교체 판단: 보유 에셋 FX+코어글린트와 동점 배치 시
                    //   확산 링 2겹 중첩 과포화 — 2페이즈 링 1겹으로 위계 정리(교체 보고 = 본 주석).
                    ShockwaveRingFX.SpawnParryConvergeRing(p, 1.6f, ringColor);

                    // [무번호 36-PG1] GOOD 톤 보강 — 흰 미니 링([171차] 흰 단일 — 성공이지만 만점 아님).
                    //   PERFECT 위계(2겹+아우터+트리플 3박) 무손상 — GOOD 증분만.
                    if (grade != ParryGrade.Perfect)
                        ShockwaveRingFX.Spawn(p, 0.9f, new Color(FXPalette.ParryWhite.r, FXPalette.ParryWhite.g, FXPalette.ParryWhite.b, 0.6f), 0.35f);

                    // [95차 후속 SFX] 패링 실오디오 — 등급별 금속 clang(Perfect 밝은 '딩!'/Good 무디어 clang).
                    if (Application.isPlaying)
                    {
                        var parrySfx = SoundEffectManager.Instance;
                        if (parrySfx != null)
                        {
                            SoundEffectManager.SFXType parrySfxType = grade == ParryGrade.Perfect
                                ? SoundEffectManager.SFXType.Parry_Perfect
                                : SoundEffectManager.SFXType.Parry_Good;
                            parrySfx.PlaySFXAtPoint(parrySfxType, p);
                        }
                    }
                    // [171차 → 176차] 보유 에셋 FX — 메인 시각(Guard_01/Holy hit 접촉점 배치, Sparks white 방사형 스파크 철수), 절차 링은 지면 보조.
                    ParryAssetFX.Spawn(grade == ParryGrade.Perfect, p);
                }
            }
            catch (System.Exception e) { Debug.LogWarning($"[PlayerCombat] 패링 FX 웨이브 실패: {e.Message}"); }
        }

        /// <summary>
        /// [무번호 28-P1] 패링 무기 본체 플래시 — 장착 무기 비주얼 전체 발광 펄스 0.15s.
        /// 대상: WeaponEquipManager.CurrentVisual(105차 F1 계약) — null(주먹/미장착)이면 즉시 종료.
        /// GetComponentsInChildren<Renderer>(true) 전부 순회(Trail/Particle/Line 스킵 — AnimalAI M1 필터 선례),
        /// 각 인스턴스 material에서 _EmissionColor(HasProperty 가드)를 캡처 후 FXPalette.ParryWhite.rgb × 2.2f로
        /// 펄스하고, 캡처값으로 원복(기존 발광 무손상 — 계약 "머티리얼 인스턴스 원복 보장"). Unscaled 시간 기준.
        /// </summary>
        private System.Collections.IEnumerator WeaponFlashPulseRoutine()
        {
            GameObject visual = WeaponEquipManager.CurrentVisual;
            if (visual == null) yield break;   // 주먹/미장착 — 즉시 종료

            // [무번호 29-P1-2] 무기 플래시 림 — 가장자리만 강조하는 프레넬 계층(D-3, 단색 오버레이 탈피).
            //   캡처/원복 계약 무손상(아래 emissive 루프 그대로) — 림은 별도 오버레이 인스턴스(누출 금지).
            GameObject rim = FxRimOverlay.AttachRimStatic(visual, FXPalette.ParryWhite, 2.5f, 1.6f);

            var captured = new System.Collections.Generic.List<(Renderer rend, Color emission)>();
            foreach (var rend in visual.GetComponentsInChildren<Renderer>(true))
            {
                if (rend is TrailRenderer || rend is ParticleSystemRenderer || rend is LineRenderer)
                    continue;   // 이펙트 렌더러 스킵 — AnimalAI M1 필터 선례
                if (rend.sharedMaterial == null) continue;
                var mat = rend.material;   // 인스턴스 머티리얼 — 원복은 동일 인스턴스
                if (mat == null || !mat.HasProperty("_EmissionColor")) continue;
                captured.Add((rend, mat.GetColor("_EmissionColor")));   // 캡처
                mat.EnableKeyword("_EMISSION");
                Color flashWhite = FXPalette.ParryWhite;
                mat.SetColor("_EmissionColor", new Color(flashWhite.r * 2.2f, flashWhite.g * 2.2f, flashWhite.b * 2.2f, 1f));
            }
            yield return new WaitForSecondsRealtime(0.15f);
            if (rim != null) FxRimOverlay.Release(rim);   // [무번호 29-P1-2] 림 해제(인스턴스 누출 금지)
            foreach (var cap in captured)
            {
                if (cap.rend == null) continue;   // 파괴된 렌더러 — 무해 스킵
                var mat = cap.rend.material;
                if (mat == null || !mat.HasProperty("_EmissionColor")) continue;
                mat.SetColor("_EmissionColor", cap.emission);   // 캡처값 원복 — 기존 발광 무손상
            }
        }

        /// <summary>[패링P2-A] PERFECT 화이트 프레임 — 카메라 자식 풀스크린 페이드 쿼드(ParryFlashFader, 0.07s).
        ///   Systems 계약: UI 어셈블리 직접 참조 없이 카메라 공간 쿼드. 실패 시 무음 스킵(시각 보조 요소).</summary>
        private void SpawnParryScreenFlash()
        {
            try
            {
                Camera cam = _mainCamera != null ? _mainCamera : Camera.main;
                if (cam == null) return;
                // [무번호 19-D2 진단] 화이트 프레임 렌더 결손 원인 — 카메라 참조/로컬 배치/셰이더/nearClip 실측 덤프
                var flash = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.Destroy(flash.GetComponent<Collider>());
                flash.name = "ParryWhiteFrame";
                flash.transform.SetParent(cam.transform, false);
                float halfH = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 0.5f;
                float halfW = halfH * cam.aspect;
                flash.transform.localScale = new Vector3(halfW * 2.4f, halfH * 2.4f, 1f);
                flash.transform.localPosition = new Vector3(0f, 0f, 0.5f);
                flash.transform.localRotation = Quaternion.identity;
                var mrD = flash.GetComponent<MeshRenderer>();
                // [176차] 순백 화이트 프레임 유지(Color.white) — 청백 왜곡 원인(크로매틱 RGB 프린지)은 wave0 발화부 철수로 제거.
                // [205차] 잔여 청백 왜곡 근원 특정(전수 실측) — 피크 알파 0.85(<1.0). Sprites/Default 알파 블렌드
                //   (SrcAlpha, OneMinusSrcAlpha)에서 합성색 = a·백 + (1−a)·씬 — 피크 프레임에도 씬이 15% 잔존하고,
                //   삼각 램프+프레임 샘플링(60fps, 0.07s 기준 스텝≈0.24)으로 실측 최대 알파 ≈0.85~0.95 → 어떤 프레임도
                //   오파크에 도달하지 못해 청톤 씬(하늘/실내 틴트)에서 화이트 프레임이 청백으로 보임(사용자 재보고 근원).
                //   수리: 피크 1.0(오파크 — 합성식에서 씬 기여 계수 0 = 순백 수학적 보장) + ParryFlashFader 피크 홀드.
                //   색 단일 소스 계약 유지(171차): FXPalette.ParryWhite.rgb=(1,1,1) 불변 — 알파 교체는 171차
                //   "알파는 용도별 조정 전제" 범위. 비네트/라이트/블룸(무채색) 경로는 176차 계약 그대로 유지.
                var fader = flash.AddComponent<ProjectName.Systems.ParryFlashFader>();
                fader.Init(Color.white, 1f, 0.07f);
                Debug.Log($"[PlayerCombat][D2] 화이트프레임 스폰 parent={cam.name} pos={cam.transform.position} localPos={flash.transform.localPosition} localScale={flash.transform.localScale} mr={mrD != null} shader={(mrD != null && mrD.sharedMaterial != null ? mrD.sharedMaterial.shader.name : "NULL")} near={cam.nearClipPlane}");
            }
            catch (System.Exception e) { Debug.LogWarning($"[PlayerCombat] 화이트 프레임 실패(시각 생략): {e.Message}"); }
        }

        /// <summary>
        /// C4-08: 타겟 상태를 업데이트합니다.
        /// 타겟이 죽었거나 너무 멀어지면 해제합니다.
        /// </summary>
        private void UpdateTargetState()
        {
            if (_currentTarget == null) return;

            // 타겟이 죽었는지 확인
            if (!_currentTarget.IsAlive)
            {
                ClearTarget();
                return;
            }

            // 타겟 오브젝트가 파괴되었는지 확인 (MonoBehaviour null 체크)
            MonoBehaviour targetBehaviour = _currentTarget as MonoBehaviour;
            if (targetBehaviour == null) // 오브젝트 파괴됨
            {
                ClearTarget();
                return;
            }

            // 타겟 오브젝트가 범위를 벗어났는지 확인
            float dist = Vector3.Distance(transform.position, targetBehaviour.transform.position);
            if (dist > _autoAimRange * 1.5f)
            {
                ClearTarget();
            }
        }

        /// <summary>
        /// C4-08: 타겟 해제
        /// </summary>
        private void ClearTarget()
        {
            _currentTarget = null;
        }

        private bool TryAttack()
        {
            // [63-B] 혼탁/공포/취기 — 콤보 입력 누락 판정(합산 확률, 상한 0.8 시스템 내부 클램프).
            float missChance = PlayerStatusEffectSystem.ComboMissChance();
            if (missChance > 0f && Random.value < missChance)
            {
                Debug.Log("[PlayerCombat] [63-B] 상태이상 — 공격 입력 누락(콤보 미스)");
                return false;
            }

            // Driver creation can be deferred until after PlayerCombat.Start (for example by boot/test setup).
            // Refresh only a missing/destroyed cache here, immediately before melee ownership is decided.
            if (_clipDriver == null)
            {
                HumanoidClipDriver discovered = GetComponentInChildren<HumanoidClipDriver>(true);
                _clipDriver = ResolveMeleeComboDriver(_clipDriver, discovered);
                if (_clipDriver != null)
                    Debug.Log($"[CombatInput] discovered delayed HumanoidClipDriver on '{_clipDriver.gameObject.name}'");
            }

            bool fistOrSword = _currentWeapon != null
                && (_currentWeapon.weaponType == WeaponType.Fist || _currentWeapon.weaponType == WeaponType.Sword);
            bool spear = _currentWeapon != null && _currentWeapon.weaponType == WeaponType.Spear;
            bool comboReady = _clipDriver != null && _clipDriver.CanPlayPlayerMeleeCombo;
            bool useMeleeCombo = ShouldUseMeleeCombo(fistOrSword, comboReady);
            bool useSpearDriver = ShouldUseSpearAttackDriver(spear, comboReady);
            bool comboActive = useMeleeCombo && _clipDriver.IsPlayerMeleeComboActive;
            bool comboFollowup = useMeleeCombo && _clipDriver.CanAcceptBufferedMeleeFollowup;
            bool expiredComboRestart = useMeleeCombo && _clipDriver.CanRestartExpiredMeleeCombo;
            bool spearFollowup = useSpearDriver && _clipDriver.CanAcceptSpearFollowup;
            bool spearRestart = useSpearDriver && _clipDriver.CanRestartExpiredSpearSequence;
            bool canAttack = CanAttack;
            if (fistOrSword)
                Debug.Log($"[CombatInput] melee route={(useMeleeCombo ? "HumanoidClipDriver combo" : "legacy fallback")} driverReady={comboReady}");

            if (ShouldRejectMeleeAttack(comboActive, comboFollowup, expiredComboRestart, canAttack)
                || (useSpearDriver && !canAttack && !spearFollowup && !spearRestart))
            {
                Debug.Log($"[CombatInput] attack rejected type={_currentWeapon?.weaponType.ToString() ?? "none"} "
                    + $"comboActive={comboActive} followup={comboFollowup} expiredRestart={expiredComboRestart} cooldownReady={canAttack} "
                    + $"spearFollowup={spearFollowup} sequence={_acceptedAttackSequence}");
                return false;
            }

            if (useMeleeCombo || useSpearDriver)
                Debug.Log($"[CombatInput] melee accepted followup={comboFollowup} active={comboActive} "
                    + $"sequence={_acceptedAttackSequence + 1} cooldownReady={canAttack}");
            else if (fistOrSword)
                Debug.LogWarning($"[CombatInput] melee accepted via legacy fallback; driverReady={comboReady}");

            _attackStreak = (Time.time - _lastAttackTime <= AttackStreakWindow)
                ? Mathf.Min(_attackStreak + 1, AttackStreakMax)
                : 1;
            _lastAttackTime = Time.time;
            _acceptedAttackSequence++;

            // ── P6 (2026-09-11): 무기 타입별 좌클릭 공격 분기 ──
            // Bow: 화살 발사 경로 — 발사체(ArrowProjectile)가 데미지를 담당하므로 근접
            //      AttackTarget/자동조준/근접 스윕을 호출하지 않는다(발사 성공/실패 모두 return).
            //      accepted sequence 갱신(위)으로 HumanoidClipDriver가 Bow 분기에서
            //      ArcheryShot을 자동 트리거 + TryBowShot 내부에서 TriggerBowShot()으로 명시 보강.
            // Spear uses delayed driver-owned opener/follow-up hits when its Animator is ready.
            // Without a ready driver it retains the ordinary immediate fallback below.
            //      Spear는 사거리 4m < _autoAimRange 15m라 기존 조준 범위로 충분 — 별도 조정 없음.
            //      Fist/Sword는 기존 WeaponCombo B안이 드라이버에서 그대로 처리됨(정밀튜닝 보존).
            if (_currentWeapon != null && _currentWeapon.weaponType == ProjectName.Core.WeaponType.Bow)
            {
                CancelBowShotWithoutFreshDraw("direct TryAttack fallback", 1f);
                return true;
            }
            // Fist/Sword and ready Spear routes resolve hits only at their driver-owned normalized-time markers.
            if (useMeleeCombo || useSpearDriver)
                return true;

            // Phase B: 무기별 스윙 사운드 레이어링
            PlayWeaponSwingSound();

            // 스윙 VFX는 HumanoidClipDriver가 콤보 임팩트 프레임(1~3타)에서 스윙 방향에 맞춰 발화

            // 공격 애니메이션 트리거
            _rigAnim?.Attack();
            _proceduralAnim?.TriggerAction("attack");

            // C4-08: 커서 방향으로 자동 조준 먼저 시도
            bool hitAny = false; // 미스 판정용 — 어떤 경로로든 AttackTarget 도달 시 true
            IDamageable autoAimTarget = FindTargetInCursorDirection();
            if (autoAimTarget != null)
            {
                // 자동 조준 성공 → 타겟 공격
                _currentTarget = autoAimTarget;
                // 페이스 타깃(2026-09-14): 이펙트/타격 전 타겟 방향으로 신속 회전 — 공격 순간에만 동작해
                // PlayerMovement.CursorTurn(이동 입력 회전)과의 충돌을 최소화. 타겟 없으면 호출하지 않음(현재 방향 유지).
                StartFaceTarget(autoAimTarget);
                AttackTarget(_currentTarget);
                hitAny = true;
            }
            else if (AttackCenterScreen())
            {
                hitAny = true;
            }
            else
            {
                // 자동 조준 + 화면 중앙 SphereCast 모두 실패 → 근접 스윕 폴백(안전망):
                // 플레이어 전방 무기 사거리 내 가장 가까운 살아있는 IDamageable 즉시 적중.
                IDamageable sweep = MeleeSweepFallback();
                if (sweep != null)
                {
                    MonoBehaviour smb = sweep as MonoBehaviour;
                    Debug.Log($"[PlayerCombat] 근접 스윕 폴백 적중: {smb?.name ?? "?"} dist={Vector3.Distance(transform.position, smb.transform.position):F1}m");
                    _currentTarget = sweep;
                    StartFaceTarget(sweep); // 폴백 스윕 타겟도 동일하게 페이스 타깃 적용
                    AttackTarget(sweep);
                    hitAny = true;
                }
            }

            // 빈 스윙(미스) — 마지막 적중 정보 무효화 → 십자가 VFX 스킵(맞는 대상 지점에만 발화)
            if (!hitAny) LastHitValid = false;

            // [2026-09-15 Phase D] 카메라 펀치 이중 발화 정리(48차 비고: 성공 타격 시 AttackTarget + TryAttack
            // 2회 호출로 실효 2배) — 적중 시에는 AttackTarget 내부가 발화하므로 여기서는 '미스 스윙'만 담당한다.
            // → 히트/미스 카메라 강도 일치, 연타 스트릭 펀치(0.4/0.55/0.7) 정상화.
            if (!hitAny) TriggerCameraEffects();

            // 공격 전진 (attack lunge)
            StartCoroutine(AttackLungeCoroutine());
            return true;
        }

        private static HumanoidClipDriver ResolveMeleeComboDriver(
            HumanoidClipDriver cachedDriver, HumanoidClipDriver discoveredDriver)
        {
            return cachedDriver != null ? cachedDriver : discoveredDriver;
        }

        private static bool ShouldUseMeleeCombo(bool fistOrSword, bool driverReady)
        {
            return fistOrSword && driverReady;
        }

        private static bool ShouldUseSpearAttackDriver(bool spear, bool driverReady)
        {
            return spear && driverReady;
        }

        private static bool ShouldRejectMeleeAttack(bool comboActive, bool comboFollowup,
            bool expiredComboRestart, bool canAttack)
        {
            return (comboActive && !comboFollowup && !expiredComboRestart)
                || (!canAttack && !comboFollowup && !expiredComboRestart);
        }

        private static bool ShouldApplyAttackMovementEffects(bool comboOwnedImpact)
        {
            return !comboOwnedImpact;
        }

        /// <summary>Resolve one hit attempt and its damage/effects for a Weapon_Combo_2 swing segment.</summary>
        public void ResolveComboSwingHit(int comboStage)
        {
            if (_currentWeapon == null) return;
            _attackStreak = Mathf.Clamp(comboStage, 1, AttackStreakMax);
            PlayWeaponSwingSound();

            bool hitAny = false;
            IDamageable autoAimTarget = FindTargetInCursorDirection();
            if (autoAimTarget != null)
            {
                _currentTarget = autoAimTarget;
                StartFaceTarget(autoAimTarget);
                AttackTarget(_currentTarget, true);
                hitAny = true;
            }
            else if (AttackCenterScreen(true))
            {
                hitAny = true;
            }
            else
            {
                IDamageable sweep = MeleeSweepFallback();
                if (sweep != null)
                {
                    _currentTarget = sweep;
                    StartFaceTarget(sweep);
                    AttackTarget(sweep, true);
                    hitAny = true;
                }
            }

            if (!hitAny)
            {
                LastHitValid = false;
                TriggerCameraEffects();
                LogMeleeMissDiagnosis();   // [무번호 44-AIM-DIAG] 콤보 경로 미스 진단 — 기존 무음 사각지대 해소
            }
            if (ShouldApplyAttackMovementEffects(true))
                StartCoroutine(AttackLungeCoroutine());
        }

        /// <summary>
        /// P6 (2026-09-11): 활(Bow) 좌클릭 발사 — 마우스 커서 방향으로 화살 1발을 소모·발사한다.
        /// 데미지는 발사체(ArrowProjectile)가 담당하므로 근접 AttackTarget/스윕 폴백을 호출하지 않는다.
        /// 화살 부족 시 미스 처리(LastHitValid=false) 후 종료(근접 공격으로 폴백하지 않음 — 활은 근접 무기가 아님).
        /// 카메라 이펙트/런지는 발사 성공 시에만 적용.
        /// </summary>
        private void TryBowShot(float power, bool currentDrawRelease = false)
        {
            if (!currentDrawRelease)
            {
                CancelBowShotWithoutFreshDraw("unauthorized shot dispatch", power);
                return;
            }

            // ① 발사 사운드 — 무기별 레이어링
            PlayWeaponSwingSound();

            // Lazy manager setup remains unchanged; it also owns an optional configured muzzle transform.
            if (ArrowManager.Instance == null)
            {
                // Test_10 EnsureGameManager에서 생성 보장이 우선이지만, 다른 씬/순서 대비 런타임 자가 생성.
                if (Application.isPlaying) { var go = new GameObject("ArrowManager"); go.AddComponent<ArrowManager>(); }
            }

            // Consume the pixel synchronously frozen by the input release frame. The UI's
            // scheduled display sample can be up to 16ms old and is never used for solving.
            if (!BowAimState.TryGetReleaseAimScreenPoint(out Vector2 aimScreenPoint))
            {
                LastHitValid = false;
                BowAimState.Release(false, power);
                Debug.LogWarning("[PlayerCombat] 활 조준 취소 — 표시된 UTK 리티클 샘플이 없습니다.");
                return;
            }

            Vector3 fallbackOrigin = transform.position + transform.forward * 0.6f + Vector3.up * 1.4f;
            Vector3 origin = ArrowManager.Instance != null
                ? ArrowManager.Instance.GetArrowSpawnOrigin(fallbackOrigin)
                : fallbackOrigin;
            Camera aimCamera = ResolveBowAimCamera();
            if (aimCamera == null)
            {
                LastHitValid = false;
                BowAimState.Release(false, power);
                Debug.LogWarning("[PlayerCombat] 활 조준 취소 — 활성 MainCamera를 찾을 수 없습니다.");
                return;
            }

            if (!ArrowManager.TrySolveAim(aimCamera, aimScreenPoint, origin, transform,
                    out Vector3 aimPoint, out Vector3 dir))
            {
                LastHitValid = false;
                BowAimState.Release(false, power);
                Debug.LogWarning("[PlayerCombat] 활 조준 취소 — 조준점을 계산하지 못했습니다.");
                return;
            }

            // Turn the actor toward the resolved point horizontally; keep the full muzzle direction for launch.
            Vector3 launchForward = dir;
            launchForward.y = 0f;
            if (launchForward.sqrMagnitude <= 0.0001f)
            {
                launchForward = transform.forward;
                launchForward.y = 0f;
            }
            if (launchForward.sqrMagnitude <= 0.0001f) launchForward = Vector3.forward;

            // Existing base damage and draw power flow through unchanged to ArrowManager.
            // Capture the projectile itself so the diagnostic checks its actual initial velocity.
            ArrowProjectile projectile = null;
            bool fired = ArrowManager.Instance != null
                && ArrowManager.Instance.TryShootArrow(origin, dir, WeaponData.Bow.damage, power,
                    out projectile);
            if (!fired)
            {
                // 화살 부족 — 발사 실패. TryShootArrow 내부에서 차단 메시지 표시됨.
                LastHitValid = false;
                BowAimState.Release(false, power);
                Debug.Log("[PlayerCombat] 🏹 활 발사 실패 — 화살 부족 or ArrowManager 미생성");
                return;
            }
            transform.rotation = Quaternion.LookRotation(launchForward.normalized, Vector3.up);
            Debug.LogWarning($"[BowAim] fired projectile={projectile.name} muzzle={origin} aim={aimPoint} "
                + $"dir={dir} velocity={projectile.GetComponent<Rigidbody>().linearVelocity} camId={aimCamera.GetInstanceID()}"
                + $" camPos={aimCamera.transform.position}");   // [207차 P1] 카메라 원점 좌표 — 재발 시 즉시 판별용
            // [70차 후속19/C1·C6] 발사 성공 — 소형 카메라 킥(활 전용) + 파워 기억(명중 시 크리틱 연출용)
            CombatCameraEffects.PlayFireKick();
            LastBowPower = power;
            // [P25-C2] 발사 머즐 퍼프 — 활 위치에서 작은 먼지 퍼프(발사 연출).
            // [테스트45 P2] 발사 머즐퍼프(이상한 파티클) 제거 — 화살 트레일만 남긴다.
            // ArrowProjectile.SpawnMuzzlePuff(origin);
            // [TEST25-66차] 발사 성공 실측 로그 — 화살 비행(ArrowProjectile) + ArcheryShot(발사 애니) 동시 고정.
            Debug.Log("[PlayerCombat] 🏹 활 발사 성공 — 화살 비행(ArrowProjectile) + ArcheryShot(활 사격 애니)");

            // ③ 발사 애니 — ArcheryShot 명시 트리거. AcceptedAttackSequence는 TryAttack 시작부에서 이미 갱신되어
            //    드라이버 감시가 Bow 분기에서 ArcheryShot을 자동 트리거하며, TriggerBowShot()은
            //    동일 프레임 중복 SetTrigger(무해)이자 드라이버 미연결 시나리오의 안전망이다.
            //    ⚠️ _rigAnim.Attack()은 Fist 근접 클립을 재생시킬 수 있어 Bow 경로에서는 호출하지 않는다.
            _clipDriver?.TriggerBowShot();

            // ④ 발사 성공 시에만 연출 — 카메라 반동 + 발사 전진(런지)
            TriggerCameraEffects();
            StartCoroutine(AttackLungeCoroutine());
        }

        private void CancelBowShotWithoutFreshDraw(string route, float power)
        {
            LastHitValid = false;
            BowAimState.Release(false, power);
            Debug.LogWarning($"[PlayerCombat] 활 발사 취소 — 현재 드로/릴리즈에 묶인 조준 샘플 없음 (route={route})");
        }

        /// <summary>
        /// C4-08: 마우스 커서 방향으로 가장 가까운 적을 찾습니다.
        /// 카메라 → 마우스 커서 위치로 Ray를 쏘아 IDamageable 구현체를 탐색합니다.
        /// </summary>
        /// <summary>[2026-09-16 69차 후속10] 아군 오인 피해 차단 — 내 소속(포섭) 병사(RecruitedSoldier 태그)는
        ///   플레이어 공격의 대상이 되지 않는다(타겟 선정 제외 + 데미지 무시). 적 병사(Guard)는 정상 피해.</summary>
        private static bool IsOwnSoldier(IDamageable target)
        {
            var mb = target as MonoBehaviour;
            return mb != null && mb.CompareTag("RecruitedSoldier");
        }

        /// <summary>
        /// [Phase D-2] 커서가 NPC/영주/상점NPC 계열을 가리키는지 판정. 공격 대상(IDamageable)이 아닌
        ///  캐릭터(NPC)를 좌클릭할 때 빈 스윙 공격을 막기 위한 가드. HoverTargetClassifier는 같은
        ///  Systems 어셈블리의 순수 분류기라 순환참조 없이 직접 호출 가능.
        /// </summary>
        private static bool IsPointerOverNpcInteraction()
        {
            if (Mouse.current == null) return false;
            Vector2 mouse = Mouse.current.position.ReadValue();
            HoverTargetClassifier.TargetKind kind = HoverTargetClassifier.ClassifyAt(mouse);
            if (kind == HoverTargetClassifier.TargetKind.Lord)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    RaycastHit[] hits = Physics.RaycastAll(cam.ScreenPointToRay(mouse), 200f, ~0, QueryTriggerInteraction.Collide);
                    System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                    foreach (var hit in hits)
                    {
                        if (hit.collider == null) continue;
                        var encounter = hit.collider.GetComponentInParent<InvadedLordEncounter>();
                        if (encounter != null) return !encounter.IsInCombat;
                    }
                }
            }
            return kind == HoverTargetClassifier.TargetKind.NPC
                || kind == HoverTargetClassifier.TargetKind.ShopNPC
                || kind == HoverTargetClassifier.TargetKind.Lord
                || kind == HoverTargetClassifier.TargetKind.LordDaughter;   // [126차] 영주 딸 — 공격 스윙 억제(영주/마을NPC 계열)
        }

        private IDamageable FindTargetInCursorDirection()
        {
            if (_mainCamera == null || Mouse.current == null) return null;

            // 마우스 화면 좌표 → 월드 Ray
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = _mainCamera.ScreenPointToRay(mousePos);

            // RaycastAll로 모든 충돌체 검색
            RaycastHit[] hits = Physics.RaycastAll(ray, _autoAimRange, _targetLayers);
            IDamageable closestTarget = null;
            float closestDistance = Mathf.Infinity;

            foreach (RaycastHit hit in hits)
            {
                IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
                if (target != null && target.IsAlive && !IsOwnSoldier(target))
                {
                    if (hit.distance < closestDistance)
                    {
                        closestDistance = hit.distance;
                        closestTarget = target;
                    }
                }
            }

            // Direct Raycast hit이 없으면 → 원뿔(SphereCast)로 재탐색
            if (closestTarget == null)
            {
                // 카메라 → 마우스 커서 방향, 넓은 범위 SphereCast
                float coneRadius = Mathf.Tan(_autoAimAngle * Mathf.Deg2Rad) * _autoAimRange;
                RaycastHit[] sphereHits = Physics.SphereCastAll(ray.origin, coneRadius, ray.direction, _autoAimRange, _targetLayers);

                foreach (RaycastHit hit in sphereHits)
                {
                    IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
                    if (target != null && target.IsAlive && !IsOwnSoldier(target))
                    {
                        // 원뿔 각도 내에 있는지 추가 확인 (실제 각도 계산)
                        Vector3 directionToTarget = (hit.point - ray.origin).normalized;
                        float angle = Vector3.Angle(ray.direction, directionToTarget);
                        if (angle <= _autoAimAngle && hit.distance < closestDistance)
                        {
                            closestDistance = hit.distance;
                            closestTarget = target;
                        }
                    }
                }
            }

            return closestTarget;
        }

        /// <summary>
        /// C4-08: 특정 타겟을 공격합니다.
        /// </summary>
        private void AttackTarget(IDamageable target, bool comboOwnedImpact = false)
        {
            if (target == null || !target.IsAlive) return;
            if (IsOwnSoldier(target)) return;   // [69차 후속10] 아군 오인 피해 차단 — 내 소속 병사는 플레이어 공격으로 피해를 입지 않는다(합세 통보도 없음)

            // [95차 F1] PERFECT 반격 1타 배율 — 소비형(기본 1). 큐 해방 1타에만 ×1.5 적용 후 즉시 리셋.
            float counterMultiplier = _pendingCounterAttackMultiplier;
            if (counterMultiplier != 1f) _pendingCounterAttackMultiplier = 1f;

            float damage = CalculateDamage() * counterMultiplier;
            Vector3 hitDirection = Vector3.zero;

            // 타겟 방향 계산
            MonoBehaviour targetBehaviour = target as MonoBehaviour;
            if (targetBehaviour != null)
            {
                hitDirection = (targetBehaviour.transform.position - transform.position).normalized;
            }

            // 📍 2026-09-11: 마지막 적중 지점 저장 — 십자가 VFX(FireComboCross)가 플레이어 전방 고정점이
            // 아니라 실제로 공격을 맞은 대상 지점에 발화되도록 공유. 데미지 로직 자체는 변경 없음.
            // 저장 규격: 대상 Collider/Renderer bounds 중심 + up*0.1 (bounds 없으면 대상 position + up*1.2).
            // [2026-09-13] 오프셋 0.2→0.1 하향 — 히트 지점 VFX/FX가 대상에서 살짝 뜨는 체감 개선(부착감).
            if (targetBehaviour != null)
            {
                Collider hitCol = targetBehaviour.GetComponentInChildren<Collider>();
                Renderer hitRen = targetBehaviour.GetComponentInChildren<Renderer>();
                Vector3 center = hitCol != null ? hitCol.bounds.center
                               : hitRen != null ? hitRen.bounds.center
                               : targetBehaviour.transform.position + Vector3.up * 1.2f;
                LastHitPoint = center + Vector3.up * 0.1f;
                LastHitValid = true;
                LastHitTime = Time.time;
            }

            // [45차 P4] 히트 순간 스윙 트레일 밝기 펄스 — 적중 확정 지점 1줄 훅(null 가드는 Pulse 내부).
            WeaponSwingTrail.Pulse();

            target.TakeDamage(damage, hitDirection, _currentWeapon?.weaponType.ToString() ?? "melee");

            // [166-D] 몬스터 피격 효과음 — 데미지 적용 공통 종착 1줄 훅(히트스톱 이전 발화 — I-3 임팩트 선행 규약 정합).
            MonsterHitSfx.PlayMonsterHit(_currentWeapon != null ? _currentWeapon.weaponType : ProjectName.Core.WeaponType.Fist);

            // [무번호 37-H1] 강타격 프리즈 — 동일 밴드(피해≥15). 반격 배율 타 포함(무번호 36-PS2와 독립 계층).
            if (damage >= 15f && Application.isPlaying)
                HitStopManager.RequestHitStop(0.05f, 0.06f);

            // [무번호 36-PS2] 반격 식별 링 — 큐 해방 1타(반격 배율 적용 타)에 한해 타겟 지점 흰 링(일반 타격과 구분).
            //   크리급 라이트/실루엣/블룸은 타격 체인 공유(isCrit 아님 — 식별 링이 반격 증거).
            if (counterMultiplier != 1f && LastHitValid && Application.isPlaying)
                ShockwaveRingFX.Spawn(LastHitPoint, 0.8f, new Color(FXPalette.ParryWhite.r, FXPalette.ParryWhite.g, FXPalette.ParryWhite.b, 0.7f), 0.3f);

            // [56차 후속] 동행 병사 합세 — 포섭(recruited) 병사들이 플레이어가 공격한 대상을 함께 공격.
            if (targetBehaviour != null)
            {
                GuardCombatAI.NotifyPlayerAttack(targetBehaviour.gameObject);

                // [57차 후속] 타 영지(비-포섭) 병사 — 플레이어 공격 목격 시 호감도 하락 → 적대화.
                // 몬스터처럼 느낌표가 잠깐 떴다 사라지며 플레이어/내 병사 공격(GuardHostilitySystem 내부 처리).
                if (GuardHostilitySystem.Instance != null)
                    GuardHostilitySystem.Instance.NotifyPlayerAttack(targetBehaviour.gameObject, gameObject);
            }

            // ⏱️ 전투 로그: 데미지 기록
            string targetName = targetBehaviour != null ? targetBehaviour.gameObject.name : "Unknown";
            CombatLog.AddEntry($"{targetName}에게 {damage} 데미지", LogType.Damage);

            // 🔊 컨트롤러 진동: 기본 공격 hit (Light)
            HapticFeedback.PlayPreset(HapticFeedback.RumblePreset.Light);

            // G2-04: 치명타/백어택 감지 → Shake 2배 + HitStop
            bool isBackAttack = false;
            if (targetBehaviour != null)
            {
                Vector3 dirToAttacker = (transform.position - targetBehaviour.transform.position).normalized;
                float dot = Vector3.Dot(targetBehaviour.transform.forward, dirToAttacker);
                if (dot > 0.5f) // 뒤에서 공격 (back attack)
                {
                    isBackAttack = true;
                    CombatCameraEffects.PlayCrit();
                    Debug.Log("[PlayerCombat] ★ 백어택! 치명타 카메라 이펙트");
                }
                else
                {
                    // 2026-09-15 Phase A: 무기별 카메라 임펄스 프로파일 적용
                    CombatCameraEffects.PlayHit(_currentWeapon != null ? _currentWeapon.weaponType : ProjectName.Core.WeaponType.Fist);
                }
            }

            // 공격자 리코일(2026-09-14): 타격 성공 직후 타격 방향 반대(-hitDirection)로 밀려나는 짧은 반동.
            // 백어택/치명타면 반동 증폭(0.15m → 0.25m). HitStopManager는 기존 규약대로 마지막에 호출(호출 순서 변경 없음).
            // #48차 후속 FIX(2026-09-14): 리코일 시작 전 플래그 ON — 러닝 중 런지가 이 플래그 해제를 대기(순차 인계).
            if (ShouldApplyAttackMovementEffects(comboOwnedImpact))
            {
                _recoilActive = true;
                StartCoroutine(RecoilCoroutine(-hitDirection, isBackAttack ? RecoilDistanceCrit : RecoilDistanceNormal));
            }

            // Phase B: 무기별 적중 사운드 레이어링
            PlayWeaponHitSound(isBackAttack);

            // Phase 2 (COMBAT_VFX_UPGRADE_PLAN): 중앙 VFX 게이트로 히트 FX 통합.
            // CombatFXGate.PlayHitFX(GameObject 오버로드)가 스파크, 유기체 출혈(Organic),
            // 데미지 넘버, 히트 플래시, 카메라 Crit/Hit를 일괄 처리하므로
            // 기존의 중복 HitVFX.PlayHitEffect/PlayHitFlash 블록은 제거함 (게이트가 대체).
            // 백어택 = 치명타(isCrit) → 데미지 넘버 색: Accent(골드), 일반 → Core(흰).
            // 2026-09-13(45차 P2): BOTW 팔레트 통일 — 흰/골드/주황 3색.
            if (targetBehaviour != null)
            {
                Color numberColor = isBackAttack ? FXPalette.Accent : FXPalette.Core;
                // 46차 후속: 임팩트를 실제 타격 지점(bounds center+0.1)에 부착 — 기존 GameObject 오버로드는
                // target.transform.position(모델 피벗, Editor.log 실측 y≈2.9 공중 부양)에 발화해 타격 지점과 어긋났다.
                // hitPos = LastHitPoint(375행에서 직전에 갱신: bounds center+up*0.1). Time.time 기반 신선도
                // 확인(LastHitValid/LastHitTime, 0.5s — FireComboCross와 동일 규약)이 있으므로 그대로 사용하고,
                // 만료 시엔 기존과 같이 대상 피벗으로 폴백한다.
                bool hitPointFresh = LastHitValid && Time.time - LastHitTime <= 0.5f;
                Vector3 impactPos = hitPointFresh ? LastHitPoint : targetBehaviour.transform.position;
                CombatFXGate.PlayHitFX(targetBehaviour.gameObject, impactPos, hitDirection, CombatHitType.Organic, isBackAttack, damage, numberColor);
                Debug.Log($"[PlayerCombat] ✨ FX 게이트 호출: {targetName} (crit={isBackAttack}, dmg={damage}, impactPos={impactPos})");
            }

            Debug.Log($"[PlayerCombat] 🎯 자동조준! {_currentWeapon?.weaponName ?? "Unknown"} → {target} 데미지: {damage}");

            // 카메라 효과
            TriggerCameraEffects();

            // 🎬 P6 액션감 1차: 히트스톱 + FOV 펀치 훅 (적중 성공 판정 지점, 1줄).
            // 배치 주의: CombatCameraEffects.PlayHit/PlayCrit/PlayKill이 효과 시작 시점의
            // Time.timeScale을 '_baseTimeScale'(자기 복귀 기준)로 저장하므로, 이 호출들
            // "이전"에 히트스톱(0.08)을 걸면 CCE가 0.08을 원본으로 저장해 복귀 lerp 후
            // 영구 저속 정지 사고가 난다. → CCE 호출 이후 마지막에 배치.
            // 데미지 판정/HP/콤보 로직은 변경하지 않음 (FX 전용 훅).
            // 2026-09-15: 무기별 차등 지속시간 — 검 50ms / 창 60ms / 활 30ms / 맨손 40ms
            // (HitStopManager.DurationOf 테이블). 무기 미장착 시 맨손(Fist) 폴백.
            // bare WeaponType 대신 정규화명 사용 — Neural 네임스페이스의 동명 enum과의 모호성 회피(218/603행 선례).
            HitStopManager.RequestHitStop(
                _currentWeapon != null ? _currentWeapon.weaponType : ProjectName.Core.WeaponType.Fist,
                0.08f);
        }

        /// <summary>
        /// 자동 조준 실패 시 화면 중앙 방향으로 SphereCast 공격
        /// </summary>
        private bool AttackCenterScreen(bool comboOwnedImpact = false)
        {
            if (_mainCamera == null)
            {
                Debug.LogWarning("[PlayerCombat] ⚠️ 메인 카메라 없음 — 조준 불가(Camera.main 태그 확인)");
                return false;
            }

            Vector3 screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            Ray ray = _mainCamera.ScreenPointToRay(screenCenter);

            if (Physics.SphereCast(ray, _attackRadius, out RaycastHit hit, _maxRange, _targetLayers))
            {
                IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
                if (target != null && target.IsAlive)
                {
                    AttackTarget(target, comboOwnedImpact);
                    return true;
                }
            }

            // 미스 원인 진단 (1줄) — 다음 Play에서 즉시 판별용
            Vector2 mousePos = Mouse.current != null
                ? Mouse.current.position.ReadValue()
                : (Vector2)new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Ray cursorRay = _mainCamera.ScreenPointToRay(mousePos);
            RaycastHit[] cursorHits = Physics.RaycastAll(cursorRay, _autoAimRange, _targetLayers);
            string nearest = "없음";
            float nearestDist = float.MaxValue;
            foreach (var ch in cursorHits)
            {
                if (ch.distance < nearestDist)
                {
                    nearestDist = ch.distance;
                    nearest = ch.collider != null ? ch.collider.name : "?";
                }
            }
            Debug.Log($"[PlayerCombat] ⚔️ 공격 실패 — cursorRay히트={cursorHits.Length}건(최근접: {nearest} {(cursorHits.Length > 0 ? nearestDist.ToString("F1") + "m" : "")}), 사거리 autoAim={_autoAimRange}m/sphereMax={_maxRange}m → 근접 스윕 폴백 시도");
            return false;
        }

        /// <summary>
        /// 근접 스윕 폴백 — 커서 레이캐스트/화면중앙 스피어캐스트 실패 시 안전망.
        /// 플레이어 위치에서 무기 사거리(최소 2.5m) 내 OverlapSphere로 가장 가까운
        /// 살아있는 IDamageable을 찾는다. 자기 자신(PlayerHealth)은 제외.
        /// </summary>
        private IDamageable MeleeSweepFallback()
        {
            float sweepRange = Mathf.Max(_currentWeapon != null ? _currentWeapon.range : 2f, 2.5f);
            Collider[] overlaps = Physics.OverlapSphere(transform.position, sweepRange, _targetLayers);

            IDamageable closest = null;
            float closestDist = Mathf.Infinity;
            foreach (var col in overlaps)
            {
                IDamageable dmg = col.GetComponentInParent<IDamageable>();
                if (dmg == null || !dmg.IsAlive) continue;

                MonoBehaviour dmgBehaviour = dmg as MonoBehaviour;
                if (dmgBehaviour == null) continue;
                // 자기 자신(플레이어) 제외 — PlayerHealth가 같은 오브젝트에 있으면 스킵
                if (dmgBehaviour.GetComponent<PlayerHealth>() != null && dmgBehaviour.transform == transform) continue;

                float dist = Vector3.Distance(transform.position, dmgBehaviour.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = dmg;
                }
            }
            return closest;
        }

        /// <summary>
        /// [무번호 44-AIM-DIAG] 콤보/드라이버 경로 미스 1급 진단 — 11-15 21:00 세션에서 드라이버 수락 공격 2회가
        /// 3중 조준(커서 레이/화면중앙/스윕 폴백) 전부 실패로 무피해 종료됐는데 로그가 전무한 사각지대를 해소한다.
        /// 몬스터 무피해→사망 없음→전리품 부재 보고의 직접 관측 장치(수리 없음 — 근원 확정 후 별도 수리).
        /// 레거시 TryAttack 경로는 AttackCenterScreen 내장 미스 로그가 이미 있으므로 본 메서드는 콤보 경로 전용.
        /// </summary>
        private void LogMeleeMissDiagnosis()
        {
            if (!Application.isPlaying || Time.unscaledTime < _nextMissDiagLog) return;
            _nextMissDiagLog = Time.unscaledTime + 3f;

            var sb = new System.Text.StringBuilder("[44-AIM-DIAG] 근접 미스 — ");
            sb.Append($"playerPos=({transform.position.x:F1},{transform.position.y:F1},{transform.position.z:F1}) ");
            sb.Append($"weapon={_currentWeapon?.weaponType.ToString() ?? "none"} ");

            if (_mainCamera != null && Mouse.current != null)
            {
                Ray ray = _mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
                RaycastHit[] cursorHits = Physics.RaycastAll(ray, _autoAimRange, _targetLayers);
                string nearest = "없음";
                float nearestDist = float.MaxValue;
                foreach (var ch in cursorHits)
                {
                    if (ch.distance < nearestDist)
                    {
                        nearestDist = ch.distance;
                        nearest = ch.collider != null ? $"{ch.collider.name}(layer={LayerMask.LayerToName(ch.collider.gameObject.layer)})" : "?";
                    }
                }
                sb.Append($"cursorRay히트={cursorHits.Length}건 최근접={nearest} ");
            }
            else
            {
                sb.Append($"커서레이 불가 camera={(_mainCamera != null)} mouse={(Mouse.current != null)} ");
            }

            float sweepRange = Mathf.Max(_currentWeapon != null ? _currentWeapon.range : 2f, 5f);
            Collider[] overlaps = Physics.OverlapSphere(transform.position, sweepRange, _targetLayers);
            sb.Append($"주변{sweepRange:F0}m 콜라이더 {overlaps.Length}건:");
            int listed = 0;
            foreach (var col in overlaps)
            {
                if (col == null || listed >= 5) break;
                IDamageable dmg = col.GetComponentInParent<IDamageable>();
                MonoBehaviour dmgMb = dmg as MonoBehaviour;
                // C# 9 보간 문자열 제약 — 구멍 내부 따옴표 금지 → 라벨 사전 계산(컴파일 계약).
                string dmgLabel = dmg == null ? "none" : (dmg.IsAlive ? "alive" : "dead");
                string layerName = LayerMask.LayerToName(col.gameObject.layer);
                float dist = Vector3.Distance(transform.position, dmgMb != null ? dmgMb.transform.position : col.transform.position);
                sb.Append($" [{col.name}|L{layerName}|dmg:{dmgLabel}|{dist:F1}m]");
                listed++;
            }
            Debug.LogWarning(sb.ToString());
        }

        /// <summary>
        /// 공격 시 카메라 이펙트 — Cinemachine Impulse Source로 반동 처리.
        /// CombatCameraEffects.PlayCrit()가 추가 Shake/HitStop을 처리합니다.
        /// 2026-09-14: 연타 카운터(1~3타)에 따라 펀치 강도 차등(0.4 / 0.55 / 0.7).
        /// </summary>
        private void TriggerCameraEffects()
        {
            if (_impulseSource != null)
            {
                // 연타 스트릭별 펀치 강도(2026-09-14) — HumanoidClipDriver 콤보 스테이지와 독립적으로
                // 이 파일 자체의 _attackStreak만 사용한다(드라이버 참조 없음).
                float impulse;
                switch (_attackStreak)
                {
                    case 1: impulse = 0.4f; break;  // 1타 — 가벼운 펀치
                    case 2: impulse = 0.55f; break; // 2타 — 중간 강도
                    default: impulse = 0.7f; break; // 3타(최대) — 가장 강한 펀치
                }
                _impulseSource.GenerateImpulse(Vector3.forward * impulse);
            }
        }

        /// <summary>
        /// 공격 전진 (attack lunge, 2026-09-14 개선) — 스윙 임팩트 타이밍에 맞춰 3프레임(≈0.05s) 지연 후
        /// 무기 타입별 고정 거리만큼 전진. 전반 40% 가속 → 후반 감속 곡선으로 시작은 느리게 벌떡 오르고 마지막에 꽂히는 무게감.
        /// 방향: 최근(0.5s 내) 유효 적중이면 타겟(LastHitPoint) 방향, 아니면 현재 전방 폴백(y 제거 후 정규화).
        /// </summary>
        private System.Collections.IEnumerator AttackLungeCoroutine()
        {
            // 임팩트 동기화: 스윙 애니의 타격 프레임과 겹치도록 이동 시작을 3프레임(≈0.05s @60fps) 지연(anticipation).
            yield return null;
            yield return null;
            yield return null;

            const float duration = 0.15f; // 이동 지속 시간(기존 유지) — 짧고 강한 전진
            // 무기 타입별 런지 거리(고정 상수 맵 — WeaponData.range 직접 사용 금지. 스윙 아크 최적화상 너무 크면 어색함):
            // 2026-09-15 무기별 전진 거리 차등 — 검 1.5m / 창 2.5m / 활 0.3m / 맨손 1.0m
            float distance;
            switch (_currentWeapon != null ? _currentWeapon.weaponType : ProjectName.Core.WeaponType.Fist)
            {
                case ProjectName.Core.WeaponType.Sword: distance = 1.5f; break; // 검 — 1.5m 전진
                case ProjectName.Core.WeaponType.Spear: distance = 2.5f; break; // 창 — 찌르기 특성상 가장 긴 2.5m 전진
                case ProjectName.Core.WeaponType.Bow:   distance = 0.3f; break; // 활 — 발사 반동 수준의 최소 전진 0.3m
                default:                                distance = 1.0f; break; // 맨손 — 잽형 1.0m 전진
            }

            // 방향 결정: 최근(0.5s 내) 유효 적중이면 타겟(LastHitPoint) 방향, 아니면 현재 전방 폴백.
            Vector3 dir;
            if (LastHitValid && Time.time - LastHitTime <= 0.5f)
                dir = LastHitPoint - transform.position; // 타겟 방향 런지
            else
                dir = transform.forward;                 // 폴백 — 현재 전방
            dir.y = 0f; // xz 평면 이동만 — 수직 출렁임 방지
            if (dir.sqrMagnitude < 0.0001f)
            {
                dir = transform.forward; // 극단 케이스(동일 위치 등) — 현재 전방 재폴백
                dir.y = 0f;
            }
            if (dir.sqrMagnitude < 0.0001f) yield break; // 그래도 무효하면 런지 스킵
            dir.Normalize();

            // 지연 "후" 위치 캡처 — 리코일(후방 반동)이 끝난 지점에서 런지가 이어져 스냅 없이 자연 연결
            // #48차 후속 FIX(2026-09-14): 리코일 완료 대기 게이트 — 리코일 생존 중(_recoilActive) 매 프레임 양보 후 캡처.
            // 히트스톱(timeScale 0.08)/고프레임 환경에서도 리코일→런지 순차 인계 보장. 기존 3프레임 지연은 유지.
            // 플래그가 이미 false(미스 경로 등 리코일 미시작)면 즉시 통과 — 기존 미스 경로 속도 불변.
            while (_recoilActive) yield return null;
            Vector3 startPos = transform.position;
            Vector3 endPos = startPos + dir * distance;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = elapsed / duration;
                // 무게감 곡선: 전반 40%는 ease-in(느린 출발→가속), 이후 ease-out(최고 속도→감속하며 꽂힘).
                // 40% 경계에서 속도 연속(양쪽 모두 평균 속도의 2배) — 중간에 쉬지 않는 자연스러운 가속→감속.
                float ease;
                if (t < 0.4f)
                {
                    float local = t / 0.4f;
                    ease = 0.4f * local * local; // ease-in — 정지 상태에서 가속 ("벌떡" 참진 시작)
                }
                else
                {
                    float local = (t - 0.4f) / 0.6f;
                    ease = 0.4f + 0.6f * (1f - (1f - local) * (1f - local)); // ease-out — 감속 후 정확히 도달
                }
                transform.position = Vector3.Lerp(startPos, endPos, ease);
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>
        /// 페이스 타깃(2026-09-14) — 공격 진입 순간에만 타겟 xz 방향으로 빠르게 회전(Slerp).
        /// 타겟 없으면 아예 호출하지 않아 현재 방향 유지(기존 동작 변경 없음).
        /// </summary>
        private void StartFaceTarget(IDamageable target)
        {
            MonoBehaviour targetBehaviour = target as MonoBehaviour;
            if (targetBehaviour == null) return;
            StartCoroutine(FaceTargetRoutine(targetBehaviour.transform.position));
        }

        private System.Collections.IEnumerator FaceTargetRoutine(Vector3 targetPos)
        {
            Vector3 dir = targetPos - transform.position;
            dir.y = 0f; // xz 평면만 회전 — 상하 기울임 없음
            if (dir.sqrMagnitude < 0.0001f) yield break;
            Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            for (int frame = 0; frame < FaceTargetMaxFrames; frame++) // 최대 10프레임 — 공격 순간에만 회전
            {
                // Slerp 보간(속도 15 × deltaTime): 180도 반전 같은 큰 각도도 부드럽게 회전
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, FaceTargetSpeed * Time.deltaTime);
                if (Quaternion.Angle(transform.rotation, targetRot) < 1f) yield break; // 정렬 완료 시 조기 종료
                yield return null;
            }
        }

        /// <summary>
        /// 공격자 리코일(2026-09-14) — 타격 성공 직후 타격 방향 반대로 dist만큼 밀려나는 짧은 반동.
        /// smoothstep(t²(3−2t)) 곡선: 가속→감속으로 출발/정지 모두 부드럽다. 기존 런지와 동일하게 transform.position 직접 이동.
        /// </summary>
        private System.Collections.IEnumerator RecoilCoroutine(Vector3 dir, float dist)
        {
            const float duration = 0.05f; // 리코일 지속 — 히트 임팩트에 맞춘 0.05초 반동
            dir.y = 0f; // 수평 반동만
            // #48차 후속 FIX(2026-09-14): 조기 종료 경로에서도 플래그 해제 — 미해제 시 런지 대기 무한 블로킹 방지.
            if (dir.sqrMagnitude < 0.0001f) { _recoilActive = false; yield break; } // 방향 불명(타겟 transform 없음) 시 스킵
            dir.Normalize();
            Vector3 startPos = transform.position;
            Vector3 endPos = startPos + dir * dist;
            float elapsed = 0f;
            // [2026-09-14(51차 방어)] try/finally — 루프 중 예외/StopCoroutine/컴포넌트 비활성화로 코루틴이 강제 종료돼도
            // 반드시 플래그를 해제한다(Unity가 코루틴 중단 시 iterator를 Dispose → finally 실행). C# iterator 제약상
            // finally 블록 안에는 yield를 둘 수 없으므로, yield는 전부 try 블록 내부에 유지.
            try
            {
                while (elapsed < duration)
                {
                    float t = Mathf.Clamp01(elapsed / duration);
                    float ease = t * t * (3f - 2f * t); // smoothstep — 가속 후 감속
                    transform.position = Vector3.Lerp(startPos, endPos, ease);
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }
            finally
            {
                // #48차 후속 FIX(2026-09-14): 리코일 루프 완료 직후 플래그 OFF — 대기 중이던 런지가 여기서 인계받음.
                // 51차: finally로 이동 — 정상 종료뿐 아니라 예외/강제 중단 시에도 해제 보장(런지 영구 스킵 방지).
                _recoilActive = false;
            }
        }

        /// <summary>
        /// [Phase I] 무기별 스윙 사운드 재생 — 스윙 레이어(공격 시작 시점)만 담당.
        /// AttackSoundLayerManager가 무기 타입별 피치/볼륨을 적용해 재생한다.
        /// 토큰 재활용: attack_swing_{weapon} 우선, attack_swing 기본 폴백 (기존 AudioConfig 경로 유지).
        /// </summary>
        private void PlayWeaponSwingSound()
        {
            var w = _currentWeapon != null ? _currentWeapon.weaponType : ProjectName.Core.WeaponType.Fist;
            AttackSoundLayerManager.PlaySwing(w);
        }

        /// <summary>
        /// [Phase I] 무기별 적중 사운드 재생 — 타격 시점에 임팩트+서브베이스+보이스 3중 레이어를
        /// 즉시 동시 발화. 히트스톱(RequestHitStop)은 호출부에서 이 메서드 이후 마지막에 걸리므로
        /// I-3 규약(히트스톱 지연과 무관하게 임팩트 선행)이 충족된다.
        /// </summary>
        private void PlayWeaponHitSound(bool isBackAttack)
        {
            var w = _currentWeapon != null ? _currentWeapon.weaponType : ProjectName.Core.WeaponType.Fist;
            AttackSoundLayerManager.PlayAttackHit(w, isBackAttack);
        }
    }
}
