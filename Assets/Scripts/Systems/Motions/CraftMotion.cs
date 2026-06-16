using System;
using System.Collections;
using UnityEngine;

namespace ProjectName.Systems.Motions
{
    /// <summary>
    /// Procedural crafting motion for a rigged character.
    /// Simulates two-handed work at a crafting station (e.g. mortar and pestle,
    /// hammering, mixing) with a rhythmic arm-pumping motion.
    /// </summary>
    [AddComponentMenu("ProjectName/Systems/Motions/Craft Motion")]
    public class CraftMotion : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Bones")]
        [SerializeField] private Transform _spineBone;
        [SerializeField] private Transform _chestBone;
        [SerializeField] private Transform _leftArmBone;
        [SerializeField] private Transform _rightArmBone;
        [SerializeField] private Transform _headBone;
        [SerializeField] private Transform _hipsBone;

        [Header("IK Controllers")]
        [SerializeField] private TwoBoneIKController _leftArmIK;
        [SerializeField] private TwoBoneIKController _rightArmIK;

        [Header("Craft Settings")]
        [SerializeField] private float _workSpeed = 2.0f;   // cycles per second
        [SerializeField] private float _workDuration = 3.0f; // total seconds before auto-stop
        [SerializeField] private float _armPumpAmount = 30f; // degrees
        [SerializeField] private float _bodyRockAmount = 5f; // degrees
        [SerializeField] private Vector3 _workPosition = new Vector3(0f, -0.1f, 0.4f); // relative to hips

        #endregion

        #region Private State

        private Coroutine _motionRoutine;
        private Vector3 _spineOriginalLocalEuler;
        private Vector3 _chestOriginalLocalEuler;
        private Vector3 _leftArmOriginalEuler;
        private Vector3 _rightArmOriginalEuler;
        private Quaternion _headOriginalLocalRot;
        private Vector3 _hipsOriginalLocalPos;
        private bool _isPlaying;

        #endregion

        #region Public Properties

        /// <summary>Speed of the crafting motion cycles per second.</summary>
        public float WorkSpeed
        {
            get => _workSpeed;
            set => _workSpeed = Mathf.Max(0.1f, value);
        }

        /// <summary>Total duration of the crafting animation in seconds.</summary>
        public float WorkDuration
        {
            get => _workDuration;
            set => _workDuration = Mathf.Max(0.5f, value);
        }

        /// <summary>True while the craft coroutine is running.</summary>
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
            if (_leftArmBone != null)
                _leftArmOriginalEuler = _leftArmBone.localEulerAngles;
            if (_rightArmBone != null)
                _rightArmOriginalEuler = _rightArmBone.localEulerAngles;
            if (_headBone != null)
                _headOriginalLocalRot = _headBone.localRotation;
            if (_hipsBone != null)
                _hipsOriginalLocalPos = _hipsBone.localPosition;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Starts the crafting motion. Runs for <see cref="WorkDuration"/> seconds
        /// then automatically stops.
        /// </summary>
        public void StartMotion()
        {
            if (_motionRoutine != null)
                StopCoroutine(_motionRoutine);

            CacheOriginalTransforms();
            _isPlaying = true;
            _motionRoutine = StartCoroutine(CraftLoop());
        }

        /// <summary>
        /// Stops the crafting motion and resets bones.
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
        /// Main craft loop: rhythmic arm pumping with body rock.
        /// Runs for <see cref="WorkDuration"/> seconds.
        /// </summary>
        private IEnumerator CraftLoop()
        {
            float elapsed = 0f;

            // Move IK targets to work position
            SetupWorkPosition();

            while (_isPlaying && elapsed < _workDuration)
            {
                elapsed += Time.deltaTime;
                float dt = Time.deltaTime;

                // Arm pumping (sinusoidal)
                ApplyArmPump(elapsed);

                // Body rock
                ApplyBodyRock(elapsed);

                // Head slight downward focus
                ApplyHeadFocus();

                yield return null;
            }

            // Auto-stop when duration expires
            ResetBones();
            _isPlaying = false;
            _motionRoutine = null;
        }

        #endregion

        #region Animation Methods

        /// <summary>
        /// Moves both arm IK targets to the work position.
        /// </summary>
        private void SetupWorkPosition()
        {
            if (_hipsBone == null) return;

            Vector3 workWorldPos = _hipsBone.TransformPoint(_workPosition);

            if (_leftArmIK != null)
            {
                _leftArmIK.SetTargetPosition(workWorldPos);
                _leftArmIK.BlendWeight = 1f;
            }

            if (_rightArmIK != null)
            {
                _rightArmIK.SetTargetPosition(workWorldPos);
                _rightArmIK.BlendWeight = 1f;
            }
        }

        /// <summary>
        /// Applies rhythmic arm pumping (like using mortar and pestle).
        /// </summary>
        private void ApplyArmPump(float time)
        {
            float pump = Mathf.Sin(time * _workSpeed * Mathf.PI * 2f) * _armPumpAmount;

            if (_leftArmBone != null)
            {
                Vector3 euler = _leftArmOriginalEuler;
                euler.x += pump;
                _leftArmBone.localEulerAngles = euler;
            }

            if (_rightArmBone != null)
            {
                Vector3 euler = _rightArmOriginalEuler;
                euler.x += pump;
                _rightArmBone.localEulerAngles = euler;
            }

            // Also oscillate IK targets slightly for visual feedback
            if (_leftArmIK?.Target != null)
            {
                Vector3 pos = _leftArmIK.Target.localPosition;
                pos.y += Mathf.Sin(time * _workSpeed * Mathf.PI * 2f) * 0.03f;
                pos.z += Mathf.Cos(time * _workSpeed * Mathf.PI * 2f) * 0.02f;
                _leftArmIK.Target.localPosition = pos;
            }

            if (_rightArmIK?.Target != null)
            {
                Vector3 pos = _rightArmIK.Target.localPosition;
                pos.y += Mathf.Sin(time * _workSpeed * Mathf.PI * 2f) * 0.03f;
                pos.z += Mathf.Cos(time * _workSpeed * Mathf.PI * 2f) * 0.02f;
                _rightArmIK.Target.localPosition = pos;
            }
        }

        /// <summary>
        /// Applies a subtle side-to-side body rock while crafting.
        /// </summary>
        private void ApplyBodyRock(float time)
        {
            float rock = Mathf.Sin(time * _workSpeed * Mathf.PI * 2f * 0.5f) * _bodyRockAmount;

            if (_spineBone != null)
            {
                Vector3 euler = _spineOriginalLocalEuler;
                euler.z += rock;
                _spineBone.localEulerAngles = euler;
            }

            if (_chestBone != null)
            {
                Vector3 euler = _chestOriginalLocalEuler;
                euler.z += rock * 0.7f;
                _chestBone.localEulerAngles = euler;
            }
        }

        /// <summary>
        /// Tilts the head slightly downward to focus on the crafting work.
        /// </summary>
        private void ApplyHeadFocus()
        {
            if (_headBone == null) return;

            _headBone.localRotation = _headOriginalLocalRot *
                                       Quaternion.Euler(10f, 0f, 0f);
        }

        /// <summary>
        /// Resets all bones and IK targets to original transforms.
        /// </summary>
        private void ResetBones()
        {
            if (_spineBone != null)
                _spineBone.localEulerAngles = _spineOriginalLocalEuler;

            if (_chestBone != null)
                _chestBone.localEulerAngles = _chestOriginalLocalEuler;

            if (_leftArmBone != null)
                _leftArmBone.localEulerAngles = _leftArmOriginalEuler;

            if (_rightArmBone != null)
                _rightArmBone.localEulerAngles = _rightArmOriginalEuler;

            if (_headBone != null)
                _headBone.localRotation = _headOriginalLocalRot;

            if (_hipsBone != null)
                _hipsBone.localPosition = _hipsOriginalLocalPos;

            if (_leftArmIK != null)
                _leftArmIK.BlendWeight = 0f;

            if (_rightArmIK != null)
                _rightArmIK.BlendWeight = 0f;
        }

        #endregion
    }
}
