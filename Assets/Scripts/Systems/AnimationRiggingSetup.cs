using System;
using System.Collections.Generic;
using UnityEngine;
#pragma warning disable 0414

#if UNITY_ANIMATION_RIGGING
using UnityEngine.Animations.Rigging;
#endif

namespace ProjectName.Systems
{
    /// <summary>
    /// Automatically sets up Unity's Animation Rigging (RigBuilder + Rig + constraints)
    /// on a rigged GameObject. Works with humanoid, quadruped, and monster skeletons.
    ///
    /// Attach this component to a character root that has a valid bone hierarchy.
    /// Call <see cref="SetupRigging"/> after bones are discovered, or rely on
    /// <see cref="FindBones"/> + <see cref="RegisterWithRuntimeModelLoader"/> for
    /// automatic setup when a GLB model is loaded via <see cref="RuntimeModelLoader"/>.
    /// </summary>
    [AddComponentMenu("ProjectName/Systems/Animation Rigging Setup")]
    public class AnimationRiggingSetup : MonoBehaviour
    {
        #region Nested Types

        /// <summary>
        /// Event raised when a new rigged model is loaded. Subscribe via
        /// <see cref="OnModelLoaded"/> to auto-setup rigging on newly instantiated models.
        /// </summary>
        public static event Action<GameObject> OnModelLoaded;

        #endregion

        // ──────────────────────────────────────────────
        //  Serialized bone references
        // ──────────────────────────────────────────────

        [Header("Bone References (auto-populated by FindBones)")]
        [SerializeField] private Transform _headBone;
        [SerializeField] private Transform _spineBone;
        [SerializeField] private Transform _leftArmBone;
        [SerializeField] private Transform _rightArmBone;
        [SerializeField] private Transform _leftLegBone;
        [SerializeField] private Transform _rightLegBone;

        [Header("Optional Extra Bones")]
        [SerializeField] private Transform _neckBone;
        [SerializeField] private Transform _chestBone;
        [SerializeField] private Transform _hipsBone;

        [Header("Rigging Settings")]
        [SerializeField] private bool _autoFindBonesOnStart = true;
        [SerializeField] private bool _autoSetupRiggingOnStart = true;

        /// <summary>Skeleton type — auto-detected or manually specified.</summary>
        [SerializeField] private CharacterType _characterType = CharacterType.Humanoid;

        // ──────────────────────────────────────────────
        //  Public properties
        // ──────────────────────────────────────────────

        /// <summary>Detected head bone Transform.</summary>
        public Transform HeadBone => _headBone;
        /// <summary>Detected spine bone Transform.</summary>
        public Transform SpineBone => _spineBone;
        /// <summary>Detected left arm bone Transform.</summary>
        public Transform LeftArmBone => _leftArmBone;
        /// <summary>Detected right arm bone Transform.</summary>
        public Transform RightArmBone => _rightArmBone;
        /// <summary>Detected left leg bone Transform.</summary>
        public Transform LeftLegBone => _leftLegBone;
        /// <summary>Detected right leg bone Transform.</summary>
        public Transform RightLegBone => _rightLegBone;
        /// <summary>Detected neck bone Transform (optional).</summary>
        public Transform NeckBone => _neckBone;
        /// <summary>Detected chest bone Transform (optional).</summary>
        public Transform ChestBone => _chestBone;
        /// <summary>Detected hips bone Transform (optional).</summary>
        public Transform HipsBone => _hipsBone;

        /// <summary>
        /// The character type this setup is configured for.
        /// </summary>
        public CharacterType CharacterType => _characterType;

        /// <summary>True if rigging has been set up successfully.</summary>
        public bool IsRiggingReady { get; private set; }

        /// <summary>
        /// True if the Animation Rigging package is available at compile time.
        /// </summary>
        public static bool IsAnimationRiggingAvailable
        {
            get
            {
#if UNITY_ANIMATION_RIGGING
                return true;
#else
                return false;
#endif
            }
        }

        // ──────────────────────────────────────────────
        //  Unity lifecycle
        // ──────────────────────────────────────────────

        private void Start()
        {
            if (_autoFindBonesOnStart)
                FindBones();

            if (_autoSetupRiggingOnStart && IsRiggingReady)
                SetupRigging();
        }

        private void OnDestroy()
        {
            // Clean up any rig builder references if this object is destroyed
            UnregisterFromRuntimeModelLoader();
            IsRiggingReady = false;
        }

        private void Awake()
        {
            // If bones were manually assigned in the inspector, mark rigging as ready
            if (_headBone != null)
                IsRiggingReady = true;
        }

        // ──────────────────────────────────────────────
        //  Bone discovery
        // ──────────────────────────────────────────────

        /// <summary>
        /// Searches the GameObject hierarchy for common bone names and populates
        /// the serialized bone reference fields. Uses <see cref="AnimationBoneDefinitions"/>
        /// to handle humanoid, quadruped, and monster naming conventions.
        /// </summary>
        /// <returns>True if all core bones (head, spine) were found.</returns>
        public bool FindBones()
        {
            Transform root = transform;

            // Determine character type heuristically if not manually set
            if (_characterType == CharacterType.Humanoid)
            {
                // Check if we find quadruped-specific bones
                if (AnimationBoneDefinitions.TryFindBoneCanonical(root, "UpperLeg_FL") != null ||
                    AnimationBoneDefinitions.TryFindBoneCanonical(root, "Paw_FL") != null)
                {
                    _characterType = CharacterType.Quadruped;
                }
            }

            string[] boneNames = AnimationBoneDefinitions.GetBoneNamesForType(_characterType);

            _headBone = FindBoneWithFallback("Head", boneNames);
            _spineBone = FindBoneWithFallback("Spine", boneNames);
            _neckBone = FindBoneWithFallback("Neck", boneNames);
            _chestBone = FindBoneWithFallback("Chest", boneNames);
            _hipsBone = FindBoneWithFallback("Hips", boneNames);

            if (_characterType == CharacterType.Humanoid)
            {
                _leftArmBone = FindBoneWithFallback("UpperArm_L", boneNames);
                _rightArmBone = FindBoneWithFallback("UpperArm_R", boneNames);
                _leftLegBone = FindBoneWithFallback("UpperLeg_L", boneNames);
                _rightLegBone = FindBoneWithFallback("UpperLeg_R", boneNames);
            }
            else
            {
                // Quadruped / Monster: find limb roots
                _leftArmBone = FindBoneWithFallback("UpperLeg_FL", boneNames)
                               ?? FindBoneWithFallback("Arm_L", AnimationBoneDefinitions.MonsterBoneGroups[4]);
                _rightArmBone = FindBoneWithFallback("UpperLeg_FR", boneNames)
                                ?? FindBoneWithFallback("Arm_R", AnimationBoneDefinitions.MonsterBoneGroups[5]);
                _leftLegBone = FindBoneWithFallback("UpperLeg_HL", boneNames)
                               ?? FindBoneWithFallback("Leg_L", AnimationBoneDefinitions.MonsterBoneGroups[6]);
                _rightLegBone = FindBoneWithFallback("UpperLeg_HR", boneNames)
                                ?? FindBoneWithFallback("Leg_R", AnimationBoneDefinitions.MonsterBoneGroups[7]);
            }

            // Head is the critical bone — if missing, rigging won't work well
            IsRiggingReady = _headBone != null;

            if (!IsRiggingReady)
            {
                Debug.LogWarning(
                    $"[AnimationRiggingSetup] Head bone not found on '{gameObject.name}'. " +
                    "Rigging setup skipped. Assign bones manually or check the model skeleton.",
                    this);
            }

            return IsRiggingReady;
        }

        /// <summary>
        /// Attempts to find a bone by canonical name first, then by iterating all
        /// bone names for the current character type.
        /// </summary>
        private Transform FindBoneWithFallback(string canonicalName, string[] allBoneNames)
        {
            // Try canonical with alternates first
            Transform result = AnimationBoneDefinitions.TryFindBoneCanonical(transform, canonicalName);
            if (result != null)
                return result;

            // Fallback: search every known bone name for this type
            foreach (string name in allBoneNames)
            {
                if (name.Equals(canonicalName, StringComparison.OrdinalIgnoreCase))
                    continue; // already tried

                result = AnimationBoneDefinitions.TryFindBone(transform, new[] { name });
                if (result != null)
                    return result;
            }

            return null;
        }

        // ──────────────────────────────────────────────
        //  Rigging setup
        // ──────────────────────────────────────────────

        /// <summary>
        /// Creates or configures the RigBuilder and Rig components on this GameObject.
        /// Adds TwoBoneIK constraints for arms/legs and a MultiAim constraint for the head
        /// when the Animation Rigging package is available.
        /// </summary>
        public void SetupRigging()
        {
            if (!IsRiggingReady)
            {
                Debug.LogWarning(
                    "[AnimationRiggingSetup] Cannot setup rigging — bones not found. " +
                    "Call FindBones() first or assign bones manually.",
                    this);
                return;
            }

#if UNITY_ANIMATION_RIGGING
            SetupRiggingInternal();
            IsRiggingReady = true;
            Debug.Log(
                $"[AnimationRiggingSetup] Rigging setup complete on '{gameObject.name}' " +
                $"(type: {_characterType}).",
                this);
#else
            Debug.Log(
                "[AnimationRiggingSetup] Animation Rigging package not installed. " +
                "RigBuilder and constraints will not be created. " +
                "Add com.unity.animation.rigging to Packages/manifest.json to enable.",
                this);
#endif
        }

#if UNITY_ANIMATION_RIGGING

        /// <summary>
        /// Internal rigging setup that uses Unity's Animation Rigging package types.
        /// Only compiled when UNITY_ANIMATION_RIGGING is defined.
        /// </summary>
        private void SetupRiggingInternal()
        {
            // 1. Ensure RigBuilder exists
            RigBuilder rigBuilder = GetComponent<RigBuilder>();
            if (rigBuilder == null)
                rigBuilder = gameObject.AddComponent<RigBuilder>();

            // Prepare layers list — preserve any existing layers
            var layers = new List<RigLayer>(rigBuilder.layers);

            // 2. Create Rig layers and register each with the builder
            CreateRigLayer(rigBuilder, layers, "IK_Arm_L", CreateArmIKConstraint(_leftArmBone));
            CreateRigLayer(rigBuilder, layers, "IK_Arm_R", CreateArmIKConstraint(_rightArmBone));
            CreateRigLayer(rigBuilder, layers, "IK_Leg_L", CreateLegIKConstraint(_leftLegBone));
            CreateRigLayer(rigBuilder, layers, "IK_Leg_R", CreateLegIKConstraint(_rightLegBone));

            if (_headBone != null)
                CreateRigLayer(rigBuilder, layers, "Aim_Head", CreateAimConstraint(_headBone));

            // 3. Assign updated layers and build
            rigBuilder.layers = layers.ToArray();
            rigBuilder.Build();
        }

        /// <summary>
        /// Creates a Rig GameObject with the given name and constraint, adding it
        /// as a child of this transform, and registers it with the RigBuilder.
        /// </summary>
        private void CreateRigLayer(RigBuilder rigBuilder, List<RigLayer> layers, string layerName, IRigConstraint constraint)
        {
            if (constraint == null)
                return;

            GameObject rigGO = new GameObject(layerName);
            rigGO.transform.SetParent(transform, false);
            rigGO.transform.localPosition = Vector3.zero;
            rigGO.transform.localRotation = Quaternion.identity;

            Rig rig = rigGO.AddComponent<Rig>();
            rig.weight = 1f;

            // Register this Rig layer with the RigBuilder
            layers.Add(new RigLayer(rig, true));

            // Add the constraint component
            MonoBehaviour constraintBehaviour = constraint as MonoBehaviour;
            if (constraintBehaviour != null)
            {
                constraintBehaviour.transform.SetParent(rigGO.transform, false);
            }
        }

        /// <summary>
        /// Creates a TwoBoneIKConstraint for a given arm root bone.
        /// Expects the bone hierarchy: UpperArm → LowerArm → Hand.
        /// </summary>
        private TwoBoneIKConstraint CreateArmIKConstraint(Transform armRoot)
        {
            if (armRoot == null) return null;

            // Resolve lower arm and hand by searching children
            Transform lowerArm = FindChildBone(armRoot, "LowerArm", "Forearm", "Elbow");
            Transform hand = lowerArm != null
                ? FindChildBone(lowerArm, "Hand", "Wrist")
                : null;

            if (lowerArm == null || hand == null)
            {
                // Try using the first two children as chain
                if (armRoot.childCount >= 2)
                {
                    lowerArm = armRoot.GetChild(0);
                    hand = lowerArm.GetChild(0);
                }
                else
                {
                    return null;
                }
            }

            // Create target and hint objects
            GameObject targetGO = new GameObject($"{armRoot.name}_IK_Target");
            targetGO.transform.SetParent(transform, false);
            targetGO.transform.position = hand.position;
            targetGO.transform.rotation = hand.rotation;

            GameObject hintGO = new GameObject($"{armRoot.name}_IK_Hint");
            hintGO.transform.SetParent(transform, false);
            hintGO.transform.position = lowerArm.position + (lowerArm.position - armRoot.position).normalized * 0.5f;

            // Create and configure the constraint
            GameObject constraintGO = new GameObject($"{armRoot.name}_TwoBoneIK");
            constraintGO.transform.SetParent(transform, false);

            var constraint = constraintGO.AddComponent<TwoBoneIKConstraint>();
            constraint.root = armRoot;
            constraint.mid = lowerArm;
            constraint.tip = hand;
            constraint.target = targetGO.transform;
            constraint.hint = hintGO.transform;

            // Set reasonable source/mask values
            constraint.data.maintainTargetOffset = 1f;
            constraint.data.targetPositionWeight = 1f;
            constraint.data.targetRotationWeight = 1f;
            constraint.data.hintWeight = 0.5f;

            return constraint;
        }

        /// <summary>
        /// Creates a TwoBoneIKConstraint for a given leg root bone.
        /// Expects the bone hierarchy: UpperLeg → LowerLeg → Foot.
        /// </summary>
        private TwoBoneIKConstraint CreateLegIKConstraint(Transform legRoot)
        {
            if (legRoot == null) return null;

            Transform lowerLeg = FindChildBone(legRoot, "LowerLeg", "Knee", "Shin");
            Transform foot = lowerLeg != null
                ? FindChildBone(lowerLeg, "Foot", "Ankle", "Paw")
                : null;

            if (lowerLeg == null || foot == null)
            {
                if (legRoot.childCount >= 2)
                {
                    lowerLeg = legRoot.GetChild(0);
                    foot = lowerLeg.GetChild(0);
                }
                else
                {
                    return null;
                }
            }

            GameObject targetGO = new GameObject($"{legRoot.name}_IK_Target");
            targetGO.transform.SetParent(transform, false);
            targetGO.transform.position = foot.position;
            targetGO.transform.rotation = foot.rotation;

            GameObject hintGO = new GameObject($"{legRoot.name}_IK_Hint");
            hintGO.transform.SetParent(transform, false);
            hintGO.transform.position = lowerLeg.position + (lowerLeg.position - legRoot.position).normalized * 0.5f;

            GameObject constraintGO = new GameObject($"{legRoot.name}_TwoBoneIK");
            constraintGO.transform.SetParent(transform, false);

            var constraint = constraintGO.AddComponent<TwoBoneIKConstraint>();
            constraint.root = legRoot;
            constraint.mid = lowerLeg;
            constraint.tip = foot;
            constraint.target = targetGO.transform;
            constraint.hint = hintGO.transform;

            constraint.data.maintainTargetOffset = 1f;
            constraint.data.targetPositionWeight = 1f;
            constraint.data.targetRotationWeight = 1f;
            constraint.data.hintWeight = 0.7f;

            return constraint;
        }

        /// <summary>
        /// Creates a MultiAimConstraint for head tracking.
        /// </summary>
        private MultiAimConstraint CreateAimConstraint(Transform headBone)
        {
            if (headBone == null) return null;

            GameObject constraintGO = new GameObject("Head_Aim");
            constraintGO.transform.SetParent(transform, false);

            var constraint = constraintGO.AddComponent<MultiAimConstraint>();
            constraint.data.constrainedObject = headBone;
            constraint.data.offset = Vector3.zero;
            constraint.data.constrainedXAxis = true;
            constraint.data.constrainedYAxis = true;
            constraint.data.constrainedZAxis = false;
            constraint.data.maintainOffset = false;

            // Start with no sources — caller sets them at runtime
            var sourceArray = new WeightedTransformArray();
            constraint.data.sourceObjects = sourceArray;

            return constraint;
        }

        /// <summary>
        /// Searches children of a bone for any of the given name possibilities.
        /// </summary>
        private Transform FindChildBone(Transform parent, params string[] possibleNames)
        {
            if (parent == null) return null;

            // Search direct children first (breadth-first lite)
            var queue = new Queue<Transform>();
            for (int i = 0; i < parent.childCount; i++)
                queue.Enqueue(parent.GetChild(i));

            while (queue.Count > 0)
            {
                Transform child = queue.Dequeue();
                foreach (string name in possibleNames)
                {
                    if (child.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                        return child;
                }

                for (int i = 0; i < child.childCount; i++)
                    queue.Enqueue(child.GetChild(i));
            }

            return null;
        }

#endif // UNITY_ANIMATION_RIGGING

        // ──────────────────────────────────────────────
        //  RuntimeModelLoader integration
        // ──────────────────────────────────────────────

        /// <summary>
        /// Registers this component to automatically set up rigging when a rigged
        /// model is loaded via <see cref="RuntimeModelLoader"/>.
        /// Subscribes to <see cref="OnModelLoaded"/> so that any future model loads
        /// trigger <see cref="SetupRigging"/> on the instantiated model if it has
        /// an <see cref="AnimationRiggingSetup"/> component.
        /// </summary>
        public void RegisterWithRuntimeModelLoader()
        {
            OnModelLoaded += HandleModelLoaded;
            Debug.Log(
                $"[AnimationRiggingSetup] Registered '{gameObject.name}' for " +
                "auto-rigging on model load events.",
                this);
        }

        /// <summary>
        /// Unsubscribes from the model-loaded event.
        /// </summary>
        public void UnregisterFromRuntimeModelLoader()
        {
            OnModelLoaded -= HandleModelLoaded;
        }

        /// <summary>
        /// Handles the <see cref="OnModelLoaded"/> event by attempting to find and
        /// configure an <see cref="AnimationRiggingSetup"/> on the loaded model.
        /// </summary>
        /// <param name="loadedModel">The newly instantiated model GameObject.</param>
        private static void HandleModelLoaded(GameObject loadedModel)
        {
            if (loadedModel == null) return;

            // Check if the loaded model already has a setup component
            var setup = loadedModel.GetComponentInChildren<AnimationRiggingSetup>();
            if (setup == null)
            {
                // Add one if this looks like a rigged character
                if (loadedModel.transform.childCount > 3) // heuristic: has bones
                {
                    setup = loadedModel.AddComponent<AnimationRiggingSetup>();
                }
            }

            if (setup != null)
            {
                setup.FindBones();
                setup.SetupRigging();
            }
        }

        // ──────────────────────────────────────────────
        //  Public utility methods
        // ──────────────────────────────────────────────

        /// <summary>
        /// Manually sets all core bone references. Useful when bones are known at
        /// design time or discovered through other means.
        /// </summary>
        /// <param name="head">Head Transform.</param>
        /// <param name="spine">Spine Transform.</param>
        /// <param name="leftArm">Left arm root Transform.</param>
        /// <param name="rightArm">Right arm root Transform.</param>
        /// <param name="leftLeg">Left leg root Transform.</param>
        /// <param name="rightLeg">Right leg root Transform.</param>
        public void SetBones(Transform head, Transform spine, Transform leftArm,
            Transform rightArm, Transform leftLeg, Transform rightLeg)
        {
            _headBone = head;
            _spineBone = spine;
            _leftArmBone = leftArm;
            _rightArmBone = rightArm;
            _leftLegBone = leftLeg;
            _rightLegBone = rightLeg;

            IsRiggingReady = _headBone != null;
        }

        /// <summary>
        /// Notifies all registered listeners that a rigged model has been loaded.
        /// Call this from model-loading code after instantiating a rigged GLB.
        /// </summary>
        /// <param name="modelInstance">The instantiated model GameObject.</param>
        public static void NotifyModelLoaded(GameObject modelInstance)
        {
            if (modelInstance == null) return;
            OnModelLoaded?.Invoke(modelInstance);
        }

        /// <summary>
        /// Draws gizmos in the Scene view to visualize the detected bone hierarchy.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!IsRiggingReady && !Application.isPlaying)
                return;

            DrawBoneGizmo(_headBone, Color.yellow, "Head");
            DrawBoneGizmo(_spineBone, Color.green, "Spine");
            DrawBoneGizmo(_leftArmBone, Color.cyan, "L_Arm");
            DrawBoneGizmo(_rightArmBone, Color.cyan, "R_Arm");
            DrawBoneGizmo(_leftLegBone, Color.magenta, "L_Leg");
            DrawBoneGizmo(_rightLegBone, Color.magenta, "R_Leg");
            DrawBoneGizmo(_neckBone, Color.yellow, "Neck");
            DrawBoneGizmo(_chestBone, Color.green, "Chest");
            DrawBoneGizmo(_hipsBone, new Color(0.5f, 0.5f, 0.5f), "Hips");
        }

        /// <summary>
        /// Draws a sphere + label gizmo at the given bone's position.
        /// </summary>
        private void DrawBoneGizmo(Transform bone, Color color, string label)
        {
            if (bone == null) return;

            Gizmos.color = color;
            Gizmos.DrawSphere(bone.position, 0.15f);

#if UNITY_EDITOR
            UnityEditor.Handles.color = color;
            UnityEditor.Handles.Label(bone.position + Vector3.up * 0.25f, label);
#endif
        }
    }
}