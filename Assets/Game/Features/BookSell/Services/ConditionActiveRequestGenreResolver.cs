using System;
using System.Collections.Generic;
using Game.Configs.Models;
using Newtonsoft.Json.Linq;

namespace Book.Sell.Services
{
    public sealed class ConditionActiveRequestGenreResolver : IActiveRequestGenreResolver
    {
        private static readonly HashSet<string> PositiveGenreOperators = new(StringComparer.OrdinalIgnoreCase)
        {
            "equal",
            "contains",
            "containsAny",
            "containsAll"
        };

        public IReadOnlyList<string> Resolve(RequestDefinitionConfig request)
        {
            if (request?.Conditions == null) return Array.Empty<string>();

            var genres = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AppendGenres(request.Conditions.All, genres, seen);
            AppendGenres(request.Conditions.Any, genres, seen);

            return genres.Count > 0 ? genres.ToArray() : Array.Empty<string>();
        }

        private static void AppendGenres(
            IReadOnlyList<RequestCondition> conditions,
            List<string> genres,
            HashSet<string> seen)
        {
            if (conditions == null) return;

            for (var i = 0; i < conditions.Count; i++)
            {
                var condition = conditions[i];
                if (condition == null) continue;
                if (!string.Equals(condition.Type, "genres", StringComparison.OrdinalIgnoreCase)) continue;
                if (!PositiveGenreOperators.Contains(condition.Operator)) continue;

                AppendValue(condition.Value, genres, seen);
            }
        }

        private static void AppendValue(JToken value, List<string> genres, HashSet<string> seen)
        {
            if (value == null || value.Type == JTokenType.Null) return;

            if (value.Type == JTokenType.Array)
            {
                var values = value.ToObject<string[]>() ?? Array.Empty<string>();
                for (var i = 0; i < values.Length; i++)
                    Add(values[i], genres, seen);
                return;
            }

            Add(value.Type == JTokenType.String ? value.Value<string>() : value.ToString(), genres, seen);
        }

        private static void Add(string genre, List<string> genres, HashSet<string> seen)
        {
            if (string.IsNullOrWhiteSpace(genre)) return;
            if (seen.Add(genre)) genres.Add(genre);
        }
    }
}
