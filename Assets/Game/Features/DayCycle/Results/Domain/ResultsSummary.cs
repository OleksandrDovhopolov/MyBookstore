using Book.Sell.API;

namespace Game.DayCycle.Results.Domain
{
    /// <summary>
    /// Read-only snapshot the Results screen renders. Built once per day from the persisted
    /// <see cref="SalesDayResult"/>; numbers come straight from sales.
    /// </summary>
    public sealed class ResultsSummary
    {
        public int Day { get; set; }

        // Numbers (mirror SalesDayResult, never recomputed in UI).
        public int SalesCount { get; set; }
        public int GoldEarned { get; set; }
        public int ExcellentCount { get; set; }
        public int NormalCount { get; set; }
        public int FailedCount { get; set; }
        public int SkippedCount { get; set; }

        // Cozy one-line review of the day.
        public string ReviewText { get; set; }

        // Results no longer applies rewards. These stay for the existing UI contract.
        public int GoldDelta { get; set; }
        public int ReputationDelta { get; set; }

        /// <summary>True when the summary was loaded from already-applied rewards (no balance change).</summary>
        public bool AlreadyApplied { get; set; }
    }

    /// <summary>The single recommendation we surface on the results screen.</summary>
    public sealed class BestMatchCard
    {
        public string BookId { get; set; }
        public string RequestId { get; set; }
        public RecommendationTier Tier { get; set; }
        public RecommendationReason Reason { get; set; }
        public int Score { get; set; }
    }
}
