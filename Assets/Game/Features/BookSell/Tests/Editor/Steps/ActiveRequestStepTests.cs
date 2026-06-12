using Book.Sell.Domain;
using Book.Sell.Domain.Steps;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor.Steps
{
    public sealed class ActiveRequestStepTests
    {
        [Test]
        public void FreeLock_Acquires_OpensMinigame()
        {
            var sink = new RecordingSink();
            var theLock = new InteractionLock();
            var ctx = SalesTestKit.Context(new SalesShelf(), SalesTestKit.Location(), sink, interactionLock: theLock);
            var step = new ActiveRequestStep(SalesTestKit.Request("r1"));
            var self = new Customer("c1", new[] { step });

            step.Enter(self, ctx);
            var status = step.Tick(self, ctx, 0.1f);

            Assert.AreEqual(StepStatus.Running, status, "Holds the lock, awaiting player input.");
            Assert.IsTrue(theLock.IsHeld);
            Assert.AreSame(self, theLock.CurrentHolder);
            Assert.AreEqual(1, sink.ActiveStarted.Count);
            Assert.AreEqual("r1", sink.ActiveStarted[0].request.Id);
        }

        [Test]
        public void LockHeldByOther_Blocks_NoMinigame()
        {
            var sink = new RecordingSink();
            var theLock = new InteractionLock();
            var other = new object();
            theLock.TryAcquire(other);

            var ctx = SalesTestKit.Context(new SalesShelf(), SalesTestKit.Location(), sink, interactionLock: theLock);
            var step = new ActiveRequestStep(SalesTestKit.Request("r1"));
            var self = new Customer("c1", new[] { step });

            step.Enter(self, ctx);
            var status = step.Tick(self, ctx, 0.1f);

            Assert.AreEqual(StepStatus.Blocked, status);
            Assert.AreSame(other, theLock.CurrentHolder, "The other holder is untouched.");
            Assert.IsEmpty(sink.ActiveStarted);
        }

        [Test]
        public void Exit_ReleasesLock()
        {
            var sink = new RecordingSink();
            var theLock = new InteractionLock();
            var ctx = SalesTestKit.Context(new SalesShelf(), SalesTestKit.Location(), sink, interactionLock: theLock);
            var step = new ActiveRequestStep(SalesTestKit.Request("r1"));
            var self = new Customer("c1", new[] { step });

            step.Enter(self, ctx);
            step.Tick(self, ctx, 0.1f);
            Assert.IsTrue(theLock.IsHeld);

            step.Exit(self, ctx);
            Assert.IsFalse(theLock.IsHeld);
        }
    }
}
