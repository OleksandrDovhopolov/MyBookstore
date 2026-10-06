using System.Text.RegularExpressions;
using Book.Sell.Domain;
using Book.Sell.Domain.Steps;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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
            var step = new ActiveRequestStep(SalesTestKit.ActiveRequest("r1"));
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
            var step = new ActiveRequestStep(SalesTestKit.ActiveRequest("r1"));
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
            var step = new ActiveRequestStep(SalesTestKit.ActiveRequest("r1"));
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
            var step = new ActiveRequestStep(SalesTestKit.ActiveRequest("r1"));
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

        // --- Late binding -------------------------------------------------------------------

        [Test]
        public void Request_IsNull_UntilTheMinigameOpens()
        {
            var selector = new FakeActiveRequestSelector().Enqueue(SalesTestKit.ActiveRequest("r1"));
            var ctx = SalesTestKit.Context(
                SalesTestKit.Shelf(SalesTestKit.Book("b1")), SalesTestKit.Location(), new RecordingSink(),
                tuning: new SalesTuning { BrowseDuration = 1f }, activeRequests: selector);
            var step = new ActiveRequestStep();
            var self = new Customer("c1", new[] { step });

            step.Enter(self, ctx);
            step.Tick(self, ctx, 0.5f);

            Assert.IsNull(step.Request, "Still thinking — nothing has been drawn yet.");
            Assert.AreEqual(0, selector.DrawCount);

            step.Tick(self, ctx, 0.6f);

            Assert.AreEqual("r1", step.Request.Id);
            Assert.AreEqual(1, selector.DrawCount);
        }

        /// <summary>
        /// The reason the draw moved out of day planning: a book sold passively while this customer was
        /// still queueing must be gone from the shelf the selector is shown.
        /// </summary>
        [Test]
        public void Draw_SeesTheShelfAsItStandsWhenTheCustomerWalksUp()
        {
            var shelf = SalesTestKit.Shelf(SalesTestKit.Book("b1"), SalesTestKit.Book("b2"));
            var selector = new FakeActiveRequestSelector().Enqueue(SalesTestKit.ActiveRequest("r1"));
            var ctx = SalesTestKit.Context(shelf, SalesTestKit.Location(), new RecordingSink(),
                activeRequests: selector);
            var step = new ActiveRequestStep();
            var self = new Customer("c1", new[] { step });

            shelf.CommitSale("b1");   // sold passively before this customer reached the minigame

            step.Enter(self, ctx);
            step.Tick(self, ctx, 0.1f);

            CollectionAssert.AreEqual(new[] { "b2" }, selector.ShelvesSeen[0],
                "The sold book must not be offered to the selector as a possible answer.");
        }

        [Test]
        public void NoRequestAvailable_CompletesWithoutHoldingTheLock()
        {
            var theLock = new InteractionLock();
            var sink = new RecordingSink();
            var ctx = SalesTestKit.Context(
                SalesTestKit.Shelf(SalesTestKit.Book("b1")), SalesTestKit.Location(), sink,
                interactionLock: theLock, activeRequests: new FakeActiveRequestSelector());
            var step = new ActiveRequestStep();
            var self = new Customer("c1", new[] { step });

            LogAssert.Expect(LogType.Warning, new Regex("reached the minigame with no request available"));

            step.Enter(self, ctx);
            var status = step.Tick(self, ctx, 0.1f);

            Assert.AreEqual(StepStatus.Completed, status);
            Assert.IsFalse(theLock.IsHeld, "A step with nothing to ask must not sit on the lock.");
            Assert.IsEmpty(sink.ActiveStarted);
        }

        [Test]
        public void TwoCustomers_ShareTheDaySelector_AndDoNotRepeatARequest()
        {
            var selector = new ProfileMatchedRequestSelector(
                new[] { SalesTestKit.ActiveRequest("r1"), SalesTestKit.ActiveRequest("r2") },
                new BookConditionRequestEvaluator());
            var theLock = new InteractionLock();
            var ctx = SalesTestKit.Context(
                SalesTestKit.Shelf(SalesTestKit.Book("b1")), SalesTestKit.Location(), new RecordingSink(),
                interactionLock: theLock, activeRequests: selector);

            var first = new ActiveRequestStep();
            var firstCustomer = new Customer("c1", new[] { first });
            first.Enter(firstCustomer, ctx);
            first.Tick(firstCustomer, ctx, 0.1f);
            first.Exit(firstCustomer, ctx);

            var second = new ActiveRequestStep();
            var secondCustomer = new Customer("c2", new[] { second });
            second.Enter(secondCustomer, ctx);
            second.Tick(secondCustomer, ctx, 0.1f);

            Assert.AreNotEqual(first.Request.Id, second.Request.Id);
        }
    }
}
