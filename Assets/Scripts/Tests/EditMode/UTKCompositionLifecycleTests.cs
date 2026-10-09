using System.Collections.Generic;
using NUnit.Framework;
using ProjectName.UI.Toolkit;

namespace ProjectName.Tests.EditMode
{
    public class UTKCompositionLifecycleTests
    {
        private sealed class FakePanel : IUTKCompositionPanel
        {
            private readonly string _name;
            private readonly List<string> _events;
            private readonly UTKCompositionLifecycle _owner;
            private readonly IUTKCompositionPanel _removeDuringOpen;

            public bool IsOpen { get; private set; }

            public FakePanel(string name, List<string> events, UTKCompositionLifecycle owner = null,
                IUTKCompositionPanel removeDuringOpen = null)
            {
                _name = name;
                _events = events;
                _owner = owner;
                _removeDuringOpen = removeDuringOpen;
            }

            public void Open()
            {
                IsOpen = true;
                _events.Add("open:" + _name);
                if (_owner != null && _removeDuringOpen != null)
                    _owner.Unregister(_removeDuringOpen);
            }

            public void Close()
            {
                IsOpen = false;
                _events.Add("close:" + _name);
            }
        }

        [Test]
        public void Register_DeduplicatesMembers_AndUnregisterRemovesThem()
        {
            var composition = new UTKCompositionLifecycle();
            var events = new List<string>();
            var panel = new FakePanel("left", events);

            Assert.That(composition.Register(panel), Is.True);
            Assert.That(composition.Register(panel), Is.False);
            Assert.That(composition.Count, Is.EqualTo(1));
            Assert.That(composition.Unregister(panel), Is.True);
            Assert.That(composition.Unregister(panel), Is.False);
            Assert.That(composition.Count, Is.Zero);
        }

        [Test]
        public void OpenAll_OpensEveryRegisteredPanelOnce_AndIsIdempotent()
        {
            var composition = new UTKCompositionLifecycle();
            var events = new List<string>();
            composition.Register(new FakePanel("left", events));
            composition.Register(new FakePanel("center", events));
            composition.Register(new FakePanel("right", events));

            composition.OpenAll();
            composition.OpenAll();

            CollectionAssert.AreEqual(new[] { "open:left", "open:center", "open:right" }, events);
            Assert.That(composition.AreAllOpen, Is.True);
        }

        [Test]
        public void CloseAll_ClosesInReverseOrder_AndPreservesMemberLevelCloseSemantics()
        {
            var composition = new UTKCompositionLifecycle();
            var events = new List<string>();
            var left = new FakePanel("left", events);
            var center = new FakePanel("center", events);
            var right = new FakePanel("right", events);
            composition.Register(left);
            composition.Register(center);
            composition.Register(right);
            composition.OpenAll();
            events.Clear();

            center.Close();
            events.Clear();
            composition.CloseAll();

            CollectionAssert.AreEqual(new[] { "close:right", "close:left" }, events);
            Assert.That(composition.AnyOpen, Is.False);
            Assert.That(composition.AreAllOpen, Is.False);
        }

        [Test]
        public void OpenAll_ReopensOnlyMembersThatWereClosedIndividually()
        {
            var composition = new UTKCompositionLifecycle();
            var events = new List<string>();
            var left = new FakePanel("left", events);
            var center = new FakePanel("center", events);
            composition.Register(left);
            composition.Register(center);
            composition.OpenAll();
            events.Clear();
            center.Close();
            events.Clear();

            composition.OpenAll();

            CollectionAssert.AreEqual(new[] { "open:center" }, events);
            Assert.That(composition.AreAllOpen, Is.True);
        }

        [Test]
        public void OpenAll_UsesSnapshotWhenCallbacksMutateMembership()
        {
            var composition = new UTKCompositionLifecycle();
            var events = new List<string>();
            var removed = new FakePanel("removed", events);
            var mutating = new FakePanel("mutating", events, composition, removed);
            composition.Register(mutating);
            composition.Register(removed);

            composition.OpenAll();

            CollectionAssert.AreEqual(new[] { "open:mutating", "open:removed" }, events);
            Assert.That(composition.Count, Is.EqualTo(1));
        }

        [Test]
        public void ToggleAll_ClosesWhenAnySiblingIsOpenAndThenReopensAll()
        {
            var composition = new UTKCompositionLifecycle();
            var events = new List<string>();
            composition.Register(new FakePanel("left", events));
            composition.Register(new FakePanel("right", events));

            composition.ToggleAll();
            Assert.That(composition.AreAllOpen, Is.True);
            composition.ToggleAll();
            Assert.That(composition.AnyOpen, Is.False);
            composition.ToggleAll();
            Assert.That(composition.AreAllOpen, Is.True);
        }
    }
}
