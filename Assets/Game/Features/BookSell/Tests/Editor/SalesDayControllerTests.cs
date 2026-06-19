using System.Collections.Generic;
using System.Threading;
using Book.Sell.API;
using Book.Sell.Domain;
using Book.Sell.Domain.Steps;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using Game.Configs.Models;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor
{
    public sealed class SalesDayControllerTests
    {
        // ----- builders -----

        private static Customer Passive(string id)
            => new(id, new ICustomerStep[] { new ApproachStep(), new PassivePurchaseStep(), new LeaveStep() });

        private static Customer Active(string id, RequestConfig req)
            => new(id, new ICustomerStep[] { new ApproachStep(), new ActiveRequestStep(req), new LeaveStep() });

        private static SalesDayController Build(
            BookConfig[] books, RequestConfig[] requests, LocationConfig location, IReadOnlyList<Customer> customers)
        {
            var configs = new FakeConfigsService();
            configs.SetAll(books);
            configs.SetAll(requests);
            configs.SetAll(new[] { location });

            return new SalesDayController(
                configs,
                new DefaultSalesSetupProvider(configs),
                new RecommendationScoringService(),
                SalesTestKit.AlwaysHitPassiveSelector(),
                new FakeSalesRandom(),
                new StubCustomerSpawner(customers),
                new InteractionLock(),
                SalesTestKit.FastTuning());
        }

        private static void StartDay(SalesDayController c)
            => c.StartDayAsync(1, CancellationToken.None).GetAwaiter().GetResult();

        private static void Run(SalesDayController c, int maxTicks = 200)
        {
            for (var i = 0; i < maxTicks && !c.IsDayCompleted; i++) c.Tick(0.1f);
        }

        private static void DriveUntilActive(SalesDayController c, int maxTicks = 50)
        {
            for (var i = 0; i < maxTicks && c.CurrentRequest == null && !c.IsDayCompleted; i++) c.Tick(0.1f);
        }

        // ----- tests -----

        [Test]
        public void StartDay_NoCustomers_CompletesImmediately()
        {
            var c = Build(
                new[] { SalesTestKit.Book("b1") }, new RequestConfig[0],
                SalesTestKit.Location(), new List<Customer>());

            var completed = false;
            c.DayCompleted += _ => completed = true;

            StartDay(c);

            Assert.IsTrue(completed);
            Assert.IsTrue(c.IsDayCompleted);
        }

        [Test]
        public void SinglePassiveCustomer_BuysOneBook_ThenLeaves()
        {
            // Two books so the day ends via "all customers done" (one book remains),
            // not via "all sold out" — that lets the customer actually reach Done (CustomersServed).
            var c = Build(
                new[]
                {
                    SalesTestKit.Book("b1", genre: "sci-fi", price: 80),
                    SalesTestKit.Book("b2", genre: "sci-fi", price: 80)
                },
                new RequestConfig[0],
                SalesTestKit.Location(demandGenres: new[] { "sci-fi" }),
                new List<Customer> { Passive("c1") });

            var passive = 0;
            c.PassiveSaleHappened += _ => passive++;

            StartDay(c);
            Run(c);

            Assert.IsTrue(c.IsDayCompleted);
            Assert.AreEqual(1, passive, "One PassivePurchaseStep → one book bought.");
            Assert.AreEqual(1, c.AccumulatedResult.SalesCount);
            Assert.AreEqual(80, c.AccumulatedResult.GoldEarned);
            Assert.AreEqual(1, c.AccumulatedResult.CustomersServed);
        }

        [Test]
        public void PassiveMiss_RaisesCustomerPassivePurchaseFailed()
        {
            var c = Build(
                new BookConfig[0],
                new RequestConfig[0],
                SalesTestKit.Location(demandGenres: new[] { "sci-fi" }),
                new List<Customer> { Passive("c1") });

            var failures = new List<string>();
            c.CustomerPassivePurchaseFailed += customer => failures.Add(customer.Id);

            StartDay(c);
            Run(c);

            Assert.IsTrue(c.IsDayCompleted);
            CollectionAssert.AreEqual(new[] { "c1" }, failures);
            Assert.AreEqual(0, c.AccumulatedResult.SalesCount);
        }

        [Test]
        public void ActiveRequest_OnlyOneMinigame_PausesOthers_ThenSequencesFifo()
        {
            var reqA = SalesTestKit.Request("reqA");
            var reqB = SalesTestKit.Request("reqB");
            var c = Build(
                new[] { SalesTestKit.Book("b1") },
                new[] { reqA, reqB },
                SalesTestKit.Location(),
                new List<Customer> { Active("c1", reqA), Active("c2", reqB) });

            var started = new List<string>();
            c.ActiveRequestStarted += r => started.Add(r.Id);

            StartDay(c);
            DriveUntilActive(c);

            Assert.AreEqual(1, started.Count, "Only one minigame opens.");
            Assert.AreEqual("reqA", c.CurrentRequest.Id);

            // Pause: while the lock is held, ticking makes no progress and no second minigame opens.
            for (var i = 0; i < 5; i++) c.Tick(0.1f);
            Assert.AreEqual(1, started.Count, "Second customer stays paused while the first is in the minigame.");

            c.SkipCurrentRequest();
            DriveUntilActive(c);

            Assert.AreEqual(2, started.Count);
            Assert.AreEqual("reqB", started[1], "FIFO: second request opens after the first resolves.");

            c.SkipCurrentRequest();
            Run(c);

            Assert.IsTrue(c.IsDayCompleted);
            CollectionAssert.AreEqual(new[] { "reqA", "reqB" }, started);
            Assert.AreEqual(2, c.AccumulatedResult.SkippedCount);
        }

        [Test]
        public void ReserveContention_TwoPassive_PickDifferentBooks()
        {
            // With the probabilistic selector always firing, each passive customer reserves an
            // available book; the reservation hides it from the second customer's pick.
            var c = Build(
                new[]
                {
                    SalesTestKit.Book("b1", genre: "sci-fi"),
                    SalesTestKit.Book("b2", genre: "romance", tags: new[] { "summer" })
                },
                new RequestConfig[0],
                SalesTestKit.Location(demandGenres: new[] { "sci-fi" }, demandTags: new[] { "space" }),
                new List<Customer> { Passive("c1"), Passive("c2") });

            var soldIds = new List<string>();
            c.PassiveSaleHappened += e => soldIds.Add(e.BookId);

            StartDay(c);
            Run(c);

            Assert.IsTrue(c.IsDayCompleted);
            Assert.AreEqual(2, soldIds.Count, "Both customers buy.");
            CollectionAssert.AreEquivalent(new[] { "b1", "b2" }, soldIds, "No double-reservation: customers pick distinct books.");
            Assert.IsTrue(c.Shelf.AllSoldOut());
        }

        [Test]
        public void DayEnds_WhenBooksSoldOut_EvenWithCustomersRemaining()
        {
            var c = Build(
                new[] { SalesTestKit.Book("b1", genre: "sci-fi") },   // single book
                new RequestConfig[0],
                SalesTestKit.Location(demandGenres: new[] { "sci-fi" }),
                new List<Customer> { Passive("c1"), Passive("c2"), Passive("c3") });

            StartDay(c);
            Run(c);

            Assert.IsTrue(c.IsDayCompleted);
            Assert.AreEqual(1, c.AccumulatedResult.SalesCount, "Only one book to sell.");
            Assert.IsTrue(c.Shelf.AllSoldOut());
        }

        [Test]
        public void RecommendBook_ActiveMinigame_ScoresSellsAndCompletes()
        {
            var reqA = SalesTestKit.Request("reqA");
            var c = Build(
                new[] { SalesTestKit.Book("b1", genre: "sci-fi", price: 80) },
                new[] { reqA },
                SalesTestKit.Location(),
                new List<Customer> { Active("c1", reqA) });

            RecommendationResult resolved = null;
            c.RecommendationResolved += r => resolved = r;

            StartDay(c);
            DriveUntilActive(c);
            Assert.AreEqual("reqA", c.CurrentRequest.Id);

            c.RecommendBook("b1");

            Assert.IsNotNull(resolved);
            Assert.AreEqual(RecommendationTier.Excellent, resolved.Tier);
            Assert.AreEqual("b1", resolved.BookId);
            Assert.AreEqual(ShelfBookState.SoldOut, c.Shelf.Find("b1").State);
            Assert.AreEqual(80 + 25, c.AccumulatedResult.GoldEarned);

            Run(c);
            Assert.IsTrue(c.IsDayCompleted);
        }

        [Test]
        public void RecommendBook_WithNoActiveMinigame_IsIgnored()
        {
            var c = Build(
                new[] { SalesTestKit.Book("b1", genre: "sci-fi") },
                new RequestConfig[0],
                SalesTestKit.Location(demandGenres: new[] { "sci-fi" }),
                new List<Customer> { Passive("c1") });

            StartDay(c);
            // No active minigame is open yet → recommend is a no-op (no exception, no sale via this path).
            c.RecommendBook("b1");

            Assert.AreEqual(0, c.AccumulatedResult.ManualRequests);
        }
    }
}
