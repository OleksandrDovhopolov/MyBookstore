namespace Game.DayCycle.Results.Domain
{
    /// <summary>
    /// Persisted record of rewards already applied for a completed day. Drives idempotency:
    /// if a day id appears here, Results must not re-apply gold/reputation deltas on restart.
    /// </summary>
    public sealed class AppliedDayRewards
    {
        public int CompletedDay { get; set; }
        public int GoldDelta { get; set; }
        public int ReputationDelta { get; set; }
        public long AppliedAtUtcMs { get; set; }
    }
}
