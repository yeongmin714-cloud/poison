using System;
using UnityEngine;

#if UNITY_ANIMATION_RIGGING
using UnityEngine.Animations.Rigging;
#endif

namespace ProjectName.Systems
{
    /// <summary>
    /// Controls head, eye, and body rotation toward a target transform.
    ///
    /// When Unity's Animation Rigging package is installed, uses
    /// <see cref="MultiAimConstraint"/> for IK-driven head/eye aiming.
    /// When the package is absent, falls back to a custom smooth-rotation
    /// implementation using <see cref="Quaternion.RotateTowards"/> with
    /// configurable speed and per-axis constraints.
    ///
    /// Supports smooth rotation with configurable speed, per-axis restriction,
    /// and blending between the original animation pose and the aim target.
    /// </summary>
    [AddComponentMenu("ProjectName/Systems/Multi Aim Controller")]
    public class MultiAimController : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        //  Serialized fields
        // ──────────────────────────────────────────────

        [Header("Bones")]
        [SerializeField] private Transform _headBone;
        [SerializeField] private Transform _spineBone;
        [SerializeField] private Transform _bodyBone;

        [Header("Target")]
        [SerializeField] private Transform _target;

        [Header("Settings")]
        [SerializeField] private float _rotationSpeed = 180f;   // degrees per second
        [SerializeField, Range(0f, 1f)] private float _aimWeight = 1f;
        [SerializeField] private bool _constrainHorizontal = true;
        [SerializeField] private bool _constrainVertical = true;
        [SerializeField] private float _maxHorizontalAngle = 120f;
        [SerializeField] private float _maxVerticalAngle = 80f;

        [Header("Body Follow")]
        [SerializeField] private bool _bodyFollows = true;
        [SerializeField] private float _bodyRotationSpeed = 90f;
        [SerializeField, Range(0f, 1f)] private float _bodyFollowWeight = 0.3f;

        [Header("Eye Tracking")]
        [SerializeField] private Transform _leftEyeBone;
        [SerializeField] private Transform _rightEyeBone;
        [SerializeField] private float _eyeRotationSpeed = 360f;

        // ──────────────────────────────────────────────
        //  Private state
        // ──────────────────────────────────────────────

        // Cached original rotations for blending
        private Quaternion _headOriginalLocalRot;
        private Quaternion _spineOriginalLocalRot;
        private Quaternion _bodyOriginalLocalRot;
        private Quaternion _leftEyeOriginalLocalRot;
        private Quaternion _rightEyeOriginalLocalRot;

        private bool _hasInitialized;

        // Tracks temporary target GameObject created by SetAimPosition for cleanup
        private GameObject _tempTargetObject;

#if UNITY_ANIMATION_RIGGING
        private MultiAimConstraint _headConstraint;
        private MultiAimConstraint _spineConstraint;
        private GameObject _constraintRoot;
#endif

        // ──────────────────────────────────────────────
        //  Public properties
        // ──────────────────────────────────────────────

        /// <summary>Head bone Transform driven by this controller.</summary>
        public Transform HeadBone
        {
            get => _headBone;
            set => _headBone = value;
        }

        /// <summary>Spine bone Transform (partial body follow).</summary>
        public Transform SpineBone
        {
            get => _spineBone;
            set => _spineBone = value;
        }

        /// <summary>Current aim target Transform.</summary>
        public Transform Target
        {
            get => _target;
            set => _target = value;
        }

        /// <summary>
        /// Blending weight between 0 (original animation pose) and 1 (full aim toward target).
        /// </summary>
        public float AimWeight
        {
            get => _aimWeight;
            set => _aimWeight = Mathf.Clamp01(value);
        }

        /// <summary>Rotation speed in degrees per second.</summary>
        public float RotationSpeed
        {
            get => _rotationSpeed;
            set => _rotationSpeed = Mathf.Max(0f, value);
        }

        /// <summary>
        /// True if the controller has a valid head bone and is ready.
        /// </summary>
        public bool IsReady => _headBone != null;

        // ──────────────────────────────────────────────
        //  Unity lifecycle
        // ──────────────────────────────────────────────

        private void Start()
        {
            Initialize();
        }

        private void LateUpdate()
        {
            if (!IsReady || _aimWeight < 0.001f)
                return;

#if UNITY_ANIMATION_RIGGING
            // If Animation Rigging manages head/spine, skip procedural rotations
            // but still run eye tracking (eyes aren't typically in the rig constraints)
            if (_headConstraint != null && _headConstraint.isActiveAndEnabled)
            {
                if (_target != null && (_leftEyeBone != null || _rightEyeBone != null))
                {
                    Vector3 targetDir = (_target.position - _headBone.position).normalized;
                    if (targetDir.sqrMagnitude >= 0.001f)
                        UpdateEyeTracking(targetDir);
                }
                return;
            }
#endif

            if (_target != null)
                UpdateAimRotation();
        }

        // ──────────────────────────────────────────────
        //  Initialization
        // ──────────────────────────────────────────────

        /// <summary>
        /// Initializes the controller. Caches original bone transforms and attempts
        /// to hook into the Animation Rigging package if available.
        /// </summary>
        public void Initialize()
        {
            if (_hasInitialized)
                return;

            if (_headBone == null)
            {
                Debug.LogWarning(
                    $"[MultiAimController] Head bone not assigned on '{gameObject.name}'. " +
                    "Assign head bone for aiming to work.",
                    this);
                return;
            }

            // Cache original local rotations
            _headOriginalLocalRot = _headBone.localRotation;
            if (_spineBone != null) _spineOriginalLocalRot = _spineBone.localRotation;
            if (_bodyBone != null) _bodyOriginalLocalRot = _bodyBone.localRotation;
            if (_leftEyeBone != null) _leftEyeOriginalLocalRot = _leftEyeBone.localRotation;
            if (_rightEyeBone != null) _rightEyeOriginalLocalRot = _rightEyeBone.localRotation;

#if UNITY_ANIMATION_RIGGING
            TrySetupRigConstraints();
#endif

            _hasInitialized = true;
        }

#if UNITY_ANIMATION_RIGGING

        /// <summary>
        /// Attempts to use MultiAimConstraint from the Animation Rigging package.
        /// </summary>
        private void TrySetupRigConstraints()
        {
            // Create a root for constraint objects
            _constraintRoot = new GameObject("MultiAim_Constraints");
            _constraintRoot.transform.SetParent(transform, false);

            // Head constraint
            if (_headBone != null)
            {
                _headConstraint = CreateAimConstraint(
                    $"{_headBone.name}_Aim",
                    _headBone,
                    _constraintRoot.transform);
            }

            // Spine constraint (partial aim)
            if (_spineBone != null)
            {
                _spineConstraint = CreateAimConstraint(
                    $"{_spineBone.name}_Aim",
                    _spineBone,
                    _constraintRoot.transform);
            }
        }

        /// <summary>
        /// Creates a MultiAimConstraint for the given bone.
        /// </summary>
        private MultiAimConstraint CreateAimConstraint(string name, Transform bone, Transform parent)
        {
            GameObject constraintGO = new GameObject(name);
            constraintGO.transform.SetParent(parent, false);

            var constraint = constraintGO.AddComponent<MultiAimConstraint>();
            constraint.data.constrainedObject = bone;
            constraint.data.offset = Vector3.zero;
            constraint.data.constrainedXAxis = _constrainHorizontal;
            constraint.data.constrainedYAxis = _constrainVertical;
            constraint.data.constrainedZAxis = false;
            constraint.data.maintainOffset = true;
            constraint.weight = _aimWeight;

            // Add target as a source if one exists
            if (_target != null)
            {
                var sources = new WeightedTransformArray();
                sources.Add(new WeightedTransform(_target, 1f));
                constraint.data.sourceObjects = sources;
            }

            return constraint;
        }

#endif

        // ──────────────────────────────────────────────
        //  Procedural aim rotation
        // ──────────────────────────────────────────────

        /// <summary>
        /// Updates head (and optionally spine/body/eyes) rotation toward the target
        /// using smooth Quaternion rotation.
        /// </summary>
        private void UpdateAimRotation()
        {
            Vector3 targetDirection = (_target.position - _headBone.position).normalized;
            if (targetDirection.sqrMagnitude < 0.001f)
                return;

            // --- Head rotation ---
            RotateBoneToward(
                _headBone,
                _headOriginalLocalRot,
                targetDirection,
                _rotationSpeed,
                _aimWeight,
                _maxHorizontalAngle,
                _maxVerticalAngle);

            // --- Spine partial follow ---
            if (_spineBone != null && _bodyFollows)
            {
                RotateBoneToward(
                    _spineBone,
                    _spineOriginalLocalRot,
                    targetDirection,
                    _bodyRotationSpeed,
                    _aimWeight * _bodyFollowWeight,
                    _maxHorizontalAngle * 0.5f,
                    _maxVerticalAngle * 0.4f);
            }

            // --- Body follow ---
            if (_bodyBone != null && _bodyFollows)
            {
                // Body only rotates horizontally
                Vector3 horizontalDir = targetDirection;
                horizontalDir.y = 0f;
                if (horizontalDir.sqrMagnitude > 0.001f)
                {
                    horizontalDir.Normalize();

                    Quaternion targetBodyRot = Quaternion.LookRotation(horizontalDir, transform.up);
                    Quaternion blendedRot = Quaternion.Slerp(
                        _bodyOriginalLocalRot,
                        Quaternion.Inverse(_bodyBone.parent?.rotation ?? Quaternion.identity) * targetBodyRot,
                        _aimWeight * _bodyFollowWeight * 0.3f);

                    _bodyBone.localRotation = Quaternion.RotateTowards(
                        _bodyBone.localRotation, blendedRot, _bodyRotationSpeed * Time.deltaTime);
                }
            }

            // --- Eye tracking ---
            if (_leftEyeBone != null || _rightEyeBone != null)
            {
                UpdateEyeTracking(targetDirection);
            }
        }

        /// <summary>
        /// Rotates a single bone toward a world-space direction with constraints.
        /// </summary>
        private void RotateBoneToward(
            Transform bone,
            Quaternion originalLocalRot,
            Vector3 worldDirection,
            float speed,
            float weight,
            float maxHorizontal,
            float maxVertical)
        {
            // Convert world direction to bone-local space
            Vector3 localDir = bone.parent != null
                ? bone.parent.InverseTransformDirection(worldDirection)
                : worldDirection;

            // Apply angular constraints
            if (_constrainHorizontal || _constrainVertical)
            {
                Vector3 angles = Quaternion.LookRotation(localDir, Vector3.up).eulerAngles;

                if (_constrainHorizontal)
                {
                    float horizontalAngle = Mathf.DeltaAngle(0f, angles.y);
                    angles.y = Mathf.Clamp(horizontalAngle, -maxHorizontal, maxHorizontal);
                }
                if (_constrainVertical)
                {
                    float verticalAngle = Mathf.DeltaAngle(0f, angles.x);
                    angles.x = Mathf.Clamp(verticalAngle, -maxVertical, maxVertical);
                    angles.z = 0f;
                }

                localDir = Quaternion.Euler(angles) * Vector3.forward;
            }

            // Compute target local rotation
            Quaternion targetLocalRot = Quaternion.LookRotation(localDir, Vector3.up);

            // Blend with original
            Quaternion blendedRot = Quaternion.Slerp(originalLocalRot, targetLocalRot, weight);

            // Smoothly rotate toward target
            bone.localRotation = Quaternion.RotateTowards(
                bone.localRotation, blendedRot, speed * Time.deltaTime);
        }

        /// <summary>
        /// Updates eye bone rotations toward the aim direction.
        /// Computes original world rotation from cached local rotation + parent transform,
        /// then blends smoothly toward the target world rotation.
        /// </summary>
        private void UpdateEyeTracking(Vector3 worldDirection)
        {
            Quaternion targetWorldEyeRot = Quaternion.LookRotation(worldDirection, transform.up);

            if (_leftEyeBone != null)
            {
                // Original world rotation = parent.rotation * originalLocalRotation
                Quaternion originalWorldRot = _leftEyeBone.parent != null
                    ? _leftEyeBone.parent.rotation * _leftEyeOriginalLocalRot
                    : _leftEyeOriginalLocalRot;

                Quaternion blendedRot = Quaternion.Slerp(originalWorldRot, targetWorldEyeRot, _aimWeight);
                _leftEyeBone.rotation = Quaternion.RotateTowards(
                    _leftEyeBone.rotation, blendedRot, _eyeRotationSpeed * Time.deltaTime);
            }

            if (_rightEyeBone != null)
            {
                Quaternion originalWorldRot = _rightEyeBone.parent != null
                    ? _rightEyeBone.parent.rotation * _rightEyeOriginalLocalRot
                    : _rightEyeOriginalLocalRot;

                Quaternion blendedRot = Quaternion.Slerp(originalWorldRot, targetWorldEyeRot, _aimWeight);
                _rightEyeBone.rotation = Quaternion.RotateTowards(
                    _rightEyeBone.rotation, blendedRot, _eyeRotationSpeed * Time.deltaTime);
            }
        }

        // ──────────────────────────────────────────────
        //  Public methods
        // ──────────────────────────────────────────────

        /// <summary>
        /// Sets the target transform for the character to look at.
        /// Pass null to clear the target and return to the original pose.
        /// </summary>
        /// <param name="target">The target Transform, or null.</param>
        public void FollowTarget(Transform target)
        {
            _target = target;

#if UNITY_ANIMATION_RIGGING
            // Update constraint source objects
            if (_headConstraint != null)
            {
                var sources = new WeightedTransformArray();
                if (target != null)
                    sources.Add(new WeightedTransform(target, 1f));
                _headConstraint.data.sourceObjects = sources;
            }
            if (_spineConstraint != null)
            {
                var sources = new WeightedTransformArray();
                if (target != null)
                    sources.Add(new WeightedTransform(target, 1f));
                _spineConstraint.data.sourceObjects = sources;
            }
#endif
        }

        /// <summary>
        /// Sets the aim target position in world space.
        /// Creates a temporary GameObject if no target exists.
        /// </summary>
        /// <param name="worldPosition">World position to aim at.</param>
        public void SetAimPosition(Vector3 worldPosition)
        {
            if (_target == null)
            {
                _tempTargetObject = new GameObject($"{gameObject.name}_Aim_Target");
                _tempTargetObject.transform.SetParent(null);
                _target = _tempTargetObject.transform;
            }

            _target.position = worldPosition;
        }

        /// <summary>
        /// Resets head rotation back to original pose with a smooth blend.
        /// Also cleans up any temporary target GameObject created by SetAimPosition.
        /// </summary>
        public void ResetAim()
        {
            FollowTarget(null);

            if (_tempTargetObject != null)
            {
                Destroy(_tempTargetObject);
                _tempTargetObject = null;
            }
        }

        private void OnDestroy()
        {
            // Clean up temporary target on destroy to prevent leaks
            if (_tempTargetObject != null)
            {
                Destroy(_tempTargetObject);
                _tempTargetObject = null;
            }
        }

        // ──────────────────────────────────────────────
        //  Debug visualization
        // ──────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            if (_headBone == null) return;

            // Draw aim direction from head
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(_headBone.position, 0.1f);

            if (_target != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(_headBone.position, _target.position);
                Gizmos.DrawWireSphere(_target.position, 0.15f);

                // Draw cone of vision
                Vector3 direction = (_target.position - _headBone.position).normalized;
                float distance = Vector3.Distance(_headBone.position, _target.position);
                DrawCone(_headBone.position, direction, distance, _maxHorizontalAngle, _maxVerticalAngle);
            }

            // Draw eye bones
            if (_leftEyeBone != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(_leftEyeBone.position, 0.04f);
            }
            if (_rightEyeBone != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(_rightEyeBone.position, 0.04f);
            }
        }

        /// <summary>
        /// Draws a wireframe cone indicating the aim field of view.
        /// </summary>
        private static void DrawCone(Vector3 origin, Vector3 direction, float distance,
            float horizontalAngle, float verticalAngle)
        {
            const int segments = 16;

            // Horizontal arc
            Vector3 up = Vector3.up;
            Vector3 right = Vector3.Cross(direction, up).normalized;
            Vector3 forward = direction;

            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);

            for (int i = 0; i < segments; i++)
            {
                float t1 = (float)i / segments * 2f * Mathf.PI;
                float t2 = (float)(i + 1) / segments * 2f * Mathf.PI;

                Vector3 p1 = origin + forward * distance * Mathf.Cos(t1)
                             + right * distance * Mathf.Sin(t1) * Mathf.Sin(horizontalAngle * Mathf.Deg2Rad);
                Vector3 p2 = origin + forward * distance * Mathf.Cos(t2)
                             + right * distance * Mathf.Sin(t2) * Mathf.Sin(horizontalAngle * Mathf.Deg2Rad);

                Gizmos.DrawLine(origin, p1);
                Gizmos.DrawLine(p1, p2);
            }
        }
    }
}