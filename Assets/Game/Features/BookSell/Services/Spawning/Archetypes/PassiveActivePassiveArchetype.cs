using System;
using System.Collections.Generic;
using Book.Sell.Domain;
using Book.Sell.Domain.Steps;
using Game.Configs.Models;

namespace Book.Sell.Services
{
    /// <summary>
    /// Passive×N -> ActiveRequest -> Passive×1. Only the leading passive count consumes random
    /// (Range(min, max + 1), or min when min == max); the active step and trailing passive are fixed —
    /// parity with TenCustomersThreeActiveBetweenPassivesSpawner's active customers.
    /// Requires a non-null request (fail-fast); the spawner only builds this archetype when a request
    /// is available, so a null active step can never be produced.
    /// </summary>
    public sealed class PassiveActivePassiveArchetype : ICustomerArchetype
    {
        private readonly RequestConfig _request;
        private readonly int _min;
        private readonly int _max;

        public PassiveActivePassiveArchetype(RequestConfig request, int min, int max)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));
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
            steps.Add(new ActiveRequestStep(_request));
            steps.Add(new PassivePurchaseStep());
            return steps;
        }
    }
}
