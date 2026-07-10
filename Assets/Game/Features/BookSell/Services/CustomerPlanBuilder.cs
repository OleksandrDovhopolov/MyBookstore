using System;
using System.Collections.Generic;
using Book.Sell.Domain;
using Book.Sell.Domain.Steps;

namespace Book.Sell.Services
{
    /// <summary>
    /// Centralizes the mandatory customer-plan skeleton (Approach -> middle -> CompletePurchase ->
    /// Leave) and the random approach/leave duration helpers that every spawner used to duplicate.
    /// Spawners now own only day composition (how many customers) and each customer's middle steps.
    ///
    /// Random-draw order is preserved exactly as the old spawners had it: approach -> middle -> leave
    /// -> profile. <paramref name="buildMiddle"/> is invoked EXACTLY ONCE, right after the approach
    /// duration is rolled and before the leave duration, because the middle may itself consume
    /// <see cref="ISalesRandom"/> and the order matters for seeded/queued streams. Spawner-level
    /// pre-loop draws (e.g. customer count, active-index shuffles) stay in the spawner, before Build.
    ///
    /// Phase 1 note: Build returns a full <see cref="Customer"/>, not a "plan" object. After the
    /// CustomerPlan phase (see docs/INPROGRESS/CUSTOMER_STEP_PIPELINE_REFACTOR.md) this may build or
    /// delegate to a CustomerPlan instead.
    /// </summary>
    public static class CustomerPlanBuilder
    {
        public static Customer Build(
            string id,
            SalesTuning tuning,
            ISalesRandom random,
            Func<IEnumerable<ICustomerStep>> buildMiddle,
            Func<CustomerProfile> buildProfile = null)
        {
            var steps = new List<ICustomerStep>
            {
                new ApproachStep(RandomInRange(tuning.MinApproachDuration, tuning.MaxApproachDuration, random)),
            };

            var middle = buildMiddle?.Invoke();
            if (middle != null)
                steps.AddRange(middle);

            steps.Add(new CompletePurchaseStep());
            steps.Add(new LeaveStep(RandomInRange(tuning.MinLeaveDuration, tuning.MaxLeaveDuration, random)));

            var profile = buildProfile?.Invoke();
            return new Customer(id, steps, profile);
        }

        /// <summary>Uniform value in [min, max], drawn from the sales random port. Moved verbatim from
        /// the spawners: swaps an inverted range and clamps the roll defensively.</summary>
        public static float RandomInRange(float min, float max, ISalesRandom random)
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
