using System;
using System.Collections;
using UnityEngine;

namespace ProjectName.Systems.Motions
{
    /// <summary>
    /// Procedural walk motion for a quadruped (4-legged) character.
    /// Drives a 4-phase gait cycle: left hind → left front → right hind → right front.
    /// Applies spine undulation (sinusoidal wave), head bob per stride,
    /// and configurable step parameters.
    /// </summary>
    [AddComponentMenu("ProjectName/Systems/Motions/Quadruped Walk Motion")]
    public class QuadrupedWalkMotion : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Bones")]
        [SerializeField] private Transform _hipsBone;
        [SerializeField] private Transform _spineBone;
        [SerializeField] private Transform _headBone;

        [Header("Leg IK Controllers (Quadruped)")]
        [SerializeField] private TwoBoneIKController _hindLeftIK;
        [SerializeField] private TwoBoneIKController _hindRightIK;
        [SerializeField] private TwoBoneIKController _frontLeftIK;
        [SerializeField] private TwoBoneIKController _frontRightIK;

        [Header("Walk Settings")]
        [SerializeField] private float _stepHeight = 0.12f;
        [SerializeField] private float _stepLength = 0.35f;
        [SerializeField] private float _speed = 1.2f;

        [Header("Spine Undulation")]
        [SerializeField] private float _spineWaveAmplitude = 4f; // degrees
        [SerializeField] private float _spineWaveFrequency = 1f;

        [Header("Head Bob")]
        [SerializeField] private float _headBobAmplitude = 0.02f;

        #endregion

        #region Private State

        private Coroutine _motionRoutine;
        private float _cycleTime;
        private Vector3 _hipsOriginalLocalPos;
        private Vector3 _spineOriginalLocalEuler;
        private Quaternion _headOriginalLocalRot;
        private bool _isPlaying;

        // IK target transforms and rest positions
        private Transform _hindLeftTarget;
        private Transform _hindRightTarget;
        private Transform _frontLeftTarget;
        private Transform _frontRightTarget;
        private Vector3 _hindLeftRestPos;
        private Vector3 _hindRightRestPos;
        private Vector3 _frontLeftRestPos;
        private Vector3 _frontRightRestPos;

        #endregion

        #region Public Properties

        /// <summary>Step height in meters.</summary>
        public float StepHeight
        {
            get => _stepHeight;
            set => _stepHeight = Mathf.Max(0f, value);
        }

        /// <summary>Step length in meters.</summary>
        public float StepLength
        {
            get => _stepLength;
            set => _stepLength = Mathf.Max(0f, value);
        }

        /// <summary>Walk cycle speed (cycles per second).</summary>
        public float Speed
        {
            get => _speed;
            set => _speed = Mathf.Max(0f, value);
        }

        /// <summary>True while the motion coroutine is running.</summary>
        public bool IsPlaying => _isPlaying;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            CacheOriginalTransforms();
            CaptureIKTargets();
        }

        private void OnEnable()
        {
            if (_isPlaying)
                StartMotion();
        }

        private void OnDisable()
        {
            StopMotion();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Caches original local positions and rotations for blending.
        /// </summary>
        private void CacheOriginalTransforms()
        {
            if (_hipsBone != null)
                _hipsOriginalLocalPos = _hipsBone.localPosition;
            if (_spineBone != null)
                _spineOriginalLocalEuler = _spineBone.localEulerAngles;
            if (_headBone != null)
                _headOriginalLocalRot = _headBone.localRotation;
        }

        /// <summary>
        /// Captures the current IK target transforms and records their rest positions.
        /// </summary>
        private void CaptureIKTargets()
        {
            if (_hindLeftIK != null && _hindLeftIK.Target != null)
            {
                _hindLeftTarget = _hindLeftIK.Target;
                _hindLeftRestPos = _hindLeftTarget.localPosition;
            }

            if (_hindRightIK != null && _hindRightIK.Target != null)
            {
                _hindRightTarget = _hindRightIK.Target;
                _hindRightRestPos = _hindRightTarget.localPosition;
            }

            if (_frontLeftIK != null && _frontLeftIK.Target != null)
            {
                _frontLeftTarget = _frontLeftIK.Target;
                _frontLeftRestPos = _frontLeftTarget.localPosition;
            }

            if (_frontRightIK != null && _frontRightIK.Target != null)
            {
                _frontRightTarget = _frontRightIK.Target;
                _frontRightRestPos = _frontRightTarget.localPosition;
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Starts the quadruped walk motion coroutine. Resets the cycle timer.
        /// </summary>
        public void StartMotion()
        {
            if (_motionRoutine != null)
                StopCoroutine(_motionRoutine);

            CacheOriginalTransforms();
            CaptureIKTargets();
            _cycleTime = 0f;
            _isPlaying = true;
            _motionRoutine = StartCoroutine(WalkLoop());
        }

        /// <summary>
        /// Stops the walk motion and resets bones/IK to their rest positions.
        /// </summary>
        public void StopMotion()
        {
            _isPlaying = false;

            if (_motionRoutine != null)
            {
                StopCoroutine(_motionRoutine);
                _motionRoutine = null;
            }

            ResetBonesAndIK();
        }

        #endregion

        #region Motion Coroutine

        /// <summary>
        /// Main walk loop: drives the 4-phase gait cycle, spine undulation, head bob.
        /// Gait order: left hind → left front → right hind → right front (phase offsets 0, 0.25, 0.5, 0.75).
        /// </summary>
        private IEnumerator WalkLoop()
        {
            while (_isPlaying)
            {
                float dt = Time.deltaTime;
                _cycleTime += dt * _speed;

                float phase = _cycleTime % 1f; // 0..1

                // 4-phase gait: each leg has a phase offset of 0.25 (90 degrees apart)
                float hindLeftPhase = phase;
                float frontLeftPhase = (phase + 0.25f) % 1f;
                float hindRightPhase = (phase + 0.5f) % 1f;
                float frontRightPhase = (phase + 0.75f) % 1f;

                // ── Leg IK cycling ──
                ApplyLegCycle(_hindLeftTarget, _hindLeftRestPos, hindLeftPhase);
                ApplyLegCycle(_hindRightTarget, _hindRightRestPos, hindRightPhase);
                ApplyLegCycle(_frontLeftTarget, _frontLeftRestPos, frontLeftPhase);
                ApplyLegCycle(_frontRightTarget, _frontRightRestPos, frontRightPhase);

                // ── Spine undulation (sinusoidal wave along spine) ──
                ApplySpineUndulation(phase);

                // ── Head bob ──
                ApplyHeadBob(phase);

                yield return null;
            }
        }

        #endregion

        #region Animation Methods

        /// <summary>
        /// Moves a foot IK target through a swing cycle: lift → move forward → plant.
        /// </summary>
        /// <param name="footTarget">The IK target transform for the foot.</param>
        /// <param name="restPos">The rest local position of the foot target.</param>
        /// <param name="phase">Cycle phase 0..1.</param>
        private void ApplyLegCycle(Transform footTarget, Vector3 restPos, float phase)
        {
            if (footTarget == null) return;

            // Step curve: lift in the first half, plant in the second
            float liftCurve = Mathf.Sin(phase * Mathf.PI); // 0→1→0
            float forwardCurve = Mathf.Sin(phase * Mathf.PI * 2f); // 0→1→0→-1→0

            Vector3 targetPos = restPos;

            // Vertical lift
            targetPos.y += liftCurve * _stepHeight;

            // Horizontal stride
            float stride = forwardCurve * _stepLength * 0.5f;
            targetPos.z += stride;

            footTarget.localPosition = targetPos;
        }

        /// <summary>
        /// Applies a sinusoidal wave along the spine to simulate quadruped
        /// spine undulation during walking.
        /// </summary>
        private void ApplySpineUndulation(float phase)
        {
            if (_spineBone == null) return;

            float wave = Mathf.Sin(phase * Mathf.PI * 2f * _spineWaveFrequency) * _spineWaveAmplitude;

            Vector3 euler = _spineOriginalLocalEuler;
            euler.x += wave * 0.5f;
            euler.y += wave * 0.3f;
            _spineBone.localEulerAngles = euler;
        }

        /// <summary>
        /// Applies a slight vertical oscillation to the head per stride.
        /// </summary>
        private void ApplyHeadBob(float phase)
        {
            if (_headBone == null) return;

            float bob = Mathf.Abs(Mathf.Sin(phase * Mathf.PI)) * _headBobAmplitude;
            Vector3 pos = _headBone.localPosition;
            pos.y += bob;
            _headBone.localPosition = pos;
        }

        /// <summary>
        /// Resets all bones and IK targets to their original rest positions.
        /// </summary>
        private void ResetBonesAndIK()
        {
            if (_hipsBone != null)
                _hipsBone.localPosition = _hipsOriginalLocalPos;

            if (_spineBone != null)
                _spineBone.localEulerAngles = _spineOriginalLocalEuler;

            if (_headBone != null)
                _headBone.localRotation = _headOriginalLocalRot;

            if (_hindLeftTarget != null)
                _hindLeftTarget.localPosition = _hindLeftRestPos;

            if (_hindRightTarget != null)
                _hindRightTarget.localPosition = _hindRightRestPos;

            if (_frontLeftTarget != null)
                _frontLeftTarget.localPosition = _frontLeftRestPos;

            if (_frontRightTarget != null)
                _frontRightTarget.localPosition = _frontRightRestPos;
        }

        #endregion
    }
}