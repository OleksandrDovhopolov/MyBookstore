using Book.Sell.Domain;
using Book.Sell.Domain.Steps;
using Book.Sell.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor.Steps
{
    public sealed class ApproachStepTests
    {
        [Test]
        public void Completes_OnlyAfter_ApproachDuration()
        {
            var sink = new RecordingSink();
            var tuning = new SalesTuning { ApproachDuration = 1f };
            var ctx = SalesTestKit.Context(new SalesShelf(), SalesTestKit.Location(), sink, tuning: tuning);
            var step = new ApproachStep();
            var self = new Customer("c1", new[] { step });

            step.Enter(self, ctx);
            Assert.AreEqual(StepStatus.Running, step.Tick(self, ctx, 0.5f), "Below duration → still running.");
            Assert.AreEqual(StepStatus.Completed, step.Tick(self, ctx, 0.6f), "Crossing duration → completed.");
        }

        [Test]
        public void TryOverrideDuration_BeforeFirstTick_ReplacesTheDuration()
        {
            var sink = new RecordingSink();
            var tuning = new SalesTuning { ApproachDuration = 1f };
            var ctx = SalesTestKit.Context(new SalesShelf(), SalesTestKit.Location(), sink, tuning: tuning);
            var step = new ApproachStep(5f);
            var self = new Customer("c1", new[] { step });

            step.Enter(self, ctx);

            Assert.IsTrue(step.TryOverrideDuration(2f), "No time consumed yet, so the View may push.");
            Assert.AreEqual(2f, step.ResolveDuration(tuning));
            Assert.AreEqual(StepStatus.Running, step.Tick(self, ctx, 1.5f));
            Assert.AreEqual(StepStatus.Completed, step.Tick(self, ctx, 0.6f));
        }

        [Test]
        public void TryOverrideDuration_AfterTicking_IsIgnored()
        {
            var sink = new RecordingSink();
            var tuning = new SalesTuning { ApproachDuration = 1f };
            var ctx = SalesTestKit.Context(new SalesShelf(), SalesTestKit.Location(), sink, tuning: tuning);
            var step = new ApproachStep(4f);
            var self = new Customer("c1", new[] { step });

            step.Enter(self, ctx);
            step.Tick(self, ctx, 0.5f);

            Assert.IsFalse(step.TryOverrideDuration(1f), "A half-walked approach must not be rescaled.");
            Assert.AreEqual(4f, step.ResolveDuration(tuning));
        }

        [Test]
        public void TryOverrideDuration_RejectsNonPositive()
        {
            var step = new ApproachStep(3f);

            Assert.IsFalse(step.TryOverrideDuration(0f));
            Assert.IsFalse(step.TryOverrideDuration(-1f));
            Assert.AreEqual(3f, step.ResolveDuration(new SalesTuning()));
        }

        [Test]
        public void Enter_SetsApproachingPhase()
        {
            var sink = new RecordingSink();
            var ctx = SalesTestKit.Context(new SalesShelf(), SalesTestKit.Location(), sink);
            var step = new ApproachStep();
            var self = new Customer("c1", new[] { step });

            step.Enter(self, ctx);

            CollectionAssert.Contains(
                System.Linq.Enumerable.Select(sink.Phases, p => p.phase),
                CustomerPhase.Approaching);
        }
    }
}
