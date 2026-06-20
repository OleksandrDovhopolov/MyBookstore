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

        /// <summary>Minimum seconds a spawned customer spends walking up before buying can start.</summary>
        public float MinApproachDuration { get; set; } = 3f;

        /// <summary>Maximum seconds a spawned customer spends walking up before buying can start.</summary>
        public float MaxApproachDuration { get; set; } = 6f;

        /// <summary>Seconds a customer "thinks" before each passive purchase attempt.</summary>
        public float BrowseDuration { get; set; } = 3.5f;

        /// <summary>Seconds between targeting a book (reserve) and committing the passive sale.</summary>
        public float PassiveCommitDelay { get; set; } = 0.6f;

        /// <summary>Seconds the failed-passive HUD state is held before the customer closes the visit.</summary>
        public float PassiveFailureFeedbackDuration { get; set; } = 1.0f;

        /// <summary>Seconds the purchase-completion animation holds (CompletePurchaseStep) when the
        /// customer bought at least one book passively.</summary>
        public float CompletePurchaseDuration { get; set; } = 1.5f;

        /// <summary>Seconds a customer spends "leaving" (walking away) before becoming Done.
        /// Aligned with the visual exit move so the domain finishes roughly as the visual despawns.</summary>
        public float LeaveDuration { get; set; } = 3f;

        /// <summary>Minimum seconds a spawned customer spends walking away when leaving.</summary>
        public float MinLeaveDuration { get; set; } = 3f;

        /// <summary>Maximum seconds a spawned customer spends walking away when leaving.</summary>
        public float MaxLeaveDuration { get; set; } = 6f;

        /// <summary>Seconds between sequential customer spawns ("one by one").</summary>
        public float SpawnInterval { get; set; } = 5.0f;

        /// <summary>Minimum customers per day in the stub spawner.</summary>
        public int BaseCustomers { get; set; } = 6;
    }
}
