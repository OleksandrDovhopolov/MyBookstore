using System;
using System.Collections.Generic;
using Book.Sell.Domain;
using Game.Configs;
using Game.Configs.Models;
using UnityEngine;

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
        private const string TrafficLogTag = "[Sales.Traffic]";

        private readonly ICustomerTrafficResolver _trafficResolver;
        private readonly IActiveRequestRuntimeProvider _activeRequests;

        public RegularCustomerSpawner(IConfigsService configs, ICustomerTrafficResolver trafficResolver)
            : this(configs, trafficResolver, new ConfigActiveRequestRuntimeProvider(configs, new BookConditionRequestEvaluator()))
        {
        }

        public RegularCustomerSpawner(
            IConfigsService configs,
            ICustomerTrafficResolver trafficResolver,
            IActiveRequestRuntimeProvider activeRequests)
        {
            if (configs == null) throw new ArgumentNullException(nameof(configs));
            _trafficResolver = trafficResolver ?? throw new ArgumentNullException(nameof(trafficResolver));
            _activeRequests = activeRequests ?? throw new ArgumentNullException(nameof(activeRequests));
        }

        public IReadOnlyList<Customer> BuildCustomers(SalesSessionSetup setup, SalesTuning tuning, ISalesRandom random)
        {
            var result = _trafficResolver.Resolve(setup, tuning);
            var requests = _activeRequests.GetRequests(tuning.ActiveRequestMode);
            var requestCount = requests.Count;

            // Request-count floor: every active request must get a customer, but not on hard-override
            // days, whose count is exact by design. The warning keeps that conflict visible in logs.
            var count = result.FinalCount;
            if (!result.IsHardOverride)
            {
                var floored = Math.Max(count, requestCount);
                if (floored != count)
                {
                    Debug.Log($"{TrafficLogTag} spawnerFloor day={setup.Day} " +
                              $"resolvedRegular={count} requestCount={requestCount} " +
                              $"finalRegular={floored} applied=true");
                }

                count = floored;
            }
            else if (requestCount > count)
            {
                Debug.LogWarning($"{TrafficLogTag} warning day={setup.Day} " +
                                 $"hardOverride=true regularCount={count} " +
                                 $"requestFloor={requestCount} applied=false");
            }

            if (count < 0) count = 0;

            var passive = new PassiveAttemptsArchetype(tuning.MinPassiveAttempts, tuning.MaxPassiveAttempts);
            var customers = new List<Customer>(count);
            for (var i = 0; i < count; i++)
            {
                var archetype = i < requests.Count
                    ? (ICustomerArchetype)new PassiveActivePassiveArchetype(requests[i], 1, 1)
                    : passive;

                customers.Add(CustomerPlanBuilder.Build(
                    $"cust_{i + 1}", tuning, random,
                    buildMiddle: () => archetype.BuildMiddle(setup, tuning, random)));
            }

            return customers;
        }
    }
}
