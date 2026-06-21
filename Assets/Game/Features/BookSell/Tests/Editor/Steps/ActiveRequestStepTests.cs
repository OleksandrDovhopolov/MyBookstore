using Book.Sell.Domain;
using Book.Sell.Domain.Steps;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor.Steps
{
    public sealed class ActiveRequestStepTests
    {
        // FastTuning (the Context default) has BrowseDuration = 0, so the Think phase elapses on the first
        // Tick and these tests exercise the acquire/block/release behavior in one step. The shelf needs a
        // book so the post-think empty-shelf guard does not short-circuit to Completed.
        [Test]
        public void FreeLock_Acquires_OpensMinigame()
        {
            var sink = new RecordingSink();
            var theLock = new InteractionLock();
            var ctx = SalesTestKit.Context(
                SalesTestKit.Shelf(SalesTestKit.Book("b1")), SalesTestKit.Location(), sink, interactionLock: theLock);
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

            var ctx = SalesTestKit.Context(
                SalesTestKit.Shelf(SalesTestKit.Book("b1")), SalesTestKit.Location(), sink, interactionLock: theLock);
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
            var ctx = SalesTestKit.Context(
                SalesTestKit.Shelf(SalesTestKit.Book("b1")), SalesTestKit.Location(), sink, interactionLock: theLock);
            var step = new ActiveRequestStep(SalesTestKit.Request("r1"));
            var self = new Customer("c1", new[] { step });

            step.Enter(self, ctx);
            step.Tick(self, ctx, 0.1f);
            Assert.IsTrue(theLock.IsHeld);

            step.Exit(self, ctx);
            Assert.IsFalse(theLock.IsHeld);
        }

        [Test]
        public void Thinks_ForBrowseDuration_NoLockUntilDone_ThenOpensMinigame()
        {
            var sink = new RecordingSink();
            var theLock = new InteractionLock();
            var ctx = SalesTestKit.Context(
                SalesTestKit.Shelf(SalesTestKit.Book("b1")), SalesTestKit.Location(), sink,
                interactionLock: theLock, tuning: new SalesTuning { BrowseDuration = 1f });
            var step = new ActiveRequestStep(SalesTestKit.Request("r1"));
            var self = new Customer("c1", new[] { step });

            step.Enter(self, ctx);
            Assert.AreEqual(CustomerPhase.Browsing, self.Phase, "Enter shows the Choosing... think phase.");

            // While thinking: running, no lock, no minigame.
            Assert.AreEqual(StepStatus.Running, step.Tick(self, ctx, 0.5f), "Thinking → running.");
            Assert.IsFalse(theLock.IsHeld, "No lock acquired while thinking.");
            Assert.IsEmpty(sink.ActiveStarted, "Minigame not opened while thinking.");

            Assert.AreEqual(StepStatus.Running, step.Tick(self, ctx, 0.4f), "Still under BrowseDuration → still thinking.");
            Assert.IsFalse(theLock.IsHeld);
            Assert.IsEmpty(sink.ActiveStarted);

            // Crossing BrowseDuration → acquires the lock and opens the minigame this tick.
            Assert.AreEqual(StepStatus.Running, step.Tick(self, ctx, 0.2f), "After thinking → holds lock, awaiting input.");
            Assert.IsTrue(theLock.IsHeld);
            Assert.AreSame(self, theLock.CurrentHolder);
            Assert.AreEqual(1, sink.ActiveStarted.Count);
            Assert.AreEqual("r1", sink.ActiveStarted[0].request.Id);
        }
    }
}
