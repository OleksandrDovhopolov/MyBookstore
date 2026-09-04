using System;
using System.Collections.Generic;
using Game.Configs.Models;
using Game.Localization;

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
            RequestDefinitionConfig conditionRequest,
            IReadOnlyList<string> requiredGenres)
        {
            Id = id;
            Text = text ?? string.Empty;
            Difficulty = difficulty;
            ConditionRequest = conditionRequest;
            RequiredGenres = Copy(requiredGenres);
        }

        public string Id { get; }
        public string Text { get; }
        public RequestDifficulty Difficulty { get; }
        public RequestDefinitionConfig ConditionRequest { get; }
        public IReadOnlyList<string> RequiredGenres { get; }

        // Пока false: активный запрос показывает техническую debug-строку условий, а не человеческое
        // описание. DescriptionKey в sample_requests.json и тексты в localization_quests_en.json НАМЕРЕННО
        // сохранены — их подключит задача CONTENT-2 (Active Request Descriptions), где под каждый запрос
        // готовится реальный текст со своей логикой. Тогда флаг переключается в true.
        private static readonly bool UseLocalizedDescriptions = false;

        // debugText — текущий отображаемый текст запроса (техническая строка условий). См. флаг выше.
        public static ActiveRequestRuntime FromCondition(
            RequestDefinitionConfig request,
            string debugText,
            IReadOnlyList<string> requiredGenres = null,
            ILocalizationService localization = null)
        {
            if (request == null) return null;
            return new ActiveRequestRuntime(
                request.Id,
                ResolveText(request, debugText, localization),
                RequestDifficulty.Unknown,
                request,
                requiredGenres);
        }

        private static string ResolveText(
            RequestDefinitionConfig request,
            string debugText,
            ILocalizationService localization)
        {
            if (request == null) return string.Empty;
            if (UseLocalizedDescriptions && !string.IsNullOrWhiteSpace(request.DescriptionKey))
                return localization != null
                    ? localization.Get(request.DescriptionKey)
                    : LocalizationLocator.GetOrKey(request.DescriptionKey);
            return debugText;
        }

        public bool MatchesProfile(IReadOnlyList<string> desiredGenres)
        {
            if (RequiredGenres == null || RequiredGenres.Count == 0) return true;
            if (desiredGenres == null || desiredGenres.Count == 0) return false;

            for (var i = 0; i < RequiredGenres.Count; i++)
            {
                var required = RequiredGenres[i];
                if (string.IsNullOrWhiteSpace(required)) continue;

                for (var j = 0; j < desiredGenres.Count; j++)
                {
                    if (string.Equals(required, desiredGenres[j], StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        private static IReadOnlyList<string> Copy(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0) return Array.Empty<string>();

            var copy = new List<string>(values.Count);
            for (var i = 0; i < values.Count; i++)
            {
                var value = values[i];
                if (!string.IsNullOrWhiteSpace(value))
                    copy.Add(value);
            }

            return copy.Count > 0 ? copy.ToArray() : Array.Empty<string>();
        }
    }
}
