using System;
using System.Collections.Generic;
using Book.Sell.Domain;
using Game.Configs.Models;

namespace Book.Sell.Services
{
    /// <inheritdoc cref="IRecommendationScoringService"/>
    public sealed class RecommendationScoringService : IRecommendationScoringService
    {
        public const int GenreMatchPoints = 3;
        public const int TagMatchPoints = 2;
        public const int MoodMatchPoints = 1;
        public const int PriceMatchPoints = 1;
        public const int LocationBonusPoints = 1;   // capped at +1 even if both genre and tag match the location

        public RecommendationResult Score(BookConfig book, RequestConfig request, LocationConfig location)
        {
            if (book == null) throw new ArgumentNullException(nameof(book));
            if (request == null) throw new ArgumentNullException(nameof(request));

            var matchedGenres = MatchOne(request.DesiredGenres, book.Genre);
            var matchedTags = MatchMany(request.DesiredTags, book.Tags);
            var matchedMood = MatchMany(request.DesiredMood, book.Mood);

            var genrePts = matchedGenres.Count > 0 ? GenreMatchPoints : 0;
            var tagPts = matchedTags.Count * TagMatchPoints;
            var moodPts = matchedMood.Count * MoodMatchPoints;

            var priceFits = request.MaxPrice > 0 && book.BasePrice <= request.MaxPrice;
            var pricePts = priceFits ? PriceMatchPoints : 0;

            var locationBonus = HasLocationBonus(book, location);
            var locationPts = locationBonus ? LocationBonusPoints : 0;

            var breakdown = new ScoreBreakdown(genrePts, tagPts, moodPts, pricePts, locationPts);
            var tier = ClassifyTier(breakdown.Total);
            var gold = CalculateGold(tier, book, request);

            var reason = new RecommendationReason(matchedGenres, matchedTags, matchedMood, priceFits, locationBonus);
            return new RecommendationResult(request.Id, book.Id, tier, breakdown, reason, gold);
        }

        private static RecommendationTier ClassifyTier(int total)
        {
            if (total >= 6) return RecommendationTier.Excellent;
            if (total >= 3) return RecommendationTier.Normal;
            return RecommendationTier.Failed;
        }

        private static int CalculateGold(RecommendationTier tier, BookConfig book, RequestConfig request)
        {
            switch (tier)
            {
                case RecommendationTier.Excellent: return book.BasePrice + request.BaseRewardGold;
                case RecommendationTier.Normal: return book.BasePrice;
                default: return 0;  // Failed, Skipped — no reward
            }
        }

        private static List<string> MatchOne(string[] desired, string actualValue)
        {
            if (desired == null || string.IsNullOrEmpty(actualValue)) return EmptyList;
            for (var i = 0; i < desired.Length; i++)
            {
                if (string.Equals(desired[i], actualValue, StringComparison.OrdinalIgnoreCase))
                    return new List<string> { actualValue };
            }
            return EmptyList;
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

            if (location.DemandGenres != null && !string.IsNullOrEmpty(book.Genre))
            {
                foreach (var g in location.DemandGenres)
                {
                    if (string.Equals(g, book.Genre, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            if (location.DemandTags != null && book.Tags != null)
            {
                foreach (var lt in location.DemandTags)
                {
                    if (string.IsNullOrEmpty(lt)) continue;
                    foreach (var bt in book.Tags)
                    {
                        if (string.IsNullOrEmpty(bt)) continue;
                        if (string.Equals(lt, bt, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }

            return false;
        }

        private static readonly List<string> EmptyList = new(0);
    }
}
