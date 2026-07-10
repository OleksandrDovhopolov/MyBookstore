using System.Collections.Generic;
using Book.Sell.Domain;
using Book.Sell.Domain.Steps;

namespace Book.Sell.Services
{
    /// <summary>
    /// N passive purchase attempts. N = min when min == max (no random consumed — preserves
    /// FifteenCustomersSinglePassiveAttemptSpawner, which built exactly one passive without drawing),
    /// otherwise random.Range(min, max + 1). Returns a materialized list so the draw happens eagerly.
    /// </summary>
    public sealed class PassiveAttemptsArchetype : ICustomerArchetype
    {
        private readonly int _min;
        private readonly int _max;

        public PassiveAttemptsArchetype(int min, int max)
        {
            _min = min;
            _max = max;
        }

        public string Id => $"passive_{_min}_{_max}";

        public IEnumerable<ICustomerStep> BuildMiddle(SalesSessionSetup setup, SalesTuning tuning, ISalesRandom random)
        {
            var count = _min == _max ? _min : random.Range(_min, _max + 1);
            var steps = new List<ICustomerStep>(count);
            for (var i = 0; i < count; i++)
                steps.Add(new PassivePurchaseStep());
            return steps;
        }
    }
}
