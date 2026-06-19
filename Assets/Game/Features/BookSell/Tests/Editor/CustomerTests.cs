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
    }
}
