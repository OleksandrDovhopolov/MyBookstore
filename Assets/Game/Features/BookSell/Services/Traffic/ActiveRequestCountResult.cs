using System.Collections.Generic;
using Book.Sell.API;

namespace Book.Sell.Services
{
    /// <summary>
    /// Outcome of an active-request-count resolve: how many of the day's customers should arrive with a
    /// scripted request, plus the baseline and the per-contributor breakdown for logs / a future
    /// Preparation forecast UI. Mirrors <see cref="CustomerTrafficResult"/>.
    ///
    /// This is a DEMAND number only — it never raises the customer count. The spawner clamps it to the
    /// resolved customer count and to the request catalog size.
    /// </summary>
    public sealed class ActiveRequestCountResult
    {
        /// <summary>Requested count (already clamped, unless <see cref="IsHardOverride"/>).</summary>
        public int FinalCount { get; }

        /// <summary>Day baseline before modifiers.</summary>
        public int Baseline { get; }

        /// <summary>
        /// True when the day used <c>applyModifiers = false</c>: <see cref="FinalCount"/> is exact — no
        /// modifiers and no min/max clamp. The spawner still applies the physical caps (customers, pool).
        /// </summary>
        public bool IsHardOverride { get; }

        /// <summary>Contributions applied (empty on hard-override days).</summary>
        public IReadOnlyList<CustomerTrafficContribution> Breakdown { get; }

        public ActiveRequestCountResult(
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
