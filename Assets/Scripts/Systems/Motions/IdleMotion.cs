using System;
using System.Collections;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems.Motions
{
    /// <summary>
    /// Idle/breathing motion for a rigged character.
    /// Applies subtle sine-wave breathing to the spine/chest, random head
    /// micro-movements, and periodic weight shifts between feet.
    /// Uses <see cref="TwoBoneIKController"/> to keep feet planted during
    /// weight-shift cycles.
    /// </summary>
    [AddComponentMenu("ProjectName/Systems/Motions/Idle Motion")]
    public class IdleMotion : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Bones")]
        [SerializeField] private Transform _spineBone;
        [SerializeField] private Transform _chestBone;
        [SerializeField] private Transform _headBone;
        [SerializeField] private Transform _hipsBone;

        [Header("IK Controllers (optional)")]
        [SerializeField] private TwoBoneIKController _leftFootIK;
        [SerializeField] private TwoBoneIKController _rightFootIK;

        [Header("Breath Settings")]
        [SerializeField] private float _breathAmplitude = 0.5f;
        [SerializeField] private float _breathFrequency = 0.8f; // Hz

        [Header("Head Micro-Movements")]
        [SerializeField] private float _lookInterval = 3.5f;
        [SerializeField] private float _lookRange = 15f; // degrees
        [SerializeField] private float _lookSpeed = 60f;

        [Header("Weight Shift")]
        [SerializeField] private float _weightShiftInterval = 2.0f;
        [SerializeField] private float _weightShiftAmount = 0.03f;

        #endregion

        #region Private State

        private Coroutine _motionRoutine;
        private Vector3 _spineOriginalLocalEuler;
        private Vector3 _chestOriginalLocalEuler;
        private Quaternion _headOriginalLocalRot;
        private float _breathTimer;
        private bool _isPlaying;

        #endregion

        #region Public Properties

        /// <summary>Amplitude of the breathing oscillation (degrees).</summary>
        public float BreathAmplitude
        {
            get => _breathAmplitude;
            set => _breathAmplitude = Mathf.Max(0f, value);
        }

        /// <summary>Time between random head looks (seconds).</summary>
        public float LookInterval
        {
            get => _lookInterval;
            set => _lookInterval = Mathf.Max(0.5f, value);
        }

        /// <summary>True while the motion coroutine is running.</summary>
        public bool IsPlaying => _isPlaying;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            CacheOriginalRotations();
        }

        private void OnEnable()
        {
            StartMotion();
        }

        private void OnDisable()
        {
            StopMotion();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Caches the original local euler/rotation values for smooth blending.
        /// </summary>
        private void CacheOriginalRotations()
        {
            if (_spineBone != null)
                _spineOriginalLocalEuler = _spineBone.localEulerAngles;
            if (_chestBone != null)
                _chestOriginalLocalEuler = _chestBone.localEulerAngles;
            if (_headBone != null)
                _headOriginalLocalRot = _headBone.localRotation;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Starts the idle motion coroutine.
        /// Resets the breath timer and caches bone rotations.
        /// </summary>
        public void StartMotion()
        {
            if (_motionRoutine != null)
                StopCoroutine(_motionRoutine);

            CacheOriginalRotations();
            _breathTimer = 0f;
            _isPlaying = true;
            _motionRoutine = StartCoroutine(IdleLoop());
        }

        /// <summary>
        /// Stops the idle motion coroutine and resets bones to their original pose.
        /// </summary>
        public void StopMotion()
        {
            _isPlaying = false;

            if (_motionRoutine != null)
            {
                StopCoroutine(_motionRoutine);
                _motionRoutine = null;
            }

            ResetBones();
        }

        #endregion

        #region Motion Coroutine

        /// <summary>
        /// Main idle loop: applies breath oscillation, head micro-movements,
        /// and periodic weight shifts.
        /// </summary>
        private IEnumerator IdleLoop()
        {
            float lookTimer = UnityEngine.Random.Range(1f, _lookInterval);
            float shiftTimer = UnityEngine.Random.Range(0.5f, _weightShiftInterval);
            float headLookTarget = 0f;
            float currentHeadYaw = 0f;
            bool shiftingLeft = true;

            while (_isPlaying)
            {
                float dt = Time.deltaTime;
                _breathTimer += dt;

                // ── Breath ──
                ApplyBreath();

                // ── Head micro-movements ──
                lookTimer -= dt;
                if (lookTimer <= 0f)
                {
                    headLookTarget = UnityEngine.Random.Range(-_lookRange, _lookRange);
                    lookTimer = _lookInterval + UnityEngine.Random.Range(-1f, 1f);
                }

                currentHeadYaw = Mathf.MoveTowards(currentHeadYaw, headLookTarget, _lookSpeed * dt);
                ApplyHeadRotation(currentHeadYaw);

                // ── Weight shift ──
                shiftTimer -= dt;
                if (shiftTimer <= 0f)
                {
                    shiftingLeft = !shiftingLeft;
                    shiftTimer = _weightShiftInterval + UnityEngine.Random.Range(-0.3f, 0.3f);
                }

                ApplyWeightShift(shiftingLeft);

                yield return null;
            }
        }

        #endregion

        #region Animation Application

        /// <summary>
        /// Applies a subtle sine-wave oscillation to the spine and chest bones
        /// to simulate breathing.
        /// </summary>
        private void ApplyBreath()
        {
            if (_spineBone == null && _chestBone == null)
                return;

            float breath = Mathf.Sin(_breathTimer * _breathFrequency * Mathf.PI * 2f) * _breathAmplitude;

            if (_spineBone != null)
            {
                Vector3 spineEuler = _spineOriginalLocalEuler;
                spineEuler.x += breath * 0.3f;
                spineEuler.z += breath * 0.1f;
                _spineBone.localEulerAngles = spineEuler;
            }

            if (_chestBone != null)
            {
                Vector3 chestEuler = _chestOriginalLocalEuler;
                chestEuler.x += breath * 0.5f;
                chestEuler.z += breath * 0.15f;
                _chestBone.localEulerAngles = chestEuler;
            }
        }

        /// <summary>
        /// Applies a subtle yaw rotation to the head for micro-movements.
        /// </summary>
        private void ApplyHeadRotation(float yaw)
        {
            if (_headBone == null) return;

            Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);
            _headBone.localRotation = _headOriginalLocalRot * yawRot;
        }

        /// <summary>
        /// Shifts the hips slightly to one side to simulate weight bearing.
        /// Adjusts foot IK targets to keep feet planted.
        /// </summary>
        private void ApplyWeightShift(bool shiftLeft)
        {
            if (_hipsBone == null) return;

            float shift = shiftLeft ? _weightShiftAmount : -_weightShiftAmount;
            Vector3 hipsPos = _hipsBone.localPosition;
            hipsPos.x = shift;
            _hipsBone.localPosition = hipsPos;

            // Keep feet planted by adjusting IK targets
            if (_leftFootIK != null && _rightFootIK != null)
            {
                // Feet stay at their current world position — IK handles the rest
                _leftFootIK.BlendWeight = 1f;
                _rightFootIK.BlendWeight = 1f;
            }
        }

        /// <summary>
        /// Resets spine, chest, head, and hips back to original values.
        /// </summary>
        private void ResetBones()
        {
            if (_spineBone != null)
                _spineBone.localEulerAngles = _spineOriginalLocalEuler;

            if (_chestBone != null)
                _chestBone.localEulerAngles = _chestOriginalLocalEuler;

            if (_headBone != null)
                _headBone.localRotation = _headOriginalLocalRot;

            if (_hipsBone != null)
            {
                Vector3 hipsPos = _hipsBone.localPosition;
                hipsPos.x = 0f;
                _hipsBone.localPosition = hipsPos;
            }

            if (_leftFootIK != null)
                _leftFootIK.BlendWeight = 1f;

            if (_rightFootIK != null)
                _rightFootIK.BlendWeight = 1f;
        }

        #endregion
    }
}
