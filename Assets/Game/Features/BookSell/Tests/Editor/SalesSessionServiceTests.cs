using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Book.Sell.Domain;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using Game.Configs.Models;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor
{
    public sealed class SalesSessionServiceTests
    {
        // --- arrange helpers ---

        private static BookConfig Sci(string id, int price = 80) => new()
        {
            Id = id, Genre = "sci-fi", BasePrice = price,
            Tags = new[] { "space", "survival" },
            Mood = new[] { "smart" }
        };

        private static BookConfig Off(string id) => new()
        {
            Id = id, Genre = "romance", BasePrice = 40,
            Tags = new[] { "summer" },
            Mood = new[] { "warm" }
        };

        private static RequestConfig SciReq(string id) => new()
        {
            Id = id, Text = $"request {id}",
            DesiredGenres = new[] { "sci-fi" },
            DesiredTags = new[] { "space", "survival" },
            DesiredMood = new[] { "smart" },
            MaxPrice = 100,
            BaseRewardGold = 25
        };

        private static LocationConfig Uni() => new()
        {
            Id = "loc_uni", DemandGenres = new[] { "sci-fi" }, DemandTags = new[] { "space" }
        };

        private sealed class Harness
        {
            public FakeConfigsService Configs { get; } = new();
            public FakeSalesRandom Random { get; } = new();
            public SalesSessionService Service { get; }

            public List<RequestConfig> ActiveStarted { get; } = new();
            public List<RecommendationResult> Resolved { get; } = new();
            public List<PassiveSaleEvent> Passive { get; } = new();
            public List<SalesDayResult> DayCompleted { get; } = new();

            public Harness(IReadOnlyList<BookConfig> books, IReadOnlyList<RequestConfig> requests, LocationConfig location)
            {
                Configs.SetAll(books);
                Configs.SetAll(requests);
                Configs.SetAll(new[] { location });

                Service = new SalesSessionService(
                    Configs,
                    new DefaultSalesSetupProvider(Configs),
                    new RecommendationScoringService(),
                    new DefaultPassiveSaleSelector(),
                    Random);

                Service.ActiveRequestStarted += r => ActiveStarted.Add(r);
                Service.RecommendationResolved += r => Resolved.Add(r);
                Service.PassiveSaleHappened += e => Passive.Add(e);
                Service.DayCompleted += r => DayCompleted.Add(r);
            }

            public void StartDay(int day = 1)
                => Service.StartDayAsync(day, CancellationToken.None).GetAwaiter().GetResult();
        }

        // --- tests ---

        [Test]
        public void StartDay_EmitsFirstActiveRequest_AndPopulatesShelf()
        {
            var h = new Harness(
                books: new[] { Sci("b1"), Sci("b2"), Sci("b3") },
                requests: new[] { SciReq("r1"), SciReq("r2") },
                location: Uni());

            // no passive sales — the day has not started yet
            h.Random.EnqueueDouble(0.99, 0.99);

            h.StartDay();

            Assert.AreEqual(1, h.ActiveStarted.Count);
            Assert.AreEqual("r1", h.ActiveStarted[0].Id);
            Assert.AreEqual(3, h.Service.State.Shelf.Count);
            Assert.AreEqual(0, h.DayCompleted.Count);
        }

        [Test]
        public void RecommendBook_MatchingBook_EmitsRecommendation_And_AdvancesQueue()
        {
            var h = new Harness(
                new[] { Sci("b1") },
                new[] { SciReq("r1"), SciReq("r2") },
                Uni());

            // both ticks — no passive sales
            h.Random.EnqueueDouble(0.99, 0.99);

            h.StartDay();
            h.Service.RecommendBook("b1");

            Assert.AreEqual(1, h.Resolved.Count);
            Assert.AreEqual(RecommendationTier.Excellent, h.Resolved[0].Tier);
            Assert.AreEqual(ShelfBookState.SoldOut, h.Service.State.Shelf[0].State);
            Assert.Greater(h.Service.AccumulatedResult.GoldEarned, 0);
            // advance to next request
            Assert.AreEqual(2, h.ActiveStarted.Count);
            Assert.AreEqual("r2", h.ActiveStarted[1].Id);
        }

        [Test]
        public void RecommendBook_AlreadySoldOut_IsNoOp()
        {
            var h = new Harness(
                new[] { Sci("b1") },
                new[] { SciReq("r1"), SciReq("r2") },
                Uni());

            h.Random.EnqueueDouble(0.99, 0.99, 0.99, 0.99);

            h.StartDay();
            h.Service.RecommendBook("b1");
            var resolvedBefore = h.Resolved.Count;

            // second attempt — ignored
            h.Service.RecommendBook("b1");

            Assert.AreEqual(resolvedBefore, h.Resolved.Count, "A sold-out book must not resolve again.");
        }

        [Test]
        public void SkipCurrentRequest_EmitsSkippedTier_NoGoldNoSale()
        {
            var h = new Harness(
                new[] { Sci("b1") },
                new[] { SciReq("r1"), SciReq("r2") },
                Uni());

            h.Random.EnqueueDouble(0.99, 0.99);

            h.StartDay();
            h.Service.SkipCurrentRequest();

            Assert.AreEqual(1, h.Resolved.Count);
            Assert.AreEqual(RecommendationTier.Skipped, h.Resolved[0].Tier);
            Assert.IsNull(h.Resolved[0].BookId);
            Assert.AreEqual(0, h.Service.AccumulatedResult.GoldEarned);
            Assert.AreEqual(1, h.Service.AccumulatedResult.SkippedCount);
            Assert.AreEqual(ShelfBookState.Available, h.Service.State.Shelf[0].State, "Skip does not touch the shelf.");
        }

        [Test]
        public void PassiveSales_BothThresholdsTriggered_AndShelfHasMatch_FiresTwo()
        {
            // 3 matching books, 3 requests (enough for the whole tick)
            var h = new Harness(
                new[] { Sci("b1"), Sci("b2"), Sci("b3") },
                new[] { SciReq("r1"), SciReq("r2"), SciReq("r3") },
                Uni());

            // First tick: NextDouble #1 = 0.5 < 0.6 (attempt #1 OK), NextDouble #2 = 0.3 < 0.4 (attempt #2 OK).
            // Range returns the first candidate regardless.
            h.Random.EnqueueDouble(0.5, 0.3, /* following ticks unused */ 0.99, 0.99, 0.99);

            h.StartDay();
            h.Service.RecommendBook("b1"); // b1 sold via active + 2 passive sales fire

            Assert.AreEqual(2, h.Passive.Count, "Two passing thresholds with candidates available -> exactly 2 passive sales.");
        }

        [Test]
        public void PassiveSales_FirstThresholdFails_FiresZero()
        {
            var h = new Harness(
                new[] { Sci("b1"), Sci("b2") },
                new[] { SciReq("r1"), SciReq("r2") },
                Uni());

            // NextDouble #1 = 0.7 >= 0.6 -> attempt #1 does not even start
            h.Random.EnqueueDouble(0.7, 0.7);

            h.StartDay();
            h.Service.RecommendBook("b1");

            Assert.AreEqual(0, h.Passive.Count);
        }

        [Test]
        public void PassiveSales_FirstSucceeds_SecondThresholdFails_FiresOne()
        {
            var h = new Harness(
                new[] { Sci("b1"), Sci("b2") },
                new[] { SciReq("r1"), SciReq("r2") },
                Uni());

            // attempt #1: 0.3 < 0.6 -> start, success; attempt #2: 0.7 >= 0.4 -> do not start
            h.Random.EnqueueDouble(0.3, 0.7, 0.99, 0.99);

            h.StartDay();
            h.Service.RecommendBook("b1");

            Assert.AreEqual(1, h.Passive.Count);
        }

        [Test]
        public void PassiveSales_NoMatchingBooksOnShelf_FiresZero_EvenIfThresholdsPass()
        {
            // The shelf only has romance — Uni demand does not match.
            var h = new Harness(
                new[] { Off("o1"), Off("o2") },
                new[] { SciReq("r1"), SciReq("r2") },
                Uni());

            // Nothing matches the active request (romance vs sci-fi) -> Skip, and both thresholds "succeed".
            h.Random.EnqueueDouble(0.0, 0.0, 0.99, 0.99);

            h.StartDay();
            h.Service.SkipCurrentRequest();

            Assert.AreEqual(0, h.Passive.Count, "Without a matching shelf book passive sales must not fire even with lucky thresholds.");
        }

        [Test]
        public void DayCompleted_FiresOnce_AfterLastActiveResolved()
        {
            // Two requests, skip both -> the day is over.
            var h = new Harness(
                new[] { Sci("b1"), Sci("b2") },
                new[] { SciReq("r1"), SciReq("r2") },
                Uni());

            h.Random.EnqueueDouble(0.99, 0.99, 0.99, 0.99);

            h.StartDay();
            h.Service.SkipCurrentRequest();
            h.Service.SkipCurrentRequest();

            Assert.AreEqual(1, h.DayCompleted.Count);
            Assert.IsTrue(h.Service.State.DayCompleted);
            // After DayCompleted no extra ActiveRequestStarted is emitted past the 2 requests.
            Assert.AreEqual(2, h.ActiveStarted.Count);
        }

        [Test]
        public void ActiveQueue_LimitedTo_DefaultSize()
        {
            // 10 requests in config, but a day takes exactly DefaultActiveQueueSize (5).
            var requests = Enumerable.Range(1, 10).Select(i => SciReq($"r{i}")).ToArray();
            var h = new Harness(
                new[] { Sci("b1"), Sci("b2"), Sci("b3"), Sci("b4"), Sci("b5") },
                requests,
                Uni());

            h.Random.EnqueueDouble(Enumerable.Repeat(0.99, 50).ToArray());

            h.StartDay();
            for (var i = 0; i < SalesSessionService.DefaultActiveQueueSize; i++)
                h.Service.SkipCurrentRequest();

            Assert.AreEqual(SalesSessionService.DefaultActiveQueueSize, h.ActiveStarted.Count);
            Assert.AreEqual(1, h.DayCompleted.Count);
        }

        [Test]
        public void StartDay_NoBooks_NoRequests_CompletesImmediately()
        {
            var h = new Harness(
                books: new BookConfig[0],
                requests: new RequestConfig[0],
                location: Uni());

            h.StartDay();

            Assert.AreEqual(0, h.ActiveStarted.Count);
            Assert.AreEqual(1, h.DayCompleted.Count, "Empty queue -> the day completes immediately.");
        }

        [Test]
        public void EventOrder_ActiveResolved_Before_PassiveFired_Before_NextRequest()
        {
            var h = new Harness(
                new[] { Sci("b1"), Sci("b2") },
                new[] { SciReq("r1"), SciReq("r2") },
                Uni());

            // One passive sale after the first active resolution.
            h.Random.EnqueueDouble(0.3, 0.7, 0.99, 0.99);

            // Event ledger.
            var log = new List<string>();
            h.Service.RecommendationResolved += _ => log.Add("resolved");
            h.Service.PassiveSaleHappened += _ => log.Add("passive");
            h.Service.ActiveRequestStarted += _ => log.Add("started");

            h.StartDay();                  // started r1
            h.Service.RecommendBook("b1"); // resolved -> passive -> started r2

            // The first "started" entry fired inside StartDay.
            CollectionAssert.AreEqual(new[] { "started", "resolved", "passive", "started" }, log);
        }
    }
}
