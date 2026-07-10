using System;
using System.Collections.Generic;
using Book.Sell.Domain;
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
            var count = random.Range(MinCustomers, MaxCustomers + 1);   // 3..5 inclusive (pre-loop draw)
            var customers = new List<Customer>(count);

            for (var i = 0; i < count; i++)
            {
                var request = requests.Count > 0 ? requests[i % requests.Count] : null;
                var archetype = new ActiveRequestArchetype(request);
                customers.Add(CustomerPlanBuilder.Build(
                    $"active_only_{i + 1}", tuning, random,
                    buildMiddle: () => archetype.BuildMiddle(setup, tuning, random)));
            }

            return customers;
        }
    }
}
