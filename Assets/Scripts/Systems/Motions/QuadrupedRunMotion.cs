using System;
using System.Collections;
using UnityEngine;

namespace ProjectName.Systems.Motions
{
    /// <summary>
    /// Procedural run motion for a quadruped (4-legged) character.
    /// Uses a gallop/trot pattern with longer strides, more spine extension,
    /// body lower to the ground, and a forward body lean.
    /// Compared to <see cref="QuadrupedWalkMotion"/>, this has higher step
    /// height, longer stride length, faster speed, and a body lean angle.
    /// </summary>
    [AddComponentMenu("ProjectName/Systems/Motions/Quadruped Run Motion")]
    public class QuadrupedRunMotion : MonoBehaviour
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

        [Header("Run Settings")]
        [SerializeField] private float _stepHeight = 0.2f;
        [SerializeField] private float _stepLength = 0.6f;
        [SerializeField] private float _speed = 2.8f;

        [Header("Body Lean")]
        [SerializeField] private float _bodyLeanAngle = 10f; // degrees forward

        [Header("Spine Extension")]
        [SerializeField] private float _spineExtensionAmplitude = 6f; // degrees

        [Header("Head Bob")]
        [SerializeField] private float _headBobAmplitude = 0.04f;

        #endregion

        #region Private State

        private Coroutine _motionRoutine;
        private float _cycleTime;
        private Vector3 _hipsOriginalLocalPos;
        private Vector3 _spineOriginalLocalEuler;
        private Quaternion _headOriginalLocalRot;
        private Vector3 _headOriginalLocalPos;
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

        /// <summary>Run step height in meters.</summary>
        public float StepHeight
        {
            get => _stepHeight;
            set => _stepHeight = Mathf.Max(0f, value);
        }

        /// <summary>Run step length in meters.</summary>
        public float StepLength
        {
            get => _stepLength;
            set => _stepLength = Mathf.Max(0f, value);
        }

        /// <summary>Run cycle speed (cycles per second).</summary>
        public float Speed
        {
            get => _speed;
            set => _speed = Mathf.Max(0f, value);
        }

        /// <summary>Forward body lean angle in degrees.</summary>
        public float BodyLeanAngle
        {
            get => _bodyLeanAngle;
            set => _bodyLeanAngle = Mathf.Clamp(value, 0f, 45f);
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
            if (_motionRoutine != null)
            {
                StopCoroutine(_motionRoutine);
                _motionRoutine = null;
            }
            ResetBonesAndIK();
            // NOTE: _isPlaying is preserved so OnEnable can resume the motion
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
            {
                _headOriginalLocalRot = _headBone.localRotation;
                _headOriginalLocalPos = _headBone.localPosition;
            }
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
        /// Starts the quadruped run motion coroutine. Resets the cycle timer.
        /// </summary>
        public void StartMotion()
        {
            if (_motionRoutine != null)
                StopCoroutine(_motionRoutine);

            CacheOriginalTransforms();
            CaptureIKTargets();
            _cycleTime = 0f;
            _isPlaying = true;
            _motionRoutine = StartCoroutine(RunLoop());
        }

        /// <summary>
        /// Stops the run motion and resets bones/IK to their rest positions.
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
        /// Main run loop: drives the gallop/trot gait cycle with longer strides,
        /// more spine extension, body lower to the ground, and forward lean.
        /// Uses a 2-phase (trot) pattern: diagonal pairs move together.
        /// </summary>
        private IEnumerator RunLoop()
        {
            while (_isPlaying)
            {
                float dt = Time.deltaTime;
                _cycleTime += dt * _speed;

                float phase = _cycleTime % 1f;

                // Trot pattern: diagonal pairs (left hind + right front) and (right hind + left front)
                // This is a simplified trot/gallop blend
                float diagonalA_Phase = phase;          // LH + RF
                float diagonalB_Phase = (phase + 0.5f) % 1f; // RH + LF

                // ── Leg IK cycling (trot: diagonal pairs) ──
                ApplyLegCycle(_hindLeftTarget, _hindLeftRestPos, diagonalA_Phase);
                ApplyLegCycle(_frontRightTarget, _frontRightRestPos, diagonalA_Phase);
                ApplyLegCycle(_hindRightTarget, _hindRightRestPos, diagonalB_Phase);
                ApplyLegCycle(_frontLeftTarget, _frontLeftRestPos, diagonalB_Phase);

                // ── Body lean (forward) ──
                ApplyBodyLean();

                // ── Spine extension ──
                ApplySpineExtension(phase);

                // ── Head bob ──
                ApplyHeadBob(phase);

                yield return null;
            }
        }

        #endregion

        #region Animation Methods

        /// <summary>
        /// Moves a foot IK target through a swing cycle with run parameters
        /// (higher lift and longer stride than walk).
        /// </summary>
        private void ApplyLegCycle(Transform footTarget, Vector3 restPos, float phase)
        {
            if (footTarget == null) return;

            float liftCurve = Mathf.Sin(phase * Mathf.PI); // 0→1→0
            float forwardCurve = Mathf.Sin(phase * Mathf.PI * 2f); // 0→1→0→-1→0

            Vector3 targetPos = restPos;

            // Higher vertical lift for running
            targetPos.y += liftCurve * _stepHeight;

            // Longer horizontal stride
            float stride = forwardCurve * _stepLength * 0.5f;
            targetPos.z += stride;

            footTarget.localPosition = targetPos;
        }

        /// <summary>
        /// Applies spine extension oscillation for running — more pronounced
        /// than walking spine undulation.
        /// </summary>
        private void ApplySpineExtension(float phase)
        {
            if (_spineBone == null) return;

            float extension = Mathf.Sin(phase * Mathf.PI * 2f) * _spineExtensionAmplitude;

            Vector3 euler = _spineBone.localEulerAngles;
            euler.x += extension * 0.7f;
            euler.y += extension * 0.4f;
            _spineBone.localEulerAngles = euler;
        }

        /// <summary>
        /// Applies a pronounced forward body lean for running.
        /// </summary>
        private void ApplyBodyLean()
        {
            if (_spineBone == null) return;

            Vector3 euler = _spineOriginalLocalEuler;
            euler.x += _bodyLeanAngle;
            _spineBone.localEulerAngles = euler;
        }

        /// <summary>
        /// Applies a vertical oscillation to the head with run amplitude.
        /// </summary>
        private void ApplyHeadBob(float phase)
        {
            if (_headBone == null) return;

            float bob = Mathf.Abs(Mathf.Sin(phase * Mathf.PI)) * _headBobAmplitude;
            Vector3 pos = _headOriginalLocalPos;
            pos.y += bob;
            _headBone.localPosition = pos;
        }

        /// <summary>
        /// Resets all bones and IK targets to original positions.
        /// </summary>
        private void ResetBonesAndIK()
        {
            if (_hipsBone != null)
                _hipsBone.localPosition = _hipsOriginalLocalPos;

            if (_spineBone != null)
                _spineBone.localEulerAngles = _spineOriginalLocalEuler;

            if (_headBone != null)
            {
                _headBone.localRotation = _headOriginalLocalRot;
                _headBone.localPosition = _headOriginalLocalPos;
            }

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