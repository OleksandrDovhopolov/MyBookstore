using System;
using System.Collections.Generic;
using Book.Sell.Domain;
using Book.Sell.Domain.Steps;
using Book.Sell.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor
{
    /// <summary>
    /// Этап 3: CustomerPlan (mutable traversal + safe insertion) and the Customer seams that delegate
    /// to it. Pure-plan tests inspect Current/IsDone directly (CustomerPlan never calls Enter/Tick/Exit).
    /// Customer-level tests drive Tick to verify Exit semantics, insert-during-tick + Enter, and the
    /// no-closing fallback. Existing Customer/controller tests stay green as the behavior-parity guard.
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
        public void SkipToClosing_SkipsInjectedAndAuthoredMiddle()
        {
            var a = new FakeStep();
            var passive = new FakeStep();
            var complete = new FakeClosingStep();
            var leave = new FakeClosingStep();
            var injected = new FakeStep();
            var plan = new CustomerPlan(new ICustomerStep[] { a, passive, complete, leave });

            plan.InsertNext(injected);   // [a, injected, passive, complete, leave]

            Assert.IsTrue(plan.SkipToClosing());
            Assert.AreSame(complete, plan.Current, "Skips both injected and authored middle steps.");
        }

        [Test]
        public void SkipToClosing_NoClosing_ReturnsFalse_ThenFinishCompletes()
        {
            var a = new FakeStep();
            var b = new FakeStep();
            var plan = new CustomerPlan(new ICustomerStep[] { a, b });

            Assert.IsFalse(plan.SkipToClosing(), "No closing step ahead.");
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
        public void Abort_ExitsCurrentOnly_NotSkippedSteps()
        {
            var a = new FakeStep { Status = StepStatus.CompletedAndLeave };
            var skipped = new FakeStep();
            var complete = new FakeClosingStep();
            var leave = new FakeClosingStep();
            var customer = new Customer("c1", new ICustomerStep[] { a, skipped, complete, leave });
            var ctx = Ctx();

            customer.Tick(ctx, 1f);

            Assert.IsTrue(a.Exited, "Current (entered) step is exited on abort.");
            Assert.IsFalse(skipped.Exited, "Skipped (never-entered) step is not exited.");
            Assert.AreSame(complete, customer.CurrentStep, "Resumes at the first closing step.");
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
        public void AbortWithoutClosing_FinishesCustomer()
        {
            var a = new FakeStep { Status = StepStatus.CompletedAndLeave };
            var b = new FakeStep();
            var customer = new Customer("c1", new ICustomerStep[] { a, b });   // no IClosingStep
            var ctx = Ctx();

            customer.Tick(ctx, 1f);

            Assert.IsTrue(customer.IsDone, "No closing step → customer finishes instead of looping on current.");
            Assert.AreEqual(CustomerPhase.Done, customer.Phase);
        }
    }
}
