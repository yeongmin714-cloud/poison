using System.Reflection;
using NUnit.Framework;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    public class AcceptedAttackSequenceTests
    {
        private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

        [Test]
        public void ConsumeAttackSequenceDelta_PreservesEveryDistinctIncrementAcrossPolls()
        {
            var consume = typeof(HumanoidClipDriver).GetMethod("ConsumeAttackSequenceDelta", PrivateStatic);
            Assert.That(consume, Is.Not.Null);
            object[] args = { 2L, 0L };

            Assert.That((long)consume.Invoke(null, args), Is.EqualTo(2L),
                "Two same-frame accepts must remain two events rather than collapsing into one timestamp edge.");
            Assert.That((long)args[1], Is.EqualTo(2L));

            args[0] = 4L;
            Assert.That((long)consume.Invoke(null, args), Is.EqualTo(2L),
                "A later poll must return only the newly accepted events.");
            Assert.That((long)args[1], Is.EqualTo(4L));

            Assert.That((long)consume.Invoke(null, args), Is.Zero,
                "Polling an unchanged sequence must not replay or decrement the same events twice.");
            Assert.That((long)args[1], Is.EqualTo(4L));
        }

        [TestCase(1, 0, true)]
        [TestCase(1, 1, true)]
        [TestCase(1, 2, false)]
        [TestCase(2, 0, true)]
        [TestCase(2, 1, false)]
        [TestCase(3, 0, false)]
        public void HasComboFollowupCapacity_ReservesSlotsForPendingAcceptedInputs(
            int acceptedClicks, int pendingInputs, bool expected)
        {
            var hasCapacity = typeof(HumanoidClipDriver).GetMethod("HasComboFollowupCapacity", PrivateStatic);
            Assert.That(hasCapacity, Is.Not.Null);

            Assert.That((bool)hasCapacity.Invoke(null, new object[] { acceptedClicks, pendingInputs }),
                Is.EqualTo(expected));
        }
    }
}
