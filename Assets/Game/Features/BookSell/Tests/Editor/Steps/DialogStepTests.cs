using Book.Sell.API;
using Book.Sell.Domain;
using Book.Sell.Domain.Steps;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor.Steps
{
    public sealed class DialogStepTests
    {
        [Test]
        public void FreeLock_Acquires_StartsDialogue()
        {
            var sink = new RecordingSink();
            var theLock = new InteractionLock();
            var ctx = SalesTestKit.Context(
                SalesTestKit.Shelf(SalesTestKit.Book("b1")), SalesTestKit.Location(), sink, interactionLock: theLock);
            var step = new DialogStep(new DialoguePayload("dlg_intro"));
            var self = new Customer("c1", new[] { step });

            step.Enter(self, ctx);
            var status = step.Tick(self, ctx, 0.1f);

            Assert.AreEqual(StepStatus.Running, status, "Holds the lock, awaiting dialogue completion.");
            Assert.IsTrue(theLock.IsHeld);
            Assert.AreSame(self, theLock.CurrentHolder);
            Assert.AreEqual(CustomerPhase.InDialogue, self.Phase);
            Assert.AreEqual(1, sink.DialoguesStarted.Count);
            Assert.AreEqual("dlg_intro", sink.DialoguesStarted[0].payload.DialogueId);
        }

        [Test]
        public void LockHeldByOther_Blocks_NoDialogue()
        {
            var sink = new RecordingSink();
            var theLock = new InteractionLock();
            var other = new object();
            theLock.TryAcquire(other);

            var ctx = SalesTestKit.Context(
                SalesTestKit.Shelf(SalesTestKit.Book("b1")), SalesTestKit.Location(), sink, interactionLock: theLock);
            var step = new DialogStep(new DialoguePayload("dlg_intro"));
            var self = new Customer("c1", new[] { step });

            step.Enter(self, ctx);
            var status = step.Tick(self, ctx, 0.1f);

            Assert.AreEqual(StepStatus.Blocked, status);
            Assert.AreSame(other, theLock.CurrentHolder, "The other holder is untouched.");
            Assert.IsEmpty(sink.DialoguesStarted);
            Assert.AreNotEqual(CustomerPhase.InDialogue, self.Phase, "Not talking while waiting for the lock.");
        }

        [Test]
        public void Exit_ReleasesLock()
        {
            var sink = new RecordingSink();
            var theLock = new InteractionLock();
            var ctx = SalesTestKit.Context(
                SalesTestKit.Shelf(SalesTestKit.Book("b1")), SalesTestKit.Location(), sink, interactionLock: theLock);
            var step = new DialogStep(new DialoguePayload("dlg_intro"));
            var self = new Customer("c1", new[] { step });

            step.Enter(self, ctx);
            step.Tick(self, ctx, 0.1f);
            Assert.IsTrue(theLock.IsHeld);

            step.Exit(self, ctx);
            Assert.IsFalse(theLock.IsHeld);
        }

        [Test]
        public void AlreadyHolding_StaysRunning_NoDuplicateSink()
        {
            var sink = new RecordingSink();
            var theLock = new InteractionLock();
            var ctx = SalesTestKit.Context(
                SalesTestKit.Shelf(SalesTestKit.Book("b1")), SalesTestKit.Location(), sink, interactionLock: theLock);
            var step = new DialogStep(new DialoguePayload("dlg_intro"));
            var self = new Customer("c1", new[] { step });

            step.Enter(self, ctx);
            step.Tick(self, ctx, 0.1f);
            var status = step.Tick(self, ctx, 0.1f);

            Assert.AreEqual(StepStatus.Running, status);
            Assert.AreEqual(1, sink.DialoguesStarted.Count, "Dialogue is reported once, not per tick.");
        }

        [Test]
        public void ForceComplete_ReleasesLock_AndAdvances()
        {
            var sink = new RecordingSink();
            var theLock = new InteractionLock();
            var ctx = SalesTestKit.Context(
                SalesTestKit.Shelf(SalesTestKit.Book("b1")), SalesTestKit.Location(), sink, interactionLock: theLock);
            var dialog = new DialogStep(new DialoguePayload("dlg_intro"));
            var next = new CommentStep(new CustomerCommentPayload("b1"));
            var self = new Customer("c1", new ICustomerStep[] { dialog, next });

            self.Tick(ctx, 0.1f);   // Enter + Tick the dialog: acquires the lock
            Assert.IsTrue(theLock.IsHeld);

            self.ForceCompleteCurrentStep(ctx);   // controller-driven completion (dialogue UI closed)

            Assert.IsFalse(theLock.IsHeld, "Force-completing runs Exit, releasing the lock.");
            Assert.AreSame(next, self.CurrentStep, "Plan advanced to the next step.");
            Assert.IsFalse(self.IsDone);
        }

        [Test]
        public void BlockedThenExit_RemovesFromQueue_NoGhostWaiter()
        {
            var sink = new RecordingSink();
            var theLock = new InteractionLock();
            var a = new object();
            theLock.TryAcquire(a);   // A holds the lock

            var ctx = SalesTestKit.Context(
                SalesTestKit.Shelf(SalesTestKit.Book("b1")), SalesTestKit.Location(), sink, interactionLock: theLock);
            var step = new DialogStep(new DialoguePayload("dlg_intro"));
            var b = new Customer("c1", new[] { step });

            step.Enter(b, ctx);
            Assert.AreEqual(StepStatus.Blocked, step.Tick(b, ctx, 0.1f), "B is enqueued behind A.");

            step.Exit(b, ctx);       // B leaves before its turn — must drop out of the FIFO queue
            theLock.Release(a);      // A releases; queue should now be empty

            var c = new object();
            Assert.IsTrue(theLock.TryAcquire(c), "A fresh token wins immediately — B is not a ghost waiter.");
            Assert.AreSame(c, theLock.CurrentHolder);
        }
    }
}
