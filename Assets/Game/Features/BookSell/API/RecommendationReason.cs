using System;
using System.Collections.Generic;

namespace Book.Sell.API
{
    /// <summary>
    /// Human-readable breakdown of why a recommendation got its score: which genres / tags /
    /// mood actually matched + whether the price fits the customer's budget + whether the
    /// current location amplified the genre or any tag. View builds a readable sentence from this.
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
