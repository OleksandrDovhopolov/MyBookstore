using System;
using System.Collections.Generic;

namespace Book.Sell.API
{
    /// <summary>
    /// Human-readable breakdown of why a recommendation got its score: which genres / qualities
    /// actually matched + whether the price fits the customer's budget + whether the
    /// current location amplified the genre or any quality. View builds a readable sentence from this.
    /// </summary>
    public sealed class RecommendationReason
    {
        public IReadOnlyList<string> MatchedGenres { get; }
        public IReadOnlyList<string> MatchedQualities { get; }
        public bool PriceFits { get; }
        public bool LocationBonus { get; }

        public RecommendationReason(
            IReadOnlyList<string> matchedGenres,
            IReadOnlyList<string> matchedQualities,
            bool priceFits,
            bool locationBonus)
        {
            MatchedGenres = matchedGenres ?? Array.Empty<string>();
            MatchedQualities = matchedQualities ?? Array.Empty<string>();
            PriceFits = priceFits;
            LocationBonus = locationBonus;
        }

        public static RecommendationReason Empty { get; } =
            new(Array.Empty<string>(), Array.Empty<string>(), false, false);
    }
}
