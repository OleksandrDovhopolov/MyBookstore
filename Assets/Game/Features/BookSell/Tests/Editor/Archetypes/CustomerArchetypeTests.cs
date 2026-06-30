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
    /// Tests for the Этап 2 archetypes (ICustomerArchetype). Archetypes return an inspectable step list,
    /// so passive composition is asserted directly. Active archetypes get a structural smoke check, and
    /// the converted TenBetween spawner gets a stream/order parity test (active count + Passive->Active->
    /// Passive ordering) — see also the spawner-level guards in CustomerPlanBuilderTests.
    /// </summary>
    public sealed class CustomerArchetypeTests
    {
        private static readonly SalesSessionSetup Setup = new(1, "loc", Array.Empty<string>());

        private static List<ICustomerStep> Middle(ICustomerArchetype archetype, ISalesRandom random)
            => archetype.BuildMiddle(Setup, SalesTestKit.FastTuning(), random).ToList();

        private static SalesShelf ShelfOf(int books)
        {
            var shelf = new SalesShelf();
            for (var i = 0; i < books; i++)
                shelf.Add(new ShelfBook(SalesTestKit.Book($"b{i + 1}", genre: "sci-fi")));
            return shelf;
        }

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

        // --- PassiveAttemptsArchetype -------------------------------------------------------

        // 1) (1,5) draws Range(1,6); offset k => 1 + k passive steps.
        [Test]
        public void PassiveAttempts_DrawsRange()
        {
            var random = new FakeSalesRandom().EnqueueRangeIndex(2); // Range(1,6) => 1 + 2 = 3
            var middle = Middle(new PassiveAttemptsArchetype(1, 5), random);

            Assert.AreEqual(3, middle.Count);
            Assert.IsTrue(middle.All(s => s is PassivePurchaseStep));
        }

        // 2) (3,3) => exactly 3 passive steps.
        [Test]
        public void PassiveAttempts_EqualBounds_FixedCount()
        {
            var middle = Middle(new PassiveAttemptsArchetype(3, 3), new FakeSalesRandom());

            Assert.AreEqual(3, middle.Count);
            Assert.IsTrue(middle.All(s => s is PassivePurchaseStep));
        }

        // 3) min == max must NOT consume random (parity with FifteenCustomers..., which never drew):
        //    the enqueued Range index is still available for the next draw.
        [Test]
        public void PassiveAttempts_EqualBounds_DoesNotConsumeRandom()
        {
            var random = new FakeSalesRandom().EnqueueRangeIndex(4);

            var middle = Middle(new PassiveAttemptsArchetype(1, 1), random);

            Assert.AreEqual(1, middle.Count, "Exactly one passive step.");
            Assert.AreEqual(4, random.Range(0, 10), "Range index 4 was NOT consumed by the (1,1) archetype.");
        }

        // --- Active archetypes (structural smoke) -------------------------------------------

        [Test]
        public void ActiveRequest_WithRequest_BuildsActiveStep()
        {
            var request = SalesTestKit.Request("r1");
            var middle = Middle(new ActiveRequestArchetype(request), new FakeSalesRandom());

            Assert.AreEqual(1, middle.Count);
            Assert.IsInstanceOf<ActiveRequestStep>(middle[0]);
            Assert.AreSame(request, ((ActiveRequestStep)middle[0]).Request);
        }

        [Test]
        public void ActiveRequest_NullRequest_EmptyMiddle()
        {
            var middle = Middle(new ActiveRequestArchetype(null), new FakeSalesRandom());
            Assert.IsEmpty(middle);
        }

        [Test]
        public void PassiveActivePassive_OrdersPassiveActivePassive()
        {
            var request = SalesTestKit.Request("r1");
            var random = new FakeSalesRandom().EnqueueRangeIndex(0); // Range(1,3) => 1 leading passive
            var middle = Middle(new PassiveActivePassiveArchetype(request, 1, 2), random);

            Assert.AreEqual(3, middle.Count, "1 leading passive + active + 1 trailing passive.");
            Assert.IsInstanceOf<PassivePurchaseStep>(middle[0]);
            Assert.IsInstanceOf<ActiveRequestStep>(middle[1]);
            Assert.AreSame(request, ((ActiveRequestStep)middle[1]).Request);
            Assert.IsInstanceOf<PassivePurchaseStep>(middle[2]);
        }

        [Test]
        public void PassiveActivePassive_NullRequest_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new PassiveActivePassiveArchetype(null, 1, 2));
        }

        // --- Spawner-level parity (TenBetween) ----------------------------------------------

        // The converted TenBetween spawner must still produce exactly ActiveCustomerCount active customers,
        // and an active customer keeps Passive -> Active -> Passive (Browsing -> InMinigame -> Browsing).
        [Test]
        public void BetweenPassivesSpawner_PreservesActiveCountAndOrder()
        {
            var configs = new FakeConfigsService();
            configs.SetAll<RequestConfig>(new[] { SalesTestKit.Request("r1") });
            var spawner = new TenCustomersThreeActiveBetweenPassivesSpawner(configs);

            // Empty random => PickActiveIndices selects {0,1,2}; each passive count Range falls back to min (1).
            var customers = spawner.BuildCustomers(Setup, SalesTestKit.FastTuning(), new FakeSalesRandom());

            var sink = new RecordingSink();
            foreach (var customer in customers)
            {
                var ctx = SalesTestKit.Context(ShelfOf(3), SalesTestKit.Location(), sink,
                    passiveSelector: SalesTestKit.AlwaysHitPassiveSelector());
                Drive(customer, ctx);
            }

            var activeCustomers = sink.ActiveStarted.Select(a => a.customer).Distinct().ToList();
            Assert.AreEqual(3, activeCustomers.Count, "Exactly ActiveCustomerCount customers ran an active step.");

            var phases = PhasesOf(sink, activeCustomers[0]);
            var minigameIndex = phases.IndexOf(CustomerPhase.InMinigame);
            var lastBrowsingIndex = phases.LastIndexOf(CustomerPhase.Browsing);
            Assert.Greater(minigameIndex, 0, "Minigame happened after the first passive (Browsing).");
            Assert.Greater(lastBrowsingIndex, minigameIndex, "A passive (Browsing) followed the minigame.");
        }
    }
}
