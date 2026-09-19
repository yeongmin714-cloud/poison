// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 414
using System.Collections.Generic;
using UnityEngine;
using ProjectName.Systems.Animation.Procedural.Bones;
using ProjectName.Systems.Animation.Procedural.IK;
using ProjectName.Systems.Animation.Procedural.Locomotion.Quadruped;

namespace ProjectName.Systems
{
    /// <summary>
    /// 4족 동물 프로시저럴 로코모션.
    /// 걸음걸이(Gait) 선택: Walk(4박자) → Trot(대각 2박자) → Pace(동측 2박자) → Gallop(비대칭 4박자)
    /// 속도/크기에 따라 자동 전이.
    /// </summary>
    public class QuadrupedProceduralLocomotion : MonoBehaviour
    {
        public enum Gait
        {
            Walk,   // 4-beat: LF, RH, RF, LH (slow, stable)
            Trot,   // 2-beat diagonal: LF+RH, RF+LH (medium)
            Pace,   // 2-beat lateral: LF+LH, RF+RH (medium-fast)
            Gallop  // 4-beat asymmetric: LH, RH, LF, RF (fast)
        }

        [Header("Gait Parameters")]
        [SerializeField] private float _walkSpeed = 2f;
        [SerializeField] private float _trotSpeed = 5f;
        [SerializeField] private float _paceSpeed = 6f;
        [SerializeField] private float _gallopSpeed = 10f;

        [SerializeField] private float _stepLength = 0.6f;
        [SerializeField] private float _stepHeight = 0.15f;
        [SerializeField] private float _dutyCycle = 0.75f; // stance phase ratio

        [Header("Gait Transitions")]
        [SerializeField] private float _walkToTrotThreshold = 0.4f;
        [SerializeField] private float _trotToPaceThreshold = 0.6f;
        [SerializeField] private float _paceToGallopThreshold = 0.8f;

        [Header("Body")]
        [SerializeField] private float _spineWaveAmplitude = 0.05f;
        [SerializeField] private float _spineWaveFrequency = 1f;

        // Components
        private Animator _animator;
        private ProceduralBoneMap _boneMap;
        private Rigidbody _rigidbody;
        private QuadrupedProceduralAnimation _procAnim;

        // State
        private Gait _currentGait = Gait.Walk;
        private float _currentSpeed;
        private float _targetSpeed;

        // [2026-09-14(49차)] 보행 강제 오버라이드 — 토끼 호핑 등 종별 고정 보행용.
        // null이면 기존처럼 속도 기반 자동 gait 선택.
        private Gait? _gaitOverride;

        // Leg phases (0~1 per leg)
        private float _lfPhase = 0f;   // Left Front
        private float _rfPhase = 0f;   // Right Front
        private float _lhPhase = 0f;   // Left Hind
        private float _rhPhase = 0f;   // Right Hind

        // Gait phase offsets
        private readonly Dictionary<Gait, (float lf, float rf, float lh, float rh)> _gaitOffsets = new()
        {
            { Gait.Walk,   (0f, 0.5f, 0.75f, 0.25f) },   // LF, RH, RF, LH
            { Gait.Trot,   (0f, 0.5f, 0.5f, 0f) },       // LF+RH, RF+LH
            { Gait.Pace,   (0f, 0f, 0.5f, 0.5f) },       // LF+LH, RF+RH
            { Gait.Gallop, (0f, 0.25f, 0.125f, 0.375f) } // LH, RH, LF, RF (rotary)
        };

        // ──────────────────────────────────────────────
        // Unity Lifecycle
        // ──────────────────────────────────────────────

        private void Awake()
        {
            _procAnim = GetComponent<QuadrupedProceduralAnimation>();
            if (_procAnim == null)
            {
                _procAnim = gameObject.AddComponent<QuadrupedProceduralAnimation>();
            }
            _animator = GetComponent<Animator>();
            _boneMap = GetComponent<ProceduralBoneMap>();
            _rigidbody = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            InitializeGaitPhases();
        }

        private void Update()
        {
            UpdateGaitSelection();
            UpdateLegPhases();
            // [2026-09-14(49차 후속)] UpdateGaitTargets()는 여기서 호출하지 않는다.
            // 발 타겟은 QuadrupedProceduralAnimation.LateUpdate()의 지면 감지(접촉점 확정) '직후'에
            // 갱신해야 _stepHeight 스윙 리프트가 IK 솔버에 살아남는다.
            // (기존 Update 호출 방식은 직후 anim.UpdateGroundDetection이 타겟을 덮어써 무효화됐다.)
        }

        private void LateUpdate()
        {
            ApplyGaitPose();
        }

        // ──────────────────────────────────────────────
        // 초기화
        // ──────────────────────────────────────────────

        private void InitializeGaitPhases()
        {
            SetGaitPhases(Gait.Walk);
        }

        private void SetGaitPhases(Gait gait)
        {
            var offsets = _gaitOffsets[gait];
            _lfPhase = offsets.lf;
            _rfPhase = offsets.rf;
            _lhPhase = offsets.lh;
            _rhPhase = offsets.rh;
        }

        // ──────────────────────────────────────────────
        // 걸음걸이 선택 (속도 기반 자동 전이)
        // ──────────────────────────────────────────────

        private void UpdateGaitSelection()
        {
            // [2026-09-14(49차)] 강제 보행 오버라이드 — 지정된 gait 유지(토끼 도약 호핑 등).
            // 속도 기반 자동 선택을 대체한다.
            if (_gaitOverride.HasValue)
            {
                if (_currentGait != _gaitOverride.Value)
                    TransitionGait(_gaitOverride.Value);
                return;
            }

            float normalizedSpeed = _currentSpeed / _gallopSpeed;

            Gait targetGait = _currentGait;

            if (normalizedSpeed < _walkToTrotThreshold)
                targetGait = Gait.Walk;
            else if (normalizedSpeed < _trotToPaceThreshold)
                targetGait = Gait.Trot;
            else if (normalizedSpeed < _paceToGallopThreshold)
                targetGait = Gait.Pace;
            else
                targetGait = Gait.Gallop;

            if (targetGait != _currentGait)
            {
                TransitionGait(targetGait);
            }
        }

        private void TransitionGait(Gait newGait)
        {
            _currentGait = newGait;
            SetGaitPhases(newGait);
            // [2026-09-14(49차 후속)] 실제 렌더 위상(anim.LF_Phase 등)의 재정렬은 anim.UpdateLegPhases가
            // CurrentGait 변화를 감지해 SyncLegPhases를 호출하는 방식으로 수행된다(렌더 위상 소유자는 anim).
            Debug.Log($"[QuadrupedLocomotion] Gait changed: {newGait}");
        }

        // ──────────────────────────────────────────────
        // 다리 위상 업데이트
        // ──────────────────────────────────────────────

        private void UpdateLegPhases()
        {
            float phaseSpeed = GetPhaseSpeed();
            float delta = phaseSpeed * Time.deltaTime;

            _lfPhase = Mathf.Repeat(_lfPhase + delta, 1f);
            _rfPhase = Mathf.Repeat(_rfPhase + delta, 1f);
            _lhPhase = Mathf.Repeat(_lhPhase + delta, 1f);
            _rhPhase = Mathf.Repeat(_rhPhase + delta, 1f);
        }

        private float GetPhaseSpeed()
        {
            switch (_currentGait)
            {
                case Gait.Walk:   return _currentSpeed / _stepLength * 0.8f;
                case Gait.Trot:   return _currentSpeed / _stepLength * 1.2f;
                case Gait.Pace:   return _currentSpeed / _stepLength * 1.4f;
                case Gait.Gallop: return _currentSpeed / _stepLength * 2f;
                default: return 1f;
            }
        }

        // ──────────────────────────────────────────────
        // [2026-09-14(49차 후속)] gait → 실제 렌더 위상 통합 API
        // (기존엔 gait 배율/오프셋이 write-only라 화면에 반영되지 않았다)
        // ──────────────────────────────────────────────

        /// <summary>
        /// [2026-09-14(49차 후속)] 현재 gait의 위상 진행 배율 공개 조회.
        /// Walk 0.8 / Trot 1.2 / Pace 1.4 / Gallop 2.0 — GetPhaseSpeed와 동일 계수.
        /// QuadrupedProceduralAnimation.UpdateLegPhases가 실제 렌더 다리 위상 진행에 사용한다.
        /// </summary>
        public float GetGaitPhaseMultiplier()
        {
            switch (_currentGait)
            {
                case Gait.Walk:   return 0.8f;
                case Gait.Trot:   return 1.2f;
                case Gait.Pace:   return 1.4f;
                case Gait.Gallop: return 2f;
                default:          return 1f;
            }
        }

        /// <summary>
        /// [2026-09-14(49차 후속)] 현재 gait 기준 초당 위상 진행 속도 공개 조회 —
        /// private GetPhaseSpeed()의 외부 래퍼(AI/디버그 모니터링용).
        /// </summary>
        public float GetCurrentGaitPhaseSpeed() => GetPhaseSpeed();

        /// <summary>
        /// [2026-09-14(49차 후속)] 현재 gait의 4족 위상 시작 오프셋을 실제 렌더 위상에 강제 동기화.
        /// anim.UpdateLegPhases가 gait 전환을 감지하면 호출해 렌더 다리가 해당 보행 패턴
        /// (예: Gallop rotary LH→RH→LF→RF)으로 재정렬되게 한다.
        /// </summary>
        public void SyncLegPhases(QuadrupedProceduralAnimation anim)
        {
            if (anim == null) return;

            var off = _gaitOffsets[_currentGait];
            anim.LF_Phase = off.lf;
            anim.RF_Phase = off.rf;
            anim.LH_Phase = off.lh;
            anim.RH_Phase = off.rh;

            // 내부 위상도 동일값으로 정렬 — 두 모듈 위상 일관 유지
            _lfPhase = off.lf;
            _rfPhase = off.rf;
            _lhPhase = off.lh;
            _rhPhase = off.rh;
        }

        // ──────────────────────────────────────────────
        // 발 타겟 계산
        // ──────────────────────────────────────────────

        /// <summary>
        /// [2026-09-14(49차 후속)] 발 타겟 스윙 갱신 — private → public.
        /// anim.LateUpdate가 지면 감지 직후 호출하며, 이때 적용한 _stepHeight 리프트가
        /// 같은 프레임 anim.ApplyFootIK/OnAnimatorIK에 그대로 반영된다.
        /// </summary>
        public void UpdateGaitTargets()
        {
            UpdateLegTarget(_procAnim.LF_Phase, ref _procAnim.LF_Target, ref _procAnim.LF_Hint, _procAnim.LF_Grounded, ref _lfLift, BoneRole.L_Hip, BoneRole.L_Knee, BoneRole.L_Ankle);
            UpdateLegTarget(_procAnim.RF_Phase, ref _procAnim.RF_Target, ref _procAnim.RF_Hint, _procAnim.RF_Grounded, ref _rfLift, BoneRole.R_Hip, BoneRole.R_Knee, BoneRole.R_Ankle);
            UpdateLegTarget(_procAnim.LH_Phase, ref _procAnim.LH_Target, ref _procAnim.LH_Hint, _procAnim.LH_Grounded, ref _lhLift, BoneRole.L_Hip, BoneRole.L_Knee, BoneRole.L_Ankle);
            UpdateLegTarget(_procAnim.RH_Phase, ref _procAnim.RH_Target, ref _procAnim.RH_Hint, _procAnim.RH_Grounded, ref _rhLift, BoneRole.R_Hip, BoneRole.R_Knee, BoneRole.R_Ankle);
        }

        // [2026-09-14(49차 후속)] 다리별 직전 프레임 스윙 리프트 값 — 힌트/미갱신 타겟의 중복 누적 방지용
        private float _lfLift, _rfLift, _lhLift, _rhLift;

        private void UpdateLegTarget(float phase, ref Vector3 target, ref Vector3 hint, bool legGrounded, ref float lastLift, BoneRole hipRole, BoneRole kneeRole, BoneRole ankleRole)
        {
            // hip/knee/ankle 역할 파라미터는 체인 해석이 anim.ApplyFootIK에서 이뤄지므로 현재 미사용(시그니처 유지).

            // [2026-09-14(49차 후속)] 스윙 리프트 중복 누적 방지:
            //  - 타겟: 레이 히트 시 지면 감지가 접촉점으로 매 프레임 새로 쓰므로 누적 없음.
            //    레이 미스(갱신 없음)일 때만 직전 리프트를 제거해 지면 기준 복원.
            //  - 힌트: 지면 감지가 절대 갱신하지 않으므로 항상 직전 리프트 제거 후 재적용.
            if (!legGrounded)
                target.y -= lastLift;
            hint.y -= lastLift * 0.5f;

            // [2026-09-14(49차 후속)] Swing 구간(폐기 상태였던 height 로컬 변수를 실사용으로 전환):
            // 발을 _stepHeight×sin(swingProgress×π) 만큼 들어올려 보행 '떼는 동작'을 실제 렌더 타겟에 반영.
            // (토끼 stepHeight 0.35 + Gallop 고정 → 깡충 도약, swamp_croc 0.06 → 낮게 기어가는 보행)
            float height = 0f;
            if (legGrounded && phase >= 0.7f) // Swing
            {
                float swingProgress = (phase - 0.7f) / 0.3f;
                height = Mathf.Sin(swingProgress * Mathf.PI) * _stepHeight;

                // 정지 직후 위상이 스윙 중간에 멈춰 발이 공중에 뜨는 것 방지 — 실속도 비례 페이드아웃
                height *= Mathf.Clamp01(_procAnim.CurrentSpeed / 0.5f);
            }

            target.y += height;
            hint.y += height * 0.5f; // 무릎 힌트는 절반 높이만 따라올림
            lastLift = height;
            // 전진 보폭(구 forward 로컬 변수)은 지면 감지 타겟이 몸 이동을 이미 따라가므로 적용하지 않는다.
        }

        // ──────────────────────────────────────────────
        // 자세 적용
        // ──────────────────────────────────────────────

        private void ApplyGaitPose()
        {
            // Spine wave (body undulation)
            ApplySpineWave();

            // Neck/head stabilization
            ApplyNeckStabilization();
        }

        private void ApplySpineWave()
        {
            if (!_boneMap.Has(BoneRole.Spine0) ||
                !_boneMap.Has(BoneRole.Spine1) ||
                !_boneMap.Has(BoneRole.Spine2))
                return;

            var spine0 = _boneMap.Get(BoneRole.Spine0);
            var spine1 = _boneMap.Get(BoneRole.Spine1);
            var spine2 = _boneMap.Get(BoneRole.Spine2);

            if (spine0 == null || spine1 == null || spine2 == null) return;

            float time = Time.time * _spineWaveFrequency;
            float wave = Mathf.Sin(time) * _spineWaveAmplitude;

            spine0.Rotate(Vector3.up, wave * 0.3f, Space.Self);
            spine1.Rotate(Vector3.up, wave * 0.6f, Space.Self);
            spine2.Rotate(Vector3.up, wave * 0.1f, Space.Self);
        }

        private void ApplyNeckStabilization()
        {
            // Head stays level
        }

        // ──────────────────────────────────────────────
        // 공개 API
        // ──────────────────────────────────────────────

        public void SetTargetSpeed(float speed)
        {
            _targetSpeed = speed;
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, _targetSpeed, 5f * Time.deltaTime);
        }

        // ──────────────────────────────────────────────
        // [2026-09-14(49차)] 종별 보행 프로필 동기화 API
        // QuadrupedProceduralAnimation.ApplyMonsterProfile()에서 호출.
        // ──────────────────────────────────────────────

        /// <summary>
        /// [2026-09-14(49차)] 애니 컨트롤러의 종별 프로필 값을 이 모듈에 동기화.
        /// 속도 임계는 UpdateGaitSelection의 gait 선택에, 스텝 파라미터는 발 스윙에 사용된다.
        /// </summary>
        public void SyncProfileParams(float walk, float trot, float pace, float gallop, float stepLen, float stepH)
        {
            _walkSpeed = walk;
            _trotSpeed = trot;
            _paceSpeed = pace;
            _gallopSpeed = gallop;
            _stepLength = stepLen;
            _stepHeight = stepH;
        }

        /// <summary>
        /// [2026-09-14(49차)] 보행 강제 오버라이드 설정. null이면 속도 기반 자동 선택으로 복귀.
        /// (예: rabbit → Gallop 고정으로 도약 호핑 보행)
        /// </summary>
        public void SetGaitOverride(Gait? gait)
        {
            if (_gaitOverride == gait) return;
            _gaitOverride = gait;
            if (gait.HasValue)
                TransitionGait(gait.Value);
            else
                SetGaitPhases(_currentGait); // 자동 선택 복귀 시 위상 재정렬
        }

        /// <summary>
        /// [2026-09-14(49차)] 척추 파동 파라미터 설정 — 악어 등 긴 척추 종의 몸통 굽힘 강화용.
        /// </summary>
        public void SetSpineWave(float amplitude, float frequency)
        {
            _spineWaveAmplitude = amplitude;
            _spineWaveFrequency = frequency;
        }

        public Gait CurrentGait => _currentGait;
        public float CurrentSpeed => _currentSpeed;
    }
}