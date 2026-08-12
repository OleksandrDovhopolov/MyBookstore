using System;
using System.Collections.Generic;
using Book.Sell.Domain;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Book.Sell.Tests.Editor.Services
{
    public sealed class ProfileMatchedRequestSelectorTests
    {
        [Test]
        public void Draw_SelectsRequestMatchingProfile()
        {
            var selector = Selector(
                Request("crime", "Crime"),
                Request("fact", "Fact"));

            var draw = selector.Draw(Profile("Fact"), new FakeSalesRandom());

            Assert.AreEqual("fact", draw.Id);
        }

        [Test]
        public void Draw_DoesNotRepeatWhileRemainingRequestsExist()
        {
            var selector = Selector(
                Request("fact_1", "Fact"),
                Request("fact_2", "Fact"));

            var first = selector.Draw(Profile("Fact"), new FakeSalesRandom());
            var second = selector.Draw(Profile("Fact"), new FakeSalesRandom());

            Assert.AreNotEqual(first.Id, second.Id);
        }

        [Test]
        public void Draw_FallbacksToRemaining_WhenNoRemainingRequestMatches()
        {
            var selector = Selector(
                Request("crime", "Crime"),
                Request("travel", "Travel"));

            LogAssert.Expect(LogType.Warning, "[ActiveRequests] no request matches customer profile [Fact]; using any remaining request.");
            var draw = selector.Draw(Profile("Fact"), new FakeSalesRandom());

            Assert.AreEqual("crime", draw.Id);
        }

        [Test]
        public void Draw_FallbacksToFullPool_WhenRemainingIsEmpty()
        {
            var selector = Selector(Request("fact", "Fact"));
            selector.Draw(Profile("Fact"), new FakeSalesRandom());

            LogAssert.Expect(LogType.Warning, "[ActiveRequests] all active requests were already used; repeating from full pool for profile [Fact].");
            var repeated = selector.Draw(Profile("Fact"), new FakeSalesRandom());

            Assert.AreEqual("fact", repeated.Id);
        }

        [Test]
        public void Draw_ReturnsNull_WhenPoolIsEmpty()
        {
            var selector = Selector();

            Assert.IsNull(selector.Draw(Profile("Fact"), new FakeSalesRandom()));
        }

        [Test]
        public void Draw_ConsumesNoRange_WhenOnlyOneCandidate()
        {
            var random = new CountingRandom();
            var selector = Selector(Request("fact", "Fact"));

            selector.Draw(Profile("Fact"), random);

            Assert.AreEqual(0, random.RangeCalls);
        }

        [Test]
        public void Draw_ConsumesOneRange_WhenMultipleCandidates()
        {
            var random = new CountingRandom();
            var selector = Selector(
                Request("fact_1", "Fact"),
                Request("fact_2", "Fact"));

            selector.Draw(Profile("Fact"), random);

            Assert.AreEqual(1, random.RangeCalls);
        }

        private static ProfileMatchedRequestSelector Selector(params ActiveRequestRuntime[] requests)
            => new(requests);

        private static ActiveRequestRuntime Request(string id, params string[] requiredGenres)
            => SalesTestKit.ActiveRequest(id, requiredGenres: requiredGenres);

        private static CustomerProfile Profile(params string[] genres)
            => new(genres);

        private sealed class CountingRandom : ISalesRandom
        {
            public int RangeCalls { get; private set; }

            public int Range(int minInclusive, int maxExclusive)
            {
                RangeCalls++;
                return minInclusive;
            }

            public double NextDouble() => 0d;
        }
    }
}
