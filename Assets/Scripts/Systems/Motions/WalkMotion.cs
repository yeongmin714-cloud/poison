using System;
using System.Collections;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems.Motions
{
    /// <summary>
    /// Procedural walk motion for a rigged character.
    /// Drives leg IK targets through a swing cycle, applies opposite-arm
    /// swinging, forward spine tilt, and a vertical body bob with each step.
    /// </summary>
    [AddComponentMenu("ProjectName/Systems/Motions/Walk Motion")]
    public class WalkMotion : MonoBehaviour
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

        [Header("Walk Settings")]
        [SerializeField] private float _stepHeight = 0.15f;
        [SerializeField] private float _stepLength = 0.4f;
        [SerializeField] private float _speed = 1.5f;
        [SerializeField] private float _armSwingAmount = 20f; // degrees
        [SerializeField] private float _spineTilt = 5f;      // degrees forward
        [SerializeField] private float _bodyBobAmplitude = 0.03f;

        #endregion

        #region Private State

        private Coroutine _motionRoutine;
        private float _cycleTime;
        private Vector3 _hipsOriginalLocalPos;
        private Vector3 _spineOriginalLocalEuler;
        private bool _isPlaying;

        // Foot IK target game objects for cycling
        private Transform _leftFootTarget;
        private Transform _rightFootTarget;
        private Vector3 _leftFootRestPos;
        private Vector3 _rightFootRestPos;

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
        }

        /// <summary>
        /// Captures the current IK target transforms and records their rest positions.
        /// Creates default targets if none exist.
        /// </summary>
        private void CaptureIKTargets()
        {
            if (_leftLegIK != null)
            {
                _leftFootTarget = _leftLegIK.Target;
                if (_leftFootTarget != null)
                    _leftFootRestPos = _leftFootTarget.localPosition;
            }

            if (_rightLegIK != null)
            {
                _rightFootTarget = _rightLegIK.Target;
                if (_rightFootTarget != null)
                    _rightFootRestPos = _rightFootTarget.localPosition;
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Starts the walk motion coroutine. Resets the cycle timer.
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
        /// Main walk loop: drives leg swing cycle, arm swing, spine tilt, body bob.
        /// </summary>
        private IEnumerator WalkLoop()
        {
            while (_isPlaying)
            {
                float dt = Time.deltaTime;
                _cycleTime += dt * _speed;

                float phase = _cycleTime % 1f; // 0..1

                // Leg cycle: left foot forward when phase=0..0.5, right foot forward when phase=0.5..1
                float leftPhase = phase;
                float rightPhase = (phase + 0.5f) % 1f;

                // ── Leg IK cycling ──
                ApplyLegCycle(_leftFootTarget, _leftFootRestPos, leftPhase, true);
                ApplyLegCycle(_rightFootTarget, _rightFootRestPos, rightPhase, false);

                // ── Arm swing (opposite to legs) ──
                ApplyArmSwing(leftPhase, rightPhase);

                // ── Spine tilt ──
                ApplySpineTilt();

                // ── Body bob ──
                ApplyBodyBob(phase);

                yield return null;
            }
        }

        #endregion

        #region Animation Methods

        /// <summary>
        /// Moves the foot IK target through a swing cycle: lift → move forward → plant.
        /// </summary>
        /// <param name="footTarget">The IK target transform for the foot.</param>
        /// <param name="restPos">The rest local position of the foot target.</param>
        /// <param name="phase">Cycle phase 0..1.</param>
        /// <param name="isLeft">True for the left foot.</param>
        private void ApplyLegCycle(Transform footTarget, Vector3 restPos, float phase, bool isLeft)
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
            targetPos.z += isLeft ? stride : stride;

            footTarget.localPosition = targetPos;
        }

        /// <summary>
        /// Applies opposite-arm swing relative to the leg cycle.
        /// </summary>
        private void ApplyArmSwing(float leftLegPhase, float rightLegPhase)
        {
            if (_leftArmBone != null)
            {
                // Left arm swings opposite to left leg
                float swing = Mathf.Sin(leftLegPhase * Mathf.PI * 2f) * _armSwingAmount;
                _leftArmBone.localEulerAngles = new Vector3(swing, 0f, 0f);
            }

            if (_rightArmBone != null)
            {
                float swing = Mathf.Sin(rightLegPhase * Mathf.PI * 2f) * _armSwingAmount;
                _rightArmBone.localEulerAngles = new Vector3(swing, 0f, 0f);
            }

            // Also drive arm IK if available
            if (_leftArmIK != null && _leftArmIK.Target != null)
            {
                float swing = Mathf.Sin(leftLegPhase * Mathf.PI * 2f) * _armSwingAmount * 0.01f;
                Vector3 armPos = _leftArmIK.Target.localPosition;
                armPos.z += swing;
                _leftArmIK.Target.localPosition = armPos;
            }

            if (_rightArmIK != null && _rightArmIK.Target != null)
            {
                float swing = Mathf.Sin(rightLegPhase * Mathf.PI * 2f) * _armSwingAmount * 0.01f;
                Vector3 armPos = _rightArmIK.Target.localPosition;
                armPos.z += swing;
                _rightArmIK.Target.localPosition = armPos;
            }
        }

        /// <summary>
        /// Applies a slight forward lean to the spine.
        /// </summary>
        private void ApplySpineTilt()
        {
            if (_spineBone == null) return;

            Vector3 euler = _spineOriginalLocalEuler;
            euler.x += _spineTilt;
            _spineBone.localEulerAngles = euler;
        }

        /// <summary>
        /// Applies a vertical oscillation to the hips for body bob.
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
        /// Resets all bones and IK targets to their original rest positions.
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
