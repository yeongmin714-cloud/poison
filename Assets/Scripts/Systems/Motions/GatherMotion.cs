using System;
using System.Collections;
using UnityEngine;

namespace ProjectName.Systems.Motions
{
    /// <summary>
    /// Procedural gather/resource-collection motion.
    /// Sequence: reach toward ground (bend at waist, extend arm) →
    /// grasp motion (hand closes) → stand back up.
    /// Uses spine bending, arm IK targeting, and optional hand closure.
    /// </summary>
    [AddComponentMenu("ProjectName/Systems/Motions/Gather Motion")]
    public class GatherMotion : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Bones")]
        [SerializeField] private Transform _hipsBone;
        [SerializeField] private Transform _spineBone;
        [SerializeField] private Transform _chestBone;
        [SerializeField] private Transform _headBone;
        [SerializeField] private Transform _reachArmBone;   // the arm that reaches

        [Header("IK Controllers")]
        [SerializeField] private TwoBoneIKController _reachArmIK;
        [SerializeField] private TwoBoneIKController _otherArmIK;

        [Header("Gather Timings")]
        [SerializeField] private float _reachDuration = 0.4f;
        [SerializeField] private float _gatherDuration = 0.3f;
        [SerializeField] private float _standDuration = 0.4f;

        [Header("Gather Settings")]
        [SerializeField] private float _reachDistance = 0.6f;  // how far down/forward to reach
        [SerializeField] private float _spineBendAngle = 30f;   // degrees to bend at waist
        [SerializeField] private Vector3 _reachOffset = new Vector3(0f, -0.5f, 0.6f);

        #endregion

        #region Private State

        private Coroutine _motionRoutine;
        private Vector3 _hipsOriginalLocalPos;
        private Vector3 _spineOriginalLocalEuler;
        private Vector3 _chestOriginalLocalEuler;
        private Quaternion _headOriginalLocalRot;
        private Vector3 _reachArmOriginalEuler;
        private bool _isPlaying;

        #endregion

        #region Public Properties

        /// <summary>Duration of the reach-down phase in seconds.</summary>
        public float ReachDuration
        {
            get => _reachDuration;
            set => _reachDuration = Mathf.Max(0.05f, value);
        }

        /// <summary>Duration of the grasp/pick-up phase in seconds.</summary>
        public float GatherDuration
        {
            get => _gatherDuration;
            set => _gatherDuration = Mathf.Max(0.05f, value);
        }

        /// <summary>Duration of the stand-up phase in seconds.</summary>
        public float StandDuration
        {
            get => _standDuration;
            set => _standDuration = Mathf.Max(0.05f, value);
        }

        /// <summary>Distance to reach forward/downward.</summary>
        public float ReachDistance
        {
            get => _reachDistance;
            set => _reachDistance = Mathf.Max(0f, value);
        }

        /// <summary>True while the gather coroutine is running.</summary>
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
            if (_chestBone != null)
                _chestOriginalLocalEuler = _chestBone.localEulerAngles;
            if (_headBone != null)
                _headOriginalLocalRot = _headBone.localRotation;
            if (_reachArmBone != null)
                _reachArmOriginalEuler = _reachArmBone.localEulerAngles;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Starts the gather motion sequence (reach → grasp → stand).
        /// </summary>
        public void StartMotion()
        {
            if (_motionRoutine != null)
                StopCoroutine(_motionRoutine);

            CacheOriginalTransforms();
            _isPlaying = true;
            _motionRoutine = StartCoroutine(GatherSequence());
        }

        /// <summary>
        /// Stops the gather motion immediately and resets bones.
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
        /// Full gather sequence: reach down → grasp → stand back up.
        /// </summary>
        private IEnumerator GatherSequence()
        {
            // ── Phase 1: Reach ──
            yield return StartCoroutine(ReachPhase());
            if (!_isPlaying) yield break;

            // ── Phase 2: Gather (grasp) ──
            yield return StartCoroutine(GatherPhase());
            if (!_isPlaying) yield break;

            // ── Phase 3: Stand ──
            yield return StartCoroutine(StandPhase());

            // ── Done ──
            _isPlaying = false;
            _motionRoutine = null;
            ResetBones();
        }

        /// <summary>
        /// Reach phase: bend at the waist and extend the arm toward the ground.
        /// </summary>
        private IEnumerator ReachPhase()
        {
            float elapsed = 0f;

            while (elapsed < _reachDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _reachDuration);
                float curve = t * t * (3f - 2f * t); // smoothstep

                // Spine bend (waist)
                if (_spineBone != null)
                {
                    Vector3 spineEuler = _spineOriginalLocalEuler;
                    spineEuler.x += curve * _spineBendAngle;
                    _spineBone.localEulerAngles = spineEuler;
                }

                // Chest follow
                if (_chestBone != null)
                {
                    Vector3 chestEuler = _chestOriginalLocalEuler;
                    chestEuler.x += curve * _spineBendAngle * 0.6f;
                    _chestBone.localEulerAngles = chestEuler;
                }

                // Head looks down
                if (_headBone != null)
                {
                    _headBone.localRotation = _headOriginalLocalRot *
                                               Quaternion.Euler(curve * 20f, 0f, 0f);
                }

                // IK target moves to reach position
                if (_reachArmIK != null && _reachArmIK.Target != null)
                {
                    Vector3 reachPos = _reachArmIK.Target.localPosition +
                                       _reachOffset * curve * _reachDistance;

                    // Clamp to reasonable reach
                    reachPos = Vector3.Lerp(_reachArmIK.Target.localPosition, reachPos, curve);
                    _reachArmIK.Target.localPosition = reachPos;
                    _reachArmIK.BlendWeight = 1f;
                }

                yield return null;
            }
        }

        /// <summary>
        /// Gather phase: brief pause with hand at the ground (simulating grasp/collect).
        /// </summary>
        private IEnumerator GatherPhase()
        {
            float elapsed = 0f;

            // Subtle hand-closing oscillation
            while (elapsed < _gatherDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / _gatherDuration;

                // Small pulsing motion at the hand to simulate grasping
                if (_reachArmIK != null && _reachArmIK.Target != null)
                {
                    Vector3 pos = _reachArmIK.Target.localPosition;
                    pos.y += Mathf.Sin(t * Mathf.PI * 4f) * 0.02f;
                    _reachArmIK.Target.localPosition = pos;
                }

                // Slight arm retraction (pulling toward body)
                if (_reachArmBone != null)
                {
                    float retract = Mathf.Sin(t * Mathf.PI) * 5f;
                    _reachArmBone.localEulerAngles = new Vector3(
                        _reachArmBone.localEulerAngles.x - retract,
                        _reachArmBone.localEulerAngles.y,
                        _reachArmBone.localEulerAngles.z);
                }

                yield return null;
            }
        }

        /// <summary>
        /// Stand phase: return to upright position.
        /// </summary>
        private IEnumerator StandPhase()
        {
            float elapsed = 0f;

            while (elapsed < _standDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _standDuration);

                // Smoothly interpolate everything back to original
                if (_spineBone != null)
                {
                    _spineBone.localEulerAngles = Vector3.Lerp(
                        _spineBone.localEulerAngles, _spineOriginalLocalEuler, t);
                }

                if (_chestBone != null)
                {
                    _chestBone.localEulerAngles = Vector3.Lerp(
                        _chestBone.localEulerAngles, _chestOriginalLocalEuler, t);
                }

                if (_headBone != null)
                {
                    _headBone.localRotation = Quaternion.Slerp(
                        _headBone.localRotation, _headOriginalLocalRot, t);
                }

                if (_reachArmIK != null && _reachArmIK.Target != null)
                {
                    _reachArmIK.Target.localPosition = Vector3.Lerp(
                        _reachArmIK.Target.localPosition,
                        _reachArmIK.Target.localPosition - _reachOffset * _reachDistance * (1f - t),
                        t);
                }

                if (_reachArmBone != null)
                {
                    _reachArmBone.localEulerAngles = Vector3.Lerp(
                        _reachArmBone.localEulerAngles, _reachArmOriginalEuler, t);
                }

                yield return null;
            }
        }

        /// <summary>
        /// Resets all bones to their original rest transforms.
        /// </summary>
        private void ResetBones()
        {
            if (_spineBone != null)
                _spineBone.localEulerAngles = _spineOriginalLocalEuler;

            if (_chestBone != null)
                _chestBone.localEulerAngles = _chestOriginalLocalEuler;

            if (_headBone != null)
                _headBone.localRotation = _headOriginalLocalRot;

            if (_reachArmBone != null)
                _reachArmBone.localEulerAngles = _reachArmOriginalEuler;

            if (_reachArmIK != null && _reachArmIK.Target != null)
            {
                // Return to original rest pos
                _reachArmIK.Target.localPosition -= _reachOffset * _reachDistance;
                _reachArmIK.BlendWeight = 0f;
            }

            if (_otherArmIK != null)
                _otherArmIK.BlendWeight = 0f;
        }

        #endregion
    }
}
