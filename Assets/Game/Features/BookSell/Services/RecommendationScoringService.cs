using System;
using System.Collections.Generic;
using Book.Sell.API;
using Game.Configs.Models;

namespace Book.Sell.Services
{
    /// <inheritdoc cref="IRecommendationScoringService"/>
    public sealed class RecommendationScoringService : IRecommendationScoringService, IActiveRequestScoringService
    {
        public const int GenreMatchPoints = 3;
        public const int QualityMatchPoints = 2;
        public const int PriceMatchPoints = 1;
        public const int LocationBonusPoints = 1;   // capped at +1 even if both genre and quality match the location

        public RecommendationResult Score(BookConfig book, RequestConfig request, LocationConfig location)
        {
            if (book == null) throw new ArgumentNullException(nameof(book));
            if (request == null) throw new ArgumentNullException(nameof(request));

            var matchedGenres = MatchMany(request.DesiredGenres, book.Genres);
            var matchedQualities = MatchMany(request.DesiredQualities, book.Qualities);

            var genrePts = matchedGenres.Count > 0 ? GenreMatchPoints : 0;
            var qualityPts = matchedQualities.Count * QualityMatchPoints;

            var priceFits = request.MaxPrice > 0 && BookConfig.FixedPriceGold <= request.MaxPrice;
            var pricePts = priceFits ? PriceMatchPoints : 0;

            var locationBonus = HasLocationBonus(book, location);
            var locationPts = locationBonus ? LocationBonusPoints : 0;

            var breakdown = new ScoreBreakdown(genrePts, qualityPts, pricePts, locationPts);
            var tier = ClassifyTier(breakdown.Total);
            var gold = CalculateGold(tier, request);

            var reason = new RecommendationReason(matchedGenres, matchedQualities, priceFits, locationBonus);
            return new RecommendationResult(request.Id, book.Id, tier, breakdown, reason, gold);
        }

        public RecommendationResult Score(BookConfig book, Domain.ActiveRequestRuntime request, LocationConfig location)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.LegacyRequest == null)
                throw new InvalidOperationException("RecommendationScoringService can only score legacy active requests.");

            return Score(book, request.LegacyRequest, location);
        }

        private static RecommendationTier ClassifyTier(int total)
        {
            if (total >= 6) return RecommendationTier.Excellent;
            if (total >= 3) return RecommendationTier.Normal;
            return RecommendationTier.Failed;
        }

        private static int CalculateGold(RecommendationTier tier, RequestConfig request)
        {
            switch (tier)
            {
                case RecommendationTier.Excellent: return BookConfig.FixedPriceGold + request.BaseRewardGold;
                case RecommendationTier.Normal: return BookConfig.FixedPriceGold;
                default: return 0;  // Failed, Skipped — no reward
            }
        }

        private static List<string> MatchMany(string[] desired, string[] actual)
        {
            if (desired == null || desired.Length == 0 || actual == null || actual.Length == 0)
                return EmptyList;

            var matched = new List<string>();
            foreach (var d in desired)
            {
                if (string.IsNullOrEmpty(d)) continue;
                foreach (var a in actual)
                {
                    if (string.IsNullOrEmpty(a)) continue;
                    if (string.Equals(d, a, StringComparison.OrdinalIgnoreCase))
                    {
                        matched.Add(a);
                        break;
                    }
                }
            }
            return matched;
        }

        private static bool HasLocationBonus(BookConfig book, LocationConfig location)
        {
            if (location == null) return false;

            var primaryGenre = book.PrimaryGenre;
            if (location.DemandGenres != null && !string.IsNullOrEmpty(primaryGenre))
            {
                foreach (var g in location.DemandGenres)
                {
                    if (string.Equals(g, primaryGenre, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            if (location.DemandQualities != null && book.Qualities != null)
            {
                foreach (var lq in location.DemandQualities)
                {
                    if (string.IsNullOrEmpty(lq)) continue;
                    foreach (var bq in book.Qualities)
                    {
                        if (string.IsNullOrEmpty(bq)) continue;
                        if (string.Equals(lq, bq, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }

            return false;
        }

        private static readonly List<string> EmptyList = new(0);
    }
}
