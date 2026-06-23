using Book.Sell.API;

namespace Game.DayCycle.Results.Services
{
    public interface IResultsRewardService
    {
        RewardComputation Compute(SalesDayResult sales, int currentReputation);
    }

    /// <summary>Computed reward deltas + the best recommendation to feature on the screen.</summary>
    public readonly struct RewardComputation
    {
        public int GoldDelta { get; }
        public int ReputationDelta { get; }

        public RewardComputation(int goldDelta, int reputationDelta)
        {
            GoldDelta = goldDelta;
            ReputationDelta = reputationDelta;
        }
    }
}
