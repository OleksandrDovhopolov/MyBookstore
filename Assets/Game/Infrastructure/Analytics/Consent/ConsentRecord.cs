using System;

namespace Analytics
{
    /// <summary>
    /// A persisted consent decision. <see cref="PolicyVersion"/> records which version of the policy the
    /// player was shown, so a later policy bump can invalidate the decision.
    /// </summary>
    public readonly struct ConsentRecord
    {
        public ConsentRecord(
            int policyVersion,
            bool analytics,
            bool attribution,
            bool personalizedAds,
            long decidedAtUtcTicks)
        {
            PolicyVersion = policyVersion;
            Analytics = analytics;
            Attribution = attribution;
            PersonalizedAds = personalizedAds;
            DecidedAtUtcTicks = decidedAtUtcTicks;
        }

        public int PolicyVersion { get; }
        public bool Analytics { get; }
        public bool Attribution { get; }
        public bool PersonalizedAds { get; }

        /// <summary>UTC ticks of the decision, or 0 when unknown (e.g. a corrupt stored value).</summary>
        public long DecidedAtUtcTicks { get; }

        public DateTime? DecidedAtUtc => DecidedAtUtcTicks > 0
            ? new DateTime(DecidedAtUtcTicks, DateTimeKind.Utc)
            : (DateTime?)null;
    }
}
