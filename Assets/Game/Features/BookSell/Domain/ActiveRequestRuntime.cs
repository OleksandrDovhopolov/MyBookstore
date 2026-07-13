using Game.Configs.Models;

namespace Book.Sell.Domain
{
    /// <summary>
    /// Runtime shape consumed by the active recommendation flow. It keeps the sales day and UI independent
    /// from the authored config type while preserving a typed payload for the condition evaluator.
    /// </summary>
    public sealed class ActiveRequestRuntime
    {
        private ActiveRequestRuntime(
            string id,
            string text,
            RequestDifficulty difficulty,
            RequestDefinitionConfig conditionRequest)
        {
            Id = id;
            Text = text ?? string.Empty;
            Difficulty = difficulty;
            ConditionRequest = conditionRequest;
        }

        public string Id { get; }
        public string Text { get; }
        public RequestDifficulty Difficulty { get; }
        public RequestDefinitionConfig ConditionRequest { get; }

        public static ActiveRequestRuntime FromCondition(RequestDefinitionConfig request, string debugText)
        {
            if (request == null) return null;
            return new ActiveRequestRuntime(
                request.Id,
                debugText,
                RequestDifficulty.Unknown,
                request);
        }
    }
}
