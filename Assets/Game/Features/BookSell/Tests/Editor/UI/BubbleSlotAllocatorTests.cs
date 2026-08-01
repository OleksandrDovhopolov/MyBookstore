using System;
using System.Collections.Generic;
using Book.Sell.UI.Customer;
using Game.Location.API;
using NUnit.Framework;
using UnityEngine;

namespace Book.Sell.Tests.Editor.UI
{
    public sealed class BubbleSlotAllocatorTests
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
            var slots = CreateSlots(3);
            var allocator = new BubbleSlotAllocator(new StubLocationContext(slots));

            Assert.That(allocator.Acquire("a"), Is.SameAs(slots[0]));
            Assert.That(allocator.Acquire("b"), Is.SameAs(slots[1]));
            Assert.That(allocator.Acquire("c"), Is.SameAs(slots[2]));
        }

        [Test]
        public void Acquire_SameCustomerTwice_ReturnsSameSlot()
        {
            var slots = CreateSlots(2);
            var allocator = new BubbleSlotAllocator(new StubLocationContext(slots));

            var first = allocator.Acquire("customer");
            var second = allocator.Acquire("customer");

            Assert.That(second, Is.SameAs(first));
            Assert.That(allocator.Acquire("other"), Is.SameAs(slots[1]));
        }

        [Test]
        public void Release_FreesOnlyThatSlot_AndDoesNotReflow()
        {
            var slots = CreateSlots(3);
            var allocator = new BubbleSlotAllocator(new StubLocationContext(slots));

            var a = allocator.Acquire("a");
            var b = allocator.Acquire("b");
            var c = allocator.Acquire("c");

            allocator.Release("b");

            Assert.That(allocator.Acquire("a"), Is.SameAs(a));
            Assert.That(allocator.Acquire("c"), Is.SameAs(c));
            Assert.That(allocator.Acquire("d"), Is.SameAs(b));
        }

        [Test]
        public void Acquire_WhenAllSlotsTaken_ReturnsNull()
        {
            var allocator = new BubbleSlotAllocator(new StubLocationContext(CreateSlots(1)));

            Assert.That(allocator.Acquire("a"), Is.Not.Null);
            Assert.That(allocator.Acquire("b"), Is.Null);
        }

        [Test]
        public void Acquire_WithEmptyBubbleSlots_ReturnsNull_AndCapacityIsZero()
        {
            var allocator = new BubbleSlotAllocator(new StubLocationContext(Array.Empty<Transform>()));

            Assert.That(allocator.Capacity, Is.Zero);
            Assert.That(allocator.Acquire("a"), Is.Null);
        }

        [Test]
        public void Acquire_AfterEmptySnapshot_RefreshesWhenSlotsAppear()
        {
            var context = new StubLocationContext(Array.Empty<Transform>());
            var allocator = new BubbleSlotAllocator(context);

            Assert.That(allocator.Capacity, Is.Zero);

            var slots = CreateSlots(1);
            context.BubbleSlots = slots;

            Assert.That(allocator.Acquire("a"), Is.SameAs(slots[0]));
        }

        [Test]
        public void Acquire_SkipsDestroyedSlot()
        {
            var slots = CreateSlots(2);
            UnityEngine.Object.DestroyImmediate(slots[0].gameObject);
            var allocator = new BubbleSlotAllocator(new StubLocationContext(slots));

            Assert.That(allocator.Acquire("a"), Is.SameAs(slots[1]));
        }

        [Test]
        public void Release_UnknownCustomer_DoesNotThrow()
        {
            var allocator = new BubbleSlotAllocator(new StubLocationContext(CreateSlots(1)));

            Assert.DoesNotThrow(() => allocator.Release("missing"));
        }

        private Transform[] CreateSlots(int count)
        {
            var slots = new Transform[count];
            for (var i = 0; i < count; i++)
            {
                var obj = new GameObject($"slot-{i}");
                _objects.Add(obj);
                slots[i] = obj.transform;
            }

            return slots;
        }

        private sealed class StubLocationContext : ILocationContext
        {
            public StubLocationContext(IReadOnlyList<Transform> bubbleSlots)
            {
                BubbleSlots = bubbleSlots ?? Array.Empty<Transform>();
            }

            public bool IsBound => true;
            public string LocationId => "test";
            public Transform CustomerSpawnRoot => null;
            public Transform EntryLeft => null;
            public Transform EntryRight => null;
            public Transform ExitLeft => null;
            public Transform ExitRight => null;
            public Transform ShopApproach => null;
            public IReadOnlyList<Transform> LaneAnchors => Array.Empty<Transform>();
            public IReadOnlyList<Transform> BubbleSlots { get; set; }
        }
    }
}
