using System;
using System.Collections.Generic;
using Book.Sell.Domain;
using Book.Sell.Domain.Steps;
using Book.Sell.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor
{
    /// <summary>
    /// CustomerPlan (mutable traversal + safe insertion) and the Customer seams that delegate to it.
    /// Pure-plan tests inspect Current/IsDone directly (CustomerPlan never calls Enter/Tick/Exit).
    /// Customer-level tests drive Tick to verify Exit semantics, insert-during-tick + Enter, and the
    /// passive-chain end (ADR-0003): a passive miss drops the remaining PASSIVE steps only — non-passive
    /// steps (active request, dialogue, comment) and the closing tail still run.
    /// </summary>
    public sealed class CustomerPlanTests
    {
        private class FakeStep : ICustomerStep
        {
            public bool Entered;
            public bool Exited;
            public StepStatus Status = StepStatus.Completed;
            public Action<Customer> OnTick;

            public void Enter(Customer self, CustomerContext ctx) => Entered = true;

            public StepStatus Tick(Customer self, CustomerContext ctx, float dt)
            {
                OnTick?.Invoke(self);
                return Status;
            }

            public void Exit(Customer self, CustomerContext ctx) => Exited = true;
        }

        private sealed class FakeClosingStep : FakeStep, IClosingStep
        {
        }

        /// <summary>A passive purchase intent — dropped from the plan once the passive chain ends.</summary>
        private sealed class FakePassiveStep : FakeStep, IPassivePurchaseStep
        {
        }

        private static CustomerContext Ctx() =>
            SalesTestKit.Context(new SalesShelf(), SalesTestKit.Location(), new RecordingSink());

        // --- Pure CustomerPlan ---------------------------------------------------------------

        [Test]
        public void InsertNext_PutsStepImmediatelyAfterCurrent()
        {
            var a = new FakeStep();
            var b = new FakeStep();
            var x = new FakeStep();
            var plan = new CustomerPlan(new ICustomerStep[] { a, b });

            Assert.AreSame(a, plan.Current);
            Assert.IsTrue(plan.InsertNext(x));

            plan.Advance();
            Assert.AreSame(x, plan.Current, "Inserted step runs before the originally-next step.");
            plan.Advance();
            Assert.AreSame(b, plan.Current);
        }

        [Test]
        public void InsertNext_RepeatedCalls_AreFifo()
        {
            var a = new FakeStep();
            var b = new FakeStep();
            var x = new FakeStep();
            var y = new FakeStep();
            var plan = new CustomerPlan(new ICustomerStep[] { a, b });

            plan.InsertNext(x);
            plan.InsertNext(y);

            plan.Advance();
            Assert.AreSame(x, plan.Current, "First inserted runs first (FIFO, not LIFO).");
            plan.Advance();
            Assert.AreSame(y, plan.Current);
            plan.Advance();
            Assert.AreSame(b, plan.Current);
        }

        [Test]
        public void Insert_OnClosingStep_IsRejected()
        {
            var a = new FakeStep();
            var close = new FakeClosingStep();
            var x = new FakeStep();
            var plan = new CustomerPlan(new ICustomerStep[] { a, close });

            plan.Advance();   // Current == close
            Assert.AreSame(close, plan.Current);

            Assert.IsFalse(plan.InsertNext(x), "No insertion after the closing tail has started.");
            Assert.IsFalse(plan.InsertBeforeClosing(x));

            plan.Advance();
            Assert.IsTrue(plan.IsDone, "Plan unchanged: only A and the closing step existed.");
        }

        [Test]
        public void InsertBeforeClosing_InsertsBeforeFirstClosingStep()
        {
            var a = new FakeStep();
            var passive = new FakeStep();
            var complete = new FakeClosingStep();
            var leave = new FakeClosingStep();
            var x = new FakeStep();
            var plan = new CustomerPlan(new ICustomerStep[] { a, passive, complete, leave });

            Assert.IsTrue(plan.InsertBeforeClosing(x));

            plan.Advance(); Assert.AreSame(passive, plan.Current);
            plan.Advance(); Assert.AreSame(x, plan.Current, "Inserted before the closing tail.");
            plan.Advance(); Assert.AreSame(complete, plan.Current, "Still before the first closing step.");
        }

        [Test]
        public void RemoveRemainingPassivePurchases_DropsPassiveOnly_KeepsCurrentAndNonPassive()
        {
            var current = new FakePassiveStep();
            var injected = new FakeStep();
            var trailingPassive = new FakePassiveStep();
            var complete = new FakeClosingStep();
            var plan = new CustomerPlan(new ICustomerStep[] { current, trailingPassive, complete });

            plan.InsertNext(injected);   // [current, injected, trailingPassive, complete]

            Assert.AreEqual(1, plan.RemoveRemainingPassivePurchases(), "Only the trailing passive is dropped.");
            Assert.AreSame(current, plan.Current, "The current (passive) step is never removed under itself.");

            plan.Advance();
            Assert.AreSame(injected, plan.Current, "Non-passive injected step survives.");
            plan.Advance();
            Assert.AreSame(complete, plan.Current, "Trailing passive is gone; the closing tail is next.");
        }

        [Test]
        public void RemoveRemainingPassivePurchases_NoPassiveAhead_ChangesNothing()
        {
            var a = new FakeStep();
            var b = new FakeStep();
            var plan = new CustomerPlan(new ICustomerStep[] { a, b });

            Assert.AreEqual(0, plan.RemoveRemainingPassivePurchases());
            plan.Advance();
            Assert.AreSame(b, plan.Current);
        }

        [Test]
        public void Finish_CompletesPlan()
        {
            var plan = new CustomerPlan(new ICustomerStep[] { new FakeStep(), new FakeStep() });

            plan.Finish();

            Assert.IsTrue(plan.IsDone);
        }

        [Test]
        public void Insert_NullStep_Throws()
        {
            var plan = new CustomerPlan(new ICustomerStep[] { new FakeStep() });
            Assert.Throws<ArgumentNullException>(() => plan.InsertNext(null));
            Assert.Throws<ArgumentNullException>(() => plan.InsertBeforeClosing(null));
        }

        [Test]
        public void Ctor_CopiesList()
        {
            var a = new FakeStep();
            var b = new FakeStep();
            var src = new List<ICustomerStep> { a, b };
            var plan = new CustomerPlan(src);

            src.Clear();   // must not affect the plan

            Assert.AreSame(a, plan.Current);
            plan.Advance();
            Assert.AreSame(b, plan.Current);
            plan.Advance();
            Assert.IsTrue(plan.IsDone);
        }

        // --- Customer seams ------------------------------------------------------------------

        [Test]
        public void EndPassiveChain_ExitsCurrentOnly_AndResumesAtNonPassiveStep()
        {
            var a = new FakePassiveStep { Status = StepStatus.CompletedAndEndPassiveChain };
            var nonPassive = new FakeStep();
            var complete = new FakeClosingStep();
            var leave = new FakeClosingStep();
            var customer = new Customer("c1", new ICustomerStep[] { a, nonPassive, complete, leave });
            var ctx = Ctx();

            customer.Tick(ctx, 1f);

            Assert.IsTrue(a.Exited, "Current (entered) step is exited when the passive chain ends.");
            Assert.IsFalse(nonPassive.Exited, "The next step is not entered yet, so it is not exited.");
            Assert.AreSame(nonPassive, customer.CurrentStep,
                "ADR-0003: a passive miss ends the passive chain, it does not skip non-passive steps.");
        }

        /// <summary>
        /// Regression for the PassiveActivePassive shape: the trailing passive sits AFTER the non-passive
        /// step, so "skip forward to the next non-passive" would leave it to run after the minigame.
        /// </summary>
        [Test]
        public void EndPassiveChain_DropsPassiveStepsBehindANonPassiveStep()
        {
            var leading = new FakePassiveStep { Status = StepStatus.CompletedAndEndPassiveChain };
            var active = new FakeStep();
            var trailing = new FakePassiveStep();
            var complete = new FakeClosingStep();
            var customer = new Customer("c1", new ICustomerStep[] { leading, active, trailing, complete });
            var ctx = Ctx();

            customer.Tick(ctx, 1f);
            Assert.AreSame(active, customer.CurrentStep, "The active step survives the passive-chain end.");

            customer.Tick(ctx, 1f);   // active completes
            Assert.AreSame(complete, customer.CurrentStep, "The trailing passive was dropped, not merely skipped.");
            Assert.IsFalse(trailing.Entered, "No further passive intent runs after a miss.");
        }

        [Test]
        public void InsertDuringTick_RunsNext_AndEntersOnFollowingTick()
        {
            var marker = new FakeStep();
            var next = new FakeStep();
            FakeStep a = null;
            a = new FakeStep { OnTick = self => self.InsertNext(marker) };   // inserts during its own Tick
            var customer = new Customer("c1", new ICustomerStep[] { a, next });
            var ctx = Ctx();

            customer.Tick(ctx, 1f);   // a enters, inserts marker, completes -> advance to marker
            Assert.AreSame(marker, customer.CurrentStep, "Injected step becomes current after advance.");
            Assert.IsFalse(marker.Entered, "Not entered yet (entered only on next tick).");

            customer.Tick(ctx, 1f);   // marker enters
            Assert.IsTrue(marker.Entered, "Injected step's Enter runs on the following tick (_entered reset).");
        }

        [Test]
        public void EndPassiveChain_WithoutClosing_StillRunsTheRemainingNonPassiveStep()
        {
            var a = new FakePassiveStep { Status = StepStatus.CompletedAndEndPassiveChain };
            var b = new FakeStep();
            var customer = new Customer("c1", new ICustomerStep[] { a, b });   // no IClosingStep
            var ctx = Ctx();

            customer.Tick(ctx, 1f);

            Assert.IsFalse(customer.IsDone, "A non-passive step is still ahead — the visit is not over.");
            Assert.AreSame(b, customer.CurrentStep);
        }

        [Test]
        public void EndPassiveChain_WithNothingLeft_FinishesCustomer()
        {
            var a = new FakePassiveStep { Status = StepStatus.CompletedAndEndPassiveChain };
            var trailing = new FakePassiveStep();
            var customer = new Customer("c1", new ICustomerStep[] { a, trailing });   // only passive steps
            var ctx = Ctx();

            customer.Tick(ctx, 1f);

            Assert.IsTrue(customer.IsDone, "Trailing passive dropped and nothing else left → the plan ends.");
            Assert.AreEqual(CustomerPhase.Done, customer.Phase);
        }
    }
}
