using System;
using System.Collections;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems.Motions
{
    /// <summary>
    /// Idle/breathing motion for a quadruped (4-legged) character.
    /// Applies subtle sine-wave breathing to the spine, random head
    /// lowering/raising, periodic ear twitches, and tail swishing.
    /// Designed for rabbit, wolf, boar, deer, manticore, and similar
    /// four-legged creatures.
    /// </summary>
    [AddComponentMenu("ProjectName/Systems/Motions/Quadruped Idle Motion")]
    public class QuadrupedIdleMotion : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Bones")]
        [SerializeField] private Transform _headBone;
        [SerializeField] private Transform _spineBone;
        [SerializeField] private Transform _tailBone;
        [SerializeField] private Transform _earLeftBone;
        [SerializeField] private Transform _earRightBone;

        [Header("Breath Settings")]
        [SerializeField] private float _breathAmplitude = 0.8f;
        [SerializeField] private float _breathFrequency = 0.6f; // Hz

        [Header("Head Look Settings")]
        [SerializeField] private float _lookInterval = 4.0f;
        [SerializeField] private float _lookRange = 12f; // degrees
        [SerializeField] private float _lookSpeed = 45f;

        [Header("Tail Swish Settings")]
        [SerializeField] private float _tailSwishSpeed = 1.2f;
        [SerializeField] private float _tailSwishAmplitude = 8f; // degrees

        [Header("Ear Twitch Settings")]
        [SerializeField] private float _earTwitchInterval = 3.0f;
        [SerializeField] private float _earTwitchAngle = 10f; // degrees

        #endregion

        #region Private State

        private Coroutine _motionRoutine;
        private Vector3 _spineOriginalLocalEuler;
        private Vector3 _tailOriginalLocalEuler;
        private Vector3 _earLeftOriginalLocalEuler;
        private Vector3 _earRightOriginalLocalEuler;
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

        /// <summary>Speed of the tail swish oscillation (Hz).</summary>
        public float TailSwishSpeed
        {
            get => _tailSwishSpeed;
            set => _tailSwishSpeed = Mathf.Max(0f, value);
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
        /// Caches the original local euler/rotation values for smooth blending.
        /// </summary>
        private void CacheOriginalRotations()
        {
            if (_spineBone != null)
                _spineOriginalLocalEuler = _spineBone.localEulerAngles;
            if (_tailBone != null)
                _tailOriginalLocalEuler = _tailBone.localEulerAngles;
            if (_headBone != null)
                _headOriginalLocalRot = _headBone.localRotation;
            if (_earLeftBone != null)
                _earLeftOriginalLocalEuler = _earLeftBone.localEulerAngles;
            if (_earRightBone != null)
                _earRightOriginalLocalEuler = _earRightBone.localEulerAngles;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Starts the quadruped idle motion coroutine.
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
        /// Main quadruped idle loop: applies breath oscillation, head micro-movements,
        /// tail swishing, and periodic ear twitches.
        /// </summary>
        private IEnumerator IdleLoop()
        {
            float lookTimer = UnityEngine.Random.Range(1f, _lookInterval);
            float earTwitchTimer = UnityEngine.Random.Range(0.5f, _earTwitchInterval);
            float headLookTarget = 0f;
            float currentHeadPitch = 0f;
            float tailPhase = 0f;

            while (_isPlaying)
            {
                float dt = Time.deltaTime;
                _breathTimer += dt;

                // ── Breath ──
                ApplyBreath();

                // ── Tail swish ──
                tailPhase += dt * _tailSwishSpeed;
                ApplyTailSwish(tailPhase);

                // ── Head look (lowering/raising) ──
                lookTimer -= dt;
                if (lookTimer <= 0f)
                {
                    headLookTarget = UnityEngine.Random.Range(-_lookRange, _lookRange);
                    lookTimer = _lookInterval + UnityEngine.Random.Range(-1f, 1f);
                }

                currentHeadPitch = Mathf.MoveTowards(currentHeadPitch, headLookTarget, _lookSpeed * dt);
                ApplyHeadLook(currentHeadPitch);

                // ── Ear twitch ──
                earTwitchTimer -= dt;
                if (earTwitchTimer <= 0f)
                {
                    ApplyEarTwitch();
                    earTwitchTimer = _earTwitchInterval + UnityEngine.Random.Range(-0.5f, 0.5f);
                }

                yield return null;
            }
        }

        #endregion

        #region Animation Application

        /// <summary>
        /// Applies a subtle sine-wave oscillation to the spine bone to simulate breathing.
        /// </summary>
        private void ApplyBreath()
        {
            if (_spineBone == null)
                return;

            float breath = Mathf.Sin(_breathTimer * _breathFrequency * Mathf.PI * 2f) * _breathAmplitude;

            Vector3 spineEuler = _spineOriginalLocalEuler;
            spineEuler.x += breath * 0.4f;
            spineEuler.z += breath * 0.12f;
            _spineBone.localEulerAngles = spineEuler;
        }

        /// <summary>
        /// Applies a sinusoidal swish to the tail bone.
        /// </summary>
        private void ApplyTailSwish(float phase)
        {
            if (_tailBone == null)
                return;

            float swish = Mathf.Sin(phase * Mathf.PI * 2f) * _tailSwishAmplitude;
            Vector3 tailEuler = _tailOriginalLocalEuler;
            tailEuler.y += swish;
            _tailBone.localEulerAngles = tailEuler;
        }

        /// <summary>
        /// Applies a subtle pitch rotation to the head (lowering/raising).
        /// </summary>
        private void ApplyHeadLook(float pitch)
        {
            if (_headBone == null) return;

            Quaternion pitchRot = Quaternion.Euler(pitch, 0f, 0f);
            _headBone.localRotation = _headOriginalLocalRot * pitchRot;
        }

        /// <summary>
        /// Applies a quick twitch rotation to the ear bones.
        /// </summary>
        private void ApplyEarTwitch()
        {
            float angle = _earTwitchAngle * (UnityEngine.Random.value > 0.5f ? 1f : -1f);

            if (_earLeftBone != null)
            {
                Vector3 euler = _earLeftOriginalLocalEuler;
                euler.z += angle;
                _earLeftBone.localEulerAngles = euler;
            }

            if (_earRightBone != null)
            {
                Vector3 euler = _earRightOriginalLocalEuler;
                euler.z += angle;
                _earRightBone.localEulerAngles = euler;
            }
        }

        /// <summary>
        /// Resets spine, head, tail, and ears back to original values.
        /// </summary>
        private void ResetBones()
        {
            if (_spineBone != null)
                _spineBone.localEulerAngles = _spineOriginalLocalEuler;

            if (_headBone != null)
                _headBone.localRotation = _headOriginalLocalRot;

            if (_tailBone != null)
                _tailBone.localEulerAngles = _tailOriginalLocalEuler;

            if (_earLeftBone != null)
                _earLeftBone.localEulerAngles = _earLeftOriginalLocalEuler;

            if (_earRightBone != null)
                _earRightBone.localEulerAngles = _earRightOriginalLocalEuler;
        }

        #endregion
    }
}