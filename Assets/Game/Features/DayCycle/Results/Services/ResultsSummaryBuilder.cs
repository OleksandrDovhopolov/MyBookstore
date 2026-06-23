using System;
using Book.Sell.API;
using Game.DayCycle.Results.Domain;

namespace Game.DayCycle.Results.Services
{
    public sealed class ResultsSummaryBuilder : IResultsSummaryBuilder
    {
        private readonly IResultsReviewTextProvider _reviewProvider;

        public ResultsSummaryBuilder(IResultsReviewTextProvider reviewProvider)
        {
            _reviewProvider = reviewProvider ?? throw new ArgumentNullException(nameof(reviewProvider));
        }

        public ResultsSummary Build(SalesDayResult sales)
        {
            if (sales == null) throw new ArgumentNullException(nameof(sales));

            return new ResultsSummary
            {
                Day = sales.Day,
                SalesCount = sales.SalesCount,
                GoldEarned = sales.GoldEarned,
                ExcellentCount = sales.ExcellentCount,
                NormalCount = sales.NormalCount,
                FailedCount = sales.FailedCount,
                SkippedCount = sales.SkippedCount,
                ReviewText = _reviewProvider.Pick(sales),
                GoldDelta = 0,
                ReputationDelta = 0,
                AlreadyApplied = false
            };
        }
    }
}
