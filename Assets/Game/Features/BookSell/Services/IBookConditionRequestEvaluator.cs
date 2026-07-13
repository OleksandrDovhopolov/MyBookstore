using Game.Configs.Models;

namespace Book.Sell.Services
{
    public interface IBookConditionRequestEvaluator
    {
        BookConditionEvaluation Evaluate(BookConfig book, RequestDefinitionConfig request);
        bool IsValid(RequestDefinitionConfig request, out string reason);
        string BuildDebugText(RequestDefinitionConfig request);
    }
}
