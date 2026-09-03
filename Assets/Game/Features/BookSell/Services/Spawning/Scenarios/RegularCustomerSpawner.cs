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
    /// and the active-request count from <see cref="IActiveRequestCountResolver"/>. Active request slots are
    /// spread across the day, then each active customer draws a request matching its DesiredGenres profile.
    /// Wrapped by <see cref="ScriptedCustomerSpawner"/>, which can replace regular slots with scripted visits.
    /// </summary>
    public sealed class RegularCustomerSpawner : ICustomerSpawner
    {
        private const string TrafficLogTag = "[Sales.Traffic]";

        private static readonly IReadOnlyList<ActiveRequestRuntime> NoRequests = Array.Empty<ActiveRequestRuntime>();

        private readonly ICustomerTrafficResolver _trafficResolver;
        private readonly IActiveRequestRuntimeProvider _activeRequests;
        private readonly ICustomerProfileProvider _profiles;
        private readonly IActiveRequestCountResolver _requestCount;
        private readonly IActiveRequestSelectorFactory _selectorFactory;

        public RegularCustomerSpawner(IConfigsService configs, ICustomerTrafficResolver trafficResolver)
            : this(
                configs,
                trafficResolver,
                new ConfigActiveRequestRuntimeProvider(
                    configs,
                    new BookConditionRequestEvaluator(),
                    new ConditionActiveRequestGenreResolver()),
                profileProvider: null,
                requestCountResolver: null,
                selectorFactory: null)
        {
        }

        public RegularCustomerSpawner(
            IConfigsService configs,
            ICustomerTrafficResolver trafficResolver,
            IActiveRequestRuntimeProvider activeRequests,
            ICustomerProfileProvider profileProvider = null,
            IActiveRequestCountResolver requestCountResolver = null,
            IActiveRequestSelectorFactory selectorFactory = null)
        {
            if (configs == null) throw new ArgumentNullException(nameof(configs));
            _trafficResolver = trafficResolver ?? throw new ArgumentNullException(nameof(trafficResolver));
            _activeRequests = activeRequests ?? throw new ArgumentNullException(nameof(activeRequests));
            _profiles = profileProvider;
            _selectorFactory = selectorFactory ?? new ProfileMatchedRequestSelectorFactory();

            // Null means default knobs with no contributors. Never fall back to "pool size" here: that is
            // the bug this resolver exists to prevent.
            _requestCount = requestCountResolver ?? new ActiveRequestCountResolver(
                new SalesTrafficSettings(), configs, Array.Empty<IActiveRequestCountContributor>());
        }

        public IReadOnlyList<Customer> BuildCustomers(SalesSessionSetup setup, SalesTuning tuning, ISalesRandom random)
        {
            var traffic = _trafficResolver.Resolve(setup, tuning);
            var customerCount = Math.Max(0, traffic.FinalCount);

            var pool = _activeRequests.GetRequests() ?? NoRequests;
            var requestCount = ResolveActiveRequestCount(setup, tuning, customerCount, pool.Count);

            var activeSlots = PickActiveSlots(customerCount, requestCount, random);
            var selector = _selectorFactory.CreateForDay(pool);

            var passive = new PassiveAttemptsArchetype(tuning.MinPassiveAttempts, tuning.MaxPassiveAttempts);
            var customers = new List<Customer>(customerCount);

            for (var i = 0; i < customerCount; i++)
            {
                var profile = _profiles?.Create(setup, random) ?? CustomerProfile.Empty;
                ICustomerArchetype archetype = passive;

                if (activeSlots != null && activeSlots.Contains(i))
                {
                    var request = selector.Draw(profile, random);
                    if (request != null)
                        archetype = new PassiveActivePassiveArchetype(request, 1, 1);
                }

                customers.Add(CustomerPlanBuilder.Build(
                    $"cust_{i + 1}", tuning, random,
                    buildMiddle: () => archetype.BuildMiddle(setup, tuning, random),
                    profile: profile));
            }

            return customers;
        }

        // Demand from the day config, capped by what the day can physically serve. Requests never raise
        // the customer count.
        private int ResolveActiveRequestCount(SalesSessionSetup setup, SalesTuning tuning, int customerCount, int poolCount)
        {
            var demand = Math.Max(0, _requestCount.Resolve(setup, tuning).FinalCount);
            var capped = Math.Min(demand, Math.Min(customerCount, poolCount));

            if (capped < demand)
            {
                Debug.LogWarning($"{TrafficLogTag} requestCap day={setup.Day} " +
                                 $"demand={demand} customers={customerCount} pool={poolCount} " +
                                 $"final={capped} - the day asks for more active requests than it can serve.");
            }

            return capped;
        }

        // Which customer slots arrive with a request. Consumes exactly `requestCount` draws
        // (0 when every customer, or no customer, is active).
        private static HashSet<int> PickActiveSlots(int customerCount, int requestCount, ISalesRandom random)
        {
            if (requestCount <= 0 || customerCount <= 0) return null;

            if (requestCount >= customerCount)
            {
                var all = new HashSet<int>();
                for (var i = 0; i < customerCount; i++) all.Add(i);
                return all;
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
