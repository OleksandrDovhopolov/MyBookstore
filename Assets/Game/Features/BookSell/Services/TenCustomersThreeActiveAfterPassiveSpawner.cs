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
    /// <see cref="ActiveCustomerCount"/> of them also make one active recommendation purchase after the
    /// passive ones, then leave.
    /// Plan (active customers):  Approach -> Passive x[1..2] -> ActiveRequest -> CompletePurchase -> Leave.
    /// Plan (other customers):   Approach -> Passive x[1..2] -> CompletePurchase -> Leave.
    /// Active requests are drawn in config order and cycled if there are fewer configs than active customers.
    /// </summary>
    public sealed class TenCustomersThreeActiveAfterPassiveSpawner : ICustomerSpawner
    {
        private const int CustomerCount = 10;
        private const int ActiveCustomerCount = 3;
        private const int MinPassiveAttempts = 1;
        private const int MaxPassiveAttempts = 2;

        private readonly IConfigsService _configs;

        public TenCustomersThreeActiveAfterPassiveSpawner(IConfigsService configs)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
        }

        public IReadOnlyList<Customer> BuildCustomers(SalesSessionSetup setup, SalesTuning tuning, ISalesRandom random)
        {
            var requests = _configs.GetAll<RequestConfig>();
            var activeIndices = PickActiveIndices(CustomerCount, ActiveCustomerCount, random);
            var customers = new List<Customer>(CustomerCount);
            var activeOrder = 0;

            for (var i = 0; i < CustomerCount; i++)
            {
                var steps = new List<ICustomerStep> { new ApproachStep(RandomApproachDuration(tuning, random)) };

                var passiveAttempts = random.Range(MinPassiveAttempts, MaxPassiveAttempts + 1);
                for (var p = 0; p < passiveAttempts; p++)
                    steps.Add(new PassivePurchaseStep());

                if (activeIndices.Contains(i) && requests.Count > 0)
                    steps.Add(new ActiveRequestStep(requests[activeOrder++ % requests.Count]));

                steps.Add(new CompletePurchaseStep());
                steps.Add(new LeaveStep(RandomLeaveDuration(tuning, random)));

                customers.Add(new Customer($"cust_{i + 1}", steps));
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

        private static float RandomApproachDuration(SalesTuning tuning, ISalesRandom random)
            => RandomInRange(tuning.MinApproachDuration, tuning.MaxApproachDuration, random);

        private static float RandomLeaveDuration(SalesTuning tuning, ISalesRandom random)
            => RandomInRange(tuning.MinLeaveDuration, tuning.MaxLeaveDuration, random);

        private static float RandomInRange(float min, float max, ISalesRandom random)
        {
            if (max < min)
            {
                var tmp = min;
                min = max;
                max = tmp;
            }

            if (max <= min) return min;

            var roll = random.NextDouble();
            if (roll < 0d) roll = 0d;
            if (roll > 1d) roll = 1d;

            return min + (float)(roll * (max - min));
        }
    }
}
