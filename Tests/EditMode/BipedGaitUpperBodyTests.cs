using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems.Animation.Procedural;

namespace ProjectName.Tests.EditMode
{
    public class BipedGaitUpperBodyTests
    {
        private const float Eps = 0.0001f;

        [Test]
        public void PelvisWave_IsNeutralAtHalfCycles_AndOppositeAtQuarterCycles()
        {
            Assert.AreEqual(0f, BipedGaitUpperBody.PelvisWave(0f), Eps);
            Assert.AreEqual(0f, BipedGaitUpperBody.PelvisWave(0.5f), Eps);
            Assert.That(BipedGaitUpperBody.PelvisWave(0.25f), Is.GreaterThan(0.999f));
            Assert.That(BipedGaitUpperBody.PelvisWave(0.75f), Is.LessThan(-0.999f));
        }

        [Test]
        public void SpineWave_CounterRollsPelvisAcrossCycle()
        {
            for (int i = 0; i <= 32; i++)
            {
                float phase = i / 32f;
                Assert.That(Mathf.Abs(BipedGaitUpperBody.PelvisWave(phase) + BipedGaitUpperBody.SpineWave(phase)), Is.LessThan(Eps));
                float torsoDegrees = 2.5f * BipedGaitUpperBody.PelvisWave(phase);
                Assert.That(Mathf.Abs(2.5f * BipedGaitUpperBody.SpineWave(phase) + torsoDegrees), Is.LessThan(Eps));
            }
        }

        [Test]
        public void PelvisWave_NormalizesWrappedPhase()
        {
            Assert.That(Mathf.Abs(BipedGaitUpperBody.PelvisWave(1.25f) - BipedGaitUpperBody.PelvisWave(0.25f)), Is.LessThan(Eps));
            Assert.That(Mathf.Abs(BipedGaitUpperBody.PelvisWave(-0.75f) - BipedGaitUpperBody.PelvisWave(0.25f)), Is.LessThan(Eps));
        }
    }
}
