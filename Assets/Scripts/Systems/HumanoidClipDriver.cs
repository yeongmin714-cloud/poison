// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 162,618
using UnityEngine;
using System.Collections.Generic;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>Pure timing/authorization state for the two-swing Weapon_Combo_2 clip.</summary>
    public sealed class MeleeComboStateMachine
    {
        public enum Decision { Inactive, Playing, Hold, Resume, Exit }

        private int _acceptedClicks;
        private float _lastClickTime;
        private bool _active;
        private bool _holding;
        private bool _resumePending;

        public int AcceptedClicks => _acceptedClicks;
        public bool IsActive => _active;
        public bool DidExitBecauseFollowupWindowExpired { get; private set; }

        public void Start(float now)
        {
            _acceptedClicks = 1;
            _lastClickTime = now;
            _active = true;
            _holding = false;
            _resumePending = false;
            DidExitBecauseFollowupWindowExpired = false;
        }

        public bool CanAcceptFollowup(float now, float clickWindow)
        {
            return _active && _acceptedClicks < 2 && now - _lastClickTime < clickWindow;   // [168-P1] 수용 상한 3→2 (2클릭 콤보 계약)
        }

        public bool IsFollowupWindowExpired(float now, float clickWindow)
        {
            return _active && now - _lastClickTime >= clickWindow;
        }

        public bool TryAcceptFollowup(float now, float clickWindow)
        {
            if (!CanAcceptFollowup(now, clickWindow)) return false;
            _acceptedClicks++;
            _lastClickTime = now;
            _resumePending = _holding;
            _holding = false;
            return true;
        }

        public Decision Evaluate(float now, float normalizedTime, float clickWindow)
        {
            if (!_active) return Decision.Inactive;
            DidExitBecauseFollowupWindowExpired = false;
            if (_acceptedClicks == 2 && normalizedTime >= 1f)   // [168-P1] ==3→==2 — 전체 수용 시 클립 종료 판정
            {
                _active = false;
                return Decision.Exit;
            }
            float playbackLimit = _acceptedClicks / 2f;   // [168-P1] /3f→/2f — 1클릭=클립 1/2 보유
            if (_acceptedClicks < 3 && now - _lastClickTime >= clickWindow
                && normalizedTime >= playbackLimit - 0.001f)
            {
                _active = false;
                DidExitBecauseFollowupWindowExpired = true;
                return Decision.Exit;
            }
            if (_resumePending)
            {
                _resumePending = false;
                return Decision.Resume;
            }

            if (normalizedTime >= playbackLimit)
            {
                _holding = true;
                return Decision.Hold;
            }
            return Decision.Playing;
        }

        public void Reset()
        {
            _acceptedClicks = 0;
            _lastClickTime = 0f;
            _active = false;
            _holding = false;
            _resumePending = false;
            DidExitBecauseFollowupWindowExpired = false;
        }
    }

    /// <summary>
    /// 믹사모 Humanoid Animator 런타임 드라이버 (플레이어 / 병사).
    ///
    /// 각 런타임 AnimatorController가 정의한 공통 파라미터를 구동한다:
    ///   Speed(float) / 트리거 Attack, AttackCombo, Hit, Death
    ///   (Player_AC에만 추가) Roll, RollBack, RollSideL, RollSideR, Jump
    ///
    /// ■ Player 모드 — CharacterController.velocity(수평)를 Speed로, PlayerCombat.AcceptedAttackSequence
    ///   변화(허용된 클릭 엣지)를 등록된 AttackCombo/2/3 상태의 순차 재생 및 strike 타이밍으로 변환하며,
    ///   PlayerMovement.IsRolling/IsJumping 상승엣지를 Roll/RollBack/RollSideL/RollSideR(Jump) 트리거로 변환한다.
    ///   ([132차] 구르기 방향은 PlayerMovement.LastRollKind로 분기 — Shift+방향 키 체계.)
    /// ■ Soldier 모드 — transform 위치 델타로 Speed를 계산하고, 공격은 GuardCombatAI가
    ///   TriggerAttack()으로 호출한다.
    /// </summary>
    public class HumanoidClipDriver : MonoBehaviour
    {
        public enum DriveMode { Player, Soldier }

        [Header("드라이브 모드")]
        public DriveMode mode = DriveMode.Player;

        /// <summary>[59차] 같은 오브젝트에 활성 드라이버(Player/Soldier) 존재 — RigAnimationController의 Speed 이중 서술 게이트용.</summary>
        public static bool HasActiveDriverOn(GameObject go)
            => go != null && go.GetComponent<HumanoidClipDriver>() is HumanoidClipDriver d && d.enabled
               && (d.mode == DriveMode.Player || d.mode == DriveMode.Soldier);

        private static readonly string[] StageStateNames = { "AttackCombo", "AttackCombo2", "AttackCombo3" };
        private const string MeleeComboStateName = "WeaponCombo"; // retained for the separate Spear follow-up route
        private const string MeleeComboExitStateName = "Idle";
        private const string SpearOpenerStateName = "AttackThrust";
        private const string SpearOpenerExitStateName = "Idle";
        // [168-P1] 2클릭 콤보: 클릭1=1휘두르기(1/2 보유), 0.85s 내 클릭2=전체. 4.53s 클립 3등분이 1클릭=2스윙 체감 원인(11-21 실측)
        private const int MaxMeleeComboClicks = 2;
        private const float StageStrikeSyncNormT = 0.32f;
        private const float StageAdvanceNormT = 0.90f;
        private const float MeleeComboClickWindow = 0.85f;
        /// <summary>
        /// [205차 배치B-1] 스피어 전용 팔로업 클릭 윈도우 — 세그먼트2 데미지 미발화 근원 수리.
        /// 근원(11-26 Editor.log 실측): 스피어 쿨다운(WeaponData.Spear.attackSpeed=1.5s — PlayerCombat.CanAttack)
        /// 가 구 melee 공용 윈도우(0.85s)보다 길어 2클릭 팔로업이 구조적으로 도달 불능이었다.
        /// ① 0.85s 내 클릭2 = PlayerCombat 거부(쿨다운 미개시 — 세션 로그 "attack rejected ... spearFollowup=True" 14건,
        ///   쿨다운이 spearFollowup 승인보다 우선됨 — PlayerCombat L1498 구조는 P4 보고 별도).
        /// ② 0.85s 경과 클릭 = 만료 재시작(새 오프너 → 스윙1 데미지만 — IsSpearSequenceExpired).
        ///   세션 전체에서 "Spear segment accepted=2" 및 "[Spear] WeaponCombo follow-up hit resolved" 0건.
        /// 1.8s = 쿨다운 1.5s + 0.3s 수용 밴드 — 쿨다운 개시(1.5s) 직후 접수되는 클릭2가 팔로업으로 승인되고,
        /// 1.8s 경과 클릭은 기존과 동일하게 만료 재시작한다. 주먹/검 melee 콤보 윈도우(0.85s)와 [168-P1] 계약 무수정.
        /// </summary>
        private const float SpearFollowupClickWindow = 1.8f;
        private const float ComboSegmentStrikeNormT = 0.32f;
        private const float ComboSlashVfxLeadNormT = 0.12f;   // [168-P1] 슬래시 VFX를 데미지 스트라이크보다 먼저 발화(사용자: 아크가 늦다)
        /// <summary>
        /// [174차] 콤보 스테이지 간격 실측 하한(초) — FX 쿨다운(슬래시/찌르기 각 0.25s)이 절대 이보다 길지 않아야
        /// 하는 근거값. ① 스피어 오프너 스윙 1→2 간격 = (0.75−0.30)×오프너 클립 — 폴백 길이 1.2s 기준 최악 0.54s
        /// (실측 클립 3.77s면 1.70s). ② 근접 콤보 세그먼트 간격 = 0.5×4.53s÷3속도 ≈ 0.76s. 최소 0.54s ≫ 0.25s —
        /// 쿨다운 return에 의한 스테이지 소실 없음 → 상수 트림 불요. 부등식은 CombatFxThrustSeparationTests가 고정.
        /// </summary>
        internal const float MinComboStageGapSeconds = 0.54f;
        private const float ComboExitBlend = 0.10f;
        // ═════════ [166-B] 무기별 스윙 임팩트 시간 테이블 — 스윙별 멀티임팩트 단일 소스 ═════════
        // 그립테이블(WeaponEquipManager._gripTable) 패턴 준수 — 타입 테이블 + 미등록 폴백.
        // 사용자 관측([166-B]): 찌르기(스피어 오프너) 애니 클립에 휘두르기 2회 박혀 있음 → 판정도 스윙별 2회.
        // 초기값(예: 0.30/0.75)은 Play 실측 트림 대상 — 영상→판정→트림 루프로 정점 상수 갱신(계획 §B 검증).
        internal static readonly Dictionary<WeaponType, float[]> LegacySwingImpactTable = new Dictionary<WeaponType, float[]>
        {
            { WeaponType.Spear, new[] { 0.30f, 0.75f } },   // 오프너 클립(플레이어 창 휘두르기.fbx, Take 001 3.77s) 스윙 1/2 정점 초기값
        };

        /// <summary>[166-B] 테이블 미등록 무기 폴백 — 기존 단일 임팩트 시점(0.50) 유지(레거시 Attack/AttackBase 등).</summary>
        internal const float LegacySwingImpactNormTFallback = 0.50f;

        /// <summary>[무번호 45-SPEAR-TIMEGATE] 오프너 스윙 1 정점 — [166-B] 테이블(Spear[0]=0.30) 단일 소스.
        /// 기존 0.50 단일 임팩트 → 스윙별 분리로 0.30 조정. P45 시간 게이트 산식(SpearOpenerStrikeNormT) 식별자 유지.</summary>
        private static readonly float SpearOpenerStrikeNormT = GetLegacySwingImpactNormT(WeaponType.Spear, 0);

        /// <summary>[166-B→176차] 오프너 스윙 2 정점 — Spear 테이블[1](0.75). 자동 게이트는 철수됐으나
        /// 판정 함수(단위테스트 리플렉션 핀)가 이 상수를 계속 소비한다.</summary>
        private static readonly float SpearOpenerSwing2NormT = GetLegacySwingImpactNormT(WeaponType.Spear, 1);

        /// <summary>[166-B] 무기별 스윙 임팩트 시간 조회 — 테이블 미등록/인덱스 초과 시 폴백(0.50 단일 시점).</summary>
        internal static float GetLegacySwingImpactNormT(WeaponType weaponType, int swingIndex)
        {
            if (LegacySwingImpactTable.TryGetValue(weaponType, out var times) && times != null
                && swingIndex >= 0 && swingIndex < times.Length)
                return times[swingIndex];
            return LegacySwingImpactNormTFallback;
        }

        /// <summary>[166-B] 무기별 등록 스윙 수 — 미등록 무기는 1회(기존 단일 임팩트 동작 유지).</summary>
        internal static int GetLegacySwingImpactCount(WeaponType weaponType)
        {
            if (LegacySwingImpactTable.TryGetValue(weaponType, out var times) && times != null && times.Length > 0)
                return times.Length;
            return 1;
        }

        private int _comboStage;          // 0=inactive, 1..3=current registered Animator stage
        private readonly bool[] _stageHitResolved = new bool[3];
        private readonly bool[] _stageVfxResolved = new bool[3];   // [168-P1] 슬래시 VFX 조기발화 1회 가드 — _stageHitResolved 미러(리셋 동기)
        private bool _expiredComboRestartAvailable;
        private int _comboBufferedClicks; // accepted followups queued for distinct registered states
        private int _comboAcceptedClicks;
        private readonly MeleeComboStateMachine _meleeCombo = new MeleeComboStateMachine();
        private int _spearAcceptedClicks;
        private float _spearOpenerAcceptedTime;
        private bool _spearOpenerHitResolved;
        private bool _spearFollowupHitResolved;
        private bool _spearFollowupAuthorized;
        private bool _spearFollowupStarted;
        private float _spearStateTransitionTime;
        // [2026-09-25 프리미엄 AttackArcVFX A/B] 인스펙터에서 켜면 실제 무기 궤적 3D 스윕 리본을 켠다(기본 OFF).
        [SerializeField, Header("AttackArcVFX (프리미엄 스윕 리본) A/B")]
        private bool _premiumArcEnabled = true;   // [무번호 35-S1] 프리미엄 참격 리본 기본 활성(F6 토글로 A/B 비교 유지)
        private float _comboStartTime = -999f;
        // [166-B] 레거시 Attack* 스윙별 해결 플래그 — 무기별 스윙 임팩트 테이블 인덱스별 1회 가드(최대 3스윙 여유).
        private readonly bool[] _legacySwingResolved = new bool[3];
        private int _prevAttackStateHash = -1;

        private Animator _anim;
        private bool? _tillParamExists;   // [153차 P2] "Till" 파라미터 유무 1회 판정 캐시 — TriggerTill 전용
        private bool? _mineParamExists;   // [156차 P5] "Mine" 파라미터 유무 1회 판정 캐시 — TriggerMine 전용
        private CharacterController _cc;
        private PlayerMovement _movement;
        private PlayerCombat _combat;
        private SoldierSwingTrail _soldierSwingTrail;   // [무번호 26-ST4] 병사 스윙 트레일 캐시 — null이면 조용히 무시(무기 부착 실패 병사 무해)

        private Vector3 _lastPos;
        private long _consumedAttackSequence;       // 마지막으로 소비한 accepted-attack sequence
        private long _pendingAcceptedAttackInputs;  // sequence deltas retained until the active three-swing combo has room
        private bool _acceptedMeleeRouteDispatchedThisUpdate;
        private float _prevPlayerHP = -1f;         // 직전 프레임의 PlayerHealth.CurrentHP (HP 감소 엣지 → HitLight)
        private bool _pendingPlayerHitLight;
        private bool _prevRolling, _prevJumping;
        private float _prevSpeedForTransition = -999f;   // T-D3: Run→Walk 전환 연출용 직전 프레임 속도
        private bool _prevBow, _prevSpear, _prevThrow;   // T-D3: 무기 모드 엣지 감지
        private WeaponType _prevWType = WeaponType.Fist; // M2: CurrentType 엣지 감지
        private bool _deathFired;

        // ── [134차 E-3] 측면 롤 바디 스핀 — 진행축 기준 360° 스핀(구 요잉 ±140° 치환) + 종료 원복 ──
        // 근거(계획 §7 E 재판정): 요잉(±140°) 후에도 어색 — 롤클립 자체가 전방 텈 구르기를 옆으로 눕힌
        // 형태라 요잉만으론 "옆으로 구르는" 돕법이 안 만들어짐 → 요잉 로직을 스핀으로 치환(제거 아님).
        // 스핀축: 롤 방향의 월드 진행축(좌=−transform.right, 우=+transform.right)을 로컬축으로 변환해 회전 —
        // 톱다운에서 실린더 굴림 독법. Left=−360°/Right=+360°를 롤 지속시간에 걸쳐 선형 회전하고,
        // 종료 시 저장 localRotation 복원(기존 타이머/OnDisable 가드 재사용). 앞/뒤 롤(Roll/RollBack) 무수정.
        private const float SideRollSpinFallbackSeconds = 0.5f;  // _movement 접근 불가 폴백(PlayerMovement._rollDuration 기본값과 동일 값)
        private const float SideRollSpinDegrees = 360f;          // 스핀 총 회전각 — 진행축 기준 1회전
        private Transform _sideRollVisual;           // 스핀 대상 비주얼 트랜스폼(Animator 부착 트랜스폼 — _anim.transform)
        private Quaternion _sideRollSavedLocalRot;   // 발화 시점 localRotation(원복 기준 — 저장값 복원)
        private Vector3 _sideRollSpinLocalAxis;      // 스핀 로컬축 — 월드 진행축(좌=−right/우=+right)을 로컬로 변환
        private float _sideRollSpinSign;             // 스핀 부호(Left=-1/Right=+1)
        private float _sideRollDuration;             // 스핀 총 지속(초) — 롤 타이머 duration과 동일 값
        private float _sideRollSpinLeft;             // 스핀 잔여 시간(초) — >0 = 진행 중, 0 = 휴면
        private const float SideRollCrouchScaleY = 0.65f;   // [134차 E5] 딥웅크림 — 스핀 중 비주얼 localScale.y 배율(높이 65%)
        private const float SideRollBulgeScaleXZ = 1.08f;   // [134차 E5] 공 말림 — 스핀 중 localScale.x/z 배율(딥웅크림 대응 부풀림)
        private Vector3 _sideRollSavedLocalScale;    // [134차 E4] 발화 시점 localScale(웅크림 원복 기준 — 저장값 복원)

        public bool CanPlayPlayerMeleeCombo => mode == DriveMode.Player && _anim != null
            && _anim.isActiveAndEnabled && _anim.isInitialized && _anim.runtimeAnimatorController != null;
        public bool IsPlayerMeleeComboActive => CanPlayPlayerMeleeCombo && _comboStage >= 1 && _comboStage <= 3;
        public bool CanAcceptBufferedMeleeFollowup
        {
            get
            {
                if (!CanPlayPlayerMeleeCombo || _combat == null) return false;

                long unconsumed = _combat.AcceptedAttackSequence - _consumedAttackSequence;
                long pendingClicks = _pendingAcceptedAttackInputs + (unconsumed > 0 ? unconsumed : 0L);
                if (_comboStage >= 1)
                {
                    bool windowOpen = _meleeCombo.CanAcceptFollowup(Time.time, MeleeComboClickWindow);
                    return CanAcceptBufferedFollowup(true, _meleeCombo.AcceptedClicks, pendingClicks, windowOpen);
                }

                // The accepted opener may still be waiting for this driver's next Update. Reserve
                // remaining combo slots from its unconsumed sequence delta, not the cooldown gate.
                bool pendingOpenerInWindow = pendingClicks > 0
                    && Time.time - _combat.LastAttackTime < MeleeComboClickWindow;
                return CanAcceptBufferedFollowup(true, 0, pendingClicks, pendingOpenerInWindow);
            }
        }
        /// <summary>True only when an active melee combo's click window elapsed; a new opener may bypass cooldown.</summary>
        public bool CanRestartExpiredMeleeCombo => CanPlayPlayerMeleeCombo
            && ((_comboStage >= 1 && _meleeCombo.IsFollowupWindowExpired(Time.time, MeleeComboClickWindow))
                || _expiredComboRestartAvailable);

        /// <summary>Only one timely second Spear click can authorize the WeaponCombo follow-up.</summary>
        public bool CanAcceptSpearFollowup
        {
            get
            {
                if (!CanPlayPlayerMeleeCombo || _combat == null) return false;
                long unconsumed = _combat.AcceptedAttackSequence - _consumedAttackSequence;
                long pending = _pendingAcceptedAttackInputs + (unconsumed > 0 ? unconsumed : 0L);
                float openerTime = _spearAcceptedClicks > 0 ? _spearOpenerAcceptedTime : _combat.LastAttackTime;
                return EvaluateSpearFollowupAcceptance(_spearAcceptedClicks > 0, _spearAcceptedClicks, pending,
                    openerTime, Time.time, SpearFollowupClickWindow);   // [205차 배치B-1] 스피어 전용 윈도우(쿨다운 1.5s > 구 0.85s — 근원 수리)
            }
        }
        public bool IsSpearSequenceActive => CanPlayPlayerMeleeCombo && _spearAcceptedClicks > 0;
        public bool CanRestartExpiredSpearSequence => IsSpearSequenceActive
            && Time.time - _spearOpenerAcceptedTime >= SpearFollowupClickWindow;   // [205차 배치B-1] 스피어 전용 윈도우 — 만료 재시작 게이트와 팔로업 수용 상수 일치
        /// <summary>T-D3+: 외부 시스템 발화용 퍼블릭 트리거(채집/경직/스턴/다운).</summary>
        // [129차] 액션 애니 트리거 — Player_AC 신규 상태(Gather/Craft/Cheer) 배선. 발화 가드: 액션 상태 중 Gather/Craft 재발화 무시.
        public void TriggerHarvest() { TriggerGather(); } // [129차 Q4] TriggerGather 위임
        public void TriggerGather() { if (CanFireAction) _anim.SetTrigger("Gather"); }
        public void TriggerCraft() { if (CanFireAction) _anim.SetTrigger("Craft"); }
        public void TriggerCheer() { if (_anim != null) _anim.SetTrigger("Cheer"); }
        public void TriggerActionEnd() { if (_anim != null) _anim.SetTrigger("ActionEnd"); }

        /// <summary>[153차 P2] 굉이질/정리 채널용 트리거 — "Till" 파라미터 미보유 컨트롤러에서 무해.
        ///   파라미터 유무는 1회 판정 후 캐시 → 미보유 컨트롤러에서는 SetTrigger를 스킵(경고 로그 억제).</summary>
        public void TriggerTill()
        {
            if (_anim == null) return;
            if (!_tillParamExists.HasValue)
            {
                if (!_anim.isInitialized) return;   // [153차 P2] 미초기화 — 판정 보류(캐시 오염 방지).
                bool found = false;
                foreach (AnimatorControllerParameter p in _anim.parameters)
                {
                    if (p.name != "Till") continue;
                    found = true;
                    break;
                }
                _tillParamExists = found;   // [153차 P2] 1회 판정 캐시 — 미보유 시 이후 발화 전부 스킵.
            }
            if (_tillParamExists.Value) _anim.SetTrigger("Till");
        }

        /// <summary>[156차 P5] 채굴 채널용 트리거 — 곡갱이질 애니(Player_AC "Mine" 상태) 발화.
        ///   TriggerTill 선례 1:1(파라미터 유무 1회 판정 캐시 — "Mine" 미보유 컨트롤러에서 무해·경고 억제)에
        ///   CanFireAction 게이트 동일 적용(Gather/Craft/Cheer 상태 중 재발화 무시 — TriggerGather 선례).</summary>
        public void TriggerMine()
        {
            if (_anim == null) return;
            if (!CanFireAction) return;
            if (!_mineParamExists.HasValue)
            {
                if (!_anim.isInitialized) return;   // [156차 P5] 미초기화 — 판정 보류(캐시 오염 방지).
                bool found = false;
                foreach (AnimatorControllerParameter p in _anim.parameters)
                {
                    if (p.name != "Mine") continue;
                    found = true;
                    break;
                }
                _mineParamExists = found;   // [156차 P5] 1회 판정 캐시 — 미보유 시 이후 발화 전부 스킵.
            }
            if (_mineParamExists.Value) _anim.SetTrigger("Mine");
        }

        /// <summary>[153차 P2] 액션 상태 재시작 — 지정 상태를 0프레임부터 다시 재생(채널 리듬 동작 유지용).
        ///   CanFireAction 재트리거 가드의 우회 목적이 아니라, 채널 타격 리듬마다 굉이질/채집 동작을
        ///   이어서 유지하기 위한 상태 재발화다. null/비활성 _anim에서 무영향(가드).</summary>
        public void ReplayAction(string stateName)
        {
            if (_anim == null || !_anim.isActiveAndEnabled) return;
            if (string.IsNullOrEmpty(stateName)) return;
            _anim.Play(stateName, 0, 0f);
        }
        private bool CanFireAction
        {
            get
            {
                if (_anim == null || mode != DriveMode.Player) return false;
                var cur = _anim.GetCurrentAnimatorStateInfo(0);
                return !(cur.IsName("Gather") || cur.IsName("Craft") || cur.IsName("Cheer"));
            }
        }
        public void TriggerHitLight()
        {
            if (_anim == null) return;
            if (mode == DriveMode.Player)
            {
                AnimatorStateInfo current = _anim.GetCurrentAnimatorStateInfo(0);
                bool transitioning = _anim.IsInTransition(0);
                AnimatorStateInfo next = transitioning ? _anim.GetNextAnimatorStateInfo(0) : default(AnimatorStateInfo);
                if (ShouldQueueExternalHitLight(true, IsMeleeAttackAnimatorState(current), transitioning,
                    transitioning && IsMeleeAttackAnimatorState(next)))
                {
                    _pendingPlayerHitLight = true;
                    return;
                }
            }
            _anim.SetTrigger("HitLight");
        }

        private static bool ShouldQueueExternalHitLight(bool driveModeIsPlayer, bool currentStateIsMelee,
            bool isTransitioning, bool nextStateIsMelee)
        {
            return driveModeIsPlayer && ShouldDeferHitLight(currentStateIsMelee, isTransitioning, nextStateIsMelee);
        }

        // ── P4: 무기별 공격 애니 분기용 공용 트리거 (PlayerCombat 경유 호출 전용) ──
        // 의존 방향: PlayerCombat → 이 공용 API로만 트리거(PlayerCombat에서 _anim 직접 접근 금지).
        /// <summary>창(Spear) 찌르기 애니 — 레거시 Attack 트리거(AttackThrust 찌르기 경로) 발화. WeaponCombo 미진입.</summary>
        public void TriggerSpearAttack()
        {
            if (_anim == null) return;
            _anim.SetTrigger(SpearOpenerStateName);
            _attackHoldUntil = Time.time + 0.6f;   // 찌르기 중 Speed 0 홀드(Idle/Walk 인터럽트 방지)
            Debug.Log("[HumanoidClipDriver] Spear 찌르기 트리거 (AttackThrust)");
        }

        /// <summary>활(Bow) 발사 애니 — ArcheryShot 트리거 발화. WeaponCombo 진입 금지.
        /// 화살 소모/발사체 생성은 PlayerCombat(좌클릭 TryBowShot) 경로에서 ArrowManager가 담당.</summary>
        public void TriggerBowShot()
        {
            if (_anim == null) return;
            _anim.SetTrigger("ArcheryShot");
            _attackHoldUntil = Time.time + 0.6f;   // 발사 중 Speed 0 홀드
            Debug.Log("[HumanoidClipDriver] ArcheryShot 트리거 (활 발사)");
        }

        /// <summary>[A 고품질] 활 드로 애니 — 사용자 애니 '플레이어 활쏘기.fbx' 트리거(DrawShoot).
        /// 좌클릭 홀드 드로 시작 시 발화해 '당기고 쏘기' 2단계 feel을 준다(릴리즈 시 ArcheryShot이 이어서).</summary>
        public void TriggerBowDraw()
        {
            if (_anim == null) return;
            _anim.SetTrigger("DrawShoot");
            _attackHoldUntil = Mathf.Max(_attackHoldUntil, Time.time + 0.5f);   // 드로 중 Speed 0 홀드(발사와 겹침 방지)
            Debug.Log("[HumanoidClipDriver] DrawShoot 트리거 (활 드로—사용자 애니)");
        }

        /// <summary>[테스트45 P5] V키 패링 애니(171차 정정 — 좌클릭은 공격/큐잉) — Player_AC 'Parry' 상태 트리거(사용자 FBX: Sword_Parry_Backward_1).
        /// 실클립 재생 + 홀드 → 성공/종료 시 자연 복귀.</summary>
        public void TriggerParry()
        {
            if (_anim == null) return;
            _anim.SetTrigger("Parry");
            _attackHoldUntil = Mathf.Max(_attackHoldUntil, Time.time + 0.45f);   // 패링 모션 중 Speed 0 홀드
            Debug.Log("[HumanoidClipDriver] Parry 트리거 (V키 패링—사용자 애니)");
        }

        private void OnDestroy()
        {
            PlayerHealth.OnPlayerDamaged -= OnPlayerDamagedFX;
        }

        private void OnDisable()
        {
            // [134차 E-3/E5] 스핀·딥웅크림 원복 가드 — 비활성화 시점에 측면 롤 스핀 진행 중이면 저장 회전/스케일(y+xz) 즉시 복원
            // (롤 중 컴포넌트 정지/파괴로 스핀·딥웅크림이 잔존하는 영구 비틀림 방지 — 원복은 멱등).
            if (_sideRollSpinLeft > 0f) EndSideRollSpin();
        }

        // ── [134차 E-3] 측면 롤 바디 스핀 연출 ──

        /// <summary>
        /// [134차 E-3/E4] 측면 롤(RollSideL/RollSideR) 발화 시 스핀 개시 — 비주얼 트랜스폼(Animator 부착
        /// 트랜스폼 — _anim.transform)의 localRotation·localScale 저장 후, 롤 지속시간(PlayerMovement.RollDuration
        /// 실측 — 기존 롤 타이머 상수와 동일 값, 접근 불가 시 폴백 상수) 동안:
        ///  · 360° 스핀(E-3 유지) — 월드 진행축(좌=−transform.right, 우=+transform.right)을 로컬축으로 변환해
        ///    Left=−360°/Right=+360° 선형 회전(localRotation = 저장값 × 스핀쿼터니언 양산).
        ///  · [134차 E5] 딥웅크림 — 스핀 중 비주얼 localScale.y ×0.65 + x/z ×1.08(공처럼 말린 체감), 종료 시 저장값 원복.
        /// 종료 시 저장값 복원(기존 타이머/OnDisable 가드 재사용). 루트 이동/무적/쿨다운 계약 무변경.
        /// 롤클립은 미재생(E4 — SetTrigger 미발화, 현재 상태 유지). 앞/뒤 롤(Roll/RollBack)은 호출 없음(무수정).
        /// </summary>
        private void BeginSideRollSpin(bool isLeft)
        {
            Transform visual = _anim != null ? _anim.transform : transform;
            if (_sideRollSpinLeft > 0f) EndSideRollSpin();   // 중복 발화 — 이전 스핀 원복 후 재적용
            float duration = _movement != null && _movement.RollDuration > 0f
                ? _movement.RollDuration
                : SideRollSpinFallbackSeconds;
            _sideRollVisual = visual;
            _sideRollSavedLocalRot = visual.localRotation;
            // [134차 E5] 딥웅크림 가드 — localScale.y ×0.65 + x/z ×1.08(공처럼 말린 체감 — 저장/원복 쌍, 스핀 종료·OnDisable 양쪽 복원).
            _sideRollSavedLocalScale = visual.localScale;
            Vector3 crouched = _sideRollSavedLocalScale;
            crouched.y *= SideRollCrouchScaleY;
            crouched.x *= SideRollBulgeScaleXZ;
            crouched.z *= SideRollBulgeScaleXZ;
            visual.localScale = crouched;
            Vector3 worldAxis = isLeft ? -visual.right : visual.right;   // 월드 진행축 — 좌=−right/우=+right
            _sideRollSpinLocalAxis = visual.InverseTransformDirection(worldAxis).normalized;   // 로컬축 변환
            _sideRollSpinSign = isLeft ? -1f : 1f;
            _sideRollDuration = Mathf.Max(0.01f, duration);
            ApplySideRollSpin(0f);         // 0°에서 시작 — duration에 걸쳐 ±360° 선형 진행
            _sideRollSpinLeft = duration;
            Debug.Log($"[134차 E5] 측면 롤 — 스핀+딥웅크림(0.65) duration={duration:F2} " +
                      $"{(isLeft ? "Left" : "Right")} " +
                      $"axis=({_sideRollSpinLocalAxis.x:F2},{_sideRollSpinLocalAxis.y:F2},{_sideRollSpinLocalAxis.z:F2})");
        }

        /// <summary>[134차 E-3] 스핀 적용 — 저장 localRotation × (부호×360°×진행률) 스핀쿼터니언 양산.</summary>
        private void ApplySideRollSpin(float t01)
        {
            if (_sideRollVisual == null) return;
            Quaternion spin = Quaternion.AngleAxis(_sideRollSpinSign * SideRollSpinDegrees * t01, _sideRollSpinLocalAxis);
            _sideRollVisual.localRotation = _sideRollSavedLocalRot * spin;
        }

        /// <summary>[134차 E-3] 스핀 타이머 틱 — duration 경과까지 선형 진행, 완료 시 저장값 복원
        /// (구르기 엣지 블록 직후 매 프레임 호출).</summary>
        private void TickSideRollSpin()
        {
            if (_sideRollVisual == null) return;
            if (_sideRollSpinLeft <= 0f) return;

            _sideRollSpinLeft -= Time.deltaTime;
            float t01 = 1f - Mathf.Clamp01(_sideRollSpinLeft / _sideRollDuration);
            ApplySideRollSpin(t01);
            if (_sideRollSpinLeft <= 0f) EndSideRollSpin();
        }

        /// <summary>[134차 E-3/E5] 스핀 원복 — 발화 시점 localRotation·localScale(딥웅크림 y+xz) 복원 + 상태 리셋
        /// (멱등 — 중복 호출 무해).</summary>
        private void EndSideRollSpin()
        {
            _sideRollSpinLeft = 0f;
            if (_sideRollVisual == null) return;
            _sideRollVisual.localRotation = _sideRollSavedLocalRot;   // 저장값 복원
            _sideRollVisual.localScale = _sideRollSavedLocalScale;    // [134차 E5] 딥웅크림 원복 — localScale(y/xz) 저장값 전체 복원
            _sideRollVisual = null;
        }

        /// <summary>플레이어 피격 확정 이벤트 핸들러 — 임팩트 FX + 데미지넘버 일괄 발화.</summary>
        private void OnPlayerDamagedFX(Vector3 pos, float amount)
        {
            try
            {
                // 몬스터 피격과 동일 경로(CombatFXGate): 히트 플래시+스파크+임팩트+출혈+데미지넘버+카메라 히트.
                // GameObject 오버로드 우선(플래시/데미지넘버 포함), 인스턴스 부재 시 위치 기반 오버로드 폴백.
                var ph = PlayerHealth.Instance;
                if (ph != null)
                    CombatFXGate.PlayHitFX(ph.gameObject, Vector3.back, CombatHitType.Organic, false, amount, Color.white);
                else
                    CombatFXGate.PlayHitFX(pos, Vector3.back, CombatHitType.Organic, false, amount, Color.white);
                Debug.Log($"[PlayerHit] 피격 임팩트 FX 발화 pos={pos} dmg={amount:F1}");
            }
            catch (System.Exception hitFxEx)
            {
                Debug.LogWarning($"[PlayerHit] 피격 임팩트 FX 실패(전투 계속): {hitFxEx.Message}");
            }
        }
        public void TriggerStun() { if (_anim != null) _anim.SetTrigger("Stun"); }
        public void TriggerKnockdown() { if (_anim != null) _anim.SetTrigger("Knockdown"); }
        public void TriggerBlock() { if (_anim != null) _anim.SetTrigger("Block"); }   // [무번호 87] 아레나 맨손 — 막기(병사 신규 FBX)

        // [무번호 86-P2] 승마 자세 — 기존 Sit 파이프라인(SitDown→SitHold→SitUp) 재용. 컨트롤러 무수정.
        /// <summary>탑승 중 앉기 홀드 플래그. _anim 부재(EditMode 등) 환경에서도 플래그만 전이한다.</summary>
        public bool IsMountedPose { get; private set; }
        public void SetMounted(bool mounted)
        {
            if (IsMountedPose == mounted) return;
            IsMountedPose = mounted;
            if (_anim == null) return;
            _anim.SetTrigger(mounted ? "SitDown" : "SitUp");
        }
        public void TriggerSitDown() { if (_anim != null) _anim.SetTrigger("SitDown"); }
        public void TriggerSitUp() { if (_anim != null) _anim.SetTrigger("SitUp"); }
        public void TriggerToss() { if (_anim != null) _anim.SetTrigger("Toss"); }
        public void TriggerDrink() { if (_anim != null) _anim.SetTrigger("Drink"); }
        public void TriggerLadder() { if (_anim != null) _anim.SetTrigger("Ladder"); }
        public void TriggerClimbStairs() { if (_anim != null) _anim.SetTrigger("ClimbStairs"); }
        public void TriggerWallDown() { if (_anim != null) _anim.SetTrigger("WallDown"); }
        public void TriggerThrow() { if (_anim != null) _anim.SetTrigger("Throw"); }
        public void TriggerCarry() { if (_anim != null) _anim.SetTrigger("Carry"); }
        public void TriggerCast() { if (_anim != null) _anim.SetTrigger("Cast"); }
        public void TriggerHarvestObject() { TriggerGather(); } // [129차 Q4] TriggerGather 위임
        public void TriggerHarvestPickUp() { TriggerGather(); } // [129차 Q4] TriggerGather 위임
        public void TriggerOpenDoor() { if (_anim != null) _anim.SetTrigger("OpenDoor"); }
        public void TriggerTalkA() { if (_anim != null) _anim.SetTrigger("Talk"); }
        public void TriggerTalkB() { if (_anim != null) _anim.SetTrigger("Talk2"); }
        public void TriggerVictory() { if (_anim != null) _anim.SetTrigger("Victory"); }

        // DD1: 애니 상태 진단 타임라인 (최초 600초, 주기 로그 + 상태 전환 즉시 로그)
        private float _diagStart = -1f;
        private float _nextDiagTime = 0f;
        private int _prevStateHash = -1;     // 직전 프레임 상태 hash (전환 감지용)
        private string _prevStateName = "?"; // 직전 상태 이름 (전환 로그 출력용)
        private bool _diagEndLogged;         // 600초 종료 로그 1회 여부
        private float _diagRawSpeed;         // 진단용 raw(스무딩 전) 속도

        // DD2: 뼈 변위 진단 — 2초 주기 스냅샷 비교로 뼈가 실제 움직이는지 수치 확정
        private Vector3 _hipsPosRef;         // 직전 주기의 Hips 위치
        private Quaternion _hipsRotRef;      // 직전 주기의 Hips 회전
        private bool _hipsRefValid;          // 첫 주기는 기준점 저장만 (Δ 계산 스킵)
        // DD3-2: 사지 뼈 변위 — LeftHand/LeftFoot의 Hips 기준 상대벡터(world) 스냅샷 비교
        private Vector3 _lhRelRef;           // 직전 주기 LeftHand의 Hips 기준 상대벡터
        private Vector3 _lfRelRef;           // 직전 주기 LeftFoot의 Hips 기준 상대벡터
        private bool _limbRefValid;          // 첫 주기는 기준 저장만 (Δ 계산 스킵)

        // DD4: 사지 로컬회전 Δ — localRotation 스냅샷 비교. 루트 모션/루트 회전에 완전 면역
        // (DD3-2의 world 상대벡터는 캐릭터가 회전만 해도 Δ가 발생하는 오염이 있음).
        private Quaternion _lfaLocalRef;     // 직전 주기 LeftLowerArm localRotation
        private Quaternion _lulLocalRef;     // 직전 주기 LeftUpperLeg localRotation
        private bool _limbLocalValid;        // 첫 주기는 기준 저장만 (Δ 계산 스킵)

        // Speed 지수 평활 + 멈춤 스냅 (지형/경사 충돌로 속도가 0 근처로 순간 떨어질 때
        // Idle로 떨어졌다 복귀하는 "끊김 + 멈춤 모션"을 방지)
        private float _smoothedSpeed;
        // 2026-09-10: 공격 애니 상태 최소 유지 — 연타 콤보 중 Idle 경유 팝 방지
        private float _attackHoldUntil;   // 이 시각까지 Speed 파라미터를 0으로 고정(공격 애니 보호)
        private float _stallTime;        // 정지 판정 홀드 타이머 — 0.25초 연속 정체 시에만 스냅
        private float _lastTarget;       // 직전 프레임 목표 속도 — 미세 정체 홀드 중 유지값
        // T2B-3(애니 측): 점프→착지 하강 에지 직후 Speed 목표 상승을 잠시 제한 —
        // Jump 종료 후 Walk/Run 복귀 시 클립 팝(급가속) 체감 완화. 블렌드 시간은
        // 애니메이터 컨트롤러(에셋)가 담당하므로 여기서는 속도 파라미터 램프만 완화한다.
        private float _landingSoftTimer;

        // [무번호 44-SPEAR-DIAG] 오프너 미해결 원인 관측 버퍼 — 클릭당 상한 6회, 0.4s 간격 샘플.
        private float _spearDiagNextLog;
        private int _spearDiagSamples;

        // [무번호 45-SPEAR-TIMEGATE] 오프너 히트 시간 게이트 — 상태 머신이 AttackThrust에 진입하지 않는
        // 실측(11-15 21:00 세션 [Spear] hit resolved 0건 + 45차 [44-SPEAR-DIAG] opener/exit/followup/idle
        // 전부 False·해시 불변·normT 루핑)에도 게임플레이 타격이 반드시 해결되도록 발화 시각+클립 길이로 판정.
        private float _spearOpenerResolveAtTime;
        private float _spearOpenerClipLength = -1f;
        private const float SpearOpenerClipLengthFallback = 1.2f;
        // [176차] [166-B] 스윙 2 자동 게이트 필드 2종 철수 — 2타는 2클릭 승인 경로(팔로업) 단일 소스
        // (사용자 지시 1클릭=1타·2클릭=2타 콤보 분리).
        // [무번호 84] 오프너 클립 교체 — 사용자 제공 "플레이어 창 휘두르기.fbx"(단일 테이크 Take 001, 3.77s)를
        //   Player_AC AttackThrust 모션으로 배선(배치 스크립트 SpearThrustClipWiring). 이 Unity 버전은
        //   clipAnimations 자동 생성이 없어 리네임 경로가 무효(11-20 실측) → 테이크명 원직 프래그먼트 계약.
        private const string SpearOpenerClipNameFragment = "Take 001";

        // [무번호 47-P1] 스윙 방출 상태 추적 — 안전왕치용(스윙 중 아크 원칙).
        private bool _swingEmitOn;
        private float _swingEmitSince;

        private void Start()
        {
            _anim = GetComponentInChildren<Animator>();
            if (_anim == null) _anim = GetComponent<Animator>();

            _cc = GetComponentInParent<CharacterController>();

            switch (mode)
            {
                case DriveMode.Player:
                    _movement = GetComponentInParent<PlayerMovement>();
                    _combat = GetComponentInParent<PlayerCombat>();
                    if (_combat != null)
                    {
                        _consumedAttackSequence = GetInitialConsumedAttackSequence(_combat.AcceptedAttackSequence);
                        _pendingAcceptedAttackInputs = 0;
                        Debug.Log($"[HumanoidClipDriver] Player sequence baseline={_consumedAttackSequence}; earlier fallback clicks will not replay");
                    }
                    break;
                case DriveMode.Soldier:
                    _lastPos = transform.position;
                    break;
            }

            // 이벤트 기반 플레이어 피격 임팩트 FX — PlayerHealth.OnPlayerDamaged 구독.
            // 기존 HP 폴링 엣지 PlayImpact는 제거(중복 방지). Player 모드 드라이버만 구독해
            // 다중 드라이버(Soldier 등) 환경에서 이중 발화를 차단. FX 실패는 전투 방해 금지 원칙대로 흡수.
            if (mode == DriveMode.Player)
                PlayerHealth.OnPlayerDamaged += OnPlayerDamagedFX;

            if (_anim != null) _anim.SetFloat("Speed", 0f);

            // 진단: 아바타/컨트롤러 상태 (T-pose 원인 판별용)
            var avatarInfo = (_anim != null && _anim.avatar != null)
                ? $"{_anim.avatar.name}:isValid={_anim.avatar.isValid}"
                : "NULL";
            var ctrlInfo = (_anim != null && _anim.runtimeAnimatorController != null)
                ? _anim.runtimeAnimatorController.name
                : "NULL";
            Debug.Log($"[HumanoidClipDriver] anim=OK avatar={avatarInfo} controller={ctrlInfo} mode={mode}");

            // DD1: 이중 Animator 감지 (1회) — 오작동/모션 미표시 원인 후보 파악
            var allAnims = GetComponentsInChildren<Animator>(true);
            if (allAnims != null && allAnims.Length > 0)
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < allAnims.Length; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(allAnims[i].gameObject.name);
                }
                Debug.Log($"[HumanoidClipDriver][Diag] 애니메이터 개수={allAnims.Length} → [{sb}]");
            }

            // DD2: 뼈 진단 — 아바타 유효성 / 렌더러(스키닝) 상태 / 루트 컴포넌트 구성
            // (Player_AC 재생 정상 + 뼈 무응답 원인 판별: 스키닝 렌더러 누락, 골격 덮어쓰기 컨트롤러 부착 여부 등)
            if (_anim != null)
            {
                bool avValid = _anim.avatar != null && _anim.avatar.isValid;
                bool avHuman = _anim.avatar != null && _anim.avatar.isHuman;
                Debug.Log($"[HumanoidClipDriver][DD2] avatar={(_anim.avatar != null ? _anim.avatar.name : "NULL")} isValid={avValid} isHuman={avHuman}");

                // PlayerBody(드라이버 자신=bodyF) 하위의 렌더러 수집 — SkinnedMesh가 뼈 스키닝의 최종 표현체
                var skinned = GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var meshRend = GetComponentsInChildren<MeshRenderer>(true);
                int smEnabled = 0;
                string smDetail = "없음";
                if (skinned != null && skinned.Length > 0)
                {
                    var sm0 = skinned[0];
                    smDetail = $"{sm0.gameObject.name}: rootBone={(sm0.rootBone != null ? sm0.rootBone.name : "NULL")}, bones={(sm0.bones != null ? sm0.bones.Length : 0)}, mesh={(sm0.sharedMesh != null ? sm0.sharedMesh.name : "NULL")}";
                    foreach (var s in skinned)
                        if (s != null && s.enabled) smEnabled++;
                    // DD3-3: SMR 본 동일성(1회) — sm0.bones 중 이 Animator(_anim.transform)의 자손이 아닌 본이
                    // 있으면 스키닝이 다른 골격을 따라가 애니 결과가 렌더에 반영되지 않는 원인이 된다.
                    int foreignBones = 0;
                    var sbF = new System.Text.StringBuilder();
                    if (sm0.bones != null)
                    {
                        for (int i = 0; i < sm0.bones.Length; i++)
                        {
                            var bb = sm0.bones[i];
                            if (bb == null || !bb.IsChildOf(_anim.transform))
                            {
                                foreignBones++;
                                if (foreignBones <= 3)
                                {
                                    if (foreignBones > 1) sbF.Append(", ");
                                    sbF.Append(bb != null ? bb.name : "NULL");
                                }
                            }
                        }
                    }
                    Debug.Log($"[HumanoidClipDriver][DD3-3] SMR본 외부골격={foreignBones}/{(sm0.bones != null ? sm0.bones.Length : 0)} 처음3개=[{sbF}]");
                }
                int mrEnabled = 0;
                foreach (var mr in meshRend)
                    if (mr != null && mr.enabled) mrEnabled++;
                int smCount = skinned != null ? skinned.Length : 0;
                int mrCount = meshRend != null ? meshRend.Length : 0;
                Debug.Log($"[HumanoidClipDriver][DD2] PlayerBody 렌더러: SkinnedMesh={smCount}(enabled={smEnabled}, {smDetail}), Mesh={mrCount}(enabled={mrEnabled})");

                // 루트(Player)에 붙은 골격 개입 컴포넌트 존재 여부 — 골격 덮어쓰기 용의 판별
                // [2026-09-22 뉴럴 애니 제거] Neural/Hybrid 카운트 퇴역 — Procedural/BoneMap/RigAnim만 점검.
                var root = _anim.transform.root;
                int hasProc = root.GetComponent<ProjectName.Systems.Animation.Procedural.ProceduralAnimationController>() != null ? 1 : 0;
                int hasBoneMap = root.GetComponent<ProjectName.Systems.Animation.Procedural.Bones.ProceduralBoneMap>() != null ? 1 : 0;
                int hasRigAnim = root.GetComponent<RigAnimationController>() != null ? 1 : 0;
                var rootAnims = root.GetComponentsInChildren<Animator>(true);
                var sbA = new System.Text.StringBuilder();
                if (rootAnims != null)
                    for (int i = 0; i < rootAnims.Length; i++)
                    {
                        if (i > 0) sbA.Append(", ");
                        sbA.Append(rootAnims[i].gameObject.name);
                    }
                Debug.Log($"[HumanoidClipDriver][DD2] 루트 컴포넌트: Procedural={hasProc} BoneMap={hasBoneMap} RigAnim={hasRigAnim} Animator(루트포함전체)=[{sbA}]");
            }
            // DD3-1: 아바타 매핑 덤프(1회) — humanDescription.human 비어있음 가설 검증.
            // 매핑 본수=0이면 휴머노이드 리타깃이 전혀 안 되어 클립 재생(normT 진행)과 무관하게 포즈가 동결한다.
            if (_anim != null && _anim.avatar != null)
            {
                try
                {
                    var humanMap = _anim.avatar.humanDescription.human;
                    var sbM = new System.Text.StringBuilder();
                    for (int i = 0; i < humanMap.Length; i++)
                    {
                        if (i > 0) sbM.Append(", ");
                        sbM.Append($"{humanMap[i].boneName}→{humanMap[i].humanName}");
                    }
                    Debug.Log($"[HumanoidClipDriver][DD3-1] 매핑 본수={humanMap.Length} [{sbM}]");
                }
                catch (System.Exception mapEx)
                {
                    Debug.Log($"[HumanoidClipDriver][DD3-1] humanDescription 접근 실패: {mapEx.GetType().Name}: {mapEx.Message}");
                }
                // 둘 다 확정: HumanBodyBones 0~54를 GetBoneTransform으로 훑어 non-null 개수 확인
                try
                {
                    int mappedCount = 0;
                    for (int b = 0; b < (int)HumanBodyBones.LastBone; b++)
                        if (_anim.GetBoneTransform((HumanBodyBones)b) != null) mappedCount++;
                    Debug.Log($"[HumanoidClipDriver][DD3-1] 실질매핑={mappedCount}/55");
                }
                catch (System.Exception boneEx)
                {
                    Debug.Log($"[HumanoidClipDriver][DD3-1] GetBoneTransform 스캔 실패: {boneEx.GetType().Name}: {boneEx.Message}");
                }
            }
            else
            {
                Debug.Log("[HumanoidClipDriver][DD3-1] avatar=NULL — 매핑 덤프 불가");
            }
        }

        private void Update()
        {
            if (_anim == null) return;

            switch (mode)
            {
                case DriveMode.Player:
                    UpdatePlayer();
                    break;
                case DriveMode.Soldier:
                    UpdateSoldier();
                    break;
            }
        }

        // ───────────────────── Player 모드 ─────────────────────
        private int GetCurrentStageClip(AnimatorStateInfo state)
        {
            for (int i = 0; i < StageStateNames.Length; i++)
                if (state.IsName(StageStateNames[i])) return i + 1;
            return 0;
        }

        private static string GetStageStateName(int stage)
        {
            return StageStateNames[Mathf.Clamp(stage, 1, MaxMeleeComboClicks) - 1];
        }

        private static bool TryResolveStageHit(int stage, float normalizedTime, bool[] alreadyResolved)
        {
            if (stage < 1 || stage > MaxMeleeComboClicks || alreadyResolved == null
                || alreadyResolved.Length < MaxMeleeComboClicks) return false;
            int index = stage - 1;
            if (alreadyResolved[index] || normalizedTime < StageStrikeSyncNormT) return false;
            alreadyResolved[index] = true;
            return true;
        }

        private static bool TryResolveMeleeComboHit(int segment, float normalizedTime, bool[] alreadyResolved)
        {
            if (segment < 1 || segment > MaxMeleeComboClicks || alreadyResolved == null
                || alreadyResolved.Length < MaxMeleeComboClicks) return false;
            int index = segment - 1;
            if (alreadyResolved[index] || normalizedTime < GetMeleeComboStrikeNormalizedTime(segment)) return false;
            alreadyResolved[index] = true;
            return true;
        }

        private static bool IsMeleeAttackStateName(string stateName)
        {
            return stateName == "AttackThrust" || stateName == MeleeComboStateName
                || stateName == "Attack" || stateName == "AttackBase"
                || stateName == "AttackCombo" || stateName == "AttackCombo2" || stateName == "AttackCombo3";
        }

        private static bool IsMeleeWeaponType(WeaponType weaponType)
        {
            return weaponType == WeaponType.Fist || weaponType == WeaponType.Sword
                || weaponType == WeaponType.Spear;
        }

        private static bool IsMeleeAttackAnimatorState(AnimatorStateInfo state)
        {
            return state.IsName("AttackThrust") || state.IsName("WeaponCombo")
                || state.IsName("Attack") || state.IsName("AttackBase")
                || state.IsName("AttackCombo") || state.IsName("AttackCombo2") || state.IsName("AttackCombo3");
        }

        private static readonly int RollStateShortNameHash = Animator.StringToHash("Roll");
        // [132차] 방향 구르기 상태 3종 — Roll과 동일한 HitLight 면책 계약(IsHigherPriorityHitLightState 포함)
        private static readonly int RollBackStateShortNameHash = Animator.StringToHash("RollBack");
        private static readonly int RollSideLStateShortNameHash = Animator.StringToHash("RollSideL");
        private static readonly int RollSideRStateShortNameHash = Animator.StringToHash("RollSideR");
        private static readonly int ParryStateShortNameHash = Animator.StringToHash("Parry");
        private static readonly int StunStateShortNameHash = Animator.StringToHash("Stun");
        private static readonly int DeathStateShortNameHash = Animator.StringToHash("Death");
        private static readonly int KnockdownStateShortNameHash = Animator.StringToHash("Knockdown");
        private static readonly int HitLightStateShortNameHash = Animator.StringToHash("HitLight");

        private static bool ShouldDeferHitLight(bool currentStateIsMelee, bool isTransitioning, bool nextStateIsMelee)
        {
            // Keep protection across a melee state handoff, including AttackThrust → WeaponCombo.
            return currentStateIsMelee || (isTransitioning && nextStateIsMelee);
        }

        private static bool IsHigherPriorityHitLightState(int stateShortNameHash)
        {
            return stateShortNameHash == RollStateShortNameHash
                || stateShortNameHash == RollBackStateShortNameHash
                || stateShortNameHash == RollSideLStateShortNameHash
                || stateShortNameHash == RollSideRStateShortNameHash
                || stateShortNameHash == ParryStateShortNameHash
                || stateShortNameHash == StunStateShortNameHash
                || stateShortNameHash == DeathStateShortNameHash
                || stateShortNameHash == KnockdownStateShortNameHash;
        }

        private static bool IsHitLightState(int stateShortNameHash)
        {
            return stateShortNameHash == HitLightStateShortNameHash;
        }

        private static bool ShouldDiscardPendingHitLight(int currentStateShortNameHash,
            bool isTransitioning, int nextStateShortNameHash)
        {
            // AnyState triggers can override these states. Also drop a newly observed HP edge while
            // HitLight is already active/entering, rather than retriggering it into a loop.
            return IsHigherPriorityHitLightState(currentStateShortNameHash)
                || (isTransitioning && IsHigherPriorityHitLightState(nextStateShortNameHash))
                || IsHitLightState(currentStateShortNameHash)
                || (isTransitioning && IsHitLightState(nextStateShortNameHash));
        }

        private static bool ShouldDeferHitLightForMeleeIntent(bool currentStateIsMelee,
            bool isTransitioning, bool nextStateIsMelee, int comboStage,
            int spearAcceptedClicks, bool pendingAcceptedMeleeRoute)
        {
            // Accepted Bow inputs are excluded by HasPendingAcceptedMeleeRoute; ArcheryShot itself
            // is not considered a melee Animator state above.
            return ShouldDeferHitLight(currentStateIsMelee, isTransitioning, nextStateIsMelee)
                || comboStage > 0 || spearAcceptedClicks > 0 || pendingAcceptedMeleeRoute;
        }

        private static bool HasPendingAcceptedMeleeRoute(WeaponType currentWeaponType,
            bool acceptedMeleeDispatchThisUpdate)
        {
            // This one-update intent is set only by dispatch's selected Fist/Sword/Spear branch.
            // AcceptedAttackSequence has no per-input weapon association, so pending deltas are
            // classified at dispatch time; Bow deltas never become melee intent. Combo/Spear
            // logical state protects later frames after this transient marker is cleared.
            return IsMeleeWeaponType(currentWeaponType) && acceptedMeleeDispatchThisUpdate;
        }

        private static bool ConsumePendingHitReaction(ref bool pending, bool committedMeleeAttack,
            bool discardForPriorityState)
        {
            if (!pending) return false;
            if (discardForPriorityState)
            {
                pending = false;
                return false;
            }
            if (committedMeleeAttack) return false;
            pending = false;
            return true;
        }

        private void UpdatePlayer()
        {
            // Per-update dispatch evidence must not survive a drained sequence or route change.
            _acceptedMeleeRouteDispatchedThisUpdate = false;
            // [2026-09-25 프리미엄 A/B] 인스펙터 체크박스 + F6 핫키 토글 — 켠 즉시 스윙에서 실제 블레이드 궤적 3D 스윕 리본 구동.
            if (Input.GetKeyDown(KeyCode.F6))
            {
                _premiumArcEnabled = !_premiumArcEnabled;
                UnityEngine.Debug.Log("[AttackArcVFX] F6 토글 → " + (_premiumArcEnabled ? "활성 (3D 스윕 리본)" : "비활성 (무기 트레일만)"));
            }
            AttackArcVFX.PremiumArcEnabled = _premiumArcEnabled;
            if (_diagStart < 0f) _diagStart = Time.time;
            bool diagActive = Time.time - _diagStart <= 600f;

            // Speed — CharacterController 수평 속도 크기 (지수 평활로 끊김 제거)
            float raw = 0f;
            if (_cc != null)
            {
                var v = _cc.velocity;
                v.y = 0f;
                raw = v.magnitude;
            }
            _diagRawSpeed = raw; // DD1: 스무딩 전 속도 — 스냅 로직 오작동 구분용

            // 측정 속도 추적(실측 확정: 일반 이동 5.0 / 대시 15.0) — raw를 그대로 목표로 사용하면
            // Run 클립이 실제 이동속도와 매칭된다(발 미끄러짐 방지는 Run 상태 speed 스케일링 0.2 담당).
            float target;
            if (raw > 0.05f)
            {
                _stallTime = 0f;
                target = raw;                       // 실측 속도 추적 — Run 클립이 실제 이동속도와 매칭
            }
            else
            {
                _stallTime += Time.deltaTime;
                target = _stallTime >= 0.25f ? 0f : _lastTarget; // 미세 정체 홀드
            }
            _lastTarget = target;
            // T2B-3: 착지 직후 0.22초간 Speed 목표를 "현재 평활값 + 1.5" 이하로 제한 —
            // Walk/Run 클립 복귀 시 파라미터 급등에 의한 클립 팝을 완화 (지수 평활과 합쳐 자연 흡수)
            if (_landingSoftTimer > 0f)
            {
                _landingSoftTimer -= Time.deltaTime;
                target = Mathf.Min(target, _smoothedSpeed + 1.5f);
                _lastTarget = target;
            }
            float k = 1f - Mathf.Exp(-10f * Time.deltaTime);
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, target, k);
            // 2026-09-10: 공격 애니 보호 — 공격 상태 유지 시간(_attackHoldUntil) 내엔 Speed를 0으로 고정해
            // Speed 조건 전이(Idle/Walk)가 공격 애니를 인터럽트하지 않게 한다.
            bool inAttackHold = Time.time < _attackHoldUntil;
            _anim.SetFloat("Speed", inAttackHold ? 0f : _smoothedSpeed);
            // T-D3 이동 파라미터 확장: 로컬 이동 벡터 → MoveX(측면)/MoveY(전후) — 후진/선회 상태 전환용
            if (_movement != null)
            {
                Vector3 lmv = _movement.LocalMoveDirection;
                _anim.SetFloat("MoveX", lmv.x);
                _anim.SetFloat("MoveY", lmv.z);
            }

            // T-D3+: 웅크림/수영 상태 피드(QA 발견 누락 보강) — CrouchF/B/L/R·SwimI/F 진입 조건
            _anim.SetBool("IsCrouch", _movement != null && _movement.IsCrouching);
            _anim.SetBool("IsSwimming", _movement != null && _movement.IsSwimming);

            // 2026-09-09: 은신 피드 추가 — 기존엔 Sneaky 상태로 가는 전이 조건이 전혀 공급되지 않아 은신 애니 미발동
            var stealth = StealthSystem.Instance;
            _anim.SetBool("IsStealthed", stealth != null && stealth.IsStealthed);

            // 피격 애니 — PlayerHealth HP 감소 엣지 감시. 감소 순간 HitLight 트리거(Any State 전이) 발화.
            // 죽음(HP 0 도달/IsDead)은 Death 경로(TriggerDeath)가 담당하므로 여기서는 발화하지 않는다.
            var ph = PlayerHealth.Instance;
            if (ph != null)
            {
                float hp = ph.CurrentHP;
                if (_prevPlayerHP >= 0f && hp < _prevPlayerHP - 0.001f && !ph.IsDead)
                {
                    _pendingPlayerHitLight = true;
                    // 임팩트 FX는 PlayerHealth.OnPlayerDamaged 이벤트(Start 구독)로 이전 —
                    // 폴링 엣지 PlayImpact는 제거(회복 동시 발생 엣지 흡수/드라이버 부재 누락 등 결함 차단 + 중복 발화 방지).
                }
                if (ph.IsDead) _pendingPlayerHitLight = false;
                _prevPlayerHP = hp;
            }

            // M2 정식 장착 연동: CurrentType 기반 게이트(핫바 장착이 유일한 전환 경로)
            var wtype = WeaponEquipManager.CurrentType;
            bool throwing = PlayerWeaponModeBridge.ThrowSelected;
            if (wtype != _prevWType)
            {
                // [TEST25-66차] 전환 실측 로그 — 활 장착 시 BowEnter 트리거 발화 증거 고정
                // (Player_AC에 BowEnter 전이 존재 확인: m_ConditionEvent: BowEnter, HasExitTime=0 → 도달 가능).
                Debug.Log($"[Anim] 무기 타입 전환 {_prevWType} → {wtype}"
                    + (wtype == WeaponType.Bow ? " — BowEnter 트리거 발화(활 스탠스 전이)"
                    : wtype == WeaponType.Spear ? " — SpearEnter 트리거 발화" : ""));
                if (wtype == WeaponType.Bow) _anim.SetTrigger("BowEnter");
                if (wtype == WeaponType.Spear) _anim.SetTrigger("SpearEnter");
            }
            _prevWType = wtype;
            if (throwing && !_prevThrow) _anim.SetTrigger("ThrowEnter");
            _prevThrow = throwing;
            _anim.SetBool("IsCombat", wtype != WeaponType.Fist);
            _anim.SetBool("IsBow", wtype == WeaponType.Bow);
            _anim.SetBool("IsSpear", wtype == WeaponType.Spear);
            _anim.SetBool("IsThrowing", throwing);

            // DD1: 상태 전환 즉시 로그 — Idle ↔ Walk(걷기) 전환 발생 여부 결정적 증거
            if (diagActive) LogStateTransition();

            // DD1: 600초간 2초 간격 주기 — 실제 재생 상태(state/normT/speed)와 컨트롤러 속도 진단
            if (diagActive && Time.time >= _nextDiagTime)
            {
                _nextDiagTime = Time.time + 2f;
                LogAnimDiagnostic();
            }

            // DD1: 진단 기간 종료 — 스팸 방지를 위해 주기/전환 로그 중단
            if (!diagActive && !_diagEndLogged)
            {
                _diagEndLogged = true;
                Debug.Log($"[HumanoidClipDriver][Diag] 진단 기간 600초 종료 — 주기/전환 로그 중단 (state={ResolveStateName(_anim.GetCurrentAnimatorStateInfo(0))})");
            }

            // 공격 상태 감시 — Attack*/stage clip 진입 중 Speed 0 고정(Idle/Walk 인터럽트 차단)
            if (_anim != null)
            {
                var stInfo = _anim.GetCurrentAnimatorStateInfo(0);
                // Registered combo states and legacy attacks are protected from Speed transitions.
                bool attackStateHold = stInfo.IsName("Attack") || stInfo.IsName("AttackBase") || stInfo.IsName("AttackThrust")
                    || stInfo.IsName(MeleeComboStateName) || stInfo.IsName("AttackCombo")
                    || stInfo.IsName("AttackCombo2") || stInfo.IsName("AttackCombo3") || stInfo.IsName("ArcheryShot");
                if (attackStateHold)
                {
                    // 공격 애니 재생 중: Speed 파라미터를 0으로 고정 — Idle/Walk로 가는 Speed 조건 전이 불발
                    _anim.SetFloat("Speed", 0f);
                    // Protect each registered stage from Speed-driven Idle/Walk transitions.
                    if (_comboStage > 0 && GetCurrentStageClip(stInfo) == _comboStage)
                        _attackHoldUntil = Mathf.Max(_attackHoldUntil, Time.time + 0.35f);
                }
            }
            if (_combat != null)
            {
                _pendingAcceptedAttackInputs += ConsumeAttackSequenceDelta(_combat.AcceptedAttackSequence, ref _consumedAttackSequence);
                long attackDelta = _pendingAcceptedAttackInputs;
                for (long acceptedInput = 0; acceptedInput < attackDelta; acceptedInput++)
                {
                    // P4: 무기 타입별 공격 애니 분기 — Bow/Spear는 WeaponCombo(근접 3연타)에 진입 금지.
                    // Bow: ArcheryShot만 트리거(화살 소모/발사는 PlayerCombat이 ArrowManager로 담당).
                    // Spear: 레거시 Attack 트리거(AttackThrust 찌르기 경로).
                    // Fist/Sword: one accepted click starts or authorizes a segment of WeaponCombo.
                    var curWType = WeaponEquipManager.CurrentType;
                    if (curWType == WeaponType.Bow)
                    {
                        _anim.SetTrigger("ArcheryShot");
                        _attackHoldUntil = Time.time + 0.6f;   // 발사 중 Speed 0 홀드
                        _pendingAcceptedAttackInputs--;
                        Debug.Log("[Combo] 활 발사 — WeaponCombo 미진행 (ArcheryShot)");
                    }
                    else if (curWType == WeaponType.Spear)
                    {
                        // Compare the accepted event time, not this delayed dispatch frame. A timely
                        // buffered follow-up remains a follow-up even if the driver updates after expiry.
                        float acceptedInputTime = _combat != null ? _combat.LastAttackTime : Time.time;
                        bool expiredSpearRestart = IsSpearSequenceExpired(_spearAcceptedClicks > 0,
                            _spearOpenerAcceptedTime, acceptedInputTime, SpearFollowupClickWindow);   // [205차 배치B-1] 쿨다운(1.5s) 개시 후 클릭2 = 만료 아님 → 팔로업 승인 경로 도달
                        if (expiredSpearRestart)
                        {
                            // AttackThrust disallows a self-transition; explicitly replay its opener.
                            ResetSpearSequence();
                            StartSpearOpener(true);
                        }
                        else if (_spearAcceptedClicks == 0)
                        {
                            StartSpearOpener(false);
                        }
                        else if (ShouldAuthorizeSpearFollowupAtDispatch(_spearFollowupAuthorized,
                            _spearAcceptedClicks))
                        {
                            // PlayerCombat already authorized this accepted sequence delta. Do not
                            // re-check its click window while dispatching the buffered delta.
                            _spearAcceptedClicks = 2;
                            _spearOpenerAcceptedTime = Time.time; // restart cooldown window from the latest accepted stage
                            _spearFollowupAuthorized = true;
                            _spearFollowupHitResolved = false;
                            _attackHoldUntil = Time.time + SpearFollowupClickWindow;   // [205차 배치B-1] 팔로업(WeaponCombo×3속도 ≈1.0s) 전 구간 Speed 0 홀드
                        }
                        else
                        {
                            // Defensive recovery for stale or inconsistent buffered sequence state.
                            ResetSpearSequence();
                            StartSpearOpener(true);
                        }
                        _spearStateTransitionTime = Time.time;
                        _pendingAcceptedAttackInputs--;
                        _acceptedMeleeRouteDispatchedThisUpdate = true;
                        Debug.Log($"[Combo] Spear segment accepted={_spearAcceptedClicks}; opener=AttackThrust, follow-up=WeaponCombo");
                    }
                    else
                    {
                        // PlayerCombat applies its ordinary fallback when the Animator is unavailable.
                        if ((curWType == WeaponType.Fist || curWType == WeaponType.Sword) && !CanPlayPlayerMeleeCombo)
                        {
                            _pendingAcceptedAttackInputs--;
                            Debug.Log("[Melee] Animator unavailable — one-shot skipped (PlayerCombat fallback retained)");
                        }
                        else
                        {
                            if (!AcceptPlayerMeleeComboClick()) break;
                            _acceptedMeleeRouteDispatchedThisUpdate = true;
                        }
                    }
                }
            }

            if (_comboStage == 0 && _pendingAcceptedAttackInputs > 0
                && (WeaponEquipManager.CurrentType == WeaponType.Sword || WeaponEquipManager.CurrentType == WeaponType.Fist))
            {
                while (_pendingAcceptedAttackInputs > 0 && AcceptPlayerMeleeComboClick())
                    _acceptedMeleeRouteDispatchedThisUpdate = true;
            }

            // HitLight is full-body AnyState. Consume it only after accepted attack dispatch has
            // established logical melee commitment; the Animator transition can still be pending.
            AnimatorStateInfo hitCurrent = _anim.GetCurrentAnimatorStateInfo(0);
            bool hitTransitioning = _anim.IsInTransition(0);
            AnimatorStateInfo hitNext = hitTransitioning ? _anim.GetNextAnimatorStateInfo(0) : default(AnimatorStateInfo);
            bool meleeAttackCommitted = ShouldDeferHitLightForMeleeIntent(
                IsMeleeAttackAnimatorState(hitCurrent), hitTransitioning,
                hitTransitioning && IsMeleeAttackAnimatorState(hitNext), _comboStage,
                _spearAcceptedClicks,
                HasPendingAcceptedMeleeRoute(WeaponEquipManager.CurrentType, _acceptedMeleeRouteDispatchedThisUpdate));
            bool discardPendingHitLight = ShouldDiscardPendingHitLight(
                hitCurrent.shortNameHash, hitTransitioning,
                hitTransitioning ? hitNext.shortNameHash : 0);
            if (ConsumePendingHitReaction(ref _pendingPlayerHitLight, meleeAttackCommitted, discardPendingHitLight))
                _anim.SetTrigger("HitLight");   // 피격 애니 — AnyState 트리거, deferred once

            // The single Weapon_Combo_2 clip is segmented/held by click authorization.
            var st = _anim.GetCurrentAnimatorStateInfo(0);
            MonitorSpearSequence(st);
            MonitorMeleeComboClip();
            WatchSwingEmitDeadline();   // [무번호 47-P1] 스윙 방출 상한 감시 — 아크 잔존 원천 차단

            // ── 레거시 Attack* 상태 보존(기존 경로: TriggerAttack/창 등) — 상태 진입 1회 전방 슬래시 ──
            bool legacyAttack = st.IsName("Attack") || st.IsName("AttackBase") || st.IsName("AttackThrust");
            if (legacyAttack)
            {
                int atkHash = st.fullPathHash;
                if (atkHash != _prevAttackStateHash)
                {
                    _prevAttackStateHash = atkHash;
                    // [166-B] 상태 진입/전환 시 스윙별 해결 플래그 리셋(기존 _legacyImpactFired 1회 리셋 대체)
                    for (int i = 0; i < _legacySwingResolved.Length; i++) _legacySwingResolved[i] = false;
                }

                // [166-B] 스윙별 멀티임팩트 — 무기별 스윙 임팩트 시간 테이블(스피어 초기값 2회: 0.30/0.75,
                // 그립테이블 패턴 준수; 미등록 무기는 폴백 0.50 단일 시점 유지). 트레일/Slash VFX 발화를
                // 스윙 정점마다로 분리(기존 0.50 1회 → 스윙별 1회). 판정은 AttackThrust(스피어 오프너)의 경우
                // MonitorSpearSequence가 스윙별 소유(ResolveComboSwingHit 재사용 — 계획 §B-2) — 이중 발화 차단.
                int legacySwings = Mathf.Min(GetLegacySwingImpactCount(WeaponEquipManager.CurrentType),
                    _legacySwingResolved.Length);
                for (int swing = 0; swing < legacySwings; swing++)
                {
                    if (_legacySwingResolved[swing]) continue;
                    if (st.normalizedTime < GetLegacySwingImpactNormT(WeaponEquipManager.CurrentType, swing)) continue;
                    _legacySwingResolved[swing] = true;
                    if (st.IsName(SpearOpenerStateName)) continue;   // 오프너는 MonitorSpearSequence 전담 — 이중 방출 차단
                    // [2026-09-12 P2 스윙 트레일 전환] 유지 — 무기 팁 WeaponSwingTrail 궤적 잔상 +
                    // [166-C] Slash VFX 스윙 1회당 1장(스윙 정점 발화). 상태 탈출(else 엣지)에서 방출 종료.
                    FireComboTrail(swing + 1);
                    FireComboSlash(swing + 1);
                    Debug.Log($"[Combo] 레거시 Attack* 스윙별 발화 (swing={swing + 1}, normT>={GetLegacySwingImpactNormT(WeaponEquipManager.CurrentType, swing):F2})");
                }
            }
            else
            {
                if (_prevAttackStateHash != -1)
                {
                    // [2026-09-12 P2] 레거시 Attack* 탈출 엣지에서만 OFF — 무조건 OFF하면 WeaponCombo 진행
                    // 프레임(레거시 상태 아님)마다 방출이 강제 종료된다(콤보 방출은 EndCombo/인터럽트가 담당).
                    SetSwingTrailEmitting(false);
                }
                _prevAttackStateHash = -1;
            }

            // 구르기 — 상승엣지 1회 ([132차] LastRollKind 방향 분기 / [134차 E4] 측면은 롤클립 미재생 —
            //   RollSideL·RollSideR 트리거는 미발화(미사용 경로 — Player_AC 상태/트리거는 존중 유지, 삭제 별도 승인).
            //   앞/뒤 롤(Roll/RollBack)은 기존 롤클립+기존 경로 무수정.)
            bool rolling = _movement != null && _movement.IsRolling;
            if (rolling && !_prevRolling)
            {
                switch (_movement.LastRollKind)
                {
                    case RollKind.Back:  _anim.SetTrigger("RollBack"); break;
                    case RollKind.Left:  BeginSideRollSpin(true); break;   // [134차 E4] SetTrigger("RollSideL") 미발화(클립 미재생) — 스핀+웅크림 연출만(현재 상태 유지)
                    case RollKind.Right: BeginSideRollSpin(false); break;  // [134차 E4] SetTrigger("RollSideR") 미발화(클립 미재생) — 스핀+웅크림 연출만(현재 상태 유지)
                    default:             _anim.SetTrigger("Roll"); break; // Forward(기본) — [134차 E4] 앞/뒤 롤 무수정
                }
            }
            _prevRolling = rolling;
            TickSideRollSpin();  // [134차 E-3] 스핀 타이머 틱 — 롤 지속 경과 후 원복

            // 점프 — 상승엣지 1회 (후진 입력 중 점프면 Back_Jump)
            bool jumping = _movement != null && _movement.IsJumping;
            if (jumping && !_prevJumping)
            {
                bool backJump = _movement.LocalMoveDirection.z < -0.2f;
                _anim.SetTrigger(backJump ? "JumpBack" : "Jump");
            }

            // M3/M4: 무기 모드 입력 — 폭탄 선택 중 좌클릭(던지기: Bomb 스폰)만 처리.
            // 활(Bow) 발사는 PlayerCombat의 좌클릭 TryBowShot 단일 경로로 통일 — 우클릭 활 발사 경로 제거(2026-09-16).
            if (_movement != null && _anim != null)
            {
                if (PlayerWeaponModeBridge.ThrowSelected && Input.GetMouseButtonDown(0))
                {
                    bool pitch = Random.Range(0, 2) == 0;
                    _anim.SetTrigger(pitch ? "ThrowPitch" : "Throw");
                    var bombPrefab = Resources.Load<GameObject>("Bombs/Bomb_Explosive");
                    if (bombPrefab != null)
                    {
                        var go = Object.Instantiate(bombPrefab, _anim.transform.position + Vector3.up * 1.2f + _anim.transform.forward * 0.4f, Quaternion.identity);
                        var rb = go.GetComponent<Rigidbody>();
                        if (rb != null) rb.linearVelocity = _anim.transform.forward * 8f + Vector3.up * 5f;
                    }
                    PlayerWeaponModeBridge.ThrowSelected = false; // 1회 투척 후 해제
                }
            }
            // T2B-3: 착지(하강 에지) 직후 짧은 흡수 창 — Speed 급상승 제한은 아래 목표 계산에서 적용
            if (!jumping && _prevJumping) _landingSoftTimer = 0.22f;
            _prevJumping = jumping;

            // T-D3: Run→Walk 전환 연출 — Speed가 2.0 임계를 아래로 하향 통과할 때 1회
            // 2026-09-10: 공격 애니 보호 — _attackHoldUntil 유지 중엔 RunToWalk 트리거 발화 억제.
            // (RunToWalk는 AnyState 전이라 공격 애니 중에도 Run_to_Walk_Transition으로 인터럽트함 — normT 5% 팝 실증)
            if (_prevSpeedForTransition > 2f && _smoothedSpeed <= 2f && _anim != null
                && Time.time >= _attackHoldUntil)
                _anim.SetTrigger("RunToWalk");
            _prevSpeedForTransition = _smoothedSpeed;
        }

        private static long GetInitialConsumedAttackSequence(long acceptedAttackSequence)
        {
            return acceptedAttackSequence > 0 ? acceptedAttackSequence : 0L;
        }

        private void MonitorSpearSequence(AnimatorStateInfo state)
        {
            if (_spearAcceptedClicks == 0) return;

            bool isOpenerState = state.IsName(SpearOpenerStateName);
            bool isFollowupState = state.IsName(MeleeComboStateName);
            bool isOpenerExitState = state.IsName(SpearOpenerExitStateName);

            // [무번호 44-SPEAR-DIAG] 오프너 미해결 상태 관측 — 11-15 21:00 세션에서 "[Combo] Spear segment
            // accepted"는 2건 기록됐는데 "[Spear] AttackThrust hit resolved"가 0건(몬스터 무피해·전리품 부재 직결).
            // 상태 머신이 AttackThrust에 실제 진입했는지/마커(0.50)까지 진행했는지를 클릭 후 2.4s 동안 샘플한다.
            // 판별 축: ①isOpener=false 지속 = _anim.Play가 상태 진입에 실패(컨트롤러/상태명/레이어 불일치)
            // ②isOpener=true인데 normT 정체 = 상태 재시작 루프 ③isOpener=true+진행 정상 = 마커 판정식 문제.
            bool isIdleState = state.IsName(SpearOpenerExitStateName);

            // [무번호 45-SPEAR-TIMEGATE] 시간 게이트 히트 해결 — 상태 진입 실패(opener:False 지속, 45차 실측)와
            // 무관하게 발화 시각+오프너 클립 길이×마커(0.50)로 타격을 반드시 해결한다. 상태 기반 해결(아래)은
            // 정상 세션의 조기 해결용으로 병행 유지 — 어느 쪽이 먼저 와도 resolved 플래그가 이중 발화를 차단.
            if (!_spearOpenerHitResolved && Time.time >= _spearOpenerResolveAtTime)
            {
                _spearOpenerHitResolved = true;
                FireComboTrail(1);
                FireComboSlash(1);   // [176차] 스윙1 슬래시 복원(사용자 정정 — 창은 찌르기가 없고 2스윙 콤보) — [166-C] 스윙별 1회 발화 계약 유지
                _combat?.ResolveComboSwingHit(1);
                Debug.Log($"[Spear] AttackThrust hit resolved (time-gate) expect={_spearOpenerResolveAtTime:F2} " +
                    $"stateHash={state.fullPathHash} clipLen={ResolveSpearOpenerClipLength():F2}");
            }

            // [무번호 44-SPEAR-DIAG] 오프너 미해결 상태 관측 — 11-15 21:00 세션에서 "[Combo] Spear segment
            // accepted"는 2건 기록됐는데 "[Spear] AttackThrust hit resolved"가 0건(몬스터 무피해·전리품 부재 직결).
            // 상태 머신이 AttackThrust에 실제 진입했는지/마커(0.50)까지 진행했는지를 클릭 후 2.4s 동안 샘플한다.
            // 45차 확정: opener/exit/followup/idle 전부 False + 해시 불변 + normT 루핑 → 상태 미진입. 근원 식별을
            // 위해 실제 재생 클립명과 컨트롤러명을 동반 기록한다(시간 게이트가 게임플레이를 담당하므로 관측 전용).
            bool isIdleStateDiag = isIdleState;
            if (!_spearOpenerHitResolved && Time.unscaledTime >= _spearDiagNextLog && _spearDiagSamples < 6)
            {
                _spearDiagNextLog = Time.unscaledTime + 0.4f;
                _spearDiagSamples++;
                float normT = Mathf.Max(0f, state.normalizedTime);
                string clipName = "?";
                string ctrlName = "?";
                if (_anim != null)
                {
                    var clipInfos = _anim.GetCurrentAnimatorClipInfo(0);
                    if (clipInfos != null && clipInfos.Length > 0 && clipInfos[0].clip != null)
                        clipName = clipInfos[0].clip.name;
                    var rc = _anim.runtimeAnimatorController;
                    ctrlName = rc != null ? rc.name : "null";
                }
                Debug.Log($"[44-SPEAR-DIAG] opener 미해결 감시 #{_spearDiagSamples} — " +
                    $"state=opener:{isOpenerState}/exit:{isOpenerExitState}/followup:{isFollowupState}/idle:{isIdleStateDiag} " +
                    $"normT={normT:F2} marker={SpearOpenerStrikeNormT:F2} hash={state.fullPathHash} " +
                    $"clip={clipName} ctrl={ctrlName} " +
                    $"transitioning={(_anim != null && _anim.IsInTransition(0))} accepted={_spearAcceptedClicks}");
            }
            bool recentlyDispatched = Time.time - _spearStateTransitionTime <= 0.15f;

            // Follow-up approval is retained until the authored opener reaches its strike marker.
            // At clip exit, jump to the registered combo clip's segment two; do not expect a
            // follow-up input to retrigger AttackThrust or let the controller route it to Idle.
            if (_spearFollowupAuthorized && !_spearFollowupStarted && _spearOpenerHitResolved
                && (isOpenerState || isOpenerExitState))
            {
                _spearFollowupStarted = true;
                _spearStateTransitionTime = Time.time;
                _anim.Play(MeleeComboStateName, 0, GetSpearFollowupStartNormalizedTime());
                Debug.Log("[Spear] AttackThrust exit → WeaponCombo stage 2");
                return;
            }

            if (!recentlyDispatched && ShouldResetInterruptedSpearSequence(_spearAcceptedClicks > 0,
                _spearOpenerHitResolved, _spearFollowupAuthorized, _spearFollowupStarted,
                isOpenerState || isOpenerExitState, isFollowupState))
            {
                ResetSpearSequence();
                return;
            }

            if (!_spearOpenerHitResolved && isOpenerState
                && ShouldResolveSpearOpenerHit(true, Mathf.Max(0f, state.normalizedTime), _spearOpenerHitResolved))
            {
                _spearOpenerHitResolved = true;
                FireComboTrail(1);
                FireComboSlash(1);   // [176차] 스윙1 슬래시 복원(사용자 정정 — 창은 찌르기가 없고 2스윙 콤보) — [166-C] 스윙별 1회 발화 계약 유지
                _combat?.ResolveComboSwingHit(1);
                Debug.Log($"[Spear] AttackThrust hit resolved normT={state.normalizedTime:F2}");
            }

            // [176차] 오프너 내장 2스윙 자동 해결 게이트 철수 — [166-B] 이중 가드(시간·상태)가 1클릭만으로
            // 2타까지 발화시켜 콤보가 분리되지 않았다(사용자 지시: 1클릭=1타, 2클릭=2타). 스윙 2는 기존
            // 2클릭 승인 경로(_spearFollowupAuthorized → WeaponCombo stage 2 → ShouldResolveSpearFollowupHit)가
            // 단일 소스로 담당한다. ShouldResolveSpearOpenerSwing2 판정 함수는 정의 존속(단위테스트 리플렉션 핀).

            // No follow-up was authorized: retire the opener once its real exit state is reached.
            if (!_spearFollowupAuthorized && _spearOpenerHitResolved && isOpenerExitState)
            {
                ResetSpearSequence();
                return;
            }

            if (!_spearFollowupAuthorized || !isFollowupState) return;
            float normalizedTime = Mathf.Max(0f, state.normalizedTime);
            if (ShouldResolveSpearFollowupHit(true, normalizedTime, _spearFollowupHitResolved))
            {
                _spearFollowupHitResolved = true;
                FireComboTrail(2);
                _combat?.ResolveComboSwingHit(2);
                FireComboSlash(2);
                Debug.Log($"[Spear] WeaponCombo follow-up hit resolved normT={normalizedTime:F2}");
            }

            if (normalizedTime >= GetMeleeComboPlaybackLimit(2))
            {
                _anim.CrossFade(MeleeComboExitStateName, ComboExitBlend, 0);
                ResetSpearSequence();
            }
        }

        private void StartSpearOpener(bool restart)
        {
            _spearAcceptedClicks = 1;
            _spearOpenerAcceptedTime = Time.time;
            _spearOpenerHitResolved = false;
            _spearFollowupHitResolved = false;
            _spearFollowupAuthorized = false;
            _spearFollowupStarted = false;
            _spearStateTransitionTime = Time.time;
            _spearDiagNextLog = 0f;   // [무번호 44-SPEAR-DIAG] 클릭 즉시 샘플 개시
            _spearDiagSamples = 0;
            _spearOpenerResolveAtTime = Time.time + ResolveSpearOpenerClipLength() * SpearOpenerStrikeNormT;   // [무번호 45-SPEAR-TIMEGATE]
            if (restart)
            {
                // AttackThrust disallows a self-transition; explicitly rewind the authored state.
                _anim.Play(SpearOpenerStateName, 0, GetSpearOpenerRestartNormalizedTime());
                _attackHoldUntil = Time.time + 0.6f;
            }
            else
            {
                // Bypass any-state trigger arbitration for the opener. This accepted Spear click
                // owns the AttackThrust state and must visibly start from its first frame.
                _anim.Play(SpearOpenerStateName, 0, 0f);
                _attackHoldUntil = Time.time + 0.6f;
                Debug.Log("[HumanoidClipDriver] Spear opener played directly (AttackThrust, t=0)");
            }
        }

        private void ResetSpearSequence()
        {
            _spearAcceptedClicks = 0;
            _spearOpenerHitResolved = false;
            _spearFollowupHitResolved = false;
            _spearFollowupAuthorized = false;
            _spearFollowupStarted = false;
            // [무번호 47-P1] 시퀀스 은퇴 = 스윙 종료 — 흰 아크는 스윙 중에만 존재. 45차 실측: 상태 미진입
            //   경로에서는 콤보 종료(EndCombo)·레거시 상태 탈출 OFF가 영영 발화하지 않아 트레일이 이동/패링
            //   중에도 계속 방출됐다. 은퇴 지점에서 즉시 종료+아크 세틀(만료 샘플 페이드아웃).
            SetSwingTrailEmitting(false);
            AttackArcVFX.Settle();
        }

        private static bool IsSpearSequenceExpired(bool sequenceActive, float openerTime, float now, float clickWindow)
        {
            // Decimal click-window boundaries can subtract to one ULP below the configured value.
            return sequenceActive && now - openerTime >= clickWindow - 0.0001f;
        }

        private static bool ShouldAuthorizeSpearFollowupAtDispatch(bool alreadyAuthorized, int acceptedClicks)
        {
            // PlayerCombat accepted this input edge before it advanced AcceptedAttackSequence.
            // Only the pending accepted delta and current stage matter here; never re-evaluate time.
            return !alreadyAuthorized && acceptedClicks == 1;
        }

        private static bool ShouldResetInterruptedSpearSequence(bool sequenceActive, bool openerHitResolved,
            bool followupAuthorized, bool followupStarted, bool isOpenerState, bool isFollowupState)
        {
            if (!sequenceActive) return false;
            if (followupStarted) return !isFollowupState;
            if (followupAuthorized && openerHitResolved)
                return !isOpenerState && !isFollowupState;
            // Before the opener hit, idle/non-opener during trigger or exit transitions may be
            // a valid controller handoff. Keep pending intent until a hit marker proves it played.
            if (!openerHitResolved) return false;
            return !isOpenerState && !isFollowupState;
        }

        private static float GetSpearOpenerRestartNormalizedTime()
        {
            return 0f;
        }

        private static float GetSpearFollowupStartNormalizedTime()
        {
            return 1f / 3f - 0.0001f;
        }

        private static float GetSpearFollowupStrikeNormalizedTime()
        {
            return (1f + ComboSegmentStrikeNormT) / 3f;
        }

        private static float GetMeleeComboPlaybackLimit(int acceptedClicks)
        {
            return Mathf.Clamp(acceptedClicks, 0, MaxMeleeComboClicks) / (float)MaxMeleeComboClicks;
        }

        private static float GetMeleeComboStrikeNormalizedTime(int clickSegment)
        {
            int segment = Mathf.Clamp(clickSegment, 1, MaxMeleeComboClicks) - 1;
            return (segment + ComboSegmentStrikeNormT) / MaxMeleeComboClicks;
        }

        private static bool EvaluateSpearFollowupAcceptance(bool sequenceActive, int acceptedClicks, long pendingClicks,
            float openerTime, float now, float clickWindow)
        {
            // A pending opener may accept its first rapid follow-up before the driver consumes the opener delta.
            bool openerIsPending = !sequenceActive && pendingClicks == 1;
            // Once the opener is active, any pending delta is the already-accepted second click, not capacity
            // for another physical input. Its eventual dispatch is authorized separately, without a time check.
            bool openerIsActive = sequenceActive && acceptedClicks == 1 && pendingClicks == 0;
            // Decimal subtraction can be one ULP below a click-window boundary.
            return (openerIsPending || openerIsActive) && now - openerTime < clickWindow - 0.0001f;
        }

        private static bool ShouldResolveSpearOpenerHit(bool isSpearOpener, float normalizedTime, bool alreadyResolved)
        {
            return isSpearOpener && !alreadyResolved && normalizedTime >= SpearOpenerStrikeNormT;
        }

        /// <summary>[166-B] 오프너 스윙 2 마커 판정 — 스윙 1(ShouldResolveSpearOpenerHit)과 동형 정책(정점 통과 1회).</summary>
        private static bool ShouldResolveSpearOpenerSwing2(float normalizedTime, bool alreadyResolved)
        {
            return !alreadyResolved && normalizedTime >= SpearOpenerSwing2NormT;
        }

        /// <summary>
        /// [무번호 45-SPEAR-TIMEGATE] 오프너 클립 길이 — 런타임 컨트롤러의 클립 목록에서 프래그먼트
        /// 일치 클립 길이를 1회 캐싱. 미발견 시 폴백(1.2s). 상태 머신이 AttackThrust에 진입하지 않는 실측 하에서
        /// 시간 게이트 판정의 기준값. Player_AC의 AttackThrust 상태 모션 = [무번호 84 교체] 사용자 제공
        /// "플레이어 창 휘두르기.fbx"(SpearSwing, non-loop) — 이름 프래그먼트로 매칭(임포트 클립명 변화 무관화).
        /// </summary>
        private float ResolveSpearOpenerClipLength()
        {
            if (_spearOpenerClipLength > 0f) return _spearOpenerClipLength;
            float resolved = SpearOpenerClipLengthFallback;
            if (_anim != null)
            {
                var rc = _anim.runtimeAnimatorController;
                if (rc != null && rc.animationClips != null)
                {
                    foreach (var clip in rc.animationClips)
                    {
                        if (clip != null && !string.IsNullOrEmpty(clip.name)
                            && clip.name.IndexOf(SpearOpenerClipNameFragment, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            resolved = Mathf.Max(0.2f, clip.length);
                            break;
                        }
                    }
                }
            }
            _spearOpenerClipLength = resolved;
            return resolved;
        }

        private static bool ShouldResolveSpearFollowupHit(bool authorized, float normalizedTime, bool alreadyResolved)
        {
            return authorized && !alreadyResolved && normalizedTime >= GetSpearFollowupStrikeNormalizedTime();
        }

        private static long ConsumeAttackSequenceDelta(long currentSequence, ref long consumedSequence)
        {
            long delta = currentSequence - consumedSequence;
            consumedSequence = currentSequence;
            return delta > 0 ? delta : 0;
        }

        private bool AcceptPlayerMeleeComboClick()
        {
            if (_anim == null) return false;

            // AcceptedAttackSequence is dispatched from Update, which may run one or more frames
            // after PlayerCombat accepted the click. Use that click's timestamp so a valid buffered
            // follow-up is not reclassified as a late restart by driver scheduling.
            float now = _combat != null ? _combat.LastAttackTime : Time.time;
            if (_comboStage == 0)
            {
                _comboStage = 1;
                for (int i = 0; i < _stageHitResolved.Length; i++) _stageHitResolved[i] = false;
                for (int i = 0; i < _stageVfxResolved.Length; i++) _stageVfxResolved[i] = false;   // [168-P1] VFX 미러 동기 리셋
                _comboBufferedClicks = 0;
                _comboAcceptedClicks = 1;
                _expiredComboRestartAvailable = false;
                _comboStartTime = now;
                _meleeCombo.Start(now);
                _anim.Play(MeleeComboStateName, 0, 0f);
                FireComboTrail(1);
                _attackHoldUntil = now + MeleeComboClickWindow;
                _pendingAcceptedAttackInputs--;
                Debug.Log("[Combo] Weapon_Combo_2 시작 (segment 1/3)");
                return true;
            }

            if (HasComboFollowupCapacity(_comboAcceptedClicks, 0)
                && _meleeCombo.CanAcceptFollowup(now, MeleeComboClickWindow))
            {
                bool accepted = _meleeCombo.TryAcceptFollowup(now, MeleeComboClickWindow);
                if (!accepted) return false;
                _comboAcceptedClicks = _meleeCombo.AcceptedClicks;
                _comboStage = _comboAcceptedClicks;
                _comboBufferedClicks = _comboAcceptedClicks - 1;
                FireComboTrail(_comboAcceptedClicks);
                _attackHoldUntil = Mathf.Max(_attackHoldUntil, now + MeleeComboClickWindow);
                _pendingAcceptedAttackInputs--;
                Debug.Log($"[Combo] Weapon_Combo_2 segment {_comboAcceptedClicks}/3 accepted; resume same clip");
                return true;
            }

            if (_meleeCombo.IsFollowupWindowExpired(now, MeleeComboClickWindow))
            {
                AnimatorStateInfo previousState = _anim.GetCurrentAnimatorStateInfo(0);
                int previousSegment = _comboAcceptedClicks;
                if (previousSegment > 0 && TryResolveMeleeComboHit(previousSegment,
                    Mathf.Max(0f, previousState.normalizedTime), _stageHitResolved))
                {
                    _combat?.ResolveComboSwingHit(previousSegment);   // [168-P1] 데미지만 — VFX는 모니터 조기발화 소유(이중발화 방지)
                }
                ResetInterruptedCombo("Weapon_Combo_2 follow-up window expired before accepted click dispatch", true);
                return AcceptPlayerMeleeComboClick();
            }

            // Preserve the sequence delta if a transient capacity/order race is observed;
            // PlayerCombat remains the gate that rejects invalid or overflowing clicks.
            return false;
        }

        private static bool CanAcceptBufferedFollowup(bool animatorReady, int activeClicks,
            long pendingClicks, bool followupWindowOpen)
        {
            if (!animatorReady || !followupWindowOpen || (activeClicks <= 0 && pendingClicks <= 0)) return false;
            int pending = pendingClicks <= 0 ? 0
                : pendingClicks >= MaxMeleeComboClicks ? MaxMeleeComboClicks : (int)pendingClicks;
            int reservedTotal = Mathf.Clamp(activeClicks, 0, MaxMeleeComboClicks) + pending;
            return reservedTotal < MaxMeleeComboClicks;
        }

        private static bool HasComboFollowupCapacity(int acceptedClicks, int pendingAcceptedInputs)
        {
            return acceptedClicks + pendingAcceptedInputs < MaxMeleeComboClicks;
        }

        /// <summary>One Weapon_Combo_2 clip segmented at click-count boundaries (1/2, 1) — [168-P1] 2클릭 재절단.</summary>
        private void MonitorMeleeComboClip()
        {
            if (_anim == null || _comboStage <= 0) return;

            AnimatorStateInfo state = _anim.GetCurrentAnimatorStateInfo(0);
            if (state.IsName(MeleeComboStateName))
            {
                float normalizedTime = Mathf.Max(0f, state.normalizedTime);
                for (int segment = 1; segment <= _comboAcceptedClicks; segment++)
                {
                    // [168-P1] 슬래시 VFX 조기발화 — 데미지 스트라이크(정점 0.32)보다 LeadNormT(0.12) 선행 1회(플래그 가드).
                    // 임계값 = max(세그먼트 시작+0.02, 스트라이크-0.12) — 세그먼트 미개시 조기발화 차단.
                    if (!_stageVfxResolved[segment - 1] && normalizedTime >= Mathf.Max(
                            (segment - 1) / (float)MaxMeleeComboClicks + 0.02f,
                            GetMeleeComboStrikeNormalizedTime(segment) - ComboSlashVfxLeadNormT))
                    {
                        _stageVfxResolved[segment - 1] = true;
                        FireComboSlash(segment);
                        Debug.Log($"[Combo] slash VFX early segment={segment} normT={normalizedTime:F2}");
                    }

                    if (!TryResolveMeleeComboHit(segment, normalizedTime, _stageHitResolved)) continue;
                    _combat?.ResolveComboSwingHit(segment);   // [168-P1] 데미지 스트라이크만 — VFX는 상단 조기발화 소유(이중발화 방지)
                    Debug.Log($"[Combo] Weapon_Combo_2 strike resolved segment={segment} normT={normalizedTime:F2}");
                }

                MeleeComboStateMachine.Decision decision = _meleeCombo.Evaluate(
                    Time.time, normalizedTime, MeleeComboClickWindow);
                if (decision == MeleeComboStateMachine.Decision.Hold)
                {
                    float boundary = GetMeleeComboPlaybackLimit(_meleeCombo.AcceptedClicks);
                    _anim.Play(MeleeComboStateName, 0, boundary);
                }
                else if (decision == MeleeComboStateMachine.Decision.Exit)
                {
                    bool expired = _meleeCombo.DidExitBecauseFollowupWindowExpired;
                    _anim.CrossFade(MeleeComboExitStateName, ComboExitBlend, 0);
                    ResetInterruptedCombo(expired
                        ? "Weapon_Combo_2 follow-up window expired"
                        : "Weapon_Combo_2 completed", expired);
                }
                return;
            }

            if (Time.time - _comboStartTime > 0.15f)
                ResetInterruptedCombo("Weapon_Combo_2 interrupted or exited");
        }

        private void ResetInterruptedCombo(string reason, bool allowExpiredRestart = false)
        {
            _comboStage = 0;
            _comboBufferedClicks = 0;
            _comboAcceptedClicks = 0;
            _expiredComboRestartAvailable = allowExpiredRestart;
            _meleeCombo.Reset();
            SetSwingTrailEmitting(false);
            AttackArcVFX.Settle();
            WeaponSwingTrail.SetComboStage(0);
            Debug.Log($"[Combo] {reason}");
        }


        /// <summary>
        /// Stage clip path swing trail emitted at each stage start.
        /// </summary>
        private void FireComboTrail(int stage)
        {
            try
            {
                if (mode == DriveMode.Player)
                {
                    WeaponSwingTrail.EnsureBareFist(transform.root);
                    WeaponSwingTrail.SetComboStage(stage);
                    SetSwingTrailEmitting(true);
                    // [2026-09-25 프리미엄] 실제 블레이드 궤적 스윕 리본(선택) — 기본 OFF. 활성 시 트레일과 중첩.
                    if (AttackArcVFX.PremiumArcEnabled)
                    {
                        AttackArcVFX.Swing(WeaponSwingTrail.GetCurrentWeaponType(), transform.root);
                        AttackArcVFX.SetTint(WeaponSwingTrail.GetCurrentWeaponType(), stage);   // [QA_fix] 콤보 스테이지 틴트 배선
                    }
                    Debug.Log($"[Combo] 스윙 트레일 즉시 방출 (stage={stage}, 아크는 strike 대기)");
                }
            }
            catch (System.Exception fxEx)
            {
                Debug.LogWarning($"[Combo] 트레일 즉시 방출 실패(전투 계속): {fxEx.Message}");
            }
        }

        /// <summary>
        /// Combo FX path, with swing trails retained and optional premium arc support.
        /// </summary>
        private void FireComboSlash(int stage)
        {
            try
            {
                if (mode == DriveMode.Player)
                {
                    // [2026-09-13 맨손 폴백] 무기 트레일 미부착(맨손)이면 플레이어 오른손에 폴백 트레일 부착.
                    // 내부 _trail==null 가드가 무기 트레일을 보존하므로 매 콤보 진입마다 호출해도 무해(부착 1회, 로그 1회).
                    WeaponSwingTrail.EnsureBareFist(transform.root);

                    // [Phase B] 콤보 스테이지 설정 — 트레일 색상 오버라이드 적용
                    WeaponSwingTrail.SetComboStage(stage);

                    // [2026-09-13 스타일라이즈드 슬래시 발화] 콤보 방향(ComboStageDirection → 전방 반구 클램프) +
                    // yaw 부호(좌우 플립 판정). [46차 후속] 스윙 앵커를 몸에 가깝게 — 기존 fwd 0.9 + up 1.2(38차 규격)에서
                    // fwd 0.55 + up 1.25로 조정(플레이어 루트 기준). ← 튜닝 포인트: 아크가 몸에서 떠 보이면 fwd를,
                    // 높이가 어색하면 up 값을 조정할 것. (앵커 계산은 호출부 단일 소스 — 러너 내부 계산 없음, 불일치 제거)
                    var t = transform;
                    // [174차] 부착 지연 해소 — 앵커를 무기 팁(WeaponSwingTrail.GetTrailTipAnchor — 손 본 추적 앵커)
                    // 기준으로 발화한다. 기존 47차 후속9 고정점(t.position + fwd 0.8 + up 1.25)은 스윙 초반/후반에
                    // 팁과 어긋나 "슬래시 부착이 늦다" 체감의 주원인(사용자 리포트 11-23). strike 마커/조기발화 리드
                    // 시점의 팁은 스윙 궤적 위 → 아크가 궤적 위에서 즉시 시작된다. 팁 앵커 부재 시 기존 고정점 폴백.
                    Vector3 pos = ComboFxAnchorPos(t);
                    // [2026-09-15 Phase A] 방향/yaw 산식 — [174차] 찌르기 분리(FireComboThrust)와 공용화(ComboFxDir, 로직 무변경).
                    Vector3 dir = ComboFxDir(pos, stage, t, out float yawSign);

                    // 46차 후속: playerRoot = 플레이어 루트(transform.root — EnsureBareFist와 동일 기준) 전달 —
                    // 슬래시 인스턴스가 플레이어에 부착되어 이동/회전을 추종한다(부착감). null이면 러너가 폴백.
                    // [2026-09-25 공격 FX] 사용자 지정: 스윙 슬래쉬 아크 에셋(SlashVFXRunner.PlaySlashStage) 제거 —
                    // 공격 연출을 젤다(BOTW)식 '무기 궤적 흰 트레일(WeaponSwingTrail.SetEmitting)'로 일원화한다.
                    // 피격 임펙트(Magic Hit 2, PlayImpactMulti)는 별도 경로(CombatFXGate/PlayerCombat)로 그대로 유지.
                    // 복원 필요 시 아래 주석만 해제.
                    // [167-C] 재부착 — 사용자 지시 11-21("Free Slash 에셋 여전히 미부착")로 스윙 아크 재활성.
                    //   흰 트레일(WeaponSwingTrail) 유지 + Free Slash 아크 병행(중첩 허용). 60차 우선 경로/쿨다운/스테이지 롤 규격 무수정.
                    SlashVFXRunner.PlaySlashStage(pos, dir, stage, yawSign, transform.root);
                }
                SetSwingTrailEmitting(true);
                // [2026-09-25 프리미엄] 실제 블레이드 궤적 스윕 리본(선택) — 기본 OFF. 활성 시 트레일과 중첩.
                if (AttackArcVFX.PremiumArcEnabled)
                {
                    AttackArcVFX.Swing(WeaponSwingTrail.GetCurrentWeaponType(), transform.root);
                    AttackArcVFX.SetTint(WeaponSwingTrail.GetCurrentWeaponType(), stage);   // [QA_fix] 콤보 스테이지 틴트 배선
                }
                // [무번호 70-U2] 스윙 발화 무기 발광 펄스 — 플레이어 모드 전용(병사 SoldierSwingTrail 기존 계약 무수정).
                //   색=FXPalette.AttackPrimary 단일 소스(공격 이펙트 통일색), 지속 0.15s. CurrentVisual==null(주먹/미장착)은 조용 스킵(내부 가드).
                //   [무번호 80-P3] Pulse 내부에서 트레일 층(칼날 팁 즉발 잔상) 동시 발화 — 별도 호출/키/파라미터 없음(Task 3.3).
                if (mode == DriveMode.Player)
                    FxWeaponGlow.Pulse(WeaponEquipManager.CurrentVisual, FXPalette.AttackPrimary, 0.15f);   // [79-R20] 공격 이펙트 통일색
                Debug.Log($"[Combo] 스윙 트레일 방출 (stage={stage})");
            }
            catch (System.Exception fxEx)
            {
                Debug.LogWarning($"[Combo] 스윙 트레일 토글 실패(전투 계속): {fxEx.Message}");
            }
        }

        /// <summary>
        /// [174차] 콤보 FX 공용 앵커 — 무기 팁(WeaponSwingTrail.GetTrailTipAnchor — 손 본 추적 공짜 동기) 우선.
        /// strike 마커/조기발화 리드 시점의 팁은 스윙 궤적 위에 있으므로 아크/찌르기가 궤적에서 즉시 시작된다
        /// (부착 지연 해소). 팁 앵커 부재(트레일 미부착 등) 시 기존 전방 고정점 폴백(47차 후속9 규격 — 변경 전 값 유지).
        /// </summary>
        private static Vector3 ComboFxAnchorPos(Transform t)
        {
            var tipGo = WeaponSwingTrail.GetTrailTipAnchor();
            if (tipGo != null && tipGo.transform != null)
                return tipGo.transform.position;
            return t.position + t.forward * 0.8f + Vector3.up * 1.25f;   // 폴백: 47차 후속9 규격(변경 전 값)
        }

        /// <summary>
        /// [174차] 콤보 FX 공용 방향/yaw 산출 — 기존 FireComboSlash 본문(2026-09-15 Phase A)의 로직 이동, 무변경.
        /// 슬래시 호/찌르기 스트릭이 피격 대상(몬스터)을 향하도록 — LastHitPoint(최근 적중 지점) 또는
        /// CurrentTarget 위치 기준, 폴백 ComboStageDirection. yawSign은 아크 진행 좌우 플립 판정용.
        /// </summary>
        private static Vector3 ComboFxDir(Vector3 pos, int stage, Transform t, out float yawSign)
        {
            Vector3 dir;
            if (ProjectName.Systems.PlayerCombat.LastHitValid && Time.time - ProjectName.Systems.PlayerCombat.LastHitTime <= 0.5f)
            {
                dir = (ProjectName.Systems.PlayerCombat.LastHitPoint - pos).normalized;  // FX 앵커 → 타격 지점 방향
            }
            else if (ProjectName.Systems.PlayerCombat.Instance != null && ProjectName.Systems.PlayerCombat.Instance.CurrentTarget != null)
            {
                var targetComp = ProjectName.Systems.PlayerCombat.Instance.CurrentTarget as Component;
                dir = (targetComp.transform.position - pos).normalized;  // FX 앵커 → 현재 타겟 방향
            }
            else
            {
                dir = ComboStageDirection(stage, t);  // 폴백: 기존 스윙 방향
            }
            dir.y = 0f; // 수평 방향만 사용 (수직 성분 제거)
            if (dir.sqrMagnitude < 0.0001f)
                dir = t.forward; // 극단 케이스 폴백

            // yawSign — 타겟 방향의 수평 yaw 부호: 좌측이면 -1, 우측이면 +1 (아크 진행 좌우 플립용)
            Vector3 fwdFlat = new Vector3(t.forward.x, 0f, t.forward.z);
            Vector3 dirFlat = new Vector3(dir.x, 0f, dir.z);
            yawSign = (fwdFlat.sqrMagnitude > 0.000001f && dirFlat.sqrMagnitude > 0.000001f
                && Vector3.SignedAngle(fwdFlat.normalized, dirFlat.normalized, Vector3.up) < 0f)
                ? -1f : 1f;
            return dir;
        }

        /// <summary>
        /// [174차] 찌르기(스피어 오프너 AttackThrust) 전용 FX 발화 — 슬래시 아크(PlaySlashStage) 대신
        /// 선형 찌르기 스트릭(PlayThrustStage). 사용자 리포트(11-23): 오프너(찌르기)와 팔로업 WeaponCombo(스윙)가
        /// 같은 슬래시 아크를 써서 시각적으로 구분 불가 → AttackThrust 경로(오프너 스윙 1·2) 전체를 찌르기 FX로
        /// 분리하고, 팔로업/근접 콤보는 기존 슬래시 아크 유지(찌르기=직선 / 스윙=아크 — 명백한 시각 분리).
        /// [166-C] 스윙별 1회 발화 계약 유지(발화 시점도 기존 strike 마커 — 변경 없음). 스윙 트레일은 각
        /// 오프너 사이트의 선행 FireComboTrail이 담당(기존 배선 무수정), 발광 펄스는 기존 FireComboSlash 배선 동형.
        /// </summary>
        private void FireComboThrust(int stage)
        {
            try
            {
                if (mode == DriveMode.Player)
                {
                    WeaponSwingTrail.EnsureBareFist(transform.root);
                    WeaponSwingTrail.SetComboStage(stage);

                    // 앵커/방향 — FireComboSlash와 공용 산식([174차] 무기 팁 앵커 우선 + 대상 방향).
                    var t = transform;
                    Vector3 pos = ComboFxAnchorPos(t);
                    Vector3 dir = ComboFxDir(pos, stage, t, out float yawSign);

                    // 찌르기 전용 경로 — AttackThrust에서는 슬래시 아크(PlaySlashStage)를 발화하지 않는다(계약).
                    SlashVFXRunner.PlayThrustStage(pos, dir, stage, transform.root);
                    SetSwingTrailEmitting(true);
                    // [무번호 70-U2] 스윙 발화 무기 발광 펄스 — 찌르기도 동일 발광(FireComboSlash 배선 동형, 색=공격 이펙트 통일색).
                    FxWeaponGlow.Pulse(WeaponEquipManager.CurrentVisual, FXPalette.AttackPrimary, 0.15f);
                    Debug.Log($"[Combo] 찌르기 FX 발화 (stage={stage}, yawSign={yawSign}, AttackThrust — 슬래시 아크 아님)");
                }
            }
            catch (System.Exception fxEx)
            {
                Debug.LogWarning($"[Combo] 찌르기 FX 발화 실패(전투 계속): {fxEx.Message}");
            }
        }

        /// <summary>
        /// [2026-09-13 스타일라이즈드 슬래시 복원] 스테이지별 스윙 방향(튜닝 상수 — 2026-09-11
        /// WeaponSwingDirectionAnalyzer 실측(Heat 리그 RightHand 리타깃) 기반, 42차 스윙 쿼드 폐기 때 제거했다가
        /// 스타일라이즈드 아크 발화용 — 모든 스테이지가 전방 반구를 향하도록 고정.
        /// 1타: 좌전방 수평(yaw -45°), 2타: 우전방 수직(yaw +45°, 롤 -90°로 수직 처리), 3타: 정면 전방(yaw 0°).
        /// ClampForwardHemisphere는 안전망으로만 유지(이미 전방이면 무변경).
        /// </summary>
        private static Vector3 ComboStageDirection(int stage, Transform t)
        {
            Vector3 dir;
            switch (stage)
            {
                case 2:
                    dir = Quaternion.Euler(0f, 45f, 0f) * t.forward;      // 2타: 우전방 (수직 궤적은 러너 롤 -90 담당)
                    break;
                case 3:
                    dir = t.forward;  // 3타: 정면 전방 (후방/사선 제거)
                    break;
                default:
                    dir = Quaternion.Euler(0f, -45f, 0f) * t.forward;     // 1타: 좌전방 수평
                    break;
            }
            return ClampForwardHemisphere(dir, t, stage);
        }

        /// <summary>[무번호 26-ST4] 스윙 트레일 발화 분기 — Player=기존 WeaponSwingTrail(static), Soldier=root의 SoldierSwingTrail(인스턴스).
        ///   문자열 계약 보존: Player 경로의 "WeaponSwingTrail.SetEmitting(" 표현 유지.</summary>
        private void SetSwingTrailEmitting(bool on)
        {
            // [무번호 47-P1] 방출 상태 추적 — 안전왕치(아래)가 어떤 OFF 누락 경로에도 아크 잔존을 차단한다.
            if (on) { _swingEmitOn = true; _swingEmitSince = Time.time; }
            else _swingEmitOn = false;
            if (mode == DriveMode.Soldier)
            {
                if (_soldierSwingTrail == null) _soldierSwingTrail = GetComponentInParent<SoldierSwingTrail>();
                if (_soldierSwingTrail == null) _soldierSwingTrail = GetComponentInChildren<SoldierSwingTrail>();   // 트레일은 무기 GLB 자식 GO 부착 — 드라이버(root) 기준 자손 탐색 폴백
                if (_soldierSwingTrail != null) _soldierSwingTrail.SetEmittingLocal(on);
                return;
            }
            WeaponSwingTrail.SetEmitting(on);   // 기존 경로 그대로 — 계약 문자열 유지
        }

        /// <summary>[무번호 47-P1] 스윙 방출 상한 — ON 후 이 시각을 넘으면 어떤 OFF 누락 경로든 강제 종료.
        /// 콤보 1스테이지가 이 값보다 짧음을 근거로 상한 설정(2.0s — 히트 리듬 유지에 충분).</summary>
        private const float SwingEmitMaxSeconds = 2.0f;

        /// <summary>[무번호 47-P1] 안전왕치 — UpdatePlayer 매 프레임 감시. ON 상태로 상한 경과 시 강제 OFF+세틀.</summary>
        private void WatchSwingEmitDeadline()
        {
            if (!_swingEmitOn) return;
            if (Time.time - _swingEmitSince <= SwingEmitMaxSeconds) return;
            _swingEmitOn = false;
            SetSwingTrailEmitting(false);
            AttackArcVFX.Settle();
            Debug.Log("[47-ARC] 스윙 방출 안전왕치 — 상한 초과 강제 종료(스윙 중 아크 원칙 복원)");
        }

        /// <summary>
        /// [2026-09-11] 스윙 방향 전방 반구 클램프 — 뒤방향 스윙 금지(스윙 FX는 항상 앞방향).
        /// dir의 수평 성분이 플레이어 forward 뒤쪽(dot(dir.xz, forward.xz) < 0, 즉 수평 |yaw| > 90°)이면
        /// yaw를 전방측으로 미러(θ → 180°−θ: 전방 성분 부호 반전, 측면 성분 유지)하고 수직 성분(dir.y)은 그대로 유지한다.
        /// → 모든 스테이지에서 수평 |yaw| ≤ 90°(전방 반구) 보장. 이미 전방이면 무변경·무로그.
        /// [2026-09-13 스타일라이즈드 슬래시 복원] FireComboSlash(스윙, ComboStageDirection 경유)가 복원되어
        /// 스윙/크로스(FireComboCross, 히트 지점 dir) 모두 이 헬퍼를 경유한다 — 전방 반구 클램프 공용 유지.
        /// </summary>
        private static Vector3 ClampForwardHemisphere(Vector3 dir, Transform t, int stage)
        {
            Vector3 fwdFlat = new Vector3(t.forward.x, 0f, t.forward.z);
            if (fwdFlat.sqrMagnitude < 0.000001f) return dir;   // 플레이어 수직 응시 등 퇴화 — 수평 기준 판정 불가, 원본 유지
            fwdFlat.Normalize();
            Vector3 dirFlat = new Vector3(dir.x, 0f, dir.z);
            if (dirFlat.sqrMagnitude < 0.000001f) return dir;   // 수직 성분만 존재 — 후방 성분 없음, 클램프 불필요
            float fwdDot = Vector3.Dot(dirFlat, fwdFlat);
            if (fwdDot >= 0f) return dir;                        // 이미 전방 반구(수평 |yaw| ≤ 90°) — 무변경

            // 후방 → 전방 미러: 전방 성분(음수)만 부호 반전, 측면 성분 유지 = 수평 yaw θ → 180°−θ
            Vector3 mirroredFlat = dirFlat - 2f * fwdDot * fwdFlat;
            float yawBefore = Vector3.SignedAngle(fwdFlat, dirFlat, Vector3.up);
            float yawAfter = Vector3.SignedAngle(fwdFlat, mirroredFlat, Vector3.up);
            Debug.Log($"[Combo] 후방 스윙 → 전방 미러 클램프 (stage={stage}, 수평 yaw {yawBefore:F1}°→{yawAfter:F1}°, |yaw|≤90° 강제)");
            return new Vector3(mirroredFlat.x, dir.y, mirroredFlat.z).normalized;
        }

        /// <summary>
        /// #13: 타 완료 시점 십자가 VFX — 스윙이 끝나는 지점(스테이지 완료 경계 통과)에서 Multiple Slashes 발화.
        /// 2026-09-11 발화 위치 규격 변경: 플레이어 전방 고정점(up 1.25 + fwd 1.1)이 아니라
        /// <see cref="PlayerCombat.LastHitPoint"/>(실제 공격을 맞은 대상 지점 = 대상 bounds 중심 + up*0.2)에 발화.
        /// 최근 0.5초 내 적중이 없으면(빈 스윙) 크로스를 스킵한다 — 맞는 대상 지점에만 발화.
        /// 방향은 스테이지 실측 dir 대신 히트 지점 기준: dir = (LastHitPoint - 플레이어 머리) 정규화.
        /// 발화 타이밍/1회 보장은 콤보 감시 블록의 경계 통과 엣지(_comboCrossFired)가 담당(유지).
        /// [2026-09-12 루트 기준 통일] 기준 트랜스폼 = 드라이버 자신 transform(루트, 논리 정면) — 비주얼
        /// FBX 자식(_anim.transform)의 임포트 yaw 오프셋에 면역. 쿼드는 SlashVFXRunner가 카메라 수평
        /// 빌보드로 세우므로 dir은 빌보드 폴백/로그용.
        /// [2026-09-12 명시 갱신] 사용자 지정 — 십자가는 스윙 아크의 정중앙에(타격점 하나로 읽힘):
        /// 발화 pos를 스윙 FX와 완전히 동일 앵커로 통일 — Vector3.Lerp(플레이어 루트, LastHitPoint, 0.5) + up*1.2
        /// (플레이어↔적중지점 사이 중간점 = 스윙 아크 정중앙, 39차 Play 실측 앵커). 기존 LastHitPoint 그대로 발화
        /// 규격(대상 bounds 중심)은 스윙과 십자가 중심이 어긋나 타격이 분열처럼 보이던 문제로 폐기.
        /// dir 계산(기존: LastHitPoint - 머리 → ClampForwardHemisphere)은 방향 전용이므로 기존 유지.
        /// </summary>
        private void FireComboCross(int stage)
        {
            try
            {
                // 유효 적중 게이트 — 빈 스윙엔 크로스 없음 (PlayerCombat.LastHitPoint는 적중 직접 갱신, 미스 시 무효화)
                if (!PlayerCombat.LastHitValid || Time.time - PlayerCombat.LastHitTime > 0.5f)
                {
                    Debug.Log("[Combo] 크로스 FX 스킵 — 최근 0.5초 내 적중 없음(빈 스윙)");
                    return;
                }
                // [2026-09-12 루트 기준 통일] 비주얼 FBX 자식(_anim.transform) 대신 드라이버 루트(논리 정면).
                var t = transform;
                Vector3 head = t.position + Vector3.up * 1.5f;                       // 플레이어 머리 기준점
                Vector3 dir = PlayerCombat.LastHitPoint - head;
                dir = dir.sqrMagnitude > 0.000001f ? dir.normalized : t.forward;     // 히트 지점==머리 등 퇴화 방어
                dir = ClampForwardHemisphere(dir, t, stage);                         // [2026-09-11] 스윙/크로스 공용 전방 반구 클램프 — 뒤방향 스윙 금지
                // 2026-09-14(47차): 피격대상 밀착 — 중간지점 Lerp 제거(사용자 지정).
                // 기존: Lerp(플레이어 루트, LastHitPoint, 0.5) + up*0.6 = 플레이어↔대상 중간 지점 → 크로스가
                // 대상에서 멀어 보임. 이제 LastHitPoint(대상 bounds 중심 + up*0.2)에 밀착, up*0.15 미세 보정.
                // LastHitValid/0.5s 게이트(상단)는 유지 — 빈 스윙엔 크로스 없음.
                Vector3 pos = PlayerCombat.LastHitPoint + Vector3.up * 0.15f;
                // [2026-09-25 공격 FX] 십자가/크로스 슬래시 에셋도 슬래쉬 계열로 함께 제거 —
                // 젤다식 무기 트레일 + 피격 임펙트(Magic Hit 2)만 유지.
                // SlashVFXRunner.PlayCross(pos, dir);
                Debug.Log($"[Combo] 크로스 FX stage={stage} → 피격대상 밀착 발화 pos={pos:F2}");
            }
            catch (System.Exception fxEx)
            {
                Debug.LogWarning($"[Combo] 크로스 FX 실패(전투 계속): {fxEx.Message}");
            }
        }

        /// <summary>T-D3: 활 화살/투척물 연출 스폰(전방 포물선). 데미지 연동은 무기 시스템 후속.</summary>
        private void SpawnProjectile(string kind)
        {
            if (_anim == null) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = kind == "arrow" ? "Arrow_Visual" : "Thrown_Visual";
            go.transform.localScale = Vector3.one * (kind == "arrow" ? 0.15f : 0.3f);
            var rb = go.AddComponent<Rigidbody>();
            var origin = _anim.transform.position + Vector3.up * 1.5f;
            go.transform.position = origin;
            var forward = _anim.transform.forward;
            rb.linearVelocity = forward * 18f + Vector3.up * (kind == "arrow" ? 2f : 5f);
            Object.Destroy(go, 3f);
        }

        /// <summary>DD1: 현재 애니 상태/진행도/속도 로그 (state는 shortNameHash→이름 매핑).</summary>
        private void LogAnimDiagnostic()
        {
            if (_anim == null) return;
            var sinfo = _anim.GetCurrentAnimatorStateInfo(0);
            string stateName = ResolveStateName(sinfo);
            float speed = 0f;
            try { speed = _anim.GetFloat("Speed"); } catch { }
            float ccVel = _cc != null ? _cc.velocity.magnitude : -1f;

            // DD2: 뼈 변위 (Hips 기준) — 2초 전 스냅샷과 비교. 뼈가 실제 움직이는지 수치 확정.
            // 첫 주기는 기준점 저장만 하고 Δ는 생략. Δ=0이 지속되면 뼈가 얼어있음(골격 덮어쓰기/드라이브 실패).
            // 휴머노이드 아바타가 유효할 때만 GetBoneTransform 호출 (non-humanoid InvalidOperationException 예방).
            var av = _anim.avatar;
            bool canHuman = av != null && av.isValid && av.isHuman;
            string hipsDelta = " hips=NULL";
            if (!canHuman)
            {
                hipsDelta = " hips=N/A(nonhumanoid)";
            }
            else
            {
                var hips = _anim.GetBoneTransform(HumanBodyBones.Hips);
                if (hips != null)
                {
                    if (_hipsRefValid)
                    {
                        float posD = Vector3.Distance(hips.position, _hipsPosRef);
                        float rotD = Quaternion.Angle(hips.rotation, _hipsRotRef);
                        hipsDelta = $" hipsΔ={posD:F4}m rotΔ={rotD:F2}°";
                    }
                    _hipsPosRef = hips.position;
                    _hipsRotRef = hips.rotation;
                    _hipsRefValid = true;
                    // DD3-2: 사지 변위 — LeftHand/LeftFoot의 Hips 기준 상대벡터(world)를 같은 2초 주기로 스냅샷 비교.
                    // Hips는 이동으로 움직여도 사지 포즈가 얼어있으면 상대벡터 불변 → 매핑/드라이브 실패 분리 확정.
                    // 첫 주기는 기준 저장만. 매핑 누락(GetBoneTransform=null) 시 해당 필드 UNMAPPED 표시.
                    var lh = _anim.GetBoneTransform(HumanBodyBones.LeftHand);
                    var lf = _anim.GetBoneTransform(HumanBodyBones.LeftFoot);
                    var limbSb = new System.Text.StringBuilder();
                    if (lh != null)
                    {
                        var lhRel = lh.position - hips.position;
                        if (_limbRefValid) limbSb.Append($" LHandΔ={Vector3.Distance(lhRel, _lhRelRef):F3}");
                        _lhRelRef = lhRel;
                    }
                    else
                    {
                        limbSb.Append(" LHand=UNMAPPED");
                    }
                    if (lf != null)
                    {
                        var lfRel = lf.position - hips.position;
                        if (_limbRefValid) limbSb.Append($" LFootΔ={Vector3.Distance(lfRel, _lfRelRef):F3}");
                        _lfRelRef = lfRel;
                    }
                    else
                    {
                        limbSb.Append(" LFoot=UNMAPPED");
                    }
                    if (lh != null || lf != null) _limbRefValid = true;
                    hipsDelta += limbSb.ToString();
                }
            }

            // DD4: 실제 재생 클립명 — 상태(Walk)가 재생 중이어도 클립이 안 붙었으면 뼈에 아무 것도 쓰이지 않는다.
            string clipInfo = "NONE";
            try
            {
                var clips = _anim.GetCurrentAnimatorClipInfo(0);
                clipInfo = clips != null && clips.Length > 0
                    ? clips[0].clip.name + "(human=" + clips[0].clip.humanMotion + ",len=" + clips[0].clip.length.ToString("F2") + "s)"
                    : "NONE";
            }
            catch (System.Exception clipEx)
            {
                clipInfo = "ERR:" + clipEx.GetType().Name;
            }

            // DD4: 사지 로컬회전 Δ — localRotation 스냅샷 비교. 루트 모션/루트 회전에 완전 면역
            // (world 상대벡터 지표와 달리 캐릭터 회전만으로는 Δ가 생기지 않음). Δ=0 지속이면 사지 포즈 동결 확정.
            string limbLocal = "";
            if (canHuman)
            {
                var lfa = _anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                var lul = _anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                var llSb = new System.Text.StringBuilder();
                if (lfa != null)
                {
                    if (_limbLocalValid) llSb.Append($" LFArmRotΔ={Quaternion.Angle(lfa.localRotation, _lfaLocalRef):F2}°");
                    _lfaLocalRef = lfa.localRotation;
                }
                else
                {
                    llSb.Append(" LFArmRot=UNMAPPED");
                }
                if (lul != null)
                {
                    if (_limbLocalValid) llSb.Append($" LULegRotΔ={Quaternion.Angle(lul.localRotation, _lulLocalRef):F2}°");
                    _lulLocalRef = lul.localRotation;
                }
                else
                {
                    llSb.Append(" LULegRot=UNMAPPED");
                }
                if (lfa != null || lul != null) _limbLocalValid = true;
                limbLocal = llSb.ToString();
            }

            Debug.Log($"[HumanoidClipDriver][Diag] t={Time.time:F1}s state={stateName} normT={sinfo.normalizedTime:F2} speed={speed:F2} rawSpd={_diagRawSpeed:F2} ccVel={ccVel:F2} animEnabled={_anim.enabled} culling={_anim.cullingMode}{hipsDelta} clip={clipInfo}{limbLocal}");
        }

        /// <summary>DD1: 상태 shortNameHash를 이름으로 매핑 (매핑 실패 시 hex hash 반환).</summary>
        private string ResolveStateName(AnimatorStateInfo sinfo)
        {
            if (sinfo.IsName("Idle")) return "Idle";
            if (sinfo.IsName("Walk")) return "Walk";
            if (sinfo.IsName("Run")) return "Run";
            if (sinfo.IsName("Attack")) return "Attack";
            if (sinfo.IsName("AttackCombo")) return "AttackCombo";
            if (sinfo.IsName("Roll")) return "Roll";
            if (sinfo.IsName("RollBack")) return "RollBack";
            if (sinfo.IsName("RollSideL")) return "RollSideL";
            if (sinfo.IsName("RollSideR")) return "RollSideR";
            if (sinfo.IsName("Jump")) return "Jump";
            if (sinfo.IsName("Death")) return "Death";
            return sinfo.shortNameHash.ToString("X8");
        }

        /// <summary>DD1: 현재 상태 hash가 직전 프레임과 다르면 전환 즉시 1회 로그.</summary>
        private void LogStateTransition()
        {
            if (_anim == null) return;
            var sinfo = _anim.GetCurrentAnimatorStateInfo(0);
            int hash = sinfo.fullPathHash;
            if (hash == _prevStateHash) return;
            string curName = ResolveStateName(sinfo);
            float speed = 0f;
            try { speed = _anim.GetFloat("Speed"); } catch { }
            Debug.Log($"[HumanoidClipDriver][State] {_prevStateName} → {curName} (speed={speed:F2} rawSpd={_diagRawSpeed:F2} normT={sinfo.normalizedTime:F2})");
            _prevStateHash = hash;
            _prevStateName = curName;
        }

        // ───────────────────── Soldier 모드 ─────────────────────
        // [2026-10-11 이동 끊김 수리] Soldier 모드 Speed 평활 상태 — Player 모드의
        // "지수 평활로 끊김 제거" 패턴과 동일 계수(k = 1-exp(-10·dt)) + 스톨 홀드.
        private float _soldierSmoothedSpeed;
        private float _soldierStallTime;
        private float _soldierLastTarget;

        /// <summary>[59차] 이동 명령 도착 즉시 정지 — 스무딩/홀드 상태를 0으로 스냅해 제자리걸음 잔상을 제거.
        /// GuardPlaceholder 도착 분기(ClearCommand 직후)에서 호출. Soldier 모드 외 무시.</summary>
        public void NotifyStop()
        {
            if (mode != DriveMode.Soldier) return;
            _soldierStallTime = 0f;
            _soldierLastTarget = 0f;
            _soldierSmoothedSpeed = 0f;
            _lastPos = transform.position;
            if (_anim != null) _anim.SetFloat("Speed", 0f);
        }

        private void UpdateSoldier()
        {
            float dt = Time.deltaTime;
            Vector3 delta = transform.position - _lastPos;
            delta.y = 0f;   // 지형 y스냅 급변 제외 — XZ 수평 속도만 계산(속도 노이즈 원인 차단)
            float raw = dt > 0.0001f ? delta.magnitude / dt : 0f;
            float target;
            if (raw > 0.05f)
            {
                _soldierStallTime = 0f;
                target = raw;
            }
            else
            {
                _soldierStallTime += dt;
                target = _soldierStallTime >= 0.25f ? 0f : _soldierLastTarget;   // 미세 정체 홀드
            }
            _soldierLastTarget = target;
            float k = 1f - Mathf.Exp(-10f * dt);
            _soldierSmoothedSpeed = Mathf.Lerp(_soldierSmoothedSpeed, target, k);
            _anim.SetFloat("Speed", _soldierSmoothedSpeed);
            // [61차] Move 클립 재생속도 정합 — 병사 실측 이동속도(m/s)를 보행 배속으로 정규화해 공급.
            //   MoveRate = Speed(m/s) / 기준 3.0 — 이동 3.0m/s = 1배(클립 원속), 감속 시 비례 축소(0.4~1.4 클램프).
            //   이동 중 슬라이드(보폭-이동속도 불일치) 완화. 정지(Idle)는 Speed 전이 임계가 먼저 처리.
            //   (_anim은 Update() 진입 가드에서 null 확인 — UpdateSoldier 내부는 안전.)
            float moveRate = Mathf.Clamp(_soldierSmoothedSpeed / 3f, 0.4f, 1.4f);
            _anim.SetFloat("MoveRate", moveRate);
            _lastPos = transform.position;
        }


        // ───────────────────── 머티리얼 유틸 ─────────────────────
        /// <summary>
        /// 원본 GLB(텍스처 정상)의 머티리얼을 Humanoid FBX 본체로 복사한다.
        /// Blender FBX export는 텍스처를 유실하므로 흰색으로 보이는 문제의 해결책.
        /// URP Lit 재생성 + GLB의 _BaseMap 텍스처 이식.
        /// </summary>
        public static void CopyMaterialsFromGlb(GameObject fbxBody, string glbResourcePath, Color fallbackTint = default, bool skipTexturedTargets = false)
        {
            var glbPrefab = Resources.Load<GameObject>(glbResourcePath);
            if (glbPrefab == null)
            {
                Debug.LogWarning($"[HumanoidClipDriver] GLB 원본 없음: {glbResourcePath}");
                return;
            }
            var temp = Object.Instantiate(glbPrefab);
            temp.SetActive(false);
            try
            {
                var srcRends = temp.GetComponentsInChildren<Renderer>(true);
                var dstRends = fbxBody.GetComponentsInChildren<Renderer>(true);
                if (srcRends.Length == 0 || dstRends.Length == 0)
                {
                    Debug.LogWarning("[HumanoidClipDriver] 머티리얼 복사 대상 렌더러 없음");
                    return;
                }

                // [무번호5/P0] 텍스처 보유 대상 스킵 가드 — 플레이어처럼 FBX 본체+GLB 장비가 같은 계층에
                // 공존하면 전 렌더러 덮어쓰기가 장비 GLB의 정상 텍스처(예: wood_*)를 파괴한다.
                // 가드: 현재 머티리얼에 이미 바인딩된 텍스처가 있으면(셰이더 텍스처 프로퍼티 전수 검사) 스킵.
                if (skipTexturedTargets)
                {
                    var filtered = new List<Renderer>();
                    foreach (var r in dstRends)
                    {
                        if (r == null) continue;
                        var cur = r.sharedMaterial;
                        if (cur != null && HasAnyBoundTexture(cur)) continue;
                        filtered.Add(r);
                    }
                    dstRends = filtered.ToArray();
                    if (dstRends.Length == 0)
                    {
                        Debug.Log("[HumanoidClipDriver] 스킵 가드 — 전 대상이 이미 텍스처 보유(재이식 불요)");
                        return;
                    }
                }

                var src = srcRends[0].sharedMaterial;
                Texture baseTex = src != null && src.HasProperty("_BaseMap")
                    ? src.GetTexture("_BaseMap") : (src != null ? src.mainTexture : null);
                Color baseCol = src != null && src.HasProperty("_BaseColor")
                    ? src.GetColor("_BaseColor") : Color.white;

                // [2026-09-22 UV 정합 검증] GLB 본체와 FBX 대상의 메시 정점수가 다르면 UV 배치가 달라
                // 텍스처가 뭉개져 '메시가 깨진 것처럼' 보인다(영주 실측). 이 경우 텍스처 복사를 생략하고
                // 틴트 폴백 재질로 대체한다.
                var srcMesh = GetRendererMesh(srcRends[0]);
                var dstMesh = GetRendererMesh(dstRends[0]);
                bool uvMismatch = srcMesh != null && dstMesh != null
                    && Mathf.Abs(srcMesh.vertexCount - dstMesh.vertexCount) > dstMesh.vertexCount * 0.05f;

                var urpLit = Shader.Find("Universal Render Pipeline/Lit");
                var mat = new Material(urpLit);
                if (uvMismatch)
                {
                    Color tint = fallbackTint.a > 0.01f ? fallbackTint : new Color(0.82f, 0.78f, 0.72f);
                    mat.SetColor("_BaseColor", tint);
                    Debug.LogWarning($"[HumanoidClipDriver] UV 미스매치(정점 GLB={srcMesh.vertexCount} vs FBX={dstMesh.vertexCount}) — 텍스처 복사 생략, 틴트 폴백({tint})");
                }
                else
                {
                    if (baseTex != null) mat.SetTexture("_BaseMap", baseTex);
                    mat.SetColor("_BaseColor", baseCol);
                }
                foreach (var r in dstRends)
                    if (r != null) r.sharedMaterial = mat;
                Debug.Log($"[HumanoidClipDriver] 머티리얼 적용 완료: 대상 {dstRends.Length}개, 텍스처={(baseTex != null && !uvMismatch ? baseTex.name : "생략(UV미스매치/없음)")}");
            }
            finally
            {
                Object.Destroy(temp);
            }
        }

        /// <summary>렌더러에서 메시 추출(SMR=sharedMesh, MR=MeshFilter).</summary>
        private static Mesh GetRendererMesh(Renderer r)
        {
            if (r is SkinnedMeshRenderer smr) return smr.sharedMesh;
            var mf = r.GetComponent<MeshFilter>();
            return mf != null ? mf.sharedMesh : null;
        }

        /// <summary>[무번호5/P0] 머티리얼에 바인딩된 텍스처 존재 여부 — 셰이더 텍스처 프로퍼티 전수 검사.</summary>
        private static bool HasAnyBoundTexture(Material m)
        {
            if (m == null || m.shader == null) return false;
            var shader = m.shader;
            int count = shader.GetPropertyCount();
            for (int i = 0; i < count; i++)
            {
                if (shader.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Texture) continue;
                if (m.GetTexture(shader.GetPropertyName(i)) != null) return true;
            }
            return m.mainTexture != null;   // 비표준 셰이더 폴백(mainTexture=첫 텍스처 프로퍼티)
        }

        // ───────────────────── 외부 트리거 ─────────────────────
        /// <summary>병사 공격 트리거 (GuardCombatAI 등에서 호출). 모드 무관 동작.</summary>
        public void TriggerAttack()
        {
            if (_anim == null) return;
            _anim.SetTrigger("Attack");
        }

        /// <summary>사망 트리거. 최초 1회만 발동 (외부에서 사망 시 호출).</summary>
        public void TriggerDeath()
        {
            if (_anim == null || _deathFired) return;
            _deathFired = true;
            _pendingPlayerHitLight = false;
            _anim.SetTrigger("Death");
        }
    }
}