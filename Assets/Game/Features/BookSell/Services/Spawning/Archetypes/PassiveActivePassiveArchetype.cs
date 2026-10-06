using System;
using System.Collections.Generic;
using Book.Sell.Domain;
using Book.Sell.Domain.Steps;

namespace Book.Sell.Services
{
    /// <summary>
    /// Passive×N -> ActiveRequest -> Passive×1. Only the leading passive count consumes random
    /// (Range(min, max + 1), or min when min == max); the active step and trailing passive are fixed.
    /// <para>
    /// Which request gets asked for is not decided here: the step draws it from the day's selector when the
    /// customer reaches the minigame, by which time the leading passive purchase has already taken its book
    /// off the shelf.
    /// </para>
    /// </summary>
    public sealed class PassiveActivePassiveArchetype : ICustomerArchetype
    {
        private readonly int _min;
        private readonly int _max;

        public PassiveActivePassiveArchetype(int min, int max)
        {
            _min = min;
            _max = max;
        }

        public string Id => "passive_active_passive";

        public IEnumerable<ICustomerStep> BuildMiddle(SalesSessionSetup setup, SalesTuning tuning, ISalesRandom random)
        {
            var count = _min == _max ? _min : random.Range(_min, _max + 1);
            var steps = new List<ICustomerStep>(count + 2);
            for (var i = 0; i < count; i++)
                steps.Add(new PassivePurchaseStep());
            steps.Add(new ActiveRequestStep());
            steps.Add(new PassivePurchaseStep());
            return steps;
        }
    }
}
