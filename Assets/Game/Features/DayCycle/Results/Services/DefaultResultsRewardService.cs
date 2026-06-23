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

            return new RewardComputation(gold, rep);
        }
    }
}
