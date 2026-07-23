using UnityEngine;

namespace ProjectName.Systems.Animation.Neural.Evaluation
{
    /// <summary>
    /// Physics validity checker for Neural Animation output.
    /// Checks penetration, floating feet, and joint limit violations per frame.
    /// </summary>
    [RequireComponent(typeof(NeuralAnimationController))]
    public class PhysicsValidityChecker : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] LayerMask _groundMask = ~0;
        [SerializeField] LayerMask _environmentMask = ~0;
        [SerializeField, Range(0.1f, 2f)] float _footGroundThreshold = 0.3f;
        [SerializeField] bool _showGizmos = true;

        [Header("Live Validity")]
        [SerializeField, Range(0f, 1f)] float _validityScore = 1f;
        [SerializeField] float _penetrationDepth;
        [SerializeField] float _leftFootFloatDistance;
        [SerializeField] float _rightFootFloatDistance;
        [SerializeField] int _jointLimitViolations;

        // ──────────────────────────────────────────────
        //  State
        // ──────────────────────────────────────────────

        NeuralAnimationController _controller;
        Animator _animator;
        Collider[] _overlapResults = new Collider[8];
        float _scoreSum;
        int _scoreCount;

        // Weight factors for validity score
        const float PENETRATION_WEIGHT = 0.4f;
        const float FLOATING_FEET_WEIGHT = 0.35f;
        const float JOINT_LIMIT_WEIGHT = 0.25f;

        // ──────────────────────────────────────────────
        //  Unity Lifecycle
        // ──────────────────────────────────────────────

        void Awake()
        {
            _controller = GetComponent<NeuralAnimationController>();
            _animator = GetComponent<Animator>();
        }

        void LateUpdate()
        {
            EvaluateValidity();
        }

        // ──────────────────────────────────────────────
        //  Validity Checks
        // ──────────────────────────────────────────────

        void EvaluateValidity()
        {
            float penetrationScore = 1f;
            float footScore = 1f;
            float jointScore = 1f;

            // 1. Penetration check
            CheckPenetration(out penetrationScore, out _penetrationDepth);

            // 2. Floating feet check
            CheckFloatingFeet(out footScore, out _leftFootFloatDistance, out _rightFootFloatDistance);

            // 3. Joint limit check
            CheckJointLimits(out jointScore, out _jointLimitViolations);

            // Weighted average
            _validityScore = Mathf.Clamp01(
                penetrationScore * PENETRATION_WEIGHT +
                footScore * FLOATING_FEET_WEIGHT +
                jointScore * JOINT_LIMIT_WEIGHT
            );

            _scoreSum += _validityScore;
            _scoreCount++;
        }

        void CheckPenetration(out float score, out float maxDepth)
        {
            maxDepth = 0f;
            int hits = Physics.OverlapSphereNonAlloc(transform.position, 0.5f, _overlapResults, _environmentMask);
            for (int i = 0; i < hits; i++)
            {
                if (_overlapResults[i].transform == transform) continue;
                Vector3 dir = _overlapResults[i].ClosestPoint(transform.position) - transform.position;
                float depth = 0.5f - dir.magnitude;
                if (depth > maxDepth) maxDepth = depth;
            }
            // Score: 0% penetration = 1.0, 0.5m+ penetration = 0.0
            score = Mathf.Clamp01(1f - (maxDepth / 0.5f));
        }

        void CheckFloatingFeet(out float score, out float leftFloat, out float rightFloat)
        {
            leftFloat = 0f;
            rightFloat = 0f;

            Transform lFoot = null, rFoot = null;
            var boneMap = GetComponent<Procedural.Bones.ProceduralBoneMap>();
            if (boneMap != null)
            {
                lFoot = boneMap.Get(Procedural.Bones.BoneRole.L_Foot);
                rFoot = boneMap.Get(Procedural.Bones.BoneRole.R_Foot);
            }

            if (lFoot != null)
            {
                if (Physics.Raycast(lFoot.position, Vector3.down, out var hit, _footGroundThreshold * 2f, _groundMask))
                    leftFloat = lFoot.position.y - hit.point.y;
                else
                    leftFloat = _footGroundThreshold;
            }

            if (rFoot != null)
            {
                if (Physics.Raycast(rFoot.position, Vector3.down, out var hit, _footGroundThreshold * 2f, _groundMask))
                    rightFloat = rFoot.position.y - hit.point.y;
                else
                    rightFloat = _footGroundThreshold;
            }

            float maxFloat = Mathf.Max(leftFloat, rightFloat);
            score = Mathf.Clamp01(1f - (maxFloat / _footGroundThreshold));
        }

        void CheckJointLimits(out float score, out int violations)
        {
            violations = 0;
            if (_animator == null) { score = 1f; return; }

            // Simple check: if any bone rotation looks extreme
            var bones = GetComponent<Procedural.Bones.ProceduralBoneMap>()?.GetAllBones();
            if (bones == null) { score = 1f; return; }

            foreach (var bone in bones)
            {
                if (bone.Transform == null) continue;
                Quaternion localRot = bone.Transform.localRotation;
                float angle = localRot.eulerAngles.magnitude;
                if (angle > 150f) // Extreme rotation past reasonable limit
                    violations++;
            }

            score = Mathf.Clamp01(1f - (violations * 0.1f));
        }

        // ──────────────────────────────────────────────
        //  Public API
        // ──────────────────────────────────────────────

        /// <summary>
        /// Current validity score (0=invalid, 1=perfect).
        /// </summary>
        public float ValidityScore => _validityScore;

        /// <summary>
        /// Average validity score since last reset.
        /// </summary>
        public float AverageValidityScore => _scoreCount > 0 ? _scoreSum / _scoreCount : 1f;

        /// <summary>
        /// Reset average score tracking.
        /// </summary>
        public void ResetScore()
        {
            _scoreSum = 0f;
            _scoreCount = 0;
        }

        /// <summary>
        /// Get a formatted validity report.
        /// </summary>
        public string GetValidityReport()
        {
            return $"=== Physics Validity Report ===\n" +
                   $"Validity Score: {_validityScore:P1}\n" +
                   $"Penetration Depth: {_penetrationDepth:F3}m\n" +
                   $"Left Foot Float: {_leftFootFloatDistance:F3}m\n" +
                   $"Right Foot Float: {_rightFootFloatDistance:F3}m\n" +
                   $"Joint Limit Violations: {_jointLimitViolations}";
        }

        void OnDrawGizmos()
        {
            if (!_showGizmos) return;

            // Color code by validity
            Gizmos.color = _validityScore > 0.7f ? Color.green :
                           _validityScore > 0.4f ? Color.yellow : Color.red;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.5f, 0.3f);

            // Foot float indicators
            if (_leftFootFloatDistance > _footGroundThreshold)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawRay(transform.position + Vector3.left * 0.3f, Vector3.down * _leftFootFloatDistance);
            }
            if (_rightFootFloatDistance > _footGroundThreshold)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawRay(transform.position + Vector3.right * 0.3f, Vector3.down * _rightFootFloatDistance);
            }
        }
    }
}