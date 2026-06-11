namespace Book.Sell.Domain
{
    /// <summary>
    /// Outcome category for a recommendation. Scoring produces three tiers by score
    /// (0-2 Failed / 3-5 Normal / 6+ Excellent). Skip is a fourth, non-scoring tier:
    /// the player honestly declined to recommend anything. NOT the same as Failed.
    /// </summary>
    public enum RecommendationTier
    {
        Failed = 0,
        Normal = 1,
        Excellent = 2,
        Skipped = 3
    }
}
