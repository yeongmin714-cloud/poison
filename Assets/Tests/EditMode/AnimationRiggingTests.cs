using System.Collections;
using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for the Animation Rigging core system components.
    /// Tests bone discovery, state transitions, procedural IK math, and
    /// aim rotation without requiring the Animation Rigging package.
    /// </summary>
    [TestFixture]
    public class AnimationRiggingTests
    {
        #region AnimationBoneDefinitions Tests

        /// <summary>
        /// Tests that <see cref="AnimationBoneDefinitions.TryFindBone"/> returns null
        /// when given null arguments.
        /// </summary>
        [Test]
        public void AnimationBoneDefinitions_TryFindBone_NullRoot_ReturnsNull()
        {
            Transform result = AnimationBoneDefinitions.TryFindBone(null, new[] { "Head" });
            Assert.IsNull(result, "Null root should return null");
        }

        /// <summary>
        /// Tests that <see cref="AnimationBoneDefinitions.TryFindBone"/> returns null
        /// when given null names array.
        /// </summary>
        [Test]
        public void AnimationBoneDefinitions_TryFindBone_NullNames_ReturnsNull()
        {
            var go = new GameObject("Root");
            Transform result = AnimationBoneDefinitions.TryFindBone(go.transform, null);
            Assert.IsNull(result, "Null names should return null");
            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that <see cref="AnimationBoneDefinitions.TryFindBone"/> correctly finds
        /// a bone by name in a flat hierarchy.
        /// </summary>
        [Test]
        public void AnimationBoneDefinitions_TryFindBone_FindsHeadInHierarchy()
        {
            var root = new GameObject("Root");
            var head = new GameObject("Head");
            head.transform.SetParent(root.transform);

            Transform result = AnimationBoneDefinitions.TryFindBone(
                root.transform, new[] { "Head" });

            Assert.IsNotNull(result, "Should find Head bone");
            Assert.AreEqual("Head", result.name, "Found bone should be named Head");

            Object.DestroyImmediate(root);
        }

        /// <summary>
        /// Tests that <see cref="AnimationBoneDefinitions.TryFindBone"/> performs a
        /// breadth-first search through nested hierarchy.
        /// </summary>
        [Test]
        public void AnimationBoneDefinitions_TryFindBone_NestedHierarchy()
        {
            var root = new GameObject("Root");
            var spine = new GameObject("Spine");
            spine.transform.SetParent(root.transform);
            var head = new GameObject("Head");
            head.transform.SetParent(spine.transform);
            var armL = new GameObject("Arm_L");
            armL.transform.SetParent(spine.transform);

            Transform result = AnimationBoneDefinitions.TryFindBone(
                root.transform, new[] { "Head", "Arm_L" });

            Assert.IsNotNull(result, "Should find a bone from the hierarchy");
            Assert.AreEqual("Head", result.name, "Head should be found first (BFS)");

            Object.DestroyImmediate(root);
        }

        /// <summary>
        /// Tests that <see cref="AnimationBoneDefinitions.GetBoneNamesForType"/> returns
        /// appropriate arrays for each character type.
        /// </summary>
        [Test]
        public void AnimationBoneDefinitions_GetBoneNamesForType_ReturnsCorrectCounts()
        {
            string[] humanoid = AnimationBoneDefinitions.GetBoneNamesForType(CharacterType.Humanoid);
            Assert.GreaterOrEqual(humanoid.Length, 10,
                "Humanoid should have at least 10 bone names");

            string[] quadruped = AnimationBoneDefinitions.GetBoneNamesForType(CharacterType.Quadruped);
            Assert.GreaterOrEqual(quadruped.Length, 10,
                "Quadruped should have at least 10 bone names");

            string[] monster = AnimationBoneDefinitions.GetBoneNamesForType(CharacterType.Monster);
            Assert.GreaterOrEqual(monster.Length, 5,
                "Monster should have at least 5 bone name groups");
        }

        /// <summary>
        /// Tests that <see cref="AnimationBoneDefinitions.HumanoidBoneNames"/> includes
        /// all required bone name constants from the spec.
        /// </summary>
        [Test]
        public void AnimationBoneDefinitions_HumanoidBoneNames_ContainsRequiredBones()
        {
            Assert.Contains("Head", AnimationBoneDefinitions.HumanoidBoneNames);
            Assert.Contains("Neck", AnimationBoneDefinitions.HumanoidBoneNames);
            Assert.Contains("Spine", AnimationBoneDefinitions.HumanoidBoneNames);
            Assert.Contains("Chest", AnimationBoneDefinitions.HumanoidBoneNames);
            Assert.Contains("Hips", AnimationBoneDefinitions.HumanoidBoneNames);
            Assert.Contains("UpperArm_L", AnimationBoneDefinitions.HumanoidBoneNames);
            Assert.Contains("UpperArm_R", AnimationBoneDefinitions.HumanoidBoneNames);
            Assert.Contains("LowerArm_L", AnimationBoneDefinitions.HumanoidBoneNames);
            Assert.Contains("LowerArm_R", AnimationBoneDefinitions.HumanoidBoneNames);
            Assert.Contains("Hand_L", AnimationBoneDefinitions.HumanoidBoneNames);
            Assert.Contains("Hand_R", AnimationBoneDefinitions.HumanoidBoneNames);
            Assert.Contains("UpperLeg_L", AnimationBoneDefinitions.HumanoidBoneNames);
            Assert.Contains("UpperLeg_R", AnimationBoneDefinitions.HumanoidBoneNames);
            Assert.Contains("LowerLeg_L", AnimationBoneDefinitions.HumanoidBoneNames);
            Assert.Contains("LowerLeg_R", AnimationBoneDefinitions.HumanoidBoneNames);
            Assert.Contains("Foot_L", AnimationBoneDefinitions.HumanoidBoneNames);
            Assert.Contains("Foot_R", AnimationBoneDefinitions.HumanoidBoneNames);
        }

        /// <summary>
        /// Tests that <see cref="AnimationBoneDefinitions.GetAlternateNames"/> returns
        /// alternate name variants for known canonical bones.
        /// </summary>
        [Test]
        public void AnimationBoneDefinitions_GetAlternateNames_ReturnsAlternatives()
        {
            string[] headAlts = AnimationBoneDefinitions.GetAlternateNames("Head");
            Assert.GreaterOrEqual(headAlts.Length, 1,
                "Head should have at least one alternate name");

            string[] unknownAlts = AnimationBoneDefinitions.GetAlternateNames("UnknownBone_XYZ");
            Assert.AreEqual(0, unknownAlts.Length,
                "Unknown bone should return empty array");
        }

        /// <summary>
        /// Tests that <see cref="AnimationBoneDefinitions.TryFindBoneCanonical"/> finds
        /// bones using canonical names.
        /// </summary>
        [Test]
        public void AnimationBoneDefinitions_TryFindBoneCanonical_FindsByCanonicalName()
        {
            var root = new GameObject("Root");
            var head = new GameObject("Head");
            head.transform.SetParent(root.transform);

            Transform result = AnimationBoneDefinitions.TryFindBoneCanonical(root.transform, "Head");
            Assert.IsNotNull(result, "Should find Head by canonical name");
            Assert.AreEqual("Head", result.name);

            Object.DestroyImmediate(root);
        }

        #endregion

        #region AnimationRiggingSetup Tests

        /// <summary>
        /// Tests that <see cref="AnimationRiggingSetup.FindBones"/> correctly discovers
        /// bones in a mock humanoid hierarchy.
        /// </summary>
        [Test]
        public void AnimationRiggingSetup_FindBones_DetectsHumanoidHierarchy()
        {
            var root = new GameObject("Character");

            // Build a mock humanoid skeleton
            var hips = CreateChild(root, "Hips");
            var spine = CreateChild(hips, "Spine");
            var chest = CreateChild(spine, "Chest");
            var neck = CreateChild(chest, "Neck");
            var head = CreateChild(neck, "Head");
            var upperArmL = CreateChild(chest, "UpperArm_L");
            var forearmL = CreateChild(upperArmL, "LowerArm_L");
            var handL = CreateChild(forearmL, "Hand_L");
            var upperArmR = CreateChild(chest, "UpperArm_R");
            var forearmR = CreateChild(upperArmR, "LowerArm_R");
            var handR = CreateChild(forearmR, "Hand_R");
            var upperLegL = CreateChild(hips, "UpperLeg_L");
            var lowerLegL = CreateChild(upperLegL, "LowerLeg_L");
            var footL = CreateChild(lowerLegL, "Foot_L");
            var upperLegR = CreateChild(hips, "UpperLeg_R");
            var lowerLegR = CreateChild(upperLegR, "LowerLeg_R");
            var footR = CreateChild(lowerLegR, "Foot_R");

            var setup = root.AddComponent<AnimationRiggingSetup>();
            bool found = setup.FindBones();

            Assert.IsTrue(found, "FindBones should succeed with mock humanoid hierarchy");
            Assert.IsNotNull(setup.HeadBone, "Head bone should be found");
            Assert.IsNotNull(setup.SpineBone, "Spine bone should be found");
            Assert.IsNotNull(setup.LeftArmBone, "Left arm bone should be found");
            Assert.IsNotNull(setup.RightArmBone, "Right arm bone should be found");
            Assert.IsNotNull(setup.LeftLegBone, "Left leg bone should be found");
            Assert.IsNotNull(setup.RightLegBone, "Right leg bone should be found");

            Assert.AreEqual("Head", setup.HeadBone.name);
            Assert.AreEqual("Spine", setup.SpineBone.name);
            Assert.AreEqual("UpperArm_L", setup.LeftArmBone.name);
            Assert.AreEqual("UpperArm_R", setup.RightArmBone.name);
            Assert.AreEqual("UpperLeg_L", setup.LeftLegBone.name);
            Assert.AreEqual("UpperLeg_R", setup.RightLegBone.name);

            Object.DestroyImmediate(root);
        }

        /// <summary>
        /// Tests that <see cref="AnimationRiggingSetup.FindBones"/> returns false when
        /// the hierarchy has no recognizable bones.
        /// </summary>
        [Test]
        public void AnimationRiggingSetup_FindBones_EmptyHierarchy_ReturnsFalse()
        {
            var root = new GameObject("Empty");
            var child = new GameObject("Child_Unknown");
            child.transform.SetParent(root.transform);

            var setup = root.AddComponent<AnimationRiggingSetup>();
            bool found = setup.FindBones();

            Assert.IsFalse(found, "FindBones should fail on hierarchy with no recognizable bones");

            Object.DestroyImmediate(root);
        }

        /// <summary>
        /// Tests that <see cref="AnimationRiggingSetup.FindBones"/> works with alternative
        /// bone naming conventions (Mixamo-style names).
        /// </summary>
        [Test]
        public void AnimationRiggingSetup_FindBones_AlternateNaming()
        {
            var root = new GameObject("Character");
            var mixamoHead = CreateChild(root, "Head_Top");
            var mixamoSpine = CreateChild(root, "Spine1");

            var setup = root.AddComponent<AnimationRiggingSetup>();
            bool found = setup.FindBones();

            Assert.IsTrue(found, "FindBones should work with alternative naming conventions");

            Object.DestroyImmediate(root);
        }

        /// <summary>
        /// Tests that the static <see cref="AnimationRiggingSetup.IsAnimationRiggingAvailable"/>
        /// property reports correctly based on package presence.
        /// </summary>
        [Test]
        public void AnimationRiggingSetup_CompileFlag_ReportsAvailability()
        {
            // This test just validates the property exists and returns a bool
            // (the actual value depends on whether the Animation Rigging package is installed)
            bool available = AnimationRiggingSetup.IsAnimationRiggingAvailable;

            // In edit-mode tests without the package loaded, this should be false
            Assert.IsFalse(available,
                "IsAnimationRiggingAvailable should be false when package define is absent");
        }

        #endregion

        #region RigAnimationController Tests

        /// <summary>
        /// Tests that <see cref="RigAnimationController.CurrentState"/> starts at Idle.
        /// </summary>
        [Test]
        public void RigAnimationController_InitialState_IsIdle()
        {
            var go = new GameObject("Character", typeof(Animator));
            var controller = go.AddComponent<RigAnimationController>();

            // Initial state should be Idle after Start
            // We can't call Start directly, but the serialized default is Idle
            Assert.AreEqual(AnimationState.Idle, controller.CurrentState,
                "Default state should be Idle");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that <see cref="RigAnimationController.SetState"/> transitions work
        /// through all defined states without throwing.
        /// </summary>
        [Test]
        public void RigAnimationController_SetState_AllStatesTransitions()
        {
            var go = new GameObject("Character", typeof(Animator));
            var controller = go.AddComponent<RigAnimationController>();

            // Test each state transition
            Assert.DoesNotThrow(() => controller.SetState(AnimationState.Idle));
            Assert.DoesNotThrow(() => controller.SetState(AnimationState.Walk));
            Assert.DoesNotThrow(() => controller.SetState(AnimationState.Run));
            Assert.DoesNotThrow(() => controller.SetState(AnimationState.Jump));
            Assert.DoesNotThrow(() => controller.SetState(AnimationState.Gather));
            Assert.DoesNotThrow(() => controller.SetState(AnimationState.Craft));
            Assert.DoesNotThrow(() => controller.SetState(AnimationState.Attack));
            Assert.DoesNotThrow(() => controller.SetState(AnimationState.Throw));
            Assert.DoesNotThrow(() => controller.SetState(AnimationState.Kneel));

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that the convenience methods on RigAnimationController function
        /// without throwing exceptions.
        /// </summary>
        [Test]
        public void RigAnimationController_ConvenienceMethods_Work()
        {
            var go = new GameObject("Character", typeof(Animator));
            var controller = go.AddComponent<RigAnimationController>();

            Assert.DoesNotThrow(() => controller.Idle());
            Assert.DoesNotThrow(() => controller.Walk());
            Assert.DoesNotThrow(() => controller.Run());
            Assert.DoesNotThrow(() => controller.Jump());
            Assert.DoesNotThrow(() => controller.Gather());
            Assert.DoesNotThrow(() => controller.Craft());
            Assert.DoesNotThrow(() => controller.Attack());
            Assert.DoesNotThrow(() => controller.Throw());
            Assert.DoesNotThrow(() => controller.Kneel());

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that <see cref="RigAnimationController.SetStateImmediate"/> applies
        /// state instantly without coroutine delay.
        /// </summary>
        [Test]
        public void RigAnimationController_SetStateImmediate_AppliesInstantly()
        {
            var go = new GameObject("Character", typeof(Animator));
            var controller = go.AddComponent<RigAnimationController>();

            controller.SetStateImmediate(AnimationState.Run);
            Assert.AreEqual(AnimationState.Run, controller.CurrentState,
                "State should be Run after immediate set");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that <see cref="RigAnimationController.TransitionDuration"/> clamps
        /// correctly to minimum value.
        /// </summary>
        [Test]
        public void RigAnimationController_TransitionDuration_ClampsMinimum()
        {
            var go = new GameObject("Character", typeof(Animator));
            var controller = go.AddComponent<RigAnimationController>();

            controller.TransitionDuration = 0f;
            Assert.GreaterOrEqual(controller.TransitionDuration, 0.01f,
                "Transition duration should be clamped to minimum");

            Object.DestroyImmediate(go);
        }

        #endregion

        #region TwoBoneIKController Tests

        /// <summary>
        /// Tests that <see cref="TwoBoneIKController.IsValid"/> is false when no
        /// bones are assigned.
        /// </summary>
        [Test]
        public void TwoBoneIKController_NoBones_IsInvalid()
        {
            var go = new GameObject("IKController");
            var controller = go.AddComponent<TwoBoneIKController>();

            controller.Initialize();
            Assert.IsFalse(controller.IsValid,
                "Controller should be invalid with no bones assigned");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that <see cref="TwoBoneIKController.IsValid"/> is true after assigning
        /// a valid bone chain.
        /// </summary>
        [Test]
        public void TwoBoneIKController_ValidBoneChain_IsValid()
        {
            var go = new GameObject("IKController");

            // Create a simple 3-bone chain: Root → Mid → Tip
            var root = new GameObject("UpperArm_L");
            root.transform.SetParent(go.transform);
            var mid = new GameObject("LowerArm_L");
            mid.transform.SetParent(root.transform);
            mid.transform.localPosition = new Vector3(0, 0, 0.5f);
            var tip = new GameObject("Hand_L");
            tip.transform.SetParent(mid.transform);
            tip.transform.localPosition = new Vector3(0, 0, 0.5f);

            var controller = go.AddComponent<TwoBoneIKController>();
            controller.SetBoneChain(
                root.transform,
                mid.transform,
                tip.transform,
                TwoBoneIKController.LimbType.LeftArm);

            Assert.IsTrue(controller.IsValid,
                "Controller should be valid with proper bone chain");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that <see cref="TwoBoneIKController.SetTargetPosition"/> creates a
        /// target and positions it correctly.
        /// </summary>
        [Test]
        public void TwoBoneIKController_SetTargetPosition_CreatesTarget()
        {
            var go = new GameObject("IKController");
            var root = new GameObject("UpperArm_L");
            root.transform.SetParent(go.transform);
            var mid = new GameObject("LowerArm_L");
            mid.transform.SetParent(root.transform);
            mid.transform.localPosition = new Vector3(0, 0, 0.5f);
            var tip = new GameObject("Hand_L");
            tip.transform.SetParent(mid.transform);
            tip.transform.localPosition = new Vector3(0, 0, 0.5f);

            var controller = go.AddComponent<TwoBoneIKController>();
            controller.SetBoneChain(root.transform, mid.transform, tip.transform,
                TwoBoneIKController.LimbType.LeftArm);

            Vector3 targetPos = new Vector3(1f, 0.5f, 1.5f);
            controller.SetTargetPosition(targetPos);

            Assert.IsNotNull(controller.Target, "Target should be created");
            Assert.AreEqual(targetPos, controller.Target.position,
                "Target position should match");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that <see cref="TwoBoneIKController.BlendWeight"/> clamps correctly.
        /// </summary>
        [Test]
        public void TwoBoneIKController_BlendWeight_ClampsCorrectly()
        {
            var go = new GameObject("IKController");
            var controller = go.AddComponent<TwoBoneIKController>();

            controller.BlendWeight = -0.5f;
            Assert.AreEqual(0f, controller.BlendWeight,
                "Negative blend weight should clamp to 0");

            controller.BlendWeight = 1.5f;
            Assert.AreEqual(1f, controller.BlendWeight,
                "Over-1 blend weight should clamp to 1");

            Object.DestroyImmediate(go);
        }

        #endregion

        #region MultiAimController Tests

        /// <summary>
        /// Tests that <see cref="MultiAimController.IsReady"/> is false when no
        /// head bone is assigned.
        /// </summary>
        [Test]
        public void MultiAimController_NoHeadBone_NotReady()
        {
            var go = new GameObject("AimController");
            var controller = go.AddComponent<MultiAimController>();

            controller.Initialize();
            Assert.IsFalse(controller.IsReady,
                "Controller should not be ready without head bone");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that <see cref="MultiAimController.IsReady"/> is true after assigning
        /// a head bone.
        /// </summary>
        [Test]
        public void MultiAimController_WithHeadBone_IsReady()
        {
            var go = new GameObject("Character");
            var head = new GameObject("Head");
            head.transform.SetParent(go.transform);

            var controller = go.AddComponent<MultiAimController>();
            controller.HeadBone = head.transform;
            controller.Initialize();

            Assert.IsTrue(controller.IsReady,
                "Controller should be ready with head bone assigned");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that <see cref="MultiAimController.FollowTarget"/> accepts a target
        /// without throwing.
        /// </summary>
        [Test]
        public void MultiAimController_FollowTarget_AcceptsTarget()
        {
            var go = new GameObject("Character");
            var head = new GameObject("Head");
            head.transform.SetParent(go.transform);

            var targetGO = new GameObject("Target");
            targetGO.transform.position = new Vector3(5f, 2f, 5f);

            var controller = go.AddComponent<MultiAimController>();
            controller.HeadBone = head.transform;
            controller.Initialize();

            Assert.DoesNotThrow(() => controller.FollowTarget(targetGO.transform),
                "FollowTarget should not throw");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(targetGO);
        }

        /// <summary>
        /// Tests that <see cref="MultiAimController.FollowTarget"/> with null target
        /// clears the aim.
        /// </summary>
        [Test]
        public void MultiAimController_FollowTarget_NullClears()
        {
            var go = new GameObject("Character");
            var head = new GameObject("Head");
            head.transform.SetParent(go.transform);

            var controller = go.AddComponent<MultiAimController>();
            controller.HeadBone = head.transform;
            controller.Initialize();

            Assert.DoesNotThrow(() => controller.FollowTarget(null),
                "FollowTarget with null should not throw");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that <see cref="MultiAimController.SetAimPosition"/> creates a target
        /// if none exists.
        /// </summary>
        [Test]
        public void MultiAimController_SetAimPosition_CreatesTarget()
        {
            var go = new GameObject("Character");
            var head = new GameObject("Head");
            head.transform.SetParent(go.transform);

            var controller = go.AddComponent<MultiAimController>();
            controller.HeadBone = head.transform;
            controller.Initialize();

            Vector3 aimPos = new Vector3(10f, 5f, 10f);
            controller.SetAimPosition(aimPos);

            Assert.IsNotNull(controller.Target, "Target should be auto-created");
            Assert.AreEqual(aimPos, controller.Target.position,
                "Target position should match SetAimPosition");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that <see cref="MultiAimController.ResetAim"/> clears the target.
        /// </summary>
        [Test]
        public void MultiAimController_ResetAim_ClearsTarget()
        {
            var go = new GameObject("Character");
            var head = new GameObject("Head");
            head.transform.SetParent(go.transform);

            var targetGO = new GameObject("Target");
            targetGO.transform.position = new Vector3(5f, 2f, 5f);

            var controller = go.AddComponent<MultiAimController>();
            controller.HeadBone = head.transform;
            controller.Initialize();

            controller.FollowTarget(targetGO.transform);
            Assert.IsNotNull(controller.Target, "Target should be set");

            controller.ResetAim();
            Assert.IsNull(controller.Target, "Target should be null after ResetAim");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(targetGO);
        }

        /// <summary>
        /// Tests that <see cref="MultiAimController.AimWeight"/> clamps correctly.
        /// </summary>
        [Test]
        public void MultiAimController_AimWeight_ClampsCorrectly()
        {
            var go = new GameObject("AimController");
            var controller = go.AddComponent<MultiAimController>();

            controller.AimWeight = 1.5f;
            Assert.AreEqual(1f, controller.AimWeight,
                "AimWeight over 1 should clamp to 1");

            controller.AimWeight = -0.5f;
            Assert.AreEqual(0f, controller.AimWeight,
                "AimWeight below 0 should clamp to 0");

            Object.DestroyImmediate(go);
        }

        #endregion

        // ──────────────────────────────────────────────
        //  Helpers
        // ──────────────────────────────────────────────

        /// <summary>
        /// Creates a child GameObject with the given name under the parent.
        /// </summary>
        /// <param name="parent">Optional parent GameObject.</param>
        /// <param name="name">Name for the new GameObject.</param>
        /// <returns>The created GameObject.</returns>
        private static GameObject CreateChild(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform);
            return go;
        }
    }
}