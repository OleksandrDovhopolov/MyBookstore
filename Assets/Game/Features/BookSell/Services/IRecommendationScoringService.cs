using Book.Sell.Domain;
using Game.Configs.Models;

namespace Book.Sell.Services
{
    /// <summary>
    /// Чистая логика: BookConfig × RequestConfig × LocationConfig → RecommendationResult.
    /// Без Unity-зависимостей, легко покрывается EditMode-тестами.
    /// </summary>
    public interface IRecommendationScoringService
    {
        RecommendationResult Score(BookConfig book, RequestConfig request, LocationConfig location);
    }
}
