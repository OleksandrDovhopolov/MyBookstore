using System.Collections.Generic;

namespace Book.Sell.API
{
    /// <summary>
    /// Collects <see cref="CustomerTrafficContribution"/>s from contributors. The resolver runs every
    /// contributor against one accumulator, then sums the percent deltas. Contributors should call
    /// <see cref="Add"/> only when they actually affect traffic (a neutral 0% is fine to skip).
    /// See docs/INPROGRESS/CUSTOMER_TRAFFIC_COUNT_SYSTEM.md.
    /// </summary>
    public sealed class CustomerTrafficAccumulator
    {
        private readonly List<CustomerTrafficContribution> _contributions = new();

        public IReadOnlyList<CustomerTrafficContribution> Contributions => _contributions;

        public void Add(CustomerTrafficContribution contribution) => _contributions.Add(contribution);

        public void Add(float percentDelta, string reason)
            => _contributions.Add(new CustomerTrafficContribution(percentDelta, reason));

        public void Add(float percentDelta, string source, string id, string reason)
            => _contributions.Add(new CustomerTrafficContribution(percentDelta, source, id, reason));

        /// <summary>Sum of all collected percent deltas.</summary>
        public float TotalPercentDelta()
        {
            var sum = 0f;
            for (var i = 0; i < _contributions.Count; i++)
                sum += _contributions[i].PercentDelta;
            return sum;
        }
    }
}
