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
            BookConfig[] books, RequestConfig[] requests, LocationConfig location, IReadOnlyList<Customer> customers,
            SalesTuning tuning = null)
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
                tuning ?? SalesTestKit.FastTuning());
        }

        private static void StartDay(SalesDayController c)
            => c.StartDayAsync(1, CancellationToken.None).GetAwaiter().GetResult();

        // Drives the day until it stops being Running (i.e. reaches ReadyToClose). The day no longer
        // auto-completes; tests assert at ReadyToClose, then call ConcludeDay() when they need the
        // published result.
        private static void Run(SalesDayController c, int maxTicks = 200)
        {
            for (var i = 0; i < maxTicks && c.Phase == SalesDayPhase.Running; i++) c.Tick(0.1f);
        }

        private static void DriveUntilActive(SalesDayController c, int maxTicks = 50)
        {
            for (var i = 0; i < maxTicks && c.CurrentRequest == null && c.Phase == SalesDayPhase.Running; i++) c.Tick(0.1f);
        }

        // ----- tests -----

        [Test]
        public void StartDay_NoCustomers_BecomesReadyToClose_ThenConcludes()
        {
            var c = Build(
                new[] { SalesTestKit.Book("b1") }, new RequestConfig[0],
                SalesTestKit.Location(), new List<Customer>());

            var readyToClose = false;
            var completed = false;
            c.DayReadyToClose += () => readyToClose = true;
            c.DayCompleted += _ => completed = true;

            StartDay(c);
            Run(c);

            // No customers → day is immediately closable, but does NOT auto-complete.
            Assert.IsTrue(readyToClose);
            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
            Assert.IsFalse(completed, "Day waits for the player to close the shop.");

            c.ConcludeDay();

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

            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
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

            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
            CollectionAssert.AreEqual(new[] { "c1" }, failures);
            Assert.AreEqual(0, c.AccumulatedResult.SalesCount);
        }

        [Test]
        public void PassiveFailure_AbandonsRemainingPassiveSteps_AndLeaves()
        {
            // Empty shelf → every passive attempt misses. The first miss must end the visit, so the
            // second PassivePurchaseStep never runs (one failure, not two).
            var c = Build(
                new BookConfig[0],
                new RequestConfig[0],
                SalesTestKit.Location(),
                new List<Customer>
                {
                    new("c1", new ICustomerStep[]
                    {
                        new ApproachStep(), new PassivePurchaseStep(), new PassivePurchaseStep(), new LeaveStep()
                    })
                });

            var failures = 0;
            c.CustomerPassivePurchaseFailed += _ => failures++;

            StartDay(c);
            Run(c);

            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
            Assert.AreEqual(1, failures, "First passive miss aborts the plan → the second passive never runs.");
            Assert.AreEqual(0, c.AccumulatedResult.SalesCount);
            Assert.AreEqual(1, c.AccumulatedResult.CustomersServed, "The aborting customer still leaves (served).");
        }

        [Test]
        public void PassiveFailure_BeforeActiveRequest_SkipsTheMinigame()
        {
            // Plan: Approach → Passive(miss) → Active → Leave. The passive miss ends the visit before
            // the active step is reached, so the minigame never opens.
            var req = SalesTestKit.Request("reqA");
            var c = Build(
                new BookConfig[0],
                new[] { req },
                SalesTestKit.Location(),
                new List<Customer>
                {
                    new("c1", new ICustomerStep[]
                    {
                        new ApproachStep(), new PassivePurchaseStep(), new ActiveRequestStep(req), new LeaveStep()
                    })
                });

            var activeStarted = 0;
            c.ActiveRequestStarted += _ => activeStarted++;

            StartDay(c);
            Run(c);

            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
            Assert.IsNull(c.CurrentRequest, "No active minigame should ever open.");
            Assert.AreEqual(0, activeStarted, "Passive failure aborts before the active step is entered.");
        }

        [Test]
        public void PassiveSuccess_DoesNotAbort_ContinuesToNextPassive()
        {
            // Guard against over-aborting: a successful passive returns plain Completed, so a second
            // passive step still runs. Two books so the day ends via "all customers done".
            var c = Build(
                new[]
                {
                    SalesTestKit.Book("b1", genre: "sci-fi", price: 80),
                    SalesTestKit.Book("b2", genre: "sci-fi", price: 80)
                },
                new RequestConfig[0],
                SalesTestKit.Location(demandGenres: new[] { "sci-fi" }),
                new List<Customer>
                {
                    new("c1", new ICustomerStep[]
                    {
                        new ApproachStep(), new PassivePurchaseStep(), new PassivePurchaseStep(), new LeaveStep()
                    })
                });

            var passive = 0;
            c.PassiveSaleHappened += _ => passive++;

            StartDay(c);
            Run(c);

            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
            Assert.AreEqual(2, passive, "Both passive steps succeed; success does not end the cycle.");
            Assert.AreEqual(2, c.AccumulatedResult.SalesCount);
        }

        [Test]
        public void CompletePurchase_HappyPath_FiresWithPassiveCount()
        {
            // Three books so two passive sales leave one on the shelf — the day ends via "all customers
            // done" (not "all sold out"), letting the customer reach CompletePurchase with count 2.
            var c = Build(
                new[]
                {
                    SalesTestKit.Book("b1", genre: "sci-fi", price: 80),
                    SalesTestKit.Book("b2", genre: "sci-fi", price: 80),
                    SalesTestKit.Book("b3", genre: "sci-fi", price: 80)
                },
                new RequestConfig[0],
                SalesTestKit.Location(demandGenres: new[] { "sci-fi" }),
                new List<Customer>
                {
                    new("c1", new ICustomerStep[]
                    {
                        new ApproachStep(), new PassivePurchaseStep(), new PassivePurchaseStep(),
                        new CompletePurchaseStep(), new LeaveStep()
                    })
                });

            var completions = new List<int>();
            c.CustomerPurchaseCompleted += (_, count) => completions.Add(count);

            StartDay(c);
            Run(c);

            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
            CollectionAssert.AreEqual(new[] { 2 }, completions, "Completion fires once with the passive count.");
        }

        [Test]
        public void CompletePurchase_AbortWithZeroSales_DoesNotFire()
        {
            // Empty shelf → first passive misses → abort → CompletePurchase skipped (count 0).
            var c = Build(
                new BookConfig[0],
                new RequestConfig[0],
                SalesTestKit.Location(),
                new List<Customer>
                {
                    new("c1", new ICustomerStep[]
                    {
                        new ApproachStep(), new PassivePurchaseStep(),
                        new CompletePurchaseStep(), new LeaveStep()
                    })
                });

            var completions = 0;
            c.CustomerPurchaseCompleted += (_, _) => completions++;

            StartDay(c);
            Run(c);

            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
            Assert.AreEqual(0, completions, "No books bought → completion skipped.");
            Assert.AreEqual(1, c.AccumulatedResult.CustomersServed, "Customer still leaves (served).");
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

            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
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

            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
            Assert.AreEqual(2, soldIds.Count, "Both customers buy.");
            CollectionAssert.AreEquivalent(new[] { "b1", "b2" }, soldIds, "No double-reservation: customers pick distinct books.");
            Assert.IsTrue(c.Shelf.AllSoldOut());
        }

        [Test]
        public void SoldOut_StopsSpawning_FinishesInFlight_ThenReadyToClose()
        {
            // Single book, three customers, spawned one-at-a-time (large SpawnInterval). c1 spawns and
            // buys the only book; once sold out, spawning stops so c2/c3 never appear. c1 must still run
            // its closing steps (LeaveStep → Done) before the day is closable — AllSoldOut no longer ends
            // the day instantly.
            var tuning = SalesTestKit.FastTuning();
            tuning.SpawnInterval = 100f;   // only the first customer spawns within the test's tick budget

            var c = Build(
                new[] { SalesTestKit.Book("b1", genre: "sci-fi") },
                new RequestConfig[0],
                SalesTestKit.Location(demandGenres: new[] { "sci-fi" }),
                new List<Customer> { Passive("c1"), Passive("c2"), Passive("c3") },
                tuning);

            StartDay(c);
            Run(c);

            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase, "Day waits for the in-flight buyer to finish, then is closable.");
            Assert.AreEqual(1, c.AccumulatedResult.SalesCount, "Only one book to sell.");
            Assert.IsTrue(c.Shelf.AllSoldOut());
            Assert.AreEqual(1, c.AccumulatedResult.CustomersServed,
                "Only c1 was served; spawning stopped on sold-out so c2/c3 never appeared.");
        }

        [Test]
        public void LastBookBought_RunsClosingSteps_ThenReadyToClose()
        {
            // Repro of the freeze bug: 1 book, 1 customer with the full closing tail. Buying the last
            // book must NOT end the day before CompletePurchase + Leave run.
            var c = Build(
                new[] { SalesTestKit.Book("b1", genre: "sci-fi", price: 70) },
                new RequestConfig[0],
                SalesTestKit.Location(demandGenres: new[] { "sci-fi" }),
                new List<Customer>
                {
                    new("c1", new ICustomerStep[]
                    {
                        new ApproachStep(), new PassivePurchaseStep(),
                        new CompletePurchaseStep(), new LeaveStep()
                    })
                });

            var completions = new List<int>();
            c.CustomerPurchaseCompleted += (_, count) => completions.Add(count);
            SalesDayResult published = null;
            c.DayCompleted += r => published = r;

            StartDay(c);
            Run(c);

            // Day did NOT auto-complete; the customer finished its plan.
            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
            Assert.IsTrue(c.Shelf.AllSoldOut());
            CollectionAssert.AreEqual(new[] { 1 }, completions, "CompletePurchase ran for the one bought book.");
            Assert.AreEqual(1, c.AccumulatedResult.CustomersServed, "Buyer reached Done, not frozen.");
            Assert.IsNull(published, "Results not published until the player closes the shop.");

            c.ConcludeDay();

            Assert.IsTrue(c.IsDayCompleted);
            Assert.IsNotNull(published);
            Assert.AreEqual(1, published.SalesCount);
            Assert.AreEqual(1, published.CustomersServed);
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
            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
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
