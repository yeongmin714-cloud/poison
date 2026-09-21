// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 414,618
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectName.Core;
using ProjectName.Systems.Animation.Procedural;
using ProjectName.Systems.Animation.Neural;
using Unity.Cinemachine;

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
        [SerializeField] private float _autoAimAngle = 10f;           // 조준 원뿔 각도 (도)

        private WeaponData _currentWeapon;
        private float _lastAttackTime = -10f;
        private Camera _mainCamera;
        private CinemachineImpulseSource _impulseSource;

        // ===== 애니메이션 =====
        private RigAnimationController _rigAnim;
        private ProceduralAnimationController _proceduralAnim;
        private NeuralAnimationController _neuralAnim;
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
        private const float ParryWindow = 0.28f;         // 패링 방어 판정 창(초) — 공격/우클릭 시작 시점으로부터
        private float _chargeHeldTime;                   // 우클릭 홀드 누적 시간
        private bool _charging;                          // 차지 충전 중 여부
        private bool _parryActive;                       // 패링 방어 창 활성 여부
        private float _parryActiveUntil;                 // 패링 창 만료 시각
        public string ChargedClipName = "Charged_Upward_Slash"; // [Phase 1-1] 차지 클립 플러그인 — FBX 확보 시 클립명만 갱신
        public string ParryClipName = "Sword_Parry_Backward_1"; // [Phase 1-2] 패링 클립 플러그인 — FBX 확보 시 클립명만 갱신

        // ===== 활(Bow) 드로→릴리즈 (2026-09-17): 좌클릭 홀드 = 드로(파워 축적), 해제 = 릴리즈(발사) =====
        // 근접 우클릭 차지(위)와 별개의 좌클릭 경로 — 검/창/Fist에는 영향 없음.
        private bool _bowDrawing;                 // 활 드로 진행 중 여부
        /// <summary>[70차 후속19/C6] 마지막 활 발사 파워(0~1) — 명중 시 파워 풀 크리틱 연출 판정용.</summary>
        public static float LastBowPower { get; private set; } = 1f;
        private float _bowDrawHeldTime;           // 드로 홀드 누적 시간(초)
        private const float BowDrawMaxHold = 0.5f; // 드로 최대 = 0.5초 — 이때 파워 1.0 도달
        private const float BowMinFire = 0.18f;    // 이 파워 미만 = 탭 = 캔슬(발사 안 함)

        // ===== C4-08: 자동 조준 상태 =====
        private IDamageable _currentTarget;

        /// <summary>현재 타겟 (자동 조준으로 선택된 적)</summary>
        public IDamageable CurrentTarget => _currentTarget;

        /// <summary>타겟이 있고 살아있는가?</summary>
        public bool HasTarget => _currentTarget != null && _currentTarget.IsAlive;

        /// <summary>공격이 가능한지 여부 (쿨다운 기준)</summary>
        public bool CanAttack => _currentWeapon != null && Time.time - _lastAttackTime >= _currentWeapon.attackSpeed;

        /// <summary>남은 쿨다운 시간 (0 이하이면 공격 가능)</summary>
        public float RemainingCooldown => Mathf.Max(0f, (_lastAttackTime + _currentWeapon?.attackSpeed ?? 1f) - Time.time);
        // 애니메이션 폴링용: 마지막 공격 시각 (변화 감지로 공격 모션 트리거)
        public float LastAttackTime => _lastAttackTime;

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

            // Neural 보류(2026-09-05): 자동부착 금지 — 씬에 명시 배치된 경우만 사용
            _neuralAnim = GetComponent<NeuralAnimationController>();
        }

        private void Update()
        {
            if (PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead) return;

            // 타겟 상태 업데이트 (사망 또는 범위 이탈 체크)
            UpdateTargetState();

            // [Phase 1-1/1-2] 패링 창 만료 감시
            if (_parryActive && Time.time >= _parryActiveUntil)
            {
                _parryActive = false;
                _proceduralAnim?.TriggerAction("parry_end");
            }

            // [RTS 연동] 우클릭 차지(강공) 제거 — 부대 우클릭 명령(병사 이동/공격)과 겹침. 우클릭은 RTSCommandSystem 전용으로 해방. 패링은 좌클릭 기반 유지.
            bool isBowEquipped = _currentWeapon != null
                && _currentWeapon.weaponType == ProjectName.Core.WeaponType.Bow;
            if (Mouse.current != null)
            {
                // [활 드로→릴리즈] 매 프레임 드로 누적 + 좌클릭 해제 = 릴리즈(파워 반영 발사)
                // 근접 우클릭 차지와 별개 좌클릭 경로 — 검/창/Fist에는 영향 없음(_bowDrawing은 활에서만 true).
                if (_bowDrawing)
                {
                    _bowDrawHeldTime += Time.deltaTime;
                    BowAimState.UpdatePower(Mathf.Clamp01(_bowDrawHeldTime / BowDrawMaxHold)); // [P25-C1] 리티클 파워
                }
                if (Mouse.current.leftButton.wasReleasedThisFrame && _bowDrawing)
                {
                    bool wasDrawing = _bowDrawing;
                    _bowDrawing = false;
                    if (wasDrawing)
                    {
                        float relPower = Mathf.Clamp01(_bowDrawHeldTime / BowDrawMaxHold);
                        BowAimState.Release(relPower >= BowMinFire, relPower);   // [P25-C1] 릴리즈(탭 캔슬/발사 페이드)
                        ReleaseBow(relPower);
                    }
                }
            }

            // 좌클릭 감지 (InputSystem)
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                // [U8 수리] UTK UI 위 좌클릭(인벤 드래그/슬롯 클릭)은 게임 공격으로 소비 금지
                if (ProjectName.Core.UITransitionState.PointerOverUI)
                    return;
                // [폭탄] 이번 프레임 이미 폭탄 투척 → 근접/무기 공격 금지 (AttackSystem과 순서 무관)
                if (BombArmController.BombThrowIssuedThisFrame)
                    return;
                // [폭탄] 무장 상태 좌클릭 = 폭탄 투척 — 일반/무기 공격 대신 발사하고 건너뜀
                if (BombArmController.Instance != null && BombArmController.Instance.IsArmed)
                {
                    BombArmController.Instance.ThrowTowardCursor();
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
                if (Keyboard.current != null
                    && (Keyboard.current.ctrlKey.isPressed
                        || Keyboard.current.leftCtrlKey.isPressed
                        || Keyboard.current.rightCtrlKey.isPressed))
                {
                    GuardSelectionManager.consumeLeftClickAsDrag = false;   // 드래그로 소비(중복 방지)
                    return;   // Ctrl+좌클릭 → 드래그(GuardSelectionManager가 선택 처리)
                }
                // [60차] GuardSelectionManager가 미리 세팅한 드래그 플래그 소비(보조 백업 — Ctrl 아닌 드래그 경로)
                if (GuardSelectionManager.consumeLeftClickAsDrag)
                {
                    GuardSelectionManager.consumeLeftClickAsDrag = false;   // 드래그로 소비
                    return;
                }
                // [활 드로 시작] 좌클릭 press 시 활이면 드로 시작 — 근접 parry/TryAttack 경로는 스킵하고
                // 해제(wasReleasedThisFrame) 시 ReleaseBow로 파워 반영 발사. 근접 우클릭 차지와 무관.
                if (isBowEquipped)
                {
                    _bowDrawing = true;
                    _bowDrawHeldTime = 0f;
                    BowAimState.Begin();   // [P25-C1] 리티클 드로 시작
                    AttackSoundLayerManager.PlayBowDraw(); // [활 드로] 좌클릭 press — 당김 스트레치 사운드 발화
                    return;
                }
                // [Phase 1-2] 패링 — 공격 시작 짧은 순간 방어 판정 창 (우클릭 차지 제거로 항상 패링 경로 실행)
                _parryActive = true;
                _parryActiveUntil = Time.time + ParryWindow;
                _proceduralAnim?.TriggerAction("parry");
                TryAttack();
            }
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
        private void ReleaseBow(float power)
        {
            if (power < BowMinFire)
            {
                Debug.Log($"[Bow] 드로 캔슬 (파워 너무 낮음) power={power:F2}");
                return;
            }
            _bowDrawHeldTime = 0f;
            TryBowShot(power);
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

        /// <summary>[Phase 1-2] 패링 방어 판정 — PlayerHealth가 근접 피격 시 호출(false 반환 시 회피 처리).</summary>
        public bool TryParry()
        {
            if (_parryActive)
            {
                _parryActive = false;   // 1회만
                _parryActiveUntil = -999f;
                _proceduralAnim?.TriggerAction("parry_success");
                Debug.Log("[PlayerCombat] 🛡️ 패링 성공 — 근접 공격 흡수");
                return true;
            }
            return false;
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

        private void TryAttack()
        {
            if (!CanAttack) return;
            // 연타 카운터 갱신(2026-09-14): 직전 공격 후 0.6초 이내 재공격이면 +1(최대 3), 아니면 1로 리셋.
            // _lastAttackTime 갱신 "전"에 판정해야 직전 공격 시각 기준으로 정상 판정된다.
            _attackStreak = (Time.time - _lastAttackTime <= AttackStreakWindow)
                ? Mathf.Min(_attackStreak + 1, AttackStreakMax)
                : 1;
            _lastAttackTime = Time.time;

            // ── P6 (2026-09-11): 무기 타입별 좌클릭 공격 분기 ──
            // Bow: 화살 발사 경로 — 발사체(ArrowProjectile)가 데미지를 담당하므로 근접
            //      AttackTarget/자동조준/근접 스윕을 호출하지 않는다(발사 성공/실패 모두 return).
            //      LastAttackTime 갱신(위)으로 HumanoidClipDriver 감시(L456)가 Bow 분기에서
            //      ArcheryShot을 자동 트리거 + TryBowShot 내부에서 TriggerBowShot()으로 명시 보강.
            // Spear/Fist/Sword: 기존 근접 공격 경로 유지(자동조준→AttackTarget 등).
            //      Spear는 사거리 4m < _autoAimRange 15m라 기존 조준 범위로 충분 — 별도 조정 없음.
            //      Fist/Sword는 기존 WeaponCombo B안이 드라이버에서 그대로 처리됨(정밀튜닝 보존).
            if (_currentWeapon != null && _currentWeapon.weaponType == ProjectName.Core.WeaponType.Bow)
            {
                TryBowShot(1f);   // 드로→릴리즈 외 즉발 회귀용 — 파워 풀(1f)
                return;
            }

            // Phase B: 무기별 스윙 사운드 레이어링
            PlayWeaponSwingSound();

            // 스윙 VFX는 HumanoidClipDriver가 콤보 임팩트 프레임(1~3타)에서 스윙 방향에 맞춰 발화

            // 공격 애니메이션 트리거
            _rigAnim?.Attack();
            _proceduralAnim?.TriggerAction("attack");
            _neuralAnim?.SwitchPolicy(NeuralAnimationController.PolicyType.Combat);

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
        }

        /// <summary>
        /// P6 (2026-09-11): 활(Bow) 좌클릭 발사 — 마우스 커서 방향으로 화살 1발을 소모·발사한다.
        /// 데미지는 발사체(ArrowProjectile)가 담당하므로 근접 AttackTarget/스윕 폴백을 호출하지 않는다.
        /// 화살 부족 시 미스 처리(LastHitValid=false) 후 종료(근접 공격으로 폴백하지 않음 — 활은 근접 무기가 아님).
        /// 카메라 이펙트/런지는 발사 성공 시에만 적용.
        /// </summary>
        private void TryBowShot(float power)
        {
            // ① 발사 사운드 — 무기별 레이어링
            PlayWeaponSwingSound();

            // ① 사격 방향 계산 — 마우스 커서 Ray 우선, 실패(카메라/마우스 없음) 시 플레이어 전방
            Vector3 dir = transform.forward;
            if (_mainCamera != null && Mouse.current != null)
            {
                Ray ray = _mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
                if (ray.direction.sqrMagnitude > 0.0001f)
                {
                    // [70차 후속19] 커서 레이를 지면 기준 수평 방향으로 평탄화 — 탑다운 카메라의 레이는
                    //   아래로 기울어져 있어 그대로 발사하면 화살이 땅으로 다이빙(테스트 5 실측 뿌리).
                    Vector3 flat = new Vector3(ray.direction.x, 0f, ray.direction.z);
                    if (flat.sqrMagnitude > 0.0001f)
                        dir = flat.normalized;
                }
            }

            // ② 조준 보정(자동 조준) 2026-09-16 — 커서 Ray가 적을 직접 못 맞히면 커서 방향(전방 반구)에서
            //    가장 가까운 적으로 dir 보정. 기존 조준 수단(FindTargetInCursorDirection: Raycast→원뿔 스윕) 재사용.
            //    보정은 직접 Raycast에 적 히트가 없을 때만 동작하며, cosθ>0.7 클램프로 화면 뒤 180도 스냅을 방지한다.
            if (_mainCamera != null && Mouse.current != null)
            {
                Ray cursorRay = _mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
                bool directEnemyHit = false;
                RaycastHit[] directHits = Physics.RaycastAll(cursorRay, _autoAimRange, _targetLayers);
                for (int i = 0; i < directHits.Length && !directEnemyHit; i++)
                {
                    IDamageable dmg = directHits[i].collider.GetComponentInParent<IDamageable>();
                    if (dmg != null && dmg.IsAlive) directEnemyHit = true;
                }
                if (!directEnemyHit)
                {
                    IDamageable target = FindTargetInCursorDirection();
                    MonoBehaviour targetBehaviour = (target != null) ? target as MonoBehaviour : null;
                    if (targetBehaviour != null)
                    {
                        Vector3 toTarget = targetBehaviour.transform.position - transform.position;
                        if (toTarget.sqrMagnitude > 0.0001f)
                        {
                            toTarget.Normalize();
                            if (Vector3.Dot(toTarget, transform.forward) > 0.7f)
                            {
                                // [70차 후속19] 자동 조준 방향도 지면 기준 평탄화 — 타겟 중심(y 낮음)으로 다이빙 방지
                                toTarget.y = 0f;
                                toTarget.Normalize();
                                dir = toTarget;
                                Debug.Log("[PlayerCombat] 자동 조준: " + targetBehaviour.name);
                            }
                        }
                    }
                }
            }

            // ③ 화살 소모 + 발사체 생성 — origin: 활 위치(전방 0.6m·눈높이 1.4m), 데미지: WeaponData.Bow.damage
            // [70차 후속18] 플레이어 중심 스폰이 몸을 뚫는 문제(테스트 4 실측) → 활 위치로 이동
            Vector3 aimFwd = transform.forward; aimFwd.y = 0f; aimFwd.Normalize();
            Vector3 origin = transform.position + aimFwd * 0.6f + Vector3.up * 1.4f;
            //    (화살 종류별 보너스 데미지 합산 + 파워 반영 속도/데미지는 ArrowManager 내부 처리)
            //    [TEST21-FOLLOWUP] ArrowManager lazy 자가 확보 — 씬 미부트/초기화 순서로 Instance가 null이면
            //    즉시 생성 시도(EnsureGameManager 보장과 이중 안전, 멱등). 없으면 발사 불가로 안내.
            if (ArrowManager.Instance == null)
            {
                // Test_10 EnsureGameManager에서 생성 보장이 우선이지만, 다른 씬/순서 대비 런타임 자가 생성.
                if (Application.isPlaying) { var go = new GameObject("ArrowManager"); go.AddComponent<ArrowManager>(); }
            }
            bool fired = ArrowManager.Instance != null
                && ArrowManager.Instance.TryShootArrow(origin, dir, WeaponData.Bow.damage, power);
            if (!fired)
            {
                // 화살 부족 — 발사 실패. TryShootArrow 내부에서 차단 메시지 표시됨.
                LastHitValid = false;
                Debug.Log("[PlayerCombat] 🏹 활 발사 실패 — 화살 부족 or ArrowManager 미생성");
                return;
            }
            // [70차 후속19/C1·C6] 발사 성공 — 소형 카메라 킥(활 전용) + 파워 기억(명중 시 크리틱 연출용)
            CombatCameraEffects.PlayFireKick();
            LastBowPower = power;
            // [P25-C2] 발사 머즐 퍼프 — 활 위치에서 작은 먼지 퍼프(발사 연출).
            ArrowProjectile.SpawnMuzzlePuff(origin);
            // [TEST25-66차] 발사 성공 실측 로그 — 화살 비행(ArrowProjectile) + ArcheryShot(발사 애니) 동시 고정.
            Debug.Log("[PlayerCombat] 🏹 활 발사 성공 — 화살 비행(ArrowProjectile) + ArcheryShot(활 사격 애니)");

            // ③ 발사 애니 — ArcheryShot 명시 트리거. LastAttackTime은 TryAttack 시작부에서 이미 갱신되어
            //    드라이버 감시가 Bow 분기에서 ArcheryShot을 자동 트리거하며, TriggerBowShot()은
            //    동일 프레임 중복 SetTrigger(무해)이자 드라이버 미연결 시나리오의 안전망이다.
            //    ⚠️ _rigAnim.Attack()은 Fist 근접 클립을 재생시킬 수 있어 Bow 경로에서는 호출하지 않는다.
            _clipDriver?.TriggerBowShot();

            // ④ 발사 성공 시에만 연출 — 카메라 반동 + 발사 전진(런지)
            TriggerCameraEffects();
            StartCoroutine(AttackLungeCoroutine());
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
        private void AttackTarget(IDamageable target)
        {
            if (target == null || !target.IsAlive) return;
            if (IsOwnSoldier(target)) return;   // [69차 후속10] 아군 오인 피해 차단 — 내 소속 병사는 플레이어 공격으로 피해를 입지 않는다(합세 통보도 없음)

            float damage = CalculateDamage();
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
            _recoilActive = true;
            StartCoroutine(RecoilCoroutine(-hitDirection, isBackAttack ? RecoilDistanceCrit : RecoilDistanceNormal));

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
        private bool AttackCenterScreen()
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
                    AttackTarget(target);
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
        private void OnGUI()
        {
            // [70차 후속19/C5] 드로 중 조준 프리뷰 — 활 위치→조준 방향 얇은 궤적 라인(파워 비례 길이·알파).
            if (!_bowDrawing || _mainCamera == null) return;

            Vector3 aimFwd = transform.forward; aimFwd.y = 0f; aimFwd.Normalize();
            Vector3 arrowOrigin = transform.position + aimFwd * 0.6f + Vector3.up * 1.4f;
            Vector3 end = arrowOrigin + aimFwd * (12f + 50f * _bowDrawHeldTime / Mathf.Max(0.01f, BowDrawMaxHold));

            Vector3 s0 = _mainCamera.WorldToScreenPoint(arrowOrigin);
            Vector3 s1 = _mainCamera.WorldToScreenPoint(end);
            if (s0.z < 0f || s1.z < 0f) return;
            s0.y = Screen.height - s0.y; s1.y = Screen.height - s1.y;

            float power = Mathf.Clamp01(_bowDrawHeldTime / Mathf.Max(0.01f, BowDrawMaxHold));
            var prevColor = GUI.color;
            GUI.color = new Color(1f, 0.85f, 0.4f, 0.25f + 0.45f * power);   // 골드 프리뷰 — 파워 비례 진해짐
            Vector2 delta = new Vector2(s1.x - s0.x, s1.y - s0.y);
            float len = delta.magnitude;
            if (len > 1f)
            {
                float ang = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                GUIUtility.RotateAroundPivot(ang, s0);
                GUI.DrawTexture(new Rect(s0.x, s0.y - 1.5f, len, 3f), Texture2D.whiteTexture);
                GUI.matrix = Matrix4x4.identity;
            }
            GUI.color = prevColor;
        }
    }
}
