namespace Book.Sell.API
{
    /// <summary>
    /// One contributor's effect on the day's regular customer count. This iteration uses percent-only
    /// modifiers; contributions are summed by the resolver: <c>raw = baseline * (1 + Σ PercentDelta)</c>.
    /// <see cref="Reason"/> is a debug/forecast label (e.g. "location: downtown +20%").
    /// See docs/INPROGRESS/CUSTOMER_TRAFFIC_COUNT_SYSTEM.md.
    /// </summary>
    public readonly struct CustomerTrafficContribution
    {
        /// <summary>Additive percent, e.g. +0.20 for +20% or -0.05 for -5%. Neutral = 0.</summary>
        public float PercentDelta { get; }

        /// <summary>Stable contributor family id for parser-friendly logs, e.g. "location" or "decor".</summary>
        public string Source { get; }

        /// <summary>Stable source object id for parser-friendly logs, e.g. location id or decor group id.</summary>
        public string Id { get; }

        /// <summary>Human-readable reason for logs / the future Preparation forecast UI.</summary>
        public string Reason { get; }

        public CustomerTrafficContribution(float percentDelta, string reason)
            : this(percentDelta, source: null, id: null, reason: reason)
        {
        }

        public CustomerTrafficContribution(float percentDelta, string source, string id, string reason)
        {
            PercentDelta = percentDelta;
            Source = source;
            Id = id;
            Reason = reason;
        }
    }
}
