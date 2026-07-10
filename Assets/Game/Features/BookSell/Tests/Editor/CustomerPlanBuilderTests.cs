using System;
using System.Collections.Generic;
using System.Linq;
using Book.Sell.Domain;
using Book.Sell.Domain.Steps;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using Game.Configs.Models;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor
{
    /// <summary>
    /// Tests for <see cref="CustomerPlanBuilder"/> (Этап 1). Verifies the mandatory skeleton
    /// (Approach -> middle -> CompletePurchase -> Leave), behavior parity with hand-built plans,
    /// that active middle steps survive the refactor, and that the random-draw order is preserved
    /// (builder-local approach -> middle -> leave, and spawner pre-loop draws stay before Build).
    /// Driven directly through customer.Tick() like SingleCustomerPassivePurchaseTests.
    /// </summary>
    public sealed class CustomerPlanBuilderTests
    {
        private sealed class FixedProfileProvider : ICustomerProfileProvider
        {
            public CustomerProfile Create(SalesSessionSetup setup, ISalesRandom random) => CustomerProfile.Empty;
        }

        private static SalesShelf ShelfOf(int books)
        {
            var shelf = new SalesShelf();
            for (var i = 0; i < books; i++)
                shelf.Add(new ShelfBook(SalesTestKit.Book($"b{i + 1}", genre: "sci-fi")));
            return shelf;
        }

        // Ticks until done, auto-resolving any active minigame the instant the customer enters it
        // (simulates the player/controller force-completing ActiveRequestStep).
        private static void Drive(Customer customer, CustomerContext ctx, int maxTicks = 500)
        {
            for (var i = 0; i < maxTicks && !customer.IsDone; i++)
            {
                customer.Tick(ctx, 1f);
                if (customer.CurrentStep is ActiveRequestStep && customer.Phase == CustomerPhase.InMinigame)
                    customer.ForceCompleteCurrentStep(ctx);
            }
        }

        private static List<CustomerPhase> PhasesOf(RecordingSink sink, Customer customer)
            => sink.Phases.Where(p => p.customer == customer).Select(p => p.phase).ToList();

        // --- Passive (deep) ------------------------------------------------------------------

        // 1) Skeleton order: Approach -> middle -> CompletePurchase -> Leave is observable as
        //    Approaching first and Leaving last, with the customer finishing.
        [Test]
        public void Build_PassiveMiddle_ProducesApproachMiddleCompleteLeaveSkeleton()
        {
            var sink = new RecordingSink();
            var ctx = SalesTestKit.Context(ShelfOf(2), SalesTestKit.Location(), sink,
                passiveSelector: SalesTestKit.AlwaysHitPassiveSelector());
            var customer = CustomerPlanBuilder.Build(
                "c1", SalesTestKit.FastTuning(), new FakeSalesRandom(),
                buildMiddle: () => new ICustomerStep[] { new PassivePurchaseStep(), new PassivePurchaseStep() });

            Drive(customer, ctx);

            var phases = PhasesOf(sink, customer);
            Assert.IsTrue(customer.IsDone, "Customer finishes the plan.");
            Assert.AreEqual(CustomerPhase.Approaching, phases.First(), "First phase is Approaching (head).");
            Assert.AreEqual(CustomerPhase.Leaving, phases.Last(p => p != CustomerPhase.Done),
                "Last non-Done phase is Leaving (tail).");
            Assert.AreEqual(2, sink.PassiveSales.Count, "Both passive middle steps ran.");
        }

        // 2) Parity: a builder-made passive customer behaves identically to the hand-built plan used
        //    in SingleCustomerPassivePurchaseTests (same purchases and sink facts).
        [TestCase(1)]
        [TestCase(3)]
        [TestCase(5)]
        public void Build_PassiveMiddle_MatchesHandBuiltPlan(int n)
        {
            Customer Manual()
            {
                var steps = new List<ICustomerStep> { new ApproachStep() };
                for (var i = 0; i < n; i++) steps.Add(new PassivePurchaseStep());
                steps.Add(new CompletePurchaseStep());
                steps.Add(new LeaveStep());
                return new Customer("manual", steps);
            }

            Customer Built() => CustomerPlanBuilder.Build(
                "built", SalesTestKit.FastTuning(), new FakeSalesRandom(),
                buildMiddle: () => Enumerable.Range(0, n).Select(_ => (ICustomerStep)new PassivePurchaseStep()));

            var manualSink = new RecordingSink();
            var manual = Manual();
            Drive(manual, SalesTestKit.Context(ShelfOf(n), SalesTestKit.Location(), manualSink,
                passiveSelector: SalesTestKit.AlwaysHitPassiveSelector()));

            var builtSink = new RecordingSink();
            var built = Built();
            Drive(built, SalesTestKit.Context(ShelfOf(n), SalesTestKit.Location(), builtSink,
                passiveSelector: SalesTestKit.AlwaysHitPassiveSelector()));

            Assert.AreEqual(manual.PurchasedBookCount, built.PurchasedBookCount, "Same number bought.");
            Assert.AreEqual(manualSink.PassiveSales.Count, builtSink.PassiveSales.Count, "Same passive sales.");
            Assert.AreEqual(manualSink.PurchaseCompletions.Count, builtSink.PurchaseCompletions.Count,
                "Same completion facts.");
        }

        // 3) The builder threads a random approach duration (from MinApproach..MaxApproach) into the
        //    ApproachStep: roll 0.5 over [0,10] => duration 5 => 5 ticks of dt=1 to pass Approach.
        [Test]
        public void Build_ThreadsRandomApproachDurationIntoSkeleton()
        {
            var tuning = SalesTestKit.FastTuning();
            tuning.MinApproachDuration = 0f;
            tuning.MaxApproachDuration = 10f;
            // FastTuning leaves MinLeave/MaxLeave at the SalesTuning defaults (3/6), which would draw —
            // zero them so RandomInRange returns 0 without drawing and only approach consumes the 0.5.
            tuning.MinLeaveDuration = 0f;
            tuning.MaxLeaveDuration = 0f;
            var random = new FakeSalesRandom().EnqueueDouble(0.5);

            var customer = CustomerPlanBuilder.Build("c1", tuning, random,
                buildMiddle: () => Array.Empty<ICustomerStep>());
            var ctx = SalesTestKit.Context(new SalesShelf(), SalesTestKit.Location(), new RecordingSink(),
                tuning: tuning);

            var ticks = 0;
            while (customer.CurrentStep is ApproachStep && ticks < 100)
            {
                customer.Tick(ctx, 1f);
                ticks++;
            }

            Assert.AreEqual(5, ticks, "Approach (override duration 5) takes 5 ticks of dt=1.");
        }

        // --- Active (structural / smoke) ----------------------------------------------------

        // 4) An active middle step is preserved and actually runs (acquires the lock, fires the fact).
        [Test]
        public void Build_ActiveMiddle_RunsActiveStep()
        {
            var sink = new RecordingSink();
            var ctx = SalesTestKit.Context(ShelfOf(1), SalesTestKit.Location(), sink);
            var request = SalesTestKit.Request("r1");
            var customer = CustomerPlanBuilder.Build("c1", SalesTestKit.FastTuning(), new FakeSalesRandom(),
                buildMiddle: () => new ICustomerStep[] { new ActiveRequestStep(request) });

            Drive(customer, ctx);

            Assert.AreEqual(1, sink.ActiveStarted.Count, "Active request started exactly once.");
            Assert.IsTrue(customer.IsDone, "Customer finishes after the active step is resolved.");
        }

        // 5) Passive -> Active -> Passive keeps its order: the minigame happens between two passive
        //    sales (a Browsing phase follows InMinigame), and both passive sales land.
        [Test]
        public void Build_PassiveActivePassive_PreservesOrder()
        {
            var sink = new RecordingSink();
            var ctx = SalesTestKit.Context(ShelfOf(3), SalesTestKit.Location(), sink,
                passiveSelector: SalesTestKit.AlwaysHitPassiveSelector());
            var request = SalesTestKit.Request("r1");
            var customer = CustomerPlanBuilder.Build("c1", SalesTestKit.FastTuning(), new FakeSalesRandom(),
                buildMiddle: () => new ICustomerStep[]
                {
                    new PassivePurchaseStep(), new ActiveRequestStep(request), new PassivePurchaseStep(),
                });

            Drive(customer, ctx);

            var phases = PhasesOf(sink, customer);
            var minigameIndex = phases.IndexOf(CustomerPhase.InMinigame);
            var lastBrowsingIndex = phases.LastIndexOf(CustomerPhase.Browsing);

            Assert.AreEqual(1, sink.ActiveStarted.Count, "One active step ran.");
            Assert.AreEqual(2, sink.PassiveSales.Count, "Both passive steps sold.");
            Assert.Greater(minigameIndex, 0, "Minigame happened after the first passive (Browsing).");
            Assert.Greater(lastBrowsingIndex, minigameIndex, "A passive (Browsing) followed the minigame.");
        }

        // 6) Regression guard: TenCustomersThreeActiveAfterPassiveSpawner's active block is commented
        //    out, so after migration NO customer must produce an active step.
        [Test]
        public void AfterPassiveSpawner_StillHasNoActiveSteps()
        {
            var configs = new FakeConfigsService();
            configs.SetAll<RequestConfig>(new[] { SalesTestKit.Request("r1") });
            var spawner = new TenCustomersThreeActiveAfterPassiveSpawner(configs, new FixedProfileProvider());
            var setup = new SalesSessionSetup(1, "loc", Array.Empty<string>());

            var customers = spawner.BuildCustomers(setup, SalesTestKit.FastTuning(), new FakeSalesRandom());

            var sink = new RecordingSink();
            foreach (var customer in customers)
            {
                var ctx = SalesTestKit.Context(ShelfOf(50), SalesTestKit.Location(), sink,
                    passiveSelector: SalesTestKit.AlwaysHitPassiveSelector());
                Drive(customer, ctx);
            }

            Assert.IsEmpty(sink.ActiveStarted, "Commented active block must stay disabled after migration.");
        }

        // --- Random order -------------------------------------------------------------------

        // 7) Builder-local order is approach -> middle -> leave: the first double feeds Approach, the
        //    second feeds Leave, with the middle's Range draw in between.
        [Test]
        public void Build_DrawOrder_IsApproachThenMiddleThenLeave()
        {
            var tuning = SalesTestKit.FastTuning();
            tuning.MinApproachDuration = 0f;
            tuning.MaxApproachDuration = 10f;
            tuning.MinLeaveDuration = 0f;
            tuning.MaxLeaveDuration = 10f;
            // approach double = 0.2 (=>2), leave double = 0.8 (=>8); middle Range index 1 => 2 passive steps.
            var random = new FakeSalesRandom().EnqueueDouble(0.2, 0.8).EnqueueRangeIndex(1);

            var passiveCount = 0;
            var customer = CustomerPlanBuilder.Build("c1", tuning, random,
                buildMiddle: () =>
                {
                    var n = random.Range(1, 3);      // middle draw, must happen AFTER approach, BEFORE leave
                    passiveCount = n;
                    return Enumerable.Range(0, n).Select(_ => (ICustomerStep)new PassivePurchaseStep());
                });
            var ctx = SalesTestKit.Context(ShelfOf(0), SalesTestKit.Location(), new RecordingSink(), tuning: tuning);

            var approachTicks = 0;
            while (customer.CurrentStep is ApproachStep && approachTicks < 100)
            {
                customer.Tick(ctx, 1f);
                approachTicks++;
            }

            Assert.AreEqual(2, passiveCount, "Middle Range index 1 over [1,3) => 2 passive steps.");
            Assert.AreEqual(2, approachTicks, "Approach consumed the FIRST double (0.2 => 2), proving it drew first.");
        }

        // 8) Spawner-level: a pre-loop draw (ActiveRequestsOnly's customer count = Range(3,6)) stays
        //    BEFORE the builder calls — the count comes from the first Range, not per-customer draws.
        [Test]
        public void ActiveOnlySpawner_KeepsPreLoopCountDraw()
        {
            var configs = new FakeConfigsService();
            configs.SetAll<RequestConfig>(new[] { SalesTestKit.Request("r1") });
            var spawner = new ActiveRequestsOnlyCustomerSpawner(configs);
            var setup = new SalesSessionSetup(1, "loc", Array.Empty<string>());
            // First Range(3,6): index 2 => count 5. FastTuning leaves approach/leave at Min==Max==0 (no draws),
            // and the active-only middle has no Range draw, so this is the only Range consumed.
            var random = new FakeSalesRandom().EnqueueRangeIndex(2);

            var customers = spawner.BuildCustomers(setup, SalesTestKit.FastTuning(), random);

            Assert.AreEqual(5, customers.Count, "Customer count came from the pre-loop Range draw (3 + 2).");
        }
    }
}
