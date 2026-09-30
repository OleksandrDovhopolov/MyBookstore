using System.Collections.Generic;
using System.Text.RegularExpressions;
using Book.Sell.Domain;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Book.Sell.Tests.Editor.Services
{
    /// <summary>
    /// The selector answers one question: which request does this customer ask for, given what is still on
    /// the shelf. Answerability outranks the customer's own genre profile — an off-profile request the
    /// player can win beats an on-profile one they cannot.
    /// </summary>
    public sealed class ProfileMatchedRequestSelectorTests
    {
        [Test]
        public void Draw_SelectsRequestMatchingProfile()
        {
            var selector = Selector(
                Request("crime", "space", "Crime"),
                Request("fact", "space", "Fact"));

            var draw = selector.Draw(Profile("Fact"), Shelf("space"), new FakeSalesRandom());

            Assert.AreEqual("fact", draw.Id);
        }

        [Test]
        public void Draw_SkipsRequestNothingOnTheShelfCanAnswer()
        {
            var selector = Selector(
                Request("fact", "ruins", "Fact"),      // nothing on the shelf has this quality
                Request("crime", "space", "Crime"));

            var draw = selector.Draw(Profile("Fact"), Shelf("space"), new FakeSalesRandom());

            Assert.AreEqual("crime", draw.Id,
                "An off-profile request the shelf can answer beats an on-profile dead end.");
        }

        [Test]
        public void Draw_IgnoresSoldOutBooks()
        {
            var selector = Selector(Request("fact", "space", "Fact"));

            var shelf = Shelf("space");
            shelf[0].State = ShelfBookState.SoldOut;

            LogAssert.Expect(LogType.Warning, new Regex("no remaining request can be answered"));
            var draw = selector.Draw(Profile("Fact"), shelf, new FakeSalesRandom());

            Assert.AreEqual("fact", draw.Id, "Issued anyway, but the warning must fire — the answer was sold.");
        }

        [Test]
        public void Draw_DoesNotRepeatWhileRemainingRequestsExist()
        {
            var selector = Selector(
                Request("fact_1", "space", "Fact"),
                Request("fact_2", "space", "Fact"));

            var first = selector.Draw(Profile("Fact"), Shelf("space"), new FakeSalesRandom());
            var second = selector.Draw(Profile("Fact"), Shelf("space"), new FakeSalesRandom());

            Assert.AreNotEqual(first.Id, second.Id);
        }

        [Test]
        public void Draw_WarnsAndIssuesAnyway_WhenNothingIsAnswerable()
        {
            var selector = Selector(Request("fact", "ruins", "Fact"));

            LogAssert.Expect(LogType.Warning, new Regex("no remaining request can be answered from the current shelf"));
            var draw = selector.Draw(Profile("Fact"), Shelf("space"), new FakeSalesRandom());

            Assert.AreEqual("fact", draw.Id);
        }

        [Test]
        public void Draw_FallbacksToFullPool_WhenRemainingIsEmpty()
        {
            var selector = Selector(Request("fact", "space", "Fact"));
            selector.Draw(Profile("Fact"), Shelf("space"), new FakeSalesRandom());

            LogAssert.Expect(LogType.Warning, new Regex("all active requests were already used"));
            var repeated = selector.Draw(Profile("Fact"), Shelf("space"), new FakeSalesRandom());

            Assert.AreEqual("fact", repeated.Id);
        }

        [Test]
        public void Draw_ReturnsNull_WhenPoolIsEmpty()
        {
            var selector = Selector();

            Assert.IsNull(selector.Draw(Profile("Fact"), Shelf("space"), new FakeSalesRandom()));
        }

        [Test]
        public void Draw_ConsumesNoRange_WhenOnlyOneCandidate()
        {
            var random = new CountingRandom();
            var selector = Selector(Request("fact", "space", "Fact"));

            selector.Draw(Profile("Fact"), Shelf("space"), random);

            Assert.AreEqual(0, random.RangeCalls);
        }

        [Test]
        public void Draw_ConsumesOneRange_WhenMultipleCandidates()
        {
            var random = new CountingRandom();
            var selector = Selector(
                Request("fact_1", "space", "Fact"),
                Request("fact_2", "space", "Fact"));

            selector.Draw(Profile("Fact"), Shelf("space"), random);

            Assert.AreEqual(1, random.RangeCalls);
        }

        private static ProfileMatchedRequestSelector Selector(params ActiveRequestRuntime[] requests)
            => new(requests, new BookConditionRequestEvaluator());

        private static ActiveRequestRuntime Request(string id, string quality, params string[] requiredGenres)
            => SalesTestKit.ActiveRequest(id, quality, requiredGenres);

        private static CustomerProfile Profile(params string[] genres)
            => new(genres);

        /// <summary>One shelf book per quality, all available for selection.</summary>
        private static List<ShelfBook> Shelf(params string[] qualities)
        {
            var shelf = new List<ShelfBook>(qualities.Length);
            for (var i = 0; i < qualities.Length; i++)
                shelf.Add(new ShelfBook(SalesTestKit.Book($"b{i + 1}", qualities: new[] { qualities[i] })));
            return shelf;
        }

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
