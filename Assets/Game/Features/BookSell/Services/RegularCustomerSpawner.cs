using System;
using System.Collections.Generic;
using Book.Sell.Domain;
using Game.Configs;
using Game.Configs.Models;

namespace Book.Sell.Services
{
    /// <summary>
    /// Production base spawner: sources the regular customer count from <see cref="ICustomerTrafficResolver"/>
    /// instead of owning it. Composition (each customer's plan) is the same passive-attempts shape the
    /// stub spawners use. Wrapped by <see cref="QuestSchedulingCustomerSpawner"/>, which prepends quest
    /// characters. See docs/INPROGRESS/CUSTOMER_TRAFFIC_COUNT_SYSTEM.md.
    /// </summary>
    public sealed class RegularCustomerSpawner : ICustomerSpawner
    {
        private const int MaxExtraPassivePerSide = 2;   // k in 1..2

        private readonly IConfigsService _configs;
        private readonly ICustomerTrafficResolver _trafficResolver;

        public RegularCustomerSpawner(IConfigsService configs, ICustomerTrafficResolver trafficResolver)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _trafficResolver = trafficResolver ?? throw new ArgumentNullException(nameof(trafficResolver));
        }

        public IReadOnlyList<Customer> BuildCustomers(SalesSessionSetup setup, SalesTuning tuning, ISalesRandom random)
        {
            var result = _trafficResolver.Resolve(setup, tuning);

            // Request-count floor: every active RequestConfig must get a customer — but NOT on a hard-override
            // day, whose count is exact by design (config validation warns if that count is under-supplied).
            var count = result.FinalCount;
            if (!result.IsHardOverride)
                count = Math.Max(count, _configs.GetAll<RequestConfig>().Count);
            if (count < 0) count = 0;

            var archetype = new PassiveAttemptsArchetype(1, MaxExtraPassivePerSide);
            var customers = new List<Customer>(count);
            for (var i = 0; i < count; i++)
            {
                customers.Add(CustomerPlanBuilder.Build(
                    $"cust_{i + 1}", tuning, random,
                    buildMiddle: () => archetype.BuildMiddle(setup, tuning, random)));
            }

            return customers;
        }
    }
}
