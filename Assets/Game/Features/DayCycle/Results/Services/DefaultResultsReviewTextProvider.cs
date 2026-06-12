using Book.Sell.API;

namespace Game.DayCycle.Results.Services
{
    /// <inheritdoc cref="IResultsReviewTextProvider"/>
    public sealed class DefaultResultsReviewTextProvider : IResultsReviewTextProvider
    {
        public string Pick(SalesDayResult sales)
        {
            // No sales at all — neutral encouragement.
            if (sales.SalesCount == 0)
                return "A quiet day for the cart. Tomorrow brings new readers.";

            var positive = sales.ExcellentCount + sales.NormalCount;
            var negative = sales.FailedCount;

            if (sales.ExcellentCount > 0 && negative == 0)
                return "Every reader walked away with the right book today.";

            if (sales.ExcellentCount >= positive)
                return "A day full of perfect matches.";

            if (negative > positive)
                return "A tough day — but you held the shop together.";

            return "A cozy day at the book cart.";
        }
    }
}
