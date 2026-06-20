using System.Collections.Generic;
using Book.Sell.Domain;
using Book.Sell.Domain.Steps;
using Book.Sell.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor
{
    /// <summary>
    /// Single-customer passive-purchase scenarios, driven directly through customer.Tick() (no
    /// SalesDayController, so shelf sold-out does not end a "day"). The AlwaysHit selector passes
    /// every per-genre gate, so a purchase happens whenever a book is available — making the
    /// "buys" scenarios deterministic. The empty-shelf scenario exercises the abort path.
    /// </summary>
    public sealed class SingleCustomerPassivePurchaseTests
    {
        // Plan: Approach -> Passive x n -> CompletePurchase -> Leave.
        private static Customer PassiveCustomer(int passiveSteps)
        {
            var steps = new List<ICustomerStep> { new ApproachStep() };
            for (var i = 0; i < passiveSteps; i++) steps.Add(new PassivePurchaseStep());
            steps.Add(new CompletePurchaseStep());
            steps.Add(new LeaveStep());
            return new Customer("c1", steps);
        }

        private static SalesShelf ShelfOf(int books)
        {
            var shelf = new SalesShelf();
            for (var i = 0; i < books; i++)
                shelf.Add(new ShelfBook(SalesTestKit.Book($"b{i + 1}", genre: "sci-fi")));
            return shelf;
        }

        private static void Drive(Customer customer, CustomerContext ctx, int maxTicks = 200)
        {
            for (var i = 0; i < maxTicks && !customer.IsDone; i++) customer.Tick(ctx, 1f);
        }

        // 1) N passive steps, 0 books -> 0 purchases. The first miss aborts the cycle, so only one
        //    attempt runs (not N) and CompletePurchase is skipped; the customer still leaves.
        [TestCase(1)]
        [TestCase(3)]
        [TestCase(5)]
        public void NPassive_ZeroBooks_BuysNothing_AbortsOnFirst(int n)
        {
            var sink = new RecordingSink();
            var ctx = SalesTestKit.Context(new SalesShelf(), SalesTestKit.Location(), sink);
            var customer = PassiveCustomer(n);

            Drive(customer, ctx);

            Assert.IsTrue(customer.IsDone, "Customer leaves.");
            Assert.AreEqual(0, customer.PassivePurchaseCount, "Nothing bought.");
            Assert.AreEqual(1, sink.PassiveFailures.Count, "First miss aborts → remaining passive steps never run.");
            Assert.IsEmpty(sink.PurchaseCompletions, "0 books bought → CompletePurchase skipped.");
        }

        // 2) N passive steps, N books -> every attempt succeeds, shelf fully sold out.
        [TestCase(1)]
        [TestCase(3)]
        [TestCase(5)]
        public void NPassive_NBooks_BuysAll(int n)
        {
            var ctx = SalesTestKit.Context(ShelfOf(n), SalesTestKit.Location(), new RecordingSink(),
                passiveSelector: SalesTestKit.AlwaysHitPassiveSelector());
            var customer = PassiveCustomer(n);

            Drive(customer, ctx);

            Assert.IsTrue(customer.IsDone);
            Assert.AreEqual(n, customer.PassivePurchaseCount);
        }

        // 3) N passive steps, M > N books -> customer stops at its plan length, not at the shelf
        //    (count == N, not M; leftover books remain).
        [TestCase(1, 3)]
        [TestCase(3, 5)]
        [TestCase(2, 10)]
        public void NPassive_MoreBooks_BuysExactlyN(int n, int m)
        {
            var ctx = SalesTestKit.Context(ShelfOf(m), SalesTestKit.Location(), new RecordingSink(),
                passiveSelector: SalesTestKit.AlwaysHitPassiveSelector());
            var customer = PassiveCustomer(n);

            Drive(customer, ctx);

            Assert.IsTrue(customer.IsDone);
            Assert.AreEqual(n, customer.PassivePurchaseCount);
        }
    }
}
