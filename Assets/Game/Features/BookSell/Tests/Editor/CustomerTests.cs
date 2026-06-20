using Book.Sell.Domain;
using Book.Sell.Domain.Steps;
using Book.Sell.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor
{
    // Drives a Customer's plan directly (no day controller), so shelf sold-out does not end the run.
    public sealed class CustomerTests
    {
        private static void Drive(Customer customer, CustomerContext ctx, int maxTicks = 50)
        {
            for (var i = 0; i < maxTicks && !customer.IsDone; i++) customer.Tick(ctx, 1f);
        }

        private static SalesTuning FeedbackTuning() => new()
        {
            ApproachDuration = 0f,
            BrowseDuration = 0f,
            PassiveCommitDelay = 0f,
            PassiveFailureFeedbackDuration = 1f,
            CompletePurchaseDuration = 0f,
            LeaveDuration = 0f
        };

        [Test]
        public void PassiveFailureAfterOneSale_RoutesThroughCompletePurchase_WithCountOne()
        {
            // Plan: Approach → Passive → Passive → CompletePurchase → Leave.
            // One book: first passive buys it (count 1), second passive misses (shelf empty) → abort.
            // The abort must resume at CompletePurchase (a closing step), which fires with count 1.
            var sink = new RecordingSink();
            var shelf = SalesTestKit.Shelf(SalesTestKit.Book("b1", genre: "sci-fi"));
            var ctx = SalesTestKit.Context(
                shelf,
                SalesTestKit.Location(demandGenres: new[] { "sci-fi" }),
                sink,
                passiveSelector: SalesTestKit.AlwaysHitPassiveSelector());

            var customer = new Customer("c1", new ICustomerStep[]
            {
                new ApproachStep(), new PassivePurchaseStep(), new PassivePurchaseStep(),
                new CompletePurchaseStep(), new LeaveStep()
            });

            Drive(customer, ctx);

            Assert.IsTrue(customer.IsDone);
            Assert.AreEqual(1, customer.PassivePurchaseCount, "Only the first passive committed.");
            Assert.AreEqual(1, sink.PassiveFailures.Count, "Second passive missed once.");
            Assert.AreEqual(1, sink.PurchaseCompletions.Count, "Completion still runs after the abort.");
            Assert.AreEqual(1, sink.PurchaseCompletions[0].count);
            CollectionAssert.Contains(
                System.Linq.Enumerable.Select(sink.Phases, p => p.phase), CustomerPhase.Leaving);
        }

        [Test]
        public void PassiveFailureAfterOneSale_HoldsFailedFeedback_BeforeCompletePurchase()
        {
            var sink = new RecordingSink();
            var shelf = SalesTestKit.Shelf(SalesTestKit.Book("b1", genre: "sci-fi"));
            var ctx = SalesTestKit.Context(
                shelf,
                SalesTestKit.Location(demandGenres: new[] { "sci-fi" }),
                sink,
                tuning: FeedbackTuning(),
                passiveSelector: SalesTestKit.AlwaysHitPassiveSelector());

            var customer = new Customer("c1", new ICustomerStep[]
            {
                new ApproachStep(), new PassivePurchaseStep(), new PassivePurchaseStep(),
                new CompletePurchaseStep(), new LeaveStep()
            });

            for (var i = 0; i < 10 && sink.PassiveFailures.Count == 0; i++)
                customer.Tick(ctx, 1f);

            Assert.AreEqual(1, customer.PassivePurchaseCount);
            Assert.AreEqual(1, sink.PassiveFailures.Count);
            Assert.IsEmpty(sink.PurchaseCompletions, "Failed must be visible before CompletePurchase can overwrite it.");
            Assert.IsInstanceOf<PassivePurchaseStep>(customer.CurrentStep);

            customer.Tick(ctx, 0.5f);
            Assert.IsEmpty(sink.PurchaseCompletions, "CompletePurchase still waits while Failed is held.");

            customer.Tick(ctx, 0.5f);
            Assert.IsEmpty(sink.PurchaseCompletions, "Completing the Failed hold only routes to the closing step.");

            customer.Tick(ctx, 0f);
            Assert.AreEqual(1, sink.PurchaseCompletions.Count);
            Assert.AreEqual(1, sink.PurchaseCompletions[0].count);
        }

        [Test]
        public void PassiveFailureWithZeroSales_SkipsCompletePurchase_StillLeaves()
        {
            // Empty shelf → first passive misses → abort → CompletePurchase skipped (count 0) → Leave.
            var sink = new RecordingSink();
            var ctx = SalesTestKit.Context(new SalesShelf(), SalesTestKit.Location(), sink);

            var customer = new Customer("c1", new ICustomerStep[]
            {
                new ApproachStep(), new PassivePurchaseStep(),
                new CompletePurchaseStep(), new LeaveStep()
            });

            Drive(customer, ctx);

            Assert.IsTrue(customer.IsDone);
            Assert.AreEqual(0, customer.PassivePurchaseCount);
            Assert.IsEmpty(sink.PurchaseCompletions, "No books bought → completion skipped.");
            CollectionAssert.Contains(
                System.Linq.Enumerable.Select(sink.Phases, p => p.phase), CustomerPhase.Leaving);
        }

        [Test]
        public void PassiveFailureWithZeroSales_HoldsFailedFeedback_BeforeLeaving()
        {
            var sink = new RecordingSink();
            var ctx = SalesTestKit.Context(
                new SalesShelf(),
                SalesTestKit.Location(),
                sink,
                tuning: FeedbackTuning());

            var customer = new Customer("c1", new ICustomerStep[]
            {
                new ApproachStep(), new PassivePurchaseStep(),
                new CompletePurchaseStep(), new LeaveStep()
            });

            for (var i = 0; i < 10 && sink.PassiveFailures.Count == 0; i++)
                customer.Tick(ctx, 1f);

            Assert.AreEqual(1, sink.PassiveFailures.Count);
            Assert.IsEmpty(sink.PurchaseCompletions);
            Assert.AreNotEqual(CustomerPhase.Leaving, customer.Phase, "Customer must not leave before Failed is held.");

            customer.Tick(ctx, 0.5f);
            Assert.AreNotEqual(CustomerPhase.Leaving, customer.Phase);

            customer.Tick(ctx, 0.5f);
            Assert.IsEmpty(sink.PurchaseCompletions, "0 books bought means CompletePurchase remains skipped.");

            customer.Tick(ctx, 0f);
            customer.Tick(ctx, 0f);
            Assert.IsTrue(customer.IsDone);
            CollectionAssert.Contains(
                System.Linq.Enumerable.Select(sink.Phases, p => p.phase), CustomerPhase.Leaving);
        }
    }
}
