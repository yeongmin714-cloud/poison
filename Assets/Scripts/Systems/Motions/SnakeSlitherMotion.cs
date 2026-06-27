using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems.Motions
{
    /// <summary>
    /// Procedural slither motion for snake-like creatures.
    /// Uses a chain of spine bones to propagate a sinusoidal wave,
    /// creating an S-curve slithering effect. Configurable wave
    /// amplitude, frequency, and speed.
    /// </summary>
    [AddComponentMenu("ProjectName/Systems/Motions/Snake Slither Motion")]
    public class SnakeSlitherMotion : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Spine Bones (chain, head to tail)")]
        [SerializeField] private List<Transform> _spineBones = new List<Transform>();

        [Header("Slither Settings")]
        [SerializeField] private float _waveAmplitude = 15f;   // degrees
        [SerializeField] private float _waveFrequency = 0.8f;  // Hz
        [SerializeField] private float _waveSpeed = 1.5f;

        [Header("Body Offset")]
        [SerializeField] private float _bodyVerticalOffset = 0.02f; // subtle lift

        #endregion

        #region Private State

        private Coroutine _motionRoutine;
        private List<Vector3> _originalLocalEulers = new List<Vector3>();
        private List<Vector3> _originalLocalPositions = new List<Vector3>();
        private float _waveTimer;
        private bool _isPlaying;

        #endregion

        #region Public Properties

        /// <summary>Maximum wave angle amplitude in degrees.</summary>
        public float WaveAmplitude
        {
            get => _waveAmplitude;
            set => _waveAmplitude = Mathf.Max(0f, value);
        }

        /// <summary>Wave oscillation frequency in Hz.</summary>
        public float WaveFrequency
        {
            get => _waveFrequency;
            set => _waveFrequency = Mathf.Max(0f, value);
        }

        /// <summary>Wave propagation speed multiplier.</summary>
        public float WaveSpeed
        {
            get => _waveSpeed;
            set => _waveSpeed = Mathf.Max(0f, value);
        }

        /// <summary>Number of spine segments in the chain.</summary>
        public int SegmentCount => _spineBones.Count;

        /// <summary>True while the motion coroutine is running.</summary>
        public bool IsPlaying => _isPlaying;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            CacheOriginalTransforms();
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
        /// Caches the original local euler angles and positions for all spine bones.
        /// </summary>
        private void CacheOriginalTransforms()
        {
            _originalLocalEulers.Clear();
            _originalLocalPositions.Clear();

            for (int i = 0; i < _spineBones.Count; i++)
            {
                if (_spineBones[i] != null)
                {
                    _originalLocalEulers.Add(_spineBones[i].localEulerAngles);
                    _originalLocalPositions.Add(_spineBones[i].localPosition);
                }
                else
                {
                    _originalLocalEulers.Add(Vector3.zero);
                    _originalLocalPositions.Add(Vector3.zero);
                }
            }
        }

        /// <summary>
        /// Sets the spine bone chain from a list of transforms (head to tail).
        /// </summary>
        /// <param name="bones">Ordered list of spine bone transforms.</param>
        public void SetSpineBones(List<Transform> bones)
        {
            if (bones == null)
            {
                Debug.LogWarning("[SnakeSlitherMotion] Cannot set spine bones to null.", this);
                return;
            }

            _spineBones = new List<Transform>(bones);
            CacheOriginalTransforms();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Starts the snake slither motion coroutine.
        /// Resets the wave timer and caches bone transforms.
        /// </summary>
        public void StartMotion()
        {
            if (_motionRoutine != null)
                StopCoroutine(_motionRoutine);

            if (_spineBones.Count == 0)
            {
                Debug.LogWarning("[SnakeSlitherMotion] No spine bones assigned. Slither motion cannot start.", this);
                return;
            }

            CacheOriginalTransforms();
            _waveTimer = 0f;
            _isPlaying = true;
            _motionRoutine = StartCoroutine(SlitherLoop());
        }

        /// <summary>
        /// Stops the slither motion coroutine and resets all spine bones
        /// to their original transforms.
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
        /// Main slither loop: propagates a sinusoidal wave along the spine chain.
        /// Each bone's angle is determined by its position along the chain,
        /// creating an S-curve that propagates over time.
        /// </summary>
        private IEnumerator SlitherLoop()
        {
            while (_isPlaying)
            {
                float dt = Time.deltaTime;
                _waveTimer += dt * _waveSpeed;

                int boneCount = _spineBones.Count;

                for (int i = 0; i < boneCount; i++)
                {
                    Transform bone = _spineBones[i];
                    if (bone == null) continue;

                    // Each bone's phase shifts along the chain: head leads, tail follows
                    // Normalized position along the spine (0 = head, 1 = tail)
                    float t = (float)i / Mathf.Max(1, boneCount - 1);

                    // Wave propagates from head to tail with a phase offset
                    float phase = _waveTimer + t * _waveFrequency * Mathf.PI * 2f;

                    // Horizontal undulation (yaw) — main slither motion
                    float yaw = Mathf.Sin(phase) * _waveAmplitude;

                    // Subtle vertical undulation (pitch) — gives body a slight ripple
                    float pitch = Mathf.Sin(phase * 0.7f + 1.2f) * _waveAmplitude * 0.3f;

                    Vector3 euler = _originalLocalEulers[i];
                    euler.y += yaw;
                    euler.x += pitch;
                    bone.localEulerAngles = euler;

                    // Subtle vertical body offset for a more organic look
                    if (_bodyVerticalOffset > 0f)
                    {
                        Vector3 pos = _originalLocalPositions[i];
                        pos.y += Mathf.Sin(phase * 1.3f) * _bodyVerticalOffset;
                        bone.localPosition = pos;
                    }
                }

                yield return null;
            }
        }

        #endregion

        #region Reset

        /// <summary>
        /// Resets all spine bones back to their original transforms.
        /// </summary>
        private void ResetBones()
        {
            for (int i = 0; i < _spineBones.Count; i++)
            {
                if (_spineBones[i] == null) continue;

                if (i < _originalLocalEulers.Count)
                    _spineBones[i].localEulerAngles = _originalLocalEulers[i];

                if (i < _originalLocalPositions.Count)
                    _spineBones[i].localPosition = _originalLocalPositions[i];
            }
        }

        #endregion
    }
}