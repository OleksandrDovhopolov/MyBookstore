using Book.Sell.Domain;
using Book.Sell.Domain.Steps;
using Book.Sell.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor.Steps
{
    public sealed class CompletePurchaseStepTests
    {
        private static Customer CustomerWithPassives(int passiveCount)
        {
            var c = new Customer("c1", new ICustomerStep[] { new CompletePurchaseStep() });
            for (var i = 0; i < passiveCount; i++) c.RegisterPassivePurchase();
            return c;
        }

        [Test]
        public void ZeroPassives_SkipsInstantly_NoCompletionEvent()
        {
            var sink = new RecordingSink();
            var ctx = SalesTestKit.Context(new SalesShelf(), SalesTestKit.Location(), sink);
            var step = new CompletePurchaseStep();
            var self = CustomerWithPassives(0);

            step.Enter(self, ctx);
            Assert.AreEqual(StepStatus.Completed, step.Tick(self, ctx, 1f), "0 passive books → skip immediately.");
            Assert.IsEmpty(sink.PurchaseCompletions);
        }

        [Test]
        public void HasPassives_RaisesCompletion_ThenHoldsForDuration()
        {
            var sink = new RecordingSink();
            var tuning = new SalesTuning { CompletePurchaseDuration = 1f };
            var ctx = SalesTestKit.Context(new SalesShelf(), SalesTestKit.Location(), sink, tuning: tuning);
            var step = new CompletePurchaseStep();
            var self = CustomerWithPassives(3);

            step.Enter(self, ctx);
            Assert.AreEqual(1, sink.PurchaseCompletions.Count, "Completion fires once on enter.");
            Assert.AreSame(self, sink.PurchaseCompletions[0].customer);
            Assert.AreEqual(3, sink.PurchaseCompletions[0].count);

            Assert.AreEqual(StepStatus.Running, step.Tick(self, ctx, 0.5f), "Below duration → still running.");
            Assert.AreEqual(StepStatus.Completed, step.Tick(self, ctx, 0.6f), "Crossing duration → completed.");
        }

        [Test]
        public void DurationOverride_TakesPrecedenceOverTuning()
        {
            var sink = new RecordingSink();
            var tuning = new SalesTuning { CompletePurchaseDuration = 10f };
            var ctx = SalesTestKit.Context(new SalesShelf(), SalesTestKit.Location(), sink, tuning: tuning);
            var step = new CompletePurchaseStep(duration: 1f);
            var self = CustomerWithPassives(1);

            step.Enter(self, ctx);
            Assert.AreEqual(StepStatus.Completed, step.Tick(self, ctx, 1f), "Override (1s) beats tuning (10s).");
        }
    }
}
