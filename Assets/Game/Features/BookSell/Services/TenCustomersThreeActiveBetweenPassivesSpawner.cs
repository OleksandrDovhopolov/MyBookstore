using System;
using System.Collections.Generic;
using Book.Sell.Domain;
using Book.Sell.Domain.Steps;
using Game.Configs;
using Game.Configs.Models;

namespace Book.Sell.Services
{
    /// <summary>
    /// Runtime scenario spawner: 10 customers. Every customer makes 1..2 passive purchases first; the first
    /// <see cref="ActiveCustomerCount"/> of them then make one active recommendation purchase followed by one
    /// more passive purchase, and finally leave.
    /// Plan (active customers):  Approach -> Passive x[1..2] -> ActiveRequest -> Passive x1 -> CompletePurchase -> Leave.
    /// Plan (other customers):   Approach -> Passive x[1..2] -> CompletePurchase -> Leave.
    /// Active requests are drawn in config order and cycled if there are fewer configs than active customers.
    /// </summary>
    public sealed class TenCustomersThreeActiveBetweenPassivesSpawner : ICustomerSpawner
    {
        private const int CustomerCount = 10;
        private const int ActiveCustomerCount = 3;
        private const int MinPassiveAttempts = 1;
        private const int MaxPassiveAttempts = 2;

        private readonly IConfigsService _configs;

        public TenCustomersThreeActiveBetweenPassivesSpawner(IConfigsService configs)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
        }

        public IReadOnlyList<Customer> BuildCustomers(SalesSessionSetup setup, SalesTuning tuning, ISalesRandom random)
        {
            var requests = _configs.GetAll<RequestConfig>();
            // Pre-loop random draw — kept here verbatim (consumes the random stream before the customer loop).
            var activeIndices = PickActiveIndices(CustomerCount, ActiveCustomerCount, random);
            var customers = new List<Customer>(CustomerCount);
            var activeOrder = 0;

            for (var i = 0; i < CustomerCount; i++)
            {
                var index = i;
                customers.Add(CustomerPlanBuilder.Build(
                    $"cust_{index + 1}", tuning, random,
                    buildMiddle: () =>
                    {
                        var middle = new List<ICustomerStep>();

                        var passiveAttempts = random.Range(MinPassiveAttempts, MaxPassiveAttempts + 1);
                        for (var p = 0; p < passiveAttempts; p++)
                            middle.Add(new PassivePurchaseStep());

                        if (activeIndices.Contains(index) && requests.Count > 0)
                        {
                            middle.Add(new ActiveRequestStep(requests[activeOrder++ % requests.Count]));
                            middle.Add(new PassivePurchaseStep());   // one more passive after the active recommendation
                        }

                        return middle;
                    }));
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
