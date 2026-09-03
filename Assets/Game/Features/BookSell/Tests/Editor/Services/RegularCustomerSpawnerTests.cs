using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Book.Sell.Domain;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Book.Sell.Tests.Editor.Services
{
    /// <summary>
    /// Tests for <see cref="RegularCustomerSpawner"/>: the customer count comes from
    /// <see cref="ICustomerTrafficResolver"/> and the active-request count from
    /// <see cref="IActiveRequestCountResolver"/>. The requests catalog is a POOL — it must never size the
    /// day (the old request-count floor did exactly that) — and the requests are spread across random
    /// customer slots rather than always landing on the first N.
    /// </summary>
    public sealed class RegularCustomerSpawnerTests
    {
        private sealed class StubResolver : ICustomerTrafficResolver
        {
            private readonly CustomerTrafficResult _result;
            public StubResolver(CustomerTrafficResult result) => _result = result;
            public CustomerTrafficResult Resolve(SalesSessionSetup setup, SalesTuning tuning) => _result;
        }

        private sealed class StubRequestCount : IActiveRequestCountResolver
        {
            private readonly int _count;
            public StubRequestCount(int count) => _count = count;

            public ActiveRequestCountResult Resolve(SalesSessionSetup setup, SalesTuning tuning)
                => new(_count, _count, isHardOverride: false, breakdown: null);
        }

        private sealed class StubActiveRequests : IActiveRequestRuntimeProvider
        {
            private readonly IReadOnlyList<ActiveRequestRuntime> _requests;
            public StubActiveRequests(IReadOnlyList<ActiveRequestRuntime> requests) => _requests = requests;
            public IReadOnlyList<ActiveRequestRuntime> GetRequests() => _requests;
        }

        private sealed class StubProfileProvider : ICustomerProfileProvider
        {
            public CustomerProfile Create(SalesSessionSetup setup, ISalesRandom random)
                => new(new[] { "Fact", "Travel" });
        }

        private static SalesSessionSetup Setup()
            => new SalesSessionSetup(1, "loc", Array.Empty<string>(), Array.Empty<string>());

        private static IReadOnlyList<Customer> Build(
            int customerCount,
            int requestDemand,
            int poolSize,
            ICustomerProfileProvider profiles = null,
            FakeSalesRandom random = null)
        {
            var pool = new ActiveRequestRuntime[poolSize];
            for (var i = 0; i < poolSize; i++) pool[i] = SalesTestKit.ActiveRequest($"r{i + 1}");

            var spawner = new RegularCustomerSpawner(
                new FakeConfigsService(),
                new StubResolver(new CustomerTrafficResult(customerCount, customerCount, isHardOverride: false, breakdown: null)),
                new StubActiveRequests(pool),
                profiles,
                new StubRequestCount(requestDemand));

            return spawner.BuildCustomers(Setup(), SalesTestKit.FastTuning(), random ?? new FakeSalesRandom());
        }

        private static RecordingSink DriveAll(IReadOnlyList<Customer> customers)
        {
            var sink = new RecordingSink();
            foreach (var customer in customers)
            {
                var ctx = SalesTestKit.Context(
                    SalesTestKit.Shelf(SalesTestKit.Book("b1"), SalesTestKit.Book("b2"), SalesTestKit.Book("b3")),
                    SalesTestKit.Location(),
                    sink,
                    passiveSelector: SalesTestKit.AlwaysHitPassiveSelector());

                for (var i = 0; i < 100 && !customer.IsDone; i++)
                {
                    customer.Tick(ctx, 1f);
                    if (customer.Phase == CustomerPhase.InMinigame)
                        customer.ForceCompleteCurrentStep(ctx);
                }
            }

            return sink;
        }

        /// <summary>Regression: a 49-entry catalog used to floor the day at 49 customers.</summary>
        [Test]
        public void CatalogSize_DoesNotRaiseCustomerCount()
        {
            var customers = Build(customerCount: 2, requestDemand: 1, poolSize: 49);
            Assert.AreEqual(2, customers.Count);
        }

        [Test]
        public void ActiveCount_ComesFromResolver_NotCatalog()
        {
            var customers = Build(customerCount: 4, requestDemand: 1, poolSize: 5);
            var sink = DriveAll(customers);

            Assert.AreEqual(4, customers.Count);
            Assert.AreEqual(1, sink.ActiveStarted.Count);
        }

        [Test]
        public void RequestDemand_CappedByCustomerCount()
        {
            LogAssert.Expect(LogType.Warning,
                new Regex(@"\[Sales\.Traffic\] requestCap day=1 demand=5 customers=2 pool=5 final=2.*"));

            var customers = Build(customerCount: 2, requestDemand: 5, poolSize: 5);
            var sink = DriveAll(customers);

            Assert.AreEqual(2, customers.Count);
            Assert.AreEqual(2, sink.ActiveStarted.Count);
        }

        [Test]
        public void RequestDemand_CappedByPoolSize()
        {
            LogAssert.Expect(LogType.Warning,
                new Regex(@"\[Sales\.Traffic\] requestCap day=1 demand=5 customers=10 pool=2 final=2.*"));

            var customers = Build(customerCount: 10, requestDemand: 5, poolSize: 2);
            var sink = DriveAll(customers);

            Assert.AreEqual(10, customers.Count);
            Assert.AreEqual(2, sink.ActiveStarted.Count);
        }

        [Test]
        public void ZeroDemand_LeavesEveryCustomerPassive()
        {
            var customers = Build(customerCount: 3, requestDemand: 0, poolSize: 5);
            var sink = DriveAll(customers);

            Assert.AreEqual(3, customers.Count);
            Assert.AreEqual(0, sink.ActiveStarted.Count);
        }

        /// <summary>
        /// Active customers are no longer hardcoded to the first N slots. The slot-picking draws
        /// place active requests at the tail of the day.
        /// </summary>
        [Test]
        public void ActiveSlots_AreSpreadAcrossCustomers_NotAlwaysFirstN()
        {
            var random = new FakeSalesRandom().EnqueueRangeIndex(3, 1);
            var customers = Build(customerCount: 4, requestDemand: 2, poolSize: 2, random: random);
            var sink = DriveAll(customers);

            CollectionAssert.AreEquivalent(
                new[] { "cust_3", "cust_4" },
                sink.ActiveStarted.Select(x => x.customer.Id).ToArray());
        }

        /// <summary>Empty draw queue → Range returns min → the legacy "first N slots" layout.</summary>
        [Test]
        public void ActiveSlots_DefaultDraw_FallsBackToFirstSlots()
        {
            var customers = Build(customerCount: 4, requestDemand: 2, poolSize: 2);
            var sink = DriveAll(customers);

            CollectionAssert.AreEquivalent(
                new[] { "cust_1", "cust_2" },
                sink.ActiveStarted.Select(x => x.customer.Id).ToArray());
        }

        [Test]
        public void RegularCustomers_ReceiveProfileFromProvider()
        {
            var customers = Build(customerCount: 1, requestDemand: 0, poolSize: 0, profiles: new StubProfileProvider());

            Assert.AreEqual(1, customers.Count);
            CollectionAssert.AreEqual(new[] { "Fact", "Travel" }, customers[0].Profile.DesiredGenres);
        }

        [Test]
        public void ActiveRequest_IsSelectedToMatchCustomerProfile()
        {
            var spawner = new RegularCustomerSpawner(
                new FakeConfigsService(),
                new StubResolver(new CustomerTrafficResult(1, 1, isHardOverride: false, breakdown: null)),
                new StubActiveRequests(new[]
                {
                    SalesTestKit.ActiveRequest("crime", requiredGenres: new[] { "Crime" }),
                    SalesTestKit.ActiveRequest("fact", requiredGenres: new[] { "Fact" })
                }),
                new StubProfileProvider(),
                new StubRequestCount(1));
            var customers = spawner.BuildCustomers(Setup(), SalesTestKit.FastTuning(), new FakeSalesRandom());
            var sink = DriveAll(customers);

            Assert.AreEqual(1, sink.ActiveStarted.Count);
            Assert.AreEqual("fact", sink.ActiveStarted[0].request.Id);
        }
    }
}
