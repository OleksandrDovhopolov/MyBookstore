using Book.Sell.API;

namespace Game.DayCycle.Results.Services
{
    /// <summary>Picks a short cozy review line for the day, based on the tier mix.</summary>
    public interface IResultsReviewTextProvider
    {
        string Pick(SalesDayResult sales);
    }
}
