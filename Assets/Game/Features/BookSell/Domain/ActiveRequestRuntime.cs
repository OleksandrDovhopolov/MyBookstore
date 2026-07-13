using System;
using Game.Configs.Models;

namespace Book.Sell.Domain
{
    public enum ActiveRequestSourceKind
    {
        LegacyScoring = 0,
        Conditions = 1
    }

    /// <summary>
    /// Runtime shape consumed by the active recommendation flow. It keeps the sales day and UI independent
    /// from the authored config type while preserving a typed payload for the selected scoring strategy.
    /// </summary>
    public sealed class ActiveRequestRuntime
    {
        private ActiveRequestRuntime(
            string id,
            string text,
            RequestDifficulty difficulty,
            ActiveRequestSourceKind sourceKind,
            RequestConfig legacyRequest,
            RequestDefinitionConfig conditionRequest)
        {
            Id = id;
            Text = text ?? string.Empty;
            Difficulty = difficulty;
            SourceKind = sourceKind;
            LegacyRequest = legacyRequest;
            ConditionRequest = conditionRequest;
        }

        public string Id { get; }
        public string Text { get; }
        public RequestDifficulty Difficulty { get; }
        public ActiveRequestSourceKind SourceKind { get; }
        public RequestConfig LegacyRequest { get; }
        public RequestDefinitionConfig ConditionRequest { get; }

        public static ActiveRequestRuntime FromLegacy(RequestConfig request)
        {
            if (request == null) return null;
            return new ActiveRequestRuntime(
                request.Id,
                request.Text,
                request.Difficulty,
                ActiveRequestSourceKind.LegacyScoring,
                request,
                null);
        }

        public static ActiveRequestRuntime FromCondition(RequestDefinitionConfig request, string debugText)
        {
            if (request == null) return null;
            return new ActiveRequestRuntime(
                request.Id,
                debugText,
                RequestDifficulty.Unknown,
                ActiveRequestSourceKind.Conditions,
                null,
                request);
        }
    }
}
