using System.Collections.Generic;
using NUnit.Framework;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    public class WarMarchAttackForceTests
    {
        [Test]
        public void SelectRosterAttackerIds_SkipsInUseAndCapsAtLordLimit()
        {
            var candidates = new List<string>();
            for (int i = 0; i < 25; i++) candidates.Add("G" + i.ToString("D6"));
            var inUse = new HashSet<string> { "G000000", "G000001" };

            List<string> picked = WarMarchSimulation.SelectRosterAttackerIds(candidates, inUse, 27);

            Assert.AreEqual(20, picked.Count);
            CollectionAssert.DoesNotContain(picked, "G000000");
            CollectionAssert.DoesNotContain(picked, "G000001");
            Assert.AreEqual("G000002", picked[0]);
        }

        [Test]
        public void SelectRosterAttackerIds_ZeroRequest_ReturnsEmpty()
        {
            var candidates = new List<string> { "G000001" };
            Assert.AreEqual(0, WarMarchSimulation.SelectRosterAttackerIds(candidates, null, 0).Count);
        }

        [Test]
        public void SelectRosterAttackerIds_NoDoubleCountOfSameId()
        {
            var candidates = new List<string> { "G000001", "G000001", "G000002" };
            List<string> picked = WarMarchSimulation.SelectRosterAttackerIds(candidates, null, 5);
            Assert.AreEqual(2, picked.Count);
        }

        [TestCase(1, 0, 1)]
        [TestCase(5, 0, 5)]
        [TestCase(20, 0, 20)]
        [TestCase(27, 0, 20)]
        [TestCase(10, 15, 5)]
        [TestCase(10, 20, 0)]
        public void ResolveAttackForce_EnforcesTwentyConcurrentSoldiersPerLord(int planned, int alreadyDeployed, int expected)
        {
            Assert.AreEqual(expected, WarMarchSimulation.ResolveAttackForce(planned, alreadyDeployed));
        }
    }
}
