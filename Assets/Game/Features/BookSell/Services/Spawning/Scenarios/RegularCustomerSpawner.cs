using System;
using System.Collections.Generic;
using Book.Sell.API;
using Book.Sell.Domain;
using Game.Configs;
using Game.Configs.Models;
using UnityEngine;

namespace Book.Sell.Services
{
    /// <summary>
    /// Production base spawner: sources the regular customer count from <see cref="ICustomerTrafficResolver"/>
    /// and the active-request count from <see cref="IActiveRequestCountResolver"/> — it owns neither number.
    /// Composition (each customer's plan) is the same passive-attempts shape the stub spawners use. Wrapped
    /// by <see cref="QuestReplacingCustomerSpawner"/>, which can replace regular slots with quest characters.
    /// See docs/INPROGRESS/CUSTOMER_TRAFFIC_COUNT_SYSTEM.md.
    ///
    /// The request catalog is a POOL, never a schedule: demand comes from the day config, is capped by the
    /// customer count, and the requests are then drawn from the pool and spread across random customer slots.
    /// </summary>
    public sealed class RegularCustomerSpawner : ICustomerSpawner
    {
        private const string TrafficLogTag = "[Sales.Traffic]";

        private static readonly IReadOnlyList<ActiveRequestRuntime> NoRequests = Array.Empty<ActiveRequestRuntime>();

        private readonly ICustomerTrafficResolver _trafficResolver;
        private readonly IActiveRequestRuntimeProvider _activeRequests;
        private readonly ICustomerProfileProvider _profiles;
        private readonly IActiveRequestCountResolver _requestCount;

        public RegularCustomerSpawner(IConfigsService configs, ICustomerTrafficResolver trafficResolver)
            : this(
                configs,
                trafficResolver,
                new ConfigActiveRequestRuntimeProvider(configs, new BookConditionRequestEvaluator()),
                profileProvider: null,
                requestCountResolver: null)
        {
        }

        public RegularCustomerSpawner(
            IConfigsService configs,
            ICustomerTrafficResolver trafficResolver,
            IActiveRequestRuntimeProvider activeRequests,
            ICustomerProfileProvider profileProvider = null,
            IActiveRequestCountResolver requestCountResolver = null)
        {
            if (configs == null) throw new ArgumentNullException(nameof(configs));
            _trafficResolver = trafficResolver ?? throw new ArgumentNullException(nameof(trafficResolver));
            _activeRequests = activeRequests ?? throw new ArgumentNullException(nameof(activeRequests));
            _profiles = profileProvider;

            // Null → default knobs with no contributors. Never fall back to "pool size" here: that is the
            // exact bug this resolver exists to remove.
            _requestCount = requestCountResolver ?? new ActiveRequestCountResolver(
                new SalesTrafficSettings(), configs, Array.Empty<IActiveRequestCountContributor>());
        }

        public IReadOnlyList<Customer> BuildCustomers(SalesSessionSetup setup, SalesTuning tuning, ISalesRandom random)
        {
            var traffic = _trafficResolver.Resolve(setup, tuning);
            var customerCount = Math.Max(0, traffic.FinalCount);

            var pool = _activeRequests.GetRequests() ?? NoRequests;
            var requestCount = ResolveActiveRequestCount(setup, tuning, customerCount, pool.Count);

            // Spawner-level pre-loop draws. These MUST stay ahead of the Build loop: CustomerPlanBuilder
            // fixes each customer's own draw order (approach → middle → leave → profile), and interleaving
            // spawner draws into the loop would reshuffle every seeded/queued stream. Both helpers consume
            // ZERO draws when there is nothing to choose, so days without active requests keep the exact
            // stream they had before this resolver existed.
            var selected = SelectRequests(pool, requestCount, random);
            var activeSlots = PickActiveSlots(customerCount, requestCount, random);

            var passive = new PassiveAttemptsArchetype(tuning.MinPassiveAttempts, tuning.MaxPassiveAttempts);
            var customers = new List<Customer>(customerCount);
            var nextRequest = 0;

            for (var i = 0; i < customerCount; i++)
            {
                var archetype = activeSlots != null && activeSlots.Contains(i)
                    ? (ICustomerArchetype)new PassiveActivePassiveArchetype(selected[nextRequest++], 1, 1)
                    : passive;

                customers.Add(CustomerPlanBuilder.Build(
                    $"cust_{i + 1}", tuning, random,
                    buildMiddle: () => archetype.BuildMiddle(setup, tuning, random),
                    buildProfile: () => _profiles?.Create(setup, random) ?? CustomerProfile.Empty));
            }

            return customers;
        }

        // Demand from the day config, capped by what the day can physically serve. Requests never raise
        // the customer count — the old Math.Max(count, requestCount) floor let a 49-entry catalog force a
        // 49-customer day.
        private int ResolveActiveRequestCount(SalesSessionSetup setup, SalesTuning tuning, int customerCount, int poolCount)
        {
            var demand = Math.Max(0, _requestCount.Resolve(setup, tuning).FinalCount);
            var capped = Math.Min(demand, Math.Min(customerCount, poolCount));

            if (capped < demand)
            {
                Debug.LogWarning($"{TrafficLogTag} requestCap day={setup.Day} " +
                                 $"demand={demand} customers={customerCount} pool={poolCount} " +
                                 $"final={capped} — the day asks for more active requests than it can serve.");
            }

            return capped;
        }

        // Partial Fisher-Yates over the pool: consumes exactly `count` draws (0 when nothing is drawn).
        private static IReadOnlyList<ActiveRequestRuntime> SelectRequests(
            IReadOnlyList<ActiveRequestRuntime> pool, int count, ISalesRandom random)
        {
            if (count <= 0) return NoRequests;
            if (count >= pool.Count) return pool;   // whole pool → nothing to choose, no draw

            var indices = new List<int>(pool.Count);
            for (var i = 0; i < pool.Count; i++) indices.Add(i);

            var selected = new List<ActiveRequestRuntime>(count);
            for (var i = 0; i < count; i++)
            {
                var j = random.Range(i, indices.Count);
                (indices[i], indices[j]) = (indices[j], indices[i]);
                selected.Add(pool[indices[i]]);
            }

            return selected;
        }

        // Which customer slots arrive with a request. Previously the first N always did, so every active
        // customer showed up back-to-back at the start of the day. Consumes exactly `requestCount` draws
        // (0 when every customer — or no customer — is active).
        private static HashSet<int> PickActiveSlots(int customerCount, int requestCount, ISalesRandom random)
        {
            if (requestCount <= 0 || customerCount <= 0) return null;

            if (requestCount >= customerCount)
            {
                var all = new HashSet<int>();
                for (var i = 0; i < customerCount; i++) all.Add(i);
                return all;   // everyone is active → nothing to choose, no draw
            }

            var slots = new List<int>(customerCount);
            for (var i = 0; i < customerCount; i++) slots.Add(i);

            var picked = new HashSet<int>();
            for (var i = 0; i < requestCount; i++)
            {
                var j = random.Range(i, slots.Count);
                (slots[i], slots[j]) = (slots[j], slots[i]);
                picked.Add(slots[i]);
            }

            return picked;
        }
    }
}
