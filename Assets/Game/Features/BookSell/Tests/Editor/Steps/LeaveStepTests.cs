using Book.Sell.Domain;
using Book.Sell.Domain.Steps;
using Book.Sell.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor.Steps
{
    public sealed class LeaveStepTests
    {
        [Test]
        public void Completes_OnlyAfter_LeaveDuration()
        {
            var sink = new RecordingSink();
            var tuning = new SalesTuning { LeaveDuration = 1f };
            var ctx = SalesTestKit.Context(new SalesShelf(), SalesTestKit.Location(), sink, tuning: tuning);
            var step = new LeaveStep();
            var self = new Customer("c1", new[] { step });

            step.Enter(self, ctx);
            Assert.AreEqual(StepStatus.Running, step.Tick(self, ctx, 0.5f), "Below duration → still running.");
            Assert.AreEqual(StepStatus.Completed, step.Tick(self, ctx, 0.6f), "Crossing duration → completed.");
        }

        [Test]
        public void Enter_SetsLeavingPhase()
        {
            var sink = new RecordingSink();
            var ctx = SalesTestKit.Context(new SalesShelf(), SalesTestKit.Location(), sink);
            var step = new LeaveStep();
            var self = new Customer("c1", new[] { step });

            step.Enter(self, ctx);

            CollectionAssert.Contains(
                System.Linq.Enumerable.Select(sink.Phases, p => p.phase),
                CustomerPhase.Leaving);
        }

        [Test]
        public void DurationOverride_TakesPrecedenceOverTuning()
        {
            var sink = new RecordingSink();
            var tuning = new SalesTuning { LeaveDuration = 10f };
            var ctx = SalesTestKit.Context(new SalesShelf(), SalesTestKit.Location(), sink, tuning: tuning);
            var step = new LeaveStep(duration: 1f);
            var self = new Customer("c1", new[] { step });

            step.Enter(self, ctx);
            Assert.AreEqual(StepStatus.Completed, step.Tick(self, ctx, 1f), "Override (1s) beats tuning (10s).");
        }
    }
}
