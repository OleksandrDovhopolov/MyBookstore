using System;
using System.Collections.Generic;
using Book.Sell.Domain;
using Game.Configs;

namespace Book.Sell.Services
{
    /// <summary>
    /// Runtime scenario spawner: 7 customers. Every customer makes 1..5 passive purchases.
    /// Plan: Approach -> Passive x[1..5] -> CompletePurchase -> Leave.
    /// (The "first N also make an active request after passive" mix is currently disabled — see below.)
    /// </summary>
    public sealed class TenCustomersThreeActiveAfterPassiveSpawner : ICustomerSpawner
    {
        private const int CustomerCount = 7;
        private const int ActiveCustomerCount = 3;
        private const int MinPassiveAttempts = 1;
        private const int MaxPassiveAttempts = 5;

        private readonly IConfigsService _configs;
        private readonly ICustomerProfileProvider _profiles;

        public TenCustomersThreeActiveAfterPassiveSpawner(IConfigsService configs, ICustomerProfileProvider profiles)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        }

        public IReadOnlyList<Customer> BuildCustomers(SalesSessionSetup setup, SalesTuning tuning, ISalesRandom random)
        {
            // Legacy behavior: the active-after-passive mix is currently disabled, but this draw is kept to
            // preserve the seeded random stream. When re-enabled, compose like TenBetween:
            //   var requests = _configs.GetAll<RequestConfig>();
            //   var activeIndices = PickActiveIndices(CustomerCount, ActiveCustomerCount, random);
            //   var activeOrder = 0;
            //   for active index with requests.Count > 0 -> a "passive then active" archetype using
            //     requests[activeOrder++ % requests.Count].
            // activeIndices / activeOrder / requests belong to that disabled block and are intentionally
            // absent from runtime code; only the draw below runs to keep the stream identical.
            _ = PickActiveIndices(CustomerCount, ActiveCustomerCount, random);

            var archetype = new PassiveAttemptsArchetype(MinPassiveAttempts, MaxPassiveAttempts);
            var customers = new List<Customer>(CustomerCount);
            for (var i = 0; i < CustomerCount; i++)
            {
                customers.Add(CustomerPlanBuilder.Build(
                    $"cust_{i + 1}", tuning, random,
                    buildMiddle: () => archetype.BuildMiddle(setup, tuning, random),
                    buildProfile: () => _profiles.Create(setup, random)));
            }

            return customers;
        }

        // Picks <paramref name="count"/> distinct customer indices in [0, total) via a partial
        // Fisher–Yates shuffle, so the active requests land on random customers (not always the first ones).
        private static HashSet<int> PickActiveIndices(int total, int count, ISalesRandom random)
        {
            var indices = new int[total];
            for (var i = 0; i < total; i++) indices[i] = i;

            var picks = count < total ? count : total;
            for (var i = 0; i < picks; i++)
            {
                var j = random.Range(i, total);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }

            var result = new HashSet<int>();
            for (var i = 0; i < picks; i++) result.Add(indices[i]);
            return result;
        }
    }
}
