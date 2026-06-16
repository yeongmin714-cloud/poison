using System;
using System.Collections;
using UnityEngine;

namespace ProjectName.Systems.Motions
{
    /// <summary>
    /// Procedural melee attack motion for a rigged character.
    /// Three-phase sequence: Wind-up (arm back, body twist) →
    /// Swing (arm forward, body twist through) →
    /// Follow-through (arm extended, recovery).
    /// </summary>
    [AddComponentMenu("ProjectName/Systems/Motions/Attack Motion")]
    public class AttackMotion : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Bones")]
        [SerializeField] private Transform _spineBone;
        [SerializeField] private Transform _chestBone;
        [SerializeField] private Transform _headBone;
        [SerializeField] private Transform _hipsBone;
        [SerializeField] private Transform _weaponArmBone;  // arm holding the weapon
        [SerializeField] private Transform _otherArmBone;   // off hand

        [Header("IK Controllers")]
        [SerializeField] private TwoBoneIKController _weaponArmIK;
        [SerializeField] private TwoBoneIKController _otherArmIK;

        [Header("Attack Timings")]
        [SerializeField] private float _windUpTime = 0.2f;
        [SerializeField] private float _swingSpeed = 0.3f;
        [SerializeField] private float _recoveryTime = 0.25f;

        [Header("Attack Settings")]
        [SerializeField] private float _armPullback = 60f;  // degrees arm goes back
        [SerializeField] private float _armSwingAngle = 120f; // total swing arc
        [SerializeField] private float _bodyTwistAngle = 45f;
        [SerializeField] private float _forwardLunge = 0.3f; // hip lunge distance

        #endregion

        #region Private State

        private Coroutine _motionRoutine;
        private Vector3 _spineOriginalLocalEuler;
        private Vector3 _chestOriginalLocalEuler;
        private Quaternion _headOriginalLocalRot;
        private Vector3 _hipsOriginalLocalPos;
        private Quaternion _hipsOriginalLocalRot;
        private Vector3 _weaponArmOriginalEuler;
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

        /// <summary>Duration of the swing phase in seconds.</summary>
        public float SwingSpeed
        {
            get => _swingSpeed;
            set => _swingSpeed = Mathf.Max(0.02f, value);
        }

        /// <summary>Duration of the recovery/follow-through phase in seconds.</summary>
        public float RecoveryTime
        {
            get => _recoveryTime;
            set => _recoveryTime = Mathf.Max(0.02f, value);
        }

        /// <summary>True while the attack coroutine is running.</summary>
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
            if (_weaponArmBone != null)
                _weaponArmOriginalEuler = _weaponArmBone.localEulerAngles;
            if (_otherArmBone != null)
                _otherArmOriginalEuler = _otherArmBone.localEulerAngles;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Starts the attack motion sequence (wind-up → swing → follow-through).
        /// </summary>
        public void StartMotion()
        {
            if (_motionRoutine != null)
                StopCoroutine(_motionRoutine);

            CacheOriginalTransforms();
            _isPlaying = true;
            _motionRoutine = StartCoroutine(AttackSequence());
        }

        /// <summary>
        /// Stops the attack motion immediately and resets bones.
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
        /// Full attack sequence: wind-up → swing → follow-through/recovery.
        /// </summary>
        private IEnumerator AttackSequence()
        {
            // ── Phase 1: Wind-up ──
            yield return StartCoroutine(WindUpPhase());
            if (!_isPlaying) yield break;

            // ── Phase 2: Swing ──
            yield return StartCoroutine(SwingPhase());
            if (!_isPlaying) yield break;

            // ── Phase 3: Follow-through / Recovery ──
            yield return StartCoroutine(RecoveryPhase());

            // ── Done ──
            _isPlaying = false;
            _motionRoutine = null;
            ResetBones();
        }

        /// <summary>
        /// Wind-up phase: pull weapon arm back, twist body away.
        /// </summary>
        private IEnumerator WindUpPhase()
        {
            float elapsed = 0f;

            while (elapsed < _windUpTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _windUpTime);
                float curve = t * t; // ease-in

                // Weapon arm pulls back
                if (_weaponArmBone != null)
                {
                    _weaponArmBone.localEulerAngles = new Vector3(
                        _weaponArmOriginalEuler.x - curve * _armPullback,
                        _weaponArmOriginalEuler.y + curve * 20f,
                        _weaponArmOriginalEuler.z);
                }

                // Off arm raises slightly
                if (_otherArmBone != null)
                {
                    _otherArmBone.localEulerAngles = new Vector3(
                        _otherArmOriginalEuler.x + curve * 30f,
                        _otherArmOriginalEuler.y - curve * 10f,
                        _otherArmOriginalEuler.z);
                }

                // Spine twist (wind-up)
                if (_spineBone != null)
                {
                    Vector3 spineEuler = _spineOriginalLocalEuler;
                    spineEuler.y += curve * _bodyTwistAngle * 0.5f;
                    _spineBone.localEulerAngles = spineEuler;
                }

                // Chest twist further
                if (_chestBone != null)
                {
                    Vector3 chestEuler = _chestOriginalLocalEuler;
                    chestEuler.y += curve * _bodyTwistAngle * 0.8f;
                    _chestBone.localEulerAngles = chestEuler;
                }

                // Head follows the wind-up
                if (_headBone != null)
                {
                    _headBone.localRotation = _headOriginalLocalRot *
                                               Quaternion.Euler(0f, curve * 15f, 0f);
                }

                // Hips slight rotation
                if (_hipsBone != null)
                {
                    _hipsBone.localRotation = _hipsOriginalLocalRot *
                                              Quaternion.Euler(0f, curve * _bodyTwistAngle * 0.3f, 0f);
                }

                yield return null;
            }
        }

        /// <summary>
        /// Swing phase: arm swings forward rapidly, body twists through.
        /// </summary>
        private IEnumerator SwingPhase()
        {
            float elapsed = 0f;

            while (elapsed < _swingSpeed)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _swingSpeed);
                float curve = t * (2f - t); // ease-out

                // Weapon arm swings forward
                if (_weaponArmBone != null)
                {
                    float armAngle = Mathf.Lerp(-_armPullback, _armSwingAngle, curve);
                    _weaponArmBone.localEulerAngles = new Vector3(
                        _weaponArmOriginalEuler.x + armAngle,
                        _weaponArmOriginalEuler.y + Mathf.Lerp(20f, -10f, curve),
                        _weaponArmOriginalEuler.z);
                }

                // Off arm drops back
                if (_otherArmBone != null)
                {
                    _otherArmBone.localEulerAngles = Vector3.Lerp(
                        _otherArmBone.localEulerAngles, _otherArmOriginalEuler, curve);
                }

                // Spine and chest twist through
                if (_spineBone != null)
                {
                    Vector3 spineEuler = _spineOriginalLocalEuler;
                    spineEuler.y += Mathf.Lerp(_bodyTwistAngle * 0.5f, -_bodyTwistAngle * 0.3f, curve);
                    _spineBone.localEulerAngles = spineEuler;
                }

                if (_chestBone != null)
                {
                    Vector3 chestEuler = _chestOriginalLocalEuler;
                    chestEuler.y += Mathf.Lerp(_bodyTwistAngle * 0.8f, -_bodyTwistAngle * 0.5f, curve);
                    _chestBone.localEulerAngles = chestEuler;
                }

                // Head snaps to follow swing
                if (_headBone != null)
                {
                    _headBone.localRotation = _headOriginalLocalRot *
                                               Quaternion.Euler(0f, Mathf.Lerp(15f, -10f, curve), 0f);
                }

                // Forward lunge
                if (_hipsBone != null)
                {
                    Vector3 hipsPos = _hipsOriginalLocalPos;
                    hipsPos.z += curve * _forwardLunge;
                    _hipsBone.localPosition = hipsPos;
                }

                yield return null;
            }
        }

        /// <summary>
        /// Recovery phase: return to neutral pose smoothly.
        /// </summary>
        private IEnumerator RecoveryPhase()
        {
            float elapsed = 0f;

            while (elapsed < _recoveryTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _recoveryTime);
                float smooth = t * t * (3f - 2f * t);

                // Smoothly interpolate everything back to original
                if (_weaponArmBone != null)
                {
                    _weaponArmBone.localEulerAngles = Vector3.Lerp(
                        _weaponArmBone.localEulerAngles, _weaponArmOriginalEuler, smooth);
                }

                if (_otherArmBone != null)
                {
                    _otherArmBone.localEulerAngles = Vector3.Lerp(
                        _otherArmBone.localEulerAngles, _otherArmOriginalEuler, smooth);
                }

                if (_spineBone != null)
                {
                    _spineBone.localEulerAngles = Vector3.Lerp(
                        _spineBone.localEulerAngles, _spineOriginalLocalEuler, smooth);
                }

                if (_chestBone != null)
                {
                    _chestBone.localEulerAngles = Vector3.Lerp(
                        _chestBone.localEulerAngles, _chestOriginalLocalEuler, smooth);
                }

                if (_headBone != null)
                {
                    _headBone.localRotation = Quaternion.Slerp(
                        _headBone.localRotation, _headOriginalLocalRot, smooth);
                }

                if (_hipsBone != null)
                {
                    _hipsBone.localPosition = Vector3.Lerp(
                        _hipsBone.localPosition, _hipsOriginalLocalPos, smooth);
                    _hipsBone.localRotation = Quaternion.Slerp(
                        _hipsBone.localRotation, _hipsOriginalLocalRot, smooth);
                }

                yield return null;
            }
        }

        /// <summary>
        /// Resets all bones to their original rest transforms.
        /// </summary>
        private void ResetBones()
        {
            if (_weaponArmBone != null)
                _weaponArmBone.localEulerAngles = _weaponArmOriginalEuler;

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

            if (_weaponArmIK != null)
                _weaponArmIK.BlendWeight = 0f;

            if (_otherArmIK != null)
                _otherArmIK.BlendWeight = 0f;
        }

        #endregion
    }
}
