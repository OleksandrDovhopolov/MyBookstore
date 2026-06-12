using Book.Sell.API;
using Game.DayCycle.Results.Domain;

namespace Game.DayCycle.Results.Services
{
    /// <inheritdoc cref="IResultsRewardService"/>
    public sealed class DefaultResultsRewardService : IResultsRewardService
    {
        public const int FailedReputationPenaltyThreshold = 2;   // failed > N -> -1 reputation

        public RewardComputation Compute(SalesDayResult sales, int currentReputation)
        {
            // Gold: trust the sales numbers verbatim, never recompute from UI.
            var gold = sales.GoldEarned;

            // Reputation: +1 per excellent; penalty of -1 if too many failed; never below 0 after delta.
            var rep = sales.ExcellentCount;
            if (sales.FailedCount > FailedReputationPenaltyThreshold) rep -= 1;
            if (currentReputation + rep < 0) rep = -currentReputation;

            return new RewardComputation(gold, rep, PickBestMatch(sales));
        }

        private static BestMatchCard PickBestMatch(SalesDayResult sales)
        {
            if (sales.Recommendations == null || sales.Recommendations.Count == 0)
                return null;

            RecommendationResult best = null;
            for (var i = 0; i < sales.Recommendations.Count; i++)
            {
                var r = sales.Recommendations[i];
                if (best == null) { best = r; continue; }

                // Skipped never wins.
                if (r.Tier == RecommendationTier.Skipped) continue;
                if (best.Tier == RecommendationTier.Skipped) { best = r; continue; }

                if (r.Breakdown.Total > best.Breakdown.Total) { best = r; continue; }
                if (r.Breakdown.Total == best.Breakdown.Total && TierRank(r.Tier) > TierRank(best.Tier))
                    best = r;
                // ties: keep the earlier one (already in best).
            }

            if (best == null || best.Tier == RecommendationTier.Skipped) return null;

            return new BestMatchCard
            {
                BookId = best.BookId,
                RequestId = best.RequestId,
                Tier = best.Tier,
                Reason = best.Reason,
                Score = best.Breakdown.Total
            };
        }

        private static int TierRank(RecommendationTier tier) => tier switch
        {
            RecommendationTier.Excellent => 3,
            RecommendationTier.Normal => 2,
            RecommendationTier.Failed => 1,
            _ => 0
        };
    }
}
