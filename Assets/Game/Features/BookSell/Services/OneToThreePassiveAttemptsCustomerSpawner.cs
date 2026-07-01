using System.Collections.Generic;
using Book.Sell.Domain;

namespace Book.Sell.Services
{
    /// <summary>
    /// Runtime scenario spawner for passive-only sales with selected books:
    /// BaseCustomers customers, each with 1..3 passive purchase attempts and no active requests.
    /// Plan: Approach -> Passive x N -> CompletePurchase -> Leave.
    /// </summary>
    public sealed class OneToThreePassiveAttemptsCustomerSpawner : ICustomerSpawner
    {
        private const int MinPassiveAttempts = 1;
        private const int MaxPassiveAttempts = 5;

        public IReadOnlyList<Customer> BuildCustomers(SalesSessionSetup setup, SalesTuning tuning, ISalesRandom random)
        {
            //var count = tuning.BaseCustomers;
            var count = 10;
            var archetype = new PassiveAttemptsArchetype(MinPassiveAttempts, MaxPassiveAttempts);
            var customers = new List<Customer>(count);

            for (var i = 0; i < count; i++)
            {
                customers.Add(CustomerPlanBuilder.Build(
                    $"passive_only_{i + 1}", tuning, random,
                    buildMiddle: () => archetype.BuildMiddle(setup, tuning, random)));
            }

            return customers;
        }
    }
}
