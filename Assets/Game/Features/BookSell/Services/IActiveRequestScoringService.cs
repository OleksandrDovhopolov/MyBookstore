using Book.Sell.API;
using Book.Sell.Domain;
using Game.Configs.Models;

namespace Book.Sell.Services
{
    public interface IActiveRequestScoringService
    {
        RecommendationResult Score(BookConfig book, ActiveRequestRuntime request, LocationConfig location);
    }
}
