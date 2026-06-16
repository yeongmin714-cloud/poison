using System;
using UnityEngine;

#if UNITY_ANIMATION_RIGGING
using UnityEngine.Animations.Rigging;
#endif

namespace ProjectName.Systems
{
    /// <summary>
    /// Configures and drives Two Bone IK on a character's limb (arm or leg).
    ///
    /// When Unity's Animation Rigging package is installed, uses
    /// <see cref="TwoBoneIKConstraint"/> for high-performance IK.
    /// When the package is absent, falls back to a procedural FABRIK
    /// (Forward And Backward Reaching Inverse Kinematics) implementation
    /// that operates on a three-bone chain.
    ///
    /// Supports smooth blending between the IK solution and the original
    /// animation pose via <see cref="BlendWeight"/>.
    /// </summary>
    [AddComponentMenu("ProjectName/Systems/Two Bone IK Controller")]
    public class TwoBoneIKController : MonoBehaviour
    {
        #region Nested Types

        /// <summary>
        /// Defines which limb this controller targets.
        /// </summary>
        public enum LimbType
        {
            /// <summary>Left arm (UpperArm → LowerArm → Hand).</summary>
            LeftArm,
            /// <summary>Right arm (UpperArm → LowerArm → Hand).</summary>
            RightArm,
            /// <summary>Left leg (UpperLeg → LowerLeg → Foot).</summary>
            LeftLeg,
            /// <summary>Right leg (UpperLeg → LowerLeg → Foot).</summary>
            RightLeg
        }

        #endregion

        // ──────────────────────────────────────────────
        //  Serialized fields
        // ──────────────────────────────────────────────

        [Header("Bone Chain")]
        [SerializeField] private Transform _rootBone;    // UpperArm / UpperLeg
        [SerializeField] private Transform _midBone;     // LowerArm / LowerLeg (Forearm / Shin)
        [SerializeField] private Transform _tipBone;     // Hand / Foot

        [Header("IK Target")]
        [SerializeField] private Transform _target;
        [SerializeField] private Transform _hint;

        [Header("Settings")]
        [SerializeField] private LimbType _limbType = LimbType.RightArm;
        [SerializeField, Range(0f, 1f)] private float _blendWeight = 1f;
        [SerializeField] private bool _applyRotation = true;
        [SerializeField] private float _hintWeight = 0.5f;

        [Header("Procedural IK (FABRIK)")]
        [SerializeField] private int _fabrikIterations = 10;
        [SerializeField] private float _fabrikTolerance = 0.001f;

        // ──────────────────────────────────────────────
        //  Private state
        // ──────────────────────────────────────────────

        // Cached original transforms for smooth blending
        private Vector3 _rootOriginalPos;
        private Quaternion _rootOriginalRot;
        private Vector3 _midOriginalPos;
        private Quaternion _midOriginalRot;
        private Vector3 _tipOriginalPos;
        private Quaternion _tipOriginalRot;

        private bool _hasValidChain;
        private bool _initialized;

#if UNITY_ANIMATION_RIGGING
        private TwoBoneIKConstraint _rigConstraint;
        private GameObject _constraintGO;
#endif

        // ──────────────────────────────────────────────
        //  Public properties
        // ──────────────────────────────────────────────

        /// <summary>Root bone (upper arm / upper leg).</summary>
        public Transform RootBone => _rootBone;
        /// <summary>Mid bone (forearm / shin).</summary>
        public Transform MidBone => _midBone;
        /// <summary>Tip bone (hand / foot).</summary>
        public Transform TipBone => _tipBone;

        /// <summary>IK target position/rotation source.</summary>
        public Transform Target
        {
            get => _target;
            set => _target = value;
        }

        /// <summary>IK hint (elbow / knee direction).</summary>
        public Transform Hint
        {
            get => _hint;
            set => _hint = value;
        }

        /// <summary>
        /// Blending weight between 0 (original animation pose) and 1 (full IK).
        /// </summary>
        public float BlendWeight
        {
            get => _blendWeight;
            set => _blendWeight = Mathf.Clamp01(value);
        }

        /// <summary>The limb type this controller targets.</summary>
        public LimbType Type => _limbType;

        /// <summary>
        /// True when the bone chain is properly configured and IK can run.
        /// </summary>
        public bool IsValid => _hasValidChain;

        // ──────────────────────────────────────────────
        //  Unity lifecycle
        // ──────────────────────────────────────────────

        private void Start()
        {
            Initialize();
        }

        private void LateUpdate()
        {
            if (!_hasValidChain || _blendWeight < 0.001f)
                return;

#if UNITY_ANIMATION_RIGGING
            // If the Animation Rigging package manages this, we don't need procedural IK
            if (_rigConstraint != null && _rigConstraint.isActiveAndEnabled)
                return;
#endif

            // Procedural FABRIK fallback
            if (_target != null)
                SolveFABRIK();
        }

        // ──────────────────────────────────────────────
        //  Initialization
        // ──────────────────────────────────────────────

        /// <summary>
        /// Initializes the controller. Attempts to use the Animation Rigging package
        /// <see cref="TwoBoneIKConstraint"/> if available, otherwise prepares the
        /// procedural FABRIK solver.
        /// </summary>
        public void Initialize()
        {
            if (_initialized)
                return;

            ValidateBoneChain();

            if (!_hasValidChain)
            {
                Debug.LogWarning(
                    $"[TwoBoneIKController] Invalid bone chain on '{gameObject.name}'. " +
                    "Assign root, mid, and tip bones.",
                    this);
                return;
            }

            CacheOriginalTransforms();

#if UNITY_ANIMATION_RIGGING
            TrySetupRigConstraint();
#endif

            _initialized = true;
        }

        /// <summary>
        /// Validates that all three bones in the chain are assigned and have a
        /// parent-child relationship.
        /// </summary>
        private void ValidateBoneChain()
        {
            _hasValidChain = _rootBone != null && _midBone != null && _tipBone != null;

            if (_hasValidChain)
            {
                // Log a warning if the hierarchy doesn't look right
                if (!IsDescendantOf(_midBone, _rootBone))
                {
                    Debug.LogWarning(
                        $"[TwoBoneIKController] Mid bone '{_midBone.name}' is not a descendant " +
                        $"of root bone '{_rootBone.name}'.",
                        this);
                }

                if (!IsDescendantOf(_tipBone, _midBone))
                {
                    Debug.LogWarning(
                        $"[TwoBoneIKController] Tip bone '{_tipBone.name}' is not a descendant " +
                        $"of mid bone '{_midBone.name}'.",
                        this);
                }
            }
        }

        /// <summary>
        /// Caches the original local positions/rotations for blend calculations.
        /// </summary>
        private void CacheOriginalTransforms()
        {
            _rootOriginalPos = _rootBone.localPosition;
            _rootOriginalRot = _rootBone.localRotation;
            _midOriginalPos = _midBone.localPosition;
            _midOriginalRot = _midBone.localRotation;
            _tipOriginalPos = _tipBone.localPosition;
            _tipOriginalRot = _tipBone.localRotation;
        }

#if UNITY_ANIMATION_RIGGING

        /// <summary>
        /// Attempts to find or create a <see cref="TwoBoneIKConstraint"/> for this chain.
        /// </summary>
        private void TrySetupRigConstraint()
        {
            // Check if there's already a constraint targeting our bones
            _rigConstraint = GetComponentInChildren<TwoBoneIKConstraint>();
            if (_rigConstraint != null)
            {
                // Ensure it targets our chain
                if (_rigConstraint.root == _rootBone && _rigConstraint.mid == _midBone)
                    return;
                _rigConstraint = null;
            }

            // Create a new constraint
            if (_target == null)
            {
                CreateDefaultTarget();
            }

            _constraintGO = new GameObject($"{_rootBone.name}_TwoBoneIK_Controller");
            _constraintGO.transform.SetParent(transform, false);

            _rigConstraint = _constraintGO.AddComponent<TwoBoneIKConstraint>();
            _rigConstraint.root = _rootBone;
            _rigConstraint.mid = _midBone;
            _rigConstraint.tip = _tipBone;
            _rigConstraint.target = _target;
            _rigConstraint.hint = _hint;

            _rigConstraint.data.maintainTargetOffset = 1f;
            _rigConstraint.data.targetPositionWeight = _blendWeight;
            _rigConstraint.data.targetRotationWeight = _applyRotation ? _blendWeight : 0f;
            _rigConstraint.data.hintWeight = _hintWeight;
        }

#endif

        /// <summary>
        /// Creates a default IK target at the tip bone's world position if none is assigned.
        /// </summary>
        private void CreateDefaultTarget()
        {
            GameObject targetGO = new GameObject($"{_rootBone.name}_IK_Target_Default");
            targetGO.transform.SetParent(transform, false);
            targetGO.transform.position = _tipBone.position;
            targetGO.transform.rotation = _tipBone.rotation;
            _target = targetGO.transform;
        }

        // ──────────────────────────────────────────────
        //  FABRIK solver
        // ──────────────────────────────────────────────

        /// <summary>
        /// Solves the 2-bone IK chain using FABRIK (Forward And Backward Reaching IK).
        /// This is a simple, robust iterative solver suitable for 3-bone chains.
        /// </summary>
        private void SolveFABRIK()
        {
            if (_target == null) return;

            float rootToMidLen = Vector3.Distance(_rootBone.position, _midBone.position);
            float midToTipLen = Vector3.Distance(_midBone.position, _tipBone.position);
            float chainLength = rootToMidLen + midToTipLen;

            Vector3 targetPos = _target.position;

            // Clamp target within reach
            float distToTarget = Vector3.Distance(_rootBone.position, targetPos);
            if (distToTarget > chainLength)
            {
                targetPos = _rootBone.position + (targetPos - _rootBone.position).normalized * chainLength * 0.999f;
            }

            // --- Forward pass: root → mid → tip ---
            Vector3 rootPos = _rootBone.position;
            Vector3 midPos = _midBone.position;
            Vector3 tipPos = _tipBone.position;

            // Set tip to target
            tipPos = targetPos;

            // Pull mid toward tip maintaining mid→tip distance
            Vector3 midToTipDir = (tipPos - midPos).normalized;
            midPos = tipPos - midToTipDir * midToTipLen;

            // Pull root toward mid maintaining root→mid distance
            Vector3 rootToMidDir = (midPos - rootPos).normalized;
            rootPos = midPos - rootToMidDir * rootToMidLen;

            // --- Backward pass: fix root position, propagate forward ---
            rootPos = _rootBone.position;

            Vector3 rootToMidDir2 = (midPos - rootPos).normalized;
            midPos = rootPos + rootToMidDir2 * rootToMidLen;

            Vector3 midToTipDir2 = (tipPos - midPos).normalized;
            tipPos = midPos + midToTipDir2 * midToTipLen;

            // --- Hint (pole vector) adjustment ---
            if (_hint != null && _hintWeight > 0.01f)
            {
                Vector3 hintDir = (_hint.position - rootPos).normalized;
                Vector3 midDir = (midPos - rootPos).normalized;
                Vector3 blendedMidDir = Vector3.Slerp(midDir, hintDir, _hintWeight * 0.5f).normalized;
                midPos = rootPos + blendedMidDir * rootToMidLen;

                // Re-project tip
                Vector3 midToTipDir3 = (tipPos - midPos).normalized;
                tipPos = midPos + midToTipDir3 * midToTipLen;
            }

            // --- Apply with blending ---
            _midBone.position = Vector3.Lerp(_midBone.position, midPos, _blendWeight);
            _tipBone.position = Vector3.Lerp(_tipBone.position, tipPos, _blendWeight);

            // --- Apply rotations ---
            if (_applyRotation)
            {
                Quaternion targetRootRot = Quaternion.LookRotation(
                    (_midBone.position - _rootBone.position).normalized,
                    _hint != null ? (_hint.position - _rootBone.position).normalized : Vector3.up);

                Quaternion targetMidRot = Quaternion.LookRotation(
                    (_tipBone.position - _midBone.position).normalized,
                    _midBone.position - _rootBone.position);

                _rootBone.rotation = Quaternion.Slerp(
                    _rootBone.rotation, targetRootRot, _blendWeight);

                _midBone.rotation = Quaternion.Slerp(
                    _midBone.rotation, targetMidRot, _blendWeight);
            }

            // Apply target rotation to tip
            if (_applyRotation)
            {
                _tipBone.rotation = Quaternion.Slerp(
                    _tipBone.rotation, _target.rotation, _blendWeight);
            }
        }

        // ──────────────────────────────────────────────
        //  Public methods
        // ──────────────────────────────────────────────

        /// <summary>
        /// Reinitializes the controller — useful after changing bone references at runtime.
        /// </summary>
        public void Reinitialize()
        {
            _initialized = false;
            Initialize();
        }

        /// <summary>
        /// Configures the bone chain for this IK controller.
        /// </summary>
        /// <param name="root">Root bone (upper arm / upper leg).</param>
        /// <param name="mid">Mid bone (forearm / shin).</param>
        /// <param name="tip">Tip bone (hand / foot).</param>
        /// <param name="limbType">The type of limb.</param>
        public void SetBoneChain(Transform root, Transform mid, Transform tip, LimbType limbType)
        {
            _rootBone = root;
            _midBone = mid;
            _tipBone = tip;
            _limbType = limbType;
            _initialized = false;
            Initialize();
        }

        /// <summary>
        /// Sets the IK target position in world space. Creates a temporary target if none exists.
        /// </summary>
        /// <param name="worldPosition">Target world position.</param>
        public void SetTargetPosition(Vector3 worldPosition)
        {
            if (_target == null)
                CreateDefaultTarget();

            _target.position = worldPosition;
        }

        /// <summary>
        /// Sets the IK target position and rotation in world space.
        /// </summary>
        /// <param name="worldPosition">Target world position.</param>
        /// <param name="worldRotation">Target world rotation.</param>
        public void SetTargetPose(Vector3 worldPosition, Quaternion worldRotation)
        {
            if (_target == null)
                CreateDefaultTarget();

            _target.position = worldPosition;
            _target.rotation = worldRotation;
        }

        // ──────────────────────────────────────────────
        //  Helpers
        // ──────────────────────────────────────────────

        /// <summary>
        /// Returns true if <paramref name="child"/> is a descendant of <paramref name="potentialParent"/>.
        /// </summary>
        private static bool IsDescendantOf(Transform child, Transform potentialParent)
        {
            if (child == null || potentialParent == null)
                return false;

            Transform current = child.parent;
            while (current != null)
            {
                if (current == potentialParent)
                    return true;
                current = current.parent;
            }
            return false;
        }

        // ──────────────────────────────────────────────
        //  Debug visualization
        // ──────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            if (!_hasValidChain && !Application.isPlaying)
                ValidateBoneChain();

            if (!_hasValidChain) return;

            // Draw bone chain
            Gizmos.color = _limbType == LimbType.LeftArm || _limbType == LimbType.RightArm
                ? Color.cyan
                : Color.magenta;

            if (_rootBone != null && _midBone != null)
            {
                Gizmos.DrawLine(_rootBone.position, _midBone.position);
                Gizmos.DrawSphere(_rootBone.position, 0.08f);
            }

            if (_midBone != null && _tipBone != null)
            {
                Gizmos.DrawLine(_midBone.position, _tipBone.position);
                Gizmos.DrawSphere(_midBone.position, 0.06f);
                Gizmos.DrawSphere(_tipBone.position, 0.1f);
            }

            // Draw target
            if (_target != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(_target.position, 0.15f);
                Gizmos.DrawLine(_tipBone.position, _target.position);
            }

            // Draw hint
            if (_hint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(_hint.position, 0.1f);
            }
        }
    }
}