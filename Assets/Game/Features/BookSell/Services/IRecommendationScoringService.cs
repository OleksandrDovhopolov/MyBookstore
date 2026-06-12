using Book.Sell.API;
using Game.Configs.Models;

namespace Book.Sell.Services
{
    /// <summary>
    /// Pure scoring logic: BookConfig × RequestConfig × LocationConfig → RecommendationResult.
    /// No Unity dependencies — fully covered by EditMode tests.
    /// </summary>
    public interface IRecommendationScoringService
    {
        RecommendationResult Score(BookConfig book, RequestConfig request, LocationConfig location);
    }
}
