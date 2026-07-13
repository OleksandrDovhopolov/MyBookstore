using System;
using System.Collections.Generic;
using Book.Sell.API;
using Game.Configs;
using Game.Configs.Models;
using Game.DayCycle.Results.Domain;

namespace Game.DayCycle.Results.Services
{
    public sealed class ResultsSummaryBuilder : IResultsSummaryBuilder
    {
        private readonly IResultsReviewTextProvider _reviewProvider;
        private readonly IConfigsService _configs;

        public ResultsSummaryBuilder(IResultsReviewTextProvider reviewProvider, IConfigsService configs = null)
        {
            _reviewProvider = reviewProvider ?? throw new ArgumentNullException(nameof(reviewProvider));
            _configs = configs;
        }

        public ResultsSummary Build(SalesDayResult sales)
        {
            if (sales == null) throw new ArgumentNullException(nameof(sales));

            return new ResultsSummary
            {
                Day = sales.Day,
                SalesCount = sales.SalesCount,
                GoldEarned = sales.GoldEarned,
                ExcellentCount = sales.ExcellentCount,
                NormalCount = sales.NormalCount,
                FailedCount = sales.FailedCount,
                SkippedCount = sales.SkippedCount,
                SoldByGenre = BuildSoldByGenre(sales),
                ReviewText = _reviewProvider.Pick(sales),
                GoldDelta = 0,
                ReputationDelta = 0,
                AlreadyApplied = false
            };
        }

        private Dictionary<string, int> BuildSoldByGenre(SalesDayResult sales)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (sales?.SoldBookIds == null || _configs == null)
                return BookGenreCounts.Normalize(counts);

            foreach (var bookId in sales.SoldBookIds)
            {
                if (string.IsNullOrWhiteSpace(bookId)) continue;
                if (!_configs.TryGet<BookConfig>(bookId, out var book)
                    || book == null
                    || !BookGenreExtensions.TryParseGenre(book.PrimaryGenre, out var genre))
                {
                    continue;
                }

                var genreId = genre.ToConfigValue();
                counts.TryGetValue(genreId, out var current);
                counts[genreId] = current + 1;
            }

            return BookGenreCounts.Normalize(counts);
        }
    }
}
