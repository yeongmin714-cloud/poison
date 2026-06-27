using System;
using System.Collections;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems.Motions
{
    /// <summary>
    /// Procedural run motion for a rigged character.
    /// Similar to <see cref="WalkMotion"/> but with faster cycle speed,
    /// higher steps, longer stride, more arm swing, and a greater
    /// forward body lean.
    /// </summary>
    [AddComponentMenu("ProjectName/Systems/Motions/Run Motion")]
    public class RunMotion : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Bones")]
        [SerializeField] private Transform _hipsBone;
        [SerializeField] private Transform _spineBone;
        [SerializeField] private Transform _leftArmBone;
        [SerializeField] private Transform _rightArmBone;

        [Header("IK Controllers")]
        [SerializeField] private TwoBoneIKController _leftLegIK;
        [SerializeField] private TwoBoneIKController _rightLegIK;
        [SerializeField] private TwoBoneIKController _leftArmIK;
        [SerializeField] private TwoBoneIKController _rightArmIK;

        [Header("Run Settings")]
        [SerializeField] private float _stepHeight = 0.25f;
        [SerializeField] private float _stepLength = 0.7f;
        [SerializeField] private float _speed = 3.0f;
        [SerializeField] private float _armSwingAmount = 35f;  // degrees
        [SerializeField] private float _bodyLeanAngle = 12f;   // degrees forward
        [SerializeField] private float _bodyBobAmplitude = 0.05f;

        #endregion

        #region Private State

        private Coroutine _motionRoutine;
        private float _cycleTime;
        private Vector3 _hipsOriginalLocalPos;
        private Vector3 _spineOriginalLocalEuler;
        private bool _isPlaying;

        private Transform _leftFootTarget;
        private Transform _rightFootTarget;
        private Vector3 _leftFootRestPos;
        private Vector3 _rightFootRestPos;

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
            StopMotion();
        }

        #endregion

        #region Initialization

        private void CacheOriginalTransforms()
        {
            if (_hipsBone != null)
                _hipsOriginalLocalPos = _hipsBone.localPosition;
            if (_spineBone != null)
                _spineOriginalLocalEuler = _spineBone.localEulerAngles;
        }

        private void CaptureIKTargets()
        {
            if (_leftLegIK != null && _leftLegIK.Target != null)
            {
                _leftFootTarget = _leftLegIK.Target;
                _leftFootRestPos = _leftFootTarget.localPosition;
            }

            if (_rightLegIK != null && _rightLegIK.Target != null)
            {
                _rightFootTarget = _rightLegIK.Target;
                _rightFootRestPos = _rightFootTarget.localPosition;
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Starts the run motion coroutine.
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
        /// Stops the run motion and resets bones/IK.
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
        /// Main run loop: faster leg cycle, higher steps, more arm swing,
        /// greater body lean.
        /// </summary>
        private IEnumerator RunLoop()
        {
            while (_isPlaying)
            {
                float dt = Time.deltaTime;
                _cycleTime += dt * _speed;

                float phase = _cycleTime % 1f;
                float leftPhase = phase;
                float rightPhase = (phase + 0.5f) % 1f;

                // ── Leg cycle (higher steps, longer stride) ──
                ApplyLegCycle(_leftFootTarget, _leftFootRestPos, leftPhase, true);
                ApplyLegCycle(_rightFootTarget, _rightFootRestPos, rightPhase, false);

                // ── Arm swing (more aggressive) ──
                ApplyArmSwing(leftPhase, rightPhase);

                // ── Body lean ──
                ApplyBodyLean();

                // ── Body bob ──
                ApplyBodyBob(phase);

                yield return null;
            }
        }

        #endregion

        #region Animation Methods

        /// <summary>
        /// Moves the foot IK target through a swing cycle with run parameters.
        /// </summary>
        private void ApplyLegCycle(Transform footTarget, Vector3 restPos, float phase, bool isLeft)
        {
            if (footTarget == null) return;

            // Run has a more explosive lift and longer stride
            float liftCurve = Mathf.Sin(phase * Mathf.PI);
            float forwardCurve = Mathf.Sin(phase * Mathf.PI * 2f);

            Vector3 targetPos = restPos;

            // Higher vertical lift for running
            targetPos.y += liftCurve * _stepHeight;

            // Longer horizontal stride
            float stride = forwardCurve * _stepLength * 0.5f;
            targetPos.z += isLeft ? stride : stride;

            footTarget.localPosition = targetPos;
        }

        /// <summary>
        /// Applies aggressive opposite-arm swing for running.
        /// </summary>
        private void ApplyArmSwing(float leftLegPhase, float rightLegPhase)
        {
            if (_leftArmBone != null)
            {
                float swing = Mathf.Sin(leftLegPhase * Mathf.PI * 2f) * _armSwingAmount;
                _leftArmBone.localEulerAngles = new Vector3(swing, 0f, 0f);
            }

            if (_rightArmBone != null)
            {
                float swing = Mathf.Sin(rightLegPhase * Mathf.PI * 2f) * _armSwingAmount;
                _rightArmBone.localEulerAngles = new Vector3(swing, 0f, 0f);
            }

            if (_leftArmIK != null && _leftArmIK.Target != null)
            {
                float swing = Mathf.Sin(leftLegPhase * Mathf.PI * 2f) * _armSwingAmount * 0.015f;
                Vector3 armPos = _leftArmIK.Target.localPosition;
                armPos.z += swing;
                _leftArmIK.Target.localPosition = armPos;
            }

            if (_rightArmIK != null && _rightArmIK.Target != null)
            {
                float swing = Mathf.Sin(rightLegPhase * Mathf.PI * 2f) * _armSwingAmount * 0.015f;
                Vector3 armPos = _rightArmIK.Target.localPosition;
                armPos.z += swing;
                _rightArmIK.Target.localPosition = armPos;
            }
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
        /// Applies a vertical oscillation to the hips.
        /// </summary>
        private void ApplyBodyBob(float phase)
        {
            if (_hipsBone == null) return;

            float bob = Mathf.Abs(Mathf.Sin(phase * Mathf.PI)) * _bodyBobAmplitude;
            Vector3 pos = _hipsOriginalLocalPos;
            pos.y += bob;
            _hipsBone.localPosition = pos;
        }

        /// <summary>
        /// Resets bones and IK targets to original positions.
        /// </summary>
        private void ResetBonesAndIK()
        {
            if (_hipsBone != null)
                _hipsBone.localPosition = _hipsOriginalLocalPos;

            if (_spineBone != null)
                _spineBone.localEulerAngles = _spineOriginalLocalEuler;

            if (_leftArmBone != null)
                _leftArmBone.localEulerAngles = Vector3.zero;

            if (_rightArmBone != null)
                _rightArmBone.localEulerAngles = Vector3.zero;

            if (_leftFootTarget != null)
                _leftFootTarget.localPosition = _leftFootRestPos;

            if (_rightFootTarget != null)
                _rightFootTarget.localPosition = _rightFootRestPos;
        }

        #endregion
    }
}
