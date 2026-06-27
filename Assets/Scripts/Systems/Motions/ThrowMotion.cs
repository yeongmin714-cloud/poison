using System;
using System.Collections;
using UnityEngine;

namespace ProjectName.Systems.Motions
{
    /// <summary>
    /// Procedural throw motion for a rigged character.
    /// Three-phase sequence: Wind-up (arm pulls back, looks up) →
    /// Throw (arm snaps forward, body follows) →
    /// Follow-through (arm continues forward, recovery).
    /// </summary>
    [AddComponentMenu("ProjectName/Systems/Motions/Throw Motion")]
    public class ThrowMotion : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Bones")]
        [SerializeField] private Transform _spineBone;
        [SerializeField] private Transform _chestBone;
        [SerializeField] private Transform _headBone;
        [SerializeField] private Transform _hipsBone;
        [SerializeField] private Transform _throwArmBone;   // arm that throws
        [SerializeField] private Transform _otherArmBone;   // off hand / balance arm

        [Header("IK Controllers")]
        [SerializeField] private TwoBoneIKController _throwArmIK;
        [SerializeField] private TwoBoneIKController _otherArmIK;

        [Header("Throw Timings")]
        [SerializeField] private float _windUpTime = 0.3f;
        [SerializeField] private float _throwSpeed = 0.15f;
        [SerializeField] private float _followThroughTime = 0.3f;

        [Header("Throw Settings")]
        [SerializeField] private float _armPullback = 80f;   // how far arm goes back
        [SerializeField] private float _throwAngle = 150f;    // total throw arc (forward/up)
        [SerializeField] private float _releaseAngle = 45f;   // angle of release relative to horizon
        [SerializeField] private float _bodyTwistWindup = 40f;
        [SerializeField] private float _bodyTwistThrow = -30f;

        #endregion

        #region Private State

        private Coroutine _motionRoutine;
        private Vector3 _spineOriginalLocalEuler;
        private Vector3 _chestOriginalLocalEuler;
        private Quaternion _headOriginalLocalRot;
        private Vector3 _hipsOriginalLocalPos;
        private Quaternion _hipsOriginalLocalRot;
        private Vector3 _throwArmOriginalEuler;
        private Vector3 _otherArmOriginalEuler;
        private bool _isPlaying;

        #endregion

        #region Public Properties

        /// <summary>Duration of the wind-up phase in seconds.</summary>
        public float WindUpTime
        {
            get => _windUpTime;
            set => _windUpTime = Mathf.Max(0.02f, value);
        }

        /// <summary>Duration of the throw phase in seconds.</summary>
        public float ThrowSpeed
        {
            get => _throwSpeed;
            set => _throwSpeed = Mathf.Max(0.02f, value);
        }

        /// <summary>Angle at which the projectile is released (degrees from horizon).</summary>
        public float ReleaseAngle
        {
            get => _releaseAngle;
            set => _releaseAngle = Mathf.Clamp(value, 0f, 90f);
        }

        /// <summary>True while the throw coroutine is running.</summary>
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
            if (_spineBone != null)
                _spineOriginalLocalEuler = _spineBone.localEulerAngles;
            if (_chestBone != null)
                _chestOriginalLocalEuler = _chestBone.localEulerAngles;
            if (_headBone != null)
                _headOriginalLocalRot = _headBone.localRotation;
            if (_hipsBone != null)
            {
                _hipsOriginalLocalPos = _hipsBone.localPosition;
                _hipsOriginalLocalRot = _hipsBone.localRotation;
            }
            if (_throwArmBone != null)
                _throwArmOriginalEuler = _throwArmBone.localEulerAngles;
            if (_otherArmBone != null)
                _otherArmOriginalEuler = _otherArmBone.localEulerAngles;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Starts the throw motion sequence (wind-up → throw → follow-through).
        /// </summary>
        public void StartMotion()
        {
            if (_motionRoutine != null)
                StopCoroutine(_motionRoutine);

            CacheOriginalTransforms();
            _isPlaying = true;
            _motionRoutine = StartCoroutine(ThrowSequence());
        }

        /// <summary>
        /// Stops the throw motion immediately and resets bones.
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
        /// Full throw sequence: wind-up → throw → follow-through/recovery.
        /// </summary>
        private IEnumerator ThrowSequence()
        {
            // ── Phase 1: Wind-up ──
            yield return StartCoroutine(WindUpPhase());
            if (!_isPlaying) yield break;

            // ── Phase 2: Throw ──
            yield return StartCoroutine(ThrowPhase());
            if (!_isPlaying) yield break;

            // ── Phase 3: Follow-through ──
            yield return StartCoroutine(FollowThroughPhase());

            // ── Done ──
            _isPlaying = false;
            _motionRoutine = null;
            ResetBones();
        }

        /// <summary>
        /// Wind-up phase: pull the throw arm back, look up, twist body.
        /// </summary>
        private IEnumerator WindUpPhase()
        {
            float elapsed = 0f;

            while (elapsed < _windUpTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _windUpTime);
                float curve = t * t; // ease-in

                // Throw arm pulls back and up
                if (_throwArmBone != null)
                {
                    _throwArmBone.localEulerAngles = new Vector3(
                        _throwArmOriginalEuler.x - curve * _armPullback,
                        _throwArmOriginalEuler.y + curve * 30f,
                        _throwArmOriginalEuler.z);
                }

                // Other arm extends for balance
                if (_otherArmBone != null)
                {
                    _otherArmBone.localEulerAngles = new Vector3(
                        _otherArmOriginalEuler.x - curve * 40f,
                        _otherArmOriginalEuler.y - curve * 20f,
                        _otherArmOriginalEuler.z);
                }

                // Spine twist (wind-up)
                if (_spineBone != null)
                {
                    Vector3 spineEuler = _spineOriginalLocalEuler;
                    spineEuler.y += curve * _bodyTwistWindup * 0.5f;
                    _spineBone.localEulerAngles = spineEuler;
                }

                // Chest twist
                if (_chestBone != null)
                {
                    Vector3 chestEuler = _chestOriginalLocalEuler;
                    chestEuler.y += curve * _bodyTwistWindup * 0.7f;
                    _chestBone.localEulerAngles = chestEuler;
                }

                // Head looks up
                if (_headBone != null)
                {
                    _headBone.localRotation = _headOriginalLocalRot *
                                               Quaternion.Euler(-curve * 15f, curve * 20f, 0f);
                }

                // Hips slight rotation
                if (_hipsBone != null)
                {
                    _hipsBone.localRotation = _hipsOriginalLocalRot *
                                              Quaternion.Euler(0f, curve * _bodyTwistWindup * 0.3f, 0f);
                }

                yield return null;
            }
        }

        /// <summary>
        /// Throw phase: arm snaps forward along the release angle.
        /// </summary>
        private IEnumerator ThrowPhase()
        {
            float elapsed = 0f;

            // Capture other arm wind-up rotation for safe slerp back to original
            Quaternion otherArmStartRot = _otherArmBone != null ? _otherArmBone.localRotation : Quaternion.identity;

            while (elapsed < _throwSpeed)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _throwSpeed);
                float curve = t * (2f - t); // ease-out (snap)

                // Throw arm snaps forward at release angle
                if (_throwArmBone != null)
                {
                    float armAngle = Mathf.Lerp(-_armPullback, _throwAngle, curve);
                    float horizontalSwing = Mathf.Lerp(30f, -_releaseAngle * 0.5f, curve);
                    _throwArmBone.localEulerAngles = new Vector3(
                        _throwArmOriginalEuler.x + armAngle,
                        _throwArmOriginalEuler.y + horizontalSwing,
                        _throwArmOriginalEuler.z);
                }

                // Other arm drops back for counterbalance
                if (_otherArmBone != null)
                {
                    _otherArmBone.localRotation = Quaternion.Slerp(
                        otherArmStartRot, Quaternion.Euler(_otherArmOriginalEuler), curve);
                }

                // Body twists through
                if (_spineBone != null)
                {
                    Vector3 spineEuler = _spineOriginalLocalEuler;
                    spineEuler.y += Mathf.Lerp(_bodyTwistWindup * 0.5f, _bodyTwistThrow * 0.5f, curve);
                    _spineBone.localEulerAngles = spineEuler;
                }

                if (_chestBone != null)
                {
                    Vector3 chestEuler = _chestOriginalLocalEuler;
                    chestEuler.y += Mathf.Lerp(_bodyTwistWindup * 0.7f, _bodyTwistThrow * 0.7f, curve);
                    _chestBone.localEulerAngles = chestEuler;
                }

                // Head follows the throw
                if (_headBone != null)
                {
                    float headLook = Mathf.Lerp(-15f, _releaseAngle * 0.3f, curve);
                    _headBone.localRotation = _headOriginalLocalRot *
                                               Quaternion.Euler(headLook, Mathf.Lerp(20f, -_releaseAngle * 0.3f, curve), 0f);
                }

                // Hips rotate through
                if (_hipsBone != null)
                {
                    float hipTwist = Mathf.Lerp(_bodyTwistWindup * 0.3f, _bodyTwistThrow * 0.3f, curve);
                    _hipsBone.localRotation = _hipsOriginalLocalRot *
                                              Quaternion.Euler(0f, hipTwist, 0f);
                }

                yield return null;
            }

            // At the moment of release, trigger any throw IK adjustments
            OnRelease();
        }

        /// <summary>
        /// Called at the instant of projectile release.
        /// Can be extended in subclasses or via UnityEvents.
        /// </summary>
        private void OnRelease()
        {
            // Placeholder for projectile-spawn logic
            Debug.Log("[ThrowMotion] Projectile released.", this);
        }

        /// <summary>
        /// Follow-through phase: arm continues forward, body settles.
        /// </summary>
        private IEnumerator FollowThroughPhase()
        {
            float elapsed = 0f;

            // Capture current rotations for safe slerp back to originals
            Quaternion throwArmStartRot = _throwArmBone != null ? _throwArmBone.localRotation : Quaternion.identity;
            Quaternion otherArmStartRot = _otherArmBone != null ? _otherArmBone.localRotation : Quaternion.identity;
            Quaternion spineStartRot = _spineBone != null ? _spineBone.localRotation : Quaternion.identity;
            Quaternion chestStartRot = _chestBone != null ? _chestBone.localRotation : Quaternion.identity;

            while (elapsed < _followThroughTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _followThroughTime);
                float smooth = t * t * (3f - 2f * t);

                // Smoothly interpolate everything back to original
                if (_throwArmBone != null)
                    _throwArmBone.localRotation = Quaternion.Slerp(
                        throwArmStartRot, Quaternion.Euler(_throwArmOriginalEuler), smooth);

                if (_otherArmBone != null)
                    _otherArmBone.localRotation = Quaternion.Slerp(
                        otherArmStartRot, Quaternion.Euler(_otherArmOriginalEuler), smooth);

                if (_spineBone != null)
                    _spineBone.localRotation = Quaternion.Slerp(
                        spineStartRot, Quaternion.Euler(_spineOriginalLocalEuler), smooth);

                if (_chestBone != null)
                    _chestBone.localRotation = Quaternion.Slerp(
                        chestStartRot, Quaternion.Euler(_chestOriginalLocalEuler), smooth);

                if (_headBone != null)
                    _headBone.localRotation = Quaternion.Slerp(
                        _headBone.localRotation, _headOriginalLocalRot, smooth);

                if (_hipsBone != null)
                    _hipsBone.localRotation = Quaternion.Slerp(
                        _hipsBone.localRotation, _hipsOriginalLocalRot, smooth);

                yield return null;
            }
        }

        /// <summary>
        /// Resets all bones to their original rest transforms.
        /// </summary>
        private void ResetBones()
        {
            if (_throwArmBone != null)
                _throwArmBone.localEulerAngles = _throwArmOriginalEuler;

            if (_otherArmBone != null)
                _otherArmBone.localEulerAngles = _otherArmOriginalEuler;

            if (_spineBone != null)
                _spineBone.localEulerAngles = _spineOriginalLocalEuler;

            if (_chestBone != null)
                _chestBone.localEulerAngles = _chestOriginalLocalEuler;

            if (_headBone != null)
                _headBone.localRotation = _headOriginalLocalRot;

            if (_hipsBone != null)
            {
                _hipsBone.localPosition = _hipsOriginalLocalPos;
                _hipsBone.localRotation = _hipsOriginalLocalRot;
            }

            if (_throwArmIK != null)
                _throwArmIK.BlendWeight = 0f;

            if (_otherArmIK != null)
                _otherArmIK.BlendWeight = 0f;
        }

        #endregion
    }
}
