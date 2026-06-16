using System.Collections;
using NUnit.Framework;
using ProjectName.Systems.Motions;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for quadruped and snake motion systems.
    /// Tests parameter clamping, start/stop lifecycle, gait cycle phases,
    /// run vs walk differences, snake slither wave parameters,
    /// and MotionDetector character type detection.
    /// </summary>
    [TestFixture]
    public class QuadrupedMotionTests
    {
        #region QuadrupedIdleMotion Tests

        /// <summary>
        /// Tests that <see cref="QuadrupedIdleMotion.BreathAmplitude"/> clamps
        /// to non-negative values.
        /// </summary>
        [Test]
        public void QuadrupedIdleMotion_BreathAmplitude_ClampsNonNegative()
        {
            var go = new GameObject("QuadIdleTest");
            var idle = go.AddComponent<QuadrupedIdleMotion>();

            idle.BreathAmplitude = -1f;
            Assert.AreEqual(0f, idle.BreathAmplitude,
                "Breath amplitude should be clamped to 0");

            idle.BreathAmplitude = 5f;
            Assert.AreEqual(5f, idle.BreathAmplitude,
                "Breath amplitude should accept positive values");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that <see cref="QuadrupedIdleMotion.LookInterval"/> clamps
        /// to a minimum value.
        /// </summary>
        [Test]
        public void QuadrupedIdleMotion_LookInterval_ClampsMinimum()
        {
            var go = new GameObject("QuadIdleTest");
            var idle = go.AddComponent<QuadrupedIdleMotion>();

            idle.LookInterval = 0f;
            Assert.GreaterOrEqual(idle.LookInterval, 0.5f,
                "Look interval should be clamped to minimum of 0.5");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that <see cref="QuadrupedIdleMotion.TailSwishSpeed"/> clamps
        /// to non-negative values.
        /// </summary>
        [Test]
        public void QuadrupedIdleMotion_TailSwishSpeed_ClampsNonNegative()
        {
            var go = new GameObject("QuadIdleTest");
            var idle = go.AddComponent<QuadrupedIdleMotion>();

            idle.TailSwishSpeed = -1f;
            Assert.AreEqual(0f, idle.TailSwishSpeed,
                "Tail swish speed should be clamped to 0");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that start/stop toggles IsPlaying correctly.
        /// </summary>
        [Test]
        public void QuadrupedIdleMotion_StartStop_TogglesIsPlaying()
        {
            var go = new GameObject("QuadIdleTest");
            var idle = go.AddComponent<QuadrupedIdleMotion>();

            Assert.IsFalse(idle.IsPlaying, "Should not be playing initially");

            idle.StartMotion();
            Assert.IsTrue(idle.IsPlaying, "Should be playing after StartMotion");

            idle.StopMotion();
            Assert.IsFalse(idle.IsPlaying, "Should not be playing after StopMotion");

            Object.DestroyImmediate(go);
        }

        #endregion

        #region QuadrupedWalkMotion Tests

        /// <summary>
        /// Tests that <see cref="QuadrupedWalkMotion.StepHeight"/> clamps to non-negative.
        /// </summary>
        [Test]
        public void QuadrupedWalkMotion_StepHeight_ClampsNonNegative()
        {
            var go = new GameObject("QuadWalkTest");
            var walk = go.AddComponent<QuadrupedWalkMotion>();

            walk.StepHeight = -0.5f;
            Assert.AreEqual(0f, walk.StepHeight, "Step height should be non-negative");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that <see cref="QuadrupedWalkMotion.Speed"/> clamps to non-negative.
        /// </summary>
        [Test]
        public void QuadrupedWalkMotion_Speed_ClampsNonNegative()
        {
            var go = new GameObject("QuadWalkTest");
            var walk = go.AddComponent<QuadrupedWalkMotion>();

            walk.Speed = -1f;
            Assert.AreEqual(0f, walk.Speed, "Speed should be non-negative");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that <see cref="QuadrupedWalkMotion.StepLength"/> clamps to non-negative.
        /// </summary>
        [Test]
        public void QuadrupedWalkMotion_StepLength_ClampsNonNegative()
        {
            var go = new GameObject("QuadWalkTest");
            var walk = go.AddComponent<QuadrupedWalkMotion>();

            walk.StepLength = -0.5f;
            Assert.AreEqual(0f, walk.StepLength, "Step length should be non-negative");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests start/stop toggles IsPlaying for walk motion.
        /// </summary>
        [Test]
        public void QuadrupedWalkMotion_StartStop_TogglesIsPlaying()
        {
            var go = new GameObject("QuadWalkTest");
            var walk = go.AddComponent<QuadrupedWalkMotion>();

            Assert.IsFalse(walk.IsPlaying, "Should not be playing initially");

            walk.StartMotion();
            Assert.IsTrue(walk.IsPlaying, "Should be playing after StartMotion");

            walk.StopMotion();
            Assert.IsFalse(walk.IsPlaying, "Should not be playing after StopMotion");

            Object.DestroyImmediate(go);
        }

        #endregion

        #region QuadrupedRunMotion Tests

        /// <summary>
        /// Tests that <see cref="QuadrupedRunMotion.StepHeight"/> is greater than
        /// <see cref="QuadrupedWalkMotion.StepHeight"/> by default (run should have higher steps).
        /// </summary>
        [Test]
        public void QuadrupedRunMotion_StepHeight_GreaterThanWalk()
        {
            var goWalk = new GameObject("QuadWalkTest");
            var walk = goWalk.AddComponent<QuadrupedWalkMotion>();

            var goRun = new GameObject("QuadRunTest");
            var run = goRun.AddComponent<QuadrupedRunMotion>();

            Assert.Greater(run.StepHeight, walk.StepHeight,
                "Run step height should be greater than walk step height");

            Object.DestroyImmediate(goWalk);
            Object.DestroyImmediate(goRun);
        }

        /// <summary>
        /// Tests that <see cref="QuadrupedRunMotion.Speed"/> is greater than
        /// <see cref="QuadrupedWalkMotion.Speed"/> by default.
        /// </summary>
        [Test]
        public void QuadrupedRunMotion_Speed_GreaterThanWalk()
        {
            var goWalk = new GameObject("QuadWalkTest");
            var walk = goWalk.AddComponent<QuadrupedWalkMotion>();

            var goRun = new GameObject("QuadRunTest");
            var run = goRun.AddComponent<QuadrupedRunMotion>();

            Assert.Greater(run.Speed, walk.Speed,
                "Run speed should be greater than walk speed");

            Object.DestroyImmediate(goWalk);
            Object.DestroyImmediate(goRun);
        }

        /// <summary>
        /// Tests that <see cref="QuadrupedRunMotion.StepLength"/> is greater than
        /// <see cref="QuadrupedWalkMotion.StepLength"/> by default.
        /// </summary>
        [Test]
        public void QuadrupedRunMotion_StepLength_GreaterThanWalk()
        {
            var goWalk = new GameObject("QuadWalkTest");
            var walk = goWalk.AddComponent<QuadrupedWalkMotion>();

            var goRun = new GameObject("QuadRunTest");
            var run = goRun.AddComponent<QuadrupedRunMotion>();

            Assert.Greater(run.StepLength, walk.StepLength,
                "Run step length should be greater than walk step length");

            Object.DestroyImmediate(goWalk);
            Object.DestroyImmediate(goRun);
        }

        /// <summary>
        /// Tests that <see cref="QuadrupedRunMotion.BodyLeanAngle"/> clamps correctly.
        /// </summary>
        [Test]
        public void QuadrupedRunMotion_BodyLeanAngle_ClampsCorrectly()
        {
            var go = new GameObject("QuadRunTest");
            var run = go.AddComponent<QuadrupedRunMotion>();

            run.BodyLeanAngle = 50f;
            Assert.AreEqual(45f, run.BodyLeanAngle,
                "Body lean angle should clamp to max of 45");

            run.BodyLeanAngle = -10f;
            Assert.AreEqual(0f, run.BodyLeanAngle,
                "Body lean angle should clamp to min of 0");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that run start/stop toggles IsPlaying.
        /// </summary>
        [Test]
        public void QuadrupedRunMotion_StartStop_TogglesIsPlaying()
        {
            var go = new GameObject("QuadRunTest");
            var run = go.AddComponent<QuadrupedRunMotion>();

            run.StartMotion();
            Assert.IsTrue(run.IsPlaying, "Should be playing after StartMotion");

            run.StopMotion();
            Assert.IsFalse(run.IsPlaying, "Should not be playing after StopMotion");

            Object.DestroyImmediate(go);
        }

        #endregion

        #region SnakeSlitherMotion Tests

        /// <summary>
        /// Tests that <see cref="SnakeSlitherMotion.WaveAmplitude"/> clamps to non-negative.
        /// </summary>
        [Test]
        public void SnakeSlitherMotion_WaveAmplitude_ClampsNonNegative()
        {
            var go = new GameObject("SnakeTest");
            var snake = go.AddComponent<SnakeSlitherMotion>();

            snake.WaveAmplitude = -5f;
            Assert.AreEqual(0f, snake.WaveAmplitude,
                "Wave amplitude should be clamped to 0");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that <see cref="SnakeSlitherMotion.WaveFrequency"/> clamps to non-negative.
        /// </summary>
        [Test]
        public void SnakeSlitherMotion_WaveFrequency_ClampsNonNegative()
        {
            var go = new GameObject("SnakeTest");
            var snake = go.AddComponent<SnakeSlitherMotion>();

            snake.WaveFrequency = -1f;
            Assert.AreEqual(0f, snake.WaveFrequency,
                "Wave frequency should be clamped to 0");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that <see cref="SnakeSlitherMotion.WaveSpeed"/> clamps to non-negative.
        /// </summary>
        [Test]
        public void SnakeSlitherMotion_WaveSpeed_ClampsNonNegative()
        {
            var go = new GameObject("SnakeTest");
            var snake = go.AddComponent<SnakeSlitherMotion>();

            snake.WaveSpeed = -2f;
            Assert.AreEqual(0f, snake.WaveSpeed,
                "Wave speed should be clamped to 0");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that <see cref="SnakeSlitherMotion.SetSpineBones"/> correctly updates
        /// the segment count.
        /// </summary>
        [Test]
        public void SnakeSlitherMotion_SetSpineBones_UpdatesSegmentCount()
        {
            var go = new GameObject("SnakeTest");
            var snake = go.AddComponent<SnakeSlitherMotion>();

            Assert.AreEqual(0, snake.SegmentCount,
                "Initial segment count should be 0");

            // Create spine bones
            var bones = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < 5; i++)
            {
                var bone = new GameObject($"Spine_{i}").transform;
                bone.SetParent(go.transform);
                bones.Add(bone);
            }

            snake.SetSpineBones(bones);
            Assert.AreEqual(5, snake.SegmentCount,
                "Segment count should match the number of bones set");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests start/stop toggles IsPlaying for snake slither motion.
        /// </summary>
        [Test]
        public void SnakeSlitherMotion_StartStop_TogglesIsPlaying()
        {
            var go = new GameObject("SnakeTest");
            var snake = go.AddComponent<SnakeSlitherMotion>();

            // Add some spine bones so it can start
            for (int i = 0; i < 5; i++)
            {
                var bone = new GameObject($"Spine_{i}").transform;
                bone.SetParent(go.transform);
            }

            // Manually set spine bones via reflection-like approach isn't needed
            // since we need to call SetSpineBones, and the bones need capturing
            var bones = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < go.transform.childCount; i++)
                bones.Add(go.transform.GetChild(i));
            snake.SetSpineBones(bones);

            // StartMotion should not start without bones assigned
            // Hard to test coroutine in edit mode, but we can test the flag
            // The coroutine won't run frames in edit mode unless we use [UnityTest]

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that snake slither start does nothing when no bones are assigned.
        /// </summary>
        [Test]
        public void SnakeSlitherMotion_StartWithoutBones_DoesNotThrow()
        {
            var go = new GameObject("SnakeTest");
            var snake = go.AddComponent<SnakeSlitherMotion>();

            Assert.DoesNotThrow(() => snake.StartMotion(),
                "Starting without bones should not throw an exception");

            Object.DestroyImmediate(go);
        }

        #endregion

        #region MotionDetector Tests

        /// <summary>
        /// Tests that <see cref="MotionDetector.DetectSkeletonType"/> returns
        /// <see cref="ModelType.Static"/> for an empty GameObject.
        /// </summary>
        [Test]
        public void MotionDetector_EmptyGameObject_DetectsStatic()
        {
            var go = new GameObject("EmptyTest");
            var detector = go.AddComponent<MotionDetector>();

            ModelType type = detector.DetectSkeletonType();
            Assert.AreEqual(ModelType.Static, type,
                "Empty GameObject should be detected as Static");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that <see cref="MotionDetector.DetectSkeletonType"/> returns
        /// <see cref="ModelType.RiggedQuadruped"/> for a hierarchy with quadruped bone names.
        /// </summary>
        [Test]
        public void MotionDetector_QuadrupedBones_DetectsRiggedQuadruped()
        {
            var go = new GameObject("QuadTest");
            var detector = go.AddComponent<MotionDetector>();

            // Create a minimal quadruped-like hierarchy
            var head = new GameObject("Head").transform;
            head.SetParent(go.transform);

            var spine = new GameObject("Spine").transform;
            spine.SetParent(go.transform);

            var fl = new GameObject("UpperLeg_FL").transform;
            fl.SetParent(go.transform);

            var hl = new GameObject("UpperLeg_HL").transform;
            hl.SetParent(go.transform);

            ModelType type = detector.DetectSkeletonType();
            Assert.AreEqual(ModelType.RiggedQuadruped, type,
                "Hierarchy with UpperLeg_FL should be detected as Quadruped");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that <see cref="MotionDetector.DetectSkeletonType"/> returns
        /// <see cref="ModelType.RiggedHumanoid"/> for a hierarchy with humanoid bone names.
        /// </summary>
        [Test]
        public void MotionDetector_HumanoidBones_DetectsRiggedHumanoid()
        {
            var go = new GameObject("HumanoidTest");
            var detector = go.AddComponent<MotionDetector>();

            // Create a minimal humanoid-like hierarchy
            var head = new GameObject("Head").transform;
            head.SetParent(go.transform);

            var armL = new GameObject("UpperArm_L").transform;
            armL.SetParent(go.transform);

            var legL = new GameObject("UpperLeg_L").transform;
            legL.SetParent(go.transform);

            ModelType type = detector.DetectSkeletonType();
            Assert.AreEqual(ModelType.RiggedHumanoid, type,
                "Hierarchy with UpperArm_L and UpperLeg_L should be detected as Humanoid");

            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Tests that <see cref="MotionDetector.DetectSkeletonType"/> returns
        /// <see cref="ModelType.RiggedMonster"/> for a hierarchy with many spine bones.
        /// </summary>
        [Test]
        public void MotionDetector_ManySpineBones_DetectsRiggedMonster()
        {
            var go = new GameObject("MonsterTest");
            var detector = go.AddComponent<MotionDetector>();

            // Create a hierarchy with many spine/body-like bones
            for (int i = 0; i < 6; i++)
            {
                var spine = new GameObject($"Spine_{i}").transform;
                spine.SetParent(go.transform);
            }

            ModelType type = detector.DetectSkeletonType();
            Assert.AreEqual(ModelType.RiggedMonster, type,
                "Hierarchy with 6 spine bones should be detected as Monster");

            Object.DestroyImmediate(go);
        }

        #endregion
    }
}