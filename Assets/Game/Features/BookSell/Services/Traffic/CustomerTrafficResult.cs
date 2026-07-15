using System.Collections.Generic;
using Book.Sell.API;

namespace Book.Sell.Services
{
    /// <summary>
    /// Outcome of a customer-traffic resolve: the count the spawner builds, plus the baseline and the
    /// per-contributor breakdown for logs / a future Preparation forecast UI. Lives in Book.Sell (not
    /// Book.Sell.API) because the resolver signature depends on <c>SalesSessionSetup</c>/<c>SalesTuning</c>.
    /// See docs/INPROGRESS/CUSTOMER_TRAFFIC_COUNT_SYSTEM.md.
    /// </summary>
    public sealed class CustomerTrafficResult
    {
        /// <summary>Count the spawner should build (already clamped, unless <see cref="IsHardOverride"/>).</summary>
        public int FinalCount { get; }

        /// <summary>Day baseline before modifiers.</summary>
        public int Baseline { get; }

        /// <summary>
        /// True when the day used <c>applyModifiers = false</c>: <see cref="FinalCount"/> is exact — no
        /// modifiers, no min/max clamp, and the spawner must NOT apply the request-count floor.
        /// </summary>
        public bool IsHardOverride { get; }

        /// <summary>Contributions applied (empty on hard-override days).</summary>
        public IReadOnlyList<CustomerTrafficContribution> Breakdown { get; }

        public CustomerTrafficResult(
            int finalCount,
            int baseline,
            bool isHardOverride,
            IReadOnlyList<CustomerTrafficContribution> breakdown)
        {
            FinalCount = finalCount;
            Baseline = baseline;
            IsHardOverride = isHardOverride;
            Breakdown = breakdown ?? System.Array.Empty<CustomerTrafficContribution>();
        }
    }
}
