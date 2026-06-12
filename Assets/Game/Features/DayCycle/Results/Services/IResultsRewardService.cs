using Book.Sell.API;
using Game.DayCycle.Results.Domain;

namespace Game.DayCycle.Results.Services
{
    /// <summary>
    /// Pure conversion: <see cref="SalesDayResult"/> -> reward deltas + best-match card.
    /// No state, no Save, no Unity — fully covered by EditMode tests.
    /// </summary>
    public interface IResultsRewardService
    {
        /// <summary>
        /// Compute gold/reputation deltas and pick the best match for the summary card.
        /// Reputation is clamped so a (currentReputation + delta) cannot go below zero.
        /// </summary>
        RewardComputation Compute(SalesDayResult sales, int currentReputation);
    }

    /// <summary>Computed reward deltas + the best recommendation to feature on the screen.</summary>
    public readonly struct RewardComputation
    {
        public int GoldDelta { get; }
        public int ReputationDelta { get; }
        public BestMatchCard BestMatch { get; }

        public RewardComputation(int goldDelta, int reputationDelta, BestMatchCard bestMatch)
        {
            GoldDelta = goldDelta;
            ReputationDelta = reputationDelta;
            BestMatch = bestMatch;
        }
    }
}
