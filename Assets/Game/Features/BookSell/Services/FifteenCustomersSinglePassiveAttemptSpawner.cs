using System.Collections.Generic;
using Book.Sell.Domain;

namespace Book.Sell.Services
{
    /// <summary>
    /// Runtime scenario spawner for checking passive failures with an empty shelf:
    /// 15 customers, each with exactly one passive purchase attempt and no active requests.
    /// Plan: Approach -> Passive -> CompletePurchase -> Leave.
    /// </summary>
    public sealed class FifteenCustomersSinglePassiveAttemptSpawner : ICustomerSpawner
    {
        private const int CustomerCount = 15;

        public IReadOnlyList<Customer> BuildCustomers(SalesSessionSetup setup, SalesTuning tuning, ISalesRandom random)
        {
            // (1,1) builds exactly one passive without consuming random — parity with the original inline plan.
            var archetype = new PassiveAttemptsArchetype(1, 1);
            var customers = new List<Customer>(CustomerCount);
            for (var i = 0; i < CustomerCount; i++)
            {
                customers.Add(CustomerPlanBuilder.Build(
                    $"single_passive_{i + 1}", tuning, random,
                    buildMiddle: () => archetype.BuildMiddle(setup, tuning, random)));
            }

            return customers;
        }
    }
}
