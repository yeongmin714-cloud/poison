using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectName.Systems.Motions;

namespace ProjectName.Systems
{
    /// <summary>
    /// Classifies a loaded model into a specific rig type based on its skeleton.
    /// </summary>
    public enum ModelType
    {
        /// <summary>No skeleton detected — static mesh without bones.</summary>
        Static,
        /// <summary>Humanoid bipedal skeleton (two arms, two legs, head, spine).</summary>
        RiggedHumanoid,
        /// <summary>Four-legged skeleton (quadruped bones: FL/FR/HL/HR limbs, tail, spine).</summary>
        RiggedQuadruped,
        /// <summary>Monster skeleton — non-standard, fallback heuristic. May include snake-like long spines.</summary>
        RiggedMonster
    }

    /// <summary>
    /// Auto-detects character type from a skeleton and maps it to the appropriate
    /// motion controller components. Used at runtime after a rigged GLB model is loaded.
    ///
    /// Detection rules:
    /// <list type="bullet">
    ///   <item><description>Has QuadrupedBoneNames (UpperLeg_FL, etc.) → <see cref="ModelType.RiggedQuadruped"/></description></item>
    ///   <item><description>Has HumanoidBoneNames (UpperArm_L, UpperLeg_L, etc.) → <see cref="ModelType.RiggedHumanoid"/></description></item>
    ///   <item><description>Has Armature/animation rig with many spine bones (5+) → <see cref="ModelType.RiggedMonster"/> (snake-like)</description></item>
    ///   <item><description>Otherwise → <see cref="ModelType.Static"/></description></item>
    /// </list>
    /// </summary>
    [AddComponentMenu("ProjectName/Systems/Motion Detector")]
    public class MotionDetector : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Detection Result")]
        [SerializeField] private ModelType _detectedType = ModelType.Static;

        [Header("Auto-Apply Motion Components")]
        [SerializeField] private bool _autoSetupOnStart = true;

        #endregion

        #region Private State

        private Transform[] _allBoneTransforms;
        private bool _isDetectionDone;

        #endregion

        #region Public Properties

        /// <summary>The detected model type after skeleton analysis.</summary>
        public ModelType DetectedType => _detectedType;

        /// <summary>True if skeleton detection has been completed.</summary>
        public bool IsDetectionDone => _isDetectionDone;

        /// <summary>
        /// All bone transforms found in the skeleton hierarchy.
        /// May be empty if no skeleton was detected.
        /// </summary>
        public Transform[] AllBoneTransforms
        {
            get
            {
                if (_allBoneTransforms == null)
                    return Array.Empty<Transform>();
                return (Transform[])_allBoneTransforms.Clone();
            }
        }

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            if (_autoSetupOnStart && !_isDetectionDone)
            {
                DetectAndSetup();
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Runs skeleton detection on this GameObject and sets up the appropriate
        /// motion controller components. Safe to call multiple times — re-runs detection.
        /// </summary>
        /// <returns>The detected <see cref="ModelType"/>.</returns>
        public ModelType DetectAndSetup()
        {
            _detectedType = DetectSkeletonType();
            _isDetectionDone = true;

            Debug.Log($"[MotionDetector] '{gameObject.name}' detected as: {_detectedType}", this);

            // Apply appropriate motion components based on type
            switch (_detectedType)
            {
                case ModelType.RiggedQuadruped:
                    SetupQuadrupedMotions();
                    break;
                case ModelType.RiggedHumanoid:
                    SetupHumanoidMotions();
                    break;
                case ModelType.RiggedMonster:
                    SetupMonsterMotions();
                    break;
                case ModelType.Static:
                default:
                    // No procedural motion components needed
                    break;
            }

            return _detectedType;
        }

        /// <summary>
        /// Detects the skeleton type by scanning the bone hierarchy of this GameObject.
        /// Uses <see cref="AnimationBoneDefinitions"/> to match known bone name patterns.
        /// </summary>
        /// <returns>The detected ModelType.</returns>
        public ModelType DetectSkeletonType()
        {
            Transform root = transform;

            // Collect all bone transforms (non-null children with typical bone naming)
            var boneList = new List<Transform>();
            CollectBoneTransforms(root, boneList);
            _allBoneTransforms = boneList.ToArray();

            if (_allBoneTransforms.Length == 0)
                return ModelType.Static;

            // Check for quadruped bones first (more specific)
            if (AnimationBoneDefinitions.TryFindBoneCanonical(root, "UpperLeg_FL") != null ||
                AnimationBoneDefinitions.TryFindBoneCanonical(root, "Paw_FL") != null ||
                AnimationBoneDefinitions.TryFindBoneCanonical(root, "UpperLeg_HL") != null)
            {
                return ModelType.RiggedQuadruped;
            }

            // Check for humanoid bones
            if (AnimationBoneDefinitions.TryFindBoneCanonical(root, "UpperArm_L") != null &&
                AnimationBoneDefinitions.TryFindBoneCanonical(root, "UpperLeg_L") != null &&
                AnimationBoneDefinitions.TryFindBoneCanonical(root, "Head") != null)
            {
                return ModelType.RiggedHumanoid;
            }

            // Check for long spine chain (snake-like monsters)
            int spineCount = CountSpineBones(root);
            if (spineCount >= 5)
            {
                return ModelType.RiggedMonster;
            }

            // Fallback: has bones but not matching known patterns
            if (_allBoneTransforms.Length >= 3)
            {
                // Check for an Animator component (suggests a rigged model)
                Animator animator = GetComponentInChildren<Animator>();
                if (animator != null && animator.isHuman)
                    return ModelType.RiggedHumanoid;

                // Generic rigged model
                return ModelType.RiggedMonster;
            }

            return ModelType.Static;
        }

        /// <summary>
        /// Collects all bone-like transforms from the hierarchy.
        /// Filters out obvious non-bone objects by checking if names
        /// match typical bone naming patterns.
        /// </summary>
        private static void CollectBoneTransforms(Transform root, List<Transform> bones)
        {
            if (root == null) return;

            // Check if this looks like a bone (has a SkinnedMeshRenderer or has bone-like name)
            // We use a simple heuristic: any Transform with children that has no MeshFilter
            // or has name containing "bone", "Bone", "armature", etc.
            bool isBone = true;

            // Skip obvious mesh objects
            if (root.GetComponent<MeshFilter>() != null &&
                root.GetComponent<SkinnedMeshRenderer>() == null)
            {
                isBone = false;
            }

            if (isBone)
                bones.Add(root);

            for (int i = 0; i < root.childCount; i++)
            {
                CollectBoneTransforms(root.GetChild(i), bones);
            }
        }

        /// <summary>
        /// Counts the number of spine-like bones in the hierarchy.
        /// Looks for bones with names containing "Spine", "spine",
        /// "Body", "body", or similar.
        /// </summary>
        private static int CountSpineBones(Transform root)
        {
            int count = 0;
            var queue = new Queue<Transform>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                Transform current = queue.Dequeue();
                string name = current.name.ToLowerInvariant();

                if (name.Contains("spine") || name.Contains("body") ||
                    name.Contains("segment") || name.Contains("bone") ||
                    name.Contains("vertebra"))
                {
                    count++;
                }

                for (int i = 0; i < current.childCount; i++)
                    queue.Enqueue(current.GetChild(i));
            }

            return count;
        }

        #endregion

        #region Motion Setup

        /// <summary>
        /// Adds and configures quadruped motion components (QuadrupedIdle, QuadrupedWalk, QuadrupedRun).
        /// Also adds the AnimationRiggingSetup if not already present.
        /// </summary>
        private void SetupQuadrupedMotions()
        {
            // Ensure AnimationRiggingSetup is present
            var riggingSetup = GetComponent<AnimationRiggingSetup>();
            if (riggingSetup == null)
            {
                riggingSetup = gameObject.AddComponent<AnimationRiggingSetup>();
                riggingSetup.FindBones();
                riggingSetup.SetupRigging();
            }

            // Add quadruped motion components
            if (GetComponent<QuadrupedIdleMotion>() == null)
                gameObject.AddComponent<QuadrupedIdleMotion>();

            if (GetComponent<QuadrupedWalkMotion>() == null)
                gameObject.AddComponent<QuadrupedWalkMotion>();

            if (GetComponent<QuadrupedRunMotion>() == null)
                gameObject.AddComponent<QuadrupedRunMotion>();

            Debug.Log($"[MotionDetector] Quadruped motion components added to '{gameObject.name}'.", this);
        }

        /// <summary>
        /// Adds and configures humanoid motion components (IdleMotion, WalkMotion, RunMotion, etc.).
        /// </summary>
        private void SetupHumanoidMotions()
        {
            // Ensure AnimationRiggingSetup is present
            var riggingSetup = GetComponent<AnimationRiggingSetup>();
            if (riggingSetup == null)
            {
                riggingSetup = gameObject.AddComponent<AnimationRiggingSetup>();
                riggingSetup.FindBones();
                riggingSetup.SetupRigging();
            }

            // Add humanoid motion components (only if not already present)
            if (GetComponent<IdleMotion>() == null)
                gameObject.AddComponent<IdleMotion>();

            if (GetComponent<WalkMotion>() == null)
                gameObject.AddComponent<WalkMotion>();

            if (GetComponent<RunMotion>() == null)
                gameObject.AddComponent<RunMotion>();

            if (GetComponent<JumpMotion>() == null)
                gameObject.AddComponent<JumpMotion>();

            if (GetComponent<GatherMotion>() == null)
                gameObject.AddComponent<GatherMotion>();

            if (GetComponent<CraftMotion>() == null)
                gameObject.AddComponent<CraftMotion>();

            if (GetComponent<AttackMotion>() == null)
                gameObject.AddComponent<AttackMotion>();

            if (GetComponent<ThrowMotion>() == null)
                gameObject.AddComponent<ThrowMotion>();

            Debug.Log($"[MotionDetector] Humanoid motion components added to '{gameObject.name}'.", this);
        }

        /// <summary>
        /// Adds and configures monster motion components.
        /// For long-spined monsters (snakes), adds SnakeSlitherMotion.
        /// For other monsters, adds generic motion components.
        /// </summary>
        private void SetupMonsterMotions()
        {
            // Check for snake-like skeleton (long spine chain)
            int spineCount = CountSpineBones(transform);

            if (spineCount >= 5)
            {
                // Snake-like: add SnakeSlitherMotion
                if (GetComponent<SnakeSlitherMotion>() == null)
                {
                    var slither = gameObject.AddComponent<SnakeSlitherMotion>();

                    // Auto-detect spine bones for the slither chain
                    var spineBones = FindSpineBoneChain(transform);
                    if (spineBones.Count > 0)
                    {
                        slither.SetSpineBones(spineBones);
                    }
                }

                Debug.Log($"[MotionDetector] Snake slither motion added to '{gameObject.name}' " +
                          $"(detected {spineCount} spine bones).", this);
            }
            else
            {
                // Other monster types: add basic motion components
                SetupHumanoidMotions();
            }
        }

        /// <summary>
        /// Finds an ordered chain of spine/body bones from the skeleton.
        /// Walks from head to tail through the bone hierarchy.
        /// </summary>
        private static List<Transform> FindSpineBoneChain(Transform root)
        {
            var spineBones = new List<Transform>();

            // Use BFS/DFS to find bones with spine/body/segment-like names
            var candidates = new List<Transform>();
            var queue = new Queue<Transform>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                Transform current = queue.Dequeue();
                string name = current.name.ToLowerInvariant();

                if (name.Contains("spine") || name.Contains("body") ||
                    name.Contains("segment") || name.Contains("vertebra") ||
                    name.Contains("bone"))
                {
                    candidates.Add(current);
                }

                for (int i = 0; i < current.childCount; i++)
                    queue.Enqueue(current.GetChild(i));
            }

            // Sort candidates by depth (parent first = head to tail heuristic)
            // Simple approach: sort by transform sibling index hierarchy
            candidates.Sort((a, b) =>
            {
                int depthA = GetDepth(a);
                int depthB = GetDepth(b);
                return depthA.CompareTo(depthB);
            });

            return candidates;
        }

        /// <summary>
        /// Returns the depth of a Transform in the hierarchy.
        /// </summary>
        private static int GetDepth(Transform t)
        {
            int depth = 0;
            while (t.parent != null)
            {
                depth++;
                t = t.parent;
            }
            return depth;
        }

        #endregion
    }
}