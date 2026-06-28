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
                customers.Add(new Customer($"single_passive_{i + 1}", new ICustomerStep[]
                {
                    new ApproachStep(RandomApproachDuration(tuning, random)),
                    new PassivePurchaseStep(),
                    new CompletePurchaseStep(),
                    new LeaveStep(RandomLeaveDuration(tuning, random))
                }));
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
