using System;
using Game.Conditions.API;
using Game.LocationVisits.API;
using Game.LocationVisits.Conditions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.LocationVisits.Tests.Editor
{
    public sealed class LocationVisitsConditionTests
    {
        private sealed class FakeVisits : ILocationVisitsReader, ICurrentLocationProvider, ILocationVisitChangeSource
        {
            public int Visits;
            public string Current;
            public event Action Changed;
            public int GetVisits(string locationId) => Visits;
            public string CurrentLocationId => Current;
            public void RaiseChanged() => Changed?.Invoke();
        }

        // ----- visitLocation -----

        [Test]
        public void VisitLocation_Met_AtOrAboveMin_NotBelow()
        {
            var reader = new FakeVisits { Visits = 3 };
            var cond = new VisitLocationCondition(reader, "far_beach", 3);
            Assert.IsTrue(cond.Evaluate().IsMet);

            reader.Visits = 2;
            Assert.IsFalse(cond.Evaluate().IsMet);
        }

        [Test]
        public void VisitLocationFactory_DefaultsMinToOne()
        {
            var reader = new FakeVisits { Visits = 1 };
            var cond = new VisitLocationConditionFactory(reader)
                .Create(new JObject { ["type"] = "visitLocation", ["locationId"] = "far_beach" });

            Assert.IsTrue(cond.Evaluate().IsMet);  // 1 >= default min 1

            reader.Visits = 0;
            Assert.IsFalse(cond.Evaluate().IsMet); // bare node is not always-true
        }

        [Test]
        public void VisitLocationFactory_Throws_OnMissingLocationId()
        {
            var factory = new VisitLocationConditionFactory(new FakeVisits());
            Assert.Throws<ArgumentException>(
                () => factory.Create(new JObject { ["type"] = "visitLocation", ["min"] = 3 }));
        }

        [Test]
        public void VisitLocationFactory_ForwardsChangeSource()
        {
            var visits = new FakeVisits();
            var factory = new VisitLocationConditionFactory(visits, visits);
            var changed = 0;

            ((IConditionChangeSource)factory).Changed += () => changed++;
            visits.RaiseChanged();

            Assert.AreEqual(1, changed);
        }

        // ----- locationIs -----

        [Test]
        public void LocationIs_Met_WhenCurrentMatches_FalseAfterClear()
        {
            var provider = new FakeVisits { Current = "far_beach" };
            var cond = new LocationIsCondition(provider, "far_beach");
            Assert.IsTrue(cond.Evaluate().IsMet);

            provider.Current = null; // hub-return clear
            Assert.IsFalse(cond.Evaluate().IsMet);
        }

        [Test]
        public void LocationIsFactory_Throws_OnMissingLocationId()
        {
            var factory = new LocationIsConditionFactory(new FakeVisits());
            Assert.Throws<ArgumentException>(
                () => factory.Create(new JObject { ["type"] = "locationIs" }));
        }

        [Test]
        public void LocationIsFactory_ForwardsChangeSource()
        {
            var visits = new FakeVisits();
            var factory = new LocationIsConditionFactory(visits, visits);
            var changed = 0;

            ((IConditionChangeSource)factory).Changed += () => changed++;
            visits.RaiseChanged();

            Assert.AreEqual(1, changed);
        }
    }
}
