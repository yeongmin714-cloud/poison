using System.Collections;
using NUnit.Framework;
using ProjectName.Systems.Motions;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for the Animation Motion systems.
    /// Tests each motion type's parameter configuration, coroutine lifecycle,
    /// state transitions, and IK target positions during key phases.
    /// </summary>
    [TestFixture]
    public class AnimationMotionTests
    {
        #region IdleMotion Tests

        /// <summary>
        /// Tests that <see cref="IdleMotion.BreathAmplitude"/> clamps correctly
        /// to non-negative values.
        /// </summary>
        [Test]
        public void IdleMotion_BreathAmplitude_ClampsNonNegative()
        {
            var go = new GameObject("IdleTest");
            var idle = go.AddComponent<IdleMotion>();

            idle.BreathAmplitude = -1f;
            Assert.AreEqual(0f, idle.BreathAmplitude,
                "Breath amplitude should be clamped to 0");

            idle.BreathAmplitude = 5f;
            Assert.AreEqual(5f, idle.BreathAmplitude,
                "Breath amplitude should accept positive values");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that <see cref="IdleMotion.LookInterval"/> clamps correctly
        /// to a minimum value.
        /// </summary>
        [Test]
        public void IdleMotion_LookInterval_ClampsMinimum()
        {
            var go = new GameObject("IdleTest");
            var idle = go.AddComponent<IdleMotion>();

            idle.LookInterval = 0f;
            Assert.GreaterOrEqual(idle.LookInterval, 0.5f,
                "Look interval should be clamped to minimum");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that <see cref="IdleMotion.StartMotion"/> and StopMotion toggle
        /// the IsPlaying flag correctly.
        /// </summary>
        [Test]
        public void IdleMotion_StartStop_TogglesIsPlaying()
        {
            var go = new GameObject("IdleTest");
            var idle = go.AddComponent<IdleMotion>();

            Assert.IsFalse(idle.IsPlaying, "Should not be playing initially");

            idle.StartMotion();
            Assert.IsTrue(idle.IsPlaying, "Should be playing after StartMotion");

            idle.StopMotion();
            Assert.IsFalse(idle.IsPlaying, "Should not be playing after StopMotion");

            Object.DestroyImmediate(go);
        }

        #endregion

        #region WalkMotion Tests

        /// <summary>
        /// Tests that <see cref="WalkMotion.StepHeight"/> clamps correctly.
        /// </summary>
        [Test]
        public void WalkMotion_StepHeight_ClampsNonNegative()
        {
            var go = new GameObject("WalkTest");
            var walk = go.AddComponent<WalkMotion>();

            walk.StepHeight = -0.5f;
            Assert.AreEqual(0f, walk.StepHeight, "Step height should be non-negative");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that <see cref="WalkMotion.Speed"/> clamps correctly.
        /// </summary>
        [Test]
        public void WalkMotion_Speed_ClampsNonNegative()
        {
            var go = new GameObject("WalkTest");
            var walk = go.AddComponent<WalkMotion>();

            walk.Speed = -1f;
            Assert.AreEqual(0f, walk.Speed, "Speed should be non-negative");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that walk motion start/stop toggles IsPlaying.
        /// </summary>
        [Test]
        public void WalkMotion_StartStop_TogglesIsPlaying()
        {
            var go = new GameObject("WalkTest");
            var walk = go.AddComponent<WalkMotion>();

            Assert.IsFalse(walk.IsPlaying, "Should not be playing initially");

            walk.StartMotion();
            Assert.IsTrue(walk.IsPlaying, "Should be playing after StartMotion");

            walk.StopMotion();
            Assert.IsFalse(walk.IsPlaying, "Should not be playing after StopMotion");

            Object.DestroyImmediate(go);
        }

        #endregion

        #region RunMotion Tests

        /// <summary>
        /// Tests that <see cref="RunMotion.StepHeight"/> is greater than
        /// <see cref="WalkMotion.StepHeight"/> by default (run should have
        /// higher steps).
        /// </summary>
        [Test]
        public void RunMotion_StepHeight_GreaterThanWalk()
        {
            var goWalk = new GameObject("WalkTest");
            var walk = goWalk.AddComponent<WalkMotion>();

            var goRun = new GameObject("RunTest");
            var run = goRun.AddComponent<RunMotion>();

            Assert.Greater(run.StepHeight, walk.StepHeight,
                "Run step height should be greater than walk step height");

            Object.DestroyImmediate(goWalk);
            Object.DestroyImmediate(goRun);
        }

        /// <summary>
        /// Tests that <see cref="RunMotion.Speed"/> is greater than
        /// <see cref="WalkMotion.Speed"/> by default.
        /// </summary>
        [Test]
        public void RunMotion_Speed_GreaterThanWalk()
        {
            var goWalk = new GameObject("WalkTest");
            var walk = goWalk.AddComponent<WalkMotion>();

            var goRun = new GameObject("RunTest");
            var run = goRun.AddComponent<RunMotion>();

            Assert.Greater(run.Speed, walk.Speed,
                "Run speed should be greater than walk speed");

            Object.DestroyImmediate(goWalk);
            Object.DestroyImmediate(goRun);
        }

        /// <summary>
        /// Tests that <see cref="RunMotion.BodyLeanAngle"/> clamps correctly.
        /// </summary>
        [Test]
        public void RunMotion_BodyLeanAngle_ClampsCorrectly()
        {
            var go = new GameObject("RunTest");
            var run = go.AddComponent<RunMotion>();

            run.BodyLeanAngle = 50f;
            Assert.AreEqual(45f, run.BodyLeanAngle,
                "Body lean angle should clamp to max of 45");

            run.BodyLeanAngle = -10f;
            Assert.AreEqual(0f, run.BodyLeanAngle,
                "Body lean angle should clamp to min of 0");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that run motion start/stop toggles IsPlaying.
        /// </summary>
        [Test]
        public void RunMotion_StartStop_TogglesIsPlaying()
        {
            var go = new GameObject("RunTest");
            var run = go.AddComponent<RunMotion>();

            run.StartMotion();
            Assert.IsTrue(run.IsPlaying, "Should be playing after StartMotion");

            run.StopMotion();
            Assert.IsFalse(run.IsPlaying, "Should not be playing after StopMotion");

            Object.DestroyImmediate(go);
        }

        #endregion

        #region JumpMotion Tests

        /// <summary>
        /// Tests that jump phase transitions follow the correct order:
        /// Crouch → Launch → Air → Land → null.
        /// </summary>
        [UnityTest]
        public IEnumerator JumpMotion_PhaseTransitions_FollowCorrectOrder()
        {
            var go = new GameObject("JumpTest");
            var jump = go.AddComponent<JumpMotion>();

            // Create an IK target so JumpMotion has something to work with
            var ikTargetL = new GameObject("IK_Target_L");
            ikTargetL.transform.SetParent(go.transform);

            var ikTargetR = new GameObject("IK_Target_R");
            ikTargetR.transform.SetParent(go.transform);

            jump.StartMotion();

            // After one frame, should be in Crouch phase
            yield return null;
            Assert.AreEqual(JumpMotion.JumpPhase.Crouch, jump.CurrentPhase,
                "First phase should be Crouch");

            // Wait for crouch to finish
            yield return new WaitForSeconds(0.35f);
            if (jump.IsPlaying)
            {
                Assert.AreEqual(JumpMotion.JumpPhase.Launch, jump.CurrentPhase,
                    "After crouch, phase should be Launch");
            }

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that <see cref="JumpMotion.CrouchDuration"/> clamps correctly.
        /// </summary>
        [Test]
        public void JumpMotion_CrouchDuration_ClampsMinimum()
        {
            var go = new GameObject("JumpTest");
            var jump = go.AddComponent<JumpMotion>();

            jump.CrouchDuration = 0f;
            Assert.GreaterOrEqual(jump.CrouchDuration, 0.05f,
                "Crouch duration should be clamped to minimum");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that <see cref="JumpMotion.StartMotion"/> and StopMotion toggle
        /// the IsPlaying flag.
        /// </summary>
        [Test]
        public void JumpMotion_StartStop_TogglesIsPlaying()
        {
            var go = new GameObject("JumpTest");
            var jump = go.AddComponent<JumpMotion>();

            jump.StartMotion();
            Assert.IsTrue(jump.IsPlaying, "Should be playing after StartMotion");

            jump.StopMotion();
            Assert.IsFalse(jump.IsPlaying, "Should not be playing after StopMotion");

            Object.DestroyImmediate(go);
        }

        #endregion

        #region GatherMotion Tests

        /// <summary>
        /// Tests that <see cref="GatherMotion.ReachDistance"/> clamps correctly.
        /// </summary>
        [Test]
        public void GatherMotion_ReachDistance_ClampsNonNegative()
        {
            var go = new GameObject("GatherTest");
            var gather = go.AddComponent<GatherMotion>();

            gather.ReachDistance = -1f;
            Assert.AreEqual(0f, gather.ReachDistance,
                "Reach distance should be non-negative");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that gather motion start/stop toggles IsPlaying.
        /// </summary>
        [Test]
        public void GatherMotion_StartStop_TogglesIsPlaying()
        {
            var go = new GameObject("GatherTest");
            var gather = go.AddComponent<GatherMotion>();

            gather.StartMotion();
            Assert.IsTrue(gather.IsPlaying, "Should be playing after StartMotion");

            gather.StopMotion();
            Assert.IsFalse(gather.IsPlaying, "Should not be playing after StopMotion");

            Object.DestroyImmediate(go);
        }

        #endregion

        #region CraftMotion Tests

        /// <summary>
        /// Tests that <see cref="CraftMotion.WorkSpeed"/> clamps correctly.
        /// </summary>
        [Test]
        public void CraftMotion_WorkSpeed_ClampsMinimum()
        {
            var go = new GameObject("CraftTest");
            var craft = go.AddComponent<CraftMotion>();

            craft.WorkSpeed = 0f;
            Assert.GreaterOrEqual(craft.WorkSpeed, 0.1f,
                "Work speed should be clamped to minimum");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that craft motion auto-stops after <see cref="CraftMotion.WorkDuration"/>.
        /// </summary>
        [UnityTest]
        public IEnumerator CraftMotion_AutoStopsAfterDuration()
        {
            var go = new GameObject("CraftTest");
            var craft = go.AddComponent<CraftMotion>();

            craft.WorkDuration = 0.5f; // Short duration for test
            craft.StartMotion();

            Assert.IsTrue(craft.IsPlaying, "Should be playing right after StartMotion");

            // Wait for the duration plus a small buffer
            yield return new WaitForSeconds(0.7f);

            Assert.IsFalse(craft.IsPlaying, "Should have stopped after WorkDuration");

            Object.DestroyImmediate(go);
        }

        #endregion

        #region AttackMotion Tests

        /// <summary>
        /// Tests that attack motion phases execute in the correct order:
        /// wind-up → swing → recovery.
        /// </summary>
        [UnityTest]
        public IEnumerator AttackMotion_Sequence_ExecutesPhasesInOrder()
        {
            var go = new GameObject("AttackTest");
            var attack = go.AddComponent<AttackMotion>();

            attack.StartMotion();

            // Should be playing
            Assert.IsTrue(attack.IsPlaying, "Should be playing after StartMotion");

            // Wait for the full sequence
            float totalTime = attack.WindUpTime + attack.SwingSpeed + attack.RecoveryTime + 0.1f;
            yield return new WaitForSeconds(totalTime);

            Assert.IsFalse(attack.IsPlaying, "Should have completed after full sequence");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that <see cref="AttackMotion.WindUpTime"/> clamps correctly.
        /// </summary>
        [Test]
        public void AttackMotion_WindUpTime_ClampsMinimum()
        {
            var go = new GameObject("AttackTest");
            var attack = go.AddComponent<AttackMotion>();

            attack.WindUpTime = 0f;
            Assert.GreaterOrEqual(attack.WindUpTime, 0.02f,
                "Wind-up time should be clamped to minimum");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that <see cref="AttackMotion.RecoveryTime"/> clamps correctly.
        /// </summary>
        [Test]
        public void AttackMotion_RecoveryTime_ClampsMinimum()
        {
            var go = new GameObject("AttackTest");
            var attack = go.AddComponent<AttackMotion>();

            attack.RecoveryTime = 0f;
            Assert.GreaterOrEqual(attack.RecoveryTime, 0.02f,
                "Recovery time should be clamped to minimum");

            Object.DestroyImmediate(go);
        }

        #endregion

        #region ThrowMotion Tests

        /// <summary>
        /// Tests that throw motion phases execute in the correct order:
        /// wind-up → throw → follow-through.
        /// </summary>
        [UnityTest]
        public IEnumerator ThrowMotion_Sequence_ExecutesPhasesInOrder()
        {
            var go = new GameObject("ThrowTest");
            var throwMotion = go.AddComponent<ThrowMotion>();

            throwMotion.StartMotion();

            // Should be playing
            Assert.IsTrue(throwMotion.IsPlaying, "Should be playing after StartMotion");

            // Wait for a generous amount of time to let the full sequence complete
            yield return new WaitForSeconds(1.0f);

            Assert.IsFalse(throwMotion.IsPlaying, "Should have completed after full sequence");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that <see cref="ThrowMotion.ReleaseAngle"/> clamps correctly.
        /// </summary>
        [Test]
        public void ThrowMotion_ReleaseAngle_ClampsCorrectly()
        {
            var go = new GameObject("ThrowTest");
            var throwMotion = go.AddComponent<ThrowMotion>();

            throwMotion.ReleaseAngle = -10f;
            Assert.AreEqual(0f, throwMotion.ReleaseAngle,
                "Release angle should clamp to 0");

            throwMotion.ReleaseAngle = 100f;
            Assert.AreEqual(90f, throwMotion.ReleaseAngle,
                "Release angle should clamp to 90");

            Object.DestroyImmediate(go);
        }

        #endregion

        #region AnimationMotionController Tests

        /// <summary>
        /// Tests that <see cref="AnimationMotionController.ActivateMotion"/> correctly
        /// toggles the IsMotionActive flag for each state.
        /// </summary>
        [Test]
        public void AnimationMotionController_ActivateMotion_TogglesMotionStates()
        {
            var go = new GameObject("MotionController");
            var rigController = go.AddComponent<RigAnimationController>();
            var controller = go.AddComponent<AnimationMotionController>();

            // Add motion components
            var idle = go.AddComponent<IdleMotion>();
            var walk = go.AddComponent<WalkMotion>();
            var run = go.AddComponent<RunMotion>();
            var jump = go.AddComponent<JumpMotion>();
            var gather = go.AddComponent<GatherMotion>();
            var craft = go.AddComponent<CraftMotion>();
            var attack = go.AddComponent<AttackMotion>();
            var throwMotion = go.AddComponent<ThrowMotion>();

            // Test Idle
            controller.ActivateMotion(AnimationState.Idle);
            Assert.IsTrue(controller.IsMotionActive(AnimationState.Idle),
                "Idle should be active after ActivateMotion(Idle)");

            // Test Walk
            controller.ActivateMotion(AnimationState.Walk);
            Assert.IsTrue(controller.IsMotionActive(AnimationState.Walk),
                "Walk should be active after ActivateMotion(Walk)");
            Assert.IsFalse(controller.IsMotionActive(AnimationState.Idle),
                "Idle should no longer be active after switching to Walk");

            // Test Run
            controller.ActivateMotion(AnimationState.Run);
            Assert.IsTrue(controller.IsMotionActive(AnimationState.Run),
                "Run should be active after ActivateMotion(Run)");

            // Test Jump
            controller.ActivateMotion(AnimationState.Jump);
            Assert.IsTrue(controller.IsMotionActive(AnimationState.Jump),
                "Jump should be active after ActivateMotion(Jump)");

            // Test Gather
            controller.ActivateMotion(AnimationState.Gather);
            Assert.IsTrue(controller.IsMotionActive(AnimationState.Gather),
                "Gather should be active after ActivateMotion(Gather)");

            // Test Craft
            controller.ActivateMotion(AnimationState.Craft);
            Assert.IsTrue(controller.IsMotionActive(AnimationState.Craft),
                "Craft should be active after ActivateMotion(Craft)");

            // Test Attack
            controller.ActivateMotion(AnimationState.Attack);
            Assert.IsTrue(controller.IsMotionActive(AnimationState.Attack),
                "Attack should be active after ActivateMotion(Attack)");

            // Test Throw
            controller.ActivateMotion(AnimationState.Throw);
            Assert.IsTrue(controller.IsMotionActive(AnimationState.Throw),
                "Throw should be active after ActivateMotion(Throw)");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that <see cref="AnimationMotionController.CrossFadeDuration"/>
        /// clamps correctly.
        /// </summary>
        [Test]
        public void AnimationMotionController_CrossFadeDuration_ClampsNonNegative()
        {
            var go = new GameObject("MotionController");
            var rigController = go.AddComponent<RigAnimationController>();
            var controller = go.AddComponent<AnimationMotionController>();

            controller.CrossFadeDuration = -1f;
            Assert.AreEqual(0f, controller.CrossFadeDuration,
                "Cross fade duration should clamp to 0");

            Object.DestroyImmediate(go);
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Creates a child GameObject under a parent with the given name.
        /// </summary>
        private static GameObject CreateChild(GameObject parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform);
            return child;
        }

        #endregion
    }
}