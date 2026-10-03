using System;
using System.Collections.Generic;
using Book.Sell.UI.Customer;
using Game.Location.API;
using NUnit.Framework;
using UnityEngine;

namespace Book.Sell.Tests.Editor.UI
{
    /// <summary>
    /// Lane anchors replaced a <c>spawnIndex % laneCount</c> rule that never released anything, so two
    /// live customers could share a spot. These pin the acquire/release contract that fixes it.
    /// </summary>
    public sealed class LaneSlotAllocatorTests
    {
        private readonly List<GameObject> _objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _objects)
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            _objects.Clear();
        }

        [Test]
        public void Acquire_FillsInAuthoredArrayOrder()
        {
            var lanes = CreateLanes(3);
            var allocator = new LaneSlotAllocator(new StubLocationContext(lanes));

            Assert.That(allocator.Acquire("a"), Is.SameAs(lanes[0]));
            Assert.That(allocator.Acquire("b"), Is.SameAs(lanes[1]));
            Assert.That(allocator.Acquire("c"), Is.SameAs(lanes[2]));
        }

        [Test]
        public void Acquire_SameCustomerTwice_ReturnsSameLane()
        {
            var lanes = CreateLanes(2);
            var allocator = new LaneSlotAllocator(new StubLocationContext(lanes));

            var first = allocator.Acquire("customer");

            Assert.That(allocator.Acquire("customer"), Is.SameAs(first));
            Assert.That(allocator.Acquire("other"), Is.SameAs(lanes[1]));
        }

        [Test]
        public void TwoLiveCustomers_NeverShareALane()
        {
            var lanes = CreateLanes(2);
            var allocator = new LaneSlotAllocator(new StubLocationContext(lanes));

            var a = allocator.Acquire("a");
            var b = allocator.Acquire("b");

            Assert.That(b, Is.Not.SameAs(a), "This is the whole point of the allocator.");
        }

        [Test]
        public void Release_FreesTheLaneForTheNextCustomer()
        {
            var lanes = CreateLanes(2);
            var allocator = new LaneSlotAllocator(new StubLocationContext(lanes));

            var a = allocator.Acquire("a");
            allocator.Acquire("b");
            Assert.That(allocator.Acquire("c"), Is.Null, "Both lanes are taken.");

            allocator.Release("a");

            Assert.That(allocator.Acquire("c"), Is.SameAs(a), "The freed lane is reused.");
        }

        [Test]
        public void Acquire_WhenAllLanesTaken_ReturnsNull()
        {
            var allocator = new LaneSlotAllocator(new StubLocationContext(CreateLanes(1)));

            Assert.That(allocator.Acquire("a"), Is.Not.Null);
            Assert.That(allocator.Acquire("b"), Is.Null);
        }

        [Test]
        public void Acquire_WithNoLaneAnchors_ReturnsNull_AndCapacityIsZero()
        {
            var allocator = new LaneSlotAllocator(new StubLocationContext(Array.Empty<Transform>()));

            Assert.That(allocator.Capacity, Is.Zero);
            Assert.That(allocator.Acquire("a"), Is.Null);
        }

        [Test]
        public void Acquire_AfterEmptySnapshot_RefreshesWhenLanesAppear()
        {
            var context = new StubLocationContext(Array.Empty<Transform>());
            var allocator = new LaneSlotAllocator(context);

            Assert.That(allocator.Capacity, Is.Zero);

            var lanes = CreateLanes(1);
            context.LaneAnchors = lanes;

            Assert.That(allocator.Acquire("a"), Is.SameAs(lanes[0]));
        }

        [Test]
        public void Acquire_SkipsDestroyedLane()
        {
            var lanes = CreateLanes(2);
            UnityEngine.Object.DestroyImmediate(lanes[0].gameObject);
            var allocator = new LaneSlotAllocator(new StubLocationContext(lanes));

            Assert.That(allocator.Acquire("a"), Is.SameAs(lanes[1]));
        }

        [Test]
        public void Release_UnknownCustomer_DoesNotThrow()
        {
            var allocator = new LaneSlotAllocator(new StubLocationContext(CreateLanes(1)));

            Assert.DoesNotThrow(() => allocator.Release("missing"));
        }

        private Transform[] CreateLanes(int count)
        {
            var lanes = new Transform[count];
            for (var i = 0; i < count; i++)
            {
                var obj = new GameObject($"lane-{i}");
                _objects.Add(obj);
                lanes[i] = obj.transform;
            }

            return lanes;
        }

        private sealed class StubLocationContext : ILocationContext
        {
            public StubLocationContext(IReadOnlyList<Transform> laneAnchors)
            {
                LaneAnchors = laneAnchors ?? Array.Empty<Transform>();
            }

            public bool IsBound => true;
            public string LocationId => "test";
            public Transform CustomerSpawnRoot => null;
            public Transform EntryLeft => null;
            public Transform EntryRight => null;
            public Transform ExitLeft => null;
            public Transform ExitRight => null;
            public Transform ShopApproach => null;
            public IReadOnlyList<Transform> LaneAnchors { get; set; }
            public IReadOnlyList<Transform> BubbleSlots => Array.Empty<Transform>();
        }
    }
}
