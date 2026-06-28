using System;
using System.Collections.Generic;
using Book.Sell.Domain;
using Book.Sell.Domain.Steps;
using Game.Configs;
using Game.Configs.Models;

namespace Book.Sell.Services
{
    /// <summary>
    /// Runtime scenario spawner for active-request-only days: 3..5 customers, each with exactly one
    /// active request (the recommendation minigame) and no passive purchases.
    /// Plan: Approach -> ActiveRequest(request) -> CompletePurchase -> Leave.
    /// Requests are drawn in config order and cycled when there are fewer configs than customers.
    /// </summary>
    public sealed class ActiveRequestsOnlyCustomerSpawner : ICustomerSpawner
    {
        private const int MinCustomers = 3;
        private const int MaxCustomers = 5;

        private readonly IConfigsService _configs;

        public ActiveRequestsOnlyCustomerSpawner(IConfigsService configs)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
        }

        public IReadOnlyList<Customer> BuildCustomers(SalesSessionSetup setup, SalesTuning tuning, ISalesRandom random)
        {
            var requests = _configs.GetAll<RequestConfig>();
            var count = random.Range(MinCustomers, MaxCustomers + 1);   // 3..5 inclusive
            var customers = new List<Customer>(count);

            for (var i = 0; i < count; i++)
            {
                var steps = new List<ICustomerStep>
                {
                    new ApproachStep(RandomApproachDuration(tuning, random))
                };

                if (requests.Count > 0)
                    steps.Add(new ActiveRequestStep(requests[i % requests.Count]));

                steps.Add(new CompletePurchaseStep());
                steps.Add(new LeaveStep(RandomLeaveDuration(tuning, random)));

                customers.Add(new Customer($"active_only_{i + 1}", steps));
            }

            return customers;
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
