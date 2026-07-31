using System;
using System.Collections.Generic;
using Game.Location.API;
using Game.Location.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Game.Location.Tests.Editor
{
    public sealed class LocationContextBinderTests
    {
        [Test]
        public void UnboundContext_UsesFallback()
        {
            var fallbackRoot = new GameObject("fallback");
            try
            {
                var fallback = new TestContext(entryLeft: fallbackRoot.transform);
                var binder = new LocationContextBinder(fallback);

                Assert.That(binder.IsBound, Is.False);
                Assert.That(binder.EntryLeft, Is.SameAs(fallbackRoot.transform));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fallbackRoot);
            }
        }

        [Test]
        public void PrimaryWithNullFields_KeepsFallbackFields()
        {
            var fallbackRoot = new GameObject("fallback");
            try
            {
                var binder = new LocationContextBinder(new TestContext(shopApproach: fallbackRoot.transform));
                binder.Bind(new TestContext(isBound: true));

                Assert.That(binder.IsBound, Is.True);
                Assert.That(binder.ShopApproach, Is.SameAs(fallbackRoot.transform));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fallbackRoot);
            }
        }

        [Test]
        public void PrimaryWithPartialFields_OverridesOnlyPresentFields()
        {
            var fallbackRoot = new GameObject("fallback");
            var primaryRoot = new GameObject("primary");
            try
            {
                var binder = new LocationContextBinder(new TestContext(entryLeft: fallbackRoot.transform));
                binder.Bind(new TestContext(isBound: true, locationId: "loc_park", entryLeft: primaryRoot.transform));

                Assert.That(binder.LocationId, Is.EqualTo("loc_park"));
                Assert.That(binder.EntryLeft, Is.SameAs(primaryRoot.transform));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fallbackRoot);
                UnityEngine.Object.DestroyImmediate(primaryRoot);
            }
        }

        [Test]
        public void EmptyPrimaryLaneAnchors_UsesFallbackLaneAnchors()
        {
            var fallbackLane = new GameObject("fallback lane");
            try
            {
                var fallback = new TestContext(laneAnchors: new[] { fallbackLane.transform });
                var binder = new LocationContextBinder(fallback);
                binder.Bind(new TestContext(isBound: true, laneAnchors: Array.Empty<Transform>()));

                Assert.That(binder.LaneAnchors, Has.Count.EqualTo(1));
                Assert.That(binder.LaneAnchors[0], Is.SameAs(fallbackLane.transform));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fallbackLane);
            }
        }

        private sealed class TestContext : ILocationContext
        {
            public TestContext(
                bool isBound = false,
                string locationId = null,
                Transform customerSpawnRoot = null,
                Transform entryLeft = null,
                Transform entryRight = null,
                Transform exitLeft = null,
                Transform exitRight = null,
                Transform shopApproach = null,
                IReadOnlyList<Transform> laneAnchors = null)
            {
                IsBound = isBound;
                LocationId = locationId;
                CustomerSpawnRoot = customerSpawnRoot;
                EntryLeft = entryLeft;
                EntryRight = entryRight;
                ExitLeft = exitLeft;
                ExitRight = exitRight;
                ShopApproach = shopApproach;
                LaneAnchors = laneAnchors ?? Array.Empty<Transform>();
            }

            public bool IsBound { get; }
            public string LocationId { get; }
            public Transform CustomerSpawnRoot { get; }
            public Transform EntryLeft { get; }
            public Transform EntryRight { get; }
            public Transform ExitLeft { get; }
            public Transform ExitRight { get; }
            public Transform ShopApproach { get; }
            public IReadOnlyList<Transform> LaneAnchors { get; }
        }
    }
}
