namespace Book.Sell.Domain
{
    /// <summary>
    /// Timing knobs for the sales simulation. Constants for the MVP; later sourced from config.
    /// Tests override these for fast, deterministic ticking.
    /// </summary>
    public sealed class SalesTuning
    {
        /// <summary>Fallback fixed approach duration used by manually constructed approach steps.</summary>
        public float ApproachDuration { get; set; } = 3f;

        /// <summary>
        /// Minimum seconds a spawned customer spends walking up before buying can start. Also the lower
        /// clamp applied to the distance-derived duration (see <see cref="ApproachSpeed"/>).
        /// </summary>
        public float MinApproachDuration { get; set; } = 3f;

        /// <summary>
        /// Maximum seconds a spawned customer spends walking up before buying can start. Also the upper
        /// clamp applied to the distance-derived duration (see <see cref="ApproachSpeed"/>).
        /// </summary>
        public float MaxApproachDuration { get; set; } = 6f;

        /// <summary>
        /// Walking speed in world units per second used by the View to turn the real entry→lane distance
        /// into an approach duration, so customers entering from the far side don't appear to sprint.
        /// The result is clamped to [<see cref="MinApproachDuration"/>, <see cref="MaxApproachDuration"/>].
        /// <c>&lt;= 0</c> falls back to the random duration drawn when the plan was built.
        /// </summary>
        public float ApproachSpeed { get; set; } = 2.5f;

        /// <summary>Seconds a customer "thinks" before each passive purchase attempt.</summary>
        public float BrowseDuration { get; set; } = 3.5f;

        /// <summary>Seconds between targeting a book (reserve) and committing the passive sale.</summary>
        public float PassiveCommitDelay { get; set; } = 0.6f;

        /// <summary>Seconds the failed-passive HUD state is held before the customer closes the visit.</summary>
        public float PassiveFailureFeedbackDuration { get; set; } = 1.0f;

        /// <summary>Seconds the "bought book" HUD state is held after a passive sale, so it stays visible
        /// before the next attempt's "Choosing" overwrites it.</summary>
        public float PassiveSaleFeedbackDuration { get; set; } = 1.0f;

        /// <summary>Chance to insert an optional comment step after a successful passive sale. A value of
        /// 0 keeps the old seeded random stream because no roll is consumed.</summary>
        public float PassiveSaleCommentChance { get; set; } = 0.35f;

        /// <summary>Seconds an inserted customer comment is held before the plan advances.</summary>
        public float CommentDuration { get; set; } = 1.2f;

        /// <summary>Seconds the purchase-completion animation holds (CompletePurchaseStep) when the
        /// customer bought at least one book passively.</summary>
        public float CompletePurchaseDuration { get; set; } = 1.5f;

        /// <summary>Seconds a customer spends "leaving" (walking away) before becoming Done.
        /// Aligned with the visual exit move so the domain finishes roughly as the visual despawns.</summary>
        public float LeaveDuration { get; set; } = 3f;

        /// <summary>Minimum seconds a spawned customer spends walking away when leaving. Also the lower
        /// clamp on the distance-derived duration (see <see cref="LeaveSpeed"/>).</summary>
        public float MinLeaveDuration { get; set; } = 3f;

        /// <summary>Maximum seconds a spawned customer spends walking away when leaving. Also the upper
        /// clamp on the distance-derived duration (see <see cref="LeaveSpeed"/>).</summary>
        public float MaxLeaveDuration { get; set; } = 6f;

        /// <summary>Walk-away speed in world units per second; the leave counterpart of
        /// <see cref="ApproachSpeed"/>. <c>&lt;= 0</c> falls back to the drawn duration.</summary>
        public float LeaveSpeed { get; set; } = 2.5f;

        /// <summary>Seconds between sequential customer spawns ("one by one").</summary>
        public float SpawnInterval { get; set; } = 5.0f;

        /// <summary>Maximum customers present on the floor at once (spawned and not yet Done).
        /// Spawning is gated until a slot frees. <c>&lt;= 0</c> means no limit.</summary>
        public int MaxConcurrentCustomers { get; set; } = 9;

        /// <summary>Minimum passive purchase attempts a regular customer makes per visit.</summary>
        public int MinPassiveAttempts { get; set; } = 1;

        /// <summary>Maximum passive purchase attempts a regular customer makes per visit. Independent of
        /// shelf size — the count is drawn in <c>[MinPassiveAttempts, MaxPassiveAttempts]</c> regardless of
        /// how many books are on the shelf.</summary>
        public int MaxPassiveAttempts { get; set; } = 6;

        /// <summary>How many genres a customer's passive desire profile holds (requested-genre model).
        /// Clamped to the catalog genres by the profile provider.</summary>
        public int PassiveRequestGenreCount { get; set; } = 2;

        /// <summary>Target share of passive profile slots filled from LocationConfig.DemandGenres when the
        /// authored location has at least <see cref="NarrowDemandGenreThreshold"/> demand genres.</summary>
        public double PassiveDemandRequestShare { get; set; } = 0.70d;

        /// <summary>Target share of passive profile slots filled from LocationConfig.DemandGenres when the
        /// authored location has fewer than <see cref="NarrowDemandGenreThreshold"/> demand genres.</summary>
        public double PassiveDemandRequestShareNarrow { get; set; } = 0.50d;

        /// <summary>Authored LocationConfig.DemandGenres count that switches passive demand from narrow to
        /// wide share.</summary>
        public int NarrowDemandGenreThreshold { get; set; } = 3;
    }
}
