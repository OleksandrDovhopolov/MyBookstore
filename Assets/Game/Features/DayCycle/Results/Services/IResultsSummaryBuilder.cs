using Book.Sell.API;
using Game.DayCycle.Results.Domain;

namespace Game.DayCycle.Results.Services
{
    public interface IResultsSummaryBuilder
    {
        ResultsSummary Build(SalesDayResult sales);
    }
}
