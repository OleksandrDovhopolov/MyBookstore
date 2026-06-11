using System;
using System.Collections.Generic;

namespace Book.Sell.Domain
{
    /// <summary>
    /// «Человеческое объяснение» результата рекомендации: какие именно жанры/теги/тон
    /// совпали + есть ли бонус локации + укладывается ли в бюджет.
    /// View собирает из этого читаемую фразу.
    /// </summary>
    public sealed class RecommendationReason
    {
        public IReadOnlyList<string> MatchedGenres { get; }
        public IReadOnlyList<string> MatchedTags { get; }
        public IReadOnlyList<string> MatchedMood { get; }
        public bool PriceFits { get; }
        public bool LocationBonus { get; }

        public RecommendationReason(
            IReadOnlyList<string> matchedGenres,
            IReadOnlyList<string> matchedTags,
            IReadOnlyList<string> matchedMood,
            bool priceFits,
            bool locationBonus)
        {
            MatchedGenres = matchedGenres ?? Array.Empty<string>();
            MatchedTags = matchedTags ?? Array.Empty<string>();
            MatchedMood = matchedMood ?? Array.Empty<string>();
            PriceFits = priceFits;
            LocationBonus = locationBonus;
        }

        public static RecommendationReason Empty { get; } =
            new(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), false, false);
    }
}
