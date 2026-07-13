using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Book.Sell.Domain;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using Game.Configs.Models;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Book.Sell.Tests.Editor.Services
{
    /// <summary>
    /// Tests for <see cref="RegularCustomerSpawner"/>: consumes the resolver's FinalCount, applies the
    /// request-count floor on normal days, and skips the floor on hard-override days (exact count).
    /// </summary>
    public sealed class RegularCustomerSpawnerTests
    {
        private sealed class StubResolver : ICustomerTrafficResolver
        {
            private readonly CustomerTrafficResult _result;
            public StubResolver(CustomerTrafficResult result) => _result = result;
            public CustomerTrafficResult Resolve(SalesSessionSetup setup, SalesTuning tuning) => _result;
        }

        private sealed class StubActiveRequests : IActiveRequestRuntimeProvider
        {
            private readonly IReadOnlyList<ActiveRequestRuntime> _requests;
            public StubActiveRequests(IReadOnlyList<ActiveRequestRuntime> requests) => _requests = requests;
            public IReadOnlyList<ActiveRequestRuntime> GetRequests() => _requests;
        }

        private static SalesSessionSetup Setup()
            => new SalesSessionSetup(1, "loc", Array.Empty<string>(), Array.Empty<string>());

        private static FakeConfigsService ConfigsWithRequests(int requestCount)
        {
            var configs = new FakeConfigsService();
            var requests = new List<RequestDefinitionConfig>(requestCount);
            for (var i = 0; i < requestCount; i++) requests.Add(SalesTestKit.RequestDef($"r{i + 1}"));
            configs.SetAll(requests);
            return configs;
        }

        private static int BuildCount(FakeConfigsService configs, CustomerTrafficResult result)
        {
            var spawner = new RegularCustomerSpawner(configs, new StubResolver(result));
            return spawner.BuildCustomers(Setup(), SalesTestKit.FastTuning(), new FakeSalesRandom()).Count;
        }

        private static IReadOnlyList<Customer> BuildCustomers(
            IReadOnlyList<ActiveRequestRuntime> requests,
            CustomerTrafficResult result)
        {
            var configs = new FakeConfigsService();
            var spawner = new RegularCustomerSpawner(
                configs,
                new StubResolver(result),
                new StubActiveRequests(requests));

            var tuning = SalesTestKit.FastTuning();
            return spawner.BuildCustomers(Setup(), tuning, new FakeSalesRandom());
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

        [Test]
        public void NonHardDay_RequestFloor_RaisesCount()
        {
            LogAssert.Expect(LogType.Log, new Regex(@"\[Sales\.Traffic\] spawnerFloor day=1 resolvedRegular=2 requestCount=5 finalRegular=5 applied=true"));
            var count = BuildCount(ConfigsWithRequests(5),
                new CustomerTrafficResult(2, 2, isHardOverride: false, breakdown: null));
            Assert.AreEqual(5, count); // floor(2, 5) = 5
        }

        [Test]
        public void NonHardDay_ResolvedAboveRequests_KeepsResolvedCount()
        {
            var count = BuildCount(ConfigsWithRequests(2),
                new CustomerTrafficResult(8, 8, isHardOverride: false, breakdown: null));
            Assert.AreEqual(8, count);
        }

        [Test]
        public void HardOverrideDay_SkipsFloor_StaysExact()
        {
            LogAssert.Expect(LogType.Warning, new Regex(@"\[Sales\.Traffic\] warning day=1 hardOverride=true regularCount=3 requestFloor=5 applied=false"));
            var count = BuildCount(ConfigsWithRequests(5),
                new CustomerTrafficResult(3, 3, isHardOverride: true, breakdown: null));
            Assert.AreEqual(3, count); // floor skipped despite 5 requests
        }

        [Test]
        public void ConditionsMode_FirstNCustomersReceiveActiveRequests()
        {
            var requests = new[]
            {
                SalesTestKit.ActiveRequest("r1"),
                SalesTestKit.ActiveRequest("r2")
            };

            var customers = BuildCustomers(
                requests,
                new CustomerTrafficResult(4, 4, isHardOverride: false, breakdown: null));
            var sink = DriveAll(customers);

            CollectionAssert.AreEqual(new[] { "cust_1", "cust_2" }, sink.ActiveStarted.Select(x => x.customer.Id).ToArray());
        }

        [Test]
        public void HardOverrideBelowConditionRequests_DoesNotExceedExactCount()
        {
            var requests = new[]
            {
                SalesTestKit.ActiveRequest("r1"),
                SalesTestKit.ActiveRequest("r2"),
                SalesTestKit.ActiveRequest("r3")
            };

            LogAssert.Expect(LogType.Warning, new Regex(@"\[Sales\.Traffic\] warning day=1 hardOverride=true regularCount=2 requestFloor=3 applied=false"));
            var customers = BuildCustomers(
                requests,
                new CustomerTrafficResult(2, 2, isHardOverride: true, breakdown: null));
            var sink = DriveAll(customers);

            Assert.AreEqual(2, customers.Count);
            CollectionAssert.AreEqual(new[] { "cust_1", "cust_2" }, sink.ActiveStarted.Select(x => x.customer.Id).ToArray());
        }
    }
}
