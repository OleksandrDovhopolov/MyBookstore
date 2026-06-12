namespace Book.Sell.Domain
{
    /// <summary>
    /// Timing knobs for the sales simulation. Constants for the MVP; later sourced from config.
    /// Tests override these for fast, deterministic ticking.
    /// </summary>
    public sealed class SalesTuning
    {
        /// <summary>Seconds a customer spends walking up before they can be interacted with.</summary>
        public float ApproachDuration { get; set; } = 1.5f;

        /// <summary>Seconds a customer "thinks" before each passive purchase attempt.</summary>
        public float BrowseDuration { get; set; } = 1.2f;

        /// <summary>Seconds between targeting a book (reserve) and committing the passive sale.</summary>
        public float PassiveCommitDelay { get; set; } = 0.6f;

        /// <summary>Seconds between sequential customer spawns ("one by one").</summary>
        public float SpawnInterval { get; set; } = 2.0f;

        /// <summary>Minimum customers per day in the stub spawner.</summary>
        public int BaseCustomers { get; set; } = 6;
    }
}
