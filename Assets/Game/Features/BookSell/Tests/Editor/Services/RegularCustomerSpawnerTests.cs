using System;
using System.Collections.Generic;
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

        private static SalesSessionSetup Setup()
            => new SalesSessionSetup(1, "loc", Array.Empty<string>(), Array.Empty<string>());

        private static FakeConfigsService ConfigsWithRequests(int requestCount)
        {
            var configs = new FakeConfigsService();
            var requests = new List<RequestConfig>(requestCount);
            for (var i = 0; i < requestCount; i++) requests.Add(new RequestConfig { Id = $"r{i + 1}" });
            configs.SetAll(requests);
            return configs;
        }

        private static int BuildCount(FakeConfigsService configs, CustomerTrafficResult result)
        {
            var spawner = new RegularCustomerSpawner(configs, new StubResolver(result));
            return spawner.BuildCustomers(Setup(), SalesTestKit.FastTuning(), new FakeSalesRandom()).Count;
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
    }
}
