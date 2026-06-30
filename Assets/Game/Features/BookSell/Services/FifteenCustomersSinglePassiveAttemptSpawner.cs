using System.Collections.Generic;
using Book.Sell.Domain;
using Book.Sell.Domain.Steps;

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
            var customers = new List<Customer>(CustomerCount);
            for (var i = 0; i < CustomerCount; i++)
            {
                customers.Add(CustomerPlanBuilder.Build(
                    $"single_passive_{i + 1}", tuning, random,
                    buildMiddle: () => new ICustomerStep[] { new PassivePurchaseStep() }));
            }

            return customers;
        }
    }
}
