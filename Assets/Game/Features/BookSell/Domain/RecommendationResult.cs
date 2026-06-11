namespace Book.Sell.Domain
{
    /// <summary>
    /// Result of one active customer interaction: which book the player handed over
    /// (or null if they skipped), the score tier, and how much gold was earned. Scoring
    /// internals live in <see cref="Breakdown"/> and <see cref="Reason"/>.
    /// </summary>
    public sealed class RecommendationResult
    {
        public string RequestId { get; }

        /// <summary>Book id, or null when the player chose to skip the request.</summary>
        public string BookId { get; }

        public RecommendationTier Tier { get; }
        public ScoreBreakdown Breakdown { get; }
        public RecommendationReason Reason { get; }
        public int GoldEarned { get; }

        public RecommendationResult(
            string requestId,
            string bookId,
            RecommendationTier tier,
            ScoreBreakdown breakdown,
            RecommendationReason reason,
            int goldEarned)
        {
            RequestId = requestId;
            BookId = bookId;
            Tier = tier;
            Breakdown = breakdown;
            Reason = reason;
            GoldEarned = goldEarned;
        }

        public static RecommendationResult Skipped(string requestId)
            => new(requestId, null, RecommendationTier.Skipped, default, RecommendationReason.Empty, 0);
    }
}
