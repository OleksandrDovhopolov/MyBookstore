namespace Book.Sell.Domain
{
    /// <summary>How the resolver turns the raw (fractional) customer count into an integer.</summary>
    public enum TrafficRounding
    {
        /// <summary>Round to nearest; ties go away from zero. Default — avoids "small positive modifier felt broken".</summary>
        NearestAwayFromZero = 0,
        Floor = 1,
        Ceil = 2
    }

    /// <summary>
    /// Global (day-independent) knobs for customer traffic: the fallback baseline used when a day has no
    /// explicit count, the final min/max clamp, and the rounding policy. Per-day counts and the
    /// <c>applyModifiers</c> hard-override live in <c>DayConfig</c> (days.json), not here.
    /// Pure domain so the resolver and its tests stay Unity-free; produced from <c>SalesTrafficConfig</c>.
    /// See docs/INPROGRESS/CUSTOMER_TRAFFIC_COUNT_SYSTEM.md.
    /// </summary>
    public sealed class SalesTrafficSettings
    {
        /// <summary>Baseline count for a day with no explicit <c>DayConfig.CustomerCount</c>.</summary>
        public int DefaultCustomerCount { get; set; } = 10;

        /// <summary>Final lower clamp (non-hard-override days).</summary>
        public int MinCustomerCount { get; set; } = 0;

        /// <summary>Final upper clamp (non-hard-override days).</summary>
        public int MaxCustomerCount { get; set; } = 50;

        /// <summary>
        /// Baseline number of customers arriving with an active request, for a day with no explicit
        /// <c>DayConfig.ActiveRequestCount</c>. The requests catalog (requests.json) is a POOL to draw
        /// from — its size must never decide how many requests a day runs.
        /// </summary>
        public int DefaultActiveRequestCount { get; set; } = 1;

        /// <summary>Final lower clamp for the active-request count (non-hard-override days).</summary>
        public int MinActiveRequestCount { get; set; } = 0;

        /// <summary>Final upper clamp for the active-request count (non-hard-override days).</summary>
        public int MaxActiveRequestCount { get; set; } = 50;

        public TrafficRounding Rounding { get; set; } = TrafficRounding.NearestAwayFromZero;
    }
}
