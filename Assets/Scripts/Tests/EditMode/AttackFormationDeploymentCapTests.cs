using NUnit.Framework;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    public class AttackFormationDeploymentCapTests
    {
        [SetUp]
        public void SetUp()
        {
            // AttackFormationSystem keeps static cell assignments; start every test from an empty formation.
            AttackFormationSystem.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            AttackFormationSystem.ClearAll();
        }

        [Test]
        public void FormationGridCapacity_MatchesPerLordAttackCap()
        {
            Assert.AreEqual(WarMarchSimulation.MAX_ATTACKING_SOLDIERS_PER_LORD,
                AttackFormationSystem.Columns * AttackFormationSystem.Rows,
                "배치판 칸 수는 영주별 동시 공격 상한(20)과 같아야 한다.");
        }

        // ---- Zero assignment: no soldiers placed on the formation grid -> nothing deploys ----

        [TestCase(0)]
        [TestCase(20)]
        public void ResolveDeployableCount_ZeroAssigned_ReturnsZero(int alreadyDeployed)
        {
            Assert.AreEqual(0, AttackFormationSystem.AssignedCount);
            Assert.AreEqual(0, AttackFormationSystem.ResolveDeployableCount(alreadyDeployed));
        }

        [Test]
        public void ResolveAttackForce_ZeroPlanned_ReturnsZeroWithNoDeployed()
        {
            Assert.AreEqual(0, WarMarchSimulation.ResolveAttackForce(0, 0));
        }

        // ---- Within remaining cap: planned fits inside (20 - alreadyDeployed) ----

        [TestCase(8, 0, 8)]
        [TestCase(8, 15, 5)]
        [TestCase(1, 19, 1)]
        public void ResolveAttackForce_WithinRemainingCap_ReturnsPlanned(int planned, int alreadyDeployed, int expected)
        {
            Assert.AreEqual(expected, WarMarchSimulation.ResolveAttackForce(planned, alreadyDeployed));
        }

        // ---- Overflow clamp: planned exceeds remaining cap -> clamped to remaining cap ----

        [TestCase(30, 0, 20)]
        [TestCase(30, 20, 0)]
        [TestCase(25, 10, 10)]
        public void ResolveAttackForce_OverflowPlanned_ClampsToRemainingCap(int planned, int alreadyDeployed, int expected)
        {
            Assert.AreEqual(expected, WarMarchSimulation.ResolveAttackForce(planned, alreadyDeployed));
        }

        [Test]
        public void ResolveAttackForce_AlreadyDeployedOverCap_ReturnsZero()
        {
            // Defensive: an over-cap deployed count must not produce a negative force.
            Assert.AreEqual(0, WarMarchSimulation.ResolveAttackForce(8, 25));
        }
    }
}
