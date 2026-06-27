using System;
using System.Collections;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems.Motions
{
    /// <summary>
    /// Procedural jump motion with four phases:
    /// Crouch → Launch → Air → Land.
    /// Drives leg IK targets, spine/hip positions, and arm positions
    /// through each phase.
    /// </summary>
    [AddComponentMenu("ProjectName/Systems/Motions/Jump Motion")]
    public class JumpMotion : MonoBehaviour
    {
        #region Nested Types

        /// <summary>Phases of the jump sequence.</summary>
        public enum JumpPhase
        {
            /// <summary>Character bends knees, lowers body.</summary>
            Crouch,
            /// <summary>Character extends legs rapidly, arms up.</summary>
            Launch,
            /// <summary>Character in mid-air, legs tucked, arms balanced.</summary>
            Air,
            /// <summary>Character lands, legs absorb impact, slight crouch.</summary>
            Land
        }

        #endregion

        #region Serialized Fields

        [Header("Bones")]
        [SerializeField] private Transform _hipsBone;
        [SerializeField] private Transform _spineBone;
        [SerializeField] private Transform _leftArmBone;
        [SerializeField] private Transform _rightArmBone;
        [SerializeField] private Transform _leftLegBone;
        [SerializeField] private Transform _rightLegBone;

        [Header("IK Controllers")]
        [SerializeField] private TwoBoneIKController _leftLegIK;
        [SerializeField] private TwoBoneIKController _rightLegIK;

        [Header("Jump Timings")]
        [SerializeField] private float _crouchDuration = 0.3f;
        [SerializeField] private float _jumpHeight = 0.5f;
        [SerializeField] private float _airTime = 0.4f;
        [SerializeField] private float _landDuration = 0.25f;

        [Header("Jump Curves")]
        [SerializeField] private AnimationCurve _heightCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve _crouchCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        #endregion

        #region Private State

        private Coroutine _motionRoutine;
        private Vector3 _hipsOriginalLocalPos;
        private Vector3 _leftArmOriginalEuler;
        private Vector3 _rightArmOriginalEuler;
        private Vector3 _spineOriginalLocalEuler;
        private Vector3 _leftLegRestPos;
        private Vector3 _rightLegRestPos;
        private bool _isPlaying;

        #endregion

        #region Public Properties

        /// <summary>Duration of the crouch phase in seconds.</summary>
        public float CrouchDuration
        {
            get => _crouchDuration;
            set => _crouchDuration = Mathf.Max(0.05f, value);
        }

        /// <summary>Peak height of the jump in meters.</summary>
        public float JumpHeight
        {
            get => _jumpHeight;
            set => _jumpHeight = Mathf.Max(0f, value);
        }

        /// <summary>Time spent in the air (seconds).</summary>
        public float AirTime
        {
            get => _airTime;
            set => _airTime = Mathf.Max(0.05f, value);
        }

        /// <summary>Duration of the landing impact phase (seconds).</summary>
        public float LandDuration
        {
            get => _landDuration;
            set => _landDuration = Mathf.Max(0.05f, value);
        }

        /// <summary>Current jump phase, or null if not jumping.</summary>
        public JumpPhase? CurrentPhase { get; private set; }

        /// <summary>True while the jump coroutine is running.</summary>
        public bool IsPlaying => _isPlaying;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            CacheOriginalTransforms();
        }

        private void OnDisable()
        {
            StopMotion();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Caches original local transforms for smooth blending.
        /// </summary>
        private void CacheOriginalTransforms()
        {
            if (_hipsBone != null)
                _hipsOriginalLocalPos = _hipsBone.localPosition;
            if (_spineBone != null)
                _spineOriginalLocalEuler = _spineBone.localEulerAngles;
            if (_leftArmBone != null)
                _leftArmOriginalEuler = _leftArmBone.localEulerAngles;
            if (_rightArmBone != null)
                _rightArmOriginalEuler = _rightArmBone.localEulerAngles;
            if (_leftLegIK != null && _leftLegIK.Target != null)
                _leftLegRestPos = _leftLegIK.Target.localPosition;
            if (_rightLegIK != null && _rightLegIK.Target != null)
                _rightLegRestPos = _rightLegIK.Target.localPosition;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Starts the full jump sequence (crouch → launch → air → land).
        /// </summary>
        public void StartMotion()
        {
            if (_motionRoutine != null)
                StopCoroutine(_motionRoutine);

            CacheOriginalTransforms();
            _isPlaying = true;
            _motionRoutine = StartCoroutine(JumpSequence());
        }

        /// <summary>
        /// Stops the jump motion immediately and resets bones.
        /// </summary>
        public void StopMotion()
        {
            _isPlaying = false;

            if (_motionRoutine != null)
            {
                StopCoroutine(_motionRoutine);
                _motionRoutine = null;
            }

            CurrentPhase = null;
            ResetBones();
        }

        #endregion

        #region Motion Coroutine

        /// <summary>
        /// Full jump sequence: crouch → launch → air → land.
        /// Each phase uses coroutine-driven interpolation.
        /// </summary>
        private IEnumerator JumpSequence()
        {
            // ── Phase 1: Crouch ──
            CurrentPhase = JumpPhase.Crouch;
            yield return StartCoroutine(CrouchPhase());

            if (!_isPlaying) yield break;

            // ── Phase 2: Launch ──
            CurrentPhase = JumpPhase.Launch;
            yield return StartCoroutine(LaunchPhase());

            if (!_isPlaying) yield break;

            // ── Phase 3: Air ──
            CurrentPhase = JumpPhase.Air;
            yield return StartCoroutine(AirPhase());

            if (!_isPlaying) yield break;

            // ── Phase 4: Land ──
            CurrentPhase = JumpPhase.Land;
            yield return StartCoroutine(LandPhase());

            // ── Done ──
            CurrentPhase = null;
            _isPlaying = false;
            _motionRoutine = null;
            ResetBones();
        }

        /// <summary>
        /// Crouch phase: bend knees (lower hips), lean forward slightly.
        /// </summary>
        private IEnumerator CrouchPhase()
        {
            float elapsed = 0f;
            Vector3 hipsStartPos = _hipsOriginalLocalPos;

            while (elapsed < _crouchDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _crouchDuration);
                float curve = _crouchCurve.Evaluate(t);

                // Lower hips (bend knees)
                if (_hipsBone != null)
                {
                    Vector3 hipsPos = hipsStartPos;
                    hipsPos.y -= curve * 0.2f; // crouch down
                    hipsPos.z += curve * 0.05f; // slight lean back to balance
                    _hipsBone.localPosition = hipsPos;
                }

                // Bend legs via IK
                if (_leftLegIK != null && _leftLegIK.Target != null)
                {
                    Vector3 legTarget = _leftLegRestPos;
                    legTarget.y += curve * 0.05f; // lift feet slightly for knee bend
                    _leftLegIK.Target.localPosition = legTarget;
                }

                if (_rightLegIK != null && _rightLegIK.Target != null)
                {
                    Vector3 legTarget = _rightLegRestPos;
                    legTarget.y += curve * 0.05f;
                    _rightLegIK.Target.localPosition = legTarget;
                }

                yield return null;
            }
        }

        /// <summary>
        /// Launch phase: extend legs rapidly, raise arms.
        /// </summary>
        private IEnumerator LaunchPhase()
        {
            float elapsed = 0f;
            float launchDuration = 0.15f;

            while (elapsed < launchDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / launchDuration);

                // Extend legs (push off)
                if (_hipsBone != null)
                {
                    Vector3 hipsPos = _hipsOriginalLocalPos;
                    hipsPos.y += t * _jumpHeight * 0.5f; // begin upward motion
                    _hipsBone.localPosition = hipsPos;
                }

                // Arms up
                if (_leftArmBone != null)
                {
                    _leftArmBone.localEulerAngles = new Vector3(
                        _leftArmOriginalEuler.x - t * 120f, // arms forward/up
                        _leftArmOriginalEuler.y,
                        _leftArmOriginalEuler.z);
                }

                if (_rightArmBone != null)
                {
                    _rightArmBone.localEulerAngles = new Vector3(
                        _rightArmOriginalEuler.x - t * 120f,
                        _rightArmOriginalEuler.y,
                        _rightArmOriginalEuler.z);
                }

                yield return null;
            }
        }

        /// <summary>
        /// Air phase: character in mid-air, legs tucked, arms balanced.
        /// </summary>
        private IEnumerator AirPhase()
        {
            float elapsed = 0f;

            while (elapsed < _airTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _airTime);
                float heightCurve = _heightCurve.Evaluate(t);

                // Body at peak height
                if (_hipsBone != null)
                {
                    Vector3 hipsPos = _hipsOriginalLocalPos;
                    hipsPos.y += _jumpHeight * (1f - Mathf.Pow(2f * t - 1f, 2f)); // arch
                    _hipsBone.localPosition = hipsPos;
                }

                // Tuck legs
                if (_leftLegIK != null && _leftLegIK.Target != null)
                {
                    Vector3 legTarget = _leftLegRestPos;
                    legTarget.y += Mathf.Sin(t * Mathf.PI) * 0.15f; // tuck up
                    legTarget.z -= Mathf.Sin(t * Mathf.PI) * 0.1f;  // tuck back
                    _leftLegIK.Target.localPosition = legTarget;
                }

                if (_rightLegIK != null && _rightLegIK.Target != null)
                {
                    Vector3 legTarget = _rightLegRestPos;
                    legTarget.y += Mathf.Sin(t * Mathf.PI) * 0.15f;
                    legTarget.z -= Mathf.Sin(t * Mathf.PI) * 0.1f;
                    _rightLegIK.Target.localPosition = legTarget;
                }

                // Arms balance
                if (_leftArmBone != null)
                {
                    float armAngle = Mathf.Lerp(-120f, -90f, t);
                    _leftArmBone.localEulerAngles = new Vector3(armAngle, -10f, 0f);
                }

                if (_rightArmBone != null)
                {
                    float armAngle = Mathf.Lerp(-120f, -90f, t);
                    _rightArmBone.localEulerAngles = new Vector3(armAngle, 10f, 0f);
                }

                yield return null;
            }
        }

        /// <summary>
        /// Land phase: legs absorb impact, slight crouch on landing.
        /// </summary>
        private IEnumerator LandPhase()
        {
            float elapsed = 0f;
            Vector3 hipsLandPos = _hipsOriginalLocalPos;
            hipsLandPos.y += _jumpHeight * 0.1f; // slight residual height

            while (elapsed < _landDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _landDuration);
                float smoothStep = t * t * (3f - 2f * t);

                // Absorb landing: drop from residual height to below original, then recover
                if (_hipsBone != null)
                {
                    Vector3 hipsPos = _hipsOriginalLocalPos;
                    float absorbOffset = Mathf.Lerp(_jumpHeight * 0.1f, -0.05f, smoothStep);
                    hipsPos.y += absorbOffset;
                    _hipsBone.localPosition = hipsPos;
                }

                // Extend legs back to rest
                if (_leftLegIK != null && _leftLegIK.Target != null)
                {
                    _leftLegIK.Target.localPosition = Vector3.Lerp(
                        _leftLegIK.Target.localPosition, _leftLegRestPos, smoothStep);
                }

                if (_rightLegIK != null && _rightLegIK.Target != null)
                {
                    _rightLegIK.Target.localPosition = Vector3.Lerp(
                        _rightLegIK.Target.localPosition, _rightLegRestPos, smoothStep);
                }

                // Arms come back down
                if (_leftArmBone != null)
                {
                    _leftArmBone.localEulerAngles = Vector3.Lerp(
                        _leftArmBone.localEulerAngles, _leftArmOriginalEuler, smoothStep);
                }

                if (_rightArmBone != null)
                {
                    _rightArmBone.localEulerAngles = Vector3.Lerp(
                        _rightArmBone.localEulerAngles, _rightArmOriginalEuler, smoothStep);
                }

                yield return null;
            }
        }

        /// <summary>
        /// Resets all bones and IK targets to their original rest positions.
        /// </summary>
        private void ResetBones()
        {
            if (_hipsBone != null)
                _hipsBone.localPosition = _hipsOriginalLocalPos;

            if (_spineBone != null)
                _spineBone.localEulerAngles = _spineOriginalLocalEuler;

            if (_leftArmBone != null)
                _leftArmBone.localEulerAngles = _leftArmOriginalEuler;

            if (_rightArmBone != null)
                _rightArmBone.localEulerAngles = _rightArmOriginalEuler;

            if (_leftLegIK != null && _leftLegIK.Target != null)
                _leftLegIK.Target.localPosition = _leftLegRestPos;

            if (_rightLegIK != null && _rightLegIK.Target != null)
                _rightLegIK.Target.localPosition = _rightLegRestPos;
        }

        #endregion
    }
}
