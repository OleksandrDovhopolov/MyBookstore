using System.Collections.Generic;
using Book.Sell.Domain;
using Book.Sell.Domain.Steps;

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
        private const int MaxPassiveAttempts = 3;

        public IReadOnlyList<Customer> BuildCustomers(SalesSessionSetup setup, SalesTuning tuning, ISalesRandom random)
        {
            var count = tuning.BaseCustomers;
            var customers = new List<Customer>(count);

            for (var i = 0; i < count; i++)
            {
                var steps = new List<ICustomerStep>
                {
                    new ApproachStep(RandomApproachDuration(tuning, random))
                };

                var passiveAttempts = random.Range(MinPassiveAttempts, MaxPassiveAttempts + 1);
                for (var p = 0; p < passiveAttempts; p++)
                    steps.Add(new PassivePurchaseStep());

                steps.Add(new CompletePurchaseStep());
                steps.Add(new LeaveStep(RandomLeaveDuration(tuning, random)));

                customers.Add(new Customer($"passive_only_{i + 1}", steps));
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
