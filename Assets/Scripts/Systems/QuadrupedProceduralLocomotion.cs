using System.Collections.Generic;
using UnityEngine;

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
            UpdateGaitTargets();
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
        // 발 타겟 계산
        // ──────────────────────────────────────────────

        private void UpdateGaitTargets()
        {
            UpdateLegTarget(_procAnim.LF_Phase, ref _procAnim.LF_Target, ref _procAnim.LF_Hint, ProceduralBoneUtility.BoneRole.L_Hip, ProceduralBoneUtility.BoneRole.L_Knee, ProceduralBoneUtility.BoneRole.L_Ankle);
            UpdateLegTarget(_procAnim.RF_Phase, ref _procAnim.RF_Target, ref _procAnim.RF_Hint, ProceduralBoneUtility.BoneRole.R_Hip, ProceduralBoneUtility.BoneRole.R_Knee, ProceduralBoneUtility.BoneRole.R_Ankle);
            UpdateLegTarget(_procAnim.LH_Phase, ref _procAnim.LH_Target, ref _procAnim.LH_Hint, ProceduralBoneUtility.BoneRole.L_Hip, ProceduralBoneUtility.BoneRole.L_Knee, ProceduralBoneUtility.BoneRole.L_Ankle); // Reuse for hind
            UpdateLegTarget(_procAnim.RH_Phase, ref _procAnim.RH_Target, ref _procAnim.RH_Hint, ProceduralBoneUtility.BoneRole.R_Hip, ProceduralBoneUtility.BoneRole.R_Knee, ProceduralBoneUtility.BoneRole.R_Ankle);
        }

        private void UpdateLegTarget(float phase, ref Vector3 target, ref Vector3 hint, ProceduralBoneUtility.BoneRole hipRole, ProceduralBoneUtility.BoneRole kneeRole, ProceduralBoneUtility.BoneRole ankleRole)
        {
            if (phase < 0.7f) // Stance
            {
                // Keep grounded - handled by ground detection
            }
            else // Swing
            {
                float swingProgress = (phase - 0.7f) / 0.3f;
                float height = Mathf.Sin(swingProgress * Mathf.PI) * _stepHeight;
                float forward = swingProgress * _stepLength;

                // target updated in main proc anim
            }
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
            if (!_boneMap.Has(ProceduralBoneUtility.BoneRole.Spine0) ||
                !_boneMap.Has(ProceduralBoneUtility.BoneRole.Spine1) ||
                !_boneMap.Has(ProceduralBoneUtility.BoneRole.Spine2))
                return;

            var spine0 = _boneMap.Get(ProceduralBoneUtility.BoneRole.Spine0);
            var spine1 = _boneMap.Get(ProceduralBoneUtility.BoneRole.Spine1);
            var spine2 = _boneMap.Get(ProceduralBoneUtility.BoneRole.Spine2);

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

        public Gait CurrentGait => _currentGait;
        public float CurrentSpeed => _currentSpeed;
    }
}