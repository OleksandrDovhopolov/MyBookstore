namespace Book.Sell.Domain
{
    /// <summary>
    /// Итог одной активной интеракции с клиентом: какую книгу выбрал (или null если Skip),
    /// какой tier, сколько gold принёс. Скоринговые детали — в <see cref="Breakdown"/> и <see cref="Reason"/>.
    /// </summary>
    public sealed class RecommendationResult
    {
        public string RequestId { get; }

        /// <summary>id книги или null, если игрок выбрал Skip.</summary>
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
